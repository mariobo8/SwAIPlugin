# SolidWorks AI Plugin 🤖

An intelligent AI assistant for SolidWorks that can analyze your 3D models, create geometry, and execute CAD commands through natural language.

## Features

### 🔍 Model Analysis with Vision
- Captures screenshots of your model and sends them to AI for visual analysis
- Detailed feature tree inspection
- Bounding box and mass properties extraction
- Material and configuration information

### 🔧 Geometry Creation
- **Boxes/Blocks**: `"Create a 100x50x25mm box"`
- **Cylinders**: `"Create a cylinder Ø30mm, 80mm tall"`
- **Extrusions**: `"Extrude this sketch 50mm"`
- **Cuts**: `"Cut 10mm deep"`

### 🔩 Hole Features
- **Simple Holes**: `"Add a 10mm diameter hole"`
- **Threaded Holes**: `"Add 4 M6 threaded holes"` (supports M2-M20)
- **Counterbore/Countersink**: `"Create a counterbore hole"`
- **Hole Patterns**: `"Add 10 M4 holes with 20mm spacing"`

### 📐 Modifications
- **Dimension Changes**: `"Make it 10mm longer"` or `"Make it 5mm shorter"`
- **Fillets**: `"Add a 5mm fillet"` (select edges first)
- **Chamfers**: `"Add 2mm chamfer at 45°"` (select edges first)
- **Patterns**: Linear and circular patterns
- **Shell**: `"Create a 2mm wall shell"`

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    SolidWorks                               │
│  ┌─────────────────────────────────────────────────────┐   │
│  │              SwAIPlugin (C# Add-in)                  │   │
│  │  ┌─────────────┐  ┌──────────────┐  ┌────────────┐  │   │
│  │  │ TaskPaneUI  │  │ModelAnalyzer │  │CommandExec │  │   │
│  │  │   (WinForms)│  │ (Screenshot) │  │ (Features) │  │   │
│  │  └──────┬──────┘  └──────┬───────┘  └─────┬──────┘  │   │
│  └─────────┼────────────────┼────────────────┼─────────┘   │
│            │                │                │              │
└────────────┼────────────────┼────────────────┼──────────────┘
             │     HTTP/JSON  │                │
             ▼                ▼                ▼
┌─────────────────────────────────────────────────────────────┐
│                 Python Backend (Flask)                       │
│  ┌─────────────┐  ┌──────────────┐  ┌────────────────────┐  │
│  │   /ask      │  │  /analyze    │  │  CommandParser     │  │
│  │  endpoint   │  │  (vision)    │  │  (NLP → Commands)  │  │
│  └──────┬──────┘  └──────┬───────┘  └─────────┬──────────┘  │
│         │                │                    │              │
│         └────────────────┼────────────────────┘              │
│                          ▼                                   │
│  ┌──────────────────────────────────────────────────────┐   │
│  │         OpenAI (GPT-4o) / Claude (Sonnet)            │   │
│  │         Vision + Natural Language Processing          │   │
│  └──────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
```

## Installation

### Prerequisites
- SolidWorks 2020 or later
- .NET Framework 4.7.2
- Python 3.9+
- OpenAI or Anthropic API key

### 1. Python Backend Setup

```bash
cd Backend

# Create virtual environment (recommended)
python -m venv venv
venv\Scripts\activate  # Windows
# source venv/bin/activate  # Linux/Mac

# Install dependencies
pip install -r requirements.txt

# Configure API keys (create .env file)
echo AI_PROVIDER=openai > .env
echo OPENAI_API_KEY=sk-your-key-here >> .env
# OR for Claude:
# echo AI_PROVIDER=claude > .env
# echo ANTHROPIC_API_KEY=sk-ant-your-key-here >> .env

# Start the server
python server.py
```

The server will start at `http://127.0.0.1:5000`

### 2. SolidWorks Add-in Installation

1. **Build the project** in Visual Studio (Release mode recommended)
2. **Register the DLL** (run as Administrator):
   ```cmd
   cd SwAIPlugin\bin\Debug
   %windir%\Microsoft.NET\Framework64\v4.0.30319\regasm.exe SwAIPlugin.dll /codebase
   ```
3. **Restart SolidWorks**
4. The AI Assistant will appear in the right sidebar

### Uninstall
```cmd
%windir%\Microsoft.NET\Framework64\v4.0.30319\regasm.exe SwAIPlugin.dll /unregister
```

## Usage

### Basic Workflow

1. **Start the Python server** (keep it running in background)
2. **Open SolidWorks** and create/open a part
3. **Open the AI Assistant** task pane (right sidebar)
4. **Type your request** in natural language
5. **Click Send** - AI will respond with a command
6. **Click Execute** to run the command in SolidWorks

### Example Commands

| What you type | What happens |
|---------------|--------------|
| `Create a 100x50x25 box` | Creates an extruded rectangle on Front Plane |
| `Add 4 M6 threaded holes` | Creates 4 tap-drill sized holes for M6 threads |
| `Make it 10mm longer` | Increases the length dimension by 10mm |
| `Create a cylinder 50mm diameter, 100mm tall` | Creates an extruded circle |
| `Add 5mm fillet` | Adds fillet to selected edges |
| `Shell with 2mm walls` | Creates shell feature on selected faces |
| `Create linear pattern 5x3 with 20mm spacing` | Patterns selected features |

### Analyze Model

Click **🔍 Analyze Model** to:
- Capture a screenshot of your current model
- Extract feature tree information
- Get bounding box dimensions
- Read mass properties
- Send all this to AI for detailed analysis

The AI will describe what it "sees" and can suggest improvements!

## API Configuration

### Environment Variables

Create a `.env` file in the `Backend` folder:

```env
# Choose provider: 'openai' or 'claude'
AI_PROVIDER=openai

# OpenAI (https://platform.openai.com/api-keys)
OPENAI_API_KEY=sk-...

# Anthropic Claude (https://console.anthropic.com/)
ANTHROPIC_API_KEY=sk-ant-...
```

### Pricing Comparison

| Model | Input | Output | Best For |
|-------|-------|--------|----------|
| GPT-4o-mini | $0.15/1M | $0.60/1M | Text-only commands |
| GPT-4o | $2.50/1M | $10.00/1M | Vision/image analysis |
| Claude Haiku | $0.25/1M | $1.25/1M | Text-only commands |
| Claude Sonnet | $3.00/1M | $15.00/1M | Vision/image analysis |

Vision models are automatically selected when screenshots are included.

## Project Structure

```
SwAIPlugin/
├── Backend/
│   ├── server.py           # Flask server with AI endpoints
│   ├── command_parser.py   # NLP to command translation
│   └── requirements.txt    # Python dependencies
│
├── SwAIPlugin/
│   ├── SwAddin.cs          # Main add-in entry point
│   ├── TaskPaneUI.cs       # UI controller
│   ├── ModelAnalyzer.cs    # Model inspection + screenshots
│   └── SolidWorksCommandExecutor.cs  # Feature creation
│
├── packages/               # SolidWorks API NuGet packages
└── README.md
```

## Supported Features

### Geometry Creation
- ✅ Box/Rectangle/Block
- ✅ Cylinder
- ✅ Extrusion (from sketch)
- ✅ Cut/Pocket

### Holes
- ✅ Simple holes
- ✅ Threaded holes (M2-M20)
- ✅ Counterbore holes
- ⬜ Countersink (basic)
- ⬜ Hole Wizard integration

### Modifications
- ✅ Fillets
- ✅ Chamfers
- ✅ Dimension changes
- ✅ Shell
- ⬜ Mirror
- ⬜ Scale

### Patterns
- ✅ Linear pattern
- ✅ Circular pattern

## Troubleshooting

### "Connection Error" when sending requests
- Make sure Python server is running (`python server.py`)
- Check that port 5000 is not blocked by firewall
- Verify API keys are set correctly

### "Could not select Front Plane"
- Ensure you have an active part document open
- The add-in supports English and localized plane names

### Features not executing
- Some features require pre-selection (edges for fillet/chamfer)
- Ensure you have geometry to operate on
- Check the response text for specific error messages

### Add-in not appearing in SolidWorks
- Run `regasm` as Administrator
- Check Windows Event Viewer for COM registration errors
- Try rebuilding in Debug mode

## Contributing

Contributions welcome! Areas that need work:
- More feature types (loft, sweep, etc.)
- Better dimension modification (by name)
- Assembly operations
- Drawing automation
- Improved error handling

## License

MIT License - feel free to use and modify for your projects.
