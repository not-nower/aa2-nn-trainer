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
            CharacterNote.Text = "Prototype: writes are wired in M2.";
        }

        private void Revert_Click(object sender, RoutedEventArgs e)
        {
            CharacterNote.Text = "Prototype: revert is wired in M2.";
        }
    }
}
