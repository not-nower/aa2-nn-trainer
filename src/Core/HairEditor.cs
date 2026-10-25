using System;
using System.Collections.Generic;
using System.Text;

namespace AA2Trainer
{
    public enum HairFieldKind
    {
        Number,
        Choice,
        Toggle
    }

    public enum HairGroup
    {
        Style,
        Length,
        Flip
    }

    public sealed class HairFieldDef
    {
        public string Name;
        public string Label;
        public int Offset;
        public HairGroup Group;
        public HairFieldKind Kind;
    }

    // Hair appearance fields. The game keeps them as one-byte values in a
    // contiguous block at charData + 0x69C .. 0x6A7: four style indices, four
    // length sliders and four flip flags.
    public static class HairEditor
    {
        public const int FirstOffset = 0x69C;
        public const int LastOffset = 0x6A7;
        public const int SpanSize = LastOffset - FirstOffset + 1;

        private static readonly HairFieldDef[] fields = new HairFieldDef[]
        {
            F("FrontHair", "Front", 0x69C, HairGroup.Style, HairFieldKind.Choice),
            F("SideHair", "Side", 0x69D, HairGroup.Style, HairFieldKind.Choice),
            F("BackHair", "Back", 0x69E, HairGroup.Style, HairFieldKind.Choice),
            F("ExtHair", "Extension", 0x69F, HairGroup.Style, HairFieldKind.Choice),

            F("FrontHairL", "Front", 0x6A0, HairGroup.Length, HairFieldKind.Number),
            F("SideHairL", "Side", 0x6A1, HairGroup.Length, HairFieldKind.Number),
            F("BackHairL", "Back", 0x6A2, HairGroup.Length, HairFieldKind.Number),
            F("ExtHairL", "Extension", 0x6A3, HairGroup.Length, HairFieldKind.Number),

            F("FlipFrontHair", "Front", 0x6A4, HairGroup.Flip, HairFieldKind.Toggle),
            F("FlipSideHair", "Side", 0x6A5, HairGroup.Flip, HairFieldKind.Toggle),
            F("FlipBackHair", "Back", 0x6A6, HairGroup.Flip, HairFieldKind.Toggle),
            F("FlipExtHair", "Extension", 0x6A7, HairGroup.Flip, HairFieldKind.Toggle)
        };

        private static HairFieldDef F(string name, string label, int offset, HairGroup group,
            HairFieldKind kind)
        {
            HairFieldDef def = new HairFieldDef();
            def.Name = name;
            def.Label = label;
            def.Offset = offset;
            def.Group = group;
            def.Kind = kind;
            return def;
        }

        public static int IndexOf(string name)
        {
            for (int i = 0; i < fields.Length; i++)
                if (fields[i].Name == name) return i;
            return -1;
        }

        // Flip flags are shown beside their matching style dropdown, so the
        // control needs the pairing.
        public static int LengthIndexForStyle(int styleIndex)
        {
            if (styleIndex < 0 || styleIndex >= fields.Length) return -1;
            return IndexOf(fields[styleIndex].Name + "L");
        }

        public static int FlipIndexForStyle(int styleIndex)
        {
            if (styleIndex < 0 || styleIndex >= fields.Length) return -1;
            return IndexOf("Flip" + fields[styleIndex].Name);
        }

        public static IList<HairFieldDef> Fields { get { return fields; } }

        public static bool TryRead(GameMemory mem, CharacterInfo info,
            out int[] values, out string error)
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

            uint characterData;
            if (!CharacterEditor.TryGetCharacterData(mem, info, out characterData, out error))
                return false;

            byte[] bytes = mem.ReadBytes(characterData + (uint)FirstOffset, SpanSize);
            if (bytes == null)
            {
                error = "The hair fields are unreadable.";
                return false;
            }

            values = new int[fields.Length];
            for (int i = 0; i < fields.Length; i++)
                values[i] = bytes[fields[i].Offset - FirstOffset];
            return true;
        }

        public static bool TryWrite(GameMemory mem, CharacterInfo info,
            int[] original, int[] edited, out string changed, out string error)
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
                original.Length != fields.Length || edited.Length != fields.Length)
            {
                error = "The hair values are incomplete.";
                return false;
            }

            for (int i = 0; i < fields.Length; i++)
            {
                if (edited[i] < 0 || edited[i] > 255)
                {
                    error = fields[i].Label + " is out of range (0-255).";
                    return false;
                }
            }

            List<int> pending = new List<int>();
            for (int i = 0; i < fields.Length; i++)
            {
                if (edited[i] != original[i]) pending.Add(i);
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
                HairFieldDef def = fields[i];
                uint address = characterData + (uint)def.Offset;

                if (!MemoryWriter.TryWriteByte(mem, address, (byte)edited[i], def.Label, out error))
                    return Partial(written, error, out changed);

                if (written.Length > 0) written.Append(", ");
                written.Append(GroupName(def.Group).ToLowerInvariant() + " " +
                               def.Label.ToLowerInvariant());
            }

            changed = written.ToString();
            return true;
        }

        private static string GroupName(HairGroup group)
        {
            switch (group)
            {
                case HairGroup.Style: return "Style";
                case HairGroup.Length: return "Length";
                default: return "Flip";
            }
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
