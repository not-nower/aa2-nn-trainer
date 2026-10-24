using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace AA2Trainer
{
    public sealed class TraitDef
    {
        public string Name;
        public string Display;
        public int Offset;
        public string Effect;
    }

    // The game keeps 52 one-byte character traits in a contiguous block at
    // charData + 0x6CB. The list is built from the pointer definitions so the
    // names and offsets stay in sync with the debug pages.
    public static class TraitCatalog
    {
        public const int FirstOffset = 0x6CB;
        public const int LastOffset = 0x6FE;
        public const int RainbowCardOffset = 0x6F1;
        public const int PreferenceOffset = 0x6F2;

        private static readonly Dictionary<string, string> effects = LoadEffects();
        private static readonly TraitDef[] traits = Build();

        public static IList<TraitDef> All { get { return traits; } }

        private static Dictionary<string, string> LoadEffects()
        {
            Dictionary<string, string> map =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                using (Stream stream =
                    assembly.GetManifestResourceStream("AA2Trainer.TraitEffects.tsv"))
                {
                    if (stream == null) return map;

                    using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (line.Length == 0 || line[0] == '#') continue;
                            int tab = line.IndexOf('\t');
                            if (tab <= 0) continue;

                            string key = line.Substring(0, tab).Trim();
                            string text = line.Substring(tab + 1).Replace("\\n", "\n").Trim();
                            if (key.Length == 0 || text.Length == 0) continue;
                            map[key] = text;
                        }
                    }
                }
            }
            catch
            {
                // Tooltips are optional; the UI falls back to the trait name.
            }
            return map;
        }

        private static TraitDef[] Build()
        {
            List<TraitDef> list = new List<TraitDef>();
            foreach (PointerDef def in Definitions.All)
            {
                if (def.Context != "character") continue;
                if (def.FieldOffset < FirstOffset || def.FieldOffset > LastOffset) continue;
                if (def.Chain == null || def.Chain.Length != 1 || def.Chain[0] != 0x28) continue;

                TraitDef trait = new TraitDef();
                trait.Name = def.Name;
                trait.Display = DisplayName(def.Name);
                trait.Offset = def.FieldOffset;
                string effect;
                if (effects.TryGetValue(def.Name, out effect)) trait.Effect = effect;
                list.Add(trait);
            }

            list.Sort(delegate(TraitDef a, TraitDef b) { return a.Offset.CompareTo(b.Offset); });
            return list.ToArray();
        }

        private static string DisplayName(string name)
        {
            switch (name)
            {
                case "BadwithGuys": return "Bad with guys";
                case "BadwithGirls": return "Bad with girls";
                case "Tsundare": return "Tsundere";
                case "Preverted": return "Perverted";
                case "Absentmind": return "Absent-minded";
                case "Maddlesome": return "Meddlesome";
                case "ClassInco": return "Class Prez";
                case "Singlemind": return "Single-minded";
                case "Indencisive": return "Indecisive";
                case "ASexual": return "Asexual";
                case "RainbowCard": return "Rainbow card";
                case "BreastCares": return "Breast Cares";
                case "VaginaCares": return "Vagina Cares";
                case "PenisCares": return "Penis Cares";
                case "Cunninglus": return "Cunnilingus";
                case "Fellation": return "Fellatio";
                case "DoggyStyle": return "Doggy style";
                case "Fendom": return "Femdom";
                case "NoCondom": return "No condom";
                case "Bukake": return "Bukkake";
                default: return name;
            }
        }

        public static bool TryRead(GameMemory mem, CharacterInfo info,
            out bool[] values, out string error)
        {
            values = null;
            error = null;

            if (mem == null || !mem.IsAttached)
            {
                error = "The game is not attached.";
                return false;
            }
            if (info == null || info.Pointer == 0)
            {
                error = "No character is selected.";
                return false;
            }
            if (traits.Length == 0)
            {
                error = "No trait definitions are available.";
                return false;
            }

            uint characterData;
            if (!CharacterEditor.TryGetCharacterData(mem, info, out characterData, out error))
                return false;

            byte[] bytes = mem.ReadBytes(characterData + (uint)FirstOffset, traits.Length);
            if (bytes == null)
            {
                error = "The character traits are unreadable.";
                return false;
            }

            values = new bool[traits.Length];
            for (int i = 0; i < traits.Length; i++)
                values[i] = bytes[i] != 0;
            return true;
        }

        public static bool TryWrite(GameMemory mem, CharacterInfo info, int index,
            bool enabled, out string error)
        {
            error = null;

            if (mem == null || !mem.IsAttached)
            {
                error = "The game is not attached.";
                return false;
            }
            if (!mem.CanWrite)
            {
                error = string.IsNullOrEmpty(mem.WriteError)
                    ? "The game handle is read-only."
                    : mem.WriteError;
                return false;
            }
            if (info == null || info.Pointer == 0)
            {
                error = "No character is selected.";
                return false;
            }
            if (index < 0 || index >= traits.Length)
            {
                error = "The trait index is out of range.";
                return false;
            }

            uint characterData;
            if (!CharacterEditor.TryGetCharacterData(mem, info, out characterData, out error))
                return false;

            TraitDef trait = traits[index];
            uint address = characterData + (uint)trait.Offset;
            byte desired = enabled ? (byte)1 : (byte)0;

            if (!mem.WriteBytes(address, new byte[] { desired }))
            {
                error = trait.Display + " write failed: " +
                        (mem.WriteError ?? "unknown error");
                return false;
            }

            byte? check = mem.ReadU8(address);
            if (!check.HasValue || check.Value != desired)
            {
                error = trait.Display + " verification failed at 0x" +
                        address.ToString("X8") + ".";
                return false;
            }

            return true;
        }
    }
}
