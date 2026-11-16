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
            lblControllerCaption = new Label();
            lblController = new Label();
            lblSolidWorksCaption = new Label();
            lblSolidWorks = new Label();
            lblKeysCaption = new Label();
            lblKeys = new Label();
            lblMapping = new Label();
            btnQuit = new Button();
            SuspendLayout();
            //
            // lblControllerCaption
            //
            lblControllerCaption.AutoSize = true;
            lblControllerCaption.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblControllerCaption.Location = new Point(12, 12);
            lblControllerCaption.Name = "lblControllerCaption";
            lblControllerCaption.Text = "Controller";
            //
            // lblController
            //
            lblController.AutoSize = true;
            lblController.Location = new Point(100, 12);
            lblController.Name = "lblController";
            lblController.Text = "Looking for an Xbox controller...";
            //
            // lblSolidWorksCaption
            //
            lblSolidWorksCaption.AutoSize = true;
            lblSolidWorksCaption.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblSolidWorksCaption.Location = new Point(12, 36);
            lblSolidWorksCaption.Name = "lblSolidWorksCaption";
            lblSolidWorksCaption.Text = "SolidWorks";
            //
            // lblSolidWorks
            //
            lblSolidWorks.AutoSize = true;
            lblSolidWorks.Location = new Point(100, 36);
            lblSolidWorks.Name = "lblSolidWorks";
            lblSolidWorks.Text = "Looking for SolidWorks...";
            //
            // lblKeysCaption
            //
            lblKeysCaption.AutoSize = true;
            lblKeysCaption.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            lblKeysCaption.Location = new Point(12, 60);
            lblKeysCaption.Name = "lblKeysCaption";
            lblKeysCaption.Text = "Keys held";
            //
            // lblKeys
            //
            lblKeys.AutoSize = true;
            lblKeys.Font = new Font("Consolas", 9F);
            lblKeys.Location = new Point(100, 61);
            lblKeys.Name = "lblKeys";
            lblKeys.Text = "(none)";
            //
            // lblMapping
            //
            lblMapping.Font = new Font("Segoe UI", 8.25F);
            lblMapping.ForeColor = SystemColors.GrayText;
            lblMapping.Location = new Point(12, 90);
            lblMapping.Name = "lblMapping";
            lblMapping.Size = new Size(396, 130);
            lblMapping.Text =
                "Left stick: walk / strafe          Right stick: turn (X) / look up-down (Y)\r\n" +
                "RT / LT: move up / down          RB / LB: zoom in / out\r\n" +
                "D-pad up / down: speed + / -\r\n\r\n" +
                "Open an assembly, start the Walk-through, and click into SolidWorks. Keys are only " +
                "sent while SolidWorks has focus. Strafe pauses while turning or rising, and walking " +
                "pauses while looking, because those keys share Shift/Alt.";
            //
            // btnQuit
            //
            btnQuit.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            btnQuit.Location = new Point(333, 228);
            btnQuit.Name = "btnQuit";
            btnQuit.Size = new Size(75, 25);
            btnQuit.TabIndex = 0;
            btnQuit.Text = "Quit";
            btnQuit.UseVisualStyleBackColor = true;
            btnQuit.Click += BtnQuit_Click;
            //
            // Form1
            //
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(420, 265);
            Controls.Add(lblControllerCaption);
            Controls.Add(lblController);
            Controls.Add(lblSolidWorksCaption);
            Controls.Add(lblSolidWorks);
            Controls.Add(lblKeysCaption);
            Controls.Add(lblKeys);
            Controls.Add(lblMapping);
            Controls.Add(btnQuit);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            Name = "Form1";
            Text = "Xbox Control for SolidWorks";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label lblControllerCaption;
        private Label lblController;
        private Label lblSolidWorksCaption;
        private Label lblSolidWorks;
        private Label lblKeysCaption;
        private Label lblKeys;
        private Label lblMapping;
        private Button btnQuit;
    }
}
