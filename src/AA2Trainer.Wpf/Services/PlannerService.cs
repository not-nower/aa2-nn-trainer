using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;

namespace AA2Trainer.WpfPrototype
{
    /// <summary>
    /// Owns the planner engine, runner and one PlanQueue per seat. The UI calls
    /// these methods on the dispatcher; the runner logs and finishes on its own
    /// thread, so all notifications are marshalled back to the dispatcher.
    /// </summary>
    public sealed class PlannerService : IDisposable
    {
        private readonly Dispatcher dispatcher;
        private readonly ActionStateReader reader;
        private readonly ActionPlannerEngine engine;
        private readonly PlanRunner runner;
        private readonly Dictionary<int, PlanQueue> plans =
            new Dictionary<int, PlanQueue>();
        private readonly object sync = new object();
        private PlanQueue clipboard;
        private bool disposed;

        public PlannerService(GameMemory memory, Dispatcher dispatcher)
        {
            this.dispatcher = dispatcher;
            reader = new ActionStateReader(memory);
            engine = new ActionPlannerEngine(memory);
            runner = new PlanRunner(engine, reader, memory, LogFromRunner);
            runner.PlanFinished += OnPlanFinished;
        }

        public event Action<string> Log;
        public event Action StateChanged;

        public bool HookInstalled
        {
            get { return engine != null && engine.HookInstalled; }
        }

        public string HookState
        {
            get
            {
                if (engine == null) return "Hook: unavailable";
                return engine.HookInstalled
                    ? "Hook: installed (" + engine.HookModeName + ")"
                    : "Hook: not installed";
            }
        }

        public bool ObserveOnly
        {
            get { return engine != null && engine.ObserveOnly; }
        }

        public bool InstallHook(out string message)
        {
            message = null;
            if (engine == null)
            {
                message = "The planner engine is unavailable.";
                return false;
            }
            bool ok = engine.InstallHook(out message);
            PublishLog(ok ? "Planner hook installed." :
                "Hook install failed: " + (message ?? "unknown error"));
            RaiseState();
            return ok;
        }

        public bool UninstallHook(out string message)
        {
            message = null;
            if (engine == null)
            {
                message = "The planner engine is unavailable.";
                return false;
            }
            bool ok = engine.UninstallHook(out message);
            PublishLog(ok ? "Planner hook uninstalled." :
                "Hook uninstall failed: " + (message ?? "unknown error"));
            RaiseState();
            return ok;
        }

        public bool RepairHook(out string message)
        {
            message = null;
            if (engine == null)
            {
                message = "The planner engine is unavailable.";
                return false;
            }
            if (!engine.RepairHook(out message))
            {
                PublishLog("Hook repair failed: " +
                    (message ?? "unknown error"));
                RaiseState();
                return false;
            }

            string installMessage;
            bool installed = engine.InstallHook(out installMessage);
            message = installed
                ? installMessage
                : "Vanilla bytes restored, but reinstall failed: " +
                    (installMessage ?? "unknown error");
            PublishLog(installed
                ? "Planner hook repaired and reinstalled."
                : message);
            RaiseState();
            return installed;
        }

        public bool SetObserveOnly(bool enabled)
        {
            if (engine == null) return false;
            bool ok = engine.SetObserveOnly(enabled);
            PublishLog(ok
                ? (enabled ? "Observe-only enabled." : "Observe-only disabled.")
                : "Could not change observe-only mode.");
            RaiseState();
            return ok;
        }

        public bool TryReadObservation(out uint key, out byte[] parameters,
            out uint counter)
        {
            key = 0;
            parameters = null;
            counter = 0;
            return engine != null &&
                engine.ReadObservation(out key, out parameters, out counter);
        }

        public PlanQueue GetPlan(int seat, string name)
        {
            lock (sync)
            {
                PlanQueue plan;
                if (!plans.TryGetValue(seat, out plan))
                {
                    plan = new PlanQueue();
                    plan.Seat = seat;
                    plan.CharacterName = name ?? "";
                    plans[seat] = plan;
                }
                else if (!string.IsNullOrEmpty(name))
                {
                    plan.CharacterName = name;
                }
                return plan;
            }
        }

