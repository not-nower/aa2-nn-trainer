using System.Collections.Generic;

namespace AA2Trainer.WpfPrototype
{
    public sealed class CharacterOption
    {
        public int Value { get; set; }
        public string Text { get; set; }

        public override string ToString()
        {
            return Text;
        }
    }

    public sealed class CharacterOptions
    {
        public List<CharacterOption> Personality { get; set; } = new List<CharacterOption>();
        public List<CharacterOption> Club { get; set; } = new List<CharacterOption>();
        public List<CharacterOption> Social { get; set; } = new List<CharacterOption>();
        public List<CharacterOption> FightingStyle { get; set; } = new List<CharacterOption>();
        public List<CharacterOption> Virtue { get; set; } = new List<CharacterOption>();
        public List<CharacterOption> Orientation { get; set; } = new List<CharacterOption>();
        public List<CharacterOption> VoicePitch { get; set; } = new List<CharacterOption>();
        public List<CharacterOption> Levels { get; set; } = new List<CharacterOption>();
        public List<CharacterOption> ClubLevels { get; set; } = new List<CharacterOption>();
        public List<CharacterOption> Grades { get; set; } = new List<CharacterOption>();
        public Dictionary<string, List<CharacterOption>> BodyChoices { get; set; } =
            new Dictionary<string, List<CharacterOption>>();
        public Dictionary<string, List<CharacterOption>> FaceChoices { get; set; } =
            new Dictionary<string, List<CharacterOption>>();
        public Dictionary<string, List<CharacterOption>> HairChoices { get; set; } =
            new Dictionary<string, List<CharacterOption>>();
        public Dictionary<string, List<CharacterOption>> OutfitChoices { get; set; } =
            new Dictionary<string, List<CharacterOption>>();
    }
}
