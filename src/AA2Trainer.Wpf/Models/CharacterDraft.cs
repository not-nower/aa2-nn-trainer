using System;
using System.Runtime.CompilerServices;

namespace AA2Trainer.WpfPrototype
{
    /// <summary>
    /// Editable copy of the selected character. The live snapshot remains the
    /// original; Apply writes this draft, Revert reloads it.
    /// </summary>
    public sealed class CharacterDraft : ObservableObject
    {
        private CharacterSnapshot original;
        private string firstName = "";
        private string lastName = "";
        private string profile = "";
        private int personality;
        private int club;
        private int social;
        private int fightingStyle;
        private int virtue;
        private int orientation;
        private int voicePitch;
        private int intelligenceLevel;
        private int intelligenceRank;
        private int intelligenceValue;
        private int intelligenceGrade;
        private int strengthLevel;
        private int strengthRank;
        private int strengthValue;
        private int strengthGrade;
        private int clubLevel;
        private int clubValue;
        private int clubGrade;

        public uint Pointer { get; private set; }
        public int SeatIndex { get; private set; } = -1;

        public CharacterSnapshot Original
        {
            get { return original; }
        }

        public string FirstName
        {
            get { return firstName; }
            set { SetDirty(ref firstName, value); }
        }

        public string LastName
        {
            get { return lastName; }
            set { SetDirty(ref lastName, value); }
        }

        public string Profile
        {
            get { return profile; }
            set { SetDirty(ref profile, value); }
        }

        public int Personality
        {
            get { return personality; }
            set { SetDirty(ref personality, value); }
        }

        public int Club
        {
            get { return club; }
            set { SetDirty(ref club, value); }
        }

        public int Social
        {
            get { return social; }
            set { SetDirty(ref social, value); }
        }

        public int FightingStyle
        {
            get { return fightingStyle; }
            set { SetDirty(ref fightingStyle, value); }
        }

        public int Virtue
        {
            get { return virtue; }
            set { SetDirty(ref virtue, value); }
        }

        public int Orientation
        {
            get { return orientation; }
            set { SetDirty(ref orientation, value); }
        }

        public int VoicePitch
        {
            get { return voicePitch; }
            set { SetDirty(ref voicePitch, value); }
        }

        public int IntelligenceLevel
        {
            get { return intelligenceLevel; }
            set { SetDirty(ref intelligenceLevel, value); }
        }

        public int IntelligenceRank
        {
            get { return intelligenceRank; }
            set { SetDirty(ref intelligenceRank, value); }
        }

        public int IntelligenceValue
        {
            get { return intelligenceValue; }
            set { SetDirty(ref intelligenceValue, value); }
        }

        public int IntelligenceGrade
        {
            get { return intelligenceGrade; }
            set { SetDirty(ref intelligenceGrade, value); }
        }

        public int StrengthLevel
        {
            get { return strengthLevel; }
            set { SetDirty(ref strengthLevel, value); }
        }

        public int StrengthRank
        {
            get { return strengthRank; }
            set { SetDirty(ref strengthRank, value); }
        }

        public int StrengthValue
        {
            get { return strengthValue; }
            set { SetDirty(ref strengthValue, value); }
        }

        public int StrengthGrade
        {
            get { return strengthGrade; }
            set { SetDirty(ref strengthGrade, value); }
        }

        public int ClubLevel
        {
            get { return clubLevel; }
            set { SetDirty(ref clubLevel, value); }
        }

        public int ClubValue
        {
            get { return clubValue; }
            set { SetDirty(ref clubValue, value); }
        }

        public int ClubGrade
        {
            get { return clubGrade; }
            set { SetDirty(ref clubGrade, value); }
        }

        public bool IsDirty
        {
            get
            {
                if (original == null) return false;
                return !Same(FirstName, original.LastName) ||
                       !Same(LastName, original.FirstName) ||
                       !Same(Profile, original.Profile) ||
                       Personality != original.Personality ||
                       Club != original.Club ||
                       Social != original.Social ||
                       FightingStyle != original.FightingStyle ||
                       Virtue != original.Virtue ||
                       Orientation != original.SexualOrientation ||
                       VoicePitch != original.VoicePitch ||
                       IntelligenceLevel != original.IntelligenceLevel ||
                       IntelligenceRank != original.IntelligenceRank ||
                       IntelligenceValue != original.IntelligenceValue ||
                       IntelligenceGrade != original.IntelligenceGrade ||
                       StrengthLevel != original.StrengthLevel ||
                       StrengthRank != original.StrengthRank ||
                       StrengthValue != original.StrengthValue ||
                       StrengthGrade != original.StrengthGrade ||
                       ClubLevel != original.ClubLevel ||
                       ClubValue != original.ClubValue ||
                       ClubGrade != original.ClubGrade;
            }
        }

