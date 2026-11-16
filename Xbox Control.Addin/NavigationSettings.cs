using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Xbox_Control;

// Shared by the add-in (.NET Framework 4.8), the settings app and the tests.

namespace XboxControl.Addin
{
    /// <summary>
    /// Control mapping, speeds and options, stored in %APPDATA%\XboxControl\addin.ini as
    /// "Name = value" lines. The add-in creates the file with the defaults on first load and
    /// reloads it whenever it changes, so the settings app's changes apply right away.
    /// </summary>
    public sealed class NavigationSettings
    {
        public double RotateSpeedDeg { get; set; } = 160;  // orbit, degrees per second at full stick
        public double PanSpeed { get; set; } = 1.0;        // move sideways / up-down, view heights per second
        public double ZoomSpeed { get; set; } = 3.0;       // move forward, zoom factor per second
        public double RollSpeedDeg { get; set; } = 90;     // degrees per second
        public double PrecisionScale { get; set; } = 0.25; // speed multiplier in precision mode
        public double Expo { get; set; } = 0.5;            // 0 = linear sticks, 1 = cubic (finer near center)
        public int TickMs { get; set; } = 10;              // timer interval (Windows rounds up to ~15.6 ms)
        public bool BatchRedraw { get; set; } = true;      // apply all axes, then redraw once

        public Dictionary<AnalogControl, AxisAction> Axes { get; } = ControlMapping.DefaultAxes();
        public Dictionary<AnalogControl, bool> Invert { get; } = ControlMapping.AnalogControls.ToDictionary(c => c, _ => false);
        public Dictionary<GamepadButtons, ButtonAction> Buttons { get; } = ControlMapping.DefaultButtons();

        public static string DefaultPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XboxControl", "addin.ini");

        private static PropertyInfo[] ScalarProperties => typeof(NavigationSettings)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite).ToArray();

        /// <summary>Reads the file; unknown or malformed lines keep their defaults. Writes defaults if missing.</summary>
        public static NavigationSettings LoadOrCreate(string? path = null)
        {
            path ??= DefaultPath;
            var settings = new NavigationSettings();
            if (File.Exists(path))
                settings.Parse(File.ReadAllLines(path));
            else
                settings.Save(path);
            return settings;
        }

        /// <summary>Writes the file via a temporary file, so a reader never sees half of it.</summary>
        public void Save(string? path = null)
        {
            path ??= DefaultPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string temp = path + ".tmp";
            File.WriteAllText(temp, ToIni());
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    if (File.Exists(path)) File.Replace(temp, path, null);
                    else File.Move(temp, path);
                    return;
                }
                catch (IOException) when (attempt < 10)
                {
                    Thread.Sleep(30); // the add-in is reading it right now
                }
            }
        }

        public void Parse(IEnumerable<string> lines)
        {
            var props = ScalarProperties.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
            foreach (var raw in lines)
            {
                var line = raw.Split('#')[0].Trim();
                int eq = line.IndexOf('=');
                if (eq <= 0) continue;
                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim();

                int dot = key.IndexOf('.');
                if (dot > 0)
                {
                    string group = key.Substring(0, dot), name = key.Substring(dot + 1);
                    if (group.Equals("Axis", StringComparison.OrdinalIgnoreCase)
                        && TryEnum(name, out AnalogControl axis) && TryEnum(value, out AxisAction axisAction))
                        Axes[axis] = axisAction;
                    else if (group.Equals("Invert", StringComparison.OrdinalIgnoreCase)
                        && TryEnum(name, out AnalogControl inv) && bool.TryParse(value, out bool flag))
                        Invert[inv] = flag;
                    else if (group.Equals("Button", StringComparison.OrdinalIgnoreCase)
                        && TryEnum(name, out GamepadButtons button) && ControlMapping.MappableButtons.Contains(button)
                        && TryEnum(value, out ButtonAction buttonAction))
                        Buttons[button] = buttonAction;
                    continue;
                }

                if (!props.TryGetValue(key, out var prop)) continue;
                try
                {
                    object parsed = prop.PropertyType == typeof(bool) ? bool.Parse(value)
                        : Convert.ChangeType(value, prop.PropertyType, CultureInfo.InvariantCulture);
                    prop.SetValue(this, parsed);
                }
                catch (FormatException) { /* keep the default */ }
            }
        }

        public string ToIni()
        {
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.AppendLine("# Xbox Control settings. Edit with the Xbox Control Settings app (SolidWorks: Tools menu),");
            sb.AppendLine("# or by hand: the add-in reloads this file within a second of it changing.");
            sb.AppendLine();
            sb.AppendLine("# Speeds at full stick. PanSpeed is in view heights per second, ZoomSpeed is zoom factor per second.");
            foreach (var p in ScalarProperties)
                sb.AppendLine(string.Format(inv, "{0} = {1}", p.Name, p.GetValue(this)));
            sb.AppendLine();
            sb.AppendLine("# Sticks and triggers: " + string.Join(", ", Enum.GetNames(typeof(AxisAction))));
            foreach (var c in ControlMapping.AnalogControls)
                sb.AppendLine($"Axis.{c} = {Axes[c]}");
            foreach (var c in ControlMapping.AnalogControls)
                sb.AppendLine($"Invert.{c} = {Invert[c]}");
            sb.AppendLine();
            sb.AppendLine("# Buttons: " + string.Join(", ", Enum.GetNames(typeof(ButtonAction))));
            foreach (var b in ControlMapping.MappableButtons)
                sb.AppendLine($"Button.{b} = {Buttons[b]}");
            return sb.ToString();
        }

        public void ResetToDefaults()
        {
            var d = new NavigationSettings();
            foreach (var p in ScalarProperties) p.SetValue(this, p.GetValue(d));
            foreach (var kv in d.Axes) Axes[kv.Key] = kv.Value;
            foreach (var kv in d.Invert) Invert[kv.Key] = kv.Value;
            foreach (var kv in d.Buttons) Buttons[kv.Key] = kv.Value;
        }

        private static bool TryEnum<T>(string text, out T value) where T : struct =>
            Enum.TryParse(text, true, out value) && Enum.IsDefined(typeof(T), value);
    }
}
