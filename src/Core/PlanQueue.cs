using System;
using System.Collections.Generic;

namespace AA2Trainer
{
    /// <summary>
    /// Pure plan state: an ordered list of steps plus the cursor, repeat and
    /// shuffle rules. No game calls, so it can be tested on its own.
    ///
    /// Invariants:
    ///   - only one step is current at a time;
    ///   - <see cref="Shuffle"/> plays every step exactly once per cycle;
    ///   - <see cref="Repeat"/> rewinds into a new cycle after the last step;
    ///   - the list is only edited while the plan is stopped.
    /// </summary>
    public sealed class PlanQueue
    {
        public readonly List<PlanStep> Steps = new List<PlanStep>();

        /// <summary>Character pointer this plan belongs to.</summary>
        public uint Character;

        /// <summary>
        /// Roster seat (0..24). Plans are keyed by seat in the UI and the runner
        /// re-resolves the character pointer from it, so a scene/class reload
        /// (new pointers) does not orphan the plan.
        /// </summary>
        public int Seat = -1;

        public string CharacterName = "";
        public PlanMode Mode = PlanMode.Normal;
        public PlanTrigger Trigger = PlanTrigger.Actor;
        public bool Repeat;
        public bool Shuffle;

        public int Cycle { get; private set; }
        public bool Started { get; private set; }
        public bool Finished { get; private set; }
        public int Cursor { get; private set; }

        private readonly List<int> order = new List<int>();
        private readonly Random random;

        public PlanQueue()
        {
            random = new Random();
        }

        public PlanQueue(int seed)
        {
            random = new Random(seed);
        }

        public int Count { get { return Steps.Count; } }

        /// <summary>Number of steps that will actually run.</summary>
        public int EnabledCount
        {
            get
            {
                int count = 0;
                foreach (PlanStep step in Steps)
                    if (step != null && step.Enabled) count++;
                return count;
            }
        }

        /// <summary>Skipped steps in the current cycle.</summary>
        public int SkippedCount
        {
            get
            {
                int count = 0;
                foreach (PlanStep step in Steps)
                    if (step != null && step.State == PlanStepState.Skipped) count++;
                return count;
            }
        }

        public int CurrentIndex
        {
            get
            {
                if (!Started || Finished) return -1;
                if (Cursor < 0 || Cursor >= order.Count) return -1;
                return order[Cursor];
            }
        }

        public PlanStep Current
        {
            get
            {
                int index = CurrentIndex;
                return index < 0 ? null : Steps[index];
            }
        }

        /// <summary>Starts a new run from the first (or shuffled first) step.</summary>
        public bool Begin(out string message)
        {
            message = null;
            if (Steps.Count == 0)
            {
                message = "The plan has no steps.";
                return false;
            }

            for (int i = 0; i < Steps.Count; i++)
            {
                if (Steps[i] == null)
                {
                    Steps.RemoveAt(i);
                    i--;
                    continue;
                }
                Steps[i].Reset();
            }
            if (Steps.Count == 0)
            {
                message = "The plan has no usable steps.";
                return false;
            }
            if (EnabledCount == 0)
            {
                message = "Every step is disabled.";
                return false;
            }

            Cycle = 1;
            Cursor = 0;
            Started = true;
            Finished = false;
            BuildOrder();
            return true;
        }

        /// <summary>Marks the current step and moves to the next one.</summary>
        /// <returns>False when the plan has finished.</returns>
        public bool Advance(PlanStepState state, string note)
        {
            PlanStep step = Current;
            if (step != null)
            {
                step.State = state;
                step.Note = note ?? "";
            }

            if (!Started || Finished) return false;

            Cursor++;
            if (Cursor < order.Count) return true;

            if (!Repeat)
            {
                Finished = true;
                Cursor = order.Count;
                return false;
            }

            Cycle++;
            Cursor = 0;
            BuildOrder();
            foreach (PlanStep s in Steps)
                if (s != null) s.Reset();
            return order.Count > 0;
        }

