using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace AA2Trainer
{
    /// <summary>
    /// Incoming relationship editor: how each classmate feels about the
    /// selected character. Records are directional, so "incoming" means
    /// classmate -> selected target.
    /// </summary>
    public sealed class RelationshipControl : UserControl
    {
        public event EventHandler<CharacterSelectedEventArgs> OpenCharacterRequested;

        public Func<bool> ConfirmWriteAllowed { get; set; }

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

        private sealed class FrozenEntry
        {
            public uint SourcePointer;
            public int TargetSeat;
            public string SourceName;
            public string TargetName;
            public readonly int[] Counts = new int[4];
            public readonly int[] Points = new int[4];
            public readonly int[] Levels = new int[4];
            public int Latest;
            public int Failures;

            public FrozenEntry(RelationshipRecord record, string targetName)
            {
                SourcePointer = record.Source == null ? 0 : record.Source.Pointer;
                TargetSeat = record.TargetSeat;
                SourceName = record.Source == null ? "" : record.Source.Name;
                TargetName = targetName ?? "";
                Array.Copy(record.Counts, Counts, 4);
                Array.Copy(record.Points, Points, 4);
                Array.Copy(record.Levels, Levels, 4);
                Latest = record.Latest;
            }
        }

        private GameMemory mem;
        private GameContext ctx;
        private CharacterInfo selected;
        private NameMap names;
        private bool writeAllowed;

        private readonly List<RelationshipRecord> records =
            new List<RelationshipRecord>();
        private readonly Dictionary<string, FrozenEntry> freezes =
            new Dictionary<string, FrozenEntry>();
        private RelationshipRecord editorRecord;
        private bool dirty;
        private bool suppress;
        private bool suppressSelection;
        private bool freezing;
        private int draftLatest = -1;
        private uint selectedSourcePointer;

        private Label targetLabel;
        private Label frozenLabel;
        private Button clearFreezesButton;
        private ComboBox filterCombo;
        private TextBox searchBox;
        private DataGridView grid;
        private Label statusLabel;

        private ContextMenuStrip contextMenu;
        private RelationshipRecord contextRecord;
        private ToolStripMenuItem menuLove;
        private ToolStripMenuItem menuLike;
        private ToolStripMenuItem menuDislike;
        private ToolStripMenuItem menuHate;
        private ToolStripMenuItem menuLoveAll;
        private ToolStripMenuItem menuLikeAll;
        private ToolStripMenuItem menuDislikeAll;
        private ToolStripMenuItem menuHateAll;
        private ToolStripMenuItem menuFreezeAll;
        private ToolStripMenuItem menuUnfreezeAll;

        private SplitContainer split;
        private bool splitInitialized;

        private PictureBox portraitBox;
        private Label titleLabel;
        private Label directionLabel;
        private Label metaLabel;
        private Label summaryLabel;
        private Label windowLabel;
        private NumericUpDown[] countBoxes = new NumericUpDown[4];
        private NumericUpDown[] pointBoxes = new NumericUpDown[4];
        private ComboBox[] levelBoxes = new ComboBox[4];
        private ComboBox latestBox;
        private CheckBox freezeCheck;
        private Button applyButton;
        private Button revertButton;
        private Button openButton;

        private Timer freezeTimer;
        private PortraitImage detailPortrait;
        private uint portraitPointer;
        private uint portraitAddress;
        private uint portraitSize;

        public RelationshipControl()
        {
            DoubleBuffered = true;
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(248, 249, 251);
            Padding = new Padding(4, 2, 4, 4);

            freezeTimer = new Timer();
            freezeTimer.Interval = 100;
            freezeTimer.Tick += delegate { FreezeTick(); };

            BuildUi();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopFreezeTimer();
                freezes.Clear();
                ClearPortrait();
                if (freezeTimer != null)
                {
                    freezeTimer.Dispose();
                    freezeTimer = null;
                }
            }
            base.Dispose(disposing);
        }

        // ===================== UI =====================

        private void BuildUi()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 3;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.Margin = new Padding(0);
            Controls.Add(root);

            root.Controls.Add(BuildHeader(), 0, 0);
            root.Controls.Add(BuildFilterRow(), 0, 1);

            SplitContainer split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.Orientation = Orientation.Vertical;
            split.SplitterWidth = 5;
            split.FixedPanel = FixedPanel.None;
            split.Margin = new Padding(0);
            split.SizeChanged += delegate { InitializeSplit(); };
            split.Panel1.Controls.Add(BuildTable());
            split.Panel2.Controls.Add(BuildDetail());
            root.Controls.Add(split, 0, 2);
            this.split = split;

            UpdateHeader();
            UpdateFrozenLabels();
            SetStatus("Select a character in the left list.", false);
        }

        private void InitializeSplit()
        {
            if (splitInitialized || split == null) return;

            int width = split.Width;
            if (width < 700) return;

            const int min1 = 320;
            const int min2 = 280;
            if (width < min1 + min2 + split.SplitterWidth) return;

            try
            {
                split.Panel1MinSize = min1;
                split.Panel2MinSize = min2;
                int desired = (int)(width * 0.58);
                if (desired < min1) desired = min1;
                int maximum = width - min2 - split.SplitterWidth;
                if (desired > maximum) desired = maximum;
                split.SplitterDistance = desired;
                splitInitialized = true;
            }
            catch
            {
            }
        }

        private Control BuildHeader()
        {
            TableLayoutPanel header = new TableLayoutPanel();
            header.Dock = DockStyle.Fill;
            header.ColumnCount = 4;
            header.RowCount = 1;
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90F));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 8F));
            header.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            header.Margin = new Padding(0);

            targetLabel = new Label();
            targetLabel.Dock = DockStyle.Fill;
            targetLabel.TextAlign = ContentAlignment.MiddleLeft;
            targetLabel.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            targetLabel.ForeColor = Color.FromArgb(40, 44, 52);
            targetLabel.AutoEllipsis = true;
            targetLabel.Text = "Incoming relationship view";
            header.Controls.Add(targetLabel, 0, 0);

            frozenLabel = new Label();
            frozenLabel.AutoSize = true;
            frozenLabel.TextAlign = ContentAlignment.MiddleRight;
            frozenLabel.ForeColor = Color.DimGray;
            frozenLabel.Margin = new Padding(6, 7, 6, 0);
            frozenLabel.Text = "Frozen: 0";
            header.Controls.Add(frozenLabel, 1, 0);

            clearFreezesButton = new Button();
            clearFreezesButton.Text = "Clear all";
            clearFreezesButton.Dock = DockStyle.Fill;
            clearFreezesButton.Margin = new Padding(2, 3, 2, 3);
            clearFreezesButton.Visible = false;
            clearFreezesButton.Click += delegate
            {
                ClearFreezes("All relationship freezes cleared.");
            };
            header.Controls.Add(clearFreezesButton, 2, 0);

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

            filterCombo = new ComboBox();
            filterCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            filterCombo.Width = 110;
            filterCombo.Margin = new Padding(0, 2, 10, 0);
            filterCombo.Items.AddRange(new object[]
            {
                "All", "Positive", "Negative", "None", "Frozen"
            });
            filterCombo.SelectedIndex = 0;
            filterCombo.SelectedIndexChanged += delegate { ApplyFilter(); };
            row.Controls.Add(filterCombo);

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

        private Control BuildTable()
        {
            grid = new DataGridView();
            grid.Dock = DockStyle.Fill;
            grid.BackgroundColor = Color.White;
            grid.BorderStyle = BorderStyle.FixedSingle;
            grid.RowHeadersVisible = false;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.ReadOnly = true;
            grid.MultiSelect = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.AutoGenerateColumns = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.ColumnHeadersHeight = 26;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.RowTemplate.Height = 23;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(240, 241, 244);
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(240, 241, 244);
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.FromArgb(30, 30, 30);
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            grid.DefaultCellStyle.Font = new Font("Segoe UI", 9F);
            grid.DefaultCellStyle.SelectionBackColor = SystemColors.Highlight;
            grid.DefaultCellStyle.SelectionForeColor = SystemColors.HighlightText;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.Margin = new Padding(0);
            grid.AccessibleName = "Incoming relationships";
            TryDoubleBuffer(grid);

            AddColumn("Classmate", 30F, 100, false);
            AddColumn("Latest", 26F, 100, false);
            AddColumn("\u2665", 9F, 34, true);
            AddColumn("\u25CF", 9F, 34, true);
            AddColumn("\u25BC", 10F, 36, true);
            AddColumn("\u2715", 9F, 34, true);
            AddColumn("Count", 10F, 60, true);
            AddFrozenColumn();

            grid.SelectionChanged += Grid_SelectionChanged;
            grid.CellMouseClick += Grid_CellMouseClick;
            grid.CellMouseDown += Grid_CellMouseDown;
            grid.CellDoubleClick += delegate(object sender, DataGridViewCellEventArgs e)
            {
                if (e.RowIndex >= 0 && e.ColumnIndex != 7)
                    OpenSelectedCharacter();
            };

            BuildContextMenu();
            return grid;
        }

        private void AddFrozenColumn()
        {
            DataGridViewCheckBoxColumn column = new DataGridViewCheckBoxColumn();
            column.HeaderText = "Frozen";
            column.FillWeight = 9F;
            column.MinimumWidth = 56;
            column.SortMode = DataGridViewColumnSortMode.NotSortable;
            column.ReadOnly = true;
            column.TrueValue = true;
            column.FalseValue = false;
            column.FlatStyle = FlatStyle.Standard;
            column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            column.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            column.HeaderCell.Style.SelectionBackColor = Color.FromArgb(240, 241, 244);
            column.HeaderCell.Style.SelectionForeColor = Color.FromArgb(30, 30, 30);
            column.ToolTipText = "Freeze this relationship in-game";
            grid.Columns.Add(column);
        }

        private void BuildContextMenu()
        {
            contextMenu = new ContextMenuStrip();
            contextMenu.ShowItemToolTips = true;
            contextMenu.Opening += ContextMenu_Opening;

            menuLove = AddMenuItem("Love", delegate { ApplyFeelingToContext(0); });
            menuLike = AddMenuItem("Like", delegate { ApplyFeelingToContext(1); });
            menuDislike = AddMenuItem("Dislike", delegate { ApplyFeelingToContext(2); });
            menuHate = AddMenuItem("Hate", delegate { ApplyFeelingToContext(3); });
            menuLove.ToolTipText = "Set all counts to Love 30 and the others to 0";
            menuLike.ToolTipText = "Set all counts to Like 30 and the others to 0";
            menuDislike.ToolTipText = "Set all counts to Dislike 30 and the others to 0";
            menuHate.ToolTipText = "Set all counts to Hate 30 and the others to 0";

            contextMenu.Items.Add(new ToolStripSeparator());

            menuLoveAll = AddMenuItem("Love All", delegate { ApplyFeelingToAll(0); });
            menuLikeAll = AddMenuItem("Like All", delegate { ApplyFeelingToAll(1); });
            menuDislikeAll = AddMenuItem("Dislike All", delegate { ApplyFeelingToAll(2); });
            menuHateAll = AddMenuItem("Hate All", delegate { ApplyFeelingToAll(3); });
            menuLoveAll.ToolTipText = "Set every classmate in this view to Love";
            menuLikeAll.ToolTipText = "Set every classmate in this view to Like";
            menuDislikeAll.ToolTipText = "Set every classmate in this view to Dislike";
            menuHateAll.ToolTipText = "Set every classmate in this view to Hate";

            contextMenu.Items.Add(new ToolStripSeparator());

            menuFreezeAll = AddMenuItem("Freeze All", delegate { FreezeAll(); });
            menuUnfreezeAll = AddMenuItem("Unfreeze All", delegate { UnfreezeAll(); });
            menuFreezeAll.ToolTipText = "Freeze every classmate in this view at their current values";
            menuUnfreezeAll.ToolTipText = "Stop freezing every classmate in this view";
        }

        private ToolStripMenuItem AddMenuItem(string text, EventHandler handler)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(text);
            item.Click += handler;
            contextMenu.Items.Add(item);
            return item;
        }

        private void AddColumn(string header, float fillWeight, int minimumWidth,
            bool centered)
        {
            DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn();
            column.HeaderText = header;
            column.FillWeight = fillWeight;
            column.MinimumWidth = minimumWidth;
            column.SortMode = DataGridViewColumnSortMode.NotSortable;
            column.ReadOnly = true;
            if (header == "Classmate") column.ToolTipText = "Classmate";
            else if (header == "Latest") column.ToolTipText = "Newest feeling change";
            else if (header == "\u2665") column.ToolTipText = "Love count";
            else if (header == "\u25CF") column.ToolTipText = "Like count";
            else if (header == "\u25BC") column.ToolTipText = "Dislike count";
            else if (header == "\u2715") column.ToolTipText = "Hate count";
            else if (header == "Count") column.ToolTipText = "Total of the four LLDH counts";
            else if (header == "Frozen") column.ToolTipText = "Relationship is frozen in-game";
            if (centered)
            {
                column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                column.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            }
            if (header == "\u2665" || header == "\u25CF" ||
                header == "\u25BC" || header == "\u2715" || header == "Frozen")
            {
                column.DefaultCellStyle.Font = new Font("Segoe UI Symbol", 9.5F);
            }
            grid.Columns.Add(column);
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

        private Control BuildDetail()
        {
            Panel host = new Panel();
            host.Dock = DockStyle.Fill;
            host.AutoScroll = true;
            host.BackColor = Color.FromArgb(248, 249, 251);
            host.Padding = new Padding(8, 4, 8, 8);
            host.Margin = new Padding(0);

            TableLayoutPanel detail = new TableLayoutPanel();
            detail.Dock = DockStyle.Top;
            detail.AutoSize = true;
            detail.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            detail.ColumnCount = 1;
            detail.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            detail.RowCount = 0;
            detail.Margin = new Padding(0);
            host.Controls.Add(detail);

            // Identity
            TableLayoutPanel identity = new TableLayoutPanel();
            identity.Dock = DockStyle.Top;
            identity.AutoSize = true;
            identity.ColumnCount = 2;
            identity.RowCount = 1;
            identity.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72F));
            identity.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            identity.Margin = new Padding(0, 0, 0, 2);
            AddDetailRow(detail, identity);

            portraitBox = new PictureBox();
            portraitBox.Dock = DockStyle.Fill;
            portraitBox.SizeMode = PictureBoxSizeMode.Zoom;
            portraitBox.BorderStyle = BorderStyle.FixedSingle;
            portraitBox.BackColor = Color.FromArgb(236, 238, 242);
            portraitBox.MinimumSize = new Size(64, 64);
            portraitBox.MaximumSize = new Size(64, 64);
            portraitBox.Margin = new Padding(0, 0, 8, 0);
            identity.Controls.Add(portraitBox, 0, 0);

            TableLayoutPanel idText = new TableLayoutPanel();
            idText.Dock = DockStyle.Fill;
            idText.AutoSize = true;
            idText.ColumnCount = 1;
            idText.RowCount = 3;
            idText.Margin = new Padding(0);
            identity.Controls.Add(idText, 1, 0);

            titleLabel = new Label();
            titleLabel.Dock = DockStyle.Fill;
            titleLabel.AutoSize = true;
            titleLabel.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            titleLabel.ForeColor = Color.FromArgb(40, 44, 52);
            titleLabel.Text = "No relationship record";
            idText.Controls.Add(titleLabel, 0, 0);

            directionLabel = new Label();
            directionLabel.Dock = DockStyle.Fill;
            directionLabel.AutoSize = true;
            directionLabel.Font = new Font("Segoe UI", 8.5F);
            directionLabel.ForeColor = Color.DimGray;
            directionLabel.Text = "";
            idText.Controls.Add(directionLabel, 0, 1);

            metaLabel = new Label();
            metaLabel.Dock = DockStyle.Fill;
            metaLabel.AutoSize = true;
            metaLabel.Font = new Font("Segoe UI", 8.5F);
            metaLabel.ForeColor = Color.DimGray;
            metaLabel.Text = "";
            idText.Controls.Add(metaLabel, 0, 2);

            // Summary
            TableLayoutPanel summaryRow = new TableLayoutPanel();
            summaryRow.Dock = DockStyle.Top;
            summaryRow.AutoSize = true;
            summaryRow.ColumnCount = 2;
            summaryRow.RowCount = 1;
            summaryRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            summaryRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            summaryRow.Margin = new Padding(0, 2, 0, 2);
            AddDetailRow(detail, summaryRow);

            summaryLabel = new Label();
            summaryLabel.AutoSize = true;
            summaryLabel.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
            summaryLabel.ForeColor = Color.DimGray;
            summaryLabel.Text = "Latest: None";
            summaryRow.Controls.Add(summaryLabel, 0, 0);

            windowLabel = new Label();
            windowLabel.AutoSize = true;
            windowLabel.TextAlign = ContentAlignment.MiddleRight;
            windowLabel.Font = new Font("Segoe UI", 8.5F);
            windowLabel.ForeColor = Color.DimGray;
            windowLabel.Margin = new Padding(10, 4, 0, 0);
            windowLabel.Text = "Count \u2014";
            summaryRow.Controls.Add(windowLabel, 1, 0);

            // LLDH editor
            GroupBox group = new GroupBox();
            group.Text = "Counts, points, level";
            group.Dock = DockStyle.Top;
            group.AutoSize = true;
            group.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            group.Padding = new Padding(8, 2, 8, 4);
            group.Margin = new Padding(0, 2, 0, 2);
            AddDetailRow(detail, group);

            TableLayoutPanel table = new TableLayoutPanel();
            table.Dock = DockStyle.Top;
            table.AutoSize = true;
            table.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            table.ColumnCount = 4;
            table.RowCount = 5;
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92F));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 66F));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 66F));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88F));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
            for (int i = 0; i < 4; i++)
                table.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            table.Margin = new Padding(0);
            group.Controls.Add(table);

            AddEditorHeader(table, 0, 1, "Count");
            AddEditorHeader(table, 0, 2, "Points");
            AddEditorHeader(table, 0, 3, "Level");

            for (int f = 0; f < 4; f++)
            {
                Label feeling = new Label();
                feeling.Dock = DockStyle.Fill;
                feeling.TextAlign = ContentAlignment.MiddleLeft;
                feeling.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                feeling.ForeColor = FeelingColors[f];
                feeling.Text = FeelingGlyphs[f] + "  " + FeelingNames[f];
                feeling.Margin = new Padding(0);
                table.Controls.Add(feeling, 0, f + 1);

                NumericUpDown count = new NumericUpDown();
                count.Minimum = 0;
                count.Maximum = 30;
                count.Increment = 1;
                count.DecimalPlaces = 0;
                count.TextAlign = HorizontalAlignment.Left;
                count.Width = 58;
                count.Anchor = AnchorStyles.Left;
                count.Margin = new Padding(2, 2, 2, 2);
                count.AccessibleName = FeelingNames[f] + " count";
                count.ValueChanged += delegate { EditorChanged(); };
                table.Controls.Add(count, 1, f + 1);
                countBoxes[f] = count;

                NumericUpDown points = new NumericUpDown();
                points.Minimum = 0;
                points.Maximum = 29;
                points.Increment = 1;
                points.DecimalPlaces = 0;
                points.TextAlign = HorizontalAlignment.Left;
                points.Width = 58;
                points.Anchor = AnchorStyles.Left;
                points.Margin = new Padding(2, 2, 2, 2);
                points.AccessibleName = FeelingNames[f] + " points";
                points.ValueChanged += delegate { EditorChanged(); };
                table.Controls.Add(points, 2, f + 1);
                pointBoxes[f] = points;

                ComboBox level = new ComboBox();
                level.DropDownStyle = ComboBoxStyle.DropDownList;
                level.Width = 82;
                level.Anchor = AnchorStyles.Left;
                level.Margin = new Padding(2, 2, 2, 2);
                level.Items.AddRange(new object[] { "Lots", "Some", "Low" });
                level.SelectedIndex = 2;
                level.AccessibleName = FeelingNames[f] + " level";
                level.SelectedIndexChanged += delegate { EditorChanged(); };
                table.Controls.Add(level, 3, f + 1);
                levelBoxes[f] = level;
            }

            // Latest change
            FlowLayoutPanel latestRow = new FlowLayoutPanel();
            latestRow.Dock = DockStyle.Top;
            latestRow.AutoSize = true;
            latestRow.WrapContents = false;
            latestRow.Margin = new Padding(0, 2, 0, 2);
            latestRow.Padding = new Padding(0, 2, 0, 2);
            AddDetailRow(detail, latestRow);

            Label latestLabel = new Label();
            latestLabel.Text = "Latest change";
            latestLabel.AutoSize = true;
            latestLabel.Margin = new Padding(0, 5, 6, 0);
            latestRow.Controls.Add(latestLabel);

            latestBox = new ComboBox();
            latestBox.DropDownStyle = ComboBoxStyle.DropDownList;
            latestBox.Width = 120;
            latestBox.Margin = new Padding(0, 2, 0, 0);
            latestBox.AccessibleName = "Latest change";
            for (int f = 0; f < 4; f++)
                latestBox.Items.Add(FeelingGlyphs[f] + " " + FeelingNames[f]);
            latestBox.SelectedIndexChanged += delegate { LatestChanged(); };
            latestRow.Controls.Add(latestBox);

            // Freeze
            freezeCheck = new CheckBox();
            freezeCheck.Text = "Freeze this relationship";
            freezeCheck.AutoSize = true;
            freezeCheck.Margin = new Padding(0, 3, 0, 0);
            freezeCheck.AccessibleName = "Freeze this relationship";
            freezeCheck.CheckedChanged += delegate { FreezeChanged(); };
            AddDetailRow(detail, freezeCheck);

            Label freezeHint = new Label();
            freezeHint.AutoSize = true;
            freezeHint.MaximumSize = new Size(480, 0);
            freezeHint.Font = new Font("Segoe UI", 8F);
            freezeHint.ForeColor = Color.Gray;
            freezeHint.Text = "Restores these counts, points, levels, and latest entry in-game every 100 ms.";
            freezeHint.Margin = new Padding(0, 0, 0, 2);
            AddDetailRow(detail, freezeHint);

            statusLabel = new Label();
            statusLabel.AutoSize = true;
            statusLabel.MaximumSize = new Size(480, 0);
            statusLabel.ForeColor = Color.DimGray;
            statusLabel.Margin = new Padding(0, 2, 0, 2);
            AddDetailRow(detail, statusLabel);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Top;
            buttons.AutoSize = true;
            buttons.WrapContents = false;
            buttons.Margin = new Padding(0, 2, 0, 0);
            AddDetailRow(detail, buttons);

            applyButton = MakeButton("Apply", 78);
            applyButton.Click += delegate { ApplyChanges(); };
            buttons.Controls.Add(applyButton);

            revertButton = MakeButton("Revert", 78);
            revertButton.Click += delegate { RevertChanges(); };
            buttons.Controls.Add(revertButton);

            openButton = MakeButton("Open character", 120);
            openButton.Click += delegate { OpenSelectedCharacter(); };
            buttons.Controls.Add(openButton);

            return host;
        }

        private static void AddEditorHeader(TableLayoutPanel table, int row, int column,
            string text)
        {
            Label label = new Label();
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.BottomLeft;
            label.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            label.ForeColor = Color.Gray;
            label.Text = text;
            label.Margin = new Padding(2, 0, 2, 0);
            table.Controls.Add(label, column, row);
        }

        private static Button MakeButton(string text, int width)
        {
            Button button = new Button();
            button.Text = text;
            button.Width = width;
            button.Height = 27;
            button.Margin = new Padding(0, 0, 6, 0);
            return button;
        }

        private static void AddDetailRow(TableLayoutPanel detail, Control control)
        {
            int row = detail.RowCount;
            detail.RowCount = row + 1;
            detail.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            detail.Controls.Add(control, 0, row);
        }

        // ===================== target / table =====================

        public void SetTarget(GameMemory memory, GameContext context,
            CharacterInfo target, NameMap nameMap, bool canWrite)
        {
            if (memory == null || context == null || target == null ||
                !memory.IsAttached)
            {
                Clear();
                return;
            }

            bool targetChanged = selected == null ||
                selected.Pointer != target.Pointer;
            mem = memory;
            ctx = context;
            selected = target;
            names = nameMap;
            SetWriteAllowed(canWrite);

            if (targetChanged)
            {
                selectedSourcePointer = 0;
                dirty = false;
                editorRecord = null;
            }

            UpdateHeader();

            if (target.Seat < 0 || target.Seat > 24)
            {
                suppressSelection = true;
                grid.Rows.Clear();
                suppressSelection = false;
                records.Clear();
                LoadEditor(null, false);
                SetStatus("This character has no class seat, so relationships cannot be read.",
                    true);
                return;
            }

            RefreshTable(targetChanged);

            if (targetChanged || grid.CurrentRow == null)
                SelectFirstVisibleRow();

            if (!dirty && (targetChanged || !EditorHasFocus()))
                LoadSelectedRow(targetChanged);
        }

        private bool EditorHasFocus()
        {
            if (countBoxes != null)
            {
                for (int f = 0; f < 4; f++)
                {
                    if (countBoxes[f] != null && countBoxes[f].ContainsFocus) return true;
                    if (pointBoxes[f] != null && pointBoxes[f].ContainsFocus) return true;
                    if (levelBoxes[f] != null &&
                        (levelBoxes[f].ContainsFocus || levelBoxes[f].DroppedDown)) return true;
                }
            }
            return latestBox != null &&
                (latestBox.ContainsFocus || latestBox.DroppedDown);
        }

        public void SetWriteAllowed(bool allowed)
        {
            if (writeAllowed == allowed)
            {
                UpdateEditorEnabled();
                return;
            }

            writeAllowed = allowed;
            if (!allowed && freezes.Count > 0)
                ClearFreezes("Freezes stopped: write gate locked.");

            UpdateEditorEnabled();
        }

        public void Clear()
        {
            StopFreezeTimer();
            freezes.Clear();
            records.Clear();
            editorRecord = null;
            selected = null;
            ctx = null;
            mem = null;
            selectedSourcePointer = 0;
            dirty = false;

            suppress = true;
            suppressSelection = true;
            if (grid != null) grid.Rows.Clear();
            suppressSelection = false;
            if (countBoxes != null)
            {
                for (int f = 0; f < 4; f++)
                {
                    if (countBoxes[f] != null) countBoxes[f].Value = 0;
                    if (pointBoxes[f] != null) pointBoxes[f].Value = 0;
                    if (levelBoxes[f] != null)
                    {
                        levelBoxes[f].SelectedIndex = 2;
                        levelBoxes[f].Enabled = false;
                    }
                }
            }
            if (latestBox != null)
            {
                latestBox.SelectedIndex = -1;
                latestBox.Enabled = false;
            }
            if (freezeCheck != null)
            {
                freezeCheck.Checked = false;
                freezeCheck.Enabled = false;
            }
            suppress = false;

            ClearPortrait();
            if (titleLabel != null) titleLabel.Text = "No relationship record";
            if (directionLabel != null) directionLabel.Text = "";
            if (metaLabel != null) metaLabel.Text = "";
            if (summaryLabel != null)
            {
                summaryLabel.Text = "Latest: None";
                summaryLabel.ForeColor = Color.DimGray;
            }
            if (windowLabel != null) windowLabel.Text = "Count \u2014";
            UpdateHeader();
            UpdateFrozenLabels();
            UpdateEditorEnabled();
            SetStatus("Select a character in the left list.", false);
        }

        private void UpdateHeader()
        {
            if (targetLabel == null) return;

            if (selected == null)
            {
                targetLabel.Text = "Incoming relationship view";
                return;
            }

            string seat = selected.Seat >= 0 ? selected.Seat.ToString("D2") + " " : "";
            string name = string.IsNullOrEmpty(selected.Name) ? "(unnamed)" : selected.Name;
            targetLabel.Text = "Incoming \u00B7 target: " + seat + name +
                (selected.IsPlayer ? " \u00B7 PC" : "");
        }

        private void RefreshTable(bool targetChanged)
        {
            records.Clear();
            if (mem == null || ctx == null || selected == null ||
                selected.Seat < 0) return;

            List<RelationshipRecord> next = RelationshipEditor.ReadIncoming(mem, ctx, selected);
            records.AddRange(next);

            bool rebuild = targetChanged || grid.Rows.Count != records.Count;
            if (!rebuild)
            {
                for (int i = 0; i < records.Count; i++)
                {
                    RelationshipRecord existing = grid.Rows[i].Tag as RelationshipRecord;
                    if (existing == null || existing.Source == null ||
                        records[i].Source == null ||
                        existing.Source.Pointer != records[i].Source.Pointer)
                    {
                        rebuild = true;
                        break;
                    }
                }
            }

            if (rebuild)
            {
                suppressSelection = true;
                grid.Rows.Clear();
                foreach (RelationshipRecord record in records)
                {
                    int index = grid.Rows.Add();
                    UpdateRow(grid.Rows[index], record);
                }
                suppressSelection = false;

                if (selectedSourcePointer != 0)
                    SelectRowByPointer(selectedSourcePointer);
            }
            else
            {
                for (int i = 0; i < records.Count; i++)
                    UpdateRow(grid.Rows[i], records[i]);
            }

            ApplyFilter();

            if (grid.CurrentRow == null && grid.Rows.Count > 0)
                SelectFirstVisibleRow();
        }

        private void UpdateRow(DataGridViewRow row, RelationshipRecord record)
        {
            row.Tag = record;
            if (record == null || record.Source == null) return;

            CharacterInfo source = record.Source;
            string seat = source.Seat >= 0 ? source.Seat.ToString("D2") + " " : "";
            row.Cells[0].Value = seat + (string.IsNullOrEmpty(source.Name) ? "(unnamed)" : source.Name);

            if (!record.HasRecord)
            {
                row.Cells[1].Value = "No record yet";
                row.Cells[1].Style.ForeColor = Color.Gray;
                for (int i = 2; i <= 6; i++)
                {
                    row.Cells[i].Value = "\u2014";
                    row.Cells[i].Style.ForeColor = Color.Gray;
                }
                row.Cells[7].Value = false;
                return;
            }

            int latest = record.Latest;
            if (latest < 0)
            {
                row.Cells[1].Value = "None";
                row.Cells[1].Style.ForeColor = Color.Gray;
            }
            else
            {
                row.Cells[1].Value = FeelingGlyphs[latest] + " " + FeelingNames[latest] +
                    " \u00B7 " + LevelName(record.Levels[latest]);
                row.Cells[1].Style.ForeColor = FeelingColors[latest];
            }

            for (int f = 0; f < 4; f++)
            {
                int cell = 2 + f;
                row.Cells[cell].Value = record.Counts[f].ToString();
                row.Cells[cell].Style.ForeColor = record.Counts[f] > 0
                    ? FeelingColors[f] : Color.Gray;
            }

            row.Cells[6].Value = record.SumCounts.ToString() + " / 30";
            row.Cells[6].Style.ForeColor = record.SumCounts > 30
                ? Color.DarkOrange : Color.DimGray;

            row.Cells[7].Value = freezes.ContainsKey(Key(record.Source.Pointer,
                record.TargetSeat));
        }

        private void ApplyFilter()
        {
            if (grid == null || filterCombo == null || searchBox == null) return;

            string text = searchBox.Text.Trim();
            int filter = filterCombo.SelectedIndex;
            bool selectedVisible = false;
            bool anyVisible = false;

            foreach (DataGridViewRow row in grid.Rows)
            {
                RelationshipRecord record = row.Tag as RelationshipRecord;
                if (record == null || record.Source == null) continue;

                bool visible = MatchesFilter(record, filter, text);
                row.Visible = visible;
                if (visible) anyVisible = true;
                if (visible && row.Selected) selectedVisible = true;
            }

            if (!anyVisible && !dirty)
            {
                LoadEditor(null, false);
                SetStatus(records.Count == 0
                    ? "No classmates are loaded."
                    : "No relationships match the filter.", false);
                return;
            }

            if (!selectedVisible && !dirty)
            {
                DataGridViewRow first = FirstVisibleRow();
                if (first != null)
                {
                    suppressSelection = true;
                    grid.ClearSelection();
                    first.Selected = true;
                    grid.CurrentCell = first.Cells[0];
                    suppressSelection = false;
                    LoadSelectedRow(false);
                }
            }
        }

        private bool MatchesFilter(RelationshipRecord record, int filter, string text)
        {
            if (filter == 4 && !freezes.ContainsKey(Key(record.Source.Pointer,
                record.TargetSeat)))
                return false;

            if (filter == 1)
            {
                if (!record.HasRecord || record.Latest < 0 || record.Latest > 1)
                    return false;
            }
            else if (filter == 2)
            {
                if (!record.HasRecord || record.Latest < 2)
                    return false;
            }
            else if (filter == 3)
            {
                if (record.HasRecord && record.Latest >= 0) return false;
            }

            if (text.Length == 0) return true;

            string name = record.Source.Name ?? "";
            if (name.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            string seat = record.Source.Seat >= 0 ? record.Source.Seat.ToString() : "";
            return seat.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void SelectFirstVisibleRow()
        {
            DataGridViewRow first = FirstVisibleRow();
            if (first == null)
            {
                LoadEditor(null, false);
                return;
            }

            suppressSelection = true;
            grid.ClearSelection();
            first.Selected = true;
            grid.CurrentCell = first.Cells[0];
            suppressSelection = false;
            LoadSelectedRow(false);
        }

        private DataGridViewRow FirstVisibleRow()
        {
            foreach (DataGridViewRow row in grid.Rows)
                if (row.Visible) return row;
            return null;
        }

        private bool SelectRowByPointer(uint pointer)
        {
            foreach (DataGridViewRow row in grid.Rows)
            {
                RelationshipRecord record = row.Tag as RelationshipRecord;
                if (record == null || record.Source == null) continue;
                if (record.Source.Pointer != pointer) continue;

                suppressSelection = true;
                grid.ClearSelection();
                row.Selected = true;
                grid.CurrentCell = row.Cells[0];
                suppressSelection = false;
                return true;
            }
            return false;
        }

        private void Grid_SelectionChanged(object sender, EventArgs e)
        {
            if (suppressSelection) return;

            DataGridViewRow row = grid.CurrentRow;
            if (row == null) return;

            RelationshipRecord record = row.Tag as RelationshipRecord;
            if (record == null || record.Source == null) return;

            if (dirty && editorRecord != null &&
                editorRecord.Source != null &&
                record.Source.Pointer != editorRecord.Source.Pointer)
            {
                SelectRowByPointer(editorRecord.Source.Pointer);
                SetStatus("Apply or revert changes before selecting another classmate.", true);
                return;
            }

            if (dirty && editorRecord != null && editorRecord.Source != null &&
                record.Source.Pointer == editorRecord.Source.Pointer)
                return;

            selectedSourcePointer = record.Source.Pointer;
            LoadEditor(record, true);
        }

        private void LoadSelectedRow(bool announce)
        {
            DataGridViewRow row = grid.CurrentRow;
            if (row == null)
            {
                LoadEditor(null, false);
                return;
            }

            RelationshipRecord record = row.Tag as RelationshipRecord;
            if (record == null || record.Source == null)
            {
                LoadEditor(null, false);
                return;
            }

            selectedSourcePointer = record.Source.Pointer;
            LoadEditor(record, announce);
        }

        private void Grid_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || e.RowIndex < 0 || e.ColumnIndex != 7)
                return;

            RelationshipRecord record = grid.Rows[e.RowIndex].Tag as RelationshipRecord;
            if (record == null || record.Source == null) return;
            if (!record.HasRecord)
            {
                SetStatus("No relationship record to freeze.", true);
                UpdateRowFrozenMarks();
                return;
            }

            bool frozen = freezes.ContainsKey(Key(record.Source.Pointer, record.TargetSeat));
            if (!frozen && dirty)
            {
                SetStatus("Apply or revert changes before freezing another relationship.", true);
                UpdateRowFrozenMarks();
                return;
            }
            SetFrozen(record, !frozen);
        }

        private void Grid_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right || e.RowIndex < 0) return;
            if (dirty)
            {
                SetStatus("Apply or revert changes before using the context menu.", true);
                return;
            }

            RelationshipRecord record = grid.Rows[e.RowIndex].Tag as RelationshipRecord;
            if (record == null || record.Source == null) return;

            SelectRowByPointer(record.Source.Pointer);
            LoadSelectedRow(true);
            contextRecord = record;

            if (contextMenu != null)
                contextMenu.Show(grid, grid.PointToClient(Cursor.Position));
        }

        private void ContextMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            bool gate = writeAllowed && mem != null && mem.IsAttached && !dirty;
            bool hasRecord = contextRecord != null && contextRecord.HasRecord;
            bool anyRecord = false;
            bool anyFrozen = false;

            foreach (RelationshipRecord record in records)
            {
                if (record == null || !record.HasRecord || record.Source == null) continue;
                anyRecord = true;
                if (freezes.ContainsKey(Key(record.Source.Pointer, record.TargetSeat)))
                    anyFrozen = true;
            }

            if (menuLove != null) menuLove.Enabled = gate && hasRecord;
            if (menuLike != null) menuLike.Enabled = gate && hasRecord;
            if (menuDislike != null) menuDislike.Enabled = gate && hasRecord;
            if (menuHate != null) menuHate.Enabled = gate && hasRecord;
            if (menuLoveAll != null) menuLoveAll.Enabled = gate && anyRecord;
            if (menuLikeAll != null) menuLikeAll.Enabled = gate && anyRecord;
            if (menuDislikeAll != null) menuDislikeAll.Enabled = gate && anyRecord;
            if (menuHateAll != null) menuHateAll.Enabled = gate && anyRecord;
            if (menuFreezeAll != null) menuFreezeAll.Enabled = gate && anyRecord;
            if (menuUnfreezeAll != null) menuUnfreezeAll.Enabled = anyFrozen;
        }

        private bool GateForWrite()
        {
            if (mem == null || !mem.IsAttached || !writeAllowed)
            {
                SetStatus("The game is not attached or the write gate is locked.", true);
                return false;
            }
            if (ConfirmWriteAllowed != null && !ConfirmWriteAllowed())
            {
                SetStatus("Write gate is locked. Wait for a clean pointer check.", true);
                return false;
            }
            return true;
        }

        private string TargetLabel()
        {
            if (selected == null) return "";
            string seat = selected.Seat >= 0 ? selected.Seat.ToString("D2") + " " : "";
            return seat + (string.IsNullOrEmpty(selected.Name) ? "(unnamed)" : selected.Name);
        }

        private static string CharacterLabel(CharacterInfo info)
        {
            if (info == null) return "";
            string seat = info.Seat >= 0 ? info.Seat.ToString("D2") + " " : "";
            return seat + (string.IsNullOrEmpty(info.Name) ? "(unnamed)" : info.Name);
        }

        private void ApplyFeelingToContext(int feeling)
        {
            if (contextRecord == null || contextRecord.Source == null) return;
            if (!GateForWrite()) return;

            string error;
            if (!ApplyFeeling(contextRecord, feeling, out error))
            {
                SetStatus(error, true);
                return;
            }

            string note = contextRecord.HistoryCount > 0
                ? "" : " (no history entry to update)";
            SetStatus("Set " + CharacterLabel(contextRecord.Source) + " to " +
                FeelingNames[feeling] + note + ".", false);
            RefreshTable(false);
            if (!dirty) LoadSelectedRow(true);
        }

        private void ApplyFeelingToAll(int feeling)
        {
            if (!GateForWrite()) return;

            int recordsWithData = 0;
            foreach (RelationshipRecord record in records)
            {
                if (record != null && record.HasRecord && record.Source != null)
                    recordsWithData++;
            }
            if (recordsWithData == 0)
            {
                SetStatus("There are no relationship records to update.", true);
                return;
            }

            string target = TargetLabel();
            string question = "Set every classmate's feeling toward " + target +
                " to " + FeelingNames[feeling] + "?\r\n\r\n" +
                "This sets that feeling's count to 30, clears the other counts and all points, " +
                "sets the chosen level to Lots and the others to Low, and updates the newest " +
                "history entry where one exists.";
            if (MessageBox.Show(this, question, "Set all feelings",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            int changed = 0;
            string failure = null;
            foreach (RelationshipRecord record in records)
            {
                if (record == null || !record.HasRecord || record.Source == null) continue;
                string error;
                if (!ApplyFeeling(record, feeling, out error))
                {
                    failure = error;
                    break;
                }
                changed++;
            }

            RefreshTable(false);
            if (!dirty) LoadSelectedRow(true);

            if (failure != null)
                SetStatus("Set " + changed.ToString() + " relationships, then failed: " + failure, true);
            else
                SetStatus("Set " + changed.ToString() + " relationships toward " + target +
                    " to " + FeelingNames[feeling] + ".", false);
        }

        private bool ApplyFeeling(RelationshipRecord record, int feeling, out string error)
        {
            error = null;
            if (mem == null || record == null || record.Source == null)
            {
                error = "No relationship record is available.";
                return false;
            }

            RelationshipRecord current = RelationshipEditor.FindRecord(mem,
                record.Source.Pointer, record.TargetSeat);
            if (current == null)
            {
                error = "The relationship record is no longer available.";
                return false;
            }
            current.Source = record.Source;

            int[] counts = new int[4];
            int[] points = new int[4];
            int[] levels = new int[4];
            for (int f = 0; f < 4; f++) levels[f] = 2;
            counts[feeling] = 30;
            levels[feeling] = 0;

            int latest = current.HistoryCount > 0 ? feeling : -1;
            bool writeLatest = current.HistoryCount > 0;

            if (!RelationshipEditor.TryWrite(mem, current, counts, points, levels,
                latest, writeLatest, out error))
                return false;

            UpdateFrozenSnapshot(current, counts, points, levels, latest);
            return true;
        }

        private void UpdateFrozenSnapshot(RelationshipRecord record, int[] counts,
            int[] points, int[] levels, int latest)
        {
            if (record == null || record.Source == null) return;

            string key = Key(record.Source.Pointer, record.TargetSeat);
            FrozenEntry entry;
            if (!freezes.TryGetValue(key, out entry)) return;

            RelationshipRecord intended = record.Clone();
            for (int f = 0; f < 4; f++)
            {
                intended.Counts[f] = counts[f];
                intended.Points[f] = points[f];
                intended.Levels[f] = levels[f];
            }
            intended.Latest = latest;
            freezes[key] = new FrozenEntry(intended, entry.TargetName);
        }

        private void FreezeAll()
        {
            if (!GateForWrite()) return;

            int count = 0;
            foreach (RelationshipRecord record in records)
            {
                if (record == null || !record.HasRecord || record.Source == null) continue;
                RelationshipRecord current = RelationshipEditor.FindRecord(mem,
                    record.Source.Pointer, record.TargetSeat);
                if (current == null) continue;

                current.Source = record.Source;
                string key = Key(record.Source.Pointer, record.TargetSeat);
                freezes[key] = new FrozenEntry(current,
                    selected == null ? "" : selected.Name);
                count++;
            }

            if (count == 0)
            {
                SetStatus("There are no relationship records to freeze.", true);
                return;
            }

            StartFreezeTimer();
            UpdateFrozenLabels();
            UpdateRowFrozenMarks();
            UpdateEditorEnabled();
            SetStatus("Froze " + count.ToString() + " relationships.", false);
        }

        private void UnfreezeAll()
        {
            int count = 0;
            foreach (RelationshipRecord record in records)
            {
                if (record == null || record.Source == null) continue;
                if (freezes.Remove(Key(record.Source.Pointer, record.TargetSeat)))
                    count++;
            }

            StopFreezeTimerIfEmpty();
            UpdateFrozenLabels();
            UpdateRowFrozenMarks();
            UpdateEditorEnabled();
            SetStatus(count == 0
                ? "No relationships in this view are frozen."
                : "Unfroze " + count.ToString() + " relationships.", false);
        }

        // ===================== editor =====================

        private void LoadEditor(RelationshipRecord record, bool announce)
        {
            editorRecord = record == null ? null : record.Clone();

            suppress = true;
            if (editorRecord == null || !editorRecord.HasRecord)
            {
                for (int f = 0; f < 4; f++)
                {
                    countBoxes[f].Value = 0;
                    pointBoxes[f].Value = 0;
                    levelBoxes[f].SelectedIndex = 2;
                    countBoxes[f].Enabled = false;
                    pointBoxes[f].Enabled = false;
                    levelBoxes[f].Enabled = false;
                }
                latestBox.SelectedIndex = -1;
                latestBox.Enabled = false;
                freezeCheck.Checked = false;
                freezeCheck.Enabled = false;
                titleLabel.Text = "No relationship record";
                directionLabel.Text = "";
                metaLabel.Text = "";
                summaryLabel.Text = "Latest: None";
                summaryLabel.ForeColor = Color.DimGray;
                windowLabel.Text = "Count \u2014";
                ClearPortrait();
                draftLatest = -1;
                dirty = false;
                UpdateEditorEnabled();
                suppress = false;
                if (announce) SetStatus("No relationship record for this pair yet.", false);
                return;
            }

            CharacterInfo source = editorRecord.Source;
            string sourceSeat = source.Seat >= 0 ? source.Seat.ToString("D2") + " " : "";
            string targetSeat = selected.Seat >= 0 ? selected.Seat.ToString("D2") + " " : "";
            titleLabel.Text = sourceSeat + (string.IsNullOrEmpty(source.Name)
                ? "(unnamed)" : source.Name);
            directionLabel.Text = sourceSeat + (string.IsNullOrEmpty(source.Name)
                ? "(unnamed)" : source.Name) + " \u2192 " + targetSeat +
                (string.IsNullOrEmpty(selected.Name) ? "(unnamed)" : selected.Name);
            metaLabel.Text = PersonalityFor(source);

            for (int f = 0; f < 4; f++)
            {
                countBoxes[f].Value = Clamp(editorRecord.Counts[f], 0, 30);
                pointBoxes[f].Value = Clamp(editorRecord.Points[f], 0, 29);
                levelBoxes[f].SelectedIndex = Clamp(editorRecord.Levels[f], 0, 2);
                countBoxes[f].Enabled = true;
                pointBoxes[f].Enabled = true;
                levelBoxes[f].Enabled = true;
            }

            draftLatest = editorRecord.Latest;
            if (editorRecord.HistoryCount > 0 && editorRecord.Latest >= 0)
            {
                latestBox.SelectedIndex = editorRecord.Latest;
                latestBox.Enabled = true;
            }
            else
            {
                latestBox.SelectedIndex = -1;
                latestBox.Enabled = false;
            }

            string key = Key(source.Pointer, editorRecord.TargetSeat);
            freezeCheck.Checked = freezes.ContainsKey(key);
            freezeCheck.Enabled = writeAllowed;
            dirty = false;
            UpdateSummary();
            UpdateEditorEnabled();
            UpdatePortrait(source);
            suppress = false;

            if (announce) SetStatus("Loaded " + titleLabel.Text + ".", false);
            else SetStatus(freezes.ContainsKey(key)
                ? "Frozen \u00B7 restoring in-game values."
                : "Live values are refreshing.", false);
        }

        private void EditorChanged()
        {
            if (suppress) return;
            dirty = true;
            UpdateSummary();
            UpdateEditorEnabled();
            SetStatus("Unsaved changes.", false);
        }

        private void LatestChanged()
        {
            if (suppress) return;
            if (editorRecord == null || !editorRecord.HasRecord) return;
            if (editorRecord.HistoryCount <= 0) return;

            int next = latestBox.SelectedIndex;
            if (next < 0 || next > 3) return;

            int previous = draftLatest;
            if (previous == next) return;

            bool priorSuppress = suppress;
            suppress = true;
            if (previous >= 0 && previous < 4)
            {
                if (countBoxes[previous].Value > 0)
                    countBoxes[previous].Value -= 1;
            }
            if (countBoxes[next].Value < 30)
                countBoxes[next].Value += 1;
            suppress = priorSuppress;

            draftLatest = next;
            dirty = true;
            UpdateSummary();
            UpdateEditorEnabled();
            SetStatus("Latest change updated; counts adjusted by one.", false);
        }

        private void UpdateSummary()
        {
            if (summaryLabel == null) return;

            if (editorRecord == null || !editorRecord.HasRecord)
            {
                summaryLabel.Text = "Latest: None";
                summaryLabel.ForeColor = Color.DimGray;
                windowLabel.Text = "Count \u2014";
                windowLabel.ForeColor = Color.DimGray;
                return;
            }

            int latest = latestBox.Enabled ? latestBox.SelectedIndex : -1;
            if (latest < 0 || latest > 3)
            {
                summaryLabel.Text = "Latest: None";
                summaryLabel.ForeColor = Color.DimGray;
            }
            else
            {
                summaryLabel.Text = "Latest: " + FeelingGlyphs[latest] + " " +
                    FeelingNames[latest] + " \u00B7 " +
                    LevelName(levelBoxes[latest].SelectedIndex);
                summaryLabel.ForeColor = FeelingColors[latest];
            }

            int sum = 0;
            for (int f = 0; f < 4; f++)
                sum += (int)countBoxes[f].Value;

            windowLabel.Text = "Count " + sum.ToString() + " / 30";
            windowLabel.ForeColor = sum > 30 ? Color.DarkOrange : Color.DimGray;
        }

        private void UpdateEditorEnabled()
        {
            bool hasRecord = editorRecord != null && editorRecord.HasRecord;
            bool hasSource = editorRecord != null && editorRecord.Source != null;

            if (countBoxes != null)
            {
                for (int f = 0; f < 4; f++)
                {
                    if (countBoxes[f] != null) countBoxes[f].Enabled = hasRecord;
                    if (pointBoxes[f] != null) pointBoxes[f].Enabled = hasRecord;
                    if (levelBoxes[f] != null) levelBoxes[f].Enabled = hasRecord;
                }
            }

            if (latestBox != null)
                latestBox.Enabled = hasRecord && editorRecord.HistoryCount > 0;
            if (freezeCheck != null)
            {
                freezeCheck.Enabled = hasRecord && writeAllowed;
                if (!hasRecord) freezeCheck.Checked = false;
            }
            if (applyButton != null)
                applyButton.Enabled = hasRecord && writeAllowed && dirty;
            if (revertButton != null)
                revertButton.Enabled = hasRecord && dirty;
            if (openButton != null)
                openButton.Enabled = hasSource;
        }

        private void ApplyChanges()
        {
            if (!writeAllowed || mem == null || !mem.IsAttached)
            {
                SetStatus("The game is not attached or the write gate is locked.", true);
                return;
            }
            if (editorRecord == null || !editorRecord.HasRecord)
            {
                SetStatus("No relationship record is loaded.", true);
                return;
            }
            if (ConfirmWriteAllowed != null && !ConfirmWriteAllowed())
            {
                SetStatus("Write gate is locked. Wait for a clean pointer check.", true);
                return;
            }

            int[] counts = ReadCounts();
            int[] points = ReadPoints();
            int[] levels = ReadLevels();
            int latest = latestBox.Enabled ? latestBox.SelectedIndex : -1;
            bool latestChanged = latest != editorRecord.Latest;

            string error;
            if (!RelationshipEditor.TryWrite(mem, editorRecord.Source.Pointer,
                editorRecord.TargetSeat, counts, points, levels, latest, latestChanged,
                out error))
            {
                SetStatus(error, true);
                return;
            }

            string key = Key(editorRecord.Source.Pointer, editorRecord.TargetSeat);
            if (freezes.ContainsKey(key))
            {
                RelationshipRecord intended = editorRecord.Clone();
                for (int f = 0; f < 4; f++)
                {
                    intended.Counts[f] = counts[f];
                    intended.Points[f] = points[f];
                    intended.Levels[f] = levels[f];
                }
                intended.Latest = latest;
                freezes[key] = new FrozenEntry(intended,
                    selected == null ? "" : selected.Name);
                UpdateFrozenLabels();
                UpdateRowFrozenMarks();
            }

            dirty = false;
            RelationshipRecord fresh = RelationshipEditor.FindRecord(mem,
                editorRecord.Source.Pointer, editorRecord.TargetSeat);
            if (fresh != null)
            {
                fresh.Source = editorRecord.Source;
                LoadEditor(fresh, false);
            }
            else
            {
                for (int f = 0; f < 4; f++)
                {
                    editorRecord.Counts[f] = counts[f];
                    editorRecord.Points[f] = points[f];
                    editorRecord.Levels[f] = levels[f];
                }
                editorRecord.Latest = latest;
                UpdateSummary();
                UpdateEditorEnabled();
            }

            RefreshTable(false);
            SetStatus("Saved relationship values.", false);
        }

        private void RevertChanges()
        {
            if (editorRecord == null) return;
            RelationshipRecord baseline = editorRecord.Clone();
            LoadEditor(baseline, false);
            dirty = false;
            UpdateEditorEnabled();
            SetStatus("Reverted to the last loaded values.", false);
        }

        private void OpenSelectedCharacter()
        {
            if (editorRecord == null || editorRecord.Source == null) return;
            if (dirty)
            {
                SetStatus("Apply or revert changes before opening another character.", true);
                return;
            }
            if (OpenCharacterRequested != null)
                OpenCharacterRequested(this, new CharacterSelectedEventArgs(editorRecord.Source));
        }

        private int[] ReadCounts()
        {
            int[] values = new int[4];
            for (int f = 0; f < 4; f++) values[f] = (int)countBoxes[f].Value;
            return values;
        }

        private int[] ReadPoints()
        {
            int[] values = new int[4];
            for (int f = 0; f < 4; f++) values[f] = (int)pointBoxes[f].Value;
            return values;
        }

        private int[] ReadLevels()
        {
            int[] values = new int[4];
            for (int f = 0; f < 4; f++) values[f] = levelBoxes[f].SelectedIndex;
            return values;
        }

        // ===================== freeze =====================

        private void FreezeChanged()
        {
            if (suppress) return;
            if (editorRecord == null || !editorRecord.HasRecord)
            {
                SetFreezeChecked(false);
                return;
            }

            bool want = freezeCheck.Checked;
            if (want && dirty)
            {
                SetFreezeChecked(false);
                SetStatus("Apply or revert changes before freezing.", true);
                return;
            }

            if (!SetFrozen(editorRecord, want))
                SetFreezeChecked(false);
        }

        private bool SetFrozen(RelationshipRecord record, bool frozen)
        {
            if (record == null || record.Source == null) return false;
            if (!record.HasRecord)
            {
                SetStatus("No relationship record to freeze.", true);
                UpdateRowFrozenMarks();
                return false;
            }

            string key = Key(record.Source.Pointer, record.TargetSeat);
            if (frozen)
            {
                if (freezes.ContainsKey(key)) return true;
                if (!writeAllowed)
                {
                    SetStatus("The write gate is locked.", true);
                    UpdateRowFrozenMarks();
                    return false;
                }
                if (ConfirmWriteAllowed != null && !ConfirmWriteAllowed())
                {
                    SetStatus("Write gate is locked. Wait for a clean pointer check.", true);
                    UpdateRowFrozenMarks();
                    return false;
                }

                freezes[key] = new FrozenEntry(record,
                    selected == null ? "" : selected.Name);
                StartFreezeTimer();
                SetStatus("Frozen " + CharacterLabel(record.Source) +
                    ". Restoring every 100 ms.", false);
            }
            else
            {
                if (!freezes.Remove(key)) return true;
                StopFreezeTimerIfEmpty();
                SetStatus("Unfroze " + CharacterLabel(record.Source) + ".", false);
            }

            UpdateFrozenLabels();
            UpdateRowFrozenMarks();
            UpdateEditorEnabled();
            return true;
        }

        private void SetFreezeChecked(bool value)
        {
            suppress = true;
            freezeCheck.Checked = value;
            suppress = false;
        }

        private void FreezeTick()
        {
            if (freezing) return;
            if (freezes.Count == 0)
            {
                StopFreezeTimer();
                return;
            }
            if (mem == null || !mem.IsAttached || !writeAllowed)
            {
                ClearFreezes("Freezes stopped: the game or write gate is unavailable.");
                return;
            }

            freezing = true;
            try
            {
                List<string> keys = new List<string>(freezes.Keys);
                foreach (string key in keys)
                {
                    FrozenEntry entry;
                    if (!freezes.TryGetValue(key, out entry)) continue;

                    RelationshipRecord current = RelationshipEditor.FindRecord(mem,
                        entry.SourcePointer, entry.TargetSeat);
                    if (current == null)
                    {
                        freezes.Remove(key);
                        SetStatus("Freeze stopped for " + entry.SourceName +
                            ": the record is no longer available.", true);
                        continue;
                    }

                    string error;
                    if (!RelationshipEditor.TryWrite(mem, current, entry.Counts,
                        entry.Points, entry.Levels, entry.Latest, true, out error))
                    {
                        entry.Failures++;
                        if (entry.Failures >= 3)
                        {
                            freezes.Remove(key);
                            SetStatus("Freeze stopped for " + entry.SourceName +
                                ": " + error, true);
                        }
                    }
                    else
                    {
                        entry.Failures = 0;
                    }
                }
            }
            finally
            {
                freezing = false;
                UpdateFrozenLabels();
                UpdateRowFrozenMarks();
                if (freezes.Count == 0) StopFreezeTimer();
            }
        }

        private void ClearFreezes(string reason)
        {
            if (freezes.Count == 0)
            {
                UpdateFrozenLabels();
                UpdateRowFrozenMarks();
                return;
            }

            freezes.Clear();
            StopFreezeTimer();
            UpdateFrozenLabels();
            UpdateRowFrozenMarks();
            if (!string.IsNullOrEmpty(reason)) SetStatus(reason, false);
        }

        private void StartFreezeTimer()
        {
            if (freezeTimer != null && !freezeTimer.Enabled) freezeTimer.Start();
        }

        private void StopFreezeTimerIfEmpty()
        {
            if (freezes.Count == 0) StopFreezeTimer();
        }

        private void StopFreezeTimer()
        {
            if (freezeTimer != null && freezeTimer.Enabled) freezeTimer.Stop();
        }

        private void UpdateFrozenLabels()
        {
            if (frozenLabel != null)
                frozenLabel.Text = "Frozen: " + freezes.Count.ToString();
            if (clearFreezesButton != null)
                clearFreezesButton.Visible = freezes.Count > 0;
            SyncFreezeCheck();
        }

        private void SyncFreezeCheck()
        {
            if (freezeCheck == null || editorRecord == null ||
                editorRecord.Source == null || !editorRecord.HasRecord)
                return;

            bool frozen = freezes.ContainsKey(Key(editorRecord.Source.Pointer,
                editorRecord.TargetSeat));
            if (freezeCheck.Checked != frozen)
            {
                suppress = true;
                freezeCheck.Checked = frozen;
                suppress = false;
            }
        }

        private void UpdateRowFrozenMarks()
        {
            if (grid == null) return;
            foreach (DataGridViewRow row in grid.Rows)
            {
                RelationshipRecord record = row.Tag as RelationshipRecord;
                if (record == null || record.Source == null) continue;
                bool frozen = freezes.ContainsKey(Key(record.Source.Pointer,
                    record.TargetSeat));
                row.Cells[7].Value = frozen;
            }
        }

        private static string Key(uint sourcePointer, int targetSeat)
        {
            return sourcePointer.ToString("X8") + ":" + targetSeat.ToString();
        }

        // ===================== portrait / helpers =====================

        private void UpdatePortrait(CharacterInfo source)
        {
            if (mem == null || ctx == null || source == null)
            {
                ClearPortrait();
                return;
            }

            uint address;
            uint size;
            GameContext view = ctx.CreateCharacterView(mem, source);
            PortraitLoader.ReadInfo(mem, view, out address, out size);

            if (detailPortrait != null && portraitPointer == source.Pointer &&
                portraitAddress == address && portraitSize == size)
                return;

            ClearPortrait();
            detailPortrait = PortraitLoader.Load(mem, address, size);
            portraitPointer = source.Pointer;
            portraitAddress = address;
            portraitSize = size;

            if (portraitBox != null)
            {
                portraitBox.Image = detailPortrait == null ? null : detailPortrait.Bitmap;
                portraitBox.Visible = portraitBox.Image != null;
            }
        }

        private void ClearPortrait()
        {
            if (portraitBox != null)
            {
                portraitBox.Image = null;
                portraitBox.Visible = false;
            }
            if (detailPortrait != null)
            {
                detailPortrait.Dispose();
                detailPortrait = null;
            }
            portraitPointer = 0;
            portraitAddress = 0;
            portraitSize = 0;
        }

        private string PersonalityFor(CharacterInfo source)
        {
            if (mem == null || source == null) return "";
            uint? data = mem.ReadU32(source.Pointer + 0x28);
            if (!data.HasValue || data.Value == 0) return "";
            byte? personality = mem.ReadU8(data.Value + 0x445);
            if (!personality.HasValue) return "";
            string label = names == null ? null : names.Get("PERSONALITY", personality.Value);
            if (string.IsNullOrEmpty(label)) label = "P" + personality.Value.ToString();
            string seat = source.Seat >= 0 ? " \u00B7 Seat " + source.Seat.ToString("D2") : "";
            return label + seat;
        }

        private void SetStatus(string text, bool error)
        {
            if (statusLabel == null) return;
            statusLabel.Text = text ?? "";
            statusLabel.ForeColor = error ? Color.Firebrick : Color.DimGray;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
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
