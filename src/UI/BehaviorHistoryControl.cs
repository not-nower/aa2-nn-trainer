using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace AA2Trainer
{
    /// <summary>
    /// Read-only feeling history: every classmate's newest-30 feeling events
    /// toward the character selected in the Class Seats list.
    /// </summary>
    public sealed class BehaviorHistoryControl : UserControl
    {
        private static readonly string[] FeelingNames =
        {
            "Love", "Like", "Dislike", "Hate"
        };

        private static readonly string[] FeelingGlyphs =
        {
            "\u2665", "\u25CF", "\u25BC", "\u2715"
        };

        private static readonly Color[] FeelingColors =
        {
            Color.FromArgb(200, 60, 105),
            Color.FromArgb(40, 105, 180),
            Color.FromArgb(200, 120, 35),
            Color.FromArgb(185, 55, 55)
        };

        private GameMemory mem;
        private GameContext ctx;
        private CharacterInfo target;
        private bool writeAllowed;

        public Func<bool> ConfirmWriteAllowed { get; set; }

        private readonly List<RelationshipHistoryRow> rows =
            new List<RelationshipHistoryRow>();

        private Label targetLabel;
        private ComboBox highlightCombo;
        private TextBox searchBox;
        private DataGridView grid;
        private Label emptyLabel;
        private Label statusLabel;
        private Font historyFont;
        private Font headerNumberFont;

        private ContextMenuStrip historyMenu;
        private ToolStripMenuItem historyMenuHeader;
        private readonly ToolStripMenuItem[] historyMenuItems =
            new ToolStripMenuItem[4];
        private ToolStripMenuItem historyAllMenu;
        private readonly ToolStripMenuItem[] historyAllItems =
            new ToolStripMenuItem[4];
        private int editRowIndex = -1;
        private int editEventIndex = -1;
        private RelationshipHistoryRow editRow;

        public BehaviorHistoryControl()
        {
            DoubleBuffered = true;
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(248, 249, 251);
            Padding = new Padding(4, 2, 4, 4);
            historyFont = new Font("Segoe UI Symbol", 9.5F);
            headerNumberFont = new Font("Segoe UI", 6.5F);
            BuildUi();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (historyFont != null)
                {
                    historyFont.Dispose();
                    historyFont = null;
                }
                if (headerNumberFont != null)
                {
                    headerNumberFont.Dispose();
                    headerNumberFont = null;
                }
                if (historyMenu != null)
                {
                    historyMenu.Dispose();
                    historyMenu = null;
                }
            }
            base.Dispose(disposing);
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

            targetLabel = new Label();
            targetLabel.Dock = DockStyle.Fill;
            targetLabel.TextAlign = ContentAlignment.MiddleLeft;
            targetLabel.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            targetLabel.ForeColor = Color.FromArgb(40, 44, 52);
            targetLabel.AutoEllipsis = true;
            targetLabel.Text = "Feeling history";
            root.Controls.Add(targetLabel, 0, 0);

            FlowLayoutPanel filter = new FlowLayoutPanel();
            filter.Dock = DockStyle.Fill;
            filter.WrapContents = false;
            filter.FlowDirection = FlowDirection.LeftToRight;
            filter.Padding = new Padding(0, 4, 0, 0);
            filter.Margin = new Padding(0);
            root.Controls.Add(filter, 0, 1);

            Label highlightLabel = new Label();
            highlightLabel.Text = "Highlight";
            highlightLabel.AutoSize = true;
            highlightLabel.Margin = new Padding(0, 5, 5, 0);
            filter.Controls.Add(highlightLabel);

            highlightCombo = new ComboBox();
            highlightCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            highlightCombo.Width = 130;
            highlightCombo.Margin = new Padding(0, 2, 12, 0);
            highlightCombo.AccessibleName = "Highlight feeling";
            highlightCombo.Items.Add("All");
            for (int f = 0; f < 4; f++)
                highlightCombo.Items.Add(FeelingGlyphs[f] + " " + FeelingNames[f]);
            highlightCombo.SelectedIndex = 0;
            highlightCombo.SelectedIndexChanged += delegate
            {
                if (grid != null) grid.InvalidateColumn(1);
            };
            filter.Controls.Add(highlightCombo);

            Label searchLabel = new Label();
            searchLabel.Text = "Search";
            searchLabel.AutoSize = true;
            searchLabel.Margin = new Padding(0, 5, 5, 0);
            filter.Controls.Add(searchLabel);

            searchBox = new TextBox();
            searchBox.Width = 220;
            searchBox.Margin = new Padding(0, 2, 0, 0);
            searchBox.AccessibleName = "Search classmates";
            searchBox.TextChanged += delegate { ApplySearch(); };
            filter.Controls.Add(searchBox);

            Panel host = new Panel();
            host.Dock = DockStyle.Fill;
            host.BackColor = Color.FromArgb(248, 249, 251);
            host.Margin = new Padding(0);
            root.Controls.Add(host, 0, 2);

            grid = BuildTable();
            host.Controls.Add(grid);
            BuildHistoryMenu();

            emptyLabel = new Label();
            emptyLabel.Dock = DockStyle.Fill;
            emptyLabel.TextAlign = ContentAlignment.MiddleCenter;
            emptyLabel.Font = new Font("Segoe UI", 10F);
            emptyLabel.ForeColor = Color.Gray;
            emptyLabel.BackColor = Color.FromArgb(248, 249, 251);
            emptyLabel.Visible = false;
            host.Controls.Add(emptyLabel);
            emptyLabel.BringToFront();

            statusLabel = new Label();
            statusLabel.Dock = DockStyle.Fill;
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            statusLabel.AutoEllipsis = true;
            statusLabel.Font = new Font("Segoe UI", 8F);
            statusLabel.ForeColor = Color.DimGray;
            statusLabel.Margin = new Padding(0);
            statusLabel.Text = "Left-click an existing history cell to edit its feeling.";
            root.Controls.Add(statusLabel, 0, 3);

            UpdateHeader();
            UpdateEmptyState(false, false);
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
            value.ColumnHeadersHeight = 44;
            value.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            value.RowTemplate.Height = 30;
            value.EnableHeadersVisualStyles = false;
            value.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(240, 241, 244);
            value.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(240, 241, 244);
            value.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.FromArgb(30, 30, 30);
            value.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            value.DefaultCellStyle.Font = new Font("Segoe UI", 9F);
            value.DefaultCellStyle.SelectionBackColor = Color.White;
            value.DefaultCellStyle.SelectionForeColor = Color.FromArgb(30, 30, 30);
            value.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            value.ShowCellToolTips = true;
            value.AccessibleName = "Feeling history";
            value.Margin = new Padding(0);
            TryDoubleBuffer(value);

            DataGridViewTextBoxColumn classmate = new DataGridViewTextBoxColumn();
            classmate.HeaderText = "Classmate";
            classmate.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            classmate.Width = 190;
            classmate.MinimumWidth = 130;
            classmate.SortMode = DataGridViewColumnSortMode.NotSortable;
            classmate.ReadOnly = true;
            value.Columns.Add(classmate);

            DataGridViewTextBoxColumn history = new DataGridViewTextBoxColumn();
            history.HeaderText = "Feeling history (oldest \u2192 newest)";
            history.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            history.FillWeight = 100F;
            history.MinimumWidth = 420;
            history.SortMode = DataGridViewColumnSortMode.NotSortable;
            history.ReadOnly = true;
            value.Columns.Add(history);

            value.CellPainting += Grid_CellPainting;
            value.CellMouseDown += Grid_CellMouseDown;
            value.CellMouseClick += Grid_CellMouseClick;
            return value;
        }

        private void BuildHistoryMenu()
        {
            historyMenu = new ContextMenuStrip();
            historyMenu.ShowItemToolTips = true;
            historyMenu.Opening += HistoryMenu_Opening;

            historyMenuHeader = new ToolStripMenuItem("Event");
            historyMenuHeader.Enabled = false;
            historyMenu.Items.Add(historyMenuHeader);
            historyMenu.Items.Add(new ToolStripSeparator());

            for (int f = 0; f < 4; f++)
            {
                int feeling = f;
                ToolStripMenuItem item = new ToolStripMenuItem(
                    FeelingGlyphs[f] + " " + FeelingNames[f]);
                item.Click += delegate { SetHistoryFeeling(feeling); };
                historyMenu.Items.Add(item);
                historyMenuItems[f] = item;
            }

            historyMenu.Items.Add(new ToolStripSeparator());
            historyAllMenu = new ToolStripMenuItem("All");
            for (int f = 0; f < 4; f++)
            {
                int feeling = f;
                ToolStripMenuItem item = new ToolStripMenuItem(
                    FeelingGlyphs[f] + " " + FeelingNames[f]);
                item.Click += delegate { SetAllHistoryFeeling(feeling); };
                historyAllMenu.DropDownItems.Add(item);
                historyAllItems[f] = item;
            }
            historyMenu.Items.Add(historyAllMenu);
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

        // ===================== target / refresh =====================

        public void SetTarget(GameMemory memory, GameContext context, CharacterInfo selected)
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
            if (targetChanged)
                SetStatus("Left-click an existing history cell to edit its feeling.", false);

            UpdateHeader();

            if (selected.Seat < 0 || selected.Seat > 24)
            {
                rows.Clear();
                grid.Rows.Clear();
                UpdateEmptyState(false, false);
                return;
            }

            List<RelationshipHistoryRow> next =
                RelationshipEditor.ReadIncomingHistory(mem, ctx, target);
            rows.Clear();
            rows.AddRange(next);

            bool rebuild = targetChanged || grid.Rows.Count != rows.Count;
            if (!rebuild)
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    RelationshipHistoryRow existing =
                        grid.Rows[i].Tag as RelationshipHistoryRow;
                    if (existing == null || existing.Source == null ||
                        rows[i].Source == null ||
                        existing.Source.Pointer != rows[i].Source.Pointer)
                    {
                        rebuild = true;
                        break;
                    }
                }
            }

            if (rebuild)
            {
                grid.Rows.Clear();
                foreach (RelationshipHistoryRow row in rows)
                {
                    int index = grid.Rows.Add();
                    UpdateRow(grid.Rows[index], row);
                }
                grid.ClearSelection();
            }
            else
            {
                for (int i = 0; i < rows.Count; i++)
                    UpdateRow(grid.Rows[i], rows[i]);
            }

            ApplySearch();
            grid.InvalidateColumn(1);
        }

        public void Clear()
        {
            mem = null;
            ctx = null;
            target = null;
            rows.Clear();
            if (grid != null) grid.Rows.Clear();
            UpdateHeader();
            UpdateEmptyState(false, false);
            SetStatus("Select a character in the left list.", false);
        }

        public void SetWriteAllowed(bool allowed)
        {
            writeAllowed = allowed;
        }

        private void UpdateHeader()
        {
            if (targetLabel == null) return;

            if (target == null)
            {
                targetLabel.Text = "Feeling history";
                return;
            }

            string seat = target.Seat >= 0 ? target.Seat.ToString("D2") + " " : "";
            string name = string.IsNullOrEmpty(target.Name) ? "(unnamed)" : target.Name;
            targetLabel.Text = "Feeling history \u00B7 target: " + seat + name +
                (target.IsPlayer ? " \u00B7 PC" : "");
        }

        private void UpdateRow(DataGridViewRow gridRow, RelationshipHistoryRow row)
        {
            gridRow.Tag = row;
            if (row == null || row.Source == null) return;

            string seat = row.Source.Seat >= 0 ? row.Source.Seat.ToString("D2") + " " : "";
            gridRow.Cells[0].Value = seat +
                (string.IsNullOrEmpty(row.Source.Name) ? "(unnamed)" : row.Source.Name);

            string summary = RowSummary(row);
            gridRow.Cells[1].Value = summary;
            gridRow.Cells[1].ToolTipText = summary;
        }

        private void SetStatus(string text, bool error)
        {
            if (statusLabel == null) return;
            statusLabel.Text = text ?? "";
            statusLabel.ForeColor = error ? Color.Firebrick : Color.DimGray;
        }

        private string RowSummary(RelationshipHistoryRow row)
        {
            if (row == null) return "";
            if (!row.HasRecord) return "No relationship record for this classmate.";
            if (row.Count == 0) return "No feeling history yet.";

            string text = row.Count.ToString() + " events";
            if (row.Latest >= 0 && row.Latest <= 3)
            {
                text += " \u00B7 latest " + FeelingGlyphs[row.Latest] + " " +
                    FeelingNames[row.Latest];
                if (row.LatestLevel >= 0)
                    text += " \u00B7 " + LevelName(row.LatestLevel);
            }
            else
            {
                text += " \u00B7 latest unknown";
            }
            return text;
        }

        private void Grid_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
                OpenHistoryMenu(e);
        }

        private void Grid_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                OpenHistoryMenu(e);
        }

        private void OpenHistoryMenu(DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != 1) return;

            if (!writeAllowed)
            {
                SetStatus("History is read-only while the write gate is locked.", true);
                return;
            }

            RelationshipHistoryRow row = grid.Rows[e.RowIndex].Tag as RelationshipHistoryRow;
            if (row == null || row.Source == null || !row.HasRecord || row.Count == 0)
            {
                SetStatus("No feeling history to edit for this classmate.", true);
                return;
            }

            Rectangle cell = grid.GetCellDisplayRectangle(1, e.RowIndex, false);
            Point pointer = grid.PointToClient(Cursor.Position);
            int localX = pointer.X - cell.Left;
            int index;
            if (!TryHitHistoryCell(localX, cell.Width, out index))
            {
                SetStatus("No event at this position.", true);
                return;
            }
            if (index >= row.Count)
            {
                SetStatus("No event at position " + (index + 1).ToString() +
                    "; only existing events can be edited.", true);
                return;
            }

            editRowIndex = e.RowIndex;
            editEventIndex = index;
            editRow = row;

            if (historyMenu != null)
                historyMenu.Show(grid, grid.PointToClient(Cursor.Position));
        }

        private void HistoryMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (editRow == null || editRow.History == null ||
                editEventIndex < 0 || editEventIndex >= editRow.Count)
            {
                e.Cancel = true;
                return;
            }

            int current = editRow.History[editEventIndex];
            string currentText = current >= 0 && current <= 3
                ? FeelingGlyphs[current] + " " + FeelingNames[current]
                : "unknown";
            historyMenuHeader.Text = "Event " + (editEventIndex + 1).ToString() +
                " \u00B7 current " + currentText;

            for (int f = 0; f < 4; f++)
            {
                if (historyMenuItems[f] == null) continue;
                historyMenuItems[f].Checked = current == f;
                historyMenuItems[f].Enabled = writeAllowed;
            }

            if (historyAllMenu != null)
            {
                historyAllMenu.Enabled = writeAllowed && editRow.Count > 0;
                for (int f = 0; f < 4; f++)
                {
                    if (historyAllItems[f] == null) continue;
                    historyAllItems[f].Enabled = writeAllowed;
                    historyAllItems[f].Checked = AllEqual(editRow, f);
                }
            }
        }

        private static bool AllEqual(RelationshipHistoryRow row, int feeling)
        {
            if (row == null || row.History == null || row.History.Length == 0)
                return false;
            for (int i = 0; i < row.History.Length; i++)
                if (row.History[i] != feeling) return false;
            return true;
        }

        private void SetAllHistoryFeeling(int feeling)
        {
            if (editRow == null || editRow.Source == null || target == null) return;
            if (!writeAllowed)
            {
                SetStatus("The write gate is locked.", true);
                return;
            }
            if (ConfirmWriteAllowed != null && !ConfirmWriteAllowed())
            {
                SetStatus("Write gate is locked. Wait for a clean pointer check.", true);
                return;
            }

            string error;
            if (!RelationshipEditor.TryWriteHistoryAll(mem, editRow.Source.Pointer,
                target.Seat, feeling, out error))
            {
                SetStatus(error, true);
                return;
            }

            for (int i = 0; i < editRow.History.Length; i++)
                editRow.History[i] = feeling;
            editRow.Latest = feeling;
            RelationshipRecord record = RelationshipEditor.FindRecord(mem,
                editRow.Source.Pointer, target.Seat);
            if (record != null && feeling >= 0 && feeling <= 3)
                editRow.LatestLevel = record.Levels[feeling];

            for (int i = 0; i < grid.Rows.Count; i++)
            {
                RelationshipHistoryRow row = grid.Rows[i].Tag as RelationshipHistoryRow;
                if (row == null || row.Source == null || editRow.Source == null) continue;
                if (row.Source.Pointer != editRow.Source.Pointer) continue;
                UpdateRow(grid.Rows[i], editRow);
                break;
            }

            grid.InvalidateColumn(1);
            SetStatus("All events set to " + FeelingNames[feeling] +
                "; counts adjusted.", false);
        }

        private void SetHistoryFeeling(int feeling)
        {
            if (editRow == null || editRow.Source == null || target == null) return;
            if (!writeAllowed)
            {
                SetStatus("The write gate is locked.", true);
                return;
            }
            if (ConfirmWriteAllowed != null && !ConfirmWriteAllowed())
            {
                SetStatus("Write gate is locked. Wait for a clean pointer check.", true);
                return;
            }

            string error;
            if (!RelationshipEditor.TryWriteHistory(mem, editRow.Source.Pointer,
                target.Seat, editEventIndex, feeling, out error))
            {
                SetStatus(error, true);
                return;
            }

            editRow.History[editEventIndex] = feeling;
            int latestIndex = editRow.Count - 1;
            int latest = latestIndex >= 0 ? editRow.History[latestIndex] : -1;
            editRow.Latest = latest >= 0 && latest <= 3 ? latest : -1;
            RelationshipRecord record = RelationshipEditor.FindRecord(mem,
                editRow.Source.Pointer, target.Seat);
            if (record != null && editRow.Latest >= 0)
                editRow.LatestLevel = record.Levels[editRow.Latest];

            for (int i = 0; i < grid.Rows.Count; i++)
            {
                RelationshipHistoryRow row = grid.Rows[i].Tag as RelationshipHistoryRow;
                if (row == null || row.Source == null || editRow.Source == null) continue;
                if (row.Source.Pointer != editRow.Source.Pointer) continue;
                UpdateRow(grid.Rows[i], editRow);
                break;
            }

            grid.InvalidateColumn(1);
            SetStatus("Event " + (editEventIndex + 1).ToString() + " set to " +
                FeelingNames[feeling] + "; counts adjusted.", false);
        }

        private static void ComputeLayout(int width, out int slot, out int gap,
            out int startX)
        {
            const int padding = 6;
            int available = Math.Max(0, width - padding * 2);
            gap = 2;
            slot = (available - gap * 29) / 30;
            if (slot < 8)
            {
                gap = 1;
                slot = (available - 29) / 30;
            }
            if (slot < 4) slot = 4;

            int actualGap = (available - slot * 30) / 29;
            if (actualGap < 1) actualGap = 1;
            if (actualGap > 3) actualGap = 3;
            gap = actualGap;

            int total = slot * 30 + gap * 29;
            startX = padding + Math.Max(0, (available - total) / 2);
        }

        private static bool TryHitHistoryCell(int localX, int width, out int index)
        {
            index = -1;
            int slot;
            int gap;
            int startX;
            ComputeLayout(width, out slot, out gap, out startX);
            if (localX < startX) return false;

            int stride = slot + gap;
            if (stride <= 0) return false;
            int position = localX - startX;
            int candidate = position / stride;
            int offset = position % stride;
            if (candidate < 0 || candidate >= 30 || offset >= slot) return false;
            index = candidate;
            return true;
        }

        private void ApplySearch()
        {
            if (grid == null || searchBox == null) return;

            string text = searchBox.Text.Trim();
            bool anyVisible = false;

            foreach (DataGridViewRow gridRow in grid.Rows)
            {
                RelationshipHistoryRow row = gridRow.Tag as RelationshipHistoryRow;
                if (row == null || row.Source == null) continue;

                string name = row.Source.Name ?? "";
                string seat = row.Source.Seat >= 0 ? row.Source.Seat.ToString() : "";
                bool visible = text.Length == 0 ||
                    name.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    seat.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;

                gridRow.Visible = visible;
                if (visible) anyVisible = true;
            }

            UpdateEmptyState(anyVisible, text.Length > 0);
            if (grid.Columns.Count > 1) grid.InvalidateColumn(1);
        }

        private void UpdateEmptyState(bool anyVisible, bool searching)
        {
            if (emptyLabel == null) return;

            string message = null;
            if (mem == null || ctx == null || target == null)
                message = "Select a character in the left list.";
            else if (target.Seat < 0 || target.Seat > 24)
                message = "This character has no class seat.";
            else if (rows.Count == 0)
                message = "No classmates are loaded.";
            else if (!anyVisible)
                message = searching
                    ? "No classmates match the search."
                    : "No classmates to show.";

            emptyLabel.Text = message ?? "";
            emptyLabel.Visible = message != null;
            if (emptyLabel.Visible) emptyLabel.BringToFront();
        }

        // ===================== painting =====================

        private void Grid_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.ColumnIndex != 1) return;

            if (e.RowIndex < 0)
            {
                e.PaintBackground(e.CellBounds, false);
                DrawHistoryHeader(e);
                e.Handled = true;
                return;
            }

            e.PaintBackground(e.CellBounds,
                (e.State & DataGridViewElementStates.Selected) != 0);

            RelationshipHistoryRow row = grid.Rows[e.RowIndex].Tag as RelationshipHistoryRow;
            if (row == null)
            {
                e.Handled = true;
                return;
            }

            Rectangle bounds = e.CellBounds;
            int slot;
            int gap;
            int startX;
            ComputeLayout(bounds.Width, out slot, out gap, out startX);

            int height = Math.Max(14, bounds.Height - 8);
            int top = bounds.Top + (bounds.Height - height) / 2;
            int highlight = highlightCombo == null ? 0 : highlightCombo.SelectedIndex - 1;

            for (int i = 0; i < 30; i++)
            {
                int x = bounds.Left + startX + i * (slot + gap);
                bool hasEvent = i < row.Count;
                int feeling = hasEvent ? row.History[i] : -1;
                bool valid = feeling >= 0 && feeling <= 3;

                Color glyphColor = Color.Gray;
                Color fill = Color.FromArgb(251, 251, 251);
                Color border = Color.FromArgb(238, 238, 238);

                if (hasEvent && valid)
                {
                    Color feelingColor = FeelingColors[feeling];
                    bool matches = highlight < 0 || highlight == feeling;
                    if (highlight >= 0 && !matches)
                    {
                        glyphColor = Color.FromArgb(195, 195, 195);
                        fill = Color.FromArgb(249, 249, 249);
                        border = Color.FromArgb(238, 238, 238);
                    }
                    else
                    {
                        glyphColor = feelingColor;
                        fill = highlight >= 0
                            ? Color.FromArgb(28, feelingColor)
                            : Color.White;
                        border = highlight >= 0
                            ? feelingColor
                            : Color.FromArgb(225, 225, 225);
                    }
                }

                using (SolidBrush brush = new SolidBrush(fill))
                    e.Graphics.FillRectangle(brush, x, top, slot, height);
                using (Pen pen = new Pen(border))
                    e.Graphics.DrawRectangle(pen, x, top, Math.Max(1, slot - 1),
                        Math.Max(1, height - 1));

                if (hasEvent)
                {
                    string glyph = valid ? FeelingGlyphs[feeling] : "?";
                    Color color = valid ? glyphColor : Color.FromArgb(190, 190, 190);
                    TextRenderer.DrawText(e.Graphics, glyph, historyFont,
                        new Rectangle(x, top, slot, height), color,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                        TextFormatFlags.NoPrefix);
                }
            }

            e.Handled = true;
        }

        private void DrawHistoryHeader(DataGridViewCellPaintingEventArgs e)
        {
            Rectangle bounds = e.CellBounds;
            Font titleFont = grid.ColumnHeadersDefaultCellStyle.Font;
            TextRenderer.DrawText(e.Graphics, "Feeling history (oldest \u2192 newest)",
                titleFont, new Rectangle(bounds.Left + 6, bounds.Top + 2,
                    Math.Max(0, bounds.Width - 12), 18),
                Color.FromArgb(40, 44, 52),
                TextFormatFlags.Left | TextFormatFlags.Top |
                TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

            int slot;
            int gap;
            int startX;
            ComputeLayout(bounds.Width, out slot, out gap, out startX);
            int top = bounds.Top + 23;
            for (int i = 0; i < 30; i++)
            {
                Rectangle numberBounds = new Rectangle(
                    bounds.Left + startX + i * (slot + gap), top, slot, 16);
                TextRenderer.DrawText(e.Graphics, (i + 1).ToString(),
                    headerNumberFont, numberBounds, Color.Gray,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Top |
                    TextFormatFlags.NoPrefix);
            }
        }

        private static string LevelName(int level)
        {
            switch (level)
            {
                case 0: return "Lots";
                case 1: return "Some";
                default: return "Low";
            }
        }
    }
}
