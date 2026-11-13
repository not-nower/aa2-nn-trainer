using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;

namespace AA2Trainer.WpfPrototype
{
    public sealed class MainViewModel : ObservableObject
    {
        private const int LogLineLimit = 5000;
        private const int LogTrimSlack = 250;
        private const int LogDrainPerTick = 256;
        private const int LogPendingLimit = 10000;

        private readonly GameSessionService session;
        private readonly LogBuffer liveLogBuffer =
            new LogBuffer(LogPendingLimit);
        private readonly LogBuffer plannerLogBuffer =
            new LogBuffer(LogPendingLimit);
        private readonly List<LogLine> liveLogBatch =
            new List<LogLine>(LogDrainPerTick);
        private readonly List<LogLine> plannerLogBatch =
            new List<LogLine>(LogDrainPerTick);
        private readonly Dictionary<int, string> liveEventSignatures =
            new Dictionary<int, string>();
        private int liveEventRosterVersion = -1;
        private bool liveEventStarted;
        private DispatcherTimer logFlushTimer;
        private readonly List<CardEntry> cardIndex = new List<CardEntry>();
        private readonly Dictionary<string, CardImages> loadedCards =
            new Dictionary<string, CardImages>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> failedCards =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool cardLoadRunning;
        private bool cardIndexRunning;
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
        private CharacterOptions faceOptionsSource;
        private CharacterOptions outfitOptionsSource;
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
        private string cardStatus = "Portraits: not loaded";
        private string rosterStatus = "";
        private string rosterSignature = "";
        private string seatRailSignature = "";
        private int namedSeatCount;
        private string rosterFilter = "";
        private int seatFilterIndex;
        private int selectedTabIndex;
        private string rosterEmptyText = "";
        private string limitEmptyText = "";
        private string relationshipEmptyText = "";
        private string loverEmptyText = "";
        private string promiseEmptyText = "";
        private string debugEmptyText = "";
        private string lastSaveName = "";
        private string currentSavePath = "";
        private TrainerSettings trainerSettings;
        private string backupStatus = "No save selected.";
        private string nameTableStatus = "";
        private string aauModuleStatus = "No AAU card data";
        private string aauModuleSignature = "";
        private static readonly int[] RefreshIntervalChoices =
            new int[] { 100, 250, 500, 1000 };
        private PlannerSeatOption selectedPlannerTarget;
        private PlannerSeatOption selectedPlannerAbout;
        private CharacterOption selectedPlannerAction;
        private CharacterOption selectedPlannerRoom;
        private CharacterOptions plannerOptionsSource;
        private string plannerSeatSignature = "";
        private string plannerStatus = "Plan: not running";
        private string plannerHookState = "Hook: not installed";
        private string plannerHookButtonText = "Install hook";
        private bool plannerHookInstalled;
        private bool plannerObserveOnly;
        private bool plannerRunning;
        private int plannerModeIndex = 2;
        private int plannerTriggerIndex;
        private bool plannerRepeat;
        private bool plannerShuffle;
        private int selectedPlannerStepIndex = -1;
        private bool limitEnabled;
        private bool limitApplyToPc;
        private string limitStatus = "Blocked: 0";
        private string limitTargetText = "Limits: no character";
        private string limitCharacterStatus = "";
        private string limitCopyStatus = "";
        private string limitFilter = "";
        private ICollectionView limitActionsView;
        private int[] limitClipboard;
        private bool canPasteLimits;
        private OptionSnapshot liveGameOptions;
        private string gameOptionsStatus = "Game options: waiting for the game.";
        private bool gameOptionsBusy;
        private readonly Dictionary<int, GameOptionItem> gameOptionItems =
            new Dictionary<int, GameOptionItem>();
        private string gameplayTableSignature = "";
        private int selectedRelationshipIndex = -1;
        private int selectedBehaviorIndex = -1;
        private int selectedLoverIndex = -1;
        private BehaviorDraft behaviorDraft;
        private bool gameplayWriteBusy;
        private string relationshipEditStatus = "No changes.";
        private string behaviorEditStatus = "No changes.";
        private string behaviorEmptyText = "";
        private string loverEditStatus = "No changes.";
        private string debugFilter = "";
        private int debugTypeIndex;
        private int debugCategoryIndex;
        private bool debugOnlyProblems;
        private ICollectionView debugFieldsView;
        private DebugFieldItem selectedDebugField;
        private string debugDetails = "Select a row to inspect its pointer chain.";
        private string debugCountText = "";
        private List<DebugFieldItem> debugSource;
        private static readonly string[] DebugCategoryChoices =
            new string[]
            {
                "All categories", "Overview", "Character", "Relationships",
                "Activity", "Mood", "Promises", "Class", "Config", "Pointer"
            };

