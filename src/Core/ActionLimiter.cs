using System;
using System.Collections.Generic;
using System.Threading;

namespace AA2Trainer
{
    /// <summary>
    /// Watches every NPC's current action on a 100 ms timer and cancels actions
    /// that are present in the persistent limit list. Cancellation uses the same
    /// proven stop path as the action planner (clear action/status/targets).
    ///
    /// This is a poll-and-cancel limiter: an action can start for a fraction of a
    /// second, but it is stopped before it can play out. Conversations, locked
    /// animations and loading characters are left alone so dialogue is not broken.
    /// </summary>
    public sealed class ActionLimiter : IDisposable
    {
        private readonly ActionLimitList limits;
        private readonly Func<bool> externalWriteCheck;
        private readonly object gate = new object();
        private readonly Dictionary<uint, DateTime> cooldowns =
            new Dictionary<uint, DateTime>();

        private GameMemory mem;
        private GameContext ctx;
        private ActionStateReader reader;
        private ActionPlannerEngine engine;
        private System.Threading.Timer timer;
        private bool enabled;
        private bool applyToPc;
        private bool writeAllowed;
        private long blockedCount;
        private string lastBlocked = "";
        private string lastError = "";
        private int ticking;

        public ActionLimiter(ActionLimitList limits, Func<bool> externalWriteCheck)
        {
            this.limits = limits;
            this.externalWriteCheck = externalWriteCheck;
        }

        public bool Enabled
        {
            get { lock (gate) return enabled; }
            set
            {
                lock (gate)
                {
                    if (enabled == value) return;
                    enabled = value;
                    if (enabled) StartTimerLocked();
                    else StopTimerLocked();
                }
            }
        }

        public bool ApplyToPc
        {
            get { lock (gate) return applyToPc; }
            set { lock (gate) applyToPc = value; }
        }

        public bool WriteAllowed
        {
            get { lock (gate) return writeAllowed; }
            set { lock (gate) writeAllowed = value; }
        }

        public long BlockedCount
        {
            get { lock (gate) return blockedCount; }
        }

        public string LastBlocked
        {
            get { lock (gate) return lastBlocked; }
        }

        public string LastError
        {
            get { lock (gate) return lastError; }
        }

        public void SetTarget(GameMemory memory, GameContext context)
        {
            lock (gate)
            {
                if (ReferenceEquals(mem, memory) && ReferenceEquals(ctx, context))
                    return;

                StopTimerLocked();
                if (engine != null)
                {
                    engine.Dispose();
                    engine = null;
                }

                mem = memory;
                ctx = context;
                reader = memory == null ? null : new ActionStateReader(memory);
                engine = memory == null ? null : new ActionPlannerEngine(memory);
                cooldowns.Clear();
                lastError = "";

                if (enabled) StartTimerLocked();
            }
        }

        private void StartTimerLocked()
        {
            if (timer != null) return;
            if (mem == null || ctx == null) return;
            timer = new System.Threading.Timer(Tick, null, 100, 100);
        }

        private void StopTimerLocked()
        {
            if (timer == null) return;
            timer.Dispose();
            timer = null;
        }

        private void Tick(object state)
        {
            if (Interlocked.Exchange(ref ticking, 1) != 0) return;
            try
            {
                GameMemory memory;
                GameContext context;
                ActionStateReader stateReader;
                ActionPlannerEngine planner;
                bool includePc;
                bool writes;
                lock (gate)
                {
                    if (!enabled) return;
                    memory = mem;
                    context = ctx;
                    stateReader = reader;
                    planner = engine;
                    includePc = applyToPc;
                    writes = writeAllowed;
                }

                if (memory == null || context == null || stateReader == null ||
                    planner == null || !memory.IsAttached || !memory.CanWrite || !writes)
                    return;
                if (limits.Count == 0) return;
                if (externalWriteCheck != null && !externalWriteCheck()) return;

                List<CharacterInfo> roster = new List<CharacterInfo>();
                try
                {
                    foreach (CharacterInfo c in context.Characters)
                        if (c != null) roster.Add(c);
                }
                catch
                {
                    return;
                }
                if (roster.Count == 0) return;

                DateTime now = DateTime.UtcNow;
                for (int i = 0; i < roster.Count; i++)
                {
                    CharacterInfo character = roster[i];
                    if (character.Pointer == 0) continue;
                    if (character.IsPlayer && !includePc) continue;

                    CharacterState st;
                    try
                    {
                        st = stateReader.ReadState(character.Pointer, roster);
                    }
                    catch
                    {
                        continue;
                    }
                    if (st == null || st.ActionId < 0) continue;
                    if (!limits.Contains(st.ActionId)) continue;

                    // Too late to stop cleanly: the NPC is already talking or
                    // animation-locked. Let the interaction finish.
                    if (st.InConversation != 0 || st.Talking != 0 ||
                        st.AnimLocked != 0 || st.Loading != 0)
                        continue;

                    uint cooldownKey = character.Pointer;
                    DateTime last;
                    bool cooling;
                    lock (gate)
                    {
                        cooling = cooldowns.TryGetValue(cooldownKey, out last) &&
                            (now - last).TotalMilliseconds < 1500;
                    }
                    if (cooling) continue;

                    string message;
                    bool ok;
                    try
                    {
                        ok = planner.StopCurrentAction(character, roster, out message);
                    }
                    catch (Exception ex)
                    {
                        ok = false;
                        message = ex.Message;
                    }

                    if (ok)
                    {
                        lock (gate)
                        {
                            cooldowns[cooldownKey] = now;
                            blockedCount++;
                            lastBlocked = st.ActionId + " " +
                                DisplayText.ActionLabel(st.ActionId);
                            lastError = "";
                        }
                    }
                    else
                    {
                        lock (gate)
                        {
                            lastError = message ?? "cancel failed";
                            cooldowns[cooldownKey] = now;
                        }
                    }
                }
            }
            finally
            {
                Interlocked.Exchange(ref ticking, 0);
            }
        }

        public void Dispose()
        {
            lock (gate)
            {
                enabled = false;
                StopTimerLocked();
                if (engine != null)
                {
                    engine.Dispose();
                    engine = null;
                }
                mem = null;
                ctx = null;
                reader = null;
            }
        }
    }
}
