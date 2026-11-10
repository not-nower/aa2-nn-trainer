namespace AA2Trainer.WpfPrototype
{
    public sealed class PlayerSwitchResult
    {
        public uint Pointer;
        public int Seat = -1;
        public bool Success;
        public string Error = "";
    }

    public sealed class RosterActionResult
    {
        public bool Success;
        public string Message = "";
        public string Error = "";
    }
}
