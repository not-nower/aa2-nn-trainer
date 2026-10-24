using System;
using System.Collections.Generic;
using System.Text;

namespace AA2Trainer
{
    public sealed class CharacterSnapshot
    {
        public string FirstName = "";
        public string LastName = "";
        public string Profile = "";
        public string FirstTime = "";
        public string FirstAnal = "";
        public string LastTime = "";
        public int Personality;
        public int Club;
        public int Social;
        public int FightingStyle;
        public int Virtue;
        public int SexualOrientation;
        public int VoicePitch;
        public int IntelligenceLevel;
        public int IntelligenceRank;
        public int IntelligenceValue;
        public int IntelligenceGrade;
        public int IntelligenceTopRank;
        public int StrengthLevel;
        public int StrengthRank;
        public int StrengthValue;
        public int StrengthGrade;
        public int StrengthTopRank;
        public int ClubLevel;
        public int ClubValue;
        public int ClubGrade;
        public int VictoryCount;
        public int ClassSkip;
        public int WinningOver;
        public int Partner;
        public int Rejected;
        public int SexIntercourse;
        public int SexPartner;
        public bool SexExperience;
        public bool AnalExperience;
    }

    // Live character data fields. The game decodes its save-file strings in
    // memory, so these fields are plain Shift-JIS here (the save editor sees
    // the same fields XOR-encoded).
    public static class CharacterEditor
    {
        public const int FirstNameOffset = 0x3D;
        public const int LastNameOffset = 0x141;
        public const int NameFieldSize = 260;
        public const int NameMaxBytes = NameFieldSize - 1;

        public const int ProfileOffset = 0x245;
        public const int ProfileFieldSize = 512;
        public const int ProfileMaxBytes = ProfileFieldSize - 1;

        public const int PersonalityOffset = 0x445;
        public const int ClubOffset = 0x446;
        public const int SocialOffset = 0x6C4;
        public const int FightingStyleOffset = 0x6C5;
        public const int VirtueOffset = 0x6C6;
        public const int SexualOrientationOffset = 0x6C7;
        public const int VoicePitchOffset = 0x6CA;
        public const int IntelligenceLevelOffset = 0x6AC;
        public const int IntelligenceRankOffset = 0x6AD;
        public const int IntelligenceValueOffset = 0x6B0;
        public const int IntelligenceGradeStatusOffset = 0x1F0;
        public const int IntelligenceTopRankStatusOffset = 0x1FC;
        public const int StrengthLevelOffset = 0x6B4;
        public const int StrengthRankOffset = 0x6B5;
        public const int StrengthValueOffset = 0x6B8;
        public const int StrengthGradeStatusOffset = 0x1F4;
        public const int StrengthTopRankStatusOffset = 0x200;
        public const int ClubLevelOffset = 0x6BC;
        public const int ClubValueOffset = 0x6C0;
        public const int ClubGradeStatusOffset = 0x1F8;
        public const int VictoryStatusOffset = 0x1DC;
        public const int ClassSkipStatusOffset = 0x1E0;
        public const int WinningOverStatusOffset = 0x1E4;
        public const int PartnerStatusOffset = 0x1E8;
        public const int RejectedStatusOffset = 0x1EC;
        public const int SexIntercourseStatusOffset = 0x204;
        public const int SexPartnerStatusOffset = 0x208;
        public const int SexExperienceOffset = 0x6C8;
        public const int AnalExperienceOffset = 0x6C9;
        public const int FirstTimeStatusOffset = 0x20C;
        public const int FirstAnalStatusOffset = 0x22C;
        public const int LastTimeStatusOffset = 0x24C;
        public const int HHistoryFieldSize = 32;
        public const int HHistoryMaxBytes = HHistoryFieldSize - 1;
        public const int MaxPersonality = 255;
        public const int MaxClub = 7;
        public const int MaxAttributeValue = 255;
        public const int MaxIntelligenceLevel = 5;
        public const int MaxIntelligenceRank = 8;
        public const int MaxIntelligenceValue = 1000;
        public const int MaxIntelligenceTopRank = 255;
        public const int MaxStrengthLevel = 5;
        public const int MaxStrengthRank = 8;
        public const int MaxStrengthValue = 1000;
        public const int MaxStrengthTopRank = 255;
        public const int MaxClubLevel = 8;
        public const int MaxClubValue = 1000;

        private static readonly Encoding TextEncoding = GetEncoding();

