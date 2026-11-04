using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace AA2Trainer
{
    /// <summary>
    /// One timestamped .bak next to an AA2 class save. The timestamp in the
    /// file name is the creation time; backups written by Import/Export,
    /// Backup, and Restore all use the same naming scheme.
    /// </summary>
    public sealed class SaveBackupEntry
    {
        public string BackupPath;
        public string SavePath;
        public DateTime Created;
        public long Size;

        public string SaveName
        {
            get { return Path.GetFileName(SavePath); }
        }

        public string BackupName
        {
            get { return Path.GetFileName(BackupPath); }
        }
    }

    /// <summary>
    /// Creates, lists, and restores the timestamped .bak files kept beside
    /// AA2 class saves. Only names of the form
    /// &lt;save&gt;.sav.yyyyMMdd-HHmmss[-n].bak are considered, because the
    /// file name is what identifies the save a backup belongs to.
    /// </summary>
    public static class SaveBackup
    {
        private static readonly Regex BackupPattern = new Regex(
            @"^(?<save>.+\.sav)\.(?<stamp>\d{8}-\d{6})(?:-\d+)?\.bak$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static string DefaultDirectory()
        {
            string root = NameMap.FindGameRoot();
            if (string.IsNullOrEmpty(root)) return "";
            return Path.Combine(Path.Combine(Path.Combine(root, "data"),
                "save"), "class");
        }

        public static string Create(string savePath)
        {
            if (string.IsNullOrEmpty(savePath))
                throw new ArgumentException("Select a class save first.", "savePath");
            if (!File.Exists(savePath))
                throw new FileNotFoundException("The class save was not found.",
                    savePath);
            if (!string.Equals(Path.GetExtension(savePath), ".sav",
                StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    "Only AA2 class .sav files can be backed up.");

            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss",
                CultureInfo.InvariantCulture);
            string backup = savePath + "." + stamp + ".bak";
            int suffix = 1;
            while (File.Exists(backup))
            {
                backup = savePath + "." + stamp + "-" + suffix + ".bak";
                suffix++;
            }
            File.Copy(savePath, backup, false);
            return backup;
        }

        public static List<SaveBackupEntry> List(string savePath,
            bool includeOtherSaves)
        {
            List<SaveBackupEntry> entries = new List<SaveBackupEntry>();
            string wantedPath = null;
            string directory;
            try
            {
                directory = string.IsNullOrEmpty(savePath)
                    ? DefaultDirectory()
                    : Path.GetDirectoryName(Path.GetFullPath(savePath));
                if (!string.IsNullOrEmpty(savePath))
                    wantedPath = Path.GetFullPath(savePath);
            }
            catch
            {
                directory = DefaultDirectory();
            }
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return entries;

            string[] files;
            try
            {
                files = Directory.GetFiles(directory, "*.bak");
            }
            catch
            {
                return entries;
            }

            foreach (string file in files)
            {
                string name = Path.GetFileName(file);
                Match match = BackupPattern.Match(name);
                if (!match.Success) continue;

                string original = Path.Combine(directory,
                    match.Groups["save"].Value);
                if (!includeOtherSaves && wantedPath != null &&
                    !PathsEqual(original, wantedPath))
                    continue;

                SaveBackupEntry entry = new SaveBackupEntry();
                entry.BackupPath = file;
                entry.SavePath = original;
                entry.Size = 0;
                try { entry.Size = new FileInfo(file).Length; }
                catch { }

                DateTime created;
                if (!DateTime.TryParseExact(match.Groups["stamp"].Value,
                    "yyyyMMdd-HHmmss", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out created))
                {
                    try { created = File.GetLastWriteTime(file); }
                    catch { created = DateTime.MinValue; }
                }
                entry.Created = created;
                entries.Add(entry);
            }

            entries.Sort(delegate (SaveBackupEntry a, SaveBackupEntry b)
            {
                int byTime = b.Created.CompareTo(a.Created);
                if (byTime != 0) return byTime;
                return string.Compare(a.BackupName, b.BackupName,
                    StringComparison.OrdinalIgnoreCase);
            });
            return entries;
        }

        public static string Restore(SaveBackupEntry entry,
            out string safetyBackup)
        {
            if (entry == null)
                throw new ArgumentException("Select a backup first.", "entry");
            if (!File.Exists(entry.BackupPath))
                throw new FileNotFoundException("The selected backup was not found.",
                    entry.BackupPath);

            // Read the backup before touching the live save. Load() checks the
            // AA2 class-save header and card layout, so an unrelated .bak cannot
            // overwrite a save.
            ClassSaveFile.Load(entry.BackupPath);

            string target = entry.SavePath;
            string directory = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            safetyBackup = null;
            if (File.Exists(target))
                safetyBackup = Create(target);

            string temp = target + ".restore.tmp";
            File.Copy(entry.BackupPath, temp, true);
            try
            {
                if (File.Exists(target))
                {
                    try
                    {
                        File.Replace(temp, target, null);
                    }
                    catch
                    {
                        File.Copy(temp, target, true);
                        File.Delete(temp);
                    }
                }
                else
                {
                    File.Move(temp, target);
                }
            }
            finally
            {
                if (File.Exists(temp)) File.Delete(temp);
            }
            return target;
        }

        public static bool PathsEqual(string first, string second)
        {
            if (string.IsNullOrEmpty(first) || string.IsNullOrEmpty(second))
                return false;
            try
            {
                return string.Equals(Path.GetFullPath(first), Path.GetFullPath(second),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return string.Equals(first, second,
                    StringComparison.OrdinalIgnoreCase);
            }
        }

        public static string LoadLastSavePath()
        {
            return TrainerSettings.LoadLastSavePath();
        }

        public static void SaveLastSavePath(string savePath)
        {
            TrainerSettings.SaveLastSavePath(savePath);
        }
    }
}
