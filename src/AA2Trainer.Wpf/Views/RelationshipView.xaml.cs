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
            SelectRowUnderMouse(e.OriginalSource as DependencyObject);
        }

        private void RelationshipGrid_PreviewMouseRightButtonDown(
            object sender, MouseButtonEventArgs e)
        {
            SelectRowUnderMouse(e.OriginalSource as DependencyObject);
        }

        private void SelectRowUnderMouse(DependencyObject source)
        {
            while (source != null && !(source is DataGridRow))
                source = VisualTreeHelper.GetParent(source);

            DataGridRow row = source as DataGridRow;
            if (row == null) return;
            row.IsSelected = true;
            RelationshipGrid.SelectedItem = row.Item;
        }

        private void ContextLove_Click(object sender, RoutedEventArgs e)
        {
            SetSelectedFeeling(0);
        }

        private void ContextLike_Click(object sender, RoutedEventArgs e)
        {
            SetSelectedFeeling(1);
        }

        private void ContextDislike_Click(object sender, RoutedEventArgs e)
        {
            SetSelectedFeeling(2);
        }

        private void ContextHate_Click(object sender, RoutedEventArgs e)
        {
            SetSelectedFeeling(3);
        }

        private void SetSelectedFeeling(int feeling)
        {
            MainViewModel viewModel = DataContext as MainViewModel;
            if (viewModel != null) viewModel.SetRelationshipFeeling(feeling);
        }

        private void ContextFreeze_Click(object sender, RoutedEventArgs e)
        {
            SetSelectedFreeze(true);
        }

        private void ContextUnfreeze_Click(object sender, RoutedEventArgs e)
        {
            SetSelectedFreeze(false);
        }

        private void SetSelectedFreeze(bool frozen)
        {
            MainViewModel viewModel = DataContext as MainViewModel;
            if (viewModel != null)
                viewModel.SetRelationshipFreezeFromRow(frozen);
        }

        private void AllLove_Click(object sender, RoutedEventArgs e)
        {
            SetAllFeelings(0);
        }

        private void AllLike_Click(object sender, RoutedEventArgs e)
        {
            SetAllFeelings(1);
        }

        private void AllDislike_Click(object sender, RoutedEventArgs e)
        {
            SetAllFeelings(2);
        }

        private void AllHate_Click(object sender, RoutedEventArgs e)
        {
            SetAllFeelings(3);
        }

        private void SetAllFeelings(int feeling)
        {
            MainViewModel viewModel = DataContext as MainViewModel;
            if (viewModel != null) viewModel.SetAllRelationshipFeeling(feeling);
        }

        private void FreezeAll_Click(object sender, RoutedEventArgs e)
        {
            SetAllFreeze(true);
        }

        private void UnfreezeAll_Click(object sender, RoutedEventArgs e)
        {
            SetAllFreeze(false);
        }

        private void SetAllFreeze(bool frozen)
        {
            MainViewModel viewModel = DataContext as MainViewModel;
            if (viewModel != null) viewModel.SetAllRelationshipFreeze(frozen);
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
