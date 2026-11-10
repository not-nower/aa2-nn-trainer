using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AA2Trainer.WpfPrototype.Views
{
    public partial class RosterView : UserControl
    {
        public RosterView()
        {
            InitializeComponent();
            RosterList.PreviewKeyDown += RosterList_PreviewKeyDown;
        }

        /// <summary>
        /// Arrow keys move by classroom position, not by seat index, so the
        /// right-to-left column layout stays spatially intuitive.
        /// </summary>
        private void RosterList_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            MainViewModel viewModel = DataContext as MainViewModel;
            SeatItem current = RosterList == null
                ? null : RosterList.SelectedItem as SeatItem;
            if (viewModel == null || current == null) return;

            int row;
            int column;
            if (!ClassroomRosterPanel.TryGetPosition(
                current.SeatIndex, out row, out column)) return;

            switch (e.Key)
            {
                case Key.Up: row--; break;
                case Key.Down: row++; break;
                case Key.Left: column--; break;
                case Key.Right: column++; break;
                default: return;
            }

            int seat = ClassroomRosterPanel.SeatAt(row, column);
            e.Handled = true;
            if (seat < 0) return;

            SeatItem target = viewModel.Seats.FirstOrDefault(
                item => item.SeatIndex == seat);
            if (target == null || !target.HasName || target.IsFilteredOut) return;

            RosterList.SelectedItem = target;
        }
    }
}
