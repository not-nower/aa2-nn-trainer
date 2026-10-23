using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Microsoft.Win32;

namespace AA2Trainer
{
    public sealed class NameMap
    {
        private readonly Dictionary<string, SortedDictionary<int, string>> maps =
            new Dictionary<string, SortedDictionary<int, string>>(StringComparer.OrdinalIgnoreCase);

        private readonly List<string> loadedFiles = new List<string>();
        private readonly List<string> errors = new List<string>();

        public string StatusText { get; private set; }
        public int EntryCount { get; private set; }
        public int FileCount { get { return loadedFiles.Count; } }
        public string GameRoot { get; private set; }

        private static readonly Dictionary<string, string> DirectFields = BuildDirectFields();
        private static readonly string[] ClothingTypeFields = new string[]
        {
            "OutfitUType", "SportUType", "SwimUType", "ClubUType"
        };
        private static readonly string[] SocksFields = new string[]
        {
            "OutfitSocks", "SportSocks", "SwimSocks", "ClubSocks"
        };
        private static readonly string[] ShoesFields = new string[]
        {
            "OutfitInShoes", "OutfitOutShoes", "SportInShoes", "SportOutShoes",
            "SwimInShoes", "SwimOutShoes", "ClubInShoes", "ClubOutShoes"
        };

        public NameMap()
        {
            StatusText = "not loaded";
        }

        public bool Load(string gameRoot)
        {
            maps.Clear();
            loadedFiles.Clear();
            errors.Clear();
            EntryCount = 0;
            GameRoot = gameRoot;

            if (string.IsNullOrEmpty(gameRoot) || !Directory.Exists(gameRoot))
            {
                StatusText = "game path not found";
                return false;
            }

            string baseFile = Path.Combine(new string[]
            {
                gameRoot, "ReiEdit", "XML", "DataBlocks", "chardata.xml"
            });
            LoadFile(baseFile);

            try
            {
                string[] rootFiles = Directory.GetFiles(gameRoot, "*.xml", SearchOption.TopDirectoryOnly);
                Array.Sort(rootFiles, StringComparer.OrdinalIgnoreCase);
                foreach (string file in rootFiles)
                {
                    if (string.Equals(Path.GetFileName(file), "chardata.xml", StringComparison.OrdinalIgnoreCase))
                        continue;
                    LoadFile(file);
                }
            }
            catch (Exception ex)
            {
                errors.Add("root XML: " + ex.Message);
            }

            try
            {
                string overrideDir = Path.Combine(new string[]
                {
                    gameRoot, "ReiEdit", "XML", "Overrides"
                });
                if (Directory.Exists(overrideDir))
                {
                    string[] overrideFiles = Directory.GetFiles(overrideDir, "*.xml", SearchOption.TopDirectoryOnly);
                    Array.Sort(overrideFiles, StringComparer.OrdinalIgnoreCase);
                    foreach (string file in overrideFiles)
                        LoadFile(file);
                }
            }
            catch (Exception ex)
            {
                errors.Add("overrides: " + ex.Message);
            }

            StatusText = FileCount.ToString() + " file" + (FileCount == 1 ? "" : "s") +
                         " / " + EntryCount.ToString() + " names";
            if (errors.Count > 0) StatusText += " / " + errors.Count.ToString() + " error" +
                                                 (errors.Count == 1 ? "" : "s");
            return FileCount > 0;
        }

