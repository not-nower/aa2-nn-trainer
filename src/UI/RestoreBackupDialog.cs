using System;
using System.Drawing;
using System.Windows.Forms;

namespace AA2Trainer
{
    /// <summary>
    /// Lists the timestamped .bak files that belong to a class save (or every
    /// class save in the same folder) and returns the one the user picks.
    /// The actual restore and confirmation are handled by OptionsControl.
    /// </summary>
    public sealed class RestoreBackupDialog : Form
    {
        private readonly string savePath;
        private ListView backupList;
        private CheckBox showAllCheck;
        private Label pathLabel;
        private Label statusLabel;
        private Button restoreButton;

        public SaveBackupEntry SelectedEntry { get; private set; }

        public RestoreBackupDialog(string savePath)
        {
            this.savePath = savePath;

            Text = "Restore Save Backup";
            Font = new Font("Segoe UI", 9F);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(760, 470);
            MinimumSize = new Size(620, 380);

            BuildUi();
            Populate();
        }

        private void BuildUi()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 4;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            root.Padding = new Padding(10, 8, 10, 8);
            Controls.Add(root);

            TableLayoutPanel top = new TableLayoutPanel();
            top.Dock = DockStyle.Fill;
            top.ColumnCount = 1;
            top.RowCount = 2;
            top.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            top.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));

            Label info = new Label();
            info.Dock = DockStyle.Fill;
            info.Text = "Choose a backup to restore. The timestamped file name records " +
                "the save it belongs to, so the backup is written back to that .sav. " +
                "Backups marked AAU also restore the AAUnlimited .json sidecar. " +
                "AA2Play must reload or restart after a restore.";
            info.TextAlign = ContentAlignment.MiddleLeft;

            pathLabel = new Label();
            pathLabel.Dock = DockStyle.Fill;
            pathLabel.AutoEllipsis = true;
            pathLabel.ForeColor = Color.DimGray;
            pathLabel.TextAlign = ContentAlignment.MiddleLeft;

            top.Controls.Add(info, 0, 0);
            top.Controls.Add(pathLabel, 0, 1);
            root.Controls.Add(top, 0, 0);

            backupList = new ListView();
            backupList.Dock = DockStyle.Fill;
            backupList.View = View.Details;
            backupList.FullRowSelect = true;
            backupList.MultiSelect = false;
            backupList.HideSelection = false;
            backupList.GridLines = false;
            backupList.Columns.Add("Created", 140, HorizontalAlignment.Left);
            backupList.Columns.Add("Save", 150, HorizontalAlignment.Left);
            backupList.Columns.Add("AAU", 50, HorizontalAlignment.Center);
            backupList.Columns.Add("Size", 75, HorizontalAlignment.Right);
            backupList.Columns.Add("Backup file", 300, HorizontalAlignment.Left);
            backupList.SelectedIndexChanged += delegate { UpdateRestoreButton(); };
            backupList.DoubleClick += delegate { AcceptSelection(); };
            root.Controls.Add(backupList, 0, 1);

            TableLayoutPanel options = new TableLayoutPanel();
            options.Dock = DockStyle.Fill;
            options.ColumnCount = 2;
            options.RowCount = 1;
            options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            options.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180F));
            options.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            showAllCheck = new CheckBox();
            showAllCheck.AutoSize = true;
            showAllCheck.Text = "Show backups for all saves in this folder";
            showAllCheck.Anchor = AnchorStyles.Left;
            showAllCheck.Checked = string.IsNullOrEmpty(savePath);
            showAllCheck.Enabled = !string.IsNullOrEmpty(savePath);
            showAllCheck.CheckedChanged += delegate { Populate(); };
            options.Controls.Add(showAllCheck, 0, 0);

            statusLabel = new Label();
            statusLabel.Dock = DockStyle.Fill;
            statusLabel.AutoEllipsis = true;
            statusLabel.ForeColor = Color.DimGray;
            statusLabel.TextAlign = ContentAlignment.MiddleRight;
            options.Controls.Add(statusLabel, 1, 0);
            root.Controls.Add(options, 0, 2);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.WrapContents = false;
            buttons.Padding = new Padding(0, 7, 0, 0);

            Button cancel = new Button();
            cancel.Text = "Cancel";
            cancel.Width = 96;
            cancel.DialogResult = DialogResult.Cancel;
            buttons.Controls.Add(cancel);

            restoreButton = new Button();
            restoreButton.Text = "Restore";
            restoreButton.Width = 96;
            restoreButton.Enabled = false;
            restoreButton.Click += delegate { AcceptSelection(); };
            buttons.Controls.Add(restoreButton);

            Button refresh = new Button();
            refresh.Text = "Refresh";
            refresh.Width = 96;
            refresh.Click += delegate { Populate(); };
            buttons.Controls.Add(refresh);

            root.Controls.Add(buttons, 0, 3);

            AcceptButton = restoreButton;
            CancelButton = cancel;
        }

        private void Populate()
        {
            backupList.BeginUpdate();
            backupList.Items.Clear();
            try
            {
                bool all = showAllCheck.Checked || string.IsNullOrEmpty(savePath);
                System.Collections.Generic.List<SaveBackupEntry> entries =
                    SaveBackup.List(savePath, all);
                foreach (SaveBackupEntry entry in entries)
                {
                    ListViewItem item = new ListViewItem(entry.Created ==
                        DateTime.MinValue ? "" :
                        entry.Created.ToString("yyyy-MM-dd HH:mm:ss"));
                    item.SubItems.Add(entry.SaveName);
                    item.SubItems.Add(entry.HasAauBackup ? "yes" : "no");
                    item.SubItems.Add(FormatSize(entry.Size));
                    item.SubItems.Add(entry.BackupName);
                    item.Tag = entry;
                    backupList.Items.Add(item);
                }
                statusLabel.Text = entries.Count == 0
                    ? "No matching backups found."
                    : entries.Count + (entries.Count == 1 ? " backup" : " backups");
            }
            finally
            {
                backupList.EndUpdate();
            }

            pathLabel.Text = string.IsNullOrEmpty(savePath)
                ? "No save selected; showing the class save folder."
                : "Current save: " + savePath;

            if (backupList.Items.Count > 0)
                backupList.Items[0].Selected = true;
            UpdateRestoreButton();
        }

        private void UpdateRestoreButton()
        {
            restoreButton.Enabled = backupList.SelectedItems.Count > 0;
        }

        private void AcceptSelection()
        {
            if (backupList.SelectedItems.Count == 0) return;
            SelectedEntry = backupList.SelectedItems[0].Tag as SaveBackupEntry;
            if (SelectedEntry == null) return;
            DialogResult = DialogResult.OK;
            Close();
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024)
                return (bytes / 1024.0).ToString("0.0") + " KB";
            return (bytes / (1024.0 * 1024.0)).ToString("0.0") + " MB";
        }
    }
}
