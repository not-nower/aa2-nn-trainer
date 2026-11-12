using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace AA2Trainer.WpfPrototype
{
    /// <summary>
    /// Four outfit sets share one value array. Each set gets its own FieldDraft
    /// so the tab can show a compact group list, while the wrapper merges the
    /// values for the single OutfitEditor write.
    /// </summary>
    public sealed class OutfitDraft : ObservableObject
    {
        public OutfitDraft(CharacterOptions options, bool female)
        {
            Func<string, List<CharacterOption>> selector = name =>
            {
                List<CharacterOption> list;
                return options != null &&
                    options.OutfitChoices.TryGetValue(name, out list)
                    ? list : null;
            };

            School = new FieldDraft(AppearanceFields.Outfits(0), selector);
            Sport = new FieldDraft(AppearanceFields.Outfits(1), selector);
            Swim = new FieldDraft(AppearanceFields.Outfits(2), selector);
            Club = new FieldDraft(AppearanceFields.Outfits(3), selector);

            if (!female)
            {
                foreach (FieldDraft draft in AllDrafts())
                {
                    foreach (FieldItem item in draft.AllItems())
                    {
                        if (IsFemaleOnly(item.Name))
                            item.Visible = false;
                    }
                }
            }

            foreach (FieldDraft draft in AllDrafts())
                draft.PropertyChanged += Draft_PropertyChanged;
        }

        private static bool IsFemaleOnly(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return name.EndsWith("SkirtLen", StringComparison.Ordinal) ||
                name.EndsWith("Onepiece", StringComparison.Ordinal) ||
                name.EndsWith("Skirt", StringComparison.Ordinal) ||
                name.EndsWith("SkirtT", StringComparison.Ordinal) ||
                name.EndsWith("SkirtH", StringComparison.Ordinal) ||
                name.EndsWith("SkirtL", StringComparison.Ordinal) ||
                name.EndsWith("SkirtShdH", StringComparison.Ordinal) ||
                name.EndsWith("SkirtShdL", StringComparison.Ordinal);
        }

        public FieldDraft School { get; private set; }
        public FieldDraft Sport { get; private set; }
        public FieldDraft Swim { get; private set; }
        public FieldDraft Club { get; private set; }

        public uint Pointer { get { return School.Pointer; } }
        public int SeatIndex { get { return School.SeatIndex; } }
        public int[] OriginalValues { get { return School.OriginalValues; } }

        public bool IsDirty
        {
            get
            {
                foreach (FieldDraft draft in AllDrafts())
                    if (draft.IsDirty) return true;
                return false;
            }
        }

        public void Load(int[] values, uint pointer, int seatIndex)
        {
            foreach (FieldDraft draft in AllDrafts())
                draft.Load(values, null, pointer, seatIndex);
            OnPropertyChanged(nameof(IsDirty));
        }

        public int[] ToValues()
        {
            int[] values = new int[OutfitEditor.Fields.Count];
            foreach (FieldDraft draft in AllDrafts())
            {
                foreach (FieldItem item in draft.AllItems())
                {
                    if (item.Index >= 0 && item.Index < values.Length)
                        values[item.Index] = item.Value;
                }
            }
            return values;
        }

        public void MarkSaved()
        {
            foreach (FieldDraft draft in AllDrafts())
                draft.MarkSaved();
            OnPropertyChanged(nameof(IsDirty));
        }

        private IEnumerable<FieldDraft> AllDrafts()
        {
            yield return School;
            yield return Sport;
            yield return Swim;
            yield return Club;
        }

        private void Draft_PropertyChanged(object sender,
            PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FieldDraft.IsDirty))
                OnPropertyChanged(nameof(IsDirty));
        }
    }
}
