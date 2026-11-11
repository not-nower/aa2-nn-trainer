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
            new string[] { "Lots", "Some", "Very little" };

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

    public sealed class RelationshipRowItem
    {
        public CharacterInfo Source;
        public RelationshipRecord Record;
        public int SourceSeat = -1;
        public string SourceName = "";
        public int LoveCount;
        public int LikeCount;
        public int DislikeCount;
        public int HateCount;
        public int LoveLevel;
        public int LikeLevel;
        public int DislikeLevel;
        public int HateLevel;
        public string Latest { get; set; } = "-";
        public bool IsLover;

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

    public sealed class RelationshipFeelingEditItem : ObservableObject
    {
        private int count;
        private int points;
        private int levelIndex;

        public RelationshipFeelingEditItem(string name)
        {
            Name = name ?? "";
        }

        public string Name { get; private set; }
        public string[] LevelChoices { get { return GameplayLabels.TierChoices; } }

        public int Count
        {
            get { return count; }
            set
            {
                int next = Math.Max(0, Math.Min(30, value));
                Set(ref count, next);
            }
        }

        public int Points
        {
            get { return points; }
            set
            {
                int next = Math.Max(0, Math.Min(29, value));
                Set(ref points, next);
            }
        }

        public int LevelIndex
        {
            get { return levelIndex; }
            set
            {
                int next = Math.Max(0, Math.Min(2, value));
                Set(ref levelIndex, next);
            }
        }
    }

    public sealed class RelationshipDraft : ObservableObject
    {
        private readonly int[] liveCounts = new int[4];
        private readonly int[] livePoints = new int[4];
        private readonly int[] liveLevels = new int[4];
        private int liveLatest = -1;
        private int latestIndex;
        private bool hasHistory;
        private bool loading;

        public RelationshipDraft()
        {
            Feelings = new RelationshipFeelingEditItem[4];
            for (int i = 0; i < 4; i++)
            {
                Feelings[i] = new RelationshipFeelingEditItem(
                    GameplayLabels.Feeling(i));
                Feelings[i].PropertyChanged += Item_PropertyChanged;
            }
        }

        public event Action Changed;

        private void Item_PropertyChanged(object sender,
            System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (loading) return;
            OnPropertyChanged(nameof(IsDirty));
            if (Changed != null) Changed();
        }

        public CharacterInfo Source { get; private set; }
        public int TargetSeat { get; private set; } = -1;
        public RelationshipFeelingEditItem[] Feelings { get; private set; }
        public string[] LatestChoices { get { return GameplayLabels.FeelingNames; } }
        public bool HasHistory { get { return hasHistory; } }

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
                for (int i = 0; i < 4; i++)
                {
                    if (Feelings[i].Count != liveCounts[i] ||
                        Feelings[i].Points != livePoints[i] ||
                        Feelings[i].LevelIndex != liveLevels[i])
                        return true;
                }
                return hasHistory && latestIndex != liveLatest;
            }
        }

        public void Load(RelationshipRecord record)
        {
            loading = true;
            try
            {
                Source = record == null ? null : record.Source;
                TargetSeat = record == null ? -1 : record.TargetSeat;
                if (record == null) return;

                hasHistory = record.HistoryCount > 0;
                liveLatest = record.Latest;
                latestIndex = record.Latest >= 0 ? record.Latest : 0;
                for (int i = 0; i < 4; i++)
                {
                    liveCounts[i] = record.Counts[i];
                    livePoints[i] = record.Points[i];
                    liveLevels[i] = record.Levels[i];
                    Feelings[i].Count = record.Counts[i];
                    Feelings[i].Points = record.Points[i];
                    Feelings[i].LevelIndex = record.Levels[i];
                }
                OnPropertyChanged(nameof(HasHistory));
                OnPropertyChanged(nameof(LatestIndex));
                OnPropertyChanged(nameof(IsDirty));
            }
            finally
            {
                loading = false;
            }
        }

        public GameplayWriteRequest BuildRequest()
        {
            int[] counts = new int[4];
            int[] points = new int[4];
            int[] levels = new int[4];
            for (int i = 0; i < 4; i++)
            {
                counts[i] = Feelings[i].Count;
                points[i] = Feelings[i].Points;
                levels[i] = Feelings[i].LevelIndex;
            }

            return new GameplayWriteRequest
            {
                Kind = GameplayWriteKind.RelationshipValues,
                SourcePointer = Source == null ? 0 : Source.Pointer,
                TargetSeat = TargetSeat,
                Counts = counts,
                Points = points,
                Levels = levels,
                Latest = latestIndex,
                WriteLatest = hasHistory && latestIndex != liveLatest
            };
        }

        public void MarkClean()
        {
            for (int i = 0; i < 4; i++)
            {
                liveCounts[i] = Feelings[i].Count;
                livePoints[i] = Feelings[i].Points;
                liveLevels[i] = Feelings[i].LevelIndex;
            }
            liveLatest = latestIndex;
            OnPropertyChanged(nameof(IsDirty));
        }

        public void Revert()
        {
            for (int i = 0; i < 4; i++)
            {
                Feelings[i].Count = liveCounts[i];
                Feelings[i].Points = livePoints[i];
                Feelings[i].LevelIndex = liveLevels[i];
            }
            latestIndex = liveLatest >= 0 ? liveLatest : 0;
            OnPropertyChanged(nameof(LatestIndex));
            OnPropertyChanged(nameof(IsDirty));
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

        public CharacterInfo Source;
        public int SourceSeat = -1;
        public string SourceName = "";
        public bool IsLover;

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
            bool isLover)
        {
            liveDays = liveDaysValue;
            liveCompatibility = liveCompatibilityValue;
            days = liveDaysValue;
            compatibility = liveCompatibilityValue;
            IsLover = isLover;
            OnPropertyChanged(nameof(Days));
            OnPropertyChanged(nameof(Compatibility));
            OnPropertyChanged(nameof(IsDirty));
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
