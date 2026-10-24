using System;

namespace AA2Trainer
{
    // Two weeks of weekday risk levels. Week 1 lives at charData + 0x702 and
    // week 2 directly after it at charData + 0x709 (seven bytes each).
    public static class RiskSchedule
    {
        public const int Week1Offset = 0x702;
        public const int Week2Offset = 0x709;
        public const int DayCount = 7;
        public const int WeekCount = 2;
        public const int MaxLevel = 2;

        public static readonly string[] Days = new string[]
        {
            "Sunday", "Monday", "Tuesday", "Wednesday",
            "Thursday", "Friday", "Saturday"
        };

        public static readonly string[] WeekNames = new string[]
        {
            "First week", "Second week"
        };

        public static string LevelName(int value)
        {
            switch (value)
            {
                case 0: return "No Risk";
                case 1: return "Low";
                case 2: return "High";
                default: return value.ToString();
            }
        }

        public static int OffsetOf(int week, int day)
        {
            return (week == 0 ? Week1Offset : Week2Offset) + day;
        }

        public static bool TryRead(GameMemory mem, CharacterInfo info,
            out int[,] values, out string error)
        {
            values = null;
            error = null;

            if (mem == null || !mem.IsAttached)
            {
                error = "The game is not attached.";
                return false;
            }
            if (info == null || info.Pointer == 0)
            {
                error = "No character is selected.";
                return false;
            }

            uint characterData;
            if (!CharacterEditor.TryGetCharacterData(mem, info, out characterData, out error))
                return false;

            byte[] bytes = mem.ReadBytes(characterData + (uint)Week1Offset,
                DayCount * WeekCount);
            if (bytes == null)
            {
                error = "The risk schedule is unreadable.";
                return false;
            }

            values = new int[WeekCount, DayCount];
            for (int week = 0; week < WeekCount; week++)
            {
                for (int day = 0; day < DayCount; day++)
                    values[week, day] = bytes[week * DayCount + day];
            }
            return true;
        }

        public static bool TryWrite(GameMemory mem, CharacterInfo info, int week, int day,
            int value, out string error)
        {
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
            if (info == null || info.Pointer == 0)
            {
                error = "No character is selected.";
                return false;
            }
            if (week < 0 || week >= WeekCount || day < 0 || day >= DayCount)
            {
                error = "The risk slot is out of range.";
                return false;
            }
            if (value < 0 || value > 255)
            {
                error = "The risk value is out of range.";
                return false;
            }

            uint characterData;
            if (!CharacterEditor.TryGetCharacterData(mem, info, out characterData, out error))
                return false;

            uint address = characterData + (uint)OffsetOf(week, day);
            byte desired = (byte)value;
            if (!mem.WriteBytes(address, new byte[] { desired }))
            {
                error = "Risk write failed: " + (mem.WriteError ?? "unknown error");
                return false;
            }

            byte? check = mem.ReadU8(address);
            if (!check.HasValue || check.Value != desired)
            {
                error = "Risk verification failed at 0x" + address.ToString("X8") + ".";
                return false;
            }

            return true;
        }
    }
}
