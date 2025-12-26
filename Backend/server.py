import os
import base64
from flask import Flask, request, jsonify
from flask_cors import CORS

# Load environment variables from .env file
try:
    from dotenv import load_dotenv
    load_dotenv()
except ImportError:
    pass  # dotenv is optional

# Try to import OpenAI, but handle gracefully if not installed
try:
    from openai import OpenAI
    OPENAI_AVAILABLE = True
except ImportError:
    OPENAI_AVAILABLE = False
    print("⚠️  OpenAI package not installed. Install with: pip install openai")

# Try to import Anthropic (Claude), but handle gracefully if not installed
try:
    import anthropic
    ANTHROPIC_AVAILABLE = True
except ImportError:
    ANTHROPIC_AVAILABLE = False
    print("⚠️  Anthropic package not installed. Install with: pip install anthropic")

# Import command parser
try:
    from command_parser import SolidWorksCommandParser
    PARSER_AVAILABLE = True
except ImportError:
    PARSER_AVAILABLE = False
    print("⚠️  Command parser not available")

app = Flask(__name__)
CORS(app)  # Enable CORS for local development

# Initialize AI clients if API keys are available
openai_client = None
claude_client = None
ai_provider = os.getenv('AI_PROVIDER', 'openai').lower()  # Default to OpenAI

if OPENAI_AVAILABLE:
    api_key = os.getenv('OPENAI_API_KEY')
    if api_key:
        try:
            openai_client = OpenAI(api_key=api_key)
            print("✅ OpenAI client initialized")
        except Exception as e:
            print(f"❌ Failed to initialize OpenAI client: {e}")
            print("💡 Try updating: pip install --upgrade openai httpx")
            OPENAI_AVAILABLE = False
    else:
        print("⚠️  OPENAI_API_KEY not found in environment variables")

if ANTHROPIC_AVAILABLE:
    api_key = os.getenv('ANTHROPIC_API_KEY')
    if api_key:
        try:
            claude_client = anthropic.Anthropic(api_key=api_key)
            print("✅ Claude (Anthropic) client initialized")
        except Exception as e:
            print(f"❌ Failed to initialize Claude client: {e}")
            print("💡 Try updating: pip install --upgrade anthropic")
            ANTHROPIC_AVAILABLE = False
    else:
        print("⚠️  ANTHROPIC_API_KEY not found in environment variables")

# Determine which provider to use
if ai_provider == 'claude' and claude_client:
    print(f"🤖 Using Claude as AI provider")
elif openai_client:
    print(f"🤖 Using OpenAI as AI provider")
else:
    print(f"⚠️  No AI provider configured. Using mock responses.")

# Initialize command parser
command_parser = None
if PARSER_AVAILABLE:
    command_parser = SolidWorksCommandParser()
    print("✅ Command parser initialized")

