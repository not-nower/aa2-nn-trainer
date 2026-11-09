namespace AA2Trainer.WpfPrototype
{
    public sealed class PlanStepItem : ObservableObject
    {
        private bool enabled = true;

        public bool Enabled
        {
            get { return enabled; }
            set { Set(ref enabled, value); }
        }

        public int Number { get; set; }
        public string Description { get; set; } = "";
        public string State { get; set; } = "pending";
        public string Note { get; set; } = "";
    }
}
