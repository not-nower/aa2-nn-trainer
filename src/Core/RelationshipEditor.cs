using System;
using System.Collections.Generic;

namespace AA2Trainer
{
    /// <summary>
    /// One incoming relationship record: the source character's view of the
    /// selected target. Points are partial progress toward the next count;
    /// counts are the latest-30 window; levels are the game's cached Lots/Some/Low.
    /// </summary>
    public sealed class RelationshipRecord
    {
        public CharacterInfo Source;
        public int TargetSeat = -1;
        public uint Address;
        public bool HasRecord;

        public readonly int[] Points = new int[4];
        public readonly int[] Counts = new int[4];
        public readonly int[] Levels = new int[4];

        public int Latest = -1;
        public int HistoryCount;
        public uint HistoryStart;
        public uint HistoryEnd;

        public int SumCounts
        {
            get { return Counts[0] + Counts[1] + Counts[2] + Counts[3]; }
        }

        public int LatestLevel
        {
            get { return Latest >= 0 && Latest < 4 ? Levels[Latest] : -1; }
        }

        public RelationshipRecord Clone()
        {
            RelationshipRecord copy = new RelationshipRecord();
            copy.Source = Source;
            copy.TargetSeat = TargetSeat;
            copy.Address = Address;
            copy.HasRecord = HasRecord;
            Array.Copy(Points, copy.Points, 4);
            Array.Copy(Counts, copy.Counts, 4);
            Array.Copy(Levels, copy.Levels, 4);
            copy.Latest = Latest;
            copy.HistoryCount = HistoryCount;
            copy.HistoryStart = HistoryStart;
            copy.HistoryEnd = HistoryEnd;
            return copy;
        }
    }

    /// <summary>
    /// One classmate's ordered feeling history toward the selected character.
    /// History is oldest first, newest last, and contains only feeling codes
    /// (0 Love, 1 Like, 2 Dislike, 3 Hate). No timestamps or action names are
    /// stored by the game.
    /// </summary>
    public sealed class RelationshipHistoryRow
    {
        public CharacterInfo Source;
        public bool HasRecord;
        public int[] History = new int[0];
        public int Latest = -1;
        public int LatestLevel = -1;

        public int Count
        {
            get { return History == null ? 0 : History.Length; }
        }
    }

    /// <summary>
    /// Reads and writes the compact 0x58-byte relationship records. Incoming
    /// means: for every classmate, find that classmate's record whose target is
    /// the selected character's seat.
    /// </summary>
    public static class RelationshipEditor
    {
        public const int RecordSize = 0x58;

        /// <summary>
        /// Returns one row per classmate, excluding the selected character.
        /// Pairs without a record are returned with HasRecord=false so the UI
        /// can show "No record yet" instead of hiding them.
        /// </summary>
        public static List<RelationshipRecord> ReadIncoming(GameMemory mem,
            GameContext ctx, CharacterInfo target)
        {
            List<RelationshipRecord> list = new List<RelationshipRecord>();
            if (mem == null || ctx == null || target == null) return list;
            if (target.Seat < 0 || target.Seat > 24) return list;

            foreach (CharacterInfo source in ctx.Characters)
            {
                if (source == null || source.Pointer == target.Pointer) continue;

                RelationshipRecord record = FindRecord(mem, source.Pointer, target.Seat);
                if (record == null)
                {
                    record = new RelationshipRecord();
                    record.TargetSeat = target.Seat;
                    record.HasRecord = false;
                }
                record.Source = source;
                list.Add(record);
            }

            list.Sort(delegate (RelationshipRecord a, RelationshipRecord b)
            {
                int sa = a.Source == null ? 99 : a.Source.Seat;
                int sb = b.Source == null ? 99 : b.Source.Seat;
                int c = sa.CompareTo(sb);
                if (c != 0) return c;
                string na = a.Source == null ? "" : a.Source.Name;
                string nb = b.Source == null ? "" : b.Source.Name;
                return string.Compare(na, nb, StringComparison.OrdinalIgnoreCase);
            });
            return list;
        }

