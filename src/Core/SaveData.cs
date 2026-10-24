using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AA2Trainer
{
    public sealed class SaveActionData
    {
        public int Key;
        public int Counter;
        public int Success;
        public int Failure;
        public int Huh;
    }

    public sealed class SaveInteractionData
    {
        public int Seat;
        public List<SaveActionData> Actions = new List<SaveActionData>();
    }

    public sealed class SaveEvaluationData
    {
        public int Seat;
        public int LovePoints;
        public int LikePoints;
        public int DislikePoints;
        public int HatePoints;
        public int[] History = new int[0];
        public int LoveCount;
        public int LikeCount;
        public int DislikeCount;
        public int HateCount;
        public int LoveStatus;
        public int LikeStatus;
        public int DislikeStatus;
        public int HateStatus;

        public int LoveTotal { get { return LovePoints + LoveCount * 30; } }
        public int LikeTotal { get { return LikePoints + LikeCount * 30; } }
        public int DislikeTotal { get { return DislikePoints + DislikeCount * 30; } }
        public int HateTotal { get { return HatePoints + HateCount * 30; } }
    }

    public sealed class SavePromiseData
    {
        public int Seat;
        public int TestType;
        public int Unknown;
    }

    public sealed class SaveMeetingData
    {
        public bool Flag;
        public int Seat;
        public int Location;
    }

    public sealed class SavePlayerStatus
    {
        public int Quarrels;
        public int Skipped;
        public int Competitions;
        public int Relationships;
        public int Rejected;
        public int ScoreTest;
        public int ScorePhys;
        public int ScoreClub;
        public int TotalSex;
        public int SexPartners;
        public string FirstPartner = "";
        public string FirstPartnerAnal = "";
        public string LastPartner = "";
    }

    public sealed class SavePlayData
    {
        public List<SaveEvaluationData> Relationships = new List<SaveEvaluationData>();
        public List<int> DatesOut = new List<int>();
        public List<int> DatesIn = new List<int>();
        public List<SaveInteractionData> InteractionsIn = new List<SaveInteractionData>();
        public List<SaveInteractionData> InteractionsOut = new List<SaveInteractionData>();
        public List<SavePromiseData> PromisesIn = new List<SavePromiseData>();
        public List<SavePromiseData> PromisesOut = new List<SavePromiseData>();
        public List<SaveMeetingData> MeetingsIn = new List<SaveMeetingData>();
        public List<SaveMeetingData> MeetingsOut = new List<SaveMeetingData>();
        public SavePlayerStatus Status = new SavePlayerStatus();

        public SaveEvaluationData FindRelationship(int seat)
        {
            foreach (SaveEvaluationData e in Relationships)
                if (e.Seat == seat) return e;
            return null;
        }

        public SaveInteractionData FindInteraction(List<SaveInteractionData> list, int seat)
        {
            foreach (SaveInteractionData i in list)
                if (i.Seat == seat) return i;
            return null;
        }

        public int CountHuh(List<SaveInteractionData> list)
        {
            int count = 0;
            foreach (SaveInteractionData i in list)
                foreach (SaveActionData a in i.Actions)
                    count += a.Huh;
            return count;
        }

        public bool HasHuhOut(int targetSeat)
        {
            SaveInteractionData i = FindInteraction(InteractionsOut, targetSeat);
            if (i == null) return false;
            foreach (SaveActionData a in i.Actions)
                if (a.Huh != 0) return true;
            return false;
        }

        public bool HasHuhIn(int sourceSeat)
        {
            SaveInteractionData i = FindInteraction(InteractionsIn, sourceSeat);
            if (i == null) return false;
            foreach (SaveActionData a in i.Actions)
                if (a.Huh != 0) return true;
            return false;
        }
    }

    public sealed class SaveCardData
    {
        public int Seat;
        public SavePlayData PlayData = new SavePlayData();
        public string Error = "";
    }

    public sealed class SaveClassData
    {
        public string Path = "";
        public string Academy = "";
        public string Year = "";
        public string ClassName = "";
        public int Boys;
        public int Girls;
        public int Hours;
        public int Minutes;
        public int Seconds;
        public string[] ClubNames = new string[8];
        public int[] ClubTypes = new int[8];
        public int GameTime;
        public int GameDays;
        public int GameWeekday;
        public List<SaveCardData> Cards = new List<SaveCardData>();

        public int StudentCount { get { return Boys + Girls; } }

        public SaveCardData FindCard(int seat)
        {
            foreach (SaveCardData c in Cards)
                if (c.Seat == seat) return c;
            return null;
        }
    }

    public static class SaveDataReader
    {
        private static readonly byte[] PngHeader = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A
        };

        private static readonly byte[] Iend = new byte[] { 0x49, 0x45, 0x4E, 0x44 };
        private const int HeaderSize = 0x13D;
        private const int MaxCards = 100;
        private const int MaxTargets = 64;
        private const int MaxActions = 256;

        private static readonly Encoding Japanese = GetJapaneseEncoding();

        public static string FindSaveDirectory(string gameRoot)
        {
            if (string.IsNullOrEmpty(gameRoot)) return null;
            return Path.Combine(Path.Combine(Path.Combine(gameRoot, "data"), "save"), "class");
        }

        public static string[] FindSaveFiles(string gameRoot)
        {
            string dir = FindSaveDirectory(gameRoot);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return new string[0];
            string[] files = Directory.GetFiles(dir, "*.sav");
            Array.Sort(files, delegate(string a, string b)
            {
                int cmp = File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a));
                if (cmp != 0) return cmp;
                return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
            });
            return files;
        }

        public static SaveClassData Load(string path)
        {
            byte[] data = File.ReadAllBytes(path);
            if (data.Length < HeaderSize) throw new InvalidDataException("Save file is too small.");
            if (ReadI32(data, 0) != 0x65) throw new InvalidDataException("Unsupported save format (expected AA2 class save version 0x65).");

            SaveClassData save = new SaveClassData();
            save.Path = path;
            save.Academy = ReadCString(data, 0x04, 64);
            save.Year = ReadCString(data, 0x44, 16);
            save.ClassName = ReadCString(data, 0x54, 16);
            save.Boys = ReadI32(data, 0x64);
            save.Girls = ReadI32(data, 0x68);
            save.Hours = ReadI16(data, 0x6C);
            save.Minutes = ReadI16(data, 0x6E);
            save.Seconds = ReadI16(data, 0x70);
            for (int i = 0; i < 8; i++)
            {
                int offset = 0x72 + i * 25;
                save.ClubNames[i] = ReadCString(data, offset, 24);
                save.ClubTypes[i] = ReadByte(data, offset + 24);
            }
            save.GameTime = ReadByte(data, 0x13A);
            save.GameDays = ReadByte(data, 0x13B);
            save.GameWeekday = ReadByte(data, 0x13C);

            int wanted = save.Boys + save.Girls;
            if (wanted < 0 || wanted > MaxCards) wanted = MaxCards;

            int pos = HeaderSize;
            while (save.Cards.Count < wanted)
            {
                int cardOffset = IndexOf(data, PngHeader, pos);
                if (cardOffset < 4) break;
                int firstEnd = IndexOf(data, Iend, cardOffset + 1);
                if (firstEnd < 0) break;
                int secondEnd = IndexOf(data, Iend, firstEnd + 1);
                if (secondEnd < 0) break;
                int cardEnd = secondEnd + 12;
                if (cardEnd <= cardOffset || cardEnd > data.Length) break;

                int seat = ReadI32(data, cardOffset - 4);
                if (seat < 0 || seat > 24)
                {
                    pos = cardEnd;
                    continue;
                }

                SaveCardData card = new SaveCardData();
                card.Seat = seat;
                try
                {
                    card.PlayData = ParsePlayData(data, cardEnd);
                }
                catch (Exception ex)
                {
                    card.Error = ex.Message;
                }
                save.Cards.Add(card);
                pos = cardEnd;
            }

            save.Cards.Sort(delegate(SaveCardData a, SaveCardData b)
            {
                return a.Seat.CompareTo(b.Seat);
            });
            return save;
        }

        private static SavePlayData ParsePlayData(byte[] data, int start)
        {
            SavePlayData p = new SavePlayData();
            int o = start;

            o = SkipIntArray(data, o); // unknown array 1
            o = SkipIntArray(data, o); // unknown array 2
            p.Relationships = ReadEvaluationArray(data, ref o);
            p.DatesOut = ReadIntArray(data, ref o);
            p.DatesIn = ReadIntArray(data, ref o);
            p.InteractionsIn = ReadInteractionArray(data, ref o);
            p.InteractionsOut = ReadInteractionArray(data, ref o);
            p.PromisesIn = ReadPromiseArray(data, ref o);
            p.PromisesOut = ReadPromiseArray(data, ref o);
            p.MeetingsIn = ReadMeetingArray(data, ref o);
            o = SkipFixedArray(data, o, 21); // unknown meeting array
            p.MeetingsOut = ReadMeetingArray(data, ref o);
            o = SkipFixedArray(data, o, 21); // unknown meeting array
            p.Status = ReadPlayerStatus(data, ref o);
            return p;
        }

        private static List<SaveEvaluationData> ReadEvaluationArray(byte[] data, ref int o)
        {
            int count = ReadCount(data, ref o, MaxTargets);
            List<SaveEvaluationData> list = new List<SaveEvaluationData>(count);
            for (int i = 0; i < count; i++)
            {
                SaveEvaluationData e = new SaveEvaluationData();
                e.Seat = ReadI32(data, o); o += 4;
                e.LovePoints = ReadI32(data, o); o += 4;
                e.LikePoints = ReadI32(data, o); o += 4;
                e.DislikePoints = ReadI32(data, o); o += 4;
                e.HatePoints = ReadI32(data, o); o += 4;

                int historyCount = ReadCount(data, ref o, 256);
                e.History = new int[historyCount];
                for (int j = 0; j < historyCount; j++)
                {
                    e.History[j] = ReadI32(data, o);
                    o += 4;
                }

                e.LoveCount = ReadI32(data, o); o += 4;
                e.LikeCount = ReadI32(data, o); o += 4;
                e.DislikeCount = ReadI32(data, o); o += 4;
                e.HateCount = ReadI32(data, o); o += 4;
                e.LoveStatus = ReadI32(data, o); o += 4;
                e.LikeStatus = ReadI32(data, o); o += 4;
                e.DislikeStatus = ReadI32(data, o); o += 4;
                e.HateStatus = ReadI32(data, o); o += 4;

                o = SkipIntArray(data, o); // unknown int array
                o += 4; // trailing dummy int
                list.Add(e);
            }
            return list;
        }

        private static List<SaveInteractionData> ReadInteractionArray(byte[] data, ref int o)
        {
            int count = ReadCount(data, ref o, MaxTargets);
            List<SaveInteractionData> list = new List<SaveInteractionData>(count);
            for (int i = 0; i < count; i++)
            {
                SaveInteractionData target = new SaveInteractionData();
                target.Seat = ReadI32(data, o); o += 4;
                int actionCount = ReadCount(data, ref o, MaxActions);
                for (int j = 0; j < actionCount; j++)
                {
                    SaveActionData a = new SaveActionData();
                    a.Key = ReadI32(data, o); o += 4;
                    a.Counter = ReadI32(data, o); o += 4;
                    a.Success = ReadI32(data, o); o += 4;
                    a.Failure = ReadI32(data, o); o += 4;
                    a.Huh = ReadI32(data, o); o += 4;
                    target.Actions.Add(a);
                }
                list.Add(target);
            }
            return list;
        }

        private static List<SavePromiseData> ReadPromiseArray(byte[] data, ref int o)
        {
            int count = ReadCount(data, ref o, MaxTargets);
            List<SavePromiseData> list = new List<SavePromiseData>(count);
            for (int i = 0; i < count; i++)
            {
                SavePromiseData p = new SavePromiseData();
                p.Seat = ReadI32(data, o); o += 4;
                p.TestType = ReadI32(data, o); o += 4;
                p.Unknown = ReadI32(data, o); o += 4;
                list.Add(p);
            }
            return list;
        }

        private static List<SaveMeetingData> ReadMeetingArray(byte[] data, ref int o)
        {
            int count = ReadCount(data, ref o, MaxTargets);
            List<SaveMeetingData> list = new List<SaveMeetingData>(count);
            for (int i = 0; i < count; i++)
            {
                SaveMeetingData m = new SaveMeetingData();
                m.Flag = ReadByte(data, o) != 0; o += 1;
                m.Seat = ReadI32(data, o); o += 4;
                o += 8; // unknown int,int
                m.Location = ReadI32(data, o); o += 4;
                o += 4; // unknown int
                list.Add(m);
            }
            return list;
        }

        private static SavePlayerStatus ReadPlayerStatus(byte[] data, ref int o)
        {
            SavePlayerStatus s = new SavePlayerStatus();
            s.Quarrels = ReadI32(data, o); o += 4;
            s.Skipped = ReadI32(data, o); o += 4;
            s.Competitions = ReadI32(data, o); o += 4;
            s.Relationships = ReadI32(data, o); o += 4;
            s.Rejected = ReadI32(data, o); o += 4;
            s.ScoreTest = ReadI32(data, o); o += 4;
            s.ScorePhys = ReadI32(data, o); o += 4;
            s.ScoreClub = ReadI32(data, o); o += 4;
            o += 8; // unknown int,int
            s.TotalSex = ReadI32(data, o); o += 4;
            s.SexPartners = ReadI32(data, o); o += 4;
            s.FirstPartner = ReadCString(data, o, 32); o += 32;
            s.FirstPartnerAnal = ReadCString(data, o, 32); o += 32;
            s.LastPartner = ReadCString(data, o, 32); o += 32;
            return s;
        }

        private static int SkipIntArray(byte[] data, int o)
        {
            return SkipFixedArray(data, o, 4);
        }

        private static int SkipFixedArray(byte[] data, int o, int itemSize)
        {
            int count = ReadI32(data, o); o += 4;
            if (count < 0 || count > 100000) throw new InvalidDataException("Invalid array length in save data.");
            long bytes = (long)count * itemSize;
            if (bytes < 0 || o + bytes > data.Length) throw new InvalidDataException("Array runs past end of save data.");
            return o + (int)bytes;
        }

        private static List<int> ReadIntArray(byte[] data, ref int o)
        {
            int count = ReadCount(data, ref o, 100000);
            List<int> list = new List<int>(count);
            for (int i = 0; i < count; i++)
            {
                list.Add(ReadI32(data, o));
                o += 4;
            }
            return list;
        }

        private static int ReadCount(byte[] data, ref int o, int max)
        {
            int count = ReadI32(data, o); o += 4;
            if (count < 0 || count > max) throw new InvalidDataException("Invalid array length in save data.");
            return count;
        }

        private static int ReadI32(byte[] data, int offset)
        {
            if (offset < 0 || offset + 4 > data.Length) throw new EndOfStreamException("Save data ended unexpectedly.");
            return BitConverter.ToInt32(data, offset);
        }

        private static int ReadI16(byte[] data, int offset)
        {
            if (offset < 0 || offset + 2 > data.Length) throw new EndOfStreamException("Save data ended unexpectedly.");
            return BitConverter.ToInt16(data, offset);
        }

        private static int ReadByte(byte[] data, int offset)
        {
            if (offset < 0 || offset >= data.Length) throw new EndOfStreamException("Save data ended unexpectedly.");
            return data[offset];
        }

        private static string ReadCString(byte[] data, int offset, int maxLength)
        {
            if (offset < 0 || offset >= data.Length) return "";
            int end = offset;
            int limit = Math.Min(data.Length, offset + maxLength);
            while (end < limit && data[end] != 0) end++;
            if (end <= offset) return "";
            return Japanese.GetString(data, offset, end - offset).Trim();
        }

        private static int IndexOf(byte[] data, byte[] pattern, int start)
        {
            if (data == null || pattern == null || pattern.Length == 0) return -1;
            if (start < 0) start = 0;
            int last = data.Length - pattern.Length;
            for (int i = start; i <= last; i++)
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

        private static Encoding GetJapaneseEncoding()
        {
            try { return Encoding.GetEncoding(932); }
            catch { return Encoding.Default; }
        }
    }
}
