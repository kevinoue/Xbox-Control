using Xbox_Control;
using XboxControl.Addin;

namespace Xbox_Control.Tests;

public class OrbitNavigatorTests
{
    private const double Deg = Math.PI / 180;
    private readonly List<ButtonAction> commands = new();

    private static GamepadSnapshot Pad(float lx = 0, float ly = 0, float rx = 0, float ry = 0,
        float lt = 0, float rt = 0, GamepadButtons buttons = GamepadButtons.None) => new(lx, ly, rx, ry, lt, rt, buttons);

    /// <summary>Navigator that has already seen one neutral tick, so buttons register as presses.</summary>
    private OrbitNavigator Ready(NavigationSettings? settings = null)
    {
        var nav = new OrbitNavigator(settings ?? new NavigationSettings());
        nav.Update(Pad(), 0.016, commands);
        commands.Clear();
        return nav;
    }

    private ViewMotion Step(OrbitNavigator nav, GamepadSnapshot pad, double dt = 0.05)
    {
        commands.Clear();
        return nav.Update(pad, dt, commands);
    }

    // ---- default layout -----------------------------------------------------------------

    [Fact]
    public void Neutral_input_does_nothing()
    {
        var m = Step(Ready(), Pad());
        Assert.True(m.IsNone);
        Assert.Empty(commands);
    }

    [Fact]
    public void Right_stick_right_swings_the_front_right_at_the_configured_speed()
    {
        var m = Step(Ready(), Pad(rx: 1), dt: 0.05);
        Assert.Equal(160 * Deg * 0.05, m.Yaw, 9);
        Assert.Equal(0, m.Pitch);
    }

    [Fact]
    public void Right_stick_up_tips_the_front_up_and_invert_flips_it()
    {
        Assert.True(Step(Ready(), Pad(ry: 1)).Pitch < 0);
        var inverted = new NavigationSettings();
        inverted.Invert[AnalogControl.RightStickY] = true;
        Assert.True(Step(Ready(inverted), Pad(ry: 1)).Pitch > 0);
    }

    [Fact]
    public void Left_stick_sideways_moves_the_camera_in_view_heights_per_second()
    {
        // Stick right moves the camera right, so the model moves left on screen.
        var m = Step(Ready(), Pad(lx: 1), dt: 0.1);
        Assert.Equal(-0.1, m.PanX, 9);
        Assert.Equal(0, m.PanY);
    }

    [Fact]
    public void Left_stick_forward_and_back_zooms_in_and_out()
    {
        var nav = Ready();
        Assert.Equal(Math.Pow(3, 0.1), Step(nav, Pad(ly: 1), dt: 0.1).Zoom, 9);
        Assert.Equal(Math.Pow(3, -0.1), Step(nav, Pad(ly: -1), dt: 0.1).Zoom, 9);
        Assert.Equal(0, Step(nav, Pad(ly: 1)).PanY);
    }

    [Fact]
    public void Triggers_move_the_camera_up_and_down_and_cancel_when_both_pulled()
    {
        var nav = Ready();
        Assert.Equal(-0.1, Step(nav, Pad(rt: 1), dt: 0.1).PanY, 9); // camera up, model down
        Assert.Equal(0.1, Step(nav, Pad(lt: 1), dt: 0.1).PanY, 9);
        Assert.Equal(0, Step(nav, Pad(lt: 1, rt: 1)).PanY);
        Assert.Equal(1, Step(nav, Pad(rt: 1)).Zoom);
    }

    [Fact]
    public void Bumpers_roll_left_and_right()
    {
        var nav = Ready();
        Assert.True(Step(nav, Pad(buttons: GamepadButtons.LeftShoulder)).Roll > 0);  // counter-clockwise
        Assert.True(Step(nav, Pad(buttons: GamepadButtons.RightShoulder)).Roll < 0);
        Assert.Equal(0, Step(nav, Pad(buttons: GamepadButtons.LeftShoulder | GamepadButtons.RightShoulder)).Roll);
    }

    [Fact]
    public void All_axes_work_at_the_same_time()
    {
        var m = Step(Ready(), Pad(lx: 0.7f, ly: 0.3f, rx: -0.6f, ry: 0.4f, rt: 0.5f, buttons: GamepadButtons.RightShoulder));
        Assert.True(m.HasPan && m.HasRotation && m.HasZoom && m.HasRoll);
    }

    [Fact]
    public void Default_buttons_map_to_their_actions()
    {
        var nav = Ready();
        var expected = new (GamepadButtons, ButtonAction)[]
        {
            (GamepadButtons.A, ButtonAction.ZoomToFit),
            (GamepadButtons.B, ButtonAction.ZoomToSelection),
            (GamepadButtons.Y, ButtonAction.ViewIsometric),
            (GamepadButtons.X, ButtonAction.TogglePerspective),
            (GamepadButtons.DPadUp, ButtonAction.ViewTop),
            (GamepadButtons.DPadDown, ButtonAction.ViewFront),
            (GamepadButtons.DPadLeft, ButtonAction.ViewLeft),
            (GamepadButtons.DPadRight, ButtonAction.ViewRight),
        };
        foreach (var (button, action) in expected)
        {
            Step(nav, Pad(buttons: button));
            Assert.Equal(new[] { action }, commands);
            Step(nav, Pad());
        }
    }

