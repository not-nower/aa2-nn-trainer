using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace AA2Trainer
{
    /// <summary>
    /// Portable trainer settings stored in AA2Trainer.ini beside the exe.
    /// Kept in one file so the action limit list and the last save path cannot
    /// overwrite each other when either one changes.
    /// </summary>
    public static class TrainerSettings
    {
        public const string FileName = "AA2Trainer.ini";

        private const string LastSaveKey = "last-save";
        private const string LimitActionsKey = "limit-actions";

        private static readonly object Gate = new object();
        private static readonly Dictionary<string, string> Values =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static bool loaded;

        public static string FilePath
        {
            get
            {
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    FileName);
            }
        }

        public static int[] LoadLimitActions()
        {
            lock (Gate)
            {
                EnsureLoaded();
                string raw;
                if (!Values.TryGetValue(LimitActionsKey, out raw))
                    return new int[0];

                List<int> ids = new List<int>();
                string[] parts = raw.Split(new char[] { ',', ';', ' ', '\t' },
                    StringSplitOptions.RemoveEmptyEntries);
                foreach (string part in parts)
                {
                    int id;
                    if (!int.TryParse(part, NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out id))
                        continue;
                    if (id < 0 || id > 999) continue;
                    if (!ids.Contains(id)) ids.Add(id);
                }
                ids.Sort();
                return ids.ToArray();
            }
        }

        public static void SaveLimitActions(int[] actionIds)
        {
            lock (Gate)
            {
                EnsureLoaded();
                List<int> sorted = new List<int>();
                if (actionIds != null)
                {
                    for (int i = 0; i < actionIds.Length; i++)
                    {
                        int id = actionIds[i];
                        if (id < 0 || id > 999 || sorted.Contains(id)) continue;
                        sorted.Add(id);
                    }
                }
                sorted.Sort();

                string[] parts = new string[sorted.Count];
                for (int i = 0; i < sorted.Count; i++)
                    parts[i] = sorted[i].ToString(CultureInfo.InvariantCulture);
                Values[LimitActionsKey] = string.Join(",", parts);
                Save();
            }
        }

        public static string LoadLastSavePath()
        {
            lock (Gate)
            {
                EnsureLoaded();
                string value;
                if (!Values.TryGetValue(LastSaveKey, out value)) return null;
                value = value.Trim();
                return value.Length == 0 ? null : value;
            }
        }

        public static void SaveLastSavePath(string savePath)
        {
            lock (Gate)
            {
                EnsureLoaded();
                Values[LastSaveKey] = savePath ?? "";
                Save();
            }
        }

        private static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            Values.Clear();

            try
            {
                if (!File.Exists(FilePath)) return;
                foreach (string raw in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line[0] == '#' || line[0] == ';')
                        continue;

                    int equals = line.IndexOf('=');
                    if (equals <= 0) continue;
                    string key = line.Substring(0, equals).Trim();
                    string value = line.Substring(equals + 1).Trim();
                    if (key.Length > 0) Values[key] = value;
                }
            }
            catch
            {
                // A corrupt or unreadable config must not block startup.
            }
        }

        private static void Save()
        {
            try
            {
                string directory = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                StringBuilder text = new StringBuilder();
                text.AppendLine("# NN's AA2 Trainer settings");
                text.AppendLine("# Portable: keep this file beside AA2Trainer.exe.");
                text.AppendLine();
                foreach (KeyValuePair<string, string> pair in Values)
                    text.AppendLine(pair.Key + "=" + pair.Value);

                string temp = FilePath + ".tmp";
                File.WriteAllText(temp, text.ToString(), new UTF8Encoding(false));
                if (File.Exists(FilePath))
                {
                    try
                    {
                        File.Replace(temp, FilePath, null);
                    }
                    catch
                    {
                        File.Copy(temp, FilePath, true);
                        File.Delete(temp);
                    }
                }
                else
                {
                    File.Move(temp, FilePath);
                }
            }
            catch
            {
                // Persistence is best-effort; the in-memory values still work.
            }
        }
    }
}