        /// <summary>
        /// Returns one row per classmate with that classmate's ordered feeling
        /// history toward the target. Pairs without a record are included with
        /// HasRecord=false so the UI can show empty history slots.
        /// </summary>
        public static List<RelationshipHistoryRow> ReadIncomingHistory(GameMemory mem,
            GameContext ctx, CharacterInfo target)
        {
            List<RelationshipHistoryRow> list = new List<RelationshipHistoryRow>();
            if (mem == null || ctx == null || target == null) return list;
            if (target.Seat < 0 || target.Seat > 24) return list;

            foreach (CharacterInfo source in ctx.Characters)
            {
                if (source == null || source.Pointer == target.Pointer) continue;

                RelationshipHistoryRow row = new RelationshipHistoryRow();
                row.Source = source;

                RelationshipRecord record = FindRecord(mem, source.Pointer, target.Seat);
                if (record != null)
                {
                    row.HasRecord = true;
                    ReadHistory(mem, record, row);
                }
                list.Add(row);
            }

            list.Sort(delegate (RelationshipHistoryRow a, RelationshipHistoryRow b)
            {
                int sa = a.Source == null ? 99 : a.Source.Seat;
                int sb = b.Source == null ? 99 : b.Source.Seat;
                int c = sa.CompareTo(sb);
                if (c != 0) return c;
                string na = a.Source == null ? "" : a.Source.Name;
                string nb = b.Source == null ? "" : b.Source.Name;
                return string.Compare(na, nb, StringComparison.OrdinalIgnoreCase);
            });
            return list;
        }

        private static void ReadHistory(GameMemory mem, RelationshipRecord record,
            RelationshipHistoryRow row)
        {
            if (record.HistoryCount <= 0 || record.HistoryStart == 0) return;

            int take = Math.Min(record.HistoryCount, 30);
            uint start = record.HistoryStart +
                (uint)((record.HistoryCount - take) * 4);
            byte[] data = mem.ReadBytes(start, take * 4);
            if (data == null) return;

            int[] history = new int[take];
            for (int i = 0; i < take; i++)
                history[i] = BitConverter.ToInt32(data, i * 4);

            row.History = history;
            int last = history[take - 1];
            if (last >= 0 && last <= 3)
            {
                row.Latest = last;
                row.LatestLevel = record.Levels[last];
            }
        }

        public static RelationshipRecord FindRecord(GameMemory mem, uint sourcePointer,
            int targetSeat)
        {
            if (mem == null || !mem.IsAttached) return null;
            if (sourcePointer == 0 || targetSeat < 0 || targetSeat > 24) return null;

            uint? more = mem.ReadU32(sourcePointer + 0xF64);
            if (!more.HasValue || more.Value == 0) return null;

            uint? start = mem.ReadU32(more.Value + 0x16A18);
            uint? end = mem.ReadU32(more.Value + 0x16A1C);
            if (!start.HasValue || !end.HasValue) return null;
            if (start.Value == 0 || end.Value <= start.Value) return null;

            long bytes = (long)end.Value - (long)start.Value;
            long count = bytes / RecordSize;
            if (count <= 0 || count > 64) return null;

            byte[] block = mem.ReadBytes(start.Value, (int)(count * RecordSize));
            if (block == null) return null;

            for (int i = 0; i < count; i++)
            {
                int offset = i * RecordSize;
                if (BitConverter.ToInt32(block, offset) != targetSeat) continue;

                RelationshipRecord record = new RelationshipRecord();
                record.TargetSeat = targetSeat;
                record.Address = start.Value + (uint)offset;
                record.HasRecord = true;

                for (int f = 0; f < 4; f++)
                {
                    record.Points[f] = BitConverter.ToInt32(block, offset + 0x04 + f * 4);
                    record.Counts[f] = BitConverter.ToInt32(block, offset + 0x24 + f * 4);
                    record.Levels[f] = BitConverter.ToInt32(block, offset + 0x34 + f * 4);
                }

                uint historyStart = BitConverter.ToUInt32(block, offset + 0x18);
                uint historyEnd = BitConverter.ToUInt32(block, offset + 0x1C);
                record.HistoryStart = historyStart;
                record.HistoryEnd = historyEnd;

                long historyBytes = (long)historyEnd - (long)historyStart;
                long historyCount = historyBytes > 0 ? historyBytes / 4 : 0;
                if (historyCount < 0 || historyCount > 256) historyCount = 0;
                record.HistoryCount = (int)historyCount;

                if (record.HistoryCount > 0)
                {
                    int? latest = mem.ReadI32(historyEnd - 4);
                    if (latest.HasValue && latest.Value >= 0 && latest.Value <= 3)
                        record.Latest = latest.Value;
                }

                return record;
            }
            return null;
        }

