using System;
using System.Collections.ObjectModel;
using AA2Trainer;

namespace AA2Trainer.WpfPrototype
{
    /// <summary>
    /// One live game option. Values are kept in the same integer units that
    /// OptionEditor reads and writes: toggles/bools are 0/1, percent fields are
    /// 0-100, and DirectFloat fields are scaled by 10^DecimalPlaces.
    /// </summary>
    public sealed class GameOptionItem : ObservableObject
    {
        private readonly Action<GameOptionItem> edited;
        private int value;
        private int liveValue;

        public GameOptionItem(int index, OptionFieldDef definition,
            Action<GameOptionItem> edited)
        {
            Index = index;
            Name = definition.Name ?? "";
            Label = definition.Label ?? Name;
            Kind = definition.Kind;
            Min = definition.Min;
            Max = definition.Max;
            DecimalPlaces = definition.DecimalPlaces;
            Choices = definition.Choices ?? new string[0];
            this.edited = edited;
        }

        public int Index { get; private set; }
        public string Name { get; private set; }
        public string Label { get; private set; }
        public OptionKind Kind { get; private set; }
        public int Min { get; private set; }
        public int Max { get; private set; }
        public int DecimalPlaces { get; private set; }
        public string[] Choices { get; private set; }

        /// <summary>Optional gray hint shown next to the toggle.</summary>
        public string Hint { get; set; } = "";

        public bool HasHint
        {
            get { return !string.IsNullOrEmpty(Hint); }
        }

        public bool IsToggle
        {
            get
            {
                return Kind == OptionKind.Toggle ||
                    Kind == OptionKind.InvertedToggle;
            }
        }

        public bool IsChoice
        {
            get { return Kind == OptionKind.Choice; }
        }

        public bool IsSlider
        {
            get { return !IsToggle && !IsChoice; }
        }

        public int Value
        {
            get { return value; }
            set
            {
                if (!Set(ref this.value, value)) return;
                OnPropertyChanged(nameof(ToggleValue));
                OnPropertyChanged(nameof(DisplayValue));
                OnPropertyChanged(nameof(ValueText));
                OnPropertyChanged(nameof(IsDirty));
                if (edited != null) edited(this);
            }
        }

        public bool ToggleValue
        {
            get { return value != 0; }
            set { Value = value ? 1 : 0; }
        }

        /// <summary>
        /// Numeric-box text for slider fields. Percent/byte sliders show a
        /// plain integer; DirectFloat fields show their scaled value. Typed
        /// input is clamped to the field range so an out-of-range entry can
        /// never reach Apply.
        /// </summary>
        public string ValueText
        {
            get
            {
                if (Kind == OptionKind.DirectFloat && DecimalPlaces > 0)
                {
                    double divisor = Math.Pow(10, DecimalPlaces);
                    return (value / divisor).ToString("F" + DecimalPlaces);
                }
                return value.ToString();
            }
            set
            {
                string text = (value ?? "").Trim();
                if (text.Length == 0) return;

                double parsed;
                if (!double.TryParse(text, out parsed)) return;

                double divisor = Kind == OptionKind.DirectFloat &&
                    DecimalPlaces > 0 ? Math.Pow(10, DecimalPlaces) : 1;
                int raw = (int)Math.Round(parsed * divisor);
                if (IsSlider)
                {
                    if (raw < Min) raw = Min;
                    if (raw > Max) raw = Max;
                }
                Value = raw;
            }
        }

        public int LiveValue
        {
            get { return liveValue; }
        }

        public bool IsDirty
        {
            get { return value != liveValue; }
        }

        public string DisplayValue
        {
            get
            {
                switch (Kind)
                {
                    case OptionKind.Toggle:
                    case OptionKind.InvertedToggle:
                        return value != 0 ? "On" : "Off";
                    case OptionKind.VolumeSlider:
                    case OptionKind.PercentFloat:
                        return value + "%";
                    case OptionKind.Choice:
                        return Choices != null && value >= 0 &&
                            value < Choices.Length
                            ? Choices[value] : value.ToString();
                    case OptionKind.DirectFloat:
                        if (DecimalPlaces > 0)
                        {
                            double divisor = Math.Pow(10, DecimalPlaces);
                            return (value / divisor).ToString(
                                "F" + DecimalPlaces);
                        }
                        return value.ToString();
                    default:
                        return value.ToString();
                }
            }
        }

        public string RangeText
        {
            get
            {
                if (!IsSlider) return "";
                if (Kind == OptionKind.DirectFloat && DecimalPlaces > 0)
                {
                    double divisor = Math.Pow(10, DecimalPlaces);
                    return (Min / divisor).ToString("F" + DecimalPlaces) +
                        " - " + (Max / divisor).ToString("F" + DecimalPlaces);
                }
                if (Kind == OptionKind.VolumeSlider ||
                    Kind == OptionKind.PercentFloat)
                    return Min + "% - " + Max + "%";
                return Min + " - " + Max;
            }
        }

        /// <summary>
        /// Applies a live snapshot value without treating it as a user edit.
        /// </summary>
        public void SetLive(int live)
        {
            liveValue = live;
            bool changed = value != live;
            value = live;
            if (changed) OnPropertyChanged(nameof(Value));
            OnPropertyChanged(nameof(ToggleValue));
            OnPropertyChanged(nameof(DisplayValue));
            OnPropertyChanged(nameof(ValueText));
            OnPropertyChanged(nameof(IsDirty));
        }

        /// <summary>
        /// Marks the current value as the new live baseline after a successful
        /// write.
        /// </summary>
        public void MarkClean()
        {
            liveValue = value;
            OnPropertyChanged(nameof(IsDirty));
        }
    }

    public sealed class GameOptionSection
    {
        public string Title { get; set; } = "";
        public ObservableCollection<GameOptionItem> Items { get; private set; } =
            new ObservableCollection<GameOptionItem>();
    }

    /// <summary>
    /// One sound channel: its on/off toggle and its volume slider, shown on a
    /// single row (checkbox | slider | value | range).
    /// </summary>
    public sealed class SoundChannelItem
    {
        public SoundChannelItem(string label, GameOptionItem toggle,
            GameOptionItem level)
        {
            Label = label ?? "";
            Toggle = toggle;
            Level = level;
        }

        public string Label { get; private set; }
        public GameOptionItem Toggle { get; private set; }
        public GameOptionItem Level { get; private set; }
    }
}
