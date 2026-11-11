using System.Windows;
using System.Windows.Controls;

namespace AA2Trainer.WpfPrototype.Views
{
    public partial class LoverView : UserControl
    {
        public LoverView()
        {
            InitializeComponent();
        }

        private void SetLover_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel viewModel = DataContext as MainViewModel;
            if (viewModel != null) viewModel.SetLoverFromRow(true);
        }

        private void BreakUp_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel viewModel = DataContext as MainViewModel;
            if (viewModel != null) viewModel.SetLoverFromRow(false);
        }
    }
}
