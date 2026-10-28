using System;
using System.Threading;
using System.Windows.Forms;

namespace AA2Trainer
{
    public sealed partial class MainForm
    {
        private const int LiveActiveIntervalMs = 100;
        private const int LiveIdleIntervalMs = 500;
        private const int LiveLingerMs = 1000;

        private System.Threading.Timer liveTimer;
        private int liveBusy;
        private int liveUiQueued;
        private LiveSnapshot livePending;
        private LiveSnapshot liveAppliedSnapshot;
        private long liveAppliedSequence;
        private long liveGeneration;
        private volatile bool liveEnabled = true;
        private volatile bool liveAuto = true;
        private volatile int liveIntervalMs = 250;
        private DateTime liveLastActiveUtc = DateTime.MinValue;
        private volatile bool livePlannerActive;
        private bool liveWasMinimized;

        /// <summary>
        /// The planner can force fast polling even while no live tab is visible.
        /// Kept here so Force Action / Action Planner can reuse this loop later.
        /// </summary>
        public bool PlannerActive
        {
            get { return livePlannerActive; }
            set
            {
                if (livePlannerActive == value) return;
                livePlannerActive = value;
                UpdateLiveState();
            }
        }

        private void ApplyLiveSettings()
        {
            if (optionsControl == null) return;
            SetLiveSettings(optionsControl.LiveRefreshEnabled,
                optionsControl.LiveIntervalAuto,
                optionsControl.LiveIntervalMs);
        }

        private void SetLiveSettings(bool enabled, bool auto, int intervalMs)
        {
            bool wasEnabled = liveEnabled;
            liveEnabled = enabled;
            liveAuto = auto;
            liveIntervalMs = Math.Max(50, intervalMs);

            if (refreshTimer != null) refreshTimer.Enabled = enabled;

            if (!enabled)
            {
                StopLiveReader();
                return;
            }

            if (!wasEnabled && mem.IsAttached)
                FullRefresh();

            UpdateLiveState();
        }

        /// <summary>
        /// Starts or stops the background poller based on the visible tab.
        /// A detached, minimized, or hidden trainer does not poll unless a
        /// planner action has claimed the loop.
        /// </summary>
        private void UpdateLiveState()
        {
            bool wanted = liveEnabled && mem.IsAttached &&
                WindowState != FormWindowState.Minimized &&
                (livePlannerActive || IsLiveTabActive());
            if (!wanted)
            {
                StopLiveReader();
                return;
            }
            StartLiveReader();
        }

        private bool IsLiveTabActive()
        {
            if (tabs == null) return false;
            if (tabs.SelectedIndex == 0) return true;
            if (tabs.SelectedIndex == 2 && characterEditorTabs != null &&
                characterEditorTabs.SelectedIndex == 0)
                return true;
            return false;
        }

        private void StartLiveReader()
        {
            if (!liveEnabled || liveReader == null || !mem.IsAttached) return;
            if (WindowState == FormWindowState.Minimized) return;

            if (liveTimer == null)
                liveTimer = new System.Threading.Timer(LiveTick, null,
                    Timeout.Infinite, Timeout.Infinite);

            try { liveTimer.Change(0, Timeout.Infinite); }
            catch (ObjectDisposedException) { }
        }

        private void StopLiveReader()
        {
            System.Threading.Timer timer = liveTimer;
            liveTimer = null;
            if (timer == null) return;

            // Dispose(WaitHandle) signals after the callback has returned, so a
            // detach cannot close the process handle underneath an active read.
            using (ManualResetEvent done = new ManualResetEvent(false))
            {
                try
                {
                    if (timer.Dispose(done))
                        done.WaitOne(1000);
                }
                catch (ObjectDisposedException) { }
            }
        }

        private void ClearLiveState()
        {
            liveEnabled = false;
            StopLiveReader();
            livePending = null;
            liveAppliedSnapshot = null;
            liveAppliedSequence = 0;
            Interlocked.Exchange(ref liveUiQueued, 0);
            if (liveReader != null)
            {
                liveReader.Clear();
                liveGeneration = liveReader.Generation;
            }
        }

