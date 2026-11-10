using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Threading;
using AA2Trainer;

namespace AA2Trainer.WpfPrototype
{
    /// <summary>
    /// Owns the game process. All memory reads happen on the poll thread; the UI
    /// receives one coalesced SessionSnapshot at a time on the Dispatcher.
    /// </summary>
    public sealed class GameSessionService : IDisposable
    {
        private const int PollMs = 250;
        private const int ContextRefreshMs = 1000;
        private const int HealthRefreshMs = 5000;
        private const int AttachRetryMs = 1000;
        private const int DisconnectedPublishMs = 1000;

        private readonly GameMemory mem = new GameMemory();
        private readonly LiveReader live;
        private readonly NameMap names = new NameMap();
        private readonly Dispatcher dispatcher;

        private GameContext ctx = new GameContext();
        private readonly System.Threading.Timer timer;
        private PointerHealth health;
        private ScheduleSnapshot schedule;
        private CharacterDetail selectedCharacterDetail;
        private CharacterSnapshot selectedCharacterSnapshot;
        private bool[] selectedTraitValues;
        private int[,] selectedRiskValues;
        private int[] selectedBodyValues;
        private CharacterOptions characterOptions;
        private string clubSignature = "";
        private readonly object writeSync = new object();
        private bool writePending;
        private uint writePointer;
        private int writeSeat = -1;
        private CharacterSnapshot writeOriginal;
        private CharacterSnapshot writeEdited;
        private readonly object traitWriteSync = new object();
        private bool traitWritePending;
        private uint traitWritePointer;
        private int traitWriteSeat = -1;
        private bool[] traitWriteOriginal;
        private bool[] traitWriteEdited;
        private int[,] riskWriteOriginal;
        private int[,] riskWriteEdited;
        private readonly object bodyWriteSync = new object();
        private bool bodyWritePending;
        private uint bodyWritePointer;
        private int bodyWriteSeat = -1;
        private int[] bodyWriteOriginal;
        private int[] bodyWriteEdited;
        private readonly object faceWriteSync = new object();
        private bool faceWritePending;
        private uint faceWritePointer;
        private int faceWriteSeat = -1;
        private int[] faceWriteOriginalValues;
        private int[] faceWriteEditedValues;
        private string[] faceWriteOriginalTexts;
        private string[] faceWriteEditedTexts;
        private readonly object hairWriteSync = new object();
        private bool hairWritePending;
        private uint hairWritePointer;
        private int hairWriteSeat = -1;
        private int[] hairWriteOriginal;
        private int[] hairWriteEdited;
        private readonly object outfitWriteSync = new object();
        private bool outfitWritePending;
        private uint outfitWritePointer;
        private int outfitWriteSeat = -1;
        private int[] outfitWriteOriginal;
        private int[] outfitWriteEdited;
        private readonly object playerSwitchSync = new object();
        private bool playerSwitchPending;
        private int playerSwitchSeat = -1;
        private readonly object rosterActionSync = new object();
        private bool rosterActionPending;
        private int rosterActionKind;
        private int rosterActionSourceSeat = -1;
        private int rosterActionTargetSeat = -1;
        private int rosterActionFeeling;
        private bool rosterActionFlag;
        private readonly object relationshipFreezeSync = new object();
        private readonly Dictionary<long, FrozenRelationship> relationshipFreezes =
            new Dictionary<long, FrozenRelationship>();
        private DateTime lastFreezeUtc = DateTime.MinValue;
        private readonly object scheduleWriteSync = new object();
        private bool scheduleWritePending;
        private ScheduleSnapshot scheduleWriteOriginal;
        private ScheduleSnapshot scheduleWriteEdited;
        private int? timeFreezeMilliseconds;
        private int timeFreezeFailures;
        private DateTime lastPinUtc = DateTime.MinValue;
        private int selectedGender;
        private int[] selectedFaceValues;
        private string[] selectedFaceTexts;
        private int[] selectedHairValues;
        private int[] selectedOutfitValues;
        private readonly Dictionary<int, string> relationships =
            new Dictionary<int, string>();
        private readonly Dictionary<int, string> headerRelationships =
            new Dictionary<int, string>();
        private readonly Dictionary<int, bool> hasRelationships =
            new Dictionary<int, bool>();
        private readonly Dictionary<int, bool> isLovers =
            new Dictionary<int, bool>();
        private readonly Dictionary<int, bool> otherIsLovers =
            new Dictionary<int, bool>();
        private readonly Dictionary<int, bool> frozenRelationships =
            new Dictionary<int, bool>();
        private DateTime lastContextUtc = DateTime.MinValue;
        private DateTime lastHealthUtc = DateTime.MinValue;
        private DateTime lastAttachUtc = DateTime.MinValue;
        private DateTime lastDisconnectedUtc = DateTime.MinValue;
        private int pendingSeat = -1;
        private int busy;
        private int uiQueued;
        private long sequence;
        private volatile bool attachRequested = true;
        private volatile bool detachRequested;
        private volatile bool disposed;
        private SessionSnapshot pendingSnapshot;

        public GameSessionService(Dispatcher dispatcher)
        {
            this.dispatcher = dispatcher;
            live = new LiveReader(mem);
            timer = new System.Threading.Timer(Poll, null, 0, PollMs);
        }

        public event Action<SessionSnapshot> SnapshotReady;
        public event Action<CharacterWriteResult> CharacterWriteCompleted;
        public event Action<TraitWriteResult> TraitWriteCompleted;
        public event Action<BodyWriteResult> BodyWriteCompleted;
        public event Action<FaceWriteResult> FaceWriteCompleted;
        public event Action<HairWriteResult> HairWriteCompleted;
        public event Action<OutfitWriteResult> OutfitWriteCompleted;
        public event Action<PlayerSwitchResult> PlayerSwitchCompleted;
        public event Action<RosterActionResult> RosterActionCompleted;
        public event Action<ScheduleWriteResult> ScheduleWriteCompleted;

        public bool IsAttached { get { return mem.IsAttached; } }

        public void RequestAttach()
        {
            detachRequested = false;
            attachRequested = true;
            lastAttachUtc = DateTime.MinValue;
            lastContextUtc = DateTime.MinValue;
            lastHealthUtc = DateTime.MinValue;
        }

        public void RequestDetach()
        {
            attachRequested = false;
            detachRequested = true;
        }

        public void RequestRefresh()
        {
            RequestAttach();
        }

        public void RequestSeat(int seat)
        {
            if (seat < 0 || seat > 24) return;
            Interlocked.Exchange(ref pendingSeat, seat);
        }

        /// <summary>
        /// Queues a write for the poll thread. Returns false when a write is
        /// already queued or the game is not attached.
        /// </summary>
        public bool RequestCharacterWrite(uint pointer, int seat,
            CharacterSnapshot original, CharacterSnapshot edited)
        {
            if (disposed || !mem.IsAttached || pointer == 0 ||
                original == null || edited == null)
                return false;

            lock (writeSync)
            {
                if (writePending) return false;
                writePointer = pointer;
                writeSeat = seat;
                writeOriginal = original;
                writeEdited = edited;
                writePending = true;
                return true;
            }
        }

        public bool RequestTraitWrite(uint pointer, int seat,
            bool[] originalTraits, bool[] editedTraits,
            int[,] originalRisk, int[,] editedRisk)
        {
            if (disposed || !mem.IsAttached || pointer == 0 ||
                originalTraits == null || editedTraits == null ||
                originalRisk == null || editedRisk == null)
                return false;

            lock (traitWriteSync)
            {
                if (traitWritePending) return false;
                traitWritePointer = pointer;
                traitWriteSeat = seat;
                traitWriteOriginal = originalTraits;
                traitWriteEdited = editedTraits;
                riskWriteOriginal = originalRisk;
                riskWriteEdited = editedRisk;
                traitWritePending = true;
                return true;
            }
        }

        public bool RequestBodyWrite(uint pointer, int seat,
            int[] original, int[] edited)
        {
            if (disposed || !mem.IsAttached || pointer == 0 ||
                original == null || edited == null)
                return false;

            lock (bodyWriteSync)
            {
                if (bodyWritePending) return false;
                bodyWritePointer = pointer;
                bodyWriteSeat = seat;
                bodyWriteOriginal = original;
                bodyWriteEdited = edited;
                bodyWritePending = true;
                return true;
            }
        }

        public bool RequestFaceWrite(uint pointer, int seat,
            int[] originalValues, int[] editedValues,
            string[] originalTexts, string[] editedTexts)
        {
            if (disposed || !mem.IsAttached || pointer == 0 ||
                originalValues == null || editedValues == null ||
                originalTexts == null || editedTexts == null)
                return false;

            lock (faceWriteSync)
            {
                if (faceWritePending) return false;
                faceWritePointer = pointer;
                faceWriteSeat = seat;
                faceWriteOriginalValues = originalValues;
                faceWriteEditedValues = editedValues;
                faceWriteOriginalTexts = originalTexts;
                faceWriteEditedTexts = editedTexts;
                faceWritePending = true;
                return true;
            }
        }

