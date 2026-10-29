using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace AA2Trainer
{
    /// <summary>
    /// One field inside the 3011-byte card edit-data block. The card block is
    /// the same layout used by the live CharacterData, with a -0x28 offset.
    /// </summary>
    public sealed class CardFieldDef
    {
        public string Section;
        public string Key;
        public int Offset;
        public string Type;
        public int Size;
        public string EnumName;
        public int CopyOffset;
        public string Label;
    }

    public static class CardFields
    {
        private const int EditDataSize = 3011;

        private static readonly string[] SectionOrder =
        {
            "Profile",
            "Appearance",
            "Traits",
            "H Preferences",
            "Risk/Pregnancy",
            "Outfit - Uniform",
            "Outfit - Sports",
            "Outfit - Swimsuit",
            "Outfit - Club"
        };

        private static readonly List<CardFieldDef> all = Load();

        public static List<CardFieldDef> All
        {
            get { return all; }
        }

        public static string[] Sections
        {
            get { return SectionOrder; }
        }

        public static List<CardFieldDef> InSection(string section)
        {
            List<CardFieldDef> list = new List<CardFieldDef>();
            foreach (CardFieldDef field in all)
            {
                if (string.Equals(field.Section, section, StringComparison.Ordinal))
                    list.Add(field);
            }
            return list;
        }

        public static bool MatchesGender(CardFieldDef field, int gender)
        {
            if (field == null || gender < 0) return true;
            if (field.Key.EndsWith("_F", StringComparison.OrdinalIgnoreCase) && gender == 0)
                return false;
            if (field.Key.EndsWith("_M", StringComparison.OrdinalIgnoreCase) && gender == 1)
                return false;
            return true;
        }

        public static bool Same(byte[] current, byte[] incoming, CardFieldDef field)
        {
            if (current == null || incoming == null || field == null) return false;
            if (!Valid(field, current) || !Valid(field, incoming)) return false;
            for (int i = 0; i < field.Size; i++)
            {
                if (current[field.Offset + i] != incoming[field.Offset + i])
                    return false;
            }
            if (field.CopyOffset >= 0 &&
                field.CopyOffset + field.Size <= current.Length &&
                field.CopyOffset + field.Size <= incoming.Length)
            {
                for (int i = 0; i < field.Size; i++)
                {
                    if (current[field.CopyOffset + i] != incoming[field.CopyOffset + i])
                        return false;
                }
            }
            return true;
        }

        public static void Copy(byte[] source, byte[] target, CardFieldDef field)
        {
            if (source == null || target == null || field == null) return;
            if (!Valid(field, source) || !Valid(field, target)) return;
            Array.Copy(source, field.Offset, target, field.Offset, field.Size);
            if (field.CopyOffset >= 0 &&
                field.CopyOffset + field.Size <= EditDataSize)
            {
                Array.Copy(source, field.Offset, target, field.CopyOffset, field.Size);
            }
        }

        public static void CopySection(byte[] source, byte[] target, string section)
        {
            foreach (CardFieldDef field in all)
            {
                if (string.Equals(field.Section, section, StringComparison.Ordinal))
                    Copy(source, target, field);
            }
        }

        public static string Format(byte[] data, CardFieldDef field, NameMap names)
        {
            if (data == null || field == null || !Valid(field, data)) return "";
            switch (field.Type)
            {
                case "encodedstring":
                case "string":
                    return ReadText(data, field.Offset, field.Size);
                case "enum":
                {
                    int value;
                    if (field.Size == 4)
                        value = BitConverter.ToInt32(data, field.Offset);
                    else if (field.Size == 2)
                        value = BitConverter.ToInt16(data, field.Offset);
                    else
                        value = data[field.Offset];
                    if (string.Equals(field.EnumName, "GENDER", StringComparison.Ordinal))
                        return value == 0 ? "Male" : "Female";
                    string name = names == null ? null : names.Get(field.EnumName, value);
                    return string.IsNullOrEmpty(name)
                        ? value.ToString(CultureInfo.InvariantCulture) : name;
                }
                case "bool":
                    return data[field.Offset] != 0 ? "On" : "Off";
                case "color":
                {
                    uint value = BitConverter.ToUInt32(data, field.Offset);
                    return "#" + (value & 0xFFFFFF).ToString("X6");
                }
                case "int32":
                    return BitConverter.ToInt32(data, field.Offset)
                        .ToString(CultureInfo.InvariantCulture);
                case "byte":
                    return data[field.Offset].ToString(CultureInfo.InvariantCulture);
                default:
                    return "";
            }
        }

        public static string ReadText(byte[] data, int offset, int size)
        {
            if (data == null || offset < 0 || size <= 0 ||
                offset + size > data.Length) return "";

            // Card edit-data strings are stored with every byte XORed by 0xFF.
            int length = 0;
            while (length < size)
            {
                byte value = (byte)(data[offset + length] ^ 0xFF);
                if (value == 0) break;
                length++;
            }
            if (length == 0) return "";

            byte[] decoded = new byte[length];
            for (int i = 0; i < length; i++)
                decoded[i] = (byte)(data[offset + i] ^ 0xFF);

            try
            {
                return Encoding.GetEncoding(932)
                    .GetString(decoded, 0, decoded.Length).Trim();
            }
            catch
            {
                return Encoding.Default
                    .GetString(decoded, 0, decoded.Length).Trim();
            }
        }

        private static bool Valid(CardFieldDef field, byte[] data)
        {
            return data != null && field.Offset >= 0 && field.Size > 0 &&
                field.Offset + field.Size <= data.Length;
        }

        private static List<CardFieldDef> Load()
        {
            List<CardFieldDef> list = new List<CardFieldDef>();
            using (Stream stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("AA2Trainer.CardFields.tsv"))
            {
                if (stream == null) return list;
                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.Length == 0) continue;
                        string[] parts = line.Split('\t');
                        if (parts.Length < 7) continue;

                        CardFieldDef field = new CardFieldDef();
                        field.Section = parts[0];
                        field.Key = parts[1];
                        field.Offset = int.Parse(parts[2], CultureInfo.InvariantCulture);
                        field.Type = parts[3];
                        field.Size = int.Parse(parts[4], CultureInfo.InvariantCulture);
                        field.EnumName = parts[5];
                        field.CopyOffset = int.Parse(parts[6], CultureInfo.InvariantCulture);
                        field.Label = MakeLabel(field.Key);
                        if (field.Offset >= 0 && field.Size > 0 &&
                            field.Offset + field.Size <= EditDataSize)
                            list.Add(field);
                    }
                }
            }
            return list;
        }

        private static string MakeLabel(string key)
        {
            string text = key ?? "";
            int underscore = text.IndexOf('_');
            if (underscore >= 0) text = text.Substring(underscore + 1);
            text = text.Replace('_', ' ').ToLowerInvariant();
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(text);
        }
    }
}