        /// <summary>
        /// Advances past consecutive disabled steps, marking each one
        /// <see cref="PlanStepState.Skipped"/>. Returns the number skipped.
        /// The loop is bounded by the step count so a repeat cycle made only
        /// of disabled steps cannot spin forever.
        /// </summary>
        public int SkipDisabled()
        {
            int skipped = 0;
            for (int guard = 0; guard < Steps.Count; guard++)
            {
                PlanStep step = Current;
                if (step == null || step.Enabled) break;

                bool hasNext = Advance(PlanStepState.Skipped, "disabled");
                skipped++;
                if (!hasNext) break;
            }
            return skipped;
        }

        public void Stop()
        {
            Started = false;
            Finished = false;
            Cursor = 0;
            Cycle = 0;
            order.Clear();
        }

        /// <summary>
        /// Marks the plan finished normally (every step was performed). Unlike
        /// <see cref="Stop"/>, the steps and their final states are kept so the
        /// UI can still show them.
        /// </summary>
        public void Complete()
        {
            Started = false;
            Finished = true;
            Cursor = order.Count;
        }

        /// <summary>Current step's position in the original list (1-based).</summary>
        public int CurrentNumber
        {
            get
            {
                int index = CurrentIndex;
                return index < 0 ? 0 : index + 1;
            }
        }

        public string ProgressText()
        {
            if (Steps.Count == 0) return "no steps";
            if (Finished) return "finished";
            if (!Started) return "stopped";

            int skipped = SkippedCount;
            string suffix = "";
            if (Cycle > 0 || skipped > 0)
            {
                suffix = " (";
                if (Cycle > 0)
                {
                    suffix += "cycle " + Cycle;
                    if (skipped > 0) suffix += ", ";
                }
                if (skipped > 0) suffix += skipped + " skipped";
                suffix += ")";
            }
            return "step " + CurrentNumber + "/" + Steps.Count + suffix;
        }

        public void Add(PlanStep step)
        {
            if (step != null) Steps.Add(step);
        }

        public bool RemoveAt(int index)
        {
            if (index < 0 || index >= Steps.Count) return false;
            Steps.RemoveAt(index);
            return true;
        }

        public bool MoveUp(int index)
        {
            if (index <= 0 || index >= Steps.Count) return false;
            PlanStep step = Steps[index];
            Steps.RemoveAt(index);
            Steps.Insert(index - 1, step);
            return true;
        }

        public bool MoveDown(int index)
        {
            if (index < 0 || index >= Steps.Count - 1) return false;
            PlanStep step = Steps[index];
            Steps.RemoveAt(index);
            Steps.Insert(index + 1, step);
            return true;
        }

        public bool MoveTo(int from, int to)
        {
            if (from < 0 || from >= Steps.Count) return false;
            if (to < 0 || to >= Steps.Count) return false;
            if (from == to) return false;
            PlanStep step = Steps[from];
            Steps.RemoveAt(from);
            Steps.Insert(to, step);
            return true;
        }

        public void ClearSteps()
        {
            Steps.Clear();
            order.Clear();
            Started = false;
            Finished = false;
            Cursor = 0;
            Cycle = 0;
        }

        private void BuildOrder()
        {
            order.Clear();
            for (int i = 0; i < Steps.Count; i++)
                order.Add(i);

            if (!Shuffle || Steps.Count < 2) return;

            // Fisher-Yates.
            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                int tmp = order[i];
                order[i] = order[j];
                order[j] = tmp;
            }

            // Do not repeat the previous cycle's exact order.
            if (previousOrder.Count == Steps.Count && order.Count > 2)
            {
                bool same = true;
                for (int i = 0; i < order.Count; i++)
                {
                    if (order[i] != lastOrderAt(i)) { same = false; break; }
                }
                if (same)
                {
                    int tmp = order[0];
                    order[0] = order[order.Count - 1];
                    order[order.Count - 1] = tmp;
                }
            }

            previousOrder.Clear();
            previousOrder.AddRange(order);
        }

        private readonly List<int> previousOrder = new List<int>();

        private int lastOrderAt(int index)
        {
            return index < previousOrder.Count ? previousOrder[index] : -1;
        }
    }
}
