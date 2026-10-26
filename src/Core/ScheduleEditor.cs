using System;
using System.Text;

namespace AA2Trainer
{
    public sealed class ScheduleSnapshot
    {
        public int Weekday;
        public int Week;
        public int GameDay;
        public int Period;
        public int TimeMilliseconds;
        public int PeriodDuration;
        public string Academy = "";
        public int Year;
        public int ClassNumber;
        public string SaveName = "";
    }

    // The daily schedule and class header share two pointer roots hanging off
    // the global class data. The week is 1-6, the weekday and period are
    // zero-based, TimeGame is the current period timer in milliseconds, and
    // each period has its own duration slot (in minutes).
    public static class ScheduleEditor
    {
        public const int TimeRootChainOffset = 0x2C;
        public const int ClassRootChainOffset = 0x28;

        public const int WeekdayOffset = 0x24;
        public const int WeekOffset = 0x28;
        public const int PeriodOffset = 0x20;
        public const int TimeOffset = 0x2C;
        public const int DurationBaseOffset = 0x30;
        public const int DurationSlots = 16;

        public const int AcademyOffset = 0x04;
        public const int AcademyBytes = 64;
        public const int YearOffset = 0x44;
        public const int ClassOffset = 0x54;
        public const int SaveNameOffset = 0x64;
        public const int SaveNameBytes = 260;

        public const int MinDuration = 1;
        public const int MaxDuration = 999;
        public const int MaxTimeMilliseconds = 59999000;

        public static readonly string[] Weekdays = new string[]
        {
            "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"
        };

        public static readonly string[] Periods = new string[]
        {
            "Morning", "Go Out", "Before Classes =>", "Studying", "Lunch",
            "Go Lessons =>", "Lessons", "Before Clubs =>", "Evening",
            "Fall Asleep", "Go Home =>", "Test", "Preg End", "Skip End"
        };

        private static readonly Encoding GameEncoding = GetGameEncoding();

        public static string WeekLabel(int week)
        {
            return "Week " + week.ToString();
        }

        public static bool TryRead(GameMemory mem, GameContext ctx,
            out ScheduleSnapshot snapshot, out string error)
        {
            snapshot = null;
            error = null;

            uint timeRoot;
            uint classRoot;
            if (!TryResolveRoots(mem, ctx, out timeRoot, out classRoot, out error))
                return false;

            byte[] time = mem.ReadBytes(timeRoot, 0x70);
            if (time == null || time.Length < 0x70)
            {
                error = "The schedule block is unreadable.";
                return false;
            }

            int period = time[PeriodOffset];
            if (period < 0 || period >= DurationSlots)
            {
                error = "The current period index is out of range.";
                return false;
            }

            int gameDay = time[WeekOffset];
            int weekday = time[WeekdayOffset];
            if (gameDay < 1 || gameDay > 42)
            {
                error = "The game day is out of range.";
                return false;
            }
            if (weekday < 0 || weekday >= Weekdays.Length)
            {
                error = "The weekday is out of range.";
                return false;
            }

            byte[] year = mem.ReadBytes(classRoot + YearOffset, 1);
            byte[] classNumber = mem.ReadBytes(classRoot + ClassOffset, 1);
            if (year == null || classNumber == null)
            {
                error = "The class header is unreadable.";
                return false;
            }

            string academy = mem.ReadAnsiString(classRoot + AcademyOffset, AcademyBytes);
            string saveName = mem.ReadUnicodeString(classRoot + SaveNameOffset, SaveNameBytes);
            if (academy == null || saveName == null)
            {
                error = "The academy or save name is unreadable.";
                return false;
            }

            snapshot = new ScheduleSnapshot();
            snapshot.Period = period;
            snapshot.Weekday = weekday;
            snapshot.GameDay = gameDay;
            // DayWeek is the 1-42 game day counter. The UI exposes it as a
            // six-week calendar, so derive the week from the day counter.
            snapshot.Week = ((gameDay - 1) / 7) + 1;
            snapshot.TimeMilliseconds = BitConverter.ToInt32(time, TimeOffset);
            snapshot.PeriodDuration = BitConverter.ToInt32(
                time, DurationBaseOffset + period * 4);
            snapshot.Academy = academy.Trim();
            snapshot.Year = DigitValue(year[0]);
            snapshot.ClassNumber = DigitValue(classNumber[0]);
            snapshot.SaveName = saveName.Trim();
            return true;
        }

