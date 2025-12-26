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
        openai_client = OpenAI(api_key=api_key)
        print("✅ OpenAI client initialized")
    else:
        print("⚠️  OPENAI_API_KEY not found in environment variables")

if ANTHROPIC_AVAILABLE:
    api_key = os.getenv('ANTHROPIC_API_KEY')
    if api_key:
        claude_client = anthropic.Anthropic(api_key=api_key)
        print("✅ Claude (Anthropic) client initialized")
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

If the user's request is unclear or cannot be translated to a SolidWorks command, explain what you understood and ask for clarification."""

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
        
        if not user_prompt:
            return jsonify({"error": "No prompt provided"}), 400
        
        print(f"🔹 Received from SolidWorks: {user_prompt}")

        # Try Claude first if configured as provider, otherwise try OpenAI
        if ai_provider == 'claude' and claude_client:
            try:
                message = claude_client.messages.create(
                    model="claude-3-5-haiku-20241022",  # Claude Haiku - cheapest option
                    max_tokens=500,
                    system=SYSTEM_PROMPT,
                    messages=[
                        {"role": "user", "content": user_prompt}
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
                        {"role": "user", "content": user_prompt}
                    ],
                    temperature=0.7,
                    max_tokens=500
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

# 3. Command Parsing Endpoint
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