        private void LiveTick(object state)
        {
            if (!liveEnabled || !mem.IsAttached) return;
            if (Interlocked.Exchange(ref liveBusy, 1) == 1) return;

            int next = LiveIdleIntervalMs;
            try
            {
                LiveSnapshot snapshot = liveReader == null ? null : liveReader.Read();
                if (snapshot != null)
                {
                    Volatile.Write(ref livePending, snapshot);
                    if (Interlocked.Exchange(ref liveUiQueued, 1) == 0)
                    {
                        try { BeginInvoke((Action)ApplyLiveSnapshot); }
                        catch (InvalidOperationException)
                        {
                            Interlocked.Exchange(ref liveUiQueued, 0);
                        }
                    }
                    next = ChooseLiveInterval(snapshot);
                }
            }
            catch
            {
                // A failed read is not fatal. The full 1 s loop will re-resolve
                // the context and the next tick will retry.
            }
            finally
            {
                Interlocked.Exchange(ref liveBusy, 0);

                System.Threading.Timer timer = liveTimer;
                if (timer != null && liveEnabled && !IsDisposed)
                {
                    try { timer.Change(next, Timeout.Infinite); }
                    catch (ObjectDisposedException) { }
                }
            }
        }

        private int ChooseLiveInterval(LiveSnapshot snapshot)
        {
            if (!liveAuto) return Math.Max(50, liveIntervalMs);

            bool active = livePlannerActive;
            if (!active && snapshot != null)
            {
                foreach (LiveCharacterState character in snapshot.Characters)
                {
                    if (character == null) continue;
                    if (character.IsBusy || character.MovementType > 1)
                    {
                        active = true;
                        break;
                    }
                }
            }

            DateTime now = DateTime.UtcNow;
            if (active)
            {
                liveLastActiveUtc = now;
                return LiveActiveIntervalMs;
            }

            // Stay fast for a moment after the last activity so a chained
            // action is not sampled at the slow idle rate.
            if ((now - liveLastActiveUtc).TotalMilliseconds < LiveLingerMs)
                return LiveActiveIntervalMs;

            return LiveIdleIntervalMs;
        }

        private void ApplyLiveSnapshot()
        {
            try
            {
                LiveSnapshot snapshot = Volatile.Read(ref livePending);
                Volatile.Write(ref livePending, null);

                if (snapshot == null || !liveEnabled || !mem.IsAttached) return;
                if (WindowState == FormWindowState.Minimized) return;
                if (snapshot.Generation != liveGeneration) return;
                if (snapshot.Sequence <= liveAppliedSequence) return;

                liveAppliedSequence = snapshot.Sequence;
                liveAppliedSnapshot = snapshot;

                if (tabs == null) return;
                if (tabs.SelectedIndex == 0)
                {
                    if (roster != null)
                        roster.ApplyLiveSnapshot(snapshot, names);
                }
                else if (tabs.SelectedIndex == 2 && characterEditorTabs != null &&
                         characterEditorTabs.SelectedIndex == 0 && characterData != null)
                {
                    characterData.ApplyLiveStatus(snapshot.SelectedStatus);
                }
            }
            catch
            {
                // The full refresh remains authoritative.
            }
            finally
            {
                Interlocked.Exchange(ref liveUiQueued, 0);
            }
        }

        private void PublishLiveRoster()
        {
            if (liveReader == null) return;
            if (!mem.IsAttached || ctx.Selected == null)
            {
                liveReader.Clear();
                liveGeneration = liveReader.Generation;
                return;
            }
            liveGeneration = liveReader.PublishRoster(ctx.Characters, ctx.Selected);
        }

        private void HandleMinimizeState()
        {
            bool minimized = WindowState == FormWindowState.Minimized;
            if (minimized)
            {
                liveWasMinimized = true;
                StopLiveReader();
                return;
            }

            if (!liveWasMinimized) return;
            liveWasMinimized = false;
            if (!liveEnabled || !mem.IsAttached) return;

            FullRefresh();
            UpdateLiveState();
        }
    }
}
