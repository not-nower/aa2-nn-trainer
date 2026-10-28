using System;
using System.Collections.Generic;

namespace AA2Trainer
{
    /// <summary>
    /// One classmate's lover data toward the selected character. The lover flag
    /// and day counter are incoming only: they describe the classmate's view of
    /// the target, not the target's view of the classmate.
    /// </summary>
    public sealed class LoverEntry
    {
        public CharacterInfo Source;
        public bool IsLover;
        public int Days;
        public int Compatibility;
        public bool HasCompatibility;
    }

    /// <summary>
    /// Reads and writes the per-pair lover fields. The game stores one byte per
    /// target seat for the lover flag and one byte per target seat for the day
    /// counter. Compatibility is an int32 per target seat in the character data
    /// block, with a range of 0-999.
    /// </summary>
    public static class LoverEditor
    {
        public const int LoverOffset = 0xF20;
        public const int DaysOffset = 0xF39;
        public const int CharDataOffset = 0x28;
        public const int CompatibilityOffset = 0x710;

        public const int MaxDays = 255;
        public const int MaxCompatibility = 999;

        public static List<LoverEntry> ReadIncoming(GameMemory mem, GameContext ctx,
            CharacterInfo target)
        {
            List<LoverEntry> list = new List<LoverEntry>();
            if (mem == null || ctx == null || target == null) return list;
            if (target.Seat < 0 || target.Seat > 24) return list;

            foreach (CharacterInfo source in ctx.Characters)
            {
                if (source == null || source.Pointer == 0 ||
                    source.Pointer == target.Pointer) continue;

                LoverEntry entry = new LoverEntry();
                entry.Source = source;

                byte? lover = mem.ReadU8(source.Pointer + LoverOffset +
                    (uint)target.Seat);
                entry.IsLover = lover.HasValue && lover.Value != 0;

                byte? days = mem.ReadU8(source.Pointer + DaysOffset +
                    (uint)target.Seat);
                entry.Days = days.HasValue ? days.Value : 0;

                uint? data = mem.ReadU32(source.Pointer + CharDataOffset);
                if (data.HasValue && data.Value != 0)
                {
                    int? compatibility = mem.ReadI32(data.Value + CompatibilityOffset +
                        (uint)(target.Seat * 4));
                    if (compatibility.HasValue && compatibility.Value >= 0 &&
                        compatibility.Value <= MaxCompatibility)
                    {
                        entry.Compatibility = compatibility.Value;
                        entry.HasCompatibility = true;
                    }
                }

                list.Add(entry);
            }

            list.Sort(delegate(LoverEntry a, LoverEntry b)
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

        public static bool TryWriteLover(GameMemory mem, CharacterInfo source,
            int targetSeat, bool isLover, out string error)
        {
            error = null;
            if (!ValidatePair(source, targetSeat, out error)) return false;

            uint address = source.Pointer + LoverOffset + (uint)targetSeat;
            return MemoryWriter.TryWriteByte(mem, address, isLover ? (byte)1 : (byte)0,
                "Lover flag", out error);
        }

        public static bool TryWriteDays(GameMemory mem, CharacterInfo source,
            int targetSeat, int days, out string error)
        {
            error = null;
            if (!ValidatePair(source, targetSeat, out error)) return false;
            if (days < 0 || days > MaxDays)
            {
                error = "Days must be between 0 and " + MaxDays.ToString() + ".";
                return false;
            }

            uint address = source.Pointer + DaysOffset + (uint)targetSeat;
            return MemoryWriter.TryWriteByte(mem, address, (byte)days,
                "Lover days", out error);
        }

        public static bool TryWriteCompatibility(GameMemory mem, CharacterInfo source,
            int targetSeat, int compatibility, out string error)
        {
            error = null;
            if (!ValidatePair(source, targetSeat, out error)) return false;
            if (compatibility < 0 || compatibility > MaxCompatibility)
            {
                error = "Compatibility must be between 0 and " +
                    MaxCompatibility.ToString() + ".";
                return false;
            }

            uint? data = mem.ReadU32(source.Pointer + CharDataOffset);
            if (!data.HasValue || data.Value == 0)
            {
                error = "The character data for this classmate is unavailable.";
                return false;
            }
            if (!mem.IsPlausiblePointer(data.Value))
            {
                error = "The character data pointer is not safe to write.";
                return false;
            }

            uint address = data.Value + CompatibilityOffset +
                (uint)(targetSeat * 4);
            return MemoryWriter.TryWriteInt32(mem, address, compatibility,
                "Compatibility", out error);
        }

        private static bool ValidatePair(CharacterInfo source, int targetSeat,
            out string error)
        {
            error = null;
            if (source == null || source.Pointer == 0)
            {
                error = "The classmate is no longer available.";
                return false;
            }
            if (targetSeat < 0 || targetSeat > 24)
            {
                error = "The selected character has no class seat.";
                return false;
            }
            return true;
        }
    }
}
