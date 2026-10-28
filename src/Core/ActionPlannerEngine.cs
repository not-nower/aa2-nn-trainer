using System;
using System.Collections.Generic;
using System.Threading;

namespace AA2Trainer
{
    public enum PlannerLogLevel
    {
        Info,
        Success,
        Warning,
        Error,
        Detail
    }

    public sealed class PlannerRunResult
    {
        public bool Observed;
        public bool Cancelled;
        public bool TimedOut;
        public bool ConsumedAny;
        public bool AbortedBeforeQueue;
        public int Attempts;
    }

    /// <summary>
    /// Owns the planner hook and the proven queue/wait/observe sequence. The UI
    /// is responsible for marshalling log callbacks to the UI thread.
    /// </summary>
    public sealed class ActionPlannerEngine : IDisposable
    {
        private readonly GameMemory mem;
        private readonly ActionStateReader reader;
        private readonly PlannerHook hook;
        private readonly int pid;

        public bool HookInstalled { get { return hook.Installed; } }
        public string HookModeName { get { return hook.ModeName; } }
        public string LastHookError { get { return hook.LastError; } }
        public int Pid { get { return pid; } }

        public ActionPlannerEngine(GameMemory mem)
        {
            this.mem = mem;
            pid = mem == null ? 0 : mem.Pid;
            reader = new ActionStateReader(mem);
            hook = new PlannerHook(mem);
        }

        public bool InstallHook(out string message)
        {
            return hook.Install(out message);
        }

        public bool UninstallHook(out string message)
        {
            return hook.Uninstall(out message);
        }

        public void Dispose()
        {
            hook.Dispose();
        }

        public CharacterState ReadState(CharacterInfo actor,
            IList<CharacterInfo> roster)
        {
            if (actor == null) return null;
            return reader.ReadState(actor.Pointer, roster);
        }

        public CharacterSummary ReadSummary(CharacterInfo actor)
        {
            if (actor == null) return null;
            return reader.ReadSummary(actor.Pointer);
        }

        public CharacterInfo FindByActivity(uint activity,
            IList<CharacterInfo> roster)
        {
            return reader.FindByActivity(activity, roster);
        }

        public bool Queue(CharacterState actor, ActionParams parameters,
            out string message)
        {
            return hook.Queue(actor, parameters, out message);
        }

        public bool IsPending()
        {
            return hook.IsPending();
        }

        public void ClearQueue()
        {
            hook.Clear();
        }

        public uint HitCount()
        {
            return hook.HitCount();
        }