        public static bool TryRead(GameMemory mem, CharacterInfo info,
            out CharacterSnapshot snapshot, out string error)
        {
            snapshot = null;
            error = null;

            if (mem == null || !mem.IsAttached)
            {
                error = "The game is not attached.";
                return false;
            }
            if (info == null || info.Pointer == 0)
            {
                error = "No character is selected.";
                return false;
            }

            uint characterData;
            if (!TryGetCharacterData(mem, info, out characterData, out error))
                return false;

            string first = ReadText(mem, characterData + FirstNameOffset, NameFieldSize);
            string last = ReadText(mem, characterData + LastNameOffset, NameFieldSize);
            string profile = ReadText(mem, characterData + ProfileOffset, ProfileFieldSize);
            byte? personality = mem.ReadU8(characterData + PersonalityOffset);
            byte? club = mem.ReadU8(characterData + ClubOffset);
            byte? social = mem.ReadU8(characterData + SocialOffset);
            byte? fightingStyle = mem.ReadU8(characterData + FightingStyleOffset);
            byte? virtue = mem.ReadU8(characterData + VirtueOffset);
            byte? sexualOrientation = mem.ReadU8(characterData + SexualOrientationOffset);
            byte? voicePitch = mem.ReadU8(characterData + VoicePitchOffset);
            byte? intelligenceLevel = mem.ReadU8(characterData + IntelligenceLevelOffset);
            byte? intelligenceRank = mem.ReadU8(characterData + IntelligenceRankOffset);
            int? intelligenceValue = mem.ReadI32(characterData + IntelligenceValueOffset);
            byte? strengthLevel = mem.ReadU8(characterData + StrengthLevelOffset);
            byte? strengthRank = mem.ReadU8(characterData + StrengthRankOffset);
            int? strengthValue = mem.ReadI32(characterData + StrengthValueOffset);
            byte? clubLevel = mem.ReadU8(characterData + ClubLevelOffset);
            int? clubValue = mem.ReadI32(characterData + ClubValueOffset);

            uint? statusPointer = mem.ReadU32(info.Pointer + 0xF6C);
            if (!statusPointer.HasValue || !mem.IsPlausiblePointer(statusPointer.Value))
            {
                error = "The status pointer is unreadable.";
                return false;
            }
            byte? intelligenceGrade = mem.ReadU8(statusPointer.Value + IntelligenceGradeStatusOffset);
            byte? intelligenceTopRank = mem.ReadU8(statusPointer.Value + IntelligenceTopRankStatusOffset);
            byte? strengthGrade = mem.ReadU8(statusPointer.Value + StrengthGradeStatusOffset);
            byte? strengthTopRank = mem.ReadU8(statusPointer.Value + StrengthTopRankStatusOffset);
            byte? clubGrade = mem.ReadU8(statusPointer.Value + ClubGradeStatusOffset);
            int? victory = mem.ReadI32(statusPointer.Value + VictoryStatusOffset);
            int? classSkip = mem.ReadI32(statusPointer.Value + ClassSkipStatusOffset);
            int? winningOver = mem.ReadI32(statusPointer.Value + WinningOverStatusOffset);
            int? partner = mem.ReadI32(statusPointer.Value + PartnerStatusOffset);
            int? rejected = mem.ReadI32(statusPointer.Value + RejectedStatusOffset);
            int? sexIntercourse = mem.ReadI32(statusPointer.Value + SexIntercourseStatusOffset);
            int? sexPartner = mem.ReadI32(statusPointer.Value + SexPartnerStatusOffset);
            byte? sexExperience = mem.ReadU8(characterData + SexExperienceOffset);
            byte? analExperience = mem.ReadU8(characterData + AnalExperienceOffset);
            string historyFirst = ReadText(mem, statusPointer.Value + FirstTimeStatusOffset,
                HHistoryFieldSize);
            string historyFirstAnal = ReadText(mem, statusPointer.Value + FirstAnalStatusOffset,
                HHistoryFieldSize);
            string historyLast = ReadText(mem, statusPointer.Value + LastTimeStatusOffset,
                HHistoryFieldSize);

            if (first == null || last == null || profile == null ||
                !personality.HasValue || !club.HasValue ||
                !social.HasValue || !fightingStyle.HasValue || !virtue.HasValue ||
                !sexualOrientation.HasValue || !voicePitch.HasValue ||
                !intelligenceLevel.HasValue || !intelligenceRank.HasValue ||
                !intelligenceValue.HasValue || !intelligenceGrade.HasValue ||
                !intelligenceTopRank.HasValue ||
                !strengthLevel.HasValue || !strengthRank.HasValue ||
                !strengthValue.HasValue || !strengthGrade.HasValue ||
                !strengthTopRank.HasValue ||
                !clubLevel.HasValue || !clubValue.HasValue || !clubGrade.HasValue ||
                !victory.HasValue || !classSkip.HasValue || !winningOver.HasValue ||
                !partner.HasValue || !rejected.HasValue || !sexIntercourse.HasValue ||
                !sexPartner.HasValue || !sexExperience.HasValue || !analExperience.HasValue ||
                historyFirst == null || historyFirstAnal == null ||
                historyLast == null)
            {
                error = "One or more character fields are unreadable.";
                return false;
            }

            snapshot = new CharacterSnapshot();
            snapshot.FirstName = first.Trim();
            snapshot.LastName = last.Trim();
            snapshot.Profile = NormalizeProfileText(profile);
            snapshot.Personality = personality.Value;
            snapshot.Club = club.Value;
            snapshot.Social = social.Value;
            snapshot.FightingStyle = fightingStyle.Value;
            snapshot.Virtue = virtue.Value;
            snapshot.SexualOrientation = sexualOrientation.Value;
            snapshot.VoicePitch = voicePitch.Value;
            snapshot.IntelligenceLevel = intelligenceLevel.Value;
            snapshot.IntelligenceRank = intelligenceRank.Value;
            snapshot.IntelligenceValue = intelligenceValue.Value;
            snapshot.IntelligenceGrade = intelligenceGrade.Value;
            snapshot.IntelligenceTopRank = intelligenceTopRank.Value;
            snapshot.StrengthLevel = strengthLevel.Value;
            snapshot.StrengthRank = strengthRank.Value;
            snapshot.StrengthValue = strengthValue.Value;
            snapshot.StrengthGrade = strengthGrade.Value;
            snapshot.StrengthTopRank = strengthTopRank.Value;
            snapshot.ClubLevel = clubLevel.Value;
            snapshot.ClubValue = clubValue.Value;
            snapshot.ClubGrade = clubGrade.Value;
            snapshot.VictoryCount = victory.Value;
            snapshot.ClassSkip = classSkip.Value;
            snapshot.WinningOver = winningOver.Value;
            snapshot.Partner = partner.Value;
            snapshot.Rejected = rejected.Value;
            snapshot.SexIntercourse = sexIntercourse.Value;
            snapshot.SexPartner = sexPartner.Value;
            snapshot.SexExperience = sexExperience.Value != 0;
            snapshot.AnalExperience = analExperience.Value != 0;
            snapshot.FirstTime = historyFirst.Trim();
            snapshot.FirstAnal = historyFirstAnal.Trim();
            snapshot.LastTime = historyLast.Trim();
            return true;
        }

