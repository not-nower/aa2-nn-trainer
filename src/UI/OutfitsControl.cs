using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace AA2Trainer
{
    public sealed class OutfitsControl : UserControl
    {
        public event EventHandler CharacterSaved;

        // Set by the main form. Gives the editor a chance to re-run the
        // fail-safe health check immediately before a write.
        public Func<bool> ConfirmWriteAllowed { get; set; }

        private const int RowHeight = 26;
        private const int LabelWidth = 96;
        private const int SwatchWidth = 78;
        private const int NumberWidth = 56;

        private GameMemory mem;
        private CharacterInfo character;
        private uint loadedPointer;
        private OutfitSnapshot loaded;
        private bool gateAllowed;
        private bool applying;

        private readonly Control[] editors;
        private readonly TrackBar[] bars;
        private readonly int[] colors;
        private Dictionary<string, List<CharacterChoice>> choices =
            new Dictionary<string, List<CharacterChoice>>(StringComparer.Ordinal);
        private string choiceSignature = "";

        private TabControl setTabs;
        private Button applyButton;
        private Button revertButton;
        private Label gateLabel;
        private Label statusLabel;
        private ToolTip tooltip;

        public OutfitsControl()
        {
            DoubleBuffered = true;
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(248, 249, 251);

            editors = new Control[OutfitEditor.Fields.Count];
            bars = new TrackBar[OutfitEditor.Fields.Count];
            colors = new int[OutfitEditor.Fields.Count];
            for (int i = 0; i < colors.Length; i++)
                colors[i] = unchecked((int)0xFF000000);

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

            setTabs = new TabControl();
            setTabs.Dock = DockStyle.Fill;
            setTabs.Padding = new Point(12, 6);
            for (int set = 0; set < OutfitEditor.Sets.Count; set++)
                setTabs.TabPages.Add(BuildSetPage(set));
            root.Controls.Add(setTabs, 0, 0);

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
                "Outfit changes are written only when the known game build and all pointer checks pass.");

            UpdateGate();
            SetStatus("Attach to the game and select a character.", false);
        }

        private TabPage BuildSetPage(int setIndex)
        {
            TabPage page = new TabPage(OutfitEditor.Sets[setIndex].Label);
            page.BackColor = Color.FromArgb(248, 249, 251);

            Panel scroll = new Panel();
            scroll.Dock = DockStyle.Fill;
            scroll.AutoScroll = true;
            scroll.BackColor = Color.FromArgb(248, 249, 251);
            scroll.Margin = new Padding(0);
            page.Controls.Add(scroll);

            TableLayoutPanel columns = new TableLayoutPanel();
            columns.Dock = DockStyle.None;
            columns.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            columns.Location = new Point(0, 0);
            columns.ColumnCount = 2;
            columns.RowCount = 1;
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            columns.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            columns.Margin = new Padding(0);
            columns.Height = Math.Max(
                GroupRowHeight(9) + GroupRowHeight(6),
                GroupRowHeight(2) + GroupRowHeight(4) + GroupRowHeight(2));
            scroll.Controls.Add(columns);
            columns.Width = Math.Max(0,
                scroll.ClientSize.Width - SystemInformation.VerticalScrollBarWidth);
            scroll.Resize += delegate
            {
                int width = scroll.ClientSize.Width - SystemInformation.VerticalScrollBarWidth;
                if (width > 0) columns.Width = width;
            };

            columns.Controls.Add(Stack(setIndex, new Padding(0, 0, 6, 0), true), 0, 0);
            columns.Controls.Add(Stack(setIndex, new Padding(6, 0, 0, 0), false), 1, 0);

            return page;
        }

        private static int GroupRowHeight(int rows)
        {
            return (rows * RowHeight) + 40 + 4;
        }

        private Control Stack(int setIndex, Padding margin, bool left)
        {
            TableLayoutPanel stack = new TableLayoutPanel();
            stack.Dock = DockStyle.Fill;
            stack.ColumnCount = 1;
            stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            stack.Margin = margin;
            stack.Padding = new Padding(0);

            if (left)
            {
                stack.RowCount = 3;
                stack.RowStyles.Add(new RowStyle(SizeType.Absolute, GroupRowHeight(9)));
                stack.RowStyles.Add(new RowStyle(SizeType.Absolute, GroupRowHeight(6)));
                stack.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
                stack.Controls.Add(BuildUniformGroup(setIndex), 0, 0);
                stack.Controls.Add(BuildUnderwearGroup(setIndex), 0, 1);
                stack.Controls.Add(new Panel(), 0, 2);
            }
            else
            {
                stack.RowCount = 4;
                stack.RowStyles.Add(new RowStyle(SizeType.Absolute, GroupRowHeight(2)));
                stack.RowStyles.Add(new RowStyle(SizeType.Absolute, GroupRowHeight(4)));
                stack.RowStyles.Add(new RowStyle(SizeType.Absolute, GroupRowHeight(2)));
                stack.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
                stack.Controls.Add(BuildBottomGroup(setIndex), 0, 0);
                stack.Controls.Add(BuildShoesGroup(setIndex), 0, 1);
                stack.Controls.Add(BuildSocksGroup(setIndex), 0, 2);
                stack.Controls.Add(new Panel(), 0, 3);
            }

            return stack;
        }

        private GroupBox BuildUniformGroup(int setIndex)
        {
            TableLayoutPanel table = MakeColorTable();

            int row = 0;
            AddChoiceRow(table, row++, DefIndex(setIndex, "UType"), 3);
            AddNumberRow(table, row++, DefIndex(setIndex, "SkirtLen"));
            AddToggleRow(table, row++, DefIndex(setIndex, "Onepiece"));
            AddToggleRow(table, row++, DefIndex(setIndex, "Undie"));
            AddToggleRow(table, row++, DefIndex(setIndex, "Skirt"));
            AddColorRow(table, row++, setIndex, "Top1C", "Top1O");
            AddColorRow(table, row++, setIndex, "Top2C", "Top2O");
            AddColorRow(table, row++, setIndex, "Top3C", "Top3O");
            AddColorRow(table, row++, setIndex, "Top4C", "Top4O");

            FinishRows(table, row);
            return MakeGroup("Uniform", table);
        }

        private GroupBox BuildBottomGroup(int setIndex)
        {
            TableLayoutPanel table = MakeColorTable();
            int row = 0;
            AddColorRow(table, row++, setIndex, "Bottom1C", "Bottom1O");
            AddColorRow(table, row++, setIndex, "Bottom2C", "Bottom2O");
            FinishRows(table, row);
            return MakeGroup("Bottom", table);
        }

        private GroupBox BuildShoesGroup(int setIndex)
        {
            TableLayoutPanel table = MakeColorTable();
            int row = 0;
            AddChoiceRow(table, row++, DefIndex(setIndex, "InShoes"), 3);
            AddColorRow(table, row++, setIndex, "InShoesC", "InShoesO");
            AddChoiceRow(table, row++, DefIndex(setIndex, "OutShoes"), 3);
            AddColorRow(table, row++, setIndex, "OutShoesC", "OutShoesO");
            FinishRows(table, row);
            return MakeGroup("Shoes", table);
        }

        private GroupBox BuildSocksGroup(int setIndex)
        {
            TableLayoutPanel table = MakeColorTable();
            int row = 0;
            AddChoiceRow(table, row++, DefIndex(setIndex, "Socks"), 3);
            AddColorRow(table, row++, setIndex, "SocksC", "SocksO");
            FinishRows(table, row);
            return MakeGroup("Socks", table);
        }

        private GroupBox BuildUnderwearGroup(int setIndex)
        {
            TableLayoutPanel table = MakeColorTable();
            int row = 0;
            AddColorRow(table, row++, setIndex, "UndieC", "UndieO", "Colour");
            AddNumberRow(table, row++, DefIndex(setIndex, "UndieT"), "Texture");
            AddNumberRow(table, row++, DefIndex(setIndex, "UndieH"), "Hue");
            AddNumberRow(table, row++, DefIndex(setIndex, "UndieL"), "Lightness");
            AddNumberRow(table, row++, DefIndex(setIndex, "UndieShdH"), "Shd Hue");
            AddNumberRow(table, row++, DefIndex(setIndex, "UndieShdL"), "Shd Light");
            FinishRows(table, row);
            return MakeGroup("Underwear", table);
        }

        private static TableLayoutPanel MakeTable(int columnCount)
        {
            TableLayoutPanel table = new TableLayoutPanel();
            table.Dock = DockStyle.Fill;
            table.ColumnCount = columnCount;
            table.RowCount = 1;
            table.Margin = new Padding(0);
            table.Padding = new Padding(0);
            return table;
        }

        private static TableLayoutPanel MakeColorTable()
        {
            TableLayoutPanel table = MakeTable(4);
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, LabelWidth));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, SwatchWidth));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, NumberWidth));
            return table;
        }

        private static GroupBox MakeGroup(string title, TableLayoutPanel table)
        {
            GroupBox box = new GroupBox();
            box.Text = title;
            box.Dock = DockStyle.Fill;
            box.Padding = new Padding(10, 5, 10, 5);
            box.Margin = new Padding(0, 0, 0, 4);
            box.Controls.Add(table);
            return box;
        }

        private static void AddRow(TableLayoutPanel table, int row)
        {
            table.RowCount = row + 1;
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, RowHeight));
        }

        private static void FinishRows(TableLayoutPanel table, int rows)
        {
            table.RowCount = rows + 1;
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        }

        private static void AddLabel(TableLayoutPanel table, int row, string text)
        {
            Label label = new Label();
            label.Text = text;
            label.Dock = DockStyle.Fill;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Margin = new Padding(0);
            table.Controls.Add(label, 0, row);
        }

        private void AddChoiceRow(TableLayoutPanel table, int row, int index, int columnSpan)
        {
            if (index < 0) return;
            OutfitFieldDef def = OutfitEditor.Fields[index];

            AddRow(table, row);
            AddLabel(table, row, def.Label);

            ComboBox combo = new ComboBox();
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
            combo.Font = new Font("Segoe UI", 9F);
            combo.Dock = DockStyle.None;
            combo.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            combo.Margin = new Padding(0, 3, 0, 3);
            combo.Tag = index;
            combo.DropDown += delegate
            {
                int minimum = def.Name.EndsWith("UType", StringComparison.Ordinal) ? 460 : 260;
                int width = Math.Max(combo.Width, minimum);
                if (combo.DropDownWidth < width) combo.DropDownWidth = width;
            };
            tooltip.SetToolTip(combo, TipFor(def));
            table.Controls.Add(combo, 1, row);
            if (columnSpan > 1) table.SetColumnSpan(combo, columnSpan);
            editors[index] = combo;
        }

        private void AddNumberRow(TableLayoutPanel table, int row, int index,
            string labelOverride = null)
        {
            if (index < 0) return;
            OutfitFieldDef def = OutfitEditor.Fields[index];

            AddRow(table, row);
            AddLabel(table, row, labelOverride ?? def.Label);

            NumericUpDown number = new NumericUpDown();
            number.Minimum = def.Minimum;
            number.Maximum = def.Maximum;
            number.Font = new Font("Segoe UI", 9F);
            number.TextAlign = HorizontalAlignment.Left;
            number.Dock = DockStyle.None;
            number.Anchor = AnchorStyles.Left;
            number.Width = 78;
            number.Margin = new Padding(0, 3, 0, 3);
            number.Tag = index;
            int initial = 0;
            if (initial < def.Minimum) initial = def.Minimum;
            if (initial > def.Maximum) initial = def.Maximum;
            number.Value = initial;
            tooltip.SetToolTip(number, TipFor(def));
            table.Controls.Add(number, 1, row);
            editors[index] = number;
        }

        private void AddToggleRow(TableLayoutPanel table, int row, int index)
        {
            if (index < 0) return;
            OutfitFieldDef def = OutfitEditor.Fields[index];

            AddRow(table, row);
            AddLabel(table, row, def.Label);

            CheckBox check = new CheckBox();
            check.Dock = DockStyle.None;
            check.Anchor = AnchorStyles.Left;
            check.AutoSize = true;
            check.Margin = new Padding(0, 6, 0, 0);
            check.Tag = index;
            tooltip.SetToolTip(check, TipFor(def));
            table.Controls.Add(check, 1, row);
            editors[index] = check;
        }

        private void AddColorRow(TableLayoutPanel table, int row, int setIndex,
            string colorSuffix, string opacitySuffix, string labelOverride = null)
        {
            int colorIndex = OutfitEditor.IndexOf(setIndex, colorSuffix);
            int opacityIndex = OutfitEditor.IndexOf(setIndex, opacitySuffix);
            if (colorIndex < 0 || opacityIndex < 0) return;

            OutfitFieldDef colorDef = OutfitEditor.Fields[colorIndex];
            OutfitFieldDef opacityDef = OutfitEditor.Fields[opacityIndex];

            AddRow(table, row);
            AddLabel(table, row, labelOverride ?? colorDef.Label);

            Button colorButton = new Button();
            colorButton.Dock = DockStyle.Fill;
            colorButton.Margin = new Padding(0, 2, 4, 2);
            colorButton.FlatStyle = FlatStyle.Flat;
            colorButton.Font = new Font("Segoe UI", 8.25F);
            colorButton.AutoEllipsis = true;
            colorButton.Tag = colorIndex;
            colorButton.Click += Color_Click;
            colorButton.EnabledChanged += delegate { RefreshColorButton(colorIndex); };
            tooltip.SetToolTip(colorButton, TipFor(colorDef));
            table.Controls.Add(colorButton, 1, row);
            editors[colorIndex] = colorButton;

            TrackBar bar = new TrackBar();
            bar.Minimum = 0;
            bar.Maximum = 255;
            bar.TickStyle = TickStyle.None;
            bar.SmallChange = 1;
            bar.LargeChange = 10;
            bar.Dock = DockStyle.Fill;
            bar.Margin = new Padding(4, 0, 4, 0);
            bar.TabStop = false;
            bar.Value = 255;
            bar.Tag = opacityIndex;
            tooltip.SetToolTip(bar, TipFor(opacityDef));
            table.Controls.Add(bar, 2, row);
            bars[opacityIndex] = bar;

            NumericUpDown number = new NumericUpDown();
            number.Minimum = 0;
            number.Maximum = 255;
            number.Font = new Font("Segoe UI", 9F);
            number.TextAlign = HorizontalAlignment.Left;
            number.Dock = DockStyle.None;
            number.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            number.Margin = new Padding(0, 3, 0, 3);
            number.Value = 255;
            number.Tag = opacityIndex;
            tooltip.SetToolTip(number, TipFor(opacityDef));
            table.Controls.Add(number, 3, row);
            editors[opacityIndex] = number;

            bar.ValueChanged += delegate
            {
                if (applying) return;
                applying = true;
                try { number.Value = bar.Value; }
                finally { applying = false; }
            };
            number.ValueChanged += delegate
            {
                if (applying) return;
                applying = true;
                try { bar.Value = (int)number.Value; }
                finally { applying = false; }
            };

            RefreshColorButton(colorIndex);
        }

        private static string TipFor(OutfitFieldDef def)
        {
            return def.Name + "   (char + 0x" + def.Offset.ToString("X") + ")";
        }

        private static int DefIndex(int setIndex, string suffix)
        {
            return OutfitEditor.IndexOf(setIndex, suffix);
        }

        private void Color_Click(object sender, EventArgs e)
        {
            if (applying) return;

            Button button = sender as Button;
            if (button == null) return;
            int index = (int)button.Tag;

            using (ColorDialog dialog = new ColorDialog())
            {
                dialog.FullOpen = true;
                dialog.AnyColor = true;
                dialog.Color = ToColor(colors[index]);
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                SetColorValue(index, dialog.Color.ToArgb());
            }
        }

        private static Color ToColor(int argb)
        {
            return Color.FromArgb(255, (argb >> 16) & 0xFF, (argb >> 8) & 0xFF, argb & 0xFF);
        }

        private void SetColorValue(int index, int argb)
        {
            // The game stores opaque RGB bytes plus a separate opacity byte;
            // normalising the alpha keeps the swatch and the write aligned.
            colors[index] = (argb & 0x00FFFFFF) | unchecked((int)0xFF000000);
            RefreshColorButton(index);
        }

        private void RefreshColorButton(int index)
        {
            Button button = editors[index] as Button;
            if (button == null) return;

            int argb = colors[index];
            Color color = ToColor(argb);
            button.Text = string.Format("#{0:X6}", argb & 0xFFFFFF);

            if (!button.Enabled)
            {
                button.BackColor = SystemColors.Control;
                button.ForeColor = Color.Gray;
                return;
            }

            button.BackColor = color;
            int brightness = (color.R * 299 + color.G * 587 + color.B * 114) / 1000;
            button.ForeColor = brightness > 140 ? Color.Black : Color.White;
        }

        public void SetChoices(Dictionary<string, List<CharacterChoice>> source, string signature)
        {
            if (source == null) source = new Dictionary<string, List<CharacterChoice>>();
            if (string.Equals(signature ?? "", choiceSignature, StringComparison.Ordinal)) return;

            choiceSignature = signature ?? "";
            choices = source;

            for (int i = 0; i < OutfitEditor.Fields.Count; i++)
            {
                OutfitFieldDef def = OutfitEditor.Fields[i];
                if (def.Kind != OutfitFieldKind.Choice) continue;

                ComboBox combo = editors[i] as ComboBox;
                if (combo == null) continue;

                List<CharacterChoice> list;
                if (!choices.TryGetValue(def.Name, out list) || list == null)
                    list = new List<CharacterChoice>();

                int current = SelectedValue(combo,
                    loaded != null && loaded.Values != null ? loaded.Values[i] : 0);
                RebuildCombo(combo, list, current);
            }
        }

        private static void RebuildCombo(ComboBox combo, List<CharacterChoice> list, int current)
        {
            combo.BeginUpdate();
            try
            {
                combo.Items.Clear();
                foreach (CharacterChoice choice in list)
                    combo.Items.Add(choice);

                int index = -1;
                for (int i = 0; i < combo.Items.Count; i++)
                {
                    CharacterChoice choice = combo.Items[i] as CharacterChoice;
                    if (choice != null && choice.Value == current) { index = i; break; }
                }

                if (index < 0)
                {
                    combo.Items.Add(new CharacterChoice(current,
                        current.ToString("D3") + "  (unnamed)"));
                    index = combo.Items.Count - 1;
                }
                combo.SelectedIndex = index;
            }
            finally
            {
                combo.EndUpdate();
            }
        }

        private static int SelectedValue(ComboBox combo, int fallback)
        {
            CharacterChoice choice = combo.SelectedItem as CharacterChoice;
            return choice == null ? fallback : choice.Value;
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
                OutfitSnapshot values;
                string error;
                if (OutfitEditor.TryRead(mem, info, out values, out error))
                {
                    bool changed = !sameCharacter || loaded == null ||
                        !ValuesEqual(values.Values, loaded.Values);
                    if (changed) ApplyValues(values);
                    loaded = values;
                    loadedPointer = info.Pointer;

                    if (!sameCharacter)
                        SetStatus("Loaded the outfit values from live memory.", false);
                }
                else
                {
                    loaded = null;
                    loadedPointer = info.Pointer;
                    ApplyValues(null);
                    SetStatus(error, true);
                }
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
            mem = null;
            character = null;
            loadedPointer = 0;
            loaded = null;
            gateAllowed = false;

            ApplyValues(null);
            SetStatus("Attach to the game and select a character.", false);
            UpdateGate();
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
            if (loaded == null)
            {
                SetStatus("The outfit values are not loaded.", true);
                UpdateGate();
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

            OutfitSnapshot edited = ReadUi();
            if (ValuesEqual(edited.Values, loaded.Values))
            {
                SetStatus("No changes to apply.", false);
                return;
            }

            string changed;
            string error;
            if (!OutfitEditor.TryWrite(mem, character, loaded, edited, out changed, out error))
            {
                SetStatus(error, true);
                UpdateGate();
                return;
            }

            OutfitSnapshot check;
            string readError;
            if (OutfitEditor.TryRead(mem, character, out check, out readError))
            {
                ApplyValues(check);
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

            OutfitSnapshot values;
            string error;
            if (!OutfitEditor.TryRead(mem, character, out values, out error))
            {
                SetStatus(error, true);
                return;
            }

            ApplyValues(values);
            loaded = values;
            loadedPointer = character.Pointer;
            SetStatus("Reverted to the values currently in memory.", false);
        }

        private OutfitSnapshot ReadUi()
        {
            OutfitSnapshot snapshot = new OutfitSnapshot();
            snapshot.Values = new int[editors.Length];

            for (int i = 0; i < editors.Length; i++)
            {
                OutfitFieldDef def = OutfitEditor.Fields[i];

                if (def.Kind == OutfitFieldKind.Choice)
                {
                    ComboBox combo = editors[i] as ComboBox;
                    snapshot.Values[i] = combo == null ? 0 : SelectedValue(combo, 0);
                    continue;
                }

                if (def.Kind == OutfitFieldKind.Toggle)
                {
                    CheckBox check = editors[i] as CheckBox;
                    bool on = check != null && check.Checked;
                    // Keep an unusual stored value when the box still reflects
                    // it, so pressing Apply does not flatten it to 1.
                    int stored = loaded != null && loaded.Values != null
                        ? loaded.Values[i] : 0;
                    snapshot.Values[i] = on == (stored != 0) ? stored : (on ? 1 : 0);
                    continue;
                }

                if (def.Kind == OutfitFieldKind.Color)
                {
                    snapshot.Values[i] = colors[i];
                    continue;
                }

                NumericUpDown number = editors[i] as NumericUpDown;
                snapshot.Values[i] = number == null ? 0 : (int)number.Value;
            }

            return snapshot;
        }

        private bool IsDirty
        {
            get
            {
                return loaded != null && loaded.Values != null &&
                    !ValuesEqual(ReadUi().Values, loaded.Values);
            }
        }

        private static bool ValuesEqual(int[] a, int[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        private void ApplyValues(OutfitSnapshot values)
        {
            applying = true;
            try
            {
                for (int i = 0; i < editors.Length; i++)
                {
                    OutfitFieldDef def = OutfitEditor.Fields[i];
                    int value = values != null && values.Values != null ? values.Values[i] : 0;

                    if (def.Kind == OutfitFieldKind.Choice)
                    {
                        ComboBox combo = editors[i] as ComboBox;
                        if (combo != null) SelectValue(combo, value);
                    }
                    else if (def.Kind == OutfitFieldKind.Toggle)
                    {
                        CheckBox check = editors[i] as CheckBox;
                        if (check != null) check.Checked = value != 0;
                    }
                    else if (def.Kind == OutfitFieldKind.Color)
                    {
                        SetColorValue(i, value);
                    }
                    else if (def.Kind == OutfitFieldKind.Opacity)
                    {
                        NumericUpDown number = editors[i] as NumericUpDown;
                        TrackBar bar = bars[i];
                        if (value < 0) value = 0;
                        if (value > 255) value = 255;
                        if (number != null) number.Value = value;
                        if (bar != null) bar.Value = value;
                    }
                    else
                    {
                        NumericUpDown number = editors[i] as NumericUpDown;
                        if (number != null)
                        {
                            if (value < number.Minimum) value = (int)number.Minimum;
                            if (value > number.Maximum) value = (int)number.Maximum;
                            number.Value = value;
                        }
                    }
                }
            }
            finally
            {
                applying = false;
            }
        }

        private static void SelectValue(ComboBox combo, int value)
        {
            for (int i = 0; i < combo.Items.Count; i++)
            {
                CharacterChoice choice = combo.Items[i] as CharacterChoice;
                if (choice != null && choice.Value == value)
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }

            combo.Items.Add(new CharacterChoice(value,
                value.ToString("D3") + "  (unnamed)"));
            combo.SelectedIndex = combo.Items.Count - 1;
        }

        private void UpdateGate()
        {
            bool attached = mem != null && mem.IsAttached;
            bool hasCharacter = attached && character != null;
            bool writable = gateAllowed && hasCharacter && mem.CanWrite;

            for (int i = 0; i < editors.Length; i++)
                if (editors[i] != null) editors[i].Enabled = writable;

            for (int i = 0; i < bars.Length; i++)
                if (bars[i] != null) bars[i].Enabled = writable;

            if (applyButton != null) applyButton.Enabled = writable;
            if (revertButton != null) revertButton.Enabled = hasCharacter;

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
                    "Outfit changes are verified after each write.");
            }
        }

        private void SetStatus(string text, bool error)
        {
            if (statusLabel == null) return;
            statusLabel.Text = text ?? "";
            statusLabel.ForeColor = error ? Color.Firebrick : Color.DimGray;
        }
    }
}
