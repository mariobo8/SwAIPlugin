using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;

namespace SwAIPlugin
{
    [ComVisible(true)]
    [ProgId("SwAIPlugin.TaskPaneUI")]
    public partial class TaskPaneUI : UserControl
    {
        private static readonly HttpClient httpClient = new HttpClient();
        private const string BACKEND_URL = "http://127.0.0.1:5000/ask";
        
        private SldWorks swApp;
        private ModelAnalyzer modelAnalyzer;
        private SolidWorksCommandExecutor commandExecutor;
        
        // Colors for chat
        private readonly Color colorUser = Color.FromArgb(0, 122, 204);
        private readonly Color colorAI = Color.FromArgb(80, 80, 80);
        private readonly Color colorSuccess = Color.FromArgb(40, 167, 69);
        private readonly Color colorError = Color.FromArgb(220, 53, 69);
        private readonly Color colorSystem = Color.FromArgb(100, 100, 100);

        public TaskPaneUI()
        {
            InitializeComponent();
            httpClient.Timeout = TimeSpan.FromSeconds(120);
            
            // Setup input handling
            textBoxInput.KeyDown += TextBoxInput_KeyDown;
            textBoxInput.GotFocus += TextBoxInput_GotFocus;
            textBoxInput.LostFocus += TextBoxInput_LostFocus;
            
            // Set placeholder
            SetPlaceholder();
        }

        public void SetSolidWorksApp(SldWorks app)
        {
            swApp = app;
            if (swApp != null)
            {
                modelAnalyzer = new ModelAnalyzer(swApp);
                commandExecutor = new SolidWorksCommandExecutor(swApp);
            }
        }

        #region Input Handling

        private void SetPlaceholder()
        {
            textBoxInput.Text = "Type your command...";
            textBoxInput.ForeColor = Color.Gray;
        }

        private void TextBoxInput_GotFocus(object sender, EventArgs e)
        {
            if (textBoxInput.Text == "Type your command...")
            {
                textBoxInput.Text = "";
                textBoxInput.ForeColor = Color.White;
            }
        }

        private void TextBoxInput_LostFocus(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(textBoxInput.Text))
            {
                SetPlaceholder();
            }
        }

