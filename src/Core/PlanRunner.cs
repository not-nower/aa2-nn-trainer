using System;
using System.Collections.Generic;
using System.Threading;

namespace AA2Trainer
{
    /// <summary>
    /// Drives one or more <see cref="PlanQueue"/>s on a single 100 ms scheduler
    /// thread. Every game call happens on that thread; the UI only reads the
    /// status strings.
    ///
    /// Step state machine (per character):
    ///   Idle -> Armed -> Observing -> Cooldown -> Idle
    /// The hook page has one slot per planner key, so plans for different
    /// characters do not consume each other's steps.
    /// </summary>
    public sealed class PlanRunner : IDisposable
    {
        private enum Phase
        {
            Idle,
            Armed,
            Observing,
            Cooldown
        }

        private sealed class RunState
        {
            public PlanQueue Plan;
            public uint Character;
            public int Seat = -1;
            public uint Key;
            public uint Activity;
            public PlanStep Step;
            public ActionParams Params;
            public uint TargetInterface;
            public uint ArmedCallCount;
            public Phase Phase = Phase.Idle;
            public bool ChainActive;
            public int LastActionId = -1;
            public DateTime LastActionAt;
            public bool DirectTried;
            public uint TargetActivity;
            public DateTime ArmedAt;
            public DateTime Deadline;
            public DateTime NextActionAt;
            public int Attempts;
            public string Status = "idle";
            public string LastNotIdleReason;
            public volatile bool Cancelled;
        }

        private const int TickMs = 100;
        private const int ArmTimeoutMs = 2000;
        private const int ObserveTimeoutMs = 2500;
        private const int CooldownMs = 600;
        private const int ForcedAttempts = 3;
        private const int NormalAttempts = 4;
        private const int ActionEndTimeoutMs = 60000;

        private readonly ActionPlannerEngine engine;
        private readonly ActionStateReader reader;
        private readonly GameMemory mem;
        private readonly Action<string, PlannerLogLevel> log;

        private readonly object gate = new object();
        private readonly List<RunState> runs = new List<RunState>();

        private Thread thread;
        private volatile bool stopRequested;

        /// <summary>Raised on the scheduler thread when a plan finishes on its own.</summary>
        public event Action<PlanQueue> PlanFinished;

        public PlanRunner(ActionPlannerEngine engine, ActionStateReader reader,
            GameMemory mem, Action<string, PlannerLogLevel> log)
        {
            this.engine = engine;
            this.reader = reader;
            this.mem = mem;
            this.log = log;
        }

        public bool HookInstalled
        {
            get { return engine != null && engine.HookInstalled; }
        }

        public bool HasActivePlans
        {
            get
            {
                lock (gate)
                {
                    foreach (RunState run in runs)
                        if (run.Plan != null && run.Plan.Started) return true;
                    return false;
                }
            }
        }

        /// <summary>
        /// Begins a plan. The plan must already contain its steps. Any running
        /// plan for the same character is stopped first.
        /// </summary>
        public bool StartPlan(PlanQueue plan, out string message)
        {
            message = null;
            if (plan == null)
            {
                message = "The plan is missing.";
                return false;
            }
            if (engine == null || !engine.HookInstalled)
            {
                message = "Install the planner hook first (the plan needs it to " +
                    "hand a step to the game).";
                return false;
            }
            if (engine.ObserveOnly)
            {
                message = "Observe-only is on: switch it off before starting a plan.";
                return false;
            }

            if (plan.Count == 0)
            {
                message = "The plan has no steps.";
                return false;
            }

            // Drop any previous run for this character (or this plan) before
            // Begin() resets the step states.
            lock (gate)
            {
                for (int i = runs.Count - 1; i >= 0; i--)
                {
                    RunState old = runs[i];
                    if (old.Plan == plan ||
                        (old.Character == plan.Character && old.Plan != null &&
                         old.Plan.Started))
                    {
                        StopPlanLocked(old, false);
                        runs.RemoveAt(i);
                        if (old.Plan != null && old.Plan != plan)
                        {
                            old.Plan.Stop();
                            Log("Stopped the previous plan for " +
                                Describe(plan) + ".", PlannerLogLevel.Warning);
                        }
                    }
                }
            }

            if (!plan.Begin(out message)) return false;

            lock (gate)
            {
                RunState run = new RunState();
                run.Plan = plan;
                run.Seat = plan.Seat;
                run.Character = plan.Character != 0
                    ? plan.Character : ResolveCharacter(plan.Seat);
                run.Key = 0;
                run.Activity = ResolveActivity(run.Character);
                run.Status = "starting";
                runs.Add(run);
            }

            EnsureThread();
            Log("Plan started for " + Describe(plan) + " (" + plan.Count +
                " step" + (plan.Count == 1 ? "" : "s") + ", " + ModeName(plan.Mode) +
                (plan.Repeat ? ", repeat" : "") + (plan.Shuffle ? ", shuffle" : "") +
                ").", PlannerLogLevel.Info);
            return true;
        }

