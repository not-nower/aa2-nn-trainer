namespace AA2Trainer.WpfPrototype
{
    public sealed class CharacterWriteResult
    {
        public bool Success;
        public string Error = "";
        public string Changed = "";
        public uint Pointer;
        public int Seat = -1;
    }
}
