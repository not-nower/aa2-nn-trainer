using System;
using System.Collections.Generic;
using System.Text;

namespace AA2Trainer
{
    public enum OutfitFieldKind
    {
        Choice,
        Toggle,
        Number,
        Color,
        Opacity,
        Int32
    }

    public enum OutfitFieldGroup
    {
        Garment,
        Top,
        Bottom,
        Accessories,
        Underwear
    }

    public sealed class OutfitSetDef
    {
        public string Prefix;
        public string Label;
        public int BaseOffset;
    }

    public sealed class OutfitFieldDef
    {
        public int SetIndex;
        public string Name;
        public string Label;
        public int Offset;
        public OutfitFieldKind Kind;
        public OutfitFieldGroup Group;
        public int Minimum;
        public int Maximum;
    }

    public sealed class OutfitSnapshot
    {
        public int[] Values;
    }

    // The four outfit sets are identical 0x5C-byte blocks.  A colour field is
    // three RGB bytes and the "opacity" field is the fourth byte of the same
    // colour, so colour writes must only touch the first three bytes.
    public static class OutfitEditor
    {
        public const int SetSize = 0x5C;

        private const int ColorAlpha = unchecked((int)0xFF000000);

        private static readonly OutfitSetDef[] sets = new OutfitSetDef[]
        {
            S("Outfit", "School", 0xA80),
            S("Sport", "Sport", 0xADC),
            S("Swim", "Swimsuit", 0xB38),
            S("Club", "Club", 0xB94)
        };

        private sealed class Template
        {
            public string Suffix;
            public string Label;
            public int RelativeOffset;
            public OutfitFieldKind Kind;
            public OutfitFieldGroup Group;
            public int Minimum;
            public int Maximum;
        }

        private static readonly Template[] template = new Template[]
        {
            T("UType", "Outfit type", 0x00, OutfitFieldKind.Choice, OutfitFieldGroup.Garment),
            T("SkirtLen", "Skirt length", 0x04, OutfitFieldKind.Number, OutfitFieldGroup.Garment),
            T("Socks", "Socks type", 0x05, OutfitFieldKind.Choice, OutfitFieldGroup.Accessories),
            T("InShoes", "In-shoes type", 0x06, OutfitFieldKind.Choice, OutfitFieldGroup.Accessories),
            T("OutShoes", "Out-shoes type", 0x07, OutfitFieldKind.Choice, OutfitFieldGroup.Accessories),
            T("Onepiece", "One-piece", 0x08, OutfitFieldKind.Toggle, OutfitFieldGroup.Garment),
            T("Undie", "Underwear", 0x09, OutfitFieldKind.Toggle, OutfitFieldGroup.Garment),
            T("Skirt", "Skirt", 0x0A, OutfitFieldKind.Toggle, OutfitFieldGroup.Garment),

            T("Top1C", "Top 1", 0x0C, OutfitFieldKind.Color, OutfitFieldGroup.Top),
            T("Top1O", "Top 1", 0x0F, OutfitFieldKind.Opacity, OutfitFieldGroup.Top),
            T("Top2C", "Top 2", 0x10, OutfitFieldKind.Color, OutfitFieldGroup.Top),
            T("Top2O", "Top 2", 0x13, OutfitFieldKind.Opacity, OutfitFieldGroup.Top),
            T("Top3C", "Top 3", 0x14, OutfitFieldKind.Color, OutfitFieldGroup.Top),
            T("Top3O", "Top 3", 0x17, OutfitFieldKind.Opacity, OutfitFieldGroup.Top),
            T("Top4C", "Top 4", 0x18, OutfitFieldKind.Color, OutfitFieldGroup.Top),
            T("Top4O", "Top 4", 0x1B, OutfitFieldKind.Opacity, OutfitFieldGroup.Top),

            T("Bottom1C", "Bottom 1", 0x1C, OutfitFieldKind.Color, OutfitFieldGroup.Bottom),
            T("Bottom1O", "Bottom 1", 0x1F, OutfitFieldKind.Opacity, OutfitFieldGroup.Bottom),
            T("Bottom2C", "Bottom 2", 0x20, OutfitFieldKind.Color, OutfitFieldGroup.Bottom),
            T("Bottom2O", "Bottom 2", 0x23, OutfitFieldKind.Opacity, OutfitFieldGroup.Bottom),

            T("UndieC", "Underwear", 0x24, OutfitFieldKind.Color, OutfitFieldGroup.Underwear),
            T("UndieO", "Underwear", 0x27, OutfitFieldKind.Opacity, OutfitFieldGroup.Underwear),
            T("UndieT", "Underwear texture", 0x38, OutfitFieldKind.Int32, OutfitFieldGroup.Underwear),
            T("UndieH", "Underwear hue", 0x4C, OutfitFieldKind.Int32, OutfitFieldGroup.Underwear, -359, 359),
            T("UndieL", "Underwear lightness", 0x50, OutfitFieldKind.Int32, OutfitFieldGroup.Underwear, -100, 100),
            T("UndieShdH", "Underwear shadow hue", 0x54, OutfitFieldKind.Int32, OutfitFieldGroup.Underwear, -359, 359),
            T("UndieShdL", "Underwear shadow light", 0x58, OutfitFieldKind.Int32, OutfitFieldGroup.Underwear, -100, 100),
            T("SocksC", "Socks", 0x28, OutfitFieldKind.Color, OutfitFieldGroup.Accessories),
            T("SocksO", "Socks", 0x2B, OutfitFieldKind.Opacity, OutfitFieldGroup.Accessories),
            T("InShoesC", "In-shoes", 0x2C, OutfitFieldKind.Color, OutfitFieldGroup.Accessories),
            T("InShoesO", "In-shoes", 0x2F, OutfitFieldKind.Opacity, OutfitFieldGroup.Accessories),
            T("OutShoesC", "Out-shoes", 0x30, OutfitFieldKind.Color, OutfitFieldGroup.Accessories),
            T("OutShoesO", "Out-shoes", 0x33, OutfitFieldKind.Opacity, OutfitFieldGroup.Accessories)
        };