        public MainViewModel()
        {
            Seats = new ObservableCollection<SeatItem>();
            PlanSteps = new ObservableCollection<PlanStepItem>();
            PlannerSteps = new ObservableCollection<PlannerStepItem>();
            PlannerTargetOptions = new ObservableCollection<PlannerSeatOption>();
            PlannerAboutOptions = new ObservableCollection<PlannerSeatOption>();
            PlannerActions = new ObservableCollection<CharacterOption>();
            PlannerRooms = new ObservableCollection<CharacterOption>();
            LimitActions = new ObservableCollection<LimitActionItem>();
            limitActionsView = CollectionViewSource.GetDefaultView(LimitActions);
            limitActionsView.Filter = FilterLimitAction;
            RelationshipRows = new ObservableCollection<RelationshipRowItem>();
            BehaviorRows = new ObservableCollection<BehaviorRowItem>();
            LoverRows = new ObservableCollection<LoverRowItem>();
            PromiseRows = new ObservableCollection<PromiseRowItem>();
            SystemOptionSections = new ObservableCollection<GameOptionSection>();
            SoundChannels = new ObservableCollection<SoundChannelItem>();
            Additional1OptionSections = new ObservableCollection<GameOptionSection>();
            Additional2OptionSections = new ObservableCollection<GameOptionSection>();
            Additional3OptionSections = new ObservableCollection<GameOptionSection>();
            LiveLines = new ObservableCollection<LogLine>();
            PlannerLines = new ObservableCollection<LogLine>();
            DebugFields = new ObservableCollection<DebugFieldItem>();
            debugFieldsView = CollectionViewSource.GetDefaultView(DebugFields);
            debugFieldsView.Filter = FilterDebugField;
            AauModules = new ObservableCollection<AauModuleItem>();

            SeedSeats();
            SeatRailView = new ListCollectionView(Seats);
            SeatRailView.Filter = IsNamedSeat;
            selectedSeat = Seats[0];
            SeedPlan();
            SeedLogs();
            BuildGameOptionSections();

            trainerSettings = TrainerSettings.Current;

            Dispatcher dispatcher = Application.Current != null
                ? Application.Current.Dispatcher
                : Dispatcher.CurrentDispatcher;
            session = new GameSessionService(dispatcher);
            session.LiveRefreshEnabled = trainerSettings.LiveRefreshEnabled;
            session.RefreshIntervalMs = trainerSettings.RefreshIntervalMs;
            session.LiveIntervalAuto = trainerSettings.LiveIntervalAuto;
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
            session.OptionWriteCompleted += OnOptionWriteCompleted;
            session.GameplayWriteCompleted += OnGameplayWriteCompleted;
            session.LiveLog += OnLiveLog;
            session.Planner.Log += OnPlannerLog;
            session.Planner.StateChanged += RefreshPlannerNow;

            logFlushTimer = new DispatcherTimer(DispatcherPriority.Background,
                dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            logFlushTimer.Tick += delegate { FlushLogs(); };
            logFlushTimer.Start();

            LoadCardsAsync();
        }

        public ObservableCollection<SeatItem> Seats { get; private set; }
        public ICollectionView SeatRailView { get; private set; }

        public string SeatRailStatus
        {
            get
            {
                return namedSeatCount +
                    (namedSeatCount == 1 ? " seat | live" : " seats | live");
            }
        }
        public ObservableCollection<PlanStepItem> PlanSteps { get; private set; }
        public ObservableCollection<PlannerStepItem> PlannerSteps { get; private set; }
        public ObservableCollection<PlannerSeatOption> PlannerTargetOptions { get; private set; }
        public ObservableCollection<PlannerSeatOption> PlannerAboutOptions { get; private set; }
        public ObservableCollection<CharacterOption> PlannerActions { get; private set; }
        public ObservableCollection<CharacterOption> PlannerRooms { get; private set; }
        public ObservableCollection<LimitActionItem> LimitActions { get; private set; }
        public ObservableCollection<GameOptionSection> SystemOptionSections { get; private set; }
        public ObservableCollection<SoundChannelItem> SoundChannels { get; private set; }
        public ObservableCollection<GameOptionSection> Additional1OptionSections { get; private set; }
        public ObservableCollection<GameOptionSection> Additional2OptionSections { get; private set; }
        public ObservableCollection<GameOptionSection> Additional3OptionSections { get; private set; }
        public ObservableCollection<RelationshipRowItem> RelationshipRows { get; private set; }
        public ObservableCollection<BehaviorRowItem> BehaviorRows { get; private set; }
        public ObservableCollection<LoverRowItem> LoverRows { get; private set; }
        public ObservableCollection<PromiseRowItem> PromiseRows { get; private set; }
        public ObservableCollection<LogLine> LiveLines { get; private set; }
        public ObservableCollection<LogLine> PlannerLines { get; private set; }
        public ObservableCollection<DebugFieldItem> DebugFields { get; private set; }
        public ObservableCollection<AauModuleItem> AauModules { get; private set; }

        public string AauModuleStatus
        {
            get { return aauModuleStatus; }
            private set { Set(ref aauModuleStatus, value); }
        }

        public SeatItem SelectedSeat
        {
            get { return selectedSeat; }
            set
            {
                if (!Set(ref selectedSeat, value)) return;
                ResetGameplayEditors();
                RefreshLimitActionsForCurrentSeat();
                UpdateGameplayEmptyTexts();
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
                OnPropertyChanged(nameof(CanApplyOptions));
                OnPropertyChanged(nameof(CanRevertOptions));
                OnPropertyChanged(nameof(CanBackup));
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

        public string CurrentSavePath
        {
            get { return currentSavePath; }
            private set { Set(ref currentSavePath, value); }
        }

        private string academyName = "";
        private string saveFileName = "";

        public string AcademyName
        {
            get { return academyName; }
            private set
            {
                if (!Set(ref academyName, value ?? "")) return;
                OnPropertyChanged(nameof(SaveContextText));
            }
        }

        public string SaveFileName
        {
            get { return saveFileName; }
            private set
            {
                if (!Set(ref saveFileName, value ?? "")) return;
                OnPropertyChanged(nameof(SaveContextText));
            }
        }

        /// <summary>Academy and save file shown in the top bar.</summary>
        public string SaveContextText
        {
            get
            {
                string academy = string.IsNullOrEmpty(academyName)
                    ? "-" : academyName;
                string save = string.IsNullOrEmpty(saveFileName)
                    ? "-" : saveFileName;
                return academy + "  |  " + save;
            }
        }

        public string SaveContextToolTip
        {
            get
            {
                return string.IsNullOrEmpty(currentSavePath)
                    ? "Academy and save file are read from the live game."
                    : currentSavePath;
            }
        }

        public int[] RefreshIntervals
        {
            get { return RefreshIntervalChoices; }
        }

        public bool LiveRefreshEnabled
        {
            get { return trainerSettings.LiveRefreshEnabled; }
            set
            {
                if (trainerSettings.LiveRefreshEnabled == value) return;
                trainerSettings.LiveRefreshEnabled = value;
                if (session != null) session.LiveRefreshEnabled = value;
                trainerSettings.Save();
                OnPropertyChanged(nameof(LiveRefreshEnabled));
                LogSession(value ? "Live refresh enabled." :
                    "Live refresh paused.");
            }
        }

        public int RefreshIntervalMs
        {
            get { return trainerSettings.RefreshIntervalMs; }
            set
            {
                if (trainerSettings.RefreshIntervalMs == value) return;
                trainerSettings.RefreshIntervalMs = value;
                if (session != null) session.RefreshIntervalMs = value;
                trainerSettings.Save();
                OnPropertyChanged(nameof(RefreshIntervalMs));
            }
        }

        public bool LiveIntervalAuto
        {
            get { return trainerSettings.LiveIntervalAuto; }
            set
            {
                if (trainerSettings.LiveIntervalAuto == value) return;
                trainerSettings.LiveIntervalAuto = value;
                if (session != null) session.LiveIntervalAuto = value;
                trainerSettings.Save();
                OnPropertyChanged(nameof(LiveIntervalAuto));
                OnPropertyChanged(nameof(CanEditRefreshInterval));
            }
        }

        public bool CanEditRefreshInterval
        {
            get { return !trainerSettings.LiveIntervalAuto; }
        }

        public string BackupStatus
        {
            get { return backupStatus; }
            private set { Set(ref backupStatus, value); }
        }

        public bool CanBackup
        {
            get
            {
                return isAttached && !string.IsNullOrEmpty(currentSavePath) &&
                    File.Exists(currentSavePath);
            }
        }

        public event Action<string> SaveRestored;

        public string NameTableStatus
        {
            get { return nameTableStatus; }
            private set { Set(ref nameTableStatus, value); }
        }

        public void BackupCurrentSave()
        {
            if (!CanBackup)
            {
                BackupStatus = "No current class save was found.";
                return;
            }
            try
            {
                string backup = SaveBackup.Create(CurrentSavePath);
                bool hasAau = File.Exists(SaveBackup.AauPath(CurrentSavePath));
                BackupStatus = "Backup: " + Path.GetFileName(backup) +
                    (hasAau ? " (AAU data included)" : "");
                LogSession("Created save backup " + Path.GetFileName(backup) +
                    (hasAau ? " including AAU data." : "."));
            }
            catch (Exception ex)
            {
                BackupStatus = "Backup failed: " + ex.Message;
                LogSession(BackupStatus, "Error");
            }
        }

        public bool RestoreBackup(SaveBackupEntry entry)
        {
            if (entry == null)
            {
                BackupStatus = "Select a backup first.";
                return false;
            }
            try
            {
                SaveRestoreResult result = SaveBackup.Restore(entry);
                CurrentSavePath = entry.SavePath;
                OnPropertyChanged(nameof(CanBackup));
                BackupStatus = "Restored " +
                    Path.GetFileName(entry.BackupPath) +
                    (entry.HasAauBackup ? " (AAU data included)" : "") +
                    (string.IsNullOrEmpty(result.SafetySaveBackup)
                        ? "" : "; safety backup " +
                            Path.GetFileName(result.SafetySaveBackup));
                LogSession("Restored save backup " +
                    Path.GetFileName(entry.BackupPath) +
                    ". Restart or reload the class save in-game.");

                Action<string> handler = SaveRestored;
                if (handler != null) handler(entry.SavePath);
                return true;
            }
            catch (Exception ex)
            {
                BackupStatus = "Restore failed: " + ex.Message;
                LogSession(BackupStatus, "Error");
                return false;
            }
        }

        public bool RestoreBackup(string backupPath)
        {
            if (string.IsNullOrEmpty(backupPath) ||
                string.IsNullOrEmpty(CurrentSavePath))
            {
                BackupStatus = "Select a backup for the current save.";
                return false;
            }

            List<SaveBackupEntry> entries = SaveBackup.List(
                CurrentSavePath, false);
            SaveBackupEntry entry = entries.FirstOrDefault(
                candidate => SaveBackup.PathsEqual(
                    candidate.BackupPath, backupPath));
            if (entry == null)
            {
                BackupStatus =
                    "That backup does not belong to the current save.";
                return false;
            }
            return RestoreBackup(entry);
        }

        public void ReloadNameTables()
        {
            if (session != null) session.RequestNamesReload();
        }

        public void ImportNameTables(string folder)
        {
            if (session != null) session.RequestNamesImport(folder);
        }

        public string RosterStatus
        {
            get { return rosterStatus; }
            private set { Set(ref rosterStatus, value); }
        }

        public string RosterEmptyText
        {
            get { return rosterEmptyText; }
            private set { Set(ref rosterEmptyText, value); }
        }

        public string LimitEmptyText
        {
            get { return limitEmptyText; }
            private set { Set(ref limitEmptyText, value); }
        }

        public string RelationshipEmptyText
        {
            get { return relationshipEmptyText; }
            private set { Set(ref relationshipEmptyText, value); }
        }

        public string LoverEmptyText
        {
            get { return loverEmptyText; }
            private set { Set(ref loverEmptyText, value); }
        }

        public string PromiseEmptyText
        {
            get { return promiseEmptyText; }
            private set { Set(ref promiseEmptyText, value); }
        }

        public string DebugEmptyText
        {
            get { return debugEmptyText; }
            private set { Set(ref debugEmptyText, value); }
        }

        public int SelectedTabIndex
        {
            get { return selectedTabIndex; }
            set
            {
                if (!Set(ref selectedTabIndex, value)) return;
                if (session != null)
                {
                    bool debugActive = value == 4;
                    session.DebugActive = debugActive;
                    if (debugActive) session.RequestDebugRefresh();
                }
            }
        }

        public string HookState { get { return plannerHookState; } }
        public string PlanStatus { get { return plannerStatus; } }

        public string PlannerStatus
        {
            get { return plannerStatus; }
            private set { Set(ref plannerStatus, value); }
        }

        public string PlannerHookState
        {
            get { return plannerHookState; }
            private set { Set(ref plannerHookState, value); }
        }

        public string PlannerHookButtonText
        {
            get { return plannerHookButtonText; }
            private set { Set(ref plannerHookButtonText, value); }
        }

        public bool PlannerHookInstalled
        {
            get { return plannerHookInstalled; }
            private set
            {
                if (!Set(ref plannerHookInstalled, value)) return;
                OnPropertyChanged(nameof(PlannerHookButtonText));
                OnPropertyChanged(nameof(CanToggleObserve));
                OnPropertyChanged(nameof(CanStartPlanner));
            }
        }

        public bool PlannerObserveOnly
        {
            get { return plannerObserveOnly; }
            private set { Set(ref plannerObserveOnly, value); }
        }

        public bool PlannerRunning
        {
            get { return plannerRunning; }
            private set
            {
                if (!Set(ref plannerRunning, value)) return;
                OnPropertyChanged(nameof(CanEditPlanner));
                OnPropertyChanged(nameof(CanStartPlanner));
                OnPropertyChanged(nameof(PlannerStopEnabled));
            }
        }

        public int PlannerModeIndex
        {
            get { return plannerModeIndex; }
            set { Set(ref plannerModeIndex, value); }
        }

        public int PlannerTriggerIndex
        {
            get { return plannerTriggerIndex; }
            set { Set(ref plannerTriggerIndex, value); }
        }

        public bool PlannerRepeat
        {
            get { return plannerRepeat; }
            set { Set(ref plannerRepeat, value); }
        }

        public bool PlannerShuffle
        {
            get { return plannerShuffle; }
            set { Set(ref plannerShuffle, value); }
        }

        public PlannerSeatOption SelectedPlannerTarget
        {
            get { return selectedPlannerTarget; }
            set { Set(ref selectedPlannerTarget, value); }
        }

        public PlannerSeatOption SelectedPlannerAbout
        {
            get { return selectedPlannerAbout; }
            set { Set(ref selectedPlannerAbout, value); }
        }

        public CharacterOption SelectedPlannerAction
        {
            get { return selectedPlannerAction; }
            set { Set(ref selectedPlannerAction, value); }
        }

        public CharacterOption SelectedPlannerRoom
        {
            get { return selectedPlannerRoom; }
            set { Set(ref selectedPlannerRoom, value); }
        }

        public int SelectedPlannerStepIndex
        {
            get { return selectedPlannerStepIndex; }
            set
            {
                if (!Set(ref selectedPlannerStepIndex, value)) return;
                OnPropertyChanged(nameof(CanRemovePlannerStep));
                OnPropertyChanged(nameof(CanCopyPlanner));
            }
        }

        public bool CanEditPlanner
        {
            get { return !plannerRunning; }
        }

        public bool CanStartPlanner
        {
            get
            {
                return !plannerRunning && plannerHookInstalled &&
                    !plannerObserveOnly && PlannerSteps.Count > 0;
            }
        }

        public bool CanForcePlanner
        {
            get
            {
                return !plannerRunning && plannerHookInstalled &&
                    !plannerObserveOnly;
            }
        }

        public bool PlannerStopEnabled
        {
            get { return plannerRunning; }
        }

        public bool CanToggleObserve
        {
            get { return plannerHookInstalled; }
        }

        public bool CanRemovePlannerStep
        {
            get
            {
                return !plannerRunning && selectedPlannerStepIndex >= 0 &&
                    selectedPlannerStepIndex < PlannerSteps.Count;
            }
        }

        public bool CanCopyPlanner
        {
            get { return PlannerSteps.Count > 0 && !plannerRunning; }
        }

        public bool CanPastePlanner
        {
            get { return !plannerRunning && plannerClipboardAvailable; }
        }

        private bool plannerClipboardAvailable;

        public bool LimitEnabled
        {
            get { return limitEnabled; }
            set
            {
                if (!Set(ref limitEnabled, value)) return;
                if (session != null) session.LimitEnabled = value;
                UpdateAllLimitMarkers();
            }
        }

        private void UpdateAllLimitMarkers()
        {
            foreach (SeatItem seat in Seats)
            {
                if (seat == null || seat.SeatIndex < 0) continue;
                seat.HasActionLimits = limitEnabled && session != null &&
                    session.LimitedActions(seat.SeatIndex).Length > 0;
            }
        }

        public bool LimitApplyToPc
        {
            get { return limitApplyToPc; }
            set
            {
                if (!Set(ref limitApplyToPc, value)) return;
                if (session != null) session.LimitApplyToPc = value;
            }
        }

        public string LimitStatus
        {
            get { return limitStatus; }
            private set { Set(ref limitStatus, value); }
        }

        public string LimitTargetText
        {
            get { return limitTargetText; }
            private set { Set(ref limitTargetText, value); }
        }

        public string LimitCharacterStatus
        {
            get { return limitCharacterStatus; }
            private set { Set(ref limitCharacterStatus, value); }
        }

        public string LimitCopyStatus
        {
            get { return limitCopyStatus; }
            private set { Set(ref limitCopyStatus, value); }
        }

        public ICollectionView LimitActionsView
        {
            get { return limitActionsView; }
        }

        public string LimitFilter
        {
            get { return limitFilter; }
            set
            {
                if (!Set(ref limitFilter, value ?? "")) return;
                if (limitActionsView != null) limitActionsView.Refresh();
                UpdateLimitEmptyText();
            }
        }

        private void UpdateLimitEmptyText()
        {
            if (SelectedSeat == null || !SelectedSeat.HasName)
            {
                LimitEmptyText = "Select a character in the roster.";
                return;
            }
            if (LimitActions.Count == 0)
            {
                LimitEmptyText = "No action definitions loaded.";
                return;
            }
            LimitEmptyText = limitActionsView != null && limitActionsView.IsEmpty
                ? "No actions match the search." : "";
        }

        private void UpdateGameplayEmptyTexts()
        {
            if (SelectedSeat == null || !SelectedSeat.HasName)
            {
                RelationshipEmptyText = "Select a character in the roster.";
                LoverEmptyText = "Select a character in the roster.";
                PromiseEmptyText = "Select a character in the roster.";
                return;
            }

            RelationshipEmptyText = RelationshipRows.Count == 0
                ? "No relationship records for this character." : "";
            LoverEmptyText = LoverRows.Count == 0
                ? "No lover entries for this character." : "";
            PromiseEmptyText = PromiseRows.Count == 0
                ? "No active promises for this character." : "";
        }

        public bool CanCopyLimits
        {
            get { return LimitSeat >= 0; }
        }

        public bool CanPasteLimits
        {
            get { return canPasteLimits && LimitSeat >= 0; }
        }

        private int LimitSeat
        {
            get { return SelectedSeat == null ? -1 : SelectedSeat.SeatIndex; }
        }

        private bool FilterLimitAction(object item)
        {
            LimitActionItem action = item as LimitActionItem;
            if (action == null) return false;
            if (string.IsNullOrEmpty(limitFilter)) return true;
            return action.Text != null &&
                action.Text.IndexOf(limitFilter,
                    StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public void CopyLimits()
        {
            int seat = LimitSeat;
            if (session == null || seat < 0) return;
            limitClipboard = session.LimitedActions(seat);
            canPasteLimits = true;
            OnPropertyChanged(nameof(CanPasteLimits));
            LimitCopyStatus = "Copied " + limitClipboard.Length +
                " limit(s) from " + SelectedSeat.Name + ".";
        }

        public void PasteLimits()
        {
            int seat = LimitSeat;
            if (session == null || seat < 0) return;
            if (limitClipboard == null)
            {
                LimitCopyStatus = "Copy a character's limits first.";
                return;
            }

            session.ClearActionLimits(seat);
            foreach (int actionId in limitClipboard)
                session.SetActionLimit(seat, actionId, true);
            RefreshLimitActionsForCurrentSeat();
            UpdateLimitSeatFlag(seat);
            LimitCopyStatus = "Pasted " + limitClipboard.Length +
                " limit(s) to " + SelectedSeat.Name + ".";
        }

        public string GameOptionsStatus
        {
            get { return gameOptionsStatus; }
            private set { Set(ref gameOptionsStatus, value); }
        }

        public bool IsGameOptionsBusy
        {
            get { return gameOptionsBusy; }
        }

        public bool CanApplyOptions
        {
            get
            {
                return isAttached && !gameOptionsBusy &&
                    liveGameOptions != null && AnyGameOptionDirty();
            }
        }

        public bool CanRevertOptions
        {
            get { return !gameOptionsBusy && AnyGameOptionDirty(); }
        }

        public void ApplyGameOptions()
        {
            if (session == null || gameOptionsBusy || liveGameOptions == null ||
                !AnyGameOptionDirty())
                return;

            OptionSnapshot original = BuildGameOptionSnapshot(true);
            OptionSnapshot edited = BuildGameOptionSnapshot(false);
            if (original == null || edited == null) return;

            if (!session.RequestOptionWrite(original, edited))
            {
                GameOptionsStatus = "Write request was not accepted.";
                return;
            }

            gameOptionsBusy = true;
            GameOptionsStatus = "Writing...";
            NotifyOptionButtons();
        }

        public void RevertGameOptions()
        {
            if (gameOptionsBusy) return;
            foreach (GameOptionItem item in gameOptionItems.Values)
                item.SetLive(item.LiveValue);
            GameOptionsStatus = "Reverted to live values.";
            NotifyOptionButtons();
        }

        private void OnGameOptionEdited(GameOptionItem item)
        {
            if (gameOptionsBusy) return;
            GameOptionsStatus = "Unsaved changes.";
            NotifyOptionButtons();
        }

        private void OnOptionWriteCompleted(OptionWriteResult result)
        {
            gameOptionsBusy = false;
            if (result != null && result.Success)
            {
                foreach (GameOptionItem item in gameOptionItems.Values)
                    item.MarkClean();
                GameOptionsStatus = string.IsNullOrEmpty(result.Changed) ||
                    result.Changed == "No changes."
                    ? "No changes."
                    : "Applied: " + result.Changed;
            }
            else
            {
                string error = result == null || string.IsNullOrEmpty(result.Error)
                    ? "unknown error" : result.Error;
                GameOptionsStatus = "Write failed: " + error;
            }
            NotifyOptionButtons();
        }

        private void NotifyOptionButtons()
        {
            OnPropertyChanged(nameof(IsGameOptionsBusy));
            OnPropertyChanged(nameof(CanApplyOptions));
            OnPropertyChanged(nameof(CanRevertOptions));
        }

        private bool AnyGameOptionDirty()
        {
            foreach (GameOptionItem item in gameOptionItems.Values)
            {
                if (item.IsDirty) return true;
            }
            return false;
        }

        private OptionSnapshot BuildGameOptionSnapshot(bool live)
        {
            if (liveGameOptions == null) return null;
            OptionSnapshot snapshot = liveGameOptions.Clone();
            foreach (GameOptionItem item in gameOptionItems.Values)
            {
                if (item.Index < 0 ||
                    item.Index >= snapshot.Values.Length)
                    continue;
                snapshot.Values[item.Index] = live
                    ? item.LiveValue : item.Value;
            }
            return snapshot;
        }

        private void UpdateGameOptions(SessionSnapshot sessionSnapshot)
        {
            if (sessionSnapshot.GameOptions != null)
            {
                liveGameOptions = sessionSnapshot.GameOptions;
                foreach (GameOptionItem item in gameOptionItems.Values)
                {
                    if (item.IsDirty) continue;
                    if (item.Index >= 0 &&
                        item.Index < liveGameOptions.Values.Length)
                    {
                        item.SetLive(liveGameOptions.Values[item.Index]);
                    }
                }

                if (!gameOptionsBusy && !AnyGameOptionDirty())
                    GameOptionsStatus = "Game options: live";
            }
            else if (!string.IsNullOrEmpty(sessionSnapshot.GameOptionsError))
            {
                GameOptionsStatus = "Game options: " +
                    sessionSnapshot.GameOptionsError;
            }

            NotifyOptionButtons();
        }

        private void UpdateSaveContext(SessionSnapshot snapshot)
        {
            AcademyName = snapshot.Schedule == null
                ? "" : (snapshot.Schedule.Academy ?? "").Trim();

            string saveName = snapshot.Schedule == null
                ? "" : snapshot.Schedule.SaveName ?? "";
            bool changed = saveName != lastSaveName;
            if (changed)
            {
                string path = "";
                if (!string.IsNullOrEmpty(saveName))
                {
                    string directory = SaveBackup.DefaultDirectory();
                    if (!string.IsNullOrEmpty(directory))
                    {
                        string fileName = saveName.EndsWith(".sav",
                            StringComparison.OrdinalIgnoreCase)
                            ? saveName : saveName + ".sav";
                        path = Path.Combine(directory, fileName);
                    }
                }
                if (path != currentSavePath)
                {
                    CurrentSavePath = path;
                    OnPropertyChanged(nameof(CanBackup));
                    OnPropertyChanged(nameof(SaveContextToolTip));
                    if (!string.IsNullOrEmpty(path))
                        BackupStatus = "Current save: " + Path.GetFileName(path);
                }

                bool reindex = !string.IsNullOrEmpty(lastSaveName);
                lastSaveName = saveName;
                if (reindex && !cardIndexRunning)
                    LoadCardsAsync();
            }

            SaveFileName = string.IsNullOrEmpty(currentSavePath)
                ? "" : Path.GetFileName(currentSavePath);
        }

        private void BuildGameOptionSections()
        {
            AddOptionSection(SystemOptionSections, "Display",
                "DisplayShadow", "DisplayOutline", "DisplayCrosshair");
            AddOptionSection(SystemOptionSections, "H-Scene Settings",
                "DisplayPlayer", "DisplayNPC", "DisplayPenis", "CameraAngle",
                "DisplayBGChar", "Display3D", "ClickFocus");
            AddOptionSection(SystemOptionSections,
                "Monochrome/Transparent Setting",
                "MonoMan", "MonoManOpa", "MonoManRed", "MonoManGreen",
                "MonoManBlue");

            AddSoundChannel("Background Music", "BGM", "BGMVal");
            AddSoundChannel("Sound Effect", "Sound", "SoundVal");
            AddSoundChannel("Voice", "Voice", "VoiceVal");
            AddSoundChannel("H-Sound", "HSound", "HSoundVal");

            AddOptionSection(Additional1OptionSections, "Misc",
                "NPCConversion", "NPCConversionVal", "BGExternal",
                "ReleaseMouse");
            AddOptionSection(Additional1OptionSections, "Movement",
                "PlayerMove", "PlayerMoveVal", "NPCMove", "NPCMoveVal",
                "WorldMove", "WorldMoveVal");
            AddOptionSection(Additional1OptionSections, "Time & Risk",
                "CustomBell", "CustomBellVal", "OverrideRisk",
                "OverrideRiskVal", "OverrideCondom", "OverrideCondomVal");

            AddOptionSection(Additional2OptionSections, "Interaction",
                "DisableInteractionNPCtoPlayer",
                "DisableInteruptionNPCtoPlayer",
                "DisableInteractionNPCtoNPC",
                "DisableInteruptionNPCtoNPC",
                "DisableInteractionAutoPCtoNPC",
                "DisableInteruptionAutoPCtoNPC");
            AddOptionSection(Additional2OptionSections, "Disabled Actions",
                "DisableABUSEActions", "DisableViolenceActions",
                "DisableContestActions", "DisableNPCEscape",
                "DisableCOMMANDActions", "DisableJOINCLUBActions",
                "DisableBELOVERAction", "DisableBREAKUPAction",
                "DisableSKINSHIP", "DisableSEXAction",
                "DisableSexualExceptSex", "DisableEveryoneCommand",
                "DisableNPCGoingHome", "DisableNPCTraining");

            AddOptionSection(Additional3OptionSections,
                "Relationship Rate Modifiers",
                "LOVEorLIKEModPlayertoNPC", "LOVEorLIKEModPlayertoNPCVal",
                "DISLIKEorHATEModPlayertoNPC",
                "DISLIKEorHATEModPlayertoNPCVal",
                "LOVEorLIKEModNPCtoPlayer", "LOVEorLIKEModNPCtoPlayerVal",
                "DISLIKEorHATEModNPCtoPlayer",
                "DISLIKEorHATEModNPCtoPlayerVal",
                "LOVEorLIKEModNPCtoNPC", "LOVEorLIKEModNPCtoNPCval",
                "DISLIKEorHATEModNPCtoNPC",
                "DISLIKEorHATEModNPCtoNPCval");
            AddOptionSection(Additional3OptionSections, "Other",
                "PositiveRespontoPlayer", "SuccessfulTraining", "SkipHScane",
                "EveryoneHMood");
        }

        private void AddOptionSection(
            ObservableCollection<GameOptionSection> sections, string title,
            params string[] names)
        {
            GameOptionSection section = new GameOptionSection { Title = title };
            foreach (string name in names)
            {
                GameOptionItem item = CreateOptionItem(name);
                if (item != null) section.Items.Add(item);
            }
            if (section.Items.Count > 0) sections.Add(section);
        }

        private void AddSoundChannel(string label, string toggleName,
            string levelName)
        {
            GameOptionItem toggle = CreateOptionItem(toggleName);
            GameOptionItem level = CreateOptionItem(levelName);
            if (toggle == null || level == null) return;
            SoundChannels.Add(new SoundChannelItem(label, toggle, level));
        }

        private GameOptionItem CreateOptionItem(string name)
        {
            int index = OptionEditor.IndexOf(name);
            if (index < 0) return null;
            GameOptionItem item = new GameOptionItem(index,
                OptionEditor.Fields[index], OnGameOptionEdited);
            item.Hint = OptionHint(name);
            gameOptionItems[index] = item;
            return item;
        }

        private static string OptionHint(string name)
        {
            if (name == "BGExternal") return "AA2/data/CustomBgm";
            return "";
        }

        public ICollectionView DebugFieldsView
        {
            get { return debugFieldsView; }
        }

        public string DebugFilter
        {
            get { return debugFilter; }
            set
            {
                if (!Set(ref debugFilter, value ?? "")) return;
                RefreshDebugFilter();
            }
        }

        public int DebugTypeIndex
        {
            get { return debugTypeIndex; }
            set
            {
                if (!Set(ref debugTypeIndex, value)) return;
                RefreshDebugFilter();
            }
        }

        public string[] DebugCategories
        {
            get { return DebugCategoryChoices; }
        }

        public int DebugCategoryIndex
        {
            get { return debugCategoryIndex; }
            set
            {
                if (!Set(ref debugCategoryIndex, value)) return;
                RefreshDebugFilter();
            }
        }

        public bool DebugOnlyProblems
        {
            get { return debugOnlyProblems; }
            set
            {
                if (!Set(ref debugOnlyProblems, value)) return;
                RefreshDebugFilter();
            }
        }

        public DebugFieldItem SelectedDebugField
        {
            get { return selectedDebugField; }
            set
            {
                if (!Set(ref selectedDebugField, value)) return;
                DebugDetails = BuildDebugDetails(value);
            }
        }

        public string DebugDetails
        {
            get { return debugDetails; }
            private set { Set(ref debugDetails, value ?? ""); }
        }

        public string DebugCountText
        {
            get { return debugCountText; }
            private set { Set(ref debugCountText, value ?? ""); }
        }

        private bool FilterDebugField(object item)
        {
            DebugFieldItem field = item as DebugFieldItem;
            if (field == null) return false;

            if (debugOnlyProblems && !field.IsProblem) return false;

            if (debugCategoryIndex > 0 &&
                debugCategoryIndex < DebugCategoryChoices.Length)
            {
                string category = DebugCategoryChoices[debugCategoryIndex];
                if (!string.Equals(field.Category, category,
                    StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            if (!string.IsNullOrEmpty(debugFilter))
            {
                if (!ContainsText(field.Category) &&
                    !ContainsText(field.Name) &&
                    !ContainsText(field.Address) &&
                    !ContainsText(field.Type) &&
                    !ContainsText(field.Value) &&
                    !ContainsText(field.Note) &&
                    !ContainsText(field.Chain) &&
                    !ContainsText(field.Status))
                    return false;
            }

            switch (debugTypeIndex)
            {
                case 1:
                    return field.IsPointer;
                case 2:
                    return IsIntegerType(field.Type);
                case 3:
                    return field.Type == "string";
                case 4:
                    return field.Type == "bool";
                default:
                    return true;
            }
        }

        private void RefreshDebugFilter()
        {
            if (debugFieldsView != null) debugFieldsView.Refresh();
            UpdateDebugFilterState();
        }

        private void UpdateDebugFilterState()
        {
            int visible = 0;
            if (debugFieldsView != null)
            {
                foreach (object item in debugFieldsView) visible++;
            }
            DebugCountText = DebugFields.Count == 0
                ? "" : visible + " / " + DebugFields.Count + " fields";
            UpdateDebugEmptyText();
        }

        private static string BuildDebugDetails(DebugFieldItem field)
        {
            if (field == null)
                return "Select a row to inspect its pointer chain.";

            StringBuilder text = new StringBuilder();
            text.AppendLine("Name:     " + field.Name);
            text.AppendLine("Category: " + field.Category +
                "    Type: " + field.Type);
            text.AppendLine("Address:  " +
                (string.IsNullOrEmpty(field.Address) ? "-" : field.Address));
            text.AppendLine("Status:   " + field.Status +
                (field.Optional ? "  (optional)" : ""));
            text.AppendLine("Value:    " + field.Value);
            text.AppendLine("Chain:    " + field.Chain);
            text.AppendLine("Note:     " + field.Note);
            text.AppendLine();
            text.Append(field.Trace ?? "");
            if (!string.IsNullOrEmpty(field.BytesText))
            {
                text.AppendLine();
                text.AppendLine("Bytes:    " + field.BytesText);
            }
            return text.ToString();
        }

        private bool ContainsText(string value)
        {
            return !string.IsNullOrEmpty(value) &&
                value.IndexOf(debugFilter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsIntegerType(string type)
        {
            return type == "int" || type == "uint" || type == "u32" ||
                type == "i32" || type == "int32" || type == "long" ||
                type == "short" || type == "byte" || type == "u8";
        }

        private void ApplyDebugFields(SessionSnapshot sessionSnapshot)
        {
            List<DebugFieldItem> source = sessionSnapshot.DebugFields;
            if (debugFieldsView == null) return;

            if (source == null || source.Count == 0)
            {
                ClearDebugFields();
                UpdateDebugEmptyText();
                return;
            }

            // The service rebuilds the list at most once per second while the
            // Debug tab is visible; other polls publish the same list.
            if (ReferenceEquals(source, debugSource))
            {
                UpdateDebugFilterState();
                return;
            }
            debugSource = source;

            string keepName = selectedDebugField == null
                ? null : selectedDebugField.Name;

            bool rebuild = DebugFields.Count != source.Count;
            if (!rebuild)
            {
                for (int i = 0; i < DebugFields.Count; i++)
                {
                    if (DebugFields[i].Name != source[i].Name ||
                        DebugFields[i].Category != source[i].Category)
                    {
                        rebuild = true;
                        break;
                    }
                }
            }

            if (rebuild)
            {
                DebugFields.Clear();
                foreach (DebugFieldItem field in source)
                    DebugFields.Add(field);
            }
            else
            {
                for (int i = 0; i < DebugFields.Count; i++)
                    DebugFields[i].Apply(source[i]);
            }

            if (rebuild)
            {
                SelectedDebugField = string.IsNullOrEmpty(keepName)
                    ? null : FindDebugField(keepName);
            }

            RefreshDebugFilter();
        }

        private DebugFieldItem FindDebugField(string name)
        {
            foreach (DebugFieldItem field in DebugFields)
            {
                if (string.Equals(field.Name, name,
                    StringComparison.OrdinalIgnoreCase))
                    return field;
            }
            return null;
        }

        private void ClearDebugFields()
        {
            if (DebugFields.Count == 0 && debugSource == null) return;
            debugSource = null;
            DebugFields.Clear();
            SelectedDebugField = null;
            if (debugFieldsView != null) debugFieldsView.Refresh();
            DebugCountText = "";
        }

        private bool IsDebugFilterActive
        {
            get
            {
                return debugOnlyProblems ||
                    debugCategoryIndex > 0 ||
                    debugTypeIndex != 0 ||
                    !string.IsNullOrEmpty(debugFilter);
            }
        }

        private void UpdateDebugEmptyText()
        {
            if (!isAttached)
            {
                DebugEmptyText =
                    "Not attached. Debug data appears when AA2Play is attached.";
                return;
            }
            if (DebugFields.Count == 0)
            {
                DebugEmptyText = "No debug fields available.";
                return;
            }
            DebugEmptyText = debugFieldsView != null &&
                debugFieldsView.IsEmpty && IsDebugFilterActive
                ? "No fields match the filter." : "";
        }

        public int SelectedRelationshipIndex
        {
            get { return selectedRelationshipIndex; }
            set
            {
                if (!Set(ref selectedRelationshipIndex, value)) return;
                RelationshipEditStatus = "No changes.";
                NotifyGameplayButtons();
            }
        }

        public int SelectedBehaviorIndex
        {
            get { return selectedBehaviorIndex; }
            set
            {
                int previous = selectedBehaviorIndex;
                if (!Set(ref selectedBehaviorIndex, value)) return;
                if (behaviorDraft != null && behaviorDraft.IsDirty)
                {
                    selectedBehaviorIndex = previous;
                    OnPropertyChanged(nameof(SelectedBehaviorIndex));
                    BehaviorEditStatus =
                        "Apply or revert changes before selecting another classmate.";
                    return;
                }
                LoadBehaviorDraft();
            }
        }

        public int SelectedLoverIndex
        {
            get { return selectedLoverIndex; }
            set { Set(ref selectedLoverIndex, value); }
        }

        public BehaviorDraft BehaviorDraft
        {
            get { return behaviorDraft; }
            private set
            {
                if (!Set(ref behaviorDraft, value)) return;
                OnPropertyChanged(nameof(CanApplyBehavior));
                OnPropertyChanged(nameof(CanRevertBehavior));
            }
        }

        public bool IsGameplayWriteBusy
        {
            get { return gameplayWriteBusy; }
        }

        public string RelationshipEditStatus
        {
            get { return relationshipEditStatus; }
            private set { Set(ref relationshipEditStatus, value); }
        }

        public string BehaviorEditStatus
        {
            get { return behaviorEditStatus; }
            private set { Set(ref behaviorEditStatus, value); }
        }

        public string BehaviorEmptyText
        {
            get { return behaviorEmptyText; }
            private set { Set(ref behaviorEmptyText, value); }
        }

        public string LoverEditStatus
        {
            get { return loverEditStatus; }
            private set { Set(ref loverEditStatus, value); }
        }

        public bool CanApplyRelationship
        {
            get
            {
                return isAttached && !gameplayWriteBusy &&
                    AnyRelationshipDirty();
            }
        }

        public bool CanRevertRelationship
        {
            get
            {
                return !gameplayWriteBusy && AnyRelationshipDirty();
            }
        }

        public bool CanApplyBehavior
        {
            get
            {
                return isAttached && !gameplayWriteBusy &&
                    behaviorDraft != null && behaviorDraft.IsDirty;
            }
        }

        public bool CanRevertBehavior
        {
            get
            {
                return !gameplayWriteBusy && behaviorDraft != null &&
                    behaviorDraft.IsDirty;
            }
        }

        public bool CanEditLovers
        {
            get { return isAttached && !gameplayWriteBusy; }
        }

        public void ApplyRelationshipEdits()
        {
            if (session == null || gameplayWriteBusy) return;
            List<RelationshipEdit> edits = new List<RelationshipEdit>();
            foreach (RelationshipRowItem row in RelationshipRows)
            {
                if (row != null && row.IsDirty) edits.Add(row.BuildEdit());
            }
            if (edits.Count == 0)
            {
                RelationshipEditStatus = "No changes.";
                return;
            }

            GameplayWriteRequest request = new GameplayWriteRequest
            {
                Kind = GameplayWriteKind.RelationshipBatch,
                RelationshipEdits = edits
            };
            if (!session.RequestGameplayWrite(request))
            {
                RelationshipEditStatus = "Write request was not accepted.";
                return;
            }
            gameplayWriteBusy = true;
            RelationshipEditStatus = "Writing...";
            NotifyGameplayButtons();
        }

        public void RevertRelationshipEdits()
        {
            if (gameplayWriteBusy) return;
            foreach (RelationshipRowItem row in RelationshipRows)
                row.Revert();
            RelationshipEditStatus = "Reverted to live values.";
            NotifyGameplayButtons();
        }

        private bool AnyRelationshipDirty()
        {
            foreach (RelationshipRowItem row in RelationshipRows)
            {
                if (row != null && row.IsDirty) return true;
            }
            return false;
        }

        public void ApplyBehaviorEdits()
        {
            if (session == null || behaviorDraft == null ||
                !behaviorDraft.IsDirty || gameplayWriteBusy)
                return;

            GameplayWriteRequest request = behaviorDraft.BuildRequest();
            if (request.EventIndices == null || request.EventIndices.Length == 0)
            {
                BehaviorEditStatus = "No changes.";
                return;
            }
            if (!session.RequestGameplayWrite(request))
            {
                BehaviorEditStatus = "Write request was not accepted.";
                return;
            }
            gameplayWriteBusy = true;
            BehaviorEditStatus = "Writing...";
            NotifyGameplayButtons();
        }

        public void RevertBehaviorEdits()
        {
            if (behaviorDraft == null || gameplayWriteBusy) return;
            behaviorDraft.Revert();
            BehaviorEditStatus = "Reverted to live values.";
            NotifyGameplayButtons();
        }

        public void SetAllBehaviorFeelings(int feeling)
        {
            if (session == null || behaviorDraft == null ||
                behaviorDraft.Source == null || gameplayWriteBusy) return;

            GameplayWriteRequest request = new GameplayWriteRequest
            {
                Kind = GameplayWriteKind.HistoryAll,
                SourcePointer = behaviorDraft.Source.Pointer,
                TargetSeat = behaviorDraft.TargetSeat,
                Feeling = feeling
            };
            if (!session.RequestGameplayWrite(request))
            {
                BehaviorEditStatus = "Write request was not accepted.";
                return;
            }
            gameplayWriteBusy = true;
            BehaviorEditStatus = "Writing all events...";
            NotifyGameplayButtons();
        }

        public void ApplyLoverEdits()
        {
            if (session == null || gameplayWriteBusy) return;
            List<LoverEdit> edits = new List<LoverEdit>();
            foreach (LoverRowItem row in LoverRows)
            {
                if (row != null && row.IsDirty) edits.Add(row.BuildEdit());
            }
            if (edits.Count == 0)
            {
                LoverEditStatus = "No changes.";
                return;
            }

            GameplayWriteRequest request = new GameplayWriteRequest
            {
                Kind = GameplayWriteKind.LoverBatch,
                LoverEdits = edits
            };
            if (!session.RequestGameplayWrite(request))
            {
                LoverEditStatus = "Write request was not accepted.";
                return;
            }
            gameplayWriteBusy = true;
            LoverEditStatus = "Writing...";
            NotifyGameplayButtons();
        }

        public void RevertLoverEdits()
        {
            if (gameplayWriteBusy) return;
            foreach (LoverRowItem row in LoverRows)
                row.Revert();
            LoverEditStatus = "Reverted to live values.";
            NotifyGameplayButtons();
        }

        private void LoadBehaviorDraft()
        {
            BehaviorRowItem row = SelectedBehaviorRow();
            if (row == null || SelectedSeat == null || !SelectedSeat.HasName)
            {
                BehaviorDraft = null;
                BehaviorEmptyText = SelectedSeat == null || !SelectedSeat.HasName
                    ? "Select a character in the roster."
                    : "No behavior history rows for this character.";
                BehaviorEditStatus = "No changes.";
                NotifyGameplayButtons();
                return;
            }
            BehaviorDraft draft = new BehaviorDraft();
            draft.Changed += OnBehaviorDraftChanged;
            draft.Load(new RelationshipHistoryRow
            {
                Source = row.Source,
                HasRecord = row.HasRecord,
                History = row.Values,
                Latest = row.Values.Length > 0
                    ? row.Values[row.Values.Length - 1] : -1
            }, SelectedSeat.SeatIndex);
            BehaviorDraft = draft;
            BehaviorEmptyText = row.Values == null || row.Values.Length == 0
                ? "No history events yet for this classmate. The game has no history buffer to edit."
                : "";
            BehaviorEditStatus = "No changes.";
            NotifyGameplayButtons();
        }

        private BehaviorRowItem SelectedBehaviorRow()
        {
            if (selectedBehaviorIndex < 0 ||
                selectedBehaviorIndex >= BehaviorRows.Count)
                return null;
            return BehaviorRows[selectedBehaviorIndex];
        }

        private void ResetGameplayEditors()
        {
            BehaviorDraft = null;
            selectedRelationshipIndex = -1;
            selectedBehaviorIndex = -1;
            RelationshipEditStatus = "No changes.";
            BehaviorEditStatus = "No changes.";
            BehaviorEmptyText = "";
            LoverEditStatus = "No changes.";
            OnPropertyChanged(nameof(SelectedRelationshipIndex));
            OnPropertyChanged(nameof(SelectedBehaviorIndex));
            NotifyGameplayButtons();
        }

        private void OnRelationshipRowChanged()
        {
            if (gameplayWriteBusy) return;
            RelationshipRowItem row = SelectedRelationshipRow();
            if (row != null && row.IsDirty)
                RelationshipEditStatus = "Unsaved changes.";
            NotifyGameplayButtons();
        }

        private void OnRelationshipFrozenChanged(RelationshipRowItem row,
            bool frozen)
        {
            if (row == null || SelectedSeat == null || session == null)
            {
                if (row != null) row.SetFrozenSilently(!frozen);
                return;
            }
            if (!session.RequestRelationshipFreeze(row.SourceSeat,
                SelectedSeat.SeatIndex, frozen))
            {
                row.SetFrozenSilently(!frozen);
                RosterStatus = "Freeze change was not accepted.";
            }
            else
            {
                RosterStatus = frozen
                    ? "Freezing relationship..." : "Unfreezing relationship...";
            }
        }

        private void OnBehaviorDraftChanged()
        {
            if (gameplayWriteBusy) return;
            if (behaviorDraft != null && behaviorDraft.IsDirty)
                BehaviorEditStatus = "Unsaved changes.";
            NotifyGameplayButtons();
        }

        private void OnGameplayWriteCompleted(GameplayWriteResult result)
        {
            gameplayWriteBusy = false;
            string message;
            bool success = result != null && result.Success;

            if (result != null && result.Kind == GameplayWriteKind.LoverBatch)
            {
                if (success)
                {
                    foreach (LoverRowItem row in LoverRows)
                        row.MarkClean();
                    LoverEditStatus = "Saved lover values.";
                }
                else
                {
                    LoverEditStatus = "Write failed: " +
                        (result.Error ?? "unknown error");
                }
                message = LoverEditStatus;
            }
            else if (result != null &&
                (result.Kind == GameplayWriteKind.HistoryEvents ||
                 result.Kind == GameplayWriteKind.HistoryAll))
            {
                if (success)
                {
                    if (behaviorDraft != null) behaviorDraft.MarkClean();
                    BehaviorEditStatus = "Saved feeling history.";
                }
                else
                {
                    BehaviorEditStatus = "Write failed: " +
                        (result.Error ?? "unknown error");
                }
                message = BehaviorEditStatus;
            }
            else
            {
                if (success)
                {
                    foreach (RelationshipRowItem row in RelationshipRows)
                        row.MarkClean();
                    RelationshipEditStatus = "Saved relationship values.";
                }
                else
                {
                    RelationshipEditStatus = "Write failed: " +
                        (result == null ? "unknown error" : result.Error);
                }
                message = RelationshipEditStatus;
            }

            LogSession(message, success ? "Info" : "Error");
            NotifyGameplayButtons();
        }

        private static int FindSeatIndex<T>(IList<T> rows,
            Func<T, int> seatSelector, int wantedSeat)
        {
            if (rows == null || rows.Count == 0) return -1;
            if (wantedSeat >= 0)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    if (rows[i] != null && seatSelector(rows[i]) == wantedSeat)
                        return i;
                }
            }
            return 0;
        }

        private void NotifyGameplayButtons()
        {
            OnPropertyChanged(nameof(IsGameplayWriteBusy));
            OnPropertyChanged(nameof(CanApplyRelationship));
            OnPropertyChanged(nameof(CanRevertRelationship));
            OnPropertyChanged(nameof(CanApplyBehavior));
            OnPropertyChanged(nameof(CanRevertBehavior));
            OnPropertyChanged(nameof(CanEditLovers));
        }

        private void OnLimitActionChanged(int seat, int actionId, bool limited)
        {
            if (session != null) session.SetActionLimit(seat, actionId, limited);
            UpdateLimitSeatFlag(seat);
            UpdateLimitCharacterStatus();
        }

        public void ClearLimits()
        {
            int seat = LimitSeat;
            if (session == null || seat < 0) return;
            session.ClearActionLimits(seat);
            foreach (LimitActionItem item in LimitActions)
                item.SetLimitedSilently(false);
            UpdateLimitSeatFlag(seat);
            UpdateLimitCharacterStatus();
            LimitCopyStatus = "Cleared limits for " + SelectedSeat.Name + ".";
        }

        private void UpdateLimitSeatFlag(int seat)
        {
            SeatItem item = Seats.FirstOrDefault(
                candidate => candidate.SeatIndex == seat);
            if (item == null) return;
            item.HasActionLimits = limitEnabled && session != null &&
                session.LimitedActions(seat).Length > 0;
        }

        private void RefreshLimitActionsForCurrentSeat()
        {
            if (characterOptions != null)
                RefreshLimitActions(characterOptions);
            UpdateLimitCharacterStatus();
        }

        private void UpdateLimitCharacterStatus()
        {
            int seat = LimitSeat;
            if (seat < 0 || SelectedSeat == null || !SelectedSeat.HasName)
            {
                LimitTargetText = "Limits: no character";
                LimitCharacterStatus = "";
                return;
            }

            LimitTargetText = "Limits for " + SelectedSeat.Seat + "  " +
                SelectedSeat.Name;
            int count = session == null
                ? 0 : session.LimitedActions(seat).Length;
            LimitCharacterStatus = count == 1
                ? "1 blocked action for this character"
                : count + " blocked actions for this character";
            OnPropertyChanged(nameof(CanCopyLimits));
            OnPropertyChanged(nameof(CanPasteLimits));
        }

        public void SetRelationshipFeeling(int feeling)
        {
            RelationshipRowItem row = SelectedRelationshipRow();
            if (row == null || SelectedSeat == null) return;
            SeatItem source = Seats.FirstOrDefault(
                seat => seat.SeatIndex == row.SourceSeat);
            if (source != null) SetRelationship(source, feeling);
        }

        public void SetLoverFromRow(bool lovers)
        {
            LoverRowItem row = SelectedLoverRow();
            if (row == null || SelectedSeat == null) return;
            SeatItem source = Seats.FirstOrDefault(
                seat => seat.SeatIndex == row.SourceSeat);
            if (source != null) SetLover(source, lovers);
        }

        private void OnLoverCheckChanged(LoverRowItem row, bool lovers)
        {
            if (session == null || row == null || SelectedSeat == null)
                return;
            if (!session.RequestLover(row.SourceSeat,
                SelectedSeat.SeatIndex, lovers))
            {
                row.SetLoverSilently(!lovers);
                LoverEditStatus = "Lover change was not accepted.";
                return;
            }
            LoverEditStatus = lovers ? "Setting lovers..." : "Breaking up...";
        }

        public void ToggleRelationshipFreezeFromRow()
        {
            RelationshipRowItem row = SelectedRelationshipRow();
            if (row == null) return;
            SetRelationshipFreezeFromRow(!row.IsFrozen);
        }

        public void SetRelationshipFreezeFromRow(bool frozen)
        {
            RelationshipRowItem row = SelectedRelationshipRow();
            if (row == null) return;
            row.SetFrozenSilently(frozen);
            OnRelationshipFrozenChanged(row, frozen);
        }

        public void SetAllRelationshipFeeling(int feeling)
        {
            if (RelationshipRows.Count == 0) return;
            foreach (RelationshipRowItem row in RelationshipRows)
            {
                if (row != null && row.Record != null && row.Source != null)
                    row.SetFeeling(feeling);
            }
            ApplyRelationshipEdits();
        }

        public void SetAllRelationshipFreeze(bool frozen)
        {
            if (session == null || SelectedSeat == null) return;
            List<int> seats = new List<int>();
            foreach (RelationshipRowItem row in RelationshipRows)
            {
                if (row != null && row.SourceSeat >= 0)
                    seats.Add(row.SourceSeat);
            }
            if (seats.Count == 0) return;
            if (!session.RequestRelationshipFreezeAll(SelectedSeat.SeatIndex,
                seats, frozen))
                RosterStatus = "Freeze change was not accepted.";
            else
                RosterStatus = frozen
                    ? "Freezing relationships..." : "Unfreezing relationships...";
        }

        private RelationshipRowItem SelectedRelationshipRow()
        {
            if (selectedRelationshipIndex < 0 ||
                selectedRelationshipIndex >= RelationshipRows.Count)
                return null;
            return RelationshipRows[selectedRelationshipIndex];
        }

        private LoverRowItem SelectedLoverRow()
        {
            if (selectedLoverIndex < 0 ||
                selectedLoverIndex >= LoverRows.Count)
                return null;
            return LoverRows[selectedLoverIndex];
        }

        private void RefreshLimitActions(CharacterOptions options)
        {
            LimitActions.Clear();
            if (options == null) return;
            int seat = LimitSeat;
            if (seat < 0 || SelectedSeat == null || !SelectedSeat.HasName)
            {
                UpdateLimitCharacterStatus();
                UpdateLimitEmptyText();
                return;
            }

            HashSet<int> limited = session == null
                ? new HashSet<int>()
                : new HashSet<int>(session.LimitedActions(seat));
            foreach (CharacterOption option in options.Actions)
            {
                int actionId = option.Value;
                LimitActionItem item = new LimitActionItem(
                    actionId, option.Text,
                    (id, value) => OnLimitActionChanged(seat, id, value));
                item.SetLimitedSilently(limited.Contains(actionId));
                LimitActions.Add(item);
            }
            if (limitActionsView != null) limitActionsView.Refresh();
            UpdateLimitCharacterStatus();
            UpdateLimitEmptyText();
        }

        private void RefreshGameplayTables(SessionSnapshot snapshot)
        {
            if (session != null)
            {
                LimitStatus = "Blocked: " + session.LimitBlockedCount +
                    (string.IsNullOrEmpty(session.LimitLastBlocked)
                        ? "" : "  |  last: " + session.LimitLastBlocked);
            }

            if (!snapshot.IsAttached)
            {
                if (limitEnabled)
                {
                    limitEnabled = false;
                    OnPropertyChanged(nameof(LimitEnabled));
                    UpdateAllLimitMarkers();
                }
            }

            string signature = BuildGameplaySignature(snapshot);
            if (signature == gameplayTableSignature) return;
            gameplayTableSignature = signature;

            List<LoverEntry> lovers = snapshot.LoverRows ??
                new List<LoverEntry>();

            int keepRelationshipSeat = selectedRelationshipIndex >= 0 &&
                selectedRelationshipIndex < RelationshipRows.Count
                ? RelationshipRows[selectedRelationshipIndex].SourceSeat : -1;
            int keepBehaviorSeat = selectedBehaviorIndex >= 0 &&
                selectedBehaviorIndex < BehaviorRows.Count
                ? BehaviorRows[selectedBehaviorIndex].SourceSeat : -1;
            int keepLoverSeat = selectedLoverIndex >= 0 &&
                selectedLoverIndex < LoverRows.Count
                ? LoverRows[selectedLoverIndex].SourceSeat : -1;

            RelationshipRows.Clear();
            if (snapshot.RelationshipRows != null)
            {
                foreach (RelationshipRecord record in snapshot.RelationshipRows)
                {
                    if (record == null || record.Source == null) continue;
                    bool isLover = false;
                    foreach (LoverEntry lover in lovers)
                    {
                        if (lover != null && lover.Source != null &&
                            lover.Source.Seat == record.Source.Seat)
                        {
                            isLover = lover.IsLover;
                            break;
                        }
                    }
                    RelationshipRowItem relationshipRow =
                        new RelationshipRowItem { IsLover = isLover };
                    relationshipRow.Load(record);
                    SeatSnapshot sourceSeatSnapshot = snapshot.Seats == null
                        ? null : snapshot.Seats.FirstOrDefault(
                            seat => seat != null &&
                                seat.Seat == record.Source.Seat);
                    relationshipRow.SetFrozenSilently(
                        sourceSeatSnapshot != null &&
                        sourceSeatSnapshot.IsFrozen);
                    relationshipRow.Changed += OnRelationshipRowChanged;
                    relationshipRow.FrozenChanged += OnRelationshipFrozenChanged;
                    RelationshipRows.Add(relationshipRow);
                }
            }

            BehaviorRows.Clear();
            if (snapshot.BehaviorRows != null)
            {
                foreach (RelationshipHistoryRow row in snapshot.BehaviorRows)
                {
                    if (row == null || row.Source == null) continue;
                    string history = "";
                    if (row.History != null)
                    {
                        for (int i = 0; i < row.History.Length; i++)
                        {
                            if (i > 0) history += " > ";
                            history += GameplayLabels.Feeling(row.History[i]);
                        }
                    }
                    BehaviorRows.Add(new BehaviorRowItem
                    {
                        Source = row.Source,
                        SourceSeat = row.Source.Seat,
                        SourceName = row.Source.Name ?? "",
                        History = history,
                        Values = row.History ?? new int[0],
                        HasRecord = row.HasRecord,
                        Latest = row.Latest >= 0
                            ? GameplayLabels.Feeling(row.Latest) : "-"
                    });
                }
            }

            LoverRows.Clear();
            foreach (LoverEntry entry in lovers)
            {
                if (entry == null || entry.Source == null) continue;
                LoverRowItem loverRow = new LoverRowItem
                {
                    Source = entry.Source,
                    SourceSeat = entry.Source.Seat,
                    SourceName = entry.Source.Name ?? "",
                    LoverChanged = OnLoverCheckChanged
                };
                loverRow.SetLiveValues(entry.Days, entry.Compatibility,
                    entry.IsLover);
                LoverRows.Add(loverRow);
            }

            PromiseRows.Clear();
            if (snapshot.PromiseRows != null)
            {
                foreach (PromiseEntry entry in snapshot.PromiseRows)
                {
                    if (entry == null) continue;
                    string otherName = "";
                    if (snapshot.RosterCharacters != null &&
                        entry.OtherSeat >= 0)
                    {
                        foreach (CharacterInfo info in snapshot.RosterCharacters)
                        {
                            if (info != null && info.Seat == entry.OtherSeat)
                            {
                                otherName = info.Name ?? "";
                                break;
                            }
                        }
                    }
                    PromiseRows.Add(new PromiseRowItem
                    {
                        Direction = entry.Direction.ToString(),
                        Kind = entry.Kind.ToString(),
                        OtherSeat = entry.OtherSeat,
                        OtherName = otherName,
                        Location = entry.Location >= 0
                            ? entry.Location.ToString() : "-",
                        TestType = entry.TestType >= 0
                            ? entry.TestType.ToString() : "-",
                        Flag = entry.Flag ? "yes" : "no"
                    });
                }
            }

            selectedRelationshipIndex = FindSeatIndex(RelationshipRows,
                row => row.SourceSeat, keepRelationshipSeat);
            OnPropertyChanged(nameof(SelectedRelationshipIndex));
            RelationshipEditStatus = selectedRelationshipIndex < 0
                ? "Select a classmate." : "No changes.";

            selectedBehaviorIndex = FindSeatIndex(BehaviorRows,
                row => row.SourceSeat, keepBehaviorSeat);
            OnPropertyChanged(nameof(SelectedBehaviorIndex));
            LoadBehaviorDraft();

            selectedLoverIndex = FindSeatIndex(LoverRows,
                row => row.SourceSeat, keepLoverSeat);
            OnPropertyChanged(nameof(SelectedLoverIndex));

            UpdateGameplayEmptyTexts();
            NotifyGameplayButtons();
        }

        private static string BuildGameplaySignature(SessionSnapshot snapshot)
        {
            StringBuilder text = new StringBuilder();
            text.Append('v').Append(snapshot.RosterVersion).Append(';');
            if (snapshot.RelationshipRows != null)
            {
                foreach (RelationshipRecord record in snapshot.RelationshipRows)
                {
                    if (record == null) continue;
                    text.Append('r').Append(record.TargetSeat);
                    for (int i = 0; i < 4; i++)
                    {
                        text.Append('.').Append(record.Counts[i])
                            .Append(':').Append(record.Points[i])
                            .Append(':').Append(record.Levels[i]);
                    }
                    text.Append(':').Append(record.Latest);
                }
            }
            if (snapshot.BehaviorRows != null)
            {
                foreach (RelationshipHistoryRow row in snapshot.BehaviorRows)
                {
                    if (row == null || row.Source == null) continue;
                    text.Append('b').Append(row.Source.Seat);
                    if (row.History != null)
                    {
                        foreach (int feeling in row.History)
                            text.Append(feeling);
                    }
                }
            }
            if (snapshot.LoverRows != null)
            {
                foreach (LoverEntry entry in snapshot.LoverRows)
                {
                    if (entry == null || entry.Source == null) continue;
                    text.Append('l').Append(entry.Source.Seat)
                        .Append(entry.IsLover ? '1' : '0')
                        .Append(':').Append(entry.Days)
                        .Append(':').Append(entry.Compatibility);
                }
            }
            if (snapshot.PromiseRows != null)
            {
                foreach (PromiseEntry entry in snapshot.PromiseRows)
                {
                    if (entry == null) continue;
                    text.Append('p').Append((int)entry.Direction)
                        .Append((int)entry.Kind).Append(entry.OtherSeat)
                        .Append(':').Append(entry.Location)
                        .Append(':').Append(entry.TestType)
                        .Append(entry.Flag ? '1' : '0');
                }
            }
            return text.ToString();
        }

        public void Refresh()
        {
            if (session != null) session.RequestRefresh();
        }

        public void ToggleAttach()
        {
            if (session == null) return;
            if (session.IsAttached)
            {
                LogSession("Detach requested.");
                session.RequestDetach();
            }
            else
            {
                LogSession("Attach requested.");
                session.RequestAttach();
            }
        }

        private int PlannerSeat
        {
            get { return SelectedSeat == null ? -1 : SelectedSeat.SeatIndex; }
        }

        private string PlannerActorName
        {
            get { return SelectedSeat == null ? "" : SelectedSeat.Name; }
        }

        private PlanMode PlannerMode
        {
            get
            {
                switch (plannerModeIndex)
                {
                    case 1: return PlanMode.Forced;
                    case 2: return PlanMode.ForcedPerform;
                    case 3: return PlanMode.Ignore;
                    default: return PlanMode.Normal;
                }
            }
        }

        private PlanTrigger PlannerTrigger
        {
            get
            {
                switch (plannerTriggerIndex)
                {
                    case 1: return PlanTrigger.Target;
                    case 2: return PlanTrigger.Either;
                    default: return PlanTrigger.Actor;
                }
            }
        }

        public void StartPlanner()
        {
            if (session == null || PlannerSeat < 0) return;
            string message;
            if (!session.Planner.Start(PlannerSeat, PlannerActorName, out message))
            {
                RosterStatus = message ?? "The plan could not start.";
                LogPlanner(RosterStatus);
            }
            RefreshPlannerNow();
        }

        public void StopPlanner()
        {
            if (session == null || PlannerSeat < 0) return;
            session.Planner.Stop(PlannerSeat, PlannerActorName);
            RefreshPlannerNow();
        }

        public void TogglePlannerHook()
        {
            if (session == null) return;
            string message;
            bool wasInstalled = plannerHookInstalled;
            bool ok = wasInstalled
                ? session.Planner.UninstallHook(out message)
                : session.Planner.InstallHook(out message);
            if (ok)
            {
                RosterStatus = wasInstalled
                    ? "Planner hook uninstalled."
                    : "Planner hook installed.";
            }
            else if (!string.IsNullOrEmpty(message))
            {
                RosterStatus = message;
                LogPlanner(message);
            }
            if (ok && !plannerHookInstalled && trainerSettings.ObserveOnly)
                session.Planner.SetObserveOnly(true);
            RefreshPlannerNow();
        }

        public void RepairPlannerHook()
        {
            if (session == null) return;
            string message;
            bool ok = session.Planner.RepairHook(out message);
            if (!string.IsNullOrEmpty(message))
            {
                RosterStatus = message;
                LogPlanner(message);
            }
            RefreshPlannerNow();
        }

        public void SetPlannerObserve(bool enabled)
        {
            if (session == null) return;
            if (session.Planner.SetObserveOnly(enabled))
            {
                trainerSettings.ObserveOnly = enabled;
                trainerSettings.Save();
            }
            RefreshPlannerNow();
        }

        public void UpdatePlannerSettings()
        {
            if (session == null || PlannerSeat < 0) return;
            session.Planner.UpdateSettings(PlannerSeat, PlannerActorName,
                PlannerMode, PlannerTrigger, plannerRepeat, plannerShuffle);
        }

        public void AddPlannerAction()
        {
            if (session == null || PlannerSeat < 0 || plannerRunning) return;
            if (selectedPlannerAction == null ||
                selectedPlannerTarget == null || selectedPlannerTarget.Seat < 0)
            {
                RosterStatus = "Choose an action and a target first.";
                return;
            }

            LogPlannerRoomWarning(selectedPlannerTarget);
            bool ok = session.Planner.AddAction(PlannerSeat, PlannerActorName,
                selectedPlannerTarget.Seat, selectedPlannerTarget.Text,
                selectedPlannerAbout == null ? -1 : selectedPlannerAbout.Seat,
                selectedPlannerAbout == null ? "" : selectedPlannerAbout.Text,
                selectedPlannerAction.Value, selectedPlannerAction.Text);
            if (!ok) RosterStatus = "The step could not be added.";
            RefreshPlannerNow();
        }

        private void LogPlannerRoomWarning(PlannerSeatOption target)
        {
            if (SelectedSeat == null || target == null) return;
            SeatItem targetSeat = Seats.FirstOrDefault(
                seat => seat.SeatIndex == target.Seat);
            if (targetSeat == null) return;

            int actorRoom = RoomId(SelectedSeat);
            int targetRoom = RoomId(targetSeat);
            if (actorRoom < 0 || targetRoom < 0)
            {
                LogPlanner("Room warning: actor or target room is unknown; " +
                    "the planner may wait for a valid room.");
            }
            else if (actorRoom != targetRoom)
            {
                LogPlanner("Room warning: actor room " + actorRoom +
                    ", target room " + targetRoom +
                    " (planner will route).");
            }
        }

        private static int RoomId(SeatItem seat)
        {
            if (seat == null || string.IsNullOrEmpty(seat.Room)) return -1;
            string room = seat.Room.Trim();
            if (room.Length == 0 || room == "?") return -1;
            int split = room.IndexOf(' ');
            string number = split < 0 ? room : room.Substring(0, split);
            int id;
            return int.TryParse(number, out id) ? id : -1;
        }

        public void AddPlannerMove()
        {
            if (session == null || PlannerSeat < 0 || plannerRunning) return;
            if (selectedPlannerRoom == null || selectedPlannerRoom.Value < 0)
            {
                RosterStatus = "Choose a room first.";
                return;
            }

            bool ok = session.Planner.AddMove(PlannerSeat, PlannerActorName,
                selectedPlannerRoom.Value, selectedPlannerRoom.Text);
            if (!ok) RosterStatus = "The move step could not be added.";
            RefreshPlannerNow();
        }

        public void ForcePlannerAction()
        {
            if (session == null || PlannerSeat < 0) return;
            if (plannerRunning)
            {
                RosterStatus = "Stop the plan before forcing a step.";
                return;
            }
            if (selectedPlannerAction == null ||
                selectedPlannerTarget == null || selectedPlannerTarget.Seat < 0)
            {
                RosterStatus = "Choose an action and a target first.";
                return;
            }

            LogPlannerRoomWarning(selectedPlannerTarget);
            string message;
            bool ok = session.Planner.ForceAction(PlannerSeat, PlannerActorName,
                selectedPlannerTarget.Seat, selectedPlannerTarget.Text,
                selectedPlannerAbout == null ? -1 : selectedPlannerAbout.Seat,
                selectedPlannerAbout == null ? "" : selectedPlannerAbout.Text,
                selectedPlannerAction.Value, selectedPlannerAction.Text,
                out message);
            RosterStatus = ok
                ? "Forcing action " + selectedPlannerAction.Text + "."
                : (message ?? "The action could not be forced.");
            if (!ok) LogPlanner(RosterStatus);
            RefreshPlannerNow();
        }

        public void ForcePlannerMove()
        {
            if (session == null || PlannerSeat < 0) return;
            if (plannerRunning)
            {
                RosterStatus = "Stop the plan before forcing a step.";
                return;
            }
            if (selectedPlannerRoom == null || selectedPlannerRoom.Value < 0)
            {
                RosterStatus = "Choose a room first.";
                return;
            }

            string message;
            bool ok = session.Planner.ForceMove(PlannerSeat, PlannerActorName,
                selectedPlannerRoom.Value, selectedPlannerRoom.Text,
                out message);
            RosterStatus = ok
                ? "Forcing move to " + selectedPlannerRoom.Text + "."
                : (message ?? "The move could not be forced.");
            if (!ok) LogPlanner(RosterStatus);
            RefreshPlannerNow();
        }

        public void RemovePlannerStep()
        {
            if (session == null || PlannerSeat < 0 || plannerRunning) return;
            session.Planner.RemoveStep(PlannerSeat, PlannerActorName,
                selectedPlannerStepIndex);
            RefreshPlannerNow();
        }

        public void MovePlannerStepTo(int from, int targetIndex)
        {
            if (session == null || PlannerSeat < 0 || plannerRunning) return;
            int count = PlannerSteps.Count;
            if (from < 0 || from >= count || targetIndex < 0) return;

            if (targetIndex >= count) targetIndex = count - 1;
            if (targetIndex == from) return;

            if (session.Planner.MoveStepTo(PlannerSeat, PlannerActorName,
                from, targetIndex))
            {
                selectedPlannerStepIndex = targetIndex;
                OnPropertyChanged(nameof(SelectedPlannerStepIndex));
            }
            RefreshPlannerNow();
        }

        public void ClearPlanner()
        {
            if (session == null || PlannerSeat < 0 || plannerRunning) return;
            session.Planner.ClearSteps(PlannerSeat, PlannerActorName);
            RefreshPlannerNow();
        }

        public void CopyPlanner()
        {
            if (session == null || PlannerSeat < 0) return;
            session.Planner.CopyPlan(PlannerSeat, PlannerActorName);
            plannerClipboardAvailable = true;
            OnPropertyChanged(nameof(CanPastePlanner));
        }

        public void PastePlanner()
        {
            if (session == null || PlannerSeat < 0 || plannerRunning) return;
            session.Planner.PastePlan(PlannerSeat, PlannerActorName);
            RefreshPlannerNow();
        }

        public void SavePlanner()
        {
            if (session == null) return;
            string message;
            if (!session.Planner.SavePlans(out message) &&
                !string.IsNullOrEmpty(message))
                RosterStatus = message;
        }

        public void LoadPlanner()
        {
            if (session == null) return;
            string message;
            if (!session.Planner.LoadPlans(out message) &&
                !string.IsNullOrEmpty(message))
                RosterStatus = message;
            RefreshPlannerNow();
        }

        private void OnLiveLog(string message)
        {
            LogSession(message);
        }

        private void OnPlannerLog(string message)
        {
            LogPlanner(message);
        }

        private void LogPlanner(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            plannerLogBuffer.Add(new LogLine(
                DateTime.Now.ToString("HH:mm:ss.fff"), message));
        }

        public void LogPlannerMessage(string text)
        {
            LogPlanner(text);
        }

        private void LogLiveSeatChanges(SessionSnapshot snapshot)
        {
            if (snapshot == null) return;
            if (snapshot.RosterVersion != liveEventRosterVersion)
            {
                bool first = !liveEventStarted;
                liveEventStarted = true;
                liveEventRosterVersion = snapshot.RosterVersion;
                liveEventSignatures.Clear();
                LogSession(first
                    ? "Live event stream started."
                    : "Live event stream reset: roster changed.", "Detail");
            }

            foreach (SeatSnapshot seat in snapshot.Seats)
            {
                if (seat == null) continue;

                string previous;
                bool hadPrevious = liveEventSignatures.TryGetValue(
                    seat.Seat, out previous);
                if (string.IsNullOrEmpty(seat.Name))
                {
                    if (hadPrevious && !string.IsNullOrEmpty(previous))
                        LogSession("Seat " + seat.Seat.ToString("D2") +
                            ": empty", "Detail");
                    liveEventSignatures[seat.Seat] = "";
                    continue;
                }

                string signature = BuildLiveSeatSignature(seat);
                if (hadPrevious && previous == signature) continue;
                liveEventSignatures[seat.Seat] = signature;
                LogSession(FormatLiveSeatEvent(seat), "Detail");
            }
        }

        private static string BuildLiveSeatSignature(SeatSnapshot seat)
        {
            return (seat.Name ?? "") + "|" + (seat.Room ?? "") + "|" +
                (seat.Action ?? "") + "|" + (seat.Status ?? "") + "|" +
                (seat.Target ?? "") + "|" + (seat.Mood ?? "") + "|" +
                (seat.Relationship ?? "") + "|" +
                (seat.IsLover ? "1" : "0") + "|" +
                (seat.IsFrozen ? "1" : "0") + "|" +
                (seat.IsBusy ? "1" : "0");
        }

        private static string FormatLiveSeatEvent(SeatSnapshot seat)
        {
            StringBuilder text = new StringBuilder();
            text.Append("Seat ").Append(seat.Seat.ToString("D2"))
                .Append(' ').Append(seat.Name);
            if (seat.IsPlayer) text.Append(" [PC]");
            if (!string.IsNullOrEmpty(seat.Room))
                text.Append(" | ").Append(seat.Room);
            if (!string.IsNullOrEmpty(seat.Status))
                text.Append(" | ").Append(seat.Status);
            if (!string.IsNullOrEmpty(seat.Action) && seat.Action != "None")
                text.Append(" | ").Append(seat.Action);
            if (!string.IsNullOrEmpty(seat.Target))
                text.Append(" | target ").Append(seat.Target);
            if (!string.IsNullOrEmpty(seat.Mood) && seat.Mood != "-")
                text.Append(" | mood ").Append(seat.Mood);
            if (!string.IsNullOrEmpty(seat.Relationship))
                text.Append(" | ").Append(seat.Relationship);
            if (seat.IsLover) text.Append(" | lover");
            if (seat.IsFrozen) text.Append(" | frozen");
            return text.ToString();
        }

        private void FlushLogs()
        {
            if (disposed) return;

            FlushLog(liveLogBuffer, LiveLines, liveLogBatch);
            FlushLog(plannerLogBuffer, PlannerLines, plannerLogBatch);
        }

        private static void FlushLog(LogBuffer buffer,
            ObservableCollection<LogLine> target, List<LogLine> batch)
        {
            batch.Clear();
            if (buffer.Drain(batch, LogDrainPerTick) == 0) return;

            int overflow = target.Count + batch.Count - LogLineLimit;
            if (overflow > 0)
            {
                // Trim some slack so a busy log does not remove items on every
                // flush while still keeping the collection at or below 5,000.
                int remove = Math.Min(target.Count,
                    Math.Max(overflow, LogTrimSlack));
                for (int i = 0; i < remove; i++) target.RemoveAt(0);
            }

            for (int i = 0; i < batch.Count; i++) target.Add(batch[i]);
        }

        public void ClearLog(bool planner)
        {
            if (planner)
            {
                plannerLogBuffer.Clear();
                PlannerLines.Clear();
            }
            else
            {
                liveLogBuffer.Clear();
                LiveLines.Clear();
            }
        }

        private void RefreshPlannerNow()
        {
            if (session == null || SelectedSeat == null || PlannerSeat < 0)
                return;
            PlannerSnapshot snapshot = session.Planner.BuildSnapshot(
                PlannerSeat, PlannerActorName);
            ApplyPlannerSnapshot(snapshot);
        }

        private void RefreshPlannerSnapshot(SessionSnapshot sessionSnapshot)
        {
            if (sessionSnapshot == null || sessionSnapshot.Options != null)
                RefreshPlannerOptions(sessionSnapshot);
            RefreshPlannerSeats();
            if (session == null || SelectedSeat == null || PlannerSeat < 0)
                return;
            ApplyPlannerSnapshot(session.Planner.BuildSnapshot(
                PlannerSeat, PlannerActorName));
        }

        private void RefreshPlannerOptions(SessionSnapshot snapshot)
        {
            CharacterOptions options = snapshot.Options;
            if (options == null || ReferenceEquals(options, plannerOptionsSource))
                return;
            plannerOptionsSource = options;

            PlannerActions.Clear();
            foreach (CharacterOption option in options.Actions)
                PlannerActions.Add(option);
            PlannerRooms.Clear();
            foreach (CharacterOption option in options.Rooms)
                PlannerRooms.Add(option);

            selectedPlannerAction = PlannerActions.FirstOrDefault();
            selectedPlannerRoom = PlannerRooms.FirstOrDefault();
            OnPropertyChanged(nameof(SelectedPlannerAction));
            OnPropertyChanged(nameof(SelectedPlannerRoom));
            RefreshLimitActions(options);
        }

        private void RefreshPlannerSeats()
        {
            if (SelectedSeat == null) return;
            string signature = SelectedSeat.SeatIndex.ToString();
            foreach (SeatItem seat in Seats)
            {
                if (seat == null) continue;
                signature += "|" + seat.SeatIndex + ":" + seat.Name + ":" +
                    seat.HasName;
            }
            if (signature == plannerSeatSignature) return;
            plannerSeatSignature = signature;

            int keepTarget = selectedPlannerTarget == null
                ? -1 : selectedPlannerTarget.Seat;
            int keepAbout = selectedPlannerAbout == null
                ? -1 : selectedPlannerAbout.Seat;

            PlannerTargetOptions.Clear();
            PlannerAboutOptions.Clear();
            PlannerTargetOptions.Add(new PlannerSeatOption(-1, "None"));
            PlannerAboutOptions.Add(new PlannerSeatOption(-1, "None"));

            foreach (SeatItem seat in Seats)
            {
                if (seat == null || seat.SeatIndex < 0 || !seat.HasName)
                    continue;
                if (seat.SeatIndex == SelectedSeat.SeatIndex) continue;
                PlannerSeatOption option = new PlannerSeatOption(
                    seat.SeatIndex, seat.Seat + "  " + seat.Name);
                PlannerTargetOptions.Add(option);
                PlannerAboutOptions.Add(new PlannerSeatOption(
                    seat.SeatIndex, seat.Seat + "  " + seat.Name));
            }

            selectedPlannerTarget = PlannerTargetOptions.FirstOrDefault(
                option => option.Seat == keepTarget) ??
                PlannerTargetOptions[0];
            selectedPlannerAbout = PlannerAboutOptions.FirstOrDefault(
                option => option.Seat == keepAbout) ??
                PlannerAboutOptions[0];
            OnPropertyChanged(nameof(SelectedPlannerTarget));
            OnPropertyChanged(nameof(SelectedPlannerAbout));
        }

        private void ApplyPlannerSnapshot(PlannerSnapshot snapshot)
        {
            if (snapshot == null) return;
            PlannerRunning = snapshot.Running;
            PlannerStatus = snapshot.Status;
            PlannerHookState = snapshot.HookState;
            PlannerHookInstalled = snapshot.HookInstalled;
            PlannerHookButtonText = snapshot.HookInstalled
                ? "Uninstall hook" : "Install hook";
            PlannerObserveOnly = snapshot.ObserveOnly;
            PlannerModeIndex = (int)snapshot.Mode;
            PlannerTriggerIndex = (int)snapshot.Trigger;
            PlannerRepeat = snapshot.Repeat;
            PlannerShuffle = snapshot.Shuffle;

            bool rebuild = PlannerSteps.Count != snapshot.Steps.Count;
            if (!rebuild)
            {
                for (int i = 0; i < PlannerSteps.Count; i++)
                {
                    if (!ReferenceEquals(PlannerSteps[i].Step,
                        snapshot.Steps[i].Step))
                    {
                        rebuild = true;
                        break;
                    }
                }
            }

            if (rebuild)
            {
                PlannerSteps.Clear();
                foreach (PlannerStepItem item in snapshot.Steps)
                    PlannerSteps.Add(item);
                selectedPlannerStepIndex = PlannerSteps.Count > 0 ? 0 : -1;
                OnPropertyChanged(nameof(SelectedPlannerStepIndex));
            }
            else
            {
                for (int i = 0; i < PlannerSteps.Count; i++)
                {
                    PlannerSteps[i].Number = i + 1;
                    PlannerSteps[i].Refresh();
                }
            }

            OnPropertyChanged(nameof(CanStartPlanner));
            OnPropertyChanged(nameof(CanForcePlanner));
            OnPropertyChanged(nameof(CanEditPlanner));
            OnPropertyChanged(nameof(CanRemovePlannerStep));
            OnPropertyChanged(nameof(CanCopyPlanner));
            OnPropertyChanged(nameof(CanPastePlanner));
            OnPropertyChanged(nameof(PlannerStopEnabled));
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
            LogSession(RosterStatus, result.Success ? "Info" : "Error");
        }

        private void OnRosterActionCompleted(RosterActionResult result)
        {
            if (result == null) return;
            RosterStatus = result.Success
                ? result.Message
                : "Action failed: " +
                    (string.IsNullOrEmpty(result.Error)
                        ? "unknown error" : result.Error);

            if (result.Kind == 1)
            {
                LoverRowItem row = LoverRows.FirstOrDefault(
                    candidate => candidate.SourceSeat == result.SourceSeat);
                if (row != null)
                {
                    if (!result.Success)
                        row.SetLoverSilently(!result.Flag);
                    else
                        row.SetLoverSilently(result.Flag);
                }
                LoverEditStatus = result.Success
                    ? result.Message : RosterStatus;
            }

            LogSession(RosterStatus, result.Success ? "Info" : "Error");
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
            LogSession(CharacterStatus, result.Success ? "Info" : "Error");
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
            LogSession(TraitStatus, result.Success ? "Info" : "Error");
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
            LogSession(BodyStatus, result.Success ? "Info" : "Error");
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
            LogSession(FaceStatus, result.Success ? "Info" : "Error");
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
            LogSession(HairStatus, result.Success ? "Info" : "Error");
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
            LogSession(OutfitStatus, result.Success ? "Info" : "Error");
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
                {
                    IsTimeFrozen = true;
                    LogSession("Time frozen at " +
                        (milliseconds / 60000).ToString("00") + ":" +
                        ((milliseconds / 1000) % 60).ToString("00") + ".");
                }
            }
            else
            {
                session.RequestTimeUnfreeze();
                IsTimeFrozen = false;
                LogSession("Time freeze stopped.");
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
            LogSession(ScheduleStatus, result.Success ? "Info" : "Error");
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
            if (logFlushTimer != null)
            {
                logFlushTimer.Stop();
                logFlushTimer = null;
            }
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
            session.OptionWriteCompleted -= OnOptionWriteCompleted;
            session.GameplayWriteCompleted -= OnGameplayWriteCompleted;
            session.LiveLog -= OnLiveLog;
            session.Planner.Log -= OnPlannerLog;
            session.Planner.StateChanged -= RefreshPlannerNow;
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
                NameTableStatus = snapshot.NameTableStatus;
                WriteGate = snapshot.WriteGate;
                UpdatedText = snapshot.UpdatedText;
                SelectedCharacter = snapshot.Character;
                if (snapshot.Options != null) CharacterOptions = snapshot.Options;
                UpdateCharacterDraft(snapshot);
                UpdateAauModules(snapshot);
                UpdateTraitDraft(snapshot);
                UpdateBodyDraft(snapshot);
                UpdateFaceDraft(snapshot);
                UpdateHairDraft(snapshot);
                UpdateOutfitDraft(snapshot);
                UpdateScheduleDraft(snapshot);
                IsTimeFrozen = snapshot.TimeFrozen;
                UpdateGameOptions(snapshot);
                UpdateSaveContext(snapshot);
                if (snapshot.IsAttached)
                {
                    ApplyDebugFields(snapshot);
                }
                else
                {
                    ClearDebugFields();
                    DebugEmptyText =
                        "Not attached. Debug data appears when AA2Play is attached.";
                }

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
                    seat.ApplyLiveDetails(seatSnapshot.Personality,
                        seatSnapshot.Target, seatSnapshot.Location,
                        seatSnapshot.Mood);
                    seat.TargetSeatItem = seatSnapshot.TargetSeat >= 0 &&
                        seatSnapshot.TargetSeat < Seats.Count &&
                        seatSnapshot.TargetSeat != seatSnapshot.Seat
                        ? Seats[seatSnapshot.TargetSeat] : null;
                    seat.ApplyLivePortrait(seatSnapshot.Thumbnail,
                        seatSnapshot.FullPortrait);
                    seat.HasActionLimits = seatSnapshot.HasActionLimits;
                }

                LogLiveSeatChanges(snapshot);
                UpdateSeatRail();

                if (SelectedSeat == null || !SelectedSeat.HasName)
                {
                    if (LimitActions.Count > 0) LimitActions.Clear();
                    UpdateLimitCharacterStatus();
                    UpdateLimitEmptyText();
                }
                else if (LimitActions.Count == 0 && characterOptions != null)
                {
                    RefreshLimitActionsForCurrentSeat();
                }

                UpdatePortraitStatus();
                ApplyRosterFilter();
                RefreshPlannerSnapshot(snapshot);
                RefreshGameplayTables(snapshot);

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

        private void UpdateAauModules(SessionSnapshot snapshot)
        {
            AauModuleStatus = snapshot == null
                ? "No AAU card data"
                : snapshot.AauModuleStatus ?? "";
            List<AauModuleInfo> modules = snapshot == null
                ? null : snapshot.AauModules;
            string signature = BuildAauModulesSignature(modules);
            if (signature == aauModuleSignature) return;

            aauModuleSignature = signature;
            AauModules.Clear();
            if (modules == null) return;
            foreach (AauModuleInfo module in modules)
            {
                if (module == null) continue;
                AauModules.Add(new AauModuleItem
                {
                    Name = module.Name ?? "",
                    Description = module.Description ?? ""
                });
            }
        }

        private static string BuildAauModulesSignature(
            List<AauModuleInfo> modules)
        {
            if (modules == null || modules.Count == 0) return "";
            StringBuilder text = new StringBuilder();
            foreach (AauModuleInfo module in modules)
            {
                if (module == null) continue;
                text.Append(module.Name).Append('\u001f')
                    .Append(module.Description).Append('\u001e');
            }
            return text.ToString();
        }

        public void RefreshAauModules()
        {
            if (session != null) session.RequestAauModuleRefresh();
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
            bool optionsChanged =
                !ReferenceEquals(faceOptionsSource, CharacterOptions);

            liveFaceValues = snapshot.FaceValues;
            liveFaceTexts = snapshot.FaceTexts;

            if (!sameCharacter ||
                (optionsChanged && (FaceDraft == null || !FaceDraft.IsDirty)))
            {
                FieldDraft draft = new FieldDraft(AppearanceFields.Face(),
                    FaceChoice);
                draft.Load(snapshot.FaceValues, snapshot.FaceTexts,
                    snapshot.SelectedPointer, snapshot.SelectedSeat);
                FaceDraft = draft;
                faceOptionsSource = CharacterOptions;
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
            bool optionsChanged =
                !ReferenceEquals(outfitOptionsSource, CharacterOptions);

            liveOutfitValues = snapshot.OutfitValues;

            if (!sameCharacter ||
                (optionsChanged && (OutfitDraft == null || !OutfitDraft.IsDirty)))
            {
                OutfitDraft draft = new OutfitDraft(CharacterOptions,
                    snapshot.SelectedGender != 0);
                draft.Load(snapshot.OutfitValues, snapshot.SelectedPointer,
                    snapshot.SelectedSeat);
                OutfitDraft = draft;
                outfitOptionsSource = CharacterOptions;
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

        private static bool IsNamedSeat(object item)
        {
            SeatItem seat = item as SeatItem;
            return seat != null && seat.HasName;
        }

        private void UpdateSeatRail()
        {
            if (SeatRailView == null) return;

            StringBuilder signature = new StringBuilder();
            int count = 0;
            SeatItem firstNamed = null;
            foreach (SeatItem seat in Seats)
            {
                if (seat == null || !seat.HasName) continue;
                count++;
                if (firstNamed == null) firstNamed = seat;
                if (signature.Length > 0) signature.Append(',');
                signature.Append(seat.SeatIndex);
            }

            string text = signature.ToString();
            if (text == seatRailSignature) return;
            seatRailSignature = text;
            namedSeatCount = count;

            SeatItem keep = SelectedSeat;
            SeatRailView.Refresh();
            OnPropertyChanged(nameof(SeatRailStatus));

            if (keep != null && keep.HasName && SeatRailView.Contains(keep))
            {
                if (!ReferenceEquals(SelectedSeat, keep))
                    SelectedSeat = keep;
            }
            else if (firstNamed != null &&
                !ReferenceEquals(SelectedSeat, firstNamed))
            {
                SelectedSeat = firstNamed;
            }
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
            UpdateRosterEmptyState();
        }

        private void UpdateRosterEmptyState()
        {
            if (!isAttached)
            {
                RosterEmptyText =
                    "Not attached. Start AA2Play and press Attach.";
                return;
            }

            bool anyNamed = false;
            int namedVisible = 0;
            foreach (SeatItem seat in Seats)
            {
                if (!seat.HasName) continue;
                anyNamed = true;
                if (!seat.IsFilteredOut) namedVisible++;
            }

            if (namedVisible > 0)
            {
                RosterEmptyText = "";
                return;
            }

            RosterEmptyText = anyNamed
                ? "No seats match the current filter."
                : "No class loaded. Load a class/save in AA2Play.";
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
            if (cardIndexRunning) return;
            cardIndexRunning = true;

            string[] roots = FindCardRoots();
            if (roots.Length == 0)
            {
                CardStatus = "Portraits: none";
                cardIndexRunning = false;
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
                    foreach (string file in found)
                    {
                        string name = Path.GetFileName(file);
                        if (name.IndexOf("_edit", StringComparison.OrdinalIgnoreCase) >= 0)
                            continue;
                        files.Add(file);
                    }
                }
                catch { }
            }

            if (files.Count == 0)
            {
                CardStatus = "Portraits: none";
                cardIndexRunning = false;
                return;
            }

            CardStatus = "Portraits: loading card fallback...";
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
                CardStatus = "Portraits: card index failed";
                cardIndexRunning = false;
                return;
            }

            if (disposed || Application.Current == null)
            {
                cardIndexRunning = false;
                return;
            }
            Application.Current.Dispatcher.Invoke(delegate
            {
                if (disposed) return;
                cardIndex.Clear();
                cardIndex.AddRange(entries);
                loadedCards.Clear();
                failedCards.Clear();
                if (cardIndex.Count == 0)
                {
                    CardStatus = "Portraits: none";
                    cardIndexRunning = false;
                    return;
                }
                AssignCards();
                cardIndexRunning = false;
            });
        }

        /// <summary>
        /// Matches cards to live names exactly. Card names are never applied to
        /// seats and are never translated; unmatched seats keep the initial
        /// tile. Images are decoded only for matched cards.
        /// </summary>
        private void AssignCards()
        {
            if (disposed) return;
            if (cardIndex.Count == 0)
            {
                UpdatePortraitStatus();
                return;
            }

            HashSet<CardEntry> used = new HashSet<CardEntry>();
            List<CardEntry> pending = new List<CardEntry>();

            foreach (SeatItem seat in Seats)
            {
                if (!seat.HasName)
                {
                    seat.ClearCardArt();
                    continue;
                }

                CardEntry match = cardIndex.FirstOrDefault(
                    card => !used.Contains(card) &&
                            !failedCards.Contains(card.Path) &&
                            NamesMatch(seat, card));
                if (match == null)
                {
                    seat.ClearCardArt();
                    continue;
                }

                used.Add(match);

                CardImages images;
                if (loadedCards.TryGetValue(match.Path, out images))
                {
                    AssignImages(seat, images);
                }
                else
                {
                    seat.ClearCardArt();
                    pending.Add(match);
                }
            }

            if (pending.Count > 0) LoadCardImages(pending);

            UpdatePortraitStatus();
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

        /// <summary>
        /// Reports where each named seat's art came from: live memory first,
        /// then a card file, then nothing.
        /// </summary>
        private void UpdatePortraitStatus()
        {
            int named = 0;
            int live = 0;
            int card = 0;
            int none = 0;

            foreach (SeatItem seat in Seats)
            {
                if (seat == null || !seat.HasName) continue;
                named++;
                if (seat.HasLiveRosterImage) live++;
                else if (seat.HasCardFallback) card++;
                else none++;
            }

            if (named == 0)
            {
                CardStatus = "Portraits: none";
                return;
            }

            string text = "Portraits: " + live + " live";
            if (card > 0) text += ", " + card + " card";
            if (none > 0) text += ", " + none + " none";
            CardStatus = text;
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
                    Mood = "-"
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
            LiveLines.Add(new LogLine(DateTime.Now.ToString("HH:mm:ss.fff"),
                "AA2 Trainer ready. Attach to AA2Play to begin."));
        }

        public void LogSession(string text, string level = "Info")
        {
            if (string.IsNullOrEmpty(text)) return;
            liveLogBuffer.Add(new LogLine(
                DateTime.Now.ToString("HH:mm:ss.fff"), text, level));
        }

    }
}