        public bool RequestHairWrite(uint pointer, int seat,
            int[] original, int[] edited)
        {
            if (disposed || !mem.IsAttached || pointer == 0 ||
                original == null || edited == null)
                return false;

            lock (hairWriteSync)
            {
                if (hairWritePending) return false;
                hairWritePointer = pointer;
                hairWriteSeat = seat;
                hairWriteOriginal = original;
                hairWriteEdited = edited;
                hairWritePending = true;
                return true;
            }
        }

        public bool RequestOutfitWrite(uint pointer, int seat,
            int[] original, int[] edited)
        {
            if (disposed || !mem.IsAttached || pointer == 0 ||
                original == null || edited == null)
                return false;

            lock (outfitWriteSync)
            {
                if (outfitWritePending) return false;
                outfitWritePointer = pointer;
                outfitWriteSeat = seat;
                outfitWriteOriginal = original;
                outfitWriteEdited = edited;
                outfitWritePending = true;
                return true;
            }
        }

        public bool RequestPlayerSwitch(int seat)
        {
            if (disposed || !mem.IsAttached || seat < 0 || seat > 24)
                return false;

            lock (playerSwitchSync)
            {
                if (playerSwitchPending) return false;
                playerSwitchSeat = seat;
                playerSwitchPending = true;
                return true;
            }
        }

        public bool RequestRelationshipFeeling(int sourceSeat, int targetSeat,
            int feeling)
        {
            return QueueRosterAction(0, sourceSeat, targetSeat, feeling, false);
        }

        public bool RequestLover(int sourceSeat, int targetSeat, bool lovers)
        {
            return QueueRosterAction(1, sourceSeat, targetSeat, 0, lovers);
        }

        public bool RequestRelationshipFreeze(int sourceSeat, int targetSeat,
            bool frozen)
        {
            return QueueRosterAction(2, sourceSeat, targetSeat, 0, frozen);
        }

        private bool QueueRosterAction(int kind, int sourceSeat, int targetSeat,
            int feeling, bool flag)
        {
            if (disposed || !mem.IsAttached ||
                sourceSeat < 0 || sourceSeat > 24 ||
                targetSeat < 0 || targetSeat > 24 ||
                sourceSeat == targetSeat)
                return false;

            lock (rosterActionSync)
            {
                if (rosterActionPending) return false;
                rosterActionKind = kind;
                rosterActionSourceSeat = sourceSeat;
                rosterActionTargetSeat = targetSeat;
                rosterActionFeeling = feeling;
                rosterActionFlag = flag;
                rosterActionPending = true;
                return true;
            }
        }

        public bool RequestScheduleWrite(ScheduleSnapshot original,
            ScheduleSnapshot edited)
        {
            if (disposed || !mem.IsAttached || original == null || edited == null)
                return false;

            lock (scheduleWriteSync)
            {
                if (scheduleWritePending) return false;
                scheduleWriteOriginal = original;
                scheduleWriteEdited = edited;
                scheduleWritePending = true;
                return true;
            }
        }

        public bool RequestTimeFreeze(int milliseconds)
        {
            if (disposed || !mem.IsAttached) return false;
            timeFreezeMilliseconds = Math.Max(0,
                Math.Min(ScheduleEditor.MaxTimeMilliseconds, milliseconds));
            timeFreezeFailures = 0;
            lastPinUtc = DateTime.MinValue;
            return true;
        }

        public void RequestTimeUnfreeze()
        {
            timeFreezeMilliseconds = null;
            timeFreezeFailures = 0;
        }

        public void Dispose()
        {
            disposed = true;
            using (ManualResetEvent done = new ManualResetEvent(false))
            {
                try
                {
                    if (timer.Dispose(done)) done.WaitOne(1000);
                }
                catch (ObjectDisposedException) { }
            }
            live.Clear();
            mem.Dispose();
            SnapshotReady = null;
            CharacterWriteCompleted = null;
            TraitWriteCompleted = null;
            BodyWriteCompleted = null;
            FaceWriteCompleted = null;
            HairWriteCompleted = null;
            OutfitWriteCompleted = null;
            PlayerSwitchCompleted = null;
            RosterActionCompleted = null;
            ScheduleWriteCompleted = null;
        }

        private void Poll(object state)
        {
            if (disposed) return;
            if (Interlocked.Exchange(ref busy, 1) == 1) return;

            try
            {
                if (detachRequested)
                {
                    detachRequested = false;
                    DetachInternal();
                    return;
                }

                if (!mem.IsAttached)
                {
                    DateTime now = DateTime.UtcNow;
                    if (attachRequested &&
                        (now - lastAttachUtc).TotalMilliseconds >= AttachRetryMs)
                    {
                        lastAttachUtc = now;
                        TryAttach();
                    }

                    if (!mem.IsAttached)
                    {
                        PublishDisconnected();
                        return;
                    }
                }

                PollAttached();
            }
            catch
            {
                // A transient read failure must not kill the poller; the next
                // tick retries, and the full context refresh repairs pointers.
            }
            finally
            {
                Interlocked.Exchange(ref busy, 0);
            }
        }

        private void TryAttach()
        {
            if (!mem.Attach())
            {
                lastDisconnectedUtc = DateTime.MinValue;
                return;
            }

            ctx = new GameContext();
            bool hasClass = ctx.Refresh(mem, 0);
            names.Load(NameMap.FindGameRoot() ?? "");
            selectedGender = ReadSelectedGender();
            characterOptions = BuildCharacterOptions(selectedGender);
            clubSignature = BuildClubSignature() + "/g" + selectedGender;
            live.PublishRoster(ctx.Characters, ctx.Selected);
            health = hasClass ? PointerHealth.Run(mem, ctx) : null;
            lastContextUtc = DateTime.UtcNow;
            lastHealthUtc = DateTime.UtcNow;
            RefreshRelations();
            RefreshSelectedCharacter();
        }

        private void DetachInternal()
        {
            CancelPendingWrite("Detached.");
            CancelPendingTraitWrite("Detached.");
            CancelPendingBodyWrite("Detached.");
            CancelPendingFaceWrite("Detached.");
            CancelPendingHairWrite("Detached.");
            CancelPendingOutfitWrite("Detached.");
            CancelPendingPlayerSwitch("Detached.");
            CancelPendingRosterAction("Detached.");
            lock (relationshipFreezeSync) relationshipFreezes.Clear();
            CancelPendingScheduleWrite("Detached.");
            timeFreezeMilliseconds = null;
            live.Clear();
            mem.Detach();
            ctx = new GameContext();
            health = null;
            schedule = null;
            selectedCharacterDetail = null;
            selectedCharacterSnapshot = null;
            selectedTraitValues = null;
            selectedRiskValues = null;
            selectedBodyValues = null;
            lastContextUtc = DateTime.MinValue;
            lastHealthUtc = DateTime.MinValue;
            lastDisconnectedUtc = DateTime.MinValue;
            PublishDisconnected();
        }

        private void PollAttached()
        {
            ProcessCharacterWrite();
            ProcessTraitWrite();
            ProcessBodyWrite();
            ProcessFaceWrite();
            ProcessHairWrite();
            ProcessOutfitWrite();
            ProcessPlayerSwitch();
            ProcessRosterAction();
            RestoreRelationshipFreezes();
            ProcessScheduleWrite();
            PinTime();

            int requested = Interlocked.Exchange(ref pendingSeat, -1);
            if (requested >= 0)
            {
                CharacterInfo info = ctx.Characters.FirstOrDefault(
                    c => c != null && c.Seat == requested);
                if (info != null)
                {
                    ctx.Select(mem, info);
                    live.PublishRoster(ctx.Characters, ctx.Selected);
                    RefreshRelations();
                    RefreshSelectedCharacter();
                }
            }

            DateTime now = DateTime.UtcNow;
            if ((now - lastContextUtc).TotalMilliseconds >= ContextRefreshMs)
            {
                lastContextUtc = now;
                uint preferred = ctx.Selected == null ? 0 : ctx.Selected.Pointer;
                bool hasClass = ctx.Refresh(mem, preferred);
                if (!hasClass)
                {
                    live.Clear();
                    selectedCharacterDetail = null;
                    selectedCharacterSnapshot = null;
                    selectedTraitValues = null;
                    selectedRiskValues = null;
                    PublishAttachedNoClass();
                    return;
                }

                live.PublishRoster(ctx.Characters, ctx.Selected);
                RefreshRelations();
                RefreshSelectedCharacter();
                RefreshCharacterOptions();

                ScheduleSnapshot readSchedule;
                string scheduleError;
                schedule = ScheduleEditor.TryRead(mem, ctx, out readSchedule,
                    out scheduleError) ? readSchedule : null;
            }

            if ((now - lastHealthUtc).TotalMilliseconds >= HealthRefreshMs)
            {
                lastHealthUtc = now;
                health = PointerHealth.Run(mem, ctx);
            }

            LiveSnapshot liveSnapshot = live.Read();
            Publish(BuildSnapshot(liveSnapshot));
        }