        public static bool TryReadPeriodDuration(GameMemory mem, GameContext ctx,
            int period, out int minutes, out string error)
        {
            minutes = 0;
            error = null;

            if (period < 0 || period >= DurationSlots)
            {
                error = "The selected period is out of range.";
                return false;
            }

            uint timeRoot;
            uint classRoot;
            if (!TryResolveRoots(mem, ctx, out timeRoot, out classRoot, out error))
                return false;

            int? value = mem.ReadI32(timeRoot + (uint)(DurationBaseOffset + period * 4));
            if (!value.HasValue)
            {
                error = "The period duration is unreadable.";
                return false;
            }

            minutes = value.Value;
            return true;
        }

        public static bool TryWrite(GameMemory mem, GameContext ctx,
            ScheduleSnapshot original, ScheduleSnapshot edited,
            out string changed, out string error)
        {
            changed = "";
            error = null;

            if (mem == null || !mem.IsAttached)
            {
                error = "The game is not attached.";
                return false;
            }
            if (!mem.CanWrite)
            {
                error = string.IsNullOrEmpty(mem.WriteError)
                    ? "The game handle is read-only."
                    : mem.WriteError;
                return false;
            }
            if (edited == null)
            {
                error = "The schedule values are incomplete.";
                return false;
            }
            if (edited.Weekday < 0 || edited.Weekday >= Weekdays.Length)
            {
                error = "The weekday is out of range.";
                return false;
            }
            if (edited.Week < 1 || edited.Week > 6)
            {
                error = "The week must be between 1 and 6.";
                return false;
            }
            int gameDay = (edited.Week - 1) * 7 + edited.Weekday + 1;
            if (gameDay < 1 || gameDay > 42)
            {
                error = "The combined week and weekday are out of range.";
                return false;
            }
            if (edited.Period < 0 || edited.Period >= DurationSlots)
            {
                error = "The period is out of range.";
                return false;
            }
            if (edited.PeriodDuration < MinDuration || edited.PeriodDuration > MaxDuration)
            {
                error = "The period duration must be between " + MinDuration.ToString() +
                        " and " + MaxDuration.ToString() + " minutes.";
                return false;
            }
            if (edited.TimeMilliseconds < 0 ||
                edited.TimeMilliseconds > MaxTimeMilliseconds)
            {
                error = "The time must be between 00:00 and 999:59.";
                return false;
            }
            if (edited.Year < 0 || edited.Year > 9)
            {
                error = "The year must be a single digit.";
                return false;
            }
            if (edited.ClassNumber < 0 || edited.ClassNumber > 9)
            {
                error = "The class must be a single digit.";
                return false;
            }

            edited.Academy = (edited.Academy ?? "").Trim();
            byte[] academyBytes;
            try
            {
                academyBytes = GameEncoding.GetBytes(edited.Academy);
            }
            catch (Exception ex)
            {
                error = "The academy name could not be encoded: " + ex.Message;
                return false;
            }
            if (academyBytes.Length > AcademyBytes - 1)
            {
                error = "The academy name is too long for the game's " +
                        (AcademyBytes - 1).ToString() + "-byte field.";
                return false;
            }

            uint timeRoot;
            uint classRoot;
            if (!TryResolveRoots(mem, ctx, out timeRoot, out classRoot, out error))
                return false;

            StringBuilder written = new StringBuilder();
            bool academyChanged = original == null ||
                !string.Equals((original.Academy ?? "").Trim(), edited.Academy,
                    StringComparison.Ordinal);
            if (academyChanged)
            {
                byte[] buffer = new byte[AcademyBytes];
                Array.Copy(academyBytes, buffer, academyBytes.Length);
                if (!MemoryWriter.TryWriteBlock(mem, classRoot + AcademyOffset, buffer,
                    "Academy name", out error))
                    return Partial(written, error, out changed);
                Mark(written, "academy");
            }

            if (original == null || edited.Year != original.Year)
            {
                byte value = (byte)('0' + edited.Year);
                if (!MemoryWriter.TryWriteByte(mem, classRoot + YearOffset, value,
                    "Year", out error))
                    return Partial(written, error, out changed);
                Mark(written, "year");
            }

            if (original == null || edited.ClassNumber != original.ClassNumber)
            {
                byte value = (byte)('0' + edited.ClassNumber);
                if (!MemoryWriter.TryWriteByte(mem, classRoot + ClassOffset, value,
                    "Class", out error))
                    return Partial(written, error, out changed);
                Mark(written, "class");
            }

            bool weekdayChanged = original == null ||
                edited.Weekday != original.Weekday;
            bool gameDayChanged = original == null || gameDay != original.GameDay;
            if (weekdayChanged || gameDayChanged)
            {
                if (!MemoryWriter.TryWriteByte(mem, timeRoot + WeekdayOffset,
                    (byte)edited.Weekday, "Weekday", out error))
                    return Partial(written, error, out changed);
                if (!MemoryWriter.TryWriteByte(mem, timeRoot + WeekOffset,
                    (byte)gameDay, "Week", out error))
                    return Partial(written, error, out changed);
                if (weekdayChanged) Mark(written, "weekday");
                if (gameDayChanged) Mark(written, "week");
            }

            if (original == null || edited.PeriodDuration != original.PeriodDuration)
            {
                uint address = timeRoot +
                    (uint)(DurationBaseOffset + edited.Period * 4);
                if (!MemoryWriter.TryWriteInt32(mem, address, edited.PeriodDuration,
                    "Period duration", out error))
                    return Partial(written, error, out changed);
                Mark(written, "period duration");
            }

            if (original == null || edited.Period != original.Period)
            {
                if (!MemoryWriter.TryWriteByte(mem, timeRoot + PeriodOffset,
                    (byte)edited.Period, "Period", out error))
                    return Partial(written, error, out changed);
                Mark(written, "period");
            }

            if (original == null || edited.TimeMilliseconds != original.TimeMilliseconds)
            {
                if (!MemoryWriter.TryWriteInt32(mem, timeRoot + TimeOffset,
                    edited.TimeMilliseconds, "Time", out error))
                    return Partial(written, error, out changed);
                Mark(written, "time");
            }

            changed = written.Length == 0 ? "No changes." : written.ToString();
            return true;
        }