        public PlannerRunResult Run(CharacterInfo actor,
            IList<CharacterInfo> roster, ActionParams parameters, string label,
            bool retry, bool waitIdle, bool keepRepeating,
            Func<bool> cancelled, Action<string, PlannerLogLevel> log)
        {
            PlannerRunResult result = new PlannerRunResult();
            if (actor == null || parameters == null)
            {
                Log(log, "The actor or action parameters are unavailable.",
                    PlannerLogLevel.Error);
                return result;
            }

            CharacterState before = ReadState(actor, roster);
            if (before == null)
            {
                Log(log, "Actor state unreadable.", PlannerLogLevel.Error);
                return result;
            }

            Log(log, "== " + label + " [planner hook] ==", PlannerLogLevel.Info);
            Log(log, "  Before: " + Describe(before), PlannerLogLevel.Detail);
            Log(log, "  Params: " + parameters.Describe(), PlannerLogLevel.Detail);

            int maxAttempts = keepRepeating ? int.MaxValue :
                (retry ? 3 : 1);
            DateTime deadline = keepRepeating
                ? DateTime.UtcNow.AddMinutes(10)
                : DateTime.MaxValue;
            if (keepRepeating)
                Log(log, "  Keep repeating is on (up to 10 minutes).",
                    PlannerLogLevel.Info);

            int attempt = 0;
            while (attempt < maxAttempts)
            {
                attempt++;
                result.Attempts = attempt;
                bool verbose = !keepRepeating || attempt == 1 || attempt % 5 == 0;

                if (Cancelled(cancelled))
                {
                    result.Cancelled = true;
                    break;
                }
                if (keepRepeating && DateTime.UtcNow >= deadline)
                {
                    result.TimedOut = true;
                    break;
                }

                bool skipIdleWait = parameters.ActionId == ActionParams.StareActionId;
                if (!skipIdleWait && waitIdle)
                {
                    if (!WaitUntilIdle(actor, roster,
                        keepRepeating ? 30000 : 15000, cancelled, log))
                    {
                        if (Cancelled(cancelled))
                        {
                            result.Cancelled = true;
                            break;
                        }
                        if (keepRepeating)
                        {
                            Log(log, "  Still not idle; continuing to wait...",
                                PlannerLogLevel.Warning);
                            continue;
                        }
                        Log(log, "  Actor did not become idle in time; action cancelled.",
                            PlannerLogLevel.Error);
                        result.AbortedBeforeQueue = true;
                        break;
                    }
                }

                CharacterState current = ReadState(actor, roster) ?? before;
                string message;
                if (!hook.Queue(current, parameters, out message))
                {
                    Log(log, "  [" + attempt + "] queue failed: " + message,
                        PlannerLogLevel.Error);
                    break;
                }

                if (verbose)
                    Log(log, "  [" + attempt + "] " + message,
                        PlannerLogLevel.Success);
                else
                    Log(log, "  [" + attempt + "] queued",
                        PlannerLogLevel.Detail);

                DateTime start = DateTime.UtcNow;
                bool consumed = false;
                for (int i = 0; i < 20; i++)
                {
                    Thread.Sleep(100);
                    if (Cancelled(cancelled))
                    {
                        result.Cancelled = true;
                        break;
                    }
                    if (!hook.IsPending())
                    {
                        consumed = true;
                        break;
                    }
                }

                if (result.Cancelled) break;

                if (!consumed)
                {
                    Log(log, "  [" + attempt +
                        "] still pending after 2s (hits=" + hook.HitCount() +
                        ").", PlannerLogLevel.Warning);
                    if (verbose)
                        LogPlannerNotTicking(ReadState(actor, roster), log);

                    if (keepRepeating) Thread.Sleep(1500);
                    else if (attempt < maxAttempts) Thread.Sleep(800);
                    continue;
                }

                result.ConsumedAny = true;
                int ms = (int)(DateTime.UtcNow - start).TotalMilliseconds;
                if (verbose)
                    Log(log, "  [" + attempt + "] consumed at +" + ms +
                        "ms (hits=" + hook.HitCount() + ").",
                        PlannerLogLevel.Success);
                else
                    Log(log, "  [" + attempt + "] consumed",
                        PlannerLogLevel.Detail);

                if (WaitForActionObserved(actor, roster, parameters, 1500,
                    cancelled))
                {
                    result.Observed = true;
                    break;
                }

                if (Cancelled(cancelled))
                {
                    result.Cancelled = true;
                    break;
                }

                Log(log, "  [" + attempt + "] action did not stick.",
                    PlannerLogLevel.Warning);
                if (keepRepeating) Thread.Sleep(1500);
                else if (attempt < maxAttempts) Thread.Sleep(800);
            }

            if (result.Cancelled)
            {
                Log(log, "  Stopped by user.", PlannerLogLevel.Warning);
            }
            else if (result.TimedOut)
            {
                Log(log, "  Keep-repeat timed out after 10 minutes.",
                    PlannerLogLevel.Warning);
            }
            else if (!result.Observed && !result.AbortedBeforeQueue)
            {
                Log(log, "  Action was not observed after the final attempt.",
                    PlannerLogLevel.Warning);
                if (!result.ConsumedAny)
                {
                    Log(log, "  The planner never consumed the queue; the NPC was " +
                        "probably not idle (Minna/group, animLocked, or conversation).",
                        PlannerLogLevel.Warning);
                }
                else
                {
                    Log(log, "  The plan was consumed but the AI replaced or " +
                        "cancelled it. It may be game-driven or refused in a " +
                        "private/gender-locked room.", PlannerLogLevel.Warning);
                }
            }

            if (!result.Observed) hook.Clear();

            if (result.Observed || !keepRepeating)
            {
                for (int i = 0; i < 4; i++)
                {
                    Thread.Sleep(400);
                    CharacterState s = ReadState(actor, roster);
                    if (s != null)
                        Log(log, "  +" + ((i + 1) * 400) + "ms: " + Describe(s),
                            PlannerLogLevel.Detail);
                }
            }

            return result;
        }

