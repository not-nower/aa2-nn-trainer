using System.Windows;
using System.Windows.Controls;

namespace AA2Trainer.WpfPrototype
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel viewModel;
        private bool loaded;
        private bool railCollapsed;
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

            // Roster is a focused 5x5 view: the seat rail, character header
            // and log drawer would steal the height the portraits need.
            SeatRailBorder.Visibility = roster
                ? Visibility.Collapsed : Visibility.Visible;
            SeatRailColumn.Width = roster
                ? new GridLength(0)
                : new GridLength(railCollapsed ? 52 : 220);
            SeatSplitterColumn.Width = roster
                ? new GridLength(0) : new GridLength(4);

            if (!roster)
            {
                RailHeaderText.Visibility = railCollapsed
                    ? Visibility.Collapsed : Visibility.Visible;
                RailFooterText.Visibility = railCollapsed
                    ? Visibility.Collapsed : Visibility.Visible;
                CollapseRailButton.Content = railCollapsed ? "\uE76C" : "\uE76B";
                CollapseRailButton.ToolTip = railCollapsed
                    ? "Expand seat rail" : "Collapse seat rail";
            }

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
            if (viewModel != null) viewModel.RailCollapsed = railCollapsed && !roster;
        }

        private void ThemeCombo_SelectionChanged(object sender,
            SelectionChangedEventArgs e)
        {
            if (!loaded) return;
            ThemeService.Apply((ThemeMode)ThemeCombo.SelectedIndex, this);
        }

        private void CollapseRail_Click(object sender, RoutedEventArgs e)
        {
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
    }
}
