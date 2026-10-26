using System;
using System.Drawing;
using System.Windows.Forms;

namespace AA2Trainer
{
    public sealed partial class MainForm
    {
        private ComboBox scheduleWeekday;
        private ComboBox scheduleWeek;
        private ComboBox schedulePeriod;
        private CheckBox scheduleFreeze;
        private NumericUpDown scheduleDuration;
        private NumericUpDown scheduleMinute;
        private NumericUpDown scheduleSecond;
        private TextBox scheduleAcademy;
        private NumericUpDown scheduleYear;
        private NumericUpDown scheduleClass;
        private Label scheduleSaveName;
        private Button scheduleApply;

        private Font scheduleFont;
        private ToolTip scheduleTips;
        private Timer scheduleFreezeTimer;
        private Timer scheduleClockTimer;
        private ScheduleSnapshot scheduleLoaded;
        private bool scheduleSuppress;
        private bool scheduleDirty;
        private bool scheduleApplying;
        private bool scheduleFreezeActive;
        private int scheduleFreezeFailures;

        private Control BuildScheduleRow()
        {
            TableLayoutPanel p = new TableLayoutPanel();
            p.Dock = DockStyle.Fill;
            p.Margin = new Padding(0);
            p.Padding = new Padding(0);
            p.ColumnCount = 21;
            p.RowCount = 1;
            p.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            scheduleFont = new Font("Segoe UI", 8.25F);
            p.Font = scheduleFont;

            p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92F));   // weekday
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64F));   // week
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112F));  // period
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 11F));   // |
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92F));   // freeze
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34F));   // Time
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44F));   // duration
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42F));   // minute
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 6F));    // :
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 42F));   // second
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 11F));   // |
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58F));   // Academy
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84F));   // academy value
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 32F));   // Year
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 38F));   // year value
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44F));   // Class
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 38F));   // class value
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 11F));   // |
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36F));   // Save:
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));   // save name
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 54F));   // Apply

            scheduleTips = new ToolTip();
            scheduleTips.AutoPopDelay = 10000;
            scheduleTips.InitialDelay = 350;
            scheduleTips.ReshowDelay = 100;
            scheduleTips.ShowAlways = true;

            scheduleWeekday = new ComboBox();
            scheduleWeekday.DropDownStyle = ComboBoxStyle.DropDownList;
            scheduleWeekday.Dock = DockStyle.Fill;
            scheduleWeekday.Margin = new Padding(2, 3, 2, 3);
            scheduleWeekday.MaxDropDownItems = 8;
            scheduleWeekday.DropDownWidth = 130;
            scheduleWeekday.Items.AddRange(ScheduleEditor.Weekdays);
            scheduleWeekday.SelectedIndexChanged += ScheduleEdited;
            scheduleTips.SetToolTip(scheduleWeekday, "Weekday");
            p.Controls.Add(scheduleWeekday, 0, 0);

            scheduleWeek = new ComboBox();
            scheduleWeek.DropDownStyle = ComboBoxStyle.DropDownList;
            scheduleWeek.Dock = DockStyle.Fill;
            scheduleWeek.Margin = new Padding(2, 3, 2, 3);
            scheduleWeek.MaxDropDownItems = 6;
            scheduleWeek.DropDownWidth = 100;
            for (int i = 1; i <= 6; i++)
                scheduleWeek.Items.Add(ScheduleEditor.WeekLabel(i));
            scheduleWeek.SelectedIndexChanged += ScheduleEdited;
            scheduleTips.SetToolTip(scheduleWeek, "Week (1-6)");
            p.Controls.Add(scheduleWeek, 1, 0);

            schedulePeriod = new ComboBox();
            schedulePeriod.DropDownStyle = ComboBoxStyle.DropDownList;
            schedulePeriod.Dock = DockStyle.Fill;
            schedulePeriod.Margin = new Padding(2, 3, 2, 3);
            schedulePeriod.MaxDropDownItems = 14;
            schedulePeriod.DropDownWidth = 190;
            schedulePeriod.Items.AddRange(ScheduleEditor.Periods);
            schedulePeriod.SelectedIndexChanged += ScheduleEdited;
            scheduleTips.SetToolTip(schedulePeriod, "Current period / phase");
            p.Controls.Add(schedulePeriod, 2, 0);

            p.Controls.Add(ScheduleSeparator(), 3, 0);

            scheduleFreeze = new CheckBox();
            scheduleFreeze.Text = "Freeze Time";
            scheduleFreeze.AutoSize = false;
            scheduleFreeze.Dock = DockStyle.Fill;
            scheduleFreeze.CheckAlign = ContentAlignment.MiddleLeft;
            scheduleFreeze.TextAlign = ContentAlignment.MiddleLeft;
            scheduleFreeze.Margin = new Padding(4, 0, 2, 0);
            scheduleFreeze.CheckedChanged += ScheduleFreezeChanged;
            scheduleTips.SetToolTip(scheduleFreeze,
                "Pin the current period time at the displayed minute:second.");
            p.Controls.Add(scheduleFreeze, 4, 0);

            Label timeCaption = ScheduleCaption("Time");
            timeCaption.Margin = new Padding(2, 0, 0, 0);
            scheduleTips.SetToolTip(timeCaption,
                "Current period timer. Minute:Second is the elapsed time in the current period.");
            p.Controls.Add(timeCaption, 5, 0);

            scheduleDuration = ScheduleNumber(ScheduleEditor.MinDuration,
                ScheduleEditor.MaxDuration);
            scheduleDuration.Margin = new Padding(2, 5, 2, 5);
            scheduleDuration.ValueChanged += ScheduleEdited;
            scheduleTips.SetToolTip(scheduleDuration,
                "Period Duration (minutes).\n" +
                "How long the selected period lasts before the game advances.");
            p.Controls.Add(scheduleDuration, 6, 0);

            scheduleMinute = ScheduleNumber(0, 999);
            scheduleMinute.Margin = new Padding(2, 5, 2, 5);
            scheduleMinute.ValueChanged += ScheduleEdited;
            scheduleTips.SetToolTip(scheduleMinute, "Time - minutes in the current period");
            p.Controls.Add(scheduleMinute, 7, 0);

            Label colon = ScheduleCaption(":");
            colon.TextAlign = ContentAlignment.MiddleCenter;
            colon.Margin = new Padding(0);
            p.Controls.Add(colon, 8, 0);

            scheduleSecond = ScheduleNumber(0, 59);
            scheduleSecond.Margin = new Padding(2, 5, 2, 5);
            scheduleSecond.ValueChanged += ScheduleEdited;
            scheduleTips.SetToolTip(scheduleSecond, "Time - seconds in the current period");
            p.Controls.Add(scheduleSecond, 9, 0);

            p.Controls.Add(ScheduleSeparator(), 10, 0);

            Label academyCaption = ScheduleCaption("Academy");
            p.Controls.Add(academyCaption, 11, 0);

            scheduleAcademy = new TextBox();
            scheduleAcademy.Dock = DockStyle.Fill;
            scheduleAcademy.Margin = new Padding(2, 5, 2, 5);
            scheduleAcademy.TextChanged += ScheduleEdited;
            scheduleTips.SetToolTip(scheduleAcademy,
                "Academy name. The game stores up to 63 Shift-JIS bytes.");
            p.Controls.Add(scheduleAcademy, 12, 0);

            Label yearCaption = ScheduleCaption("Year");
            p.Controls.Add(yearCaption, 13, 0);

            scheduleYear = ScheduleDigit();
            scheduleYear.Margin = new Padding(2, 5, 2, 5);
            scheduleYear.ValueChanged += ScheduleEdited;
            scheduleTips.SetToolTip(scheduleYear, "Year (single digit)");
            p.Controls.Add(scheduleYear, 14, 0);

            Label classCaption = ScheduleCaption("Class");
            p.Controls.Add(classCaption, 15, 0);

            scheduleClass = ScheduleDigit();
            scheduleClass.Margin = new Padding(2, 5, 2, 5);
            scheduleClass.ValueChanged += ScheduleEdited;
            scheduleTips.SetToolTip(scheduleClass, "Class (single digit)");
            p.Controls.Add(scheduleClass, 16, 0);

            p.Controls.Add(ScheduleSeparator(), 17, 0);

            Label saveCaption = ScheduleCaption("Save:");
            saveCaption.Margin = new Padding(2, 0, 0, 0);
            p.Controls.Add(saveCaption, 18, 0);

            scheduleSaveName = new Label();
            scheduleSaveName.AutoSize = false;
            scheduleSaveName.AutoEllipsis = true;
            scheduleSaveName.Dock = DockStyle.Fill;
            scheduleSaveName.TextAlign = ContentAlignment.MiddleLeft;
            scheduleSaveName.BorderStyle = BorderStyle.FixedSingle;
            scheduleSaveName.BackColor = SystemColors.Window;
            scheduleSaveName.ForeColor = Color.DimGray;
            scheduleSaveName.Margin = new Padding(2, 4, 2, 4);
            scheduleSaveName.Padding = new Padding(2, 0, 2, 0);
            scheduleSaveName.UseMnemonic = false;
            scheduleSaveName.Text = "-";
            p.Controls.Add(scheduleSaveName, 19, 0);

            scheduleApply = new Button();
            scheduleApply.Text = "Apply";
            scheduleApply.Dock = DockStyle.Fill;
            scheduleApply.Margin = new Padding(2, 3, 2, 3);
            scheduleApply.Click += ScheduleApply_Click;
            scheduleTips.SetToolTip(scheduleApply,
                "Write the changed schedule and class fields.");
            p.Controls.Add(scheduleApply, 20, 0);

            scheduleFreezeTimer = new Timer();
            scheduleFreezeTimer.Interval = 250;
            scheduleFreezeTimer.Tick += delegate { ScheduleFreezeTick(); };

            // The schedule/time header stays live even when the heavier Auto 1s
            // refresh is off.
            scheduleClockTimer = new Timer();
            scheduleClockTimer.Interval = 1000;
            scheduleClockTimer.Tick += delegate
            {
                if (mem.IsAttached) RefreshScheduleRow();
            };
            scheduleClockTimer.Start();

            UpdateScheduleGate();
            return p;
        }

        private static Label ScheduleCaption(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = false;
            label.AutoEllipsis = true;
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Margin = new Padding(2, 0, 2, 0);
            label.UseMnemonic = false;
            return label;
        }

        private static NumericUpDown ScheduleNumber(int minimum, int maximum)
        {
            NumericUpDown number = new NumericUpDown();
            number.Minimum = minimum;
            number.Maximum = maximum;
            number.DecimalPlaces = 0;
            number.ThousandsSeparator = false;
            number.TextAlign = HorizontalAlignment.Left;
            number.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            return number;
        }

        private static NumericUpDown ScheduleDigit()
        {
            NumericUpDown number = ScheduleNumber(0, 9);
            number.Value = 0;
            return number;
        }

        private static Panel ScheduleSeparator()
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.Margin = new Padding(5, 5, 5, 5);
            panel.BackColor = Color.FromArgb(205, 208, 214);
            return panel;
        }

        private void ScheduleEdited(object sender, EventArgs e)
        {
            if (scheduleSuppress || scheduleApplying) return;
            scheduleDirty = true;

            if (ReferenceEquals(sender, schedulePeriod))
                ReloadSelectedPeriodDuration();
        }

        private void ReloadSelectedPeriodDuration()
        {
            if (schedulePeriod == null || scheduleDuration == null) return;
            int period = schedulePeriod.SelectedIndex;
            if (period < 0) return;

            int minutes;
            string error;
            if (!ScheduleEditor.TryReadPeriodDuration(mem, ctx, period,
                out minutes, out error))
                return;

            scheduleSuppress = true;
            try
            {
                scheduleDuration.Value = Clamp(minutes,
                    (int)scheduleDuration.Minimum, (int)scheduleDuration.Maximum);
            }
            finally
            {
                scheduleSuppress = false;
            }
        }

        private void RefreshScheduleRow()
        {
            if (scheduleWeekday == null) return;
            if (!mem.IsAttached || ctx.GlobalRoot == 0)
            {
                ClearScheduleRow();
                return;
            }

            ScheduleSnapshot snapshot;
            string error;
            if (!ScheduleEditor.TryRead(mem, ctx, out snapshot, out error))
            {
                if (!scheduleDirty) UpdateScheduleGate();
                return;
            }

            scheduleSaveName.Text = snapshot.SaveName;
            scheduleTips.SetToolTip(scheduleSaveName, snapshot.SaveName);
            if (scheduleDirty || scheduleApplying || ScheduleComboOpen())
            {
                UpdateScheduleGate();
                return;
            }

            ApplyScheduleSnapshot(snapshot, !scheduleFreezeActive);
            scheduleLoaded = snapshot;
            UpdateScheduleGate();
        }

        private void ApplyScheduleSnapshot(ScheduleSnapshot snapshot, bool updateTime)
        {
            if (snapshot == null || scheduleWeekday == null) return;

            scheduleSuppress = true;
            try
            {
                scheduleWeekday.SelectedIndex = Clamp(snapshot.Weekday, 0,
                    scheduleWeekday.Items.Count - 1);
                scheduleWeek.SelectedIndex = Clamp(snapshot.Week - 1, 0,
                    scheduleWeek.Items.Count - 1);
                schedulePeriod.SelectedIndex = Clamp(snapshot.Period, 0,
                    schedulePeriod.Items.Count - 1);
                scheduleDuration.Value = Clamp(snapshot.PeriodDuration,
                    ScheduleEditor.MinDuration, ScheduleEditor.MaxDuration);
                if (updateTime)
                {
                    int seconds = Math.Min(59999,
                        Math.Max(0, snapshot.TimeMilliseconds / 1000));
                    scheduleMinute.Value = Math.Min(999, seconds / 60);
                    scheduleSecond.Value = seconds % 60;
                }
                scheduleAcademy.Text = snapshot.Academy ?? "";
                scheduleYear.Value = Clamp(snapshot.Year, 0, 9);
                scheduleClass.Value = Clamp(snapshot.ClassNumber, 0, 9);
                scheduleSaveName.Text = snapshot.SaveName ?? "";
                scheduleTips.SetToolTip(scheduleSaveName, snapshot.SaveName ?? "");
            }
            finally
            {
                scheduleSuppress = false;
            }
        }

        private void ClearScheduleRow()
        {
            scheduleDirty = false;
            scheduleLoaded = null;
            if (scheduleWeekday == null) return;

            scheduleSuppress = true;
            try
            {
                scheduleWeekday.SelectedIndex = -1;
                scheduleWeek.SelectedIndex = -1;
                schedulePeriod.SelectedIndex = -1;
                scheduleDuration.Value = 60;
                scheduleMinute.Value = 0;
                scheduleSecond.Value = 0;
                scheduleAcademy.Text = "";
                scheduleYear.Value = 0;
                scheduleClass.Value = 0;
                scheduleSaveName.Text = "-";
                scheduleTips.SetToolTip(scheduleSaveName, "");
            }
            finally
            {
                scheduleSuppress = false;
            }

            UpdateScheduleGate();
        }

        private void DiscardScheduleEdits()
        {
            scheduleDirty = false;
        }

        private ScheduleSnapshot ReadScheduleControls()
        {
            ScheduleSnapshot snapshot = new ScheduleSnapshot();
            snapshot.Weekday = scheduleWeekday.SelectedIndex < 0
                ? 0 : scheduleWeekday.SelectedIndex;
            snapshot.Week = scheduleWeek.SelectedIndex < 0
                ? 1 : scheduleWeek.SelectedIndex + 1;
            snapshot.GameDay = (snapshot.Week - 1) * 7 + snapshot.Weekday + 1;
            snapshot.Period = schedulePeriod.SelectedIndex < 0
                ? 0 : schedulePeriod.SelectedIndex;
            snapshot.PeriodDuration = (int)scheduleDuration.Value;
            int totalSeconds = (int)scheduleMinute.Value * 60 +
                (int)scheduleSecond.Value;
            snapshot.TimeMilliseconds = totalSeconds * 1000;
            if (scheduleLoaded != null &&
                totalSeconds == scheduleLoaded.TimeMilliseconds / 1000)
            {
                // Keep the sub-second part when the visible minute:second did
                // not change; otherwise editing the time would quietly round it.
                snapshot.TimeMilliseconds = scheduleLoaded.TimeMilliseconds;
            }
            snapshot.Academy = scheduleAcademy.Text.Trim();
            snapshot.Year = (int)scheduleYear.Value;
            snapshot.ClassNumber = (int)scheduleClass.Value;
            snapshot.SaveName = scheduleSaveName.Text;
            return snapshot;
        }

        private void ScheduleApply_Click(object sender, EventArgs e)
        {
            if (scheduleApplying) return;
            if (scheduleLoaded == null)
            {
                MessageBox.Show(this, "The schedule values are not loaded.",
                    "Apply schedule", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            RunHealthCheck();
            if (!writesAllowed || !mem.CanWrite)
            {
                MessageBox.Show(this,
                    "The write gate is locked. Wait for a clean pointer check and try again.",
                    "Apply schedule", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ScheduleSnapshot edited = ReadScheduleControls();
            bool wasFrozen = scheduleFreezeActive;
            if (wasFrozen && scheduleFreezeTimer != null) scheduleFreezeTimer.Stop();

            bool ok = false;
            string changed = "";
            string error = null;
            scheduleApplying = true;
            try
            {
                ok = ScheduleEditor.TryWrite(mem, ctx, scheduleLoaded, edited,
                    out changed, out error);
            }
            finally
            {
                scheduleApplying = false;
                if (wasFrozen && scheduleFreeze.Checked) scheduleFreezeTimer.Start();
            }

            if (!ok)
            {
                string message = string.IsNullOrEmpty(changed)
                    ? error
                    : "Fields written before the failure: " + changed +
                      "\r\n\r\n" + error;
                MessageBox.Show(this, message, "Apply schedule",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            scheduleDirty = false;
            RefreshScheduleRow();
            SetSummary();
            SetSummaryText(changed == "No changes."
                ? "No schedule/class changes."
                : "Applied: " + changed + ".");
        }

        private void ScheduleFreezeChanged(object sender, EventArgs e)
        {
            if (scheduleSuppress) return;

            if (!scheduleFreeze.Checked)
            {
                StopScheduleFreeze(false);
                RefreshScheduleRow();
                return;
            }

            if (scheduleLoaded == null)
            {
                StopScheduleFreeze(true);
                return;
            }

            // If the user has not edited anything, refresh from live memory
            // before reading the freeze target so a stale header does not
            // rewind time to an old value.
            if (!scheduleDirty) RefreshScheduleRow();

            RunHealthCheck();
            if (!mem.IsAttached || !writesAllowed || !mem.CanWrite)
            {
                StopScheduleFreeze(true);
                MessageBox.Show(this,
                    "The write gate is locked. Wait for a clean pointer check before freezing time.",
                    "Freeze time", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int milliseconds = ReadTimeMilliseconds(!scheduleDirty);
            string error = null;
            bool wrote;
            bool pinned = false;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                if (ScheduleEditor.TryPinTime(mem, ctx, milliseconds, out wrote, out error))
                {
                    pinned = true;
                    break;
                }
            }
            if (!pinned)
            {
                StopScheduleFreeze(true);
                MessageBox.Show(this, error, "Freeze time",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            scheduleFreezeFailures = 0;
            scheduleFreezeActive = true;
            scheduleFreeze.ForeColor = Color.Firebrick;
            scheduleTips.SetToolTip(scheduleFreeze,
                "Time is frozen at " + FormatTime(milliseconds) + ". Uncheck to resume.");
            SetSummaryText("Time frozen at " + FormatTime(milliseconds) + ".");
            scheduleFreezeTimer.Start();
        }

        private void ScheduleFreezeTick()
        {
            if (!scheduleFreezeActive) return;

            if (!mem.IsAttached || !writesAllowed || !mem.CanWrite)
            {
                StopScheduleFreeze(true);
                SetSummaryText("Freeze stopped: write gate locked.");
                return;
            }

            int milliseconds = ReadTimeMilliseconds(!scheduleDirty);
            string error;
            bool wrote;
            if (!ScheduleEditor.TryPinTime(mem, ctx, milliseconds, out wrote, out error))
            {
                // The game can advance the timer between our write and the
                // read-back check. A single miss is transient; only stop after
                // several consecutive failures.
                scheduleFreezeFailures++;
                if (scheduleFreezeFailures >= 3)
                {
                    StopScheduleFreeze(true);
                    SetSummaryText("Freeze stopped: " + error);
                }
                return;
            }
            scheduleFreezeFailures = 0;
        }

        private void StopScheduleFreeze(bool uncheck)
        {
            scheduleFreezeActive = false;
            scheduleFreezeFailures = 0;
            if (scheduleFreezeTimer != null) scheduleFreezeTimer.Stop();
            if (scheduleFreeze == null) return;

            scheduleFreeze.ForeColor = SystemColors.ControlText;
            scheduleTips.SetToolTip(scheduleFreeze,
                "Pin the current period time at the displayed minute:second.");
            if (uncheck && scheduleFreeze.Checked)
            {
                scheduleSuppress = true;
                try { scheduleFreeze.Checked = false; }
                finally { scheduleSuppress = false; }
            }
        }

        private int ReadTimeMilliseconds(bool preserveFraction)
        {
            int totalSeconds = (int)scheduleMinute.Value * 60 +
                (int)scheduleSecond.Value;
            if (totalSeconds < 0) return 0;
            if (totalSeconds > 59999) return ScheduleEditor.MaxTimeMilliseconds;

            if (preserveFraction && scheduleLoaded != null &&
                totalSeconds == scheduleLoaded.TimeMilliseconds / 1000)
            {
                return scheduleLoaded.TimeMilliseconds;
            }
            return totalSeconds * 1000;
        }

        private bool ScheduleComboOpen()
        {
            if (scheduleWeekday != null && scheduleWeekday.DroppedDown) return true;
            if (scheduleWeek != null && scheduleWeek.DroppedDown) return true;
            if (schedulePeriod != null && schedulePeriod.DroppedDown) return true;
            return false;
        }

        private void UpdateScheduleGate()
        {
            if (scheduleApply == null) return;

            bool hasData = scheduleLoaded != null;
            bool canWrite = mem.IsAttached && writesAllowed && mem.CanWrite;

            if (!canWrite && scheduleFreezeActive)
                StopScheduleFreeze(true);

            scheduleWeekday.Enabled = hasData;
            scheduleWeek.Enabled = hasData;
            schedulePeriod.Enabled = hasData;
            scheduleDuration.Enabled = hasData;
            scheduleMinute.Enabled = hasData;
            scheduleSecond.Enabled = hasData;
            scheduleAcademy.Enabled = hasData;
            scheduleYear.Enabled = hasData;
            scheduleClass.Enabled = hasData;
            scheduleFreeze.Enabled = hasData && canWrite;
            scheduleApply.Enabled = hasData && canWrite;

            string tip = canWrite
                ? "Write the changed schedule and class fields."
                : "The fail-safe write gate is locked until all pointer checks pass.";
            scheduleTips.SetToolTip(scheduleApply, tip);
        }

        private void DisposeScheduleUi()
        {
            StopScheduleFreeze(true);
            if (scheduleFreezeTimer != null)
            {
                scheduleFreezeTimer.Stop();
                scheduleFreezeTimer.Dispose();
                scheduleFreezeTimer = null;
            }
            if (scheduleClockTimer != null)
            {
                scheduleClockTimer.Stop();
                scheduleClockTimer.Dispose();
                scheduleClockTimer = null;
            }
            if (scheduleTips != null)
            {
                scheduleTips.Dispose();
                scheduleTips = null;
            }
            if (scheduleFont != null)
            {
                scheduleFont.Dispose();
                scheduleFont = null;
            }
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum) return minimum;
            if (value > maximum) return maximum;
            return value;
        }

        private static string FormatTime(int milliseconds)
        {
            if (milliseconds < 0) milliseconds = 0;
            int seconds = milliseconds / 1000;
            return (seconds / 60).ToString("D2") + ":" +
                   (seconds % 60).ToString("D2");
        }
    }
}
