using Xbox_Control;
using XboxControl.Addin;

namespace Xbox_Control.Tests;

public class OrbitNavigatorTests
{
    private const double Deg = Math.PI / 180;
    private readonly List<ViewCommand> commands = new();

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
        Assert.True(Step(Ready(new NavigationSettings { InvertOrbitY = true }), Pad(ry: 1)).Pitch > 0);
    }

    [Fact]
    public void Left_stick_sideways_moves_the_camera_in_view_heights_per_second()
    {
        // Stick right moves the camera right, so the model moves left on screen.
        var m = Step(Ready(), Pad(lx: 1), dt: 0.1);
        Assert.Equal(-0.1, m.PanX, 9);
        Assert.Equal(0, m.PanY);
        Assert.Equal(0.1, Step(Ready(new NavigationSettings { InvertPanX = true }), Pad(lx: 1), dt: 0.1).PanX, 9);
    }

    [Fact]
    public void Left_stick_forward_and_back_zooms_in_and_out()
    {
        var nav = Ready();
        Assert.Equal(Math.Pow(3, 0.1), Step(nav, Pad(ly: 1), dt: 0.1).Zoom, 9);
        Assert.Equal(Math.Pow(3, -0.1), Step(nav, Pad(ly: -1), dt: 0.1).Zoom, 9);
        Assert.Equal(0, Step(nav, Pad(ly: 1)).PanY);
        Assert.True(Step(Ready(new NavigationSettings { InvertForward = true }), Pad(ly: 1)).Zoom < 1);
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
        Assert.True(Step(nav, Pad(buttons: GamepadButtons.LeftShoulder)).Roll > 0);
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
    public void Precision_click_toggles_slow_mode_once_per_press()
    {
        var nav = Ready();
        double normal = Step(nav, Pad(rx: 1)).Yaw;

        Step(nav, Pad(rx: 1, buttons: GamepadButtons.LeftThumb));
        Assert.Equal(new[] { ViewCommand.TogglePrecision }, commands);
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
        Assert.Equal(new[] { ViewCommand.ZoomToFit }, commands);
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
    public void Buttons_map_to_view_commands()
    {
        var nav = Ready();
        var expected = new (GamepadButtons, ViewCommand)[]
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
        foreach (var (button, command) in expected)
        {
            Step(nav, Pad(buttons: button));
            Assert.Equal(new[] { command }, commands);
            Step(nav, Pad());
        }
    }

    [Fact]
    public void Start_pauses_all_motion_until_pressed_again()
    {
        var nav = Ready();
        Step(nav, Pad(buttons: GamepadButtons.Start));
        Assert.Equal(new[] { ViewCommand.TogglePause }, commands);
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
            "invertorbity = true",
            "PanSpeed = not-a-number",
            "Unknown = 5",
            "garbage line",
        });
        Assert.Equal(90, s.RotateSpeedDeg);
        Assert.True(s.InvertOrbitY);
        Assert.Equal(1.0, s.PanSpeed);
    }

    [Fact]
    public void Ini_round_trips()
    {
        var a = new NavigationSettings { ZoomSpeed = 4.5, InvertPanX = true, TickMs = 8 };
        var b = new NavigationSettings();
        b.Parse(a.ToIni().Split('\n'));
        Assert.Equal(4.5, b.ZoomSpeed);
        Assert.True(b.InvertPanX);
        Assert.Equal(8, b.TickMs);
    }

    [Fact]
    public void Missing_file_is_created_with_defaults()
    {
        string path = Path.Combine(Path.GetTempPath(), $"xbox-addin-{Guid.NewGuid():N}.ini");
        try
        {
            var s = NavigationSettings.LoadOrCreate(path);
            Assert.True(File.Exists(path));
            Assert.Equal(160, s.RotateSpeedDeg);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
