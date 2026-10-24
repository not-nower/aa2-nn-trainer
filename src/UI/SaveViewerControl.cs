using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace AA2Trainer
{
    public sealed class SaveViewerControl : UserControl
    {
        private sealed class SaveFileItem
        {
            public string Path;
            public string Text;

            public override string ToString()
            {
                return Text;
            }
        }

        private readonly NameMap names;
        private readonly Font seatBoldFont;
        private ComboBox saveCombo;
        private Button openButton;
        private Button reloadButton;
        private Label pathLabel;
        private Label headerLabel;
        private SplitContainer viewSplit;
        private ListView seatList;
        private DataGridView relationGrid;
        private DataGridView outGrid;
        private DataGridView inGrid;
        private TextBox statusText;

        private SaveClassData save;
        private Dictionary<int, string> liveNames = new Dictionary<int, string>();
        private string gameRoot;
        private int selectedSeat = -1;
        private bool loadingCombo;
        private bool suppressSeat;
        private DateTime lastLoadWriteTime;

        public SaveViewerControl(NameMap names)
        {
            this.names = names;
            seatBoldFont = new Font("Segoe UI", 9F, FontStyle.Bold);
            DoubleBuffered = true;
            BackColor = Color.FromArgb(248, 249, 251);
            Padding = new Padding(6);
            BuildUi();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                if (seatBoldFont != null) seatBoldFont.Dispose();
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            PositionSplit();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            PositionSplit();
        }

        private void PositionSplit()
        {
            if (viewSplit == null) return;
            if (viewSplit.Width < 640) return;
            int desired = 270;
            int max = viewSplit.Width - 420;
            if (max < 220) max = 220;
            if (desired > max) desired = max;
            if (desired < 220) desired = 220;
            try
            {
                if (viewSplit.SplitterDistance != desired)
                    viewSplit.SplitterDistance = desired;
            }
            catch { }
        }

        public SaveClassData CurrentSave
        {
            get { return save; }
        }

        public bool RefreshIfChanged()
        {
            if (save == null || string.IsNullOrEmpty(save.Path) || !File.Exists(save.Path))
                return false;
            DateTime writeTime = File.GetLastWriteTimeUtc(save.Path);
            if (writeTime <= lastLoadWriteTime) return false;
            LoadSave(save.Path);
            return true;
        }

        public void SetGameRoot(string root)
        {
            if (string.Equals(gameRoot, root, StringComparison.OrdinalIgnoreCase)) return;
            gameRoot = root;
            PopulateSaveCombo(true);
        }

        public void SetLiveCharacters(IEnumerable<CharacterInfo> characters)
        {
            Dictionary<int, string> next = new Dictionary<int, string>();
            if (characters != null)
            {
                foreach (CharacterInfo info in characters)
                {
                    if (info == null || info.Seat < 0 || info.Seat > 24) continue;
                    next[info.Seat] = string.IsNullOrEmpty(info.Name) ? "Seat " + info.Seat : info.Name;
                }
            }

            bool changed = next.Count != liveNames.Count;
            if (!changed)
            {
                foreach (KeyValuePair<int, string> pair in next)
                {
                    string old;
                    if (!liveNames.TryGetValue(pair.Key, out old) || !string.Equals(old, pair.Value, StringComparison.Ordinal))
                    {
                        changed = true;
                        break;
                    }
                }
            }
            if (!changed) return;

            liveNames = next;
            if (save != null)
            {
                int keep = selectedSeat;
                PopulateSeatList(keep);
            }
        }

        public void SetSelectedSeat(int seat)
        {
            selectedSeat = seat;
            if (save == null) return;
            SelectSeatItem(seat);
        }

        public void Reload()
        {
            SaveFileItem selected = saveCombo == null ? null : saveCombo.SelectedItem as SaveFileItem;
            string path = save != null ? save.Path : (selected == null ? null : selected.Path);
            PopulateSaveCombo(false);
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                LoadSave(path);
                return;
            }
            SaveFileItem first = saveCombo == null ? null : saveCombo.SelectedItem as SaveFileItem;
            if (first != null) LoadSave(first.Path);
        }

        private void BuildUi()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 3;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 74F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.Padding = new Padding(0);

            FlowLayoutPanel bar = new FlowLayoutPanel();
            bar.Dock = DockStyle.Fill;
            bar.FlowDirection = FlowDirection.LeftToRight;
            bar.WrapContents = false;
            bar.Padding = new Padding(2, 4, 2, 2);
            bar.BackColor = Color.FromArgb(245, 246, 248);

            Label caption = new Label();
            caption.Text = "Save file:";
            caption.AutoSize = true;
            caption.Margin = new Padding(4, 7, 2, 0);
            caption.ForeColor = Color.DimGray;
            bar.Controls.Add(caption);

            saveCombo = new ComboBox();
            saveCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            saveCombo.Width = 340;
            saveCombo.Margin = new Padding(2, 3, 6, 0);
            saveCombo.SelectedIndexChanged += delegate
            {
                if (loadingCombo) return;
                SaveFileItem item = saveCombo.SelectedItem as SaveFileItem;
                if (item != null) LoadSave(item.Path);
            };
            bar.Controls.Add(saveCombo);

            openButton = new Button();
            openButton.Text = "Open...";
            openButton.Width = 76;
            openButton.Margin = new Padding(2, 1, 2, 0);
            openButton.Click += delegate { OpenSaveDialog(); };
            bar.Controls.Add(openButton);

            reloadButton = new Button();
            reloadButton.Text = "Reload";
            reloadButton.Width = 76;
            reloadButton.Margin = new Padding(2, 1, 2, 0);
            reloadButton.Click += delegate { Reload(); };
            bar.Controls.Add(reloadButton);

            pathLabel = new Label();
            pathLabel.AutoSize = true;
            pathLabel.ForeColor = Color.DimGray;
            pathLabel.Margin = new Padding(10, 7, 2, 0);
            pathLabel.Text = "Read-only snapshot";
            bar.Controls.Add(pathLabel);

            root.Controls.Add(bar, 0, 0);

            GroupBox headerBox = new GroupBox();
            headerBox.Text = "Class save";
            headerBox.Dock = DockStyle.Fill;
            headerBox.Padding = new Padding(8, 4, 8, 4);
            headerBox.Margin = new Padding(2, 2, 2, 4);

            headerLabel = new Label();
            headerLabel.Dock = DockStyle.Fill;
            headerLabel.AutoSize = false;
            headerLabel.TextAlign = ContentAlignment.MiddleLeft;
            headerLabel.Font = new Font("Consolas", 8.5F);
            headerLabel.ForeColor = Color.FromArgb(40, 40, 40);
            headerLabel.Text = "No save loaded. Choose a .sav file or press Reload.";
            headerBox.Controls.Add(headerLabel);
            root.Controls.Add(headerBox, 0, 1);

            SplitContainer split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.Orientation = Orientation.Vertical;
            split.SplitterDistance = 250;
            split.Panel1MinSize = 190;
            split.Panel2MinSize = 0;
            split.SplitterWidth = 5;
            viewSplit = split;

            seatList = new ListView();
            seatList.Dock = DockStyle.Fill;
            seatList.View = View.Details;
            seatList.FullRowSelect = true;
            seatList.HideSelection = false;
            seatList.MultiSelect = false;
            seatList.GridLines = false;
            seatList.Columns.Add("Seat", 42);
            seatList.Columns.Add("Name", 210);
            seatList.SelectedIndexChanged += delegate
            {
                if (suppressSeat) return;
                if (seatList.SelectedItems.Count == 0) return;
                object tag = seatList.SelectedItems[0].Tag;
                if (tag is int)
                {
                    selectedSeat = (int)tag;
                    PopulateDetails(selectedSeat);
                }
            };
            split.Panel1.Controls.Add(seatList);

            TabControl tabs = new TabControl();
            tabs.Dock = DockStyle.Fill;

            TabPage relationships = new TabPage("Relationships");
            relationGrid = MakeGrid(
                new string[] { "Target", "Love", "Like", "Dislike", "Hate", "History" },
                new int[] { 74, 160, 160, 120, 120, 64 });
            relationships.Controls.Add(relationGrid);
            tabs.TabPages.Add(relationships);

            TabPage outPage = new TabPage("Interactions Out");
            outGrid = MakeGrid(
                new string[] { "Target", "Key", "Action", "Counter", "Success", "Failure", "HUH" },
                new int[] { 74, 46, 210, 62, 62, 62, 50 });
            outPage.Controls.Add(outGrid);
            tabs.TabPages.Add(outPage);

            TabPage inPage = new TabPage("Interactions In");
            inGrid = MakeGrid(
                new string[] { "Target", "Key", "Action", "Counter", "Success", "Failure", "HUH" },
                new int[] { 74, 46, 210, 62, 62, 62, 50 });
            inPage.Controls.Add(inGrid);
            tabs.TabPages.Add(inPage);

            TabPage misc = new TabPage("Status / Misc");
            statusText = new TextBox();
            statusText.Dock = DockStyle.Fill;
            statusText.Multiline = true;
            statusText.ReadOnly = true;
            statusText.ScrollBars = ScrollBars.Both;
            statusText.WordWrap = false;
            statusText.Font = new Font("Consolas", 8.5F);
            statusText.BackColor = Color.White;
            statusText.Text = "No save loaded.";
            misc.Controls.Add(statusText);
            tabs.TabPages.Add(misc);

            split.Panel2.Controls.Add(tabs);
            root.Controls.Add(split, 0, 2);
            Controls.Add(root);
        }

        private static DataGridView MakeGrid(string[] headers, int[] widths)
        {
            DataGridView grid = new DataGridView();
            grid.Dock = DockStyle.Fill;
            grid.BackgroundColor = Color.White;
            grid.BorderStyle = BorderStyle.FixedSingle;
            grid.RowHeadersVisible = false;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.ReadOnly = true;
            grid.MultiSelect = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.AutoGenerateColumns = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            grid.RowTemplate.Height = 19;
            grid.ColumnHeadersHeight = 24;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(240, 240, 240);
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            grid.DefaultCellStyle.Font = new Font("Consolas", 8.5F);
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;

            for (int i = 0; i < headers.Length; i++)
            {
                DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn();
                column.HeaderText = headers[i];
                column.Width = widths[i];
                column.SortMode = DataGridViewColumnSortMode.Automatic;
                grid.Columns.Add(column);
            }
            return grid;
        }

        private void PopulateSaveCombo(bool autoLoad)
        {
            if (saveCombo == null) return;

            string currentPath = save == null ? null : save.Path;
            SaveFileItem[] items = new SaveFileItem[0];
            string[] files = SaveDataReader.FindSaveFiles(gameRoot);
            if (files != null && files.Length > 0)
            {
                items = new SaveFileItem[files.Length];
                for (int i = 0; i < files.Length; i++)
                {
                    SaveFileItem item = new SaveFileItem();
                    item.Path = files[i];
                    item.Text = Path.GetFileName(files[i]) + "   [" +
                        File.GetLastWriteTime(files[i]).ToString("yyyy-MM-dd HH:mm") + "]";
                    items[i] = item;
                }
            }

            loadingCombo = true;
            saveCombo.BeginUpdate();
            try
            {
                saveCombo.Items.Clear();
                foreach (SaveFileItem item in items)
                    saveCombo.Items.Add(item);

                int select = -1;
                for (int i = 0; i < items.Length; i++)
                {
                    if (currentPath != null &&
                        string.Equals(items[i].Path, currentPath, StringComparison.OrdinalIgnoreCase))
                    {
                        select = i;
                        break;
                    }
                }
                if (select < 0 && items.Length > 0) select = 0;
                if (select >= 0) saveCombo.SelectedIndex = select;
            }
            finally
            {
                saveCombo.EndUpdate();
                loadingCombo = false;
            }

            if (autoLoad && save == null && items.Length > 0)
                LoadSave(items[0].Path);
            else if (items.Length == 0)
            {
                pathLabel.Text = string.IsNullOrEmpty(gameRoot)
                    ? "No game root; use Open..."
                    : "No .sav files found in " + SaveDataReader.FindSaveDirectory(gameRoot);
            }
        }

        private void OpenSaveDialog()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "AA2 class saves (*.sav)|*.sav|All files (*.*)|*.*";
                dialog.Title = "Open AA2 class save";
                string dir = SaveDataReader.FindSaveDirectory(gameRoot);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    dialog.InitialDirectory = dir;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                LoadSave(dialog.FileName);
                PopulateSaveCombo(false);
            }
        }

        private void LoadSave(string path)
        {
            SaveClassData previousSave = save;
            bool reloadingCurrent = previousSave != null &&
                string.Equals(previousSave.Path, path, StringComparison.OrdinalIgnoreCase);

            Cursor previous = Cursor;
            Cursor = Cursors.WaitCursor;
            try
            {
                int keepSeat = selectedSeat;
                SaveClassData loaded = SaveDataReader.Load(path);
                save = loaded;
                lastLoadWriteTime = File.GetLastWriteTimeUtc(path);
                selectedSeat = keepSeat;
                pathLabel.Text = path;
                headerLabel.Text = FormatHeader(loaded);
                PopulateSeatList(keepSeat);
            }
            catch (Exception ex)
            {
                if (reloadingCurrent)
                {
                    // A game save can be locked briefly while it is being
                    // written. Keep the last good snapshot and try again on
                    // the next refresh.
                    headerLabel.Text = "Could not reload save (keeping last good snapshot):\r\n" + ex.Message;
                }
                else
                {
                    save = null;
                    pathLabel.Text = path;
                    headerLabel.Text = "Could not read save:\r\n" + ex.Message;
                    ClearDetails();
                    seatList.Items.Clear();
                }
            }
            finally
            {
                Cursor = previous;
            }
        }

        private string FormatHeader(SaveClassData data)
        {
            string weekday = names == null ? null : names.Get("WEEKDAY", data.GameWeekday);
            if (string.IsNullOrEmpty(weekday)) weekday = data.GameWeekday.ToString();

            StringBuilder clubs = new StringBuilder();
            for (int i = 0; i < 8; i++)
            {
                if (string.IsNullOrEmpty(data.ClubNames[i])) continue;
                if (clubs.Length > 0) clubs.Append(", ");
                clubs.Append(data.ClubNames[i]);
                string type = names == null ? null : names.Get("CLUB_TYPE", data.ClubTypes[i]);
                if (!string.IsNullOrEmpty(type)) clubs.Append(" (").Append(type).Append(")");
            }
            if (clubs.Length == 0) clubs.Append("-");

            return string.Format(
                "Academy: {0}    Year: {1}    Class: {2}\r\n" +
                "Students: {3} (boys {4} / girls {5})    Time: {6:D2}:{7:D2}:{8:D2}    Game day: {9} / {10}\r\n" +
                "Clubs: {11}",
                string.IsNullOrEmpty(data.Academy) ? "-" : data.Academy,
                string.IsNullOrEmpty(data.Year) ? "-" : data.Year,
                string.IsNullOrEmpty(data.ClassName) ? "-" : data.ClassName,
                data.StudentCount, data.Boys, data.Girls,
                data.Hours, data.Minutes, data.Seconds,
                data.GameDays, weekday, clubs.ToString());
        }

        private void PopulateSeatList(int keepSeat)
        {
            if (save == null || seatList == null) return;

            suppressSeat = true;
            seatList.BeginUpdate();
            try
            {
                seatList.Items.Clear();
                foreach (SaveCardData card in save.Cards)
                {
                    string name = LiveName(card.Seat);

                    ListViewItem item = new ListViewItem(card.Seat.ToString("D2"));
                    item.SubItems.Add(name);
                    item.Tag = card.Seat;
                    if (card.Seat == selectedSeat || (keepSeat < 0 && selectedSeat < 0))
                        item.Font = seatBoldFont;
                    seatList.Items.Add(item);
                }

                int select = keepSeat;
                if (select < 0) select = selectedSeat;
                if (select < 0 && seatList.Items.Count > 0)
                {
                    object tag = seatList.Items[0].Tag;
                    if (tag is int) select = (int)tag;
                }
                SelectSeatItem(select);
            }
            finally
            {
                seatList.EndUpdate();
                suppressSeat = false;
            }

            if (selectedSeat >= 0)
                PopulateDetails(selectedSeat);
            else
                ClearDetails();
        }

        private void SelectSeatItem(int seat)
        {
            if (seatList == null) return;
            bool found = false;
            foreach (ListViewItem item in seatList.Items)
            {
                object tag = item.Tag;
                if (tag is int && (int)tag == seat)
                {
                    item.Selected = true;
                    item.Focused = true;
                    item.EnsureVisible();
                    found = true;
                    break;
                }
            }
            if (!found && seatList.Items.Count > 0)
            {
                seatList.Items[0].Selected = true;
                object tag = seatList.Items[0].Tag;
                if (tag is int)
                {
                    selectedSeat = (int)tag;
                    PopulateDetails(selectedSeat);
                }
            }
        }

        private void PopulateDetails(int seat)
        {
            if (save == null)
            {
                ClearDetails();
                return;
            }

            SaveCardData card = save.FindCard(seat);
            if (card == null)
            {
                ClearDetails();
                return;
            }

            if (!string.IsNullOrEmpty(card.Error))
            {
                statusText.Text = "This card's PlayData could not be read:\r\n" + card.Error;
            }

            relationGrid.Rows.Clear();
            foreach (SaveEvaluationData e in card.PlayData.Relationships)
            {
                int index = relationGrid.Rows.Add();
                DataGridViewRow row = relationGrid.Rows[index];
                row.Cells[0].Value = TargetLabel(e.Seat);
                row.Cells[1].Value = FormatEvaluation(e.LovePoints, e.LoveCount, e.LoveStatus);
                row.Cells[2].Value = FormatEvaluation(e.LikePoints, e.LikeCount, e.LikeStatus);
                row.Cells[3].Value = FormatEvaluation(e.DislikePoints, e.DislikeCount, e.DislikeStatus);
                row.Cells[4].Value = FormatEvaluation(e.HatePoints, e.HateCount, e.HateStatus);
                row.Cells[5].Value = e.History == null ? "0" : e.History.Length.ToString();
            }

            outGrid.Rows.Clear();
            AddInteractions(outGrid, card.PlayData.InteractionsOut);
            inGrid.Rows.Clear();
            AddInteractions(inGrid, card.PlayData.InteractionsIn);

            statusText.Text = FormatMisc(card);
        }

        private void AddInteractions(DataGridView grid, List<SaveInteractionData> targets)
        {
            foreach (SaveInteractionData target in targets)
            {
                foreach (SaveActionData action in target.Actions)
                {
                    int index = grid.Rows.Add();
                    DataGridViewRow row = grid.Rows[index];
                    row.Cells[0].Value = TargetLabel(target.Seat);
                    row.Cells[1].Value = action.Key.ToString();
                    row.Cells[2].Value = ActionName(action.Key);
                    row.Cells[3].Value = action.Counter.ToString();
                    row.Cells[4].Value = action.Success.ToString();
                    row.Cells[5].Value = action.Failure.ToString();
                    row.Cells[6].Value = action.Huh.ToString();
                }
            }
        }

        private string FormatMisc(SaveCardData card)
        {
            SavePlayerStatus s = card.PlayData.Status;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Seat: " + card.Seat + "   " + LiveName(card.Seat));
            sb.AppendLine();
            sb.AppendLine("Player status");
            sb.AppendLine("  Quarrels:       " + s.Quarrels);
            sb.AppendLine("  Skipped:        " + s.Skipped);
            sb.AppendLine("  Competitions:   " + s.Competitions);
            sb.AppendLine("  Relationships:  " + s.Relationships);
            sb.AppendLine("  Rejected:       " + s.Rejected);
            sb.AppendLine("  Test / Phys / Club score: " + ScoreName(s.ScoreTest) + " / " +
                          ScoreName(s.ScorePhys) + " / " + ScoreName(s.ScoreClub));
            sb.AppendLine("  Total sex:      " + s.TotalSex);
            sb.AppendLine("  Sex partners:   " + s.SexPartners);
            sb.AppendLine("  First partner:  " + s.FirstPartner);
            sb.AppendLine("  First anal:     " + s.FirstPartnerAnal);
            sb.AppendLine("  Last partner:   " + s.LastPartner);
            sb.AppendLine();

            sb.AppendLine("HUH interactions out");
            bool anyHuh = false;
            foreach (SaveInteractionData target in card.PlayData.InteractionsOut)
            {
                foreach (SaveActionData action in target.Actions)
                {
                    if (action.Huh == 0) continue;
                    anyHuh = true;
                    sb.AppendLine("  To " + TargetLabel(target.Seat) + ": " +
                                  action.Key + " " + ActionName(action.Key) +
                                  "  counter " + action.Counter +
                                  ", success " + action.Success +
                                  ", failure " + action.Failure +
                                  ", huh " + action.Huh);
                }
            }
            if (!anyHuh) sb.AppendLine("  (none)");
            sb.AppendLine();

            sb.AppendLine("HUH interactions in");
            anyHuh = false;
            foreach (SaveInteractionData target in card.PlayData.InteractionsIn)
            {
                foreach (SaveActionData action in target.Actions)
                {
                    if (action.Huh == 0) continue;
                    anyHuh = true;
                    sb.AppendLine("  From " + TargetLabel(target.Seat) + ": " +
                                  action.Key + " " + ActionName(action.Key) +
                                  "  counter " + action.Counter +
                                  ", success " + action.Success +
                                  ", failure " + action.Failure +
                                  ", huh " + action.Huh);
                }
            }
            if (!anyHuh) sb.AppendLine("  (none)");
            sb.AppendLine();

            sb.AppendLine("Dates out:    " + JoinSeats(card.PlayData.DatesOut));
            sb.AppendLine("Dates in:     " + JoinSeats(card.PlayData.DatesIn));
            sb.AppendLine("Promises out: " + FormatPromises(card.PlayData.PromisesOut));
            sb.AppendLine("Promises in:  " + FormatPromises(card.PlayData.PromisesIn));
            sb.AppendLine("Meetings out: " + FormatMeetings(card.PlayData.MeetingsOut));
            sb.AppendLine("Meetings in:  " + FormatMeetings(card.PlayData.MeetingsIn));
            return sb.ToString();
        }

        private void ClearDetails()
        {
            if (relationGrid != null) relationGrid.Rows.Clear();
            if (outGrid != null) outGrid.Rows.Clear();
            if (inGrid != null) inGrid.Rows.Clear();
            if (statusText != null) statusText.Text = "No save loaded.";
        }

        private string LiveName(int seat)
        {
            string name;
            if (liveNames.TryGetValue(seat, out name) && !string.IsNullOrEmpty(name))
                return name;
            return "Seat " + seat;
        }

        private string TargetLabel(int seat)
        {
            return seat.ToString("D2") + " " + LiveName(seat);
        }

        private string ActionName(int key)
        {
            string name = names == null ? null : names.Get("ACTION_KEY", key);
            if (!string.IsNullOrEmpty(name)) return name;
            return DisplayText.ActionLabel(key);
        }

        private string FormatEvaluation(int points, int count, int status)
        {
            int total = points + count * 30;
            return total.ToString() + " (" + count.ToString() + "c, " + StatusName(status) + ")";
        }

        private static string StatusName(int status)
        {
            switch (status)
            {
                case 0: return "Lots";
                case 1: return "Some";
                case 2: return "Very Little";
                default: return status.ToString();
            }
        }

        private string ScoreName(int score)
        {
            if (names != null)
            {
                string name = names.Get("TEST_SCORE", score);
                if (!string.IsNullOrEmpty(name)) return name;
            }
            return score.ToString();
        }

        private string TestTypeName(int type)
        {
            if (names != null)
            {
                string name = names.Get("TEST_TYPE", type);
                if (!string.IsNullOrEmpty(name)) return name;
            }
            return type.ToString();
        }

        private string LocationName(int location)
        {
            if (names != null)
            {
                string name = names.Get("LOCATION", location);
                if (!string.IsNullOrEmpty(name)) return name;
            }
            return location.ToString();
        }

        private string JoinSeats(List<int> seats)
        {
            if (seats == null || seats.Count == 0) return "(none)";
            StringBuilder sb = new StringBuilder();
            foreach (int seat in seats)
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(TargetLabel(seat));
            }
            return sb.ToString();
        }

        private string FormatPromises(List<SavePromiseData> promises)
        {
            if (promises == null || promises.Count == 0) return "(none)";
            StringBuilder sb = new StringBuilder();
            foreach (SavePromiseData p in promises)
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(TargetLabel(p.Seat));
                sb.Append(" [").Append(TestTypeName(p.TestType)).Append("]");
            }
            return sb.ToString();
        }

        private string FormatMeetings(List<SaveMeetingData> meetings)
        {
            if (meetings == null || meetings.Count == 0) return "(none)";
            StringBuilder sb = new StringBuilder();
            foreach (SaveMeetingData m in meetings)
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(TargetLabel(m.Seat));
                sb.Append(" @ ").Append(LocationName(m.Location));
                if (m.Flag) sb.Append(" [flag]");
            }
            return sb.ToString();
        }
    }
}
