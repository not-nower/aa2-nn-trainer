using System;
using System.Collections.Generic;
using System.Text;

namespace AA2Trainer
{
    public enum FaceFieldKind
    {
        Number,
        Choice,
        Color,
        Toggle,
        Text
    }

    public enum FaceGroup
    {
        Face,
        Eyes,
        Iris,
        Brow,
        Glasses,
        Mole,
        Textures
    }

    public sealed class FaceFieldDef
    {
        public string Name;
        public string Label;
        public int Offset;
        public FaceFieldKind Kind;
        public FaceGroup Group;
        public int FieldSize;
        public bool FigureByGender;
    }

    public sealed class FaceSnapshot
    {
        public int[] Values;
        public string[] Texts;
    }

    // Face appearance fields. The head shape and eye sliders sit at
    // charData + 0x46A .. 0x475, the colours and the brow/glasses/mole block
    // at charData + 0x680 .. 0x698, and the AAUnlimited external-texture
    // overrides at charData + 0x476 .. 0x67F (two flags plus two 260-byte
    // Shift-JIS names). Colours are 32-bit ARGB values.
    public static class FaceEditor
    {
        public const byte ColorAlpha = 0xFF;
        public const int TextFieldSize = 260;

        private static readonly Encoding TextEncoding = GetEncoding();

        private static readonly FaceFieldDef[] fields = new FaceFieldDef[]
        {
            F("FaceType", "Face", 0x46A, FaceFieldKind.Choice, FaceGroup.Face, true),
            F("LipsticColor", "Lipstick", 0x696, FaceFieldKind.Choice, FaceGroup.Face),
            F("LipsticOpa", "Lipstick opacity", 0x697, FaceFieldKind.Number, FaceGroup.Face),

            F("EyeWidth", "Width", 0x46B, FaceFieldKind.Number, FaceGroup.Eyes),
            F("EyeHeight", "Height", 0x46C, FaceFieldKind.Number, FaceGroup.Eyes),
            F("EyePos", "Position", 0x46D, FaceFieldKind.Number, FaceGroup.Eyes),
            F("EyeSpacing", "Spacing", 0x46E, FaceFieldKind.Number, FaceGroup.Eyes),
            F("EyeAngle", "Angle", 0x46F, FaceFieldKind.Number, FaceGroup.Eyes),
            F("Eyelid", "Eyelid", 0x692, FaceFieldKind.Choice, FaceGroup.Eyes),
            F("UpperEyelid", "Upper lid", 0x693, FaceFieldKind.Choice, FaceGroup.Eyes),
            F("LowerEyelid", "Lower lid", 0x694, FaceFieldKind.Choice, FaceGroup.Eyes),
            F("LeftEyeColor", "Left color", 0x680, FaceFieldKind.Color, FaceGroup.Eyes),
            F("RightEyeColor", "Right color", 0x684, FaceFieldKind.Color, FaceGroup.Eyes),

            F("IrisShape", "Shape", 0x470, FaceFieldKind.Choice, FaceGroup.Iris),
            F("IrisType", "Type", 0x474, FaceFieldKind.Choice, FaceGroup.Iris),
            F("HightlightType", "Highlight", 0x475, FaceFieldKind.Choice, FaceGroup.Iris),
            F("IrisWidth", "Width", 0x471, FaceFieldKind.Number, FaceGroup.Iris),
            F("IrisHeight", "Height", 0x472, FaceFieldKind.Number, FaceGroup.Iris),
            F("IrisPos", "Position", 0x473, FaceFieldKind.Number, FaceGroup.Iris),

            F("BrowShape", "Shape", 0x68C, FaceFieldKind.Choice, FaceGroup.Brow),
            F("BrowAngle", "Angle", 0x68D, FaceFieldKind.Number, FaceGroup.Brow),
            F("BrowColor", "Color", 0x688, FaceFieldKind.Color, FaceGroup.Brow),

            F("Glasses", "Glasses", 0x695, FaceFieldKind.Choice, FaceGroup.Glasses),
            F("GlassesColor", "Color", 0x698, FaceFieldKind.Color, FaceGroup.Glasses),

            F("MoleLEye", "Left eye", 0x68E, FaceFieldKind.Toggle, FaceGroup.Mole),
            F("MoleREye", "Right eye", 0x68F, FaceFieldKind.Toggle, FaceGroup.Mole),
            F("MoleLMouth", "Left mouth", 0x690, FaceFieldKind.Toggle, FaceGroup.Mole),
            F("MoleRMouth", "Right mouth", 0x691, FaceFieldKind.Toggle, FaceGroup.Mole),

            F("ExtIrisTexture", "Custom iris", 0x476, FaceFieldKind.Toggle, FaceGroup.Textures),
            F("ExtIrisTextureName", "Iris file", 0x478, FaceFieldKind.Text, FaceGroup.Textures,
                false, TextFieldSize),
            F("ExtHTexture", "Custom highlight", 0x477, FaceFieldKind.Toggle, FaceGroup.Textures),
            F("ExtHTextureName", "Highlight file", 0x57C, FaceFieldKind.Text, FaceGroup.Textures,
                false, TextFieldSize)
        };

        private static FaceFieldDef F(string name, string label, int offset, FaceFieldKind kind,
            FaceGroup group, bool figureByGender = false, int fieldSize = 1)
        {
            FaceFieldDef def = new FaceFieldDef();
            def.Name = name;
            def.Label = label;
            def.Offset = offset;
            def.Kind = kind;
            def.Group = group;
            def.FigureByGender = figureByGender;
            def.FieldSize = fieldSize;
            return def;
        }