        private bool WaitUntilIdle(CharacterInfo actor, IList<CharacterInfo> roster,
            int timeoutMs, Func<bool> cancelled, Action<string, PlannerLogLevel> log)
        {
            DateTime start = DateTime.UtcNow;
            DateTime end = start.AddMilliseconds(timeoutMs);
            DateTime nextLog = start;
            const int requiredStableSamples = 5;
            int stable = 0;

            while (DateTime.UtcNow < end)
            {
                if (Cancelled(cancelled)) return false;

                CharacterState s = ReadState(actor, roster);
                if (s != null && IsIdle(s))
                {
                    stable++;
                    if (stable >= requiredStableSamples)
                    {
                        int ms = (int)(DateTime.UtcNow - start).TotalMilliseconds;
                        if (ms > 300)
                            Log(log, "  Waited " + ms + " ms for the NPC to become idle.",
                                PlannerLogLevel.Info);
                        return true;
                    }
                }
                else
                {
                    stable = 0;
                }

                if (DateTime.UtcNow >= nextLog)
                {
                    Log(log, "  Waiting for idle... " +
                        (s == null ? "state unreadable" :
                         "movement=" + s.MovementType + " busy=" +
                         s.AnimLocked + "/" + s.Loading + "/" +
                         s.InConversation), PlannerLogLevel.Detail);
                    nextLog = DateTime.UtcNow.AddSeconds(2);
                }

                Thread.Sleep(250);
            }
            return false;
        }

        private static bool IsIdle(CharacterState s)
        {
            if (s == null) return false;
            if (s.AnimLocked != 0 || s.Loading != 0 || s.InConversation != 0)
                return false;
            return s.MovementType == 0 || s.MovementType == 1;
        }

        private bool WaitForActionObserved(CharacterInfo actor,
            IList<CharacterInfo> roster, ActionParams parameters, int timeoutMs,
            Func<bool> cancelled)
        {
            DateTime end = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < end)
            {
                if (Cancelled(cancelled)) return false;
                CharacterState s = ReadState(actor, roster);
                if (s != null)
                {
                    if (parameters.MovementType == ActionParams.MoveToRoomType)
                    {
                        if (s.MovementType == ActionParams.MoveToRoomType ||
                            s.Location == parameters.RoomTarget)
                            return true;
                    }
                    else if (parameters.ActionId >= 0 &&
                        s.ActionId == parameters.ActionId)
                    {
                        return true;
                    }
                }
                Thread.Sleep(200);
            }
            return false;
        }

        private void LogPlannerNotTicking(CharacterState s,
            Action<string, PlannerLogLevel> log)
        {
            if (s == null)
            {
                Log(log, "  Planner not ticking: state unreadable.",
                    PlannerLogLevel.Warning);
                return;
            }

            string reason;
            if (s.MovementType == 8)
                reason = "the NPC is in a Minna/group action; the planner does not tick.";
            else if (s.AnimLocked != 0)
                reason = "animLocked=1; the planner skips this character.";
            else if (s.InConversation != 0)
                reason = "the NPC is in a conversation; the planner is paused.";
            else if (s.Loading != 0)
                reason = "loading=1.";
            else
                reason = "the planner did not call the hook.";

            Log(log, "  Planner not ticking - " + reason, PlannerLogLevel.Warning);
        }

