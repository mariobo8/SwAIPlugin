# SolidWorks AI Backend

Python Flask server that acts as the "brain" for the SolidWorks AI Assistant.

## Setup

1. **Install Python dependencies:**
   ```bash
   pip install -r requirements.txt
   ```

2. **Configure AI Provider:**
   
   Create a `.env` file in the `Backend` folder. Choose either OpenAI or Claude:
   
   **Option A: OpenAI (Recommended - Cheaper)**
   ```
   AI_PROVIDER=openai
   OPENAI_API_KEY=your_openai_api_key_here
   ```
   Get your API key from: https://platform.openai.com/api-keys
   
   **Option B: Claude (Anthropic)**
   ```
   AI_PROVIDER=claude
   ANTHROPIC_API_KEY=your_claude_api_key_here
   ```
   Get your API key from: https://console.anthropic.com/
   
   **Pricing Comparison:**
   - OpenAI GPT-4o-mini: ~$0.15/$0.60 per 1M tokens (cheapest)
   - Claude Haiku: ~$0.25/$1.25 per 1M tokens
   - Claude Sonnet: ~$3.00/$15.00 per 1M tokens

3. **Run the server:**
   ```bash
   python server.py
   ```
   
   The server will start on `http://127.0.0.1:5000`

## Features

- ✅ OpenAI GPT-4o-mini integration
- ✅ Claude (Anthropic) integration (Haiku & Sonnet)
- ✅ Configurable AI provider (OpenAI or Claude)
- ✅ SolidWorks command parsing structure
- ✅ Error handling and fallback responses
- ✅ CORS enabled for local development

## API Endpoints

- `GET /` - Health check
- `POST /ask` - Send prompt to AI, get response
- `POST /parse_command` - Parse AI response into SolidWorks commands (coming soon)

## Environment Variables

- `AI_PROVIDER` - Choose `openai` or `claude` (default: openai)
- `OPENAI_API_KEY` - Your OpenAI API key (required if using OpenAI)
- `ANTHROPIC_API_KEY` - Your Claude API key (required if using Claude)

