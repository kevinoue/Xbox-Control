using System;
using System.Collections.Generic;
using Xbox_Control;

namespace XboxControl.Addin
{
    public enum ViewCommand
    {
        ZoomToFit,
        ZoomToSelection,
        Isometric,
        Front,
        Top,
        Left,
        Right,
        TogglePerspective,
        TogglePrecision,
        TogglePause,
    }

    /// <summary>
    /// View motion for one tick. Angles are radians about the screen axes (right-handed:
    /// +Pitch tips the model's front down, +Yaw swings its front right, +Roll is counter-clockwise),
    /// pan is in view heights (+X right, +Y up), zoom is a factor (&gt;1 zooms in).
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
    /// Turns controller state into view motion. Pure logic, no SolidWorks calls, so it is unit-tested.
    /// The right stick turns the model (it follows the stick, like dragging it with the mouse); the
    /// left stick and triggers move the camera, as in a walk-through.
    ///
    ///   Right stick  orbit            Left stick   forward / back (zoom), left / right
    ///   RT / LT      up / down        LB / RB      roll left / right
    ///   A            zoom to fit      B            zoom to selection
    ///   Y            isometric
    ///   D-pad        top / front / left / right views
    ///   X            perspective on/off
    ///   Left stick click  precision (slow) on/off
    ///   Start        pause / resume the controller
    /// </summary>
    public sealed class OrbitNavigator
    {
        /// <summary>Longest step applied at once, so a SolidWorks hiccup doesn't cause a jump.</summary>
        public const double MaxStepSeconds = 0.1;

        private const GamepadButtons AllButtons = (GamepadButtons)0xFFFF;

        private static readonly (GamepadButtons Button, ViewCommand Command)[] ButtonCommands =
        {
            (GamepadButtons.A, ViewCommand.ZoomToFit),
            (GamepadButtons.B, ViewCommand.ZoomToSelection),
            (GamepadButtons.Y, ViewCommand.Isometric),
            (GamepadButtons.X, ViewCommand.TogglePerspective),
            (GamepadButtons.DPadUp, ViewCommand.Top),
            (GamepadButtons.DPadDown, ViewCommand.Front),
            (GamepadButtons.DPadLeft, ViewCommand.Left),
            (GamepadButtons.DPadRight, ViewCommand.Right),
        };

        // Treat everything as already held at start / after a reset, so a button that is down
        // when the controller connects or SolidWorks regains focus doesn't fire.
        private GamepadButtons previous = AllButtons;

        public OrbitNavigator(NavigationSettings settings) => Settings = settings;

        public NavigationSettings Settings { get; }
        public bool Precision { get; private set; }
        public bool Paused { get; private set; }

        public void Reset() => previous = AllButtons;

        /// <summary>Motion for this tick; button presses are appended to <paramref name="commands"/>.</summary>
        public ViewMotion Update(GamepadSnapshot pad, double dt, List<ViewCommand> commands)
        {
            var pressed = pad.Buttons & ~previous;
            previous = pad.Buttons;

            if ((pressed & GamepadButtons.Start) != 0)
            {
                Paused = !Paused;
                commands.Add(ViewCommand.TogglePause);
            }
            if (Paused) return ViewMotion.None;

            if ((pressed & GamepadButtons.LeftThumb) != 0)
            {
                Precision = !Precision;
                commands.Add(ViewCommand.TogglePrecision);
            }
            foreach (var (button, command) in ButtonCommands)
                if ((pressed & button) != 0) commands.Add(command);

            var s = Settings;
            double k = Math.Max(0, Math.Min(dt, MaxStepSeconds)) * (Precision ? s.PrecisionScale : 1);
            if (k == 0) return ViewMotion.None;

            double rotate = s.RotateSpeedDeg * Math.PI / 180 * k;
            double yaw = Curve(pad.RightX) * rotate * Sign(s.InvertOrbitX);
            double pitch = -Curve(pad.RightY) * rotate * Sign(s.InvertOrbitY); // stick up = front up
            double roll = ((pad.IsDown(GamepadButtons.LeftShoulder) ? 1 : 0) - (pad.IsDown(GamepadButtons.RightShoulder) ? 1 : 0))
                          * s.RollSpeedDeg * Math.PI / 180 * k;
            // The camera moves, so the model moves the opposite way on screen.
            double panX = -Curve(pad.LeftX) * s.PanSpeed * k * Sign(s.InvertPanX);
            double panY = -(Curve(pad.RightTrigger) - Curve(pad.LeftTrigger)) * s.PanSpeed * k * Sign(s.InvertPanY);
            double z = Curve(pad.LeftY) * Sign(s.InvertForward); // stick up = forward = zoom in
            double zoom = z == 0 ? 1 : Math.Pow(Math.Max(1.01, s.ZoomSpeed), z * k);

            return new ViewMotion(pitch, yaw, roll, panX, panY, zoom);
        }

        /// <summary>Blend of linear and cubic: fine control near the center, full speed at the edge.</summary>
        internal double Curve(double x)
        {
            double e = Math.Max(0, Math.Min(1, Settings.Expo));
            double a = Math.Abs(x);
            return Math.Sign(x) * ((1 - e) * a + e * a * a * a);
        }

        private static double Sign(bool invert) => invert ? -1 : 1;
    }
}
