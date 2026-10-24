using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace AA2Trainer
{
    public sealed class TraitsControl : UserControl
    {
        public event EventHandler CharacterSaved;

        // Set by the main form. Gives the control a chance to re-run the
        // fail-safe health check immediately before a write.
        public Func<bool> ConfirmWriteAllowed { get; set; }

        private const int TraitColumns = 3;
        private const int PreferenceColumns = 2;
        private const int RowHeight = 21;
        private const int RiskHeaderHeight = 20;
        private const int GroupOverhead = 46;

        private GameMemory mem;
        private CharacterInfo character;
        private uint loadedPointer;
        private bool[] loaded;
        private int[,] riskLoaded;
        private bool gateAllowed;
        private bool applying;

        private readonly CheckBox[] boxes;
        private ComboBox[,] riskCombos;
        private Panel contentHost;
        private Label gateLabel;
        private Label countLabel;
        private Label statusLabel;
        private ToolTip tooltip;

        public TraitsControl()
        {
            DoubleBuffered = true;
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(248, 249, 251);
            boxes = new CheckBox[TraitCatalog.All.Count];
            BuildUi();
        }

        private void BuildUi()
        {
            tooltip = new ToolTip();
            tooltip.AutoPopDelay = 30000;
            tooltip.InitialDelay = 350;

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 3;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
            root.Padding = new Padding(16, 8, 16, 8);
            Controls.Add(root);

            contentHost = new Panel();
            contentHost.Dock = DockStyle.Fill;
            contentHost.AutoScroll = true;
            contentHost.BackColor = Color.FromArgb(248, 249, 251);
            contentHost.Margin = new Padding(0);
            root.Controls.Add(contentHost, 0, 0);

            int personalityCount = 0;
            int preferenceCount = 0;
            int rainbowCount = 0;
            for (int i = 0; i < TraitCatalog.All.Count; i++)
            {
                int offset = TraitCatalog.All[i].Offset;
                if (offset == TraitCatalog.RainbowCardOffset)
                    rainbowCount++;
                else if (offset >= TraitCatalog.PreferenceOffset)
                    preferenceCount++;
                else
                    personalityCount++;
            }

            int personalityRows = (personalityCount + TraitColumns - 1) / TraitColumns;
            int preferenceRows = (preferenceCount + PreferenceColumns - 1) / PreferenceColumns;
            if (personalityRows < 1) personalityRows = 1;
            if (preferenceRows < 1) preferenceRows = 1;

            int personalityHeight = personalityRows * RowHeight + GroupOverhead;
            int preferenceHeight = preferenceRows * RowHeight + GroupOverhead;
            int extraHeight = rainbowCount > 0 ? RowHeight + GroupOverhead : 0;
            int riskHeight = RiskHeaderHeight + (RiskSchedule.DayCount * RowHeight) + GroupOverhead;

            int leftHeight = personalityHeight + (rainbowCount > 0 ? extraHeight + 6 : 0);
            int rightHeight = preferenceHeight + 6 + riskHeight;
            int boxHeight = Math.Max(leftHeight, rightHeight);

            TableLayoutPanel columns = new TableLayoutPanel();
            columns.Dock = DockStyle.Top;
            columns.Height = boxHeight;
            columns.ColumnCount = 2;
            columns.RowCount = 1;
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 61F));
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 39F));
            columns.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            columns.Margin = new Padding(0);
            columns.Padding = new Padding(0);
            contentHost.Controls.Add(columns);

            TableLayoutPanel leftStack = new TableLayoutPanel();
            leftStack.Dock = DockStyle.Fill;
            leftStack.ColumnCount = 1;
            leftStack.RowCount = 4;
            leftStack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            leftStack.RowStyles.Add(new RowStyle(SizeType.Absolute, personalityHeight));
            leftStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 6F));
            leftStack.RowStyles.Add(new RowStyle(SizeType.Absolute, extraHeight));
            leftStack.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            leftStack.Margin = new Padding(0, 0, 6, 0);
            leftStack.Padding = new Padding(0);
            columns.Controls.Add(leftStack, 0, 0);

            TableLayoutPanel personalityGrid;
            GroupBox personalityGroup = BuildGroup("Traits", TraitColumns, personalityRows,
                new Padding(0), out personalityGrid);
            leftStack.Controls.Add(personalityGroup, 0, 0);

            TableLayoutPanel rainbowGrid = null;
            if (rainbowCount > 0)
            {
                GroupBox extraGroup = BuildGroup("Extra", 1, rainbowCount,
                    new Padding(0), out rainbowGrid);
                leftStack.Controls.Add(extraGroup, 0, 2);
            }

            TableLayoutPanel rightStack = new TableLayoutPanel();
            rightStack.Dock = DockStyle.Fill;
            rightStack.ColumnCount = 1;
            rightStack.RowCount = 4;
            rightStack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            rightStack.RowStyles.Add(new RowStyle(SizeType.Absolute, preferenceHeight));
            rightStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 6F));
            rightStack.RowStyles.Add(new RowStyle(SizeType.Absolute, riskHeight));
            rightStack.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            rightStack.Margin = new Padding(6, 0, 0, 0);
            rightStack.Padding = new Padding(0);
            columns.Controls.Add(rightStack, 1, 0);

            TableLayoutPanel preferenceGrid;
            GroupBox preferenceGroup = BuildGroup("H Preferences", PreferenceColumns, preferenceRows,
                new Padding(0), out preferenceGrid);
            rightStack.Controls.Add(preferenceGroup, 0, 0);

            TableLayoutPanel riskGrid;
            GroupBox riskGroup = BuildRiskGroup(out riskGrid);
            rightStack.Controls.Add(riskGroup, 0, 2);

            int personalitySlot = 0;
            int preferenceSlot = 0;
            int rainbowSlot = 0;
            for (int i = 0; i < boxes.Length; i++)
            {
                TraitDef trait = TraitCatalog.All[i];
                int columnCount;
                TableLayoutPanel grid;
                int slot;
                if (trait.Offset == TraitCatalog.RainbowCardOffset && rainbowGrid != null)
                {
                    columnCount = 1;
                    grid = rainbowGrid;
                    slot = rainbowSlot++;
                }
                else if (trait.Offset >= TraitCatalog.PreferenceOffset)
                {
                    columnCount = PreferenceColumns;
                    grid = preferenceGrid;
                    slot = preferenceSlot++;
                }
                else
                {
                    columnCount = TraitColumns;
                    grid = personalityGrid;
                    slot = personalitySlot++;
                }

                CheckBox box = new CheckBox();
                box.Text = trait.Display;
                box.Tag = i;
                box.Dock = DockStyle.Fill;
                box.AutoSize = false;
                box.TextAlign = ContentAlignment.MiddleLeft;
                box.Margin = new Padding(2, 0, 4, 0);
                box.CheckedChanged += Trait_CheckedChanged;
                string tip = !string.IsNullOrEmpty(trait.Effect)
                    ? WrapTooltip(trait.Effect, 82)
                    : trait.Display + "   (char + 0x" + trait.Offset.ToString("X") + ")";
                tooltip.SetToolTip(box, tip);

                grid.Controls.Add(box, slot % columnCount, slot / columnCount);
                boxes[i] = box;
            }

            TableLayoutPanel actions = new TableLayoutPanel();
            actions.Dock = DockStyle.Fill;
            actions.ColumnCount = 2;
            actions.RowCount = 1;
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190F));
            actions.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            actions.Margin = new Padding(0);
            root.Controls.Add(actions, 0, 1);

            gateLabel = new Label();
            gateLabel.AutoSize = true;
            gateLabel.Margin = new Padding(14, 10, 0, 0);
            gateLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            gateLabel.Text = "Write gate: locked";
            gateLabel.ForeColor = Color.Firebrick;
            actions.Controls.Add(gateLabel, 0, 0);

            countLabel = new Label();
            countLabel.Dock = DockStyle.Fill;
            countLabel.TextAlign = ContentAlignment.MiddleRight;
            countLabel.ForeColor = Color.DimGray;
            countLabel.Margin = new Padding(0, 0, 14, 0);
            countLabel.Text = "0 / " + boxes.Length.ToString() + " active";
            actions.Controls.Add(countLabel, 1, 0);

            statusLabel = new Label();
            statusLabel.Dock = DockStyle.Fill;
            statusLabel.AutoSize = false;
            statusLabel.AutoEllipsis = true;
            statusLabel.TextAlign = ContentAlignment.TopLeft;
            statusLabel.ForeColor = Color.DimGray;
            statusLabel.Margin = new Padding(0);
            root.Controls.Add(statusLabel, 0, 2);

            tooltip.SetToolTip(gateLabel,
                "Traits are written only when the known game build and all pointer checks pass.");

            UpdateCount();
            UpdateGate();
        }

        private GroupBox BuildGroup(string title, int columnCount, int rowCount,
            Padding margin, out TableLayoutPanel grid)
        {
            GroupBox group = new GroupBox();
            group.Text = title;
            group.Dock = DockStyle.Fill;
            group.Padding = new Padding(12, 6, 12, 6);
            group.Margin = margin;

            grid = new TableLayoutPanel();
            grid.Dock = DockStyle.Fill;
            grid.ColumnCount = columnCount;
            grid.RowCount = rowCount + 1;
            grid.Margin = new Padding(0);
            grid.Padding = new Padding(0);
            for (int c = 0; c < columnCount; c++)
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / columnCount));
            for (int r = 0; r < rowCount; r++)
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, RowHeight));
            // Absorb leftover height so the checkbox rows keep their spacing.
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            group.Controls.Add(grid);

            return group;
        }

        private GroupBox BuildRiskGroup(out TableLayoutPanel grid)
        {
            GroupBox group = new GroupBox();
            group.Text = "Risk";
            group.Dock = DockStyle.Fill;
            group.Padding = new Padding(12, 6, 12, 6);
            group.Margin = new Padding(0);

            grid = new TableLayoutPanel();
            grid.Dock = DockStyle.Fill;
            grid.ColumnCount = 3;
            grid.RowCount = RiskSchedule.DayCount + 2;
            grid.Margin = new Padding(0);
            grid.Padding = new Padding(0);
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72F));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, RiskHeaderHeight));
            for (int day = 0; day < RiskSchedule.DayCount; day++)
                grid.RowStyles.Add(new RowStyle(SizeType.Absolute, RowHeight));
            // Absorb leftover height so the weekday rows keep their spacing.
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            for (int week = 0; week < RiskSchedule.WeekCount; week++)
            {
                Label header = new Label();
                header.Text = RiskSchedule.WeekNames[week];
                header.Dock = DockStyle.Fill;
                header.TextAlign = ContentAlignment.MiddleCenter;
                header.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
                header.ForeColor = Color.DimGray;
                header.Margin = new Padding(0);
                grid.Controls.Add(header, week + 1, 0);
            }

            riskCombos = new ComboBox[RiskSchedule.WeekCount, RiskSchedule.DayCount];
            for (int day = 0; day < RiskSchedule.DayCount; day++)
            {
                Label dayLabel = new Label();
                dayLabel.Text = RiskSchedule.Days[day];
                dayLabel.Dock = DockStyle.Fill;
                dayLabel.TextAlign = ContentAlignment.MiddleLeft;
                dayLabel.Margin = new Padding(0);
                grid.Controls.Add(dayLabel, 0, day + 1);

                for (int week = 0; week < RiskSchedule.WeekCount; week++)
                {
                    ComboBox combo = new ComboBox();
                    combo.DropDownStyle = ComboBoxStyle.DropDownList;
                    combo.Dock = DockStyle.Fill;
                    combo.Margin = new Padding(2, 0, 2, 0);
                    combo.Font = new Font("Segoe UI", 9F);
                    combo.FormattingEnabled = true;
                    combo.Format += Risk_Format;
                    combo.Items.Add(0);
                    combo.Items.Add(1);
                    combo.Items.Add(2);
                    combo.Tag = new int[] { week, day };
                    combo.SelectedIndexChanged += Risk_Changed;
                    tooltip.SetToolTip(combo, "Risk " + RiskSchedule.Days[day] + ", " +
                        RiskSchedule.WeekNames[week].ToLowerInvariant() + "   (char + 0x" +
                        RiskSchedule.OffsetOf(week, day).ToString("X") + ")");

                    grid.Controls.Add(combo, week + 1, day + 1);
                    riskCombos[week, day] = combo;
                }
            }

            group.Controls.Add(grid);
            return group;
        }

        public void SetTarget(GameMemory memory, CharacterInfo info, bool writeAllowed)
        {
            mem = memory;
            gateAllowed = writeAllowed;

            if (info == null)
            {
                Clear();
                return;
            }

            bool sameCharacter = info.Pointer == loadedPointer && loadedPointer != 0;
            character = info;

            bool[] values;
            string error;
            if (TraitCatalog.TryRead(mem, info, out values, out error))
            {
                bool changed = !sameCharacter || loaded == null || !ValuesEqual(values, loaded);
                if (changed)
                {
                    ApplyValues(values);
                    loaded = values;
                    if (!sameCharacter)
                    {
                        SetStatus("Loaded " + CountActive(values) +
                            " active traits from live memory.", false);
                    }
                }
                loadedPointer = info.Pointer;
            }
            else
            {
                loaded = null;
                loadedPointer = info.Pointer;
                SetStatus(error, true);
            }

            int[,] risks;
            if (RiskSchedule.TryRead(mem, info, out risks, out error))
            {
                bool riskChanged = !sameCharacter || riskLoaded == null ||
                    !RiskValuesEqual(risks, riskLoaded);
                if (riskChanged)
                {
                    ApplyRiskValues(risks);
                    riskLoaded = risks;
                }
            }
            else
            {
                riskLoaded = null;
                SetStatus(error, true);
            }

            UpdateGate();
        }

        public void SetWriteAllowed(bool allowed)
        {
            gateAllowed = allowed;
            UpdateGate();
        }

        public void Clear()
        {
            mem = null;
            character = null;
            loadedPointer = 0;
            loaded = null;
            riskLoaded = null;
            gateAllowed = false;

            ApplyValues(null);
            ApplyRiskValues(null);
            SetStatus("Attach to the game and select a character.", false);
            UpdateGate();
        }

        private void Trait_CheckedChanged(object sender, EventArgs e)
        {
            if (applying) return;

            CheckBox box = sender as CheckBox;
            if (box == null) return;

            int index = (int)box.Tag;
            bool desired = box.Checked;

            if (mem == null || character == null || !mem.IsAttached)
            {
                RestoreBox(box, index);
                SetStatus("The game is not attached.", true);
                return;
            }

            if (ConfirmWriteAllowed != null && !ConfirmWriteAllowed())
            {
                RestoreBox(box, index);
                SetStatus("Write gate is locked. Wait for a clean pointer check and try again.", true);
                UpdateGate();
                return;
            }

            if (!mem.CanWrite)
            {
                RestoreBox(box, index);
                SetStatus(string.IsNullOrEmpty(mem.WriteError)
                    ? "The game handle is read-only."
                    : mem.WriteError, true);
                UpdateGate();
                return;
            }

            string error;
            if (!TraitCatalog.TryWrite(mem, character, index, desired, out error))
            {
                RestoreBox(box, index);
                SetStatus(error, true);
                UpdateGate();
                return;
            }

            if (loaded != null) loaded[index] = desired;

            string name = TraitCatalog.All[index].Display;
            SetStatus(name + (desired ? " enabled." : " disabled."), false);
            UpdateCount();
            if (CharacterSaved != null) CharacterSaved(this, EventArgs.Empty);
        }

        private void Risk_Format(object sender, ListControlConvertEventArgs e)
        {
            if (e.ListItem == null) return;
            e.Value = RiskSchedule.LevelName(Convert.ToInt32(e.ListItem));
        }

        // Tooltips do not wrap long lines, so break the effect text manually.
        private static string WrapTooltip(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text)) return "";

            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd();
                if (line.Length <= maxChars)
                {
                    sb.AppendLine(line);
                    continue;
                }

                string indent = line.StartsWith("- ") ? "  " : "";
                string[] words = line.Split(' ');
                string current = "";
                for (int w = 0; w < words.Length; w++)
                {
                    string word = words[w];
                    if (current.Length == 0)
                    {
                        current = word;
                    }
                    else if (current.Length + 1 + word.Length <= maxChars)
                    {
                        current += " " + word;
                    }
                    else
                    {
                        sb.AppendLine(current);
                        current = indent + word;
                    }
                }
                if (current.Length > 0) sb.AppendLine(current);
            }
            return sb.ToString().TrimEnd();
        }

        private void Risk_Changed(object sender, EventArgs e)
        {
            if (applying) return;

            ComboBox combo = sender as ComboBox;
            if (combo == null) return;

            int[] tag = combo.Tag as int[];
            if (tag == null || tag.Length != 2) return;
            int week = tag[0];
            int day = tag[1];

            if (mem == null || character == null || !mem.IsAttached)
            {
                RestoreRisk(combo, week, day);
                SetStatus("The game is not attached.", true);
                return;
            }

            if (ConfirmWriteAllowed != null && !ConfirmWriteAllowed())
            {
                RestoreRisk(combo, week, day);
                SetStatus("Write gate is locked. Wait for a clean pointer check and try again.", true);
                UpdateGate();
                return;
            }

            if (!mem.CanWrite)
            {
                RestoreRisk(combo, week, day);
                SetStatus(string.IsNullOrEmpty(mem.WriteError)
                    ? "The game handle is read-only."
                    : mem.WriteError, true);
                UpdateGate();
                return;
            }

            int value = combo.SelectedIndex >= 0
                ? Convert.ToInt32(combo.Items[combo.SelectedIndex])
                : 0;

            string error;
            if (!RiskSchedule.TryWrite(mem, character, week, day, value, out error))
            {
                RestoreRisk(combo, week, day);
                SetStatus(error, true);
                UpdateGate();
                return;
            }

            if (riskLoaded != null) riskLoaded[week, day] = value;
            SetStatus(RiskSchedule.Days[day] + ", " +
                RiskSchedule.WeekNames[week].ToLowerInvariant() +
                " set to " + RiskSchedule.LevelName(value) + ".", false);
            if (CharacterSaved != null) CharacterSaved(this, EventArgs.Empty);
        }

        private void RestoreRisk(ComboBox combo, int week, int day)
        {
            applying = true;
            try
            {
                int value = riskLoaded != null ? riskLoaded[week, day] : 0;
                SelectRiskValue(combo, value);
            }
            finally
            {
                applying = false;
            }
        }

        private void ApplyRiskValues(int[,] values)
        {
            if (riskCombos == null) return;

            applying = true;
            try
            {
                for (int week = 0; week < RiskSchedule.WeekCount; week++)
                {
                    for (int day = 0; day < RiskSchedule.DayCount; day++)
                    {
                        int value = values != null ? values[week, day] : 0;
                        SelectRiskValue(riskCombos[week, day], value);
                    }
                }
            }
            finally
            {
                applying = false;
            }
        }

        private static void SelectRiskValue(ComboBox combo, int value)
        {
            int index = combo.Items.IndexOf(value);
            if (index < 0)
            {
                combo.Items.Add(value);
                index = combo.Items.Count - 1;
            }
            combo.SelectedIndex = index;
        }

        private static bool RiskValuesEqual(int[,] a, int[,] b)
        {
            if (a == null || b == null) return false;
            if (a.GetLength(0) != b.GetLength(0) ||
                a.GetLength(1) != b.GetLength(1)) return false;
            for (int week = 0; week < a.GetLength(0); week++)
            {
                for (int day = 0; day < a.GetLength(1); day++)
                {
                    if (a[week, day] != b[week, day]) return false;
                }
            }
            return true;
        }

        private void RestoreBox(CheckBox box, int index)
        {
            applying = true;
            try
            {
                box.Checked = loaded != null && index >= 0 && index < loaded.Length
                    ? loaded[index]
                    : !box.Checked;
            }
            finally
            {
                applying = false;
            }
        }

        private void ApplyValues(bool[] values)
        {
            applying = true;
            try
            {
                for (int i = 0; i < boxes.Length; i++)
                {
                    bool state = values != null && i < values.Length && values[i];
                    if (boxes[i].Checked != state) boxes[i].Checked = state;
                }
            }
            finally
            {
                applying = false;
            }
            UpdateCount();
        }

        private static bool ValuesEqual(bool[] a, bool[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        private static int CountActive(bool[] values)
        {
            int count = 0;
            if (values == null) return count;
            for (int i = 0; i < values.Length; i++)
                if (values[i]) count++;
            return count;
        }

        private int CountActive()
        {
            int count = 0;
            for (int i = 0; i < boxes.Length; i++)
                if (boxes[i].Checked) count++;
            return count;
        }

        private void UpdateCount()
        {
            if (countLabel == null) return;
            countLabel.Text = CountActive().ToString() + " / " +
                boxes.Length.ToString() + " active";
        }

        private void UpdateGate()
        {
            bool attached = mem != null && mem.IsAttached;
            bool hasCharacter = attached && character != null;
            bool writable = gateAllowed && hasCharacter && mem.CanWrite;

            for (int i = 0; i < boxes.Length; i++)
                boxes[i].Enabled = writable;

            if (riskCombos != null)
            {
                for (int week = 0; week < RiskSchedule.WeekCount; week++)
                {
                    for (int day = 0; day < RiskSchedule.DayCount; day++)
                        riskCombos[week, day].Enabled = writable;
                }
            }

            if (gateLabel == null) return;
            if (!hasCharacter)
            {
                gateLabel.Text = "Write gate: locked";
                gateLabel.ForeColor = Color.Firebrick;
                tooltip.SetToolTip(gateLabel, "Select a character to edit.");
            }
            else if (!gateAllowed)
            {
                gateLabel.Text = "Write gate: locked";
                gateLabel.ForeColor = Color.Firebrick;
                tooltip.SetToolTip(gateLabel,
                    "Edits are written only when the known game build and all pointer checks pass.");
            }
            else if (attached && !mem.CanWrite)
            {
                gateLabel.Text = "Write gate: read-only handle";
                gateLabel.ForeColor = Color.Firebrick;
                tooltip.SetToolTip(gateLabel,
                    string.IsNullOrEmpty(mem.WriteError)
                        ? "The process handle was opened without write access."
                        : mem.WriteError);
            }
            else
            {
                gateLabel.Text = "Write gate: open";
                gateLabel.ForeColor = Color.ForestGreen;
                tooltip.SetToolTip(gateLabel,
                    "Trait changes are written immediately. Each write is re-verified.");
            }
        }

        private void SetStatus(string text, bool error)
        {
            if (statusLabel == null) return;
            statusLabel.Text = text ?? "";
            statusLabel.ForeColor = error ? Color.Firebrick : Color.DimGray;
        }
    }
}
