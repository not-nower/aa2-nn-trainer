using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace AA2Trainer
{
    public sealed partial class MainForm : Form
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
