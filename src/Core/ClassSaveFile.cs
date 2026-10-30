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
    /// class footer are preserved; play data is copied byte-for-byte so
    /// replacing a card does not touch relationships, history, promises, or
    /// status.
    ///
    /// Play data lengths are not parsed. Each card ends where the next card's
    /// five-byte prefix begins, and the last card runs to the end of the file
    /// (its play data plus the class footer). The AA2 play-data layout has
    /// version-specific/unknown blocks after PLAYER_STATUS; walking it by hand
    /// silently truncated every card and corrupted the save.
    /// </summary>
    public sealed class ClassSaveFile
    {
        private const int HeaderSize = 0x13D;
        private const int MaxCards = 100;

        private static readonly byte[] PngHeader =
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A
        };

        public string Path;
        public byte[] Raw;
        public byte[] Header;
        public byte[] Footer;
        public int PcSeat = -1;
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

                int playEnd;
                if (i == wanted - 1)
                {
                    // The last card owns everything up to EOF: its play data
                    // plus the class footer. Keeping them together means the
                    // footer is written back untouched.
                    playEnd = data.Length;
                }
                else
                {
                    int nextCard = FindNextCard(data, cardEnd);
                    if (nextCard < 0)
                        throw new InvalidDataException(
                            "Could not find the next card in the save.");
                    playEnd = nextCard - 5;
                }
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

            // The footer is part of the last card's slice now. Keep the field
            // empty so Save() does not append it twice.
            save.Footer = new byte[0];
            if (data.Length >= 4)
            {
                int pcSeat = BitConverter.ToInt32(data, data.Length - 4);
                if (pcSeat >= 0 && pcSeat <= 24) save.PcSeat = pcSeat;
            }
            return save;
        }

        /// <summary>
        /// Finds the next character card: a PNG signature preceded by a
        /// plausible one-byte gender and int32 seat. This avoids parsing the
        /// play-data layout to find card boundaries.
        /// </summary>
        private static int FindNextCard(byte[] data, int from)
        {
            int pos = from;
            while (true)
            {
                int png = IndexOf(data, PngHeader, pos);
                if (png < 5) return -1;
                int prefix = png - 5;
                if (prefix >= from && IsCardPrefix(data, prefix)) return png;
                pos = png + 1;
            }
        }

        private static bool IsCardPrefix(byte[] data, int offset)
        {
            if (data == null || offset < 0 || offset + 5 > data.Length)
                return false;
            byte gender = data[offset];
            if (gender > 1) return false;
            int seat = BitConverter.ToInt32(data, offset + 1);
            return seat >= 0 && seat <= 24;
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
