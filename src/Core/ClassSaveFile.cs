using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AA2Trainer
{
    public sealed class SaveCardEntry
    {
        public int PrefixStart;
        public int CardStart;
        public int CardEnd;
        public int PlayStart;
        public int PlayEnd;
        public byte Gender;
        public int Seat;
        public CardSnapshot Card;
        public byte[] PlayData;
        public bool Dirty;
    }

    /// <summary>
    /// Offline AA2 class save reader/writer. The header, every card, and the
    /// footer are preserved; play data is copied byte-for-byte so replacing a
    /// card does not touch relationships, history, promises, or status.
    /// </summary>
    public sealed class ClassSaveFile
    {
        private const int HeaderSize = 0x13D;
        private const int MaxCards = 100;
        private const int MaxTargets = 64;
        private const int MaxActions = 256;
        private const int MaxIntArray = 100000;

        private static readonly byte[] PngHeader =
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A
        };

        public string Path;
        public byte[] Raw;
        public byte[] Header;
        public byte[] Footer;
        public readonly List<SaveCardEntry> Cards = new List<SaveCardEntry>();

        public static ClassSaveFile Load(string path)
        {
            byte[] data = File.ReadAllBytes(path);
            if (data.Length < HeaderSize)
                throw new InvalidDataException("The save file is too small.");
            if (BitConverter.ToInt32(data, 0) != 0x65)
                throw new InvalidDataException(
                    "Unsupported save format. Expected AA2 class save version 0x65.");

            int boys = BitConverter.ToInt32(data, 0x64);
            int girls = BitConverter.ToInt32(data, 0x68);
            int wanted = boys + girls;
            if (wanted <= 0 || wanted > MaxCards)
                throw new InvalidDataException("The save header has an invalid student count.");

            ClassSaveFile save = new ClassSaveFile();
            save.Path = path;
            save.Raw = data;
            save.Header = new byte[HeaderSize];
            Array.Copy(data, 0, save.Header, 0, HeaderSize);

            int pos = HeaderSize;
            for (int i = 0; i < wanted; i++)
            {
                int cardStart = IndexOf(data, PngHeader, pos);
                if (cardStart < 5) break;
                if (cardStart - 5 < pos)
                    throw new InvalidDataException("Card prefix overlaps the previous card.");

                byte gender = data[cardStart - 5];
                int seat = BitConverter.ToInt32(data, cardStart - 4);
                if (seat < 0 || seat > 24)
                    throw new InvalidDataException("The save contains an invalid seat.");

                int cardEnd;
                CardSnapshot card = CardSnapshot.ParseCard(
                    data, cardStart, seat, gender, out cardEnd);
                int playEnd = PlayDataEnd(data, cardEnd);
                if (playEnd <= cardEnd)
                    throw new InvalidDataException("The card play data is invalid.");

                SaveCardEntry entry = new SaveCardEntry();
                entry.PrefixStart = cardStart - 5;
                entry.CardStart = cardStart;
                entry.CardEnd = cardEnd;
                entry.PlayStart = cardEnd;
                entry.PlayEnd = playEnd;
                entry.Gender = gender;
                entry.Seat = seat;
                entry.Card = card;
                entry.PlayData = new byte[playEnd - cardEnd];
                Array.Copy(data, cardEnd, entry.PlayData, 0, entry.PlayData.Length);
                save.Cards.Add(entry);
                pos = playEnd;
            }

            if (save.Cards.Count == 0)
                throw new InvalidDataException("No character cards were found.");

            save.Footer = new byte[data.Length - pos];
            Array.Copy(data, pos, save.Footer, 0, save.Footer.Length);
            return save;
        }

        public SaveCardEntry FindCard(int seat)
        {
            foreach (SaveCardEntry entry in Cards)
            {
                if (entry.Seat == seat) return entry;
            }
            return null;
        }

        public string Save()
        {
            using (MemoryStream stream = new MemoryStream(Raw.Length + 4096))
            {
                stream.Write(Header, 0, Header.Length);
                foreach (SaveCardEntry entry in Cards)
                {
                    if (entry.Dirty)
                    {
                        int gender = entry.Card.Gender;
                        if (gender < 0 || gender > 1) gender = entry.Gender;
                        stream.WriteByte((byte)gender);
                        stream.Write(BitConverter.GetBytes(entry.Seat), 0, 4);
                        byte[] card = entry.Card.BuildCardBytes(entry.PlayData);
                        stream.Write(card, 0, card.Length);
                    }
                    else
                    {
                        int length = entry.PlayEnd - entry.PrefixStart;
                        stream.Write(Raw, entry.PrefixStart, length);
                    }
                }
                stream.Write(Footer, 0, Footer.Length);

                string backup = CreateBackup(Path);
                byte[] output = stream.ToArray();
                string temp = Path + ".tmp";
                File.WriteAllBytes(temp, output);
                try
                {
                    File.Replace(temp, Path, null);
                }
                catch
                {
                    File.Copy(temp, Path, true);
                    File.Delete(temp);
                }
                return backup;
            }
        }

        private static string CreateBackup(string path)
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string backup = path + "." + stamp + ".bak";
            int suffix = 1;
            while (File.Exists(backup))
            {
                backup = path + "." + stamp + "-" + suffix + ".bak";
                suffix++;
            }
            File.Copy(path, backup, false);
            return backup;
        }

        private static int PlayDataEnd(byte[] data, int start)
        {
            int o = start;
            o = SkipIntArray(data, o);
            o = SkipIntArray(data, o);
            o = SkipEvaluationArray(data, o);
            o = SkipIntArray(data, o);
            o = SkipIntArray(data, o);
            o = SkipInteractionArray(data, o);
            o = SkipInteractionArray(data, o);
            o = SkipPromiseArray(data, o);
            o = SkipPromiseArray(data, o);
            o = SkipMeetingArray(data, o);
            o = SkipFixedArray(data, o, 21);
            o = SkipMeetingArray(data, o);
            o = SkipFixedArray(data, o, 21);
            o = SkipPlayerStatus(data, o);
            return o;
        }

        private static int SkipEvaluationArray(byte[] data, int o)
        {
            int count = ReadCount(data, ref o, MaxTargets);
            for (int i = 0; i < count; i++)
            {
                o = Advance(data, o, 5 * 4);
                int history = ReadCount(data, ref o, 256);
                o = Advance(data, o, history * 4);
                o = Advance(data, o, 8 * 4);
                o = SkipIntArray(data, o);
                o = Advance(data, o, 4);
            }
            return o;
        }

        private static int SkipInteractionArray(byte[] data, int o)
        {
            int count = ReadCount(data, ref o, MaxTargets);
            for (int i = 0; i < count; i++)
            {
                o = Advance(data, o, 4);
                int actions = ReadCount(data, ref o, MaxActions);
                o = Advance(data, o, actions * 5 * 4);
            }
            return o;
        }

        private static int SkipPromiseArray(byte[] data, int o)
        {
            int count = ReadCount(data, ref o, MaxTargets);
            return Advance(data, o, count * 3 * 4);
        }

        private static int SkipMeetingArray(byte[] data, int o)
        {
            int count = ReadCount(data, ref o, MaxTargets);
            return Advance(data, o, count * 21);
        }

        private static int SkipPlayerStatus(byte[] data, int o)
        {
            o = Advance(data, o, 8 * 4);
            o = Advance(data, o, 8);
            o = Advance(data, o, 2 * 4);
            return Advance(data, o, 3 * 32);
        }

        private static int SkipIntArray(byte[] data, int o)
        {
            int count = ReadCount(data, ref o, MaxIntArray);
            return Advance(data, o, count * 4);
        }

        private static int SkipFixedArray(byte[] data, int o, int itemSize)
        {
            int count = ReadCount(data, ref o, MaxIntArray);
            return Advance(data, o, count * itemSize);
        }

        private static int ReadCount(byte[] data, ref int o, int max)
        {
            if (o < 0 || o + 4 > data.Length)
                throw new InvalidDataException("Save play data ended unexpectedly.");
            int count = BitConverter.ToInt32(data, o);
            o += 4;
            if (count < 0 || count > max)
                throw new InvalidDataException("Invalid array length in save play data.");
            return count;
        }

        private static int Advance(byte[] data, int o, int bytes)
        {
            if (bytes < 0 || o < 0 || o > data.Length - bytes)
                throw new InvalidDataException("Array runs past the end of the save.");
            return o + bytes;
        }

        private static int IndexOf(byte[] data, byte[] pattern, int start)
        {
            if (data == null || pattern == null || pattern.Length == 0) return -1;
            int last = data.Length - pattern.Length;
            for (int i = Math.Max(0, start); i <= last; i++)
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
