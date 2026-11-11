using System.IO;
using System.Windows;
using System.Windows.Controls;
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

        private void Backup_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null) ViewModel.BackupCurrentSave();
        }

        private void Restore_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel viewModel = ViewModel;
            if (viewModel == null || string.IsNullOrEmpty(viewModel.CurrentSavePath))
                return;

            MessageBoxResult confirm = MessageBox.Show(
                "Restore a backup over the current class save?\n\n" +
                "Close AA2Play or return to the title screen before restoring. " +
                "The current save is backed up first.",
                "Restore save backup", MessageBoxButton.OKCancel,
                MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.OK) return;

            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Title = "Select a class-save backup";
            dialog.Filter = "Save backups (*.bak)|*.bak|All files (*.*)|*.*";
            dialog.InitialDirectory = Path.GetDirectoryName(
                viewModel.CurrentSavePath);
            if (dialog.ShowDialog() != true) return;
            viewModel.RestoreBackup(dialog.FileName);
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
