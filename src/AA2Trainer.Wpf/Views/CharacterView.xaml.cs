using System.Windows;
using System.Windows.Controls;

namespace AA2Trainer.WpfPrototype.Views
{
    public partial class CharacterView : UserControl
    {
        public CharacterView()
        {
            InitializeComponent();
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel viewModel = DataContext as MainViewModel;
            if (viewModel != null) viewModel.ApplyCharacterEdits();
        }

        private void Revert_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel viewModel = DataContext as MainViewModel;
            if (viewModel != null) viewModel.RevertCharacterEdits();
        }
    }
}