# System prompt for SolidWorks AI Assistant
SYSTEM_PROMPT = """You are an expert SolidWorks CAD engineer and AI assistant. You help users design 3D models by providing both design guidance AND executable JSON commands.

## YOUR CAPABILITIES

1. **Create 3D geometry** - boxes, cylinders, extrusions, cuts, holes
2. **Modify existing parts** - fillets, chamfers, shells, patterns
3. **Analyze designs** - review screenshots, suggest improvements
4. **Provide CAD guidance** - best practices, design for manufacturing

## RESPONSE FORMAT

When users request geometry, you can provide **multiple JSON commands** that will execute in sequence.

**Format each command as a separate JSON code block:**

### EXAMPLE - Simple Request:
User: "Create a box 50x30x20"
Response: "I'll create a 50x30x20mm box on the Front Plane.

```json
{
    "action": "create",
    "type": "box",
    "parameters": {
        "width": 50,
        "height": 30,
        "depth": 20,
        "units": "mm"
    }
}
```"

### EXAMPLE - Multi-Step Request (executed automatically):
User: "Create a mounting plate with 4 screw holes"
Response: "I'll create the mounting plate with holes. Here are the steps:

**Step 1: Base plate (100x50x10mm)**
```json
{
    "action": "create",
    "type": "box",
    "parameters": {
        "width": 100,
        "height": 50,
        "depth": 10,
        "units": "mm"
    }
}
```

**Step 2: Add 4 M4 holes at corners**
```json
{
    "action": "create",
    "type": "threaded_hole",
    "parameters": {
        "face": "top",
        "thread_size": "M4",
        "depth": 10,
        "x": 10,
        "y": 10,
        "count": 4,
        "spacing": 30,
        "units": "mm"
    }
}
```

Both steps will execute automatically in sequence!"

### EXAMPLE - Complex Part:
User: "Create a bracket with rounded edges"
Response: "Creating an L-bracket with fillets:

**Step 1: Base plate**
```json
{"action": "create", "type": "box", "parameters": {"width": 60, "height": 40, "depth": 5, "units": "mm"}}
```

**Step 2: Vertical section**
```json
{"action": "create", "type": "boss_on_face", "parameters": {"face": "top", "width": 60, "height": 5, "depth": 30, "x": 0, "y": 17.5, "units": "mm"}}
```

*Note: For fillets, you'll need to select the edges first, then ask me to add fillets.*"

---

## AVAILABLE COMMANDS

### 1. CREATE NEW PART
Start a new empty part or with initial geometry:
```json
{"action": "create_part", "type": "empty", "parameters": {}}
```
```json
{"action": "create_part", "type": "box", "parameters": {"width": 100, "height": 50, "depth": 25, "units": "mm"}}
```

### 2. BOX / RECTANGULAR BLOCK
Creates an extruded rectangle. Use for: plates, blocks, bases, housings.
```json
{
    "action": "create",
    "type": "box",
    "parameters": {
        "plane": "Front Plane",
        "width": 100,
        "height": 50,
        "depth": 25,
        "offset_x": 0,
        "offset_y": 0,
        "units": "mm"
    }
}
```
- `plane`: "Front Plane", "Top Plane", "Right Plane"
- `width`: X dimension, `height`: Y dimension, `depth`: Z (extrusion)
- `offset_x/y`: Position offset from origin

### 3. CYLINDER
Creates an extruded circle. Use for: shafts, pins, standoffs, bushings.
```json
{
    "action": "create",
    "type": "cylinder",
    "parameters": {
        "plane": "Front Plane",
        "diameter": 50,
        "height": 100,
        "center_x": 0,
        "center_y": 0,
        "units": "mm"
    }
}
```

### 4. BOSS ON FACE (Add material)
Adds an extrusion on an existing face. Use for: mounting bosses, ribs, raised features.
```json
{
    "action": "create",
    "type": "boss_on_face",
    "parameters": {
        "face": "top",
        "width": 20,
        "height": 20,
        "depth": 10,
        "x": 0,
        "y": 0,
        "units": "mm"
    }
}
```
- `face`: "top", "bottom", "front", "back", "left", "right"

### 5. CUT ON FACE (Remove material)
Creates a pocket or slot on an existing face. Use for: pockets, slots, recesses.
```json
{
    "action": "create",
    "type": "cut_on_face",
    "parameters": {
        "face": "top",
        "width": 30,
        "height": 10,
        "depth": 5,
        "through_all": false,
        "x": 0,
        "y": 0,
        "units": "mm"
    }
}
```

### 6. SIMPLE HOLE
Creates a circular hole. Use for: clearance holes, dowel holes, access holes.
```json
{
    "action": "create",
    "type": "hole",
    "parameters": {
        "face": "top",
        "diameter": 10,
        "depth": 20,
        "through_all": false,
        "x": 25,
        "y": 15,
        "units": "mm"
    }
}
```

### 7. THREADED HOLE
Creates a tapped hole for screws. Use for: M2-M20 screw mounting.
```json
{
    "action": "create",
    "type": "threaded_hole",
    "parameters": {
        "face": "top",
        "thread_size": "M6",
        "depth": 15,
        "through_all": false,
        "x": 10,
        "y": 10,
        "count": 1,
        "spacing": 20,
        "units": "mm"
    }
}
```
- `thread_size`: "M2", "M2.5", "M3", "M4", "M5", "M6", "M8", "M10", "M12", "M14", "M16", "M20"

### 8. FILLET (Round edges)
Rounds edges. User must SELECT EDGES first in SolidWorks.
```json
{
    "action": "create",
    "type": "fillet",
    "parameters": {
        "radius": 3,
        "units": "mm"
    }
}
```
💡 Design tips:
- Use R1-3mm for hand-safe edges
- Use larger radii for stress relief
- Avoid fillets smaller than your machining tool radius

### 9. CHAMFER (Beveled edges)
Creates angled edges. User must SELECT EDGES first.
```json
{
    "action": "create",
    "type": "chamfer",
    "parameters": {
        "distance": 2,
        "angle": 45,
        "units": "mm"
    }
}
```
💡 Use chamfers for: lead-ins, deburring, assembly guidance

### 10. SHELL (Hollow out)
Creates a thin-walled part. User must SELECT FACES to remove.
```json
{
    "action": "create",
    "type": "shell",
    "parameters": {
        "thickness": 2,
        "outward": false,
        "units": "mm"
    }
}
```

### 11. LINEAR PATTERN
Repeats features in rows/columns. User must SELECT FEATURE first.
```json
{
    "action": "create",
    "type": "linear_pattern",
    "parameters": {
        "count_x": 4,
        "count_y": 2,
        "spacing_x": 25,
        "spacing_y": 20,
        "units": "mm"
    }
}
```

### 12. CIRCULAR PATTERN
Repeats features around an axis. User must SELECT FEATURE and AXIS.
```json
{
    "action": "create",
    "type": "circular_pattern",
    "parameters": {
        "count": 6,
        "angle": 360,
        "equal_spacing": true
    }
}
```

### 13. MODIFY DIMENSION
Changes an existing dimension relatively.
```json
{
    "action": "modify",
    "type": "dimension",
    "parameters": {
        "delta": 10,
        "units": "mm"
    }
}
```
- Positive delta = make larger
- Negative delta = make smaller

### 14. DELETE FEATURE
Removes a feature by name.
```json
{
    "action": "delete",
    "type": "feature",
    "parameters": {
        "feature_name": "Fillet1"
    }
}
```

---

## CAD DESIGN KNOWLEDGE

### Standard Planes
- **Front Plane (XY)**: Default for 2D profile views, front-facing features
- **Top Plane (XZ)**: Default for plan views, horizontal features
- **Right Plane (YZ)**: Side profiles, lateral features

### Feature Order Best Practices
1. Start with main body (box, cylinder)
2. Add positive features (bosses, ribs)
3. Add negative features (cuts, holes)
4. Apply fillets/chamfers last

### Common Design Patterns
- **Mounting plate**: Box → holes at corners → fillets
- **Shaft**: Cylinder → chamfer ends → keyway cut
- **Housing**: Box → shell → mounting bosses → screw holes
- **Bracket**: L-shaped extrusion → holes → fillets for strength

### Thread Hole Depths (rule of thumb)
- Soft materials (aluminum): 2x diameter
- Hard materials (steel): 1.5x diameter
- Through-hole when possible for assembly access

---

## IMPORTANT RULES

1. **Always include units**: Every dimension needs `"units": "mm"`
2. **Use exact JSON format**: Follow the schemas above exactly
3. **Ask for clarification**: If dimensions are missing or unclear, ask
4. **Consider manufacturability**: Suggest practical sizes and tolerances
5. **Selection required**: Fillets, chamfers, shells, and patterns require user to select geometry first

## CONVERSATION EXAMPLES

**User**: "I need a small box"
**You**: "I'll create a 50x30x20mm box on the Front Plane.

```json
{"action": "create", "type": "box", "parameters": {"width": 50, "height": 30, "depth": 20, "units": "mm"}}
```

What will you use it for? I can add holes, fillets, or other features next."

---

**User**: "Create a bracket with mounting holes"  
**You**: "I'll build this step by step. First, the base L-shape. Creating a 60x40x5mm plate.

```json
{"action": "create", "type": "box", "parameters": {"width": 60, "height": 40, "depth": 5, "units": "mm"}}
```

**Next**: Say 'continue' and I'll add the vertical part, then the mounting holes."

---

**User**: "Add 4 screw holes for M4 bolts"
**You**: "Adding 4 M4 threaded holes, 8mm from each edge.

```json
{"action": "create", "type": "threaded_hole", "parameters": {"face": "top", "thread_size": "M4", "depth": 10, "x": 8, "y": 8, "count": 4, "spacing": 34, "units": "mm"}}
```

💡 M4 needs minimum 6mm edge distance. 10mm depth = 2.5x thread engagement."

---

**User**: "Round off the edges"
**You**: "First, select the edges you want to round in SolidWorks. What radius? (3mm is typical for hand-safe edges)

```json
{"action": "create", "type": "fillet", "parameters": {"radius": 3, "units": "mm"}}
```"

---

**User**: "Make it hollow"
**You**: "Select the face to remove (usually top for access), then this will shell with 2mm walls:

```json
{"action": "create", "type": "shell", "parameters": {"thickness": 2, "units": "mm"}}
```

💡 2mm works for plastic. Use 3mm+ for structural parts."
"""

