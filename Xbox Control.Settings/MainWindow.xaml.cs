using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Xbox_Control;
using XboxControl.Addin;

namespace XboxControl.Settings;

public partial class MainWindow : Window
{
    private const float StickActive = 0.25f;
    private const double ThumbTravel = 9;

    private readonly NavigationSettings settings;
    private readonly List<AxisRow> axisRows = new();
    private readonly List<ButtonRow> buttonRows = new();
    private readonly List<SpeedSetting> speeds = new();
    private readonly XInputGamepad pad = new();
    private readonly DispatcherTimer poll = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly DispatcherTimer saveDelay = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private readonly Dictionary<GamepadButtons, Shape> buttonParts;
    private bool loading;

    public MainWindow()
    {
        InitializeComponent();
        settings = NavigationSettings.LoadOrCreate();

        buttonParts = new()
        {
            [GamepadButtons.A] = PartA, [GamepadButtons.B] = PartB, [GamepadButtons.X] = PartX, [GamepadButtons.Y] = PartY,
            [GamepadButtons.LeftShoulder] = PartLB, [GamepadButtons.RightShoulder] = PartRB,
            [GamepadButtons.DPadUp] = PartDUp, [GamepadButtons.DPadDown] = PartDDown,
            [GamepadButtons.DPadLeft] = PartDLeft, [GamepadButtons.DPadRight] = PartDRight,
            [GamepadButtons.LeftThumb] = PartLS, [GamepadButtons.RightThumb] = PartRS,
            [GamepadButtons.Back] = PartView, [GamepadButtons.Start] = PartMenu,
        };

        BuildCards();
        BuildSpeeds();

        saveDelay.Tick += (_, _) => { saveDelay.Stop(); Save(); };
        poll.Tick += (_, _) => PollController();
        poll.Start();
    }

    // ---- layout -------------------------------------------------------------------------

    private void BuildCards()
    {
        LeftCards.ItemsSource = new[]
        {
            new RowGroup("Left bumper", ButtonRowFor(GamepadButtons.LeftShoulder, "LB")),
            new RowGroup("Left stick",
                AxisRowFor(AnalogControl.LeftStickX, "Left / right"),
                AxisRowFor(AnalogControl.LeftStickY, "Up / down"),
                ButtonRowFor(GamepadButtons.LeftThumb, "Click")),
            new RowGroup("D-pad",
                ButtonRowFor(GamepadButtons.DPadUp, "Up"),
                ButtonRowFor(GamepadButtons.DPadDown, "Down"),
                ButtonRowFor(GamepadButtons.DPadLeft, "Left"),
                ButtonRowFor(GamepadButtons.DPadRight, "Right")),
            new RowGroup("View button", ButtonRowFor(GamepadButtons.Back, "View")),
        };

        RightCards.ItemsSource = new[]
        {
            new RowGroup("Right bumper", ButtonRowFor(GamepadButtons.RightShoulder, "RB")),
            new RowGroup("Buttons",
                ButtonRowFor(GamepadButtons.Y, "Y"),
                ButtonRowFor(GamepadButtons.B, "B"),
                ButtonRowFor(GamepadButtons.A, "A"),
                ButtonRowFor(GamepadButtons.X, "X")),
            new RowGroup("Right stick",
                AxisRowFor(AnalogControl.RightStickX, "Left / right"),
                AxisRowFor(AnalogControl.RightStickY, "Up / down"),
                ButtonRowFor(GamepadButtons.RightThumb, "Click")),
            new RowGroup("Menu button", ButtonRowFor(GamepadButtons.Start, "Menu")),
        };

        TriggerCard.Content = new RowGroup("Triggers", AxisRowFor(AnalogControl.Triggers, "RT + / LT −"));
    }

    private AxisRow AxisRowFor(AnalogControl control, string label)
    {
        var row = new AxisRow(control, label, settings.Axes[control], settings.Invert[control]);
        row.PropertyChanged += Row_Changed;
        axisRows.Add(row);
        return row;
    }

    private ButtonRow ButtonRowFor(GamepadButtons button, string label)
    {
        var row = new ButtonRow(button, label, settings.Buttons[button]);
        row.PropertyChanged += Row_Changed;
        buttonRows.Add(row);
        return row;
    }

    private void BuildSpeeds()
    {
        var inv = CultureInfo.InvariantCulture;
        speeds.Clear();
        speeds.Add(Speed("Orbit speed", "Turning at full tilt.", 30, 360,
            settings.RotateSpeedDeg, v => settings.RotateSpeedDeg = v, v => string.Format(inv, "{0:0}°/s", v)));
        speeds.Add(Speed("Move speed", "Left / right and up / down.", 0.2, 3,
            settings.PanSpeed, v => settings.PanSpeed = v, v => string.Format(inv, "{0:0.0} screens/s", v)));
        speeds.Add(Speed("Forward speed", "Moving forward and back.", 1.2, 8,
            settings.ZoomSpeed, v => settings.ZoomSpeed = v, v => string.Format(inv, "{0:0.0}× per s", v)));
        speeds.Add(Speed("Roll speed", "While a roll button is held.", 15, 270,
            settings.RollSpeedDeg, v => settings.RollSpeedDeg = v, v => string.Format(inv, "{0:0}°/s", v)));
        speeds.Add(Speed("Precision mode", "Speed in precision mode.", 0.05, 0.75,
            settings.PrecisionScale, v => settings.PrecisionScale = v, v => string.Format(inv, "{0:0}%", v * 100)));
        speeds.Add(Speed("Stick response", "Higher = finer near center.", 0, 1,
            settings.Expo, v => settings.Expo = v, v => v < 0.05 ? "Linear" : string.Format(inv, "{0:0}% curve", v * 100)));
        SpeedItems.ItemsSource = null;
        SpeedItems.ItemsSource = speeds;
    }