        public PlannerSnapshot BuildSnapshot(int seat, string name)
        {
            PlanQueue plan = GetPlan(seat, name);
            PlannerSnapshot snapshot = new PlannerSnapshot();
            snapshot.Seat = seat;
            snapshot.HookInstalled = HookInstalled;
            snapshot.HookState = HookState;
            snapshot.ObserveOnly = ObserveOnly;
            snapshot.Mode = plan.Mode;
            snapshot.Trigger = plan.Trigger;
            snapshot.Repeat = plan.Repeat;
            snapshot.Shuffle = plan.Shuffle;
            snapshot.Running = plan.Started;
            snapshot.Status = plan.Started
                ? "Plan: " + runner.GetStatus(plan) + "  |  " +
                    plan.ProgressText()
                : "Plan: not running  |  " +
                    (plan.Count == 1 ? "1 step" : plan.Count + " steps");

            lock (sync)
            {
                for (int i = 0; i < plan.Steps.Count; i++)
                {
                    snapshot.Steps.Add(new PlannerStepItem(
                        plan.Steps[i], i + 1));
                }
            }
            return snapshot;
        }

        public void UpdateSettings(int seat, string name, PlanMode mode,
            PlanTrigger trigger, bool repeat, bool shuffle)
        {
            PlanQueue plan = GetPlan(seat, name);
            plan.Mode = mode;
            plan.Trigger = trigger;
            plan.Repeat = repeat;
            plan.Shuffle = shuffle;
        }

        public bool AddAction(int seat, string name, int targetSeat,
            string targetName, int aboutSeat, string aboutName, int actionId,
            string actionLabel)
        {
            PlanQueue plan = GetPlan(seat, name);
            if (plan.Started)
            {
                PublishLog("Stop the plan before editing its steps.");
                return false;
            }

            ActionParams parameters = ActionParams.TargetAction(actionId, 0);
            string label = "Action " + actionLabel + " -> " +
                (string.IsNullOrEmpty(targetName) ? "?" : targetName);
            if (aboutSeat >= 0 && !string.IsNullOrEmpty(aboutName))
                label += " about " + aboutName;

            PlanStep step = new PlanStep(parameters, label);
            step.TargetSeat = targetSeat;
            step.AboutSeat = aboutSeat;
            plan.Add(step);
            PublishLog("Added plan step: " + label);
            RaiseState();
            return true;
        }

        public bool AddMove(int seat, string name, int roomId, string roomLabel)
        {
            PlanQueue plan = GetPlan(seat, name);
            if (plan.Started)
            {
                PublishLog("Stop the plan before editing its steps.");
                return false;
            }
            string label = "Move to room " + roomId + " (" + roomLabel + ")";
            plan.Add(new PlanStep(ActionParams.MoveToRoom(roomId), label));
            PublishLog("Added plan step: " + label);
            RaiseState();
            return true;
        }

        public bool RemoveStep(int seat, string name, int index)
        {
            PlanQueue plan = GetPlan(seat, name);
            if (plan.Started || index < 0 || index >= plan.Steps.Count)
                return false;
            if (!plan.RemoveAt(index)) return false;
            RaiseState();
            return true;
        }

        public bool MoveStep(int seat, string name, int index, int delta)
        {
            PlanQueue plan = GetPlan(seat, name);
            if (plan.Started) return false;
            if (index < 0 || index >= plan.Steps.Count) return false;
            if (delta < 0) plan.MoveUp(index);
            else if (delta > 0) plan.MoveDown(index);
            else return false;
            RaiseState();
            return true;
        }

        public bool ClearSteps(int seat, string name)
        {
            PlanQueue plan = GetPlan(seat, name);
            if (plan.Started) return false;
            plan.ClearSteps();
            RaiseState();
            return true;
        }

        public bool Start(int seat, string name, out string message)
        {
            PlanQueue plan = GetPlan(seat, name);
            bool ok = runner.StartPlan(plan, out message);
            RaiseState();
            return ok;
        }

        public void Stop(int seat, string name)
        {
            PlanQueue plan = GetPlan(seat, name);
            runner.StopPlan(plan, false);
            RaiseState();
        }

        public bool CopyPlan(int seat, string name)
        {
            PlanQueue plan = GetPlan(seat, name);
            clipboard = ClonePlan(plan);
            PublishLog("Copied " + plan.Steps.Count + " step(s).");
            return true;
        }

