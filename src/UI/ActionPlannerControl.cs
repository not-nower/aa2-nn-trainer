using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AA2Trainer
{
    /// <summary>
    /// Action planner tab. It owns the planner hook, runs a single action
    /// through the game's own planner, and reports live state and diagnostics.
    /// </summary>
    public sealed class ActionPlannerControl : UserControl
    {
        private GameMemory mem;
        private GameContext ctx;
        private CharacterInfo selected;
        private NameMap names;
        private bool writeAllowed;
        private ActionPlannerEngine engine;

        private bool running;
        private volatile bool cancelRequested;
        private bool suppressHook;
        private bool suppressSelection;
        private bool suppressPlanChecks;
        private int rosterVersion = -1;
        private NameMap choiceNames;
        private readonly List<CharacterInfo> rosterSnapshot =
            new List<CharacterInfo>();

        public Func<bool> ConfirmWriteAllowed { get; set; }
        public event Action<bool> PlannerActiveChanged;

        private CheckBox hookCheck;
        private Label hookStatusLabel;
        private ToolTip hookTooltip;
        private CheckBox observeCheck;
        private Label observationLabel;
        private uint lastObserveCounter;
        private bool haveObserved;
        private Label actorLabel;
        private ComboBox targetCombo;
        private ComboBox aboutCombo;
        private ComboBox actionCombo;
        private ComboBox roomCombo;
        private Label roomGuardLabel;
        private Button actionButton;
        private Button roomButton;
        private Button stopButton;
        private Button cancelButton;
        private CheckBox retryCheck;
        private CheckBox waitCheck;
        private CheckBox keepCheck;
        private Label actorStateLabel;
        private Label targetStateLabel;
        private RichTextBox logBox;
        private RichTextBox liveLogBox;
        private TabControl logTabs;
        private CheckBox liveCaptureCheck;
        private readonly Dictionary<int, string> liveSignatures =
            new Dictionary<int, string>();
        private Label lastResultLabel;
        private System.Windows.Forms.Timer stateTimer;

        private readonly Dictionary<int, PlanQueue> plans =
            new Dictionary<int, PlanQueue>();
        private PlanRunner planRunner;
        private PlanQueue displayedPlan;
        private string lastPlanListSignature;
        private string lastPlanStatusSignature;
        private ListView planList;
        private ComboBox planModeCombo;
        private ComboBox planTriggerCombo;
        private CheckBox planRepeatCheck;
        private CheckBox planShuffleCheck;
        private Button planAddButton;
        private Button planAddMoveButton;
        private Button planRemoveButton;
        private Button planUpButton;
        private Button planDownButton;
        private Button planClearButton;
        private Button planCopyButton;
        private Button planPasteButton;
        private Button planSaveButton;
        private Button planLoadButton;
        private Button planStartButton;
        private Button planStopButton;
        private Label planStatusLabel;
        private Label planCurrentLabel;

        private readonly List<PlanStep> clipSteps = new List<PlanStep>();
        private PlanMode clipMode = PlanMode.Normal;
        private PlanTrigger clipTrigger = PlanTrigger.Actor;
        private bool clipRepeat;
        private bool clipShuffle;
        private bool hasClipboard;

        public ActionPlannerControl()
        {
            DoubleBuffered = true;
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(248, 249, 251);
            Padding = new Padding(4, 2, 4, 4);

            stateTimer = new System.Windows.Forms.Timer();
            stateTimer.Interval = 250;
            stateTimer.Tick += delegate { UpdateStateNow(); };

            BuildUi();
            UpdateHookStatus();
            UpdateControls();
            UpdateStateNow();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                cancelRequested = true;
                if (planRunner != null)
                {
                    planRunner.Dispose();
                    planRunner = null;
                }
                if (stateTimer != null)
                {
                    stateTimer.Stop();
                    stateTimer.Dispose();
                    stateTimer = null;
                }
                if (engine != null)
                {
                    engine.Dispose();
                    engine = null;
                }
                if (hookTooltip != null)
                {
                    hookTooltip.Dispose();
                    hookTooltip = null;
                }
                if (logBox != null && logBox.Font != null)
                    logBox.Font.Dispose();
                if (liveLogBox != null && liveLogBox.Font != null)
                    liveLogBox.Font.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            UpdateStateTimer();
            if (Visible) UpdateStateNow();
        }

        private void BuildUi()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 8;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 256F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            root.Margin = new Padding(0);
            Controls.Add(root);

            root.Controls.Add(BuildHookRow(), 0, 0);
            root.Controls.Add(BuildSelectionRow(), 0, 1);
            root.Controls.Add(BuildActionRow(), 0, 2);
            root.Controls.Add(BuildButtonsRow(), 0, 3);
            root.Controls.Add(BuildPlanRow(), 0, 4);
            root.Controls.Add(BuildStatePanel(), 0, 5);
            root.Controls.Add(BuildLogBox(), 0, 6);
            root.Controls.Add(BuildLogToolbar(), 0, 7);
        }

        private Control BuildHookRow()
        {
            FlowLayoutPanel row = new FlowLayoutPanel();
            row.Dock = DockStyle.Fill;
            row.WrapContents = false;
            row.FlowDirection = FlowDirection.LeftToRight;
            row.Padding = new Padding(0, 4, 0, 0);
            row.Margin = new Padding(0);

            hookCheck = new CheckBox();
            hookCheck.Text = "Use planner hook";
            hookCheck.AutoSize = true;
            hookCheck.Margin = new Padding(0, 2, 8, 0);
            hookCheck.CheckedChanged += HookCheck_CheckedChanged;
            row.Controls.Add(hookCheck);

            hookStatusLabel = new Label();
            hookStatusLabel.AutoSize = true;
            hookStatusLabel.Margin = new Padding(0, 4, 0, 0);
            hookStatusLabel.ForeColor = Color.DimGray;
            hookStatusLabel.Text = "Hook: not installed";
            row.Controls.Add(hookStatusLabel);

            observeCheck = new CheckBox();
            observeCheck.Text = "Observe only";
            observeCheck.AutoSize = true;
            observeCheck.Margin = new Padding(16, 2, 4, 0);
            observeCheck.Enabled = false;
            observeCheck.CheckedChanged += ObserveCheck_CheckedChanged;
            row.Controls.Add(observeCheck);

            hookTooltip = new ToolTip();
            hookTooltip.AutoPopDelay = 15000;
            hookTooltip.InitialDelay = 300;
            hookTooltip.ReshowDelay = 100;
            hookTooltip.ToolTipTitle = "Planner hook";
            hookTooltip.ToolTipIcon = ToolTipIcon.Warning;
            string hookWarning =
                "Patches AA2Play.exe code in memory.\r\n\r\n" +
                "The original bytes are restored when you uncheck the hook " +
                "or close the trainer. If the trainer crashes, restart " +
                "AA2Play.exe to remove the patch.";
            hookTooltip.SetToolTip(hookCheck, hookWarning);
            hookTooltip.SetToolTip(hookStatusLabel, hookWarning);
            hookTooltip.SetToolTip(observeCheck,
                "Records what the game's own planner decides without replacing it.\r\n" +
                "Used to measure Normal-mode behaviour; queued actions will not fire " +
                "while this is on.");

            return row;
        }

        private Control BuildSelectionRow()
        {
            FlowLayoutPanel row = new FlowLayoutPanel();
            row.Dock = DockStyle.Fill;
            row.WrapContents = false;
            row.FlowDirection = FlowDirection.LeftToRight;
            row.Padding = new Padding(0, 4, 0, 0);
            row.Margin = new Padding(0);

            Label actorCaption = new Label();
            actorCaption.Text = "Actor:";
            actorCaption.AutoSize = true;
            actorCaption.Margin = new Padding(0, 5, 4, 0);
            row.Controls.Add(actorCaption);

            actorLabel = new Label();
            actorLabel.AutoSize = true;
            actorLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            actorLabel.ForeColor = Color.FromArgb(40, 44, 52);
            actorLabel.Margin = new Padding(0, 5, 16, 0);
            actorLabel.Text = "No character selected";
            row.Controls.Add(actorLabel);

            Label targetCaption = new Label();
            targetCaption.Text = "Target:";
            targetCaption.AutoSize = true;
            targetCaption.Margin = new Padding(0, 5, 4, 0);
            row.Controls.Add(targetCaption);

            targetCombo = new ComboBox();
            targetCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            targetCombo.Width = 240;
            targetCombo.Margin = new Padding(0, 2, 12, 0);
            targetCombo.SelectedIndexChanged += delegate
            {
                if (!suppressSelection) UpdateStateNow();
            };
            row.Controls.Add(targetCombo);

            Label aboutCaption = new Label();
            aboutCaption.Text = "About:";
            aboutCaption.AutoSize = true;
            aboutCaption.Margin = new Padding(0, 5, 4, 0);
            row.Controls.Add(aboutCaption);

            aboutCombo = new ComboBox();
            aboutCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            aboutCombo.Width = 220;
            aboutCombo.Margin = new Padding(0, 2, 0, 0);
            row.Controls.Add(aboutCombo);

            return row;
        }

        private Control BuildActionRow()
        {
            FlowLayoutPanel row = new FlowLayoutPanel();
            row.Dock = DockStyle.Fill;
            row.WrapContents = false;
            row.FlowDirection = FlowDirection.LeftToRight;
            row.Padding = new Padding(0, 4, 0, 0);
            row.Margin = new Padding(0);

            Label actionCaption = new Label();
            actionCaption.Text = "Action:";
            actionCaption.AutoSize = true;
            actionCaption.Margin = new Padding(0, 5, 4, 0);
            row.Controls.Add(actionCaption);

            actionCombo = new ComboBox();
            actionCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            actionCombo.Width = 300;
            actionCombo.Margin = new Padding(0, 2, 14, 0);
            actionCombo.SelectedIndexChanged += delegate
            {
                if (!suppressSelection)
                {
                    UpdateStateNow();
                    UpdateControls();
                }
            };
            row.Controls.Add(actionCombo);

            Label roomCaption = new Label();
            roomCaption.Text = "Room:";
            roomCaption.AutoSize = true;
            roomCaption.Margin = new Padding(0, 5, 4, 0);
            row.Controls.Add(roomCaption);

            roomCombo = new ComboBox();
            roomCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            roomCombo.Width = 220;
            roomCombo.Margin = new Padding(0, 2, 8, 0);
            roomCombo.SelectedIndexChanged += delegate
            {
                if (!suppressSelection)
                {
                    UpdateStateNow();
                    UpdateControls();
                }
            };
            row.Controls.Add(roomCombo);

            roomButton = new Button();
            roomButton.Text = "Move";
            roomButton.Width = 64;
            roomButton.Margin = new Padding(0, 1, 12, 0);
            roomButton.Click += RoomButton_Click;
            row.Controls.Add(roomButton);

            roomGuardLabel = new Label();
            roomGuardLabel.AutoSize = true;
            roomGuardLabel.Margin = new Padding(0, 5, 0, 0);
            roomGuardLabel.ForeColor = Color.DimGray;
            roomGuardLabel.Text = "Room: ?";
            row.Controls.Add(roomGuardLabel);

            return row;
        }

        private Control BuildButtonsRow()
        {
            FlowLayoutPanel row = new FlowLayoutPanel();
            row.Dock = DockStyle.Fill;
            row.WrapContents = false;
            row.FlowDirection = FlowDirection.LeftToRight;
            row.Padding = new Padding(0, 4, 0, 0);
            row.Margin = new Padding(0);

            actionButton = MakeButton("Action", 76, ActionButton_Click);
            stopButton = MakeButton("Stop action", 100, StopButton_Click);
            cancelButton = MakeButton("Cancel retry", 100, CancelButton_Click);
            row.Controls.Add(actionButton);
            row.Controls.Add(stopButton);
            row.Controls.Add(cancelButton);

            retryCheck = MakeCheck("Retry", true);
            waitCheck = MakeCheck("Wait until idle", true);
            keepCheck = MakeCheck("Keep repeating", false);
            row.Controls.Add(retryCheck);
            row.Controls.Add(waitCheck);
            row.Controls.Add(keepCheck);

            return row;
        }

        private static Button MakeButton(string text, int width, EventHandler handler)
        {
            Button b = new Button();
            b.Text = text;
            b.Width = width;
            b.Margin = new Padding(0, 1, 6, 0);
            b.Click += handler;
            return b;
        }

        private static CheckBox MakeCheck(string text, bool value)
        {
            CheckBox c = new CheckBox();
            c.Text = text;
            c.AutoSize = true;
            c.Checked = value;
            c.Margin = new Padding(10, 4, 0, 0);
            return c;
        }

        private Control BuildPlanRow()
        {
            TableLayoutPanel panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.ColumnCount = 2;
            panel.RowCount = 1;
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62F));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38F));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            panel.Margin = new Padding(0, 6, 0, 2);

            planList = new BufferedListView();
            planList.Dock = DockStyle.Fill;
            planList.View = View.Details;
            planList.FullRowSelect = true;
            planList.GridLines = true;
            planList.MultiSelect = false;
            planList.HideSelection = false;
            planList.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            planList.CheckBoxes = true;
            planList.ShowItemToolTips = true;
            planList.Columns.Add("On", 34, HorizontalAlignment.Center);
            planList.Columns.Add("#", 32, HorizontalAlignment.Right);
            planList.Columns.Add("Step", 230, HorizontalAlignment.Left);
            planList.Columns.Add("State", 110, HorizontalAlignment.Left);
            planList.SelectedIndexChanged += delegate { UpdatePlanButtons(); };
            planList.ItemCheck += PlanItemCheck;
            panel.Controls.Add(planList, 0, 0);

            FlowLayoutPanel side = new FlowLayoutPanel();
            side.Dock = DockStyle.Fill;
            side.FlowDirection = FlowDirection.TopDown;
            side.WrapContents = false;
            side.Padding = new Padding(8, 0, 0, 0);
            side.Margin = new Padding(0);

            FlowLayoutPanel modeRow = new FlowLayoutPanel();
            modeRow.AutoSize = true;
            modeRow.WrapContents = false;
            modeRow.Margin = new Padding(0);
            Label modeCaption = new Label();
            modeCaption.Text = "Plan mode:";
            modeCaption.AutoSize = true;
            modeCaption.Margin = new Padding(0, 5, 4, 0);
            modeRow.Controls.Add(modeCaption);
            planModeCombo = new ComboBox();
            planModeCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            planModeCombo.Width = 190;
            planModeCombo.Margin = new Padding(0, 2, 0, 0);
            planModeCombo.Items.Add("Normal (character chooses)");
            planModeCombo.Items.Add("Forced interaction");
            planModeCombo.Items.Add("Forced perform");
            planModeCombo.Items.Add("Ignore interaction");
            planModeCombo.SelectedIndex = 0;
            planModeCombo.SelectedIndexChanged += delegate
            {
                if (displayedPlan != null && !displayedPlan.Started)
                    displayedPlan.Mode = SelectedPlanMode();
            };
            modeRow.Controls.Add(planModeCombo);
            side.Controls.Add(modeRow);

            FlowLayoutPanel triggerRow = new FlowLayoutPanel();
            triggerRow.AutoSize = true;
            triggerRow.WrapContents = false;
            triggerRow.Margin = new Padding(0);
            Label triggerCaption = new Label();
            triggerCaption.Text = "Trigger:";
            triggerCaption.AutoSize = true;
            triggerCaption.Margin = new Padding(0, 5, 4, 0);
            triggerRow.Controls.Add(triggerCaption);
            planTriggerCombo = new ComboBox();
            planTriggerCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            planTriggerCombo.Width = 170;
            planTriggerCombo.Margin = new Padding(0, 2, 0, 0);
            planTriggerCombo.Items.Add("Actor initiates");
            planTriggerCombo.Items.Add("Target initiates");
            planTriggerCombo.Items.Add("Either initiates");
            planTriggerCombo.SelectedIndex = 0;
            planTriggerCombo.SelectedIndexChanged += delegate
            {
                if (displayedPlan != null && !displayedPlan.Started)
                    displayedPlan.Trigger = SelectedPlanTrigger();
            };
            triggerRow.Controls.Add(planTriggerCombo);
            side.Controls.Add(triggerRow);

            FlowLayoutPanel flagsRow = new FlowLayoutPanel();
            flagsRow.AutoSize = true;
            flagsRow.WrapContents = false;
            flagsRow.Margin = new Padding(0);
            planRepeatCheck = MakeCheck("Repeat", false);
            planRepeatCheck.Margin = new Padding(0, 2, 12, 0);
            planRepeatCheck.CheckedChanged += delegate
            {
                if (displayedPlan != null && !displayedPlan.Started)
                    displayedPlan.Repeat = planRepeatCheck.Checked;
            };
            planShuffleCheck = MakeCheck("Shuffle", false);
            planShuffleCheck.Margin = new Padding(0, 2, 0, 0);
            planShuffleCheck.CheckedChanged += delegate
            {
                if (displayedPlan != null && !displayedPlan.Started)
                    displayedPlan.Shuffle = planShuffleCheck.Checked;
            };
            flagsRow.Controls.Add(planRepeatCheck);
            flagsRow.Controls.Add(planShuffleCheck);
            side.Controls.Add(flagsRow);

            FlowLayoutPanel editRow = new FlowLayoutPanel();
            editRow.AutoSize = true;
            editRow.WrapContents = false;
            editRow.Margin = new Padding(0);
            planAddButton = MakeButton("Add action", 82, PlanAddAction_Click);
            planAddMoveButton = MakeButton("Add move", 76, PlanAddMove_Click);
            planRemoveButton = MakeButton("Remove", 70, PlanRemove_Click);
            editRow.Controls.Add(planAddButton);
            editRow.Controls.Add(planAddMoveButton);
            editRow.Controls.Add(planRemoveButton);
            side.Controls.Add(editRow);

            FlowLayoutPanel orderRow = new FlowLayoutPanel();
            orderRow.AutoSize = true;
            orderRow.WrapContents = false;
            orderRow.Margin = new Padding(0);
            planUpButton = MakeButton("Up", 46, PlanUp_Click);
            planDownButton = MakeButton("Down", 56, PlanDown_Click);
            planClearButton = MakeButton("Clear", 56, PlanClear_Click);
            orderRow.Controls.Add(planUpButton);
            orderRow.Controls.Add(planDownButton);
            orderRow.Controls.Add(planClearButton);
            side.Controls.Add(orderRow);

            FlowLayoutPanel clipboardRow = new FlowLayoutPanel();
            clipboardRow.AutoSize = true;
            clipboardRow.WrapContents = false;
            clipboardRow.Margin = new Padding(0);
            planCopyButton = MakeButton("Copy plan", 76, PlanCopy_Click);
            planPasteButton = MakeButton("Paste plan", 80, PlanPaste_Click);
            clipboardRow.Controls.Add(planCopyButton);
            clipboardRow.Controls.Add(planPasteButton);
            side.Controls.Add(clipboardRow);

            FlowLayoutPanel storeRow = new FlowLayoutPanel();
            storeRow.AutoSize = true;
            storeRow.WrapContents = false;
            storeRow.Margin = new Padding(0);
            planSaveButton = MakeButton("Save plans", 82, PlanSave_Click);
            planLoadButton = MakeButton("Load plans", 82, PlanLoad_Click);
            storeRow.Controls.Add(planSaveButton);
            storeRow.Controls.Add(planLoadButton);
            side.Controls.Add(storeRow);

            FlowLayoutPanel runRow = new FlowLayoutPanel();
            runRow.AutoSize = true;
            runRow.WrapContents = false;
            runRow.Margin = new Padding(0);
            planStartButton = MakeButton("Start plan", 84, PlanStart_Click);
            planStopButton = MakeButton("Stop plan", 84, PlanStop_Click);
            runRow.Controls.Add(planStartButton);
            runRow.Controls.Add(planStopButton);
            side.Controls.Add(runRow);

            planStatusLabel = new Label();
            planStatusLabel.AutoSize = false;
            planStatusLabel.Width = 300;
            planStatusLabel.Height = 18;
            planStatusLabel.AutoEllipsis = true;
            planStatusLabel.ForeColor = Color.DimGray;
            planStatusLabel.Text = "Plan: select a character";
            side.Controls.Add(planStatusLabel);

            planCurrentLabel = new Label();
            planCurrentLabel.AutoSize = false;
            planCurrentLabel.Width = 300;
            planCurrentLabel.Height = 18;
            planCurrentLabel.AutoEllipsis = true;
            planCurrentLabel.ForeColor = Color.FromArgb(70, 76, 86);
            planCurrentLabel.Text = "";
            side.Controls.Add(planCurrentLabel);

            panel.Controls.Add(side, 1, 0);
            return panel;
        }

        private Control BuildStatePanel()
        {
            TableLayoutPanel panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.ColumnCount = 1;
            panel.RowCount = 3;
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 33F));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 33F));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 34F));
            panel.Margin = new Padding(0, 4, 0, 2);
            panel.BackColor = Color.FromArgb(243, 245, 248);
            panel.Padding = new Padding(6, 2, 6, 2);

            actorStateLabel = MakeStateLabel("Actor: ?");
            targetStateLabel = MakeStateLabel("Target: ?");
            observationLabel = MakeStateLabel("Obs: -");
            panel.Controls.Add(actorStateLabel, 0, 0);
            panel.Controls.Add(targetStateLabel, 0, 1);
            panel.Controls.Add(observationLabel, 0, 2);
            return panel;
        }

        private static Label MakeStateLabel(string text)
        {
            Label l = new Label();
            l.Dock = DockStyle.Fill;
            l.AutoEllipsis = true;
            l.TextAlign = ContentAlignment.MiddleLeft;
            l.Font = new Font("Segoe UI", 8F);
            l.ForeColor = Color.FromArgb(70, 76, 86);
            l.Text = text;
            return l;
        }

        private Control BuildLogBox()
        {
            logTabs = new TabControl();
            logTabs.Dock = DockStyle.Fill;
            logTabs.Padding = new Point(12, 4);

            TabPage livePage = new TabPage("Live events");
            livePage.BackColor = Color.White;
            TableLayoutPanel liveLayout = new TableLayoutPanel();
            liveLayout.Dock = DockStyle.Fill;
            liveLayout.ColumnCount = 1;
            liveLayout.RowCount = 2;
            liveLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            liveLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            liveLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            liveLayout.Margin = new Padding(0);

            liveCaptureCheck = new CheckBox();
            liveCaptureCheck.Text = "Capture live events";
            liveCaptureCheck.AutoSize = true;
            liveCaptureCheck.Margin = new Padding(4, 3, 0, 0);
            liveCaptureCheck.CheckedChanged += delegate
            {
                liveSignatures.Clear();
                if (liveCaptureCheck.Checked) UpdateLiveEvents();
            };
            liveLayout.Controls.Add(liveCaptureCheck, 0, 0);

            liveLogBox = MakeLogBox();
            liveLayout.Controls.Add(liveLogBox, 0, 1);
            livePage.Controls.Add(liveLayout);
            logTabs.TabPages.Add(livePage);

            TabPage plannerPage = new TabPage("Planner log");
            plannerPage.BackColor = Color.White;
            logBox = MakeLogBox();
            plannerPage.Controls.Add(logBox);
            logTabs.TabPages.Add(plannerPage);

            return logTabs;
        }

        private static RichTextBox MakeLogBox()
        {
            RichTextBox box = new RichTextBox();
            box.Dock = DockStyle.Fill;
            box.ReadOnly = true;
            box.BackColor = Color.White;
            box.BorderStyle = BorderStyle.FixedSingle;
            box.Font = new Font("Consolas", 8.5F);
            box.WordWrap = false;
            box.DetectUrls = false;
            box.HideSelection = false;
            box.Margin = new Padding(0);
            return box;
        }

        private Control BuildLogToolbar()
        {
            FlowLayoutPanel row = new FlowLayoutPanel();
            row.Dock = DockStyle.Fill;
            row.WrapContents = false;
            row.FlowDirection = FlowDirection.LeftToRight;
            row.Padding = new Padding(0, 2, 0, 0);
            row.Margin = new Padding(0);

            Button clear = MakeButton("Clear", 70, delegate
            {
                RichTextBox active = ActiveLogBox();
                if (active != null) active.Clear();
                if (active == liveLogBox) liveSignatures.Clear();
            });
            Button save = MakeButton("Save Log", 80, SaveLog_Click);
            Button dump = MakeButton("Dump states", 90, DumpStates_Click);
            row.Controls.Add(clear);
            row.Controls.Add(save);
            row.Controls.Add(dump);

            lastResultLabel = new Label();
            lastResultLabel.AutoSize = true;
            lastResultLabel.Margin = new Padding(10, 5, 0, 0);
            lastResultLabel.ForeColor = Color.DimGray;
            lastResultLabel.Text = "Last result: -";
            row.Controls.Add(lastResultLabel);

            return row;
        }

        public void SetTarget(GameMemory memory, GameContext context,
            CharacterInfo character, NameMap nameMap, bool canWrite)
        {
            if (memory == null || context == null || character == null ||
                !memory.IsAttached)
            {
                Clear();
                return;
            }

            bool pidChanged = engine != null && engine.Pid != memory.Pid;
            if (pidChanged)
            {
                // The old process is gone; its hook page went with it. Do not
                // restore bytes through the new process. Stop the scheduler
                // thread without touching the dead process.
                if (planRunner != null)
                {
                    planRunner.Stop();
                    planRunner = null;
                }
                plans.Clear();
                displayedPlan = null;
                if (planList != null) planList.Items.Clear();
                engine = null;
                running = false;
            }

            bool actorChanged = selected == null ||
                selected.Pointer != character.Pointer;
            mem = memory;
            ctx = context;
            selected = character;
            names = nameMap;

            if (engine == null)
            {
                engine = new ActionPlannerEngine(memory);
            }
            else if (engine.Pid != memory.Pid)
            {
                engine = new ActionPlannerEngine(memory);
            }

            if (actorChanged || context.RosterVersion != rosterVersion)
            {
                rosterVersion = context.RosterVersion;
                PopulateChoices();
            }

            UpdateActorLabel();
            SetWriteAllowed(canWrite);
            EnsureChoiceLists();
            UpdateControls();
            UpdateHookStatus();
            UpdateStateTimer();
            UpdateStateNow();
        }

        public void SetWriteAllowed(bool allowed)
        {
            if (writeAllowed == allowed)
            {
                UpdateControls();
                return;
            }

            writeAllowed = allowed;
            if (!allowed)
            {
                cancelRequested = true;
                if (planRunner != null) planRunner.StopAll();
                if (engine != null) engine.ClearQueue();
                if (engine != null && engine.HookInstalled)
                    UninstallHook(false);
            }
            UpdateControls();
        }

        public void Clear()
        {
            cancelRequested = true;
            running = false;
            if (stateTimer != null) stateTimer.Stop();
            if (planRunner != null)
            {
                planRunner.Dispose();
                planRunner = null;
            }
            plans.Clear();
            displayedPlan = null;
            if (planList != null) planList.Items.Clear();
            if (engine != null)
            {
                engine.ClearQueue();
                if (engine.HookInstalled)
                {
                    string message;
                    engine.UninstallHook(out message);
                    if (!string.IsNullOrEmpty(message))
                        AppendLog(message, PlannerLogLevel.Warning);
                }
                engine.Dispose();
                engine = null;
            }
            mem = null;
            ctx = null;
            selected = null;
            names = null;
            rosterSnapshot.Clear();
            rosterVersion = -1;
            suppressSelection = true;
            if (targetCombo != null) targetCombo.Items.Clear();
            if (aboutCombo != null) aboutCombo.Items.Clear();
            suppressSelection = false;
            if (actorLabel != null) actorLabel.Text = "No character selected";
            if (actorStateLabel != null) actorStateLabel.Text = "Actor: ?";
            if (targetStateLabel != null) targetStateLabel.Text = "Target: ?";
            if (observationLabel != null) observationLabel.Text = "Obs: -";
            lastObserveCounter = 0;
            haveObserved = false;
            liveSignatures.Clear();
            if (liveCaptureCheck != null) liveCaptureCheck.Checked = false;
            if (liveLogBox != null) liveLogBox.Clear();
            if (roomGuardLabel != null) roomGuardLabel.Text = "Room: ?";
            UpdateHookStatus();
            UpdateControls();
            PlannerActiveChanged?.Invoke(false);
        }

        private void PopulateChoices()
        {
            if (ctx == null || targetCombo == null || aboutCombo == null) return;

            uint targetPointer = SelectedTarget() == null
                ? 0 : SelectedTarget().Pointer;
            uint aboutPointer = SelectedAbout() == null
                ? 0 : SelectedAbout().Pointer;

            rosterSnapshot.Clear();
            if (ctx.Characters != null)
                rosterSnapshot.AddRange(ctx.Characters);

            suppressSelection = true;
            targetCombo.BeginUpdate();
            aboutCombo.BeginUpdate();
            try
            {
                targetCombo.Items.Clear();
                targetCombo.Items.Add("None");
                aboutCombo.Items.Clear();
                aboutCombo.Items.Add("None");

                foreach (CharacterInfo c in rosterSnapshot)
                {
                    if (c == null) continue;
                    if (selected != null && c.Pointer == selected.Pointer)
                        continue;
                    targetCombo.Items.Add(c);
                    aboutCombo.Items.Add(c);
                }

                targetCombo.SelectedIndex = 0;
                aboutCombo.SelectedIndex = 0;

                for (int i = 1; i < targetCombo.Items.Count; i++)
                {
                    CharacterInfo c = targetCombo.Items[i] as CharacterInfo;
                    if (c != null && c.Pointer == targetPointer)
                    {
                        targetCombo.SelectedIndex = i;
                        break;
                    }
                }
                for (int i = 1; i < aboutCombo.Items.Count; i++)
                {
                    CharacterInfo c = aboutCombo.Items[i] as CharacterInfo;
                    if (c != null && c.Pointer == aboutPointer)
                    {
                        aboutCombo.SelectedIndex = i;
                        break;
                    }
                }
            }
            finally
            {
                targetCombo.EndUpdate();
                aboutCombo.EndUpdate();
                suppressSelection = false;
            }
        }

        private CharacterInfo SelectedTarget()
        {
            if (targetCombo == null || targetCombo.SelectedIndex <= 0)
                return null;
            return targetCombo.SelectedItem as CharacterInfo;
        }

        private CharacterInfo SelectedAbout()
        {
            if (aboutCombo == null || aboutCombo.SelectedIndex <= 0)
                return null;
            return aboutCombo.SelectedItem as CharacterInfo;
        }

        private void UpdateActorLabel()
        {
            if (actorLabel == null) return;
            if (selected == null)
            {
                actorLabel.Text = "No character selected";
                return;
            }
            string seat = selected.Seat >= 0
                ? selected.Seat.ToString("D2") + " " : "";
            actorLabel.Text = seat + (string.IsNullOrEmpty(selected.Name)
                ? "(unnamed)" : selected.Name) +
                (selected.IsPlayer ? "  [PC]" : "");
        }

        private void EnsureChoiceLists()
        {
            if (names == null || actionCombo == null || roomCombo == null)
                return;
            if (choiceNames == names && actionCombo.Items.Count > 0 &&
                roomCombo.Items.Count > 0)
                return;

            int keepAction = SelectedActionId();
            if (keepAction < 0) keepAction = 2;
            int keepRoom = SelectedRoomId();

            suppressSelection = true;
            actionCombo.BeginUpdate();
            roomCombo.BeginUpdate();
            try
            {
                actionCombo.Items.Clear();
                for (int id = 0; id < 1000; id++)
                {
                    string name = names.Get("ACTION_KEY", id);
                    if (string.IsNullOrEmpty(name)) continue;
                    actionCombo.Items.Add(new IdNameChoice(id,
                        id + " - " + name));
                }

                roomCombo.Items.Clear();
                roomCombo.Items.Add("None");
                for (int id = 0; id < 1000; id++)
                {
                    string name = names.Get("LOCATION", id);
                    if (string.IsNullOrEmpty(name)) continue;
                    roomCombo.Items.Add(new IdNameChoice(id,
                        id + " - " + name));
                }

                choiceNames = names;
                SelectActionId(keepAction);
                if (actionCombo.SelectedIndex < 0 && actionCombo.Items.Count > 0)
                    actionCombo.SelectedIndex = 0;
                SelectRoomId(keepRoom);
                if (roomCombo.SelectedIndex < 0 && roomCombo.Items.Count > 0)
                    roomCombo.SelectedIndex = 0;
            }
            finally
            {
                actionCombo.EndUpdate();
                roomCombo.EndUpdate();
                suppressSelection = false;
            }
        }

        private void SelectActionId(int id)
        {
            for (int i = 0; i < actionCombo.Items.Count; i++)
            {
                IdNameChoice choice = actionCombo.Items[i] as IdNameChoice;
                if (choice != null && choice.Id == id)
                {
                    actionCombo.SelectedIndex = i;
                    return;
                }
            }
        }

        private void SelectRoomId(int id)
        {
            if (id < 0)
            {
                roomCombo.SelectedIndex = 0;
                return;
            }
            for (int i = 1; i < roomCombo.Items.Count; i++)
            {
                IdNameChoice choice = roomCombo.Items[i] as IdNameChoice;
                if (choice != null && choice.Id == id)
                {
                    roomCombo.SelectedIndex = i;
                    return;
                }
            }
        }

        private int SelectedActionId()
        {
            if (actionCombo == null) return -1;
            IdNameChoice choice = actionCombo.SelectedItem as IdNameChoice;
            return choice == null ? -1 : choice.Id;
        }

        private int SelectedRoomId()
        {
            if (roomCombo == null || roomCombo.SelectedIndex <= 0) return -1;
            IdNameChoice choice = roomCombo.SelectedItem as IdNameChoice;
            return choice == null ? -1 : choice.Id;
        }

        /// <summary>
        /// ListView with double buffering. The plan list is refreshed from a
        /// 250 ms timer; without this the cell text flickers on every refresh.
        /// </summary>
        private sealed class BufferedListView : ListView
        {
            public BufferedListView()
            {
                DoubleBuffered = true;
                SetStyle(ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.AllPaintingInWmPaint, true);
                UpdateStyles();
            }
        }

        private sealed class IdNameChoice
        {
            public int Id;
            private string text;

            public IdNameChoice(int id, string text)
            {
                Id = id;
                this.text = text;
            }

            public override string ToString()
            {
                return text;
            }
        }

        private string ShortActionName(int id)
        {
            if (id < 0) return "None";
            string name = names == null ? null : names.Get("ACTION_KEY", id);
            return string.IsNullOrEmpty(name) ? "#" + id : name;
        }

        private string LocationName(int id)
        {
            if (id < 0) return "None";
            string name = names == null ? null : names.Get("LOCATION", id);
            return string.IsNullOrEmpty(name) ? "Location " + id : name;
        }

        private void UpdateHookStatus()
        {
            if (hookStatusLabel == null) return;
            bool installed = engine != null && engine.HookInstalled;
            hookStatusLabel.Text = installed
                ? "Hook: installed (" + engine.HookModeName + ")"
                : "Hook: not installed";
            hookStatusLabel.ForeColor = installed ? Color.SeaGreen : Color.DimGray;
            if (hookCheck != null)
            {
                suppressHook = true;
                hookCheck.Checked = installed;
                suppressHook = false;
            }
            if (observeCheck != null)
            {
                suppressHook = true;
                observeCheck.Enabled = installed;
                observeCheck.Checked = installed && engine != null && engine.ObserveOnly;
                suppressHook = false;
            }
        }

        private void UpdateControls()
        {
            if (hookCheck == null) return;
            bool attached = mem != null && mem.IsAttached && selected != null;
            bool installed = engine != null && engine.HookInstalled;
            bool editable = attached && writeAllowed && !running;

            hookCheck.Enabled = attached &&
                (installed || (writeAllowed && !running));
            if (observeCheck != null)
                observeCheck.Enabled = installed && !running;
            targetCombo.Enabled = editable;
            aboutCombo.Enabled = editable;
            actionCombo.Enabled = editable;
            roomCombo.Enabled = editable;
            bool hasAction = SelectedActionId() >= 0;
            roomButton.Enabled = editable && SelectedRoomId() >= 0;
            actionButton.Enabled = editable && hasAction;
            retryCheck.Enabled = editable;
            waitCheck.Enabled = editable;
            keepCheck.Enabled = editable;
            stopButton.Enabled = attached;
            cancelButton.Enabled = running;
            UpdatePlanButtons();
        }

        private void UpdateStateTimer()
        {
            if (stateTimer == null) return;
            bool wanted = Visible && mem != null && mem.IsAttached &&
                selected != null;
            if (wanted && !stateTimer.Enabled) stateTimer.Start();
            else if (!wanted && stateTimer.Enabled) stateTimer.Stop();
        }

        private void UpdateStateNow()
        {
            if (actorStateLabel == null) return;
            if (mem == null || !mem.IsAttached || ctx == null ||
                selected == null || engine == null)
            {
                actorStateLabel.Text = "Actor: ?";
                targetStateLabel.Text = "Target: ?";
                if (observationLabel != null) observationLabel.Text = "Obs: -";
                haveObserved = false;
                roomGuardLabel.Text = "Room: ?";
                UpdatePlanUi();
                return;
            }

            CharacterSummary actorState = engine.ReadSummary(selected);
            actorStateLabel.Text = "Actor: " + CharacterLabel(selected) +
                "  |  " + StateText(actorState);

            CharacterInfo target = SelectedTarget();
            CharacterSummary targetState = target == null
                ? null : engine.ReadSummary(target);
            targetStateLabel.Text = target == null
                ? "Target: none"
                : "Target: " + CharacterLabel(target) + "  |  " +
                  StateText(targetState);

            UpdateRoomGuard(actorState, targetState);
            UpdateObservation();
            UpdatePlanUi();
            UpdateLiveEvents();
        }

        /// <summary>
        /// Appends only state changes for every roster character to the Live
        /// events tab. Runs on the UI thread from the 250 ms state timer.
        /// </summary>
        private void UpdateLiveEvents()
        {
            if (liveCaptureCheck == null || !liveCaptureCheck.Checked) return;
            if (liveLogBox == null || mem == null || !mem.IsAttached ||
                ctx == null || engine == null)
                return;

            IList<CharacterInfo> roster = ctx.Characters;
            if (roster == null || roster.Count == 0) return;

            Dictionary<uint, string> partners = new Dictionary<uint, string>();
            List<CharacterInfo> infos = new List<CharacterInfo>();
            List<CharacterSummary> summaries = new List<CharacterSummary>();
            foreach (CharacterInfo info in roster)
            {
                CharacterSummary s = engine.ReadSummary(info);
                if (s == null) continue;
                uint iface = mem.ReadU32(s.Activity + 0xE4) ?? 0;
                if (iface != 0)
                {
                    partners[iface] = info.Seat.ToString("D2") + " " +
                        (info.Name ?? "");
                }
                infos.Add(info);
                summaries.Add(s);
            }

            for (int i = 0; i < infos.Count; i++)
            {
                CharacterInfo info = infos[i];
                CharacterSummary s = summaries[i];
                string signature = s.MovementType + "|" + s.ActionId + "|" +
                    s.Location + "|" + s.InConversation + "|" + s.Talking +
                    "|" + s.WalkTargetInterface;
                string previous;
                if (liveSignatures.TryGetValue(info.Seat, out previous) &&
                    previous == signature)
                    continue;
                liveSignatures[info.Seat] = signature;

                string partner = "-";
                if (s.WalkTargetInterface != 0)
                {
                    string name;
                    partner = partners.TryGetValue(s.WalkTargetInterface,
                        out name) ? name : "0x" +
                        s.WalkTargetInterface.ToString("X8");
                }

                AppendLiveLog(DateTime.Now.ToString("HH:mm:ss.fff") +
                    "  seat " + info.Seat.ToString("D2") + " " +
                    (info.Name ?? "") + "  move=" + s.MovementType +
                    " action=" + s.ActionId + " partner=" + partner +
                    " conv=" + s.InConversation + " talk=" + s.Talking +
                    " loc=" + s.Location);
            }
        }

        private void AppendLiveLog(string text)
        {
            if (liveLogBox == null) return;
            if (liveLogBox.TextLength > 200000) liveLogBox.Clear();
            liveLogBox.AppendText(text + Environment.NewLine);
            if (logTabs != null && logTabs.SelectedIndex == 0)
            {
                liveLogBox.SelectionStart = liveLogBox.TextLength;
                liveLogBox.ScrollToCaret();
            }
        }

        /// <summary>
        /// Phase 0: shows what the game's own planner decided while observe-only
        /// is on. The handler only advances its counter in that mode.
        /// </summary>
        private void UpdateObservation()
        {
            if (observationLabel == null) return;
            if (engine == null || !engine.HookInstalled)
            {
                observationLabel.Text = "Obs: hook not installed";
                haveObserved = false;
                return;
            }

            uint key;
            byte[] parameters;
            uint counter;
            if (!engine.ReadObservation(out key, out parameters, out counter))
            {
                observationLabel.Text = "Obs: unreadable";
                return;
            }

            if (!engine.ObserveOnly)
            {
                observationLabel.Text = "Obs: off (tick " + counter + ")";
                haveObserved = false;
                return;
            }

            uint delta = haveObserved && counter > lastObserveCounter
                ? counter - lastObserveCounter : 0;
            observationLabel.Text = "Obs: " + (haveObserved ? "+" + delta : "on") +
                "/tick  key=0x" + key.ToString("X8") + "  " +
                DescribeObservedParams(parameters);

            if (haveObserved && delta > 0)
            {
                AppendLog("obs +" + delta + "  key=0x" + key.ToString("X8") +
                    "  " + DescribeObservedParams(parameters), PlannerLogLevel.Detail);
            }

            lastObserveCounter = counter;
            haveObserved = true;
        }

        private void ObserveCheck_CheckedChanged(object sender, EventArgs e)
        {
            if (suppressHook) return;
            if (engine == null || !engine.HookInstalled)
            {
                suppressHook = true;
                observeCheck.Checked = false;
                suppressHook = false;
                return;
            }

            bool on = observeCheck.Checked;
            if (on)
            {
                if (planRunner != null) planRunner.StopAll();
                engine.ClearQueue();
            }

            if (!engine.SetObserveOnly(on))
            {
                AppendLog("Could not change observe-only mode.",
                    PlannerLogLevel.Error);
                return;
            }

            AppendLog(on
                ? "Observe-only ON: the game plans normally. Queued actions will not fire."
                : "Observe-only OFF: queued actions are injected again.",
                on ? PlannerLogLevel.Warning : PlannerLogLevel.Success);

            lastObserveCounter = 0;
            haveObserved = false;
            UpdatePlannerActive();
            UpdateStateNow();
        }

        private static string DescribeObservedParams(byte[] parameters)
        {
            if (parameters == null || parameters.Length < 28) return "(no data)";
            int action = BitConverter.ToInt32(parameters, 0x00);
            int movement = BitConverter.ToInt32(parameters, 0x04);
            int room = BitConverter.ToInt32(parameters, 0x08);
            int about = BitConverter.ToInt32(parameters, 0x0C);
            uint target1 = BitConverter.ToUInt32(parameters, 0x10);
            uint target2 = BitConverter.ToUInt32(parameters, 0x14);
            int unknown2 = BitConverter.ToInt32(parameters, 0x18);
            return "action=" + action +
                " move=" + movement +
                " room=" + room +
                " about=" + about +
                " t1=0x" + target1.ToString("X8") +
                " t2=0x" + target2.ToString("X8") +
                " u2=" + unknown2;
        }

        private static string CharacterLabel(CharacterInfo info)
        {
            if (info == null) return "(none)";
            string seat = info.Seat >= 0 ? info.Seat.ToString("D2") + " " : "";
            return seat + (string.IsNullOrEmpty(info.Name)
                ? "(unnamed)" : info.Name) +
                (info.IsPlayer ? " [PC]" : "");
        }

        private string StateText(CharacterSummary s)
        {
            if (s == null) return "unreadable";
            string room = s.Room < 0
                ? "?"
                : s.Room + " (" + LocationName(s.Room) + ")";
            return "room " + room +
                "  |  " + ActionPlannerEngine.MovementName(s.MovementType) +
                " (" + s.MovementType + ")" +
                "  |  action " + ShortActionName(s.ActionId) +
                "  |  busy " + s.AnimLocked + "/" + s.Loading + "/" +
                s.InConversation +
                (s.Talking != 0 ? "  |  talking" : "");
        }

        private void UpdateRoomGuard(CharacterSummary actorState,
            CharacterSummary targetState)
        {
            if (roomGuardLabel == null) return;
            if (selected == null)
            {
                roomGuardLabel.Text = "Room: ?";
                roomGuardLabel.ForeColor = Color.DimGray;
                return;
            }
            if (SelectedTarget() == null)
            {
                roomGuardLabel.Text = "Room: no target";
                roomGuardLabel.ForeColor = Color.DimGray;
                return;
            }
            if (actorState == null || targetState == null ||
                actorState.Room < 0 || targetState.Room < 0)
            {
                roomGuardLabel.Text = "Room: unknown";
                roomGuardLabel.ForeColor = Color.DimGray;
                return;
            }

            if (actorState.Room == targetState.Room)
            {
                roomGuardLabel.Text = "Room: same (" + actorState.Room + ")";
                roomGuardLabel.ForeColor = Color.SeaGreen;
            }
            else
            {
                roomGuardLabel.Text = "Room: actor " + actorState.Room +
                    " -> target " + targetState.Room + " (planner will route)";
                roomGuardLabel.ForeColor = Color.SteelBlue;
            }

            int restriction = RoomGenderRestriction(targetState.Room);
            if (restriction != 0)
            {
                bool mismatch = (restriction == 1 && actorState.Gender != 0) ||
                    (restriction == 2 && actorState.Gender != 1);
                if (mismatch)
                {
                    roomGuardLabel.Text += "  |  private/gender-locked";
                    roomGuardLabel.ForeColor = Color.Firebrick;
                }
            }
        }

        private int RoomGenderRestriction(int roomId)
        {
            string name = names == null ? null : names.Get("LOCATION", roomId);
            if (string.IsNullOrEmpty(name)) return 0;
            string n = name.ToLowerInvariant();
            if (n.Contains("boys'") || n.Contains("boys ") ||
                n.Contains("men's") || n.Contains("mens "))
                return 1;
            if (n.Contains("girls'") || n.Contains("girls ") ||
                n.Contains("women's") || n.Contains("womens ") ||
                n.Contains("ladies"))
                return 2;
            return 0;
        }

        private bool GateForWrite()
        {
            if (!writeAllowed)
            {
                AppendLog("The write gate is locked.", PlannerLogLevel.Error);
                return false;
            }
            if (ConfirmWriteAllowed != null && !ConfirmWriteAllowed())
            {
                AppendLog("Write gate is locked. Wait for a clean pointer check.",
                    PlannerLogLevel.Error);
                return false;
            }
            return true;
        }

        private void HookCheck_CheckedChanged(object sender, EventArgs e)
        {
            if (suppressHook) return;
            if (hookCheck.Checked)
            {
                if (!InstallHook())
                {
                    suppressHook = true;
                    hookCheck.Checked = false;
                    suppressHook = false;
                }
            }
            else
            {
                UninstallHook(true);
            }
        }

        private bool InstallHook()
        {
            if (engine == null)
            {
                AppendLog("Planner engine is unavailable.", PlannerLogLevel.Error);
                return false;
            }
            if (engine.HookInstalled) return true;

            if (!GateForWrite()) return false;

            string message;
            if (!engine.InstallHook(out message))
            {
                AppendLog(message, PlannerLogLevel.Error);
                UpdateHookStatus();
                return false;
            }

            AppendLog(message, PlannerLogLevel.Success);
            UpdateHookStatus();
            UpdateControls();
            return true;
        }

        private void UninstallHook(bool log)
        {
            cancelRequested = true;
            if (engine != null) engine.ClearQueue();
            if (engine != null && engine.HookInstalled)
            {
                string message;
                bool ok = engine.UninstallHook(out message);
                if (log) AppendLog(message, ok
                    ? PlannerLogLevel.Success : PlannerLogLevel.Error);
            }
            UpdateHookStatus();
            UpdateControls();
        }

        private bool EnsureHook()
        {
            if (engine != null && engine.HookInstalled) return true;
            if (!InstallHook()) return false;
            return engine != null && engine.HookInstalled;
        }

        private ActionParams BuildTargetParams(out string error)
        {
            error = null;
            int action = SelectedActionId();
            if (action < 0)
            {
                error = "Choose an action.";
                return null;
            }

            CharacterInfo target = SelectedTarget();
            if (target == null)
            {
                error = "Choose a target for this action.";
                return null;
            }

            CharacterSummary targetState = engine.ReadSummary(target);
            if (targetState == null || targetState.Activity == 0)
            {
                error = "Target activity is unreadable.";
                return null;
            }

            ActionParams p = ActionParams.TargetAction(action,
                targetState.Activity);
            CharacterInfo about = SelectedAbout();
            if (about != null)
            {
                CharacterSummary aboutState = engine.ReadSummary(about);
                if (aboutState == null || aboutState.Activity == 0)
                {
                    error = "About-target activity is unreadable.";
                    return null;
                }
                p.Target2 = aboutState.Activity;
            }
            return p;
        }

        private void ActionButton_Click(object sender, EventArgs e)
        {
            if (running || selected == null) return;
            if (!GateForWrite()) return;
            if (!EnsureHook()) return;

            string error;
            ActionParams p = BuildTargetParams(out error);
            if (p == null)
            {
                AppendLog(error, PlannerLogLevel.Error);
                return;
            }

            CharacterInfo target = SelectedTarget();
            CharacterInfo about = SelectedAbout();
            string label = "Action " + ShortActionName(p.ActionId) +
                " -> " + (target == null ? "?" : target.Name);
            if (about != null) label += " about " + about.Name;
            StartRun(p, label);
        }

        private void RoomButton_Click(object sender, EventArgs e)
        {
            if (running || selected == null) return;
            if (!GateForWrite()) return;
            if (!EnsureHook()) return;
            int room = SelectedRoomId();
            if (room < 0)
            {
                AppendLog("Choose a room before using Move.",
                    PlannerLogLevel.Error);
                return;
            }
            StartRun(ActionParams.MoveToRoom(room),
                "Move to room " + room + " (" + LocationName(room) + ")");
        }

        private PlanMode SelectedPlanMode()
        {
            int index = planModeCombo == null ? 0 : planModeCombo.SelectedIndex;
            if (index < 0 || index > (int)PlanMode.Ignore) index = 0;
            return (PlanMode)index;
        }

        private PlanTrigger SelectedPlanTrigger()
        {
            int index = planTriggerCombo == null
                ? 0 : planTriggerCombo.SelectedIndex;
            if (index < 0 || index > (int)PlanTrigger.Either) index = 0;
            return (PlanTrigger)index;
        }

        /// <summary>The plan owned by the character selected in the roster.</summary>
        private PlanQueue CurrentPlan()
        {
            if (selected == null) return null;
            int key = selected.Seat >= 0
                ? selected.Seat : unchecked((int)selected.Pointer);
            PlanQueue plan;
            if (!plans.TryGetValue(key, out plan))
            {
                plan = new PlanQueue();
                plan.Seat = selected.Seat;
                plan.Character = selected.Pointer;
                plan.CharacterName = selected.Name ?? "";
                plans[key] = plan;
            }
            else
            {
                // Pointers change on scene/class reloads; keep the plan keyed
                // by seat but refresh the pointer it points at.
                plan.Seat = selected.Seat;
                plan.Character = selected.Pointer;
                plan.CharacterName = selected.Name ?? "";
            }
            return plan;
        }

        private PlanRunner EnsurePlanRunner()
        {
            if (planRunner == null && engine != null && mem != null &&
                mem.IsAttached)
            {
                planRunner = new PlanRunner(engine, new ActionStateReader(mem),
                    mem, LogFromThread);
                planRunner.PlanFinished += PlanRunner_PlanFinished;
            }
            return planRunner;
        }

        private void PlanRunner_PlanFinished(PlanQueue plan)
        {
            SafeInvoke(delegate
            {
                UpdatePlanUi();
                UpdatePlannerActive();
            });
        }

        private void UpdatePlannerActive()
        {
            bool active = running ||
                (planRunner != null && planRunner.HasActivePlans);
            PlannerActiveChanged?.Invoke(active);
        }

        private void PlanAddAction_Click(object sender, EventArgs e)
        {
            if (selected == null) return;
            PlanQueue plan = CurrentPlan();
            if (plan == null) return;
            if (plan.Started)
            {
                AppendLog("Stop the plan before editing its steps.",
                    PlannerLogLevel.Warning);
                return;
            }

            string error;
            ActionParams p = BuildTargetParams(out error);
            if (p == null)
            {
                AppendLog(error, PlannerLogLevel.Error);
                return;
            }

            CharacterInfo target = SelectedTarget();
            CharacterInfo about = SelectedAbout();
            string label = "Action " + ShortActionName(p.ActionId) +
                " -> " + (target == null ? "?" : target.Name);
            if (about != null) label += " about " + about.Name;

            PlanStep step = new PlanStep(p, label);
            step.TargetCharacter = target == null ? 0 : target.Pointer;
            step.TargetSeat = target == null ? -1 : target.Seat;
            step.AboutCharacter = about == null ? 0 : about.Pointer;
            step.AboutSeat = about == null ? -1 : about.Seat;
            plan.Add(step);
            plan.Mode = SelectedPlanMode();
            plan.Trigger = SelectedPlanTrigger();
            plan.Repeat = planRepeatCheck.Checked;
            plan.Shuffle = planShuffleCheck.Checked;
            AppendLog("Added plan step: " + label, PlannerLogLevel.Info);
            UpdatePlanUi();
        }

        private void PlanAddMove_Click(object sender, EventArgs e)
        {
            if (selected == null) return;
            PlanQueue plan = CurrentPlan();
            if (plan == null) return;
            if (plan.Started)
            {
                AppendLog("Stop the plan before editing its steps.",
                    PlannerLogLevel.Warning);
                return;
            }

            int room = SelectedRoomId();
            if (room < 0)
            {
                AppendLog("Choose a room before adding a move step.",
                    PlannerLogLevel.Error);
                return;
            }

            string label = "Move to room " + room + " (" +
                LocationName(room) + ")";
            plan.Add(new PlanStep(ActionParams.MoveToRoom(room), label));
            plan.Mode = SelectedPlanMode();
            plan.Trigger = SelectedPlanTrigger();
            plan.Repeat = planRepeatCheck.Checked;
            plan.Shuffle = planShuffleCheck.Checked;
            AppendLog("Added plan step: " + label, PlannerLogLevel.Info);
            UpdatePlanUi();
        }

        private int SelectedPlanIndex()
        {
            if (planList == null || planList.SelectedIndices.Count == 0) return -1;
            return planList.SelectedIndices[0];
        }

        private void PlanRemove_Click(object sender, EventArgs e)
        {
            PlanQueue plan = CurrentPlan();
            if (plan == null || plan.Started) return;
            int index = SelectedPlanIndex();
            if (index < 0) return;
            plan.RemoveAt(index);
            UpdatePlanUi();
        }

        private void PlanUp_Click(object sender, EventArgs e)
        {
            PlanQueue plan = CurrentPlan();
            if (plan == null || plan.Started) return;
            int index = SelectedPlanIndex();
            if (index < 0) return;
            if (plan.MoveUp(index))
            {
                for (int i = 0; i < planList.Items.Count; i++)
                    planList.Items[i].Selected = false;
                if (index - 1 < planList.Items.Count)
                    planList.Items[index - 1].Selected = true;
            }
            UpdatePlanUi();
        }

        private void PlanDown_Click(object sender, EventArgs e)
        {
            PlanQueue plan = CurrentPlan();
            if (plan == null || plan.Started) return;
            int index = SelectedPlanIndex();
            if (index < 0) return;
            if (plan.MoveDown(index))
            {
                for (int i = 0; i < planList.Items.Count; i++)
                    planList.Items[i].Selected = false;
                if (index + 1 < planList.Items.Count)
                    planList.Items[index + 1].Selected = true;
            }
            UpdatePlanUi();
        }

        private void PlanClear_Click(object sender, EventArgs e)
        {
            PlanQueue plan = CurrentPlan();
            if (plan == null || plan.Started) return;
            plan.ClearSteps();
            UpdatePlanUi();
        }

        private void PlanItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (suppressPlanChecks || selected == null) return;

            PlanQueue plan = CurrentPlan();
            if (plan == null || e.Index < 0 || e.Index >= plan.Steps.Count)
                return;

            PlanStep step = plan.Steps[e.Index];
            if (step == null) return;

            if (plan.Started)
            {
                e.NewValue = e.CurrentValue;
                AppendLog("Stop the plan before enabling or disabling steps.",
                    PlannerLogLevel.Warning);
                return;
            }

            bool enabled = e.NewValue == CheckState.Checked;
            if (step.Enabled == enabled) return;
            step.Enabled = enabled;
            AppendLog((enabled ? "Enabled" : "Disabled") + " step " +
                (e.Index + 1) + ".", PlannerLogLevel.Info);
        }

        private void PlanCopy_Click(object sender, EventArgs e)
        {
            PlanQueue plan = CurrentPlan();
            if (plan == null || plan.Count == 0) return;

            clipSteps.Clear();
            foreach (PlanStep step in plan.Steps)
                if (step != null) clipSteps.Add(step.Clone());
            clipMode = plan.Mode;
            clipTrigger = plan.Trigger;
            clipRepeat = plan.Repeat;
            clipShuffle = plan.Shuffle;
            hasClipboard = true;

            AppendLog("Copied " + clipSteps.Count + " plan step(s).",
                PlannerLogLevel.Info);
            UpdatePlanButtons();
        }

        private void PlanPaste_Click(object sender, EventArgs e)
        {
            PlanQueue plan = CurrentPlan();
            if (plan == null || !hasClipboard) return;
            if (plan.Started)
            {
                AppendLog("Stop the plan before pasting.",
                    PlannerLogLevel.Warning);
                return;
            }
            if (plan.Count > 0 &&
                MessageBox.Show(this,
                    "Replace the current plan with the copied one?",
                    "Paste plan", MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            plan.ClearSteps();
            foreach (PlanStep step in clipSteps)
                if (step != null) plan.Add(step.Clone());
            plan.Mode = clipMode;
            plan.Trigger = clipTrigger;
            plan.Repeat = clipRepeat;
            plan.Shuffle = clipShuffle;

            // Force the mode/trigger/flag controls and the list to refresh.
            displayedPlan = null;
            AppendLog("Pasted " + plan.Count + " plan step(s).",
                PlannerLogLevel.Info);
            UpdatePlanUi();
        }

        private void PlanSave_Click(object sender, EventArgs e)
        {
            if (mem == null || !mem.IsAttached) return;
            string path = PlanStore.DefaultPath;
            try
            {
                int count = 0;
                foreach (PlanQueue plan in plans.Values)
                    if (plan.Count > 0) count++;
                PlanStore.Save(path, plans.Values);
                AppendLog("Saved " + count + " plan(s) to " + path,
                    PlannerLogLevel.Info);
            }
            catch (Exception ex)
            {
                AppendLog("Saving plans failed: " + ex.Message,
                    PlannerLogLevel.Error);
            }
        }

        private void PlanLoad_Click(object sender, EventArgs e)
        {
            if (mem == null || !mem.IsAttached) return;
            string path = PlanStore.DefaultPath;
            if (!File.Exists(path))
            {
                AppendLog("No plans file at " + path,
                    PlannerLogLevel.Warning);
                return;
            }

            try
            {
                List<PlanQueue> loaded = PlanStore.Load(path);
                int applied = 0;
                int skipped = 0;
                foreach (PlanQueue plan in loaded)
                {
                    PlanQueue existing;
                    if (plans.TryGetValue(plan.Seat, out existing) &&
                        existing.Started)
                    {
                        skipped++;
                        continue;
                    }
                    plans[plan.Seat] = plan;
                    applied++;
                }

                displayedPlan = null;
                UpdatePlanUi();
                AppendLog("Loaded " + applied + " plan(s) from " + path +
                    (skipped > 0
                        ? " (" + skipped + " running plan(s) skipped)" : ""),
                    PlannerLogLevel.Info);
            }
            catch (Exception ex)
            {
                AppendLog("Loading plans failed: " + ex.Message,
                    PlannerLogLevel.Error);
            }
        }

        private void PlanStart_Click(object sender, EventArgs e)
        {
            if (selected == null) return;
            if (!GateForWrite()) return;
            if (!EnsureHook()) return;

            PlanQueue plan = CurrentPlan();
            if (plan == null) return;
            if (plan.Count == 0)
            {
                AppendLog("Add at least one step to the plan.",
                    PlannerLogLevel.Warning);
                return;
            }

            plan.Mode = SelectedPlanMode();
            plan.Trigger = SelectedPlanTrigger();
            plan.Repeat = planRepeatCheck.Checked;
            plan.Shuffle = planShuffleCheck.Checked;

            PlanRunner runner = EnsurePlanRunner();
            if (runner == null)
            {
                AppendLog("The plan runner is unavailable.",
                    PlannerLogLevel.Error);
                return;
            }

            string message;
            if (!runner.StartPlan(plan, out message))
            {
                AppendLog(message, PlannerLogLevel.Error);
                return;
            }

            // PlanRunner.StartPlan already logs "Plan started ..." with the
            // step count and mode; do not duplicate it here.
            UpdatePlannerActive();
            UpdatePlanUi();
        }

        private void PlanStop_Click(object sender, EventArgs e)
        {
            PlanQueue plan = CurrentPlan();
            if (plan == null) return;
            if (planRunner != null) planRunner.StopPlan(plan, false);
            plan.Stop();
            UpdatePlannerActive();
            UpdatePlanUi();
        }

        /// <summary>Refreshes the plan list from the selected character's plan.</summary>
        private void UpdatePlanUi()
        {
            if (planList == null || planStatusLabel == null) return;

            PlanQueue plan = selected == null ? null : CurrentPlan();
            bool planChanged = !ReferenceEquals(plan, displayedPlan);
            displayedPlan = plan;

            // The state timer runs every 250 ms. Rebuilding/retouching the list
            // on every tick made the cells flicker, so only touch the UI when
            // the content actually changed.
            string listSignature = BuildPlanListSignature(plan);
            string statusSignature = BuildPlanStatusSignature(plan);
            bool listChanged = planChanged ||
                listSignature != lastPlanListSignature;
            bool statusChanged = statusSignature != lastPlanStatusSignature;
            if (!listChanged && !statusChanged) return;

            lastPlanListSignature = listSignature;
            lastPlanStatusSignature = statusSignature;

            if (plan == null)
            {
                if (planList.Items.Count > 0) planList.Items.Clear();
                SetLabelText(planStatusLabel, "Plan: select a character");
                SetLabelText(planCurrentLabel, "");
                UpdatePlanButtons();
                return;
            }

            if (planChanged)
            {
                planModeCombo.SelectedIndex = (int)plan.Mode;
                planTriggerCombo.SelectedIndex = (int)plan.Trigger;
                planRepeatCheck.Checked = plan.Repeat;
                planShuffleCheck.Checked = plan.Shuffle;
                if (planList.Items.Count > 0) planList.Items.Clear();
            }

            int selectedIndex = SelectedPlanIndex();
            if (listChanged)
            {
                bool previousSuppress = suppressPlanChecks;
                suppressPlanChecks = true;
                try
                {
                    planList.BeginUpdate();
                    try
                    {
                        while (planList.Items.Count > plan.Steps.Count)
                            planList.Items.RemoveAt(planList.Items.Count - 1);
                        while (planList.Items.Count < plan.Steps.Count)
                        {
                            ListViewItem item = new ListViewItem(new string[]
                                { "", "", "", "" });
                            item.UseItemStyleForSubItems = true;
                            planList.Items.Add(item);
                        }

                        for (int i = 0; i < plan.Steps.Count; i++)
                        {
                            PlanStep step = plan.Steps[i];
                            if (step == null) continue;
                            ListViewItem item = planList.Items[i];
                            SetSubItemText(item.SubItems[1], (i + 1).ToString());
                            SetSubItemText(item.SubItems[2], step.Describe());
                            SetSubItemText(item.SubItems[3], step.StatusText());
                            string tip = string.IsNullOrEmpty(step.Note)
                                ? step.Describe() : step.Note;
                            if (item.ToolTipText != tip) item.ToolTipText = tip;
                            Color back = plan.CurrentIndex == i
                                ? Color.FromArgb(255, 248, 214) : Color.White;
                            if (item.BackColor != back) item.BackColor = back;
                        }
                    }
                    finally
                    {
                        planList.EndUpdate();
                    }

                    // Change check state after EndUpdate: setting it inside
                    // BeginUpdate with LVS_EX_CHECKBOXES can re-enter the
                    // native ListView while it is still batching messages.
                    for (int i = 0; i < plan.Steps.Count; i++)
                    {
                        PlanStep step = plan.Steps[i];
                        if (step == null) continue;
                        ListViewItem item = planList.Items[i];
                        if (item.Checked != step.Enabled)
                            item.Checked = step.Enabled;
                    }
                }
                finally
                {
                    suppressPlanChecks = previousSuppress;
                }
            }

            if (selectedIndex >= 0 && selectedIndex < planList.Items.Count)
            {
                for (int i = 0; i < planList.Items.Count; i++)
                {
                    bool want = (i == selectedIndex);
                    if (planList.Items[i].Selected != want)
                        planList.Items[i].Selected = want;
                }
            }

            if (statusChanged || planChanged)
            {
                string status = planRunner == null
                    ? "not running" : planRunner.GetStatus(plan);
                SetLabelText(planStatusLabel, plan.Finished
                    ? "Plan: finished"
                    : "Plan: " + status + "  |  " + plan.ProgressText());
                PlanStep currentStep = plan.Current;
                SetLabelText(planCurrentLabel, currentStep == null
                    ? "" : "Now: " + currentStep.Describe());
                UpdatePlanButtons();
            }
        }

        private static string BuildPlanListSignature(PlanQueue plan)
        {
            if (plan == null) return "null";
            StringBuilder sb = new StringBuilder();
            sb.Append(plan.Character).Append('|')
              .Append(plan.CurrentIndex).Append('|')
              .Append(plan.Steps.Count);
            for (int i = 0; i < plan.Steps.Count; i++)
            {
                PlanStep step = plan.Steps[i];
                sb.Append('|');
                if (step == null) continue;
                sb.Append(step.Describe()).Append('|').Append(step.StatusText())
                  .Append('|').Append(step.Enabled ? '1' : '0')
                  .Append('|').Append(step.Note ?? "");
            }
            return sb.ToString();
        }

        private string BuildPlanStatusSignature(PlanQueue plan)
        {
            if (plan == null) return "null";
            StringBuilder sb = new StringBuilder();
            sb.Append(plan.Finished).Append('|')
              .Append(plan.Started).Append('|')
              .Append(plan.ProgressText()).Append('|');
            sb.Append(planRunner == null ? "" : planRunner.GetStatus(plan))
              .Append('|');
            sb.Append(planRunner == null ? "" : planRunner.GetDiagnostic(plan))
              .Append('|');
            PlanStep current = plan.Current;
            sb.Append(current == null ? "" : current.Describe());
            return sb.ToString();
        }

        private static void SetSubItemText(
            ListViewItem.ListViewSubItem item, string text)
        {
            if (item.Text != text) item.Text = text;
        }

        private static void SetLabelText(Label label, string text)
        {
            if (label != null && label.Text != text) label.Text = text;
        }

        private void UpdatePlanButtons()
        {
            if (planStartButton == null) return;

            bool attached = mem != null && mem.IsAttached && selected != null;
            PlanQueue plan = attached ? CurrentPlan() : null;
            bool started = plan != null && plan.Started;
            bool editable = attached && !started && !running;
            int selectedIndex = SelectedPlanIndex();

            planAddButton.Enabled = editable;
            planAddMoveButton.Enabled = editable && SelectedRoomId() >= 0;
            planRemoveButton.Enabled = editable && plan != null &&
                selectedIndex >= 0;
            planUpButton.Enabled = editable && plan != null &&
                selectedIndex > 0;
            planDownButton.Enabled = editable && plan != null &&
                selectedIndex >= 0 && selectedIndex < plan.Count - 1;
            planClearButton.Enabled = editable && plan != null && plan.Count > 0;
            planCopyButton.Enabled = attached && plan != null && plan.Count > 0;
            planPasteButton.Enabled = editable && hasClipboard;
            planSaveButton.Enabled = attached;
            planLoadButton.Enabled = attached;
            planStartButton.Enabled = attached && !started &&
                !running && plan != null && plan.Count > 0;
            planStopButton.Enabled = started;
            planModeCombo.Enabled = !started && !running;
            planTriggerCombo.Enabled = !started && !running;
            planRepeatCheck.Enabled = !started && !running;
            planShuffleCheck.Enabled = !started && !running;
        }

        private void StartRun(ActionParams parameters, string label)
        {
            if (running || selected == null || engine == null) return;

            CharacterInfo actor = selected;
            List<CharacterInfo> roster = new List<CharacterInfo>(ctx.Characters);
            bool retry = retryCheck.Checked;
            bool wait = waitCheck.Checked;
            bool keep = keepCheck.Checked;

            running = true;
            cancelRequested = false;
            UpdateControls();
            UpdatePlannerActive();
            lastResultLabel.Text = "Running: " + label;
            AppendLog(label, PlannerLogLevel.Info);

            ActionPlannerEngine planner = engine;
            Task.Run(() =>
            {
                PlannerRunResult result = null;
                try
                {
                    result = planner.Run(actor, roster, parameters, label, retry,
                        wait, keep, delegate { return cancelRequested; },
                        LogFromThread);
                }
                catch (Exception ex)
                {
                    LogFromThread("Planner exception: " + ex.Message,
                        PlannerLogLevel.Error);
                }
                SafeInvoke(delegate { FinishRun(result); });
            });
        }

        private void FinishRun(PlannerRunResult result)
        {
            running = false;
            UpdateControls();
            UpdatePlannerActive();
            if (result == null)
                lastResultLabel.Text = "Last result: failed";
            else if (result.Observed)
                lastResultLabel.Text = "Last result: observed";
            else if (result.Cancelled)
                lastResultLabel.Text = "Last result: cancelled";
            else if (result.TimedOut)
                lastResultLabel.Text = "Last result: timed out";
            else
                lastResultLabel.Text = "Last result: not observed";
            UpdateStateNow();
        }

        private void StopButton_Click(object sender, EventArgs e)
        {
            if (selected == null || engine == null) return;
            if (!GateForWrite()) return;

            cancelRequested = true;
            engine.ClearQueue();

            CharacterInfo actor = selected;
            List<CharacterInfo> roster = new List<CharacterInfo>(ctx.Characters);
            AppendLog("Stop action: " + actor.Name, PlannerLogLevel.Warning);

            ActionPlannerEngine planner = engine;
            Task.Run(() =>
            {
                string message;
                bool ok = planner.StopCurrentAction(actor, roster, out message);
                LogFromThread(ok ? message : "Stop failed: " + message,
                    ok ? PlannerLogLevel.Success : PlannerLogLevel.Error);
                Thread.Sleep(300);
                SafeInvoke(delegate { UpdateStateNow(); });
            });
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            cancelRequested = true;
            if (engine != null) engine.ClearQueue();
            AppendLog("Retry cancel requested.", PlannerLogLevel.Warning);
        }

        private void DumpStates_Click(object sender, EventArgs e)
        {
            if (mem == null || !mem.IsAttached || ctx == null || engine == null)
                return;

            IList<CharacterInfo> roster = ctx.Characters;
            foreach (CharacterInfo c in roster)
            {
                if (c == null) continue;
                CharacterState s = engine.ReadState(c, roster);
                AppendLog(c.ToString() + "  " + engine.Describe(s),
                    PlannerLogLevel.Detail);
            }
        }

        private void SaveLog_Click(object sender, EventArgs e)
        {
            bool live = logTabs != null && logTabs.SelectedIndex == 0;
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Filter = "Text files|*.txt|All files|*.*";
                dialog.FileName = live
                    ? "AA2LiveEvents.txt" : "AA2ActionPlannerLog.txt";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    RichTextBox active = ActiveLogBox();
                    File.WriteAllText(dialog.FileName,
                        active == null ? "" : active.Text);
                    AppendLog("Log saved: " + dialog.FileName,
                        PlannerLogLevel.Info);
                }
                catch (Exception ex)
                {
                    AppendLog("Saving the log failed: " + ex.Message,
                        PlannerLogLevel.Error);
                }
            }
        }

        private RichTextBox ActiveLogBox()
        {
            if (logTabs != null && logTabs.SelectedIndex == 1)
                return logBox ?? liveLogBox;
            return liveLogBox ?? logBox;
        }

        private void LogFromThread(string text, PlannerLogLevel level)
        {
            SafeInvoke(delegate { AppendLog(text, level); });
        }

        private void AppendLog(string text, PlannerLogLevel level)
        {
            if (logBox == null || logBox.IsDisposed) return;
            if (string.IsNullOrEmpty(text)) return;

            if (logBox.TextLength > 200000) logBox.Clear();

            Color color;
            switch (level)
            {
                case PlannerLogLevel.Success: color = Color.SeaGreen; break;
                case PlannerLogLevel.Warning: color = Color.FromArgb(190, 110, 20); break;
                case PlannerLogLevel.Error: color = Color.Firebrick; break;
                case PlannerLogLevel.Detail: color = Color.DimGray; break;
                default: color = Color.FromArgb(40, 44, 52); break;
            }

            logBox.SelectionStart = logBox.TextLength;
            logBox.SelectionLength = 0;
            logBox.SelectionColor = color;
            logBox.AppendText(text + Environment.NewLine);
            logBox.SelectionColor = Color.FromArgb(40, 44, 52);
            logBox.SelectionStart = logBox.TextLength;
            logBox.ScrollToCaret();
        }

        private void SafeInvoke(Action action)
        {
            if (action == null || IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke(action); }
            catch (InvalidOperationException) { }
        }
    }
}
