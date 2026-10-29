using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AA2Trainer
{
    /// <summary>
    /// One AA2 character card: the card-face PNG, AAU data, 3011-byte edit
    /// data and roster PNG. This is the shared representation for both a
    /// standalone card file and a card inside a class save.
    /// </summary>
    public sealed class CardSnapshot
    {
        public const int EditDataSize = 3011;

        private static readonly byte[] PngHeader =
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A
        };

        private static readonly byte[] EditHeader =
        {
            0x81, 0x79, 0x83, 0x47, 0x83, 0x66, 0x83, 0x42,
            0x83, 0x62, 0x83, 0x67, 0x81, 0x7A
        };

        private static readonly byte[] EndChunk =
        {
            0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44,
            0xAE, 0x42, 0x60, 0x82
        };

        public byte[] EditData;
        public byte[] FacePng;
        public byte[] RosterPng;
        public byte[] AauChunk;

        public string Name = "(unnamed)";
        public string SourcePath;
        public int Seat = -1;
        public int Gender = -1;

        public bool HasAau
        {
            get { return AauChunk != null && AauChunk.Length > 0; }
        }

        public static CardSnapshot ParseCard(byte[] data, int cardStart,
            int seat, int gender, out int cardEnd)
        {
            cardEnd = 0;
            if (data == null) throw new InvalidDataException("No card data.");
            if (!Matches(data, cardStart, PngHeader))
                throw new InvalidDataException("The card does not start with a PNG.");

            CardSnapshot card = new CardSnapshot();
            card.Seat = seat;
            card.Gender = gender;

            int mainEnd = FindPngEnd(data, cardStart);
            card.FacePng = ExtractImage(data, cardStart, out card.AauChunk);

            int editOffset = mainEnd;
            if (!Matches(data, editOffset, EditHeader))
                throw new InvalidDataException("The card edit data header is invalid.");
            if (editOffset + EditDataSize + 4 > data.Length)
                throw new InvalidDataException("The card edit data is truncated.");

            card.EditData = new byte[EditDataSize];
            Array.Copy(data, editOffset, card.EditData, 0, EditDataSize);

            int rosterSize = ReadI32(data, editOffset + EditDataSize);
            int rosterStart = editOffset + EditDataSize + 4;
            if (rosterSize <= 0 || rosterStart + rosterSize > data.Length)
                throw new InvalidDataException("The roster image is invalid.");

            int rosterEnd = FindPngEnd(data, rosterStart);
            card.RosterPng = ExtractImage(data, rosterStart, out _);
            cardEnd = rosterEnd + 4;
            if (cardEnd > data.Length)
                throw new InvalidDataException("The card ends past the save data.");

            card.RefreshIdentity();
            return card;
        }

        public static CardSnapshot ParseStandalone(string path)
        {
            byte[] data = File.ReadAllBytes(path);
            int cardEnd;
            CardSnapshot card;
            try
            {
                card = ParseCard(data, 0, -1, -1, out cardEnd);
            }
            catch
            {
                card = ParseByHeader(data);
            }
            card.SourcePath = path;
            return card;
        }

        private static CardSnapshot ParseByHeader(byte[] data)
        {
            int editOffset = IndexOf(data, EditHeader, 0);
            if (editOffset < 0)
                throw new InvalidDataException("The card edit data was not found.");
            if (editOffset + EditDataSize + 4 > data.Length)
                throw new InvalidDataException("The card edit data is truncated.");

            CardSnapshot card = new CardSnapshot();
            card.FacePng = ExtractImage(data, 0, out card.AauChunk);
            card.EditData = new byte[EditDataSize];
            Array.Copy(data, editOffset, card.EditData, 0, EditDataSize);

            int rosterSize = ReadI32(data, editOffset + EditDataSize);
            int rosterStart = editOffset + EditDataSize + 4;
            if (rosterSize > 0 && rosterStart + rosterSize <= data.Length)
                card.RosterPng = ExtractImage(data, rosterStart, out _);
            else
                card.RosterPng = EndChunk;

            card.RefreshIdentity();
            return card;
        }

        public void RefreshIdentity()
        {
            if (EditData == null) return;
            string family = CardFields.ReadText(EditData, 21, 260);
            string first = CardFields.ReadText(EditData, 281, 260);
            Name = (family + " " + first).Trim();
            if (Name.Length == 0) Name = "(unnamed)";
            if (EditData.Length > 20 && EditData[20] <= 1)
                Gender = EditData[20];
        }

        public byte[] BuildMainPng()
        {
            if (FacePng == null || FacePng.Length < 12)
                throw new InvalidDataException("The card-face PNG is missing.");
            if (!HasAau) return FacePng;

            int bodyLength = FacePng.Length - 12;
            byte[] result = new byte[bodyLength + AauChunk.Length + 12];
            Array.Copy(FacePng, 0, result, 0, bodyLength);
            Array.Copy(AauChunk, 0, result, bodyLength, AauChunk.Length);
            Array.Copy(EndChunk, 0, result, bodyLength + AauChunk.Length, 12);
            return result;
        }

        public byte[] BuildCardBytes(byte[] playData)
        {
            byte[] main = BuildMainPng();
            byte[] roster = RosterPng ?? EndChunk;
            int finalOffset = EditDataSize + 4 + roster.Length + 4;

            using (MemoryStream stream = new MemoryStream(
                main.Length + EditDataSize + roster.Length + 12 +
                (playData == null ? 0 : playData.Length)))
            {
                stream.Write(main, 0, main.Length);
                stream.Write(EditData, 0, EditDataSize);
                byte[] size = BitConverter.GetBytes(roster.Length);
                stream.Write(size, 0, 4);
                stream.Write(roster, 0, roster.Length);
                stream.Write(BitConverter.GetBytes(finalOffset), 0, 4);
                if (playData != null && playData.Length > 0)
                    stream.Write(playData, 0, playData.Length);
                return stream.ToArray();
            }
        }

        private static int FindPngEnd(byte[] data, int start)
        {
            if (!Matches(data, start, PngHeader))
                throw new InvalidDataException("Invalid PNG header.");
            int pos = start + 8;
            while (pos + 12 <= data.Length)
            {
                long length = ReadBigEndian32(data, pos);
                if (length < 0 || pos + 12 + length > data.Length)
                    throw new InvalidDataException("Invalid PNG chunk length.");
                string type = ChunkType(data, pos + 4);
                pos += 12 + (int)length;
                if (type == "IEND") return pos;
            }
            throw new InvalidDataException("PNG IEND not found.");
        }

        private static byte[] ExtractImage(byte[] data, int start,
            out byte[] aauChunk)
        {
            aauChunk = null;
            if (!Matches(data, start, PngHeader))
                throw new InvalidDataException("Invalid PNG header.");

            using (MemoryStream output = new MemoryStream())
            {
                output.Write(data, start, 8);
                int pos = start + 8;
                while (pos + 12 <= data.Length)
                {
                    long length = ReadBigEndian32(data, pos);
                    if (length < 0 || pos + 12 + length > data.Length)
                        throw new InvalidDataException("Invalid PNG chunk length.");
                    string type = ChunkType(data, pos + 4);
                    int total = 12 + (int)length;

                    if (type == "aaUd")
                    {
                        aauChunk = new byte[total];
                        Array.Copy(data, pos, aauChunk, 0, total);
                    }
                    else if (type != "pHYs")
                    {
                        output.Write(data, pos, total);
                    }

                    pos += total;
                    if (type == "IEND") return output.ToArray();
                }
            }
            throw new InvalidDataException("PNG IEND not found.");
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

        private static int ReadI32(byte[] data, int offset)
        {
            if (offset < 0 || offset + 4 > data.Length)
                throw new InvalidDataException("Card data ended unexpectedly.");
            return BitConverter.ToInt32(data, offset);
        }

        private static bool Matches(byte[] data, int offset, byte[] pattern)
        {
            if (data == null || pattern == null) return false;
            if (offset < 0 || offset > data.Length - pattern.Length) return false;
            for (int i = 0; i < pattern.Length; i++)
            {
                if (data[offset + i] != pattern[i]) return false;
            }
            return true;
        }

        private static int IndexOf(byte[] data, byte[] pattern, int start)
        {
            if (data == null || pattern == null || pattern.Length == 0) return -1;
            int last = data.Length - pattern.Length;
            for (int i = Math.Max(0, start); i <= last; i++)
            {
                if (Matches(data, i, pattern)) return i;
            }
            return -1;
        }
    }
}
