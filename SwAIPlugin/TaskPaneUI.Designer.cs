namespace SwAIPlugin
{
    partial class TaskPaneUI
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        private void InitializeComponent()
        {
            this.panelChat = new System.Windows.Forms.Panel();
            this.richTextBoxChat = new System.Windows.Forms.RichTextBox();
            this.panelInput = new System.Windows.Forms.Panel();
            this.textBoxInput = new System.Windows.Forms.TextBox();
            this.buttonSend = new System.Windows.Forms.Button();
            this.labelStatus = new System.Windows.Forms.Label();
            this.panelChat.SuspendLayout();
            this.panelInput.SuspendLayout();
            this.SuspendLayout();
            // 
            // panelChat - Main chat display area
            // 
            this.panelChat.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panelChat.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(30)))), ((int)(((byte)(30)))));
            this.panelChat.Controls.Add(this.richTextBoxChat);
            this.panelChat.Location = new System.Drawing.Point(0, 0);
            this.panelChat.Name = "panelChat";
            this.panelChat.Padding = new System.Windows.Forms.Padding(8);
            this.panelChat.Size = new System.Drawing.Size(340, 480);
            this.panelChat.TabIndex = 0;
            // 
            // richTextBoxChat - Chat messages
            // 
            this.richTextBoxChat.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(30)))), ((int)(((byte)(30)))));
            this.richTextBoxChat.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.richTextBoxChat.Dock = System.Windows.Forms.DockStyle.Fill;
            this.richTextBoxChat.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.richTextBoxChat.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(220)))), ((int)(((byte)(220)))), ((int)(((byte)(220)))));
            this.richTextBoxChat.Location = new System.Drawing.Point(8, 8);
            this.richTextBoxChat.Name = "richTextBoxChat";
            this.richTextBoxChat.ReadOnly = true;
            this.richTextBoxChat.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
            this.richTextBoxChat.Size = new System.Drawing.Size(324, 464);
            this.richTextBoxChat.TabIndex = 0;
            this.richTextBoxChat.Text = "";
            // 
            // panelInput - Input area at bottom
            // 
            this.panelInput.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panelInput.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(45)))), ((int)(((byte)(45)))));
            this.panelInput.Controls.Add(this.textBoxInput);
            this.panelInput.Controls.Add(this.buttonSend);
            this.panelInput.Controls.Add(this.labelStatus);
            this.panelInput.Location = new System.Drawing.Point(0, 480);
            this.panelInput.Name = "panelInput";
            this.panelInput.Padding = new System.Windows.Forms.Padding(8);
            this.panelInput.Size = new System.Drawing.Size(340, 110);
            this.panelInput.TabIndex = 1;
            // 
            // textBoxInput - Message input
            // 
            this.textBoxInput.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textBoxInput.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.textBoxInput.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.textBoxInput.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBoxInput.ForeColor = System.Drawing.Color.White;
            this.textBoxInput.Location = new System.Drawing.Point(8, 8);
            this.textBoxInput.Multiline = true;
            this.textBoxInput.Name = "textBoxInput";
            this.textBoxInput.Size = new System.Drawing.Size(258, 60);
            this.textBoxInput.TabIndex = 0;
            // 
            // buttonSend - Send button
            // 
            this.buttonSend.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonSend.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(122)))), ((int)(((byte)(204)))));
            this.buttonSend.Cursor = System.Windows.Forms.Cursors.Hand;
            this.buttonSend.FlatAppearance.BorderSize = 0;
            this.buttonSend.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.buttonSend.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonSend.ForeColor = System.Drawing.Color.White;
            this.buttonSend.Location = new System.Drawing.Point(272, 8);
            this.buttonSend.Name = "buttonSend";
            this.buttonSend.Size = new System.Drawing.Size(60, 60);
            this.buttonSend.TabIndex = 1;
            this.buttonSend.Text = "➤";
            this.buttonSend.UseVisualStyleBackColor = false;
            this.buttonSend.Click += new System.EventHandler(this.buttonSend_Click);
            // 
            // labelStatus - Status indicator
            // 
            this.labelStatus.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.labelStatus.Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelStatus.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(140)))), ((int)(((byte)(140)))), ((int)(((byte)(140)))));
            this.labelStatus.Location = new System.Drawing.Point(8, 72);
            this.labelStatus.Name = "labelStatus";
            this.labelStatus.Size = new System.Drawing.Size(324, 30);
            this.labelStatus.TabIndex = 2;
            this.labelStatus.Text = "Ready • Type a command or ask a question";
            this.labelStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // TaskPaneUI
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(30)))), ((int)(((byte)(30)))), ((int)(((byte)(30)))));
            this.Controls.Add(this.panelInput);
            this.Controls.Add(this.panelChat);
            this.Name = "TaskPaneUI";
            this.Size = new System.Drawing.Size(340, 590);
            this.Load += new System.EventHandler(this.TaskPaneUI_Load);
            this.panelChat.ResumeLayout(false);
            this.panelInput.ResumeLayout(false);
            this.panelInput.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panelChat;
        private System.Windows.Forms.RichTextBox richTextBoxChat;
        private System.Windows.Forms.Panel panelInput;
        private System.Windows.Forms.TextBox textBoxInput;
        private System.Windows.Forms.Button buttonSend;
        private System.Windows.Forms.Label labelStatus;
    }
}
