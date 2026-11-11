using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AA2Trainer;

namespace AA2Trainer.WpfPrototype
{
    public static class GameplayLabels
    {
        public static readonly string[] FeelingNames =
            new string[] { "Love", "Like", "Dislike", "Hate" };

        public static readonly string[] TierChoices =
            new string[] { "Lots", "Some", "Low" };

        public static string Feeling(int value)
        {
            return value >= 0 && value < FeelingNames.Length
                ? FeelingNames[value] : "-";
        }

        public static string Tier(int value)
        {
            return value >= 0 && value < TierChoices.Length
                ? TierChoices[value] : "-";
        }
    }

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

    public sealed class RelationshipFeelingValue : ObservableObject
    {
        private readonly Action changed;
        private int count;
        private int points;
        private int levelIndex;
        private int liveCount;
        private int livePoints;
        private int liveLevel;

        public RelationshipFeelingValue(string name, Action changed)
        {
            Name = name ?? "";
            this.changed = changed;
        }

        public string Name { get; private set; }
        public string[] LevelChoices { get { return GameplayLabels.TierChoices; } }

        public int Count
        {
            get { return count; }
            set
            {
                int next = Math.Max(0, Math.Min(30, value));
                if (!Set(ref count, next)) return;
                OnPropertyChanged(nameof(IsDirty));
                if (changed != null) changed();
            }
        }

        public int Points
        {
            get { return points; }
            set
            {
                int next = Math.Max(0, Math.Min(29, value));
                if (!Set(ref points, next)) return;
                OnPropertyChanged(nameof(IsDirty));
                if (changed != null) changed();
            }
        }

        public int LevelIndex
        {
            get { return levelIndex; }
            set
            {
                int next = Math.Max(0, Math.Min(2, value));
                if (!Set(ref levelIndex, next)) return;
                OnPropertyChanged(nameof(IsDirty));
                if (changed != null) changed();
            }
        }

        public bool IsDirty
        {
            get
            {
                return count != liveCount || points != livePoints ||
                    levelIndex != liveLevel;
            }
        }

        public void SetLive(int countValue, int pointsValue, int levelValue)
        {
            liveCount = countValue;
            livePoints = pointsValue;
            liveLevel = levelValue;
            count = countValue;
            points = pointsValue;
            levelIndex = levelValue;
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(nameof(Points));
            OnPropertyChanged(nameof(LevelIndex));
            OnPropertyChanged(nameof(IsDirty));
        }

        public void MarkClean()
        {
            liveCount = count;
            livePoints = points;
            liveLevel = levelIndex;
            OnPropertyChanged(nameof(IsDirty));
        }

        public void Revert()
        {
            Count = liveCount;
            Points = livePoints;
            LevelIndex = liveLevel;
        }
    }

    public sealed class RelationshipRowItem : ObservableObject
    {
        private int latestIndex;
        private int liveLatest = -1;
        private bool hasHistory;
        private bool loading;

        public RelationshipRowItem()
        {
            Love = new RelationshipFeelingValue("Love", OnFeelingChanged);
            Like = new RelationshipFeelingValue("Like", OnFeelingChanged);
            Dislike = new RelationshipFeelingValue("Dislike", OnFeelingChanged);
            Hate = new RelationshipFeelingValue("Hate", OnFeelingChanged);
        }

        public CharacterInfo Source;
        public RelationshipRecord Record;
        public int SourceSeat = -1;
        public int TargetSeat = -1;
        public string SourceName = "";
        public bool IsLover;

        public RelationshipFeelingValue Love { get; private set; }
        public RelationshipFeelingValue Like { get; private set; }
        public RelationshipFeelingValue Dislike { get; private set; }
        public RelationshipFeelingValue Hate { get; private set; }

        public string[] LatestChoices { get { return GameplayLabels.FeelingNames; } }
        public bool HasHistory { get { return hasHistory; } }

        public event Action Changed;

        public int LatestIndex
        {
            get { return latestIndex; }
            set
            {
                int next = Math.Max(0, Math.Min(3, value));
                if (!Set(ref latestIndex, next)) return;
                OnPropertyChanged(nameof(IsDirty));
                if (Changed != null) Changed();
            }
        }

        public bool IsDirty
        {
            get
            {
                return Love.IsDirty || Like.IsDirty || Dislike.IsDirty ||
                    Hate.IsDirty || (hasHistory && latestIndex != liveLatest);
            }
        }