# System prompt for model analysis with vision
ANALYSIS_PROMPT = """You are a senior SolidWorks CAD engineer reviewing designs. Analyze images with an expert eye and provide actionable feedback.

## FOR 3D MODELS (Parts/Assemblies):

### What to Identify:
1. **Geometry Type**: Prismatic, revolved, swept, sheet metal, etc.
2. **Key Features**: 
   - Base feature (extrusion, revolve, loft)
   - Secondary features (cuts, holes, bosses)
   - Finishing features (fillets, chamfers)
   - Patterns (linear, circular, mirror)
3. **Estimated Dimensions**: Use reference objects or standard sizes
4. **Material Guess**: Based on color, texture, application
5. **Manufacturing Method**: CNC, 3D print, injection mold, casting

### Design Review Checklist:
- ✅ **Strength**: Are there stress concentrators? Add fillets?
- ✅ **Manufacturability**: Can this be made? Undercuts? Tool access?
- ✅ **Assembly**: How does this fit with other parts? Tolerances?
- ✅ **Cost**: Unnecessary complexity? Material waste?

### Response Format:
```
**What I See**: [Brief description of the part]

**Feature Breakdown**:
1. Base: [e.g., "Extruded rectangle ~100x50mm"]
2. Features: [List visible operations]
3. Finishing: [Fillets, chamfers, etc.]

**Design Assessment**:
- Strengths: [What's good]
- Concerns: [Potential issues]
- Suggestions: [Improvements]

**If you want me to recreate this, I would**:
[Step-by-step approach with JSON commands]
```

## FOR ENGINEERING DRAWINGS:

Act as a quality control engineer reviewing for manufacturing release.

### Critical Checks (❌ = Must Fix):
- Missing dimensions needed for manufacturing
- Ambiguous or duplicate dimensions
- Missing tolerances on critical features
- Incorrect or missing GD&T
- Views that don't match or are inconsistent
- Missing section views for internal features

### Important Checks (⚠️ = Should Fix):
- Poor dimension organization
- Missing centerlines/center marks
- Incomplete title block
- Missing material/finish callouts
- Non-standard views or scales
- Leader lines crossing or unclear

### Suggestions (💡 = Nice to Have):
- Add isometric for clarity
- Consolidate dimensions
- Improve layout/spacing
- Add detail views for small features
- Surface finish symbols

### Response Format:
```
**Drawing Review Summary**: [One line assessment]

**Issues Found**:
❌ [Critical issues]
⚠️ [Important issues]
💡 [Suggestions]

**Manufacturing Readiness**: [X/10]

**Recommended Actions**:
1. [Most important fix]
2. [Second priority]
...
```

## IMPORTANT GUIDELINES:
- Be specific: "Add R2 fillet to sharp corner at hole edge" not "add fillets"
- Be practical: Consider cost and manufacturability
- Be helpful: Offer to help fix issues with commands
- Ask questions: If unclear, ask what the part is for"""