    private SpeedSetting Speed(string label, string hint, double min, double max, double value,
        Action<double> apply, Func<double, string> format)
    {
        var s = new SpeedSetting(label, hint, min, max, value, format);
        s.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(SpeedSetting.Value)) return;
            apply(Math.Round(s.Value, 3));
            ScheduleSave();
        };
        return s;
    }

    // ---- saving -------------------------------------------------------------------------

    private void Row_Changed(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MappingRow.IsActive)) return;
        switch (sender)
        {
            case AxisRow a:
                settings.Axes[a.Control] = a.Selected;
                settings.Invert[a.Control] = a.Invert;
                break;
            case ButtonRow b:
                settings.Buttons[b.Button] = b.Selected;
                break;
        }
        ScheduleSave();
    }

    private void ScheduleSave()
    {
        if (loading) return;
        SaveText.Text = "Saving...";
        saveDelay.Stop();
        saveDelay.Start();
    }

    private void Save()
    {
        try
        {
            settings.Save();
            SaveGlyph.Visibility = Visibility.Visible;
            SaveText.Text = $"Saved at {DateTime.Now:t}. SolidWorks picks up changes within a second.";
        }
        catch (Exception ex)
        {
            SaveGlyph.Visibility = Visibility.Collapsed;
            SaveText.Text = $"Couldn't save: {ex.Message}";
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (saveDelay.IsEnabled)
        {
            saveDelay.Stop();
            Save();
        }
        poll.Stop();
        base.OnClosing(e);
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, "Put every button, stick and speed back to the defaults?", "Reset to defaults",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        settings.ResetToDefaults();
        loading = true;
        try
        {
            foreach (var r in axisRows)
            {
                r.Selected = settings.Axes[r.Control];
                r.Invert = settings.Invert[r.Control];
            }
            foreach (var r in buttonRows) r.Selected = settings.Buttons[r.Button];
            BuildSpeeds();
        }
        finally
        {
            loading = false;
        }
        Save();
    }

    private void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        if (saveDelay.IsEnabled) { saveDelay.Stop(); Save(); }
        Process.Start(new ProcessStartInfo("notepad.exe", $"\"{NavigationSettings.DefaultPath}\"") { UseShellExecute = true });
    }

    private void Done_Click(object sender, RoutedEventArgs e) => Close();

    // ---- live controller feedback -------------------------------------------------------

    private void PollController()
    {
        bool connected = pad.TryRead(out var s);
        StatusText.Text = connected
            ? $"Controller {pad.PlayerNumber} connected"
            : "No controller. Press the Xbox button to wake it.";
        StatusDot.Fill = connected ? Brushes.LimeGreen : Brushes.Gray;

        foreach (var r in axisRows) r.IsActive = connected && Math.Abs(AxisValue(s, r.Control)) > StickActive;
        foreach (var r in buttonRows) r.IsActive = connected && s.IsDown(r.Button);

        foreach (var (button, part) in buttonParts)
        {
            bool on = connected && s.IsDown(button);
            if (button is GamepadButtons.LeftThumb or GamepadButtons.RightThumb)
                on |= connected && (button == GamepadButtons.LeftThumb ? Moved(s.LeftX, s.LeftY) : Moved(s.RightX, s.RightY));
            Highlight(part, on);
        }
        Highlight(ThumbLS, connected && Moved(s.LeftX, s.LeftY));
        Highlight(ThumbRS, connected && Moved(s.RightX, s.RightY));
        Highlight(PartLT, connected && s.LeftTrigger > 0.1f);
        Highlight(PartRT, connected && s.RightTrigger > 0.1f);
        MoveLS.X = s.LeftX * ThumbTravel; MoveLS.Y = -s.LeftY * ThumbTravel;
        MoveRS.X = s.RightX * ThumbTravel; MoveRS.Y = -s.RightY * ThumbTravel;
    }

    private static bool Moved(float x, float y) => Math.Abs(x) > StickActive || Math.Abs(y) > StickActive;

    private static float AxisValue(GamepadSnapshot s, AnalogControl c) => c switch
    {
        AnalogControl.LeftStickX => s.LeftX,
        AnalogControl.LeftStickY => s.LeftY,
        AnalogControl.RightStickX => s.RightX,
        AnalogControl.RightStickY => s.RightY,
        AnalogControl.Triggers => Math.Max(s.LeftTrigger, s.RightTrigger),
        _ => 0,
    };

    private static void Highlight(Shape part, bool on) =>
        part.SetResourceReference(Shape.FillProperty, on ? "AccentFillColorDefaultBrush" : "ControlFillColorDefaultBrush");
}
