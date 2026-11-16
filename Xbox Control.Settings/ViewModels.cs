using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using Xbox_Control;
using XboxControl.Addin;

namespace XboxControl.Settings;

public abstract class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }

    protected void Raise([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed record Choice(object Value, string Label, string Group);

/// <summary>One line in a card: a control, what it does, and whether it's being used right now.</summary>
public abstract class MappingRow : Observable
{
    private bool isActive;

    protected MappingRow(string label) => Label = label;

    public string Label { get; }
    public abstract ListCollectionView Options { get; }
    public bool IsActive { get => isActive; set => Set(ref isActive, value); }
}

public sealed class AxisRow : MappingRow
{
    private static readonly Choice[] Choices = Enum.GetValues<AxisAction>()
        .Select(a => new Choice(a, ControlMapping.Label(a), "")).ToArray();

    private AxisAction selected;
    private bool invert;

    public AxisRow(AnalogControl control, string label, AxisAction selected, bool invert) : base(label)
    {
        Control = control;
        this.selected = selected;
        this.invert = invert;
        Options = new ListCollectionView(Choices);
    }

    public AnalogControl Control { get; }
    public override ListCollectionView Options { get; }
    public AxisAction Selected { get => selected; set => Set(ref selected, value); }
    public bool Invert { get => invert; set => Set(ref invert, value); }
}

public sealed class ButtonRow : MappingRow
{
    private static readonly Choice[] Choices = Enum.GetValues<ButtonAction>()
        .Select(a => new Choice(a, ControlMapping.Label(a), GroupOf(a))).ToArray();

    private ButtonAction selected;

    public ButtonRow(GamepadButtons button, string label, ButtonAction selected) : base(label)
    {
        Button = button;
        this.selected = selected;
        Options = new ListCollectionView(Choices);
        Options.GroupDescriptions.Add(new PropertyGroupDescription(nameof(Choice.Group)));
    }

    public GamepadButtons Button { get; }
    public override ListCollectionView Options { get; }
    public ButtonAction Selected { get => selected; set => Set(ref selected, value); }

    private static string GroupOf(ButtonAction a) => a switch
    {
        ButtonAction.None => "",
        ButtonAction.ZoomToFit or ButtonAction.ZoomToSelection => "Zoom",
        ButtonAction.TogglePerspective => "Views",
        ButtonAction.TogglePrecision or ButtonAction.TogglePause or ButtonAction.OpenSettings => "Modes",
        _ when ControlMapping.IsHold(a) => "Hold to move",
        _ => "Views",
    };
}

public sealed class RowGroup
{
    public RowGroup(string title, params MappingRow[] rows)
    {
        Title = title;
        Rows = rows;
    }

    public string Title { get; }
    public IReadOnlyList<MappingRow> Rows { get; }
}

/// <summary>A labeled slider bound to one setting.</summary>
public sealed class SpeedSetting : Observable
{
    private readonly Func<double, string> format;
    private double value;

    public SpeedSetting(string label, string hint, double min, double max, double value, Func<double, string> format)
    {
        Label = label;
        Hint = hint;
        Minimum = min;
        Maximum = max;
        this.value = value;
        this.format = format;
    }

    public string Label { get; }
    public string Hint { get; }
    public double Minimum { get; }
    public double Maximum { get; }

    public double Value
    {
        get => value;
        set
        {
            if (Set(ref this.value, value)) Raise(nameof(Display));
        }
    }

    public string Display => format(value);
}
