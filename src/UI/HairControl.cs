using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace AA2Trainer
{
    public sealed class HairControl : UserControl
    {
        public event EventHandler CharacterSaved;

        // Set by the main form. Gives the editor a chance to re-run the
        // fail-safe health check immediately before a write.
        public Func<bool> ConfirmWriteAllowed { get; set; }

        private const int RowHeight = 26;
        private const int LabelWidth = 92;
        private const int FlipWidth = 58;

        private GameMemory mem;
        private CharacterInfo character;
        private uint loadedPointer;
        private int[] loaded;
        private bool gateAllowed;
        private bool applying;

        private readonly Control[] editors;
        private readonly CheckBox[] flipBoxes;
        private readonly TrackBar[] bars;
        private Dictionary<string, List<CharacterChoice>> choices =
            new Dictionary<string, List<CharacterChoice>>(StringComparer.Ordinal);
        private string choiceSignature = "";

        private Button applyButton;
        private Button revertButton;
        private Label gateLabel;
        private Label statusLabel;
        private ToolTip tooltip;

        public HairControl()
        {
            DoubleBuffered = true;
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(248, 249, 251);
            editors = new Control[HairEditor.Fields.Count];
            flipBoxes = new CheckBox[HairEditor.Fields.Count];
            bars = new TrackBar[HairEditor.Fields.Count];
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

            TableLayoutPanel content = new TableLayoutPanel();
            content.Dock = DockStyle.Fill;
            content.ColumnCount = 1;
            content.RowCount = 2;
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, (8 * RowHeight) + 40));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            content.Margin = new Padding(0);
            root.Controls.Add(content, 0, 0);
            content.Controls.Add(BuildHairGroup(), 0, 0);

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
                "Hair changes are written only when the known game build and all pointer checks pass.");

            UpdateGate();
            SetStatus("Attach to the game and select a character.", false);
        }

        private GroupBox BuildHairGroup()
        {
            GroupBox box = new GroupBox();
            box.Text = "Hair";
            box.Dock = DockStyle.Fill;
            box.Padding = new Padding(10, 5, 10, 5);
            box.Margin = new Padding(0);

            TableLayoutPanel table = new TableLayoutPanel();
            table.Dock = DockStyle.Fill;
            table.ColumnCount = 5;
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112F));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 22F));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 74F));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 10F));
            table.Margin = new Padding(0);
            table.Padding = new Padding(0);

            int row = 0;
            for (int i = 0; i < HairEditor.Fields.Count; i++)
            {
                HairFieldDef style = HairEditor.Fields[i];
                if (style.Group != HairGroup.Style) continue;

                // Style row: name, dropdown, flip box.
                table.RowCount = row + 1;
                table.RowStyles.Add(new RowStyle(SizeType.Absolute, RowHeight));

                Label styleLabel = new Label();
                styleLabel.Text = style.Label + " Style";
                styleLabel.Dock = DockStyle.Fill;
                styleLabel.TextAlign = ContentAlignment.MiddleLeft;
                styleLabel.Margin = new Padding(0);
                tooltip.SetToolTip(styleLabel, TipFor(style));
                table.Controls.Add(styleLabel, 0, row);

                ComboBox combo = new ComboBox();
                combo.DropDownStyle = ComboBoxStyle.DropDownList;
                combo.Font = new Font("Segoe UI", 9F);
                combo.Dock = DockStyle.None;
                combo.Anchor = AnchorStyles.Left | AnchorStyles.Right;
                combo.Margin = new Padding(0, 3, 0, 3);
                combo.Tag = i;
                tooltip.SetToolTip(combo, TipFor(style));
                table.Controls.Add(combo, 1, row);
                editors[i] = combo;

                int flipIndex = HairEditor.FlipIndexForStyle(i);
                if (flipIndex >= 0)
                {
                    HairFieldDef flip = HairEditor.Fields[flipIndex];
                    CheckBox check = new CheckBox();
                    check.Text = "Flip";
                    check.Dock = DockStyle.Fill;
                    check.AutoSize = false;
                    check.TextAlign = ContentAlignment.MiddleLeft;
                    check.Margin = new Padding(0);
                    check.Tag = flipIndex;
                    tooltip.SetToolTip(check, TipFor(flip));
                    table.Controls.Add(check, 3, row);
                    editors[flipIndex] = check;
                    flipBoxes[flipIndex] = check;
                }

                row++;

                // Length row: slider plus a box for exact values.
                int lengthIndex = HairEditor.LengthIndexForStyle(i);
                if (lengthIndex < 0) continue;

                HairFieldDef length = HairEditor.Fields[lengthIndex];
                table.RowCount = row + 1;
                table.RowStyles.Add(new RowStyle(SizeType.Absolute, RowHeight));

                Label lengthLabel = new Label();
                lengthLabel.Text = "Length";
                lengthLabel.Dock = DockStyle.Fill;
                lengthLabel.TextAlign = ContentAlignment.MiddleLeft;
                lengthLabel.Margin = new Padding(0);
                lengthLabel.Padding = new Padding(14, 0, 0, 0);
                tooltip.SetToolTip(lengthLabel, TipFor(length));
                table.Controls.Add(lengthLabel, 0, row);

                TrackBar bar = new TrackBar();
                bar.Minimum = 0;
                bar.Maximum = 255;
                bar.TickStyle = TickStyle.None;
                bar.SmallChange = 1;
                bar.LargeChange = 10;
                bar.Dock = DockStyle.Fill;
                bar.Margin = new Padding(0);
                bar.Tag = lengthIndex;
                bar.TabStop = false;
                tooltip.SetToolTip(bar, TipFor(length) +
                    "\nDrag the slider or type an exact value.");
                table.Controls.Add(bar, 1, row);
                bars[lengthIndex] = bar;

                NumericUpDown number = MakeNumber(lengthIndex, length);
                table.Controls.Add(number, 3, row);
                editors[lengthIndex] = number;

                int captured = lengthIndex;
                bar.ValueChanged += delegate
                {
                    if (applying) return;
                    applying = true;
                    try
                    {
                        NumericUpDown target = editors[captured] as NumericUpDown;
                        if (target != null) target.Value = bar.Value;
                    }
                    finally
                    {
                        applying = false;
                    }
                };
                number.ValueChanged += delegate
                {
                    if (applying) return;
                    applying = true;
                    try
                    {
                        TrackBar target = bars[captured];
                        if (target != null) target.Value = (int)number.Value;
                    }
                    finally
                    {
                        applying = false;
                    }
                };

                row++;
            }

            // Absorb leftover height so the rows keep their spacing.
            table.RowCount = row + 1;
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            box.Controls.Add(table);
            return box;
        }

        private NumericUpDown MakeNumber(int index, HairFieldDef def)
        {
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

        private static string TipFor(HairFieldDef def)
        {
            return def.Name + "   (char + 0x" + def.Offset.ToString("X") + ")";
        }

        public void SetChoices(Dictionary<string, List<CharacterChoice>> source, string signature)
        {
            if (source == null) source = new Dictionary<string, List<CharacterChoice>>();
            if (string.Equals(signature ?? "", choiceSignature, StringComparison.Ordinal)) return;

            choiceSignature = signature ?? "";
            choices = source;

            for (int i = 0; i < HairEditor.Fields.Count; i++)
            {
                HairFieldDef def = HairEditor.Fields[i];
                if (def.Kind != HairFieldKind.Choice) continue;

                ComboBox combo = editors[i] as ComboBox;
                if (combo == null) continue;

                List<CharacterChoice> list;
                if (!choices.TryGetValue(def.Name, out list) || list == null)
                    list = new List<CharacterChoice>();

                int current = SelectedValue(combo, loaded != null ? loaded[i] : 0);
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
                    combo.Items.Add(new CharacterChoice(current, current.ToString()));
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
                int[] values;
                string error;
                if (HairEditor.TryRead(mem, info, out values, out error))
                {
                    bool changed = !sameCharacter || loaded == null || !ValuesEqual(values, loaded);
                    if (changed) ApplyValues(values);
                    loaded = values;
                    loadedPointer = info.Pointer;

                    if (!sameCharacter)
                        SetStatus("Loaded the hair values from live memory.", false);
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

            int[] edited = ReadUi();
            if (loaded != null && ValuesEqual(edited, loaded))
            {
                SetStatus("No changes to apply.", false);
                return;
            }

            string changed;
            string error;
            if (!HairEditor.TryWrite(mem, character, loaded, edited, out changed, out error))
            {
                SetStatus(error, true);
                UpdateGate();
                return;
            }

            int[] check;
            string readError;
            if (HairEditor.TryRead(mem, character, out check, out readError))
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

            int[] values;
            string error;
            if (!HairEditor.TryRead(mem, character, out values, out error))
            {
                SetStatus(error, true);
                return;
            }

            ApplyValues(values);
            loaded = values;
            loadedPointer = character.Pointer;
            SetStatus("Reverted to the values currently in memory.", false);
        }

        private int[] ReadUi()
        {
            int[] values = new int[editors.Length];
            for (int i = 0; i < editors.Length; i++)
            {
                HairFieldDef def = HairEditor.Fields[i];

                if (def.Kind == HairFieldKind.Choice)
                {
                    ComboBox combo = editors[i] as ComboBox;
                    values[i] = combo == null ? 0 : SelectedValue(combo, 0);
                    continue;
                }

                if (def.Kind == HairFieldKind.Toggle)
                {
                    CheckBox check = editors[i] as CheckBox;
                    bool on = check != null && check.Checked;
                    // Keep the stored value when the box still reflects it, so
                    // a flip of 2 is not flattened to 1 just by pressing Apply.
                    int stored = loaded != null ? loaded[i] : 0;
                    values[i] = on == (stored != 0) ? stored : (on ? 1 : 0);
                    continue;
                }

                NumericUpDown number = editors[i] as NumericUpDown;
                values[i] = number == null ? 0 : (int)number.Value;
            }
            return values;
        }

        private bool IsDirty
        {
            get { return loaded != null && !ValuesEqual(ReadUi(), loaded); }
        }

        private static bool ValuesEqual(int[] a, int[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        private void ApplyValues(int[] values)
        {
            applying = true;
            try
            {
                for (int i = 0; i < editors.Length; i++)
                {
                    HairFieldDef def = HairEditor.Fields[i];
                    int value = values != null ? values[i] : 0;

                    if (def.Kind == HairFieldKind.Choice)
                    {
                        ComboBox combo = editors[i] as ComboBox;
                        if (combo != null) SelectValue(combo, value);
                    }
                    else if (def.Kind == HairFieldKind.Toggle)
                    {
                        CheckBox check = editors[i] as CheckBox;
                        if (check != null) check.Checked = value != 0;
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

                        TrackBar bar = bars[i];
                        if (bar != null)
                        {
                            int barValue = value;
                            if (barValue < bar.Minimum) barValue = bar.Minimum;
                            if (barValue > bar.Maximum) barValue = bar.Maximum;
                            bar.Value = barValue;
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

            combo.Items.Add(new CharacterChoice(value, value.ToString()));
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
                    "Hair changes are verified after each write.");
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