        private SessionSnapshot BuildSnapshot(LiveSnapshot liveSnapshot)
        {
            SessionSnapshot snapshot = new SessionSnapshot();
            snapshot.Sequence = Interlocked.Increment(ref sequence);
            snapshot.IsAttached = mem.IsAttached;
            snapshot.AttachState = "Attached - " +
                (string.IsNullOrEmpty(mem.ProcessName) ? "AA2Play" : mem.ProcessName) +
                (string.IsNullOrEmpty(mem.GameVersion) ? "" : " " + mem.GameVersion);
            snapshot.GameTime = FormatSchedule(schedule);
            snapshot.PointerHealth = health == null
                ? "Pointers: checking..."
                : "Pointers: " + health.ShortStatus + " - " +
                  health.ElapsedMs.ToString() + " ms";
            snapshot.NamesState = "Names: " + names.StatusText;
            snapshot.WriteGate = mem.CanWrite
                ? "Write gate: open" : "Write gate: read-only";
            snapshot.UpdatedText = "Updated " + DateTime.Now.ToString("HH:mm:ss");
            snapshot.Character = selectedCharacterDetail;
            snapshot.RawCharacter = selectedCharacterSnapshot;
            snapshot.Options = characterOptions;
            snapshot.SelectedPointer = ctx.Selected == null ? 0 : ctx.Selected.Pointer;
            snapshot.TraitValues = selectedTraitValues;
            snapshot.RiskValues = selectedRiskValues;
            snapshot.BodyValues = selectedBodyValues;
            snapshot.SelectedGender = selectedGender;
            snapshot.FaceValues = selectedFaceValues;
            snapshot.FaceTexts = selectedFaceTexts;
            snapshot.HairValues = selectedHairValues;
            snapshot.OutfitValues = selectedOutfitValues;
            snapshot.Schedule = schedule;
            snapshot.TimeFrozen = timeFreezeMilliseconds.HasValue;
            snapshot.TimeFrozenMilliseconds =
                timeFreezeMilliseconds.GetValueOrDefault();

            Dictionary<int, LiveCharacterState> liveBySeat =
                new Dictionary<int, LiveCharacterState>();
            if (liveSnapshot != null && liveSnapshot.Characters != null)
            {
                foreach (LiveCharacterState state in liveSnapshot.Characters)
                {
                    if (state == null || state.Seat < 0 || state.Seat > 24)
                        continue;
                    if (!liveBySeat.ContainsKey(state.Seat))
                        liveBySeat[state.Seat] = state;
                }
            }

            CharacterInfo selected = ctx.Selected;
            snapshot.SelectedSeat = selected == null ? -1 : selected.Seat;

            for (int seat = 0; seat < 25; seat++)
            {
                CharacterInfo info = ctx.Characters.FirstOrDefault(
                    c => c != null && c.Seat == seat);
                if (info == null)
                {
                    snapshot.Seats.Add(new SeatSnapshot
                    {
                        Seat = seat,
                        Name = "",
                        Status = "Empty",
                        Action = "None",
                        Room = "",
                        Relationship = "",
                        HeaderRelationship = ""
                    });
                    continue;
                }

                LiveCharacterState liveState;
                liveBySeat.TryGetValue(seat, out liveState);

                int actionId = liveState == null ? -1 : liveState.ActionId;
                int locationId = liveState == null ? -1 : liveState.Location;
                string room = locationId >= 0
                    ? locationId.ToString() + " " + LocationName(locationId)
                    : "?";
                string action = actionId >= 0
                    ? ActionName(actionId) : "None";

                string relationship;
                relationships.TryGetValue(seat, out relationship);
                if (relationship == null) relationship = "-";

                string headerRelationship;
                headerRelationships.TryGetValue(seat, out headerRelationship);
                if (headerRelationship == null) headerRelationship = "";

                snapshot.Seats.Add(new SeatSnapshot
                {
                    Seat = seat,
                    Name = info.Name ?? "",
                    FamilyName = info.FamilyName ?? "",
                    GivenName = info.GivenName ?? "",
                    IsPlayer = info.IsPlayer,
                    Room = room,
                    Action = action,
                    Status = StatusLabel(liveState),
                    Relationship = relationship,
                    HeaderRelationship = headerRelationship,
                    IsBusy = liveState != null && liveState.IsBusy,
                    HasRelationship = hasRelationships.ContainsKey(seat) &&
                        hasRelationships[seat],
                    IsLover = isLovers.ContainsKey(seat) && isLovers[seat],
                    OtherIsLover = otherIsLovers.ContainsKey(seat) &&
                        otherIsLovers[seat],
                    IsFrozen = frozenRelationships.ContainsKey(seat) &&
                        frozenRelationships[seat]
                });
            }

            return snapshot;
        }

        private void RefreshRelations()
        {
            relationships.Clear();
            headerRelationships.Clear();
            hasRelationships.Clear();
            isLovers.Clear();
            otherIsLovers.Clear();
            frozenRelationships.Clear();

            CharacterInfo selected = ctx.Selected;
            CharacterInfo player = ctx.Characters.FirstOrDefault(
                c => c != null && c.IsPlayer) ??
                ctx.Characters.FirstOrDefault(c => c != null && c.Seat == 0);

            for (int seat = 0; seat < 25; seat++)
            {
                CharacterInfo info = ctx.Characters.FirstOrDefault(
                    c => c != null && c.Seat == seat);
                if (info == null)
                {
                    relationships[seat] = "";
                    headerRelationships[seat] = "";
                    hasRelationships[seat] = false;
                    isLovers[seat] = false;
                    otherIsLovers[seat] = false;
                    frozenRelationships[seat] = false;
                    continue;
                }

                RelationshipSnapshot relation = null;
                if (selected != null && info.Seat != selected.Seat)
                {
                    relation = RelationshipReader.Read(mem, info, selected.Seat);
                }

                if (selected != null && info.Seat == selected.Seat)
                {
                    relationships[seat] = "Self";
                }
                else
                {
                    relationships[seat] = RelationLabel(relation);
                }

                hasRelationships[seat] = relation != null;
                isLovers[seat] = relation != null && relation.IsLover;
                otherIsLovers[seat] = selected != null &&
                    info.Seat != selected.Seat &&
                    mem.ReadU8(selected.Pointer + LoverEditor.LoverOffset +
                        (uint)info.Seat).GetValueOrDefault() != 0;
                frozenRelationships[seat] = selected != null &&
                    info.Seat != selected.Seat &&
                    IsRelationshipFrozen(info.Pointer, selected.Seat);

                if (info.IsPlayer)
                {
                    headerRelationships[seat] = "Player";
                }
                else
                {
                    RelationshipSnapshot headerRelation = player == null ||
                        player.Seat == info.Seat ? null :
                        RelationshipReader.Read(mem, player, info.Seat);
                    headerRelationships[seat] = HeaderRelationLabel(headerRelation);
                }
            }
        }

        private void RefreshSelectedCharacter()
        {
            if (ctx.Selected == null)
            {
                selectedCharacterDetail = null;
                selectedCharacterSnapshot = null;
                selectedTraitValues = null;
                selectedRiskValues = null;
                selectedBodyValues = null;
                selectedFaceValues = null;
                selectedFaceTexts = null;
                selectedHairValues = null;
                selectedOutfitValues = null;
                return;
            }

            CharacterSnapshot snapshot;
            string error;
            if (!CharacterEditor.TryRead(mem, ctx.Selected,
                out snapshot, out error))
            {
                selectedCharacterDetail = null;
                selectedCharacterSnapshot = null;
                selectedTraitValues = null;
                selectedRiskValues = null;
                selectedBodyValues = null;
                selectedFaceValues = null;
                selectedFaceTexts = null;
                selectedHairValues = null;
                selectedOutfitValues = null;
                return;
            }

            selectedCharacterSnapshot = snapshot;
            selectedCharacterDetail = BuildCharacterDetail(snapshot);

            bool[] traits;
            string traitError;
            selectedTraitValues = TraitCatalog.TryRead(mem, ctx.Selected,
                out traits, out traitError) ? traits : null;

            int[,] risk;
            string riskError;
            selectedRiskValues = RiskSchedule.TryRead(mem, ctx.Selected,
                out risk, out riskError) ? risk : null;

            int[] body;
            string bodyError;
            selectedBodyValues = BodyEditor.TryRead(mem, ctx.Selected,
                out body, out bodyError) ? body : null;

            selectedGender = ReadSelectedGender();

            FaceSnapshot face;
            string faceError;
            if (FaceEditor.TryRead(mem, ctx.Selected, out face, out faceError))
            {
                selectedFaceValues = face.Values;
                selectedFaceTexts = face.Texts;
            }
            else
            {
                selectedFaceValues = null;
                selectedFaceTexts = null;
            }

            int[] hair;
            string hairError;
            selectedHairValues = HairEditor.TryRead(mem, ctx.Selected,
                out hair, out hairError) ? hair : null;

            OutfitSnapshot outfit;
            string outfitError;
            selectedOutfitValues = OutfitEditor.TryRead(mem, ctx.Selected,
                out outfit, out outfitError) ? outfit.Values : null;
        }

