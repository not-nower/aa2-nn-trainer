using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace AA2Trainer
{
    public sealed class MainForm : Form
    {
        private readonly GameMemory mem = new GameMemory();
        private readonly GameContext ctx = new GameContext();
        private readonly NameMap names = new NameMap();

        private GameProfile profile;
        private PointerHealth health;
        private bool writesAllowed;
        private DateTime lastHealthCheck;

        private Timer refreshTimer;
        private Timer healthTimer;
        private Timer filterTimer;
        private Timer hoverTimer;
        private Timer hoverWatchTimer;
        private Timer hoverHideTimer;
        private string pendingFilter;

        private ListViewItem hoverItem;
        private CharacterInfo hoverCharacter;
        private Control hoverControl;
        private Rectangle hoverAnchorScreen;
        private ToolStripDropDown hoverPopup;
        private PictureBox hoverPicture;
        private Label hoverPlaceholder;
        private Label hoverName;
        private Label hoverMeta;
        private PortraitImage hoverPortrait;
        private uint hoverPortraitPointer;
        private bool hoverPopupVisible;

        private Label gameStatusLabel;
        private Label classLabel;
        private ListView seatList;
        private TabControl tabs;
        private TabControl characterEditorTabs;
        private TabControl debugTabs;
        private SplitContainer mainSplit;

        private PictureBox portraitBox;
        private Label portraitName;
        private PictureBox seatPortraitBox;
        private Label seatPortraitName;
        private Label seatPortraitPlaceholder;
        private PortraitImage seatPortrait;
        private uint seatPortraitPointer;
        private Dictionary<string, Label> charKv;
        private Dictionary<string, Label> statusKv;
        private Dictionary<string, Label> classKv;
        private Dictionary<string, Label> sessionKv;

        private ClassRosterControl roster;
        private CharacterDataControl characterData;
        private TraitsControl traits;
        private SaveViewerControl saveViewer;
        private FieldGridControl personalGrid;
        private FieldGridControl relationGrid;
        private FieldGridControl activityGrid;
        private FieldGridControl classGrid;
        private FieldGridControl configGrid;
        private FieldGridControl pointerGrid;

        private ComboBox personalGroup;
        private ComboBox relationGroup;
        private NumericUpDown relationIndex;
        private TextBox personalFilter;
        private TextBox relationFilter;
        private TextBox activityFilter;
        private TextBox classFilter;
        private TextBox configFilter;
        private TextBox pointerFilter;
        private CheckBox pointerProblems;
        private TextBox pointerDetails;

        private ToolStripStatusLabel summaryLabel;
        private ToolStripStatusLabel healthLabel;
        private ToolStripStatusLabel namesLabel;
        private ToolStripStatusLabel updatedLabel;

        private bool suppressSeatEvent;
        private bool buildingDefinitions;
        private bool resizeHiddenCharacterData;
        private bool resizeHiddenTraits;
        private bool resizeHiddenRoster;
        private bool resizeHiddenSaveViewer;
        private bool resizeHiddenDebugTabs;
        private string namesRoot;
        private int selectedGender;
        private string lastSeatSignature;
        private int lastSaveViewerSeat = -1;
        private uint lastPortraitAddr;
        private uint lastPortraitSize;

        private static readonly string[] PersonalityNames = new string[]
        {
            "Lively", "Delicate", "Cheerful", "Quiet", "Playful", "Frisky", "Kind",
            "Joyful", "Ordinary", "Irritated", "Harsh", "Sweet", "Creepy", "Reserved",
            "Dignified", "Aloof", "Sensitive", "Genuine", "Mature", "Lazy", "Manly",
            "Gentle [M]", "Positive [M]", "Otaku [M]", "Savage [M]", "Cadet", "Caring",
            "Schemer [M]", "Carefree", "Warm [M]"
        };

        private static readonly Dictionary<int, string> ResponseNames = BuildResponseNames();

        public MainForm()
        {
            Text = "AA2 NN's Trainer";
            Width = 1440;
            Height = 900;
            MinimumSize = new Size(1100, 650);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9F);
            KeyPreview = true;
            DoubleBuffered = true;

            BuildUi();
            KeyDown += MainForm_KeyDown;

            refreshTimer = new Timer();
            refreshTimer.Interval = 1000;
            refreshTimer.Tick += delegate { AutoRefresh(); };

            // Keep the fail-safe gate current even when Auto 1s is off.
            healthTimer = new Timer();
            healthTimer.Interval = 15000;
            healthTimer.Tick += delegate
            {
                if (mem.IsAttached) RunHealthCheck();
            };
            healthTimer.Start();

            filterTimer = new Timer();
            filterTimer.Interval = 250;
            filterTimer.Tick += delegate
            {
                filterTimer.Stop();
                ApplyPendingFilter();
            };

            hoverTimer = new Timer();
            hoverTimer.Interval = 50;
            hoverTimer.Tick += delegate
            {
                hoverTimer.Stop();
                ShowHoverPortrait();
            };

            hoverWatchTimer = new Timer();
            hoverWatchTimer.Interval = 200;
            hoverWatchTimer.Tick += delegate { CheckHoverStillValid(); };

            hoverHideTimer = new Timer();
            hoverHideTimer.Interval = 300;
            hoverHideTimer.Tick += delegate
            {
                hoverHideTimer.Stop();

                // The cursor may have returned without a fresh MouseMove
                // reaching the list. Keep the preview if it is back on the
                // same row or portrait.
                if (hoverPopupVisible && IsHoverStillValid())
                {
                    if (hoverWatchTimer != null) hoverWatchTimer.Start();
                    return;
                }

                HideHoverPortrait();
            };

            Load += delegate { SetInitialLayout(); AttachAndRefresh(); };
            ResizeBegin += delegate { BeginInteractiveResize(); };
            ResizeEnd += delegate { EndInteractiveResize(); };
            FormClosing += delegate
            {
                refreshTimer.Stop();
                healthTimer.Stop();
                filterTimer.Stop();
                hoverTimer.Stop();
                hoverWatchTimer.Stop();
                hoverHideTimer.Stop();
                HideHoverPortrait();
                DisposeHoverPopup();
                if (roster != null) roster.DisposePortraits();
                ClearSeatPortrait();
                mem.Dispose();
            };
            Deactivate += delegate { HideHoverPortrait(); };
        }

        #region UI construction

        private void BuildUi()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 2;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.Controls.Add(BuildTopPanel(), 0, 0);
            root.Controls.Add(BuildMainArea(), 0, 1);
            Controls.Add(root);

            StatusStrip strip = new StatusStrip();
            summaryLabel = new ToolStripStatusLabel("Not attached.");
            summaryLabel.Spring = true;
            summaryLabel.TextAlign = ContentAlignment.MiddleLeft;
            healthLabel = new ToolStripStatusLabel("Pointers: not checked");
            healthLabel.BorderSides = ToolStripStatusLabelBorderSides.Left;
            healthLabel.BorderStyle = Border3DStyle.Etched;
            namesLabel = new ToolStripStatusLabel("Names: not loaded");
            namesLabel.BorderSides = ToolStripStatusLabelBorderSides.Left;
            namesLabel.BorderStyle = Border3DStyle.Etched;
            updatedLabel = new ToolStripStatusLabel("");
            strip.Items.Add(summaryLabel);
            strip.Items.Add(healthLabel);
            strip.Items.Add(namesLabel);
            strip.Items.Add(updatedLabel);
            Controls.Add(strip);
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (!e.Alt) return;
            int index = -1;
            if (e.KeyCode >= Keys.D1 && e.KeyCode <= Keys.D9)
                index = e.KeyCode - Keys.D1;
            else if (e.KeyCode >= Keys.NumPad1 && e.KeyCode <= Keys.NumPad9)
                index = e.KeyCode - Keys.NumPad1;
            if (index >= 0 && tabs != null && index < tabs.TabPages.Count)
            {
                tabs.SelectedIndex = index;
                e.Handled = true;
            }
        }

        private void SetInitialLayout()
        {
            if (mainSplit == null) return;
            int desired = 340;
            int max = mainSplit.Width - 500;
            if (max < mainSplit.Panel1MinSize) max = mainSplit.Panel1MinSize;
            if (desired > max) desired = max;
            if (desired < mainSplit.Panel1MinSize) desired = mainSplit.Panel1MinSize;
            try { mainSplit.SplitterDistance = desired; } catch { }
        }

        private void BeginInteractiveResize()
        {
            // Hiding the heavy pages during a border drag avoids re-laying out
            // dozens of nested controls for every mouse move.
            if (characterData != null && characterData.Visible)
            {
                resizeHiddenCharacterData = true;
                characterData.Visible = false;
            }
            if (traits != null && traits.Visible)
            {
                resizeHiddenTraits = true;
                traits.Visible = false;
            }
            if (roster != null && roster.Visible)
            {
                resizeHiddenRoster = true;
                roster.Visible = false;
            }
            if (saveViewer != null && saveViewer.Visible)
            {
                resizeHiddenSaveViewer = true;
                saveViewer.Visible = false;
            }
            if (debugTabs != null && debugTabs.Visible)
            {
                resizeHiddenDebugTabs = true;
                debugTabs.Visible = false;
            }
        }

        private void EndInteractiveResize()
        {
            if (resizeHiddenCharacterData)
            {
                resizeHiddenCharacterData = false;
                if (characterData != null)
                {
                    characterData.Visible = true;
                    characterData.PerformLayout();
                }
            }
            if (resizeHiddenTraits)
            {
                resizeHiddenTraits = false;
                if (traits != null)
                {
                    traits.Visible = true;
                    traits.PerformLayout();
                }
            }
            if (resizeHiddenRoster)
            {
                resizeHiddenRoster = false;
                if (roster != null)
                {
                    roster.Visible = true;
                    roster.PerformLayout();
                }
            }
            if (resizeHiddenSaveViewer)
            {
                resizeHiddenSaveViewer = false;
                if (saveViewer != null)
                {
                    saveViewer.Visible = true;
                    saveViewer.PerformLayout();
                }
            }
            if (resizeHiddenDebugTabs)
            {
                resizeHiddenDebugTabs = false;
                if (debugTabs != null)
                {
                    debugTabs.Visible = true;
                    debugTabs.PerformLayout();
                }
            }
        }

        private Control BuildTopPanel()
        {
            TableLayoutPanel p = new TableLayoutPanel();
            p.Dock = DockStyle.Fill;
            p.Padding = new Padding(10, 8, 10, 4);
            p.ColumnCount = 8;
            p.RowCount = 2;
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52F));
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90F));
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90F));
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 75F));
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118F));
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 10F));
            p.RowStyles.Add(new RowStyle(SizeType.Percent, 55F));
            p.RowStyles.Add(new RowStyle(SizeType.Percent, 45F));

            Label gameCaption = MakeCaption("Game:");
            p.Controls.Add(gameCaption, 0, 0);

            gameStatusLabel = new Label();
            gameStatusLabel.Dock = DockStyle.Fill;
            gameStatusLabel.AutoSize = false;
            gameStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
            gameStatusLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            gameStatusLabel.ForeColor = Color.DarkRed;
            gameStatusLabel.Text = "Not attached";
            p.Controls.Add(gameStatusLabel, 1, 0);

            Button refresh = new Button();
            refresh.Text = "Refresh";
            refresh.Dock = DockStyle.Fill;
            refresh.Click += delegate { AttachAndRefresh(); };
            p.Controls.Add(refresh, 2, 0);

            Button recheck = new Button();
            recheck.Text = "Recheck";
            recheck.Dock = DockStyle.Fill;
            recheck.Click += delegate { RunHealthCheck(); };
            p.Controls.Add(recheck, 3, 0);

            Button details = new Button();
            details.Text = "Details";
            details.Dock = DockStyle.Fill;
            details.Click += delegate { ShowHealthDetails(); };
            p.Controls.Add(details, 4, 0);

            CheckBox auto = new CheckBox();
            auto.Text = "Auto 1s";
            auto.Dock = DockStyle.Fill;
            auto.CheckedChanged += delegate { refreshTimer.Enabled = auto.Checked; };
            p.Controls.Add(auto, 5, 0);

            Button reloadNames = new Button();
            reloadNames.Text = "Reload names";
            reloadNames.Dock = DockStyle.Fill;
            reloadNames.Click += delegate { ReloadNames(); };
            p.Controls.Add(reloadNames, 6, 0);

            Label classCaption = MakeCaption("Class:");
            p.Controls.Add(classCaption, 0, 1);

            classLabel = new Label();
            classLabel.Dock = DockStyle.Fill;
            classLabel.AutoSize = false;
            classLabel.TextAlign = ContentAlignment.MiddleLeft;
            classLabel.ForeColor = Color.DimGray;
            classLabel.Text = "-";
            p.Controls.Add(classLabel, 1, 1);
            p.SetColumnSpan(classLabel, 6);

            return p;
        }

        private Control BuildMainArea()
        {
            SplitContainer split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.Orientation = Orientation.Vertical;
            split.SplitterDistance = 340;
            split.Panel1MinSize = 280;
            split.Panel2MinSize = 0;
            split.SplitterWidth = 5;
            split.FixedPanel = FixedPanel.Panel1;
            mainSplit = split;

            TableLayoutPanel left = new TableLayoutPanel();
            left.Dock = DockStyle.Fill;
            left.ColumnCount = 1;
            left.RowCount = 3;
            left.Padding = new Padding(8, 4, 4, 8);
            left.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 330F));

            Label seatsHeader = new Label();
            seatsHeader.Text = "Class seats";
            seatsHeader.Dock = DockStyle.Fill;
            seatsHeader.Font = new Font(Font, FontStyle.Bold);
            seatsHeader.TextAlign = ContentAlignment.MiddleLeft;
            seatsHeader.Margin = new Padding(0);
            left.Controls.Add(seatsHeader, 0, 0);

            seatList = new ListView();
            seatList.Dock = DockStyle.Fill;
            seatList.View = View.Details;
            seatList.FullRowSelect = true;
            seatList.HideSelection = false;
            seatList.MultiSelect = false;
            seatList.GridLines = false;
            seatList.ShowItemToolTips = true;
            seatList.Columns.Add("Seat", 38);
            seatList.Columns.Add("Name", 110);
            seatList.Columns.Add("Personality", 130);
            seatList.Columns.Add("Sex", 32);
            seatList.Columns.Add("PC", 28);
            seatList.SelectedIndexChanged += SeatList_SelectedIndexChanged;
            seatList.MouseMove += SeatList_MouseMove;
            seatList.MouseLeave += delegate { ScheduleHideHoverPortrait(); };
            seatList.MouseWheel += delegate { HideHoverPortrait(); };
            seatList.MouseDown += delegate { HideHoverPortrait(); };
            seatList.KeyDown += delegate { HideHoverPortrait(); };
            left.Controls.Add(seatList, 0, 1);

            TableLayoutPanel seatPortraitPanel = new TableLayoutPanel();
            seatPortraitPanel.Dock = DockStyle.Fill;
            seatPortraitPanel.ColumnCount = 1;
            seatPortraitPanel.RowCount = 2;
            seatPortraitPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            seatPortraitPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            seatPortraitPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
            seatPortraitPanel.Margin = new Padding(0, 6, 0, 0);

            Panel portraitHost = new Panel();
            portraitHost.Dock = DockStyle.Fill;
            portraitHost.BackColor = Color.FromArgb(236, 238, 242);
            portraitHost.Margin = new Padding(0);

            seatPortraitBox = new PictureBox();
            seatPortraitBox.Dock = DockStyle.Fill;
            seatPortraitBox.SizeMode = PictureBoxSizeMode.Zoom;
            seatPortraitBox.BorderStyle = BorderStyle.FixedSingle;
            seatPortraitBox.BackColor = Color.FromArgb(236, 238, 242);
            seatPortraitBox.Margin = new Padding(0);
            portraitHost.Controls.Add(seatPortraitBox);

            seatPortraitPlaceholder = new Label();
            seatPortraitPlaceholder.Dock = DockStyle.Fill;
            seatPortraitPlaceholder.TextAlign = ContentAlignment.MiddleCenter;
            seatPortraitPlaceholder.Text = "No portrait";
            seatPortraitPlaceholder.ForeColor = Color.DimGray;
            seatPortraitPlaceholder.BackColor = Color.FromArgb(236, 238, 242);
            seatPortraitPlaceholder.Visible = false;
            portraitHost.Controls.Add(seatPortraitPlaceholder);
            seatPortraitPlaceholder.BringToFront();

            seatPortraitPanel.Controls.Add(portraitHost, 0, 0);

            seatPortraitName = new Label();
            seatPortraitName.Dock = DockStyle.Fill;
            seatPortraitName.TextAlign = ContentAlignment.MiddleCenter;
            seatPortraitName.AutoEllipsis = true;
            seatPortraitName.Font = new Font(Font, FontStyle.Bold);
            seatPortraitName.Text = "No character";
            seatPortraitName.Margin = new Padding(0);
            seatPortraitPanel.Controls.Add(seatPortraitName, 0, 1);

            left.Controls.Add(seatPortraitPanel, 0, 2);

            split.Panel1.Controls.Add(left);
            split.Panel2.Controls.Add(BuildTabs());
            return split;
        }

        private Control BuildTabs()
        {
            tabs = new TabControl();
            tabs.Dock = DockStyle.Fill;
            tabs.Padding = new Point(12, 6);
            tabs.SelectedIndexChanged += delegate { RefreshCurrentTabValues(); };

            tabs.TabPages.Add(BuildRosterTab());
            tabs.TabPages.Add(BuildCharacterEditorTab());
            tabs.TabPages.Add(BuildSaveViewerTab());
            tabs.TabPages.Add(BuildDebugTab());

            // The roster is the landing page.
            tabs.SelectedIndex = 0;
            return tabs;
        }

        private TabPage BuildCharacterEditorTab()
        {
            TabPage page = new TabPage("Character Editor");
            page.BackColor = Color.FromArgb(248, 249, 251);

            characterEditorTabs = new TabControl();
            characterEditorTabs.Dock = DockStyle.Fill;
            characterEditorTabs.Padding = new Point(12, 6);
            characterEditorTabs.SelectedIndexChanged += delegate { RefreshCurrentTabValues(); };

            TabPage dataPage = new TabPage("Character Data");
            dataPage.BackColor = Color.FromArgb(248, 249, 251);

            Panel scrollHost = new Panel();
            scrollHost.Dock = DockStyle.Fill;
            scrollHost.AutoScroll = true;
            scrollHost.BackColor = Color.FromArgb(248, 249, 251);

            characterData = new CharacterDataControl();
            characterData.Dock = DockStyle.None;
            characterData.ConfirmWriteAllowed = delegate
            {
                RunHealthCheck();
                return writesAllowed && mem.CanWrite;
            };
            characterData.CharacterSaved += delegate { OnCharacterSaved(); };
            scrollHost.Controls.Add(characterData);

            EventHandler resizeContent = delegate
            {
                const int minimumHeight = 470;
                int width = scrollHost.ClientSize.Width;
                int height = Math.Max(minimumHeight, scrollHost.ClientSize.Height);
                if (scrollHost.ClientSize.Height < minimumHeight)
                    width -= SystemInformation.VerticalScrollBarWidth;
                width = Math.Max(0, width);
                if (characterData.Width == width && characterData.Height == height) return;
                characterData.SuspendLayout();
                try
                {
                    characterData.SetBounds(0, 0, width, height);
                }
                finally
                {
                    characterData.ResumeLayout(true);
                }
            };
            scrollHost.Resize += resizeContent;
            resizeContent(scrollHost, EventArgs.Empty);

            dataPage.Controls.Add(scrollHost);

            characterEditorTabs.TabPages.Add(dataPage);
            characterEditorTabs.TabPages.Add(BuildTraitsTab());

            page.Controls.Add(characterEditorTabs);
            return page;
        }

        private TabPage BuildTraitsTab()
        {
            TabPage page = new TabPage("Traits");
            page.BackColor = Color.FromArgb(248, 249, 251);

            traits = new TraitsControl();
            traits.Dock = DockStyle.Fill;
            traits.ConfirmWriteAllowed = delegate
            {
                RunHealthCheck();
                return writesAllowed && mem.CanWrite;
            };
            traits.CharacterSaved += delegate { OnCharacterSaved(); };
            page.Controls.Add(traits);
            return page;
        }

        private TabPage BuildDebugTab()
        {
            TabPage page = new TabPage("Debug");
            page.BackColor = Color.FromArgb(248, 249, 251);

            debugTabs = new TabControl();
            debugTabs.Dock = DockStyle.Fill;
            debugTabs.Padding = new Point(12, 6);
            debugTabs.SelectedIndexChanged += delegate { RefreshCurrentTabValues(); };

            debugTabs.TabPages.Add(BuildOverviewTab());
            debugTabs.TabPages.Add(BuildPersonalTab());
            debugTabs.TabPages.Add(BuildRelationsTab());
            debugTabs.TabPages.Add(BuildActivityTab());
            debugTabs.TabPages.Add(BuildClassTab());
            debugTabs.TabPages.Add(BuildConfigTab());
            debugTabs.TabPages.Add(BuildPointersTab());

            page.Controls.Add(debugTabs);
            return page;
        }

        private TabPage BuildOverviewTab()
        {
            TabPage page = new TabPage("Overview");
            page.BackColor = Color.FromArgb(248, 249, 251);

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 3;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 300F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 165F));
            root.Padding = new Padding(8);

            TableLayoutPanel top = new TableLayoutPanel();
            top.Dock = DockStyle.Fill;
            top.ColumnCount = 2;
            top.RowCount = 1;
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 235F));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            top.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            top.Controls.Add(BuildPortraitGroup(), 0, 0);

            TableLayoutPanel info = new TableLayoutPanel();
            info.Dock = DockStyle.Fill;
            info.ColumnCount = 2;
            info.RowCount = 1;
            info.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            info.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            info.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            Dictionary<string, Label> cv;
            Dictionary<string, Label> sv;
            info.Controls.Add(CreateKvGroup("Character", new string[]
            {
                "Name", "Seat", "Gender", "Personality", "Club", "Virtue",
                "Oriented", "Social", "Fighting style", "Study", "Strength",
                "Club rank", "Traits"
            }, out cv), 0, 0);
            info.Controls.Add(CreateKvGroup("Live status", new string[]
            {
                "Status", "Action", "Location", "Response", "Mood action",
                "PC interact", "NPC interact", "NPC target", "Activity ptr", "Relations ptr"
            }, out sv), 1, 0);
            charKv = cv;
            statusKv = sv;
            top.Controls.Add(info, 1, 0);
            root.Controls.Add(top, 0, 0);

            Dictionary<string, Label> clv;
            root.Controls.Add(CreateKvGroup("Class & time", new string[]
            {
                "Class", "Year", "Day / week", "Time", "Save", "Play time", "Screen type"
            }, out clv), 0, 1);
            classKv = clv;

            Dictionary<string, Label> sev;
            root.Controls.Add(CreateKvGroup("Session / fail-safe", new string[]
            {
                "Global S", "Class data", "Selected char", "Character data",
                "Status", "Activity", "Relations", "Profile", "Pointer health", "Write gate"
            }, out sev), 0, 2);
            sessionKv = sev;

            page.Controls.Add(root);
            return page;
        }

        private TabPage BuildRosterTab()
        {
            TabPage page = new TabPage("Class Roster");
            page.BackColor = Color.FromArgb(248, 249, 251);

            roster = new ClassRosterControl();
            roster.Dock = DockStyle.Fill;
            roster.CharacterClicked += Roster_CharacterClicked;
            roster.CharacterActivated += Roster_CharacterActivated;
            roster.PlayRequested += Roster_PlayRequested;
            page.Controls.Add(roster);
            return page;
        }

        private void OnCharacterSaved()
        {
            if (!mem.IsAttached) return;
            if (!TryRefreshContext()) return;

            UpdateSelectedGender();
            PopulateSeats();
            UpdateOverview();
            RefreshCurrentTabValues();
            SetSummary();
            RefreshSaveViewer();
        }

        private TabPage BuildSaveViewerTab()
        {
            TabPage page = new TabPage("Save Viewer");
            page.BackColor = Color.FromArgb(248, 249, 251);

            saveViewer = new SaveViewerControl(names);
            saveViewer.Dock = DockStyle.Fill;
            page.Controls.Add(saveViewer);
            return page;
        }

        private Control BuildPortraitGroup()
        {
            GroupBox box = new GroupBox();
            box.Text = "Portrait";
            box.Dock = DockStyle.Fill;
            box.Padding = new Padding(6);

            TableLayoutPanel inner = new TableLayoutPanel();
            inner.Dock = DockStyle.Fill;
            inner.ColumnCount = 1;
            inner.RowCount = 2;
            inner.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            inner.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            inner.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));

            portraitBox = new PictureBox();
            portraitBox.Dock = DockStyle.Fill;
            portraitBox.BorderStyle = BorderStyle.FixedSingle;
            portraitBox.SizeMode = PictureBoxSizeMode.Zoom;
            portraitBox.BackColor = Color.FromArgb(235, 235, 235);
            inner.Controls.Add(portraitBox, 0, 0);

            portraitName = new Label();
            portraitName.Dock = DockStyle.Fill;
            portraitName.TextAlign = ContentAlignment.MiddleCenter;
            portraitName.Font = new Font(Font, FontStyle.Bold);
            portraitName.AutoEllipsis = true;
            portraitName.Text = "No character";
            inner.Controls.Add(portraitName, 0, 1);

            box.Controls.Add(inner);
            return box;
        }

        private TabPage BuildPersonalTab()
        {
            TabPage page = new TabPage("Personal");
            FieldGridControl grid;
            TableLayoutPanel root = BuildFieldPage(page, out grid);
            personalGrid = grid;
            personalGrid.ValueFormatter = FormatFieldValue;

            personalGroup = new ComboBox();
            personalGroup.DropDownStyle = ComboBoxStyle.DropDownList;
            personalGroup.Width = 160;
            personalGroup.Items.AddRange(DashboardData.PersonalGroups);
            personalGroup.SelectedIndex = personalGroup.Items.Count - 1;
            personalGroup.SelectedIndexChanged += delegate { ApplyPersonalFilter(); };

            personalFilter = new TextBox();
            personalFilter.Width = 220;
            personalFilter.TextChanged += delegate { QueueFilter("personal"); };

            FlowLayoutPanel bar = CreateToolbar();
            bar.Controls.Add(MakeCaption("Group:"));
            bar.Controls.Add(personalGroup);
            bar.Controls.Add(MakeCaption("Filter:"));
            bar.Controls.Add(personalFilter);
            root.Controls.Add(bar, 0, 0);
            return page;
        }

        private TabPage BuildRelationsTab()
        {
            TabPage page = new TabPage("Relations");
            FieldGridControl grid;
            TableLayoutPanel root = BuildFieldPage(page, out grid);
            relationGrid = grid;
            relationGrid.ValueFormatter = FormatFieldValue;

            relationGroup = new ComboBox();
            relationGroup.DropDownStyle = ComboBoxStyle.DropDownList;
            relationGroup.Width = 150;
            relationGroup.Items.AddRange(DashboardData.RelationGroups);
            relationGroup.SelectedIndex = 0;
            relationGroup.SelectedIndexChanged += delegate { ApplyRelationFilter(); };

            relationIndex = new NumericUpDown();
            relationIndex.Minimum = 0;
            relationIndex.Maximum = 24;
            relationIndex.Value = 0;
            relationIndex.Width = 55;
            relationIndex.ValueChanged += delegate
            {
                if (mem.IsAttached && ctx.Selected != null)
                    relationGrid.RefreshValues(mem, ctx, (int)relationIndex.Value);
            };

            relationFilter = new TextBox();
            relationFilter.Width = 190;
            relationFilter.TextChanged += delegate { QueueFilter("relation"); };

            FlowLayoutPanel bar = CreateToolbar();
            bar.Controls.Add(MakeCaption("View:"));
            bar.Controls.Add(relationGroup);
            bar.Controls.Add(MakeCaption("Target index:"));
            bar.Controls.Add(relationIndex);
            bar.Controls.Add(MakeCaption("Filter:"));
            bar.Controls.Add(relationFilter);
            root.Controls.Add(bar, 0, 0);
            return page;
        }

        private TabPage BuildActivityTab()
        {
            TabPage page = new TabPage("Activity");
            FieldGridControl grid;
            TableLayoutPanel root = BuildFieldPage(page, out grid);
            activityGrid = grid;
            activityGrid.ValueFormatter = FormatFieldValue;
            activityFilter = new TextBox();
            activityFilter.Width = 240;
            activityFilter.TextChanged += delegate { QueueFilter("activity"); };
            FlowLayoutPanel bar = CreateToolbar();
            bar.Controls.Add(MakeCaption("Filter:"));
            bar.Controls.Add(activityFilter);
            root.Controls.Add(bar, 0, 0);
            return page;
        }

        private TabPage BuildClassTab()
        {
            TabPage page = new TabPage("Class & Time");
            FieldGridControl grid;
            TableLayoutPanel root = BuildFieldPage(page, out grid);
            classGrid = grid;
            classGrid.ValueFormatter = FormatFieldValue;
            classFilter = new TextBox();
            classFilter.Width = 240;
            classFilter.TextChanged += delegate { QueueFilter("class"); };
            FlowLayoutPanel bar = CreateToolbar();
            bar.Controls.Add(MakeCaption("Filter:"));
            bar.Controls.Add(classFilter);
            root.Controls.Add(bar, 0, 0);
            return page;
        }

        private TabPage BuildConfigTab()
        {
            TabPage page = new TabPage("Config");
            FieldGridControl grid;
            TableLayoutPanel root = BuildFieldPage(page, out grid);
            configGrid = grid;
            configGrid.ValueFormatter = FormatFieldValue;
            configFilter = new TextBox();
            configFilter.Width = 240;
            configFilter.TextChanged += delegate { QueueFilter("config"); };
            FlowLayoutPanel bar = CreateToolbar();
            bar.Controls.Add(MakeCaption("Filter:"));
            bar.Controls.Add(configFilter);
            root.Controls.Add(bar, 0, 0);
            return page;
        }

        private TabPage BuildPointersTab()
        {
            TabPage page = new TabPage("All Pointers");
            pointerGrid = new FieldGridControl();
            pointerGrid.Dock = DockStyle.Fill;
            pointerGrid.ValueFormatter = FormatFieldValue;
            pointerGrid.RowSelected += delegate (object sender, GridRowEventArgs e) { ShowPointerDetails(e.Row); };

            pointerFilter = new TextBox();
            pointerFilter.Width = 230;
            pointerFilter.TextChanged += delegate { QueueFilter("pointer"); };
            pointerProblems = new CheckBox();
            pointerProblems.Text = "Only problems";
            pointerProblems.CheckedChanged += delegate
            {
                pointerGrid.HideOkRows = pointerProblems.Checked;
                if (mem.IsAttached && ctx.Selected != null)
                {
                    pointerGrid.RefreshValues(mem, ctx, 0);
                    RefreshPointerDetails();
                }
            };

            FlowLayoutPanel bar = CreateToolbar();
            bar.Controls.Add(MakeCaption("Filter:"));
            bar.Controls.Add(pointerFilter);
            bar.Controls.Add(pointerProblems);

            pointerDetails = new TextBox();
            pointerDetails.Dock = DockStyle.Fill;
            pointerDetails.Multiline = true;
            pointerDetails.ReadOnly = true;
            pointerDetails.ScrollBars = ScrollBars.Both;
            pointerDetails.WordWrap = false;
            pointerDetails.Font = new Font("Consolas", 8.5F);
            pointerDetails.BackColor = Color.White;
            pointerDetails.Text = "Select a pointer row to inspect the exact chain.";

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 3;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 120F));
            root.Controls.Add(bar, 0, 0);
            root.Controls.Add(pointerGrid, 0, 1);
            root.Controls.Add(pointerDetails, 0, 2);
            page.Controls.Add(root);
            return page;
        }

        private TableLayoutPanel BuildFieldPage(TabPage page, out FieldGridControl grid)
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 2;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.Padding = new Padding(4);

            grid = new FieldGridControl();
            grid.Dock = DockStyle.Fill;
            root.Controls.Add(grid, 0, 1);
            page.Controls.Add(root);
            return root;
        }

        private static FlowLayoutPanel CreateToolbar()
        {
            FlowLayoutPanel bar = new FlowLayoutPanel();
            bar.Dock = DockStyle.Fill;
            bar.FlowDirection = FlowDirection.LeftToRight;
            bar.WrapContents = false;
            bar.Padding = new Padding(4, 4, 4, 2);
            bar.BackColor = Color.FromArgb(245, 246, 248);
            return bar;
        }

        private static Label MakeCaption(string text)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.Margin = new Padding(6, 6, 2, 0);
            l.ForeColor = Color.DimGray;
            return l;
        }

        private static GroupBox CreateKvGroup(string title, string[] keys, out Dictionary<string, Label> values)
        {
            GroupBox box = new GroupBox();
            box.Text = title;
            box.Dock = DockStyle.Fill;
            box.Padding = new Padding(8, 4, 8, 6);
            box.Margin = new Padding(3);

            TableLayoutPanel table = new TableLayoutPanel();
            table.Dock = DockStyle.Fill;
            table.ColumnCount = 2;
            table.RowCount = keys.Length;
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105F));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (int i = 0; i < keys.Length; i++)
                table.RowStyles.Add(new RowStyle(SizeType.Percent, 100F / keys.Length));

            values = new Dictionary<string, Label>();
            for (int i = 0; i < keys.Length; i++)
            {
                Label caption = new Label();
                caption.Text = keys[i];
                caption.Dock = DockStyle.Fill;
                caption.AutoSize = false;
                caption.TextAlign = ContentAlignment.MiddleLeft;
                caption.ForeColor = Color.DimGray;
                table.Controls.Add(caption, 0, i);

                Label value = new Label();
                value.Text = "-";
                value.Dock = DockStyle.Fill;
                value.AutoSize = false;
                value.AutoEllipsis = true;
                value.TextAlign = ContentAlignment.MiddleLeft;
                value.Font = new Font("Consolas", 9F);
                table.Controls.Add(value, 1, i);
                values[keys[i]] = value;
            }
            box.Controls.Add(table);
            return box;
        }

        #endregion

        #region Refresh and data

        private string GetGameRoot()
        {
            if (!string.IsNullOrEmpty(mem.GamePath))
                return Path.GetDirectoryName(mem.GamePath);
            return NameMap.FindGameRoot();
        }

        private void EnsureNamesLoaded()
        {
            string root = GetGameRoot();
            if (root == null) root = "";
            if (namesRoot != null &&
                string.Equals(root, namesRoot, StringComparison.OrdinalIgnoreCase))
                return;
            namesRoot = root;
            names.Load(root);
            UpdateNamesLabel();
        }

        private void ReloadNames()
        {
            string root = GetGameRoot();
            if (root == null) root = namesRoot;
            if (root == null) root = "";
            namesRoot = root;
            names.Load(root);
            UpdateNamesLabel();
            if (mem.IsAttached && ctx.Selected != null)
            {
                PopulateSeats();
                UpdateOverview();
                RefreshCurrentTabValues();
                SetSummary();
                RefreshSaveViewer();
            }
        }

        private void RefreshSaveViewer()
        {
            if (saveViewer == null) return;
            saveViewer.SetGameRoot(GetGameRoot());
            saveViewer.RefreshIfChanged();
            if (mem.IsAttached && ctx.Selected != null)
            {
                saveViewer.SetLiveCharacters(ctx.Characters);
                if (ctx.Selected.Seat != lastSaveViewerSeat)
                {
                    saveViewer.SetSelectedSeat(ctx.Selected.Seat);
                    lastSaveViewerSeat = ctx.Selected.Seat;
                }
            }
            else
            {
                saveViewer.SetLiveCharacters(null);
            }
        }

        private void UpdateNamesLabel()
        {
            if (namesLabel == null) return;
            namesLabel.Text = "Names: " + (names == null ? "not loaded" : names.StatusText);
            if (names != null && !string.IsNullOrEmpty(names.ErrorText))
                namesLabel.ToolTipText = names.ErrorText;
        }

        private void UpdateSelectedGender()
        {
            selectedGender = 0;
            if (!mem.IsAttached || ctx.Selected == null) return;
            int? g = LeadingInt(DashboardData.Value(mem, ctx, "Gender", 0));
            if (g.HasValue) selectedGender = g.Value;
        }

        private void RunHealthCheck()
        {
            if (!mem.IsAttached || ctx.Selected == null)
            {
                profile = null;
                health = null;
                writesAllowed = false;
                UpdateHealthUi();
                return;
            }

            try
            {
                profile = GameProfile.Detect(mem);
                health = PointerHealth.Run(mem, ctx);
                writesAllowed = profile != null && profile.Known && health.Clean;
                lastHealthCheck = DateTime.UtcNow;
            }
            catch
            {
                profile = null;
                health = null;
                writesAllowed = false;
            }
            UpdateHealthUi();
        }

        private const double HealthCheckIntervalSeconds = 15.0;

        private void MaybeRunHealthCheck()
        {
            if (health == null || (DateTime.UtcNow - lastHealthCheck).TotalSeconds >= HealthCheckIntervalSeconds)
                RunHealthCheck();
        }

        private void ShowHealthDetails()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Profile");
            sb.AppendLine("  " + (profile != null ? profile.Display : "not detected"));
            if (profile != null)
            {
                sb.AppendLine("  version/size match: " + (profile.VersionAndSizeMatch ? "yes" : "no"));
                sb.AppendLine("  sha-256 match: " + (profile.HashMatch ? "yes" : "no"));
            }
            sb.AppendLine();
            sb.AppendLine("Pointers");
            sb.AppendLine("  " + (health != null ? health.Summary : "not checked"));
            if (health != null)
            {
                sb.AppendLine("  check took " + health.ElapsedMs.ToString() + " ms at " +
                              health.CheckedAt.ToString("HH:mm:ss"));
                if (health.EmptyFields.Count > 0)
                    sb.AppendLine("  empty: " + string.Join(", ", health.EmptyFields.ToArray()));
                if (health.OptionalFields.Count > 0)
                    sb.AppendLine("  optional/legacy: " + health.OptionalFields.Count.ToString() +
                                  " (not gated)");
            }
            sb.AppendLine("  write gate: " + (writesAllowed ? "open" : "locked"));
            if (health != null && health.Failures.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Problems");
                sb.Append(health.FailureText(25));
            }
            if (health != null && health.OptionalFields.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Optional / legacy (not gated)");
                sb.Append(health.OptionalText(12));
            }
            MessageBox.Show(this, sb.ToString(), "Fail-safe details",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void UpdateHealthUi()
        {
            string text;
            Color color;
            if (!mem.IsAttached || ctx.Selected == null || health == null)
            {
                text = "Pointers: not checked";
                color = Color.DimGray;
            }
            else if (!health.CountOk)
            {
                text = "Pointers: " + health.ShortStatus + " | writes locked";
                color = Color.Firebrick;
            }
            else if (health.Broken > 0 || health.Unreadable > 0)
            {
                text = "Pointers: " + health.ShortStatus + " / " + health.ElapsedMs.ToString() + " ms | writes locked";
                color = Color.Firebrick;
            }
            else if (health.Suspicious > 0)
            {
                text = "Pointers: " + health.ShortStatus + " / " + health.ElapsedMs.ToString() + " ms | writes locked";
                color = Color.DarkOrange;
            }
            else if (profile == null || !profile.Known)
            {
                text = "Pointers: " + health.ShortStatus + " / " + health.ElapsedMs.ToString() +
                       " ms | unknown build - writes locked";
                color = Color.DarkOrange;
            }
            else
            {
                text = "Pointers: " + health.ShortStatus + " / " + health.ElapsedMs.ToString() +
                       " ms | write gate open";
                color = Color.ForestGreen;
            }

            if (healthLabel != null)
            {
                healthLabel.Text = text;
                healthLabel.ForeColor = color;

                StringBuilder tip = new StringBuilder();
                if (profile != null)
                {
                    tip.AppendLine("Profile: " + profile.Display);
                    tip.AppendLine("Version/size match: " + (profile.VersionAndSizeMatch ? "yes" : "no"));
                    tip.AppendLine("Hash match: " + (profile.HashMatch ? "yes" : "no"));
                }
                if (health != null)
                {
                    tip.AppendLine("Pointers: " + health.Summary);
                    tip.AppendLine("Checked: " + health.CheckedAt.ToString("HH:mm:ss") +
                                   " (" + health.ElapsedMs.ToString() + " ms)");
                    if (health.Failures.Count > 0)
                    {
                        tip.AppendLine();
                        tip.Append("Problems:\n" + health.FailureText(12));
                    }
                    if (health.Optional > 0)
                        tip.AppendLine("Optional/legacy: " + health.Optional.ToString() +
                                       " (not gated)");
                }
                tip.AppendLine("Write gate: " + (writesAllowed ? "open" : "locked"));
                tip.Append("Use Details for more.");
                healthLabel.ToolTipText = tip.ToString();
            }

            SetHealthSession();
            if (characterData != null) characterData.SetWriteAllowed(writesAllowed);
            if (traits != null) traits.SetWriteAllowed(writesAllowed);
        }

        private void SetHealthSession()
        {
            if (sessionKv == null) return;
            SetKV(sessionKv, "Profile", profile != null ? profile.Display : "not detected",
                profile != null && profile.Known);
            SetKV(sessionKv, "Pointer health", health != null ? health.Summary : "not checked",
                health != null && health.Clean);
            SetKV(sessionKv, "Write gate", writesAllowed ? "open" : "locked", writesAllowed);
        }

        private string FormatFieldValue(PointerDef def, EvalResult r)
        {
            if (def == null || r == null) return r != null ? r.Value : "";
            if (r.Bytes == null) return r.Value;

            int value;
            if (def.Type == 3)
            {
                if (r.Bytes.Length < 1) return r.Value;
                value = r.Bytes[0];
            }
            else if (def.Type == 4 || def.Type == 8)
            {
                if (r.Bytes.Length < 4) return r.Value;
                value = BitConverter.ToInt32(r.Bytes, 0);
            }
            else
            {
                return r.Value;
            }
            if (value < 0) return r.Value;

            if (def.Name == "Club")
            {
                string club = ClubName(mem, ctx);
                if (!string.IsNullOrEmpty(club) && club != "-")
                    return r.Value + "   " + club;
            }

            string name = names.DescribeName(def.Name, selectedGender, value);
            if (string.IsNullOrEmpty(name)) return r.Value;
            return r.Value + "   " + name;
        }

        private string NamedValue(string field, string raw)
        {
            int? n = LeadingInt(raw);
            if (!n.HasValue) return raw;
            string name = names.DescribeName(field, selectedGender, n.Value);
            if (string.IsNullOrEmpty(name)) return raw;
            return raw + "  " + name;
        }

        private void AttachAndRefresh()
        {
            EnsureNamesLoaded();
            if (!mem.IsAttached)
            {
                if (!mem.Attach())
                {
                    ShowNotAttached(mem.LastError);
                    return;
                }
            }
            if (!TryRefreshContext())
            {
                mem.Detach();
                ShowNotAttached("Class data is not loaded.");
                return;
            }

            EnsureNamesLoaded();
            UpdateSelectedGender();
            RefreshSaveViewer();
            RunHealthCheck();
            PopulateSeats();
            BuildAllDefinitions();
            UpdateOverview();
            RefreshCurrentTabValues();
            SetSummary();
        }

        private bool TryRefreshContext()
        {
            uint preferred = 0;
            if (ctx.Selected != null) preferred = ctx.Selected.Pointer;
            try
            {
                return ctx.Refresh(mem, preferred);
            }
            catch
            {
                return false;
            }
        }

        private void AutoRefresh()
        {
            EnsureNamesLoaded();
            if (!mem.IsAttached)
            {
                if (!mem.Attach())
                {
                    ShowNotAttached(mem.LastError);
                    return;
                }
                if (!TryRefreshContext())
                {
                    mem.Detach();
                    ShowNotAttached("Class data is not loaded.");
                    return;
                }
                EnsureNamesLoaded();
                UpdateSelectedGender();
                RefreshSaveViewer();
                MaybeRunHealthCheck();
                PopulateSeats();
                BuildAllDefinitions();
                UpdateOverview();
                RefreshCurrentTabValues();
                SetSummary();
                return;
            }

            if (!TryRefreshContext())
            {
                mem.Detach();
                ShowNotAttached("Class data is not loaded.");
                return;
            }
            EnsureNamesLoaded();
            UpdateSelectedGender();
            RefreshSaveViewer();
            MaybeRunHealthCheck();
            PopulateSeats();
            UpdateOverview();
            RefreshCurrentTabValues();
            SetSummary();
        }

        private void ShowNotAttached(string reason)
        {
            gameStatusLabel.Text = "Not attached: " + (reason == null ? "" : reason);
            gameStatusLabel.ForeColor = Color.DarkRed;
            classLabel.Text = "-";
            seatList.Items.Clear();
            if (roster != null) roster.Clear();
            if (characterData != null) characterData.Clear();
            if (traits != null) traits.Clear();
            lastSeatSignature = null;
            profile = null;
            health = null;
            writesAllowed = false;
            ClearOverview();
            UpdateHealthUi();
            SetSummaryText("Not attached.");
            lastSaveViewerSeat = -1;
            RefreshSaveViewer();
        }

        private void PopulateSeats()
        {
            if (!mem.IsAttached || ctx.Selected == null)
            {
                HideHoverPortrait();
                return;
            }
            uint selectedPointer = ctx.Selected.Pointer;

            string signature = "";
            foreach (CharacterInfo info in ctx.Characters)
                signature += info.Pointer.ToString("X") + ":" + info.Seat.ToString() + ":" + info.Name + "|";
            if (signature == lastSeatSignature && seatList.Items.Count > 0)
            {
                suppressSeatEvent = true;
                try
                {
                    foreach (ListViewItem item in seatList.Items)
                    {
                        CharacterInfo info = item.Tag as CharacterInfo;
                        if (info != null && info.Pointer == selectedPointer)
                        {
                            if (!item.Selected)
                            {
                                item.Selected = true;
                                item.Focused = true;
                                item.EnsureVisible();
                            }
                            break;
                        }
                    }
                }
                finally { suppressSeatEvent = false; }
                return;
            }
            HideHoverPortrait();
            lastSeatSignature = signature;

            suppressSeatEvent = true;
            seatList.BeginUpdate();
            try
            {
                seatList.Items.Clear();
                foreach (CharacterInfo info in ctx.Characters)
                {
                    uint? cd = mem.ReadU32(info.Pointer + 0x28);
                    string personality = "?";
                    string sex = "?";
                    if (cd.HasValue && mem.IsPlausiblePointer(cd.Value))
                    {
                        byte? p = mem.ReadU8(cd.Value + 0x445);
                        if (p.HasValue) personality = PersonalityName(p.Value);
                        byte? g = mem.ReadU8(cd.Value + 0x3C);
                        if (g.HasValue) sex = g.Value == 0 ? "M" : "F";
                    }
                    ListViewItem item = new ListViewItem(info.Seat >= 0 ? info.Seat.ToString("D2") : "??");
                    item.SubItems.Add(info.Name);
                    item.SubItems.Add(personality);
                    item.SubItems.Add(sex);
                    item.SubItems.Add(info.IsPlayer ? "*" : "");
                    item.Tag = info;
                    if (info.IsPlayer) item.Font = new Font(seatList.Font, FontStyle.Bold);
                    seatList.Items.Add(item);
                }

                foreach (ListViewItem item in seatList.Items)
                {
                    CharacterInfo info = item.Tag as CharacterInfo;
                    if (info != null && info.Pointer == selectedPointer)
                    {
                        item.Selected = true;
                        item.Focused = true;
                        item.EnsureVisible();
                        break;
                    }
                }
            }
            finally
            {
                seatList.EndUpdate();
                suppressSeatEvent = false;
            }
        }

        private void SeatList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (suppressSeatEvent) return;
            if (seatList.SelectedItems.Count == 0) return;
            CharacterInfo info = seatList.SelectedItems[0].Tag as CharacterInfo;
            if (info == null) return;
            ApplySelection(info);
        }

        private void Roster_CharacterClicked(object sender, CharacterSelectedEventArgs e)
        {
            if (e.Character == null) return;
            CharacterInfo info = ctx.FindByPointer(e.Character.Pointer);
            if (info == null) info = e.Character;
            SyncSeatList(info);
            ApplySelection(info);
        }

        private void Roster_CharacterActivated(object sender, CharacterSelectedEventArgs e)
        {
            if (e.Character == null) return;
            CharacterInfo info = ctx.FindByPointer(e.Character.Pointer);
            if (info == null) info = e.Character;

            SyncSeatList(info);
            ApplySelection(info);

            if (tabs != null) tabs.SelectedIndex = 1; // Character Editor
            if (characterEditorTabs != null) characterEditorTabs.SelectedIndex = 0; // Character Data
            RefreshCurrentTabValues();
        }

        private void Roster_PlayRequested(object sender, CharacterSelectedEventArgs e)
        {
            if (e.Character == null || !mem.IsAttached) return;

            if (e.Character.IsPlayer)
            {
                SetSummaryText(e.Character.Name + " is already the player character.");
                return;
            }

            RunHealthCheck();
            if (!writesAllowed || !mem.CanWrite)
            {
                MessageBox.Show(this,
                    "The write gate is locked. Wait for a clean pointer check before switching the player character.",
                    "Play this character", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string error;
            if (!PlayerSwitch.TrySetPlayer(mem, ctx, e.Character, out error))
            {
                MessageBox.Show(this, error, "Play this character",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!TryRefreshContext())
            {
                SetSummaryText("Player switched, but the class data could not be refreshed.");
                return;
            }

            CharacterInfo info = ctx.FindByPointer(e.Character.Pointer);
            if (info == null) info = e.Character;

            SyncSeatList(info);
            ApplySelection(info);
            PopulateSeats();
            RefreshCurrentTabValues();
            SetSummaryText("Now playing: " + info.Name);
        }

        private void SyncSeatList(CharacterInfo info)
        {
            if (seatList == null || info == null) return;
            suppressSeatEvent = true;
            try
            {
                bool found = false;
                foreach (ListViewItem item in seatList.Items)
                {
                    CharacterInfo itemInfo = item.Tag as CharacterInfo;
                    if (itemInfo == null || itemInfo.Pointer != info.Pointer) continue;
                    item.Selected = true;
                    item.Focused = true;
                    item.EnsureVisible();
                    found = true;
                    break;
                }
                if (!found) seatList.SelectedItems.Clear();
            }
            finally
            {
                suppressSeatEvent = false;
            }
        }

        private void ApplySelection(CharacterInfo info)
        {
            if (info == null) return;
            HideHoverPortrait();
            ctx.Select(mem, info);
            UpdateSelectedGender();
            MaybeRunHealthCheck();
            UpdateOverview();
            RefreshCurrentTabValues();
            SetSummary();
            RefreshSaveViewer();
        }

        private void SeatList_MouseMove(object sender, MouseEventArgs e)
        {
            if (!mem.IsAttached || ctx.Selected == null)
            {
                ScheduleHideHoverPortrait();
                return;
            }

            ListViewItem item = seatList.GetItemAt(e.X, e.Y);
            if (item == null)
            {
                ScheduleHideHoverPortrait();
                return;
            }

            if (item == hoverItem)
            {
                if (hoverHideTimer != null) hoverHideTimer.Stop();
                if (hoverPopupVisible)
                {
                    if (hoverWatchTimer != null) hoverWatchTimer.Start();
                }
                else
                {
                    hoverTimer.Stop();
                    hoverTimer.Start();
                }
                return;
            }

            HideHoverPortrait();
            hoverItem = item;
            hoverCharacter = item.Tag as CharacterInfo;
            hoverControl = seatList;
            hoverAnchorScreen = seatList.RectangleToScreen(item.Bounds);
            hoverTimer.Stop();
            hoverTimer.Start();
        }

        private void ShowHoverPortrait()
        {
            if (hoverCharacter == null || hoverItem == null) return;
            if (!mem.IsAttached || ctx.Selected == null)
            {
                HideHoverPortrait();
                return;
            }

            Point listClient = seatList.PointToClient(Cursor.Position);
            if (!seatList.ClientRectangle.Contains(listClient) ||
                seatList.GetItemAt(listClient.X, listClient.Y) != hoverItem)
            {
                HideHoverPortrait();
                return;
            }

            CharacterInfo info = hoverCharacter;
            EnsureHoverPopup();
            LoadHoverPortrait(info);

            hoverName.Text = string.IsNullOrEmpty(info.Name) ? "(unnamed)" : info.Name;
            string seat = info.Seat >= 0 ? info.Seat.ToString() : "?";
            hoverMeta.Text = "Seat " + seat + "  -  " + PersonalityFor(info);

            ShowHoverPopupAtAnchor();
        }

        private string PersonalityFor(CharacterInfo info)
        {
            GameContext view = ctx.CreateCharacterView(mem, info);
            EvalResult r = DashboardData.Eval(mem, view, "Personality", 0);
            if (r == null || r.Bytes == null || r.Bytes.Length < 1) return "?";
            int id = r.Bytes[0];
            string name = names == null ? null : names.Get("PERSONALITY", id);
            if (string.IsNullOrEmpty(name)) return "P" + id.ToString();
            return id.ToString("D2") + "  " + name;
        }

        private void EnsureHoverPopup()
        {
            if (hoverPopup != null) return;

            TableLayoutPanel panel = new TableLayoutPanel();
            panel.ColumnCount = 1;
            panel.RowCount = 3;
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200F));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 300F));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
            panel.Size = new Size(200, 346);
            panel.BackColor = Color.White;
            panel.Margin = new Padding(0);
            panel.Padding = new Padding(0);

            Panel picturePanel = new Panel();
            picturePanel.Dock = DockStyle.Fill;
            picturePanel.BackColor = Color.FromArgb(236, 238, 242);

            hoverPicture = new PictureBox();
            hoverPicture.Dock = DockStyle.Fill;
            hoverPicture.SizeMode = PictureBoxSizeMode.Zoom;
            hoverPicture.BackColor = Color.FromArgb(236, 238, 242);
            picturePanel.Controls.Add(hoverPicture);

            hoverPlaceholder = new Label();
            hoverPlaceholder.Dock = DockStyle.Fill;
            hoverPlaceholder.TextAlign = ContentAlignment.MiddleCenter;
            hoverPlaceholder.Text = "No portrait";
            hoverPlaceholder.ForeColor = Color.DimGray;
            hoverPlaceholder.BackColor = Color.FromArgb(236, 238, 242);
            hoverPlaceholder.Visible = false;
            picturePanel.Controls.Add(hoverPlaceholder);

            panel.Controls.Add(picturePanel, 0, 0);

            hoverName = new Label();
            hoverName.Dock = DockStyle.Fill;
            hoverName.TextAlign = ContentAlignment.MiddleCenter;
            hoverName.AutoEllipsis = true;
            hoverName.Font = new Font(Font, FontStyle.Bold);
            panel.Controls.Add(hoverName, 0, 1);

            hoverMeta = new Label();
            hoverMeta.Dock = DockStyle.Fill;
            hoverMeta.TextAlign = ContentAlignment.MiddleCenter;
            hoverMeta.AutoEllipsis = true;
            hoverMeta.Font = new Font("Segoe UI", 8F);
            hoverMeta.ForeColor = Color.DimGray;
            panel.Controls.Add(hoverMeta, 0, 2);

            ToolStripControlHost host = new ToolStripControlHost(panel);
            host.AutoSize = false;
            host.Size = panel.Size;
            host.Margin = new Padding(0);
            host.Padding = new Padding(0);

            hoverPopup = new ToolStripDropDown();
            hoverPopup.AutoSize = false;
            hoverPopup.AutoClose = false;
            hoverPopup.DropShadowEnabled = true;
            hoverPopup.Padding = new Padding(1);
            hoverPopup.BackColor = Color.FromArgb(190, 195, 205);
            hoverPopup.Items.Add(host);
            hoverPopup.Size = new Size(202, 348);
            hoverPopup.Closed += delegate
            {
                hoverPopupVisible = false;
                ClearHoverSource();
                if (hoverWatchTimer != null) hoverWatchTimer.Stop();
                if (hoverHideTimer != null) hoverHideTimer.Stop();
            };
        }

        private void LoadHoverPortrait(CharacterInfo info)
        {
            GameContext view = ctx.CreateCharacterView(mem, info);
            uint addr;
            uint size;
            PortraitLoader.ReadFullInfo(mem, view, out addr, out size);

            if (hoverPortrait != null && hoverPortraitPointer == info.Pointer &&
                hoverPortrait.Address == addr && hoverPortrait.Size == size)
                return;

            if (hoverPicture != null) hoverPicture.Image = null;
            if (hoverPortrait != null)
            {
                hoverPortrait.Dispose();
                hoverPortrait = null;
            }

            hoverPortrait = PortraitLoader.Load(mem, addr, size);
            hoverPortraitPointer = info.Pointer;

            bool hasImage = hoverPortrait.Bitmap != null;
            if (hoverPicture != null)
            {
                hoverPicture.Image = hasImage ? hoverPortrait.Bitmap : null;
                hoverPicture.Visible = hasImage;
            }
            if (hoverPlaceholder != null) hoverPlaceholder.Visible = !hasImage;
        }

        private void ShowHoverPopupAtAnchor()
        {
            if (hoverPopup == null || hoverCharacter == null) return;
            if (hoverHideTimer != null) hoverHideTimer.Stop();

            int width = hoverPopup.Width > 0 ? hoverPopup.Width : 202;
            int height = hoverPopup.Height > 0 ? hoverPopup.Height : 348;
            Rectangle area = Screen.FromPoint(Cursor.Position).WorkingArea;
            Rectangle anchor = hoverAnchorScreen;
            if (anchor.Width <= 0 || anchor.Height <= 0)
                anchor = new Rectangle(Cursor.Position, new Size(1, 1));

            // Keep the preview beside the hovered row or portrait so it
            // never covers the thing being pointed at.
            int x = anchor.Right + 8;
            int y = anchor.Top;
            if (x + width > area.Right) x = anchor.Left - width - 8;
            if (y + height > area.Bottom) y = area.Bottom - height;
            if (x < area.Left) x = area.Left;
            if (y < area.Top) y = area.Top;

            if (hoverPopupVisible) hoverPopup.Location = new Point(x, y);
            else hoverPopup.Show(new Point(x, y));
            hoverPopupVisible = true;
            if (hoverWatchTimer != null) hoverWatchTimer.Start();
        }

        private bool IsHoverStillValid()
        {
            if (hoverCharacter == null || hoverControl == null || hoverItem == null)
                return false;

            Point client = hoverControl.PointToClient(Cursor.Position);
            if (!hoverControl.ClientRectangle.Contains(client)) return false;
            return seatList.GetItemAt(client.X, client.Y) == hoverItem;
        }

        private void CheckHoverStillValid()
        {
            if (!hoverPopupVisible)
            {
                if (hoverWatchTimer != null) hoverWatchTimer.Stop();
                return;
            }
            if (!IsHoverStillValid())
                ScheduleHideHoverPortrait();
        }

        private void ScheduleHideHoverPortrait()
        {
            if (!hoverPopupVisible || hoverPopup == null)
            {
                HideHoverPortrait();
                return;
            }

            // A leave event from the previous row/portrait can arrive after
            // the new source has already taken over.
            if (IsHoverStillValid())
            {
                if (hoverWatchTimer != null) hoverWatchTimer.Start();
                return;
            }

            if (hoverWatchTimer != null) hoverWatchTimer.Stop();
            if (hoverHideTimer != null)
            {
                hoverHideTimer.Stop();
                hoverHideTimer.Start();
            }
        }

        private void ClearHoverSource()
        {
            hoverItem = null;
            hoverCharacter = null;
            hoverControl = null;
            hoverAnchorScreen = Rectangle.Empty;
        }

        private void HideHoverPortrait()
        {
            if (hoverTimer != null) hoverTimer.Stop();
            if (hoverWatchTimer != null) hoverWatchTimer.Stop();
            if (hoverHideTimer != null) hoverHideTimer.Stop();
            ClearHoverSource();
            if (hoverPopup != null && (hoverPopupVisible || hoverPopup.Visible))
                hoverPopup.Close();
            hoverPopupVisible = false;
        }

        private void DisposeHoverPopup()
        {
            if (hoverPicture != null) hoverPicture.Image = null;
            if (hoverPortrait != null)
            {
                hoverPortrait.Dispose();
                hoverPortrait = null;
            }
            if (hoverPopup != null)
            {
                hoverPopup.Dispose();
                hoverPopup = null;
            }
            hoverPicture = null;
            hoverPlaceholder = null;
            hoverName = null;
            hoverMeta = null;
            hoverPortraitPointer = 0;
            hoverPopupVisible = false;
            ClearHoverSource();
        }

        private void UpdateOverview()
        {
            if (!mem.IsAttached || ctx.Selected == null)
            {
                ClearOverview();
                return;
            }
            UpdateSelectedGender();

            string first = StripQuotes(DashboardData.Value(mem, ctx, "FirstName", 0));
            string last = StripQuotes(DashboardData.Value(mem, ctx, "LastName", 0));
            SetKV(charKv, "Name", (first + " " + last).Trim(), true);
            SetKV(charKv, "Seat", ctx.Selected.Seat >= 0 ? ctx.Selected.Seat.ToString() : "?", true);
            SetKV(charKv, "Gender", GenderName(DashboardData.Value(mem, ctx, "Gender", 0)), true);
            string personality = DashboardData.Value(mem, ctx, "Personality", 0);
            SetKV(charKv, "Personality", PersonalityFromValue(personality), true);
            SetKV(charKv, "Club", ClubName(mem, ctx), true);
            SetKV(charKv, "Virtue", DashboardData.Value(mem, ctx, "Virtue", 0), true);
            SetKV(charKv, "Oriented", NamedValue("Oriented", DashboardData.Value(mem, ctx, "Oriented", 0)), true);
            SetKV(charKv, "Social", DashboardData.Value(mem, ctx, "Social", 0), true);
            SetKV(charKv, "Fighting style", NamedValue("FightingStyle", DashboardData.Value(mem, ctx, "FightingStyle", 0)), true);
            SetKV(charKv, "Study", StudyText("IntLevel", "IntRank", "IntValue"), true);
            SetKV(charKv, "Strength", StudyText("StrLevel", "StrRank", "StrValue"), true);
            SetKV(charKv, "Club rank", StudyText("ClubRank", null, "ClubValue"), true);
            SetKV(charKv, "Traits", TraitSummary(), true);

            SetKV(statusKv, "Status", DashboardData.Value(mem, ctx, "Status", 0), true);
            SetKV(statusKv, "Action", NamedValue("Action", DashboardData.Value(mem, ctx, "Action", 0)), true);
            SetKV(statusKv, "Location", NamedValue("Location", DashboardData.Value(mem, ctx, "Location", 0)), true);
            SetKV(statusKv, "Response", ResponseText(DashboardData.Value(mem, ctx, "Response", 0)), true);
            SetKV(statusKv, "Mood action", DashboardData.Value(mem, ctx, "MoodAction", 0), true);
            SetKV(statusKv, "PC interact", DashboardData.Value(mem, ctx, "PCInteract", 0), true);
            SetKV(statusKv, "NPC interact", DashboardData.Value(mem, ctx, "NPCInteract", 0), true);
            SetKV(statusKv, "NPC target", PointerOrNumberText(DashboardData.Value(mem, ctx, "NPCTarget", 0)), true);
            SetKV(statusKv, "Activity ptr", Addr(ctx.Activity), ctx.Activity != 0);
            SetKV(statusKv, "Relations ptr", Addr(ctx.Relations), ctx.Relations != 0);

            SetKV(classKv, "Class", StripQuotes(DashboardData.Value(mem, ctx, "ClassName", 0)), true);
            SetKV(classKv, "Year", AsciiDigitText(DashboardData.Value(mem, ctx, "Year", 0)), true);
            SetKV(classKv, "Day / week",
                "day " + DashboardData.Value(mem, ctx, "DayName", 0) +
                " / week " + DashboardData.Value(mem, ctx, "DayWeek", 0), true);
            SetKV(classKv, "Time", DashboardData.Value(mem, ctx, "TimeGame", 0), true);
            SetKV(classKv, "Save", StripQuotes(DashboardData.Value(mem, ctx, "SaveName", 0)), true);
            SetKV(classKv, "Play time", PlayTimeText(DashboardData.Value(mem, ctx, "PlayTime", 0)), true);
            SetKV(classKv, "Screen type", DashboardData.Value(mem, ctx, "ScreenType", 0), true);

            SetKV(sessionKv, "Global S", Addr(ctx.GlobalRoot), ctx.GlobalRoot != 0);
            SetKV(sessionKv, "Class data", Addr(ctx.ClassData), ctx.ClassData != 0);
            SetKV(sessionKv, "Selected char", Addr(ctx.SelectedChar), ctx.SelectedChar != 0);
            SetKV(sessionKv, "Character data", Addr(ctx.SelectedCharData), ctx.SelectedCharData != 0);
            SetKV(sessionKv, "Status", Addr(ctx.Status), ctx.Status != 0);
            SetKV(sessionKv, "Activity", Addr(ctx.Activity), ctx.Activity != 0);
            SetKV(sessionKv, "Relations", Addr(ctx.Relations), ctx.Relations != 0);
            SetHealthSession();

            LoadPortrait();
            LoadSeatPortrait();
        }

        private void ClearOverview()
        {
            if (charKv != null) foreach (Label l in charKv.Values) { l.Text = "-"; l.ForeColor = Color.DimGray; }
            if (statusKv != null) foreach (Label l in statusKv.Values) { l.Text = "-"; l.ForeColor = Color.DimGray; }
            if (classKv != null) foreach (Label l in classKv.Values) { l.Text = "-"; l.ForeColor = Color.DimGray; }
            if (sessionKv != null) foreach (Label l in sessionKv.Values) { l.Text = "-"; l.ForeColor = Color.DimGray; }
            DisposePortrait();
            ClearSeatPortrait();
            if (portraitName != null) portraitName.Text = "No character";
        }

        private void LoadPortrait()
        {
            if (!mem.IsAttached || ctx.Selected == null)
            {
                DisposePortrait();
                portraitName.Text = "No character";
                return;
            }
            portraitName.Text = ctx.Selected.Name;
            try
            {
                uint addr;
                uint size;
                byte[] data = ReadPortraitBytes("PotraitSize", "PotraitAddr", out addr, out size);
                if (data == null)
                    data = ReadPortraitBytes("ThumbSize", "ThumbAddr", out addr, out size);
                if (data == null || data.Length < 8)
                {
                    DisposePortrait();
                    portraitName.Text = ctx.Selected.Name + " (no portrait)";
                    return;
                }
                if (addr == lastPortraitAddr && size == lastPortraitSize && portraitBox.Image != null)
                {
                    portraitName.Text = ctx.Selected.Name;
                    return;
                }
                using (MemoryStream ms = new MemoryStream(data))
                using (Image img = Image.FromStream(ms))
                {
                    DisposePortrait();
                    portraitBox.Image = new Bitmap(img);
                }
                lastPortraitAddr = addr;
                lastPortraitSize = size;
            }
            catch
            {
                DisposePortrait();
                portraitName.Text = ctx.Selected.Name + " (portrait unreadable)";
            }
        }

        private void DisposePortrait()
        {
            lastPortraitAddr = 0;
            lastPortraitSize = 0;
            if (portraitBox != null && portraitBox.Image != null)
            {
                portraitBox.Image.Dispose();
                portraitBox.Image = null;
            }
        }

        private void LoadSeatPortrait()
        {
            if (seatPortraitName == null) return;
            if (!mem.IsAttached || ctx.Selected == null)
            {
                ClearSeatPortrait();
                return;
            }

            string seat = ctx.Selected.Seat >= 0 ? ctx.Selected.Seat.ToString("D2") : "??";
            string name = string.IsNullOrEmpty(ctx.Selected.Name) ? "(unnamed)" : ctx.Selected.Name;
            seatPortraitName.Text = seat + "  " + name + (ctx.Selected.IsPlayer ? "  [PC]" : "");

            uint address;
            uint size;
            PortraitLoader.ReadFullInfo(mem, ctx, out address, out size);

            if (seatPortrait != null && seatPortraitPointer == ctx.Selected.Pointer &&
                seatPortrait.Address == address && seatPortrait.Size == size)
                return;

            if (seatPortraitBox != null) seatPortraitBox.Image = null;
            if (seatPortrait != null)
            {
                seatPortrait.Dispose();
                seatPortrait = null;
            }

            seatPortrait = PortraitLoader.Load(mem, address, size);
            seatPortraitPointer = ctx.Selected.Pointer;

            bool hasImage = seatPortrait != null && seatPortrait.Bitmap != null;
            if (seatPortraitBox != null)
            {
                seatPortraitBox.Image = hasImage ? seatPortrait.Bitmap : null;
                seatPortraitBox.Visible = hasImage;
            }
            if (seatPortraitPlaceholder != null)
                seatPortraitPlaceholder.Visible = !hasImage;
        }

        private void ClearSeatPortrait()
        {
            if (seatPortraitBox != null) seatPortraitBox.Image = null;
            if (seatPortrait != null)
            {
                seatPortrait.Dispose();
                seatPortrait = null;
            }
            seatPortraitPointer = 0;
            if (seatPortraitName != null) seatPortraitName.Text = "No character";
            if (seatPortraitBox != null) seatPortraitBox.Visible = false;
            if (seatPortraitPlaceholder != null) seatPortraitPlaceholder.Visible = true;
        }

        private byte[] ReadPortraitBytes(string sizeName, string addrName, out uint addr, out uint size)
        {
            addr = 0;
            size = 0;
            EvalResult sizeResult = DashboardData.Eval(mem, ctx, sizeName, 0);
            EvalResult addrResult = DashboardData.Eval(mem, ctx, addrName, 0);
            if (sizeResult == null || addrResult == null) return null;
            if (sizeResult.Bytes == null || sizeResult.Bytes.Length < 4) return null;
            if (addrResult.Bytes == null || addrResult.Bytes.Length < 4) return null;

            size = BitConverter.ToUInt32(sizeResult.Bytes, 0);
            addr = BitConverter.ToUInt32(addrResult.Bytes, 0);
            if (size == 0 || size > 8 * 1024 * 1024 || !mem.IsPlausiblePointer(addr)) return null;
            return mem.ReadBytes(addr, (int)size);
        }

        private string StudyText(string levelName, string rankName, string valueName)
        {
            int? level = LeadingInt(DashboardData.Value(mem, ctx, levelName, 0));
            int? rank = rankName == null ? null : LeadingInt(DashboardData.Value(mem, ctx, rankName, 0));
            int? value = LeadingInt(DashboardData.Value(mem, ctx, valueName, 0));
            if (rankName == null)
                return "rank " + (level.HasValue ? level.Value.ToString() : "-") +
                       ", value " + (value.HasValue ? value.Value.ToString() : "-");
            return "Lv " + (level.HasValue ? level.Value.ToString() : "-") +
                   ", rank " + (rank.HasValue ? rank.Value.ToString() : "-") +
                   ", value " + (value.HasValue ? value.Value.ToString() : "-");
        }

        private string TraitSummary()
        {
            int count = 0;
            string first = null;
            foreach (PointerDef d in DashboardData.Personal("Traits & preferences"))
            {
                EvalResult r = DashboardData.Eval(mem, ctx, d, 0);
                if (r != null && r.Bytes != null && r.Bytes.Length >= 1 && r.Bytes[0] != 0)
                {
                    count++;
                    if (first == null) first = d.Name;
                }
            }
            if (count == 0) return "none";
            return count.ToString() + " active" + (first != null ? "  (" + first + ")" : "");
        }

        private string ClubName(GameMemory mem, GameContext ctx)
        {
            EvalResult club = DashboardData.Eval(mem, ctx, "Club", 0);
            if (club == null || club.Bytes == null || club.Bytes.Length < 1) return "-";
            int index = club.Bytes[0];
            string defName = "Club" + (index + 1).ToString() + "Name";
            string name = DashboardData.Value(mem, ctx, defName, 0);
            if (name == null || name == "-" || name == "\"\"") return "Club " + index.ToString();
            return name.Trim('"') + "  [" + index.ToString() + "]";
        }

        private List<CharacterChoice> BuildPersonalityChoices()
        {
            List<CharacterChoice> list = new List<CharacterChoice>();
            HashSet<int> seen = new HashSet<int>();

            // Root XMLs can define modded personalities well above the 0-29
            // range used by the base game (for example 78 Succubus).
            if (names != null)
            {
                foreach (KeyValuePair<int, string> pair in names.Entries("PERSONALITY"))
                {
                    if (pair.Key < 0 || pair.Key > 255) continue;
                    if (string.IsNullOrEmpty(pair.Value)) continue;
                    list.Add(new CharacterChoice(pair.Key,
                        pair.Key.ToString("D2") + "  " + pair.Value));
                    seen.Add(pair.Key);
                }
            }

            for (int i = 0; i < PersonalityNames.Length; i++)
            {
                if (seen.Contains(i)) continue;
                string name = PersonalityNames[i];
                if (string.IsNullOrEmpty(name))
                    name = "Personality " + i.ToString();
                list.Add(new CharacterChoice(i, i.ToString("D2") + "  " + name));
                seen.Add(i);
            }

            list.Sort(delegate(CharacterChoice a, CharacterChoice b)
            {
                return a.Value.CompareTo(b.Value);
            });
            return list;
        }

        private List<CharacterChoice> BuildClubChoices()
        {
            List<CharacterChoice> list = new List<CharacterChoice>();
            for (int i = 0; i < 8; i++)
            {
                string name = DashboardData.Value(mem, ctx,
                    "Club" + (i + 1).ToString() + "Name", 0);
                name = StripQuotes(name == null ? "" : name).Trim();
                if (string.IsNullOrEmpty(name) || name == "-" || name == "BROKEN")
                    name = "Club " + (i + 1).ToString();
                list.Add(new CharacterChoice(i, i.ToString() + "  " + name));
            }
            return list;
        }

        private CharacterChoiceSet BuildCharacterChoices()
        {
            CharacterChoiceSet set = new CharacterChoiceSet();
            set.Personality = BuildPersonalityChoices();
            set.Club = BuildClubChoices();

            List<CharacterChoice> lowHigh = BuildEnumChoices("LOW_HIGH", 4);
            set.Social = lowHigh;
            set.Virtue = lowHigh;
            set.VoicePitch = lowHigh;
            set.FightingStyle = BuildEnumChoices("FIGHTSTYLE", 2);
            set.SexualOrientation = BuildEnumChoices("SEXUALITY", 4);
            set.Intelligence = BuildEnumChoices("LOW_HIGHPLUS", 5);
            set.Grade = BuildGradeChoices();
            return set;
        }

        private List<CharacterChoice> BuildGradeChoices()
        {
            List<CharacterChoice> list = new List<CharacterChoice>();
            list.Add(new CharacterChoice(255, "None"));
            string[] grades = new string[] { "F", "E", "D", "C", "B", "A" };
            for (int i = 0; i < grades.Length; i++)
                list.Add(new CharacterChoice(i, grades[i]));
            return list;
        }

        private List<CharacterChoice> BuildEnumChoices(string key, int fallbackMax)
        {
            List<CharacterChoice> list = new List<CharacterChoice>();
            HashSet<int> seen = new HashSet<int>();

            if (names != null)
            {
                foreach (KeyValuePair<int, string> pair in names.Entries(key))
                {
                    if (pair.Key < 0 || pair.Key > 255) continue;
                    if (string.IsNullOrEmpty(pair.Value)) continue;
                    list.Add(new CharacterChoice(pair.Key,
                        pair.Key.ToString("D2") + "  " + pair.Value));
                    seen.Add(pair.Key);
                }
            }

            for (int i = 0; i <= fallbackMax; i++)
            {
                if (seen.Contains(i)) continue;
                list.Add(new CharacterChoice(i, i.ToString("D2") + "  (unnamed)"));
                seen.Add(i);
            }

            list.Sort(delegate(CharacterChoice a, CharacterChoice b)
            {
                return a.Value.CompareTo(b.Value);
            });
            return list;
        }

        private string PersonalityName(int value)
        {
            string name = names.Get("PERSONALITY", value);
            if (string.IsNullOrEmpty(name) && value >= 0 && value < PersonalityNames.Length)
                name = PersonalityNames[value];
            if (!string.IsNullOrEmpty(name))
                return value.ToString("D2") + "  " + name;
            return value.ToString() + "  (custom)";
        }

        private string PersonalityFromValue(string value)
        {
            int? n = LeadingInt(value);
            if (n.HasValue) return PersonalityName(n.Value);
            return value;
        }

        private string GenderName(string value)
        {
            int? n = LeadingInt(value);
            if (n.HasValue)
            {
                string name = names.Get("GENDER", n.Value);
                if (!string.IsNullOrEmpty(name)) return name + "  (" + n.Value.ToString() + ")";
                if (n.Value == 0) return "Male";
                if (n.Value == 1) return "Female";
            }
            return value;
        }

        private static string ResponseText(string value)
        {
            int? n = LeadingInt(value);
            if (!n.HasValue) return value;
            string name;
            if (ResponseNames.TryGetValue(n.Value, out name)) return n.Value.ToString() + "  " + name;
            return value;
        }

        private static string PlayTimeText(string value)
        {
            int? n = LeadingInt(value);
            if (!n.HasValue || n.Value < 0) return value;
            TimeSpan t = TimeSpan.FromSeconds(n.Value);
            return value + "  (" + ((int)t.TotalHours).ToString("D2") + ":" +
                   t.Minutes.ToString("D2") + ":" + t.Seconds.ToString("D2") + ")";
        }

        private static string AsciiDigitText(string value)
        {
            int? n = LeadingInt(value);
            if (n.HasValue && n.Value >= 0x30 && n.Value <= 0x39)
                return ((char)n.Value).ToString() + "  (0x" + n.Value.ToString("X2") + ")";
            return value;
        }

        private static string StripQuotes(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
                return value.Substring(1, value.Length - 2);
            return value;
        }

        private static int? LeadingInt(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            int i = 0;
            if (value[0] == '-') i = 1;
            int start = i;
            while (i < value.Length && value[i] >= '0' && value[i] <= '9') i++;
            if (i == start) return null;
            int n;
            if (int.TryParse(value.Substring(0, i), NumberStyles.Integer, CultureInfo.InvariantCulture, out n))
                return n;
            return null;
        }

        private static string Addr(uint value)
        {
            return value == 0 ? "0x00000000" : "0x" + value.ToString("X8");
        }

        private static string PointerOrNumberText(string value)
        {
            int? n = LeadingInt(value);
            if (n.HasValue && n.Value >= 0x10000)
                return "0x" + ((uint)n.Value).ToString("X8");
            return value;
        }

        private static void SetKV(Dictionary<string, Label> map, string key, string value, bool ok)
        {
            if (map == null) return;
            Label l;
            if (!map.TryGetValue(key, out l)) return;
            l.Text = value;
            l.ForeColor = ok ? Color.Black : Color.DimGray;
        }

        #endregion

        #region Grid filters

        private void BuildAllDefinitions()
        {
            buildingDefinitions = true;
            try
            {
                ApplyPersonalFilter();
                ApplyRelationFilter();
                ApplyActivityFilter();
                ApplyClassFilter();
                ApplyConfigFilter();
                ApplyPointerFilter();
            }
            finally
            {
                buildingDefinitions = false;
            }
        }

        private void QueueFilter(string which)
        {
            if (buildingDefinitions || filterTimer == null) return;
            pendingFilter = which;
            filterTimer.Stop();
            filterTimer.Start();
        }

        private void ApplyPendingFilter()
        {
            if (pendingFilter == "personal") ApplyPersonalFilter();
            else if (pendingFilter == "relation") ApplyRelationFilter();
            else if (pendingFilter == "activity") ApplyActivityFilter();
            else if (pendingFilter == "class") ApplyClassFilter();
            else if (pendingFilter == "config") ApplyConfigFilter();
            else if (pendingFilter == "pointer") ApplyPointerFilter();
        }

        private static List<PointerDef> FilterDefs(List<PointerDef> defs, string filter)
        {
            if (defs == null) return new List<PointerDef>();
            string f = (filter == null ? "" : filter.Trim().ToLowerInvariant());
            if (f.Length == 0) return defs;
            List<PointerDef> result = new List<PointerDef>();
            foreach (PointerDef d in defs)
            {
                string hay = (d.Name + " " + d.Raw + " " + d.Category + " " + d.Context).ToLowerInvariant();
                if (hay.IndexOf(f, StringComparison.Ordinal) >= 0) result.Add(d);
            }
            return result;
        }

        private void ApplyPersonalFilter()
        {
            string group = personalGroup == null ? "All personal" : (personalGroup.SelectedItem as string);
            if (group == null) group = "All personal";
            List<PointerDef> defs = FilterDefs(DashboardData.Personal(group), personalFilter == null ? "" : personalFilter.Text);
            personalGrid.SetDefinitions(defs);
            if (mem.IsAttached && ctx.Selected != null) personalGrid.RefreshValues(mem, ctx, 0);
        }

        private void ApplyRelationFilter()
        {
            string group = relationGroup == null ? "Relationship" : (relationGroup.SelectedItem as string);
            if (group == null) group = "Relationship";
            List<PointerDef> defs = FilterDefs(DashboardData.Relations(group), relationFilter == null ? "" : relationFilter.Text);
            relationGrid.SetDefinitions(defs);
            if (mem.IsAttached && ctx.Selected != null)
                relationGrid.RefreshValues(mem, ctx, relationIndex == null ? 0 : (int)relationIndex.Value);
        }

        private void ApplyActivityFilter()
        {
            List<PointerDef> defs = FilterDefs(DashboardData.Activity(), activityFilter == null ? "" : activityFilter.Text);
            activityGrid.SetDefinitions(defs);
            if (mem.IsAttached && ctx.Selected != null) activityGrid.RefreshValues(mem, ctx, 0);
        }

        private void ApplyClassFilter()
        {
            List<PointerDef> defs = FilterDefs(DashboardData.ClassInfo(), classFilter == null ? "" : classFilter.Text);
            classGrid.SetDefinitions(defs);
            if (mem.IsAttached && ctx.Selected != null) classGrid.RefreshValues(mem, ctx, 0);
        }

        private void ApplyConfigFilter()
        {
            List<PointerDef> defs = FilterDefs(DashboardData.Config(), configFilter == null ? "" : configFilter.Text);
            configGrid.SetDefinitions(defs);
            if (mem.IsAttached && ctx.Selected != null) configGrid.RefreshValues(mem, ctx, 0);
        }

        private void ApplyPointerFilter()
        {
            List<PointerDef> defs = FilterDefs(DashboardData.Pointers(), pointerFilter == null ? "" : pointerFilter.Text);
            pointerGrid.HideOkRows = pointerProblems != null && pointerProblems.Checked;
            pointerGrid.SetDefinitions(defs);
            if (mem.IsAttached && ctx.Selected != null)
            {
                pointerGrid.RefreshValues(mem, ctx, 0);
                RefreshPointerDetails();
            }
        }

        private void RefreshPointerDetails()
        {
            if (pointerGrid != null && pointerGrid.SelectedRow != null)
                ShowPointerDetails(pointerGrid.SelectedRow);
            else if (pointerDetails != null)
                pointerDetails.Text = "No visible pointer row.";
        }

        private void RefreshCurrentTabValues()
        {
            if (tabs == null) return;
            if (!mem.IsAttached || ctx.Selected == null)
            {
                if (roster != null) roster.Clear();
                if (characterData != null) characterData.Clear();
                if (traits != null) traits.Clear();
                return;
            }
            switch (tabs.SelectedIndex)
            {
                case 0:
                    if (roster != null)
                        roster.RefreshRoster(mem, ctx, ctx.Selected, names);
                    break;
                case 1:
                    if (characterEditorTabs != null && characterEditorTabs.SelectedIndex == 0 &&
                        characterData != null)
                    {
                        characterData.SetChoices(BuildCharacterChoices());
                        characterData.SetTarget(mem, ctx.Selected, writesAllowed);
                    }
                    else if (characterEditorTabs != null && characterEditorTabs.SelectedIndex == 1 &&
                        traits != null)
                    {
                        traits.SetTarget(mem, ctx.Selected, writesAllowed);
                    }
                    break;
                case 2:
                    if (saveViewer != null)
                    {
                        saveViewer.SetGameRoot(GetGameRoot());
                        saveViewer.SetLiveCharacters(ctx.Characters);
                        if (ctx.Selected != null) saveViewer.SetSelectedSeat(ctx.Selected.Seat);
                    }
                    break;
                case 3:
                    RefreshDebugTabValues();
                    break;
            }
        }

        private void RefreshDebugTabValues()
        {
            if (debugTabs == null || !mem.IsAttached || ctx.Selected == null) return;
            switch (debugTabs.SelectedIndex)
            {
                case 0:
                    UpdateOverview();
                    break;
                case 1:
                    personalGrid.RefreshValues(mem, ctx, 0);
                    break;
                case 2:
                    relationGrid.RefreshValues(mem, ctx,
                        relationIndex == null ? 0 : (int)relationIndex.Value);
                    break;
                case 3:
                    activityGrid.RefreshValues(mem, ctx, 0);
                    break;
                case 4:
                    classGrid.RefreshValues(mem, ctx, 0);
                    break;
                case 5:
                    configGrid.RefreshValues(mem, ctx, 0);
                    break;
                case 6:
                    pointerGrid.RefreshValues(mem, ctx, 0);
                    RefreshPointerDetails();
                    break;
            }
        }

        private void ShowPointerDetails(GridRowData data)
        {
            if (pointerDetails == null || data == null || data.Def == null) return;
            EvalResult r = data.Result;
            if (r == null && mem.IsAttached && ctx.Selected != null)
                r = Evaluator.Evaluate(mem, ctx, data.Def, 0);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Name:     " + data.Def.Name);
            sb.AppendLine("Category: " + data.Def.Category + "    Type: " + Evaluator.TypeName(data.Def.Type) +
                          "    Context: " + data.Def.Context);
            sb.AppendLine("Address:  " + (r != null && r.HasAddress ? "0x" + r.Address.ToString("X8") : "-"));
            sb.AppendLine("Status:   " + (data.Def.Optional
                ? "OPTIONAL / legacy (not gated)"
                : (r != null ? r.Status : "-")));
            if (data.Def.Optional) sb.AppendLine("Health:   excluded from the write gate");
            sb.AppendLine();
            if (r != null) sb.AppendLine(r.Trace);
            if (r != null && r.Bytes != null)
            {
                sb.Append("Bytes:    ");
                int n = Math.Min(r.Bytes.Length, 32);
                for (int i = 0; i < n; i++)
                {
                    if (i > 0) sb.Append(' ');
                    sb.Append(r.Bytes[i].ToString("X2"));
                }
                if (r.Bytes.Length > n) sb.Append(" ...");
                sb.AppendLine();
            }
            pointerDetails.Text = sb.ToString();
            pointerDetails.SelectionStart = 0;
            pointerDetails.SelectionLength = 0;
        }

        #endregion

        #region Summary

        private void SetSummary()
        {
            if (!mem.IsAttached || ctx.Selected == null)
            {
                SetSummaryText("Not attached.");
                return;
            }
            string className = DashboardData.Value(mem, ctx, "ClassName", 0).Trim('"');
            if (gameStatusLabel != null)
            {
                gameStatusLabel.Text = "AA2Play.exe   PID " + mem.Pid + "   |   module 0x" +
                                       mem.ModuleBase.ToString("X8");
                gameStatusLabel.ForeColor = Color.DarkGreen;
            }
            SetSummaryText("Class: " + className + "   |   " + ctx.Characters.Count +
                           " characters   |   selected: " + ctx.SelectedName);
            if (classLabel != null)
                classLabel.Text = className + "   |   save: " +
                    DashboardData.Value(mem, ctx, "SaveName", 0).Trim('"') +
                    "   |   " + ctx.Characters.Count + " loaded";
            if (updatedLabel != null)
                updatedLabel.Text = "Updated " + DateTime.Now.ToString("HH:mm:ss");
        }

        private void SetSummaryText(string text)
        {
            if (summaryLabel != null) summaryLabel.Text = text;
            if (updatedLabel != null) updatedLabel.Text = "Updated " + DateTime.Now.ToString("HH:mm:ss");
        }

        #endregion

        private static Dictionary<int, string> BuildResponseNames()
        {
            Dictionary<int, string> d = new Dictionary<int, string>();
            d[-1] = "None";
            d[0] = "No/None";
            d[1] = "Yes";
            d[2] = "Huh";
            return d;
        }
    }
}
