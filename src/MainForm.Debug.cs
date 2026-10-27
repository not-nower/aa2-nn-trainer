using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;


namespace AA2Trainer
{
    public partial class MainForm
    {
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
                "Orientation", "Social", "Fighting Style", "Study", "Strength",
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
            root.Controls.Add(CreateKvGroup("Class && Time", new string[]
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
            TabPage page = new TabPage("Relationships");
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
            SetKV(charKv, "Orientation", NamedValue("Oriented", DashboardData.Value(mem, ctx, "Oriented", 0)), true);
            SetKV(charKv, "Social", DashboardData.Value(mem, ctx, "Social", 0), true);
            SetKV(charKv, "Fighting Style", NamedValue("FightingStyle", DashboardData.Value(mem, ctx, "FightingStyle", 0)), true);
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
    }
}