        private void TextBoxInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.SuppressKeyPress = true;
                buttonSend_Click(sender, e);
            }
        }

        #endregion

        #region Send and Execute

        private async void buttonSend_Click(object sender, EventArgs e)
        {
            string userMessage = textBoxInput.Text.Trim();
            
            if (string.IsNullOrEmpty(userMessage) || userMessage == "Type your command...")
            {
                return;
            }

            // Clear input and show user message
            textBoxInput.Text = "";
            SetPlaceholder();
            AppendMessage("You", userMessage, colorUser);
            
            // Disable UI
            SetBusy(true, "Thinking...");

            try
            {
                // Get model context and screenshot
                string modelContext = GetModelContext();
                string imageBase64 = modelAnalyzer?.CaptureModelScreenshot();

                // Send to AI
                string response = await SendToAIAsync(userMessage, modelContext, imageBase64);
                
                // Parse response
                var parsed = ParseResponse(response);
                
                // Show AI response (cleaned up)
                string cleanResponse = CleanResponseForDisplay(parsed.Message);
                AppendMessage("AI", cleanResponse, colorAI);

                // Auto-execute if command found
                if (parsed.Command != null)
                {
                    SetBusy(true, "Executing...");
                    await Task.Delay(100); // Brief pause for UI update
                    
                    string result = ExecuteCommand(parsed.Command);
                    
                    if (result.StartsWith("Success"))
                    {
                        AppendMessage("✓", result, colorSuccess);
                    }
                    else
                    {
                        AppendMessage("✗", result, colorError);
                    }
                }
            }
            catch (HttpRequestException)
            {
                AppendMessage("Error", "Cannot connect to AI server.\nMake sure the Python backend is running:\n  cd Backend && python server.py", colorError);
            }
            catch (Exception ex)
            {
                AppendMessage("Error", ex.Message, colorError);
            }
            finally
            {
                SetBusy(false, "Ready");
            }
        }

        private string CleanResponseForDisplay(string response)
        {
            // Remove JSON blocks from display - user doesn't need to see them
            var lines = response.Split('\n');
            var cleanLines = new List<string>();
            bool inJsonBlock = false;
            
            foreach (var line in lines)
            {
                string trimmed = line.Trim();
                
                if (trimmed.StartsWith("```json") || trimmed.StartsWith("```"))
                {
                    inJsonBlock = !inJsonBlock;
                    continue;
                }
                
                if (trimmed.StartsWith("{") && trimmed.Contains("action"))
                {
                    inJsonBlock = true;
                    continue;
                }
                
                if (inJsonBlock)
                {
                    if (trimmed.StartsWith("}"))
                    {
                        inJsonBlock = false;
                    }
                    continue;
                }
                
                if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    cleanLines.Add(line);
                }
            }
            
            string result = string.Join("\n", cleanLines).Trim();
            
            // If we removed everything, show a simple message
            if (string.IsNullOrWhiteSpace(result))
            {
                result = "Executing command...";
            }
            
            return result;
        }

        #endregion

        #region Chat Display

        private void AppendMessage(string sender, string message, Color color)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => AppendMessage(sender, message, color)));
                return;
            }

            // Add spacing between messages
            if (richTextBoxChat.TextLength > 0)
            {
                richTextBoxChat.AppendText("\n\n");
            }

            // Sender label
            int start = richTextBoxChat.TextLength;
            richTextBoxChat.AppendText(sender);
            richTextBoxChat.Select(start, sender.Length);
            richTextBoxChat.SelectionColor = color;
            richTextBoxChat.SelectionFont = new Font(richTextBoxChat.Font, FontStyle.Bold);
            
            // Message
            richTextBoxChat.AppendText("\n");
            start = richTextBoxChat.TextLength;
            richTextBoxChat.AppendText(message);
            richTextBoxChat.Select(start, message.Length);
            richTextBoxChat.SelectionColor = Color.FromArgb(200, 200, 200);
            richTextBoxChat.SelectionFont = new Font(richTextBoxChat.Font, FontStyle.Regular);
            
            // Scroll to bottom
            richTextBoxChat.SelectionStart = richTextBoxChat.TextLength;
            richTextBoxChat.ScrollToCaret();
        }

        private void SetBusy(bool busy, string status)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => SetBusy(busy, status)));
                return;
            }

            buttonSend.Enabled = !busy;
            textBoxInput.Enabled = !busy;
            buttonSend.BackColor = busy ? Color.FromArgb(80, 80, 80) : Color.FromArgb(0, 122, 204);
            labelStatus.Text = busy ? $"⏳ {status}" : $"● {status}";
            labelStatus.ForeColor = busy ? Color.FromArgb(200, 200, 100) : Color.FromArgb(140, 140, 140);
        }

        #endregion

        #region AI Communication

        private string GetModelContext()
        {
            try
            {
                return modelAnalyzer?.AnalyzeModel();
            }
            catch
            {
                return null;
            }
        }

        private async Task<string> SendToAIAsync(string prompt, string modelContext, string imageBase64)
        {
            var sb = new StringBuilder();
            sb.Append("{\"prompt\":\"").Append(Escape(prompt)).Append("\"");
            
            if (!string.IsNullOrEmpty(modelContext))
            {
                sb.Append(",\"model_context\":\"").Append(Escape(modelContext)).Append("\"");
            }
            
            if (!string.IsNullOrEmpty(imageBase64))
            {
                sb.Append(",\"image\":\"").Append(imageBase64).Append("\"");
            }
            
            sb.Append("}");

            var content = new StringContent(sb.ToString(), Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync(BACKEND_URL, content);
            response.EnsureSuccessStatusCode();
            
            return await response.Content.ReadAsStringAsync();
        }

        private string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }

        #endregion

        #region Command Parsing and Execution

        private ParsedResponse ParseResponse(string json)
        {
            var result = new ParsedResponse();
            result.Message = ExtractField(json, "response") ?? json;
            
            // Try to find command in response
            string cmdJson = ExtractField(json, "command");
            if (!string.IsNullOrEmpty(cmdJson) && cmdJson != "null")
            {
                result.Command = ParseCommandDict(cmdJson);
            }
            
            // Also try to find JSON in the message itself
            if (result.Command == null)
            {
                result.Command = FindCommandInText(result.Message);
            }
            
            return result;
        }

        private Dictionary<string, object> FindCommandInText(string text)
        {
            int braceStart = text.IndexOf('{');
            if (braceStart < 0) return null;
            
            int braceCount = 0;
            int braceEnd = -1;
            
            for (int i = braceStart; i < text.Length; i++)
            {
                if (text[i] == '{') braceCount++;
                else if (text[i] == '}') braceCount--;
                
                if (braceCount == 0)
                {
                    braceEnd = i;
                    break;
                }
            }
            
            if (braceEnd > braceStart)
            {
                string jsonPart = text.Substring(braceStart, braceEnd - braceStart + 1);
                return ParseCommandDict(jsonPart);
            }
            
            return null;
        }

        private Dictionary<string, object> ParseCommandDict(string json)
        {
            try
            {
                var dict = new Dictionary<string, object>();
                
                // Extract action
                string action = ExtractField(json, "action");
                if (!string.IsNullOrEmpty(action))
                {
                    dict["action"] = action;
                }
                
                // Extract type
                string type = ExtractField(json, "type");
                if (!string.IsNullOrEmpty(type))
                {
                    dict["type"] = type;
                }
                
                // Extract parameters block
                int paramStart = json.IndexOf("\"parameters\"");
                if (paramStart >= 0)
                {
                    int braceStart = json.IndexOf('{', paramStart);
                    if (braceStart >= 0)
                    {
                        int braceCount = 0;
                        int braceEnd = -1;
                        
                        for (int i = braceStart; i < json.Length; i++)
                        {
                            if (json[i] == '{') braceCount++;
                            else if (json[i] == '}') braceCount--;
                            
                            if (braceCount == 0)
                            {
                                braceEnd = i;
                                break;
                            }
                        }
                        
                        if (braceEnd > braceStart)
                        {
                            string paramJson = json.Substring(braceStart, braceEnd - braceStart + 1);
                            dict["parameters"] = ParseParameters(paramJson);
                        }
                    }
                }
                
                return dict.Count > 0 && dict.ContainsKey("action") ? dict : null;
            }
            catch
            {
                return null;
            }
        }

        private Dictionary<string, object> ParseParameters(string json)
        {
            var dict = new Dictionary<string, object>();
            
            // Simple key-value extraction
            string[] keys = { "width", "height", "depth", "diameter", "radius", "x", "y", "count", "spacing", "thread_size", "face", "units", "through_all", "delta", "thickness", "distance", "angle" };
            
            foreach (string key in keys)
            {
                string value = ExtractField(json, key);
                if (!string.IsNullOrEmpty(value))
                {
                    // Try to parse as number
                    if (double.TryParse(value, out double d))
                    {
                        dict[key] = d;
                    }
                    else if (value == "true")
                    {
                        dict[key] = true;
                    }
                    else if (value == "false")
                    {
                        dict[key] = false;
                    }
                    else
                    {
                        dict[key] = value;
                    }
                }
            }
            
            return dict;
        }

        private string ExtractField(string json, string field)
        {
            string pattern = $"\"{field}\"";
            int idx = json.IndexOf(pattern);
            if (idx < 0) return null;
            
            int colonIdx = json.IndexOf(':', idx);
            if (colonIdx < 0) return null;
            
            int valueStart = colonIdx + 1;
            while (valueStart < json.Length && char.IsWhiteSpace(json[valueStart])) valueStart++;
            
            if (valueStart >= json.Length) return null;
            
            if (json[valueStart] == '"')
            {
                valueStart++;
                int valueEnd = valueStart;
                while (valueEnd < json.Length && json[valueEnd] != '"')
                {
                    if (json[valueEnd] == '\\') valueEnd++;
                    valueEnd++;
                }
                return json.Substring(valueStart, valueEnd - valueStart)
                    .Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\\\", "\\");
            }
            else if (json[valueStart] == '{')
            {
                int braceCount = 0;
                int end = valueStart;
                while (end < json.Length)
                {
                    if (json[end] == '{') braceCount++;
                    else if (json[end] == '}') braceCount--;
                    if (braceCount == 0) break;
                    end++;
                }
                return json.Substring(valueStart, end - valueStart + 1);
            }
            else
            {
                int end = valueStart;
                while (end < json.Length && json[end] != ',' && json[end] != '}' && json[end] != '\n')
                {
                    end++;
                }
                return json.Substring(valueStart, end - valueStart).Trim();
            }
        }

        private string ExecuteCommand(Dictionary<string, object> command)
        {
            if (commandExecutor == null)
            {
                return "Error: SolidWorks not connected";
            }

            string action = command.ContainsKey("action") ? command["action"].ToString() : "";
            string type = command.ContainsKey("type") ? command["type"].ToString() : "";
            var parameters = command.ContainsKey("parameters") ? command["parameters"] as Dictionary<string, object> : null;

            return commandExecutor.ExecuteCommand(action, type, parameters);
        }

        #endregion

        #region Initialization

        private void TaskPaneUI_Load(object sender, EventArgs e)
        {
            // Welcome message
            AppendMessage("AI", "Hi! I'm your SolidWorks assistant.\n\n" +
                "FOR PARTS:\n" +
                "• Create a 100x50x25 box\n" +
                "• Create a cylinder 30mm diameter\n" +
                "• Add 4 M6 threaded holes\n" +
                "• Make it 10mm longer\n\n" +
                "FOR DRAWINGS:\n" +
                "• Analyze this drawing\n" +
                "• Check for missing dimensions\n" +
                "• Review for manufacturing", colorAI);
        }

        // Legacy event handlers (keep for compatibility)
        private void textBox1_TextChanged(object sender, EventArgs e) { }
        private void textBox2_TextChanged(object sender, EventArgs e) { }
        private void button1_Click(object sender, EventArgs e) { }
        private void button2_Click(object sender, EventArgs e) { }
        private void button3_Click(object sender, EventArgs e) { }

        #endregion

        private class ParsedResponse
        {
            public string Message { get; set; }
            public Dictionary<string, object> Command { get; set; }
        }
    }
}
