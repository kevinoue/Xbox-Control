using System.Runtime.InteropServices;

namespace Xbox_Control
{
    /// <summary>Normalized controller state. Sticks are -1..1 (after deadzone), triggers 0..1.</summary>
    public readonly record struct GamepadSnapshot(
        float LeftX, float LeftY,
        float RightX, float RightY,
        float LeftTrigger, float RightTrigger,
        GamepadButtons Buttons)
    {
        public static readonly GamepadSnapshot Neutral = default;
        public bool IsDown(GamepadButtons b) => (Buttons & b) == b;
    }

    [Flags]
    public enum GamepadButtons : ushort
    {
        None = 0,
        DPadUp = 0x0001,
        DPadDown = 0x0002,
        DPadLeft = 0x0004,
        DPadRight = 0x0008,
        Start = 0x0010,
        Back = 0x0020,
        LeftThumb = 0x0040,
        RightThumb = 0x0080,
        LeftShoulder = 0x0100,
        RightShoulder = 0x0200,
        A = 0x1000,
        B = 0x2000,
        X = 0x4000,
        Y = 0x8000,
    }

    /// <summary>
    /// Xbox controller input through XInput directly (xinput1_4.dll ships with Windows 10/11).
    /// Replaces SharpDX, which is unmaintained and throws when the controller disconnects.
    /// Scans all four slots, so the pad is found no matter which slot Windows assigns it,
    /// and reports disconnects instead of throwing.
    /// </summary>
    public sealed class XInputGamepad
    {
        private const uint ERROR_SUCCESS = 0;
        private const float StickDeadzone = 0.18f;   // radial, fraction of full deflection
        private const float TriggerDeadzone = 0.08f;
        private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(1);

        private int slot = -1;
        private DateTime nextScan = DateTime.MinValue;

        public bool IsConnected => slot >= 0;
        /// <summary>1-based slot number for display, or 0 when not connected.</summary>
        public int PlayerNumber => slot + 1;

        /// <summary>
        /// Polls the controller. Returns false (and a neutral snapshot) when no controller is connected.
        /// Probing empty slots is slow in XInput, so scanning is throttled to once per second.
        /// </summary>
        public bool TryRead(out GamepadSnapshot snapshot)
        {
            snapshot = GamepadSnapshot.Neutral;

            if (slot < 0)
            {
                if (DateTime.UtcNow < nextScan) return false;
                nextScan = DateTime.UtcNow + ScanInterval;
                for (int i = 0; i < 4; i++)
                {
                    if (XInputGetState((uint)i, out var probe) == ERROR_SUCCESS)
                    {
                        slot = i;
                        snapshot = Normalize(probe.Gamepad);
                        return true;
                    }
                }
                return false;
            }

            if (XInputGetState((uint)slot, out var state) != ERROR_SUCCESS)
            {
                slot = -1;               // disconnected (battery, sleep, cable) -> go back to scanning
                nextScan = DateTime.MinValue;
                return false;
            }

            snapshot = Normalize(state.Gamepad);
            return true;
        }

        private static GamepadSnapshot Normalize(XINPUT_GAMEPAD g)
        {
            var (lx, ly) = RadialDeadzone(g.sThumbLX, g.sThumbLY);
            var (rx, ry) = RadialDeadzone(g.sThumbRX, g.sThumbRY);
            return new GamepadSnapshot(
                lx, ly, rx, ry,
                TriggerValue(g.bLeftTrigger), TriggerValue(g.bRightTrigger),
                (GamepadButtons)g.wButtons);
        }

        /// <summary>
        /// Round deadzone with rescaling: small drift is ignored in every direction, and the usable
        /// range is stretched back to 0..1 so there's no jump at the edge of the deadzone.
        /// </summary>
        internal static (float x, float y) RadialDeadzone(short rawX, short rawY)
        {
            float x = Math.Clamp(rawX / 32767f, -1f, 1f);
            float y = Math.Clamp(rawY / 32767f, -1f, 1f);
            float mag = MathF.Sqrt(x * x + y * y);
            if (mag <= StickDeadzone) return (0f, 0f);
            float scaled = Math.Min(1f, (mag - StickDeadzone) / (1f - StickDeadzone));
            return (x / mag * scaled, y / mag * scaled);
        }

        private static float TriggerValue(byte raw)
        {
            float v = raw / 255f;
            return v <= TriggerDeadzone ? 0f : (v - TriggerDeadzone) / (1f - TriggerDeadzone);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_GAMEPAD
        {
            public ushort wButtons;
            public byte bLeftTrigger;
            public byte bRightTrigger;
            public short sThumbLX;
            public short sThumbLY;
            public short sThumbRX;
            public short sThumbRY;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_STATE
        {
            public uint dwPacketNumber;
            public XINPUT_GAMEPAD Gamepad;
        }

        [DllImport("xinput1_4.dll")]
        private static extern uint XInputGetState(uint dwUserIndex, out XINPUT_STATE pState);
    }
}
