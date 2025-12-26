"""
SolidWorks Command Parser
Parses AI responses into executable SolidWorks API commands
"""

import json
import re
from typing import Dict, List, Optional

class SolidWorksCommandParser:
    """Parses natural language AI responses into structured SolidWorks commands"""
    
    def __init__(self):
        self.supported_actions = [
            "create_feature",
            "create_part",
            "create",
            "add",
            "modify_feature", 
            "modify",
            "edit",
            "analyze",
            "review",
            "delete",
            "remove"
        ]
        
        self.supported_types = [
            "box", "rectangle", "rectangular", "block", "cube",
            "cylinder", "cylindrical", "rod", "circle",
            "hole", "simple_hole",
            "threaded_hole", "tapped_hole",
            "counterbore", "counterbore_hole",
            "countersink", "countersink_hole",
            "fillet", "round",
            "chamfer",
            "extrusion", "extrude", "boss",
            "cut", "cut_extrude", "pocket",
            "pattern", "linear_pattern",
            "circular_pattern",
            "shell",
            "dimension", "size", "length",
            "scale"
        ]
        
        # Standard metric thread tap drill sizes
        self.thread_sizes = {
            "M2": 1.6, "M2.5": 2.05, "M3": 2.5, "M4": 3.3,
            "M5": 4.2, "M6": 5.0, "M8": 6.8, "M10": 8.5,
            "M12": 10.2, "M14": 12.0, "M16": 14.0, "M20": 17.5
        }
    
    def parse_response(self, ai_response: str) -> Dict:
        """
        Parse AI response and extract SolidWorks commands
        
        Args:
            ai_response: The AI's natural language response
            
        Returns:
            Dictionary with parsed commands and metadata
        """
        try:
            # Try to extract JSON from the response (handle multiple JSON blocks)
            json_blocks = self._extract_all_json(ai_response)
            
            valid_commands = []
            for json_str in json_blocks:
                try:
                    command = json.loads(json_str)
                    
                    # Validate command structure
                    if self._validate_command(command):
                        valid_commands.append(command)
                except json.JSONDecodeError:
                    continue
            
            # If we found valid commands
            if valid_commands:
                # Return first command for backward compatibility
                # but also include all commands for sequential execution
                return {
                    "success": True,
                    "command": valid_commands[0],
                    "commands": valid_commands,  # All commands for sequential execution
                    "command_count": len(valid_commands),
                    "description": valid_commands[0].get("description", ""),
                    "raw_response": ai_response
                }
            
            # If no valid JSON found, try to infer command from text
            inferred = self._infer_command(ai_response)
            if inferred:
                return {
                    "success": True,
                    "command": inferred,
                    "commands": [inferred],
                    "command_count": 1,
                    "description": ai_response,
                    "raw_response": ai_response,
                    "inferred": True
                }
            
            # No command found
            return {
                "success": False,
                "command": None,
                "commands": [],
                "command_count": 0,
                "description": ai_response,
                "raw_response": ai_response,
                "error": "Could not parse command from response"
            }
            
        except Exception as e:
            return {
                "success": False,
                "command": None,
                "commands": [],
                "command_count": 0,
                "description": ai_response,
                "raw_response": ai_response,
                "error": f"Parsing error: {str(e)}"
            }
    
    def _extract_all_json(self, text: str) -> List[str]:
        """Extract all JSON blocks from text"""
        json_blocks = []
        
        # Pattern 1: JSON in code blocks
        code_block_pattern = r'```(?:json)?\s*(\{[^`]*?\})\s*```'
        matches = re.findall(code_block_pattern, text, re.DOTALL)
        json_blocks.extend(matches)
        
        # Pattern 2: Standalone JSON objects (more careful matching)
        # Match balanced braces
        brace_count = 0
        start_idx = None
        
        for i, char in enumerate(text):
            if char == '{':
                if brace_count == 0:
                    start_idx = i
                brace_count += 1
            elif char == '}':
                brace_count -= 1
                if brace_count == 0 and start_idx is not None:
                    json_str = text[start_idx:i+1]
                    if json_str not in json_blocks:
                        json_blocks.append(json_str)
                    start_idx = None
        
        return json_blocks
    
    def _validate_command(self, command: Dict) -> bool:
        """Validate that a command has the required structure"""
        if not isinstance(command, dict):
            return False
        
        # Must have an action
        action = command.get("action", "").lower()
        if not action:
            return False
        
        # Check if action is supported or close enough
        if action not in self.supported_actions:
            # Check for partial matches
            for supported in self.supported_actions:
                if supported in action or action in supported:
                    return True
            return False
        
        return True
    
    def _infer_command(self, text: str) -> Optional[Dict]:
        """
        Try to infer a command from natural language text
        """
        text_lower = text.lower()
        
        # Check for threaded hole patterns
        thread_match = re.search(r'\b(m\d+(?:\.\d+)?)\b', text_lower)
        if thread_match and any(word in text_lower for word in ["hole", "threaded", "tap", "thread"]):
            thread_size = thread_match.group(1).upper()
            count = self._extract_count(text)
            depth = self._extract_single_dimension(text, "depth") or 15
            
            return {
                "action": "create",
                "type": "threaded_hole",
                "parameters": {
                    "thread_size": thread_size,
                    "depth": depth,
                    "count": count,
                    "units": "mm"
                },
                "description": f"Create {count}x {thread_size} threaded hole(s)"
            }
        
        # Check for dimension modification (make longer/shorter/wider/etc.)
        if any(word in text_lower for word in ["longer", "shorter", "wider", "narrower", "thicker", "thinner", "taller", "higher", "deeper"]):
            delta = self._extract_single_dimension(text, "")
            if delta:
                # Determine if positive or negative
                if any(word in text_lower for word in ["shorter", "narrower", "thinner", "smaller"]):
                    delta = -abs(delta)
                else:
                    delta = abs(delta)
                
                return {
                    "action": "modify",
                    "type": "dimension",
                    "parameters": {
                        "delta": delta,
                        "units": "mm"
                    },
                    "description": f"Modify dimension by {delta:+.1f}mm"
                }
        
        # Check for box/rectangle creation
        if any(word in text_lower for word in ["box", "cube", "rectangular", "block", "plate"]):
            dimensions = self._extract_dimensions(text)
            if not dimensions:
                dimensions = {"width": 100, "height": 100, "depth": 50, "units": "mm"}
            return {
                "action": "create",
                "type": "box",
                "parameters": dimensions,
                "description": f"Create box"
            }
        
        # Check for cylinder creation
        if any(word in text_lower for word in ["cylinder", "rod", "tube", "pipe", "round"]):
            dimensions = self._extract_cylinder_dimensions(text)
            return {
                "action": "create",
                "type": "cylinder",
                "parameters": dimensions,
                "description": "Create cylinder"
            }
        
        # Check for hole creation
        if any(word in text_lower for word in ["hole", "drill", "bore"]) and not thread_match:
            diameter = self._extract_single_dimension(text, "diameter") or 10
            depth = self._extract_single_dimension(text, "depth") or 20
            through = "through" in text_lower
            
            return {
                "action": "create",
                "type": "hole",
                "parameters": {
                    "diameter": diameter,
                    "depth": depth,
                    "through_all": through,
                    "units": "mm"
                },
                "description": f"Create Ø{diameter}mm hole"
            }
        
        # Check for fillet
        if any(word in text_lower for word in ["fillet", "round", "radius"]) and "corner" not in text_lower:
            radius = self._extract_single_dimension(text, "radius") or self._extract_single_dimension(text, "") or 5
            return {
                "action": "create",
                "type": "fillet",
                "parameters": {
                    "radius": radius,
                    "units": "mm"
                },
                "description": f"Create R{radius}mm fillet"
            }
        
        # Check for chamfer
        if "chamfer" in text_lower:
            distance = self._extract_single_dimension(text, "distance") or self._extract_single_dimension(text, "") or 2
            return {
                "action": "create",
                "type": "chamfer",
                "parameters": {
                    "distance": distance,
                    "angle": 45,
                    "units": "mm"
                },
                "description": f"Create {distance}mm chamfer"
            }
        
        # Check for shell
        if "shell" in text_lower or "hollow" in text_lower:
            thickness = self._extract_single_dimension(text, "thickness") or self._extract_single_dimension(text, "wall") or 2
            return {
                "action": "create",
                "type": "shell",
                "parameters": {
                    "thickness": thickness,
                    "units": "mm"
                },
                "description": f"Create shell with {thickness}mm wall"
            }
        
        # Check for extrusion
        if any(word in text_lower for word in ["extrude", "extrusion", "boss"]):
            depth = self._extract_single_dimension(text, "depth") or self._extract_single_dimension(text, "") or 25
            return {
                "action": "create",
                "type": "extrusion",
                "parameters": {
                    "depth": depth,
                    "units": "mm"
                },
                "description": f"Create extrusion {depth}mm"
            }
        
        # Check for cut
        if any(word in text_lower for word in ["cut", "pocket", "remove"]):
            depth = self._extract_single_dimension(text, "depth") or self._extract_single_dimension(text, "") or 10
            through = "through" in text_lower
            return {
                "action": "create",
                "type": "cut",
                "parameters": {
                    "depth": depth,
                    "through_all": through,
                    "units": "mm"
                },
                "description": f"Create cut"
            }
        
        # Check for pattern
        if "pattern" in text_lower:
            count = self._extract_count(text)
            spacing = self._extract_single_dimension(text, "spacing") or 20
            
            if "circular" in text_lower or "radial" in text_lower:
                return {
                    "action": "create",
                    "type": "circular_pattern",
                    "parameters": {
                        "count": count,
                        "angle": 360
                    },
                    "description": f"Create circular pattern with {count} instances"
                }
            else:
                return {
                    "action": "create",
                    "type": "linear_pattern",
                    "parameters": {
                        "count_x": count,
                        "count_y": 1,
                        "spacing_x": spacing,
                        "spacing_y": spacing,
                        "units": "mm"
                    },
                    "description": f"Create linear pattern {count}x1"
                }
        
        return None
    
    def _extract_dimensions(self, text: str) -> Optional[Dict]:
        """Extract dimensional values from text (e.g., 100x50x25 or 100mm x 50mm x 25mm)"""
        # Pattern for dimensions like "100x50x25" or "100 x 50 x 25"
        pattern = r'(\d+(?:\.\d+)?)\s*(?:mm|cm|m|in)?\s*[xX×]\s*(\d+(?:\.\d+)?)\s*(?:mm|cm|m|in)?\s*(?:[xX×]\s*(\d+(?:\.\d+)?)\s*(?:mm|cm|m|in)?)?'
        match = re.search(pattern, text)
        
        if match:
            width = float(match.group(1))
            height = float(match.group(2))
            depth = float(match.group(3)) if match.group(3) else width  # Default to width if no depth
            
            # Detect unit
            unit = "mm"
            if "cm" in text.lower():
                unit = "cm"
            elif " m " in text.lower() or text.lower().endswith(" m"):
                unit = "m"
            elif "in" in text.lower() or "inch" in text.lower():
                unit = "in"
            
            return {
                "width": width,
                "height": height,
                "depth": depth,
                "units": unit
            }
        
        # Try individual dimension extraction
        dimensions = {}
        for dim_name in ["width", "height", "depth", "length"]:
            value = self._extract_single_dimension(text, dim_name)
            if value:
                if dim_name == "length":
                    dimensions["width"] = value
                else:
                    dimensions[dim_name] = value
        
        if dimensions:
            dimensions.setdefault("units", "mm")
            return dimensions
        
        return None
    
    def _extract_cylinder_dimensions(self, text: str) -> Dict:
        """Extract cylinder dimensions (diameter/radius and height)"""
        diameter = self._extract_single_dimension(text, "diameter")
        radius = self._extract_single_dimension(text, "radius")
        height = self._extract_single_dimension(text, "height") or self._extract_single_dimension(text, "length") or 100
        
        if radius and not diameter:
            diameter = radius * 2
        elif not diameter:
            diameter = 50  # Default
        
        return {
            "diameter": diameter,
            "height": height,
            "units": "mm"
        }
    
    def _extract_single_dimension(self, text: str, name: str) -> Optional[float]:
        """Extract a single named dimension value"""
        if name:
            # Look for patterns like "width: 100mm" or "100mm width" or "width of 100mm"
            patterns = [
                rf'{name}\s*[:=]?\s*(\d+(?:\.\d+)?)\s*(?:mm|cm|m|in)?',
                rf'(\d+(?:\.\d+)?)\s*(?:mm|cm|m|in)?\s*{name}',
                rf'{name}\s+(?:of\s+)?(\d+(?:\.\d+)?)\s*(?:mm|cm|m|in)?',
            ]
            
            for pattern in patterns:
                match = re.search(pattern, text.lower())
                if match:
                    return float(match.group(1))
        
        # Just look for any number with units
        pattern = r'(\d+(?:\.\d+)?)\s*(mm|cm|m|in|inch)'
        matches = re.findall(pattern, text.lower())
        if matches:
            return float(matches[0][0])
        
        return None
    
    def _extract_count(self, text: str) -> int:
        """Extract count/quantity from text"""
        # Look for patterns like "10 holes" or "add 5" or "count: 8"
        patterns = [
            r'(\d+)\s*(?:x\s*)?(?:holes?|threaded|tapped)',
            r'(?:add|create|make)\s+(\d+)',
            r'count\s*[:=]?\s*(\d+)',
            r'(\d+)\s*(?:of|x)\s*(?:m\d+)',
        ]
        
        for pattern in patterns:
            match = re.search(pattern, text.lower())
            if match:
                return int(match.group(1))
        
        return 1  # Default to 1

# Example usage
if __name__ == "__main__":
    parser = SolidWorksCommandParser()
    
    # Test cases
    test_cases = [
        "Create a box 100x50x25mm",
        "Add 10 M4 threaded holes",
        "Make it 10mm longer",
        "Create a cylinder with diameter 30mm and height 80mm",
        "Add a 5mm fillet to the edges",
        "Create 4 M6 tapped holes 15mm deep",
        """I'll create the holes for you.
        
        {
            "action": "create",
            "type": "threaded_hole",
            "parameters": {
                "thread_size": "M4",
                "depth": 12,
                "count": 10
            }
        }
        """,
    ]
    
    for test in test_cases:
        print(f"\n{'='*60}")
        print(f"Input: {test[:50]}...")
        result = parser.parse_response(test)
        print(f"Success: {result['success']}")
        if result.get('command'):
            print(f"Command: {json.dumps(result['command'], indent=2)}")
        if result.get('error'):
            print(f"Error: {result['error']}")
