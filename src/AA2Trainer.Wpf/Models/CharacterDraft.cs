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
        private int victoryCount;
        private int classSkip;
        private int winningOver;
        private int partner;
        private int rejected;
        private int sexIntercourse;
        private int sexPartner;
        private string firstTime = "";
        private string firstAnal = "";
        private string lastTime = "";
        private bool sexExperience;
        private bool analExperience;

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
            set
            {
                if (!SetDirty(ref profile, value)) return;
                OnPropertyChanged(nameof(ProfileByteCount));
            }
        }

        public string ProfileByteCount
        {
            get
            {
                return CharacterEditor.GetProfileByteCount(Profile) + " / " +
                    CharacterEditor.ProfileMaxBytes + " bytes";
            }
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

        public int VictoryCount
        {
            get { return victoryCount; }
            set { SetDirty(ref victoryCount, value); }
        }

        public int ClassSkip
        {
            get { return classSkip; }
            set { SetDirty(ref classSkip, value); }
        }

        public int WinningOver
        {
            get { return winningOver; }
            set { SetDirty(ref winningOver, value); }
        }

        public int Partner
        {
            get { return partner; }
            set { SetDirty(ref partner, value); }
        }

        public int Rejected
        {
            get { return rejected; }
            set { SetDirty(ref rejected, value); }
        }

        public int SexIntercourse
        {
            get { return sexIntercourse; }
            set { SetDirty(ref sexIntercourse, value); }
        }

        public int SexPartner
        {
            get { return sexPartner; }
            set { SetDirty(ref sexPartner, value); }
        }

        public string FirstTime
        {
            get { return firstTime; }
            set { SetDirty(ref firstTime, value); }
        }

        public string FirstAnal
        {
            get { return firstAnal; }
            set { SetDirty(ref firstAnal, value); }
        }

        public string LastTime
        {
            get { return lastTime; }
            set { SetDirty(ref lastTime, value); }
        }

        public bool SexExperience
        {
            get { return sexExperience; }
            set { SetDirty(ref sexExperience, value); }
        }

        public bool AnalExperience
        {
            get { return analExperience; }
            set { SetDirty(ref analExperience, value); }
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
                       ClubGrade != original.ClubGrade ||
                       VictoryCount != original.VictoryCount ||
                       ClassSkip != original.ClassSkip ||
                       WinningOver != original.WinningOver ||
                       Partner != original.Partner ||
                       Rejected != original.Rejected ||
                       SexIntercourse != original.SexIntercourse ||
                       SexPartner != original.SexPartner ||
                       !Same(FirstTime, original.FirstTime) ||
                       !Same(FirstAnal, original.FirstAnal) ||
                       !Same(LastTime, original.LastTime) ||
                       SexExperience != original.SexExperience ||
                       AnalExperience != original.AnalExperience;
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
            victoryCount = snapshot.VictoryCount;
            classSkip = snapshot.ClassSkip;
            winningOver = snapshot.WinningOver;
            partner = snapshot.Partner;
            rejected = snapshot.Rejected;
            sexIntercourse = snapshot.SexIntercourse;
            sexPartner = snapshot.SexPartner;
            firstTime = snapshot.FirstTime ?? "";
            firstAnal = snapshot.FirstAnal ?? "";
            lastTime = snapshot.LastTime ?? "";
            sexExperience = snapshot.SexExperience;
            analExperience = snapshot.AnalExperience;

            OnPropertyChanged(string.Empty);
            OnPropertyChanged(nameof(IsDirty));
        }

        public CharacterSnapshot ToSnapshot()
        {
            CharacterSnapshot snapshot = new CharacterSnapshot();
            if (original != null)
            {
                snapshot.IntelligenceTopRank = original.IntelligenceTopRank;
                snapshot.StrengthTopRank = original.StrengthTopRank;
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
            snapshot.VictoryCount = VictoryCount;
            snapshot.ClassSkip = ClassSkip;
            snapshot.WinningOver = WinningOver;
            snapshot.Partner = Partner;
            snapshot.Rejected = Rejected;
            snapshot.SexIntercourse = SexIntercourse;
            snapshot.SexPartner = SexPartner;
            snapshot.FirstTime = FirstTime ?? "";
            snapshot.FirstAnal = FirstAnal ?? "";
            snapshot.LastTime = LastTime ?? "";
            snapshot.SexExperience = SexExperience;
            snapshot.AnalExperience = AnalExperience;
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