        private int ReadSelectedGender()
        {
            if (mem == null || !mem.IsAttached || ctx == null ||
                ctx.Selected == null)
                return 0;

            uint characterData;
            string error;
            if (!CharacterEditor.TryGetCharacterData(mem, ctx.Selected,
                out characterData, out error))
                return 0;

            byte? gender = mem.ReadU8(characterData + 0x3C);
            return gender.HasValue && gender.Value != 0 ? 1 : 0;
        }

        private CharacterOptions BuildCharacterOptions(int gender)
        {
            CharacterOptions options = new CharacterOptions();
            options.Personality = BuildOptions("PERSONALITY", 0, 29);
            options.Club = BuildClubOptions();
            options.Social = BuildOptions("LOW_HIGH", 0, 4);
            options.FightingStyle = BuildOptions("FIGHTSTYLE", 0, 2);
            options.Virtue = BuildOptions("LOW_HIGH", 0, 4);
            options.Orientation = BuildOptions("SEXUALITY", 0, 4);
            options.VoicePitch = BuildOptions("LOW_HIGH", 0, 4);
            options.Levels = BuildOptions("LOW_HIGHPLUS", 0, 4);
            options.ClubLevels = BuildNumbers(0, 8);
            options.Grades = BuildGrades();
            options.BodyChoices = BuildBodyChoices();
            options.FaceChoices = BuildFaceChoices(gender);
            options.HairChoices = BuildHairChoices();
            options.OutfitChoices = BuildOutfitChoices(gender);
            return options;
        }

        private Dictionary<string, List<CharacterOption>> BuildFaceChoices(int gender)
        {
            Dictionary<string, List<CharacterOption>> map =
                new Dictionary<string, List<CharacterOption>>(StringComparer.Ordinal);
            map["FaceType"] = BuildNameOptions(
                gender == 0 ? "FACETYPE_M" : "FACETYPE_F", true);
            map["IrisShape"] = BuildNameOptions("IRISSHAPE");
            map["IrisType"] = BuildNameOptions("IRISTYPE");
            map["HightlightType"] = BuildNameOptions("HIGHLIGHTTYPE");
            map["BrowShape"] = BuildNameOptions("EYEBROWSHAPE");
            map["Eyelid"] = BuildNameOptions("EYELIDTYPE");
            map["UpperEyelid"] = BuildNameOptions("EYELIDTYPEU");
            map["LowerEyelid"] = BuildNameOptions("EYELIDTYPEL");
            map["Glasses"] = BuildNameOptions("GLASSESTYPE");
            map["LipsticColor"] = BuildNameOptions("LIPSTICKCOLOR");
            return map;
        }

        private Dictionary<string, List<CharacterOption>> BuildHairChoices()
        {
            List<CharacterOption> styles = BuildNumbers(0, 255);
            Dictionary<string, List<CharacterOption>> map =
                new Dictionary<string, List<CharacterOption>>(StringComparer.Ordinal);
            map["FrontHair"] = styles;
            map["SideHair"] = styles;
            map["BackHair"] = styles;
            map["ExtHair"] = styles;
            return map;
        }

        private Dictionary<string, List<CharacterOption>> BuildOutfitChoices(int gender)
        {
            string clothes = gender == 0 ? "CLOTHES_M" : "CLOTHES_F";
            string socks = gender == 0 ? "SOCKS_M" : "SOCKS_F";
            string shoes = gender == 0 ? "SHOES_M" : "SHOES_F";

            List<CharacterOption> outfitTypes = BuildNameOptions(clothes, true);
            List<CharacterOption> sockTypes = BuildNameOptions(socks);
            List<CharacterOption> shoeTypes = BuildNameOptions(shoes);

            Dictionary<string, List<CharacterOption>> map =
                new Dictionary<string, List<CharacterOption>>(StringComparer.Ordinal);
            string[] prefixes = new string[] { "Outfit", "Sport", "Swim", "Club" };
            foreach (string prefix in prefixes)
            {
                map[prefix + "UType"] = outfitTypes;
                map[prefix + "Socks"] = sockTypes;
                map[prefix + "InShoes"] = shoeTypes;
                map[prefix + "OutShoes"] = shoeTypes;
            }
            return map;
        }

        private Dictionary<string, List<CharacterOption>> BuildBodyChoices()
        {
            Dictionary<string, List<CharacterOption>> map =
                new Dictionary<string, List<CharacterOption>>(StringComparer.Ordinal);
            map["BodyType"] = BuildLabels("Delicate", "Standard", "Tall", "Fat");
            map["Heights"] = BuildLabels("Low", "Standard", "High");
            map["Figure"] = BuildLabels("Slim", "Standard", "Fat");
            map["BreastShape"] = BuildNameOptions("BREASTSHAPE");
            map["BreastSoftness"] = BuildNameOptions("BREASTFIRMNESS");
            map["PubHairType"] = BuildNameOptions("PUBICHAIR");
            map["NippleType"] = BuildNameOptions("NIPPLETYPE");
            map["NippleColor"] = BuildNameOptions("NIPPLECOLOR");
            map["TanMark"] = BuildNameOptions("TANMARKS");
            map["PussyMosaic"] = BuildNameOptions("MOSAICCOLOR");
            return map;
        }

        private static List<CharacterOption> BuildLabels(params string[] labels)
        {
            List<CharacterOption> list = new List<CharacterOption>();
            for (int i = 0; i < labels.Length; i++)
            {
                list.Add(new CharacterOption
                {
                    Value = i,
                    Text = labels[i]
                });
            }
            return list;
        }

        private List<CharacterOption> BuildNameOptions(string key)
        {
            return BuildNameOptions(key, false);
        }

        private List<CharacterOption> BuildNameOptions(string key, bool showIndex)
        {
            List<CharacterOption> list = new List<CharacterOption>();
            foreach (KeyValuePair<int, string> pair in names.Entries(key))
            {
                if (string.IsNullOrEmpty(pair.Value)) continue;
                list.Add(new CharacterOption
                {
                    Value = pair.Key,
                    Text = showIndex
                        ? pair.Key.ToString("D2") + "  " + pair.Value
                        : pair.Value
                });
            }
            return list;
        }

        private void RefreshCharacterOptions()
        {
            string signature = BuildClubSignature() + "/g" + selectedGender;
            if (signature == clubSignature) return;
            clubSignature = signature;
            characterOptions = BuildCharacterOptions(selectedGender);
        }

        private string BuildClubSignature()
        {
            string[] names = ClubData.ReadNames(mem, ctx);
            return string.Join("\n", names);
        }

        private List<CharacterOption> BuildClubOptions()
        {
            List<CharacterOption> list = new List<CharacterOption>();
            string[] names = ClubData.ReadNames(mem, ctx);
            for (int i = 0; i < names.Length; i++)
            {
                list.Add(new CharacterOption
                {
                    Value = i,
                    Text = i.ToString("D2") + "  " + names[i]
                });
            }
            return list;
        }

        private static List<CharacterOption> BuildNumbers(int min, int max)
        {
            List<CharacterOption> list = new List<CharacterOption>();
            for (int i = min; i <= max; i++)
            {
                list.Add(new CharacterOption
                {
                    Value = i,
                    Text = i.ToString()
                });
            }
            return list;
        }

        private List<CharacterOption> BuildOptions(string key, int min, int max)
        {
            List<CharacterOption> list = new List<CharacterOption>();
            HashSet<int> seen = new HashSet<int>();

            foreach (KeyValuePair<int, string> pair in names.Entries(key))
            {
                if (pair.Key < 0 || pair.Key > 255) continue;
                if (string.IsNullOrEmpty(pair.Value)) continue;
                list.Add(new CharacterOption
                {
                    Value = pair.Key,
                    Text = pair.Key.ToString("D2") + "  " + pair.Value
                });
                seen.Add(pair.Key);
            }

            for (int i = min; i <= max; i++)
            {
                if (seen.Contains(i)) continue;
                list.Add(new CharacterOption
                {
                    Value = i,
                    Text = i.ToString("D2") + "  (unnamed)"
                });
            }

            list.Sort(delegate(CharacterOption a, CharacterOption b)
            {
                return a.Value.CompareTo(b.Value);
            });
            return list;
        }

        private static List<CharacterOption> BuildGrades()
        {
            List<CharacterOption> list = new List<CharacterOption>();
            string grades = "FEDCBA";
            for (int i = 0; i < grades.Length; i++)
            {
                list.Add(new CharacterOption
                {
                    Value = i,
                    Text = grades.Substring(i, 1)
                });
            }
            list.Add(new CharacterOption { Value = 255, Text = "None" });
            return list;
        }

