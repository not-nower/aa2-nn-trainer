using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AA2Trainer;

namespace AA2Trainer.WpfPrototype.Views
{
    public partial class RestoreBackupWindow : Window
    {
        private readonly string currentSavePath;
        private readonly ObservableCollection<SaveBackupEntry> entries =
            new ObservableCollection<SaveBackupEntry>();

        public RestoreBackupWindow(string currentSavePath)
        {
            InitializeComponent();
            this.currentSavePath = currentSavePath ?? "";
            BackupGrid.ItemsSource = entries;
            ShowAllCheck.IsChecked = string.IsNullOrEmpty(this.currentSavePath);
            RefreshList();
            Loaded += delegate
            {
                if (entries.Count > 0) BackupGrid.SelectedIndex = 0;
            };
        }

        public SaveBackupEntry SelectedEntry { get; private set; }

        private void RefreshList()
        {
            entries.Clear();
            bool showAll = ShowAllCheck.IsChecked == true;
            foreach (SaveBackupEntry entry in SaveBackup.List(
                currentSavePath, showAll))
                entries.Add(entry);

            if (entries.Count == 0)
            {
                StatusText.Text = "No backups found for this save.";
            }
            else
            {
                int aau = 0;
                foreach (SaveBackupEntry entry in entries)
                {
                    if (entry.HasAauBackup) aau++;
                }
                StatusText.Text = entries.Count +
                    (entries.Count == 1 ? " backup" : " backups") +
                    (aau == 0 ? "." : "; " + aau + " include AAU data.");
            }
            RestoreButton.IsEnabled = BackupGrid.SelectedItem != null;
        }

        private void ShowAll_Changed(object sender, RoutedEventArgs e)
        {
            RefreshList();
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshList();
        }

        private void BackupGrid_SelectionChanged(object sender,
            SelectionChangedEventArgs e)
        {
            RestoreButton.IsEnabled = BackupGrid.SelectedItem != null;
        }

        private void BackupGrid_MouseDoubleClick(object sender,
            MouseButtonEventArgs e)
        {
            if (BackupGrid.SelectedItem != null) AcceptSelection();
        }

        private void Restore_Click(object sender, RoutedEventArgs e)
        {
            AcceptSelection();
        }

        private void AcceptSelection()
        {
            SaveBackupEntry entry = BackupGrid.SelectedItem as SaveBackupEntry;
            if (entry == null) return;
            SelectedEntry = entry;
            DialogResult = true;
        }
    }
}