        public static bool TryWrite(GameMemory mem, uint sourcePointer, int targetSeat,
            int[] counts, int[] points, int[] levels, int latest, bool writeLatest,
            out string error)
        {
            RelationshipRecord current = FindRecord(mem, sourcePointer, targetSeat);
            if (current == null)
            {
                error = "The relationship record is no longer available.";
                return false;
            }
            return TryWrite(mem, current, counts, points, levels, latest, writeLatest,
                out error);
        }

        /// <summary>
        /// Writes only values that differ from the freshly read record, and
        /// reads each one back. The newest history entry is written only when
        /// requested; its address is re-read so a reallocated history array is
        /// handled safely.
        /// </summary>
        /// <summary>
        /// Changes one existing feeling event. Counts are adjusted by −1/+1 so
        /// the LLDH tally keeps matching the edited history. Empty slots are
        /// not created; the history length is unchanged.
        /// </summary>
        public static bool TryWriteHistory(GameMemory mem, uint sourcePointer,
            int targetSeat, int displayIndex, int feeling, out string error)
        {
            error = null;
            if (feeling < 0 || feeling > 3)
            {
                error = "Unknown feeling value.";
                return false;
            }

            RelationshipRecord record = FindRecord(mem, sourcePointer, targetSeat);
            if (record == null)
            {
                error = "The relationship record is no longer available.";
                return false;
            }

            int take = Math.Min(record.HistoryCount, 30);
            if (displayIndex < 0 || displayIndex >= take)
            {
                error = "There is no history event at this position.";
                return false;
            }

            int absoluteIndex = record.HistoryCount - take + displayIndex;
            uint address = record.HistoryStart + (uint)(absoluteIndex * 4);
            int? oldFeeling = mem.ReadI32(address);
            if (!oldFeeling.HasValue)
            {
                error = "The history event is unreadable.";
                return false;
            }
            if (oldFeeling.Value == feeling) return true;

            if (!MemoryWriter.TryWriteInt32(mem, address, feeling,
                "Feeling event", out error))
                return false;

            int[] counts = new int[4];
            int[] points = new int[4];
            int[] levels = new int[4];
            Array.Copy(record.Counts, counts, 4);
            Array.Copy(record.Points, points, 4);
            Array.Copy(record.Levels, levels, 4);

            if (oldFeeling.Value >= 0 && oldFeeling.Value <= 3 &&
                counts[oldFeeling.Value] > 0)
                counts[oldFeeling.Value]--;
            counts[feeling]++;

            if (!TryWrite(mem, record, counts, points, levels, -1, false, out error))
            {
                error = "History event changed, but the counts update failed: " + error;
                return false;
            }
            return true;
        }