        public static bool TryWrite(GameMemory mem, CharacterInfo info,
            CharacterSnapshot original, CharacterSnapshot edited,
            out string changed, out string error)
        {
            changed = "";
            error = null;

            if (mem == null || !mem.IsAttached)
            {
                error = "The game is not attached.";
                return false;
            }
            if (!mem.CanWrite)
            {
                error = string.IsNullOrEmpty(mem.WriteError)
                    ? "The game handle is read-only."
                    : mem.WriteError;
                return false;
            }
            if (info == null || info.Pointer == 0)
            {
                error = "No character is selected.";
                return false;
            }
            if (original == null || edited == null)
            {
                error = "No character data is loaded.";
                return false;
            }

            string firstText = (edited.FirstName ?? "").Trim();
            string lastText = (edited.LastName ?? "").Trim();
            string profileText = NormalizeProfileText(edited.Profile);
            int personality = edited.Personality;
            int club = edited.Club;
            int social = edited.Social;
            int fightingStyle = edited.FightingStyle;
            int virtue = edited.Virtue;
            int sexualOrientation = edited.SexualOrientation;
            int voicePitch = edited.VoicePitch;
            int intelligenceLevel = edited.IntelligenceLevel;
            int intelligenceRank = edited.IntelligenceRank;
            int intelligenceValue = edited.IntelligenceValue;
            int intelligenceGrade = edited.IntelligenceGrade;
            int intelligenceTopRank = edited.IntelligenceTopRank;
            int strengthLevel = edited.StrengthLevel;
            int strengthRank = edited.StrengthRank;
            int strengthValue = edited.StrengthValue;
            int strengthGrade = edited.StrengthGrade;
            int strengthTopRank = edited.StrengthTopRank;
            int clubLevel = edited.ClubLevel;
            int clubValue = edited.ClubValue;
            int clubGrade = edited.ClubGrade;
            int victory = edited.VictoryCount;
            int classSkip = edited.ClassSkip;
            int winningOver = edited.WinningOver;
            int partner = edited.Partner;
            int rejected = edited.Rejected;
            int sexIntercourse = edited.SexIntercourse;
            int sexPartner = edited.SexPartner;
            string historyFirst = (edited.FirstTime ?? "").Trim();
            string historyFirstAnal = (edited.FirstAnal ?? "").Trim();
            string historyLast = (edited.LastTime ?? "").Trim();
            bool sexExperience = edited.SexExperience;
            bool analExperience = edited.AnalExperience;

            bool firstChanged = !string.Equals(firstText,
                (original.FirstName ?? "").Trim(), StringComparison.Ordinal);
            bool lastChanged = !string.Equals(lastText,
                (original.LastName ?? "").Trim(), StringComparison.Ordinal);
            bool profileChanged = !string.Equals(profileText,
                NormalizeProfileText(original.Profile), StringComparison.Ordinal);
            bool personalityChanged = personality != original.Personality;
            bool clubChanged = club != original.Club;
            bool socialChanged = social != original.Social;
            bool fightingStyleChanged = fightingStyle != original.FightingStyle;
            bool virtueChanged = virtue != original.Virtue;
            bool sexualOrientationChanged = sexualOrientation != original.SexualOrientation;
            bool voicePitchChanged = voicePitch != original.VoicePitch;
            bool intelligenceLevelChanged = intelligenceLevel != original.IntelligenceLevel;
            bool intelligenceRankChanged = intelligenceRank != original.IntelligenceRank;
            bool intelligenceValueChanged = intelligenceValue != original.IntelligenceValue;
            bool intelligenceGradeChanged = intelligenceGrade != original.IntelligenceGrade;
            bool intelligenceTopRankChanged = intelligenceTopRank != original.IntelligenceTopRank;
            bool strengthLevelChanged = strengthLevel != original.StrengthLevel;
            bool strengthRankChanged = strengthRank != original.StrengthRank;
            bool strengthValueChanged = strengthValue != original.StrengthValue;
            bool strengthGradeChanged = strengthGrade != original.StrengthGrade;
            bool strengthTopRankChanged = strengthTopRank != original.StrengthTopRank;
            bool clubLevelChanged = clubLevel != original.ClubLevel;
            bool clubValueChanged = clubValue != original.ClubValue;
            bool clubGradeChanged = clubGrade != original.ClubGrade;
            bool victoryChanged = victory != original.VictoryCount;
            bool classSkipChanged = classSkip != original.ClassSkip;
            bool winningOverChanged = winningOver != original.WinningOver;
            bool partnerChanged = partner != original.Partner;
            bool rejectedChanged = rejected != original.Rejected;
            bool sexIntercourseChanged = sexIntercourse != original.SexIntercourse;
            bool sexPartnerChanged = sexPartner != original.SexPartner;
            bool firstTimeChanged = !string.Equals(historyFirst,
                (original.FirstTime ?? "").Trim(), StringComparison.Ordinal);
            bool firstAnalChanged = !string.Equals(historyFirstAnal,
                (original.FirstAnal ?? "").Trim(), StringComparison.Ordinal);
            bool lastTimeChanged = !string.Equals(historyLast,
                (original.LastTime ?? "").Trim(), StringComparison.Ordinal);
            bool sexExperienceChanged = sexExperience != original.SexExperience;
            bool analExperienceChanged = analExperience != original.AnalExperience;

            if (!firstChanged && !lastChanged && !profileChanged &&
                !personalityChanged && !clubChanged && !socialChanged &&
                !fightingStyleChanged && !virtueChanged &&
                !sexualOrientationChanged && !voicePitchChanged &&
                !intelligenceLevelChanged && !intelligenceRankChanged &&
                !intelligenceValueChanged && !intelligenceGradeChanged &&
                !intelligenceTopRankChanged &&
                !strengthLevelChanged && !strengthRankChanged &&
                !strengthValueChanged && !strengthGradeChanged &&
                !strengthTopRankChanged &&
                !clubLevelChanged && !clubValueChanged && !clubGradeChanged &&
                !victoryChanged && !classSkipChanged && !winningOverChanged &&
                !partnerChanged && !rejectedChanged && !sexIntercourseChanged &&
                !sexPartnerChanged && !firstTimeChanged && !firstAnalChanged &&
                !lastTimeChanged && !sexExperienceChanged && !analExperienceChanged)
            {
                changed = "No changes.";
                return true;
            }

            if ((firstChanged || lastChanged) && firstText.Length == 0 && lastText.Length == 0)
            {
                error = "First name and last name cannot both be empty.";
                return false;
            }
            if (firstChanged && HasControlCharacters(firstText))
            {
                error = "First name cannot contain tabs, newlines, or other control characters.";
                return false;
            }
            if (lastChanged && HasControlCharacters(lastText))
            {
                error = "Last name cannot contain tabs, newlines, or other control characters.";
                return false;
            }
            if (profileChanged && HasInvalidProfileCharacters(profileText))
            {
                error = "Profile can only contain normal text, tabs, and line breaks.";
                return false;
            }
            if (personalityChanged && (personality < 0 || personality > MaxPersonality))
            {
                error = "Personality value is out of range.";
                return false;
            }
            if (clubChanged && (club < 0 || club > MaxClub))
            {
                error = "Club value is out of range.";
                return false;
            }
            if (socialChanged && !IsByteValue(social))
            {
                error = "Social value is out of range.";
                return false;
            }
            if (fightingStyleChanged && !IsByteValue(fightingStyle))
            {
                error = "Fighting style value is out of range.";
                return false;
            }
            if (virtueChanged && !IsByteValue(virtue))
            {
                error = "Virtue value is out of range.";
                return false;
            }
            if (sexualOrientationChanged && !IsByteValue(sexualOrientation))
            {
                error = "Sexual orientation value is out of range.";
                return false;
            }
            if (voicePitchChanged && !IsByteValue(voicePitch))
            {
                error = "Voice pitch value is out of range.";
                return false;
            }
            if (intelligenceLevelChanged &&
                (intelligenceLevel < 0 || intelligenceLevel > MaxIntelligenceLevel))
            {
                error = "Intelligence level is out of range.";
                return false;
            }
            if (intelligenceRankChanged &&
                (intelligenceRank < 0 || intelligenceRank > MaxIntelligenceRank))
            {
                error = "Intelligence rank is out of range.";
                return false;
            }
            if (intelligenceValueChanged &&
                (intelligenceValue < 0 || intelligenceValue > MaxIntelligenceValue))
            {
                error = "Intelligence value is out of range.";
                return false;
            }
            if (intelligenceGradeChanged && !IsByteValue(intelligenceGrade))
            {
                error = "Grade value is out of range.";
                return false;
            }
            if (intelligenceTopRankChanged &&
                (intelligenceTopRank < 0 || intelligenceTopRank > MaxIntelligenceTopRank))
            {
                error = "Rank value is out of range.";
                return false;
            }
            if (strengthLevelChanged &&
                (strengthLevel < 0 || strengthLevel > MaxStrengthLevel))
            {
                error = "Strength level is out of range.";
                return false;
            }
            if (strengthRankChanged &&
                (strengthRank < 0 || strengthRank > MaxStrengthRank))
            {
                error = "Strength rank is out of range.";
                return false;
            }
            if (strengthValueChanged &&
                (strengthValue < 0 || strengthValue > MaxStrengthValue))
            {
                error = "Strength value is out of range.";
                return false;
            }
            if (strengthGradeChanged && !IsByteValue(strengthGrade))
            {
                error = "Strength grade value is out of range.";
                return false;
            }
            if (strengthTopRankChanged &&
                (strengthTopRank < 0 || strengthTopRank > MaxStrengthTopRank))
            {
                error = "Strength rank value is out of range.";
                return false;
            }
            if (clubLevelChanged && (clubLevel < 0 || clubLevel > MaxClubLevel))
            {
                error = "Club level is out of range.";
                return false;
            }
            if (clubValueChanged && (clubValue < 0 || clubValue > MaxClubValue))
            {
                error = "Club value is out of range.";
                return false;
            }
            if (clubGradeChanged && !IsByteValue(clubGrade))
            {
                error = "Club grade value is out of range.";
                return false;
            }
            if ((victoryChanged && victory < 0) || (classSkipChanged && classSkip < 0) ||
                (winningOverChanged && winningOver < 0) || (partnerChanged && partner < 0) ||
                (rejectedChanged && rejected < 0) ||
                (sexIntercourseChanged && sexIntercourse < 0) ||
                (sexPartnerChanged && sexPartner < 0))
            {
                error = "Status counts cannot be negative.";
                return false;
            }
            if (firstTimeChanged && HasControlCharacters(historyFirst))
            {
                error = "First time cannot contain tabs, newlines, or other control characters.";
                return false;
            }
            if (firstAnalChanged && HasControlCharacters(historyFirstAnal))
            {
                error = "First anal cannot contain tabs, newlines, or other control characters.";
                return false;
            }
            if (lastTimeChanged && HasControlCharacters(historyLast))
            {
                error = "Last time cannot contain tabs, newlines, or other control characters.";
                return false;
            }

            byte[] firstBytes = null;
            byte[] lastBytes = null;
            byte[] profileBytes = null;
            byte[] firstTimeBytes = null;
            byte[] firstAnalBytes = null;
            byte[] lastTimeBytes = null;
            if (firstChanged || lastChanged)
            {
                if (!TryEncode(firstText, "First name", NameMaxBytes, out firstBytes, out error))
                    return false;
                if (!TryEncode(lastText, "Last name", NameMaxBytes, out lastBytes, out error))
                    return false;
            }
            if (profileChanged &&
                !TryEncode(profileText, "Profile", ProfileMaxBytes, out profileBytes, out error))
                return false;
            if (firstTimeChanged &&
                !TryEncode(historyFirst, "First time", HHistoryMaxBytes,
                    out firstTimeBytes, out error))
                return false;
            if (firstAnalChanged &&
                !TryEncode(historyFirstAnal, "First anal", HHistoryMaxBytes,
                    out firstAnalBytes, out error))
                return false;
            if (lastTimeChanged &&
                !TryEncode(historyLast, "Last time", HHistoryMaxBytes,
                    out lastTimeBytes, out error))
                return false;

            uint characterData;
            if (!TryGetCharacterData(mem, info, out characterData, out error))
                return false;

            uint statusPointer = 0;
            if (intelligenceGradeChanged || intelligenceTopRankChanged ||
                strengthGradeChanged || strengthTopRankChanged || clubGradeChanged ||
                victoryChanged || classSkipChanged || winningOverChanged ||
                partnerChanged || rejectedChanged || sexIntercourseChanged ||
                sexPartnerChanged || firstTimeChanged || firstAnalChanged ||
                lastTimeChanged)
            {
                uint? status = mem.ReadU32(info.Pointer + 0xF6C);
                if (!status.HasValue || !mem.IsPlausiblePointer(status.Value))
                {
                    error = "The status pointer (char + 0xF6C) is unreadable.";
                    return false;
                }
                statusPointer = status.Value;
            }

            List<string> written = new List<string>();

            if (firstChanged)
            {
                if (!WriteText(mem, characterData + FirstNameOffset, firstBytes,
                        NameFieldSize, "First name", out error))
                    return Partial(written, error, out changed);
                written.Add("first name");
            }
            if (lastChanged)
            {
                if (!WriteText(mem, characterData + LastNameOffset, lastBytes,
                        NameFieldSize, "Last name", out error))
                    return Partial(written, error, out changed);
                written.Add("last name");
            }
            if (profileChanged)
            {
                if (!WriteText(mem, characterData + ProfileOffset, profileBytes,
                        ProfileFieldSize, "Profile", out error))
                    return Partial(written, error, out changed);
                written.Add("profile");
            }
            if (personalityChanged)
            {
                if (!WriteByte(mem, characterData + PersonalityOffset, (byte)personality,
                        "Personality", out error))
                    return Partial(written, error, out changed);
                written.Add("personality");
            }
            if (clubChanged)
            {
                if (!WriteByte(mem, characterData + ClubOffset, (byte)club,
                        "Club", out error))
                    return Partial(written, error, out changed);
                written.Add("club");
            }
            if (socialChanged)
            {
                if (!WriteByte(mem, characterData + SocialOffset, (byte)social,
                        "Social", out error))
                    return Partial(written, error, out changed);
                written.Add("social");
            }
            if (fightingStyleChanged)
            {
                if (!WriteByte(mem, characterData + FightingStyleOffset, (byte)fightingStyle,
                        "Fighting style", out error))
                    return Partial(written, error, out changed);
                written.Add("fighting style");
            }
            if (virtueChanged)
            {
                if (!WriteByte(mem, characterData + VirtueOffset, (byte)virtue,
                        "Virtue", out error))
                    return Partial(written, error, out changed);
                written.Add("virtue");
            }
            if (sexualOrientationChanged)
            {
                if (!WriteByte(mem, characterData + SexualOrientationOffset,
                        (byte)sexualOrientation, "Sexual orientation", out error))
                    return Partial(written, error, out changed);
                written.Add("sexual orientation");
            }
            if (voicePitchChanged)
            {
                if (!WriteByte(mem, characterData + VoicePitchOffset, (byte)voicePitch,
                        "Voice pitch", out error))
                    return Partial(written, error, out changed);
                written.Add("voice pitch");
            }
            if (intelligenceLevelChanged)
            {
                if (!WriteByte(mem, characterData + IntelligenceLevelOffset,
                        (byte)intelligenceLevel, "Intelligence", out error))
                    return Partial(written, error, out changed);
                written.Add("intelligence");
            }
            if (intelligenceRankChanged)
            {
                if (!WriteByte(mem, characterData + IntelligenceRankOffset,
                        (byte)intelligenceRank, "Level", out error))
                    return Partial(written, error, out changed);
                written.Add("level");
            }
            if (intelligenceValueChanged)
            {
                if (!WriteInt32(mem, characterData + IntelligenceValueOffset,
                        intelligenceValue, "Value", out error))
                    return Partial(written, error, out changed);
                written.Add("value");
            }
            if (intelligenceGradeChanged)
            {
                if (!WriteByte(mem, statusPointer + IntelligenceGradeStatusOffset,
                        (byte)intelligenceGrade, "Grade", out error))
                    return Partial(written, error, out changed);
                written.Add("grade");
            }
            if (intelligenceTopRankChanged)
            {
                if (!WriteByte(mem, statusPointer + IntelligenceTopRankStatusOffset,
                        (byte)intelligenceTopRank, "Rank", out error))
                    return Partial(written, error, out changed);
                written.Add("rank");
            }
            if (strengthLevelChanged)
            {
                if (!WriteByte(mem, characterData + StrengthLevelOffset,
                        (byte)strengthLevel, "Strength", out error))
                    return Partial(written, error, out changed);
                written.Add("strength");
            }
            if (strengthRankChanged)
            {
                if (!WriteByte(mem, characterData + StrengthRankOffset,
                        (byte)strengthRank, "Strength level", out error))
                    return Partial(written, error, out changed);
                written.Add("strength level");
            }
            if (strengthValueChanged)
            {
                if (!WriteInt32(mem, characterData + StrengthValueOffset,
                        strengthValue, "Strength value", out error))
                    return Partial(written, error, out changed);
                written.Add("strength value");
            }
            if (strengthGradeChanged)
            {
                if (!WriteByte(mem, statusPointer + StrengthGradeStatusOffset,
                        (byte)strengthGrade, "Strength grade", out error))
                    return Partial(written, error, out changed);
                written.Add("strength grade");
            }
            if (strengthTopRankChanged)
            {
                if (!WriteByte(mem, statusPointer + StrengthTopRankStatusOffset,
                        (byte)strengthTopRank, "Strength rank", out error))
                    return Partial(written, error, out changed);
                written.Add("strength rank");
            }
            if (clubLevelChanged)
            {
                if (!WriteByte(mem, characterData + ClubLevelOffset,
                        (byte)clubLevel, "Club level", out error))
                    return Partial(written, error, out changed);
                written.Add("club level");
            }
            if (clubValueChanged)
            {
                if (!WriteInt32(mem, characterData + ClubValueOffset,
                        clubValue, "Club value", out error))
                    return Partial(written, error, out changed);
                written.Add("club value");
            }
            if (clubGradeChanged)
            {
                if (!WriteByte(mem, statusPointer + ClubGradeStatusOffset,
                        (byte)clubGrade, "Club grade", out error))
                    return Partial(written, error, out changed);
                written.Add("club grade");
            }
            if (victoryChanged)
            {
                if (!WriteInt32(mem, statusPointer + VictoryStatusOffset,
                        victory, "Victory count", out error))
                    return Partial(written, error, out changed);
                written.Add("victory count");
            }
            if (classSkipChanged)
            {
                if (!WriteInt32(mem, statusPointer + ClassSkipStatusOffset,
                        classSkip, "Class skip", out error))
                    return Partial(written, error, out changed);
                written.Add("class skip");
            }
            if (winningOverChanged)
            {
                if (!WriteInt32(mem, statusPointer + WinningOverStatusOffset,
                        winningOver, "Winning over", out error))
                    return Partial(written, error, out changed);
                written.Add("winning over");
            }
            if (partnerChanged)
            {
                if (!WriteInt32(mem, statusPointer + PartnerStatusOffset,
                        partner, "Partner", out error))
                    return Partial(written, error, out changed);
                written.Add("partner");
            }
            if (rejectedChanged)
            {
                if (!WriteInt32(mem, statusPointer + RejectedStatusOffset,
                        rejected, "Rejected", out error))
                    return Partial(written, error, out changed);
                written.Add("rejected");
            }
            if (sexIntercourseChanged)
            {
                if (!WriteInt32(mem, statusPointer + SexIntercourseStatusOffset,
                        sexIntercourse, "H intercourse", out error))
                    return Partial(written, error, out changed);
                written.Add("H intercourse");
            }
            if (sexPartnerChanged)
            {
                if (!WriteInt32(mem, statusPointer + SexPartnerStatusOffset,
                        sexPartner, "H partner", out error))
                    return Partial(written, error, out changed);
                written.Add("H partner");
            }
            if (firstTimeChanged)
            {
                if (!WriteText(mem, statusPointer + FirstTimeStatusOffset, firstTimeBytes,
                        HHistoryFieldSize, "First time", out error))
                    return Partial(written, error, out changed);
                written.Add("first time");
            }
            if (firstAnalChanged)
            {
                if (!WriteText(mem, statusPointer + FirstAnalStatusOffset, firstAnalBytes,
                        HHistoryFieldSize, "First anal", out error))
                    return Partial(written, error, out changed);
                written.Add("first anal");
            }
            if (lastTimeChanged)
            {
                if (!WriteText(mem, statusPointer + LastTimeStatusOffset, lastTimeBytes,
                        HHistoryFieldSize, "Last time", out error))
                    return Partial(written, error, out changed);
                written.Add("last time");
            }
            if (sexExperienceChanged)
            {
                if (!WriteByte(mem, characterData + SexExperienceOffset,
                        sexExperience ? (byte)1 : (byte)0, "Sex experience", out error))
                    return Partial(written, error, out changed);
                written.Add("sex experience");
            }
            if (analExperienceChanged)
            {
                if (!WriteByte(mem, characterData + AnalExperienceOffset,
                        analExperience ? (byte)1 : (byte)0, "Anal experience", out error))
                    return Partial(written, error, out changed);
                written.Add("anal experience");
            }

            changed = string.Join(", ", written.ToArray());
            return true;
        }

