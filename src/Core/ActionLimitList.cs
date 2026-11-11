using System;
using System.Collections.Generic;

namespace AA2Trainer
{
    /// <summary>
    /// Persistent set of action IDs that NPCs are not allowed to perform.
    /// Stored in the portable AA2Trainer.ini beside the exe.
    /// </summary>
    public sealed class ActionLimitList
    {
        private readonly object gate = new object();
        private readonly HashSet<int> ids = new HashSet<int>();
        private readonly string settingsKey;

        public event Action Changed;

        public ActionLimitList() : this(null)
        {
        }

        public ActionLimitList(string settingsKey)
        {
            this.settingsKey = settingsKey;
            Load();
        }

        public bool Contains(int actionId)
        {
            lock (gate) return ids.Contains(actionId);
        }

        public int[] Snapshot()
        {
            lock (gate)
            {
                int[] result = new int[ids.Count];
                ids.CopyTo(result);
                Array.Sort(result);
                return result;
            }
        }

        public int Count
        {
            get { lock (gate) return ids.Count; }
        }

        public void Add(int actionId)
        {
            bool changed;
            lock (gate) changed = ids.Add(actionId);
            if (!changed) return;
            Save();
            RaiseChanged();
        }

        public void Remove(int actionId)
        {
            bool changed;
            lock (gate) changed = ids.Remove(actionId);
            if (!changed) return;
            Save();
            RaiseChanged();
        }

        public void Clear()
        {
            bool changed;
            lock (gate)
            {
                changed = ids.Count > 0;
                ids.Clear();
            }
            if (!changed) return;
            Save();
            RaiseChanged();
        }

        private void RaiseChanged()
        {
            Action handler = Changed;
            if (handler != null) handler();
        }

        private void Load()
        {
            foreach (int id in TrainerSettings.LoadLimitActions(settingsKey))
                ids.Add(id);
        }

        private void Save()
        {
            TrainerSettings.SaveLimitActions(settingsKey, Snapshot());
        }
    }
}
