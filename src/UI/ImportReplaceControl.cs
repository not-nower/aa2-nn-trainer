using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AA2Trainer
{
    /// <summary>
    /// Offline Import/Replace tab. Edits a class save file directly, so it does
    /// not require the game to be attached. Play data is preserved.
    /// </summary>
    public sealed class ImportReplaceControl : UserControl
    {
        private static readonly string[] SectionNames =
        {
            "Profile",
            "Appearance",
            "Traits",
            "H Preferences",
            "Risk/Pregnancy",
            "Outfit - Uniform",
            "Outfit - Sports",
            "Outfit - Swimsuit",
            "Outfit - Club",
            "Card Face",
            "Roster Image",
            "AAUnlimited Data"
        };

        private static readonly string[] SectionLabels =
        {
            "Profile",
            "Appearance: Body/Face/Hair",
            "Traits",
            "H Preferences",
            "Risk/Pregnancy",
            "Outfit - Uniform",
            "Outfit - Sports",
            "Outfit - Swimsuit",
            "Outfit - Club",
            "Card Face",
            "Roster Image",
            "AAUnlimited Data"
        };

        private readonly NameMap names = new NameMap();
        private readonly List<CheckBox> sectionChecks = new List<CheckBox>();

        private ClassSaveFile save;
        private CardSnapshot sourceCard;
        private SaveCardEntry selectedEntry;
        private string lastSaveDirectory;
        private string lastCardDirectory;
        private bool suppressCompare;

        private TextBox savePathBox;
        private TextBox cardPathBox;
        private ComboBox characterCombo;
        private Label statusLabel;
        private CheckBox showUnchanged;
        private DataGridView diffGrid;
        private Button applyButton;
        private PictureBox currentFaceBox;
        private PictureBox incomingFaceBox;
        private Label currentFaceLabel;
        private Label incomingFaceLabel;

        public ImportReplaceControl()
        {
            DoubleBuffered = true;
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(248, 249, 251);
            Padding = new Padding(16, 8, 16, 8);

            try
            {
                string root = NameMap.FindGameRoot();
                if (!string.IsNullOrEmpty(root)) names.Load(root);
            }
            catch { }

            BuildUi();
        }

        private void BuildUi()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 4;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
            root.Margin = new Padding(0);
            Controls.Add(root);

            Label note = new Label();
            note.AutoSize = true;
            note.MaximumSize = new Size(1100, 0);
            note.Font = new Font("Segoe UI", 8.5F);
            note.ForeColor = Color.DimGray;
            note.Margin = new Padding(0, 0, 0, 8);
            note.Text =
                "Offline save editor. In AA2Play, save the game first, then select that .sav here. " +
                "Select the character from that save, choose a card to import, pick the sections, " +
                "review the comparison, then Apply. The comparison lists only the changes in the " +
                "selected sections; check Show unchanged to see every field. " +
                "All selected sections, including Card Face, Roster Image, and AAUnlimited Data, are " +
                "written to the save and take effect after reloading the class. " +
                "A timestamped .bak backup is created. Do not save in-game between Apply and reloading.";
            root.Controls.Add(note, 0, 0);

            TableLayoutPanel selectors = new TableLayoutPanel();
            selectors.Dock = DockStyle.Top;
            selectors.AutoSize = true;
            selectors.ColumnCount = 3;
            selectors.RowCount = 3;
            selectors.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
            selectors.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            selectors.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            for (int i = 0; i < 3; i++)
                selectors.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            selectors.Margin = new Padding(0, 0, 0, 8);
            root.Controls.Add(selectors, 0, 1);

            selectors.Controls.Add(MakeCaption("Save:"), 0, 0);
            savePathBox = MakePathBox();
            selectors.Controls.Add(savePathBox, 1, 0);
            Button selectSave = MakeButton("Select Save File");
            selectSave.Click += delegate { SelectSaveFile(); };
            selectors.Controls.Add(selectSave, 2, 0);

            selectors.Controls.Add(MakeCaption("Character From Save:"), 0, 1);
            characterCombo = new ComboBox();
            characterCombo.Dock = DockStyle.Fill;
            characterCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            characterCombo.Margin = new Padding(0, 4, 4, 4);
            characterCombo.SelectedIndexChanged += delegate { CharacterChanged(); };
            selectors.Controls.Add(characterCombo, 1, 1);

            selectors.Controls.Add(MakeCaption("Card to import:"), 0, 2);
            cardPathBox = MakePathBox();
            selectors.Controls.Add(cardPathBox, 1, 2);
            Button selectCard = MakeButton("Select Card to Import and Replace");
            selectCard.Click += delegate { SelectCardFile(); };
            selectors.Controls.Add(selectCard, 2, 2);

            TableLayoutPanel content = new TableLayoutPanel();
            content.Dock = DockStyle.Fill;
            content.ColumnCount = 2;
            content.RowCount = 2;
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 330F));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 64F));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 36F));
            content.Margin = new Padding(0);
            root.Controls.Add(content, 0, 2);

            GroupBox replaceGroup = new GroupBox();
            replaceGroup.Text = "Replace";
            replaceGroup.Dock = DockStyle.Fill;
            replaceGroup.Padding = new Padding(10, 4, 10, 4);
            replaceGroup.Margin = new Padding(0, 0, 6, 0);
            content.Controls.Add(replaceGroup, 0, 0);

            Panel replaceScroll = new Panel();
            replaceScroll.Dock = DockStyle.Fill;
            replaceScroll.AutoScroll = true;
            replaceScroll.Margin = new Padding(0);
            replaceGroup.Controls.Add(replaceScroll);

            TableLayoutPanel replaceTable = new TableLayoutPanel();
            replaceTable.Dock = DockStyle.Top;
            replaceTable.AutoSize = true;
            replaceTable.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            replaceTable.ColumnCount = 1;
            replaceTable.RowCount = SectionNames.Length + 2;
            replaceTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            replaceTable.Margin = new Padding(0);
            for (int i = 0; i < SectionNames.Length; i++)
                replaceTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
            replaceTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
            replaceTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            replaceScroll.Controls.Add(replaceTable);

            for (int i = 0; i < SectionNames.Length; i++)
            {
                CheckBox box = new CheckBox();
                box.Text = SectionLabels[i];
                box.Tag = SectionNames[i];
                box.AutoSize = true;
                box.Checked = false;
                box.Margin = new Padding(0, 1, 0, 1);
                box.CheckedChanged += delegate
                {
                    if (!suppressCompare) UpdateComparison();
                };
                sectionChecks.Add(box);
                replaceTable.Controls.Add(box, 0, i);
            }

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.FlowDirection = FlowDirection.LeftToRight;
            buttons.WrapContents = false;
            buttons.Margin = new Padding(0, 4, 0, 0);
            Button selectAll = MakeButton("Select All");
            selectAll.Click += delegate { SetAllSections(true); };
            buttons.Controls.Add(selectAll);
            Button selectNone = MakeButton("Select None");
            selectNone.Click += delegate { SetAllSections(false); };
            buttons.Controls.Add(selectNone);
            replaceTable.Controls.Add(buttons, 0, SectionNames.Length);

            applyButton = MakeButton("Replace/Apply");
            applyButton.Dock = DockStyle.Top;
            applyButton.Height = 30;
            applyButton.Margin = new Padding(0, 2, 0, 0);
            applyButton.Enabled = false;
            applyButton.Click += delegate { ApplyChanges(); };
            replaceTable.Controls.Add(applyButton, 0, SectionNames.Length + 1);

            GroupBox compareGroup = new GroupBox();
            compareGroup.Text = "Comparison";
            compareGroup.Dock = DockStyle.Fill;
            compareGroup.Padding = new Padding(10, 4, 10, 4);
            compareGroup.Margin = new Padding(6, 0, 0, 0);
            content.Controls.Add(compareGroup, 1, 0);
            content.SetRowSpan(compareGroup, 2);

            TableLayoutPanel compareTable = new TableLayoutPanel();
            compareTable.Dock = DockStyle.Fill;
            compareTable.ColumnCount = 1;
            compareTable.RowCount = 2;
            compareTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            compareTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));
            compareTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            compareTable.Margin = new Padding(0);
            compareGroup.Controls.Add(compareTable);

            showUnchanged = new CheckBox();
            showUnchanged.Text = "Show unchanged";
            showUnchanged.AutoSize = true;
            showUnchanged.Margin = new Padding(0, 2, 0, 2);
            showUnchanged.CheckedChanged += delegate { UpdateComparison(); };
            compareTable.Controls.Add(showUnchanged, 0, 0);

            diffGrid = new DataGridView();
            diffGrid.Dock = DockStyle.Fill;
            diffGrid.BackgroundColor = Color.White;
            diffGrid.BorderStyle = BorderStyle.FixedSingle;
            diffGrid.RowHeadersVisible = false;
            diffGrid.AllowUserToAddRows = false;
            diffGrid.AllowUserToDeleteRows = false;
            diffGrid.AllowUserToResizeRows = false;
            diffGrid.ReadOnly = true;
            diffGrid.MultiSelect = false;
            diffGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            diffGrid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.DisplayedCells;
            diffGrid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            diffGrid.ColumnHeadersHeight = 26;
            diffGrid.ColumnHeadersHeightSizeMode =
                DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            diffGrid.EnableHeadersVisualStyles = false;
            diffGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(240, 241, 244);
            diffGrid.ColumnHeadersDefaultCellStyle.SelectionBackColor =
                Color.FromArgb(240, 241, 244);
            diffGrid.ColumnHeadersDefaultCellStyle.SelectionForeColor =
                Color.FromArgb(30, 30, 30);
            diffGrid.ColumnHeadersDefaultCellStyle.Font =
                new Font("Segoe UI", 8.5F, FontStyle.Bold);
            diffGrid.DefaultCellStyle.Font = new Font("Segoe UI", 9F);
            diffGrid.DefaultCellStyle.SelectionBackColor = SystemColors.Highlight;
            diffGrid.ClipboardCopyMode =
                DataGridViewClipboardCopyMode.EnableWithoutHeaderText;
            diffGrid.Margin = new Padding(0);

            AddColumn(diffGrid, "Section", 18F);
            AddColumn(diffGrid, "Field", 22F);
            AddColumn(diffGrid, "Current", 30F);
            AddColumn(diffGrid, "Incoming", 30F);
            compareTable.Controls.Add(diffGrid, 0, 1);

            GroupBox facesGroup = new GroupBox();
            facesGroup.Text = "Card Faces";
            facesGroup.Dock = DockStyle.Fill;
            facesGroup.Padding = new Padding(8, 4, 8, 6);
            facesGroup.Margin = new Padding(0, 6, 6, 0);
            content.Controls.Add(facesGroup, 0, 1);

            TableLayoutPanel facesTable = new TableLayoutPanel();
            facesTable.Dock = DockStyle.Fill;
            facesTable.ColumnCount = 2;
            facesTable.RowCount = 2;
            facesTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            facesTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            facesTable.RowStyles.Add(new RowStyle(SizeType.Absolute, 18F));
            facesTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            facesTable.Margin = new Padding(0);
            facesGroup.Controls.Add(facesTable);

            currentFaceLabel = MakeFaceCaption("In save: (none)");
            facesTable.Controls.Add(currentFaceLabel, 0, 0);
            currentFaceBox = MakeFaceBox();
            facesTable.Controls.Add(currentFaceBox, 0, 1);

            incomingFaceLabel = MakeFaceCaption("Card to import: (none)");
            facesTable.Controls.Add(incomingFaceLabel, 1, 0);
            incomingFaceBox = MakeFaceBox();
            facesTable.Controls.Add(incomingFaceBox, 1, 1);

            statusLabel = new Label();
            statusLabel.Dock = DockStyle.Fill;
            statusLabel.AutoSize = false;
            statusLabel.AutoEllipsis = true;
            statusLabel.TextAlign = ContentAlignment.TopLeft;
            statusLabel.ForeColor = Color.DimGray;
            statusLabel.Margin = new Padding(0);
            statusLabel.Text = "Select a save file, a character from that save, and a card to import.";
            root.Controls.Add(statusLabel, 0, 3);
        }

        private static Label MakeCaption(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.ForeColor = Color.DimGray;
            label.Margin = new Padding(0);
            return label;
        }

        private static Label MakeFaceCaption(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.Dock = DockStyle.Fill;
            label.AutoEllipsis = true;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Font = new Font("Segoe UI", 8.25F);
            label.ForeColor = Color.DimGray;
            label.Margin = new Padding(2, 0, 2, 0);
            return label;
        }

        private static PictureBox MakeFaceBox()
        {
            PictureBox box = new PictureBox();
            box.Dock = DockStyle.Fill;
            box.SizeMode = PictureBoxSizeMode.Zoom;
            box.BorderStyle = BorderStyle.FixedSingle;
            box.BackColor = Color.White;
            box.Margin = new Padding(2, 0, 2, 0);
            return box;
        }

        private static TextBox MakePathBox()
        {
            TextBox box = new TextBox();
            box.Dock = DockStyle.Fill;
            box.ReadOnly = true;
            box.Margin = new Padding(0, 4, 4, 4);
            return box;
        }

        private static Button MakeButton(string text)
        {
            Button button = new Button();
            button.Text = text;
            button.AutoSize = true;
            button.UseVisualStyleBackColor = true;
            button.Margin = new Padding(4, 3, 0, 3);
            return button;
        }

        private static void AddColumn(DataGridView grid, string header, float weight)
        {
            DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn();
            column.HeaderText = header;
            column.FillWeight = weight;
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            column.ReadOnly = true;
            column.SortMode = DataGridViewColumnSortMode.NotSortable;
            grid.Columns.Add(column);
        }

        private void SetAllSections(bool selected)
        {
            suppressCompare = true;
            try
            {
                foreach (CheckBox box in sectionChecks) box.Checked = selected;
            }
            finally
            {
                suppressCompare = false;
            }
            UpdateComparison();
        }

        private List<string> SelectedSections()
        {
            List<string> result = new List<string>();
            foreach (CheckBox box in sectionChecks)
            {
                if (box.Checked) result.Add((string)box.Tag);
            }
            return result;
        }

        private void SelectSaveFile()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Select an AA2 class save";
                dialog.Filter = "AA2 class saves (*.sav)|*.sav|All files (*.*)|*.*";
                dialog.InitialDirectory = string.IsNullOrEmpty(lastSaveDirectory)
                    ? DefaultSaveDirectory() : lastSaveDirectory;
                if (dialog.ShowDialog() != DialogResult.OK) return;

                try
                {
                    LoadSave(dialog.FileName);
                    lastSaveDirectory = Path.GetDirectoryName(dialog.FileName);
                    SetStatus("Save loaded. Select a character from that save and a card to import.", false);
                }
                catch (Exception ex)
                {
                    save = null;
                    selectedEntry = null;
                    characterCombo.Items.Clear();
                    UpdateFaces();
                    UpdateComparison();
                    SetStatus(ex.Message, true);
                }
            }
        }

        private void LoadSave(string path)
        {
            ClassSaveFile loaded = ClassSaveFile.Load(path);
            save = loaded;
            savePathBox.Text = path;

            int pcSeat = save.PcSeat;

            characterCombo.BeginUpdate();
            try
            {
                characterCombo.Items.Clear();
                int selected = 0;
                for (int i = 0; i < save.Cards.Count; i++)
                {
                    CharacterItem item = new CharacterItem(save.Cards[i]);
                    characterCombo.Items.Add(item);
                    if (save.Cards[i].Seat == pcSeat) selected = i;
                }
                if (characterCombo.Items.Count > 0)
                    characterCombo.SelectedIndex = selected;
            }
            finally
            {
                characterCombo.EndUpdate();
            }
            UpdateFaces();
        }

        private void SelectCardFile()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "Select an AA2 card to import";
                dialog.Filter = "AA2 cards (*.png)|*.png|All files (*.*)|*.*";
                dialog.InitialDirectory = string.IsNullOrEmpty(lastCardDirectory)
                    ? DefaultCardDirectory() : lastCardDirectory;
                if (dialog.ShowDialog() != DialogResult.OK) return;

                try
                {
                    sourceCard = CardSnapshot.ParseStandalone(dialog.FileName);
                    cardPathBox.Text = dialog.FileName;
                    lastCardDirectory = Path.GetDirectoryName(dialog.FileName);
                    UpdateFaces();
                    UpdateComparison();
                    SetStatus("Card loaded: " + sourceCard.Name, false);
                }
                catch (Exception ex)
                {
                    sourceCard = null;
                    UpdateFaces();
                    UpdateComparison();
                    SetStatus(ex.Message, true);
                }
            }
        }

        private void CharacterChanged()
        {
            CharacterItem item = characterCombo.SelectedItem as CharacterItem;
            selectedEntry = item == null ? null : item.Entry;
            UpdateFaces();
            UpdateComparison();
        }

        private void UpdateFaces()
        {
            if (currentFaceBox == null || incomingFaceBox == null) return;

            if (selectedEntry == null)
            {
                SetFace(currentFaceBox, currentFaceLabel, null, "In save: (none)");
            }
            else
            {
                string name = selectedEntry.Card.Name;
                SetFace(currentFaceBox, currentFaceLabel, selectedEntry.Card.FacePng,
                    "In save: " + selectedEntry.Seat.ToString("D2") + " " + name);
            }

            if (sourceCard == null)
            {
                SetFace(incomingFaceBox, incomingFaceLabel, null,
                    "Card to import: (none)");
            }
            else
            {
                SetFace(incomingFaceBox, incomingFaceLabel, sourceCard.FacePng,
                    "Card to import: " + sourceCard.Name);
            }
        }

        private static void SetFace(PictureBox box, Label caption, byte[] png,
            string text)
        {
            Image previous = box.Image;
            box.Image = LoadCardImage(png);
            if (previous != null) previous.Dispose();
            if (caption != null) caption.Text = text;
        }

        private static Image LoadCardImage(byte[] png)
        {
            if (png == null || png.Length < 12) return null;
            try
            {
                using (MemoryStream stream = new MemoryStream(png))
                using (Image image = Image.FromStream(stream))
                {
                    return new Bitmap(image);
                }
            }
            catch
            {
                return null;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (currentFaceBox != null && currentFaceBox.Image != null)
                    currentFaceBox.Image.Dispose();
                if (incomingFaceBox != null && incomingFaceBox.Image != null)
                    incomingFaceBox.Image.Dispose();
            }
            base.Dispose(disposing);
        }

        private void UpdateComparison()
        {
            diffGrid.SuspendLayout();
            try
            {
                diffGrid.Rows.Clear();
                if (save == null || sourceCard == null || selectedEntry == null)
                {
                    statusLabel.Text =
                        "Select a save file, a character from that save, and a card to import.";
                    statusLabel.ForeColor = Color.DimGray;
                    return;
                }

                List<string> sectionList = SelectedSections();
                HashSet<string> applies =
                    new HashSet<string>(sectionList, StringComparer.Ordinal);
                bool showAll = showUnchanged.Checked;

                CardSnapshot target = selectedEntry.Card;
                int gender = target.Gender;
                int changes = 0;

                foreach (CardFieldDef field in CardFields.All)
                {
                    if (!CardFields.MatchesGender(field, gender)) continue;
                    bool same = CardFields.Same(target.EditData, sourceCard.EditData, field);
                    bool appliesHere = applies.Contains(field.Section);
                    if (!same && appliesHere) changes++;

                    // The view shows the pending change itself. Unchanged fields
                    // and fields outside the selected sections only appear when
                    // Show unchanged is checked.
                    if (!showAll && (same || !appliesHere)) continue;

                    int row = diffGrid.Rows.Add();
                    DataGridViewRow gridRow = diffGrid.Rows[row];
                    gridRow.Cells[0].Value = field.Section;
                    gridRow.Cells[1].Value = field.Label;
                    gridRow.Cells[2].Value = CardFields.Format(
                        target.EditData, field, names);
                    gridRow.Cells[3].Value = CardFields.Format(
                        sourceCard.EditData, field, names);
                    Color color = !appliesHere ? Color.Silver
                        : same ? Color.DimGray : Color.Firebrick;
                    gridRow.Cells[3].Style.ForeColor = color;
                    if (!appliesHere) gridRow.Cells[1].Style.ForeColor = Color.Silver;
                }

                changes += AddSpecialRow("Images", "Card Face", "Card Face",
                    target.FacePng, sourceCard.FacePng, applies);
                changes += AddSpecialRow("Images", "Roster Image", "Roster Image",
                    target.RosterPng, sourceCard.RosterPng, applies);
                changes += AddSpecialRow("AAUnlimited Data", "AAU chunk",
                    "AAUnlimited Data", target.AauChunk, sourceCard.AauChunk, applies);

                if (sectionList.Count == 0)
                    statusLabel.Text =
                        "Select at least one section to see what will change.";
                else if (changes == 0)
                    statusLabel.Text = "No changes in the selected sections.";
                else
                    statusLabel.Text = changes + (changes == 1 ? " change" : " changes") +
                        " will be applied.";
                if (showAll)
                    statusLabel.Text += "  Showing all fields; dimmed rows are unchanged" +
                        " or not selected.";
                statusLabel.ForeColor = Color.DimGray;
            }
            finally
            {
                diffGrid.ResumeLayout();
                UpdateApplyEnabled();
            }
        }

        private int AddSpecialRow(string displaySection, string field,
            string appliesTo, byte[] current, byte[] incoming, HashSet<string> applies)
        {
            bool same = BytesEqual(current, incoming);
            bool appliesHere = applies.Contains(appliesTo);
            bool changed = !same && appliesHere;
            if (!showUnchanged.Checked && !changed) return 0;

            int row = diffGrid.Rows.Add();
            DataGridViewRow gridRow = diffGrid.Rows[row];
            gridRow.Cells[0].Value = displaySection;
            gridRow.Cells[1].Value = field;
            gridRow.Cells[2].Value = ByteText(current);
            gridRow.Cells[3].Value = ByteText(incoming);
            Color color = !appliesHere ? Color.Silver
                : same ? Color.DimGray : Color.Firebrick;
            gridRow.Cells[3].Style.ForeColor = color;
            if (!appliesHere) gridRow.Cells[1].Style.ForeColor = Color.Silver;
            return changed ? 1 : 0;
        }

        private static string ByteText(byte[] data)
        {
            return data == null || data.Length == 0
                ? "none" : "present (" + data.Length + " bytes)";
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a == null || b == null) return a == b;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }

        private void UpdateApplyEnabled()
        {
            if (applyButton == null) return;
            applyButton.Enabled = save != null && sourceCard != null &&
                selectedEntry != null && SelectedSections().Count > 0;
        }

        private void ApplyChanges()
        {
            if (save == null || sourceCard == null || selectedEntry == null)
            {
                SetStatus("Select a save, a character from it, and a card first.", true);
                return;
            }

            List<string> sections = SelectedSections();
            if (sections.Count == 0)
            {
                SetStatus("Select at least one section to replace.", true);
                return;
            }

            string sectionText = string.Join(", ", sections.ToArray());
            string question =
                "Replace the selected sections?\n\n" +
                "Save: " + save.Path + "\n" +
                "Character: " + selectedEntry.Seat.ToString("D2") + " " +
                selectedEntry.Card.Name + "\n" +
                "Card: " + sourceCard.SourcePath + "\n" +
                "Sections: " + sectionText + "\n\n" +
                "A timestamped .bak backup will be created. " +
                "Reload the class in AA2Play afterward.";
            if (MessageBox.Show(question, "Replace Character Data",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            SaveCardEntry entry = selectedEntry;
            CardSnapshot target = entry.Card;
            try
            {
                foreach (string section in sections)
                {
                    if (section == "Card Face")
                        target.FacePng = sourceCard.FacePng;
                    else if (section == "Roster Image")
                        target.RosterPng = sourceCard.RosterPng;
                    else if (section == "AAUnlimited Data")
                        target.AauChunk = sourceCard.AauChunk;
                    else
                        CardFields.CopySection(
                            sourceCard.EditData, target.EditData, section);
                }

                target.RefreshIdentity();
                if (target.Gender >= 0 && target.Gender <= 1)
                    entry.Gender = (byte)target.Gender;
                entry.Dirty = true;

                int seat = entry.Seat;
                string backup = save.Save();
                LoadSave(save.Path);
                SelectSeat(seat);
                SetStatus("Saved. Backup: " + backup +
                    "  Reload the class in AA2Play.", false);
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
            }
        }

        private void SelectSeat(int seat)
        {
            for (int i = 0; i < characterCombo.Items.Count; i++)
            {
                CharacterItem item = characterCombo.Items[i] as CharacterItem;
                if (item != null && item.Entry.Seat == seat)
                {
                    characterCombo.SelectedIndex = i;
                    return;
                }
            }
        }

        private void SetStatus(string text, bool error)
        {
            statusLabel.Text = text ?? "";
            statusLabel.ForeColor = error ? Color.Firebrick : Color.DimGray;
        }

        private static string DefaultSaveDirectory()
        {
            string root = NameMap.FindGameRoot();
            if (string.IsNullOrEmpty(root)) return "";
            return Path.Combine(Path.Combine(Path.Combine(root, "data"), "save"), "class");
        }

        private static string DefaultCardDirectory()
        {
            string root = NameMap.FindGameRoot();
            if (string.IsNullOrEmpty(root)) return "";
            return Path.Combine(Path.Combine(root, "data"), "save");
        }

        private sealed class CharacterItem
        {
            public readonly SaveCardEntry Entry;

            public CharacterItem(SaveCardEntry entry)
            {
                Entry = entry;
            }

            public override string ToString()
            {
                return Entry.Seat.ToString("D2") + " - " + Entry.Card.Name;
            }
        }
    }
}