        private static readonly OutfitFieldDef[] fields = BuildFields();

        private static OutfitSetDef S(string prefix, string label, int offset)
        {
            OutfitSetDef def = new OutfitSetDef();
            def.Prefix = prefix;
            def.Label = label;
            def.BaseOffset = offset;
            return def;
        }

        private static Template T(string suffix, string label, int offset,
            OutfitFieldKind kind, OutfitFieldGroup group,
            int minimum = 0, int maximum = 255)
        {
            Template item = new Template();
            item.Suffix = suffix;
            item.Label = label;
            item.RelativeOffset = offset;
            item.Kind = kind;
            item.Group = group;
            item.Minimum = minimum;
            item.Maximum = maximum;
            return item;
        }

        private static OutfitFieldDef[] BuildFields()
        {
            List<OutfitFieldDef> list = new List<OutfitFieldDef>();
            for (int set = 0; set < sets.Length; set++)
            {
                foreach (Template item in template)
                {
                    OutfitFieldDef def = new OutfitFieldDef();
                    def.SetIndex = set;
                    def.Name = sets[set].Prefix + item.Suffix;
                    def.Label = item.Label;
                    def.Offset = sets[set].BaseOffset + item.RelativeOffset;
                    def.Kind = item.Kind;
                    def.Group = item.Group;
                    def.Minimum = item.Minimum;
                    def.Maximum = item.Maximum;
                    list.Add(def);
                }
            }
            return list.ToArray();
        }

        public static IList<OutfitSetDef> Sets { get { return sets; } }
        public static IList<OutfitFieldDef> Fields { get { return fields; } }

        public static int IndexOf(string name)
        {
            for (int i = 0; i < fields.Length; i++)
                if (fields[i].Name == name) return i;
            return -1;
        }

        public static int IndexOf(int setIndex, string suffix)
        {
            if (setIndex < 0 || setIndex >= sets.Length) return -1;
            return IndexOf(sets[setIndex].Prefix + suffix);
        }

