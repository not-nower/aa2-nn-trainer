using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
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
        private string rosterSignature = "";
        private NameMap choiceNames;
        private readonly List<CharacterInfo> rosterSnapshot =
            new List<CharacterInfo>();

        public Func<bool> ConfirmWriteAllowed { get; set; }
        public event Action<bool> PlannerActiveChanged;

        private CheckBox hookCheck;
        private Label hookStatusLabel;
        private ToolTip hookTooltip;
        private Label actorLabel;
        private ComboBox targetCombo;
        private ComboBox aboutCombo;
        private ComboBox actionCombo;
        private ComboBox roomCombo;
        private Label roomGuardLabel;
        private Button talkButton;
        private Button roomButton;
        private Button stopButton;
        private Button cancelButton;
        private CheckBox retryCheck;
        private CheckBox waitCheck;
        private CheckBox keepCheck;
        private Label actorStateLabel;
        private Label targetStateLabel;
        private RichTextBox logBox;
        private Label lastResultLabel;
        private System.Windows.Forms.Timer stateTimer;

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
            root.RowCount = 7;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            root.Margin = new Padding(0);
            Controls.Add(root);

            root.Controls.Add(BuildHookRow(), 0, 0);
            root.Controls.Add(BuildSelectionRow(), 0, 1);
            root.Controls.Add(BuildActionRow(), 0, 2);
            root.Controls.Add(BuildButtonsRow(), 0, 3);
            root.Controls.Add(BuildStatePanel(), 0, 4);
            root.Controls.Add(BuildLogBox(), 0, 5);
            root.Controls.Add(BuildLogToolbar(), 0, 6);
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

            talkButton = MakeButton("Talk", 72, TalkButton_Click);
            stopButton = MakeButton("Stop action", 100, StopButton_Click);
            cancelButton = MakeButton("Cancel retry", 100, CancelButton_Click);
            row.Controls.Add(talkButton);
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

        private Control BuildStatePanel()
        {
            TableLayoutPanel panel = new TableLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.ColumnCount = 1;
            panel.RowCount = 2;
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            panel.Margin = new Padding(0, 4, 0, 2);
            panel.BackColor = Color.FromArgb(243, 245, 248);
            panel.Padding = new Padding(6, 2, 6, 2);

            actorStateLabel = MakeStateLabel("Actor: ?");
            targetStateLabel = MakeStateLabel("Target: ?");
            panel.Controls.Add(actorStateLabel, 0, 0);
            panel.Controls.Add(targetStateLabel, 0, 1);
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
            logBox = new RichTextBox();
            logBox.Dock = DockStyle.Fill;
            logBox.ReadOnly = true;
            logBox.BackColor = Color.White;
            logBox.BorderStyle = BorderStyle.FixedSingle;
            logBox.Font = new Font("Consolas", 8.5F);
            logBox.WordWrap = false;
            logBox.DetectUrls = false;
            logBox.HideSelection = false;
            logBox.Margin = new Padding(0);
            return logBox;
        }

        private Control BuildLogToolbar()
        {
            FlowLayoutPanel row = new FlowLayoutPanel();
            row.Dock = DockStyle.Fill;
            row.WrapContents = false;
            row.FlowDirection = FlowDirection.LeftToRight;
            row.Padding = new Padding(0, 2, 0, 0);
            row.Margin = new Padding(0);

            Button clear = MakeButton("Clear", 70, delegate { logBox.Clear(); });
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
                // restore bytes through the new process.
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

            string signature = BuildRosterSignature(context);
            if (actorChanged || signature != rosterSignature)
            {
                rosterSignature = signature;
                PopulateChoices();
            }

            UpdateActorLabel();
            SetWriteAllowed(canWrite);
            EnsureChoiceLists();
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
            rosterSignature = "";
            suppressSelection = true;
            if (targetCombo != null) targetCombo.Items.Clear();
            if (aboutCombo != null) aboutCombo.Items.Clear();
            suppressSelection = false;
            if (actorLabel != null) actorLabel.Text = "No character selected";
            if (actorStateLabel != null) actorStateLabel.Text = "Actor: ?";
            if (targetStateLabel != null) targetStateLabel.Text = "Target: ?";
            if (roomGuardLabel != null) roomGuardLabel.Text = "Room: ?";
            UpdateHookStatus();
            UpdateControls();
            PlannerActiveChanged?.Invoke(false);
        }

        private static string BuildRosterSignature(GameContext context)
        {
            if (context == null || context.Characters == null) return "";
            string s = "";
            foreach (CharacterInfo c in context.Characters)
            {
                if (c == null) continue;
                s += c.Pointer.ToString("X") + ":" + c.Seat + ":" + c.Name + "|";
            }
            return s;
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
        }

        private void UpdateControls()
        {
            if (hookCheck == null) return;
            bool attached = mem != null && mem.IsAttached && selected != null;
            bool installed = engine != null && engine.HookInstalled;
            bool editable = attached && writeAllowed && !running;

            hookCheck.Enabled = attached &&
                (installed || (writeAllowed && !running));
            targetCombo.Enabled = editable;
            aboutCombo.Enabled = editable;
            actionCombo.Enabled = editable;
            roomCombo.Enabled = editable;
            bool hasAction = SelectedActionId() >= 0;
            roomButton.Enabled = editable && SelectedRoomId() >= 0;
            talkButton.Enabled = editable && hasAction;
            retryCheck.Enabled = editable;
            waitCheck.Enabled = editable;
            keepCheck.Enabled = editable;
            stopButton.Enabled = attached;
            cancelButton.Enabled = running;
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
                roomGuardLabel.Text = "Room: ?";
                return;
            }

            IList<CharacterInfo> roster = ctx.Characters;
            CharacterState actorState = engine.ReadState(selected, roster);
            actorStateLabel.Text = "Actor: " + StateText(actorState);

            CharacterInfo target = SelectedTarget();
            CharacterState targetState = target == null
                ? null : engine.ReadState(target, roster);
            targetStateLabel.Text = target == null
                ? "Target: none"
                : "Target: " + StateText(targetState);

            UpdateRoomGuard(actorState, targetState);
        }

        private string StateText(CharacterState s)
        {
            if (s == null) return "unreadable";
            return "room " + s.Room +
                "  |  " + ActionPlannerEngine.MovementName(s.MovementType) +
                " (" + s.MovementType + ")" +
                "  |  action " + ShortActionName(s.ActionId) +
                "  |  busy " + s.AnimLocked + "/" + s.Loading + "/" +
                s.InConversation +
                (s.Talking != 0 ? "  |  talking" : "");
        }

        private void UpdateRoomGuard(CharacterState actorState,
            CharacterState targetState)
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

        private ActionParams BuildTalkParams(out string error)
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
                error = "Choose a target for Talk.";
                return null;
            }

            IList<CharacterInfo> roster = ctx.Characters;
            CharacterState targetState = engine.ReadState(target, roster);
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
                CharacterState aboutState = engine.ReadState(about, roster);
                if (aboutState == null || aboutState.Activity == 0)
                {
                    error = "About-target activity is unreadable.";
                    return null;
                }
                p.Target2 = aboutState.Activity;
            }
            return p;
        }

        private void TalkButton_Click(object sender, EventArgs e)
        {
            if (running || selected == null) return;
            if (!GateForWrite()) return;
            if (!EnsureHook()) return;

            string error;
            ActionParams p = BuildTalkParams(out error);
            if (p == null)
            {
                AppendLog(error, PlannerLogLevel.Error);
                return;
            }

            CharacterInfo target = SelectedTarget();
            CharacterInfo about = SelectedAbout();
            string label = "Talk " + ShortActionName(p.ActionId) +
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
            PlannerActiveChanged?.Invoke(true);
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
            PlannerActiveChanged?.Invoke(false);
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
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Filter = "Text files|*.txt|All files|*.*";
                dialog.FileName = "AA2ActionPlannerLog.txt";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    File.WriteAllText(dialog.FileName, logBox.Text);
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
