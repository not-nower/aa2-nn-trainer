using System;
using System.Collections.Specialized;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace AA2Trainer.WpfPrototype
{
    public partial class MainWindow : Window
    {
        private const int SmCxScreen = 0;
        private const int SmCyScreen = 1;
        private const int SmXVirtualScreen = 76;
        private const int SmYVirtualScreen = 77;
        private const int SmCxVirtualScreen = 78;
        private const int SmCyVirtualScreen = 79;
        private const uint SwpNoZOrder = 0x0004;
        private const uint SwpNoActivate = 0x0010;

        private readonly MainViewModel viewModel;
        private bool loaded;
        private bool railCollapsed;
        private bool rosterRailCollapsed = true;
        private bool logCollapsed;
        private bool hasSavedBounds;
        private int savedLeft;
        private int savedTop;
        private int savedWidth;
        private int savedHeight;
        private ScrollViewer liveLogScroll;
        private ScrollViewer plannerLogScroll;
        private bool liveLogAutoScroll = true;
        private bool plannerLogAutoScroll = true;
        private bool liveLogScrollQueued;
        private bool plannerLogScrollQueued;

        public MainWindow()
        {
            InitializeComponent();
            viewModel = new MainViewModel();
            DataContext = viewModel;

            TrainerSettings settings = TrainerSettings.Current;
            railCollapsed = settings.RailCollapsed;
            rosterRailCollapsed = settings.RosterRailCollapsed;
            logCollapsed = settings.LogCollapsed;
            ThemeCombo.SelectedIndex = (int)settings.Theme;

            hasSavedBounds = TryGetSavedWindowBounds(settings,
                out savedLeft, out savedTop, out savedWidth, out savedHeight);
            if (hasSavedBounds)
                WindowStartupLocation = WindowStartupLocation.Manual;

            SourceInitialized += delegate
            {
                ThemeService.ApplyWindowChrome(this, ThemeService.IsDark);
                ApplySavedWindowBounds();
            };
            Loaded += delegate
            {
                loaded = true;
                ThemeService.Apply(ThemeService.CurrentMode, this);
                ApplyShellForTab();
            };
            Closing += delegate
            {
                SaveWindowState();
            };
            Closed += delegate
            {
                viewModel.Dispose();
            };
        }

        private static bool TryGetSavedWindowBounds(TrainerSettings settings,
            out int left, out int top, out int width, out int height)
        {
            left = 0;
            top = 0;
            width = 0;
            height = 0;

            // A zero size means this settings file has no window bounds yet.
            if (settings.WindowWidth < 1 || settings.WindowHeight < 1)
                return false;

            int virtualLeft = GetSystemMetrics(SmXVirtualScreen);
            int virtualTop = GetSystemMetrics(SmYVirtualScreen);
            int virtualWidth = GetSystemMetrics(SmCxVirtualScreen);
            int virtualHeight = GetSystemMetrics(SmCyVirtualScreen);
            if (virtualWidth <= 0 || virtualHeight <= 0)
                return false;

            // Never restore a window larger than the whole desktop.
            width = (int)Math.Min(settings.WindowWidth, virtualWidth);
            height = (int)Math.Min(settings.WindowHeight, virtualHeight);
            if (width < 320 || height < 240)
                return false;

            Rect screen = new Rect(virtualLeft, virtualTop,
                virtualWidth, virtualHeight);
            Rect proposed = new Rect(settings.WindowLeft, settings.WindowTop,
                width, height);
            Rect visible = Rect.Intersect(proposed, screen);

            // A detached second monitor leaves no usable intersection. Keep
            // the restored size, but center it on the primary screen.
            if (visible.IsEmpty || visible.Width < 160 || visible.Height < 48)
            {
                left = Math.Max(0,
                    (GetSystemMetrics(SmCxScreen) - width) / 2);
                top = Math.Max(0,
                    (GetSystemMetrics(SmCyScreen) - height) / 2);
                return true;
            }

            left = (int)settings.WindowLeft;
            top = (int)settings.WindowTop;
            return true;
        }

        private void ApplySavedWindowBounds()
        {
            TrainerSettings settings = TrainerSettings.Current;
            IntPtr handle = new WindowInteropHelper(this).Handle;

            if (hasSavedBounds && handle != IntPtr.Zero)
            {
                SetWindowPos(handle, IntPtr.Zero, savedLeft, savedTop,
                    savedWidth, savedHeight, SwpNoZOrder | SwpNoActivate);
            }

            if (settings.WindowMaximized)
                WindowState = WindowState.Maximized;
        }

        private void SaveWindowState()
        {
            TrainerSettings settings = TrainerSettings.Current;
            settings.WindowMaximized = WindowState == WindowState.Maximized;

            IntPtr handle = new WindowInteropHelper(this).Handle;
            WINDOWPLACEMENT placement = new WINDOWPLACEMENT();
            placement.Length = Marshal.SizeOf(typeof(WINDOWPLACEMENT));
            if (handle != IntPtr.Zero &&
                GetWindowPlacement(handle, ref placement))
            {
                RECT bounds = placement.NormalPosition;
                int width = bounds.Right - bounds.Left;
                int height = bounds.Bottom - bounds.Top;
                if (width > 0 && height > 0)
                {
                    settings.WindowLeft = bounds.Left;
                    settings.WindowTop = bounds.Top;
                    settings.WindowWidth = width;
                    settings.WindowHeight = height;
                }
            }

            PersistShellState(settings);
        }

        private void PersistShellState()
        {
            PersistShellState(TrainerSettings.Current);
        }

        private void PersistShellState(TrainerSettings settings)
        {
            settings.RailCollapsed = railCollapsed;
            settings.RosterRailCollapsed = rosterRailCollapsed;
            settings.LogCollapsed = logCollapsed;
            settings.Save();
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

            ThemeMode mode = (ThemeMode)ThemeCombo.SelectedIndex;
            ThemeService.Apply(mode, this);
            if (TrainerSettings.Current.Theme == mode) return;
            TrainerSettings.Current.Theme = mode;
            TrainerSettings.Current.Save();
        }

        private void CollapseRail_Click(object sender, RoutedEventArgs e)
        {
            if (MainTabs.SelectedIndex == 0)
                rosterRailCollapsed = !rosterRailCollapsed;
            else
                railCollapsed = !railCollapsed;
            ApplyShellForTab();
            PersistShellState();
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
            bool live = LiveLogTab.IsChecked == true;
            MainViewModel vm = DataContext as MainViewModel;
            if (vm != null) vm.ClearLog(!live);
            if (live) liveLogAutoScroll = true;
            else plannerLogAutoScroll = true;
        }

        private void SaveLog_Click(object sender, RoutedEventArgs e)
        {
            MainViewModel vm = DataContext as MainViewModel;
            if (vm == null) return;

            bool live = LiveLogTab.IsChecked == true;
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Title = live ? "Save live events log" : "Save planner log";
            dialog.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
            dialog.DefaultExt = "txt";
            dialog.AddExtension = true;
            dialog.FileName = live ? "AA2LiveEvents.txt" : "AA2PlannerLog.txt";
            dialog.InitialDirectory = Environment.GetFolderPath(
                Environment.SpecialFolder.MyDocuments);
            if (dialog.ShowDialog() != true) return;

            try
            {
                StringBuilder text = new StringBuilder();
                var lines = live ? vm.LiveLines : vm.PlannerLines;
                foreach (LogLine line in lines)
                    text.AppendLine(line.Time + "  " + line.Text);
                File.WriteAllText(dialog.FileName, text.ToString());

                string message = "Saved " +
                    (live ? "live events" : "planner") + " log: " +
                    dialog.FileName;
                if (live) vm.LogSession(message);
                else vm.LogPlannerMessage(message);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    "Could not save the log.\n\n" + ex.Message,
                    "Save log", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CollapseLog_Click(object sender, RoutedEventArgs e)
        {
            logCollapsed = !logCollapsed;
            ApplyShellForTab();
            PersistShellState();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            MainViewModel vm = DataContext as MainViewModel;
            if (vm == null) return;
            vm.LiveLines.CollectionChanged += LiveLines_CollectionChanged;
            vm.PlannerLines.CollectionChanged += PlannerLines_CollectionChanged;
            AttachLogScrolling();
        }

        private void AttachLogScrolling()
        {
            liveLogScroll = FindScrollViewer(LiveLogList);
            if (liveLogScroll != null)
                liveLogScroll.ScrollChanged += LiveLogScroll_ScrollChanged;
            plannerLogScroll = FindScrollViewer(PlannerLogList);
            if (plannerLogScroll != null)
                plannerLogScroll.ScrollChanged += PlannerLogScroll_ScrollChanged;
        }

        private static ScrollViewer FindScrollViewer(DependencyObject root)
        {
            if (root == null) return null;
            ScrollViewer viewer = root as ScrollViewer;
            if (viewer != null) return viewer;

            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                ScrollViewer found = FindScrollViewer(
                    VisualTreeHelper.GetChild(root, i));
                if (found != null) return found;
            }
            return null;
        }

        private void LiveLogScroll_ScrollChanged(object sender,
            ScrollChangedEventArgs e)
        {
            // Extent changes come from new lines; only user scrolls should
            // change the auto-scroll preference.
            if (e.ExtentHeightChange == 0)
                liveLogAutoScroll = IsAtBottom(liveLogScroll);
        }

        private void PlannerLogScroll_ScrollChanged(object sender,
            ScrollChangedEventArgs e)
        {
            if (e.ExtentHeightChange == 0)
                plannerLogAutoScroll = IsAtBottom(plannerLogScroll);
        }

        private static bool IsAtBottom(ScrollViewer viewer)
        {
            return viewer != null &&
                viewer.VerticalOffset >= viewer.ScrollableHeight - 1.0;
        }

        private void LiveLines_CollectionChanged(object sender,
            NotifyCollectionChangedEventArgs e)
        {
            if (!liveLogAutoScroll || liveLogScrollQueued) return;
            liveLogScrollQueued = true;
            Dispatcher.BeginInvoke(new Action(delegate
            {
                liveLogScrollQueued = false;
                if (liveLogAutoScroll) ScrollLogToEnd(LiveLogList);
            }), DispatcherPriority.Background);
        }

        private void PlannerLines_CollectionChanged(object sender,
            NotifyCollectionChangedEventArgs e)
        {
            if (!plannerLogAutoScroll || plannerLogScrollQueued) return;
            plannerLogScrollQueued = true;
            Dispatcher.BeginInvoke(new Action(delegate
            {
                plannerLogScrollQueued = false;
                if (plannerLogAutoScroll) ScrollLogToEnd(PlannerLogList);
            }), DispatcherPriority.Background);
        }

        private static void ScrollLogToEnd(ListBox list)
        {
            if (list == null || list.Items.Count == 0) return;
            list.ScrollIntoView(list.Items[list.Items.Count - 1]);
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

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWPLACEMENT
        {
            public int Length;
            public int Flags;
            public int ShowCmd;
            public POINT MinPosition;
            public POINT MaxPosition;
            public RECT NormalPosition;
        }

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int index);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter,
            int x, int y, int width, int height, uint flags);

        [DllImport("user32.dll")]
        private static extern bool GetWindowPlacement(IntPtr hWnd,
            ref WINDOWPLACEMENT placement);
    }
}
