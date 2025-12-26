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
SYSTEM_PROMPT = """You are a SolidWorks CAD assistant with vision capabilities. Your role is to help users create and modify 3D models using SolidWorks API commands.

When analyzing a model (with or without an image), describe what you see and understand about the geometry.

When users ask you to create or modify geometry, you MUST respond with:
1. A brief natural language explanation of what you'll do
2. A structured JSON command that can be executed by SolidWorks

IMPORTANT: Always include the JSON command block in your response when the user requests a creation or modification.

Available command formats:

CREATE BOX/RECTANGLE:
{
    "action": "create",
    "type": "box",
    "parameters": {
        "width": 100,
        "height": 50,
        "depth": 25,
        "units": "mm"
    }
}

CREATE CYLINDER:
{
    "action": "create",
    "type": "cylinder",
    "parameters": {
        "diameter": 50,
        "height": 100,
        "units": "mm"
    }
}

CREATE THREADED HOLE (M4, M5, M6, M8, M10, etc.):
{
    "action": "create",
    "type": "threaded_hole",
    "parameters": {
        "thread_size": "M6",
        "depth": 15,
        "count": 4,
        "spacing": 25,
        "x": 10,
        "y": 10,
        "units": "mm"
    }
}

CREATE SIMPLE HOLE:
{
    "action": "create",
    "type": "hole",
    "parameters": {
        "diameter": 10,
        "depth": 20,
        "through_all": false,
        "x": 0,
        "y": 0,
        "units": "mm"
    }
}

CREATE FILLET:
{
    "action": "create",
    "type": "fillet",
    "parameters": {
        "radius": 5,
        "units": "mm"
    }
}

CREATE CHAMFER:
{
    "action": "create",
    "type": "chamfer",
    "parameters": {
        "distance": 2,
        "angle": 45,
        "units": "mm"
    }
}

MODIFY DIMENSION (make longer/shorter/wider):
{
    "action": "modify",
    "type": "dimension",
    "parameters": {
        "delta": 10,
        "units": "mm"
    }
}

CREATE SHELL:
{
    "action": "create",
    "type": "shell",
    "parameters": {
        "thickness": 2,
        "units": "mm"
    }
}

CREATE LINEAR PATTERN:
{
    "action": "create",
    "type": "linear_pattern",
    "parameters": {
        "count_x": 5,
        "count_y": 3,
        "spacing_x": 20,
        "spacing_y": 15,
        "units": "mm"
    }
}

CREATE NEW EMPTY PART (when user just wants a blank part):
{
    "action": "create_part",
    "type": "empty",
    "parameters": {}
}

CREATE NEW PART WITH GEOMETRY (when user specifies shape):
{
    "action": "create_part",
    "type": "box",
    "parameters": {
        "width": 100,
        "height": 100,
        "depth": 50,
        "units": "mm"
    }
}

ADD BOSS/EXTRUSION ON EXISTING FACE (e.g., rectangle on top of cylinder):
{
    "action": "create",
    "type": "boss_on_face",
    "parameters": {
        "width": 10,
        "height": 10,
        "depth": 5,
        "face": "top",
        "x": 0,
        "y": 0,
        "units": "mm"
    }
}

ADD CUT/POCKET ON EXISTING FACE:
{
    "action": "create",
    "type": "cut_on_face",
    "parameters": {
        "width": 10,
        "height": 10,
        "depth": 5,
        "face": "top",
        "through_all": false,
        "x": 0,
        "y": 0,
        "units": "mm"
    }
}

Notes:
- All dimensions should be in the specified units (default: mm)
- For threaded holes, use standard metric sizes: M2, M2.5, M3, M4, M5, M6, M8, M10, M12, M14, M16, M20
- When modifying dimensions, use "delta" for relative changes (+10 to make 10mm longer, -10 to make 10mm shorter)
- For fillets and chamfers, the user must first select edges in SolidWorks
- x, y coordinates specify the position of features on a face

IMPORTANT: If the user's request is unclear, ask for clarification. Always try to infer reasonable defaults from context."""

# System prompt for model analysis with vision
ANALYSIS_PROMPT = """You are a SolidWorks CAD expert with vision capabilities. Analyze the provided image and context.

## FOR 3D MODELS (Parts/Assemblies):
1. **Overall Shape**: Basic geometry description
2. **Visible Features**: Extrusions, cuts, holes, fillets, chamfers, patterns
3. **Approximate Dimensions**: Estimate sizes if visible
4. **Design Intent**: What this part might be used for
5. **Suggestions**: Improvements or modifications

## FOR ENGINEERING DRAWINGS:
When analyzing a drawing, act as a quality control engineer and check for:

### CRITICAL ISSUES (must fix):
- ❌ Missing dimensions for manufacturing
- ❌ Ambiguous or unclear dimensions
- ❌ Incorrect or missing tolerances
- ❌ Missing critical views
- ❌ Scale inconsistencies
- ❌ Overlapping or crossing dimension lines

### IMPORTANT ISSUES (should fix):
- ⚠️ Poor dimension placement or organization
- ⚠️ Missing center marks or centerlines
- ⚠️ Incomplete title block information
- ⚠️ Missing material callouts
- ⚠️ GD&T symbols missing where needed
- ⚠️ Section or detail views needed but missing

### SUGGESTIONS (nice to have):
- 💡 Better view arrangement
- 💡 Add isometric view for clarity
- 💡 Improve leader line routing
- 💡 Add surface finish symbols
- 💡 Consolidate dimensions
- 💡 Use proper standards (ISO/ASME)

### FORMAT YOUR RESPONSE AS:
1. **Summary**: One-line overall assessment
2. **Issues Found**: List each issue with severity (❌/⚠️/💡)
3. **Recommendations**: Specific actions to fix issues
4. **Overall Score**: Rate 1-10 for manufacturing readiness

Be specific and actionable - this drawing may be sent to manufacturing."""

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
                
                # Parse for commands
                parsed_command = None
                if command_parser:
                    parsed = command_parser.parse_response(ai_response)
                    if parsed.get('success'):
                        parsed_command = parsed.get('command')
                
                return jsonify({
                    "response": ai_response,
                    "source": "claude",
                    "model": "claude-sonnet-4" if image_base64 else "claude-3-5-haiku",
                    "command": parsed_command
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
                
                # Parse for commands
                parsed_command = None
                if command_parser:
                    parsed = command_parser.parse_response(ai_response)
                    if parsed.get('success'):
                        parsed_command = parsed.get('command')
                
                return jsonify({
                    "response": ai_response,
                    "source": "openai",
                    "model": model,
                    "command": parsed_command
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
