using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace AA2Trainer.WpfPrototype
{
    public sealed class MainViewModel : ObservableObject
    {
        private readonly GameSessionService session;
        private readonly List<CardEntry> cardIndex = new List<CardEntry>();
        private readonly Dictionary<string, CardImages> loadedCards =
            new Dictionary<string, CardImages>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> failedCards =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool cardLoadRunning;
        private bool disposed;

        private SeatItem selectedSeat;
        private CharacterDetail selectedCharacter;
        private CharacterDraft characterDraft;
        private TraitDraft traitDraft;
        private BodyDraft bodyDraft;
        private FieldDraft faceDraft;
        private FieldDraft hairDraft;
        private OutfitDraft outfitDraft;
        private ScheduleDraft scheduleDraft;
        private ScheduleSnapshot liveSchedule;
        private CharacterOptions characterOptions;
        private CharacterSnapshot liveCharacter;
        private uint livePointer;
        private bool[] liveTraits;
        private int[,] liveRisk;
        private int[] liveBodyValues;
        private int[] liveFaceValues;
        private string[] liveFaceTexts;
        private int[] liveHairValues;
        private int[] liveOutfitValues;
        private string characterStatus = "No changes.";
        private string traitStatus = "No changes.";
        private string bodyStatus = "No changes.";
        private string faceStatus = "No changes.";
        private string hairStatus = "No changes.";
        private string outfitStatus = "No changes.";
        private string scheduleStatus = "No changes.";
        private bool characterWriteBusy;
        private bool traitWriteBusy;
        private bool bodyWriteBusy;
        private bool faceWriteBusy;
        private bool hairWriteBusy;
        private bool outfitWriteBusy;
        private bool scheduleWriteBusy;
        private bool timeFrozen;
        private bool railCollapsed;
        private bool applyingSnapshot;
        private bool isAttached;
        private string attachState = "Not attached";
        private string gameTime = "Game time: -";
        private string pointerHealth = "Pointers: not checked";
        private string namesState = "Names: not loaded";
        private string updatedText = "Updated -";
        private string writeGate = "Write gate: read-only";
        private string cardStatus = "Cards: not loaded";
        private string rosterStatus = "";
        private string rosterSignature = "";
        private string rosterFilter = "";
        private int seatFilterIndex;
        private int selectedTabIndex;

        public MainViewModel()
        {
            Seats = new ObservableCollection<SeatItem>();
            PlanSteps = new ObservableCollection<PlanStepItem>();
            LiveLines = new ObservableCollection<LogLine>();
            PlannerLines = new ObservableCollection<LogLine>();
            DebugFields = new ObservableCollection<DebugFieldItem>();

            SeedSeats();
            selectedSeat = Seats[0];
            SeedPlan();
            SeedLogs();
            SeedDebug();

            Dispatcher dispatcher = Application.Current != null
                ? Application.Current.Dispatcher
                : Dispatcher.CurrentDispatcher;
            session = new GameSessionService(dispatcher);
            session.SnapshotReady += ApplySnapshot;
            session.CharacterWriteCompleted += OnCharacterWriteCompleted;
            session.TraitWriteCompleted += OnTraitWriteCompleted;
            session.BodyWriteCompleted += OnBodyWriteCompleted;
            session.FaceWriteCompleted += OnFaceWriteCompleted;
            session.HairWriteCompleted += OnHairWriteCompleted;
            session.OutfitWriteCompleted += OnOutfitWriteCompleted;
            session.PlayerSwitchCompleted += OnPlayerSwitchCompleted;
            session.RosterActionCompleted += OnRosterActionCompleted;
            session.ScheduleWriteCompleted += OnScheduleWriteCompleted;
            LoadCardsAsync();
        }

        public ObservableCollection<SeatItem> Seats { get; private set; }
        public ObservableCollection<PlanStepItem> PlanSteps { get; private set; }
        public ObservableCollection<LogLine> LiveLines { get; private set; }
        public ObservableCollection<LogLine> PlannerLines { get; private set; }
        public ObservableCollection<DebugFieldItem> DebugFields { get; private set; }

        public SeatItem SelectedSeat
        {
            get { return selectedSeat; }
            set
            {
                if (!Set(ref selectedSeat, value)) return;
                if (!applyingSnapshot && isAttached && value != null &&
                    value.SeatIndex >= 0)
                {
                    session.RequestSeat(value.SeatIndex);
                }
            }
        }

        public bool RailCollapsed
        {
            get { return railCollapsed; }
            set { Set(ref railCollapsed, value); }
        }

        public CharacterDetail SelectedCharacter
        {
            get { return selectedCharacter; }
            private set { Set(ref selectedCharacter, value); }
        }

        public CharacterDraft CharacterDraft
        {
            get { return characterDraft; }
            private set
            {
                if (characterDraft == value) return;
                if (characterDraft != null)
                    characterDraft.PropertyChanged -= CharacterDraft_PropertyChanged;
                characterDraft = value;
                if (characterDraft != null)
                    characterDraft.PropertyChanged += CharacterDraft_PropertyChanged;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanApply));
                OnPropertyChanged(nameof(CanRevert));
            }
        }

        public CharacterOptions CharacterOptions
        {
            get { return characterOptions; }
            private set { Set(ref characterOptions, value); }
        }

        public TraitDraft TraitDraft
        {
            get { return traitDraft; }
            private set
            {
                if (traitDraft == value) return;
                if (traitDraft != null)
                    traitDraft.PropertyChanged -= TraitDraft_PropertyChanged;
                traitDraft = value;
                if (traitDraft != null)
                    traitDraft.PropertyChanged += TraitDraft_PropertyChanged;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanApplyTraits));
                OnPropertyChanged(nameof(CanRevertTraits));
            }
        }

        public BodyDraft BodyDraft
        {
            get { return bodyDraft; }
            private set
            {
                if (bodyDraft == value) return;
                if (bodyDraft != null)
                    bodyDraft.PropertyChanged -= BodyDraft_PropertyChanged;
                bodyDraft = value;
                if (bodyDraft != null)
                    bodyDraft.PropertyChanged += BodyDraft_PropertyChanged;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanApplyBody));
                OnPropertyChanged(nameof(CanRevertBody));
            }
        }

        public string BodyStatus
        {
            get { return bodyStatus; }
            private set { Set(ref bodyStatus, value); }
        }

        public bool IsBodyWriteBusy
        {
            get { return bodyWriteBusy; }
            private set
            {
                if (!Set(ref bodyWriteBusy, value)) return;
                OnPropertyChanged(nameof(CanApplyBody));
                OnPropertyChanged(nameof(CanRevertBody));
            }
        }

        public bool CanApplyBody
        {
            get
            {
                return bodyDraft != null && bodyDraft.IsDirty &&
                    !bodyWriteBusy && isAttached;
            }
        }

        public bool CanRevertBody
        {
            get
            {
                return bodyDraft != null && bodyDraft.IsDirty &&
                    !bodyWriteBusy;
            }
        }

        public FieldDraft FaceDraft
        {
            get { return faceDraft; }
            private set
            {
                if (faceDraft == value) return;
                if (faceDraft != null)
                    faceDraft.PropertyChanged -= FaceDraft_PropertyChanged;
                faceDraft = value;
                if (faceDraft != null)
                    faceDraft.PropertyChanged += FaceDraft_PropertyChanged;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanApplyFace));
                OnPropertyChanged(nameof(CanRevertFace));
            }
        }

        public string FaceStatus
        {
            get { return faceStatus; }
            private set { Set(ref faceStatus, value); }
        }

        public bool IsFaceWriteBusy
        {
            get { return faceWriteBusy; }
            private set
            {
                if (!Set(ref faceWriteBusy, value)) return;
                OnPropertyChanged(nameof(CanApplyFace));
                OnPropertyChanged(nameof(CanRevertFace));
            }
        }

        public bool CanApplyFace
        {
            get
            {
                return faceDraft != null && faceDraft.IsDirty &&
                    !faceWriteBusy && isAttached;
            }
        }

        public bool CanRevertFace
        {
            get
            {
                return faceDraft != null && faceDraft.IsDirty &&
                    !faceWriteBusy;
            }
        }

        public FieldDraft HairDraft
        {
            get { return hairDraft; }
            private set
            {
                if (hairDraft == value) return;
                if (hairDraft != null)
                    hairDraft.PropertyChanged -= HairDraft_PropertyChanged;
                hairDraft = value;
                if (hairDraft != null)
                    hairDraft.PropertyChanged += HairDraft_PropertyChanged;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanApplyHair));
                OnPropertyChanged(nameof(CanRevertHair));
            }
        }

        public string HairStatus
        {
            get { return hairStatus; }
            private set { Set(ref hairStatus, value); }
        }

        public bool IsHairWriteBusy
        {
            get { return hairWriteBusy; }
            private set
            {
                if (!Set(ref hairWriteBusy, value)) return;
                OnPropertyChanged(nameof(CanApplyHair));
                OnPropertyChanged(nameof(CanRevertHair));
            }
        }

        public bool CanApplyHair
        {
            get
            {
                return hairDraft != null && hairDraft.IsDirty &&
                    !hairWriteBusy && isAttached;
            }
        }

        public bool CanRevertHair
        {
            get
            {
                return hairDraft != null && hairDraft.IsDirty &&
                    !hairWriteBusy;
            }
        }

        public OutfitDraft OutfitDraft
        {
            get { return outfitDraft; }
            private set
            {
                if (outfitDraft == value) return;
                if (outfitDraft != null)
                    outfitDraft.PropertyChanged -= OutfitDraft_PropertyChanged;
                outfitDraft = value;
                if (outfitDraft != null)
                    outfitDraft.PropertyChanged += OutfitDraft_PropertyChanged;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanApplyOutfit));
                OnPropertyChanged(nameof(CanRevertOutfit));
            }
        }

        public string OutfitStatus
        {
            get { return outfitStatus; }
            private set { Set(ref outfitStatus, value); }
        }

        public bool IsOutfitWriteBusy
        {
            get { return outfitWriteBusy; }
            private set
            {
                if (!Set(ref outfitWriteBusy, value)) return;
                OnPropertyChanged(nameof(CanApplyOutfit));
                OnPropertyChanged(nameof(CanRevertOutfit));
            }
        }

        public bool CanApplyOutfit
        {
            get
            {
                return outfitDraft != null && outfitDraft.IsDirty &&
                    !outfitWriteBusy && isAttached;
            }
        }

        public bool CanRevertOutfit
        {
            get
            {
                return outfitDraft != null && outfitDraft.IsDirty &&
                    !outfitWriteBusy;
            }
        }

        public ScheduleDraft ScheduleDraft
        {
            get { return scheduleDraft; }
            private set
            {
                if (scheduleDraft == value) return;
                if (scheduleDraft != null)
                    scheduleDraft.PropertyChanged -= ScheduleDraft_PropertyChanged;
                scheduleDraft = value;
                if (scheduleDraft != null)
                    scheduleDraft.PropertyChanged += ScheduleDraft_PropertyChanged;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanApplySchedule));
                OnPropertyChanged(nameof(CanRevertSchedule));
            }
        }

        public string ScheduleStatus
        {
            get { return scheduleStatus; }
            private set { Set(ref scheduleStatus, value); }
        }

        public bool IsScheduleWriteBusy
        {
            get { return scheduleWriteBusy; }
            private set
            {
                if (!Set(ref scheduleWriteBusy, value)) return;
                OnPropertyChanged(nameof(CanApplySchedule));
                OnPropertyChanged(nameof(CanRevertSchedule));
            }
        }

        public bool CanApplySchedule
        {
            get
            {
                return scheduleDraft != null && scheduleDraft.IsDirty &&
                    !scheduleWriteBusy && isAttached;
            }
        }

        public bool CanRevertSchedule
        {
            get
            {
                return scheduleDraft != null && scheduleDraft.IsDirty &&
                    !scheduleWriteBusy;
            }
        }

        public bool IsTimeFrozen
        {
            get { return timeFrozen; }
            private set { Set(ref timeFrozen, value); }
        }

        public IList<string> WeekdayOptions
        {
            get { return ScheduleEditor.Weekdays; }
        }

        public IList<string> PeriodOptions
        {
            get { return ScheduleEditor.Periods; }
        }

        public IList<string> WeekOptions
        {
            get { return weekOptions; }
        }

        private static readonly string[] weekOptions = new string[]
        {
            "Week 1", "Week 2", "Week 3", "Week 4", "Week 5", "Week 6"
        };

        public string TraitStatus
        {
            get { return traitStatus; }
            private set { Set(ref traitStatus, value); }
        }

        public bool IsTraitWriteBusy
        {
            get { return traitWriteBusy; }
            private set
            {
                if (!Set(ref traitWriteBusy, value)) return;
                OnPropertyChanged(nameof(CanApplyTraits));
                OnPropertyChanged(nameof(CanRevertTraits));
            }
        }

        public bool CanApplyTraits
        {
            get
            {
                return traitDraft != null && traitDraft.IsDirty &&
                    !traitWriteBusy && isAttached;
            }
        }

        public bool CanRevertTraits
        {
            get
            {
                return traitDraft != null && traitDraft.IsDirty &&
                    !traitWriteBusy;
            }
        }

        public string CharacterStatus
        {
            get { return characterStatus; }
            private set { Set(ref characterStatus, value); }
        }

        public bool IsCharacterWriteBusy
        {
            get { return characterWriteBusy; }
            private set
            {
                if (!Set(ref characterWriteBusy, value)) return;
                OnPropertyChanged(nameof(CanApply));
            }
        }

        public bool CanApply
        {
            get
            {
                return characterDraft != null && characterDraft.IsDirty &&
                    !characterWriteBusy && isAttached;
            }
        }

        public bool CanRevert
        {
            get
            {
                return characterDraft != null && characterDraft.IsDirty &&
                    !characterWriteBusy;
            }
        }

        public string RosterFilter
        {
            get { return rosterFilter; }
            set
            {
                if (Set(ref rosterFilter, value)) ApplyRosterFilter();
            }
        }

        public int SeatFilterIndex
        {
            get { return seatFilterIndex; }
            set
            {
                if (Set(ref seatFilterIndex, value)) ApplyRosterFilter();
            }
        }

        public bool IsAttached
        {
            get { return isAttached; }
            private set
            {
                if (!Set(ref isAttached, value)) return;
                OnPropertyChanged(nameof(AttachButtonText));
                OnPropertyChanged(nameof(CanApply));
                OnPropertyChanged(nameof(CanApplyTraits));
                OnPropertyChanged(nameof(CanApplyBody));
            }
        }

        public string AttachState
        {
            get { return attachState; }
            private set { Set(ref attachState, value); }
        }

        public string AttachButtonText
        {
            get { return isAttached ? "Detach" : "Attach"; }
        }

        public string GameTime
        {
            get { return gameTime; }
            private set { Set(ref gameTime, value); }
        }

        public string PointerHealth
        {
            get { return pointerHealth; }
            private set { Set(ref pointerHealth, value); }
        }

        public string NamesState
        {
            get { return namesState; }
            private set { Set(ref namesState, value); }
        }

        public string UpdatedText
        {
            get { return updatedText; }
            private set { Set(ref updatedText, value); }
        }

        public string WriteGate
        {
            get { return writeGate; }
            private set { Set(ref writeGate, value); }
        }

        public string CardStatus
        {
            get { return cardStatus; }
            private set { Set(ref cardStatus, value); }
        }

        public string RosterStatus
        {
            get { return rosterStatus; }
            private set { Set(ref rosterStatus, value); }
        }

        public int SelectedTabIndex
        {
            get { return selectedTabIndex; }
            set { Set(ref selectedTabIndex, value); }
        }

        public string HookState { get { return "Hook: not installed"; } }
        public string PlanStatus { get { return "Plan: not running  |  no steps"; } }

        public void Refresh()
        {
            if (session != null) session.RequestRefresh();
        }

        public void ToggleAttach()
        {
            if (session == null) return;
            if (session.IsAttached)
                session.RequestDetach();
            else
                session.RequestAttach();
        }

        public void PlayAs(SeatItem seat)
        {
            if (seat == null || session == null || seat.SeatIndex < 0) return;
            if (seat.IsPc)
            {
                RosterStatus = seat.Name + " is already the player character.";
                return;
            }
            if (!session.RequestPlayerSwitch(seat.SeatIndex))
                RosterStatus = "Player switch was not accepted.";
            else
                RosterStatus = "Switching player...";
        }

        public void OpenInCharacter(SeatItem seat)
        {
            if (seat == null) return;
            SelectedSeat = seat;
            SelectedTabIndex = 1;
        }

        public void SetRelationship(SeatItem source, int feeling)
        {
            if (source == null || SelectedSeat == null || session == null)
                return;
            if (!session.RequestRelationshipFeeling(source.SeatIndex,
                SelectedSeat.SeatIndex, feeling))
                RosterStatus = "Relationship change was not accepted.";
            else
                RosterStatus = "Updating relationship...";
        }

        public void SetLover(SeatItem source, bool lovers)
        {
            if (source == null || SelectedSeat == null || session == null)
                return;
            if (!session.RequestLover(source.SeatIndex,
                SelectedSeat.SeatIndex, lovers))
                RosterStatus = "Lover change was not accepted.";
            else
                RosterStatus = lovers ? "Setting lovers..." : "Breaking up...";
        }

        public void ToggleRelationshipFreeze(SeatItem source)
        {
            if (source == null || SelectedSeat == null || session == null)
                return;
            bool frozen = !source.IsFrozen;
            if (!session.RequestRelationshipFreeze(source.SeatIndex,
                SelectedSeat.SeatIndex, frozen))
                RosterStatus = "Freeze change was not accepted.";
            else
                RosterStatus = frozen
                    ? "Freezing relationship..." : "Unfreezing relationship...";
        }

        private void OnPlayerSwitchCompleted(PlayerSwitchResult result)
        {
            if (result == null) return;
            if (result.Success)
            {
                string name = result.Seat >= 0 && result.Seat < Seats.Count
                    ? Seats[result.Seat].Name : "character";
                RosterStatus = "Now playing: " + name;
            }
            else
            {
                RosterStatus = "Player switch failed: " +
                    (string.IsNullOrEmpty(result.Error)
                        ? "unknown error" : result.Error);
            }
        }

        private void OnRosterActionCompleted(RosterActionResult result)
        {
            if (result == null) return;
            RosterStatus = result.Success
                ? result.Message
                : "Action failed: " +
                    (string.IsNullOrEmpty(result.Error)
                        ? "unknown error" : result.Error);
        }

        public void ApplyCharacterEdits()
        {
            if (session == null || CharacterDraft == null ||
                !CharacterDraft.IsDirty || characterWriteBusy)
                return;

            IsCharacterWriteBusy = true;
            CharacterStatus = "Writing...";

            bool queued = session.RequestCharacterWrite(
                CharacterDraft.Pointer, CharacterDraft.SeatIndex,
                CharacterDraft.Original, CharacterDraft.ToSnapshot());
            if (!queued)
            {
                IsCharacterWriteBusy = false;
                CharacterStatus = "Write request was not accepted.";
            }
        }

        public void RevertCharacterEdits()
        {
            if (CharacterDraft == null || liveCharacter == null) return;
            CharacterDraft.Load(liveCharacter, livePointer,
                CharacterDraft.SeatIndex);
            CharacterStatus = "Reverted to live values.";
        }

        private void OnCharacterWriteCompleted(CharacterWriteResult result)
        {
            IsCharacterWriteBusy = false;
            if (result == null) return;

            if (result.Success)
            {
                if (CharacterDraft != null &&
                    CharacterDraft.Pointer == result.Pointer)
                {
                    CharacterDraft.MarkSaved();
                }
                CharacterStatus = string.IsNullOrEmpty(result.Changed) ||
                    result.Changed == "No changes."
                    ? "No changes."
                    : "Saved: " + result.Changed;
            }
            else
            {
                CharacterStatus = "Write failed: " +
                    (string.IsNullOrEmpty(result.Error)
                        ? "unknown error" : result.Error);
            }
        }

        private void CharacterDraft_PropertyChanged(object sender,
            System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.PropertyName) &&
                e.PropertyName != nameof(CharacterDraft.IsDirty))
                return;

            OnPropertyChanged(nameof(CanApply));
            OnPropertyChanged(nameof(CanRevert));
            if (applyingSnapshot) return;
            if (characterWriteBusy) return;
            if (CharacterDraft != null && CharacterDraft.IsDirty)
                CharacterStatus = "Unsaved changes.";
            else
                CharacterStatus = "No changes.";
        }

        public void ApplyTraitEdits()
        {
            if (session == null || TraitDraft == null ||
                !TraitDraft.IsDirty || traitWriteBusy)
                return;

            IsTraitWriteBusy = true;
            TraitStatus = "Writing...";

            bool queued = session.RequestTraitWrite(
                TraitDraft.Pointer, TraitDraft.SeatIndex,
                TraitDraft.OriginalTraits, TraitDraft.ToTraitValues(),
                TraitDraft.OriginalRisk, TraitDraft.ToRiskValues());
            if (!queued)
            {
                IsTraitWriteBusy = false;
                TraitStatus = "Write request was not accepted.";
            }
        }

        public void RevertTraitEdits()
        {
            if (TraitDraft == null || liveTraits == null || liveRisk == null)
                return;
            TraitDraft.Load(liveTraits, liveRisk, livePointer,
                TraitDraft.SeatIndex);
            TraitStatus = "Reverted to live values.";
        }

        private void OnTraitWriteCompleted(TraitWriteResult result)
        {
            IsTraitWriteBusy = false;
            if (result == null) return;

            if (result.Success)
            {
                if (TraitDraft != null && TraitDraft.Pointer == result.Pointer)
                    TraitDraft.MarkSaved();
                TraitStatus = string.IsNullOrEmpty(result.Changed) ||
                    result.Changed == "0 trait(s), 0 risk cell(s)"
                    ? "No changes."
                    : "Saved: " + result.Changed;
            }
            else
            {
                TraitStatus = "Write failed: " +
                    (string.IsNullOrEmpty(result.Error)
                        ? "unknown error" : result.Error);
            }
        }

        private void TraitDraft_PropertyChanged(object sender,
            System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.PropertyName) &&
                e.PropertyName != nameof(TraitDraft.IsDirty))
                return;

            OnPropertyChanged(nameof(CanApplyTraits));
            OnPropertyChanged(nameof(CanRevertTraits));
            if (applyingSnapshot) return;
            if (traitWriteBusy) return;
            if (TraitDraft != null && TraitDraft.IsDirty)
                TraitStatus = "Unsaved changes.";
            else
                TraitStatus = "No changes.";
        }

        public void ApplyBodyEdits()
        {
            if (session == null || BodyDraft == null ||
                !BodyDraft.IsDirty || bodyWriteBusy)
                return;

            IsBodyWriteBusy = true;
            BodyStatus = "Writing...";

            bool queued = session.RequestBodyWrite(
                BodyDraft.Pointer, BodyDraft.SeatIndex,
                BodyDraft.OriginalValues, BodyDraft.ToValues());
            if (!queued)
            {
                IsBodyWriteBusy = false;
                BodyStatus = "Write request was not accepted.";
            }
        }

        public void RevertBodyEdits()
        {
            if (BodyDraft == null || liveBodyValues == null) return;
            BodyDraft.Load(liveBodyValues, livePointer, BodyDraft.SeatIndex);
            BodyStatus = "Reverted to live values.";
        }

        private void OnBodyWriteCompleted(BodyWriteResult result)
        {
            IsBodyWriteBusy = false;
            if (result == null) return;

            if (result.Success)
            {
                if (BodyDraft != null && BodyDraft.Pointer == result.Pointer)
                    BodyDraft.MarkSaved();
                BodyStatus = string.IsNullOrEmpty(result.Changed) ||
                    result.Changed == "No changes."
                    ? "No changes."
                    : "Saved: " + result.Changed;
            }
            else
            {
                BodyStatus = "Write failed: " +
                    (string.IsNullOrEmpty(result.Error)
                        ? "unknown error" : result.Error);
            }
        }

        private void BodyDraft_PropertyChanged(object sender,
            System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.PropertyName) &&
                e.PropertyName != nameof(BodyDraft.IsDirty))
                return;

            OnPropertyChanged(nameof(CanApplyBody));
            OnPropertyChanged(nameof(CanRevertBody));
            if (applyingSnapshot) return;
            if (bodyWriteBusy) return;
            if (BodyDraft != null && BodyDraft.IsDirty)
                BodyStatus = "Unsaved changes.";
            else
                BodyStatus = "No changes.";
        }

        public void ApplyFaceEdits()
        {
            if (session == null || FaceDraft == null ||
                !FaceDraft.IsDirty || faceWriteBusy)
                return;

            IsFaceWriteBusy = true;
            FaceStatus = "Writing...";

            bool queued = session.RequestFaceWrite(
                FaceDraft.Pointer, FaceDraft.SeatIndex,
                FaceDraft.OriginalValues, FaceDraft.ToValues(),
                FaceDraft.OriginalTexts, FaceDraft.ToTexts());
            if (!queued)
            {
                IsFaceWriteBusy = false;
                FaceStatus = "Write request was not accepted.";
            }
        }

        public void RevertFaceEdits()
        {
            if (FaceDraft == null || liveFaceValues == null) return;
            FaceDraft.Load(liveFaceValues, liveFaceTexts, livePointer,
                FaceDraft.SeatIndex);
            FaceStatus = "Reverted to live values.";
        }

        private void OnFaceWriteCompleted(FaceWriteResult result)
        {
            IsFaceWriteBusy = false;
            if (result == null) return;

            if (result.Success)
            {
                if (FaceDraft != null && FaceDraft.Pointer == result.Pointer)
                    FaceDraft.MarkSaved();
                FaceStatus = string.IsNullOrEmpty(result.Changed) ||
                    result.Changed == "No changes."
                    ? "No changes."
                    : "Saved: " + result.Changed;
            }
            else
            {
                FaceStatus = "Write failed: " +
                    (string.IsNullOrEmpty(result.Error)
                        ? "unknown error" : result.Error);
            }
        }

        private void FaceDraft_PropertyChanged(object sender,
            System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.PropertyName) &&
                e.PropertyName != nameof(FieldDraft.IsDirty))
                return;

            OnPropertyChanged(nameof(CanApplyFace));
            OnPropertyChanged(nameof(CanRevertFace));
            if (applyingSnapshot) return;
            if (faceWriteBusy) return;
            if (FaceDraft != null && FaceDraft.IsDirty)
                FaceStatus = "Unsaved changes.";
            else
                FaceStatus = "No changes.";
        }

        public void ApplyHairEdits()
        {
            if (session == null || HairDraft == null ||
                !HairDraft.IsDirty || hairWriteBusy)
                return;

            IsHairWriteBusy = true;
            HairStatus = "Writing...";

            bool queued = session.RequestHairWrite(
                HairDraft.Pointer, HairDraft.SeatIndex,
                HairDraft.OriginalValues, HairDraft.ToValues());
            if (!queued)
            {
                IsHairWriteBusy = false;
                HairStatus = "Write request was not accepted.";
            }
        }

        public void RevertHairEdits()
        {
            if (HairDraft == null || liveHairValues == null) return;
            HairDraft.Load(liveHairValues, null, livePointer,
                HairDraft.SeatIndex);
            HairStatus = "Reverted to live values.";
        }

        private void OnHairWriteCompleted(HairWriteResult result)
        {
            IsHairWriteBusy = false;
            if (result == null) return;

            if (result.Success)
            {
                if (HairDraft != null && HairDraft.Pointer == result.Pointer)
                    HairDraft.MarkSaved();
                HairStatus = string.IsNullOrEmpty(result.Changed) ||
                    result.Changed == "No changes."
                    ? "No changes."
                    : "Saved: " + result.Changed;
            }
            else
            {
                HairStatus = "Write failed: " +
                    (string.IsNullOrEmpty(result.Error)
                        ? "unknown error" : result.Error);
            }
        }

        private void HairDraft_PropertyChanged(object sender,
            System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.PropertyName) &&
                e.PropertyName != nameof(FieldDraft.IsDirty))
                return;

            OnPropertyChanged(nameof(CanApplyHair));
            OnPropertyChanged(nameof(CanRevertHair));
            if (applyingSnapshot) return;
            if (hairWriteBusy) return;
            if (HairDraft != null && HairDraft.IsDirty)
                HairStatus = "Unsaved changes.";
            else
                HairStatus = "No changes.";
        }

        public void ApplyOutfitEdits()
        {
            if (session == null || OutfitDraft == null ||
                !OutfitDraft.IsDirty || outfitWriteBusy)
                return;

            IsOutfitWriteBusy = true;
            OutfitStatus = "Writing...";

            bool queued = session.RequestOutfitWrite(
                OutfitDraft.Pointer, OutfitDraft.SeatIndex,
                OutfitDraft.OriginalValues, OutfitDraft.ToValues());
            if (!queued)
            {
                IsOutfitWriteBusy = false;
                OutfitStatus = "Write request was not accepted.";
            }
        }

        public void RevertOutfitEdits()
        {
            if (OutfitDraft == null || liveOutfitValues == null) return;
            OutfitDraft.Load(liveOutfitValues, livePointer,
                OutfitDraft.SeatIndex);
            OutfitStatus = "Reverted to live values.";
        }

        private void OnOutfitWriteCompleted(OutfitWriteResult result)
        {
            IsOutfitWriteBusy = false;
            if (result == null) return;

            if (result.Success)
            {
                if (OutfitDraft != null &&
                    OutfitDraft.Pointer == result.Pointer)
                {
                    OutfitDraft.MarkSaved();
                }
                OutfitStatus = string.IsNullOrEmpty(result.Changed) ||
                    result.Changed == "No changes."
                    ? "No changes."
                    : "Saved: " + result.Changed;
            }
            else
            {
                OutfitStatus = "Write failed: " +
                    (string.IsNullOrEmpty(result.Error)
                        ? "unknown error" : result.Error);
            }
        }

        private void OutfitDraft_PropertyChanged(object sender,
            System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.PropertyName) &&
                e.PropertyName != nameof(OutfitDraft.IsDirty))
                return;

            OnPropertyChanged(nameof(CanApplyOutfit));
            OnPropertyChanged(nameof(CanRevertOutfit));
            if (applyingSnapshot) return;
            if (outfitWriteBusy) return;
            if (OutfitDraft != null && OutfitDraft.IsDirty)
                OutfitStatus = "Unsaved changes.";
            else
                OutfitStatus = "No changes.";
        }

        public void ApplyScheduleEdits()
        {
            if (session == null || ScheduleDraft == null ||
                !ScheduleDraft.IsDirty || scheduleWriteBusy)
                return;

            IsScheduleWriteBusy = true;
            ScheduleStatus = "Writing...";

            bool queued = session.RequestScheduleWrite(
                liveSchedule, ScheduleDraft.ToSnapshot());
            if (!queued)
            {
                IsScheduleWriteBusy = false;
                ScheduleStatus = "Write request was not accepted.";
            }
        }

        public void RevertScheduleEdits()
        {
            if (ScheduleDraft == null || liveSchedule == null) return;
            ScheduleDraft.Load(liveSchedule);
            ScheduleStatus = "Reverted to live values.";
        }

        public void SetTimeFrozen(bool frozen)
        {
            if (session == null) return;
            if (frozen)
            {
                int milliseconds = ScheduleDraft != null
                    ? ScheduleDraft.TimeMilliseconds
                    : (liveSchedule != null ? liveSchedule.TimeMilliseconds : 0);
                if (session.RequestTimeFreeze(milliseconds))
                    IsTimeFrozen = true;
            }
            else
            {
                session.RequestTimeUnfreeze();
                IsTimeFrozen = false;
            }
        }

        private void OnScheduleWriteCompleted(ScheduleWriteResult result)
        {
            IsScheduleWriteBusy = false;
            if (result == null) return;

            if (result.Success)
            {
                if (ScheduleDraft != null) ScheduleDraft.MarkSaved();
                ScheduleStatus = string.IsNullOrEmpty(result.Changed) ||
                    result.Changed == "No changes."
                    ? "No changes."
                    : "Saved: " + result.Changed;
            }
            else
            {
                ScheduleStatus = "Write failed: " +
                    (string.IsNullOrEmpty(result.Error)
                        ? "unknown error" : result.Error);
            }
        }

        private void ScheduleDraft_PropertyChanged(object sender,
            System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.PropertyName) &&
                e.PropertyName != nameof(ScheduleDraft.IsDirty) &&
                e.PropertyName != nameof(ScheduleDraft.TimeMilliseconds))
                return;

            OnPropertyChanged(nameof(CanApplySchedule));
            OnPropertyChanged(nameof(CanRevertSchedule));
            if (applyingSnapshot) return;
            if (scheduleWriteBusy) return;
            if (ScheduleDraft != null && ScheduleDraft.IsDirty)
                ScheduleStatus = "Unsaved changes.";
            else
                ScheduleStatus = "No changes.";
        }

        public void Dispose()
        {
            disposed = true;
            if (session == null) return;
            session.SnapshotReady -= ApplySnapshot;
            session.CharacterWriteCompleted -= OnCharacterWriteCompleted;
            session.TraitWriteCompleted -= OnTraitWriteCompleted;
            session.BodyWriteCompleted -= OnBodyWriteCompleted;
            session.FaceWriteCompleted -= OnFaceWriteCompleted;
            session.HairWriteCompleted -= OnHairWriteCompleted;
            session.OutfitWriteCompleted -= OnOutfitWriteCompleted;
            session.PlayerSwitchCompleted -= OnPlayerSwitchCompleted;
            session.RosterActionCompleted -= OnRosterActionCompleted;
            session.ScheduleWriteCompleted -= OnScheduleWriteCompleted;
            session.Dispose();
        }

        private void ApplySnapshot(SessionSnapshot snapshot)
        {
            if (snapshot == null) return;

            applyingSnapshot = true;
            try
            {
                IsAttached = snapshot.IsAttached;
                AttachState = snapshot.AttachState;
                GameTime = snapshot.GameTime;
                PointerHealth = snapshot.PointerHealth;
                NamesState = snapshot.NamesState;
                WriteGate = snapshot.WriteGate;
                UpdatedText = snapshot.UpdatedText;
                SelectedCharacter = snapshot.Character;
                if (snapshot.Options != null) CharacterOptions = snapshot.Options;
                UpdateCharacterDraft(snapshot);
                UpdateTraitDraft(snapshot);
                UpdateBodyDraft(snapshot);
                UpdateFaceDraft(snapshot);
                UpdateHairDraft(snapshot);
                UpdateOutfitDraft(snapshot);
                UpdateScheduleDraft(snapshot);
                IsTimeFrozen = snapshot.TimeFrozen;

                for (int i = 0; i < Seats.Count; i++)
                {
                    if (i >= snapshot.Seats.Count) break;
                    SeatSnapshot seatSnapshot = snapshot.Seats[i];
                    SeatItem seat = Seats[i];
                    if (seatSnapshot == null) continue;

                    seat.ApplyLive(
                        seatSnapshot.Name,
                        seatSnapshot.FamilyName,
                        seatSnapshot.GivenName,
                        seatSnapshot.Room,
                        seatSnapshot.Action,
                        seatSnapshot.Status,
                        seatSnapshot.Relationship,
                        seatSnapshot.HeaderRelationship,
                        seatSnapshot.IsPlayer,
                        seatSnapshot.HasRelationship,
                        seatSnapshot.IsLover,
                        seatSnapshot.OtherIsLover,
                        seatSnapshot.IsFrozen);
                }

                ApplyRosterFilter();

                string signature = BuildRosterSignature();
                if (!string.Equals(signature, rosterSignature,
                    StringComparison.Ordinal))
                {
                    rosterSignature = signature;
                    AssignCards();
                }

                if (snapshot.SelectedSeat >= 0 &&
                    snapshot.SelectedSeat < Seats.Count)
                {
                    SelectedSeat = Seats[snapshot.SelectedSeat];
                }
            }
            finally
            {
                applyingSnapshot = false;
            }
        }

        private void UpdateCharacterDraft(SessionSnapshot snapshot)
        {
            if (snapshot.RawCharacter == null || snapshot.SelectedPointer == 0)
            {
                liveCharacter = null;
                livePointer = 0;
                CharacterDraft = null;
                CharacterStatus = "No character selected.";
                return;
            }

            bool rawChanged = !ReferenceEquals(liveCharacter, snapshot.RawCharacter);
            bool sameCharacter = CharacterDraft != null &&
                CharacterDraft.Pointer == snapshot.SelectedPointer;

            liveCharacter = snapshot.RawCharacter;
            livePointer = snapshot.SelectedPointer;

            if (!sameCharacter)
            {
                CharacterDraft draft = new CharacterDraft();
                draft.Load(snapshot.RawCharacter, snapshot.SelectedPointer,
                    snapshot.SelectedSeat);
                CharacterDraft = draft;
                CharacterStatus = "No changes.";
            }
            else if (!CharacterDraft.IsDirty && rawChanged)
            {
                CharacterDraft.Load(snapshot.RawCharacter, snapshot.SelectedPointer,
                    snapshot.SelectedSeat);
            }
        }

        private void UpdateTraitDraft(SessionSnapshot snapshot)
        {
            if (snapshot.SelectedPointer == 0 ||
                (snapshot.TraitValues == null && snapshot.RiskValues == null))
            {
                liveTraits = null;
                liveRisk = null;
                TraitDraft = null;
                TraitStatus = "No character selected.";
                return;
            }

            bool rawChanged = !ReferenceEquals(liveTraits, snapshot.TraitValues) ||
                !ReferenceEquals(liveRisk, snapshot.RiskValues);
            bool sameCharacter = TraitDraft != null &&
                TraitDraft.Pointer == snapshot.SelectedPointer;

            liveTraits = snapshot.TraitValues;
            liveRisk = snapshot.RiskValues;

            if (!sameCharacter)
            {
                TraitDraft draft = new TraitDraft();
                draft.Load(snapshot.TraitValues, snapshot.RiskValues,
                    snapshot.SelectedPointer, snapshot.SelectedSeat);
                TraitDraft = draft;
                TraitStatus = "No changes.";
            }
            else if (!TraitDraft.IsDirty && rawChanged)
            {
                TraitDraft.Load(snapshot.TraitValues, snapshot.RiskValues,
                    snapshot.SelectedPointer, snapshot.SelectedSeat);
            }
        }

        private void UpdateBodyDraft(SessionSnapshot snapshot)
        {
            if (snapshot.SelectedPointer == 0 || snapshot.BodyValues == null)
            {
                liveBodyValues = null;
                BodyDraft = null;
                BodyStatus = "No character selected.";
                return;
            }

            bool rawChanged = !ReferenceEquals(liveBodyValues, snapshot.BodyValues);
            bool sameCharacter = BodyDraft != null &&
                BodyDraft.Pointer == snapshot.SelectedPointer;

            liveBodyValues = snapshot.BodyValues;

            if (!sameCharacter)
            {
                BodyDraft draft = new BodyDraft(CharacterOptions);
                draft.Load(snapshot.BodyValues, snapshot.SelectedPointer,
                    snapshot.SelectedSeat);
                BodyDraft = draft;
                BodyStatus = "No changes.";
            }
            else if (!BodyDraft.IsDirty && rawChanged)
            {
                BodyDraft.Load(snapshot.BodyValues, snapshot.SelectedPointer,
                    snapshot.SelectedSeat);
            }
        }

        private void UpdateFaceDraft(SessionSnapshot snapshot)
        {
            if (snapshot.SelectedPointer == 0 || snapshot.FaceValues == null)
            {
                liveFaceValues = null;
                liveFaceTexts = null;
                FaceDraft = null;
                FaceStatus = "No character selected.";
                return;
            }

            bool rawChanged =
                !ReferenceEquals(liveFaceValues, snapshot.FaceValues) ||
                !ReferenceEquals(liveFaceTexts, snapshot.FaceTexts);
            bool sameCharacter = FaceDraft != null &&
                FaceDraft.Pointer == snapshot.SelectedPointer;

            liveFaceValues = snapshot.FaceValues;
            liveFaceTexts = snapshot.FaceTexts;

            if (!sameCharacter)
            {
                FieldDraft draft = new FieldDraft(AppearanceFields.Face(),
                    FaceChoice);
                draft.Load(snapshot.FaceValues, snapshot.FaceTexts,
                    snapshot.SelectedPointer, snapshot.SelectedSeat);
                FaceDraft = draft;
                FaceStatus = "No changes.";
            }
            else if (!FaceDraft.IsDirty && rawChanged)
            {
                FaceDraft.Load(snapshot.FaceValues, snapshot.FaceTexts,
                    snapshot.SelectedPointer, snapshot.SelectedSeat);
            }
        }

        private void UpdateHairDraft(SessionSnapshot snapshot)
        {
            if (snapshot.SelectedPointer == 0 || snapshot.HairValues == null)
            {
                liveHairValues = null;
                HairDraft = null;
                HairStatus = "No character selected.";
                return;
            }

            bool rawChanged = !ReferenceEquals(liveHairValues, snapshot.HairValues);
            bool sameCharacter = HairDraft != null &&
                HairDraft.Pointer == snapshot.SelectedPointer;

            liveHairValues = snapshot.HairValues;

            if (!sameCharacter)
            {
                FieldDraft draft = new FieldDraft(AppearanceFields.Hair(),
                    HairChoice);
                draft.Load(snapshot.HairValues, null,
                    snapshot.SelectedPointer, snapshot.SelectedSeat);
                HairDraft = draft;
                HairStatus = "No changes.";
            }
            else if (!HairDraft.IsDirty && rawChanged)
            {
                HairDraft.Load(snapshot.HairValues, null,
                    snapshot.SelectedPointer, snapshot.SelectedSeat);
            }
        }

        private void UpdateOutfitDraft(SessionSnapshot snapshot)
        {
            if (snapshot.SelectedPointer == 0 || snapshot.OutfitValues == null)
            {
                liveOutfitValues = null;
                OutfitDraft = null;
                OutfitStatus = "No character selected.";
                return;
            }

            bool rawChanged =
                !ReferenceEquals(liveOutfitValues, snapshot.OutfitValues);
            bool sameCharacter = OutfitDraft != null &&
                OutfitDraft.Pointer == snapshot.SelectedPointer;

            liveOutfitValues = snapshot.OutfitValues;

            if (!sameCharacter)
            {
                OutfitDraft draft = new OutfitDraft(CharacterOptions);
                draft.Load(snapshot.OutfitValues, snapshot.SelectedPointer,
                    snapshot.SelectedSeat);
                OutfitDraft = draft;
                OutfitStatus = "No changes.";
            }
            else if (!OutfitDraft.IsDirty && rawChanged)
            {
                OutfitDraft.Load(snapshot.OutfitValues,
                    snapshot.SelectedPointer, snapshot.SelectedSeat);
            }
        }

        private void UpdateScheduleDraft(SessionSnapshot snapshot)
        {
            liveSchedule = snapshot.Schedule;
            if (snapshot.Schedule == null)
            {
                ScheduleDraft = null;
                ScheduleStatus = "No schedule data.";
                return;
            }

            if (ScheduleDraft == null)
            {
                ScheduleDraft draft = new ScheduleDraft();
                draft.Load(snapshot.Schedule);
                ScheduleDraft = draft;
                ScheduleStatus = "No changes.";
            }
            else if (!ScheduleDraft.IsDirty)
            {
                ScheduleDraft.Load(snapshot.Schedule);
            }
        }

        private List<CharacterOption> FaceChoice(string name)
        {
            List<CharacterOption> list;
            return CharacterOptions != null &&
                CharacterOptions.FaceChoices.TryGetValue(name, out list)
                ? list : null;
        }

        private List<CharacterOption> HairChoice(string name)
        {
            List<CharacterOption> list;
            return CharacterOptions != null &&
                CharacterOptions.HairChoices.TryGetValue(name, out list)
                ? list : null;
        }

        private void ApplyRosterFilter()
        {
            string filter = (rosterFilter ?? "").Trim();
            bool hasFilter = filter.Length > 0 || seatFilterIndex > 0;

            foreach (SeatItem seat in Seats)
            {
                bool typeMatch = seatFilterIndex == 1
                    ? seat.SeatIndex != 24
                    : seatFilterIndex == 2
                        ? seat.SeatIndex == 24
                        : true;
                bool textMatch = filter.Length == 0 ||
                    MatchesFilter(seat, filter);
                seat.IsFilteredOut = hasFilter && !(typeMatch && textMatch);
            }
        }

        private static bool MatchesFilter(SeatItem seat, string filter)
        {
            return Contains(seat.Name, filter) ||
                   Contains(seat.Room, filter) ||
                   Contains(seat.Action, filter) ||
                   Contains(seat.Status, filter) ||
                   Contains(seat.Relationship, filter) ||
                   Contains(seat.Seat, filter);
        }

        private static bool Contains(string value, string filter)
        {
            return !string.IsNullOrEmpty(value) &&
                value.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private string BuildRosterSignature()
        {
            StringBuilder builder = new StringBuilder(25 * 24);
            foreach (SeatItem seat in Seats)
            {
                builder.Append(seat.SeatIndex);
                builder.Append(':');
                builder.Append(seat.Name);
                builder.Append('|');
            }
            return builder.ToString();
        }

        private async void LoadCardsAsync()
        {
            string[] roots = FindCardRoots();
            if (roots.Length == 0)
            {
                CardStatus = "Cards: none";
                return;
            }

            List<string> files = new List<string>();
            foreach (string root in roots)
            {
                try
                {
                    string[] found = Directory.GetFiles(
                        root, "*.png", SearchOption.TopDirectoryOnly);
                    Array.Sort(found, StringComparer.OrdinalIgnoreCase);
                    files.AddRange(found);
                }
                catch { }
            }

            if (files.Count == 0)
            {
                CardStatus = "Cards: none";
                return;
            }

            CardStatus = "Cards: loading...";
            List<CardEntry> entries;
            try
            {
                entries = await Task.Run(delegate
                {
                    List<CardEntry> result = new List<CardEntry>();
                    HashSet<string> seen = new HashSet<string>(
                        StringComparer.OrdinalIgnoreCase);
                    foreach (string file in files)
                    {
                        CardEntry entry = CardImageLoader.ReadEntry(file);
                        if (entry == null) continue;
                        if (!seen.Add(entry.Key)) continue;
                        result.Add(entry);
                    }
                    return result;
                });
            }
            catch
            {
                CardStatus = "Cards: index failed";
                return;
            }

            if (disposed || Application.Current == null) return;
            Application.Current.Dispatcher.Invoke(delegate
            {
                if (disposed) return;
                cardIndex.Clear();
                cardIndex.AddRange(entries);
                loadedCards.Clear();
                failedCards.Clear();
                if (cardIndex.Count == 0)
                {
                    CardStatus = "Cards: none";
                    return;
                }
                AssignCards();
            });
        }

        /// <summary>
        /// Matches cards to live names exactly. Card names are never applied to
        /// seats and are never translated; unmatched seats keep the initial
        /// tile. Images are decoded only for matched cards.
        /// </summary>
        private void AssignCards()
        {
            if (disposed || cardIndex.Count == 0) return;

            HashSet<CardEntry> used = new HashSet<CardEntry>();
            List<CardEntry> pending = new List<CardEntry>();
            int shown = 0;

            foreach (SeatItem seat in Seats)
            {
                if (!seat.HasName)
                {
                    ClearCardArt(seat);
                    continue;
                }

                CardEntry match = cardIndex.FirstOrDefault(
                    card => !used.Contains(card) &&
                            !failedCards.Contains(card.Path) &&
                            NamesMatch(seat, card));
                if (match == null)
                {
                    ClearCardArt(seat);
                    continue;
                }

                used.Add(match);

                CardImages images;
                if (loadedCards.TryGetValue(match.Path, out images))
                {
                    AssignImages(seat, images);
                    shown++;
                }
                else
                {
                    ClearCardArt(seat);
                    pending.Add(match);
                }
            }

            if (pending.Count > 0) LoadCardImages(pending);

            CardStatus = "Cards: " + shown + " shown";
        }

        private void LoadCardImages(List<CardEntry> entries)
        {
            if (cardLoadRunning || entries.Count == 0) return;
            cardLoadRunning = true;

            List<CardEntry> toLoad = new List<CardEntry>(entries);
            Task.Run(delegate
            {
                List<KeyValuePair<CardEntry, CardImages>> loaded =
                    new List<KeyValuePair<CardEntry, CardImages>>();
                List<string> failed = new List<string>();

                foreach (CardEntry entry in toLoad)
                {
                    CardImages images = CardImageLoader.Load(entry.Path);
                    if (images == null)
                        failed.Add(entry.Path);
                    else
                        loaded.Add(new KeyValuePair<CardEntry, CardImages>(
                            entry, images));
                }

                if (disposed || Application.Current == null) return;
                Application.Current.Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (disposed) return;
                    foreach (KeyValuePair<CardEntry, CardImages> pair in loaded)
                        loadedCards[pair.Key.Path] = pair.Value;
                    foreach (string path in failed)
                        failedCards.Add(path);
                    cardLoadRunning = false;
                    AssignCards();
                }));
            });
        }

        private static void ClearCardArt(SeatItem seat)
        {
            seat.RosterImage = null;
            seat.CardFaceImage = null;
        }

        private static string[] FindCardRoots()
        {
            List<string> roots = new List<string>();
            string local = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "Cards");
            AddCardRoot(roots, local);
            AddCardRoot(roots, Path.Combine(local, "Female"));
            AddCardRoot(roots, Path.Combine(local, "Male"));

            string gameRoot = NameMap.FindGameRoot();
            if (!string.IsNullOrEmpty(gameRoot))
            {
                string save = Path.Combine(gameRoot, "data", "save");
                AddCardRoot(roots, Path.Combine(save, "Female"));
                AddCardRoot(roots, Path.Combine(save, "Male"));
                AddCardRoot(roots, Path.Combine(
                    save, "Male", "Illusion Default"));
            }

            return roots.ToArray();
        }

        private static void AddCardRoot(List<string> roots, string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;
            if (roots.Any(root => string.Equals(root, path,
                StringComparison.OrdinalIgnoreCase))) return;
            roots.Add(path);
        }

        private static void AssignImages(SeatItem seat, CardImages card)
        {
            seat.RosterImage = CardImageLoader.ToImage(card.RosterBytes, 136);
            seat.CardFaceImage = CardImageLoader.ToImage(card.FaceBytes, 240);
        }

        private static bool NamesMatch(SeatItem seat, CardEntry card)
        {
            if (Same(seat.Name, card.Name)) return true;
            return (Same(seat.FirstName, card.FirstName) &&
                    Same(seat.LastName, card.LastName)) ||
                   (Same(seat.FirstName, card.LastName) &&
                    Same(seat.LastName, card.FirstName));
        }

        private static bool Same(string left, string right)
        {
            if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
                return false;
            return string.Equals(left.Trim(), right.Trim(),
                StringComparison.OrdinalIgnoreCase);
        }

        private void SeedSeats()
        {
            for (int i = 0; i < 25; i++)
            {
                Seats.Add(new SeatItem
                {
                    SeatIndex = i,
                    Seat = i.ToString("00"),
                    IsTeacher = i == 24,
                    Status = "Empty",
                    Action = "None",
                    Relationship = "-",
                    HeaderRelationship = "-",
                    Mood = "-",
                    Energy = "-",
                    Study = "-",
                    Training = "-",
                    Club = "-"
                });
            }
        }

        private void SeedPlan()
        {
            PlanSteps.Add(new PlanStepItem
            {
                Number = 1,
                Description = "Pet -> seat 00",
                State = "done"
            });
            PlanSteps.Add(new PlanStepItem
            {
                Number = 2,
                Description = "Hug -> seat 00",
                State = "skipped",
                Enabled = false,
                Note = "disabled"
            });
            PlanSteps.Add(new PlanStepItem
            {
                Number = 3,
                Description = "Encourage -> seat 00",
                State = "running",
                Note = "queued for key 0x211B11C8"
            });
            PlanSteps.Add(new PlanStepItem
            {
                Number = 4,
                Description = "Talk About Love -> seat 00",
                State = "pending"
            });
        }

        private void SeedLogs()
        {
            string[] live =
            {
                "seat 00  move=7 action=23 partner=03 conv=0 talk=1 loc=19",
                "seat 03  move=7 action=23 partner=00 conv=1 talk=0 loc=19",
                "seat 01  move=1 action=-1 partner=00 conv=0 talk=0 loc=19",
                "seat 02  move=2 action=-1 partner=- conv=0 talk=0 loc=21",
                "seat 03  move=3 action=44 partner=00 conv=0 talk=0 loc=10",
                "seat 00  move=7 action=-1 partner=03 conv=0 talk=0 loc=10",
                "seat 03  move=0 action=-1 partner=- conv=0 talk=0 loc=10",
                "seat 02  move=2 action=108 partner=- conv=0 talk=0 loc=1",
                "seat 01  move=8 action=-1 partner=00 conv=0 talk=0 loc=19"
            };
            for (int i = 0; i < live.Length; i++)
            {
                string time = "12:34:" + (10 + i).ToString("00") + "." +
                    (120 + i * 7).ToString("000");
                LiveLines.Add(new LogLine(time, live[i]));
            }

            PlannerLines.Add(new LogLine("12:34:10.120",
                "Plan started for seat 03 (4 steps, Forced perform)."));
            PlannerLines.Add(new LogLine("12:34:10.220",
                "  seat 03 step 1/4: queued for key 0x211B11C8 (slot 0)"));
            PlannerLines.Add(new LogLine("12:34:10.340",
                "  seat 03 direct perform action 44 in place."));
            PlannerLines.Add(new LogLine("12:34:10.460",
                "  seat 03 step 1/4 done: Pet -> seat 00."));
            PlannerLines.Add(new LogLine("12:34:10.480",
                "  seat 03 skipped 1 disabled step(s)."));
            PlannerLines.Add(new LogLine("12:34:11.020",
                "  seat 03 step 3/4: queued for key 0x211B11C8 (slot 0)"));
            PlannerLines.Add(new LogLine("12:34:11.140",
                "  seat 03 step 3/4 running: Encourage -> seat 00."));
        }

        private void SeedDebug()
        {
            DebugFields.Add(new DebugFieldItem
            {
                Name = "Status", Address = "0x21DE2E28", Type = "ptr",
                Value = "0x21DE2F10", Note = "character status"
            });
            DebugFields.Add(new DebugFieldItem
            {
                Name = "Action", Address = "0x21DE2F50", Type = "i32",
                Value = "44", Note = "Pet"
            });
            DebugFields.Add(new DebugFieldItem
            {
                Name = "MovementType", Address = "0x21DE2F4C", Type = "i32",
                Value = "3", Note = "walk to character"
            });
            DebugFields.Add(new DebugFieldItem
            {
                Name = "Location", Address = "0x21DE2F58", Type = "i32",
                Value = "10", Note = "Outside Toilets"
            });
            DebugFields.Add(new DebugFieldItem
            {
                Name = "Response", Address = "0x21DE2F6C", Type = "i32",
                Value = "1", Note = "Yes"
            });
            DebugFields.Add(new DebugFieldItem
            {
                Name = "TargetNpc", Address = "0x21DE2F7C", Type = "u32",
                Value = "0x211B11C8", Note = "activity"
            });
            DebugFields.Add(new DebugFieldItem
            {
                Name = "WalkTarget", Address = "0x21DE2F80", Type = "u32",
                Value = "0x21DE2E58", Note = "interface"
            });
            DebugFields.Add(new DebugFieldItem
            {
                Name = "RoomPointer", Address = "0x21DE2F84", Type = "u32",
                Value = "0x21DE32A0", Note = "room block"
            });
            DebugFields.Add(new DebugFieldItem
            {
                Name = "AnimLocked", Address = "0x21DE2F24", Type = "u8",
                Value = "0", Note = "byte"
            });
            DebugFields.Add(new DebugFieldItem
            {
                Name = "InConversation", Address = "0x21DE2F29", Type = "u8",
                Value = "1", Note = "byte"
            });
        }
    }
}
