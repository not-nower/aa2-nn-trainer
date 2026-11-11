using System.Collections.Generic;
using System.Windows.Media.Imaging;

namespace AA2Trainer.WpfPrototype
{
    public sealed class SeatSnapshot
    {
        public int Seat;
        public BitmapImage Thumbnail;
        public BitmapImage FullPortrait;
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
        public bool HasRelationship;
        public bool IsLover;
        public bool OtherIsLover;
        public bool IsFrozen;
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
        public OptionSnapshot GameOptions;
        public string GameOptionsError = "";
        public bool[] TraitValues;
        public int[,] RiskValues;
        public int[] BodyValues;
        public int SelectedGender;
        public int[] FaceValues;
        public string[] FaceTexts;
        public int[] HairValues;
        public int[] OutfitValues;
        public ScheduleSnapshot Schedule;
        public bool TimeFrozen;
        public int TimeFrozenMilliseconds;
        public List<RelationshipRecord> RelationshipRows;
        public List<RelationshipHistoryRow> BehaviorRows;
        public List<LoverEntry> LoverRows;
        public List<PromiseEntry> PromiseRows;
        public List<CharacterInfo> RosterCharacters;
        public List<DebugFieldItem> DebugFields;

        public readonly List<SeatSnapshot> Seats = new List<SeatSnapshot>();
    }
}
