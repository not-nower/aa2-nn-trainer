using System;
using System.Globalization;
using System.IO;

namespace AA2Trainer.WpfPrototype
{
    /// <summary>
    /// Small portable settings file kept beside the exe. Only the trainer's own
    /// preferences live here; game values are always read from memory.
    /// </summary>
    public sealed class TrainerSettings
    {
        public bool LiveRefreshEnabled = true;
        public int RefreshIntervalMs = 250;
        public bool LiveIntervalAuto = true;
        public bool ObserveOnly;
        public ThemeMode Theme = ThemeMode.System;

        // Window bounds are physical screen pixels so mixed-DPI and
        // multi-monitor restores stay exact.
        public double WindowLeft;
        public double WindowTop;
        public double WindowWidth;
        public double WindowHeight;
        public bool WindowMaximized;

        public bool RailCollapsed;
        public bool RosterRailCollapsed = true;
        public bool LogCollapsed;

        /// <summary>
        /// The single settings instance shared by the window and view model.
        /// Saving always writes the whole object, so separate instances would
        /// let one preference overwrite another.
        /// </summary>
        public static TrainerSettings Current { get; } = Load();

        public static string FilePath
        {
            get
            {
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "AA2Trainer.settings");
            }
        }

        public static TrainerSettings Load()
        {
            TrainerSettings settings = new TrainerSettings();
            try
            {
                if (!File.Exists(FilePath)) return settings;
                foreach (string line in File.ReadAllLines(FilePath))
                {
                    int split = line.IndexOf('=');
                    if (split <= 0) continue;
                    string key = line.Substring(0, split).Trim();
                    string value = line.Substring(split + 1).Trim();
                    switch (key)
                    {
                        case "LiveRefresh":
                            settings.LiveRefreshEnabled = value != "0";
                            break;
                        case "RefreshIntervalMs":
                        {
                            int ms;
                            if (int.TryParse(value, NumberStyles.Integer,
                                CultureInfo.InvariantCulture, out ms))
                                settings.RefreshIntervalMs = ms;
                            break;
                        }
                        case "LiveIntervalAuto":
                            settings.LiveIntervalAuto = value != "0";
                            break;
                        case "ObserveOnly":
                            settings.ObserveOnly = value != "0";
                            break;
                        case "Theme":
                        {
                            ThemeMode mode;
                            if (Enum.TryParse(value, true, out mode) &&
                                Enum.IsDefined(typeof(ThemeMode), mode))
                                settings.Theme = mode;
                            break;
                        }
                        case "WindowLeft":
                            settings.WindowLeft = ParseDistance(value,
                                settings.WindowLeft);
                            break;
                        case "WindowTop":
                            settings.WindowTop = ParseDistance(value,
                                settings.WindowTop);
                            break;
                        case "WindowWidth":
                            settings.WindowWidth = ParseDistance(value,
                                settings.WindowWidth);
                            break;
                        case "WindowHeight":
                            settings.WindowHeight = ParseDistance(value,
                                settings.WindowHeight);
                            break;
                        case "WindowMaximized":
                            settings.WindowMaximized = value != "0";
                            break;
                        case "RailCollapsed":
                            settings.RailCollapsed = value != "0";
                            break;
                        case "RosterRailCollapsed":
                            settings.RosterRailCollapsed = value != "0";
                            break;
                        case "LogCollapsed":
                            settings.LogCollapsed = value != "0";
                            break;
                    }
                }
            }
            catch
            {
                // A malformed settings file falls back to defaults.
            }
            return settings;
        }

        public void Save()
        {
            try
            {
                File.WriteAllLines(FilePath, new string[]
                {
                    "# AA2 Trainer settings",
                    "LiveRefresh=" + (LiveRefreshEnabled ? "1" : "0"),
                    "RefreshIntervalMs=" + RefreshIntervalMs.ToString(
                        CultureInfo.InvariantCulture),
                    "LiveIntervalAuto=" + (LiveIntervalAuto ? "1" : "0"),
                    "ObserveOnly=" + (ObserveOnly ? "1" : "0"),
                    "Theme=" + Theme,
                    "WindowLeft=" + FormatDistance(WindowLeft),
                    "WindowTop=" + FormatDistance(WindowTop),
                    "WindowWidth=" + FormatDistance(WindowWidth),
                    "WindowHeight=" + FormatDistance(WindowHeight),
                    "WindowMaximized=" + (WindowMaximized ? "1" : "0"),
                    "RailCollapsed=" + (RailCollapsed ? "1" : "0"),
                    "RosterRailCollapsed=" + (RosterRailCollapsed ? "1" : "0"),
                    "LogCollapsed=" + (LogCollapsed ? "1" : "0")
                });
            }
            catch
            {
                // Settings are best-effort; the trainer still works.
            }
        }

        private static double ParseDistance(string value, double fallback)
        {
            double parsed;
            if (double.TryParse(value, NumberStyles.Float,
                CultureInfo.InvariantCulture, out parsed) &&
                !double.IsNaN(parsed) && !double.IsInfinity(parsed))
                return parsed;
            return fallback;
        }

        private static string FormatDistance(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return "0";
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
