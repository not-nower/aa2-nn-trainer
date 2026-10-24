using System;

namespace AA2Trainer
{
    public sealed class RelationshipSnapshot
    {
        public int SourceSeat;
        public int TargetSeat;

        public int LovePoints;
        public int LikePoints;
        public int DislikePoints;
        public int HatePoints;

        public int LoveCount;
        public int LikeCount;
        public int DislikeCount;
        public int HateCount;

        public int LoveStatus;
        public int LikeStatus;
        public int DislikeStatus;
        public int HateStatus;

        public int HistoryCount;
        public bool IsLover;
        public int LoverDays;
        public bool HasHuh;

        public int LoveTotal { get { return LovePoints + LoveCount * 30; } }
        public int LikeTotal { get { return LikePoints + LikeCount * 30; } }
        public int DislikeTotal { get { return DislikePoints + DislikeCount * 30; } }
        public int HateTotal { get { return HatePoints + HateCount * 30; } }

        public int Total { get { return LoveTotal + LikeTotal + DislikeTotal + HateTotal; } }
    }

    public static class RelationshipReader
    {
        private const int RecordSize = 0x58;

        public static RelationshipSnapshot Read(GameMemory mem, CharacterInfo source, int targetSeat)
        {
            if (mem == null || source == null) return null;
            if (targetSeat < 0 || targetSeat > 24) return null;
            if (source.Pointer == 0) return null;

            uint? more = mem.ReadU32(source.Pointer + 0xF64);
            if (!more.HasValue || more.Value == 0) return null;

            uint? start = mem.ReadU32(more.Value + 0x16A18);
            uint? end = mem.ReadU32(more.Value + 0x16A1C);
            if (!start.HasValue || !end.HasValue) return null;
            if (start.Value == 0 || end.Value <= start.Value) return null;

            long bytes = (long)end.Value - (long)start.Value;
            long count = bytes / RecordSize;
            if (count <= 0 || count > 25) return null;

            for (int i = 0; i < count; i++)
            {
                uint address = start.Value + (uint)(i * RecordSize);
                byte[] rec;
                if (!mem.TryReadBytes(address, RecordSize, out rec) || rec == null || rec.Length < RecordSize)
                    continue;

                int seat = BitConverter.ToInt32(rec, 0x00);
                if (seat != targetSeat) continue;

                RelationshipSnapshot s = new RelationshipSnapshot();
                s.SourceSeat = source.Seat;
                s.TargetSeat = targetSeat;
                s.LovePoints = BitConverter.ToInt32(rec, 0x04);
                s.LikePoints = BitConverter.ToInt32(rec, 0x08);
                s.DislikePoints = BitConverter.ToInt32(rec, 0x0C);
                s.HatePoints = BitConverter.ToInt32(rec, 0x10);

                int backlogStart = BitConverter.ToInt32(rec, 0x18);
                int backlogEnd = BitConverter.ToInt32(rec, 0x1C);
                if (backlogStart > 0 && backlogEnd >= backlogStart)
                {
                    long backlogBytes = (long)backlogEnd - (long)backlogStart;
                    s.HistoryCount = (int)(backlogBytes / 4L);
                    if (s.HistoryCount < 0 || s.HistoryCount > 256) s.HistoryCount = 0;
                }

                s.LoveCount = BitConverter.ToInt32(rec, 0x24);
                s.LikeCount = BitConverter.ToInt32(rec, 0x28);
                s.DislikeCount = BitConverter.ToInt32(rec, 0x2C);
                s.HateCount = BitConverter.ToInt32(rec, 0x30);
                s.LoveStatus = BitConverter.ToInt32(rec, 0x34);
                s.LikeStatus = BitConverter.ToInt32(rec, 0x38);
                s.DislikeStatus = BitConverter.ToInt32(rec, 0x3C);
                s.HateStatus = BitConverter.ToInt32(rec, 0x40);

                byte? lover = mem.ReadU8(source.Pointer + 0xF20 + (uint)targetSeat);
                s.IsLover = lover.HasValue && lover.Value != 0;
                byte? days = mem.ReadU8(source.Pointer + 0xF39 + (uint)targetSeat);
                s.LoverDays = days.HasValue ? days.Value : 0;

                return s;
            }
            return null;
        }
    }
}