        public void StopPlan(PlanQueue plan, bool cancelAction)
        {
            if (plan == null) return;
            lock (gate)
            {
                for (int i = runs.Count - 1; i >= 0; i--)
                {
                    if (runs[i].Plan != plan) continue;
                    StopPlanLocked(runs[i], cancelAction);
                    runs.RemoveAt(i);
                }
            }
            plan.Stop();
            Log("Plan stopped for " + Describe(plan) + ".", PlannerLogLevel.Warning);
        }

        public void StopAll()
        {
            lock (gate)
            {
                for (int i = 0; i < runs.Count; i++)
                {
                    StopPlanLocked(runs[i], false);
                    if (runs[i].Plan != null) runs[i].Plan.Stop();
                }
                runs.Clear();
            }
        }

        public void Stop()
        {
            stopRequested = true;
            Thread waiter;
            lock (gate) waiter = thread;
            if (waiter != null && waiter.IsAlive &&
                waiter != Thread.CurrentThread)
            {
                waiter.Join(1500);
            }
            lock (gate) thread = null;
        }

        public void Dispose()
        {
            StopAll();
            Stop();
        }

        public string GetStatus(PlanQueue plan)
        {
            if (plan == null) return "";
            lock (gate)
            {
                foreach (RunState run in runs)
                {
                    if (run.Plan == plan) return run.Status;
                }
            }
            if (plan.Finished) return "finished";
            return plan.Started ? "starting" : "stopped";
        }

        /// <summary>Why the last step did not arm/consume, for the log/UI.</summary>
        public string GetDiagnostic(PlanQueue plan)
        {
            if (plan == null) return "";
            lock (gate)
            {
                foreach (RunState run in runs)
                    if (run.Plan == plan) return run.LastNotIdleReason ?? "";
            }
            return "";
        }

        private void EnsureThread()
        {
            lock (gate)
            {
                if (thread != null && thread.IsAlive) return;
                stopRequested = false;
                thread = new Thread(Loop);
                thread.IsBackground = true;
                thread.Name = "AA2 Plan Runner";
                thread.Start();
            }
        }

        private void Loop()
        {
            while (!stopRequested)
            {
                try
                {
                    TickAll();
                }
                catch (Exception ex)
                {
                    Log("Plan runner error: " + ex.Message, PlannerLogLevel.Error);
                }

                for (int i = 0; i < TickMs / 10 && !stopRequested; i++)
                    Thread.Sleep(10);
            }
        }

        private void TickAll()
        {
            if (engine == null || !engine.HookInstalled)
                return;

            RunState[] snapshot;
            lock (gate) snapshot = runs.ToArray();
            if (snapshot.Length == 0) return;

            for (int i = 0; i < snapshot.Length; i++)
            {
                try
                {
                    Tick(snapshot[i]);
                }
                catch (Exception ex)
                {
                    snapshot[i].Status = "error: " + ex.Message;
                    Log("Plan step error: " + ex.Message, PlannerLogLevel.Error);
                }
            }
        }

        private void Tick(RunState run)
        {
            PlanQueue plan = run.Plan;
            if (plan == null || !plan.Started || run.Cancelled) return;

            // A scene/class reload allocates new character pointers. Resolve
            // the seat every tick so a running plan survives it.
            if (run.Seat >= 0)
            {
                uint current = ResolveCharacter(run.Seat);
                if (current != 0) run.Character = current;
            }

            run.Activity = ResolveActivity(run.Character);
            if (run.Activity == 0)
            {
                run.Status = "waiting: character not in the world";
                return;
            }

            switch (run.Phase)
            {
                case Phase.Idle:
                    TickIdle(run);
                    break;
                case Phase.Armed:
                    TickArmed(run);
                    break;
                case Phase.Observing:
                    TickObserving(run);
                    break;
                case Phase.Cooldown:
                    if (DateTime.UtcNow >= run.NextActionAt) run.Phase = Phase.Idle;
                    break;
            }
        }

