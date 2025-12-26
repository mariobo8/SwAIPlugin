using System;
using System.Drawing; // Needed for Colors (Grey/Black)
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SwAIPlugin
{
    [ComVisible(true)]
    [ProgId("SwAIPlugin.TaskPaneUI")]
    public partial class TaskPaneUI : UserControl
    {
        // Define our placeholder text
        private const string PLACEHOLDER_TEXT = "Ask AI...";
        
        // HTTP Client for communicating with Python backend
        private static readonly HttpClient httpClient = new HttpClient();
        private const string BACKEND_URL = "http://127.0.0.1:5000/ask";

        public TaskPaneUI()
        {
            InitializeComponent();

            // 1. Set up the Input Box (assuming textBox1 is your input box at the bottom)
            // If your input box is textBox2, just change "textBox1" to "textBox2" below.
            SetupPlaceholder(this.textBox1);
        }

        private void SetupPlaceholder(TextBox txt)
        {
            // Set initial state
            txt.Text = PLACEHOLDER_TEXT;
            txt.ForeColor = Color.Gray;

            // Manually add the events (so you don't have to do it in Designer)
            txt.Enter += RemovePlaceholder;
            txt.Leave += AddPlaceholder;
        }

        // Runs when you CLICK INSIDE the box
        private void RemovePlaceholder(object sender, EventArgs e)
        {
            TextBox txt = (TextBox)sender;
            if (txt.Text == PLACEHOLDER_TEXT)
            {
                txt.Text = "";
                txt.ForeColor = Color.Black; // Change text color to normal
            }
        }

        // Runs when you CLICK OUTSIDE the box
        private void AddPlaceholder(object sender, EventArgs e)
        {
            TextBox txt = (TextBox)sender;
            if (string.IsNullOrWhiteSpace(txt.Text))
            {
                txt.Text = PLACEHOLDER_TEXT;
                txt.ForeColor = Color.Gray; // Dim the text again
            }
        }

        // The Send Button Logic
        private async void button1_Click(object sender, EventArgs e)
        {
            string userPrompt = textBox1.Text;

            // Don't send if it's just the placeholder text!
            if (userPrompt == PLACEHOLDER_TEXT || string.IsNullOrWhiteSpace(userPrompt))
            {
                MessageBox.Show("Please type something first!");
                return;
            }

            // Disable the button and show loading state
            button1.Enabled = false;
            button1.Text = "Sending...";
            textBox2.Text = "Sending request to AI...";
            textBox2.ForeColor = Color.Gray;

            try
            {
                // Send HTTP POST request to Python backend
                string response = await SendToBackendAsync(userPrompt);
                
                // Display the response in textBox2
                textBox2.Text = response;
                textBox2.ForeColor = Color.Black;
            }
            catch (Exception ex)
            {
                // Handle errors gracefully
                string errorMsg = $"Error: {ex.Message}\n\nMake sure the Python server is running on http://127.0.0.1:5000";
                textBox2.Text = errorMsg;
                textBox2.ForeColor = Color.Red;
                MessageBox.Show(errorMsg, "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                // Re-enable the button
                button1.Enabled = true;
                button1.Text = "Send";
                
                // Clear the input box after sending
                textBox1.Text = "";
                AddPlaceholder(textBox1, null);
            }
        }

        /// <summary>
        /// Sends a POST request to the Python backend with the user's prompt
        /// </summary>
        private async Task<string> SendToBackendAsync(string prompt)
        {
            // Create JSON payload
            string jsonPayload = $"{{\"prompt\": \"{EscapeJsonString(prompt)}\"}}";
            
            // Create HTTP content
            StringContent content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            
            // Send POST request
            HttpResponseMessage response = await httpClient.PostAsync(BACKEND_URL, content);
            
            // Ensure we got a successful response
            response.EnsureSuccessStatusCode();
            
            // Read and return the response body
            string responseBody = await response.Content.ReadAsStringAsync();
            
            // Parse JSON response (simple extraction - expects {"response": "..."})
            // For a more robust solution, consider adding Newtonsoft.Json NuGet package
            string aiResponse = ExtractResponseFromJson(responseBody);
            
            return aiResponse;
        }

        /// <summary>
        /// Escapes special characters in JSON strings
        /// </summary>
        private string EscapeJsonString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;
            
            return input
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        /// <summary>
        /// Extracts the "response" field from JSON string
        /// Simple parser - for production, consider using Newtonsoft.Json
        /// </summary>
        private string ExtractResponseFromJson(string json)
        {
            try
            {
                // Simple extraction: look for "response": "value"
                int responseIndex = json.IndexOf("\"response\"");
                if (responseIndex == -1)
                {
                    // Try "error" field if response not found
                    int errorIndex = json.IndexOf("\"error\"");
                    if (errorIndex != -1)
                    {
                        int colonIndex = json.IndexOf(":", errorIndex);
                        int startQuote = json.IndexOf("\"", colonIndex) + 1;
                        int endQuote = json.IndexOf("\"", startQuote);
                        if (endQuote > startQuote)
                        {
                            return "Error: " + json.Substring(startQuote, endQuote - startQuote);
                        }
                    }
                    return json; // Return raw JSON if we can't parse it
                }
                
                int colonIndex2 = json.IndexOf(":", responseIndex);
                int startQuote2 = json.IndexOf("\"", colonIndex2) + 1;
                int endQuote2 = json.IndexOf("\"", startQuote2);
                
                if (endQuote2 > startQuote2)
                {
                    string extracted = json.Substring(startQuote2, endQuote2 - startQuote2);
                    // Unescape JSON string
                    return extracted
                        .Replace("\\n", "\n")
                        .Replace("\\r", "\r")
                        .Replace("\\t", "\t")
                        .Replace("\\\"", "\"")
                        .Replace("\\\\", "\\");
                }
                
                return json; // Fallback to raw JSON
            }
            catch
            {
                return json; // Return raw JSON if parsing fails
            }
        }

        // Ignore these auto-generated ones
        private void TaskPaneUI_Load(object sender, EventArgs e) { }
        private void textBox1_TextChanged(object sender, EventArgs e) { }
        private void textBox2_TextChanged(object sender, EventArgs e) { }
    }
}