        public string SourceText
        {
            get
            {
                string seat = SourceSeat >= 0
                    ? SourceSeat.ToString("D2") + "  " : "";
                return seat + SourceName;
            }
        }

        public void Load(RelationshipRecord record)
        {
            loading = true;
            try
            {
                Record = record;
                Source = record == null ? null : record.Source;
                SourceSeat = record == null || record.Source == null
                    ? -1 : record.Source.Seat;
                TargetSeat = record == null ? -1 : record.TargetSeat;
                SourceName = record == null || record.Source == null
                    ? "" : record.Source.Name ?? "";
                if (record == null) return;

                hasHistory = record.HistoryCount > 0;
                liveLatest = record.Latest;
                latestIndex = record.Latest >= 0 ? record.Latest : 0;
                Love.SetLive(record.Counts[0], record.Points[0], record.Levels[0]);
                Like.SetLive(record.Counts[1], record.Points[1], record.Levels[1]);
                Dislike.SetLive(record.Counts[2], record.Points[2], record.Levels[2]);
                Hate.SetLive(record.Counts[3], record.Points[3], record.Levels[3]);
                OnPropertyChanged(nameof(HasHistory));
                OnPropertyChanged(nameof(LatestIndex));
                OnPropertyChanged(nameof(IsDirty));
            }
            finally
            {
                loading = false;
            }
        }

        public RelationshipEdit BuildEdit()
        {
            return new RelationshipEdit
            {
                SourcePointer = Source == null ? 0 : Source.Pointer,
                TargetSeat = TargetSeat,
                Counts = new int[]
                {
                    Love.Count, Like.Count, Dislike.Count, Hate.Count
                },
                Points = new int[]
                {
                    Love.Points, Like.Points, Dislike.Points, Hate.Points
                },
                Levels = new int[]
                {
                    Love.LevelIndex, Like.LevelIndex, Dislike.LevelIndex,
                    Hate.LevelIndex
                },
                Latest = latestIndex,
                WriteLatest = hasHistory && latestIndex != liveLatest
            };
        }

        public void MarkClean()
        {
            Love.MarkClean();
            Like.MarkClean();
            Dislike.MarkClean();
            Hate.MarkClean();
            liveLatest = latestIndex;
            OnPropertyChanged(nameof(IsDirty));
        }

        public void Revert()
        {
            Love.Revert();
            Like.Revert();
            Dislike.Revert();
            Hate.Revert();
            latestIndex = liveLatest >= 0 ? liveLatest : 0;
            OnPropertyChanged(nameof(LatestIndex));
            OnPropertyChanged(nameof(IsDirty));
        }

        private void OnFeelingChanged()
        {
            if (loading) return;
            OnPropertyChanged(nameof(IsDirty));
            if (Changed != null) Changed();
        }
    }

    public sealed class BehaviorRowItem
    {
        public CharacterInfo Source;
        public int SourceSeat = -1;
        public string SourceName = "";
        public string History { get; set; } = "";
        public string Latest { get; set; } = "-";
        public int[] Values = new int[0];
        public bool HasRecord;

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

    public sealed class BehaviorEventItem : ObservableObject
    {
        private int feeling;

        public BehaviorEventItem(int index, int feeling)
        {
            Index = index;
            this.feeling = feeling;
        }

        public int Index { get; private set; }
        public string[] Choices { get { return GameplayLabels.FeelingNames; } }

        public int Feeling
        {
            get { return feeling; }
            set
            {
                int next = Math.Max(0, Math.Min(3, value));
                if (!Set(ref feeling, next)) return;
                OnPropertyChanged(nameof(FeelingText));
            }
        }

        public string FeelingText { get { return GameplayLabels.Feeling(feeling); } }
    }

    public sealed class BehaviorDraft : ObservableObject
    {
        private int[] liveFeelings = new int[0];

        public BehaviorDraft()
        {
            Events = new ObservableCollection<BehaviorEventItem>();
        }

        public event Action Changed;

        public CharacterInfo Source { get; private set; }
        public int TargetSeat { get; private set; } = -1;
        public ObservableCollection<BehaviorEventItem> Events { get; private set; }

        public bool IsDirty
        {
            get
            {
                for (int i = 0; i < Events.Count; i++)
                {
                    if (Events[i].Feeling != liveFeelings[i]) return true;
                }
                return false;
            }
        }

