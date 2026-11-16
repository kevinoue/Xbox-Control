using System.Collections.Generic;
using Xbox_Control;

// Shared by the add-in (.NET Framework 4.8), the settings app and the tests.

namespace XboxControl.Addin
{
    /// <summary>Analog inputs. Triggers is one axis: RT pushes positive, LT negative.</summary>
    public enum AnalogControl
    {
        LeftStickX,
        LeftStickY,
        RightStickX,
        RightStickY,
        Triggers,
    }

    /// <summary>
    /// Continuous movements. Positive is stick right / stick up / RT:
    /// orbit right, orbit up, forward, right, up, roll clockwise.
    /// </summary>
    public enum AxisAction
    {
        None,
        OrbitHorizontal,
        OrbitVertical,
        MoveForward,
        MoveSideways,
        MoveVertical,
        Roll,
    }

    /// <summary>What a button does. Hold actions move while the button is held.</summary>
    public enum ButtonAction
    {
        None,

        // press
        ZoomToFit,
        ZoomToSelection,
        ViewIsometric,
        ViewFront,
        ViewBack,
        ViewTop,
        ViewBottom,
        ViewLeft,
        ViewRight,
        TogglePerspective,
        TogglePrecision,
        TogglePause,
        OpenSettings,

        // hold
        MoveForward,
        MoveBack,
        MoveLeft,
        MoveRight,
        MoveUp,
        MoveDown,
        OrbitLeft,
        OrbitRight,
        OrbitUp,
        OrbitDown,
        RollLeft,
        RollRight,
    }

    public static class ControlMapping
    {
        /// <summary>Buttons that can be mapped, in display order.</summary>
        public static readonly GamepadButtons[] MappableButtons =
        {
            GamepadButtons.A, GamepadButtons.B, GamepadButtons.X, GamepadButtons.Y,
            GamepadButtons.LeftShoulder, GamepadButtons.RightShoulder,
            GamepadButtons.DPadUp, GamepadButtons.DPadDown, GamepadButtons.DPadLeft, GamepadButtons.DPadRight,
            GamepadButtons.LeftThumb, GamepadButtons.RightThumb,
            GamepadButtons.Back, GamepadButtons.Start,
        };

        public static readonly AnalogControl[] AnalogControls =
        {
            AnalogControl.LeftStickX, AnalogControl.LeftStickY,
            AnalogControl.RightStickX, AnalogControl.RightStickY,
            AnalogControl.Triggers,
        };

        public static Dictionary<AnalogControl, AxisAction> DefaultAxes() => new()
        {
            [AnalogControl.LeftStickX] = AxisAction.MoveSideways,
            [AnalogControl.LeftStickY] = AxisAction.MoveForward,
            [AnalogControl.RightStickX] = AxisAction.OrbitHorizontal,
            [AnalogControl.RightStickY] = AxisAction.OrbitVertical,
            [AnalogControl.Triggers] = AxisAction.MoveVertical,
        };

        public static Dictionary<GamepadButtons, ButtonAction> DefaultButtons() => new()
        {
            [GamepadButtons.A] = ButtonAction.ZoomToFit,
            [GamepadButtons.B] = ButtonAction.ZoomToSelection,
            [GamepadButtons.X] = ButtonAction.TogglePerspective,
            [GamepadButtons.Y] = ButtonAction.ViewIsometric,
            [GamepadButtons.LeftShoulder] = ButtonAction.RollLeft,
            [GamepadButtons.RightShoulder] = ButtonAction.RollRight,
            [GamepadButtons.DPadUp] = ButtonAction.ViewTop,
            [GamepadButtons.DPadDown] = ButtonAction.ViewFront,
            [GamepadButtons.DPadLeft] = ButtonAction.ViewLeft,
            [GamepadButtons.DPadRight] = ButtonAction.ViewRight,
            [GamepadButtons.LeftThumb] = ButtonAction.TogglePrecision,
            [GamepadButtons.RightThumb] = ButtonAction.None,
            [GamepadButtons.Back] = ButtonAction.None,
            [GamepadButtons.Start] = ButtonAction.TogglePause,
        };

