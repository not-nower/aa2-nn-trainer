using System;

namespace AA2Trainer.WpfPrototype
{
    public sealed class LimitActionItem : ObservableObject
    {
        private readonly Action<int, bool> changed;
        private bool limited;

        public LimitActionItem(int id, string text, Action<int, bool> changed)
        {
            Id = id;
            Text = text ?? "";
            this.changed = changed;
        }

        public int Id { get; private set; }
        public string Text { get; private set; }

        public bool IsLimited
        {
            get { return limited; }
            set
            {
                if (!Set(ref limited, value)) return;
                if (changed != null) changed(Id, value);
            }
        }

        public void SetLimitedSilently(bool value)
        {
            if (limited == value) return;
            limited = value;
            OnPropertyChanged(nameof(IsLimited));
        }
    }

    public sealed class RelationshipRowItem
    {
        public int SourceSeat { get; set; } = -1;
        public string SourceName { get; set; } = "";
        public int LoveCount { get; set; }
        public int LikeCount { get; set; }
        public int DislikeCount { get; set; }
        public int HateCount { get; set; }
        public int LoveLevel { get; set; }
        public int LikeLevel { get; set; }
        public int DislikeLevel { get; set; }
        public int HateLevel { get; set; }
        public string Latest { get; set; } = "-";
        public bool IsLover { get; set; }

        public string LoveText { get { return CountLevel(LoveCount, LoveLevel); } }
        public string LikeText { get { return CountLevel(LikeCount, LikeLevel); } }
        public string DislikeText { get { return CountLevel(DislikeCount, DislikeLevel); } }
        public string HateText { get { return CountLevel(HateCount, HateLevel); } }

        public string SourceText
        {
            get
            {
                string seat = SourceSeat >= 0
                    ? SourceSeat.ToString("D2") + "  " : "";
                return seat + SourceName;
            }
        }

        private static string CountLevel(int count, int level)
        {
            return count + " / " + (level >= 0 ? "L" + level : "-");
        }
    }

    public sealed class BehaviorRowItem
    {
        public int SourceSeat { get; set; } = -1;
        public string SourceName { get; set; } = "";
        public string History { get; set; } = "";
        public string Latest { get; set; } = "-";

        public string SourceText
        {
            get
            {
                string seat = SourceSeat >= 0
                    ? SourceSeat.ToString("D2") + "  " : "";
                return seat + SourceName;
            }
        }
    }

    public sealed class LoverRowItem
    {
        public int SourceSeat { get; set; } = -1;
        public string SourceName { get; set; } = "";
        public bool IsLover { get; set; }
        public int Days { get; set; }
        public int Compatibility { get; set; }

        public string SourceText
        {
            get
            {
                string seat = SourceSeat >= 0
                    ? SourceSeat.ToString("D2") + "  " : "";
                return seat + SourceName;
            }
        }
    }

    public sealed class PromiseRowItem
    {
        public string Direction { get; set; } = "";
        public string Kind { get; set; } = "";
        public int OtherSeat { get; set; } = -1;
        public string OtherName { get; set; } = "";
        public string Location { get; set; } = "";
        public string TestType { get; set; } = "";
        public string Flag { get; set; } = "";

        public string OtherText
        {
            get
            {
                string seat = OtherSeat >= 0
                    ? OtherSeat.ToString("D2") + "  " : "";
                return seat + OtherName;
            }
        }
    }
}
