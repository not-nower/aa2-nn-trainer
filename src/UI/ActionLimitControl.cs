using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace AA2Trainer
{
    /// <summary>
    /// Gameplay -> Limits. A three-column checked list of the game's actions:
    /// checked actions are cancelled as soon as an NPC starts them. The list
    /// persists in the portable AA2Trainer.ini beside the exe.
    /// </summary>
    public sealed class ActionLimitControl : UserControl
    {
        private const int ActionColumnCount = 3;
        private const int ThreeColumnThreshold = 1500;

        private readonly ActionLimitList limits;
        private readonly ActionLimiter limiter;
        private readonly Timer statusTimer;
        private readonly List<ActionChoice> allChoices = new List<ActionChoice>();

        private TableLayoutPanel actionGrid;
        private TableLayoutPanel scrollLayout;
        private Panel canvasHost;
        private VScrollBar scrollBar;
        private CheckedListBox[] actionColumns;
        private int activeColumnCount;
        private const int ScrollBarWidth = 17;
        private TextBox filterBox;
        private CheckBox enableCheck;
        private CheckBox applyPcCheck;
        private Label statusLabel;
        private NameMap names;
        private bool populated;
        private bool suppressChecks;

        public ActionLimitControl()
        {
            limits = new ActionLimitList();
            limiter = new ActionLimiter(limits, null);
            limits.Changed += delegate { UpdateStatus(); };

            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(248, 249, 251);
            Padding = new Padding(8, 6, 8, 6);
            BuildUi();

            statusTimer = new Timer();
            statusTimer.Interval = 500;
            statusTimer.Tick += delegate { UpdateStatus(); };
            statusTimer.Start();

            UpdateStatus();
        }

        public void SetTarget(GameMemory mem, GameContext ctx, NameMap nameMap,
            bool writeAllowed)
        {
            NameMap previousNames = names;
            names = nameMap;
            limiter.SetTarget(mem, ctx);
            limiter.WriteAllowed = writeAllowed;

            if (!ReferenceEquals(previousNames, nameMap))
            {
                populated = false;
                EnsureChoices();
                ApplyFilter();
            }
            UpdateStatus();
        }

        public void SetWriteAllowed(bool allowed)
        {
            limiter.WriteAllowed = allowed;
            UpdateStatus();
        }

        public void Clear()
        {
            limiter.Enabled = false;
            limiter.SetTarget(null, null);
            if (enableCheck != null) enableCheck.Checked = false;
            UpdateStatus();
        }

        private void BuildUi()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 4;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));

            FlowLayoutPanel top = new FlowLayoutPanel();
            top.Dock = DockStyle.Fill;
            top.FlowDirection = FlowDirection.LeftToRight;
            top.WrapContents = false;
            top.Padding = new Padding(0, 4, 0, 0);

            enableCheck = new CheckBox();
            enableCheck.Text = "Enable limit actions";
            enableCheck.AutoSize = true;
            enableCheck.Margin = new Padding(0, 4, 18, 0);
            enableCheck.CheckedChanged += delegate
            {
                limiter.Enabled = enableCheck.Checked;
                UpdateStatus();
            };
            top.Controls.Add(enableCheck);

            applyPcCheck = new CheckBox();
            applyPcCheck.Text = "Apply to PC";
            applyPcCheck.AutoSize = true;
            applyPcCheck.Margin = new Padding(0, 4, 18, 0);
            applyPcCheck.CheckedChanged += delegate
            {
                limiter.ApplyToPc = applyPcCheck.Checked;
            };
            top.Controls.Add(applyPcCheck);

            Label filterCaption = new Label();
            filterCaption.Text = "Filter:";
            filterCaption.AutoSize = true;
            filterCaption.Margin = new Padding(0, 8, 4, 0);
            filterCaption.ForeColor = Color.DimGray;
            top.Controls.Add(filterCaption);

            filterBox = new TextBox();
            filterBox.Width = 220;
            filterBox.Margin = new Padding(0, 4, 12, 0);
            filterBox.TextChanged += delegate { ApplyFilter(); };
            top.Controls.Add(filterBox);

            Button clear = new Button();
            clear.Text = "Clear all";
            clear.AutoSize = true;
            clear.Margin = new Padding(0, 2, 0, 0);
            clear.Click += delegate
            {
                limits.Clear();
                ApplyFilter();
            };
            top.Controls.Add(clear);

            Label warning = new Label();
            warning.Dock = DockStyle.Fill;
            warning.ForeColor = Color.FromArgb(176, 92, 0);
            warning.Text = "Warning: stopping an action can leave the NPC running in place, " +
                           "or looking briefly stuck, while the AI picks a different action. " +
                           "Already-started conversations are not interrupted. " +
                           "This is expected.";
            warning.TextAlign = ContentAlignment.MiddleLeft;
            warning.Padding = new Padding(2, 0, 0, 0);

            scrollLayout = new TableLayoutPanel();
            scrollLayout.Dock = DockStyle.Fill;
            scrollLayout.ColumnCount = 2;
            scrollLayout.RowCount = 1;
            scrollLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            scrollLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, ScrollBarWidth));
            scrollLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            canvasHost = new Panel();
            canvasHost.Dock = DockStyle.Fill;
            canvasHost.AutoScroll = false;
            canvasHost.BackColor = Color.FromArgb(248, 249, 251);
            canvasHost.SizeChanged += delegate { UpdateScrollLayout(); };

            actionGrid = new TableLayoutPanel();
            actionGrid.ColumnCount = ActionColumnCount;
            actionGrid.RowCount = 1;
            actionGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            actionGrid.Location = new Point(0, 0);
            actionGrid.BackColor = Color.FromArgb(248, 249, 251);
            canvasHost.Controls.Add(actionGrid);

            actionColumns = new CheckedListBox[ActionColumnCount];
            for (int i = 0; i < ActionColumnCount; i++)
            {
                CheckedListBox list = new CheckedListBox();
                list.Dock = DockStyle.Fill;
                list.CheckOnClick = true;
                list.IntegralHeight = false;
                list.HorizontalScrollbar = false;
                list.ScrollAlwaysVisible = false;
                list.BorderStyle = BorderStyle.None;
                list.Margin = new Padding(i == 0 ? 0 : 2, 0,
                    i == ActionColumnCount - 1 ? 0 : 2, 0);
                list.ItemCheck += OnItemCheck;
                list.MouseWheel += OnListMouseWheel;
                actionColumns[i] = list;
                actionGrid.Controls.Add(list, i, 0);
            }

            scrollBar = new VScrollBar();
            scrollBar.Dock = DockStyle.Fill;
            scrollBar.Scroll += delegate { UpdateScrollPosition(); };
            scrollBar.Enabled = false;

            scrollLayout.Controls.Add(canvasHost, 0, 0);
            scrollLayout.Controls.Add(scrollBar, 1, 0);
            ConfigureColumns(2);

            statusLabel = new Label();
            statusLabel.Dock = DockStyle.Fill;
            statusLabel.ForeColor = Color.DimGray;
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;

            root.Controls.Add(top, 0, 0);
            root.Controls.Add(warning, 0, 1);
            root.Controls.Add(scrollLayout, 0, 2);
            root.Controls.Add(statusLabel, 0, 3);
            Controls.Add(root);
        }

        private void ConfigureColumns(int count)
        {
            if (actionGrid == null || actionColumns == null) return;
            for (int i = 0; i < actionColumns.Length; i++)
                if (actionColumns[i] == null) return;
            if (count < 2) count = 2;
            if (count > ActionColumnCount) count = ActionColumnCount;
            activeColumnCount = count;

            actionGrid.ColumnStyles.Clear();
            for (int i = 0; i < ActionColumnCount; i++)
            {
                bool visible = i < count;
                actionGrid.ColumnStyles.Add(visible
                    ? new ColumnStyle(SizeType.Percent, 100F / count)
                    : new ColumnStyle(SizeType.Absolute, 0F));
                actionColumns[i].Visible = visible;
            }
        }

        private void UpdateScrollLayout()
        {
            if (actionGrid == null || actionColumns == null || canvasHost == null ||
                scrollBar == null) return;
            for (int i = 0; i < actionColumns.Length; i++)
                if (actionColumns[i] == null) return;

            int viewportW = canvasHost.ClientSize.Width;
            int viewportH = canvasHost.ClientSize.Height;
            if (viewportW <= 0 || viewportH <= 0) return;

            int desired = viewportW >= ThreeColumnThreshold ? 3 : 2;
            if (desired != activeColumnCount) ConfigureColumns(desired);

            int itemHeight = actionColumns[0].ItemHeight;
            if (itemHeight <= 0) itemHeight = 16;

            int maxItems = 0;
            for (int i = 0; i < activeColumnCount; i++)
                maxItems = Math.Max(maxItems, actionColumns[i].Items.Count);

            // One extra item of padding keeps the lists from growing their own
            // internal scrollbars when the content nearly fills the viewport.
            int contentH = maxItems * itemHeight + itemHeight + 4;
            int canvasH = Math.Max(viewportH, contentH);

            actionGrid.Left = 0;
            actionGrid.Width = Math.Max(1, viewportW);
            actionGrid.Height = canvasH;

            int maxScroll = Math.Max(0, canvasH - viewportH);
            scrollBar.Minimum = 0;
            scrollBar.Maximum = maxScroll + viewportH - 1;
            scrollBar.LargeChange = Math.Max(1, viewportH);
            scrollBar.SmallChange = Math.Max(1, itemHeight);
            scrollBar.Enabled = maxScroll > 0;

            if (scrollBar.Value > maxScroll) scrollBar.Value = maxScroll;
            if (maxScroll == 0) scrollBar.Value = 0;
            actionGrid.Top = -scrollBar.Value;
        }

        private void UpdateScrollPosition()
        {
            if (actionGrid != null && scrollBar != null)
                actionGrid.Top = -scrollBar.Value;
        }

        private void OnListMouseWheel(object sender, MouseEventArgs e)
        {
            if (scrollBar == null || !scrollBar.Enabled) return;
            int itemHeight = actionColumns != null && actionColumns[0] != null
                ? Math.Max(1, actionColumns[0].ItemHeight) : 16;
            int delta = -(e.Delta / 120) * itemHeight * 3;
            int max = scrollBar.Maximum - scrollBar.LargeChange + 1;
            if (max < scrollBar.Minimum) max = scrollBar.Minimum;
            int value = scrollBar.Value + delta;
            if (value < scrollBar.Minimum) value = scrollBar.Minimum;
            if (value > max) value = max;
            scrollBar.Value = value;
            if (actionGrid != null) actionGrid.Top = -value;
        }

        private void EnsureChoices()
        {
            if (populated || names == null) return;
            allChoices.Clear();
            for (int id = 0; id < 1000; id++)
            {
                string name = names.Get("ACTION_KEY", id);
                if (string.IsNullOrEmpty(name)) continue;
                allChoices.Add(new ActionChoice(id,
                    id.ToString("D3") + "  " + name));
            }
            populated = true;
        }

        private void ApplyFilter()
        {
            if (actionColumns == null) return;
            string filter = filterBox == null ? "" : filterBox.Text.Trim();
            filter = filter.ToLowerInvariant();

            List<ActionChoice> filtered = new List<ActionChoice>();
            for (int i = 0; i < allChoices.Count; i++)
            {
                ActionChoice choice = allChoices[i];
                if (filter.Length > 0 && choice.Text.ToLowerInvariant()
                    .IndexOf(filter, StringComparison.Ordinal) < 0)
                    continue;
                filtered.Add(choice);
            }

            int columns = Math.Max(2, activeColumnCount);
            int perColumn = filtered.Count == 0
                ? 0 : (filtered.Count + columns - 1) / columns;

            suppressChecks = true;
            try
            {
                for (int c = 0; c < actionColumns.Length; c++)
                    actionColumns[c].BeginUpdate();
                for (int c = 0; c < actionColumns.Length; c++)
                    actionColumns[c].Items.Clear();

                for (int i = 0; i < filtered.Count; i++)
                {
                    int column = perColumn == 0
                        ? 0 : Math.Min(columns - 1, i / perColumn);
                    ActionChoice choice = filtered[i];
                    actionColumns[column].Items.Add(choice,
                        limits.Contains(choice.Id));
                }
            }
            finally
            {
                for (int c = 0; c < actionColumns.Length; c++)
                    actionColumns[c].EndUpdate();
                suppressChecks = false;
            }
            UpdateScrollLayout();
            UpdateStatus();
        }

        private void OnItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (suppressChecks) return;
            CheckedListBox list = sender as CheckedListBox;
            if (list == null || e.Index < 0 || e.Index >= list.Items.Count) return;
            ActionChoice choice = list.Items[e.Index] as ActionChoice;
            if (choice == null) return;

            if (e.NewValue == CheckState.Checked) limits.Add(choice.Id);
            else limits.Remove(choice.Id);
        }

        private void UpdateStatus()
        {
            if (statusLabel == null) return;
            int count = limits.Count;
            if (!limiter.Enabled)
            {
                statusLabel.Text = "Limit actions are off. " + count +
                    " action(s) checked.";
                return;
            }
            if (!string.IsNullOrEmpty(limiter.LastError))
            {
                statusLabel.Text = "Limit actions are on; last cancel failed: " +
                    limiter.LastError;
                return;
            }
            string last = limiter.LastBlocked;
            statusLabel.Text = "Limit actions are on. Blocked " + limiter.BlockedCount +
                (string.IsNullOrEmpty(last) ? "" : "; last: " + last) + ".";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (statusTimer != null)
                {
                    statusTimer.Stop();
                    statusTimer.Dispose();
                }
                limiter.Dispose();
            }
            base.Dispose(disposing);
        }

        private sealed class ActionChoice
        {
            public readonly int Id;
            public readonly string Text;

            public ActionChoice(int id, string text)
            {
                Id = id;
                Text = text;
            }

            public override string ToString()
            {
                return Text;
            }
        }
    }
}
