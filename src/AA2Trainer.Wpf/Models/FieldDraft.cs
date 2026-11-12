using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows.Media;

namespace AA2Trainer.WpfPrototype
{
    public enum FieldKind
    {
        Number,
        Choice,
        Color,
        Toggle,
        Text,
        Opacity,
        Int32
    }

    /// <summary>
    /// UI description of one editable field. The index points into the Core
    /// editor's value array; the group name becomes a section header.
    /// </summary>
    public sealed class FieldDef
    {
        public string Name;
        public string Label;
        public string Tip;
        public FieldKind Kind;
        public string Group;
        public int Index;
        public int Minimum;
        public int Maximum;
        public bool UseSlider = true;
    }

    public sealed class FieldItem : ObservableObject
    {
        private int value;
        private string text = "";
        private string colorInput;

        public FieldItem(FieldDef def, IEnumerable<CharacterOption> options)
        {
            Index = def.Index;
            Name = def.Name ?? "";
            Label = def.Label ?? "";
            Tip = def.Tip ?? "";
            Kind = def.Kind;
            Group = def.Group ?? "";
            Minimum = def.Minimum;
            Maximum = def.Maximum;
            UseSlider = def.UseSlider;
            Options = new ObservableCollection<CharacterOption>(
                options ?? Enumerable.Empty<CharacterOption>());
        }

        public int Index { get; private set; }
        public string Name { get; private set; }
        public string Label { get; private set; }
        public string Tip { get; private set; }
        public FieldKind Kind { get; private set; }
        public string Group { get; private set; }
        public int Minimum { get; private set; }
        public int Maximum { get; private set; }
        public bool UseSlider { get; private set; }
        public ObservableCollection<CharacterOption> Options { get; private set; }

        private bool visible = true;
        public bool Visible
        {
            get { return visible; }
            set { Set(ref visible, value); }
        }

        public bool IsChoice { get { return Kind == FieldKind.Choice; } }
        public bool IsNumber
        {
            get
            {
                return Kind == FieldKind.Number || Kind == FieldKind.Opacity ||
                    Kind == FieldKind.Int32;
            }
        }
        public bool IsColor { get { return Kind == FieldKind.Color; } }
        public bool IsToggle { get { return Kind == FieldKind.Toggle; } }
        public bool IsText { get { return Kind == FieldKind.Text; } }

        public int Value
        {
            get { return value; }
            set
            {
                if (!Set(ref this.value, value)) return;
                colorInput = null;
                OnPropertyChanged(nameof(ColorText));
                OnPropertyChanged(nameof(R));
                OnPropertyChanged(nameof(G));
                OnPropertyChanged(nameof(B));
                OnPropertyChanged(nameof(Swatch));
                OnPropertyChanged(nameof(ToggleValue));
            }
        }

        public bool ToggleValue
        {
            get { return value != 0; }
            set { Value = value ? 1 : 0; }
        }

        public string Text
        {
            get { return text; }
            set { Set(ref text, value ?? ""); }
        }

        public string ColorText
        {
            get
            {
                if (colorInput != null) return colorInput;
                return "#" + (value & 0xFFFFFF).ToString("X6");
            }
            set
            {
                string input = (value ?? "").Trim();
                if (input.StartsWith("#")) input = input.Substring(1);
                int rgb;
                if (input.Length == 6 && int.TryParse(input,
                    NumberStyles.HexNumber, CultureInfo.InvariantCulture,
                    out rgb))
                {
                    colorInput = null;
                    Value = unchecked((int)(0xFF000000u | (uint)rgb));
                }
                else
                {
                    // WPF re-reads the source after each keystroke, so keep
                    // partial input visible instead of snapping back.
                    colorInput = value;
                }
            }
        }

        public int R
        {
            get { return (value >> 16) & 0xFF; }
            set { SetChannel(16, value); }
        }

        public int G
        {
            get { return (value >> 8) & 0xFF; }
            set { SetChannel(8, value); }
        }

        public int B
        {
            get { return value & 0xFF; }
            set { SetChannel(0, value); }
        }

        public Brush Swatch
        {
            get
            {
                SolidColorBrush brush = new SolidColorBrush(
                    Color.FromRgb((byte)R, (byte)G, (byte)B));
                brush.Freeze();
                return brush;
            }
        }

        private void SetChannel(int shift, int channel)
        {
            int clamped = Math.Max(0, Math.Min(255, channel));
            int rgb = value & 0xFFFFFF;
            rgb = (rgb & ~(0xFF << shift)) | (clamped << shift);
            Value = unchecked((int)(0xFF000000u | (uint)rgb));
        }
    }

    public sealed class FieldGroup
    {
        public FieldGroup(string title)
        {
            Title = title ?? "";
            Items = new ObservableCollection<FieldItem>();
        }

        public string Title { get; private set; }
        public ObservableCollection<FieldItem> Items { get; private set; }
    }