        public bool PastePlan(int seat, string name)
        {
            if (clipboard == null) return false;
            PlanQueue plan = GetPlan(seat, name);
            if (plan.Started) return false;
            plan.ClearSteps();
            foreach (PlanStep step in clipboard.Steps)
                plan.Add(CloneStep(step));
            plan.Mode = clipboard.Mode;
            plan.Trigger = clipboard.Trigger;
            plan.Repeat = clipboard.Repeat;
            plan.Shuffle = clipboard.Shuffle;
            PublishLog("Pasted " + plan.Steps.Count + " step(s).");
            RaiseState();
            return true;
        }

        public bool SavePlans(out string message)
        {
            message = null;
            try
            {
                PlanStore.Save(PlanStore.DefaultPath, plans.Values);
                PublishLog("Saved plans to " + PlanStore.DefaultPath + ".");
                return true;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                PublishLog("Could not save plans: " + message);
                return false;
            }
        }

        public bool LoadPlans(out string message)
        {
            message = null;
            try
            {
                List<PlanQueue> loaded = PlanStore.Load(PlanStore.DefaultPath);
                if (loaded == null || loaded.Count == 0)
                {
                    message = "No saved plans were found.";
                    PublishLog(message);
                    return false;
                }

                lock (sync)
                {
                    foreach (PlanQueue plan in loaded)
                    {
                        if (plan == null || plan.Seat < 0) continue;
                        PlanQueue target = GetPlan(plan.Seat, plan.CharacterName);
                        if (target.Started) continue;
                        target.ClearSteps();
                        foreach (PlanStep step in plan.Steps)
                            target.Add(CloneStep(step));
                        target.Mode = plan.Mode;
                        target.Trigger = plan.Trigger;
                        target.Repeat = plan.Repeat;
                        target.Shuffle = plan.Shuffle;
                    }
                }
                PublishLog("Loaded " + loaded.Count + " plan(s).");
                RaiseState();
                return true;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                PublishLog("Could not load plans: " + message);
                return false;
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (runner != null)
            {
                runner.PlanFinished -= OnPlanFinished;
                runner.Dispose();
            }
            if (engine != null) engine.Dispose();
        }

        private void OnPlanFinished(PlanQueue plan)
        {
            PublishLog("Plan finished for " +
                (plan == null ? "?" : plan.CharacterName) + ".");
            RaiseState();
        }

        private void LogFromRunner(string message, PlannerLogLevel level)
        {
            if (string.IsNullOrEmpty(message)) return;
            PublishLog(message);
        }

        private void PublishLog(string message)
        {
            if (disposed || string.IsNullOrEmpty(message)) return;
            dispatcher.BeginInvoke(new Action(delegate
            {
                Action<string> handler = Log;
                if (handler != null) handler(message);
            }));
        }

        private void RaiseState()
        {
            if (disposed) return;
            dispatcher.BeginInvoke(new Action(delegate
            {
                Action handler = StateChanged;
                if (handler != null) handler();
            }));
        }

        private static PlanQueue ClonePlan(PlanQueue source)
        {
            PlanQueue copy = new PlanQueue();
            copy.Seat = source.Seat;
            copy.Character = source.Character;
            copy.CharacterName = source.CharacterName;
            copy.Mode = source.Mode;
            copy.Trigger = source.Trigger;
            copy.Repeat = source.Repeat;
            copy.Shuffle = source.Shuffle;
            foreach (PlanStep step in source.Steps)
                copy.Add(CloneStep(step));
            return copy;
        }

        private static PlanStep CloneStep(PlanStep source)
        {
            ActionParams parameters = new ActionParams();
            if (source.Params != null)
            {
                parameters.ActionId = source.Params.ActionId;
                parameters.MovementType = source.Params.MovementType;
                parameters.RoomTarget = source.Params.RoomTarget;
                parameters.Unknown = source.Params.Unknown;
                parameters.Target1 = source.Params.Target1;
                parameters.Target2 = source.Params.Target2;
                parameters.Unknown2 = source.Params.Unknown2;
            }

            PlanStep copy = new PlanStep(parameters, source.Label);
            copy.TargetCharacter = source.TargetCharacter;
            copy.TargetSeat = source.TargetSeat;
            copy.AboutCharacter = source.AboutCharacter;
            copy.AboutSeat = source.AboutSeat;
            copy.Enabled = source.Enabled;
            return copy;
        }
    }
}