# 1. Health Check
@app.route('/', methods=['GET'])
def home():
    status = {
        "status": "running",
        "openai_available": OPENAI_AVAILABLE,
        "openai_configured": openai_client is not None,
        "claude_available": ANTHROPIC_AVAILABLE,
        "claude_configured": claude_client is not None,
        "current_provider": ai_provider if (ai_provider == 'claude' and claude_client) or openai_client else "none",
        "vision_supported": True
    }
    return jsonify(status), 200

# 2. The AI Endpoint (with optional image support)
@app.route('/ask', methods=['POST'])
def ask_ai():
    try:
        data = request.get_json()
        user_prompt = data.get('prompt', '')
        model_context = data.get('model_context', '')
        image_base64 = data.get('image', '')  # Base64 encoded image
        
        if not user_prompt:
            return jsonify({"error": "No prompt provided"}), 400
        
        print(f"🔹 Received from SolidWorks: {user_prompt[:100]}...")
        if model_context:
            print(f"📊 Model context provided ({len(model_context)} chars)")
        if image_base64:
            print(f"🖼️ Image provided ({len(image_base64)} chars)")
        
        # Build the full prompt with context
        full_prompt = user_prompt
        if model_context:
            full_prompt = f"""CURRENT MODEL CONTEXT:
{model_context}

USER REQUEST:
{user_prompt}

Please respond considering the current model state. If the user is asking for a modification or creation, include the JSON command."""

        # Try Claude first if configured as provider
        if ai_provider == 'claude' and claude_client:
            try:
                # Build message content for Claude
                content = []
                
                # Add image if provided
                if image_base64:
                    content.append({
                        "type": "image",
                        "source": {
                            "type": "base64",
                            "media_type": "image/png",
                            "data": image_base64
                        }
                    })
                
                content.append({
                    "type": "text",
                    "text": full_prompt
                })
                
                message = claude_client.messages.create(
                    model="claude-sonnet-4-20250514" if image_base64 else "claude-3-5-haiku-20241022",
                    max_tokens=2000,
                    system=SYSTEM_PROMPT,
                    messages=[
                        {"role": "user", "content": content}
                    ]
                )
                
                ai_response = message.content[0].text
                print(f"✅ Claude response received")
                
                # Parse for commands (supports multiple commands)
                parsed_command = None
                parsed_commands = []
                if command_parser:
                    parsed = command_parser.parse_response(ai_response)
                    if parsed.get('success'):
                        parsed_command = parsed.get('command')
                        parsed_commands = parsed.get('commands', [parsed_command] if parsed_command else [])
                        if len(parsed_commands) > 1:
                            print(f"   Found {len(parsed_commands)} commands to execute")
                
                return jsonify({
                    "response": ai_response,
                    "source": "claude",
                    "model": "claude-sonnet-4" if image_base64 else "claude-3-5-haiku",
                    "command": parsed_command,
                    "commands": parsed_commands
                })
                
            except Exception as e:
                print(f"❌ Claude Error: {e}")
                if openai_client:
                    print("🔄 Falling back to OpenAI...")
                else:
                    return jsonify({
                        "response": f"Claude error: {str(e)}",
                        "source": "error",
                        "error": str(e)
                    })
        
        # Use OpenAI if available
        if openai_client:
            try:
                # Build messages for OpenAI
                messages = [
                    {"role": "system", "content": SYSTEM_PROMPT}
                ]
                
                if image_base64:
                    # Use vision model with image
                    user_content = [
                        {
                            "type": "image_url",
                            "image_url": {
                                "url": f"data:image/png;base64,{image_base64}",
                                "detail": "high"
                            }
                        },
                        {
                            "type": "text",
                            "text": full_prompt
                        }
                    ]
                    messages.append({"role": "user", "content": user_content})
                    model = "gpt-4o"  # Vision model
                else:
                    messages.append({"role": "user", "content": full_prompt})
                    model = "gpt-4o-mini"  # Cost-effective model
                
                response = openai_client.chat.completions.create(
                    model=model,
                    messages=messages,
                    temperature=0.7,
                    max_tokens=2000
                )
                
                ai_response = response.choices[0].message.content
                print(f"✅ OpenAI response received")
                
                # Parse for commands (supports multiple commands)
                parsed_command = None
                parsed_commands = []
                if command_parser:
                    parsed = command_parser.parse_response(ai_response)
                    if parsed.get('success'):
                        parsed_command = parsed.get('command')
                        parsed_commands = parsed.get('commands', [parsed_command] if parsed_command else [])
                        if len(parsed_commands) > 1:
                            print(f"   Found {len(parsed_commands)} commands to execute")
                
                return jsonify({
                    "response": ai_response,
                    "source": "openai",
                    "model": model,
                    "command": parsed_command,
                    "commands": parsed_commands
                })
                
            except Exception as e:
                print(f"❌ OpenAI Error: {e}")
                return jsonify({
                    "response": f"OpenAI error: {str(e)}",
                    "source": "error",
                    "error": str(e)
                })
        else:
            # Mock response if no AI provider available
            ai_response = f"Python received: '{user_prompt}'\n\n⚠️ No AI provider configured. Set OPENAI_API_KEY or ANTHROPIC_API_KEY environment variable to enable AI responses."
            return jsonify({
                "response": ai_response,
                "source": "mock"
            })

    except Exception as e:
        print(f"❌ Error: {e}")
        import traceback
        traceback.print_exc()
        return jsonify({"error": str(e)}), 500

