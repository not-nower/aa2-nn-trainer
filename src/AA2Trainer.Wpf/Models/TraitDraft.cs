using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace AA2Trainer.WpfPrototype
{
    /// <summary>
    /// Editable copy of the selected character's traits and two-week risk
    /// schedule. The original arrays are kept for dirty tracking and writing.
    /// </summary>
    public sealed class TraitDraft : ObservableObject
    {
        private bool[] originalTraits;
        private int[,] originalRisk;
        private bool suppress;

        public TraitDraft()
        {
            TraitItems = new ObservableCollection<TraitToggle>();
            ExtraItems = new ObservableCollection<TraitToggle>();
            PreferenceItems = new ObservableCollection<TraitToggle>();
            RiskRows = new ObservableCollection<RiskRow>();
            RiskOptions = new List<CharacterOption>();

            IList<TraitDef> traits = TraitCatalog.All;
            for (int i = 0; i < traits.Count; i++)
            {
                TraitDef trait = traits[i];
                TraitToggle item = new TraitToggle(i, trait.Display, trait.Effect);
                item.PropertyChanged += Item_PropertyChanged;

                if (trait.Offset == TraitCatalog.RainbowCardOffset)
                    ExtraItems.Add(item);
                else if (trait.Offset >= TraitCatalog.PreferenceOffset)
                    PreferenceItems.Add(item);
                else
                    TraitItems.Add(item);
            }

            for (int day = 0; day < RiskSchedule.DayCount; day++)
            {
                RiskRow row = new RiskRow(RiskSchedule.Days[day]);
                row.PropertyChanged += Item_PropertyChanged;
                RiskRows.Add(row);
            }

            for (int level = 0; level <= RiskSchedule.MaxLevel; level++)
            {
                RiskOptions.Add(new CharacterOption
                {
                    Value = level,
                    Text = RiskSchedule.LevelName(level)
                });
            }
        }

        public ObservableCollection<TraitToggle> TraitItems { get; private set; }
        public ObservableCollection<TraitToggle> ExtraItems { get; private set; }
        public ObservableCollection<TraitToggle> PreferenceItems { get; private set; }
        public ObservableCollection<RiskRow> RiskRows { get; private set; }
        public List<CharacterOption> RiskOptions { get; private set; }

        public uint Pointer { get; private set; }
        public int SeatIndex { get; private set; } = -1;

        public bool[] OriginalTraits
        {
            get { return originalTraits; }
        }

        public int[,] OriginalRisk
        {
            get { return originalRisk; }
        }

        public bool IsDirty
        {
            get
            {
                if (originalTraits == null || originalRisk == null) return false;

                foreach (TraitToggle item in TraitItems)
                    if (item.IsOn != originalTraits[item.Index]) return true;
                foreach (TraitToggle item in ExtraItems)
                    if (item.IsOn != originalTraits[item.Index]) return true;
                foreach (TraitToggle item in PreferenceItems)
                    if (item.IsOn != originalTraits[item.Index]) return true;

                for (int day = 0; day < RiskRows.Count; day++)
                {
                    if (RiskRows[day].Week1 != originalRisk[0, day]) return true;
                    if (RiskRows[day].Week2 != originalRisk[1, day]) return true;
                }
                return false;
            }
        }

        public void Load(bool[] traits, int[,] risk, uint pointer, int seatIndex)
        {
            suppress = true;
            try
            {
                originalTraits = traits == null
                    ? null : (bool[])traits.Clone();
                originalRisk = risk == null
                    ? null : (int[,])risk.Clone();
                Pointer = pointer;
                SeatIndex = seatIndex;

                foreach (TraitToggle item in TraitItems)
                    item.IsOn = traits != null && item.Index < traits.Length &&
                        traits[item.Index];
                foreach (TraitToggle item in ExtraItems)
                    item.IsOn = traits != null && item.Index < traits.Length &&
                        traits[item.Index];
                foreach (TraitToggle item in PreferenceItems)
                    item.IsOn = traits != null && item.Index < traits.Length &&
                        traits[item.Index];

                for (int day = 0; day < RiskRows.Count; day++)
                {
                    RiskRows[day].Week1 = risk != null ? risk[0, day] : 0;
                    RiskRows[day].Week2 = risk != null ? risk[1, day] : 0;
                }
            }
            finally
            {
                suppress = false;
            }

            OnPropertyChanged(string.Empty);
            OnPropertyChanged(nameof(IsDirty));
        }

        public bool[] ToTraitValues()
        {
            int count = TraitCatalog.All.Count;
            bool[] values = new bool[count];
            foreach (TraitToggle item in TraitItems)
                if (item.Index < count) values[item.Index] = item.IsOn;
            foreach (TraitToggle item in ExtraItems)
                if (item.Index < count) values[item.Index] = item.IsOn;
            foreach (TraitToggle item in PreferenceItems)
                if (item.Index < count) values[item.Index] = item.IsOn;
            return values;
        }

        public int[,] ToRiskValues()
        {
            int[,] values = new int[RiskSchedule.WeekCount, RiskSchedule.DayCount];
            for (int day = 0; day < RiskRows.Count && day < RiskSchedule.DayCount; day++)
            {
                values[0, day] = RiskRows[day].Week1;
                values[1, day] = RiskRows[day].Week2;
            }
            return values;
        }

        public void MarkSaved()
        {
            originalTraits = ToTraitValues();
            originalRisk = ToRiskValues();
            OnPropertyChanged(nameof(IsDirty));
        }

        private void Item_PropertyChanged(object sender,
            PropertyChangedEventArgs e)
        {
            if (suppress) return;
            OnPropertyChanged(nameof(IsDirty));
        }
    }
}
