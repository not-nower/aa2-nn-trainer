using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

namespace AA2Trainer
{
    public sealed class NameOverlayResult
    {
        public int Files;
        public int Added;
        public int Updated;
        public int Unchanged;
        public int Checked;
        public int Total;
    }

    /// <summary>
    /// User-supplied name tables, stored as NameData.local.tsv next to the exe
    /// and applied on top of the embedded baseline and the game's XML files.
    /// Import is merge-only: entries are added or renamed, never removed.
    /// </summary>
    public static class NameOverlay
    {
        public const string BundledLabel = "2026-09-06";
        public const string FileName = "NameData.local.tsv";

        private const string HeaderFirst = "# AA2 NN's Trainer name overlay";
        private const string HeaderFormat = "# enum<TAB>id<TAB>name";

        public static string FilePath
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FileName); }
        }

        public static bool Exists
        {
            get { return File.Exists(FilePath); }
        }

        public static string StatusText()
        {
            string status = "Bundled: " + BundledLabel;
            string path = FilePath;
            if (!File.Exists(path)) return status + "  |  Overlay: none";

            try
            {
                List<KeyValuePair<string, SortedDictionary<int, string>>> tables = ReadOverlay();
                int count = 0;
                foreach (KeyValuePair<string, SortedDictionary<int, string>> table in tables)
                    count += table.Value.Count;

                string updated = "";
                foreach (string line in File.ReadLines(path))
                {
                    if (line.StartsWith("# Updated:", StringComparison.Ordinal))
                    {
                        updated = line.Substring("# Updated:".Length).Trim();
                        break;
                    }
                    if (!line.StartsWith("#", StringComparison.Ordinal)) break;
                }

                status += "  |  Overlay: " + count.ToString(CultureInfo.InvariantCulture) +
                    " names" + (updated.Length == 0 ? "" : " (" + updated + ")");
                return status;
            }
            catch
            {
                return status + "  |  Overlay: unreadable";
            }
        }

        public static NameOverlayResult MergeFromFolder(string folder)
        {
            return MergeFromFolder(folder, null);
        }

        /// <summary>
        /// Imports the XMLs in one folder. <paramref name="baseline"/> is the
        /// currently loaded name table, used only to report whether the import
        /// actually adds or renames anything.
        /// </summary>
        public static NameOverlayResult MergeFromFolder(string folder,
            Dictionary<string, SortedDictionary<int, string>> baseline)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                throw new DirectoryNotFoundException("The selected folder was not found.");

            Dictionary<string, SortedDictionary<int, string>> parsed;
            List<string> files;
            ParseFolder(folder, out parsed, out files);
            if (files.Count == 0)
                throw new InvalidDataException(
                    "No XML name tables were found in that folder. " +
                    "Expected clothes.xml, HEXAOC.xml, or personalities.xml.");

            Dictionary<string, SortedDictionary<int, string>> merged = ReadOverlayToDictionary();
            List<string> sources = ReadSources();

            NameOverlayResult result = new NameOverlayResult();
            result.Files = files.Count;

            foreach (KeyValuePair<string, SortedDictionary<int, string>> table in parsed)
            {
                SortedDictionary<int, string> target;
                if (!merged.TryGetValue(table.Key, out target))
                {
                    target = new SortedDictionary<int, string>();
                    merged[table.Key] = target;
                }

                foreach (KeyValuePair<int, string> pair in table.Value)
                {
                    string current;
                    bool found = target.TryGetValue(pair.Key, out current);
                    if (!found && baseline != null)
                    {
                        SortedDictionary<int, string> baseTable;
                        if (baseline.TryGetValue(table.Key, out baseTable))
                            found = baseTable.TryGetValue(pair.Key, out current);
                    }

                    if (!found)
                        result.Added++;
                    else if (!string.Equals(current, pair.Value, StringComparison.Ordinal))
                        result.Updated++;
                    else
                        result.Unchanged++;
                    target[pair.Key] = pair.Value;
                    result.Checked++;
                }
            }

            foreach (string file in files)
            {
                if (!sources.Exists(delegate(string name)
                {
                    return string.Equals(name, file, StringComparison.OrdinalIgnoreCase);
                }))
                    sources.Add(file);
            }

            foreach (SortedDictionary<int, string> table in merged.Values)
                result.Total += table.Count;

            // Nothing newer than the bundled tables: do not create a redundant
            // overlay that would shadow future game XMLs.
            if (!File.Exists(FilePath) && result.Added == 0 && result.Updated == 0)
                return result;

            WriteOverlay(merged, sources);
            return result;
        }

        public static void Clear()
        {
            string path = FilePath;
            if (File.Exists(path)) File.Delete(path);
        }

        /// <summary>Reads the overlay for NameMap. Returns entry count.</summary>
        public static int ApplyTo(Dictionary<string, SortedDictionary<int, string>> maps,
            List<string> errors)
        {
            if (!File.Exists(FilePath)) return 0;

            int count = 0;
            try
            {
                foreach (string line in File.ReadLines(FilePath))
                {
                    if (line.Length == 0 || line[0] == '#') continue;
                    string[] parts = line.Split('\t');
                    if (parts.Length < 3) continue;

                    string key = parts[0].Trim();
                    int id;
                    if (key.Length == 0 || !int.TryParse(parts[1].Trim(), out id)) continue;
                    string name = parts[2].Trim();
                    if (name.Length == 0) continue;

                    SortedDictionary<int, string> map;
                    if (!maps.TryGetValue(key, out map))
                    {
                        map = new SortedDictionary<int, string>();
                        maps[key] = map;
                    }
                    map[id] = name;
                    count++;
                }
            }
            catch (Exception ex)
            {
                if (errors != null) errors.Add("overlay: " + ex.Message);
            }
            return count;
        }

        private static List<KeyValuePair<string, SortedDictionary<int, string>>> ReadOverlay()
        {
            return new List<KeyValuePair<string, SortedDictionary<int, string>>>(
                ReadOverlayToDictionary());
        }

        private static Dictionary<string, SortedDictionary<int, string>> ReadOverlayToDictionary()
        {
            Dictionary<string, SortedDictionary<int, string>> result =
                new Dictionary<string, SortedDictionary<int, string>>(
                    StringComparer.OrdinalIgnoreCase);
            string path = FilePath;
            if (!File.Exists(path)) return result;

            foreach (string line in File.ReadLines(path))
            {
                if (line.Length == 0 || line[0] == '#') continue;
                string[] parts = line.Split('\t');
                if (parts.Length < 3) continue;

                string key = parts[0].Trim();
                int id;
                if (key.Length == 0 || !int.TryParse(parts[1].Trim(), out id)) continue;
                string name = parts[2].Trim();
                if (name.Length == 0) continue;

                SortedDictionary<int, string> map;
                if (!result.TryGetValue(key, out map))
                {
                    map = new SortedDictionary<int, string>();
                    result[key] = map;
                }
                map[id] = name;
            }
            return result;
        }

        private static List<string> ReadSources()
        {
            List<string> sources = new List<string>();
            string path = FilePath;
            if (!File.Exists(path)) return sources;

            try
            {
                foreach (string line in File.ReadLines(path))
                {
                    if (!line.StartsWith("# Sources:", StringComparison.Ordinal)) continue;
                    string text = line.Substring("# Sources:".Length).Trim();
                    foreach (string part in text.Split(','))
                    {
                        string name = part.Trim();
                        if (name.Length > 0) sources.Add(name);
                    }
                    break;
                }
            }
            catch { }
            return sources;
        }

        private static void WriteOverlay(
            Dictionary<string, SortedDictionary<int, string>> maps, List<string> sources)
        {
            StringBuilder text = new StringBuilder();
            text.AppendLine(HeaderFirst);
            text.AppendLine("# Bundled baseline: " + BundledLabel);
            text.AppendLine("# Updated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            if (sources.Count > 0)
                text.AppendLine("# Sources: " + string.Join(", ", sources.ToArray()));
            text.AppendLine(HeaderFormat);

            List<string> keys = new List<string>(maps.Keys);
            keys.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (string key in keys)
            {
                SortedDictionary<int, string> map = maps[key];
                foreach (KeyValuePair<int, string> pair in map)
                {
                    text.Append(key);
                    text.Append('\t');
                    text.Append(pair.Key.ToString(CultureInfo.InvariantCulture));
                    text.Append('\t');
                    text.Append(pair.Value);
                    text.AppendLine();
                }
            }

            string path = FilePath;
            string temp = path + ".tmp";
            File.WriteAllText(temp, text.ToString(), new UTF8Encoding(false));
            if (File.Exists(path))
            {
                try
                {
                    File.Replace(temp, path, null);
                }
                catch
                {
                    File.Copy(temp, path, true);
                    File.Delete(temp);
                }
            }
            else
            {
                File.Move(temp, path);
            }
        }

        private static void ParseFolder(string folder,
            out Dictionary<string, SortedDictionary<int, string>> maps,
            out List<string> files)
        {
            maps = new Dictionary<string, SortedDictionary<int, string>>(
                StringComparer.OrdinalIgnoreCase);
            files = new List<string>();

            // Only the folder itself: walking into mod/ReiEdit subfolders would
            // pull in old override tables and fight the curated baseline.
            string[] paths = Directory.GetFiles(folder, "*.xml", SearchOption.TopDirectoryOnly);
            Array.Sort(paths, StringComparer.OrdinalIgnoreCase);

            foreach (string path in paths)
            {
                int added = 0;
                try
                {
                    added = ParseFile(path, maps);
                }
                catch (Exception)
                {
                    continue;
                }
                if (added > 0)
                    files.Add(Path.GetFileName(path));
            }
        }

        private static int ParseFile(string path,
            Dictionary<string, SortedDictionary<int, string>> maps)
        {
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.DtdProcessing = DtdProcessing.Prohibit;
            settings.XmlResolver = null;
            settings.IgnoreComments = true;
            settings.IgnoreWhitespace = true;

            XmlDocument doc = new XmlDocument();
            doc.XmlResolver = null;
            using (XmlReader reader = XmlReader.Create(path, settings))
                doc.Load(reader);

            XmlNodeList enumerables = doc.SelectNodes("//enumerables/enumerable");
            if (enumerables == null) return 0;

            int added = 0;
            foreach (XmlNode node in enumerables)
            {
                XmlElement enumerable = node as XmlElement;
                if (enumerable == null) continue;
                string key = enumerable.GetAttribute("key");
                if (key.Length == 0) continue;

                SortedDictionary<int, string> map;
                if (!maps.TryGetValue(key, out map))
                {
                    map = new SortedDictionary<int, string>();
                    maps[key] = map;
                }

                foreach (XmlNode child in enumerable.ChildNodes)
                {
                    XmlElement pair = child as XmlElement;
                    if (pair == null || pair.Name != "enumpair") continue;
                    int id;
                    if (!int.TryParse(pair.GetAttribute("key"), out id)) continue;
                    string name = CleanName(pair.GetAttribute("value"));
                    if (name.Length == 0) continue;
                    map[id] = name;
                    added++;
                }
            }
            return added;
        }

        private static string CleanName(string value)
        {
            if (value == null) return "";
            string text = value.Trim();
            if (text.Length > 0 && text[0] == '[')
            {
                int close = text.IndexOf(']');
                if (close > 0 && close + 1 < text.Length)
                    text = text.Substring(close + 1).Trim();
            }
            return text.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');
        }
    }
}
