using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace AA2Trainer
{
    public sealed class CharacterChoice
    {
        public int Value { get; private set; }
        public string Text { get; private set; }

        public CharacterChoice(int value, string text)
        {
            Value = value;
            Text = text;
        }

        public override string ToString()
        {
            return Text;
        }
    }

    public sealed class CharacterChoiceSet
    {
        public IList<CharacterChoice> Personality;
        public IList<CharacterChoice> Club;
        public IList<CharacterChoice> Social;
        public IList<CharacterChoice> FightingStyle;
        public IList<CharacterChoice> Virtue;
        public IList<CharacterChoice> SexualOrientation;
        public IList<CharacterChoice> VoicePitch;
        public IList<CharacterChoice> Intelligence;
        public IList<CharacterChoice> Grade;
    }

    public sealed class CharacterDataControl : UserControl
    {
        public event EventHandler CharacterSaved;

        // Set by the main form. Gives the editor a chance to re-run the
        // fail-safe health check immediately before a write.
        public Func<bool> ConfirmWriteAllowed { get; set; }

        private GameMemory mem;
        private CharacterInfo character;
        private uint loadedPointer;
        private CharacterSnapshot loaded = new CharacterSnapshot();
        private bool gateAllowed;

        private List<CharacterChoice> personalityChoices = new List<CharacterChoice>();
        private List<CharacterChoice> clubChoices = new List<CharacterChoice>();
        private List<CharacterChoice> socialChoices = new List<CharacterChoice>();
        private List<CharacterChoice> fightingStyleChoices = new List<CharacterChoice>();
        private List<CharacterChoice> virtueChoices = new List<CharacterChoice>();
        private List<CharacterChoice> sexualOrientationChoices = new List<CharacterChoice>();
        private List<CharacterChoice> voicePitchChoices = new List<CharacterChoice>();
        private List<CharacterChoice> intelligenceChoices = new List<CharacterChoice>();
        private List<CharacterChoice> gradeChoices = new List<CharacterChoice>();
        private List<CharacterChoice> strengthChoices = new List<CharacterChoice>();
        private List<CharacterChoice> strengthGradeChoices = new List<CharacterChoice>();
        private List<CharacterChoice> clubGradeChoices = new List<CharacterChoice>();
        private string personalitySignature = "";
        private string clubSignature = "";
        private string socialSignature = "";
        private string fightingStyleSignature = "";
        private string virtueSignature = "";
        private string sexualOrientationSignature = "";
        private string voicePitchSignature = "";
        private string intelligenceSignature = "";
        private string gradeSignature = "";
        private string strengthSignature = "";
        private string strengthGradeSignature = "";
        private string clubGradeSignature = "";

        private TextBox firstNameBox;
        private TextBox lastNameBox;
        private TextBox profileBox;
        private Label profileBytesLabel;
        private ComboBox personalityCombo;
        private ComboBox clubCombo;
        private ComboBox socialCombo;
        private ComboBox fightingStyleCombo;
        private ComboBox virtueCombo;
        private ComboBox sexualOrientationCombo;
        private ComboBox voicePitchCombo;
        private ComboBox intelligenceCombo;
        private NumericUpDown academicLevelBox;
        private NumericUpDown academicValueBox;
        private ComboBox academicGradeCombo;
        private NumericUpDown academicRankBox;
        private ComboBox strengthCombo;
        private NumericUpDown strengthLevelBox;
        private NumericUpDown strengthValueBox;
        private ComboBox strengthGradeCombo;
        private NumericUpDown strengthRankBox;
        private NumericUpDown clubLevelBox;
        private NumericUpDown clubValueBox;
        private ComboBox clubGradeCombo;
        private NumericUpDown victoryBox;
        private NumericUpDown classSkipBox;
        private NumericUpDown winningOverBox;
        private NumericUpDown partnerBox;
        private NumericUpDown rejectedBox;
        private NumericUpDown sexIntercourseBox;
        private NumericUpDown sexPartnerBox;
        private TextBox firstTimeBox;
        private TextBox firstAnalBox;
        private TextBox lastTimeBox;
        private CheckBox sexExperienceCheck;
        private CheckBox analExperienceCheck;
        private Button applyButton;
        private Button revertButton;
        private Label gateLabel;
        private Label statusLabel;
        private DataGridView modulesGrid;
        private Label modulesStatusLabel;
        private LinkLabel modulesRefreshLink;
        private AauModuleReadResult modulesResult;
        private uint modulesCharacterPointer;
        private bool modulesCacheValid;
        private DateTime modulesRetryUtc = DateTime.MinValue;
        private ToolTip tooltip;

        private const string ModulesTooltip =
            "AAU modules are stored in the character card and loaded with the class. " +
            "Use AA2Edit to change them, then reload the character.";

        public CharacterDataControl()
        {
            DoubleBuffered = true;
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(248, 249, 251);
            BuildUi();
        }

        private void BuildUi()
        {
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

            TableLayoutPanel columns = new TableLayoutPanel();
            columns.Dock = DockStyle.Fill;
            columns.ColumnCount = 3;
            columns.RowCount = 1;
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34F));
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33F));
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33F));
            columns.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            columns.Margin = new Padding(0);
            root.Controls.Add(columns, 0, 0);

            TableLayoutPanel left = new TableLayoutPanel();
            left.Dock = DockStyle.Fill;
            left.ColumnCount = 1;
            left.RowCount = 4;
            left.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 88F));
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 88F));
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, 208F));
            left.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            left.Margin = new Padding(0, 0, 6, 0);
            columns.Controls.Add(left, 0, 0);

            TableLayoutPanel right = new TableLayoutPanel();
            right.Dock = DockStyle.Fill;
            right.ColumnCount = 1;
            right.RowCount = 2;
            right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, 380F));
            right.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            right.Margin = new Padding(6, 0, 6, 0);
            columns.Controls.Add(right, 1, 0);

            TableLayoutPanel statusColumn = new TableLayoutPanel();
            statusColumn.Dock = DockStyle.Fill;
            statusColumn.ColumnCount = 1;
            statusColumn.RowCount = 3;
            statusColumn.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            statusColumn.RowStyles.Add(new RowStyle(SizeType.Absolute, 250F));
            statusColumn.RowStyles.Add(new RowStyle(SizeType.Absolute, 135F));
            statusColumn.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            statusColumn.Margin = new Padding(6, 0, 0, 0);
            columns.Controls.Add(statusColumn, 2, 0);

            GroupBox nameGroup = new GroupBox();
            nameGroup.Text = "Name";
            nameGroup.Dock = DockStyle.Fill;
            nameGroup.Padding = new Padding(10, 4, 10, 4);
            nameGroup.Margin = new Padding(0, 2, 0, 0);

            TableLayoutPanel nameTable = new TableLayoutPanel();
            nameTable.Dock = DockStyle.Fill;
            nameTable.ColumnCount = 2;
            nameTable.RowCount = 3;
            nameTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96F));
            nameTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            nameTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            nameTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            nameTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            nameTable.Margin = new Padding(0);

            Label firstCaption = MakeCaption("First name");
            nameTable.Controls.Add(firstCaption, 0, 0);

            firstNameBox = MakeTextBox();
            nameTable.Controls.Add(firstNameBox, 1, 0);

            Label lastCaption = MakeCaption("Last name");
            nameTable.Controls.Add(lastCaption, 0, 1);

            lastNameBox = MakeTextBox();
            nameTable.Controls.Add(lastNameBox, 1, 1);

            nameGroup.Controls.Add(nameTable);
            left.Controls.Add(nameGroup, 0, 0);

            GroupBox profileGroup = new GroupBox();
            profileGroup.Text = "Profile";
            profileGroup.Dock = DockStyle.Fill;
            profileGroup.Padding = new Padding(10, 4, 10, 2);
            profileGroup.Margin = new Padding(0, 4, 0, 0);

            TableLayoutPanel profileTable = new TableLayoutPanel();
            profileTable.Dock = DockStyle.Fill;
            profileTable.ColumnCount = 1;
            profileTable.RowCount = 2;
            profileTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            profileTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            profileTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 16F));
            profileTable.Margin = new Padding(0);

            profileBox = new TextBox();
            profileBox.Dock = DockStyle.Fill;
            profileBox.Multiline = true;
            profileBox.ScrollBars = ScrollBars.Vertical;
            profileBox.AcceptsReturn = true;
            profileBox.AcceptsTab = true;
            profileBox.WordWrap = true;
            profileBox.MaxLength = CharacterEditor.ProfileMaxBytes;
            profileBox.Font = new Font("Segoe UI", 9F);
            profileBox.Margin = new Padding(0);
            profileBox.TextChanged += delegate { UpdateProfileBytes(); };
            profileTable.Controls.Add(profileBox, 0, 0);

            profileBytesLabel = new Label();
            profileBytesLabel.Dock = DockStyle.Fill;
            profileBytesLabel.TextAlign = ContentAlignment.MiddleRight;
            profileBytesLabel.Font = new Font("Segoe UI", 8F);
            profileBytesLabel.ForeColor = Color.DimGray;
            profileBytesLabel.Text = "0 / " + CharacterEditor.ProfileMaxBytes.ToString() + " bytes";
            profileBytesLabel.Margin = new Padding(0);
            profileTable.Controls.Add(profileBytesLabel, 0, 1);

            profileGroup.Controls.Add(profileTable);
            left.Controls.Add(profileGroup, 0, 1);

            GroupBox attributesGroup = new GroupBox();
            attributesGroup.Text = "Attributes";
            attributesGroup.Dock = DockStyle.Fill;
            attributesGroup.Padding = new Padding(10, 2, 10, 2);
            attributesGroup.Margin = new Padding(0, 4, 0, 0);

            TableLayoutPanel attributesTable = new TableLayoutPanel();
            attributesTable.Dock = DockStyle.Fill;
            attributesTable.ColumnCount = 2;
            attributesTable.RowCount = 8;
            attributesTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110F));
            attributesTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (int i = 0; i < 7; i++)
                attributesTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
            attributesTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            attributesTable.Margin = new Padding(0);

            personalityCombo = MakeCombo();
            AddAttribute(attributesTable, 0, "Personality", personalityCombo);

            clubCombo = MakeCombo();
            AddAttribute(attributesTable, 1, "Club", clubCombo);

            socialCombo = MakeCombo();
            AddAttribute(attributesTable, 2, "Social", socialCombo);

            fightingStyleCombo = MakeCombo();
            AddAttribute(attributesTable, 3, "Fighting Style", fightingStyleCombo);

            virtueCombo = MakeCombo();
            AddAttribute(attributesTable, 4, "Virtue", virtueCombo);

            sexualOrientationCombo = MakeCombo();
            AddAttribute(attributesTable, 5, "Orientation", sexualOrientationCombo);

            voicePitchCombo = MakeCombo();
            AddAttribute(attributesTable, 6, "Voice Pitch", voicePitchCombo);

            attributesGroup.Controls.Add(attributesTable);
            left.Controls.Add(attributesGroup, 0, 2);

            GroupBox modulesGroup = new GroupBox();
            modulesGroup.Text = "Modules (AAU, read-only)";
            modulesGroup.Dock = DockStyle.Fill;
            modulesGroup.Padding = new Padding(10, 4, 10, 4);
            modulesGroup.Margin = new Padding(0, 4, 0, 0);

            TableLayoutPanel modulesTable = new TableLayoutPanel();
            modulesTable.Dock = DockStyle.Fill;
            modulesTable.ColumnCount = 1;
            modulesTable.RowCount = 2;
            modulesTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            modulesTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
            modulesTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            modulesTable.Margin = new Padding(0);

            TableLayoutPanel modulesHeader = new TableLayoutPanel();
            modulesHeader.Dock = DockStyle.Fill;
            modulesHeader.ColumnCount = 2;
            modulesHeader.RowCount = 1;
            modulesHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            modulesHeader.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            modulesHeader.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            modulesHeader.Margin = new Padding(0);

            modulesStatusLabel = new Label();
            modulesStatusLabel.Dock = DockStyle.Fill;
            modulesStatusLabel.AutoSize = false;
            modulesStatusLabel.AutoEllipsis = true;
            modulesStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
            modulesStatusLabel.Font = new Font("Segoe UI", 8F);
            modulesStatusLabel.ForeColor = Color.DimGray;
            modulesStatusLabel.Margin = new Padding(0);
            modulesStatusLabel.Text = "No AAU modules";
            modulesHeader.Controls.Add(modulesStatusLabel, 0, 0);

            modulesRefreshLink = new LinkLabel();
            modulesRefreshLink.Text = "Refresh";
            modulesRefreshLink.AutoSize = true;
            modulesRefreshLink.Font = new Font("Segoe UI", 8F);
            modulesRefreshLink.Margin = new Padding(6, 2, 0, 0);
            modulesRefreshLink.LinkClicked += delegate
            {
                modulesCacheValid = false;
                RefreshModules();
            };
            modulesHeader.Controls.Add(modulesRefreshLink, 1, 0);

            modulesTable.Controls.Add(modulesHeader, 0, 0);

            modulesGrid = new ModuleGrid();
            modulesGrid.Dock = DockStyle.Fill;
            modulesGrid.BackgroundColor = Color.White;
            modulesGrid.BorderStyle = BorderStyle.FixedSingle;
            modulesGrid.RowHeadersVisible = false;
            modulesGrid.AllowUserToAddRows = false;
            modulesGrid.AllowUserToDeleteRows = false;
            modulesGrid.AllowUserToResizeRows = false;
            modulesGrid.ReadOnly = true;
            modulesGrid.MultiSelect = false;
            modulesGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            modulesGrid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            modulesGrid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            modulesGrid.ColumnHeadersHeight = 26;
            modulesGrid.ColumnHeadersHeightSizeMode =
                DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            modulesGrid.EnableHeadersVisualStyles = false;
            modulesGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(240, 241, 244);
            modulesGrid.ColumnHeadersDefaultCellStyle.SelectionBackColor =
                Color.FromArgb(240, 241, 244);
            modulesGrid.ColumnHeadersDefaultCellStyle.SelectionForeColor =
                Color.FromArgb(30, 30, 30);
            modulesGrid.ColumnHeadersDefaultCellStyle.Font =
                new Font("Segoe UI", 8.5F, FontStyle.Bold);
            modulesGrid.DefaultCellStyle.Font = new Font("Segoe UI", 9F);
            modulesGrid.DefaultCellStyle.SelectionBackColor = SystemColors.Highlight;
            modulesGrid.ClipboardCopyMode =
                DataGridViewClipboardCopyMode.EnableWithoutHeaderText;
            modulesGrid.Margin = new Padding(0);

            DataGridViewTextBoxColumn moduleName = new DataGridViewTextBoxColumn();
            moduleName.HeaderText = "Module";
            moduleName.FillWeight = 35F;
            moduleName.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            moduleName.ReadOnly = true;
            moduleName.SortMode = DataGridViewColumnSortMode.NotSortable;
            modulesGrid.Columns.Add(moduleName);

            DataGridViewTextBoxColumn moduleDescription = new DataGridViewTextBoxColumn();
            moduleDescription.HeaderText = "Description";
            moduleDescription.FillWeight = 65F;
            moduleDescription.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            moduleDescription.ReadOnly = true;
            moduleDescription.SortMode = DataGridViewColumnSortMode.NotSortable;
            modulesGrid.Columns.Add(moduleDescription);

            modulesTable.Controls.Add(modulesGrid, 0, 1);
            modulesGroup.Controls.Add(modulesTable);
            left.Controls.Add(modulesGroup, 0, 3);

            GroupBox academicGroup = new GroupBox();
            academicGroup.Text = "Academic";
            academicGroup.Dock = DockStyle.Fill;
            academicGroup.Padding = new Padding(10, 4, 10, 4);
            academicGroup.Margin = new Padding(0, 2, 0, 0);

            TableLayoutPanel academicTable = new TableLayoutPanel();
            academicTable.Dock = DockStyle.Fill;
            academicTable.ColumnCount = 2;
            academicTable.RowCount = 16;
            academicTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90F));
            academicTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (int i = 0; i < 15; i++)
            {
                bool spacer = i == 5 || i == 11;
                academicTable.RowStyles.Add(new RowStyle(SizeType.Absolute, spacer ? 14F : 25F));
            }
            academicTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            academicTable.Margin = new Padding(0);

            intelligenceCombo = MakeCombo();
            AddAcademic(academicTable, 0, "Intelligence", intelligenceCombo);

            academicLevelBox = MakeNumberBox(0, CharacterEditor.MaxIntelligenceRank, 1);
            AddAcademic(academicTable, 1, "Level", academicLevelBox);

            academicValueBox = MakeNumberBox(0, CharacterEditor.MaxIntelligenceValue, 1);
            academicValueBox.ThousandsSeparator = true;
            AddAcademic(academicTable, 2, "Value", academicValueBox);

            academicGradeCombo = MakeCombo();
            AddAcademic(academicTable, 3, "Grade", academicGradeCombo);

            academicRankBox = MakeNumberBox(0, CharacterEditor.MaxIntelligenceTopRank, 1);
            AddAcademic(academicTable, 4, "Rank", academicRankBox);

            strengthCombo = MakeCombo();
            AddAcademic(academicTable, 6, "Strength", strengthCombo);

            strengthLevelBox = MakeNumberBox(0, CharacterEditor.MaxStrengthRank, 1);
            AddAcademic(academicTable, 7, "Level", strengthLevelBox);

            strengthValueBox = MakeNumberBox(0, CharacterEditor.MaxStrengthValue, 1);
            strengthValueBox.ThousandsSeparator = true;
            AddAcademic(academicTable, 8, "Value", strengthValueBox);

            strengthGradeCombo = MakeCombo();
            AddAcademic(academicTable, 9, "Grade", strengthGradeCombo);

            strengthRankBox = MakeNumberBox(0, CharacterEditor.MaxStrengthTopRank, 1);
            AddAcademic(academicTable, 10, "Rank", strengthRankBox);

            clubLevelBox = MakeNumberBox(0, CharacterEditor.MaxClubLevel, 1);
            AddAcademic(academicTable, 12, "Club Level", clubLevelBox);

            clubValueBox = MakeNumberBox(0, CharacterEditor.MaxClubValue, 1);
            clubValueBox.ThousandsSeparator = true;
            AddAcademic(academicTable, 13, "Value", clubValueBox);

            clubGradeCombo = MakeCombo();
            AddAcademic(academicTable, 14, "Grade", clubGradeCombo);

            academicGroup.Controls.Add(academicTable);
            right.Controls.Add(academicGroup, 0, 0);

            GroupBox statusGroup = new GroupBox();
            statusGroup.Text = "Status";
            statusGroup.Dock = DockStyle.Fill;
            statusGroup.Padding = new Padding(10, 4, 10, 4);
            statusGroup.Margin = new Padding(0, 2, 0, 0);

            TableLayoutPanel statusTable = new TableLayoutPanel();
            statusTable.Dock = DockStyle.Fill;
            statusTable.ColumnCount = 2;
            statusTable.RowCount = 8;
            statusTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));
            statusTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (int i = 0; i < 7; i++)
                statusTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 31F));
            statusTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            statusTable.Margin = new Padding(0);

            victoryBox = MakeNumberBox(0, int.MaxValue, 1);
            victoryBox.ThousandsSeparator = true;
            AddAcademic(statusTable, 0, "Victory Count", victoryBox);

            classSkipBox = MakeNumberBox(0, int.MaxValue, 1);
            classSkipBox.ThousandsSeparator = true;
            AddAcademic(statusTable, 1, "Classes Skipped", classSkipBox);

            winningOverBox = MakeNumberBox(0, int.MaxValue, 1);
            winningOverBox.ThousandsSeparator = true;
            AddAcademic(statusTable, 2, "Won Over", winningOverBox);

            partnerBox = MakeNumberBox(0, int.MaxValue, 1);
            partnerBox.ThousandsSeparator = true;
            AddAcademic(statusTable, 3, "Partners", partnerBox);

            rejectedBox = MakeNumberBox(0, int.MaxValue, 1);
            rejectedBox.ThousandsSeparator = true;
            AddAcademic(statusTable, 4, "Rejections", rejectedBox);

            sexIntercourseBox = MakeNumberBox(0, int.MaxValue, 1);
            sexIntercourseBox.ThousandsSeparator = true;
            AddAcademic(statusTable, 5, "H Encounters", sexIntercourseBox);

            sexPartnerBox = MakeNumberBox(0, int.MaxValue, 1);
            sexPartnerBox.ThousandsSeparator = true;
            AddAcademic(statusTable, 6, "H Partners", sexPartnerBox);

            statusGroup.Controls.Add(statusTable);
            statusColumn.Controls.Add(statusGroup, 0, 0);

            GroupBox historyGroup = new GroupBox();
            historyGroup.Text = "H History";
            historyGroup.Dock = DockStyle.Fill;
            historyGroup.Padding = new Padding(10, 4, 10, 4);
            historyGroup.Margin = new Padding(0, 4, 0, 0);

            TableLayoutPanel historyTable = new TableLayoutPanel();
            historyTable.Dock = DockStyle.Fill;
            historyTable.ColumnCount = 2;
            historyTable.RowCount = 5;
            historyTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));
            historyTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (int i = 0; i < 4; i++)
                historyTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
            historyTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            historyTable.Margin = new Padding(0);

            firstTimeBox = MakeHistoryTextBox();
            AddAcademic(historyTable, 0, "First Partner", firstTimeBox);

            firstAnalBox = MakeHistoryTextBox();
            AddAcademic(historyTable, 1, "First Anal", firstAnalBox);

            lastTimeBox = MakeHistoryTextBox();
            AddAcademic(historyTable, 2, "Last Partner", lastTimeBox);

            TableLayoutPanel experienceTable = new TableLayoutPanel();
            experienceTable.Dock = DockStyle.Fill;
            experienceTable.ColumnCount = 2;
            experienceTable.RowCount = 1;
            experienceTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            experienceTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            experienceTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            experienceTable.Margin = new Padding(0);

            sexExperienceCheck = new CheckBox();
            sexExperienceCheck.Text = "Vaginal";
            sexExperienceCheck.AutoSize = true;
            sexExperienceCheck.Anchor = AnchorStyles.Left;
            sexExperienceCheck.Font = new Font("Segoe UI", 9F);
            sexExperienceCheck.Margin = new Padding(0);
            experienceTable.Controls.Add(sexExperienceCheck, 0, 0);

            analExperienceCheck = new CheckBox();
            analExperienceCheck.Text = "Anal";
            analExperienceCheck.AutoSize = true;
            analExperienceCheck.Anchor = AnchorStyles.Left;
            analExperienceCheck.Font = new Font("Segoe UI", 9F);
            analExperienceCheck.Margin = new Padding(0);
            experienceTable.Controls.Add(analExperienceCheck, 1, 0);

            AddAcademic(historyTable, 3, "Experience", experienceTable);

            historyGroup.Controls.Add(historyTable);
            statusColumn.Controls.Add(historyGroup, 0, 1);

            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.Dock = DockStyle.Fill;
            actions.FlowDirection = FlowDirection.LeftToRight;
            actions.WrapContents = false;
            actions.Padding = new Padding(0, 4, 0, 0);
            actions.Margin = new Padding(0);

            applyButton = new Button();
            applyButton.Text = "&Apply";
            applyButton.Size = new Size(88, 28);
            applyButton.UseVisualStyleBackColor = true;
            applyButton.Click += delegate { ApplyChanges(); };
            actions.Controls.Add(applyButton);

            revertButton = new Button();
            revertButton.Text = "&Revert";
            revertButton.Size = new Size(88, 28);
            revertButton.UseVisualStyleBackColor = true;
            revertButton.Click += delegate { RevertChanges(); };
            actions.Controls.Add(revertButton);

            gateLabel = new Label();
            gateLabel.AutoSize = true;
            gateLabel.Margin = new Padding(14, 9, 0, 0);
            gateLabel.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            gateLabel.Text = "Write gate: locked";
            gateLabel.ForeColor = Color.Firebrick;
            actions.Controls.Add(gateLabel);

            root.Controls.Add(actions, 0, 1);

            statusLabel = new Label();
            statusLabel.Dock = DockStyle.Fill;
            statusLabel.AutoSize = false;
            statusLabel.AutoEllipsis = true;
            statusLabel.TextAlign = ContentAlignment.TopLeft;
            statusLabel.ForeColor = Color.DimGray;
            statusLabel.Margin = new Padding(0);
            root.Controls.Add(statusLabel, 0, 2);

            tooltip = new ToolTip();
            tooltip.SetToolTip(gateLabel,
                "Edits are written only when the known game build and all pointer checks pass.");
            tooltip.SetToolTip(modulesStatusLabel, ModulesTooltip);
            tooltip.SetToolTip(modulesGrid,
                "Read-only list of AAU modules stored in this character's card data.");
            tooltip.SetToolTip(modulesRefreshLink,
                "Re-read the module list from the character card.");
            tooltip.SetToolTip(firstNameBox, "First name shown in the class roster.");
            tooltip.SetToolTip(lastNameBox, "Last name shown in the class roster.");
            tooltip.SetToolTip(profileBox,
                "Profile text is stored as Shift-JIS with Windows line endings. Maximum 511 bytes.");
            tooltip.SetToolTip(personalityCombo, "Personality stored in the character profile.");
            tooltip.SetToolTip(clubCombo, "Club index from the current class header.");
            tooltip.SetToolTip(socialCombo, "Social attribute (LOW_HIGH).");
            tooltip.SetToolTip(fightingStyleCombo, "Fighting style (FIGHTSTYLE).");
            tooltip.SetToolTip(virtueCombo, "Virtue (LOW_HIGH).");
            tooltip.SetToolTip(sexualOrientationCombo, "Sexual orientation (SEXUALITY).");
            tooltip.SetToolTip(voicePitchCombo, "Voice pitch (LOW_HIGH).");
            tooltip.SetToolTip(intelligenceCombo, "Intelligence level (LOW_HIGHPLUS).");
            tooltip.SetToolTip(academicLevelBox, "Intelligence rank/level (0-8).");
            tooltip.SetToolTip(academicValueBox, "Intelligence value (0-1000).");
            tooltip.SetToolTip(academicGradeCombo, "Academic grade: none, F through A.");
            tooltip.SetToolTip(academicRankBox, "Top academic rank (0-255, 255 = unranked).");
            tooltip.SetToolTip(strengthCombo, "Strength level (LOW_HIGHPLUS).");
            tooltip.SetToolTip(strengthLevelBox, "Strength rank/level (0-8).");
            tooltip.SetToolTip(strengthValueBox, "Strength value (0-1000).");
            tooltip.SetToolTip(strengthGradeCombo, "Strength grade: none, F through A.");
            tooltip.SetToolTip(strengthRankBox, "Top strength rank (0-255, 255 = unranked).");
            tooltip.SetToolTip(clubLevelBox, "Club level (0-8).");
            tooltip.SetToolTip(clubValueBox, "Club value (0-1000).");
            tooltip.SetToolTip(clubGradeCombo, "Club grade: none, F through A.");
            tooltip.SetToolTip(victoryBox, "Victory count.");
            tooltip.SetToolTip(classSkipBox, "Classes skipped.");
            tooltip.SetToolTip(winningOverBox, "Won over count.");
            tooltip.SetToolTip(partnerBox, "Partner count.");
            tooltip.SetToolTip(rejectedBox, "Rejected count.");
            tooltip.SetToolTip(sexIntercourseBox, "H encounter count.");
            tooltip.SetToolTip(sexPartnerBox, "H partner count.");
            tooltip.SetToolTip(firstTimeBox, "First partner name (max 31 Shift-JIS bytes).");
            tooltip.SetToolTip(firstAnalBox, "First anal partner name (max 31 Shift-JIS bytes).");
            tooltip.SetToolTip(lastTimeBox, "Last partner name (max 31 Shift-JIS bytes).");
            tooltip.SetToolTip(sexExperienceCheck, "Vaginal experience flag.");
            tooltip.SetToolTip(analExperienceCheck, "Anal experience flag.");

            UpdateGate();
        }

        private static Label MakeCaption(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = false;
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.ForeColor = Color.DimGray;
            label.Margin = new Padding(0);
            return label;
        }

        private static void AddAttribute(TableLayoutPanel table, int row,
            string caption, ComboBox combo)
        {
            table.Controls.Add(MakeCaption(caption), 0, row);
            combo.Margin = new Padding(0, 1, 0, 1);
            table.Controls.Add(combo, 1, row);
        }

        private static void AddAcademic(TableLayoutPanel table, int row,
            string caption, Control control)
        {
            table.Controls.Add(MakeCaption(caption), 0, row);
            control.Margin = new Padding(0, 1, 0, 1);
            table.Controls.Add(control, 1, row);
        }

        private static TextBox MakeTextBox()
        {
            TextBox box = new TextBox();
            box.Dock = DockStyle.Fill;
            box.MaxLength = 120;
            box.Font = new Font("Segoe UI", 10F);
            box.Margin = new Padding(0, 3, 0, 3);
            return box;
        }

        private static TextBox MakeHistoryTextBox()
        {
            TextBox box = new TextBox();
            box.Dock = DockStyle.None;
            box.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            box.MaxLength = CharacterEditor.HHistoryMaxBytes;
            box.Font = new Font("Segoe UI", 9F);
            box.Margin = new Padding(0, 1, 0, 1);
            return box;
        }

        private static ComboBox MakeCombo()
        {
            ComboBox combo = new ComboBox();
            combo.Dock = DockStyle.None;
            combo.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.Font = new Font("Segoe UI", 9F);
            return combo;
        }

        private static NumericUpDown MakeNumberBox(int min, int max, int increment)
        {
            NumericUpDown box = new NumericUpDown();
            box.Dock = DockStyle.None;
            box.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            box.Minimum = min;
            box.Maximum = max;
            box.Increment = increment;
            box.Font = new Font("Segoe UI", 9F);
            box.TextAlign = HorizontalAlignment.Left;
            box.Margin = new Padding(0, 4, 0, 4);
            return box;
        }

        public void SetChoices(CharacterChoiceSet choices)
        {
            if (choices == null) choices = new CharacterChoiceSet();

            UpdateChoices(personalityCombo, choices.Personality,
                ref personalitySignature, ref personalityChoices,
                SelectedValue(personalityCombo, loaded.Personality));
            UpdateChoices(clubCombo, choices.Club,
                ref clubSignature, ref clubChoices,
                SelectedValue(clubCombo, loaded.Club));
            UpdateChoices(socialCombo, choices.Social,
                ref socialSignature, ref socialChoices,
                SelectedValue(socialCombo, loaded.Social));
            UpdateChoices(fightingStyleCombo, choices.FightingStyle,
                ref fightingStyleSignature, ref fightingStyleChoices,
                SelectedValue(fightingStyleCombo, loaded.FightingStyle));
            UpdateChoices(virtueCombo, choices.Virtue,
                ref virtueSignature, ref virtueChoices,
                SelectedValue(virtueCombo, loaded.Virtue));
            UpdateChoices(sexualOrientationCombo, choices.SexualOrientation,
                ref sexualOrientationSignature, ref sexualOrientationChoices,
                SelectedValue(sexualOrientationCombo, loaded.SexualOrientation));
            UpdateChoices(voicePitchCombo, choices.VoicePitch,
                ref voicePitchSignature, ref voicePitchChoices,
                SelectedValue(voicePitchCombo, loaded.VoicePitch));
            UpdateChoices(intelligenceCombo, choices.Intelligence,
                ref intelligenceSignature, ref intelligenceChoices,
                SelectedValue(intelligenceCombo, loaded.IntelligenceLevel));
            UpdateChoices(academicGradeCombo, choices.Grade,
                ref gradeSignature, ref gradeChoices,
                SelectedValue(academicGradeCombo, loaded.IntelligenceGrade));
            UpdateChoices(strengthCombo, choices.Intelligence,
                ref strengthSignature, ref strengthChoices,
                SelectedValue(strengthCombo, loaded.StrengthLevel));
            UpdateChoices(strengthGradeCombo, choices.Grade,
                ref strengthGradeSignature, ref strengthGradeChoices,
                SelectedValue(strengthGradeCombo, loaded.StrengthGrade));
            UpdateChoices(clubGradeCombo, choices.Grade,
                ref clubGradeSignature, ref clubGradeChoices,
                SelectedValue(clubGradeCombo, loaded.ClubGrade));
        }

        private void UpdateChoices(ComboBox combo, IList<CharacterChoice> choices,
            ref string signature, ref List<CharacterChoice> cache, int currentValue)
        {
            string newSignature = Signature(choices);
            if (string.Equals(newSignature, signature, StringComparison.Ordinal)) return;

            signature = newSignature;
            cache = CopyChoices(choices);
            RebuildCombo(combo, cache, currentValue);
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

            // Do not replace pending edits on an auto-refresh tick.
            if (!sameCharacter || !IsDirty)
            {
                CharacterSnapshot snapshot;
                string error;
                if (CharacterEditor.TryRead(mem, info, out snapshot, out error))
                {
                    bool changed = !sameCharacter || !SnapshotsEqual(snapshot, loaded);
                    if (changed) ApplySnapshot(snapshot);
                    loaded = snapshot;
                    loadedPointer = info.Pointer;

                    if (!sameCharacter)
                        SetStatus("Loaded the current character data from live memory.", false);
                }
                else
                {
                    loaded = new CharacterSnapshot();
                    loadedPointer = info.Pointer;
                    ApplySnapshot(loaded);
                    SetStatus(error, true);
                }
            }

            RefreshModules();
            UpdateGate();
        }

        public void SetWriteAllowed(bool allowed)
        {
            gateAllowed = allowed;
            UpdateGate();
        }

        /// <summary>
        /// Applies the live Status / H History counters without touching the
        /// editable identity fields. Skipped while the form has unsaved edits
        /// or one of the status controls has focus, so a live tick cannot
        /// overwrite typing.
        /// </summary>
        public void ApplyLiveStatus(LiveCharacterStatus status)
        {
            if (status == null || character == null || loadedPointer == 0) return;
            if (status.Pointer != loadedPointer) return;
            if (mem == null || !mem.IsAttached) return;
            if (IsDirty) return;
            if (StatusControlFocused()) return;

            SetNumberIfChanged(victoryBox, status.VictoryCount);
            SetNumberIfChanged(classSkipBox, status.ClassSkip);
            SetNumberIfChanged(winningOverBox, status.WinningOver);
            SetNumberIfChanged(partnerBox, status.Partner);
            SetNumberIfChanged(rejectedBox, status.Rejected);
            SetNumberIfChanged(sexIntercourseBox, status.SexIntercourse);
            SetNumberIfChanged(sexPartnerBox, status.SexPartner);
            SetChoiceIfChanged(academicGradeCombo, status.IntelligenceGrade);
            SetChoiceIfChanged(strengthGradeCombo, status.StrengthGrade);
            SetChoiceIfChanged(clubGradeCombo, status.ClubGrade);
            SetNumberIfChanged(academicRankBox, status.IntelligenceTopRank);
            SetNumberIfChanged(strengthRankBox, status.StrengthTopRank);
            SetTextIfChanged(firstTimeBox, status.FirstTime);
            SetTextIfChanged(firstAnalBox, status.FirstAnal);
            SetTextIfChanged(lastTimeBox, status.LastTime);
            SetCheckIfChanged(sexExperienceCheck, status.SexExperience);
            SetCheckIfChanged(analExperienceCheck, status.AnalExperience);

            loaded.VictoryCount = status.VictoryCount;
            loaded.ClassSkip = status.ClassSkip;
            loaded.WinningOver = status.WinningOver;
            loaded.Partner = status.Partner;
            loaded.Rejected = status.Rejected;
            loaded.SexIntercourse = status.SexIntercourse;
            loaded.SexPartner = status.SexPartner;
            loaded.IntelligenceGrade = status.IntelligenceGrade;
            loaded.StrengthGrade = status.StrengthGrade;
            loaded.ClubGrade = status.ClubGrade;
            loaded.IntelligenceTopRank = status.IntelligenceTopRank;
            loaded.StrengthTopRank = status.StrengthTopRank;
            loaded.FirstTime = status.FirstTime;
            loaded.FirstAnal = status.FirstAnal;
            loaded.LastTime = status.LastTime;
            loaded.SexExperience = status.SexExperience;
            loaded.AnalExperience = status.AnalExperience;
        }

        private bool StatusControlFocused()
        {
            Control[] controls = new Control[]
            {
                victoryBox, classSkipBox, winningOverBox, partnerBox, rejectedBox,
                sexIntercourseBox, sexPartnerBox, academicGradeCombo,
                strengthGradeCombo, clubGradeCombo, academicRankBox, strengthRankBox,
                firstTimeBox, firstAnalBox, lastTimeBox, sexExperienceCheck,
                analExperienceCheck
            };
            foreach (Control control in controls)
            {
                if (control != null && control.ContainsFocus) return true;
            }
            return false;
        }

        public void Clear()
        {
            mem = null;
            character = null;
            loadedPointer = 0;
            loaded = new CharacterSnapshot();
            gateAllowed = false;

            firstNameBox.Text = "";
            lastNameBox.Text = "";
            profileBox.Text = "";
            SelectFirst(personalityCombo);
            SelectFirst(clubCombo);
            SelectFirst(socialCombo);
            SelectFirst(fightingStyleCombo);
            SelectFirst(virtueCombo);
            SelectFirst(sexualOrientationCombo);
            SelectFirst(voicePitchCombo);
            SelectFirst(intelligenceCombo);
            SelectFirst(academicGradeCombo);
            SetNumberValue(academicLevelBox, 0);
            SetNumberValue(academicValueBox, 0);
            SetNumberValue(academicRankBox, 0);
            SelectFirst(strengthCombo);
            SelectFirst(strengthGradeCombo);
            SetNumberValue(strengthLevelBox, 0);
            SetNumberValue(strengthValueBox, 0);
            SetNumberValue(strengthRankBox, 0);
            SetNumberValue(clubLevelBox, 0);
            SetNumberValue(clubValueBox, 0);
            SelectFirst(clubGradeCombo);
            SetNumberValue(victoryBox, 0);
            SetNumberValue(classSkipBox, 0);
            SetNumberValue(winningOverBox, 0);
            SetNumberValue(partnerBox, 0);
            SetNumberValue(rejectedBox, 0);
            SetNumberValue(sexIntercourseBox, 0);
            SetNumberValue(sexPartnerBox, 0);
            firstTimeBox.Text = "";
            firstAnalBox.Text = "";
            lastTimeBox.Text = "";
            sexExperienceCheck.Checked = false;
            analExperienceCheck.Checked = false;
            UpdateProfileBytes();
            ClearModules();

            SetStatus("Attach to the game and select a character.", false);
            UpdateGate();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible)
            {
                modulesCacheValid = false;
                RefreshModules();
            }
        }

        private void RefreshModules()
        {
            if (modulesGrid == null || modulesStatusLabel == null) return;
            if (mem == null || !mem.IsAttached || character == null)
            {
                ClearModules();
                return;
            }

            if (modulesCacheValid && modulesCharacterPointer == character.Pointer)
                return;

            DateTime now = DateTime.UtcNow;
            if (modulesResult != null &&
                modulesCharacterPointer == character.Pointer &&
                now < modulesRetryUtc)
            {
                ApplyModules(modulesResult);
                return;
            }

            AauModuleReadResult result = AauModuleReader.Read(mem, character.Pointer);
            modulesResult = result;
            modulesCharacterPointer = character.Pointer;
            modulesCacheValid = result.Status != AauModuleStatus.Error;
            modulesRetryUtc = result.Status == AauModuleStatus.Error
                ? now.AddSeconds(3) : DateTime.MinValue;
            ApplyModules(result);
        }

        private void ApplyModules(AauModuleReadResult result)
        {
            if (modulesGrid == null || modulesStatusLabel == null) return;

            modulesGrid.SuspendLayout();
            try
            {
                modulesGrid.Rows.Clear();
                if (result != null)
                {
                    foreach (AauModuleInfo module in result.Modules)
                    {
                        int index = modulesGrid.Rows.Add();
                        DataGridViewRow row = modulesGrid.Rows[index];
                        row.Cells[0].Value = module.Name ?? "";
                        row.Cells[1].Value = CleanModuleText(module.Description);
                        row.Cells[0].ToolTipText = module.Name ?? "";
                        row.Cells[1].ToolTipText = module.Description ?? "";
                    }
                }
            }
            finally
            {
                modulesGrid.ResumeLayout();
            }

            if (modulesRefreshLink != null)
                modulesRefreshLink.Enabled = character != null;

            if (result == null)
            {
                modulesStatusLabel.Text = "No AAU modules";
                modulesStatusLabel.ForeColor = Color.DimGray;
                SetModulesTooltip(ModulesTooltip);
                return;
            }

            switch (result.Status)
            {
                case AauModuleStatus.Ok:
                {
                    int count = result.Modules.Count;
                    modulesStatusLabel.Text = (count == 1
                        ? "1 module" : count + " modules") +
                        " \u00B7 read-only";
                    modulesStatusLabel.ForeColor = Color.DimGray;
                    SetModulesTooltip(ModulesTooltip);
                    break;
                }
                case AauModuleStatus.NoAauData:
                    modulesStatusLabel.Text = "No AAU card data";
                    modulesStatusLabel.ForeColor = Color.DimGray;
                    SetModulesTooltip(ModulesTooltip);
                    break;
                case AauModuleStatus.NoModules:
                    modulesStatusLabel.Text = "No AAU modules";
                    modulesStatusLabel.ForeColor = Color.DimGray;
                    SetModulesTooltip(ModulesTooltip);
                    break;
                default:
                    modulesStatusLabel.Text = "Module data unreadable";
                    modulesStatusLabel.ForeColor = Color.Firebrick;
                    SetModulesTooltip(string.IsNullOrEmpty(result.Error)
                        ? "The AAU module list could not be read."
                        : result.Error);
                    break;
            }
        }

        private void ClearModules()
        {
            modulesResult = null;
            modulesCharacterPointer = 0;
            modulesCacheValid = false;
            modulesRetryUtc = DateTime.MinValue;

            if (modulesGrid != null) modulesGrid.Rows.Clear();
            if (modulesStatusLabel != null)
            {
                modulesStatusLabel.Text = "No AAU modules";
                modulesStatusLabel.ForeColor = Color.DimGray;
            }
            if (modulesRefreshLink != null) modulesRefreshLink.Enabled = false;
            SetModulesTooltip(ModulesTooltip);
        }

        private void SetModulesTooltip(string text)
        {
            if (tooltip != null && modulesStatusLabel != null)
                tooltip.SetToolTip(modulesStatusLabel, text);
        }

        private static string CleanModuleText(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Replace("\r\n", " ")
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Trim();
        }

        private void ApplyChanges()
        {
            if (mem == null || !mem.IsAttached)
            {
                SetStatus("The game is not attached.", true);
                UpdateGate();
                return;
            }
            if (character == null)
            {
                SetStatus("Select a character first.", true);
                UpdateGate();
                return;
            }
            if (ConfirmWriteAllowed != null && !ConfirmWriteAllowed())
            {
                SetStatus("Write gate is locked. Wait for a clean pointer check and try again.", true);
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

            CharacterSnapshot edited = ReadUi();
            if (SnapshotsEqual(edited, loaded))
            {
                SetStatus("No changes to apply.", false);
                return;
            }

            string changed;
            string error;
            if (!CharacterEditor.TryWrite(mem, character, loaded, edited, out changed, out error))
            {
                SetStatus(error, true);
                UpdateGate();
                return;
            }

            CharacterSnapshot check;
            string readError;
            if (CharacterEditor.TryRead(mem, character, out check, out readError))
            {
                ApplySnapshot(check);
                loaded = check;
            }
            else
            {
                loaded = edited;
            }
            loadedPointer = character.Pointer;

            SetStatus(string.IsNullOrEmpty(changed) ? "Saved." : "Saved: " + changed + ".", false);
            if (CharacterSaved != null) CharacterSaved(this, EventArgs.Empty);
        }

        private void RevertChanges()
        {
            if (mem == null || character == null || !mem.IsAttached)
            {
                SetStatus("The game is not attached.", true);
                return;
            }

            CharacterSnapshot snapshot;
            string error;
            if (!CharacterEditor.TryRead(mem, character, out snapshot, out error))
            {
                SetStatus(error, true);
                return;
            }

            ApplySnapshot(snapshot);
            loaded = snapshot;
            loadedPointer = character.Pointer;
            SetStatus("Reverted to the values currently in memory.", false);
        }

        private CharacterSnapshot ReadUi()
        {
            CharacterSnapshot snapshot = new CharacterSnapshot();
            snapshot.FirstName = firstNameBox.Text;
            snapshot.LastName = lastNameBox.Text;
            snapshot.Profile = CharacterEditor.NormalizeProfileText(profileBox.Text);
            snapshot.Personality = SelectedValue(personalityCombo, loaded.Personality);
            snapshot.Club = SelectedValue(clubCombo, loaded.Club);
            snapshot.Social = SelectedValue(socialCombo, loaded.Social);
            snapshot.FightingStyle = SelectedValue(fightingStyleCombo, loaded.FightingStyle);
            snapshot.Virtue = SelectedValue(virtueCombo, loaded.Virtue);
            snapshot.SexualOrientation = SelectedValue(sexualOrientationCombo,
                loaded.SexualOrientation);
            snapshot.VoicePitch = SelectedValue(voicePitchCombo, loaded.VoicePitch);
            snapshot.IntelligenceLevel = SelectedValue(intelligenceCombo,
                loaded.IntelligenceLevel);
            snapshot.IntelligenceRank = (int)academicLevelBox.Value;
            snapshot.IntelligenceValue = (int)academicValueBox.Value;
            snapshot.IntelligenceGrade = SelectedValue(academicGradeCombo,
                loaded.IntelligenceGrade);
            snapshot.IntelligenceTopRank = (int)academicRankBox.Value;
            snapshot.StrengthLevel = SelectedValue(strengthCombo, loaded.StrengthLevel);
            snapshot.StrengthRank = (int)strengthLevelBox.Value;
            snapshot.StrengthValue = (int)strengthValueBox.Value;
            snapshot.StrengthGrade = SelectedValue(strengthGradeCombo, loaded.StrengthGrade);
            snapshot.StrengthTopRank = (int)strengthRankBox.Value;
            snapshot.ClubLevel = (int)clubLevelBox.Value;
            snapshot.ClubValue = (int)clubValueBox.Value;
            snapshot.ClubGrade = SelectedValue(clubGradeCombo, loaded.ClubGrade);
            snapshot.VictoryCount = (int)victoryBox.Value;
            snapshot.ClassSkip = (int)classSkipBox.Value;
            snapshot.WinningOver = (int)winningOverBox.Value;
            snapshot.Partner = (int)partnerBox.Value;
            snapshot.Rejected = (int)rejectedBox.Value;
            snapshot.SexIntercourse = (int)sexIntercourseBox.Value;
            snapshot.SexPartner = (int)sexPartnerBox.Value;
            snapshot.FirstTime = firstTimeBox.Text;
            snapshot.FirstAnal = firstAnalBox.Text;
            snapshot.LastTime = lastTimeBox.Text;
            snapshot.SexExperience = sexExperienceCheck.Checked;
            snapshot.AnalExperience = analExperienceCheck.Checked;
            return snapshot;
        }

        private bool IsDirty
        {
            get { return !SnapshotsEqual(ReadUi(), loaded); }
        }

        // Compared by reflection so a new field in CharacterSnapshot is
        // covered automatically; forgetting to extend a hand-written
        // comparison would silently treat the field as unchanged.
        private static readonly FieldInfo[] SnapshotFields =
            typeof(CharacterSnapshot).GetFields(BindingFlags.Public | BindingFlags.Instance);

        private static bool SnapshotsEqual(CharacterSnapshot a, CharacterSnapshot b)
        {
            if (a == null || b == null) return false;

            foreach (FieldInfo field in SnapshotFields)
            {
                object va = field.GetValue(a);
                object vb = field.GetValue(b);

                if (field.FieldType == typeof(string))
                {
                    string sa = (string)va ?? "";
                    string sb = (string)vb ?? "";
                    if (field.Name == "Profile")
                    {
                        sa = CharacterEditor.NormalizeProfileText(sa);
                        sb = CharacterEditor.NormalizeProfileText(sb);
                    }
                    if (!string.Equals(sa, sb, StringComparison.Ordinal)) return false;
                    continue;
                }

                if (!object.Equals(va, vb)) return false;
            }

            return true;
        }

        private void ApplySnapshot(CharacterSnapshot snapshot)
        {
            if (snapshot == null) snapshot = new CharacterSnapshot();
            firstNameBox.Text = snapshot.FirstName ?? "";
            lastNameBox.Text = snapshot.LastName ?? "";
            profileBox.Text = CharacterEditor.NormalizeProfileText(snapshot.Profile);
            SelectChoice(personalityCombo, snapshot.Personality);
            SelectChoice(clubCombo, snapshot.Club);
            SelectChoice(socialCombo, snapshot.Social);
            SelectChoice(fightingStyleCombo, snapshot.FightingStyle);
            SelectChoice(virtueCombo, snapshot.Virtue);
            SelectChoice(sexualOrientationCombo, snapshot.SexualOrientation);
            SelectChoice(voicePitchCombo, snapshot.VoicePitch);
            SelectChoice(intelligenceCombo, snapshot.IntelligenceLevel);
            SetNumberValue(academicLevelBox, snapshot.IntelligenceRank);
            SetNumberValue(academicValueBox, snapshot.IntelligenceValue);
            SelectChoice(academicGradeCombo, snapshot.IntelligenceGrade);
            SetNumberValue(academicRankBox, snapshot.IntelligenceTopRank);
            SelectChoice(strengthCombo, snapshot.StrengthLevel);
            SetNumberValue(strengthLevelBox, snapshot.StrengthRank);
            SetNumberValue(strengthValueBox, snapshot.StrengthValue);
            SelectChoice(strengthGradeCombo, snapshot.StrengthGrade);
            SetNumberValue(strengthRankBox, snapshot.StrengthTopRank);
            SetNumberValue(clubLevelBox, snapshot.ClubLevel);
            SetNumberValue(clubValueBox, snapshot.ClubValue);
            SelectChoice(clubGradeCombo, snapshot.ClubGrade);
            SetNumberValue(victoryBox, snapshot.VictoryCount);
            SetNumberValue(classSkipBox, snapshot.ClassSkip);
            SetNumberValue(winningOverBox, snapshot.WinningOver);
            SetNumberValue(partnerBox, snapshot.Partner);
            SetNumberValue(rejectedBox, snapshot.Rejected);
            SetNumberValue(sexIntercourseBox, snapshot.SexIntercourse);
            SetNumberValue(sexPartnerBox, snapshot.SexPartner);
            firstTimeBox.Text = snapshot.FirstTime ?? "";
            firstAnalBox.Text = snapshot.FirstAnal ?? "";
            lastTimeBox.Text = snapshot.LastTime ?? "";
            sexExperienceCheck.Checked = snapshot.SexExperience;
            analExperienceCheck.Checked = snapshot.AnalExperience;
            UpdateProfileBytes();
        }

        private void UpdateProfileBytes()
        {
            if (profileBox == null || profileBytesLabel == null) return;
            int bytes = CharacterEditor.GetProfileByteCount(profileBox.Text);
            profileBytesLabel.Text = bytes.ToString() + " / " +
                CharacterEditor.ProfileMaxBytes.ToString() + " bytes";
            profileBytesLabel.ForeColor = bytes > CharacterEditor.ProfileMaxBytes
                ? Color.Firebrick
                : Color.DimGray;
        }

        private void UpdateGate()
        {
            bool attached = mem != null && mem.IsAttached;
            bool hasCharacter = attached && character != null;
            bool writable = gateAllowed && hasCharacter && mem.CanWrite;

            if (applyButton != null) applyButton.Enabled = writable;
            if (revertButton != null) revertButton.Enabled = hasCharacter;

            if (firstNameBox != null) firstNameBox.Enabled = hasCharacter;
            if (lastNameBox != null) lastNameBox.Enabled = hasCharacter;
            if (profileBox != null) profileBox.Enabled = hasCharacter;
            if (personalityCombo != null) personalityCombo.Enabled = hasCharacter;
            if (clubCombo != null) clubCombo.Enabled = hasCharacter;
            if (socialCombo != null) socialCombo.Enabled = hasCharacter;
            if (fightingStyleCombo != null) fightingStyleCombo.Enabled = hasCharacter;
            if (virtueCombo != null) virtueCombo.Enabled = hasCharacter;
            if (sexualOrientationCombo != null) sexualOrientationCombo.Enabled = hasCharacter;
            if (voicePitchCombo != null) voicePitchCombo.Enabled = hasCharacter;
            if (intelligenceCombo != null) intelligenceCombo.Enabled = hasCharacter;
            if (academicLevelBox != null) academicLevelBox.Enabled = hasCharacter;
            if (academicValueBox != null) academicValueBox.Enabled = hasCharacter;
            if (academicGradeCombo != null) academicGradeCombo.Enabled = hasCharacter;
            if (academicRankBox != null) academicRankBox.Enabled = hasCharacter;
            if (strengthCombo != null) strengthCombo.Enabled = hasCharacter;
            if (strengthLevelBox != null) strengthLevelBox.Enabled = hasCharacter;
            if (strengthValueBox != null) strengthValueBox.Enabled = hasCharacter;
            if (strengthGradeCombo != null) strengthGradeCombo.Enabled = hasCharacter;
            if (strengthRankBox != null) strengthRankBox.Enabled = hasCharacter;
            if (clubLevelBox != null) clubLevelBox.Enabled = hasCharacter;
            if (clubValueBox != null) clubValueBox.Enabled = hasCharacter;
            if (clubGradeCombo != null) clubGradeCombo.Enabled = hasCharacter;
            if (victoryBox != null) victoryBox.Enabled = hasCharacter;
            if (classSkipBox != null) classSkipBox.Enabled = hasCharacter;
            if (winningOverBox != null) winningOverBox.Enabled = hasCharacter;
            if (partnerBox != null) partnerBox.Enabled = hasCharacter;
            if (rejectedBox != null) rejectedBox.Enabled = hasCharacter;
            if (sexIntercourseBox != null) sexIntercourseBox.Enabled = hasCharacter;
            if (sexPartnerBox != null) sexPartnerBox.Enabled = hasCharacter;
            if (firstTimeBox != null) firstTimeBox.Enabled = hasCharacter;
            if (firstAnalBox != null) firstAnalBox.Enabled = hasCharacter;
            if (lastTimeBox != null) lastTimeBox.Enabled = hasCharacter;
            if (sexExperienceCheck != null) sexExperienceCheck.Enabled = hasCharacter;
            if (analExperienceCheck != null) analExperienceCheck.Enabled = hasCharacter;

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
                    "All pointer checks passed. Changes are written directly to live game memory.");
            }
        }

        private static void SelectFirst(ComboBox combo)
        {
            if (combo != null && combo.Items.Count > 0)
                combo.SelectedIndex = 0;
        }

        private static void SetNumberValue(NumericUpDown box, int value)
        {
            if (box == null) return;
            int min = (int)box.Minimum;
            int max = (int)box.Maximum;
            if (value < min) value = min;
            if (value > max) value = max;
            box.Value = value;
        }

        private static void SetNumberIfChanged(NumericUpDown box, int value)
        {
            if (box == null) return;
            int min = (int)box.Minimum;
            int max = (int)box.Maximum;
            if (value < min) value = min;
            if (value > max) value = max;
            if (box.Value != value) box.Value = value;
        }

        private static void SetTextIfChanged(TextBox box, string text)
        {
            if (box == null) return;
            string value = text ?? "";
            if (box.Text != value) box.Text = value;
        }

        private static void SetCheckIfChanged(CheckBox box, bool value)
        {
            if (box == null) return;
            if (box.Checked != value) box.Checked = value;
        }

        private static void SetChoiceIfChanged(ComboBox combo, int value)
        {
            if (combo == null) return;
            if (SelectedValue(combo, int.MinValue) == value) return;
            SelectChoice(combo, value);
        }

        private static void SelectChoice(ComboBox combo, int value)
        {
            if (combo == null) return;
            if (combo.Items.Count == 0)
            {
                combo.SelectedIndex = -1;
                return;
            }

            for (int i = 0; i < combo.Items.Count; i++)
            {
                CharacterChoice choice = combo.Items[i] as CharacterChoice;
                if (choice != null && choice.Value == value)
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }

            CharacterChoice extra = new CharacterChoice(value, value.ToString() + "  (unknown)");
            combo.SelectedIndex = combo.Items.Add(extra);
        }

        private static int SelectedValue(ComboBox combo, int fallback)
        {
            if (combo == null) return fallback;
            CharacterChoice choice = combo.SelectedItem as CharacterChoice;
            return choice == null ? fallback : choice.Value;
        }

        private static void RebuildCombo(ComboBox combo, List<CharacterChoice> choices,
            int selectedValue)
        {
            if (combo == null) return;
            combo.BeginUpdate();
            try
            {
                combo.Items.Clear();
                foreach (CharacterChoice choice in choices)
                    combo.Items.Add(choice);
                SelectChoice(combo, selectedValue);
            }
            finally
            {
                combo.EndUpdate();
            }
        }

        private static string Signature(IList<CharacterChoice> choices)
        {
            if (choices == null) return "";
            StringBuilder builder = new StringBuilder();
            foreach (CharacterChoice choice in choices)
            {
                if (choice == null) continue;
                builder.Append(choice.Value).Append(':').Append(choice.Text).Append('|');
            }
            return builder.ToString();
        }

        private static List<CharacterChoice> CopyChoices(IList<CharacterChoice> choices)
        {
            List<CharacterChoice> copy = new List<CharacterChoice>();
            if (choices != null)
            {
                foreach (CharacterChoice choice in choices)
                {
                    if (choice != null)
                        copy.Add(new CharacterChoice(choice.Value, choice.Text));
                }
            }
            return copy;
        }

        private void SetStatus(string text, bool error)
        {
            if (statusLabel == null) return;
            statusLabel.Text = text ?? "";
            statusLabel.ForeColor = error ? Color.Firebrick : Color.DimGray;
        }

        /// <summary>
        /// The default DataGridView wheel scroll moves three rows per notch,
        /// which skips modules when descriptions make the rows tall. Move one
        /// row at a time so every module is readable.
        /// </summary>
        private sealed class ModuleGrid : DataGridView
        {
            protected override void OnMouseWheel(MouseEventArgs e)
            {
                if (Rows.Count == 0 || e.Delta == 0) return;

                int current = FirstDisplayedScrollingRowIndex;
                if (current < 0) current = 0;

                int notches = Math.Max(1, Math.Abs(e.Delta) / 120);
                int next = e.Delta > 0 ? current - notches : current + notches;
                if (next < 0) next = 0;
                if (next >= Rows.Count) next = Rows.Count - 1;

                if (next != current)
                    FirstDisplayedScrollingRowIndex = next;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (tooltip != null) tooltip.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
