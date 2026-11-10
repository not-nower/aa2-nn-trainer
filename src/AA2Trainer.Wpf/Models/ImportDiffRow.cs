namespace AA2Trainer.WpfPrototype
{
    public sealed class ImportDiffRow
    {
        public string Section { get; set; }
        public string Field { get; set; }
        public string Current { get; set; }
        public string Incoming { get; set; }
        public bool Changed { get; set; }
        public bool AppliesHere { get; set; }
    }
}
