using System;

namespace AA2Trainer.WpfPrototype
{
    /// <summary>
    /// Editable copy of the class schedule. The read-only academy/year/class/
    /// save fields are carried through so the Core write keeps them intact.
    /// </summary>
    public sealed class ScheduleDraft : ObservableObject
    {
        private ScheduleSnapshot original;
        private int weekday;
        private int week;
        private int period;
        private int duration;
        private int minute;
        private int second;
        private string academy = "";
        private int year;
        private int classNumber;
        private string saveName = "";

        public int Weekday
        {
            get { return weekday; }
            set { if (Set(ref weekday, value)) DirtyChanged(); }
        }

        public int Week
        {
            get { return week; }
            set
            {
                if (!Set(ref week, value)) return;
                OnPropertyChanged(nameof(WeekIndex));
                DirtyChanged();
            }
        }

        public int WeekIndex
        {
            get { return Math.Max(0, Math.Min(5, week - 1)); }
            set { Week = value + 1; }
        }

        public int Period
        {
            get { return period; }
            set { if (Set(ref period, value)) DirtyChanged(); }
        }

        public int Duration
        {
            get { return duration; }
            set
            {
                int next = Math.Max(ScheduleEditor.MinDuration,
                    Math.Min(ScheduleEditor.MaxDuration, value));
                if (Set(ref duration, next)) DirtyChanged();
            }
        }

        public int Minute
        {
            get { return minute; }
            set
            {
                int next = Math.Max(0, Math.Min(59, value));
                if (Set(ref minute, next)) DirtyChanged();
            }
        }

        public int Second
        {
            get { return second; }
            set
            {
                int next = Math.Max(0, Math.Min(59, value));
                if (Set(ref second, next)) DirtyChanged();
            }
        }

        public string Academy
        {
            get { return academy; }
            private set { Set(ref academy, value ?? ""); }
        }

        public int Year
        {
            get { return year; }
            private set { Set(ref year, value); }
        }

        public int ClassNumber
        {
            get { return classNumber; }
            private set { Set(ref classNumber, value); }
        }

        public string SaveName
        {
            get { return saveName; }
            private set { Set(ref saveName, value ?? ""); }
        }

        public int TimeMilliseconds
        {
            get { return (minute * 60 + second) * 1000; }
        }

        public bool IsDirty
        {
            get
            {
                if (original == null) return false;
                return weekday != original.Weekday ||
                    week != original.Week ||
                    period != original.Period ||
                    duration != original.PeriodDuration ||
                    TimeMilliseconds != original.TimeMilliseconds;
            }
        }

        public void Load(ScheduleSnapshot snapshot)
        {
            original = snapshot;
            if (snapshot == null)
            {
                weekday = 0;
                week = 1;
                period = 0;
                duration = ScheduleEditor.MinDuration;
                minute = 0;
                second = 0;
                academy = "";
                year = 0;
                classNumber = 0;
                saveName = "";
            }
            else
            {
                weekday = snapshot.Weekday;
                week = snapshot.Week;
                period = snapshot.Period;
                duration = snapshot.PeriodDuration;
                minute = snapshot.TimeMilliseconds / 60000;
                second = (snapshot.TimeMilliseconds / 1000) % 60;
                academy = snapshot.Academy ?? "";
                year = snapshot.Year;
                classNumber = snapshot.ClassNumber;
                saveName = snapshot.SaveName ?? "";
            }

            OnPropertyChanged(string.Empty);
            OnPropertyChanged(nameof(IsDirty));
            OnPropertyChanged(nameof(TimeMilliseconds));
        }

        public ScheduleSnapshot ToSnapshot()
        {
            ScheduleSnapshot snapshot = new ScheduleSnapshot();
            snapshot.Weekday = weekday;
            snapshot.Week = week;
            snapshot.Period = period;
            snapshot.PeriodDuration = duration;
            snapshot.TimeMilliseconds = TimeMilliseconds;
            snapshot.GameDay = (week - 1) * 7 + weekday + 1;
            snapshot.Academy = academy;
            snapshot.Year = year;
            snapshot.ClassNumber = classNumber;
            snapshot.SaveName = saveName;
            return snapshot;
        }

        public void MarkSaved()
        {
            original = ToSnapshot();
            OnPropertyChanged(nameof(IsDirty));
        }

        private void DirtyChanged()
        {
            OnPropertyChanged(nameof(IsDirty));
            OnPropertyChanged(nameof(TimeMilliseconds));
        }
    }
}
