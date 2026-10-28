using System;
using System.Collections.Generic;

namespace AA2Trainer
{
    public enum PromiseDirection
    {
        Incoming,
        Outgoing
    }

    public enum PromiseKind
    {
        Date,
        Meet,
        Lewd
    }

    /// <summary>
    /// One active promise involving the selected character. The seat is the
    /// other participant, so the same row shape works for both directions.
    /// </summary>
    public sealed class PromiseEntry
    {
        public PromiseDirection Direction;
        public PromiseKind Kind;
        public int OtherSeat = -1;
        public int Location = -1;
        public int TestType = -1;
        public bool Flag;
    }

    /// <summary>
    /// Reader for the live promise block. The block stores vectors of promise
    /// entries: dates are int32 seats, meetings are 0x18-byte records, and
    /// lewd/test promises are 0xC-byte records (the lewd stride is still
    /// provisional until a live sample exists). Every vector is bounds-checked
    /// before its entries are read.
    /// </summary>
    public static class PromiseReader
    {
        public const int RootOffset = 0xF6C;

        public const int DatesOutOffset = 0x140;
        public const int DatesInOffset = 0x150;
        public const int MeetOutOffset = 0x1A0;
        public const int MeetInOffset = 0x1C0;
        public const int LewdOutOffset = 0x190;
        public const int LewdInOffset = 0x180;

        public const int DateStride = 4;
        public const int MeetStride = 0x18;
        public const int LewdStride = 0xC;

        public const int MaxSeat = 24;
        public const int MaxLocation = 63;
        public const int MaxTestType = 2;
        public const int MaxEntries = 256;

        public static List<PromiseEntry> Read(GameMemory mem, CharacterInfo target)
        {
            List<PromiseEntry> list = new List<PromiseEntry>();
            if (mem == null || target == null || target.Pointer == 0) return list;

            uint? root = mem.ReadU32(target.Pointer + RootOffset);
            if (!root.HasValue || root.Value == 0) return list;

            ReadDates(mem, root.Value, DatesOutOffset,
                PromiseDirection.Outgoing, list);
            ReadDates(mem, root.Value, DatesInOffset,
                PromiseDirection.Incoming, list);

            ReadMeeting(mem, root.Value, MeetOutOffset,
                PromiseDirection.Outgoing, list);
            ReadMeeting(mem, root.Value, MeetInOffset,
                PromiseDirection.Incoming, list);

            ReadLewd(mem, root.Value, LewdOutOffset,
                PromiseDirection.Outgoing, list);
            ReadLewd(mem, root.Value, LewdInOffset,
                PromiseDirection.Incoming, list);

            list.Sort(delegate(PromiseEntry a, PromiseEntry b)
            {
                int c = a.Kind.CompareTo(b.Kind);
                if (c != 0) return c;
                c = a.Direction.CompareTo(b.Direction);
                if (c != 0) return c;
                return a.OtherSeat.CompareTo(b.OtherSeat);
            });
            return list;
        }

        private static void ReadDates(GameMemory mem, uint root, int offset,
            PromiseDirection direction, List<PromiseEntry> list)
        {
            uint begin;
            int count;
            if (!TryReadVector(mem, root, offset, DateStride,
                out begin, out count)) return;

            byte[] data = mem.ReadBytes(begin, count * DateStride);
            if (data == null || data.Length < count * DateStride) return;

            for (int i = 0; i < count; i++)
            {
                int seat = BitConverter.ToInt32(data, i * DateStride);
                if (seat < 0 || seat > MaxSeat) continue;

                PromiseEntry entry = new PromiseEntry();
                entry.Direction = direction;
                entry.Kind = PromiseKind.Date;
                entry.OtherSeat = seat;
                list.Add(entry);
            }
        }

        private static void ReadMeeting(GameMemory mem, uint root, int offset,
            PromiseDirection direction, List<PromiseEntry> list)
        {
            uint begin;
            int count;
            if (!TryReadVector(mem, root, offset, MeetStride,
                out begin, out count)) return;

            byte[] data = mem.ReadBytes(begin, count * MeetStride);
            if (data == null || data.Length < count * MeetStride) return;

            for (int i = 0; i < count; i++)
            {
                int at = i * MeetStride;
                int seat = BitConverter.ToInt32(data, at + 4);
                int location = BitConverter.ToInt32(data, at + 12);
                if (seat < 0 || seat > MaxSeat) continue;
                if (location < 0 || location > MaxLocation) continue;

                PromiseEntry entry = new PromiseEntry();
                entry.Direction = direction;
                entry.Kind = PromiseKind.Meet;
                entry.OtherSeat = seat;
                entry.Location = location;
                // The flag is one byte; incoming entries use 0xFF padding
                // after it, so reading an int32 would look like -256.
                entry.Flag = data[at] != 0;
                list.Add(entry);
            }
        }

        private static void ReadLewd(GameMemory mem, uint root, int offset,
            PromiseDirection direction, List<PromiseEntry> list)
        {
            uint begin;
            int count;
            if (!TryReadVector(mem, root, offset, LewdStride,
                out begin, out count)) return;

            byte[] data = mem.ReadBytes(begin, count * LewdStride);
            if (data == null || data.Length < count * LewdStride) return;

            for (int i = 0; i < count; i++)
            {
                int at = i * LewdStride;
                if (IsAllZero(data, at, LewdStride)) continue;

                int seat = BitConverter.ToInt32(data, at);
                int testType = BitConverter.ToInt32(data, at + 4);
                if (seat < 0 || seat > MaxSeat) continue;
                if (testType < 0 || testType > MaxTestType) continue;

                PromiseEntry entry = new PromiseEntry();
                entry.Direction = direction;
                entry.Kind = PromiseKind.Lewd;
                entry.OtherSeat = seat;
                entry.TestType = testType;
                list.Add(entry);
            }
        }

        /// <summary>
        /// Reads a begin/end/capacity vector header. Empty vectors have a null
        /// begin pointer; the count comes from the byte distance between begin
        /// and end, never from the capacity.
        /// </summary>
        private static bool TryReadVector(GameMemory mem, uint root, int offset,
            int stride, out uint begin, out int count)
        {
            begin = 0;
            count = 0;
            if (mem == null || stride <= 0) return false;

            uint? start = mem.ReadU32(root + (uint)offset);
            uint? end = mem.ReadU32(root + (uint)offset + 4);
            uint? capacity = mem.ReadU32(root + (uint)offset + 8);
            if (!start.HasValue || !end.HasValue || !capacity.HasValue)
                return false;
            if (start.Value == 0 || end.Value <= start.Value) return false;
            if (!mem.IsPlausiblePointer(start.Value)) return false;

            long bytes = (long)end.Value - (long)start.Value;
            if (bytes % stride != 0) return false;
            long entries = bytes / stride;
            if (entries <= 0 || entries > MaxEntries) return false;

            begin = start.Value;
            count = (int)entries;
            return true;
        }

        private static bool IsAllZero(byte[] data, int offset, int length)
        {
            if (data == null || offset < 0 || offset + length > data.Length)
                return true;
            for (int i = 0; i < length; i++)
                if (data[offset + i] != 0) return false;
            return true;
        }
    }
}
