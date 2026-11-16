namespace Xbox_Control
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            var keyboard = new KeyboardOutput();

            // Whatever goes wrong, release every key first so nothing stays stuck down system-wide.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (_, e) =>
            {
                keyboard.ReleaseEverything();
                MessageBox.Show(e.Exception.ToString(), "Xbox Control error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
            };
            AppDomain.CurrentDomain.UnhandledException += (_, _) => keyboard.ReleaseEverything();
            AppDomain.CurrentDomain.ProcessExit += (_, _) => keyboard.ReleaseAll();

            ApplicationConfiguration.Initialize();
            Application.Run(new Form1(keyboard));
        }
    }
}