        private void TickIdle(RunState run)
        {
            PlanQueue plan = run.Plan;

            int skipped = plan.SkipDisabled();
            if (skipped > 0)
            {
                Log("  " + Describe(plan) + " skipped " + skipped +
                    " disabled step(s).", PlannerLogLevel.Detail);
            }

            PlanStep step = plan.Current;
            if (step == null || !step.Enabled)
            {
                FinishPlan(run);
                return;
            }

            CharacterSummary state = reader.ReadSummary(run.Character);
            if (state == null)
            {
                run.Status = "waiting: state unreadable";
                return;
            }

            if (run.Params == null || run.Step != step)
            {
                run.Step = step;
                run.Params = BuildParams(step, out run.TargetActivity);
                run.DirectTried = false;
                step.State = PlanStepState.Running;
            }

            // Activity and interface pointers change on scene/class reloads, and
            // a passive plan can wait a long time. Re-resolve them every tick.
            if (step.TargetSeat >= 0)
            {
                uint seated = ResolveCharacter(step.TargetSeat);
                if (seated == 0)
                {
                    run.Status = "waiting: the target is not in the world";
                    run.LastNotIdleReason = "target seat " + step.TargetSeat +
                        " is not present";
                    return;
                }
                step.TargetCharacter = seated;
            }
            if (step.AboutSeat >= 0)
            {
                step.AboutCharacter = ResolveCharacter(step.AboutSeat);
                if (step.AboutCharacter == 0) run.Params.Target2 = 0;
            }

            if (step.TargetCharacter != 0)
            {
                uint activity = ResolveActivity(step.TargetCharacter);
                if (activity == 0)
                {
                    run.Status = "waiting: the target is not in the world";
                    run.LastNotIdleReason = "target activity is 0";
                    return;
                }
                run.TargetActivity = activity;
                run.Params.Target1 = activity;
                run.TargetInterface = ResolveInterface(activity);
                if (run.TargetInterface == 0)
                {
                    run.Status = "waiting: the target interface is unavailable";
                    run.LastNotIdleReason = "target interface is 0";
                    return;
                }
            }
            else
            {
                run.TargetInterface = ResolveInterface(run.Params.Target1);
            }

            if (step.AboutCharacter != 0)
            {
                uint about = ResolveActivity(step.AboutCharacter);
                if (about != 0) run.Params.Target2 = about;
            }

            // A playlist must not replace its own action mid-animation, but a
            // stuck action id must not stall the plan forever.
            if (run.LastActionId >= 0)
            {
                if (state.ActionId == run.LastActionId)
                {
                    if ((DateTime.UtcNow - run.LastActionAt).TotalMilliseconds >=
                        ActionEndTimeoutMs)
                    {
                        Log("  " + Describe(plan) + " previous action " +
                            run.LastActionId + " still running after " +
                            (ActionEndTimeoutMs / 1000) + "s; continuing.",
                            PlannerLogLevel.Warning);
                        run.LastActionId = -1;
                    }
                    else
                    {
                        run.Status = "waiting for the previous step to finish (action " +
                            run.LastActionId + ")";
                        return;
                    }
                }
                else
                {
                    run.LastActionId = -1;
                }
            }

            if (plan.Mode == PlanMode.Normal)
            {
                // First step: wait for the character herself to start toward
                // the planned target. Once latched, the rest of the playlist
                // continues without a new initiation.
                if (!run.ChainActive)
                {
                    if (!WantsStep(state, plan, step, run))
                    {
                        run.Status = "normal: waiting for " + Describe(plan) +
                            " to choose " + TargetText(step);
                        run.LastNotIdleReason = DescribeNormalMiss(state, run.Params);
                        return;
                    }
                    run.ChainActive = true;
                    Log("  " + Describe(plan) + " triggered (" +
                        TriggerName(plan.Trigger) + "): " +
                        TargetText(step) + ".", PlannerLogLevel.Info);
                }
            }
            else
            {
                if (plan.Mode == PlanMode.Ignore)
                {
                    if (PlannerPrimitives.IsIdle(state))
                    {
                        // Nothing to cancel; fall through and arm.
                    }
                    else
                    {
                        string cancelMessage;
                        engine.StopAction(run.Character, run.Activity,
                            out cancelMessage);
                        run.Status = "ignore: cancelled current action";
                        run.NextActionAt = DateTime.UtcNow.AddMilliseconds(250);
                        run.Phase = Phase.Cooldown;
                        return;
                    }
                }
                else if (plan.Mode == PlanMode.Forced)
                {
                    // Measured against TAA2 (2026-11-09): Forced Interaction
                    // waits for the character to start an interaction; it does
                    // not force while idle. Non-interaction steps (room/self)
                    // still wait for idle.
                    bool interactionStep = run.Params != null &&
                        run.Params.MovementType != ActionParams.MoveToRoomType &&
                        run.Params.Target1 != 0;
                    if (interactionStep && !IsApproach(state.MovementType))
                    {
                        run.Status = "forced: waiting for an interaction (" +
                            DescribeState(state) + ")";
                        run.LastNotIdleReason = DescribeState(state);
                        return;
                    }
                    if (!interactionStep && !PlannerPrimitives.IsIdle(state))
                    {
                        run.Status = "forced: waiting for idle (" +
                            DescribeState(state) + ")";
                        run.LastNotIdleReason = DescribeState(state);
                        return;
                    }
                }
                // ForcedPerform arms immediately, even while busy.
            }

            if (DateTime.UtcNow < run.NextActionAt) return;
            if (run.Cancelled) return;

            if (run.Params == null)
            {
                FailStep(run, "the step parameters are missing", null);
                return;
            }

            string message;
            uint key;
            if (!engine.Queue(run.Activity, run.Params, out key, out message))
            {
                if (++run.Attempts >= AttemptLimit(plan.Mode))
                {
                    FailStep(run, message, null);
                }
                else
                {
                    run.Status = "arm failed (attempt " + run.Attempts + "): " + message;
                    run.NextActionAt = DateTime.UtcNow.AddMilliseconds(CooldownMs);
                    run.Phase = Phase.Cooldown;
                }
                return;
            }

            run.Key = key;
            run.Phase = Phase.Armed;
            run.ArmedAt = DateTime.UtcNow;
            run.Deadline = run.ArmedAt.AddMilliseconds(ArmTimeoutMs);
            run.ArmedCallCount = engine.CallCount(run.Key);
            run.Status = "armed (attempt " + (run.Attempts + 1) + "/" +
                AttemptLimit(plan.Mode) + ")";
            Log("  " + Describe(plan) + " step " + plan.CurrentNumber + "/" +
                plan.Count + ": " + message, PlannerLogLevel.Detail);
        }

