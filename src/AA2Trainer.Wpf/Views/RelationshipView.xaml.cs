using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace AA2Trainer.WpfPrototype.Views
{
    public partial class RelationshipView : UserControl
    {
        public RelationshipView()
        {
            InitializeComponent();
        }

        private void Love_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel viewModel = DataContext as MainViewModel;
            if (viewModel != null) viewModel.SetRelationshipFeeling(0);
        }

        private void Like_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel viewModel = DataContext as MainViewModel;
            if (viewModel != null) viewModel.SetRelationshipFeeling(1);
        }

        private void Dislike_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel viewModel = DataContext as MainViewModel;
            if (viewModel != null) viewModel.SetRelationshipFeeling(2);
        }

        private void Hate_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel viewModel = DataContext as MainViewModel;
            if (viewModel != null) viewModel.SetRelationshipFeeling(3);
        }

        private void Freeze_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel viewModel = DataContext as MainViewModel;
            if (viewModel != null) viewModel.ToggleRelationshipFreezeFromRow();
        }

        private void RelationshipGrid_PreviewMouseLeftButtonDown(
            object sender, MouseButtonEventArgs e)
        {
            DependencyObject source = e.OriginalSource as DependencyObject;
            while (source != null && !(source is DataGridRow))
                source = VisualTreeHelper.GetParent(source);

            DataGridRow row = source as DataGridRow;
            if (row == null) return;
            row.IsSelected = true;
            RelationshipGrid.SelectedItem = row.Item;
        }

        private void ApplyRelationship_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel viewModel = DataContext as MainViewModel;
            if (viewModel != null) viewModel.ApplyRelationshipEdits();
        }

        private void RevertRelationship_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel viewModel = DataContext as MainViewModel;
            if (viewModel != null) viewModel.RevertRelationshipEdits();
        }
    }
}
