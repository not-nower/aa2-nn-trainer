using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows.Media;

namespace AA2Trainer.WpfPrototype
{
    public sealed class BodyFieldItem : ObservableObject
    {
        private int value;
        private string colorInput;

        public BodyFieldItem(int index, BodyFieldDef def,
            IEnumerable<CharacterOption> options)
        {
            Index = index;
            Label = def.Label ?? "";
            Tip = def.Tip ?? "";
            Kind = def.Kind;
            Options = new ObservableCollection<CharacterOption>(
                options ?? Enumerable.Empty<CharacterOption>());
        }

        public int Index { get; private set; }
        public string Label { get; private set; }
        public string Tip { get; private set; }
        public BodyFieldKind Kind { get; private set; }
        public ObservableCollection<CharacterOption> Options { get; private set; }

        public bool IsChoice { get { return Kind == BodyFieldKind.Choice; } }
        public bool IsNumber { get { return Kind == BodyFieldKind.Number; } }
        public bool IsColor { get { return Kind == BodyFieldKind.Color; } }

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
            }
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
                string text = (value ?? "").Trim();
                if (text.StartsWith("#")) text = text.Substring(1);
                int rgb;
                if (text.Length == 6 && int.TryParse(text,
                    NumberStyles.HexNumber, CultureInfo.InvariantCulture,
                    out rgb))
                {
                    colorInput = null;
                    Value = unchecked((int)(0xFF000000u | (uint)rgb));
                }
                else
                {
                    // Keep the partial input visible while the user is typing.
                    // WPF re-reads the source after each keystroke, so returning
                    // the formatted value here would erase the edit.
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
            OnPropertyChanged(nameof(R));
            OnPropertyChanged(nameof(G));
            OnPropertyChanged(nameof(B));
        }
    }

    /// <summary>
    /// Editable copy of the selected character's body fields. The original
    /// values are kept for dirty tracking and the batch write.
    /// </summary>
    public sealed class BodyDraft : ObservableObject
    {
        private int[] originalValues;
        private bool suppress;

        public BodyDraft(CharacterOptions options)
        {
            BodyFields = new ObservableCollection<BodyFieldItem>();
            BreastFields = new ObservableCollection<BodyFieldItem>();
            SkinFields = new ObservableCollection<BodyFieldItem>();

            IList<BodyFieldDef> defs = BodyEditor.Fields;
            Dictionary<string, List<CharacterOption>> choices =
                options == null ? null : options.BodyChoices;
            for (int i = 0; i < defs.Count; i++)
            {
                BodyFieldDef def = defs[i];
                List<CharacterOption> fieldOptions = null;
                if (choices != null && !string.IsNullOrEmpty(def.Name))
                    choices.TryGetValue(def.Name, out fieldOptions);

                BodyFieldItem item = new BodyFieldItem(i, def, fieldOptions);
                item.PropertyChanged += Item_PropertyChanged;
                if (def.Group == BodyGroup.Body)
                    BodyFields.Add(item);
                else if (def.Group == BodyGroup.Breasts)
                    BreastFields.Add(item);
                else
                    SkinFields.Add(item);
            }
        }

        public ObservableCollection<BodyFieldItem> BodyFields { get; private set; }
        public ObservableCollection<BodyFieldItem> BreastFields { get; private set; }
        public ObservableCollection<BodyFieldItem> SkinFields { get; private set; }

        public uint Pointer { get; private set; }
        public int SeatIndex { get; private set; } = -1;

        public int[] OriginalValues
        {
            get { return originalValues; }
        }

        public bool IsDirty
        {
            get
            {
                if (originalValues == null) return false;
                foreach (BodyFieldItem item in AllItems())
                    if (item.Value != originalValues[item.Index]) return true;
                return false;
            }
        }

        public void Load(int[] values, uint pointer, int seatIndex)
        {
            suppress = true;
            try
            {
                originalValues = values == null ? null : (int[])values.Clone();
                Pointer = pointer;
                SeatIndex = seatIndex;

                foreach (BodyFieldItem item in AllItems())
                {
                    item.Value = values != null && item.Index < values.Length
                        ? values[item.Index] : 0;
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
            int[] values = new int[BodyEditor.Fields.Count];
            foreach (BodyFieldItem item in AllItems())
            {
                if (item.Index >= 0 && item.Index < values.Length)
                    values[item.Index] = item.Value;
            }
            return values;
        }

        public void MarkSaved()
        {
            originalValues = ToValues();
            OnPropertyChanged(nameof(IsDirty));
        }

        private IEnumerable<BodyFieldItem> AllItems()
        {
            foreach (BodyFieldItem item in BodyFields) yield return item;
            foreach (BodyFieldItem item in BreastFields) yield return item;
            foreach (BodyFieldItem item in SkinFields) yield return item;
        }

        private void Item_PropertyChanged(object sender,
            PropertyChangedEventArgs e)
        {
            if (suppress) return;
            OnPropertyChanged(nameof(IsDirty));
        }
    }
}
