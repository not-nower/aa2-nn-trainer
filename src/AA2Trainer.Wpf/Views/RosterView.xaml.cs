using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace AA2Trainer.WpfPrototype.Views
{
    public partial class RosterView : UserControl
    {
        public RosterView()
        {
            InitializeComponent();
            RosterList.PreviewKeyDown += RosterList_PreviewKeyDown;
        }

        private void RosterList_MouseDoubleClick(object sender,
            MouseButtonEventArgs e)
        {
            MainViewModel viewModel = DataContext as MainViewModel;
            if (viewModel == null) return;

            SeatItem seat = SeatFromSource(e.OriginalSource as DependencyObject);
            if (seat == null) seat = RosterList.SelectedItem as SeatItem;
            if (seat == null || !seat.HasName) return;

            e.Handled = true;
            viewModel.OpenInCharacter(seat);
        }

        private void RosterList_PreviewMouseRightButtonDown(object sender,
            MouseButtonEventArgs e)
        {
            DependencyObject source = e.OriginalSource as DependencyObject;
            ListBoxItem item = ItemsControl.ContainerFromElement(
                RosterList, source) as ListBoxItem;
            if (item == null) return;

            SeatItem seat = item.DataContext as SeatItem;
            if (seat == null || !seat.HasName) return;

            Border card = FindCardBorder(item);
            if (card == null || card.ContextMenu == null) return;

            card.ContextMenu.PlacementTarget = card;
            card.ContextMenu.Placement = PlacementMode.MousePoint;
            card.ContextMenu.IsOpen = true;
            e.Handled = true;
        }

        private static Border FindCardBorder(DependencyObject root)
        {
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                Border border = child as Border;
                if (border != null && border.ContextMenu != null)
                    return border;

                Border nested = FindCardBorder(child);
                if (nested != null) return nested;
            }
            return null;
        }

        private static SeatItem SeatFromSource(DependencyObject source)
        {
            while (source != null)
            {
                FrameworkElement element = source as FrameworkElement;
                if (element != null && element.DataContext is SeatItem)
                    return element.DataContext as SeatItem;
                source = VisualTreeHelper.GetParent(source);
            }
            return null;
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

        private static SeatItem SeatFrom(object sender)
        {
            FrameworkElement element = sender as FrameworkElement;
            return element == null ? null : element.DataContext as SeatItem;
        }

        private void PlayAs_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel viewModel = DataContext as MainViewModel;
            if (viewModel != null) viewModel.PlayAs(SeatFrom(sender));
        }

        private void OpenInCharacter_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel viewModel = DataContext as MainViewModel;
            if (viewModel != null) viewModel.OpenInCharacter(SeatFrom(sender));
        }

        private void SetLover_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel viewModel = DataContext as MainViewModel;
            if (viewModel != null) viewModel.SetLover(SeatFrom(sender), true);
        }

        private void BreakUp_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel viewModel = DataContext as MainViewModel;
            if (viewModel != null) viewModel.SetLover(SeatFrom(sender), false);
        }

        private void SetLove_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel viewModel = DataContext as MainViewModel;
            if (viewModel != null) viewModel.SetRelationship(SeatFrom(sender), 0);
        }

        private void SetLike_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel viewModel = DataContext as MainViewModel;
            if (viewModel != null) viewModel.SetRelationship(SeatFrom(sender), 1);
        }

        private void SetDislike_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel viewModel = DataContext as MainViewModel;
            if (viewModel != null) viewModel.SetRelationship(SeatFrom(sender), 2);
        }

        private void SetHate_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel viewModel = DataContext as MainViewModel;
            if (viewModel != null) viewModel.SetRelationship(SeatFrom(sender), 3);
        }

        private void ToggleFreeze_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel viewModel = DataContext as MainViewModel;
            if (viewModel != null)
                viewModel.ToggleRelationshipFreeze(SeatFrom(sender));
        }
    }
}
