using System;
using System.Collections.Generic;
using System.Linq;
using Xbox_Control;

namespace XboxControl.Addin
{
    /// <summary>
    /// View motion for one tick. Angles are radians about the screen axes (right-handed:
    /// +Pitch tips the model's front down, +Yaw swings its front right, +Roll is counter-clockwise),
    /// pan is in view heights (+X moves the model right, +Y up), zoom is a factor (&gt;1 zooms in).
    /// </summary>
    public readonly record struct ViewMotion(double Pitch, double Yaw, double Roll, double PanX, double PanY, double Zoom)
    {
        public static readonly ViewMotion None = new(0, 0, 0, 0, 0, 1);
        public bool HasRotation => Pitch != 0 || Yaw != 0;
        public bool HasRoll => Roll != 0;
        public bool HasPan => PanX != 0 || PanY != 0;
        public bool HasZoom => Zoom != 1;
        public bool IsNone => !HasRotation && !HasRoll && !HasPan && !HasZoom;
    }

    /// <summary>
    /// Turns controller state into view motion using the mapping in <see cref="NavigationSettings"/>.
    /// Pure logic, no SolidWorks calls, so it is unit-tested.
    ///
    /// Orbit turns the model (it follows the stick, like dragging it with the mouse); move
    /// actions move the camera (move right = the model slides left), as in a walk-through.
    /// Several controls may drive the same movement; their values add up (clamped to full speed).
    /// </summary>
    public sealed class OrbitNavigator
    {
        /// <summary>Longest step applied at once, so a SolidWorks hiccup doesn't cause a jump.</summary>
        public const double MaxStepSeconds = 0.1;

        private const GamepadButtons AllButtons = (GamepadButtons)0xFFFF;
        private static readonly int AxisCount = Enum.GetValues(typeof(AxisAction)).Length;

        private readonly double[] axis = new double[AxisCount];
        private (AnalogControl Control, AxisAction Action, double Sign)[] analogMap = Array.Empty<(AnalogControl, AxisAction, double)>();
        private (GamepadButtons Button, AxisAction Action, int Sign)[] holdMap = Array.Empty<(GamepadButtons, AxisAction, int)>();
        private (GamepadButtons Button, ButtonAction Action)[] pressMap = Array.Empty<(GamepadButtons, ButtonAction)>();

        // Treat everything as already held at start / after a reset, so a button that is down
        // when the controller connects or SolidWorks regains focus doesn't fire.
        private GamepadButtons previous = AllButtons;

        public OrbitNavigator(NavigationSettings settings)
        {
            Settings = settings;
            ApplySettings(settings);
        }

        public NavigationSettings Settings { get; private set; }
        public bool Precision { get; private set; }
        public bool Paused { get; private set; }

        /// <summary>Switch to new settings (after the file changed). Keeps pause/precision state.</summary>
        public void ApplySettings(NavigationSettings settings)
        {
            Settings = settings;
            analogMap = ControlMapping.AnalogControls
                .Where(c => settings.Axes[c] != AxisAction.None)
                .Select(c => (c, settings.Axes[c], settings.Invert[c] ? -1.0 : 1.0))
                .ToArray();
            holdMap = ControlMapping.MappableButtons
                .Select(b => (Button: b, Hold: ControlMapping.HoldAxis(settings.Buttons[b])))
                .Where(x => x.Hold.Axis != AxisAction.None)
                .Select(x => (x.Button, x.Hold.Axis, x.Hold.Sign))
                .ToArray();
            pressMap = ControlMapping.MappableButtons
                .Where(b => settings.Buttons[b] != ButtonAction.None && !ControlMapping.IsHold(settings.Buttons[b]))
                .Select(b => (b, settings.Buttons[b]))
                .ToArray();

            // Don't leave the user stuck in a mode they can no longer switch off.
            if (!pressMap.Any(p => p.Action == ButtonAction.TogglePause)) Paused = false;
            if (!pressMap.Any(p => p.Action == ButtonAction.TogglePrecision)) Precision = false;
        }

        public void Reset() => previous = AllButtons;

        /// <summary>Motion for this tick; button presses are appended to <paramref name="commands"/>.</summary>
        public ViewMotion Update(GamepadSnapshot pad, double dt, List<ButtonAction> commands)
        {
            var pressed = pad.Buttons & ~previous;
            previous = pad.Buttons;

            foreach (var (button, action) in pressMap)
            {
                if ((pressed & button) == 0) continue;
                if (Paused && action != ButtonAction.TogglePause) continue; // only "resume" works while paused
                if (action == ButtonAction.TogglePause) Paused = !Paused;
                if (action == ButtonAction.TogglePrecision) Precision = !Precision;
                commands.Add(action);
            }
            if (Paused) return ViewMotion.None;

            var s = Settings;
            double k = Math.Max(0, Math.Min(dt, MaxStepSeconds)) * (Precision ? s.PrecisionScale : 1);
            if (k == 0) return ViewMotion.None;

            Array.Clear(axis, 0, axis.Length);
            foreach (var (control, action, sign) in analogMap)
                axis[(int)action] += Curve(Read(pad, control)) * sign;
            foreach (var (button, action, sign) in holdMap)
                if (pad.IsDown(button)) axis[(int)action] += sign;

            double rotate = s.RotateSpeedDeg * Math.PI / 180 * k;
            double yaw = Axis(AxisAction.OrbitHorizontal) * rotate;
            double pitch = -Axis(AxisAction.OrbitVertical) * rotate;                   // orbit up = front up
            double roll = -Axis(AxisAction.Roll) * s.RollSpeedDeg * Math.PI / 180 * k; // + = clockwise
            double panX = -Axis(AxisAction.MoveSideways) * s.PanSpeed * k;             // camera right = model left
            double panY = -Axis(AxisAction.MoveVertical) * s.PanSpeed * k;             // camera up = model down
            double forward = Axis(AxisAction.MoveForward);
            double zoom = forward == 0 ? 1 : Math.Pow(Math.Max(1.01, s.ZoomSpeed), forward * k);

            return new ViewMotion(pitch, yaw, roll, panX, panY, zoom);
        }

        private double Axis(AxisAction a) => Math.Max(-1, Math.Min(1, axis[(int)a]));

        private static double Read(GamepadSnapshot pad, AnalogControl control) => control switch
        {
            AnalogControl.LeftStickX => pad.LeftX,
            AnalogControl.LeftStickY => pad.LeftY,
            AnalogControl.RightStickX => pad.RightX,
            AnalogControl.RightStickY => pad.RightY,
            AnalogControl.Triggers => pad.RightTrigger - pad.LeftTrigger,
            _ => 0,
        };

        /// <summary>Blend of linear and cubic: fine control near the center, full speed at the edge.</summary>
        internal double Curve(double x)
        {
            double e = Math.Max(0, Math.Min(1, Settings.Expo));
            double a = Math.Abs(x);
            return Math.Sign(x) * ((1 - e) * a + e * a * a * a);
        }
    }
}
