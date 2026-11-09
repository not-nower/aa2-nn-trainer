namespace AA2Trainer.WpfPrototype
{
    public sealed class LogLine
    {
        public LogLine(string time, string text, string level = "Info")
        {
            Time = time ?? "";
            Text = text ?? "";
            Level = level ?? "Info";
        }

        public string Time { get; private set; }
        public string Text { get; private set; }
        public string Level { get; private set; }
    }
}
