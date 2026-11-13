using System.IO;
using System.Windows;
using System.Windows.Controls;
using AA2Trainer;
using Microsoft.Win32;

namespace AA2Trainer.WpfPrototype.Views
{
    public partial class OptionsView : UserControl
    {
        public OptionsView()
        {
            InitializeComponent();
        }

        private MainViewModel ViewModel
        {
            get { return DataContext as MainViewModel; }
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null) ViewModel.ApplyGameOptions();
        }

        private void Revert_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null) ViewModel.RevertGameOptions();
        }

        private void Observe_Changed(object sender, RoutedEventArgs e)
        {
            CheckBox box = sender as CheckBox;
            if (box != null && ViewModel != null)
                ViewModel.SetPlannerObserve(box.IsChecked == true);
        }

        private void SoundUp_Click(object sender, RoutedEventArgs e)
        {
            NudgeSound(sender, 1);
        }

        private void SoundDown_Click(object sender, RoutedEventArgs e)
        {
            NudgeSound(sender, -1);
        }

        private static void NudgeSound(object sender, int delta)
        {
            FrameworkElement element = sender as FrameworkElement;
            SoundChannelItem channel = element == null
                ? null : element.DataContext as SoundChannelItem;
            if (channel == null || channel.Level == null) return;

            int value = channel.Level.Value + delta;
            if (value < channel.Level.Min) value = channel.Level.Min;
            if (value > channel.Level.Max) value = channel.Level.Max;
            channel.Level.Value = value;
        }

        private void Backup_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null) ViewModel.BackupCurrentSave();
        }

        private void Restore_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel viewModel = ViewModel;
            if (viewModel == null) return;

            RestoreBackupWindow window = new RestoreBackupWindow(
                viewModel.CurrentSavePath);
            window.Owner = Window.GetWindow(this);
            if (window.ShowDialog() != true || window.SelectedEntry == null)
                return;

            SaveBackupEntry entry = window.SelectedEntry;
            MessageBoxResult confirm = MessageBox.Show(
                "Restore this backup?\n\n" +
                "Backup: " + entry.BackupName + "\n" +
                "Save: " + entry.SaveName + "\n" +
                "AAU data: " + (entry.HasAauBackup ? "yes" : "no") +
                "\n\nClose AA2Play or return to the title screen before " +
                "restoring. The current save is backed up first.",
                "Restore save backup", MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;
            viewModel.RestoreBackup(entry);
        }

        private void UpdateNames_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel viewModel = ViewModel;
            if (viewModel == null) return;

            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Title = "Select a name-table XML (clothes, HEXAOC, or personalities)";
            dialog.Filter = "AA2 name XMLs (*.xml)|*.xml|All files (*.*)|*.*";
            string root = NameMap.FindGameRoot();
            if (!string.IsNullOrEmpty(root))
                dialog.InitialDirectory = Path.Combine(root, "data");
            if (dialog.ShowDialog() != true) return;

            viewModel.ImportNameTables(Path.GetDirectoryName(dialog.FileName));
        }

        private void ReloadNames_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null) ViewModel.ReloadNameTables();
        }
    }
}
