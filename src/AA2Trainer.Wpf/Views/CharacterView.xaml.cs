using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace AA2Trainer.WpfPrototype.Views
{
    public partial class CharacterView : UserControl
    {
        public CharacterView()
        {
            InitializeComponent();
        }

        private void ColorButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            if (button == null) return;
            Popup popup = button.Tag as Popup;
            if (popup == null)
            {
                FrameworkElement parent = button.Parent as FrameworkElement;
                if (parent != null)
                    popup = parent.FindName("ColorPopup") as Popup;
            }
            if (popup != null) popup.IsOpen = true;
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

        private void ApplyTraits_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel viewModel = DataContext as MainViewModel;
            if (viewModel != null) viewModel.ApplyTraitEdits();
        }

        private void RevertTraits_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel viewModel = DataContext as MainViewModel;
            if (viewModel != null) viewModel.RevertTraitEdits();
        }

        private void ApplyBody_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel viewModel = DataContext as MainViewModel;
            if (viewModel != null) viewModel.ApplyBodyEdits();
        }

        private void RevertBody_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel viewModel = DataContext as MainViewModel;
            if (viewModel != null) viewModel.RevertBodyEdits();
        }
    }
}