        private void TickArmed(RunState run)
        {
            if (!engine.IsPending(run.Key))
            {
                run.Phase = Phase.Observing;
                run.Deadline = DateTime.UtcNow.AddMilliseconds(ObserveTimeoutMs);
                run.Status = "consumed; waiting for the action to show";
                return;
            }

            if (DateTime.UtcNow < run.Deadline) return;

            // The planner never consumed the slot. Distinguish "the game was
            // not simulating at all" (pause, loading, a conversation stall)
            // from a real refusal: only the latter spends an attempt.
            engine.ClearQueue(run.Key);
            bool plannerAsleep = engine.CallCount(run.Key) == run.ArmedCallCount;

            // Forced Perform's "no matter what": when the planner is asleep,
            // write the action into the activity block directly (TAA2's buggy
            // in-place animation path).
            if (plannerAsleep && run.Plan.Mode == PlanMode.ForcedPerform &&
                !run.DirectTried && run.Step != null)
            {
                string directMessage;
                if (TryDirectPerform(run, out directMessage))
                {
                    run.DirectTried = true;
                    run.Phase = Phase.Observing;
                    run.Deadline = DateTime.UtcNow.AddMilliseconds(ObserveTimeoutMs);
                    run.Status = "forced perform: " + directMessage;
                    Log("  " + Describe(run.Plan) + " " + directMessage + ".",
                        PlannerLogLevel.Warning);
                    return;
                }

                run.Attempts++;
                Log("  forced perform fallback failed: " + directMessage,
                    PlannerLogLevel.Warning);
                if (run.Attempts >= AttemptLimit(run.Plan.Mode))
                {
                    FailStep(run, "forced perform fallback failed: " +
                        directMessage, null);
                    return;
                }
            }

            if (plannerAsleep)
            {
                run.Params = null;
                run.Phase = Phase.Idle;
                run.NextActionAt = DateTime.UtcNow.AddMilliseconds(200);
                run.Status = "planner idle; re-arming";
                return;
            }

            run.Attempts++;
            if (run.Attempts >= AttemptLimit(run.Plan.Mode))
            {
                FailStep(run, "the planner did not consume the step", null);
                return;
            }

            run.Status = "not consumed; retrying (" + run.Attempts + "/" +
                AttemptLimit(run.Plan.Mode) + ")";
            run.Params = null;
            run.Phase = Phase.Idle;
            run.NextActionAt = DateTime.UtcNow.AddMilliseconds(CooldownMs);
        }

