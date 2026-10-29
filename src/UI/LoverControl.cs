using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace AA2Trainer
{
    /// <summary>
    /// Incoming lover editor. Each row is a classmate's lover flag, day count
    /// and compatibility toward the selected character. Values are edited in
    /// place and written through the shared pointer-health gate.
    /// </summary>
    public sealed class LoverControl : UserControl
    {
        private GameMemory mem;
        private GameContext ctx;
        private CharacterInfo target;
        private bool writeAllowed;
        private bool suppress;

        public Func<bool> ConfirmWriteAllowed { get; set; }
        public event EventHandler<CharacterSelectedEventArgs> OpenCharacterRequested;

        /// <summary>
        /// True while a Days/Compatibility cell is in edit mode. The live
        /// refresh pauses on this so typed input is never overwritten.
        /// </summary>
        public bool IsEditingCell
        {
            get { return grid != null && grid.IsCurrentCellInEditMode; }
        }

        /// <summary>
        /// The grid itself must also be left alone whenever it owns focus:
        /// rebuilding rows or toggling visibility would drop the selection the
        /// user needs before the first keystroke starts an edit.
        /// </summary>
        private bool GridHasFocus
        {
            get { return grid != null && grid.ContainsFocus; }
        }

        private readonly List<LoverEntry> entries = new List<LoverEntry>();
        private bool resettingColumns;

        private Label targetLabel;
        private Label loversLabel;
        private Label statusLabel;
        private ComboBox viewCombo;
        private TextBox searchBox;
        private DataGridView grid;

        public LoverControl()
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

            statusLabel = new Label();
            statusLabel.Dock = DockStyle.Fill;
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            statusLabel.AutoEllipsis = true;
            statusLabel.Font = new Font("Segoe UI", 8F);
            statusLabel.ForeColor = Color.DimGray;
            statusLabel.Margin = new Padding(0);
            statusLabel.Text = "Click a Lover box, or edit Days and Compatibility in place.";
            root.Controls.Add(statusLabel, 0, 3);

            UpdateHeader();
            UpdateEditorState();
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
            targetLabel.Text = "Incoming lover view";
            header.Controls.Add(targetLabel, 0, 0);

            loversLabel = new Label();
            loversLabel.AutoSize = true;
            loversLabel.TextAlign = ContentAlignment.MiddleRight;
            loversLabel.ForeColor = Color.DimGray;
            loversLabel.Margin = new Padding(6, 7, 6, 0);
            loversLabel.Text = "Lovers: 0";
            header.Controls.Add(loversLabel, 1, 0);

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

            Label viewLabel = new Label();
            viewLabel.Text = "View";
            viewLabel.AutoSize = true;
            viewLabel.Margin = new Padding(0, 5, 4, 0);
            row.Controls.Add(viewLabel);

            viewCombo = new ComboBox();
            viewCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            viewCombo.Width = 120;
            viewCombo.Margin = new Padding(0, 2, 10, 0);
            viewCombo.Items.AddRange(new object[]
            {
                "All", "Lovers", "Not lovers"
            });
            viewCombo.SelectedIndex = 0;
            viewCombo.SelectedIndexChanged += delegate { ApplyFilter(); };
            row.Controls.Add(viewCombo);

            Label searchLabel = new Label();
            searchLabel.Text = "Search";
            searchLabel.AutoSize = true;
            searchLabel.Margin = new Padding(0, 5, 4, 0);
            row.Controls.Add(searchLabel);

            searchBox = new TextBox();
            searchBox.Width = 210;
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
            value.AccessibleName = "Incoming lovers";
            value.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
            TryDoubleBuffer(value);

            DataGridViewTextBoxColumn classmate = new DataGridViewTextBoxColumn();
            classmate.HeaderText = "Classmate";
            classmate.FillWeight = 46F;
            classmate.MinimumWidth = 140;
            classmate.SortMode = DataGridViewColumnSortMode.NotSortable;
            classmate.ReadOnly = true;
            classmate.ToolTipText = "Classmate";
            value.Columns.Add(classmate);

            DataGridViewCheckBoxColumn lover = new DataGridViewCheckBoxColumn();
            lover.HeaderText = "Lover";
            lover.FillWeight = 16F;
            lover.MinimumWidth = 70;
            lover.SortMode = DataGridViewColumnSortMode.NotSortable;
            lover.ReadOnly = true;
            lover.TrueValue = true;
            lover.FalseValue = false;
            lover.FlatStyle = FlatStyle.Standard;
            lover.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            lover.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            lover.ToolTipText = "This classmate's lover flag toward the selected character";
            value.Columns.Add(lover);

            DataGridViewTextBoxColumn days = new DataGridViewTextBoxColumn();
            days.HeaderText = "Days";
            days.FillWeight = 16F;
            days.MinimumWidth = 80;
            days.SortMode = DataGridViewColumnSortMode.NotSortable;
            days.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            days.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            days.ToolTipText = "Days as this character's lover, 0-255";
            value.Columns.Add(days);

            DataGridViewTextBoxColumn compatibility = new DataGridViewTextBoxColumn();
            compatibility.HeaderText = "Compatibility";
            compatibility.FillWeight = 22F;
            compatibility.MinimumWidth = 110;
            compatibility.SortMode = DataGridViewColumnSortMode.NotSortable;
            compatibility.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            compatibility.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            compatibility.ToolTipText = "This classmate's compatibility toward the selected character, 0-999";
            value.Columns.Add(compatibility);

            value.CellMouseClick += Grid_CellMouseClick;
            value.CellBeginEdit += Grid_CellBeginEdit;
            value.CellEndEdit += Grid_CellEndEdit;
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

        /// <summary>
        /// Fill mode distributes new space when the grid first grows, which
        /// leaves the columns almost equal instead of honouring FillWeight.
        /// Re-applying the mode recalculates the weights from the final width.
        /// </summary>
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
            CharacterInfo selected, bool canWrite)
        {
            if (memory == null || context == null || selected == null ||
                !memory.IsAttached)
            {
                Clear();
                return;
            }

            bool targetChanged = target == null || target.Pointer != selected.Pointer;

            // Never touch the grid, labels or column state while a cell is
            // being edited or while the grid owns focus: reassigning them
            // cancels the in-place edit or clears the selected cell.
            if (IsEditingCell || GridHasFocus) return;

            mem = memory;
            ctx = context;
            target = selected;
            SetWriteAllowed(canWrite);
            UpdateEditorState();
            UpdateHeader();

            RefreshTable(targetChanged);
        }

        public void SetWriteAllowed(bool allowed)
        {
            if (writeAllowed == allowed) return;

            writeAllowed = allowed;
            if (!allowed && IsEditingCell)
                grid.CancelEdit();
            UpdateEditorState();
        }

        public void Clear()
        {
            mem = null;
            ctx = null;
            target = null;
            entries.Clear();
            if (grid != null)
            {
                if (grid.IsCurrentCellInEditMode) grid.CancelEdit();
                grid.Rows.Clear();
            }
            if (targetLabel != null) targetLabel.Text = "Incoming lover view";
            if (loversLabel != null) loversLabel.Text = "Lovers: 0";
            UpdateEditorState();
            SetStatus("Select a character in the left list.", false);
        }

        private void UpdateEditorState()
        {
            if (grid == null || grid.Columns.Count < 4) return;
            bool editable = writeAllowed && target != null;
            grid.Columns[2].ReadOnly = !editable;
            grid.Columns[3].ReadOnly = !editable;
            grid.Columns[0].ReadOnly = true;
            grid.Columns[1].ReadOnly = true;
        }

        private void UpdateHeader()
        {
            if (targetLabel == null) return;

            if (target == null)
            {
                targetLabel.Text = "Incoming lover view";
                if (loversLabel != null) loversLabel.Text = "Lovers: 0";
                return;
            }

            string seat = target.Seat >= 0 ? target.Seat.ToString("D2") + " " : "";
            string name = string.IsNullOrEmpty(target.Name) ? "(unnamed)" : target.Name;
            targetLabel.Text = "Incoming \u00B7 target: " + seat + name +
                (target.IsPlayer ? " \u00B7 PC" : "");

            int lovers = 0;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i] != null && entries[i].IsLover) lovers++;
            if (loversLabel != null)
                loversLabel.Text = "Lovers: " + lovers.ToString();
        }

        private void RefreshTable(bool rebuild)
        {
            List<LoverEntry> next = LoverEditor.ReadIncoming(mem, ctx, target);

            if (!rebuild)
            {
                if (next.Count != entries.Count || next.Count != grid.Rows.Count)
                {
                    rebuild = true;
                }
                else
                {
                    for (int i = 0; i < next.Count; i++)
                    {
                        LoverEntry existing = entries[i];
                        LoverEntry fresh = next[i];
                        if (existing == null || existing.Source == null ||
                            fresh.Source == null ||
                            existing.Source.Pointer != fresh.Source.Pointer)
                        {
                            rebuild = true;
                            break;
                        }
                    }
                }
            }

            if (rebuild)
            {
                entries.Clear();
                entries.AddRange(next);
                RebuildRows();
            }
            else
            {
                for (int i = 0; i < next.Count; i++)
                {
                    LoverEntry entry = entries[i];
                    LoverEntry fresh = next[i];
                    entry.IsLover = fresh.IsLover;
                    entry.Days = fresh.Days;
                    entry.Compatibility = fresh.Compatibility;
                    entry.HasCompatibility = fresh.HasCompatibility;
                    UpdateRow(grid.Rows[i], entry, true);
                }
            }

            UpdateHeader();
            ApplyFilter();
        }

        private void RebuildRows()
        {
            uint keepPointer = 0;
            if (grid.CurrentRow != null)
            {
                LoverEntry selectedEntry = grid.CurrentRow.Tag as LoverEntry;
                if (selectedEntry != null && selectedEntry.Source != null)
                    keepPointer = selectedEntry.Source.Pointer;
            }

            suppress = true;
            grid.Rows.Clear();
            for (int i = 0; i < entries.Count; i++)
            {
                int index = grid.Rows.Add();
                DataGridViewRow row = grid.Rows[index];
                row.Tag = entries[i];
                UpdateRow(row, entries[i], false);
            }
            suppress = false;

            ApplyFilter();

            if (keepPointer != 0)
            {
                for (int i = 0; i < grid.Rows.Count; i++)
                {
                    LoverEntry entry = grid.Rows[i].Tag as LoverEntry;
                    if (entry == null || entry.Source == null) continue;
                    if (entry.Source.Pointer != keepPointer) continue;
                    if (!grid.Rows[i].Visible) break;
                    grid.CurrentCell = grid.Rows[i].Cells[0];
                    break;
                }
            }
        }

        private void UpdateRow(DataGridViewRow row, LoverEntry entry, bool skipEditedCell)
        {
            if (row == null || entry == null) return;

            CharacterInfo source = entry.Source;
            string seat = source != null && source.Seat >= 0
                ? source.Seat.ToString("D2") + " " : "";
            string name = source == null || string.IsNullOrEmpty(source.Name)
                ? "(unnamed)" : source.Name;

            bool editingNow = skipEditedCell && grid.IsCurrentCellInEditMode;
            int editRow = editingNow && grid.CurrentCell != null
                ? grid.CurrentCell.RowIndex : -1;

            // Leave the row being edited untouched so the editing control keeps
            // its text. Tooltips can still be refreshed below.
            if (editRow != row.Index)
            {
                row.Cells[0].Value = seat + name;
                row.Cells[1].Value = entry.IsLover;
                row.Cells[2].Value = entry.Days;
                row.Cells[3].Value = entry.HasCompatibility
                    ? (object)entry.Compatibility : "\u2014";
            }

            row.Cells[1].ToolTipText = entry.IsLover
                ? "Lover toward the selected character. Click to clear."
                : "Not a lover of the selected character. Click to set.";
            row.Cells[2].ToolTipText = "Days as this character's lover, 0-255";
            row.Cells[3].ToolTipText = entry.HasCompatibility
                ? "Compatibility toward the selected character, 0-999"
                : "Compatibility is unavailable for this classmate.";
        }

        private void ApplyFilter()
        {
            if (grid == null || grid.IsCurrentCellInEditMode) return;

            string view = viewCombo == null || viewCombo.SelectedIndex < 0
                ? "All" : viewCombo.SelectedItem as string;
            string search = searchBox == null ? "" : searchBox.Text.Trim();

            for (int i = 0; i < grid.Rows.Count; i++)
            {
                LoverEntry entry = grid.Rows[i].Tag as LoverEntry;
                bool visible = entry != null;

                if (visible && view == "Lovers")
                    visible = entry.IsLover;
                else if (visible && view == "Not lovers")
                    visible = !entry.IsLover;

                if (visible && search.Length > 0)
                {
                    string seat = entry.Source != null && entry.Source.Seat >= 0
                        ? entry.Source.Seat.ToString("D2") : "";
                    string name = entry.Source == null ? "" : entry.Source.Name;
                    visible = (name ?? "").IndexOf(search,
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                        seat.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
                }

                grid.Rows[i].Visible = visible;
            }
        }

        private void Grid_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || e.RowIndex < 0 || e.ColumnIndex != 1)
                return;

            LoverEntry entry = grid.Rows[e.RowIndex].Tag as LoverEntry;
            if (entry == null || entry.Source == null || target == null) return;

            if (!writeAllowed)
            {
                SetStatus("Lovers are read-only while the write gate is locked.", true);
                return;
            }
            if (ConfirmWriteAllowed != null && !ConfirmWriteAllowed())
            {
                SetStatus("Write gate is locked. Wait for a clean pointer check.", true);
                return;
            }

            bool next = !entry.IsLover;
            string error;
            if (!LoverEditor.TryWriteLover(mem, entry.Source, target.Seat, next,
                out error))
            {
                SetStatus(error, true);
                return;
            }

            entry.IsLover = next;
            string daysError = null;
            if (next && entry.Days == 0)
            {
                if (LoverEditor.TryWriteDays(mem, entry.Source, target.Seat, 1,
                    out daysError))
                    entry.Days = 1;
            }
            else if (!next && entry.Days != 0)
            {
                if (LoverEditor.TryWriteDays(mem, entry.Source, target.Seat, 0,
                    out daysError))
                    entry.Days = 0;
            }

            UpdateRow(grid.Rows[e.RowIndex], entry, false);
            UpdateHeader();
            ApplyFilter();

            if (!string.IsNullOrEmpty(daysError))
                SetStatus("Lover changed, but days update failed: " + daysError, true);
            else if (next)
                SetStatus("Lover set" + (entry.Days > 0
                    ? " with day " + entry.Days.ToString() + "." : "."), false);
            else
                SetStatus("Lover cleared.", false);
        }

        private void Grid_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            if (e.RowIndex < 0 || (e.ColumnIndex != 2 && e.ColumnIndex != 3))
            {
                e.Cancel = true;
                return;
            }
            if (!writeAllowed)
            {
                e.Cancel = true;
                SetStatus("Days and Compatibility are read-only while the write gate is locked.",
                    true);
                return;
            }

            SetStatus("Editing " + grid.Columns[e.ColumnIndex].HeaderText.ToLowerInvariant() +
                ". Enter to apply, Esc to cancel.", false);
        }

        private void Grid_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (suppress || e.RowIndex < 0) return;
            if (e.ColumnIndex != 2 && e.ColumnIndex != 3) return;

            LoverEntry entry = grid.Rows[e.RowIndex].Tag as LoverEntry;
            if (entry == null || entry.Source == null || target == null) return;

            DataGridViewCell cell = grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
            string text = cell.Value == null ? "" : Convert.ToString(cell.Value);
            int value;
            if (!int.TryParse(text, out value))
            {
                RestoreCell(e.RowIndex, e.ColumnIndex, entry);
                SetStatus("Enter a whole number.", true);
                return;
            }

            if (e.ColumnIndex == 2)
            {
                if (value < 0) value = 0;
                if (value > LoverEditor.MaxDays) value = LoverEditor.MaxDays;
                if (value == entry.Days)
                {
                    cell.Value = value;
                    return;
                }
                WriteDays(e.RowIndex, entry, value);
            }
            else
            {
                if (value < 0) value = 0;
                if (value > LoverEditor.MaxCompatibility)
                    value = LoverEditor.MaxCompatibility;
                if (value == entry.Compatibility && entry.HasCompatibility)
                {
                    cell.Value = value;
                    return;
                }
                WriteCompatibility(e.RowIndex, entry, value);
            }
        }

        private void RestoreCell(int rowIndex, int columnIndex, LoverEntry entry)
        {
            DataGridViewCell cell = grid.Rows[rowIndex].Cells[columnIndex];
            if (columnIndex == 2)
                cell.Value = entry.Days;
            else if (columnIndex == 3)
                cell.Value = entry.HasCompatibility
                    ? (object)entry.Compatibility : "\u2014";
        }

        private void WriteDays(int rowIndex, LoverEntry entry, int value)
        {
            if (ConfirmWriteAllowed != null && !ConfirmWriteAllowed())
            {
                RestoreCell(rowIndex, 2, entry);
                SetStatus("Write gate is locked. Wait for a clean pointer check.", true);
                return;
            }

            string error;
            if (!LoverEditor.TryWriteDays(mem, entry.Source, target.Seat, value,
                out error))
            {
                RestoreCell(rowIndex, 2, entry);
                SetStatus(error, true);
                return;
            }

            entry.Days = value;
            UpdateRow(grid.Rows[rowIndex], entry, false);
            SetStatus("Days set to " + value.ToString() + ".", false);
        }

        private void WriteCompatibility(int rowIndex, LoverEntry entry, int value)
        {
            if (ConfirmWriteAllowed != null && !ConfirmWriteAllowed())
            {
                RestoreCell(rowIndex, 3, entry);
                SetStatus("Write gate is locked. Wait for a clean pointer check.", true);
                return;
            }

            string error;
            if (!LoverEditor.TryWriteCompatibility(mem, entry.Source, target.Seat,
                value, out error))
            {
                RestoreCell(rowIndex, 3, entry);
                SetStatus(error, true);
                return;
            }

            entry.Compatibility = value;
            entry.HasCompatibility = true;
            UpdateRow(grid.Rows[rowIndex], entry, false);
            SetStatus("Compatibility set to " + value.ToString() + ".", false);
        }

        private void Grid_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != 0) return;

            LoverEntry entry = grid.Rows[e.RowIndex].Tag as LoverEntry;
            if (entry == null || entry.Source == null) return;
            if (OpenCharacterRequested != null)
                OpenCharacterRequested(this, new CharacterSelectedEventArgs(entry.Source));
        }

        private void Grid_CellToolTipTextNeeded(object sender,
            DataGridViewCellToolTipTextNeededEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            LoverEntry entry = grid.Rows[e.RowIndex].Tag as LoverEntry;
            if (entry == null || entry.Source == null) return;

            if (e.ColumnIndex == 0)
                e.ToolTipText = entry.Source.Name + " \u00B7 Seat " +
                    entry.Source.Seat.ToString("D2");
        }

        private void SetStatus(string text, bool error)
        {
            if (statusLabel == null) return;
            statusLabel.Text = text ?? "";
            statusLabel.ForeColor = error ? Color.Firebrick : Color.DimGray;
        }
    }
}