# 3. Model Analysis Endpoint (with vision support)
@app.route('/analyze', methods=['POST'])
def analyze_model():
    """Analyze and describe a SolidWorks model with optional image"""
    try:
        data = request.get_json()
        model_context = data.get('model_context', '')
        image_base64 = data.get('image', '')
        
        if not model_context and not image_base64:
            return jsonify({"error": "No model context or image provided"}), 400
        
        print(f"📊 Analyzing model...")
        if model_context:
            print(f"   Context: {len(model_context)} chars")
        if image_base64:
            print(f"   Image: {len(image_base64)} chars")
        
        # Build analysis prompt
        analysis_prompt = "Analyze this SolidWorks model"
        if model_context:
            analysis_prompt += f":\n\n{model_context}"
        if image_base64:
            analysis_prompt += "\n\nAn image of the model is attached. Please describe what you see."
        
        # Try Claude first if configured
        if ai_provider == 'claude' and claude_client:
            try:
                content = []
                
                if image_base64:
                    content.append({
                        "type": "image",
                        "source": {
                            "type": "base64",
                            "media_type": "image/png",
                            "data": image_base64
                        }
                    })
                
                content.append({
                    "type": "text",
                    "text": analysis_prompt
                })
                
                message = claude_client.messages.create(
                    model="claude-sonnet-4-20250514" if image_base64 else "claude-3-5-haiku-20241022",
                    max_tokens=2000,
                    system=ANALYSIS_PROMPT,
                    messages=[
                        {"role": "user", "content": content}
                    ]
                )
                
                ai_response = message.content[0].text
                print(f"✅ Claude analysis received")
                
                return jsonify({
                    "response": ai_response,
                    "source": "claude",
                    "model": "claude-sonnet-4" if image_base64 else "claude-3-5-haiku"
                })
            except Exception as e:
                print(f"❌ Claude Error: {e}")
                if openai_client:
                    print("🔄 Falling back to OpenAI...")
                else:
                    return jsonify({
                        "response": f"Error analyzing model: {str(e)}",
                        "source": "error"
                    }), 500
        
        # Use OpenAI if available
        if openai_client:
            try:
                messages = [
                    {"role": "system", "content": ANALYSIS_PROMPT}
                ]
                
                if image_base64:
                    user_content = [
                        {
                            "type": "image_url",
                            "image_url": {
                                "url": f"data:image/png;base64,{image_base64}",
                                "detail": "high"
                            }
                        },
                        {
                            "type": "text",
                            "text": analysis_prompt
                        }
                    ]
                    messages.append({"role": "user", "content": user_content})
                    model = "gpt-4o"
                else:
                    messages.append({"role": "user", "content": analysis_prompt})
                    model = "gpt-4o-mini"
                
                response = openai_client.chat.completions.create(
                    model=model,
                    messages=messages,
                    temperature=0.7,
                    max_tokens=2000
                )
                
                ai_response = response.choices[0].message.content
                print(f"✅ OpenAI analysis received")
                
                return jsonify({
                    "response": ai_response,
                    "source": "openai",
                    "model": model
                })
            except Exception as e:
                print(f"❌ OpenAI Error: {e}")
                return jsonify({
                    "response": f"Error analyzing model: {str(e)}",
                    "source": "error"
                }), 500
        else:
            # Fallback: return raw model context
            return jsonify({
                "response": f"Model Analysis (AI not configured):\n\n{model_context}",
                "source": "raw"
            })
            
    except Exception as e:
        print(f"❌ Analysis Error: {e}")
        import traceback
        traceback.print_exc()
        return jsonify({"error": str(e)}), 500