        private void TickObserving(RunState run)
        {
            CharacterSummary state = reader.ReadSummary(run.Character);
            if (state != null && PlannerPrimitives.MatchesStep(state, run.Params))
            {
                CompleteStep(run);
                return;
            }

            if (DateTime.UtcNow < run.Deadline) return;

            run.Attempts++;
            if (run.Attempts >= AttemptLimit(run.Plan.Mode))
            {
                FailStep(run, "the action did not stick", null);
                return;
            }

            run.Status = "action did not stick; retrying (" + run.Attempts + "/" +
                AttemptLimit(run.Plan.Mode) + ")";
            run.Params = null;
            run.Phase = Phase.Idle;
            run.NextActionAt = DateTime.UtcNow.AddMilliseconds(CooldownMs);
        }

        private void CompleteStep(RunState run)
        {
            PlanQueue plan = run.Plan;
            int number = plan.CurrentNumber;
            int finishedAction = -1;
            if (run.Params != null &&
                run.Params.MovementType != ActionParams.MoveToRoomType &&
                run.Params.ActionId >= 0)
            {
                finishedAction = run.Params.ActionId;
            }
            Log("  " + Describe(plan) + " step " + number + "/" + plan.Count +
                " done: " + (run.Step == null ? "?" : run.Step.Describe()),
                PlannerLogLevel.Success);

            run.Attempts = 0;
            run.Step = null;
            run.Params = null;
            run.LastActionId = finishedAction;
            run.LastActionAt = DateTime.UtcNow;
            bool hasNext = plan.Advance(PlanStepState.Done, "");
            if (!hasNext)
            {
                FinishPlan(run);
                return;
            }

            run.Phase = Phase.Cooldown;
            run.NextActionAt = DateTime.UtcNow.AddMilliseconds(CooldownMs);
            run.Status = "step " + number + " done; next: " +
                (plan.Current == null ? "?" : plan.Current.Describe());
        }

        private void FailStep(RunState run, string reason, string note)
        {
            PlanQueue plan = run.Plan;
            run.Attempts = 0;
            run.Step = null;
            run.Params = null;
            bool hasNext = plan.Advance(PlanStepState.Failed, reason);
            Log("  " + Describe(plan) + " step failed: " + reason,
                PlannerLogLevel.Warning);
            if (!hasNext)
            {
                FinishPlan(run);
                return;
            }

            run.Phase = Phase.Cooldown;
            run.NextActionAt = DateTime.UtcNow.AddMilliseconds(CooldownMs);
            run.Status = "step failed: " + reason;
        }

        private void FinishPlan(RunState run)
        {
            PlanQueue plan = run.Plan;
            run.ChainActive = false;
            run.LastActionId = -1;
            run.Phase = Phase.Cooldown;
            run.Status = "finished";
            Log("Plan finished for " + Describe(plan) + ".", PlannerLogLevel.Success);

            // Keep the steps and their final states; "finished" must not look
            // like a user stop (Stop clears Finished).
            plan.Complete();

            Action<PlanQueue> finished = PlanFinished;
            if (finished != null)
            {
                try { finished(plan); }
                catch { }
            }
        }

