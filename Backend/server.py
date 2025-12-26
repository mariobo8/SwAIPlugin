import os
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
            OPENAI_AVAILABLE = False  # Disable OpenAI to prevent further errors
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
            ANTHROPIC_AVAILABLE = False  # Disable Claude to prevent further errors
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
SYSTEM_PROMPT = """You are a SolidWorks CAD assistant. Your role is to help users create and modify 3D models using SolidWorks API commands.

When users ask you to:
- Create geometry (boxes, cylinders, holes, etc.)
- Modify existing features
- Perform design operations
- Review or analyze models

You should respond with:
1. A natural language explanation of what you'll do
2. A structured command in JSON format that can be executed by the SolidWorks API

Command format:
{
    "action": "create_feature|modify_feature|analyze|review",
    "type": "box|cylinder|hole|extrude|cut|etc",
    "parameters": {
        "width": 100,
        "height": 50,
        "depth": 25,
        "units": "mm"
    },
    "description": "Human-readable description"
}

If the user's request is unclear or cannot be translated to a SolidWorks command, explain what you understood and ask for clarification.

IMPORTANT: If you receive model context information, use it to understand the current state of the model and provide context-aware responses."""

# System prompt for model analysis
ANALYSIS_PROMPT = """You are a SolidWorks CAD expert. Analyze the provided model information and describe:
1. What type of model it is (Part, Assembly, Drawing)
2. Key features and their purposes
3. Overall geometry and complexity
4. Any notable characteristics (dimensions, mass properties, etc.)
5. Potential design considerations or observations

Provide a clear, concise analysis that helps the user understand what the AI can "see" about their model."""

# 1. Health Check
@app.route('/', methods=['GET'])
def home():
    status = {
        "status": "running",
        "openai_available": OPENAI_AVAILABLE,
        "openai_configured": openai_client is not None,
        "claude_available": ANTHROPIC_AVAILABLE,
        "claude_configured": claude_client is not None,
        "current_provider": ai_provider if (ai_provider == 'claude' and claude_client) or openai_client else "none"
    }
    return jsonify(status), 200

# 2. The AI Endpoint
@app.route('/ask', methods=['POST'])
def ask_ai():
    try:
        data = request.get_json()
        user_prompt = data.get('prompt', '')
        model_context = data.get('model_context', '')
        
        if not user_prompt:
            return jsonify({"error": "No prompt provided"}), 400
        
        print(f"🔹 Received from SolidWorks: {user_prompt}")
        if model_context:
            print(f"📊 Model context provided ({len(model_context)} chars)")
        
        # Build the full prompt with context
        full_prompt = user_prompt
        if model_context:
            full_prompt = f"""CURRENT MODEL CONTEXT:
{model_context}

USER REQUEST:
{user_prompt}

Please respond considering the current model state."""

        # Try Claude first if configured as provider, otherwise try OpenAI
        if ai_provider == 'claude' and claude_client:
            try:
                message = claude_client.messages.create(
                    model="claude-3-5-haiku-20241022",  # Claude Haiku - cheapest option
                    max_tokens=1000,
                    system=SYSTEM_PROMPT,
                    messages=[
                        {"role": "user", "content": full_prompt}
                    ]
                )
                
                ai_response = message.content[0].text
                print(f"✅ Claude response received")
                
                return jsonify({
                    "response": ai_response,
                    "source": "claude",
                    "model": "claude-3-5-haiku"
                })
                
            except Exception as e:
                print(f"❌ Claude Error: {e}")
                # Fallback to OpenAI if available
                if openai_client:
                    print("🔄 Falling back to OpenAI...")
                else:
                    ai_response = f"Claude error: {str(e)}\n\nFalling back to mock response.\n\nYour request: '{user_prompt}'"
                    return jsonify({
                        "response": ai_response,
                        "source": "fallback",
                        "error": str(e)
                    })
        
        # Use OpenAI if available
        if openai_client:
            try:
                response = openai_client.chat.completions.create(
                    model="gpt-4o-mini",  # Cost-effective model
                    messages=[
                        {"role": "system", "content": SYSTEM_PROMPT},
                        {"role": "user", "content": full_prompt}
                    ],
                    temperature=0.7,
                    max_tokens=1000
                )
                
                ai_response = response.choices[0].message.content
                print(f"✅ OpenAI response received")
                
                return jsonify({
                    "response": ai_response,
                    "source": "openai",
                    "model": "gpt-4o-mini"
                })
                
            except Exception as e:
                print(f"❌ OpenAI Error: {e}")
                # Fallback to mock response
                ai_response = f"OpenAI error: {str(e)}\n\nFalling back to mock response.\n\nYour request: '{user_prompt}'"
                return jsonify({
                    "response": ai_response,
                    "source": "fallback",
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

# 3. Model Analysis Endpoint
@app.route('/analyze', methods=['POST'])
def analyze_model():
    """Analyze and describe a SolidWorks model"""
    try:
        data = request.get_json()
        model_context = data.get('model_context', '')
        
        if not model_context:
            return jsonify({"error": "No model context provided"}), 400
        
        print(f"📊 Analyzing model ({len(model_context)} chars)")
        
        # Build analysis prompt
        analysis_prompt = f"""Analyze this SolidWorks model information:

{model_context}

Provide a detailed description of what you can understand about this model."""
        
        # Try Claude first if configured
        if ai_provider == 'claude' and claude_client:
            try:
                message = claude_client.messages.create(
                    model="claude-3-5-haiku-20241022",
                    max_tokens=1500,
                    system=ANALYSIS_PROMPT,
                    messages=[
                        {"role": "user", "content": analysis_prompt}
                    ]
                )
                
                ai_response = message.content[0].text
                print(f"✅ Claude analysis received")
                
                return jsonify({
                    "response": ai_response,
                    "source": "claude",
                    "model": "claude-3-5-haiku"
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
                response = openai_client.chat.completions.create(
                    model="gpt-4o-mini",
                    messages=[
                        {"role": "system", "content": ANALYSIS_PROMPT},
                        {"role": "user", "content": analysis_prompt}
                    ],
                    temperature=0.7,
                    max_tokens=1500
                )
                
                ai_response = response.choices[0].message.content
                print(f"✅ OpenAI analysis received")
                
                return jsonify({
                    "response": ai_response,
                    "source": "openai",
                    "model": "gpt-4o-mini"
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

if __name__ == '__main__':
    print("🚀 Starting SolidWorks AI Server...")
    print("📝 Configure AI provider:")
    print("   - OpenAI: Set OPENAI_API_KEY environment variable")
    print("   - Claude: Set ANTHROPIC_API_KEY environment variable")
    print("   - Choose provider: Set AI_PROVIDER=openai or AI_PROVIDER=claude (default: openai)")
    print("\n💰 Pricing comparison:")
    print("   - OpenAI GPT-4o-mini: ~$0.15/$0.60 per 1M tokens (input/output)")
    print("   - Claude Haiku: ~$0.25/$1.25 per 1M tokens (input/output)")
    print("   - Claude Sonnet: ~$3.00/$15.00 per 1M tokens (input/output)")
    app.run(host='127.0.0.1', port=5000, debug=True)