        public static IList<FaceFieldDef> Fields { get { return fields; } }

        public static bool TryRead(GameMemory mem, CharacterInfo info,
            out FaceSnapshot snapshot, out string error)
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

            FaceSnapshot result = new FaceSnapshot();
            result.Values = new int[fields.Length];
            result.Texts = new string[fields.Length];

            for (int i = 0; i < fields.Length; i++)
            {
                FaceFieldDef def = fields[i];
                uint address = characterData + (uint)def.Offset;

                if (def.Kind == FaceFieldKind.Text)
                {
                    string text = ReadText(mem, address, def.FieldSize);
                    if (text == null)
                    {
                        error = "The face fields are unreadable.";
                        return false;
                    }
                    result.Texts[i] = text;
                    continue;
                }

                int size = def.Kind == FaceFieldKind.Color ? 4 : 1;
                byte[] bytes = mem.ReadBytes(address, size);
                if (bytes == null)
                {
                    error = "The face fields are unreadable.";
                    return false;
                }

                result.Values[i] = size == 4 ? BitConverter.ToInt32(bytes, 0) : bytes[0];
            }

            snapshot = result;
            return true;
        }

        public static bool TryWrite(GameMemory mem, CharacterInfo info,
            FaceSnapshot original, FaceSnapshot edited, out string changed, out string error)
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
                original.Texts == null || edited.Texts == null ||
                original.Values.Length != fields.Length ||
                edited.Values.Length != fields.Length ||
                original.Texts.Length != fields.Length ||
                edited.Texts.Length != fields.Length)
            {
                error = "The face values are incomplete.";
                return false;
            }

            for (int i = 0; i < fields.Length; i++)
            {
                FaceFieldDef def = fields[i];

                if (def.Kind == FaceFieldKind.Color || def.Kind == FaceFieldKind.Text) continue;
                if (edited.Values[i] < 0 || edited.Values[i] > 255)
                {
                    error = def.Label + " is out of range (0-255).";
                    return false;
                }
            }

            List<int> pending = new List<int>();
            for (int i = 0; i < fields.Length; i++)
            {
                FaceFieldDef def = fields[i];
                bool different = def.Kind == FaceFieldKind.Text
                    ? !string.Equals(original.Texts[i] ?? "", edited.Texts[i] ?? "",
                                     StringComparison.Ordinal)
                    : original.Values[i] != edited.Values[i];
                if (different) pending.Add(i);
            }
            if (pending.Count == 0)
            {
                changed = "No changes.";
                return true;
            }

            // Encode the text fields up front so a bad name cannot leave the
            // character half-written.
            Dictionary<int, byte[]> textBytes = new Dictionary<int, byte[]>();
            foreach (int i in pending)
            {
                FaceFieldDef def = fields[i];
                if (def.Kind != FaceFieldKind.Text) continue;

                byte[] encoded;
                if (!TryEncode(edited.Texts[i], def.Label, def.FieldSize, out encoded, out error))
                    return false;
                textBytes[i] = encoded;
            }

            uint characterData;
            if (!CharacterEditor.TryGetCharacterData(mem, info, out characterData, out error))
                return false;

            StringBuilder written = new StringBuilder();
            foreach (int i in pending)
            {
                FaceFieldDef def = fields[i];
                uint address = characterData + (uint)def.Offset;
                bool ok;

                if (def.Kind == FaceFieldKind.Text)
                {
                    ok = MemoryWriter.TryWriteBlock(mem, address, textBytes[i], def.Label,
                        out error);
                }
                else if (def.Kind == FaceFieldKind.Color)
                {
                    int argb = (edited.Values[i] & 0x00FFFFFF) | (ColorAlpha << 24);
                    ok = MemoryWriter.TryWriteInt32(mem, address, argb, def.Label, out error);
                }
                else
                {
                    ok = MemoryWriter.TryWriteByte(mem, address, (byte)edited.Values[i],
                        def.Label, out error);
                }

                if (!ok) return Partial(written, error, out changed);

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

        private static string ReadText(GameMemory mem, uint address, int fieldSize)
        {
            byte[] bytes = mem.ReadBytes(address, fieldSize);
            if (bytes == null) return null;

            int length = Array.IndexOf(bytes, (byte)0);
            if (length < 0) length = bytes.Length;
            if (length == 0) return "";

            try { return TextEncoding.GetString(bytes, 0, length).Trim(); }
            catch { return null; }
        }

        private static bool TryEncode(string text, string label, int fieldSize,
            out byte[] buffer, out string error)
        {
            buffer = null;
            error = null;

            string value = (text ?? "").Trim();
            byte[] bytes = TextEncoding.GetBytes(value);
            if (bytes.Length > fieldSize - 1)
            {
                error = label + " is too long: " + bytes.Length.ToString() +
                        " bytes, maximum " + (fieldSize - 1).ToString() + ".";
                return false;
            }

            buffer = new byte[fieldSize];
            if (bytes.Length > 0) Buffer.BlockCopy(bytes, 0, buffer, 0, bytes.Length);
            return true;
        }

        private static Encoding GetEncoding()
        {
            try { return Encoding.GetEncoding(932); }
            catch { return Encoding.Default; }
        }
    }
}