        public bool StopCurrentAction(CharacterInfo actor,
            IList<CharacterInfo> roster, out string message)
        {
            message = null;
            if (mem == null || !mem.IsAttached || !mem.CanWrite)
            {
                message = "The game is not attached with write access.";
                return false;
            }
            CharacterState s = ReadState(actor, roster);
            if (s == null || s.Activity == 0)
            {
                message = "The actor has no valid activity block.";
                return false;
            }

            uint act = s.Activity;
            byte[] minusOne = BitConverter.GetBytes(-1);

            mem.WriteBytes(act + 0x40, minusOne);
            mem.WriteBytes(act + 0x3C, BitConverter.GetBytes(0));
            mem.WriteBytes(act + 0x15, new byte[] { 0 });
            mem.WriteBytes(act + 0x19, new byte[] { 0 });
            mem.WriteBytes(act + 0x1A, new byte[] { 0 });
            mem.WriteBytes(act + 0x1B, new byte[] { 0 });
            mem.WriteBytes(act + 0x38, BitConverter.GetBytes(0));
            mem.WriteBytes(act + 0x44, minusOne);
            mem.WriteBytes(act + 0x48, minusOne);

            uint descriptor = s.Descriptor;
            if (descriptor != 0)
            {
                uint vtable = mem.ReadU32(descriptor) ?? 0;
                if (vtable != 0)
                {
                    int offset = mem.ReadI32(vtable + 8) ?? 0;
                    uint baseAddress = descriptor + (uint)offset;
                    uint first = mem.ReadU32(baseAddress + 4) ?? 0;
                    uint holder = first != 0
                        ? (mem.ReadU32(first + 4) ?? 0) : 0;
                    if (holder != 0)
                    {
                        mem.WriteBytes(holder + baseAddress + 0x130,
                            BitConverter.GetBytes(0));
                        mem.WriteBytes(holder + baseAddress + 0x158,
                            BitConverter.GetBytes(0u));
                    }
                }
            }

            if (s.NpcData != 0)
                mem.WriteBytes(s.NpcData + 0x26C, BitConverter.GetBytes(0u));

            message = "cleared action, status, loading, conversation flags, " +
                "refto, room target, walk target, and NPC target";
            return true;
        }

        public string Describe(CharacterState s)
        {
            if (s == null) return "(no state)";
            return "movement=" + s.MovementType + " (" + MovementName(s.MovementType) +
                ") mirror=" + s.MovementMirror +
                " action=" + s.ActionId +
                " room=" + s.Room +
                " targetNpc=" + s.TargetNpc.ToString("X8") +
                " walkTarget=" + s.WalkTargetChar.ToString("X8") +
                " stareTarget=" + s.StareTargetChar.ToString("X8") +
                " about=" + s.TargetActivity.ToString("X8") +
                " locTarget=" + s.Location +
                " mood=" + s.MoodAction +
                " conv=" + s.ConversationId +
                " wait=" + s.InstructionWait +
                " busy=" + s.AnimLocked + "/" + s.Loading + "/" + s.InConversation +
                " talking=" + s.Talking;
        }

        public static string MovementName(int value)
        {
            switch (value)
            {
                case 0: return "Still";
                case 1: return "Roaming";
                case 2: return "Go Location";
                case 3: return "Moving to Character";
                case 4: return "Follow";
                case 7: return "Talk";
                case 8: return "Minna";
                default: return "?";
            }
        }

        private static bool Cancelled(Func<bool> cancelled)
        {
            return cancelled != null && cancelled();
        }

        private static void Log(Action<string, PlannerLogLevel> log, string text,
            PlannerLogLevel level)
        {
            if (log != null) log(text, level);
        }
    }
}
