using System;
using System.Windows.Forms;

namespace Xbox_Control
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            // Enable visual styles and set text rendering
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Start the application with Form1
            Application.Run(new Form1());
        }
    }
}