        /// <summary>For a hold action: which axis it pushes and which way. (None, 0) for press actions.</summary>
        public static (AxisAction Axis, int Sign) HoldAxis(ButtonAction action) => action switch
        {
            ButtonAction.MoveForward => (AxisAction.MoveForward, 1),
            ButtonAction.MoveBack => (AxisAction.MoveForward, -1),
            ButtonAction.MoveRight => (AxisAction.MoveSideways, 1),
            ButtonAction.MoveLeft => (AxisAction.MoveSideways, -1),
            ButtonAction.MoveUp => (AxisAction.MoveVertical, 1),
            ButtonAction.MoveDown => (AxisAction.MoveVertical, -1),
            ButtonAction.OrbitRight => (AxisAction.OrbitHorizontal, 1),
            ButtonAction.OrbitLeft => (AxisAction.OrbitHorizontal, -1),
            ButtonAction.OrbitUp => (AxisAction.OrbitVertical, 1),
            ButtonAction.OrbitDown => (AxisAction.OrbitVertical, -1),
            ButtonAction.RollRight => (AxisAction.Roll, 1),
            ButtonAction.RollLeft => (AxisAction.Roll, -1),
            _ => (AxisAction.None, 0),
        };

        public static bool IsHold(ButtonAction action) => HoldAxis(action).Axis != AxisAction.None;

        public static string Label(AnalogControl c) => c switch
        {
            AnalogControl.LeftStickX => "Left stick left / right",
            AnalogControl.LeftStickY => "Left stick up / down",
            AnalogControl.RightStickX => "Right stick left / right",
            AnalogControl.RightStickY => "Right stick up / down",
            AnalogControl.Triggers => "Triggers (RT + / LT -)",
            _ => c.ToString(),
        };

        public static string Label(GamepadButtons b) => b switch
        {
            GamepadButtons.LeftShoulder => "LB",
            GamepadButtons.RightShoulder => "RB",
            GamepadButtons.DPadUp => "D-pad up",
            GamepadButtons.DPadDown => "D-pad down",
            GamepadButtons.DPadLeft => "D-pad left",
            GamepadButtons.DPadRight => "D-pad right",
            GamepadButtons.LeftThumb => "Left stick click",
            GamepadButtons.RightThumb => "Right stick click",
            GamepadButtons.Back => "View",
            GamepadButtons.Start => "Menu",
            _ => b.ToString(),
        };

        public static string Label(AxisAction a) => a switch
        {
            AxisAction.None => "Nothing",
            AxisAction.OrbitHorizontal => "Orbit left / right",
            AxisAction.OrbitVertical => "Orbit up / down",
            AxisAction.MoveForward => "Move forward / back",
            AxisAction.MoveSideways => "Move left / right",
            AxisAction.MoveVertical => "Move up / down",
            AxisAction.Roll => "Roll",
            _ => a.ToString(),
        };

        public static string Label(ButtonAction a) => a switch
        {
            ButtonAction.None => "Nothing",
            ButtonAction.ZoomToFit => "Zoom to fit",
            ButtonAction.ZoomToSelection => "Zoom to selection",
            ButtonAction.ViewIsometric => "Isometric view",
            ButtonAction.ViewFront => "Front view",
            ButtonAction.ViewBack => "Back view",
            ButtonAction.ViewTop => "Top view",
            ButtonAction.ViewBottom => "Bottom view",
            ButtonAction.ViewLeft => "Left view",
            ButtonAction.ViewRight => "Right view",
            ButtonAction.TogglePerspective => "Perspective on / off",
            ButtonAction.TogglePrecision => "Precision mode on / off",
            ButtonAction.TogglePause => "Pause controller",
            ButtonAction.OpenSettings => "Open these settings",
            ButtonAction.MoveForward => "Move forward (hold)",
            ButtonAction.MoveBack => "Move back (hold)",
            ButtonAction.MoveLeft => "Move left (hold)",
            ButtonAction.MoveRight => "Move right (hold)",
            ButtonAction.MoveUp => "Move up (hold)",
            ButtonAction.MoveDown => "Move down (hold)",
            ButtonAction.OrbitLeft => "Orbit left (hold)",
            ButtonAction.OrbitRight => "Orbit right (hold)",
            ButtonAction.OrbitUp => "Orbit up (hold)",
            ButtonAction.OrbitDown => "Orbit down (hold)",
            ButtonAction.RollLeft => "Roll left (hold)",
            ButtonAction.RollRight => "Roll right (hold)",
            _ => a.ToString(),
        };
    }
}
