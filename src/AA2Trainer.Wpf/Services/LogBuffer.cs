using System;
using System.Collections.Generic;

namespace AA2Trainer.WpfPrototype
{
    /// <summary>
    /// Thread-safe staging buffer for log lines. The UI drains it in batches so
    /// a burst of planner or live events does not become one dispatcher and
    /// collection operation per line.
    /// </summary>
    public sealed class LogBuffer
    {
        private readonly object sync = new object();
        private readonly Queue<LogLine> pending = new Queue<LogLine>();
        private readonly int capacity;

        public LogBuffer(int capacity)
        {
            this.capacity = Math.Max(1, capacity);
        }

        public int Count
        {
            get
            {
                lock (sync) return pending.Count;
            }
        }

        public void Add(LogLine line)
        {
            if (line == null) return;
            lock (sync)
            {
                pending.Enqueue(line);
                while (pending.Count > capacity) pending.Dequeue();
            }
        }

        public void Clear()
        {
            lock (sync) pending.Clear();
        }

        public int Drain(List<LogLine> destination, int max)
        {
            if (destination == null || max <= 0) return 0;
            lock (sync)
            {
                int count = Math.Min(max, pending.Count);
                for (int i = 0; i < count; i++)
                    destination.Add(pending.Dequeue());
                return count;
            }
        }
    }
}
