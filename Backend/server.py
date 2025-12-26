from flask import Flask, request, jsonify

app = Flask(__name__)

# 1. Health Check
@app.route('/', methods=['GET'])
def home():
    return "SolidWorks AI Server is Running!", 200

# 2. The AI Endpoint
@app.route('/ask', methods=['POST'])
def ask_ai():
    try:
        data = request.get_json()
        user_prompt = data.get('prompt', '')
        
        print(f"🔹 Received from SolidWorks: {user_prompt}")

        # Mock Response for now
        ai_response = f"Python received: '{user_prompt}'. Ready for OpenAI integration."
        
        return jsonify({"response": ai_response})

    except Exception as e:
        print(f"❌ Error: {e}")
        return jsonify({"error": str(e)}), 500

if __name__ == '__main__':
    print("🚀 Starting AI Server...")
    app.run(host='127.0.0.1', port=5000, debug=True)