    /// <summary>
    /// Editable copy of one Core editor's value/text arrays. Keeps the original
    /// snapshot for dirty tracking and the batch write.
    /// </summary>
    public sealed class FieldDraft : ObservableObject
    {
        private readonly List<FieldItem> allItems = new List<FieldItem>();
        private int[] originalValues;
        private string[] originalTexts;
        private bool suppress;

        public FieldDraft(IEnumerable<FieldDef> defs,
            Func<string, List<CharacterOption>> choiceSelector)
        {
            List<FieldGroup> groups = new List<FieldGroup>();
            Dictionary<string, FieldGroup> byName =
                new Dictionary<string, FieldGroup>(StringComparer.Ordinal);

            foreach (FieldDef def in defs)
            {
                List<CharacterOption> options = choiceSelector == null
                    ? null : choiceSelector(def.Name);
                FieldItem item = new FieldItem(def, options);
                item.PropertyChanged += Item_PropertyChanged;
                allItems.Add(item);

                FieldGroup group;
                if (!byName.TryGetValue(def.Group, out group))
                {
                    group = new FieldGroup(def.Group);
                    byName[def.Group] = group;
                    groups.Add(group);
                }
                group.Items.Add(item);
            }

            Groups = new ObservableCollection<FieldGroup>(groups);
        }

        public ObservableCollection<FieldGroup> Groups { get; private set; }
        public uint Pointer { get; private set; }
        public int SeatIndex { get; private set; } = -1;

        public FieldGroup this[string title]
        {
            get
            {
                if (string.IsNullOrEmpty(title)) return null;
                foreach (FieldGroup group in Groups)
                {
                    if (string.Equals(group.Title, title,
                        StringComparison.Ordinal))
                        return group;
                }
                return null;
            }
        }

        public int[] OriginalValues
        {
            get { return originalValues; }
        }

        public string[] OriginalTexts
        {
            get { return originalTexts; }
        }

        public bool IsDirty
        {
            get
            {
                if (originalValues == null) return false;
                foreach (FieldItem item in allItems)
                {
                    if (item.Index < 0 || item.Index >= originalValues.Length)
                        continue;
                    if (item.Value != originalValues[item.Index]) return true;
                    if (item.IsText)
                    {
                        string original = originalTexts != null &&
                            item.Index < originalTexts.Length
                            ? (originalTexts[item.Index] ?? "") : "";
                        if (!string.Equals(item.Text ?? "", original,
                            StringComparison.Ordinal))
                            return true;
                    }
                }
                return false;
            }
        }

        public void Load(int[] values, string[] texts, uint pointer, int seatIndex)
        {
            suppress = true;
            try
            {
                originalValues = values == null ? null : (int[])values.Clone();
                originalTexts = texts == null ? null : (string[])texts.Clone();
                Pointer = pointer;
                SeatIndex = seatIndex;

                foreach (FieldItem item in allItems)
                {
                    item.Value = values != null && item.Index < values.Length
                        ? values[item.Index] : 0;
                    item.Text = texts != null && item.Index < texts.Length
                        ? (texts[item.Index] ?? "") : "";
                    if (item.IsChoice &&
                        !item.Options.Any(option => option.Value == item.Value))
                    {
                        item.Options.Add(new CharacterOption
                        {
                            Value = item.Value,
                            Text = item.Value.ToString()
                        });
                    }
                }
            }
            finally
            {
                suppress = false;
            }

            OnPropertyChanged(string.Empty);
            OnPropertyChanged(nameof(IsDirty));
        }

        public int[] ToValues()
        {
            int count = ValueCount();
            int[] values = new int[count];
            foreach (FieldItem item in allItems)
            {
                if (item.Index >= 0 && item.Index < values.Length)
                    values[item.Index] = item.Value;
            }
            return values;
        }

        public string[] ToTexts()
        {
            int count = ValueCount();
            string[] texts = new string[count];
            for (int i = 0; i < texts.Length; i++) texts[i] = "";
            foreach (FieldItem item in allItems)
            {
                if (item.IsText && item.Index >= 0 && item.Index < texts.Length)
                    texts[item.Index] = item.Text ?? "";
            }
            return texts;
        }

        public void MarkSaved()
        {
            originalValues = ToValues();
            originalTexts = ToTexts();
            OnPropertyChanged(nameof(IsDirty));
        }

        public IEnumerable<FieldItem> AllItems()
        {
            return allItems;
        }

        private int ValueCount()
        {
            int count = 0;
            foreach (FieldItem item in allItems)
            {
                if (item.Index + 1 > count) count = item.Index + 1;
            }
            return count;
        }

        private void Item_PropertyChanged(object sender,
            PropertyChangedEventArgs e)
        {
            if (suppress) return;
            OnPropertyChanged(nameof(IsDirty));
        }
    }
}
