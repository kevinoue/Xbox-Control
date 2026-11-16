namespace Xbox_Control
{
    public partial class Form1 : Form
    {
        private readonly KeyboardOutput keyboard;
        private readonly XInputGamepad gamepad = new();
        private readonly WalkthroughKeyMapper mapper = new();
        private readonly SolidWorksWindow solidWorks = new();
        private readonly System.Windows.Forms.Timer timer = new() { Interval = 16 };

        public Form1(KeyboardOutput keyboard)
        {
            this.keyboard = keyboard;
            InitializeComponent();
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            try
            {
                if (!gamepad.TryRead(out var pad))
                {
                    mapper.Reset();
                    keyboard.ReleaseAll();
                    SetText(lblController, "Looking for an Xbox controller...");
                    SetText(lblSolidWorks, solidWorks.Check() == FocusState.NotRunning ? "Not running" : "Waiting for controller");
                    SetText(lblKeys, "(none)");
                    return;
                }

                SetText(lblController, $"Connected (player {gamepad.PlayerNumber})");
                var plan = mapper.Map(pad);

                switch (solidWorks.Check())
                {
                    case FocusState.Focused:
                        keyboard.Apply(plan);
                        SetText(lblSolidWorks, "Active: sending keys");
                        break;

                    case FocusState.OurWindowFocused:
                        keyboard.ReleaseAll();
                        // We're in the foreground, so Windows lets us hand focus to SolidWorks.
                        if (!plan.IsEmpty) solidWorks.TryBringToFront();
                        SetText(lblSolidWorks, "Paused: move a stick or click SolidWorks to resume");
                        break;

                    case FocusState.TextFieldFocused:
                        keyboard.ReleaseAll();
                        SetText(lblSolidWorks, "Paused: a text box has focus, click the model");
                        break;

                    case FocusState.OtherAppFocused:
                        keyboard.ReleaseAll();
                        SetText(lblSolidWorks, "Paused: another app has focus");
                        break;

                    case FocusState.NotRunning:
                        keyboard.ReleaseAll();
                        SetText(lblSolidWorks, "Not running");
                        break;
                }

                SetText(lblKeys, keyboard.Held.ToString());
            }
            catch (Exception ex)
            {
                // Never leave keys held down because of an error.
                keyboard.ReleaseEverything();
                mapper.Reset();
                SetText(lblSolidWorks, "Error: " + ex.Message);
            }
        }

        private static void SetText(Label label, string text)
        {
            if (label.Text != text) label.Text = text;
        }

        private void BtnQuit_Click(object? sender, EventArgs e) => Close();

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            timer.Stop();
            keyboard.ReleaseAll();
            base.OnFormClosing(e);
        }
    }
}