        private void StopPlanLocked(RunState run, bool cancelAction)
        {
            if (run == null) return;
            run.Cancelled = true;
            run.ChainActive = false;
            run.LastActionId = -1;
            if (run.Key != 0) engine.ClearQueue(run.Key);
            if (cancelAction && run.Character != 0)
            {
                string ignored;
                engine.StopAction(run.Character, run.Activity, out ignored);
            }
            if (run.Step != null)
                run.Step.State = PlanStepState.Skipped;
            run.Status = "stopped";
        }

        private static int AttemptLimit(PlanMode mode)
        {
            return mode == PlanMode.Normal ? NormalAttempts : ForcedAttempts;
        }

        private ActionParams BuildParams(PlanStep step, out uint targetActivity)
        {
            ActionParams p = step.Params == null
                ? new ActionParams() : step.Params.Clone();

            targetActivity = 0;
            if (step.TargetCharacter != 0)
            {
                targetActivity = ResolveActivity(step.TargetCharacter);
                if (targetActivity != 0) p.Target1 = targetActivity;
            }
            if (step.AboutCharacter != 0)
            {
                uint activity = ResolveActivity(step.AboutCharacter);
                if (activity != 0) p.Target2 = activity;
            }
            return p;
        }

        /// <summary>
        /// Forced Perform fallback when the planner is not ticking: cancel the
        /// current state and write the action into the activity block. The game
        /// plays it in place (TAA2's "buggy animation" path). Only ForcedPerform
        /// calls this.
        /// </summary>
        private bool TryDirectPerform(RunState run, out string message)
        {
            message = null;
            if (mem == null || !mem.IsAttached || !mem.CanWrite)
            {
                message = "no write access";
                return false;
            }
            if (run.Activity == 0 || run.Params == null)
            {
                message = "no activity/parameters";
                return false;
            }
            if (run.Params.MovementType == ActionParams.MoveToRoomType)
            {
                message = "room steps are not direct-performed";
                return false;
            }
            int action = run.Params.ActionId;
            if (action < 0)
            {
                message = "no action id";
                return false;
            }

            // End the current action/conversation first, like Ignore/Stop.
            string ignored;
            engine.StopAction(run.Character, run.Activity, out ignored);

            bool ok = true;
            ok &= mem.WriteBytes(run.Activity + 0x40,
                BitConverter.GetBytes(action));
            ok &= mem.WriteBytes(run.Activity + 0x3C,
                BitConverter.GetBytes(0));
            ok &= mem.WriteBytes(run.Activity + 0x38,
                BitConverter.GetBytes(0));
            ok &= mem.WriteBytes(run.Activity + 0x44,
                BitConverter.GetBytes(-1));
            ok &= mem.WriteBytes(run.Activity + 0x48,
                BitConverter.GetBytes(-1));
            if (!ok)
            {
                message = string.IsNullOrEmpty(mem.WriteError)
                    ? "activity write failed" : mem.WriteError;
                return false;
            }

            int check = mem.ReadI32(run.Activity + 0x40) ?? -999;
            if (check != action)
            {
                message = "write not visible (action=" + check + ")";
                return false;
            }

            message = "direct perform action " + action + " in place";
            return true;
        }

        private uint ResolveActivity(uint character)
        {
            if (mem == null || character == 0) return 0;
            uint status = mem.ReadU32(character + 0xF6C) ?? 0;
            if (status == 0) return 0;
            return mem.ReadU32(status + 0x28) ?? 0;
        }

        /// <summary>
        /// Finds the current character pointer for a roster seat by scanning the
        /// class array. Used so a plan keyed by seat survives pointer changes
        /// after a scene/class reload.
        /// </summary>
        private uint ResolveCharacter(int seat)
        {
            if (mem == null || seat < 0) return 0;
            uint root = mem.ReadU32(mem.ModuleBase + 0x376164) ?? 0;
            if (root == 0) return 0;
            uint start = mem.ReadU32(root + 0x6C) ?? 0;
            uint end = mem.ReadU32(root + 0x70) ?? 0;
            if (start == 0 || end <= start) return 0;

            int count = (int)((end - start) / 4);
            if (count > 25) count = 25;
            for (int i = 0; i < count; i++)
            {
                uint pointer = mem.ReadU32(start + (uint)(i * 4)) ?? 0;
                if (pointer == 0) continue;
                byte s = mem.ReadU8(pointer + 0x3C) ?? 255;
                if (s == seat) return pointer;
            }
            return 0;
        }

