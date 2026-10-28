using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace AA2Trainer
{
    /// <summary>
    /// Read-only promise view for the selected character. Dates, meetings and
    /// lewd/test rewards are read from the live promise block and shown in both
    /// directions. No values are written from this tab.
    /// </summary>
    public sealed class PromisesControl : UserControl
    {
        private GameMemory mem;
        private GameContext ctx;
        private CharacterInfo target;
        private NameMap names;
        private string lastSignature = "";

        public event EventHandler<CharacterSelectedEventArgs> OpenCharacterRequested;

        private readonly List<PromiseEntry> entries = new List<PromiseEntry>();

        private Label targetLabel;
        private Label countLabel;
        private Label statusLabel;
        private Label emptyLabel;
        private ComboBox directionCombo;
        private ComboBox typeCombo;
        private TextBox searchBox;
        private DataGridView grid;
        private bool resettingColumns;

        public PromisesControl()
        {
            DoubleBuffered = true;
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(248, 249, 251);
            Padding = new Padding(4, 2, 4, 4);
            BuildUi();
        }

        private void BuildUi()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 4;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
            root.Margin = new Padding(0);
            Controls.Add(root);

            root.Controls.Add(BuildHeader(), 0, 0);
            root.Controls.Add(BuildFilterRow(), 0, 1);

            grid = BuildTable();
            root.Controls.Add(grid, 0, 2);

            emptyLabel = new Label();
            emptyLabel.Dock = DockStyle.Fill;
            emptyLabel.TextAlign = ContentAlignment.MiddleCenter;
            emptyLabel.Font = new Font("Segoe UI", 10F);
            emptyLabel.ForeColor = Color.Gray;
            emptyLabel.BackColor = Color.White;
            emptyLabel.Visible = false;
            emptyLabel.Text = "No active promises for this character.";
            root.Controls.Add(emptyLabel, 0, 2);

            statusLabel = new Label();
            statusLabel.Dock = DockStyle.Fill;
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            statusLabel.AutoEllipsis = true;
            statusLabel.Font = new Font("Segoe UI", 8F);
            statusLabel.ForeColor = Color.DimGray;
            statusLabel.Margin = new Padding(0);
            statusLabel.Text = "Read-only promise view; values refresh live.";
            root.Controls.Add(statusLabel, 0, 3);

            UpdateHeader();
            UpdateEmptyState();
        }

        private Control BuildHeader()
        {
            TableLayoutPanel header = new TableLayoutPanel();
            header.Dock = DockStyle.Fill;
            header.ColumnCount = 2;
            header.RowCount = 1;
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            header.Margin = new Padding(0);

            targetLabel = new Label();
            targetLabel.Dock = DockStyle.Fill;
            targetLabel.TextAlign = ContentAlignment.MiddleLeft;
            targetLabel.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            targetLabel.ForeColor = Color.FromArgb(40, 44, 52);
            targetLabel.AutoEllipsis = true;
            targetLabel.Text = "Promises";
            header.Controls.Add(targetLabel, 0, 0);

            countLabel = new Label();
            countLabel.AutoSize = true;
            countLabel.TextAlign = ContentAlignment.MiddleRight;
            countLabel.ForeColor = Color.DimGray;
            countLabel.Margin = new Padding(6, 7, 6, 0);
            countLabel.Text = "Active: 0";
            header.Controls.Add(countLabel, 1, 0);

            return header;
        }

        private Control BuildFilterRow()
        {
            FlowLayoutPanel row = new FlowLayoutPanel();
            row.Dock = DockStyle.Fill;
            row.WrapContents = false;
            row.FlowDirection = FlowDirection.LeftToRight;
            row.Padding = new Padding(0, 3, 0, 0);
            row.Margin = new Padding(0);

            Label directionLabel = new Label();
            directionLabel.Text = "Direction";
            directionLabel.AutoSize = true;
            directionLabel.Margin = new Padding(0, 5, 4, 0);
            row.Controls.Add(directionLabel);

            directionCombo = new ComboBox();
            directionCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            directionCombo.Width = 90;
            directionCombo.Margin = new Padding(0, 2, 10, 0);
            directionCombo.Items.AddRange(new object[] { "All", "In", "Out" });
            directionCombo.SelectedIndex = 0;
            directionCombo.SelectedIndexChanged += delegate { ApplyFilter(); };
            row.Controls.Add(directionCombo);

            Label typeLabel = new Label();
            typeLabel.Text = "Type";
            typeLabel.AutoSize = true;
            typeLabel.Margin = new Padding(0, 5, 4, 0);
            row.Controls.Add(typeLabel);

            typeCombo = new ComboBox();
            typeCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            typeCombo.Width = 100;
            typeCombo.Margin = new Padding(0, 2, 10, 0);
            typeCombo.Items.AddRange(new object[]
            {
                "All", "Date", "Meet", "Lewd"
            });
            typeCombo.SelectedIndex = 0;
            typeCombo.SelectedIndexChanged += delegate { ApplyFilter(); };
            row.Controls.Add(typeCombo);

            Label searchLabel = new Label();
            searchLabel.Text = "Search";
            searchLabel.AutoSize = true;
            searchLabel.Margin = new Padding(0, 5, 4, 0);
            row.Controls.Add(searchLabel);

            searchBox = new TextBox();
            searchBox.Width = 200;
            searchBox.Margin = new Padding(0, 2, 0, 0);
            searchBox.TextChanged += delegate { ApplyFilter(); };
            row.Controls.Add(searchBox);

            return row;
        }

        private DataGridView BuildTable()
        {
            DataGridView value = new DataGridView();
            value.Dock = DockStyle.Fill;
            value.BackgroundColor = Color.White;
            value.BorderStyle = BorderStyle.FixedSingle;
            value.RowHeadersVisible = false;
            value.AllowUserToAddRows = false;
            value.AllowUserToDeleteRows = false;
            value.AllowUserToResizeRows = false;
            value.ReadOnly = true;
            value.MultiSelect = false;
            value.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            value.AutoGenerateColumns = false;
            value.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            value.ColumnHeadersHeight = 26;
            value.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            value.RowTemplate.Height = 23;
            value.EnableHeadersVisualStyles = false;
            value.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(240, 241, 244);
            value.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(240, 241, 244);
            value.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.FromArgb(30, 30, 30);
            value.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            value.DefaultCellStyle.Font = new Font("Segoe UI", 9F);
            value.DefaultCellStyle.SelectionBackColor = SystemColors.Highlight;
            value.DefaultCellStyle.SelectionForeColor = SystemColors.HighlightText;
            value.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            value.Margin = new Padding(0);
            value.AccessibleName = "Active promises";
            TryDoubleBuffer(value);

            AddColumn(value, "Direction", 9F, 70, true);
            AddColumn(value, "Classmate", 34F, 160, false);
            AddColumn(value, "Type", 12F, 70, true);
            AddColumn(value, "Detail", 30F, 160, false);
            AddColumn(value, "State", 15F, 95, true);

            value.CellDoubleClick += Grid_CellDoubleClick;
            value.CellToolTipTextNeeded += Grid_CellToolTipTextNeeded;
            value.Resize += delegate { ResetColumnFill(value); };
            value.VisibleChanged += delegate
            {
                if (value.Visible) ResetColumnFill(value);
            };
            value.DataError += delegate(object sender, DataGridViewDataErrorEventArgs e)
            {
                e.ThrowException = false;
            };

            return value;
        }

        private static void AddColumn(DataGridView grid, string header, float weight,
            int minimumWidth, bool centered)
        {
            DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn();
            column.HeaderText = header;
            column.FillWeight = weight;
            column.MinimumWidth = minimumWidth;
            column.SortMode = DataGridViewColumnSortMode.NotSortable;
            column.ReadOnly = true;
            if (centered)
            {
                column.DefaultCellStyle.Alignment =
                    DataGridViewContentAlignment.MiddleCenter;
                column.HeaderCell.Style.Alignment =
                    DataGridViewContentAlignment.MiddleCenter;
            }
            grid.Columns.Add(column);
        }

        private void ResetColumnFill(DataGridView value)
        {
            if (resettingColumns || value == null || value.Columns.Count == 0)
                return;

            resettingColumns = true;
            try
            {
                value.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
                value.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            }
            finally
            {
                resettingColumns = false;
            }
        }

        private static void TryDoubleBuffer(DataGridView value)
        {
            try
            {
                PropertyInfo property = typeof(DataGridView).GetProperty(
                    "DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
                if (property != null)
                    property.SetValue(value, true, null);
            }
            catch
            {
            }
        }

        public void SetTarget(GameMemory memory, GameContext context,
            CharacterInfo selected, NameMap nameMap)
        {
            if (memory == null || context == null || selected == null ||
                !memory.IsAttached)
            {
                Clear();
                return;
            }

            bool targetChanged = target == null || target.Pointer != selected.Pointer;
            mem = memory;
            ctx = context;
            target = selected;
            names = nameMap;
            if (targetChanged) lastSignature = "";

            RefreshTable(targetChanged);
        }

        public void Clear()
        {
            mem = null;
            ctx = null;
            target = null;
            names = null;
            lastSignature = "";
            entries.Clear();
            if (grid != null) grid.Rows.Clear();
            if (targetLabel != null) targetLabel.Text = "Promises";
            if (countLabel != null) countLabel.Text = "Active: 0";
            SetStatus("Select a character in the left list.", false);
            UpdateEmptyState();
        }

        private void RefreshTable(bool targetChanged)
        {
            List<PromiseEntry> next = PromiseReader.Read(mem, target);
            string signature = Signature(next);
            bool rebuild = targetChanged || signature != lastSignature ||
                grid.Rows.Count != next.Count;

            if (rebuild)
            {
                entries.Clear();
                entries.AddRange(next);
                lastSignature = signature;
                RebuildRows();
            }
            else
            {
                UpdateNameCells();
            }

            UpdateHeader();
            ApplyFilter();
        }

        private static string Signature(List<PromiseEntry> list)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < list.Count; i++)
            {
                PromiseEntry e = list[i];
                sb.Append((int)e.Direction).Append(':')
                  .Append((int)e.Kind).Append(':')
                  .Append(e.OtherSeat).Append(':')
                  .Append(e.Location).Append(':')
                  .Append(e.TestType).Append(':')
                  .Append(e.Flag ? '1' : '0').Append(';');
            }
            return sb.ToString();
        }

        private void RebuildRows()
        {
            int keepIndex = grid.CurrentRow == null ? -1 : grid.CurrentRow.Index;

            grid.Rows.Clear();
            for (int i = 0; i < entries.Count; i++)
            {
                int index = grid.Rows.Add();
                DataGridViewRow row = grid.Rows[index];
                row.Tag = entries[i];
                UpdateRow(row, entries[i]);
            }

            if (keepIndex >= 0 && grid.Rows.Count > 0)
            {
                int select = Math.Min(keepIndex, grid.Rows.Count - 1);
                grid.CurrentCell = grid.Rows[select].Cells[0];
            }
        }

        private void UpdateRow(DataGridViewRow row, PromiseEntry entry)
        {
            row.Cells[0].Value = entry.Direction == PromiseDirection.Incoming
                ? "In" : "Out";
            row.Cells[1].Value = ClassmateLabel(entry.OtherSeat);
            row.Cells[2].Value = KindName(entry.Kind);
            row.Cells[3].Value = DetailText(entry);
            row.Cells[4].Value = StateText(entry);
        }

        private void UpdateNameCells()
        {
            for (int i = 0; i < grid.Rows.Count && i < entries.Count; i++)
            {
                DataGridViewRow row = grid.Rows[i];
                PromiseEntry entry = row.Tag as PromiseEntry;
                if (entry == null) continue;
                row.Cells[1].Value = ClassmateLabel(entry.OtherSeat);
            }
        }

        private string ClassmateLabel(int seat)
        {
            if (seat < 0) return "(unknown)";
            CharacterInfo other = FindSeat(seat);
            string prefix = seat.ToString("D2") + " ";
            if (other == null || string.IsNullOrEmpty(other.Name))
                return prefix + "(unknown)";
            return prefix + other.Name;
        }

        private CharacterInfo FindSeat(int seat)
        {
            if (ctx == null) return null;
            foreach (CharacterInfo c in ctx.Characters)
                if (c != null && c.Seat == seat) return c;
            return null;
        }

        private string DetailText(PromiseEntry entry)
        {
            if (entry.Kind == PromiseKind.Date) return "Sunday";
            if (entry.Kind == PromiseKind.Meet)
                return LocationName(entry.Location);
            if (entry.Kind == PromiseKind.Lewd)
                return TestTypeName(entry.TestType);
            return "\u2014";
        }

        private string LocationName(int location)
        {
            if (names != null)
            {
                string name = names.Get("LOCATION", location);
                if (!string.IsNullOrEmpty(name)) return name;
            }
            return location >= 0 ? "Location " + location.ToString() : "\u2014";
        }

        private string TestTypeName(int testType)
        {
            if (names != null)
            {
                string name = names.Get("TEST_TYPE", testType);
                if (!string.IsNullOrEmpty(name)) return name;
            }
            return testType >= 0 ? "Test " + testType.ToString() : "\u2014";
        }

        private static string KindName(PromiseKind kind)
        {
            switch (kind)
            {
                case PromiseKind.Date: return "Date";
                case PromiseKind.Meet: return "Meet";
                case PromiseKind.Lewd: return "Lewd";
                default: return "?";
            }
        }

        private static string StateText(PromiseEntry entry)
        {
            if (entry.Kind != PromiseKind.Meet) return "\u2014";
            return entry.Flag ? "Confirmed" : "Pending";
        }

        private void UpdateHeader()
        {
            if (targetLabel == null) return;

            if (target == null)
            {
                targetLabel.Text = "Promises";
                if (countLabel != null) countLabel.Text = "Active: 0";
                return;
            }

            string seat = target.Seat >= 0 ? target.Seat.ToString("D2") + " " : "";
            string name = string.IsNullOrEmpty(target.Name) ? "(unnamed)" : target.Name;
            targetLabel.Text = "Promises \u00B7 target: " + seat + name +
                (target.IsPlayer ? " \u00B7 PC" : "");
            if (countLabel != null)
                countLabel.Text = "Active: " + entries.Count.ToString();
        }

        private void ApplyFilter()
        {
            if (grid == null) return;

            string direction = directionCombo == null ||
                directionCombo.SelectedIndex <= 0
                ? null : directionCombo.SelectedItem as string;
            string kind = typeCombo == null || typeCombo.SelectedIndex <= 0
                ? null : typeCombo.SelectedItem as string;
            string search = searchBox == null ? "" : searchBox.Text.Trim();

            int visible = 0;
            for (int i = 0; i < grid.Rows.Count; i++)
            {
                PromiseEntry entry = grid.Rows[i].Tag as PromiseEntry;
                bool show = entry != null;

                if (show && direction == "In")
                    show = entry.Direction == PromiseDirection.Incoming;
                else if (show && direction == "Out")
                    show = entry.Direction == PromiseDirection.Outgoing;
                if (show && kind != null)
                    show = KindName(entry.Kind) == kind;

                if (show && search.Length > 0)
                {
                    string classmate = ClassmateLabel(entry.OtherSeat);
                    string detail = DetailText(entry);
                    show = classmate.IndexOf(search,
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                        detail.IndexOf(search,
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                        KindName(entry.Kind).IndexOf(search,
                        StringComparison.OrdinalIgnoreCase) >= 0;
                }

                grid.Rows[i].Visible = show;
                if (show) visible++;
            }

            if (entries.Count == 0)
            {
                SetStatus("Read-only promise view; values refresh live.", false);
            }
            else if (visible == 0)
            {
                SetStatus("No promises match the current filter.", false);
            }
            else
            {
                SetStatus("Read-only promise view; values refresh live.", false);
            }

            UpdateEmptyState();
        }

        private void UpdateEmptyState()
        {
            if (grid == null || emptyLabel == null) return;
            bool empty = entries.Count == 0;
            grid.Visible = !empty;
            emptyLabel.Visible = empty;
            if (empty) emptyLabel.BringToFront();
        }

        private void Grid_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            PromiseEntry entry = grid.Rows[e.RowIndex].Tag as PromiseEntry;
            if (entry == null || entry.OtherSeat < 0) return;
            CharacterInfo other = FindSeat(entry.OtherSeat);
            if (other == null) return;
            if (OpenCharacterRequested != null)
                OpenCharacterRequested(this,
                    new CharacterSelectedEventArgs(other));
        }

        private void Grid_CellToolTipTextNeeded(object sender,
            DataGridViewCellToolTipTextNeededEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            PromiseEntry entry = grid.Rows[e.RowIndex].Tag as PromiseEntry;
            if (entry == null) return;

            if (e.ColumnIndex == 0)
                e.ToolTipText = entry.Direction == PromiseDirection.Incoming
                    ? "Promise made by this classmate toward the selected character"
                    : "Promise made by the selected character toward this classmate";
            else if (e.ColumnIndex == 1)
                e.ToolTipText = "Classmate \u00B7 Seat " +
                    entry.OtherSeat.ToString("D2");
            else if (e.ColumnIndex == 4 && entry.Kind == PromiseKind.Meet)
                e.ToolTipText = entry.Flag
                    ? "Meeting flag is set in the game"
                    : "Meeting flag is clear in the game";
        }

        private void SetStatus(string text, bool error)
        {
            if (statusLabel == null) return;
            statusLabel.Text = text ?? "";
            statusLabel.ForeColor = error ? Color.Firebrick : Color.DimGray;
        }
    }
}