    [Fact]
    public void Precision_click_toggles_slow_mode_once_per_press()
    {
        var nav = Ready();
        double normal = Step(nav, Pad(rx: 1)).Yaw;

        Step(nav, Pad(rx: 1, buttons: GamepadButtons.LeftThumb));
        Assert.Equal(new[] { ButtonAction.TogglePrecision }, commands);
        Step(nav, Pad(rx: 1, buttons: GamepadButtons.LeftThumb)); // still held: no repeat
        Assert.Empty(commands);
        Assert.True(nav.Precision);
        Assert.Equal(normal * 0.25, Step(nav, Pad(rx: 1)).Yaw, 9);
    }

    [Fact]
    public void Button_held_when_the_controller_connects_does_not_fire_until_pressed_again()
    {
        var nav = new OrbitNavigator(new NavigationSettings());
        Step(nav, Pad(buttons: GamepadButtons.A));
        Assert.Empty(commands);
        Step(nav, Pad());
        Step(nav, Pad(buttons: GamepadButtons.A));
        Assert.Equal(new[] { ButtonAction.ZoomToFit }, commands);
    }

    [Fact]
    public void Reset_swallows_a_button_held_across_a_focus_change()
    {
        var nav = Ready();
        nav.Reset();
        Step(nav, Pad(buttons: GamepadButtons.Y));
        Assert.Empty(commands);
    }

    [Fact]
    public void Menu_pauses_all_motion_until_pressed_again()
    {
        var nav = Ready();
        Step(nav, Pad(buttons: GamepadButtons.Start));
        Assert.Equal(new[] { ButtonAction.TogglePause }, commands);
        Assert.True(Step(nav, Pad(rx: 1, rt: 1, buttons: GamepadButtons.A)).IsNone);
        Assert.Empty(commands); // other buttons ignored while paused
        Step(nav, Pad());
        Step(nav, Pad(buttons: GamepadButtons.Start));
        Assert.False(nav.Paused);
        Assert.False(Step(nav, Pad(rx: 1)).IsNone);
    }

    [Fact]
    public void Long_stall_is_capped_so_the_view_does_not_jump()
    {
        var nav = Ready();
        Assert.Equal(Step(nav, Pad(rx: 1), dt: OrbitNavigator.MaxStepSeconds).Yaw, Step(nav, Pad(rx: 1), dt: 2.0).Yaw, 12);
    }

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(1.0, 1.0)]
    [InlineData(-1.0, -1.0)]
    [InlineData(0.5, 0.3125)] // 0.5 * 0.5 + 0.5 * 0.125
    public void Expo_curve_is_fine_near_center_and_full_at_the_edge(double input, double expected)
    {
        Assert.Equal(expected, new OrbitNavigator(new NavigationSettings()).Curve(input), 9);
    }

    // ---- remapping ----------------------------------------------------------------------

    [Fact]
    public void Swapping_the_sticks_swaps_orbit_and_move()
    {
        var s = new NavigationSettings();
        s.Axes[AnalogControl.LeftStickX] = AxisAction.OrbitHorizontal;
        s.Axes[AnalogControl.RightStickX] = AxisAction.MoveSideways;
        var nav = Ready(s);
        var left = Step(nav, Pad(lx: 1));
        Assert.True(left.Yaw > 0 && left.PanX == 0);
        var right = Step(nav, Pad(rx: 1));
        Assert.True(right.PanX < 0 && right.Yaw == 0);
    }

    [Fact]
    public void Unmapped_control_does_nothing()
    {
        var s = new NavigationSettings();
        s.Axes[AnalogControl.Triggers] = AxisAction.None;
        s.Buttons[GamepadButtons.A] = ButtonAction.None;
        var nav = Ready(s);
        Assert.True(Step(nav, Pad(rt: 1, buttons: GamepadButtons.A)).IsNone);
        Assert.Empty(commands);
    }

    [Fact]
    public void Hold_actions_move_while_held_and_add_to_the_sticks()
    {
        var s = new NavigationSettings();
        s.Buttons[GamepadButtons.DPadUp] = ButtonAction.MoveForward;
        s.Buttons[GamepadButtons.DPadRight] = ButtonAction.OrbitRight;
        var nav = Ready(s);
        var m = Step(nav, Pad(buttons: GamepadButtons.DPadUp | GamepadButtons.DPadRight), dt: 0.1);
        Assert.Equal(Math.Pow(3, 0.1), m.Zoom, 9);
        Assert.True(m.Yaw > 0);
        Assert.Empty(commands); // hold actions aren't presses
        // stick + held button on the same axis is capped at full speed
        Assert.Equal(Math.Pow(3, 0.1), Step(nav, Pad(ly: 1, buttons: GamepadButtons.DPadUp), dt: 0.1).Zoom, 9);
    }

    [Fact]
    public void Pause_can_be_moved_to_another_button()
    {
        var s = new NavigationSettings();
        s.Buttons[GamepadButtons.Start] = ButtonAction.None;
        s.Buttons[GamepadButtons.Back] = ButtonAction.TogglePause;
        var nav = Ready(s);
        Step(nav, Pad(buttons: GamepadButtons.Start));
        Assert.False(nav.Paused);
        Step(nav, Pad());
        Step(nav, Pad(buttons: GamepadButtons.Back));
        Assert.True(nav.Paused);
    }

    [Fact]
    public void Removing_the_pause_button_while_paused_unpauses()
    {
        var nav = Ready();
        Step(nav, Pad(buttons: GamepadButtons.Start));
        Assert.True(nav.Paused);
        var s = new NavigationSettings();
        s.Buttons[GamepadButtons.Start] = ButtonAction.None;
        nav.ApplySettings(s);
        Assert.False(nav.Paused);
    }

    [Fact]
    public void Every_move_orbit_and_roll_button_action_is_a_hold_action()
    {
        foreach (ButtonAction a in Enum.GetValues<ButtonAction>())
        {
            string name = a.ToString();
            bool looksLikeHold = name.StartsWith("Move") || name.StartsWith("Orbit") || name.StartsWith("Roll");
            Assert.Equal(looksLikeHold, ControlMapping.IsHold(a));
        }
    }
}

