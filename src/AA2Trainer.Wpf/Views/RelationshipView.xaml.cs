using System.Windows;
using System.Windows.Controls;

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
    }
}
