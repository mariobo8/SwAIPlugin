using System;
using System.Drawing; // Needed for Colors (Grey/Black)
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace SwAIPlugin
{
    [ComVisible(true)]
    [ProgId("SwAIPlugin.TaskPaneUI")]
    public partial class TaskPaneUI : UserControl
    {
        // Define our placeholder text
        private const string PLACEHOLDER_TEXT = "Ask AI...";

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
        private void button1_Click(object sender, EventArgs e)
        {
            string userPrompt = textBox1.Text;

            // Don't send if it's just the placeholder text!
            if (userPrompt == PLACEHOLDER_TEXT || string.IsNullOrWhiteSpace(userPrompt))
            {
                MessageBox.Show("Please type something first!");
                return;
            }

            // Test Popup
            MessageBox.Show("Sending to AI: " + userPrompt);

            // Optional: Clear the box after sending
            textBox1.Text = "";
            // Force the placeholder to come back immediately (optional)
            AddPlaceholder(textBox1, null);
        }

        // Ignore these auto-generated ones
        private void TaskPaneUI_Load(object sender, EventArgs e) { }
        private void textBox1_TextChanged(object sender, EventArgs e) { }
        private void textBox2_TextChanged(object sender, EventArgs e) { }
    }
}