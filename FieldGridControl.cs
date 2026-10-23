using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace AA2Trainer
{
    public sealed class GridRowData
    {
        public PointerDef Def;
        public EvalResult Result;
    }

    public sealed class GridRowEventArgs : EventArgs
    {
        public GridRowData Row;
        public GridRowEventArgs(GridRowData row) { Row = row; }
    }

    public sealed class FieldGridControl : UserControl
    {
        public DataGridView Grid { get; private set; }
        public bool HideOkRows { get; set; }
        public Func<PointerDef, EvalResult, string> ValueFormatter { get; set; }
        public event EventHandler<GridRowEventArgs> RowSelected;

        private List<PointerDef> defs = new List<PointerDef>();

        public FieldGridControl()
        {
            Grid = new DataGridView();
            Grid.Dock = DockStyle.Fill;
            Grid.BackgroundColor = Color.White;
            Grid.BorderStyle = BorderStyle.FixedSingle;
            Grid.RowHeadersVisible = false;
            Grid.AllowUserToAddRows = false;
            Grid.AllowUserToDeleteRows = false;
            Grid.AllowUserToResizeRows = false;
            Grid.ReadOnly = true;
            Grid.MultiSelect = false;
            Grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            Grid.AutoGenerateColumns = false;
            Grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            Grid.RowTemplate.Height = 20;
            Grid.ColumnHeadersHeight = 24;
            Grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            Grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            Grid.EnableHeadersVisualStyles = false;
            Grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(240, 240, 240);
            Grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            Grid.DefaultCellStyle.Font = new Font("Consolas", 8.5F);
            Grid.SelectionChanged += delegate
            {
                GridRowData data = SelectedRow;
                if (data != null && RowSelected != null)
                    RowSelected(this, new GridRowEventArgs(data));
            };

            AddColumn("Field", 170);
            AddColumn("Value", 260);
            Grid.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            Grid.Columns[1].MinimumWidth = 240;
            AddColumn("Address", 100);
            AddColumn("Status", 88);
            AddColumn("Chain", 130);
            DataGridViewTextBoxColumn type = new DataGridViewTextBoxColumn();
            type.HeaderText = "Type";
            type.Width = 58;
            type.SortMode = DataGridViewColumnSortMode.NotSortable;
            Grid.Columns.Add(type);

            Controls.Add(Grid);
        }

        private void AddColumn(string header, int width)
        {
            DataGridViewTextBoxColumn c = new DataGridViewTextBoxColumn();
            c.HeaderText = header;
            c.Width = width;
            c.SortMode = DataGridViewColumnSortMode.Automatic;
            Grid.Columns.Add(c);
        }

        public GridRowData SelectedRow
        {
            get
            {
                if (Grid.CurrentRow == null) return null;
                return Grid.CurrentRow.Tag as GridRowData;
            }
        }

        public void SetDefinitions(List<PointerDef> newDefs)
        {
            defs = newDefs == null ? new List<PointerDef>() : newDefs;
            Grid.SuspendLayout();
            try
            {
                Grid.Rows.Clear();
                foreach (PointerDef d in defs)
                {
                    int i = Grid.Rows.Add();
                    GridRowData data = new GridRowData();
                    data.Def = d;
                    Grid.Rows[i].Tag = data;
                    Grid.Rows[i].Cells[0].Value = d.Name;
                    Grid.Rows[i].Cells[4].Value = Evaluator.ChainText(d.Chain);
                    Grid.Rows[i].Cells[5].Value = Evaluator.TypeName(d.Type);
                }
            }
            finally
            {
                Grid.ResumeLayout();
            }
        }

        public void RefreshValues(GameMemory mem, GameContext ctx, int index)
        {
            if (Grid.Rows.Count != defs.Count)
            {
                SetDefinitions(defs);
            }

            string selectedKey = null;
            if (SelectedRow != null) selectedKey = SelectedRow.Def.Raw;

            Grid.SuspendLayout();
            try
            {
                for (int i = 0; i < Grid.Rows.Count && i < defs.Count; i++)
                {
                    DataGridViewRow row = Grid.Rows[i];
                    GridRowData data = row.Tag as GridRowData;
                    if (data == null) continue;
                    EvalResult r = Evaluator.Evaluate(mem, ctx, data.Def, index);
                    data.Result = r;
                    row.Cells[1].Value = ValueFormatter != null ? ValueFormatter(data.Def, r) : r.Value;
                    row.Cells[2].Value = r.HasAddress ? "0x" + r.Address.ToString("X8") : "";
                    row.Cells[3].Value = r.Status;
                    row.DefaultCellStyle.BackColor = DashboardData.StatusColor(r.Status);
                    row.Visible = !(HideOkRows && r.Status == "OK");
                }
            }
            finally
            {
                Grid.ResumeLayout();
            }

            if (selectedKey != null)
            {
                foreach (DataGridViewRow row in Grid.Rows)
                {
                    GridRowData data = row.Tag as GridRowData;
                    if (data != null && data.Def.Raw == selectedKey && row.Visible)
                    {
                        Grid.CurrentCell = row.Cells[0];
                        break;
                    }
                }
            }

            if (Grid.CurrentRow == null || !Grid.CurrentRow.Visible)
            {
                DataGridViewRow first = null;
                foreach (DataGridViewRow row in Grid.Rows)
                {
                    if (row.Visible)
                    {
                        first = row;
                        break;
                    }
                }
                if (first != null)
                    Grid.CurrentCell = first.Cells[0];
                else
                    Grid.ClearSelection();
            }
        }

        public void Clear()
        {
            defs = new List<PointerDef>();
            Grid.Rows.Clear();
        }
    }
}
