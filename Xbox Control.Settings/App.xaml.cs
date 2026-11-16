using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;

namespace XboxControl.Settings;

public partial class App : Application
{
    private Mutex? singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // One window at a time: a second launch (e.g. the Tools menu again) brings the first forward.
        singleInstance = new Mutex(true, @"Local\XboxControlSettings", out bool first);
        if (!first)
        {
            var me = Process.GetCurrentProcess();
            var other = Process.GetProcessesByName(me.ProcessName).FirstOrDefault(p => p.Id != me.Id);
            if (other != null && other.MainWindowHandle != IntPtr.Zero)
            {
                ShowWindow(other.MainWindowHandle, 9 /* SW_RESTORE */);
                SetForegroundWindow(other.MainWindowHandle);
            }
            Shutdown();
            return;
        }

        new MainWindow().Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        singleInstance?.Dispose();
        base.OnExit(e);
    }

    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int cmd);
}
