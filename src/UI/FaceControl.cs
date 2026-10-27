using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace AA2Trainer
{
    public sealed class FaceControl : UserControl
    {
        public event EventHandler CharacterSaved;

        // Set by the main form. Gives the editor a chance to re-run the
        // fail-safe health check immediately before a write.
        public Func<bool> ConfirmWriteAllowed { get; set; }

        private const int RowHeight = 26;
        private const int LabelWidth = 110;
        private const int GroupHeader = 34;
        private const int GroupGap = 8;

        private GameMemory mem;
        private CharacterInfo character;
        private uint loadedPointer;
        private FaceSnapshot loaded;
        private bool gateAllowed;
        private bool applying;

        private readonly Control[] editors;
        private readonly int[] colors;
        private Dictionary<string, List<CharacterChoice>> choices =
            new Dictionary<string, List<CharacterChoice>>(StringComparer.Ordinal);
        private string choiceSignature = "";

        private Button applyButton;
        private Button revertButton;
        private Label gateLabel;
        private Label statusLabel;
        private ToolTip tooltip;

        public FaceControl()
        {
            DoubleBuffered = true;
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(248, 249, 251);
            editors = new Control[FaceEditor.Fields.Count];
            colors = new int[FaceEditor.Fields.Count];
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

            // Only the field area scrolls; the gate and Apply/Revert stay put.
            Panel scroll = new Panel();
            scroll.Dock = DockStyle.Fill;
            scroll.AutoScroll = true;
            scroll.BackColor = Color.FromArgb(248, 249, 251);
            scroll.Margin = new Padding(0);
            root.Controls.Add(scroll, 0, 0);

            TableLayoutPanel columns = new TableLayoutPanel();
            columns.Dock = DockStyle.None;
            columns.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            columns.Location = new Point(0, 0);
            columns.ColumnCount = 3;
            columns.RowCount = 1;
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33F));
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34F));
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33F));
            columns.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            columns.Margin = new Padding(0);
            columns.Height = ColumnsHeight();
            scroll.Controls.Add(columns);
            columns.Width = scroll.ClientSize.Width;

            columns.Controls.Add(Stack(new Padding(0, 0, 6, 0),
                FaceGroup.Face, FaceGroup.Eyes), 0, 0);
            columns.Controls.Add(Stack(new Padding(6, 0, 6, 0),
                FaceGroup.Iris, FaceGroup.Textures), 1, 0);
            columns.Controls.Add(Stack(new Padding(6, 0, 0, 0),
                FaceGroup.Brow, FaceGroup.Glasses, FaceGroup.Mole), 2, 0);

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
                "Face changes are written only when the known game build and all pointer checks pass.");

            UpdateGate();
            SetStatus("Attach to the game and select a character.", false);
        }

        private static int ColumnsHeight()
        {
            int a = HeightOf(FaceGroup.Face) + HeightOf(FaceGroup.Eyes);
            int b = HeightOf(FaceGroup.Iris) + HeightOf(FaceGroup.Textures);
            int c = HeightOf(FaceGroup.Brow) + HeightOf(FaceGroup.Glasses) + HeightOf(FaceGroup.Mole);
            int max = a > b ? a : b;
            return c > max ? c : max;
        }

        private Control Stack(Padding margin, params FaceGroup[] groups)
        {
            TableLayoutPanel stack = new TableLayoutPanel();
            stack.Dock = DockStyle.Fill;
            stack.ColumnCount = 1;
            stack.RowCount = groups.Length + 1;
            stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            stack.Margin = margin;
            stack.Padding = new Padding(0);

            for (int i = 0; i < groups.Length; i++)
            {
                stack.RowStyles.Add(new RowStyle(SizeType.Absolute, HeightOf(groups[i])));
                stack.Controls.Add(BuildGroup(groups[i], new Padding(0)), 0, i);
            }
            // Absorb leftover height so the groups keep their size.
            stack.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            stack.Controls.Add(new Panel(), 0, groups.Length);

            return stack;
        }

        private static int HeightOf(FaceGroup group)
        {
            int rows = 0;
            for (int i = 0; i < FaceEditor.Fields.Count; i++)
                if (FaceEditor.Fields[i].Group == group) rows++;
            return (rows * RowHeight) + GroupHeader;
        }

        private static string TitleOf(FaceGroup group)
        {
            switch (group)
            {
                case FaceGroup.Face: return "Face";
                case FaceGroup.Eyes: return "Eyes";
                case FaceGroup.Iris: return "Iris";
                case FaceGroup.Brow: return "Brow";
                case FaceGroup.Glasses: return "Glasses";
                case FaceGroup.Mole: return "Mole";
                default: return "External textures";
            }
        }

        private GroupBox BuildGroup(FaceGroup group, Padding margin)
        {
            List<int> indexes = new List<int>();
            for (int i = 0; i < FaceEditor.Fields.Count; i++)
                if (FaceEditor.Fields[i].Group == group) indexes.Add(i);

            GroupBox box = new GroupBox();
            box.Text = TitleOf(group);
            box.Dock = DockStyle.Fill;
            box.Padding = new Padding(10, 5, 10, 5);
            box.Margin = margin;

            TableLayoutPanel table = new TableLayoutPanel();
            table.Dock = DockStyle.Fill;
            table.ColumnCount = 2;
            table.RowCount = indexes.Count + 1;
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, LabelWidth));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            table.Margin = new Padding(0);
            table.Padding = new Padding(0);
            for (int r = 0; r < indexes.Count; r++)
                table.RowStyles.Add(new RowStyle(SizeType.Absolute, RowHeight));
            // Absorb leftover height so the rows keep their spacing and the
            // controls are not stretched taller than their row.
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            box.Controls.Add(table);

            for (int r = 0; r < indexes.Count; r++)
            {
                int index = indexes[r];
                FaceFieldDef def = FaceEditor.Fields[index];

                Label label = new Label();
                label.Text = def.Label;
                label.Dock = DockStyle.Fill;
                label.TextAlign = ContentAlignment.MiddleLeft;
                label.Margin = new Padding(0);
                tooltip.SetToolTip(label, TipFor(def));
                table.Controls.Add(label, 0, r);

                Control editor = MakeEditor(index, def);
                table.Controls.Add(editor, 1, r);
                editors[index] = editor;
            }

            return box;
        }

        private string TipFor(FaceFieldDef def)
        {
            string tip = def.Name + "   (char + 0x" + def.Offset.ToString("X") + ")";
            if (def.Kind == FaceFieldKind.Text)
                tip += "\nMaximum " + (def.FieldSize - 1).ToString() + " bytes (Shift-JIS).";
            return tip;
        }

        private Control MakeEditor(int index, FaceFieldDef def)
        {
            if (def.Kind == FaceFieldKind.Choice)
            {
                ComboBox combo = new ComboBox();
                combo.DropDownStyle = ComboBoxStyle.DropDownList;
                combo.Font = new Font("Segoe UI", 9F);
                combo.Dock = DockStyle.None;
                combo.Anchor = AnchorStyles.Left | AnchorStyles.Right;
                combo.Margin = new Padding(0, 3, 0, 3);
                combo.Tag = index;
                tooltip.SetToolTip(combo, TipFor(def));
                return combo;
            }

            if (def.Kind == FaceFieldKind.Color)
            {
                Button button = new Button();
                button.Dock = DockStyle.Fill;
                button.Margin = new Padding(0, 1, 0, 1);
                button.FlatStyle = FlatStyle.Flat;
                button.Font = new Font("Segoe UI", 8.25F);
                button.AutoEllipsis = true;
                button.Tag = index;
                button.Text = "#000000";
                button.Click += Color_Click;
                button.EnabledChanged += delegate { RefreshColorButton(index); };
                tooltip.SetToolTip(button, TipFor(def));
                return button;
            }

            if (def.Kind == FaceFieldKind.Toggle)
            {
                CheckBox check = new CheckBox();
                check.Dock = DockStyle.Fill;
                check.Margin = new Padding(0);
                check.Tag = index;
                tooltip.SetToolTip(check, TipFor(def));
                return check;
            }

            if (def.Kind == FaceFieldKind.Text)
            {
                TextBox text = new TextBox();
                text.Dock = DockStyle.None;
                text.Anchor = AnchorStyles.Left | AnchorStyles.Right;
                text.Font = new Font("Segoe UI", 9F);
                text.Margin = new Padding(0, 3, 0, 3);
                text.MaxLength = def.FieldSize - 1;
                text.Tag = index;
                tooltip.SetToolTip(text, TipFor(def));
                return text;
            }

            NumericUpDown number = new NumericUpDown();
            number.Minimum = 0;
            number.Maximum = 255;
            number.Font = new Font("Segoe UI", 9F);
            number.TextAlign = HorizontalAlignment.Left;
            number.Dock = DockStyle.None;
            number.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            number.Margin = new Padding(0, 3, 0, 3);
            number.Tag = index;
            tooltip.SetToolTip(number, TipFor(def));
            return number;
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
            // The game stores opaque colours; normalising the alpha keeps the
            // edited value equal to what gets written back.
            colors[index] = (argb & 0x00FFFFFF) | unchecked((int)0xFF000000);
            RefreshColorButton(index);
        }

        // Enabled cells show the colour as a swatch; disabled cells fall back to
        // the normal button face so the hex text stays readable.
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

            for (int i = 0; i < FaceEditor.Fields.Count; i++)
            {
                FaceFieldDef def = FaceEditor.Fields[i];
                if (def.Kind != FaceFieldKind.Choice) continue;

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
                FaceSnapshot values;
                string error;
                if (FaceEditor.TryRead(mem, info, out values, out error))
                {
                    bool changed = !sameCharacter || loaded == null || !SnapshotsEqual(values, loaded);
                    if (changed) ApplyValues(values);
                    loaded = values;
                    loadedPointer = info.Pointer;

                    if (!sameCharacter)
                        SetStatus("Loaded the face values from live memory.", false);
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

            FaceSnapshot edited = ReadUi();
            if (loaded != null && SnapshotsEqual(edited, loaded))
            {
                SetStatus("No changes to apply.", false);
                return;
            }

            string changed;
            string error;
            if (!FaceEditor.TryWrite(mem, character, loaded, edited, out changed, out error))
            {
                SetStatus(error, true);
                UpdateGate();
                return;
            }

            FaceSnapshot check;
            string readError;
            if (FaceEditor.TryRead(mem, character, out check, out readError))
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

            FaceSnapshot values;
            string error;
            if (!FaceEditor.TryRead(mem, character, out values, out error))
            {
                SetStatus(error, true);
                return;
            }

            ApplyValues(values);
            loaded = values;
            loadedPointer = character.Pointer;
            SetStatus("Reverted to the values currently in memory.", false);
        }

        private FaceSnapshot ReadUi()
        {
            FaceSnapshot snapshot = new FaceSnapshot();
            snapshot.Values = new int[editors.Length];
            snapshot.Texts = new string[editors.Length];

            for (int i = 0; i < editors.Length; i++)
            {
                FaceFieldDef def = FaceEditor.Fields[i];
                switch (def.Kind)
                {
                    case FaceFieldKind.Choice:
                        ComboBox combo = editors[i] as ComboBox;
                        snapshot.Values[i] = combo == null ? 0 : SelectedValue(combo, 0);
                        break;
                    case FaceFieldKind.Color:
                        snapshot.Values[i] = colors[i];
                        break;
                    case FaceFieldKind.Toggle:
                        CheckBox check = editors[i] as CheckBox;
                        bool on = check != null && check.Checked;
                        // Keep the stored value when the box still reflects it,
                        // so an unusual non-zero value is not flattened to 1
                        // just by pressing Apply.
                        int stored = loaded != null && loaded.Values != null
                            ? loaded.Values[i] : 0;
                        snapshot.Values[i] = on == (stored != 0) ? stored : (on ? 1 : 0);
                        break;
                    case FaceFieldKind.Text:
                        TextBox text = editors[i] as TextBox;
                        snapshot.Texts[i] = text == null ? "" : text.Text;
                        break;
                    default:
                        NumericUpDown number = editors[i] as NumericUpDown;
                        snapshot.Values[i] = number == null ? 0 : (int)number.Value;
                        break;
                }
            }
            return snapshot;
        }

        private bool IsDirty
        {
            get { return loaded != null && !SnapshotsEqual(ReadUi(), loaded); }
        }

        private static bool SnapshotsEqual(FaceSnapshot a, FaceSnapshot b)
        {
            if (a == null || b == null || a.Values == null || b.Values == null) return false;
            if (a.Texts == null || b.Texts == null) return false;
            if (a.Values.Length != b.Values.Length) return false;
            if (a.Texts.Length != b.Texts.Length) return false;

            for (int i = 0; i < a.Values.Length; i++)
                if (a.Values[i] != b.Values[i]) return false;

            for (int i = 0; i < a.Texts.Length; i++)
            {
                if (!string.Equals(a.Texts[i] ?? "", b.Texts[i] ?? "", StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        private void ApplyValues(FaceSnapshot values)
        {
            applying = true;
            try
            {
                for (int i = 0; i < editors.Length; i++)
                {
                    FaceFieldDef def = FaceEditor.Fields[i];
                    int value = values != null ? values.Values[i] : 0;
                    string text = values != null ? (values.Texts[i] ?? "") : "";

                    switch (def.Kind)
                    {
                        case FaceFieldKind.Choice:
                            ComboBox combo = editors[i] as ComboBox;
                            if (combo != null) SelectValue(combo, value);
                            break;
                        case FaceFieldKind.Color:
                            SetColorValue(i, value);
                            break;
                        case FaceFieldKind.Toggle:
                            CheckBox check = editors[i] as CheckBox;
                            if (check != null) check.Checked = value != 0;
                            break;
                        case FaceFieldKind.Text:
                            TextBox box = editors[i] as TextBox;
                            if (box != null && box.Text != text) box.Text = text;
                            break;
                        default:
                            NumericUpDown number = editors[i] as NumericUpDown;
                            if (number == null) break;
                            if (value < number.Minimum) value = (int)number.Minimum;
                            if (value > number.Maximum) value = (int)number.Maximum;
                            number.Value = value;
                            break;
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

            // Keep values the game stores outside the known list visible (and
            // round-trippable) instead of silently falling back to the first
            // entry.
            combo.Items.Add(new CharacterChoice(value, value.ToString("D3") + "  (unnamed)"));
            combo.SelectedIndex = combo.Items.Count - 1;
        }

        private void UpdateGate()
        {
            bool attached = mem != null && mem.IsAttached;
            bool hasCharacter = attached && character != null;
            bool writable = gateAllowed && hasCharacter && mem.CanWrite;

            for (int i = 0; i < editors.Length; i++)
                if (editors[i] != null) editors[i].Enabled = writable;

            for (int i = 0; i < editors.Length; i++)
                if (FaceEditor.Fields[i].Kind == FaceFieldKind.Color) RefreshColorButton(i);

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
                    "Face changes are verified after each write.");
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
