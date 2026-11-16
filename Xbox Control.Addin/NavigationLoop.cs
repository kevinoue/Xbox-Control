using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using SolidWorks.Interop.sldworks;
using Xbox_Control;
using WinFormsTimer = System.Windows.Forms.Timer;

namespace XboxControl.Addin
{
    /// <summary>
    /// Polls the controller on SolidWorks' UI thread and moves the active view. A WinForms timer
    /// is used so every API call happens on the thread SolidWorks expects, between its own
    /// messages; while SolidWorks is busy (rebuilding, opening) the timer simply waits.
    /// </summary>
    internal sealed class NavigationLoop : IDisposable
    {
        private readonly ISldWorks sw;
        private readonly XInputGamepad pad = new();
        private readonly OrbitNavigator navigator;
        private readonly ViewDriver driver;
        private readonly WinFormsTimer timer;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly List<ButtonAction> commands = new();
        private readonly string settingsPath;
        private readonly Action openSettings;
        private DateTime settingsStamp;
        private double nextSettingsCheck;
        private readonly int processId = Process.GetCurrentProcess().Id;
        private double lastTick;
        private string status = "";
        private int consecutiveErrors;
        private bool ticking;
        private SelfTest? selfTest;
        private readonly Queue<GamepadButtons> injected = new();
        private string? flash;
        private double flashUntil;

        public NavigationLoop(ISldWorks sw, string settingsPath, Action openSettings)
        {
            this.sw = sw;
            this.settingsPath = settingsPath;
            this.openSettings = openSettings;
            var settings = NavigationSettings.LoadOrCreate(settingsPath);
            settingsStamp = File.GetLastWriteTimeUtc(settingsPath);
            navigator = new OrbitNavigator(settings);
            driver = new ViewDriver { BatchRedraw = settings.BatchRedraw };
            timer = new WinFormsTimer { Interval = Math.Max(1, settings.TickMs) };
            timer.Tick += OnTick;
            lastTick = clock.Elapsed.TotalSeconds;
            timer.Start();
        }

        public string Status => status;
        public string SelfTestResult { get; private set; } = "";

        public void Dispose()
        {
            timer.Stop();
            timer.Tick -= OnTick;
            timer.Dispose();
            if (selfTest != null) FinishSelfTest("Self-test stopped: add-in unloaded");
        }

        private void OnTick(object? sender, EventArgs e)
        {
            // SolidWorks pumps messages during some calls (view-change animations), which would
            // run the next tick inside this one.
            if (ticking) return;
            ticking = true;
            try
            {
                Tick();
                consecutiveErrors = 0;
            }
            catch (Exception ex)
            {
                // Nothing may escape into SolidWorks. Races such as a document closing mid-tick
                // land here; log the first few and keep going.
                if (consecutiveErrors++ < 5) Log.Error("tick", ex);
                if (selfTest != null) FinishSelfTest($"Self-test stopped: {ex.Message}");
            }
            finally
            {
                ticking = false;
            }
        }

        private void Tick()
        {
            double now = clock.Elapsed.TotalSeconds;
            double dt = now - lastTick;
            lastTick = now;
            if (now >= nextSettingsCheck)
            {
                nextSettingsCheck = now + 1;
                ReloadSettingsIfChanged(now);
            }

            GamepadSnapshot input;
            OrbitNavigator nav = navigator;
            if (selfTest != null)
            {
                double t = now - selfTest.Start;
                if (t >= selfTest.Seconds) { FinishSelfTest(null); return; }
                input = selfTest.Input(t);
                nav = selfTest.Navigator;
            }
            else if (injected.Count > 0)
            {
                // Simulated button press (diagnostics): bypasses the controller and focus checks.
                input = new GamepadSnapshot(0, 0, 0, 0, 0, 0, injected.Dequeue());
            }
            else
            {
                if (!pad.TryRead(out input))
                {
                    navigator.Reset();
                    SetStatus("Xbox controller: not connected");
                    return;
                }
                if (!SolidWorksHasFocus())
                {
                    navigator.Reset(); // a button held while switching windows doesn't fire on return
                    return;
                }
            }

            commands.Clear();
            var motion = nav.Update(input, dt, commands);
            if (commands.Remove(ButtonAction.OpenSettings))
            {
                openSettings(); // works with no document open
                Flash("Opening settings", now);
            }
            if (selfTest == null) SetStatus(Describe());
            if (commands.Count == 0 && motion.IsNone) return;

            if ((object)sw.ActiveDoc is not IModelDoc2 doc || (object)doc.ActiveView is not IModelView view) return;

            foreach (var command in commands)
            {
                var message = driver.Run(doc, view, command);
                if (message != null) Flash(message, now);
            }

            long start = Stopwatch.GetTimestamp();
            driver.Apply(doc, view, motion);
            selfTest?.Record(dt, (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency);
        }

        private string Describe()
        {
            if (navigator.Paused)
            {
                var resume = navigator.Settings.Buttons.FirstOrDefault(kv => kv.Value == ButtonAction.TogglePause).Key;
                return $"Xbox controller {pad.PlayerNumber}: paused (press {ControlMapping.Label(resume)} to resume)";
            }
            string text = $"Xbox controller {pad.PlayerNumber}: ready{(navigator.Precision ? ", precision mode" : "")}";
            return flash != null && clock.Elapsed.TotalSeconds < flashUntil ? $"{text}  |  {flash}" : text;
        }

        /// <summary>Picks up changes from the settings app (or a hand edit) without a restart.</summary>
        private void ReloadSettingsIfChanged(double now)
        {
            try
            {
                var stamp = File.GetLastWriteTimeUtc(settingsPath);
                if (stamp == settingsStamp) return;
                var settings = NavigationSettings.LoadOrCreate(settingsPath);
                settingsStamp = stamp;
                navigator.ApplySettings(settings);
                driver.BatchRedraw = settings.BatchRedraw;
                timer.Interval = Math.Max(1, settings.TickMs);
                Flash("Settings updated", now);
                Log.Info("Settings reloaded");
            }
            catch (IOException)
            {
                // Being written right now; try again next second.
            }
        }

        /// <summary>Shows what a button did in the status bar for a couple of seconds.</summary>
        private void Flash(string message, double now)
        {
            flash = message;
            flashUntil = now + 2.5;
            if (selfTest == null) SetStatus(Describe());
        }

        /// <summary>Diagnostics: press and release buttons (comma-separated GamepadButtons names).</summary>
        public string PressButtons(string names)
        {
            if (!Enum.TryParse(names, true, out GamepadButtons buttons) || buttons == GamepadButtons.None)
                return $"unknown buttons '{names}'";
            injected.Enqueue(GamepadButtons.None); // release first, so the press is a new press
            injected.Enqueue(buttons);
            injected.Enqueue(GamepadButtons.None);
            return "queued";
        }

        private void SetStatus(string text)
        {
            if (text == status) return;
            status = text;
            try { ((IFrame)(object)sw.Frame()).SetStatusBarText(text); }
            catch (COMException) { /* no frame yet */ }
        }

        private bool SolidWorksHasFocus()
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return false;
            GetWindowThreadProcessId(hwnd, out uint pid);
            return pid == processId;
        }