        /// <summary>
        /// Sets every existing history event to one feeling. Counts are
        /// rewritten from the history length; points and levels are unchanged.
        /// </summary>
        public static bool TryWriteHistoryAll(GameMemory mem, uint sourcePointer,
            int targetSeat, int feeling, out string error)
        {
            error = null;
            if (feeling < 0 || feeling > 3)
            {
                error = "Unknown feeling value.";
                return false;
            }

            RelationshipRecord record = FindRecord(mem, sourcePointer, targetSeat);
            if (record == null)
            {
                error = "The relationship record is no longer available.";
                return false;
            }
            if (record.HistoryCount <= 0 || record.HistoryStart == 0)
            {
                error = "There is no history to update.";
                return false;
            }

            byte[] buffer = new byte[record.HistoryCount * 4];
            byte[] one = BitConverter.GetBytes(feeling);
            for (int i = 0; i < record.HistoryCount; i++)
                Buffer.BlockCopy(one, 0, buffer, i * 4, 4);

            if (!MemoryWriter.TryWriteBlock(mem, record.HistoryStart, buffer,
                "Feeling history", out error))
                return false;

            int[] counts = new int[4];
            int[] points = new int[4];
            int[] levels = new int[4];
            counts[feeling] = record.HistoryCount;
            Array.Copy(record.Points, points, 4);
            Array.Copy(record.Levels, levels, 4);

            if (!TryWrite(mem, record, counts, points, levels, -1, false, out error))
            {
                error = "History was changed, but the counts update failed: " + error;
                return false;
            }
            return true;
        }

        public static bool TryWrite(GameMemory mem, RelationshipRecord current,
            int[] counts, int[] points, int[] levels, int latest, bool writeLatest,
            out string error)
        {
            error = null;
            if (current == null || !current.HasRecord)
            {
                error = "No relationship record is loaded.";
                return false;
            }
            if (counts == null || points == null || levels == null ||
                counts.Length < 4 || points.Length < 4 || levels.Length < 4)
            {
                error = "The relationship values are incomplete.";
                return false;
            }

            bool pointsDiffer = false;
            bool statsDiffer = false;
            for (int f = 0; f < 4; f++)
            {
                if (current.Points[f] != points[f]) pointsDiffer = true;
                if (current.Counts[f] != counts[f] ||
                    current.Levels[f] != levels[f]) statsDiffer = true;
            }

            // Counts (0x24) and levels (0x34) are contiguous, and points
            // occupy a separate 16-byte window at 0x04. Writing each window as
            // one verified block keeps the freeze loop from doing up to twelve
            // write/readback pairs per tick.
            if (pointsDiffer)
            {
                byte[] block = new byte[16];
                for (int f = 0; f < 4; f++)
                    Buffer.BlockCopy(BitConverter.GetBytes(points[f]),
                        0, block, f * 4, 4);
                if (!MemoryWriter.TryWriteBlock(mem, current.Address + 0x04,
                    block, "Points", out error))
                    return false;
            }

            if (statsDiffer)
            {
                byte[] block = new byte[32];
                for (int f = 0; f < 4; f++)
                {
                    Buffer.BlockCopy(BitConverter.GetBytes(counts[f]),
                        0, block, f * 4, 4);
                    Buffer.BlockCopy(BitConverter.GetBytes(levels[f]),
                        0, block, 16 + f * 4, 4);
                }
                if (!MemoryWriter.TryWriteBlock(mem, current.Address + 0x24,
                    block, "Counts/levels", out error))
                    return false;
            }

            if (!writeLatest || latest < 0) return true;

            uint? historyStart = mem.ReadU32(current.Address + 0x18);
            uint? historyEnd = mem.ReadU32(current.Address + 0x1C);
            if (!historyStart.HasValue || !historyEnd.HasValue ||
                historyStart.Value == 0 || historyEnd.Value <= historyStart.Value ||
                historyEnd.Value < 4)
            {
                error = "There is no history entry to update.";
                return false;
            }

            int? currentLatest = mem.ReadI32(historyEnd.Value - 4);
            if (!currentLatest.HasValue)
            {
                error = "The newest history entry is unreadable.";
                return false;
            }
            if (currentLatest.Value == latest) return true;

            return MemoryWriter.TryWriteInt32(mem, historyEnd.Value - 4, latest,
                "Latest feeling", out error);
        }
    }
}
