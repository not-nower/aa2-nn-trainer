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
        public bool ObserveOnly;

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
                        case "ObserveOnly":
                            settings.ObserveOnly = value != "0";
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
                    "ObserveOnly=" + (ObserveOnly ? "1" : "0")
                });
            }
            catch
            {
                // Settings are best-effort; the trainer still works.
            }
        }
    }
}