        public void Load(CharacterSnapshot snapshot, uint pointer, int seatIndex)
        {
            original = snapshot;
            Pointer = pointer;
            SeatIndex = seatIndex;

            firstName = snapshot.LastName ?? "";
            lastName = snapshot.FirstName ?? "";
            profile = snapshot.Profile ?? "";
            personality = snapshot.Personality;
            club = snapshot.Club;
            social = snapshot.Social;
            fightingStyle = snapshot.FightingStyle;
            virtue = snapshot.Virtue;
            orientation = snapshot.SexualOrientation;
            voicePitch = snapshot.VoicePitch;
            intelligenceLevel = snapshot.IntelligenceLevel;
            intelligenceRank = snapshot.IntelligenceRank;
            intelligenceValue = snapshot.IntelligenceValue;
            intelligenceGrade = snapshot.IntelligenceGrade;
            strengthLevel = snapshot.StrengthLevel;
            strengthRank = snapshot.StrengthRank;
            strengthValue = snapshot.StrengthValue;
            strengthGrade = snapshot.StrengthGrade;
            clubLevel = snapshot.ClubLevel;
            clubValue = snapshot.ClubValue;
            clubGrade = snapshot.ClubGrade;

            OnPropertyChanged(string.Empty);
            OnPropertyChanged(nameof(IsDirty));
        }

        public CharacterSnapshot ToSnapshot()
        {
            CharacterSnapshot snapshot = new CharacterSnapshot();
            if (original != null)
            {
                snapshot.FirstTime = original.FirstTime;
                snapshot.FirstAnal = original.FirstAnal;
                snapshot.LastTime = original.LastTime;
                snapshot.IntelligenceTopRank = original.IntelligenceTopRank;
                snapshot.StrengthTopRank = original.StrengthTopRank;
                snapshot.VictoryCount = original.VictoryCount;
                snapshot.ClassSkip = original.ClassSkip;
                snapshot.WinningOver = original.WinningOver;
                snapshot.Partner = original.Partner;
                snapshot.Rejected = original.Rejected;
                snapshot.SexIntercourse = original.SexIntercourse;
                snapshot.SexPartner = original.SexPartner;
                snapshot.SexExperience = original.SexExperience;
                snapshot.AnalExperience = original.AnalExperience;
            }

            snapshot.FirstName = LastName ?? "";
            snapshot.LastName = FirstName ?? "";
            snapshot.Profile = Profile ?? "";
            snapshot.Personality = Personality;
            snapshot.Club = Club;
            snapshot.Social = Social;
            snapshot.FightingStyle = FightingStyle;
            snapshot.Virtue = Virtue;
            snapshot.SexualOrientation = Orientation;
            snapshot.VoicePitch = VoicePitch;
            snapshot.IntelligenceLevel = IntelligenceLevel;
            snapshot.IntelligenceRank = IntelligenceRank;
            snapshot.IntelligenceValue = IntelligenceValue;
            snapshot.IntelligenceGrade = IntelligenceGrade;
            snapshot.StrengthLevel = StrengthLevel;
            snapshot.StrengthRank = StrengthRank;
            snapshot.StrengthValue = StrengthValue;
            snapshot.StrengthGrade = StrengthGrade;
            snapshot.ClubLevel = ClubLevel;
            snapshot.ClubValue = ClubValue;
            snapshot.ClubGrade = ClubGrade;
            return snapshot;
        }

        public void MarkSaved()
        {
            original = ToSnapshot();
            OnPropertyChanged(nameof(IsDirty));
        }

        private bool SetDirty<T>(ref T field, T value,
            [CallerMemberName] string name = null)
        {
            if (!Set(ref field, value, name)) return false;
            OnPropertyChanged(nameof(IsDirty));
            return true;
        }

        private static bool Same(string left, string right)
        {
            return string.Equals((left ?? "").Trim(), (right ?? "").Trim(),
                StringComparison.Ordinal);
        }
    }
}