        public static string NormalizeProfileText(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n", "\r\n");
        }

        public static int GetProfileByteCount(string text)
        {
            return TextEncoding.GetByteCount(NormalizeProfileText(text));
        }

        private static bool Partial(List<string> written, string error,
            out string changed)
        {
            changed = written.Count == 0 ? "" : string.Join(", ", written.ToArray());
            if (written.Count == 0) return false;

            error = "Saved " + changed + ", but the next field failed: " + error;
            changed = "";
            return false;
        }

        private static bool TryGetCharacterData(GameMemory mem, CharacterInfo info,
            out uint characterData, out string error)
        {
            characterData = 0;
            error = null;

            uint? pointer = mem.ReadU32(info.Pointer + 0x28);
            if (!pointer.HasValue || !mem.IsPlausiblePointer(pointer.Value))
            {
                error = "The character data pointer (char + 0x28) is unreadable.";
                return false;
            }
            characterData = pointer.Value;
            return true;
        }

        private static string ReadText(GameMemory mem, uint address, int fieldSize)
        {
            byte[] bytes = mem.ReadBytes(address, fieldSize);
            if (bytes == null) return null;

            int length = Array.IndexOf(bytes, (byte)0);
            if (length < 0) length = bytes.Length;
            if (length == 0) return "";

            try { return TextEncoding.GetString(bytes, 0, length); }
            catch { return null; }
        }

