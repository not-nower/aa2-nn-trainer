using System.Collections.Generic;

namespace AA2Trainer.WpfPrototype
{
    public sealed class PlannerSeatOption
    {
        public PlannerSeatOption(int seat, string text)
        {
            Seat = seat;
            Text = text ?? "";
        }

        public int Seat { get; private set; }
        public string Text { get; private set; }

        public override string ToString()
        {
            return Text;
        }
    }

    /// <summary>
    /// UI wrapper for a Core PlanStep. The DataGrid edits Enabled and the
    /// planner service refreshes Description/State/Note from the live step.
    /// </summary>
    public sealed class PlannerStepItem : ObservableObject
    {
        private bool enabled;
        private int number;

        public PlannerStepItem(PlanStep step, int number)
        {
            Step = step;
            this.number = number;
            enabled = step != null && step.Enabled;
            Refresh();
        }

        public PlanStep Step { get; private set; }
        public string Description { get; private set; } = "";
        public string State { get; private set; } = "";
        public string Note { get; private set; } = "";

        public int Number
        {
            get { return number; }
            set { Set(ref number, value); }
        }

        public bool Enabled
        {
            get { return enabled; }
            set
            {
                if (!Set(ref enabled, value)) return;
                if (Step != null) Step.Enabled = value;
            }
        }

        public void Refresh()
        {
            if (Step == null) return;
            Description = string.IsNullOrEmpty(Step.Label)
                ? Step.Describe() : Step.Label;
            State = Step.StatusText();
            Note = Step.Note ?? "";
            OnPropertyChanged(nameof(Description));
            OnPropertyChanged(nameof(State));
            OnPropertyChanged(nameof(Note));
            if (enabled != Step.Enabled)
            {
                enabled = Step.Enabled;
                OnPropertyChanged(nameof(Enabled));
            }
        }
    }

    public sealed class PlannerSnapshot
    {
        public int Seat = -1;
        public bool Running;
        public string Status = "Plan: not running";
        public string HookState = "Hook: not installed";
        public bool HookInstalled;
        public bool ObserveOnly;
        public PlanMode Mode = PlanMode.Normal;
        public PlanTrigger Trigger = PlanTrigger.Actor;
        public bool Repeat;
        public bool Shuffle;
        public readonly List<PlannerStepItem> Steps = new List<PlannerStepItem>();
    }
}
