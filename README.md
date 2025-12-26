# SolidWorks AI Plugin

A native SolidWorks add-in that brings AI-powered CAD assistance directly into your design workflow. Built with a hybrid architecture using C# for the SolidWorks integration and Python for AI processing.

## 🎯 Vision

Transform SolidWorks into an intelligent CAD assistant that can:
- **Text-to-CAD**: Type natural language commands to create geometry
- **Design Review**: Automatically analyze models for errors and manufacturing issues
- **Optimization**: Iterate on designs based on AI suggestions
- **Passive Learning**: Learn from your design patterns over time

## 🏗️ Architecture

### Hybrid Architecture
- **Frontend (C#)**: Native SolidWorks add-in (.dll) handling UI and SolidWorks API calls
- **Backend (Python)**: Flask server running AI logic, OpenAI integration, and command parsing
- **Bridge**: HTTP JSON communication between C# and Python

### Project Structure
```
SwAIPlugin/
├── SwAIPlugin/          # C# SolidWorks Add-in
│   ├── TaskPaneUI.cs    # Main UI and HTTP client
│   └── SwAddin.cs       # Add-in registration
├── Backend/              # Python Flask server
│   ├── server.py        # Main server with OpenAI integration
│   ├── command_parser.py # SolidWorks command parser
│   └── requirements.txt  # Python dependencies
└── packages/             # SolidWorks interop DLLs
```

## 🚀 Quick Start

### 1. Backend Setup (Python)

```bash
cd Backend
pip install -r requirements.txt
```

Create a `.env` file:
```
OPENAI_API_KEY=your_api_key_here
```

Start the server:
```bash
python server.py
```

### 2. Frontend Setup (C#)

1. Open `SwAIPlugin.slnx` in Visual Studio
2. Build the solution (F6)
3. The add-in will be registered with SolidWorks automatically

### 3. Using the Plugin

1. Start the Python backend server
2. Open SolidWorks
3. The "AI Assistant" task pane should appear
4. Type your request (e.g., "Create a 100x50x25mm box")
5. Click "Send" and watch the AI respond!

## 📋 Features

- ✅ HTTP bridge between C# and Python
- ✅ OpenAI GPT-4 integration
- ✅ Command parsing structure
- ✅ Task pane UI with input/output
- ✅ Error handling and fallback responses

## 🔧 Configuration

### OpenAI API Key
Get your API key from: https://platform.openai.com/api-keys

Set it in `Backend/.env`:
```
OPENAI_API_KEY=sk-...
```

### Backend URL
The C# add-in connects to `http://127.0.0.1:5000` by default. To change this, edit `TaskPaneUI.cs`:
```csharp
private const string BACKEND_URL = "http://127.0.0.1:5000/ask";
```

## 🛠️ Development

### Adding New Commands
1. Update `command_parser.py` with new command types
2. Add corresponding SolidWorks API calls in C#
3. Update the system prompt in `server.py`

### Testing
- Backend: Test endpoints with `curl` or Postman
- Frontend: Build and test in SolidWorks

## 📝 License

[Your License Here]

## 🤝 Contributing

[Contributing Guidelines]

---

**Status**: 🟢 Active Development