# 4. Command Parsing Endpoint
@app.route('/parse_command', methods=['POST'])
def parse_command():
    """Parse AI response into executable SolidWorks commands"""
    try:
        data = request.get_json()
        ai_response = data.get('response', '')
        
        if not ai_response:
            return jsonify({"error": "No response provided"}), 400
        
        if command_parser:
            result = command_parser.parse_response(ai_response)
            return jsonify(result)
        else:
            return jsonify({
                "success": False,
                "error": "Command parser not available"
            }), 503
        
    except Exception as e:
        print(f"❌ Parse Error: {e}")
        import traceback
        traceback.print_exc()
        return jsonify({"error": str(e)}), 500

# 5. Execute Command Endpoint (for direct command execution)
@app.route('/execute', methods=['POST'])
def execute_command():
    """Execute a SolidWorks command directly"""
    try:
        data = request.get_json()
        command = data.get('command', {})
        
        if not command:
            return jsonify({"error": "No command provided"}), 400
        
        # Validate command structure
        action = command.get('action', '')
        cmd_type = command.get('type', '')
        parameters = command.get('parameters', {})
        
        if not action:
            return jsonify({"error": "Command missing 'action' field"}), 400
        
        # Return the validated command for the C# plugin to execute
        return jsonify({
            "success": True,
            "action": action,
            "type": cmd_type,
            "parameters": parameters,
            "message": f"Command ready for execution: {action} {cmd_type}"
        })
        
    except Exception as e:
        print(f"❌ Execute Error: {e}")
        import traceback
        traceback.print_exc()
        return jsonify({"error": str(e)}), 500

if __name__ == '__main__':
    print("🚀 Starting SolidWorks AI Server with Vision Support...")
    print("=" * 60)
    print("📝 Configure AI provider:")
    print("   - OpenAI: Set OPENAI_API_KEY environment variable")
    print("   - Claude: Set ANTHROPIC_API_KEY environment variable")
    print("   - Choose provider: Set AI_PROVIDER=openai or AI_PROVIDER=claude")
    print()
    print("🖼️ Vision Support:")
    print("   - Screenshot analysis is enabled")
    print("   - OpenAI uses gpt-4o for images, gpt-4o-mini for text")
    print("   - Claude uses claude-sonnet-4 for images, claude-3-5-haiku for text")
    print()
    print("💰 Pricing comparison:")
    print("   - GPT-4o-mini: ~$0.15/$0.60 per 1M tokens")
    print("   - GPT-4o (vision): ~$2.50/$10.00 per 1M tokens")
    print("   - Claude Haiku: ~$0.25/$1.25 per 1M tokens")
    print("   - Claude Sonnet (vision): ~$3.00/$15.00 per 1M tokens")
    print("=" * 60)
    app.run(host='127.0.0.1', port=5000, debug=True)
