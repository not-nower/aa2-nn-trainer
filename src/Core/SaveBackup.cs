using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace AA2Trainer
{
    /// <summary>
    /// One timestamped .sav backup beside an AA2 class save. When AAUnlimited
    /// data existed at backup time, AauBackupPath points at the paired .json
    /// backup with the same timestamp.
    /// </summary>
    public sealed class SaveBackupEntry
    {
        public string BackupPath;
        public string SavePath;
        public string AauPath;
        public string AauBackupPath;
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

        public bool HasAauBackup
        {
            get { return !string.IsNullOrEmpty(AauBackupPath); }
        }
    }

    /// <summary>Result of restoring one backup set.</summary>
    public sealed class SaveRestoreResult
    {
        public string SavePath;
        public string AauPath;
        public bool RestoredAau;
        public string SafetySaveBackup;
        public string SafetyAauBackup;
    }

    /// <summary>
    /// Creates, lists, and restores the timestamped .bak files kept beside
    /// AA2 class saves. The .sav is always included; the AAUnlimited .json
    /// sidecar is included when it exists. Only names of the form
    /// &lt;save&gt;.sav.yyyyMMdd-HHmmss[-n].bak are listed, because the file
    /// name is what identifies the save a backup belongs to.
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

        public static string AauPath(string savePath)
        {
            if (string.IsNullOrEmpty(savePath)) return null;
            return Path.ChangeExtension(savePath, ".json");
        }

        /// <summary>
        /// Finds the AAU .json backup paired with a .sav backup. Older backups
        /// created before AAU support return null.
        /// </summary>
        public static string FindAauBackup(string savePath, string savBackupPath)
        {
            if (string.IsNullOrEmpty(savePath) ||
                string.IsNullOrEmpty(savBackupPath)) return null;

            string saveName = Path.GetFileName(savePath);
            string backupName = Path.GetFileName(savBackupPath);
            if (!backupName.StartsWith(saveName + ".",
                StringComparison.OrdinalIgnoreCase)) return null;

            string suffix = backupName.Substring(saveName.Length);
            string aauPath = AauPath(savePath);
            string aauBackup = Path.Combine(Path.GetDirectoryName(aauPath),
                Path.GetFileName(aauPath) + suffix);
            return File.Exists(aauBackup) ? aauBackup : null;
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

            string aauPath = AauPath(savePath);
            bool hasAau = File.Exists(aauPath);
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss",
                CultureInfo.InvariantCulture);

            string backup;
            string aauBackup = null;
            int suffix = 0;
            while (true)
            {
                string suffixText = suffix == 0 ? "" :
                    "-" + suffix.ToString(CultureInfo.InvariantCulture);
                backup = savePath + "." + stamp + suffixText + ".bak";
                aauBackup = hasAau
                    ? aauPath + "." + stamp + suffixText + ".bak" : null;
                if (!File.Exists(backup) &&
                    (!hasAau || !File.Exists(aauBackup)))
                    break;
                suffix++;
            }

            bool saveCopied = false;
            bool aauCopied = false;
            try
            {
                File.Copy(savePath, backup, false);
                saveCopied = true;
                if (hasAau)
                {
                    File.Copy(aauPath, aauBackup, false);
                    aauCopied = true;
                }
                return backup;
            }
            catch
            {
                if (aauCopied) TryDelete(aauBackup);
                if (saveCopied) TryDelete(backup);
                throw;
            }
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
                entry.AauPath = AauPath(original);
                entry.AauBackupPath = FindAauBackup(original, file);
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

        public static SaveRestoreResult Restore(SaveBackupEntry entry)
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
            string aauTarget = string.IsNullOrEmpty(entry.AauPath)
                ? AauPath(target) : entry.AauPath;
            bool restoreAau = entry.HasAauBackup;
            if (restoreAau && !File.Exists(entry.AauBackupPath))
                throw new FileNotFoundException(
                    "The AAUnlimited data backup was not found.",
                    entry.AauBackupPath);
            if (restoreAau)
            {
                using (File.OpenRead(entry.AauBackupPath)) { }
            }

            string directory = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            SaveRestoreResult result = new SaveRestoreResult();
            result.SavePath = target;
            result.AauPath = aauTarget;
            result.RestoredAau = restoreAau;

            if (File.Exists(target))
            {
                result.SafetySaveBackup = Create(target);
                result.SafetyAauBackup = FindAauBackup(target,
                    result.SafetySaveBackup);
            }
            else if (File.Exists(aauTarget))
            {
                result.SafetyAauBackup = CreateAauOnly(aauTarget);
            }

            string tempSave = target + ".restore.tmp";
            string tempAau = restoreAau ? aauTarget + ".restore.tmp" : null;
            try
            {
                File.Copy(entry.BackupPath, tempSave, true);
                if (restoreAau)
                    File.Copy(entry.AauBackupPath, tempAau, true);

                ReplaceWithBackup(tempSave, target);
                if (restoreAau)
                    ReplaceWithBackup(tempAau, aauTarget);
                return result;
            }
            finally
            {
                TryDelete(tempSave);
                if (tempAau != null) TryDelete(tempAau);
            }
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

        private static string CreateAauOnly(string aauPath)
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss",
                CultureInfo.InvariantCulture);
            string backup;
            int suffix = 0;
            while (true)
            {
                string suffixText = suffix == 0 ? "" :
                    "-" + suffix.ToString(CultureInfo.InvariantCulture);
                backup = aauPath + "." + stamp + suffixText + ".bak";
                if (!File.Exists(backup)) break;
                suffix++;
            }
            File.Copy(aauPath, backup, false);
            return backup;
        }

        private static void ReplaceWithBackup(string backupPath, string target)
        {
            if (File.Exists(target))
            {
                try
                {
                    File.Replace(backupPath, target, null);
                }
                catch
                {
                    File.Copy(backupPath, target, true);
                    File.Delete(backupPath);
                }
            }
            else
            {
                File.Move(backupPath, target);
            }
        }

        private static void TryDelete(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
        }
    }
}