        private uint ResolveInterface(uint activity)
        {
            if (mem == null || activity == 0) return 0;
            return mem.ReadU32(activity + 0xE4) ?? 0;
        }

        /// <summary>
        /// Passive predicate, measured against TAA2 (2026-11-08): the plan fires
        /// when the character <b>herself starts toward</b> the planned target
        /// (movement 3/5/6) and the movement descriptor's walk target is that
        /// target. The trigger can instead be the target starting toward the
        /// actor (TAA2's “Other NPC Start Interaction”), or either side.
        /// </summary>
        private bool WantsStep(CharacterSummary s, PlanQueue plan,
            PlanStep step, RunState run)
        {
            if (s == null) return false;
            ActionParams p = run.Params;
            if (p == null) return false;

            if (p.MovementType == ActionParams.MoveToRoomType)
                return PlannerPrimitives.IsIdle(s);

            uint target = p.Target1;
            if (target == 0) return PlannerPrimitives.IsIdle(s);

            bool actorStarts;
            if (run.TargetInterface != 0)
            {
                actorStarts = s.WalkTargetInterface == run.TargetInterface &&
                    IsApproach(s.MovementType);
            }
            else
            {
                actorStarts = s.TargetActivity == target &&
                    IsApproach(s.MovementType);
            }

            if (plan.Trigger == PlanTrigger.Actor) return actorStarts;
            if (plan.Trigger == PlanTrigger.Either && actorStarts) return true;

            bool targetStarts = false;
            if (step != null && step.TargetCharacter != 0)
            {
                CharacterSummary t = reader.ReadSummary(step.TargetCharacter);
                uint actorInterface = ResolveInterface(run.Activity);
                targetStarts = t != null && actorInterface != 0 &&
                    t.WalkTargetInterface == actorInterface &&
                    IsApproach(t.MovementType);
            }

            return targetStarts;
        }

        private static bool IsApproach(int movement)
        {
            return movement == 3 || movement == 5 || movement == 6;
        }

        public static string TriggerName(PlanTrigger trigger)
        {
            switch (trigger)
            {
                case PlanTrigger.Target: return "target";
                case PlanTrigger.Either: return "either side";
                default: return "actor";
            }
        }

        private static string DescribeNormalMiss(CharacterSummary s, ActionParams p)
        {
            if (s == null) return "state unreadable";
            if (p == null) return "no parameters";
            if (p.MovementType == ActionParams.MoveToRoomType)
                return "room step waits for idle (" + DescribeState(s) + ")";
            if (p.Target1 == 0) return "no target; waits for idle";
            return "walkTarget 0x" + s.WalkTargetInterface.ToString("X8") +
                " (about 0x" + s.TargetActivity.ToString("X8") + ") (" +
                DescribeState(s) + ")";
        }

        private static string DescribeState(CharacterSummary s)
        {
            if (s == null) return "state unreadable";
            return "movement=" + s.MovementType + " action=" + s.ActionId +
                " busy=" + s.AnimLocked + "/" + s.Loading + "/" +
                s.InConversation;
        }

        private static string TargetText(PlanStep step)
        {
            if (step == null) return "a step";
            if (step.Label != "") return step.Label;
            return "action " + (step.Params == null ? "?" :
                step.Params.ActionId.ToString());
        }

        private static string Describe(PlanQueue plan)
        {
            if (plan == null) return "?";
            if (!string.IsNullOrEmpty(plan.CharacterName)) return plan.CharacterName;
            return "0x" + plan.Character.ToString("X8");
        }

        public static string ModeName(PlanMode mode)
        {
            switch (mode)
            {
                case PlanMode.Normal: return "Normal";
                case PlanMode.Forced: return "Forced interaction";
                case PlanMode.ForcedPerform: return "Forced perform";
                case PlanMode.Ignore: return "Ignore interaction";
                default: return mode.ToString();
            }
        }

        private void Log(string text, PlannerLogLevel level)
        {
            Action<string, PlannerLogLevel> sink = log;
            if (sink != null) sink(text, level);
        }
    }
}
