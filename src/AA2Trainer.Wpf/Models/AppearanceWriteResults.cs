namespace AA2Trainer.WpfPrototype
{
    public sealed class FaceWriteResult
    {
        public uint Pointer;
        public int Seat = -1;
        public bool Success;
        public string Changed = "";
        public string Error = "";
    }

    public sealed class HairWriteResult
    {
        public uint Pointer;
        public int Seat = -1;
        public bool Success;
        public string Changed = "";
        public string Error = "";
    }

    public sealed class OutfitWriteResult
    {
        public uint Pointer;
        public int Seat = -1;
        public bool Success;
        public string Changed = "";
        public string Error = "";
    }
}