        // ---- self-test: synthetic stick input through the real timer loop, then view restored ----

        public string StartSelfTest(string scenario, double seconds, bool batchRedraw)
        {
            if (selfTest != null) return "already running";
            if ((object)sw.ActiveDoc is not IModelDoc2 doc || (object)doc.ActiveView is not IModelView view) return "no active document";
            driver.BatchRedraw = batchRedraw;
            selfTest = new SelfTest(scenario, Math.Max(1, seconds), clock.Elapsed.TotalSeconds,
                ViewState.Capture(view), new OrbitNavigator(new NavigationSettings()), batchRedraw); // default mapping
            SelfTestResult = "";
            return "started";
        }

        private void FinishSelfTest(string? error)
        {
            var test = selfTest!;
            selfTest = null;
            driver.BatchRedraw = navigator.Settings.BatchRedraw;
            try
            {
                if ((object)sw.ActiveDoc is IModelDoc2 doc && (object)doc.ActiveView is IModelView view)
                    test.Saved.Restore(view, (IMathUtility)(object)sw.GetMathUtility());
            }
            catch (Exception ex) { Log.Error("self-test restore", ex); }
            SelfTestResult = error ?? test.Report();
        }

        private sealed class SelfTest
        {
            private readonly string scenario;
            private readonly bool batch;
            private readonly List<double> intervals = new();
            private readonly List<double> applyMs = new();

            public SelfTest(string scenario, double seconds, double start, ViewState saved, OrbitNavigator navigator, bool batch)
            {
                this.scenario = scenario;
                this.batch = batch;
                Seconds = seconds;
                Start = start;
                Saved = saved;
                Navigator = navigator;
            }

            public double Seconds { get; }
            public double Start { get; }
            public ViewState Saved { get; }
            public OrbitNavigator Navigator { get; }

            /// <summary>Push one way for the first half, the other way for the second.</summary>
            public GamepadSnapshot Input(double t)
            {
                bool first = t < Seconds / 2;
                float d = first ? 1 : -1;
                float rt = first ? 0.5f : 0, lt = first ? 0 : 0.5f;
                return scenario switch
                {
                    "rotate" => new GamepadSnapshot(0, 0, 0.8f * d, 0, 0, 0, GamepadButtons.None),
                    "pan" => new GamepadSnapshot(0.8f * d, 0, 0, 0, 0, 0, GamepadButtons.None),
                    "zoom" => new GamepadSnapshot(0, 0.8f * d, 0, 0, 0, 0, GamepadButtons.None),
                    _ => new GamepadSnapshot(0.4f * d, 0.4f * d, 0.7f * d, 0.3f * d, lt, rt, GamepadButtons.None),
                };
            }

            public void Record(double dtSeconds, double ms)
            {
                intervals.Add(dtSeconds * 1000);
                applyMs.Add(ms);
            }

            public string Report()
            {
                if (applyMs.Count < 2) return "Self-test: no ticks recorded";
                // Skip the first tick: its interval includes the time before the test started.
                var iv = intervals.Skip(1).OrderBy(x => x).ToList();
                var ap = applyMs.Skip(1).OrderBy(x => x).ToList();
                var inv = CultureInfo.InvariantCulture;
                var sb = new StringBuilder();
                sb.AppendLine(string.Format(inv, "In-process self-test: scenario={0}, batch redraw={1}, {2:F1} s, {3} ticks",
                    scenario, batch, Seconds, ap.Count));
                sb.AppendLine(string.Format(inv, "  view update per tick : avg {0:F2} ms, p95 {1:F2} ms, max {2:F2} ms",
                    ap.Average(), P95(ap), ap[ap.Count - 1]));
                sb.AppendLine(string.Format(inv, "  tick interval        : avg {0:F2} ms, p95 {1:F2} ms, max {2:F2} ms  (~{3:F0} updates/s)",
                    iv.Average(), P95(iv), iv[iv.Count - 1], 1000 / iv.Average()));
                return sb.ToString();
            }

            private static double P95(List<double> sorted) =>
                sorted[Math.Max(0, Math.Min(sorted.Count - 1, (int)Math.Ceiling(sorted.Count * 0.95) - 1))];
        }

        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    }
}
