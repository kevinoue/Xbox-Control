namespace Xbox_Control
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support.
        /// </summary>
        private void InitializeComponent()
        {
            btnQuit = new Button();
            SuspendLayout();
            // 
            // btnQuit
            // 
            btnQuit.Anchor = AnchorStyles.Bottom;
            btnQuit.Location = new Point(84, 34);
            btnQuit.Name = "btnQuit";
            btnQuit.Size = new Size(75, 23);
            btnQuit.TabIndex = 0;
            btnQuit.Text = "Quit";
            btnQuit.UseVisualStyleBackColor = true;
            btnQuit.Click += BtnQuit_Click;
            // 
            // Form1
            // 
            ClientSize = new Size(241, 63);
            Controls.Add(btnQuit);
            Name = "Form1";
            Text = "Xbox Control";
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Button btnQuit;
    }
}
