namespace AA2Trainer.WpfPrototype
{
    /// <summary>
    /// Read-only display data for the selected character. Values are formatted
    /// from the Core CharacterSnapshot; names and profile text are passed
    /// through verbatim.
    /// </summary>
    public sealed class CharacterDetail
    {
        public string Profile { get; set; } = "";
        public string Personality { get; set; } = "";
        public string Club { get; set; } = "";
        public string Social { get; set; } = "";
        public string FightingStyle { get; set; } = "";
        public string Virtue { get; set; } = "";
        public string Orientation { get; set; } = "";
        public string VoicePitch { get; set; } = "";
        public string Intelligence { get; set; } = "";
        public string Strength { get; set; } = "";
        public string ClubStats { get; set; } = "";
        public string Record { get; set; } = "";
        public string Experience { get; set; } = "";
    }
}
