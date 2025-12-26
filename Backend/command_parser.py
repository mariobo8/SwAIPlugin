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
            "modify_feature", 
            "analyze",
            "review",
            "extrude",
            "cut",
            "hole",
            "chamfer",
            "fillet"
        ]
    
    def parse_response(self, ai_response: str) -> Dict:
        """
        Parse AI response and extract SolidWorks commands
        
        Args:
            ai_response: The AI's natural language response
            
        Returns:
            Dictionary with parsed commands and metadata
        """
        try:
            # Try to extract JSON from the response
            json_match = re.search(r'\{[^{}]*\}', ai_response, re.DOTALL)
            
            if json_match:
                json_str = json_match.group(0)
                command = json.loads(json_str)
                
                # Validate command structure
                if self._validate_command(command):
                    return {
                        "success": True,
                        "command": command,
                        "description": command.get("description", ""),
                        "raw_response": ai_response
                    }
            
            # If no JSON found, try to infer command from text
            inferred = self._infer_command(ai_response)
            if inferred:
                return {
                    "success": True,
                    "command": inferred,
                    "description": ai_response,
                    "raw_response": ai_response,
                    "inferred": True
                }
            
            # No command found
            return {
                "success": False,
                "command": None,
                "description": ai_response,
                "raw_response": ai_response,
                "error": "Could not parse command from response"
            }
            
        except json.JSONDecodeError as e:
            return {
                "success": False,
                "command": None,
                "description": ai_response,
                "raw_response": ai_response,
                "error": f"JSON parsing error: {str(e)}"
            }
        except Exception as e:
            return {
                "success": False,
                "command": None,
                "description": ai_response,
                "raw_response": ai_response,
                "error": f"Parsing error: {str(e)}"
            }
    
    def _validate_command(self, command: Dict) -> bool:
        """Validate that a command has the required structure"""
        if not isinstance(command, dict):
            return False
        
        if "action" not in command:
            return False
        
        if command["action"] not in self.supported_actions:
            return False
        
        return True
    
    def _infer_command(self, text: str) -> Optional[Dict]:
        """
        Try to infer a command from natural language text
        This is a basic implementation - can be enhanced with NLP
        """
        text_lower = text.lower()
        
        # Pattern matching for common operations
        if any(word in text_lower for word in ["box", "cube", "rectangular"]):
            # Extract dimensions if mentioned
            dimensions = self._extract_dimensions(text)
            return {
                "action": "create_feature",
                "type": "box",
                "parameters": dimensions or {"width": 100, "height": 100, "depth": 100, "units": "mm"},
                "description": text
            }
        
        elif any(word in text_lower for word in ["cylinder", "round", "tube"]):
            dimensions = self._extract_dimensions(text)
            return {
                "action": "create_feature",
                "type": "cylinder",
                "parameters": dimensions or {"radius": 50, "height": 100, "units": "mm"},
                "description": text
            }
        
        elif any(word in text_lower for word in ["hole", "drill"]):
            return {
                "action": "hole",
                "type": "hole",
                "parameters": {"diameter": 10, "depth": 50, "units": "mm"},
                "description": text
            }
        
        return None
    
    def _extract_dimensions(self, text: str) -> Optional[Dict]:
        """Extract numeric dimensions from text"""
        # Simple regex to find numbers with units
        pattern = r'(\d+(?:\.\d+)?)\s*(mm|cm|m|in|inch)'
        matches = re.findall(pattern, text, re.IGNORECASE)
        
        if matches:
            # Convert to a simple dict (this is basic - can be enhanced)
            dimensions = {}
            for value, unit in matches[:3]:  # Take first 3 dimensions
                if "width" not in dimensions:
                    dimensions["width"] = float(value)
                elif "height" not in dimensions:
                    dimensions["height"] = float(value)
                elif "depth" not in dimensions:
                    dimensions["depth"] = float(value)
            
            if dimensions:
                dimensions["units"] = matches[0][1] if matches else "mm"
                return dimensions
        
        return None

# Example usage
if __name__ == "__main__":
    parser = SolidWorksCommandParser()
    
    # Test with AI response
    test_response = """
    I'll create a rectangular box for you.
    
    {
        "action": "create_feature",
        "type": "box",
        "parameters": {
            "width": 100,
            "height": 50,
            "depth": 25,
            "units": "mm"
        },
        "description": "Create a 100x50x25mm rectangular box"
    }
    """
    
    result = parser.parse_response(test_response)
    print(json.dumps(result, indent=2))