        public void Load(RelationshipHistoryRow row, int targetSeat)
        {
            Events.Clear();
            Source = row == null ? null : row.Source;
            TargetSeat = targetSeat;
            if (row == null) return;

            int[] history = row.History ?? new int[0];
            liveFeelings = new int[history.Length];
            for (int i = 0; i < history.Length; i++)
            {
                liveFeelings[i] = history[i];
                BehaviorEventItem item = new BehaviorEventItem(i, history[i]);
                item.PropertyChanged += Item_PropertyChanged;
                Events.Add(item);
            }
            OnPropertyChanged(nameof(IsDirty));
        }

        private void Item_PropertyChanged(object sender,
            System.ComponentModel.PropertyChangedEventArgs e)
        {
            OnPropertyChanged(nameof(IsDirty));
            if (Changed != null) Changed();
        }

        public GameplayWriteRequest BuildRequest()
        {
            List<int> indices = new List<int>();
            List<int> feelings = new List<int>();
            for (int i = 0; i < Events.Count; i++)
            {
                if (Events[i].Feeling == liveFeelings[i]) continue;
                indices.Add(Events[i].Index);
                feelings.Add(Events[i].Feeling);
            }

            return new GameplayWriteRequest
            {
                Kind = GameplayWriteKind.HistoryEvents,
                SourcePointer = Source == null ? 0 : Source.Pointer,
                TargetSeat = TargetSeat,
                EventIndices = indices.ToArray(),
                EventFeelings = feelings.ToArray()
            };
        }

        public void MarkClean()
        {
            liveFeelings = new int[Events.Count];
            for (int i = 0; i < Events.Count; i++)
                liveFeelings[i] = Events[i].Feeling;
            OnPropertyChanged(nameof(IsDirty));
        }

        public void Revert()
        {
            for (int i = 0; i < Events.Count; i++)
            {
                if (i < liveFeelings.Length)
                    Events[i].Feeling = liveFeelings[i];
            }
            OnPropertyChanged(nameof(IsDirty));
        }
    }

    public sealed class LoverRowItem : ObservableObject
    {
        private int days;
        private int compatibility;
        private int liveDays;
        private int liveCompatibility;
        private bool isLover;

        public CharacterInfo Source;
        public int SourceSeat = -1;
        public string SourceName = "";
        public Action<LoverRowItem, bool> LoverChanged;

        public bool IsLover
        {
            get { return isLover; }
            set
            {
                if (!Set(ref isLover, value)) return;
                if (LoverChanged != null) LoverChanged(this, value);
            }
        }

        public string SourceText
        {
            get
            {
                string seat = SourceSeat >= 0
                    ? SourceSeat.ToString("D2") + "  " : "";
                return seat + SourceName;
            }
        }

        public int Days
        {
            get { return days; }
            set
            {
                int next = Math.Max(0, Math.Min(255, value));
                if (!Set(ref days, next)) return;
                OnPropertyChanged(nameof(IsDirty));
            }
        }

        public int Compatibility
        {
            get { return compatibility; }
            set
            {
                int next = Math.Max(0, Math.Min(999, value));
                if (!Set(ref compatibility, next)) return;
                OnPropertyChanged(nameof(IsDirty));
            }
        }

        public bool IsDirty
        {
            get
            {
                return days != liveDays || compatibility != liveCompatibility;
            }
        }

        public void SetLiveValues(int liveDaysValue, int liveCompatibilityValue,
            bool isLoverValue)
        {
            liveDays = liveDaysValue;
            liveCompatibility = liveCompatibilityValue;
            days = liveDaysValue;
            compatibility = liveCompatibilityValue;
            isLover = isLoverValue;
            OnPropertyChanged(nameof(Days));
            OnPropertyChanged(nameof(Compatibility));
            OnPropertyChanged(nameof(IsLover));
            OnPropertyChanged(nameof(IsDirty));
        }

        public void SetLoverSilently(bool value)
        {
            if (isLover == value) return;
            isLover = value;
            OnPropertyChanged(nameof(IsLover));
        }

        public void MarkClean()
        {
            liveDays = days;
            liveCompatibility = compatibility;
            OnPropertyChanged(nameof(IsDirty));
        }

        public void Revert()
        {
            Days = liveDays;
            Compatibility = liveCompatibility;
        }

        public LoverEdit BuildEdit()
        {
            return new LoverEdit
            {
                SourcePointer = Source == null ? 0 : Source.Pointer,
                TargetSeat = SourceSeat,
                Days = days,
                Compatibility = compatibility,
                WriteDays = days != liveDays,
                WriteCompatibility = compatibility != liveCompatibility
            };
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