        private CharacterDetail BuildCharacterDetail(CharacterSnapshot snapshot)
        {
            CharacterDetail detail = new CharacterDetail();
            detail.Profile = snapshot.Profile ?? "";
            detail.Personality = EnumText("PERSONALITY", snapshot.Personality,
                "Personality " + snapshot.Personality);
            detail.Club = EnumText("CLUB", snapshot.Club,
                "Club " + (snapshot.Club + 1));
            detail.Social = EnumText("LOW_HIGH", snapshot.Social,
                "Social " + snapshot.Social);
            detail.FightingStyle = EnumText("FIGHTSTYLE", snapshot.FightingStyle,
                "Style " + snapshot.FightingStyle);
            detail.Virtue = EnumText("LOW_HIGH", snapshot.Virtue,
                "Virtue " + snapshot.Virtue);
            detail.Orientation = EnumText("SEXUALITY", snapshot.SexualOrientation,
                "Orientation " + snapshot.SexualOrientation);
            detail.VoicePitch = EnumText("LOW_HIGH", snapshot.VoicePitch,
                "Voice " + snapshot.VoicePitch);
            detail.Intelligence = StatText(snapshot.IntelligenceLevel,
                snapshot.IntelligenceRank, snapshot.IntelligenceValue,
                snapshot.IntelligenceGrade);
            detail.Strength = StatText(snapshot.StrengthLevel,
                snapshot.StrengthRank, snapshot.StrengthValue,
                snapshot.StrengthGrade);
            detail.ClubStats = StatText(snapshot.ClubLevel, 0,
                snapshot.ClubValue, snapshot.ClubGrade);
            detail.Record = "Victory " + snapshot.VictoryCount +
                " | Class skip " + snapshot.ClassSkip +
                " | Won over " + snapshot.WinningOver +
                " | Partner " + snapshot.Partner +
                " | Rejected " + snapshot.Rejected;
            detail.Experience = "Intercourse " + snapshot.SexIntercourse +
                " | Partners " + snapshot.SexPartner +
                " | Sex " + YesNo(snapshot.SexExperience) +
                " | Anal " + YesNo(snapshot.AnalExperience);
            return detail;
        }

        private string EnumText(string key, int value, string fallback)
        {
            string text = names.Get(key, value);
            return string.IsNullOrEmpty(text) ? fallback : text;
        }

        private static string StatText(int level, int rank, int value, int grade)
        {
            return "Lv " + level + "  Rank " + rank +
                "  Value " + value + "  Grade " + GradeText(grade);
        }

        private static string GradeText(int grade)
        {
            if (grade == 255) return "None";
            if (grade >= 0 && grade < 6)
                return "FEDCBA".Substring(grade, 1);
            return grade.ToString();
        }

        private static string YesNo(bool value)
        {
            return value ? "Yes" : "No";
        }

