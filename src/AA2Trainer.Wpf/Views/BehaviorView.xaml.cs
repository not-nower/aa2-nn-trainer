using System.Windows;
using System.Windows.Controls;

namespace AA2Trainer.WpfPrototype.Views
{
    public partial class BehaviorView : UserControl
    {
        public BehaviorView()
        {
            InitializeComponent();
        }

        private MainViewModel ViewModel
        {
            get { return DataContext as MainViewModel; }
        }

        private void ApplyBehavior_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null) ViewModel.ApplyBehaviorEdits();
        }

        private void RevertBehavior_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null) ViewModel.RevertBehaviorEdits();
        }

        private void AllLove_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null) ViewModel.SetAllBehaviorFeelings(0);
        }

        private void AllLike_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null) ViewModel.SetAllBehaviorFeelings(1);
        }

        private void AllDislike_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null) ViewModel.SetAllBehaviorFeelings(2);
        }

        private void AllHate_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null) ViewModel.SetAllBehaviorFeelings(3);
        }
    }
}
