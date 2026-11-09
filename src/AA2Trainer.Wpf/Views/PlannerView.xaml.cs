using System.Windows;
using System.Windows.Controls;

namespace AA2Trainer.WpfPrototype.Views
{
    public partial class PlannerView : UserControl
    {
        public PlannerView()
        {
            InitializeComponent();
        }

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            PrototypeNote.Text = "Prototype: the real plan runner is wired in M3.";
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            PrototypeNote.Text = "Prototype: plan is idle.";
        }
    }
}