        private void LoadFile(string path)
        {
            if (!File.Exists(path)) return;
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(path);
                XmlNodeList enumerables = doc.SelectNodes("/dataset/enumerables/enumerable");
                if (enumerables == null) return;

                int added = 0;
                foreach (XmlNode node in enumerables)
                {
                    XmlElement enumerable = node as XmlElement;
                    if (enumerable == null) continue;
                    string key = enumerable.GetAttribute("key");
                    if (string.IsNullOrEmpty(key)) continue;

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
                        string value = CleanName(pair.GetAttribute("value"));
                        if (value.Length == 0) continue;
                        map[id] = value;
                        added++;
                        EntryCount++;
                    }
                }
                if (added > 0) loadedFiles.Add(Path.GetFileName(path));
            }
            catch (Exception ex)
            {
                errors.Add(Path.GetFileName(path) + ": " + ex.Message);
            }
        }

        private static string CleanName(string value)
        {
            if (value == null) return "";
            string s = value.Trim();
            if (s.Length > 0 && s[0] == '[')
            {
                int close = s.IndexOf(']');
                if (close > 0 && close + 1 < s.Length)
                    s = s.Substring(close + 1).Trim();
            }
            return s;
        }

        public string Get(string enumKey, int id)
        {
            SortedDictionary<int, string> map;
            if (!maps.TryGetValue(enumKey, out map)) return null;
            string value;
            if (!map.TryGetValue(id, out value)) return null;
            return value;
        }

        public string ForField(string field, int gender, int value)
        {
            if (string.IsNullOrEmpty(field)) return null;

            string key;
            if (DirectFields.TryGetValue(field, out key))
                return Get(key, value);

            if (field == "Figure")
                return Get(gender == 0 ? "MALEFIGURE" : "FEMALEFIGURE", value);
            if (field == "FaceType")
                return Get(gender == 0 ? "FACETYPE_M" : "FACETYPE_F", value);
            if (Contains(ClothingTypeFields, field))
                return Get(gender == 0 ? "CLOTHES_M" : "CLOTHES_F", value);
            if (Contains(SocksFields, field))
                return Get(gender == 0 ? "SOCKS_M" : "SOCKS_F", value);
            if (Contains(ShoesFields, field))
                return Get(gender == 0 ? "SHOES_M" : "SHOES_F", value);
            return null;
        }

        public string DescribeName(string field, int gender, int value)
        {
            string name = ForField(field, gender, value);
            if (string.IsNullOrEmpty(name)) return null;
            return name;
        }

        public string ErrorText
        {
            get { return errors.Count == 0 ? "" : string.Join("; ", errors.ToArray()); }
        }

        public static string FindGameRoot()
        {
            string dir = ReadRegistry("Software\\illusion\\AA2Play", "INSTALLDIR");
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                return dir.TrimEnd('\\');
            return null;
        }

        private static string ReadRegistry(string path, string valueName)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(path))
                {
                    if (key == null) return null;
                    return key.GetValue(valueName) as string;
                }
            }
            catch
            {
                return null;
            }
        }

        private static bool Contains(string[] values, string item)
        {
            foreach (string value in values)
                if (value == item) return true;
            return false;
        }

        private static Dictionary<string, string> BuildDirectFields()
        {
            Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            d["Personality"] = "PERSONALITY";
            d["Gender"] = "GENDER";
            d["FightingStyle"] = "FIGHTSTYLE";
            d["Oriented"] = "SEXUALITY";
            d["Heights"] = "HEIGHT";
            d["BreastSize"] = "BREASTSIZE";
            d["BreastShape"] = "BREASTSHAPE";
            d["BreastSoftness"] = "BREASTFIRMNESS";
            d["PubHairType"] = "PUBICHAIR";
            d["NippleType"] = "NIPPLETYPE";
            d["NippleColor"] = "NIPPLECOLOR";
            d["TanMark"] = "TANMARKS";
            d["PussyMosaic"] = "MOSAICCOLOR";
            d["IrisShape"] = "IRISSHAPE";
            d["IrisType"] = "IRISTYPE";
            d["HightlightType"] = "HIGHLIGHTTYPE";
            d["BrowShape"] = "EYEBROWSHAPE";
            d["Eyelid"] = "EYELIDTYPE";
            d["UpperEyelid"] = "EYELIDTYPEU";
            d["LowerEyelid"] = "EYELIDTYPEL";
            d["Glasses"] = "GLASSESTYPE";
            d["LipsticColor"] = "LIPSTICKCOLOR";
            return d;
        }
    }
}