        public static bool TryRead(GameMemory mem, CharacterInfo info,
            out OutfitSnapshot snapshot, out string error)
        {
            snapshot = null;
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

            uint characterData;
            if (!CharacterEditor.TryGetCharacterData(mem, info, out characterData, out error))
                return false;

            int[] values = new int[fields.Length];

            for (int set = 0; set < sets.Length; set++)
            {
                byte[] bytes = mem.ReadBytes(characterData + (uint)sets[set].BaseOffset, SetSize);
                if (bytes == null || bytes.Length < SetSize)
                {
                    error = "The outfit fields are unreadable.";
                    return false;
                }

                for (int i = 0; i < fields.Length; i++)
                {
                    OutfitFieldDef def = fields[i];
                    if (def.SetIndex != set) continue;

                    int relative = def.Offset - sets[set].BaseOffset;
                    if (relative < 0 || relative >= SetSize) continue;

                    if (def.Kind == OutfitFieldKind.Color)
                    {
                        if (relative + 4 > SetSize)
                        {
                            error = "The outfit colours are unreadable.";
                            return false;
                        }
                        values[i] = (BitConverter.ToInt32(bytes, relative) & 0x00FFFFFF) |
                            ColorAlpha;
                    }
                    else if (def.Kind == OutfitFieldKind.Int32)
                    {
                        if (relative + 4 > SetSize)
                        {
                            error = "The outfit values are unreadable.";
                            return false;
                        }
                        values[i] = BitConverter.ToInt32(bytes, relative);
                    }
                    else
                    {
                        values[i] = bytes[relative];
                    }
                }
            }

            OutfitSnapshot result = new OutfitSnapshot();
            result.Values = values;
            snapshot = result;
            return true;
        }

        public static bool TryWrite(GameMemory mem, CharacterInfo info,
            OutfitSnapshot original, OutfitSnapshot edited, out string changed, out string error)
        {
            changed = "";
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
            if (original == null || edited == null ||
                original.Values == null || edited.Values == null ||
                original.Values.Length != fields.Length ||
                edited.Values.Length != fields.Length)
            {
                error = "The outfit values are incomplete.";
                return false;
            }

            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i].Kind == OutfitFieldKind.Color) continue;
                if (edited.Values[i] < fields[i].Minimum ||
                    edited.Values[i] > fields[i].Maximum)
                {
                    error = fields[i].Label + " is out of range (" +
                        fields[i].Minimum.ToString() + " to " +
                        fields[i].Maximum.ToString() + ").";
                    return false;
                }
            }

            List<int> pending = new List<int>();
            for (int i = 0; i < fields.Length; i++)
            {
                if (original.Values[i] != edited.Values[i]) pending.Add(i);
            }
            if (pending.Count == 0)
            {
                changed = "No changes.";
                return true;
            }

            uint characterData;
            if (!CharacterEditor.TryGetCharacterData(mem, info, out characterData, out error))
                return false;

            StringBuilder written = new StringBuilder();
            foreach (int i in pending)
            {
                OutfitFieldDef def = fields[i];
                uint address = characterData + (uint)def.Offset;

                bool ok;
                if (def.Kind == OutfitFieldKind.Color)
                {
                    // The fourth byte is the opacity, so only write RGB here.
                    byte[] rgb = new byte[]
                    {
                        (byte)(edited.Values[i] & 0xFF),
                        (byte)((edited.Values[i] >> 8) & 0xFF),
                        (byte)((edited.Values[i] >> 16) & 0xFF)
                    };
                    ok = MemoryWriter.TryWriteBlock(mem, address, rgb, def.Label, out error);
                }
                else if (def.Kind == OutfitFieldKind.Int32)
                {
                    ok = MemoryWriter.TryWriteInt32(mem, address, edited.Values[i],
                        def.Label, out error);
                }
                else
                {
                    ok = MemoryWriter.TryWriteByte(mem, address, (byte)edited.Values[i],
                        def.Label, out error);
                }

                if (!ok) return Partial(written, error, out changed);

                if (written.Length > 0) written.Append(", ");
                written.Append(sets[def.SetIndex].Label.ToLowerInvariant() + " " +
                               Describe(def));
            }

            changed = written.ToString();
            return true;
        }

        private static string Describe(OutfitFieldDef def)
        {
            string label = def.Label.ToLowerInvariant();
            if (def.Kind == OutfitFieldKind.Color) return label + " colour";
            if (def.Kind == OutfitFieldKind.Opacity) return label + " opacity";
            return label;
        }

        private static bool Partial(StringBuilder written, string error, out string changed)
        {
            changed = written.ToString();
            if (written.Length == 0) return false;

            error = "Saved " + changed + ", but the next field failed: " + error;
            changed = "";
            return false;
        }
    }
}
