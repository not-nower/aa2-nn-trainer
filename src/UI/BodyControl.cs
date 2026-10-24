using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace AA2Trainer
{
    public sealed class BodyControl : UserControl
    {
        public event EventHandler CharacterSaved;

        // Set by the main form. Gives the editor a chance to re-run the
        // fail-safe health check immediately before a write.
        public Func<bool> ConfirmWriteAllowed { get; set; }

        private const int RowHeight = 26;
        private const int LabelWidth = 94;

        private GameMemory mem;
        private CharacterInfo character;
        private uint loadedPointer;
        private int[] loaded;
        private bool gateAllowed;
        private bool applying;

        private readonly Control[] editors;
        private Dictionary<string, List<CharacterChoice>> choices =
            new Dictionary<string, List<CharacterChoice>>(StringComparer.Ordinal);
        private string choiceSignature = "";

        private Button applyButton;
        private Button revertButton;
        private Label gateLabel;
        private Label statusLabel;
        private ToolTip tooltip;

        public BodyControl()
        {
            DoubleBuffered = true;
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(248, 249, 251);
            editors = new Control[BodyEditor.Fields.Count];
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

            TableLayoutPanel columns = new TableLayoutPanel();
            columns.Dock = DockStyle.Fill;
            columns.ColumnCount = 3;
            columns.RowCount = 1;
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32F));
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34F));
            columns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34F));
            columns.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            columns.Margin = new Padding(0);
            root.Controls.Add(columns, 0, 0);

            columns.Controls.Add(BuildGroup(BodyGroup.Body, "Body", new Padding(0, 0, 6, 0)), 0, 0);
            columns.Controls.Add(BuildGroup(BodyGroup.Breasts, "Breasts", new Padding(6, 0, 6, 0)), 1, 0);
            columns.Controls.Add(BuildGroup(BodyGroup.Skin, "Skin and hair", new Padding(6, 0, 0, 0)), 2, 0);

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
                "Body changes are written only when the known game build and all pointer checks pass.");

            UpdateGate();
            SetStatus("Attach to the game and select a character.", false);
        }

        private GroupBox BuildGroup(BodyGroup group, string title, Padding margin)
        {
            List<int> indexes = new List<int>();
            for (int i = 0; i < BodyEditor.Fields.Count; i++)
                if (BodyEditor.Fields[i].Group == group) indexes.Add(i);

            GroupBox box = new GroupBox();
            box.Text = title;
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
            // Absorb leftover height so the rows keep their spacing.
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            box.Controls.Add(table);

            for (int r = 0; r < indexes.Count; r++)
            {
                int index = indexes[r];
                BodyFieldDef def = BodyEditor.Fields[index];

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

        private string TipFor(BodyFieldDef def)
        {
            if (!string.IsNullOrEmpty(def.Tip)) return AlignColumns(def.Tip);
            return def.Name + "   (char + 0x" + def.Offset.ToString("X") + ")";
        }

        // Pads the first column of a two-column tip so the ranges line up in
        // a proportional tooltip font.
        private string AlignColumns(string text)
        {
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            if (lines.Length < 2) return text;

            // The tooltip window renders with the system tooltip font, which is
            // the small shell font rather than the form font.
            Font font = Control.DefaultFont;
            int spaceWidth = Math.Max(1, TextRenderer.MeasureText(" ", font).Width);
            int columnWidth = 0;

            string[][] cells = new string[lines.Length][];
            for (int i = 0; i < lines.Length; i++)
            {
                cells[i] = lines[i].Split(new string[] { "  " }, 2, StringSplitOptions.None);
                if (cells[i].Length == 2)
                {
                    int width = TextRenderer.MeasureText(cells[i][0], font).Width;
                    if (width > columnWidth) columnWidth = width;
                }
            }

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < cells.Length; i++)
            {
                if (i > 0) sb.Append('\n');
                if (cells[i].Length != 2)
                {
                    sb.Append(cells[i][0]);
                    continue;
                }

                int width = TextRenderer.MeasureText(cells[i][0], font).Width;
                int padding = (int)Math.Ceiling((columnWidth - width) / (double)spaceWidth) + 2;
                sb.Append(cells[i][0]);
                sb.Append(' ', padding);
                sb.Append(cells[i][1]);
            }
            return sb.ToString();
        }

        private Control MakeEditor(int index, BodyFieldDef def)
        {
            if (def.Kind == BodyFieldKind.Choice)
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

            if (def.Kind == BodyFieldKind.Color)
            {
                Button button = new Button();
                button.Dock = DockStyle.Fill;
                button.Margin = new Padding(0, 3, 0, 3);
                button.FlatStyle = FlatStyle.Flat;
                button.Tag = index;
                button.Text = "#000000";
                button.Click += Color_Click;
                tooltip.SetToolTip(button, TipFor(def));
                return button;
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
                dialog.Color = button.BackColor;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                int argb = dialog.Color.ToArgb();
                button.Tag = index;
                SetColorButton(button, argb);
            }
        }

        private static void SetColorButton(Button button, int argb)
        {
            Color color = Color.FromArgb(255, (argb >> 16) & 0xFF, (argb >> 8) & 0xFF, argb & 0xFF);
            button.BackColor = color;
            button.Text = string.Format("#{0:X6}", argb & 0xFFFFFF);

            int brightness = (color.R * 299 + color.G * 587 + color.B * 114) / 1000;
            button.ForeColor = brightness > 140 ? Color.Black : Color.White;
        }

        public void SetChoices(Dictionary<string, List<CharacterChoice>> source, string signature)
        {
            if (source == null) source = new Dictionary<string, List<CharacterChoice>>();
            if (string.Equals(signature ?? "", choiceSignature, StringComparison.Ordinal)) return;

            choiceSignature = signature ?? "";
            choices = source;

            for (int i = 0; i < BodyEditor.Fields.Count; i++)
            {
                BodyFieldDef def = BodyEditor.Fields[i];
                if (def.Kind != BodyFieldKind.Choice) continue;

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
                    combo.Items.Add(new CharacterChoice(current, current.ToString("D3") + "  (unnamed)"));
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
                if (BodyEditor.TryRead(mem, info, out values, out error))
                {
                    bool changed = !sameCharacter || loaded == null || !ValuesEqual(values, loaded);
                    if (changed) ApplyValues(values);
                    loaded = values;
                    loadedPointer = info.Pointer;

                    if (!sameCharacter)
                        SetStatus("Loaded the body values from live memory.", false);
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

            int[] edited = ReadUi();
            if (loaded != null && ValuesEqual(edited, loaded))
            {
                SetStatus("No changes to apply.", false);
                return;
            }

            string changed;
            string error;
            if (!BodyEditor.TryWrite(mem, character, loaded, edited, out changed, out error))
            {
                SetStatus(error, true);
                UpdateGate();
                return;
            }

            int[] check;
            string readError;
            if (BodyEditor.TryRead(mem, character, out check, out readError))
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
            if (!BodyEditor.TryRead(mem, character, out values, out error))
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
                BodyFieldDef def = BodyEditor.Fields[i];
                if (def.Kind == BodyFieldKind.Choice)
                {
                    ComboBox combo = editors[i] as ComboBox;
                    values[i] = combo == null ? 0 : SelectedValue(combo, 0);
                }
                else if (def.Kind == BodyFieldKind.Color)
                {
                    Button button = editors[i] as Button;
                    if (button == null) { values[i] = 0; continue; }
                    Color color = button.BackColor;
                    values[i] = (0xFF << 24) | (color.R << 16) | (color.G << 8) | color.B;
                }
                else
                {
                    NumericUpDown number = editors[i] as NumericUpDown;
                    values[i] = number == null ? 0 : (int)number.Value;
                }
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
                    BodyFieldDef def = BodyEditor.Fields[i];
                    int value = values != null ? values[i] : 0;

                    if (def.Kind == BodyFieldKind.Choice)
                    {
                        ComboBox combo = editors[i] as ComboBox;
                        if (combo == null) continue;
                        SelectValue(combo, value);
                    }
                    else if (def.Kind == BodyFieldKind.Color)
                    {
                        Button button = editors[i] as Button;
                        if (button != null) SetColorButton(button, value);
                    }
                    else
                    {
                        NumericUpDown number = editors[i] as NumericUpDown;
                        if (number == null) continue;
                        if (value < number.Minimum) value = (int)number.Minimum;
                        if (value > number.Maximum) value = (int)number.Maximum;
                        number.Value = value;
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

            if (combo.Items.Count > 0) combo.SelectedIndex = 0;
        }

        private void UpdateGate()
        {
            bool attached = mem != null && mem.IsAttached;
            bool hasCharacter = attached && character != null;
            bool writable = gateAllowed && hasCharacter && mem.CanWrite;

            for (int i = 0; i < editors.Length; i++)
                if (editors[i] != null) editors[i].Enabled = writable;

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
                    "Body changes are verified after each write.");
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