        private static bool TryEncode(string text, string fieldName, int maxBytes,
            out byte[] bytes, out string error)
        {
            bytes = TextEncoding.GetBytes(text ?? "");
            error = null;
            if (bytes.Length > maxBytes)
            {
                error = fieldName + " is too long: " + bytes.Length.ToString() +
                        " bytes, maximum " + maxBytes.ToString() + ".";
                return false;
            }
            return true;
        }

        private static bool WriteText(GameMemory mem, uint address, byte[] bytes,
            int fieldSize, string fieldName, out string error)
        {
            error = null;

            byte[] buffer = new byte[fieldSize];
            if (bytes != null && bytes.Length > 0)
                Buffer.BlockCopy(bytes, 0, buffer, 0, bytes.Length);

            if (!mem.WriteBytes(address, buffer))
            {
                error = fieldName + " write failed: " + (mem.WriteError ?? "unknown error");
                return false;
            }

            byte[] check = mem.ReadBytes(address, fieldSize);
            if (check == null || !BytesEqual(buffer, check))
            {
                error = fieldName + " verification failed at 0x" + address.ToString("X8") + ".";
                return false;
            }
            return true;
        }

        private static bool WriteByte(GameMemory mem, uint address, byte value,
            string fieldName, out string error)
        {
            error = null;

            byte[] buffer = new byte[] { value };
            if (!mem.WriteBytes(address, buffer))
            {
                error = fieldName + " write failed: " + (mem.WriteError ?? "unknown error");
                return false;
            }

            byte? check = mem.ReadU8(address);
            if (!check.HasValue || check.Value != value)
            {
                error = fieldName + " verification failed at 0x" + address.ToString("X8") + ".";
                return false;
            }
            return true;
        }

        private static bool WriteInt32(GameMemory mem, uint address, int value,
            string fieldName, out string error)
        {
            error = null;

            byte[] buffer = BitConverter.GetBytes(value);
            if (!mem.WriteBytes(address, buffer))
            {
                error = fieldName + " write failed: " + (mem.WriteError ?? "unknown error");
                return false;
            }

            int? check = mem.ReadI32(address);
            if (!check.HasValue || check.Value != value)
            {
                error = fieldName + " verification failed at 0x" + address.ToString("X8") + ".";
                return false;
            }
            return true;
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        private static bool HasControlCharacters(string text)
        {
            foreach (char c in text)
            {
                if (c < 0x20 || c == 0x7F) return true;
            }
            return false;
        }

        private static bool IsByteValue(int value)
        {
            return value >= 0 && value <= MaxAttributeValue;
        }

        private static bool HasInvalidProfileCharacters(string text)
        {
            foreach (char c in text)
            {
                if (c == '\r' || c == '\n' || c == '\t') continue;
                if (c < 0x20 || c == 0x7F) return true;
            }
            return false;
        }

        private static Encoding GetEncoding()
        {
            try { return Encoding.GetEncoding(932); }
            catch { return Encoding.Default; }
        }
    }
}
