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
        private readonly Dictionary<int, string> relationships =
            new Dictionary<int, string>();
        private readonly Dictionary<int, string> headerRelationships =
            new Dictionary<int, string>();
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
            live.PublishRoster(ctx.Characters, ctx.Selected);
            health = hasClass ? PointerHealth.Run(mem, ctx) : null;
            lastContextUtc = DateTime.UtcNow;
            lastHealthUtc = DateTime.UtcNow;
            RefreshRelations();
            RefreshSelectedCharacter();
        }

        private void DetachInternal()
        {
            live.Clear();
            mem.Detach();
            ctx = new GameContext();
            health = null;
            schedule = null;
            selectedCharacterDetail = null;
            lastContextUtc = DateTime.MinValue;
            lastHealthUtc = DateTime.MinValue;
            lastDisconnectedUtc = DateTime.MinValue;
            PublishDisconnected();
        }

        private void PollAttached()
        {
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
                    PublishAttachedNoClass();
                    return;
                }

                live.PublishRoster(ctx.Characters, ctx.Selected);
                RefreshRelations();
                RefreshSelectedCharacter();

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
                    IsBusy = liveState != null && liveState.IsBusy
                });
            }

            return snapshot;
        }

        private void RefreshRelations()
        {
            relationships.Clear();
            headerRelationships.Clear();

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
                    continue;
                }

                if (selected != null && info.Seat == selected.Seat)
                {
                    relationships[seat] = "Self";
                }
                else
                {
                    RelationshipSnapshot relation = selected == null ? null :
                        RelationshipReader.Read(mem, info, selected.Seat);
                    relationships[seat] = RelationLabel(relation);
                }

                if (info.IsPlayer)
                {
                    headerRelationships[seat] = "Player";
                }
                else
                {
                    RelationshipSnapshot relation = player == null ||
                        player.Seat == info.Seat ? null :
                        RelationshipReader.Read(mem, player, info.Seat);
                    headerRelationships[seat] = HeaderRelationLabel(relation);
                }
            }
        }

        private void RefreshSelectedCharacter()
        {
            if (ctx.Selected == null)
            {
                selectedCharacterDetail = null;
                return;
            }

            CharacterSnapshot snapshot;
            string error;
            if (!CharacterEditor.TryRead(mem, ctx.Selected,
                out snapshot, out error))
            {
                selectedCharacterDetail = null;
                return;
            }

            selectedCharacterDetail = BuildCharacterDetail(snapshot);
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
