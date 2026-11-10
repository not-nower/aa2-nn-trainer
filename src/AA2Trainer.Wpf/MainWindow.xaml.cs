using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace AA2Trainer.WpfPrototype
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel viewModel;
        private bool loaded;
        private bool railCollapsed;
        private bool rosterRailCollapsed = true;
        private bool logCollapsed;

        public MainWindow()
        {
            InitializeComponent();
            viewModel = new MainViewModel();
            DataContext = viewModel;
            ThemeCombo.SelectedIndex = (int)ThemeService.CurrentMode;
            SourceInitialized += delegate
            {
                ThemeService.ApplyWindowChrome(this, ThemeService.IsDark);
            };
            Loaded += delegate
            {
                loaded = true;
                ThemeService.Apply(ThemeService.CurrentMode, this);
                ApplyShellForTab();
            };
            Closed += delegate
            {
                viewModel.Dispose();
            };
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            viewModel.Refresh();
        }

        private void AttachButton_Click(object sender, RoutedEventArgs e)
        {
            viewModel.ToggleAttach();
        }

        private void SeatFacePreview_Loaded(object sender, RoutedEventArgs e)
        {
            Popup popup = sender as Popup;
            if (popup != null) popup.CustomPopupPlacementCallback = PlaceSeatPreview;
        }

        private static CustomPopupPlacement[] PlaceSeatPreview(
            Size popupSize, Size targetSize, Point offset)
        {
            const double gap = 8;
            return new[]
            {
                new CustomPopupPlacement(
                    new Point(targetSize.Width + gap, -20),
                    PopupPrimaryAxis.Horizontal),
                new CustomPopupPlacement(
                    new Point(-popupSize.Width - gap, -20),
                    PopupPrimaryAxis.Horizontal),
                new CustomPopupPlacement(
                    new Point(-20, targetSize.Height + gap),
                    PopupPrimaryAxis.Vertical),
                new CustomPopupPlacement(
                    new Point(-20, -popupSize.Height - gap),
                    PopupPrimaryAxis.Vertical)
            };
        }

        private void MainTabs_SelectionChanged(object sender,
            SelectionChangedEventArgs e)
        {
            ApplyShellForTab();
        }

        private void ApplyShellForTab()
        {
            if (MainTabs == null || SeatRailBorder == null ||
                SeatRailColumn == null || HeaderRow == null ||
                LogPanel == null || LogSplitterRow == null) return;

            bool roster = MainTabs.SelectedIndex == 0;
            bool gameplay = MainTabs.SelectedIndex == 2;

            // The classroom roster keeps the seat rail available, but it starts
            // collapsed so the 5x5 grid keeps the full width on first launch.
            bool railIsCollapsed = roster ? rosterRailCollapsed : railCollapsed;
            SeatRailBorder.Visibility = Visibility.Visible;
            SeatRailColumn.Width = new GridLength(railIsCollapsed ? 68 : 220);
            SeatSplitterColumn.Width = new GridLength(4);

            RailHeaderText.Visibility = railIsCollapsed
                ? Visibility.Collapsed : Visibility.Visible;
            RailFooterText.Visibility = railIsCollapsed
                ? Visibility.Collapsed : Visibility.Visible;
            CollapseRailButton.Content = railIsCollapsed ? "\uE76C" : "\uE76B";
            CollapseRailButton.ToolTip = railIsCollapsed
                ? "Expand seat rail" : "Collapse seat rail";

            HeaderRow.Height = roster
                ? new GridLength(0) : new GridLength(56);
            CharacterHeaderBorder.Visibility = roster
                ? Visibility.Collapsed : Visibility.Visible;

            LogPanel.Visibility = gameplay
                ? Visibility.Visible : Visibility.Collapsed;
            LogSplitterRow.Height = gameplay
                ? new GridLength(4) : new GridLength(0);
            LogRow.Height = gameplay
                ? new GridLength(logCollapsed ? 28 : 170)
                : new GridLength(0);
            LogContent.Visibility = gameplay && !logCollapsed
                ? Visibility.Visible : Visibility.Collapsed;
            CollapseLogButton.Content = logCollapsed ? "\uE70D" : "\uE70E";
            CollapseLogButton.ToolTip = logCollapsed
                ? "Expand log" : "Collapse log";

            MainViewModel viewModel = DataContext as MainViewModel;
            if (viewModel != null) viewModel.RailCollapsed = railIsCollapsed;
        }

        private void ThemeCombo_SelectionChanged(object sender,
            SelectionChangedEventArgs e)
        {
            if (!loaded) return;
            ThemeService.Apply((ThemeMode)ThemeCombo.SelectedIndex, this);
        }

        private void CollapseRail_Click(object sender, RoutedEventArgs e)
        {
            if (MainTabs.SelectedIndex == 0)
                rosterRailCollapsed = !rosterRailCollapsed;
            else
                railCollapsed = !railCollapsed;
            ApplyShellForTab();
        }

        private void LiveLogTab_Checked(object sender, RoutedEventArgs e)
        {
            if (LiveLogList == null || PlannerLogList == null) return;
            LiveLogList.Visibility = Visibility.Visible;
            PlannerLogList.Visibility = Visibility.Collapsed;
        }

        private void PlannerLogTab_Checked(object sender, RoutedEventArgs e)
        {
            if (LiveLogList == null || PlannerLogList == null) return;
            LiveLogList.Visibility = Visibility.Collapsed;
            PlannerLogList.Visibility = Visibility.Visible;
        }

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            ListBox list = LiveLogTab.IsChecked == true
                ? LiveLogList : PlannerLogList;
            System.Collections.IList items =
                list.ItemsSource as System.Collections.IList;
            if (items != null) items.Clear();
        }

        private void CollapseLog_Click(object sender, RoutedEventArgs e)
        {
            logCollapsed = !logCollapsed;
            ApplyShellForTab();
        }

        private void PlayAs_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel viewModel = DataContext as MainViewModel;
            if (viewModel != null) viewModel.PlayAs(viewModel.SelectedSeat);
        }

        private void ScheduleButton_Click(object sender, RoutedEventArgs e)
        {
            if (SchedulePopup != null)
                SchedulePopup.IsOpen = !SchedulePopup.IsOpen;
        }

        private void ApplySchedule_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel viewModel = DataContext as MainViewModel;
            if (viewModel != null) viewModel.ApplyScheduleEdits();
        }

        private void RevertSchedule_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel viewModel = DataContext as MainViewModel;
            if (viewModel != null) viewModel.RevertScheduleEdits();
        }

        private void TimeFreeze_Changed(object sender, RoutedEventArgs e)
        {
            CheckBox box = sender as CheckBox;
            MainViewModel viewModel = DataContext as MainViewModel;
            if (box != null && viewModel != null)
                viewModel.SetTimeFrozen(box.IsChecked == true);
        }
    }
}
