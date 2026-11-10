namespace AA2Trainer.WpfPrototype
{
    public sealed class TraitToggle : ObservableObject
    {
        private bool isOn;

        public TraitToggle(int index, string display, string effect)
        {
            Index = index;
            Display = display ?? "";
            Effect = effect ?? "";
        }

        public int Index { get; private set; }
        public string Display { get; private set; }
        public string Effect { get; private set; }

        public string Tip
        {
            get { return string.IsNullOrEmpty(Effect) ? Display : Effect; }
        }

        public bool IsOn
        {
            get { return isOn; }
            set { Set(ref isOn, value); }
        }
    }

    public sealed class RiskRow : ObservableObject
    {
        private int week1;
        private int week2;

        public RiskRow(string dayName)
        {
            DayName = dayName ?? "";
        }

        public string DayName { get; private set; }

        public int Week1
        {
            get { return week1; }
            set { Set(ref week1, value); }
        }

        public int Week2
        {
            get { return week2; }
            set { Set(ref week2, value); }
        }
    }
}
