using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace AA2Trainer
{
    public sealed class OptionsControl : UserControl
    {
        public Func<bool> ConfirmWriteAllowed { get; set; }
        public Func<Dictionary<string, SortedDictionary<int, string>>> NameSnapshot { get; set; }

        public event EventHandler RefreshRequested;
        public event EventHandler RecheckRequested;
        public event EventHandler DetailsRequested;
        public event EventHandler ReloadNamesRequested;
        public event EventHandler LiveRefreshChanged;

        public bool LiveRefreshEnabled
        {
            get { return liveRefreshBox != null && liveRefreshBox.Checked; }
        }

        public bool LiveIntervalAuto
        {
            get { return liveIntervalBox == null || liveIntervalBox.SelectedIndex <= 0; }
        }

        public int LiveIntervalMs
        {
            get
            {
                if (liveIntervalBox == null || liveIntervalBox.SelectedIndex <= 0)
                    return LiveActiveIntervalMs;

                string text = liveIntervalBox.SelectedItem as string;
                if (string.IsNullOrEmpty(text)) return LiveIdleIntervalMs;
                int space = text.IndexOf(' ');
                string number = space > 0 ? text.Substring(0, space) : text;
                int ms;
                if (int.TryParse(number, out ms)) return Math.Max(50, ms);
                return LiveIdleIntervalMs;
            }
        }

        private const int LiveActiveIntervalMs = 100;
        private const int LiveIdleIntervalMs = 500;

        private GameMemory mem;
        private GameContext ctx;
        private bool gateAllowed;
        private OptionSnapshot loaded;
        private bool dirty;
        private bool suppress;
        private bool applying;

        private readonly Dictionary<int, CheckBox> toggles =
            new Dictionary<int, CheckBox>();
        private readonly Dictionary<int, TrackBar> sliders =
            new Dictionary<int, TrackBar>();
        private readonly Dictionary<int, Label> sliderValues =
            new Dictionary<int, Label>();
        private readonly Dictionary<int, NumericUpDown> preciseInputs =
            new Dictionary<int, NumericUpDown>();
        private readonly Dictionary<int, ComboBox> choices =
            new Dictionary<int, ComboBox>();
        private readonly Dictionary<int, List<Control>> pairDependents =
            new Dictionary<int, List<Control>>();
        private bool syncingPrecise;

        private TabControl tabs;
        private Button applyButton;
        private Button revertButton;
        private CheckBox liveRefreshBox;
        private ComboBox liveIntervalBox;
        private Label nameTableStatus;
        private Label nameTableNote;
        private Button updateNameTablesButton;
        private Button resetNameTablesButton;
        private Label gateLabel;
        private Label statusLabel;
        private ToolTip tooltip;

        public OptionsControl()
        {
            DoubleBuffered = true;
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(248, 249, 251);
            BuildUi();
        }

        private void BuildUi()
        {
            tooltip = new ToolTip();
            tooltip.AutoPopDelay = 10000;
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

            tabs = new TabControl();
            tabs.Dock = DockStyle.Fill;
            tabs.Padding = new Point(12, 6);
            tabs.TabPages.Add(BuildTrainerPage());
            tabs.TabPages.Add(BuildSystemPage());
            tabs.TabPages.Add(BuildSoundPage());
            tabs.TabPages.Add(BuildAdditional1Page());
            tabs.TabPages.Add(BuildAdditional2Page());
            tabs.TabPages.Add(BuildAdditional3Page());
            root.Controls.Add(tabs, 0, 0);

            TableLayoutPanel actions = new TableLayoutPanel();
            actions.Dock = DockStyle.Fill;
            actions.ColumnCount = 4;
            actions.RowCount = 1;
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96F));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96F));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 16F));
            actions.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            actions.Margin = new Padding(0, 4, 0, 0);
            root.Controls.Add(actions, 0, 1);

            gateLabel = new Label();
            gateLabel.AutoSize = true;
            gateLabel.Margin = new Padding(14, 10, 0, 0);
            gateLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            gateLabel.Text = "Write gate: locked";
            gateLabel.ForeColor = Color.Firebrick;
            actions.Controls.Add(gateLabel, 0, 0);

            revertButton = new Button();
            revertButton.Text = "Revert";
            revertButton.Dock = DockStyle.Fill;
            revertButton.Margin = new Padding(4, 3, 4, 3);
            revertButton.Click += delegate { RevertChanges(); };
            actions.Controls.Add(revertButton, 1, 0);

            applyButton = new Button();
            applyButton.Text = "Apply";
            applyButton.Dock = DockStyle.Fill;
            applyButton.Margin = new Padding(4, 3, 4, 3);
            applyButton.Click += delegate { ApplyChanges(); };
            actions.Controls.Add(applyButton, 2, 0);

            statusLabel = new Label();
            statusLabel.Dock = DockStyle.Fill;
            statusLabel.AutoSize = false;
            statusLabel.AutoEllipsis = true;
            statusLabel.TextAlign = ContentAlignment.TopLeft;
            statusLabel.ForeColor = Color.DimGray;
            statusLabel.Margin = new Padding(0);
            root.Controls.Add(statusLabel, 0, 2);

            tooltip.SetToolTip(gateLabel,
                "Game options are written only when the known game build and all " +
                "pointer checks pass.");

            UpdateGate();
            SetStatus("Attach to the game and load a class to edit options.", false);
        }

        private TabPage BuildTrainerPage()
        {
            TabPage page = new TabPage("Trainer");
            page.BackColor = Color.FromArgb(248, 249, 251);

            FlowLayoutPanel host = new FlowLayoutPanel();
            host.Dock = DockStyle.Fill;
            host.AutoScroll = false;
            host.FlowDirection = FlowDirection.TopDown;
            host.WrapContents = false;
            host.Padding = new Padding(8, 6, 8, 6);
            host.BackColor = Color.FromArgb(248, 249, 251);
            host.Resize += delegate { ResizeGroups(host); };
            host.ControlAdded += delegate { ResizeGroups(host); };
            page.Controls.Add(host);

            TableLayoutPanel session = NewGroup(host, "Session", 5, false);
            EnsureRowCount(session, 2);

            AddTrainerButton(session, 0, "Refresh", RequestRefresh);
            AddTrainerButton(session, 1, "Recheck", RequestRecheck);
            AddTrainerButton(session, 2, "Details", RequestDetails);
            AddTrainerButton(session, 3, "Reload names", RequestReloadNames);

            liveRefreshBox = new CheckBox();
            liveRefreshBox.Text = "Live refresh";
            liveRefreshBox.AutoSize = true;
            liveRefreshBox.Anchor = AnchorStyles.Left;
            liveRefreshBox.Margin = new Padding(6, 8, 3, 3);
            liveRefreshBox.AccessibleName = "Live refresh";
            liveRefreshBox.CheckedChanged += delegate
            {
                UpdateLiveControls();
                if (LiveRefreshChanged != null)
                    LiveRefreshChanged(this, EventArgs.Empty);
            };
            session.Controls.Add(liveRefreshBox, 0, 1);

            liveIntervalBox = new ComboBox();
            liveIntervalBox.DropDownStyle = ComboBoxStyle.DropDownList;
            liveIntervalBox.Anchor = AnchorStyles.Left;
            liveIntervalBox.Width = 160;
            liveIntervalBox.Margin = new Padding(6, 6, 3, 3);
            liveIntervalBox.AccessibleName = "Live refresh rate";
            liveIntervalBox.Items.AddRange(new object[]
            {
                "Auto (100/500)", "100 ms", "250 ms", "500 ms", "1000 ms"
            });
            liveIntervalBox.SelectedIndex = 0;
            liveIntervalBox.SelectedIndexChanged += delegate
            {
                if (LiveRefreshChanged != null)
                    LiveRefreshChanged(this, EventArgs.Empty);
            };
            session.Controls.Add(liveIntervalBox, 1, 1);
            session.SetColumnSpan(liveIntervalBox, 2);

            Label description = new Label();
            description.Text = "Refresh re-reads class data and the active tab " +
                "(pending schedule edits are discarded). Recheck re-runs the health " +
                "check and updates the write gate. Details shows the last health " +
                "check. Reload names re-reads the XML name files. Update from XMLs " +
                "imports newer community name tables into a local overlay. " +
                "Live refresh updates " +
                "the roster and Character Data status at the selected rate; Auto uses " +
                "100 ms while a character is active and 500 ms when idle.";            description.AutoSize = true;
            description.MaximumSize = new Size(800, 0);
            description.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            description.ForeColor = Color.Gray;
            description.Margin = new Padding(6, 10, 6, 4);
            session.Controls.Add(description, 0, 2);
            session.SetColumnSpan(description, 5);
            session.Resize += delegate
            {
                int width = session.ClientSize.Width - 16;
                if (width < 200) width = 200;
                if (description.MaximumSize.Width != width)
                    description.MaximumSize = new Size(width, 0);
            };

            liveRefreshBox.Checked = true;
            UpdateLiveControls();

            TableLayoutPanel nameGroup = NewGroup(host, "Name tables", 2, false);
            EnsureRowCount(nameGroup, 2);

            nameTableStatus = new Label();
            nameTableStatus.AutoSize = true;
            nameTableStatus.Anchor = AnchorStyles.Left;
            nameTableStatus.ForeColor = Color.DimGray;
            nameTableStatus.Margin = new Padding(6, 4, 6, 2);
            nameGroup.Controls.Add(nameTableStatus, 0, 0);
            nameGroup.SetColumnSpan(nameTableStatus, 2);

            updateNameTablesButton = new Button();
            updateNameTablesButton.Text = "Update from XMLs...";
            updateNameTablesButton.Dock = DockStyle.Fill;
            updateNameTablesButton.MinimumSize = new Size(0, 30);
            updateNameTablesButton.Margin = new Padding(6, 3, 3, 3);
            updateNameTablesButton.AccessibleName = "Update name tables from XMLs";
            updateNameTablesButton.Click += UpdateNameTables;
            nameGroup.Controls.Add(updateNameTablesButton, 0, 1);

            resetNameTablesButton = new Button();
            resetNameTablesButton.Text = "Reset overlay";
            resetNameTablesButton.Dock = DockStyle.Fill;
            resetNameTablesButton.MinimumSize = new Size(0, 30);
            resetNameTablesButton.Margin = new Padding(3, 3, 6, 3);
            resetNameTablesButton.AccessibleName = "Reset name table overlay";
            resetNameTablesButton.Click += ResetNameTables;
            nameGroup.Controls.Add(resetNameTablesButton, 1, 1);

            nameTableNote = new Label();
            nameTableNote.Text = "Bundled name tables use the " + NameOverlay.BundledLabel +
                " XMLs (clothes.xml, HEXAOC.xml, personalities.xml). Use Update when the " +
                "community releases newer XMLs: the trainer saves NameData.local.tsv beside " +
                "AA2Trainer.exe and reloads the names. The overlay only adds or renames " +
                "entries, it never removes names. Reset deletes the overlay and returns to " +
                "the bundled tables plus the game's XMLs.";
            nameTableNote.AutoSize = true;
            nameTableNote.MaximumSize = new Size(800, 0);
            nameTableNote.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            nameTableNote.ForeColor = Color.Gray;
            nameTableNote.Margin = new Padding(6, 8, 6, 4);
            nameGroup.Controls.Add(nameTableNote, 0, 2);
            nameGroup.SetColumnSpan(nameTableNote, 2);
            nameGroup.Resize += delegate
            {
                int width = nameGroup.ClientSize.Width - 16;
                if (width < 200) width = 200;
                if (nameTableNote.MaximumSize.Width != width)
                    nameTableNote.MaximumSize = new Size(width, 0);
            };

            UpdateNameTableStatus();

            ResizeGroups(host);
            return page;
        }

        private void UpdateNameTableStatus()
        {
            if (nameTableStatus == null) return;
            nameTableStatus.Text = NameOverlay.StatusText();
            if (resetNameTablesButton != null)
                resetNameTablesButton.Enabled = NameOverlay.Exists;
        }

        public void RefreshNameTableStatus()
        {
            UpdateNameTableStatus();
        }

        private void UpdateNameTables(object sender, EventArgs e)
        {
            string root = NameMap.FindGameRoot();
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description =
                    "Select the folder that contains clothes.xml, HEXAOC.xml, " +
                    "or personalities.xml. Only this folder is read.";
                dialog.ShowNewFolderButton = false;
                if (!string.IsNullOrEmpty(root)) dialog.SelectedPath = root;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                try
                {
                    Cursor = Cursors.WaitCursor;
                    Dictionary<string, SortedDictionary<int, string>> baseline =
                        NameSnapshot == null ? null : NameSnapshot();
                    NameOverlayResult result = NameOverlay.MergeFromFolder(
                        dialog.SelectedPath, baseline);
                    UpdateNameTableStatus();
                    if (ReloadNamesRequested != null)
                        ReloadNamesRequested(this, EventArgs.Empty);

                    string message;
                    if (result.Added == 0 && result.Updated == 0)
                    {
                        message = "No newer names found in " + result.Files +
                            (result.Files == 1 ? " file" : " files") + " (" +
                            result.Checked + " checked).\n\nThe bundled tables are up to date.";
                    }
                    else
                    {
                        message = result.Files + (result.Files == 1 ? " file" : " files") +
                            ": " + result.Added + " added, " + result.Updated + " renamed, " +
                            result.Unchanged + " unchanged.\n\nThe overlay now holds " +
                            result.Total + " names.";
                    }
                    MessageBox.Show(this, message, "Name tables",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "Name tables",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    Cursor = Cursors.Default;
                }
            }
        }

        private void ResetNameTables(object sender, EventArgs e)
        {
            if (!NameOverlay.Exists) return;
            if (MessageBox.Show(this,
                "Delete " + NameOverlay.FileName + " and return to the bundled name tables?",
                "Name tables", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            try
            {
                NameOverlay.Clear();
                UpdateNameTableStatus();
                if (ReloadNamesRequested != null)
                    ReloadNamesRequested(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Name tables",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateLiveControls()
        {
            if (liveIntervalBox != null)
                liveIntervalBox.Enabled = liveRefreshBox != null && liveRefreshBox.Checked;
        }

        private void AddTrainerButton(TableLayoutPanel table, int column,
            string text, EventHandler handler)
        {
            Button button = new Button();
            button.Text = text;
            button.Dock = DockStyle.Fill;
            button.MinimumSize = new Size(0, 30);
            button.Margin = new Padding(4, 3, 4, 3);
            button.Click += handler;
            table.Controls.Add(button, column, 0);
        }

        private void RequestRefresh(object sender, EventArgs e)
        {
            if (RefreshRequested != null) RefreshRequested(this, EventArgs.Empty);
        }

        private void RequestRecheck(object sender, EventArgs e)
        {
            if (RecheckRequested != null) RecheckRequested(this, EventArgs.Empty);
        }

        private void RequestDetails(object sender, EventArgs e)
        {
            if (DetailsRequested != null) DetailsRequested(this, EventArgs.Empty);
        }

        private void RequestReloadNames(object sender, EventArgs e)
        {
            if (ReloadNamesRequested != null) ReloadNamesRequested(this, EventArgs.Empty);
            UpdateNameTableStatus();
        }

        private TabPage BuildSystemPage()
        {
            FlowLayoutPanel host;
            TabPage page = NewScrollPage("System", out host);

            AddToggleGroup(host, "Display", new string[]
            {
                "DisplayShadow", "DisplayOutline", "DisplayCrosshair"
            }, 1);

            AddToggleGroup(host, "H-Scene Settings", new string[]
            {
                "DisplayPlayer", "DisplayNPC", "DisplayPenis", "CameraAngle",
                "DisplayBGChar", "Display3D", "ClickFocus"
            }, 2);

            TableLayoutPanel mono = NewGroup(host, "Monochrome/Transparent Setting", 4, true);
            int row = 0;
            AddToggle(mono, row++, "MonoMan", 4);
            AddSliderRow(mono, row++, "MonoManOpa", "Opacity");
            AddSliderRow(mono, row++, "MonoManRed", "Red");
            AddSliderRow(mono, row++, "MonoManGreen", "Green");
            AddSliderRow(mono, row++, "MonoManBlue", "Blue");

            ResizeGroups(host);
            return page;
        }

        private TabPage BuildSoundPage()
        {
            FlowLayoutPanel host;
            TabPage page = NewScrollPage("Sound", out host);

            TableLayoutPanel general = NewGroup(host, "General", 4, true);
            int row = 0;
            AddPairRow(general, row++, "BGM", "BGMVal", true, "0 - 100%");
            AddPairRow(general, row++, "Sound", "SoundVal", true, "0 - 100%");
            AddPairRow(general, row++, "Voice", "VoiceVal", true, "0 - 100%");
            AddPairRow(general, row++, "HSound", "HSoundVal", true, "0 - 100%");

            ResizeGroups(host);
            return page;
        }

        private TabPage BuildAdditional1Page()
        {
            FlowLayoutPanel host;
            TabPage page = NewScrollPage("Additional(1)", out host);

            TableLayoutPanel misc = NewGroup(host, "Misc", 4, true);
            int row = 0;
            AddPairRow(misc, row++, "NPCConversion", "NPCConversionVal");
            AddToggleWithHint(misc, row++, "BGExternal", "/data/CustomBgm");
            AddToggle(misc, row++, "ReleaseMouse", 4);

            TableLayoutPanel movement = NewGroup(host, "Movement", 4, true);
            row = 0;
            AddPairRow(movement, row++, "PlayerMove", "PlayerMoveVal", true,
                "0.1 - 5.0");
            AddPairRow(movement, row++, "NPCMove", "NPCMoveVal", true,
                "0.1 - 5.0");
            AddPairRow(movement, row++, "WorldMove", "WorldMoveVal", true,
                "1.0 - 5.0");

            TableLayoutPanel timeRisk = NewGroup(host, "Time && Risk", 4, true);
            row = 0;
            AddPairRow(timeRisk, row++, "CustomBell", "CustomBellVal", true,
                "1 - 60 min");
            AddToggleChoiceRow(timeRisk, row++, "OverrideRisk", "OverrideRiskVal");
            AddToggleChoiceRow(timeRisk, row++, "OverrideCondom", "OverrideCondomVal");

            ResizeGroups(host);
            return page;
        }

        private TabPage BuildAdditional2Page()
        {
            FlowLayoutPanel host;
            TabPage page = NewScrollPage("Additional(2)", out host);

            AddToggleGroup(host, "Interaction", new string[]
            {
                "DisableInteractionNPCtoPlayer",
                "DisableInteruptionNPCtoPlayer",
                "DisableInteractionNPCtoNPC",
                "DisableInteruptionNPCtoNPC",
                "DisableInteractionAutoPCtoNPC",
                "DisableInteruptionAutoPCtoNPC"
            }, 2);

            AddToggleGroup(host, "Disabled Actions", new string[]
            {
                "DisableABUSEActions",
                "DisableViolenceActions",
                "DisableContestActions",
                "DisableNPCEscape",
                "DisableCOMMANDActions",
                "DisableJOINCLUBActions",
                "DisableBELOVERAction",
                "DisableBREAKUPAction",
                "DisableSKINSHIP",
                "DisableSEXAction",
                "DisableSexualExceptSex",
                "DisableEveryoneCommand",
                "DisableNPCGoingHome",
                "DisableNPCTraining"
            }, 2);

            ResizeGroups(host);
            return page;
        }

        private TabPage BuildAdditional3Page()
        {
            FlowLayoutPanel host;
            TabPage page = NewScrollPage("Additional(3)", out host);

            TableLayoutPanel rates = NewGroup(host, "Relationship Rate Modifiers", 4, true);
            int row = 0;
            AddPairRow(rates, row++, "LOVEorLIKEModPlayertoNPC",
                "LOVEorLIKEModPlayertoNPCVal", true, "0 - 10");
            AddPairRow(rates, row++, "DISLIKEorHATEModPlayertoNPC",
                "DISLIKEorHATEModPlayertoNPCVal", true, "0 - 10");
            AddPairRow(rates, row++, "LOVEorLIKEModNPCtoPlayer",
                "LOVEorLIKEModNPCtoPlayerVal", true, "0 - 10");
            AddPairRow(rates, row++, "DISLIKEorHATEModNPCtoPlayer",
                "DISLIKEorHATEModNPCtoPlayerVal", true, "0 - 10");
            AddPairRow(rates, row++, "LOVEorLIKEModNPCtoNPC",
                "LOVEorLIKEModNPCtoNPCval", true, "0 - 10");
            AddPairRow(rates, row++, "DISLIKEorHATEModNPCtoNPC",
                "DISLIKEorHATEModNPCtoNPCval", true, "0 - 10");

            AddToggleGroup(host, "Other", new string[]
            {
                "PositiveRespontoPlayer", "SuccessfulTraining",
                "SkipHScane", "EveryoneHMood"
            }, 2);

            ResizeGroups(host);
            return page;
        }

        private TabPage NewScrollPage(string title, out FlowLayoutPanel host)
        {
            TabPage page = new TabPage(title);
            page.BackColor = Color.FromArgb(248, 249, 251);

            FlowLayoutPanel scroll = new FlowLayoutPanel();
            host = scroll;
            scroll.Dock = DockStyle.Fill;
            scroll.AutoScroll = true;
            scroll.FlowDirection = FlowDirection.TopDown;
            scroll.WrapContents = false;
            scroll.Padding = new Padding(8, 6, 8, 6);
            scroll.BackColor = Color.FromArgb(248, 249, 251);
            scroll.Resize += delegate { ResizeGroups(scroll); };
            scroll.ControlAdded += delegate { ResizeGroups(scroll); };
            page.Controls.Add(scroll);
            return page;
        }

        private void ResizeGroups(FlowLayoutPanel host)
        {
            if (host == null) return;
            int width = host.ClientSize.Width - host.Padding.Horizontal -
                SystemInformation.VerticalScrollBarWidth - 24;
            if (width < 420) width = 420;

            foreach (Control control in host.Controls)
            {
                if (!(control is GroupBox)) continue;
                control.MinimumSize = new Size(width, 0);
                control.MaximumSize = new Size(width, 0);
                control.Width = width;
            }
        }

        private TableLayoutPanel NewGroup(FlowLayoutPanel host, string title,
            int columns, bool sliderLayout)
        {
            GroupBox group = new GroupBox();
            group.Text = title;
            group.AutoSize = true;
            group.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            group.Margin = new Padding(0, 0, 0, 8);
            group.Padding = new Padding(8, 4, 8, 6);
            group.MinimumSize = new Size(300, 0);

            TableLayoutPanel table = new TableLayoutPanel();
            table.Dock = DockStyle.Top;
            table.AutoSize = true;
            table.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            table.ColumnCount = columns;
            table.RowCount = 0;
            table.Margin = new Padding(0);
            for (int i = 0; i < columns; i++)
            {
                if (sliderLayout && i == 0)
                    table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250F));
                else if (sliderLayout && i == 1)
                    table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                else if (sliderLayout && i == 2)
                    table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64F));
                else if (sliderLayout)
                    table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82F));
                else
                    table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,
                        100F / columns));
            }

            group.Controls.Add(table);
            host.Controls.Add(group);
            return table;
        }

        private void AddToggleGroup(FlowLayoutPanel host, string title,
            string[] names, int columns)
        {
            TableLayoutPanel table = NewGroup(host, title, columns, false);
            int rows = (names.Length + columns - 1) / columns;
            EnsureRowCount(table, rows - 1);
            for (int i = 0; i < names.Length; i++)
            {
                AddToggle(table, i / columns, names[i], 1, i % columns);
            }
        }

        private void AddToggle(TableLayoutPanel table, int row, string name,
            int columnSpan, int column = 0)
        {
            int index = OptionEditor.IndexOf(name);
            if (index < 0) return;
            EnsureRowCount(table, row);
            OptionFieldDef def = OptionEditor.Fields[index];

            CheckBox box = new CheckBox();
            box.Text = def.Label;
            box.AutoSize = true;
            box.Margin = new Padding(3, 3, 3, 3);
            if (columnSpan > 1) table.SetColumnSpan(box, columnSpan);
            table.Controls.Add(box, column, row);
            toggles[index] = box;
            box.CheckedChanged += OptionEdited;
        }

        private void AddToggleWithHint(TableLayoutPanel table, int row, string name,
            string hint)
        {
            int index = OptionEditor.IndexOf(name);
            if (index < 0) return;
            EnsureRowCount(table, row);
            OptionFieldDef def = OptionEditor.Fields[index];

            CheckBox box = new CheckBox();
            box.Text = def.Label;
            box.AutoSize = true;
            box.Margin = new Padding(3, 3, 3, 3);
            table.Controls.Add(box, 0, row);
            toggles[index] = box;
            box.CheckedChanged += OptionEdited;

            Label hintLabel = new Label();
            hintLabel.Text = hint;
            hintLabel.AutoSize = true;
            hintLabel.Anchor = AnchorStyles.Left;
            hintLabel.ForeColor = Color.Gray;
            hintLabel.Margin = new Padding(6, 7, 3, 3);
            table.Controls.Add(hintLabel, 1, row);
            table.SetColumnSpan(hintLabel, 3);
        }

        private void AddSliderRow(TableLayoutPanel table, int row, string name,
            string labelOverride)
        {
            int index = OptionEditor.IndexOf(name);
            if (index < 0) return;
            EnsureRowCount(table, row);
            OptionFieldDef def = OptionEditor.Fields[index];

            Label label = new Label();
            label.Text = labelOverride ?? def.Label;
            label.AutoSize = true;
            label.Anchor = AnchorStyles.Left;
            label.Margin = new Padding(3, 6, 6, 3);
            table.Controls.Add(label, 0, row);
            AddSliderCell(table, row, index, 1, 2, 3, false, null);
        }

        private void AddPairRow(TableLayoutPanel table, int row, string toggleName,
            string sliderName, bool precise = false, string rangeText = null)
        {
            int toggleIndex = OptionEditor.IndexOf(toggleName);
            int sliderIndex = OptionEditor.IndexOf(sliderName);
            if (toggleIndex < 0 || sliderIndex < 0) return;
            EnsureRowCount(table, row);

            CheckBox box = new CheckBox();
            box.Text = OptionEditor.Fields[toggleIndex].Label;
            box.AutoSize = true;
            box.Margin = new Padding(3, 4, 6, 3);
            table.Controls.Add(box, 0, row);
            toggles[toggleIndex] = box;
            box.CheckedChanged += OptionEdited;

            AddSliderCell(table, row, sliderIndex, 1, 2, 3, precise, rangeText);

            List<Control> dependents = new List<Control>();
            TrackBar bar;
            if (sliders.TryGetValue(sliderIndex, out bar)) dependents.Add(bar);
            NumericUpDown number;
            if (preciseInputs.TryGetValue(sliderIndex, out number)) dependents.Add(number);
            Label value;
            if (sliderValues.TryGetValue(sliderIndex, out value)) dependents.Add(value);
            RegisterPair(toggleIndex, dependents.ToArray());
        }

        private void AddToggleChoiceRow(TableLayoutPanel table, int row,
            string toggleName, string choiceName)
        {
            int toggleIndex = OptionEditor.IndexOf(toggleName);
            int choiceIndex = OptionEditor.IndexOf(choiceName);
            if (toggleIndex < 0 || choiceIndex < 0) return;
            EnsureRowCount(table, row);

            CheckBox box = new CheckBox();
            box.Text = OptionEditor.Fields[toggleIndex].Label;
            box.AutoSize = true;
            box.Margin = new Padding(3, 4, 6, 3);
            table.Controls.Add(box, 0, row);
            toggles[toggleIndex] = box;
            box.CheckedChanged += OptionEdited;

            OptionFieldDef def = OptionEditor.Fields[choiceIndex];
            ComboBox combo = new ComboBox();
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.Dock = DockStyle.Fill;
            combo.Margin = new Padding(3, 4, 3, 4);
            if (def.Choices != null) combo.Items.AddRange(def.Choices);
            table.Controls.Add(combo, 1, row);
            table.SetColumnSpan(combo, 3);
            choices[choiceIndex] = combo;
            combo.SelectedIndexChanged += OptionEdited;
            RegisterPair(toggleIndex, combo);
        }

        private void RegisterPair(int toggleIndex, params Control[] controls)
        {
            if (toggleIndex < 0 || controls == null || controls.Length == 0) return;

            List<Control> dependents;
            if (!pairDependents.TryGetValue(toggleIndex, out dependents))
            {
                dependents = new List<Control>();
                pairDependents[toggleIndex] = dependents;
            }
            foreach (Control control in controls)
            {
                if (control != null && !dependents.Contains(control))
                    dependents.Add(control);
            }
        }

        private void UpdatePairStates(bool hasData)
        {
            foreach (KeyValuePair<int, List<Control>> pair in pairDependents)
            {
                CheckBox box;
                bool enabled = hasData;
                if (enabled && (!toggles.TryGetValue(pair.Key, out box) || !box.Checked))
                    enabled = false;
                foreach (Control control in pair.Value)
                    control.Enabled = enabled;
            }
        }

        private void AddSliderCell(TableLayoutPanel table, int row, int index,
            int barColumn, int valueColumn, int rangeColumn, bool precise,
            string rangeText)
        {
            OptionFieldDef def = OptionEditor.Fields[index];
            TrackBar bar = new TrackBar();
            bar.Minimum = def.Min;
            bar.Maximum = def.Max;
            bar.SmallChange = 1;
            bar.LargeChange = Math.Max(1, (def.Max - def.Min) / 10);
            bar.TickStyle = TickStyle.None;
            bar.AutoSize = false;
            bar.Height = 30;
            bar.Dock = DockStyle.Fill;
            bar.Margin = new Padding(3, 3, 3, 3);
            bar.Value = def.Min;
            table.Controls.Add(bar, barColumn, row);
            sliders[index] = bar;
            bar.ValueChanged += delegate
            {
                UpdateSliderLabel(index);
                if (!syncingPrecise) OptionEdited(bar, EventArgs.Empty);
            };

            if (precise)
            {
                NumericUpDown number = new NumericUpDown();
                number.Minimum = FromSlider(def, def.Min);
                number.Maximum = FromSlider(def, def.Max);
                number.DecimalPlaces = def.DecimalPlaces;
                number.Increment = def.DecimalPlaces > 0 ? 0.1m : 1m;
                number.TextAlign = HorizontalAlignment.Left;
                number.Anchor = AnchorStyles.Left | AnchorStyles.Right;
                number.Margin = new Padding(2, 5, 2, 5);
                number.Value = FromSlider(def, bar.Value);
                table.Controls.Add(number, valueColumn, row);
                preciseInputs[index] = number;
                number.ValueChanged += delegate
                {
                    if (suppress || syncingPrecise) return;
                    syncingPrecise = true;
                    try { bar.Value = ToSlider(def, number.Value); }
                    finally { syncingPrecise = false; }
                    OptionEdited(number, EventArgs.Empty);
                };

                if (!string.IsNullOrEmpty(rangeText))
                {
                    Label range = new Label();
                    range.Text = rangeText;
                    range.AutoSize = true;
                    range.Anchor = AnchorStyles.Left;
                    range.ForeColor = Color.Gray;
                    range.Margin = new Padding(6, 7, 3, 3);
                    table.Controls.Add(range, rangeColumn, row);
                }
                return;
            }

            Label value = new Label();
            value.AutoSize = false;
            value.Dock = DockStyle.Fill;
            value.TextAlign = ContentAlignment.MiddleRight;
            value.ForeColor = Color.DimGray;
            value.Margin = new Padding(2, 0, 0, 0);
            value.Text = FormatOptionValue(def, def.Min);
            table.Controls.Add(value, valueColumn, row);
            sliderValues[index] = value;
        }

        private void EnsureRowCount(TableLayoutPanel table, int lastRow)
        {
            while (table.RowCount <= lastRow)
            {
                table.RowCount++;
                table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            }
        }

        private void OptionEdited(object sender, EventArgs e)
        {
            if (suppress || applying) return;
            dirty = true;
            SetStatus("Unsaved changes.", false);
            UpdateGate();
        }

        private void UpdateSliderLabel(int index)
        {
            TrackBar bar;
            if (!sliders.TryGetValue(index, out bar)) return;

            OptionFieldDef def = OptionEditor.Fields[index];
            NumericUpDown number;
            if (preciseInputs.TryGetValue(index, out number))
            {
                decimal value = FromSlider(def, bar.Value);
                if (number.Value != value)
                {
                    syncingPrecise = true;
                    try { number.Value = value; }
                    finally { syncingPrecise = false; }
                }
                return;
            }

            Label label;
            if (!sliderValues.TryGetValue(index, out label)) return;
            label.Text = FormatOptionValue(def, bar.Value);
        }

        private static string FormatOptionValue(OptionFieldDef def, int value)
        {
            if (def != null && def.DecimalPlaces > 0)
                return value.ToString("F" + def.DecimalPlaces.ToString());
            return value.ToString();
        }

        private static decimal FromSlider(OptionFieldDef def, int value)
        {
            if (def != null && def.DecimalPlaces > 0)
                return value / 10m;
            return value;
        }

        private static int ToSlider(OptionFieldDef def, decimal value)
        {
            if (def != null && def.DecimalPlaces > 0)
                return (int)Math.Round(value * 10m);
            return (int)value;
        }

        public void SetTarget(GameMemory memory, GameContext context,
            bool writeAllowed)
        {
            mem = memory;
            ctx = context;
            gateAllowed = writeAllowed;

            if (mem == null || !mem.IsAttached || ctx == null || ctx.GlobalRoot == 0)
            {
                Clear();
                return;
            }

            OptionSnapshot snapshot;
            string error;
            if (!OptionEditor.TryRead(mem, ctx, out snapshot, out error))
            {
                if (!dirty) SetStatus(error, true);
                UpdateGate();
                return;
            }

            if (!dirty)
            {
                bool firstLoad = loaded == null;
                loaded = snapshot;
                SetValues(snapshot.Values);
                if (firstLoad)
                    SetStatus("Loaded from live memory.", false);
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
            loaded = null;
            dirty = false;
            applying = false;
            suppress = true;
            try
            {
                foreach (KeyValuePair<int, CheckBox> pair in toggles)
                    pair.Value.Checked = false;
                foreach (KeyValuePair<int, TrackBar> pair in sliders)
                {
                    pair.Value.Value = pair.Value.Minimum;
                    UpdateSliderLabel(pair.Key);
                }
                foreach (KeyValuePair<int, ComboBox> pair in choices)
                    pair.Value.SelectedIndex = -1;
            }
            finally
            {
                suppress = false;
            }

            SetStatus("Attach to the game and load a class to edit options.", false);
            UpdateGate();
        }

        private void SetValues(int[] values)
        {
            if (values == null || values.Length != OptionEditor.Fields.Count) return;

            suppress = true;
            try
            {
                for (int i = 0; i < values.Length; i++)
                {
                    CheckBox box;
                    if (toggles.TryGetValue(i, out box))
                        box.Checked = values[i] != 0;

                    TrackBar bar;
                    if (sliders.TryGetValue(i, out bar))
                    {
                        int value = values[i];
                        if (value < bar.Minimum) value = bar.Minimum;
                        if (value > bar.Maximum) value = bar.Maximum;
                        bar.Value = value;
                        UpdateSliderLabel(i);
                    }

                    ComboBox combo;
                    if (choices.TryGetValue(i, out combo))
                    {
                        if (values[i] >= 0 && values[i] < combo.Items.Count)
                            combo.SelectedIndex = values[i];
                        else
                            combo.SelectedIndex = -1;
                    }
                }
            }
            finally
            {
                suppress = false;
            }
        }

        private int[] ReadUi()
        {
            int[] values = new int[OptionEditor.Fields.Count];
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = loaded != null && loaded.Values != null
                    ? loaded.Values[i] : 0;

                CheckBox box;
                if (toggles.TryGetValue(i, out box))
                {
                    values[i] = box.Checked ? 1 : 0;
                    continue;
                }

                TrackBar bar;
                if (sliders.TryGetValue(i, out bar))
                {
                    values[i] = bar.Value;
                    continue;
                }

                ComboBox combo;
                if (choices.TryGetValue(i, out combo))
                    values[i] = combo.SelectedIndex < 0 ? 0 : combo.SelectedIndex;
            }
            return values;
        }

        private void ApplyChanges()
        {
            if (loaded == null || mem == null || !mem.IsAttached)
            {
                SetStatus("The game is not attached.", true);
                UpdateGate();
                return;
            }
            if (!dirty)
            {
                SetStatus("No changes to apply.", false);
                return;
            }
            if (ConfirmWriteAllowed != null && !ConfirmWriteAllowed())
            {
                SetStatus("Write gate is locked. Wait for a clean pointer check and try again.",
                    true);
                UpdateGate();
                return;
            }
            if (!mem.CanWrite)
            {
                SetStatus(string.IsNullOrEmpty(mem.WriteError)
                    ? "The game handle is read-only."
                    : mem.WriteError, true);
                UpdateGate();
                return;
            }

            int[] editedValues = ReadUi();
            OptionSnapshot edited = new OptionSnapshot();
            edited.Values = editedValues;

            string changed;
            string error;
            bool ok;
            applying = true;
            try
            {
                ok = OptionEditor.TryWrite(mem, ctx, loaded, edited,
                    out changed, out error);
            }
            finally
            {
                applying = false;
            }

            if (!ok)
            {
                SetStatus(error, true);
                UpdateGate();
                return;
            }

            dirty = false;
            OptionSnapshot check;
            string checkError;
            if (OptionEditor.TryRead(mem, ctx, out check, out checkError))
            {
                loaded = check;
                SetValues(check.Values);
            }
            else
            {
                loaded = edited;
                SetValues(edited.Values);
            }

            SetStatus(changed == "No changes." ? "No changes." : "Saved: " + changed + ".",
                false);
            UpdateGate();
        }

        private void RevertChanges()
        {
            if (loaded == null || mem == null || !mem.IsAttached)
            {
                SetStatus("The game is not attached.", true);
                UpdateGate();
                return;
            }

            OptionSnapshot snapshot;
            string error;
            if (!OptionEditor.TryRead(mem, ctx, out snapshot, out error))
            {
                SetStatus(error, true);
                return;
            }

            loaded = snapshot;
            dirty = false;
            SetValues(snapshot.Values);
            SetStatus("Reverted to the values currently in memory.", false);
            UpdateGate();
        }

        private void UpdateGate()
        {
            if (applyButton == null) return;

            bool hasData = loaded != null;
            bool canWrite = hasData && gateAllowed && mem != null &&
                mem.IsAttached && mem.CanWrite;

            foreach (CheckBox box in toggles.Values) box.Enabled = hasData;
            foreach (TrackBar bar in sliders.Values) bar.Enabled = hasData;
            foreach (NumericUpDown input in preciseInputs.Values) input.Enabled = hasData;
            foreach (ComboBox combo in choices.Values) combo.Enabled = hasData;
            UpdatePairStates(hasData);

            applyButton.Enabled = canWrite && dirty;
            revertButton.Enabled = hasData && dirty;

            if (!hasData)
            {
                gateLabel.Text = "Write gate: locked";
                gateLabel.ForeColor = Color.Firebrick;
            }
            else if (canWrite)
            {
                gateLabel.Text = "Write gate: open";
                gateLabel.ForeColor = Color.ForestGreen;
            }
            else
            {
                gateLabel.Text = "Write gate: locked";
                gateLabel.ForeColor = Color.Firebrick;
            }
        }

        private void SetStatus(string text, bool error)
        {
            if (statusLabel == null) return;
            statusLabel.Text = text ?? "";
            statusLabel.ForeColor = error ? Color.Firebrick : Color.DimGray;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && tooltip != null)
            {
                tooltip.Dispose();
                tooltip = null;
            }
            base.Dispose(disposing);
        }
    }
}
