using System;
using System.Collections.Generic;
using System.Text;

namespace AA2Trainer
{
    public enum BodyFieldKind
    {
        Number,
        Choice,
        Color
    }

    public enum BodyGroup
    {
        Body,
        Breasts,
        Skin
    }

    public sealed class BodyFieldDef
    {
        public string Name;
        public string Label;
        public int Offset;
        public BodyFieldKind Kind;
        public BodyGroup Group;
        public string Tip;
    }

    // Body appearance fields. The game stores them as one-byte values at
    // charData + 0x448 .. 0x469, with the two colours as 32-bit ARGB values.
    public static class BodyEditor
    {
        public const int FirstOffset = 0x448;
        public const int LastOffset = 0x469;
        public const int SpanSize = LastOffset - FirstOffset + 1;
        public const byte ColorAlpha = 0xFF;

        private static readonly BodyFieldDef[] fields = new BodyFieldDef[]
        {
            F("BodyType", "Body type", 0x448, BodyFieldKind.Choice, BodyGroup.Body),
            F("Heights", "Height", 0x449, BodyFieldKind.Choice, BodyGroup.Body),
            F("Figure", "Figure", 0x44A, BodyFieldKind.Choice, BodyGroup.Body),
            F("HeadSize", "Head size", 0x44B, BodyFieldKind.Number, BodyGroup.Body),
            F("HeadLength", "Head length", 0x44C, BodyFieldKind.Number, BodyGroup.Body),
            F("Waist", "Waist", 0x44D, BodyFieldKind.Number, BodyGroup.Body),

            F("BreastSize", "Size", 0x44E, BodyFieldKind.Number, BodyGroup.Breasts,
                "Small     0~33%\nNormal   34~66%\nBig        67%+"),
            F("BreastShape", "Shape", 0x450, BodyFieldKind.Choice, BodyGroup.Breasts),
            F("BreastRoundness", "Roundness", 0x451, BodyFieldKind.Number, BodyGroup.Breasts),
            F("BreastDirection", "Direction", 0x452, BodyFieldKind.Number, BodyGroup.Breasts),
            F("BreastHeight", "Height", 0x453, BodyFieldKind.Number, BodyGroup.Breasts),
            F("BreastSpacing", "Spacing", 0x454, BodyFieldKind.Number, BodyGroup.Breasts),
            F("BreastDepth", "Depth", 0x455, BodyFieldKind.Number, BodyGroup.Breasts),
            F("BreastSoftness", "Firmness", 0x456, BodyFieldKind.Choice, BodyGroup.Breasts),

            F("SkinColor", "Skin color", 0x458, BodyFieldKind.Color, BodyGroup.Skin),
            F("PubHairType", "Pubic hair", 0x45C, BodyFieldKind.Choice, BodyGroup.Skin),
            F("PubHairOpa", "Pubic opacity", 0x45D, BodyFieldKind.Number, BodyGroup.Skin),
            F("PubHairColor", "Pubic color", 0x460, BodyFieldKind.Color, BodyGroup.Skin),
            F("NippleType", "Nipple type", 0x464, BodyFieldKind.Choice, BodyGroup.Skin),
            F("NippleColor", "Nipple color", 0x465, BodyFieldKind.Choice, BodyGroup.Skin),
            F("NippleOpa", "Nipple opacity", 0x466, BodyFieldKind.Number, BodyGroup.Skin),
            F("TanMark", "Tan mark", 0x467, BodyFieldKind.Choice, BodyGroup.Skin),
            F("TanOpa", "Tan opacity", 0x468, BodyFieldKind.Number, BodyGroup.Skin),
            F("PussyMosaic", "Mosaic", 0x469, BodyFieldKind.Choice, BodyGroup.Skin)
        };

        private static BodyFieldDef F(string name, string label, int offset, BodyFieldKind kind,
            BodyGroup group, string tip = null)
        {
            BodyFieldDef def = new BodyFieldDef();
            def.Name = name;
            def.Label = label;
            def.Offset = offset;
            def.Kind = kind;
            def.Group = group;
            def.Tip = tip;
            return def;
        }

        public static IList<BodyFieldDef> Fields { get { return fields; } }

        public static int IndexOf(string name)
        {
            for (int i = 0; i < fields.Length; i++)
                if (fields[i].Name == name) return i;
            return -1;
        }

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
                error = "The body fields are unreadable.";
                return false;
            }

            values = new int[fields.Length];
            for (int i = 0; i < fields.Length; i++)
            {
                int offset = fields[i].Offset - FirstOffset;
                if (fields[i].Kind == BodyFieldKind.Color)
                    values[i] = BitConverter.ToInt32(bytes, offset);
                else
                    values[i] = bytes[offset];
            }
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
                error = "The body values are incomplete.";
                return false;
            }

            List<int> pending = new List<int>();
            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i].Kind == BodyFieldKind.Color) continue;
                if (edited[i] < 0 || edited[i] > 255)
                {
                    error = fields[i].Label + " is out of range (0-255).";
                    return false;
                }
            }

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
                BodyFieldDef def = fields[i];
                uint address = characterData + (uint)def.Offset;

                if (def.Kind == BodyFieldKind.Color)
                {
                    int argb = (edited[i] & 0x00FFFFFF) | (ColorAlpha << 24);
                    if (!MemoryWriter.TryWriteInt32(mem, address, argb, def.Label, out error))
                        return Partial(written, error, out changed);
                }
                else
                {
                    if (!MemoryWriter.TryWriteByte(mem, address, (byte)edited[i], def.Label,
                            out error))
                        return Partial(written, error, out changed);
                }

                if (written.Length > 0) written.Append(", ");
                written.Append(def.Label.ToLowerInvariant());
            }

            changed = written.ToString();
            return true;
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
