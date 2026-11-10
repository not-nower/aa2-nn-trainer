using System.Collections.Generic;

namespace AA2Trainer.WpfPrototype
{
    public sealed class SeatSnapshot
    {
        public int Seat;
        public string Name = "";
        public string FamilyName = "";
        public string GivenName = "";
        public bool IsPlayer;
        public string Room = "";
        public string Action = "None";
        public string Status = "Empty";
        public string Relationship = "";
        public string HeaderRelationship = "";
        public bool IsBusy;
    }

    public sealed class SessionSnapshot
    {
        public bool IsAttached;
        public string AttachState = "Not attached";
        public string GameTime = "Game time: -";
        public string PointerHealth = "Pointers: not checked";
        public string NamesState = "Names: not loaded";
        public string WriteGate = "Write gate: read-only";
        public string UpdatedText = "Updated -";
        public int SelectedSeat = -1;
        public uint SelectedPointer;
        public long Sequence;
        public CharacterDetail Character;
        public CharacterSnapshot RawCharacter;
        public CharacterOptions Options;
        public bool[] TraitValues;
        public int[,] RiskValues;
        public int[] BodyValues;
        public int SelectedGender;
        public int[] FaceValues;
        public string[] FaceTexts;
        public int[] HairValues;
        public int[] OutfitValues;

        public readonly List<SeatSnapshot> Seats = new List<SeatSnapshot>();
    }
}
