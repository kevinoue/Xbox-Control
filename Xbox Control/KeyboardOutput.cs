using System.Runtime.InteropServices;

namespace Xbox_Control
{
    /// <summary>Virtual-key codes for the keys the SolidWorks Walk-through uses.</summary>
    public enum Key : ushort
    {
        Shift = 0x10,
        Ctrl = 0x11,
        Alt = 0x12,
        Left = 0x25,
        Up = 0x26,
        Right = 0x27,
        Down = 0x28,
        A = 0x41,
        D = 0x44,
        S = 0x53,
        W = 0x57,
        Z = 0x5A,
        Equals = 0xBB,  // VK_OEM_PLUS  ('=' / '+')
        Minus = 0xBD,   // VK_OEM_MINUS
    }

    [Flags]
    public enum Modifiers
    {
        None = 0,
        Shift = 1,
        Alt = 2,
    }

    /// <summary>The complete set of keys that should be held right now, plus one-shot taps.</summary>
    public sealed record KeyPlan(Modifiers Modifiers, IReadOnlySet<Key> Keys, IReadOnlyList<Key> Taps)
    {
        public static readonly KeyPlan Empty = new(Modifiers.None, new HashSet<Key>(), Array.Empty<Key>());
        public bool IsEmpty => Modifiers == Modifiers.None && Keys.Count == 0 && Taps.Count == 0;

        public override string ToString()
        {
            var parts = new List<string>();
            if (Modifiers.HasFlag(Modifiers.Shift)) parts.Add("Shift");
            if (Modifiers.HasFlag(Modifiers.Alt)) parts.Add("Alt");
            parts.AddRange(Keys.OrderBy(k => k).Select(Name));
            return parts.Count == 0 ? "(none)" : string.Join(" + ", parts);
        }

        public static string Name(Key k) => k switch
        {
            Key.Equals => "=",
            Key.Minus => "-",
            _ => k.ToString(),
        };
    }

    public readonly record struct KeyEvent(Key Key, bool Down);

    /// <summary>
    /// Owns every key this app presses. Each tick it receives the full set of keys that should be
    /// held and sends only the difference, so one action can never release a key another action
    /// still needs (the old Start/Stop pairs did exactly that with Shift, Up, A and D).
    /// Whenever the modifiers change, the other keys are released and re-pressed under the new
    /// modifiers, so SolidWorks never sees a half-changed chord like "Shift released, D still down".
    /// </summary>
    public sealed class KeyboardOutput
    {
        private static readonly Key[] AllKeys = Enum.GetValues<Key>();
        private readonly object gate = new();
        private readonly Action<IReadOnlyList<KeyEvent>> send;
        private readonly HashSet<Key> heldKeys = new();
        private Modifiers heldMods = Modifiers.None;

        public KeyboardOutput() : this(SendKeyEvents) { }

        /// <summary>For tests: capture the events instead of injecting them.</summary>
        public KeyboardOutput(Action<IReadOnlyList<KeyEvent>> sink) => send = sink;

        public KeyPlan Held
        {
            get { lock (gate) return new KeyPlan(heldMods, new HashSet<Key>(heldKeys), Array.Empty<Key>()); }
        }

        public void Apply(KeyPlan plan)
        {
            lock (gate)
            {
                var events = new List<KeyEvent>();

                if (plan.Modifiers != heldMods || plan.Taps.Count > 0)
                {
                    // Clean re-chord: let go of everything, then press the new chord from scratch.
                    ReleaseInto(events);
                    foreach (var tap in plan.Taps)
                    {
                        events.Add(new KeyEvent(tap, true));
                        events.Add(new KeyEvent(tap, false));
                    }
                    PressInto(events, plan.Modifiers, plan.Keys);
                }
                else
                {
                    foreach (var k in heldKeys.Where(k => !plan.Keys.Contains(k)).ToList())
                    {
                        events.Add(new KeyEvent(k, false));
                        heldKeys.Remove(k);
                    }
                    foreach (var k in plan.Keys.Where(k => !heldKeys.Contains(k)))
                    {
                        events.Add(new KeyEvent(k, true));
                        heldKeys.Add(k);
                    }
                }

                if (events.Count > 0) send(events);
            }
        }

        /// <summary>Releases everything this app is holding.</summary>
        public void ReleaseAll()
        {
            lock (gate)
            {
                var events = new List<KeyEvent>();
                ReleaseInto(events);
                if (events.Count > 0) send(events);
            }
        }

        /// <summary>
        /// Last-resort cleanup for crashes and exit: sends key-up for every key this app ever uses,
        /// even if the tracked state is wrong, so nothing is left stuck down system-wide.
        /// </summary>
        public void ReleaseEverything()
        {
            lock (gate)
            {
                heldKeys.Clear();
                heldMods = Modifiers.None;
                try { send(AllKeys.Select(k => new KeyEvent(k, false)).ToList()); }
                catch { /* best effort during shutdown */ }
            }
        }

        private void ReleaseInto(List<KeyEvent> events)
        {
            foreach (var k in heldKeys) events.Add(new KeyEvent(k, false));
            heldKeys.Clear();
            foreach (var m in ModifierKeys(heldMods)) events.Add(new KeyEvent(m, false));
            heldMods = Modifiers.None;
        }

        private void PressInto(List<KeyEvent> events, Modifiers mods, IReadOnlySet<Key> keys)
        {
            foreach (var m in ModifierKeys(mods)) events.Add(new KeyEvent(m, true));
            heldMods = mods;
            foreach (var k in keys)
            {
                events.Add(new KeyEvent(k, true));
                heldKeys.Add(k);
            }
        }

        private static IEnumerable<Key> ModifierKeys(Modifiers mods)
        {
            if (mods.HasFlag(Modifiers.Shift)) yield return Key.Shift;
            if (mods.HasFlag(Modifiers.Alt)) yield return Key.Alt;
        }

        // ---- Win32 SendInput -------------------------------------------------------------

        private static void SendKeyEvents(IReadOnlyList<KeyEvent> events)
        {
            var inputs = new INPUT[events.Count];
            for (int i = 0; i < events.Count; i++)
            {
                var (key, down) = events[i];
                uint flags = down ? 0u : KEYEVENTF_KEYUP;
                if (IsExtended(key)) flags |= KEYEVENTF_EXTENDEDKEY;
                inputs[i] = new INPUT
                {
                    type = INPUT_KEYBOARD,
                    u = new InputUnion
                    {
                        ki = new KEYBDINPUT
                        {
                            wVk = (ushort)key,
                            wScan = (ushort)(MapVirtualKeyW((uint)key, MAPVK_VK_TO_VSC) & 0xFF),
                            dwFlags = flags,
                        },
                    },
                };
            }
            // One SendInput call = one atomic batch; nothing else can interleave mid-chord.
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        }

        // Arrow keys live on the extended part of the keyboard; without this flag they arrive
        // as numpad keys, which behave differently when NumLock or Shift is involved.
        private static bool IsExtended(Key k) => k is Key.Left or Key.Up or Key.Right or Key.Down;

        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint MAPVK_VK_TO_VSC = 0;

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion u;
        }

        // The union must include MOUSEINPUT so INPUT has the size Windows expects (40 bytes on x64).
        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx, dy;
            public uint mouseData, dwFlags, time;
            public UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKeyW(uint uCode, uint uMapType);
    }
}
