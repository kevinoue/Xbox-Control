using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Xbox_Control
{
    public enum FocusState
    {
        NotRunning,
        Focused,            // a SolidWorks window has focus: keys are safe to send
        TextFieldFocused,   // SolidWorks has focus, but inside a text box: keys would be typed
        OurWindowFocused,   // this app has focus: we may hand focus to SolidWorks
        OtherAppFocused,    // some other app has focus: never send keys
    }

    /// <summary>
    /// Finds the running SolidWorks process and reports where keyboard focus is. Keys are only
    /// sent when SolidWorks itself has focus; the old code sent them blind after a
    /// SetForegroundWindow call that Windows often ignores, so keys could land in other apps.
    /// Uses the process and its windows only (no COM), so a busy SolidWorks can't stall the loop.
    /// </summary>
    public sealed class SolidWorksWindow
    {
        private static readonly TimeSpan LookupInterval = TimeSpan.FromSeconds(2);
        private readonly uint ourPid = (uint)Environment.ProcessId;
        private Process? process;
        private DateTime nextLookup = DateTime.MinValue;

        public bool IsRunning => Alive(process);

        public FocusState Check()
        {
            if (!EnsureProcess()) return FocusState.NotRunning;

            IntPtr fg = GetForegroundWindow();
            if (fg == IntPtr.Zero) return FocusState.OtherAppFocused;

            uint threadId = GetWindowThreadProcessId(fg, out uint pid);
            if (pid == ourPid) return FocusState.OurWindowFocused;
            if (pid != (uint)process!.Id) return FocusState.OtherAppFocused;

            // Inside SolidWorks: is the caret in a text field (PropertyManager box, rename, etc.)?
            var info = new GUITHREADINFO { cbSize = Marshal.SizeOf<GUITHREADINFO>() };
            if (GetGUIThreadInfo(threadId, ref info) && info.hwndFocus != IntPtr.Zero)
            {
                var cls = new StringBuilder(64);
                GetClassNameW(info.hwndFocus, cls, cls.Capacity);
                var name = cls.ToString();
                if (name.Contains("Edit", StringComparison.OrdinalIgnoreCase)) return FocusState.TextFieldFocused;
            }
            return FocusState.Focused;
        }

        /// <summary>
        /// Gives focus to SolidWorks' main window. Windows only allows this while our own window
        /// is in the foreground, which is the only time the caller uses it.
        /// </summary>
        public bool TryBringToFront()
        {
            if (!EnsureProcess()) return false;
            process!.Refresh();
            IntPtr hwnd = process.MainWindowHandle;
            if (hwnd == IntPtr.Zero) return false;
            if (IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);
            return SetForegroundWindow(hwnd);
        }

        private bool EnsureProcess()
        {
            if (Alive(process)) return true;
            process?.Dispose();
            process = null;
            if (DateTime.UtcNow < nextLookup) return false;
            nextLookup = DateTime.UtcNow + LookupInterval;
            process = Process.GetProcessesByName("SLDWORKS").FirstOrDefault();
            return process != null;
        }

        private static bool Alive(Process? p)
        {
            if (p == null) return false;
            try { return !p.HasExited; }
            catch { return true; } // access denied (e.g. SolidWorks elevated): assume still running
        }

        private const int SW_RESTORE = 9;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct GUITHREADINFO
        {
            public int cbSize;
            public uint flags;
            public IntPtr hwndActive, hwndFocus, hwndCapture, hwndMenuOwner, hwndMoveSize, hwndCaret;
            public RECT rcCaret;
        }

        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
        [DllImport("user32.dll")] private static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO info);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassNameW(IntPtr hWnd, StringBuilder name, int maxCount);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    }
}