public class NavigationSettingsTests
{
    [Fact]
    public void Parse_reads_known_values_and_ignores_the_rest()
    {
        var s = new NavigationSettings();
        s.Parse(new[]
        {
            "# comment",
            "RotateSpeedDeg = 90   # trailing comment",
            "PanSpeed = not-a-number",
            "Unknown = 5",
            "garbage line",
            "Axis.LeftStickX = OrbitHorizontal",
            "axis.triggers = nonsense",
            "Invert.RightStickY = true",
            "Button.A = ViewBottom",
            "Button.Start = MoveUp",
            "Button.None = ZoomToFit",
        });
        Assert.Equal(90, s.RotateSpeedDeg);
        Assert.Equal(1.0, s.PanSpeed);
        Assert.Equal(AxisAction.OrbitHorizontal, s.Axes[AnalogControl.LeftStickX]);
        Assert.Equal(AxisAction.MoveVertical, s.Axes[AnalogControl.Triggers]); // default kept
        Assert.True(s.Invert[AnalogControl.RightStickY]);
        Assert.Equal(ButtonAction.ViewBottom, s.Buttons[GamepadButtons.A]);
        Assert.Equal(ButtonAction.MoveUp, s.Buttons[GamepadButtons.Start]);
        Assert.False(s.Buttons.ContainsKey(GamepadButtons.None));
    }

    [Fact]
    public void Ini_round_trips_speeds_and_mapping()
    {
        var a = new NavigationSettings { ZoomSpeed = 4.5, TickMs = 8 };
        a.Axes[AnalogControl.RightStickY] = AxisAction.Roll;
        a.Invert[AnalogControl.LeftStickX] = true;
        a.Buttons[GamepadButtons.RightThumb] = ButtonAction.OpenSettings;
        var b = new NavigationSettings();
        b.Parse(a.ToIni().Split('\n'));
        Assert.Equal(4.5, b.ZoomSpeed);
        Assert.Equal(8, b.TickMs);
        Assert.Equal(AxisAction.Roll, b.Axes[AnalogControl.RightStickY]);
        Assert.True(b.Invert[AnalogControl.LeftStickX]);
        Assert.Equal(ButtonAction.OpenSettings, b.Buttons[GamepadButtons.RightThumb]);
    }

    [Fact]
    public void Reset_restores_the_default_layout()
    {
        var s = new NavigationSettings { RotateSpeedDeg = 10 };
        s.Axes[AnalogControl.LeftStickY] = AxisAction.None;
        s.Buttons[GamepadButtons.A] = ButtonAction.None;
        s.ResetToDefaults();
        Assert.Equal(160, s.RotateSpeedDeg);
        Assert.Equal(AxisAction.MoveForward, s.Axes[AnalogControl.LeftStickY]);
        Assert.Equal(ButtonAction.ZoomToFit, s.Buttons[GamepadButtons.A]);
    }

    [Fact]
    public void Missing_file_is_created_with_defaults_and_save_overwrites()
    {
        string path = Path.Combine(Path.GetTempPath(), $"xbox-addin-{Guid.NewGuid():N}.ini");
        try
        {
            var s = NavigationSettings.LoadOrCreate(path);
            Assert.True(File.Exists(path));
            Assert.Equal(160, s.RotateSpeedDeg);

            s.PanSpeed = 2.5;
            s.Save(path);
            Assert.Equal(2.5, NavigationSettings.LoadOrCreate(path).PanSpeed);
            Assert.False(File.Exists(path + ".tmp"));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
