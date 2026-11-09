using System;

namespace AA2Trainer
{
    /// <summary>
    /// How a plan step is handed to the game.
    /// </summary>
    public enum PlanMode
    {
        /// <summary>
        /// Passive. The character keeps choosing what to do; the step only runs
        /// when the character itself decides to interact with the step target.
        /// </summary>
        Normal,

        /// <summary>Arm the step immediately and let the planner consume it.</summary>
        Forced,

        /// <summary>Like Forced, but skips the walk-in / delay phase.</summary>
        ForcedPerform,

        /// <summary>Cancel whatever the character is doing, then run the step.</summary>
        Ignore
    }

    /// <summary>Who must start the interaction for Normal mode to fire.</summary>
    public enum PlanTrigger
    {
        /// <summary>The planned character walks to the target (measured TAA2 default).</summary>
        Actor,

        /// <summary>The target walks to the planned character.</summary>
        Target,

        /// <summary>Either side may start it.</summary>
        Either
    }

    public enum PlanStepState
    {
        Pending,
        Running,
        Done,
        Failed,
        Skipped
    }

    /// <summary>
    /// One step of a plan. Carries both the game parameters and the character
    /// pointers, so activity pointers can be re-resolved when the step is armed
    /// (they change on scene/class reloads).
    /// </summary>
    public sealed class PlanStep
    {
        public ActionParams Params;
        public string Label = "";

        /// <summary>Target character pointer, or 0 for a self/room step.</summary>
        public uint TargetCharacter;

        /// <summary>Target roster seat (used to re-resolve the pointer).</summary>
        public int TargetSeat = -1;

        /// <summary>About character pointer (Target2), or 0.</summary>
        public uint AboutCharacter;

        /// <summary>About roster seat (used to re-resolve the pointer).</summary>
        public int AboutSeat = -1;

        public PlanStepState State = PlanStepState.Pending;
        public int Attempts;
        public string Note = "";

        public PlanStep()
        {
            Params = new ActionParams();
        }

        public PlanStep(ActionParams parameters, string label)
        {
            Params = parameters ?? new ActionParams();
            Label = label ?? "";
        }

        public bool HasTarget
        {
            get { return TargetCharacter != 0 || (Params != null && Params.Target1 != 0); }
        }

        public string Describe()
        {
            string text = string.IsNullOrEmpty(Label)
                ? "(step)" : Label;
            if (Params == null) return text;
            text += " - " + Params.Describe();
            return text;
        }

        public string StatusText()
        {
            switch (State)
            {
                case PlanStepState.Running: return "running";
                case PlanStepState.Done: return "done";
                case PlanStepState.Failed: return "failed";
                case PlanStepState.Skipped: return "skipped";
                default: return Attempts > 0 ? "retry " + Attempts : "pending";
            }
        }

        public void Reset()
        {
            State = PlanStepState.Pending;
            Attempts = 0;
            Note = "";
        }

        public PlanStep Clone()
        {
            PlanStep copy = new PlanStep();
            copy.Params = Params == null ? null : Params.Clone();
            copy.Label = Label;
            copy.TargetCharacter = TargetCharacter;
            copy.TargetSeat = TargetSeat;
            copy.AboutCharacter = AboutCharacter;
            copy.AboutSeat = AboutSeat;
            copy.State = State;
            copy.Attempts = Attempts;
            copy.Note = Note;
            return copy;
        }
    }
}
