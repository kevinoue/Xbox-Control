using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace XboxControl.Addin
{
    /// <summary>
    /// Speeds and options, read from %APPDATA%\XboxControl\addin.ini ("Name = value" lines).
    /// The file is created with the defaults the first time the add-in loads.
    /// </summary>
    public sealed class NavigationSettings
    {
        public double RotateSpeedDeg { get; set; } = 160;  // degrees per second at full stick
        public double PanSpeed { get; set; } = 1.0;        // view heights per second at full stick
        public double ZoomSpeed { get; set; } = 3.0;       // zoom factor per second at full trigger
        public double RollSpeedDeg { get; set; } = 90;     // degrees per second while LB/RB is held
        public double PrecisionScale { get; set; } = 0.25; // speed multiplier in precision mode
        public double Expo { get; set; } = 0.5;            // 0 = linear sticks, 1 = cubic (finer near center)
        public bool InvertOrbitX { get; set; }
        public bool InvertOrbitY { get; set; }
        public bool InvertPanX { get; set; }
        public bool InvertPanY { get; set; }
        public int TickMs { get; set; } = 10;              // timer interval (Windows rounds up to ~15.6 ms)
        public bool BatchRedraw { get; set; } = true;      // apply all axes, then redraw once

        public static string DefaultPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XboxControl", "addin.ini");

        private static PropertyInfo[] Properties => typeof(NavigationSettings)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite).ToArray();

        /// <summary>Reads the file; unknown or malformed lines are ignored. Writes defaults if missing.</summary>
        public static NavigationSettings LoadOrCreate(string? path = null)
        {
            path ??= DefaultPath;
            var settings = new NavigationSettings();
            if (File.Exists(path))
                settings.Parse(File.ReadAllLines(path));
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, settings.ToIni());
            }
            return settings;
        }

        public void Parse(string[] lines)
        {
            var props = Properties.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
            foreach (var raw in lines)
            {
                var line = raw.Split('#')[0].Trim();
                int eq = line.IndexOf('=');
                if (eq <= 0 || !props.TryGetValue(line.Substring(0, eq).Trim(), out var prop)) continue;
                var value = line.Substring(eq + 1).Trim();
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
            var sb = new StringBuilder();
            sb.AppendLine("# Xbox Control add-in settings. Restart SolidWorks (or reload the add-in) after editing.");
            sb.AppendLine("# Speeds are at full stick / trigger. PanSpeed is in view heights per second.");
            foreach (var p in Properties)
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0} = {1}", p.Name, p.GetValue(this)));
            return sb.ToString();
        }
    }
}