        private void ProcessCharacterWrite()
        {
            uint pointer;
            int seat;
            CharacterSnapshot original;
            CharacterSnapshot edited;

            lock (writeSync)
            {
                if (!writePending) return;
                pointer = writePointer;
                seat = writeSeat;
                original = writeOriginal;
                edited = writeEdited;
                writePending = false;
                writeOriginal = null;
                writeEdited = null;
            }

            CharacterWriteResult result = new CharacterWriteResult
            {
                Pointer = pointer,
                Seat = seat
            };

            try
            {
                CharacterInfo info = ctx.FindByPointer(pointer);
                if (info == null)
                {
                    result.Error = "The selected character is no longer loaded.";
                }
                else
                {
                    PointerHealth check = PointerHealth.Run(mem, ctx);
                    if (!check.Clean)
                    {
                        result.Error = "Pointer health check failed: " +
                            check.ShortStatus;
                    }
                    else
                    {
                        string changed;
                        string error;
                        if (CharacterEditor.TryWrite(mem, info, original, edited,
                            out changed, out error))
                        {
                            result.Success = true;
                            result.Changed = changed ?? "";
                            lastContextUtc = DateTime.MinValue;
                            lastHealthUtc = DateTime.MinValue;
                            if (ctx.Selected != null &&
                                ctx.Selected.Pointer == pointer)
                            {
                                RefreshSelectedCharacter();
                            }
                            RefreshRelations();
                        }
                        else
                        {
                            result.Error = string.IsNullOrEmpty(error)
                                ? "Write failed." : error;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
            }

            PublishCharacterWrite(result);
        }

        private void CancelPendingWrite(string error)
        {
            CharacterWriteResult result;
            lock (writeSync)
            {
                if (!writePending) return;
                writePending = false;
                result = new CharacterWriteResult
                {
                    Pointer = writePointer,
                    Seat = writeSeat,
                    Error = error
                };
                writeOriginal = null;
                writeEdited = null;
            }
            PublishCharacterWrite(result);
        }

        private void PublishCharacterWrite(CharacterWriteResult result)
        {
            if (disposed || result == null) return;
            dispatcher.BeginInvoke(new Action(delegate
            {
                if (disposed) return;
                Action<CharacterWriteResult> handler = CharacterWriteCompleted;
                if (handler != null) handler(result);
            }));
        }

        private void ProcessTraitWrite()
        {
            uint pointer;
            int seat;
            bool[] originalTraits;
            bool[] editedTraits;
            int[,] originalRisk;
            int[,] editedRisk;

            lock (traitWriteSync)
            {
                if (!traitWritePending) return;
                pointer = traitWritePointer;
                seat = traitWriteSeat;
                originalTraits = traitWriteOriginal;
                editedTraits = traitWriteEdited;
                originalRisk = riskWriteOriginal;
                editedRisk = riskWriteEdited;
                traitWritePending = false;
                traitWriteOriginal = null;
                traitWriteEdited = null;
                riskWriteOriginal = null;
                riskWriteEdited = null;
            }

            TraitWriteResult result = new TraitWriteResult
            {
                Pointer = pointer,
                Seat = seat
            };

            try
            {
                CharacterInfo info = ctx.FindByPointer(pointer);
                if (info == null)
                {
                    result.Error = "The selected character is no longer loaded.";
                }
                else
                {
                    PointerHealth check = PointerHealth.Run(mem, ctx);
                    if (!check.Clean)
                    {
                        result.Error = "Pointer health check failed: " +
                            check.ShortStatus;
                    }
                    else
                    {
                        List<string> errors = new List<string>();
                        int changedTraits = 0;
                        int changedRisk = 0;

                        int traitCount = Math.Min(TraitCatalog.All.Count,
                            Math.Min(originalTraits.Length, editedTraits.Length));
                        for (int i = 0; i < traitCount; i++)
                        {
                            if (originalTraits[i] == editedTraits[i]) continue;
                            string error;
                            if (TraitCatalog.TryWrite(mem, info, i,
                                editedTraits[i], out error))
                            {
                                changedTraits++;
                            }
                            else
                            {
                                errors.Add("trait " + i + ": " + error);
                            }
                        }

                        for (int week = 0; week < RiskSchedule.WeekCount; week++)
                        {
                            for (int day = 0; day < RiskSchedule.DayCount; day++)
                            {
                                if (originalRisk[week, day] ==
                                    editedRisk[week, day]) continue;
                                string error;
                                if (RiskSchedule.TryWrite(mem, info, week, day,
                                    editedRisk[week, day], out error))
                                {
                                    changedRisk++;
                                }
                                else
                                {
                                    errors.Add(RiskSchedule.Days[day] + " week " +
                                        (week + 1) + ": " + error);
                                }
                            }
                        }

                        result.Success = errors.Count == 0;
                        result.Changed = changedTraits + " trait(s), " +
                            changedRisk + " risk cell(s)";
                        if (errors.Count > 0)
                        {
                            result.Error = string.Join("; ", errors.Take(4));
                            if (errors.Count > 4) result.Error += "; ...";
                        }

                        lastContextUtc = DateTime.MinValue;
                        lastHealthUtc = DateTime.MinValue;
                        if (ctx.Selected != null &&
                            ctx.Selected.Pointer == pointer)
                        {
                            RefreshSelectedCharacter();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
            }

            PublishTraitWrite(result);
        }

        private void CancelPendingTraitWrite(string error)
        {
            TraitWriteResult result;
            lock (traitWriteSync)
            {
                if (!traitWritePending) return;
                traitWritePending = false;
                result = new TraitWriteResult
                {
                    Pointer = traitWritePointer,
                    Seat = traitWriteSeat,
                    Error = error
                };
                traitWriteOriginal = null;
                traitWriteEdited = null;
                riskWriteOriginal = null;
                riskWriteEdited = null;
            }
            PublishTraitWrite(result);
        }

        private void PublishTraitWrite(TraitWriteResult result)
        {
            if (disposed || result == null) return;
            dispatcher.BeginInvoke(new Action(delegate
            {
                if (disposed) return;
                Action<TraitWriteResult> handler = TraitWriteCompleted;
                if (handler != null) handler(result);
            }));
        }

        private void ProcessBodyWrite()
        {
            uint pointer;
            int seat;
            int[] original;
            int[] edited;

            lock (bodyWriteSync)
            {
                if (!bodyWritePending) return;
                pointer = bodyWritePointer;
                seat = bodyWriteSeat;
                original = bodyWriteOriginal;
                edited = bodyWriteEdited;
                bodyWritePending = false;
                bodyWriteOriginal = null;
                bodyWriteEdited = null;
            }

            BodyWriteResult result = new BodyWriteResult
            {
                Pointer = pointer,
                Seat = seat
            };

            try
            {
                CharacterInfo info = ctx.FindByPointer(pointer);
                if (info == null)
                {
                    result.Error = "The selected character is no longer loaded.";
                }
                else
                {
                    PointerHealth check = PointerHealth.Run(mem, ctx);
                    if (!check.Clean)
                    {
                        result.Error = "Pointer health check failed: " +
                            check.ShortStatus;
                    }
                    else
                    {
                        string changed;
                        string error;
                        if (BodyEditor.TryWrite(mem, info, original, edited,
                            out changed, out error))
                        {
                            result.Success = true;
                            result.Changed = changed ?? "";
                            lastContextUtc = DateTime.MinValue;
                            lastHealthUtc = DateTime.MinValue;
                            if (ctx.Selected != null &&
                                ctx.Selected.Pointer == pointer)
                            {
                                RefreshSelectedCharacter();
                            }
                        }
                        else
                        {
                            result.Error = string.IsNullOrEmpty(error)
                                ? "Write failed." : error;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
            }

            PublishBodyWrite(result);
        }

        private void CancelPendingBodyWrite(string error)
        {
            BodyWriteResult result;
            lock (bodyWriteSync)
            {
                if (!bodyWritePending) return;
                bodyWritePending = false;
                result = new BodyWriteResult
                {
                    Pointer = bodyWritePointer,
                    Seat = bodyWriteSeat,
                    Error = error
                };
                bodyWriteOriginal = null;
                bodyWriteEdited = null;
            }
            PublishBodyWrite(result);
        }

        private void PublishBodyWrite(BodyWriteResult result)
        {
            if (disposed || result == null) return;
            dispatcher.BeginInvoke(new Action(delegate
            {
                if (disposed) return;
                Action<BodyWriteResult> handler = BodyWriteCompleted;
                if (handler != null) handler(result);
            }));
        }

        private void ProcessFaceWrite()
        {
            uint pointer;
            int seat;
            int[] originalValues;
            int[] editedValues;
            string[] originalTexts;
            string[] editedTexts;

            lock (faceWriteSync)
            {
                if (!faceWritePending) return;
                pointer = faceWritePointer;
                seat = faceWriteSeat;
                originalValues = faceWriteOriginalValues;
                editedValues = faceWriteEditedValues;
                originalTexts = faceWriteOriginalTexts;
                editedTexts = faceWriteEditedTexts;
                faceWritePending = false;
                faceWriteOriginalValues = null;
                faceWriteEditedValues = null;
                faceWriteOriginalTexts = null;
                faceWriteEditedTexts = null;
            }

            FaceWriteResult result = new FaceWriteResult
            {
                Pointer = pointer,
                Seat = seat
            };

            try
            {
                CharacterInfo info = ctx.FindByPointer(pointer);
                if (info == null)
                {
                    result.Error = "The selected character is no longer loaded.";
                }
                else
                {
                    PointerHealth check = PointerHealth.Run(mem, ctx);
                    if (!check.Clean)
                    {
                        result.Error = "Pointer health check failed: " +
                            check.ShortStatus;
                    }
                    else
                    {
                        FaceSnapshot original = new FaceSnapshot
                        {
                            Values = originalValues,
                            Texts = originalTexts
                        };
                        FaceSnapshot edited = new FaceSnapshot
                        {
                            Values = editedValues,
                            Texts = editedTexts
                        };
                        string changed;
                        string error;
                        if (FaceEditor.TryWrite(mem, info, original, edited,
                            out changed, out error))
                        {
                            result.Success = true;
                            result.Changed = changed ?? "";
                            lastContextUtc = DateTime.MinValue;
                            lastHealthUtc = DateTime.MinValue;
                            if (ctx.Selected != null &&
                                ctx.Selected.Pointer == pointer)
                            {
                                RefreshSelectedCharacter();
                            }
                        }
                        else
                        {
                            result.Error = string.IsNullOrEmpty(error)
                                ? "Write failed." : error;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
            }

            PublishFaceWrite(result);
        }

        private void CancelPendingFaceWrite(string error)
        {
            FaceWriteResult result;
            lock (faceWriteSync)
            {
                if (!faceWritePending) return;
                faceWritePending = false;
                result = new FaceWriteResult
                {
                    Pointer = faceWritePointer,
                    Seat = faceWriteSeat,
                    Error = error
                };
                faceWriteOriginalValues = null;
                faceWriteEditedValues = null;
                faceWriteOriginalTexts = null;
                faceWriteEditedTexts = null;
            }
            PublishFaceWrite(result);
        }

        private void PublishFaceWrite(FaceWriteResult result)
        {
            if (disposed || result == null) return;
            dispatcher.BeginInvoke(new Action(delegate
            {
                if (disposed) return;
                Action<FaceWriteResult> handler = FaceWriteCompleted;
                if (handler != null) handler(result);
            }));
        }

        private void ProcessHairWrite()
        {
            uint pointer;
            int seat;
            int[] original;
            int[] edited;

            lock (hairWriteSync)
            {
                if (!hairWritePending) return;
                pointer = hairWritePointer;
                seat = hairWriteSeat;
                original = hairWriteOriginal;
                edited = hairWriteEdited;
                hairWritePending = false;
                hairWriteOriginal = null;
                hairWriteEdited = null;
            }

            HairWriteResult result = new HairWriteResult
            {
                Pointer = pointer,
                Seat = seat
            };

            try
            {
                CharacterInfo info = ctx.FindByPointer(pointer);
                if (info == null)
                {
                    result.Error = "The selected character is no longer loaded.";
                }
                else
                {
                    PointerHealth check = PointerHealth.Run(mem, ctx);
                    if (!check.Clean)
                    {
                        result.Error = "Pointer health check failed: " +
                            check.ShortStatus;
                    }
                    else
                    {
                        string changed;
                        string error;
                        if (HairEditor.TryWrite(mem, info, original, edited,
                            out changed, out error))
                        {
                            result.Success = true;
                            result.Changed = changed ?? "";
                            lastContextUtc = DateTime.MinValue;
                            lastHealthUtc = DateTime.MinValue;
                            if (ctx.Selected != null &&
                                ctx.Selected.Pointer == pointer)
                            {
                                RefreshSelectedCharacter();
                            }
                        }
                        else
                        {
                            result.Error = string.IsNullOrEmpty(error)
                                ? "Write failed." : error;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
            }

            PublishHairWrite(result);
        }

        private void CancelPendingHairWrite(string error)
        {
            HairWriteResult result;
            lock (hairWriteSync)
            {
                if (!hairWritePending) return;
                hairWritePending = false;
                result = new HairWriteResult
                {
                    Pointer = hairWritePointer,
                    Seat = hairWriteSeat,
                    Error = error
                };
                hairWriteOriginal = null;
                hairWriteEdited = null;
            }
            PublishHairWrite(result);
        }

        private void PublishHairWrite(HairWriteResult result)
        {
            if (disposed || result == null) return;
            dispatcher.BeginInvoke(new Action(delegate
            {
                if (disposed) return;
                Action<HairWriteResult> handler = HairWriteCompleted;
                if (handler != null) handler(result);
            }));
        }

        private void ProcessOutfitWrite()
        {
            uint pointer;
            int seat;
            int[] original;
            int[] edited;

            lock (outfitWriteSync)
            {
                if (!outfitWritePending) return;
                pointer = outfitWritePointer;
                seat = outfitWriteSeat;
                original = outfitWriteOriginal;
                edited = outfitWriteEdited;
                outfitWritePending = false;
                outfitWriteOriginal = null;
                outfitWriteEdited = null;
            }

            OutfitWriteResult result = new OutfitWriteResult
            {
                Pointer = pointer,
                Seat = seat
            };

            try
            {
                CharacterInfo info = ctx.FindByPointer(pointer);
                if (info == null)
                {
                    result.Error = "The selected character is no longer loaded.";
                }
                else
                {
                    PointerHealth check = PointerHealth.Run(mem, ctx);
                    if (!check.Clean)
                    {
                        result.Error = "Pointer health check failed: " +
                            check.ShortStatus;
                    }
                    else
                    {
                        OutfitSnapshot originalSnapshot = new OutfitSnapshot
                        {
                            Values = original
                        };
                        OutfitSnapshot editedSnapshot = new OutfitSnapshot
                        {
                            Values = edited
                        };
                        string changed;
                        string error;
                        if (OutfitEditor.TryWrite(mem, info, originalSnapshot,
                            editedSnapshot, out changed, out error))
                        {
                            result.Success = true;
                            result.Changed = changed ?? "";
                            lastContextUtc = DateTime.MinValue;
                            lastHealthUtc = DateTime.MinValue;
                            if (ctx.Selected != null &&
                                ctx.Selected.Pointer == pointer)
                            {
                                RefreshSelectedCharacter();
                            }
                        }
                        else
                        {
                            result.Error = string.IsNullOrEmpty(error)
                                ? "Write failed." : error;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
            }

            PublishOutfitWrite(result);
        }

        private void CancelPendingOutfitWrite(string error)
        {
            OutfitWriteResult result;
            lock (outfitWriteSync)
            {
                if (!outfitWritePending) return;
                outfitWritePending = false;
                result = new OutfitWriteResult
                {
                    Pointer = outfitWritePointer,
                    Seat = outfitWriteSeat,
                    Error = error
                };
                outfitWriteOriginal = null;
                outfitWriteEdited = null;
            }
            PublishOutfitWrite(result);
        }

        private void PublishOutfitWrite(OutfitWriteResult result)
        {
            if (disposed || result == null) return;
            dispatcher.BeginInvoke(new Action(delegate
            {
                if (disposed) return;
                Action<OutfitWriteResult> handler = OutfitWriteCompleted;
                if (handler != null) handler(result);
            }));
        }

        private CharacterInfo FindCharacterBySeat(int seat)
        {
            return ctx.Characters.FirstOrDefault(
                c => c != null && c.Seat == seat);
        }

        private void ProcessPlayerSwitch()
        {
            int seat;
            lock (playerSwitchSync)
            {
                if (!playerSwitchPending) return;
                seat = playerSwitchSeat;
                playerSwitchPending = false;
                playerSwitchSeat = -1;
            }

            PlayerSwitchResult result = new PlayerSwitchResult { Seat = seat };
            try
            {
                CharacterInfo info = FindCharacterBySeat(seat);
                if (info == null)
                {
                    result.Error = "The selected character is no longer loaded.";
                }
                else
                {
                    PointerHealth check = PointerHealth.Run(mem, ctx);
                    if (!check.Clean)
                    {
                        result.Error = "Pointer health check failed: " +
                            check.ShortStatus;
                    }
                    else
                    {
                        string error;
                        if (PlayerSwitch.TrySetPlayer(mem, ctx, info, out error))
                        {
                            result.Success = true;
                            result.Pointer = info.Pointer;
                            lastContextUtc = DateTime.MinValue;
                            lastHealthUtc = DateTime.MinValue;
                        }
                        else
                        {
                            result.Error = string.IsNullOrEmpty(error)
                                ? "Player switch failed." : error;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
            }

            PublishPlayerSwitch(result);
        }

        private void CancelPendingPlayerSwitch(string error)
        {
            PlayerSwitchResult result;
            lock (playerSwitchSync)
            {
                if (!playerSwitchPending) return;
                playerSwitchPending = false;
                result = new PlayerSwitchResult
                {
                    Seat = playerSwitchSeat,
                    Error = error
                };
                playerSwitchSeat = -1;
            }
            PublishPlayerSwitch(result);
        }

        private void PublishPlayerSwitch(PlayerSwitchResult result)
        {
            if (disposed || result == null) return;
            dispatcher.BeginInvoke(new Action(delegate
            {
                if (disposed) return;
                Action<PlayerSwitchResult> handler = PlayerSwitchCompleted;
                if (handler != null) handler(result);
            }));
        }

        private void ProcessRosterAction()
        {
            int kind;
            int sourceSeat;
            int targetSeat;
            int feeling;
            bool flag;

            lock (rosterActionSync)
            {
                if (!rosterActionPending) return;
                kind = rosterActionKind;
                sourceSeat = rosterActionSourceSeat;
                targetSeat = rosterActionTargetSeat;
                feeling = rosterActionFeeling;
                flag = rosterActionFlag;
                rosterActionPending = false;
                rosterActionSourceSeat = -1;
                rosterActionTargetSeat = -1;
            }

            RosterActionResult result = new RosterActionResult();
            try
            {
                CharacterInfo source = FindCharacterBySeat(sourceSeat);
                CharacterInfo target = FindCharacterBySeat(targetSeat);
                if (source == null || target == null)
                {
                    result.Error = "The selected pair is no longer loaded.";
                }
                else if (source.Pointer == target.Pointer)
                {
                    result.Error = "A character cannot target themselves.";
                }
                else
                {
                    PointerHealth check = PointerHealth.Run(mem, ctx);
                    if (!check.Clean)
                    {
                        result.Error = "Pointer health check failed: " +
                            check.ShortStatus;
                    }
                    else
                    {
                        string error;
                        bool ok;
                        if (kind == 0)
                        {
                            ok = ApplyFeeling(source, target, feeling,
                                out error);
                            result.Message = "Set " + source.Name + "'s " +
                                FeelingName(feeling) + " toward " +
                                target.Name + " to 30.";
                        }
                        else if (kind == 1)
                        {
                            ok = LoverEditor.TryWriteMutualLovers(mem,
                                source, target, flag, out error);
                            result.Message = flag
                                ? "Made " + source.Name + " and " +
                                    target.Name + " lovers."
                                : "Broke up " + source.Name + " and " +
                                    target.Name + ".";
                        }
                        else
                        {
                            ok = ApplyFreeze(source, target, flag, out error);
                            result.Message = flag
                                ? "Froze " + source.Name + " toward " +
                                    target.Name + "."
                                : "Unfroze " + source.Name + " toward " +
                                    target.Name + ".";
                        }

                        if (ok)
                        {
                            result.Success = true;
                            lastContextUtc = DateTime.MinValue;
                            lastHealthUtc = DateTime.MinValue;
                        }
                        else
                        {
                            result.Error = string.IsNullOrEmpty(error)
                                ? "The action failed." : error;
                            result.Message = "";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
            }

            PublishRosterAction(result);
        }

        private void CancelPendingRosterAction(string error)
        {
            RosterActionResult result;
            lock (rosterActionSync)
            {
                if (!rosterActionPending) return;
                rosterActionPending = false;
                result = new RosterActionResult { Error = error };
                rosterActionSourceSeat = -1;
                rosterActionTargetSeat = -1;
            }
            PublishRosterAction(result);
        }

        private void PublishRosterAction(RosterActionResult result)
        {
            if (disposed || result == null) return;
            dispatcher.BeginInvoke(new Action(delegate
            {
                if (disposed) return;
                Action<RosterActionResult> handler = RosterActionCompleted;
                if (handler != null) handler(result);
            }));
        }

        private bool ApplyFeeling(CharacterInfo source, CharacterInfo target,
            int feeling, out string error)
        {
            error = null;
            RelationshipRecord record = RelationshipEditor.FindRecord(mem,
                source.Pointer, target.Seat);
            if (record == null)
            {
                error = "No relationship record exists for this pair.";
                return false;
            }

            int[] counts = new int[4];
            int[] points = new int[4];
            int[] levels = new int[4];
            for (int f = 0; f < 4; f++) levels[f] = 2;
            counts[feeling] = 30;
            levels[feeling] = 0;

            int latest = record.HistoryCount > 0 ? feeling : -1;
            bool writeLatest = record.HistoryCount > 0;
            return RelationshipEditor.TryWrite(mem, record, counts, points,
                levels, latest, writeLatest, out error);
        }

        private bool ApplyFreeze(CharacterInfo source, CharacterInfo target,
            bool frozen, out string error)
        {
            error = null;
            long key = FreezeKey(source.Pointer, target.Seat);
            if (!frozen)
            {
                lock (relationshipFreezeSync) relationshipFreezes.Remove(key);
                return true;
            }

            RelationshipRecord record = RelationshipEditor.FindRecord(mem,
                source.Pointer, target.Seat);
            if (record == null)
            {
                error = "No relationship record exists for this pair.";
                return false;
            }
            lock (relationshipFreezeSync)
            {
                relationshipFreezes[key] = new FrozenRelationship(record);
            }
            return true;
        }

        private void RestoreRelationshipFreezes()
        {
            int count;
            lock (relationshipFreezeSync) count = relationshipFreezes.Count;
            if (count == 0) return;

            DateTime now = DateTime.UtcNow;
            if ((now - lastFreezeUtc).TotalMilliseconds < 100) return;
            lastFreezeUtc = now;

            List<KeyValuePair<long, FrozenRelationship>> snapshot;
            lock (relationshipFreezeSync)
            {
                snapshot = new List<KeyValuePair<long, FrozenRelationship>>(
                    relationshipFreezes);
            }

            List<long> stale = null;
            foreach (KeyValuePair<long, FrozenRelationship> pair in snapshot)
            {
                FrozenRelationship frozen = pair.Value;
                RelationshipRecord record = RelationshipEditor.FindRecord(mem,
                    frozen.SourcePointer, frozen.TargetSeat);
                if (record == null)
                {
                    if (stale == null) stale = new List<long>();
                    stale.Add(pair.Key);
                    continue;
                }
                if (SameRelationship(record, frozen)) continue;

                string error;
                RelationshipEditor.TryWrite(mem, record, frozen.Counts,
                    frozen.Points, frozen.Levels, frozen.Latest,
                    frozen.Latest >= 0, out error);
            }

            if (stale != null)
            {
                lock (relationshipFreezeSync)
                {
                    foreach (long key in stale) relationshipFreezes.Remove(key);
                }
            }
        }

        private sealed class FrozenRelationship
        {
            public uint SourcePointer;
            public int TargetSeat;
            public int[] Counts;
            public int[] Points;
            public int[] Levels;
            public int Latest;

            public FrozenRelationship(RelationshipRecord record)
            {
                SourcePointer = record.Source == null ? 0 : record.Source.Pointer;
                TargetSeat = record.TargetSeat;
                Counts = new int[4];
                Points = new int[4];
                Levels = new int[4];
                Array.Copy(record.Counts, Counts, 4);
                Array.Copy(record.Points, Points, 4);
                Array.Copy(record.Levels, Levels, 4);
                Latest = record.Latest;
            }
        }

        private static long FreezeKey(uint pointer, int seat)
        {
            return ((long)pointer << 8) | (uint)seat;
        }

        private bool IsRelationshipFrozen(uint sourcePointer, int targetSeat)
        {
            lock (relationshipFreezeSync)
            {
                return relationshipFreezes.ContainsKey(
                    FreezeKey(sourcePointer, targetSeat));
            }
        }

        private static bool SameRelationship(RelationshipRecord record,
            FrozenRelationship frozen)
        {
            if (record == null || frozen == null) return false;
            for (int i = 0; i < 4; i++)
            {
                if (record.Counts[i] != frozen.Counts[i]) return false;
                if (record.Points[i] != frozen.Points[i]) return false;
                if (record.Levels[i] != frozen.Levels[i]) return false;
            }
            return record.Latest == frozen.Latest;
        }

        private static string FeelingName(int feeling)
        {
            switch (feeling)
            {
                case 0: return "Love";
                case 1: return "Like";
                case 2: return "Dislike";
                case 3: return "Hate";
                default: return "feeling";
            }
        }

        private void ProcessScheduleWrite()
        {
            ScheduleSnapshot original;
            ScheduleSnapshot edited;
            lock (scheduleWriteSync)
            {
                if (!scheduleWritePending) return;
                original = scheduleWriteOriginal;
                edited = scheduleWriteEdited;
                scheduleWritePending = false;
                scheduleWriteOriginal = null;
                scheduleWriteEdited = null;
            }

            ScheduleWriteResult result = new ScheduleWriteResult();
            try
            {
                PointerHealth check = PointerHealth.Run(mem, ctx);
                if (!check.Clean)
                {
                    result.Error = "Pointer health check failed: " +
                        check.ShortStatus;
                }
                else
                {
                    string changed;
                    string error;
                    if (ScheduleEditor.TryWrite(mem, ctx, original, edited,
                        out changed, out error))
                    {
                        result.Success = true;
                        result.Changed = changed ?? "";
                        lastContextUtc = DateTime.MinValue;
                        lastHealthUtc = DateTime.MinValue;
                    }
                    else
                    {
                        result.Error = string.IsNullOrEmpty(error)
                            ? "Write failed." : error;
                    }
                }
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
            }

            PublishScheduleWrite(result);
        }

        private void CancelPendingScheduleWrite(string error)
        {
            ScheduleWriteResult result;
            lock (scheduleWriteSync)
            {
                if (!scheduleWritePending) return;
                scheduleWritePending = false;
                result = new ScheduleWriteResult { Error = error };
                scheduleWriteOriginal = null;
                scheduleWriteEdited = null;
            }
            PublishScheduleWrite(result);
        }

        private void PublishScheduleWrite(ScheduleWriteResult result)
        {
            if (disposed || result == null) return;
            dispatcher.BeginInvoke(new Action(delegate
            {
                if (disposed) return;
                Action<ScheduleWriteResult> handler = ScheduleWriteCompleted;
                if (handler != null) handler(result);
            }));
        }

        private void PinTime()
        {
            int? target = timeFreezeMilliseconds;
            if (!target.HasValue) return;

            DateTime now = DateTime.UtcNow;
            if ((now - lastPinUtc).TotalMilliseconds < 50) return;
            lastPinUtc = now;

            bool wrote;
            string error;
            if (ScheduleEditor.TryPinTime(mem, ctx, target.Value,
                out wrote, out error))
            {
                timeFreezeFailures = 0;
                return;
            }

            timeFreezeFailures++;
            if (timeFreezeFailures >= 3)
            {
                timeFreezeMilliseconds = null;
                timeFreezeFailures = 0;
            }
        }

        private void PublishAttachedNoClass()
        {
            SessionSnapshot snapshot = new SessionSnapshot();
            snapshot.Sequence = Interlocked.Increment(ref sequence);
            snapshot.IsAttached = mem.IsAttached;
            snapshot.AttachState = "Attached - class data not loaded";
            snapshot.GameTime = "Game time: -";
            snapshot.PointerHealth = "Pointers: not checked";
            snapshot.NamesState = "Names: " + names.StatusText;
            snapshot.WriteGate = mem.CanWrite
                ? "Write gate: open" : "Write gate: read-only";
            snapshot.UpdatedText = "Updated " + DateTime.Now.ToString("HH:mm:ss");
            for (int seat = 0; seat < 25; seat++)
            {
                snapshot.Seats.Add(new SeatSnapshot
                {
                    Seat = seat,
                    Status = "Empty",
                    Action = "None"
                });
            }
            Publish(snapshot);
        }

        private void PublishDisconnected()
        {
            DateTime now = DateTime.UtcNow;
            if ((now - lastDisconnectedUtc).TotalMilliseconds <
                DisconnectedPublishMs) return;
            lastDisconnectedUtc = now;

            SessionSnapshot snapshot = new SessionSnapshot();
            snapshot.Sequence = Interlocked.Increment(ref sequence);
            snapshot.IsAttached = false;
            snapshot.AttachState = string.IsNullOrEmpty(mem.LastError)
                ? "Not attached" : "Not attached: " + mem.LastError;
            snapshot.GameTime = "Game time: -";
            snapshot.PointerHealth = "Pointers: not checked";
            snapshot.NamesState = "Names: not loaded";
            snapshot.WriteGate = "Write gate: read-only";
            snapshot.UpdatedText = "Updated " + DateTime.Now.ToString("HH:mm:ss");
            for (int seat = 0; seat < 25; seat++)
            {
                snapshot.Seats.Add(new SeatSnapshot
                {
                    Seat = seat,
                    Status = "Empty",
                    Action = "None"
                });
            }
            Publish(snapshot);
        }

        private void Publish(SessionSnapshot snapshot)
        {
            if (disposed || snapshot == null) return;
            Volatile.Write(ref pendingSnapshot, snapshot);
            if (Interlocked.Exchange(ref uiQueued, 1) != 0) return;

            dispatcher.BeginInvoke(new Action(delegate
            {
                Interlocked.Exchange(ref uiQueued, 0);
                SessionSnapshot next = Volatile.Read(ref pendingSnapshot);
                if (next == null) return;
                Action<SessionSnapshot> handler = SnapshotReady;
                if (handler != null) handler(next);
            }));
        }

        private string ActionName(int id)
        {
            string name = names.Get("ACTION_KEY", id);
            if (!string.IsNullOrEmpty(name)) return name;
            return DisplayText.ActionLabel(id);
        }

        private string LocationName(int id)
        {
            string name = names.Get("LOCATION", id);
            return string.IsNullOrEmpty(name) ? "Location " + id : name;
        }

        private static string StatusLabel(LiveCharacterState state)
        {
            if (state == null) return "Idle";
            if (state.InConversation != 0) return "Talking";
            if (state.IsBusy) return "Busy";
            switch (state.MovementType)
            {
                case 0: return "Still";
                case 1: return "Roaming";
                case 2: return "Moving";
                case 3: return "Approaching";
                case 4: return "Training";
                case 7: return "Talking";
                case 8: return "Facing";
                default: return "Idle";
            }
        }

        private static string RelationLabel(RelationshipSnapshot relation)
        {
            if (relation == null) return "-";
            if (relation.IsLover) return "Lover";
            if (relation.LoveCount > 0) return "Love";
            if (relation.LikeCount > 0) return "Like";
            if (relation.DislikeCount > 0) return "Dislike";
            if (relation.HateCount > 0) return "Hate";
            return "Neutral";
        }

        private static string HeaderRelationLabel(RelationshipSnapshot relation)
        {
            if (relation == null) return "Classmate";
            if (relation.IsLover) return "Lover";
            if (relation.LoveCount > 0) return "Close";
            if (relation.LikeCount > 0) return "Friend";
            return "Classmate";
        }

        private static string FormatSchedule(ScheduleSnapshot snapshot)
        {
            if (snapshot == null) return "Game time: -";
            string day = ScheduleEditor.Weekdays[snapshot.Weekday];
            if (day.Length > 3) day = day.Substring(0, 3);
            string period = snapshot.Period >= 0 &&
                snapshot.Period < ScheduleEditor.Periods.Length
                ? ScheduleEditor.Periods[snapshot.Period] : "?";
            int totalSeconds = Math.Max(0, snapshot.TimeMilliseconds / 1000);
            string time = (totalSeconds / 60).ToString("00") + ":" +
                (totalSeconds % 60).ToString("00");
            return day + " - " + ScheduleEditor.WeekLabel(snapshot.Week) +
                " - " + period + " " + time;
        }
    }
}
