using System;
using System.IO;
using System.Text;
using System.Windows.Media.Imaging;

namespace AA2Trainer.WpfPrototype
{
    public sealed class CardImages
    {
        public byte[] FaceBytes;
        public byte[] RosterBytes;
        public string FirstName = "";
        public string LastName = "";
        public string SourcePath = "";

        public string Name
        {
            get
            {
                return ((LastName ?? "") + " " + (FirstName ?? "")).Trim();
            }
        }
    }

    /// <summary>
    /// Lightweight card identity used to match live seats before decoding any
    /// images. Names are kept exactly as stored; no translation is applied.
    /// </summary>
    public sealed class CardEntry
    {
        public string Path = "";
        public string FirstName = "";
        public string LastName = "";

        public string Name
        {
            get
            {
                return ((LastName ?? "") + " " + (FirstName ?? "")).Trim();
            }
        }

        public string Key
        {
            get { return (LastName ?? "") + "\0" + (FirstName ?? ""); }
        }
    }

    /// <summary>
    /// Prototype-only card reader. It extracts the 200×300 card face and the
    /// 74×90 roster image from an AA2 character-card PNG. The shipping app will
    /// use the real save/card provider, but the format is the same.
    /// </summary>
    public static class CardImageLoader
    {
        private const int EditDataSize = 3011;

        private static readonly byte[] PngHeader =
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A
        };

        private static readonly byte[] EditHeader =
        {
            0x81, 0x79, 0x83, 0x47, 0x83, 0x66, 0x83, 0x42,
            0x83, 0x62, 0x83, 0x67, 0x81, 0x7A
        };

        /// <summary>
        /// Reads only the card's embedded name fields. Used to index a whole
        /// card folder cheaply; images are loaded later for matched seats.
        /// </summary>
        public static CardEntry ReadEntry(string path)
        {
            try
            {
                byte[] data = File.ReadAllBytes(path);
                if (data.Length < 64) return null;

                int editOffset = -1;
                if (Matches(data, 0, PngHeader))
                {
                    int mainEnd = FindPngEnd(data, 0);
                    if (mainEnd > 0 && Matches(data, mainEnd, EditHeader))
                        editOffset = mainEnd;
                }
                if (editOffset < 0)
                    editOffset = IndexOf(data, EditHeader, 0);
                if (editOffset < 0 || editOffset + EditDataSize > data.Length)
                    return null;

                CardEntry entry = new CardEntry();
                entry.Path = path;
                entry.LastName = ReadText(data, editOffset + 21, 260);
                entry.FirstName = ReadText(data, editOffset + 281, 260);
                if (string.IsNullOrEmpty(entry.LastName) &&
                    string.IsNullOrEmpty(entry.FirstName))
                    return null;
                return entry;
            }
            catch
            {
                return null;
            }
        }

        public static CardImages Load(string path)
        {
            try
            {
                byte[] data = File.ReadAllBytes(path);
                if (data.Length < 32 || !Matches(data, 0, PngHeader)) return null;

                int mainEnd = FindPngEnd(data, 0);
                if (mainEnd <= 0) return null;

                CardImages result = new CardImages();
                result.SourcePath = path;
                result.FaceBytes = ExtractImage(data, 0);

                int editOffset = mainEnd;
                if (!Matches(data, editOffset, EditHeader))
                {
                    editOffset = IndexOf(data, EditHeader, 0);
                    if (editOffset < 0) return result;
                }

                if (editOffset + EditDataSize + 4 > data.Length)
                    return result;

                byte[] edit = new byte[EditDataSize];
                Array.Copy(data, editOffset, edit, 0, EditDataSize);
                result.LastName = ReadText(edit, 21, 260);
                result.FirstName = ReadText(edit, 281, 260);

                int rosterSize = BitConverter.ToInt32(data, editOffset + EditDataSize);
                int rosterStart = editOffset + EditDataSize + 4;
                if (rosterSize > 0 && rosterStart + rosterSize <= data.Length)
                {
                    result.RosterBytes = ExtractImage(data, rosterStart);
                }
                return result;
            }
            catch
            {
                return null;
            }
        }

        public static BitmapImage ToImage(byte[] bytes, int decodeWidth)
        {
            if (bytes == null || bytes.Length < 8) return null;
            using (MemoryStream stream = new MemoryStream(bytes))
            {
                BitmapImage image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
                return image;
            }
        }

        private static int FindPngEnd(byte[] data, int start)
        {
            if (!Matches(data, start, PngHeader)) return -1;
            int pos = start + 8;
            while (pos + 12 <= data.Length)
            {
                long length = ReadBigEndian32(data, pos);
                if (length < 0 || pos + 12 + length > data.Length) return -1;
                string type = ChunkType(data, pos + 4);
                pos += 12 + (int)length;
                if (type == "IEND") return pos;
            }
            return -1;
        }

        private static byte[] ExtractImage(byte[] data, int start)
        {
            if (!Matches(data, start, PngHeader)) return null;
            using (MemoryStream output = new MemoryStream())
            {
                output.Write(data, start, 8);
                int pos = start + 8;
                while (pos + 12 <= data.Length)
                {
                    long length = ReadBigEndian32(data, pos);
                    if (length < 0 || pos + 12 + length > data.Length) return null;
                    string type = ChunkType(data, pos + 4);
                    int total = 12 + (int)length;
                    if (type != "pHYs" && type != "aaUd")
                        output.Write(data, pos, total);
                    pos += total;
                    if (type == "IEND") return output.ToArray();
                }
            }
            return null;
        }

        private static string ReadText(byte[] data, int offset, int size)
        {
            if (data == null || offset < 0 || size <= 0 ||
                offset + size > data.Length) return "";

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
                return Encoding.GetEncoding(932).GetString(decoded).Trim();
            }
            catch
            {
                return Encoding.Default.GetString(decoded).Trim();
            }
        }

        private static string ChunkType(byte[] data, int offset)
        {
            if (offset < 0 || offset + 4 > data.Length) return "";
            return Encoding.ASCII.GetString(data, offset, 4);
        }

        private static long ReadBigEndian32(byte[] data, int offset)
        {
            if (offset < 0 || offset + 4 > data.Length) return -1;
            return ((long)data[offset] << 24) |
                ((long)data[offset + 1] << 16) |
                ((long)data[offset + 2] << 8) | data[offset + 3];
        }

        private static bool Matches(byte[] data, int offset, byte[] pattern)
        {
            if (data == null || pattern == null) return false;
            if (offset < 0 || offset > data.Length - pattern.Length) return false;
            for (int i = 0; i < pattern.Length; i++)
                if (data[offset + i] != pattern[i]) return false;
            return true;
        }

        private static int IndexOf(byte[] data, byte[] pattern, int start)
        {
            if (data == null || pattern == null) return -1;
            for (int i = Math.Max(0, start); i <= data.Length - pattern.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (data[i + j] != pattern[j]) { match = false; break; }
                }
                if (match) return i;
            }
            return -1;
        }
    }
}