        public static bool TryPinTime(GameMemory mem, GameContext ctx, int milliseconds,
            out bool wrote, out string error)
        {
            wrote = false;
            error = null;

            if (milliseconds < 0 || milliseconds > MaxTimeMilliseconds)
            {
                error = "The freeze time is out of range.";
                return false;
            }

            uint timeRoot;
            uint classRoot;
            if (!TryResolveRoots(mem, ctx, out timeRoot, out classRoot, out error))
                return false;

            int? current = mem.ReadI32(timeRoot + TimeOffset);
            if (!current.HasValue)
            {
                error = "The current time is unreadable.";
                return false;
            }
            if (current.Value == milliseconds) return true;

            if (!MemoryWriter.TryWriteInt32(mem, timeRoot + TimeOffset, milliseconds,
                "Time", out error))
                return false;

            wrote = true;
            return true;
        }

        private static bool TryResolveRoots(GameMemory mem, GameContext ctx,
            out uint timeRoot, out uint classRoot, out string error)
        {
            timeRoot = 0;
            classRoot = 0;
            error = null;

            if (mem == null || !mem.IsAttached)
            {
                error = "The game is not attached.";
                return false;
            }
            if (ctx == null || ctx.GlobalRoot == 0)
            {
                error = "The class data is not loaded.";
                return false;
            }

            uint? time = mem.ReadU32(ctx.GlobalRoot + TimeRootChainOffset);
            if (!time.HasValue || !mem.IsPlausiblePointer(time.Value))
            {
                error = "The time block pointer is not valid.";
                return false;
            }

            uint? classData = mem.ReadU32(ctx.GlobalRoot + ClassRootChainOffset);
            if (!classData.HasValue || !mem.IsPlausiblePointer(classData.Value))
            {
                error = "The class header pointer is not valid.";
                return false;
            }

            timeRoot = time.Value;
            classRoot = classData.Value;
            return true;
        }

        private static Encoding GetGameEncoding()
        {
            try
            {
                return Encoding.GetEncoding(932, EncoderFallback.ExceptionFallback,
                    DecoderFallback.ReplacementFallback);
            }
            catch
            {
                return Encoding.Default;
            }
        }

        private static int DigitValue(byte value)
        {
            if (value >= (byte)'0' && value <= (byte)'9')
                return value - (byte)'0';
            return 0;
        }

        private static bool Partial(StringBuilder written, string error,
            out string changed)
        {
            changed = written.Length == 0 ? "" : written.ToString();
            return false;
        }

        private static void Mark(StringBuilder written, string name)
        {
            if (written.Length > 0) written.Append(", ");
            written.Append(name);
        }
    }
}
