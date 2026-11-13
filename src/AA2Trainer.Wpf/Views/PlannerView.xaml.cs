using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace AA2Trainer.WpfPrototype.Views
{
    public partial class PlannerView : UserControl
    {
        private const string PlannerStepFormat = "AA2PlannerStep";
        private Point plannerDragStart;
        private int plannerDragIndex = -1;
        private bool plannerDragArmed;

        public PlannerView()
        {
            InitializeComponent();
        }

        private MainViewModel ViewModel
        {
            get { return DataContext as MainViewModel; }
        }

        private void Start_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null) ViewModel.StartPlanner();
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null) ViewModel.StopPlanner();
        }

        private void Hook_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null) ViewModel.TogglePlannerHook();
        }

        private void RepairHook_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null) ViewModel.RepairPlannerHook();
        }

        private void Observe_Changed(object sender, RoutedEventArgs e)
        {
            CheckBox box = sender as CheckBox;
            if (box != null && ViewModel != null)
                ViewModel.SetPlannerObserve(box.IsChecked == true);
        }

        private void PlannerSetting_Changed(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null) ViewModel.UpdatePlannerSettings();
        }

        private void ModeHelp_Click(object sender, RoutedEventArgs e)
        {
            ModeHelpPopup.IsOpen = !ModeHelpPopup.IsOpen;
        }

        private void PlannerGrid_PreviewMouseLeftButtonDown(object sender,
            MouseButtonEventArgs e)
        {
            plannerDragArmed = false;
            plannerDragIndex = -1;
            if (ViewModel == null || ViewModel.PlannerRunning) return;

            DependencyObject source = e.OriginalSource as DependencyObject;
            if (source == null || FindVisualParent<CheckBox>(source) != null)
                return;

            DataGridRow row = FindVisualParent<DataGridRow>(source);
            if (row == null) return;

            plannerDragStart = e.GetPosition(PlannerGrid);
            plannerDragIndex = row.GetIndex();
            plannerDragArmed = plannerDragIndex >= 0;
        }

        private void PlannerGrid_PreviewMouseMove(object sender,
            MouseEventArgs e)
        {
            if (!plannerDragArmed) return;

            if (e.LeftButton != MouseButtonState.Pressed)
            {
                plannerDragArmed = false;
                plannerDragIndex = -1;
                return;
            }

            Point position = e.GetPosition(PlannerGrid);
            if (Math.Abs(position.X - plannerDragStart.X) <
                    SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(position.Y - plannerDragStart.Y) <
                    SystemParameters.MinimumVerticalDragDistance)
                return;

            int index = plannerDragIndex;
            plannerDragArmed = false;
            plannerDragIndex = -1;
            if (index < 0) return;

            DragDrop.DoDragDrop(PlannerGrid,
                new DataObject(PlannerStepFormat, index),
                DragDropEffects.Move);
        }

        private void PlannerGrid_DragOver(object sender, DragEventArgs e)
        {
            bool allowed = ViewModel != null && !ViewModel.PlannerRunning &&
                e.Data.GetDataPresent(PlannerStepFormat);
            e.Effects = allowed
                ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
        }

        private void PlannerGrid_Drop(object sender, DragEventArgs e)
        {
            if (ViewModel == null || ViewModel.PlannerRunning) return;
            if (!e.Data.GetDataPresent(PlannerStepFormat)) return;

            object data = e.Data.GetData(PlannerStepFormat);
            if (!(data is int)) return;

            int target = PlannerDropIndex(e.GetPosition(PlannerGrid));
            if (target < 0) return;

            ViewModel.MovePlannerStepTo((int)data, target);
            e.Handled = true;
        }

        private int PlannerDropIndex(Point point)
        {
            HitTestResult hit = VisualTreeHelper.HitTest(PlannerGrid, point);
            DataGridRow row = hit == null
                ? null : FindVisualParent<DataGridRow>(hit.VisualHit);
            if (row != null) return row.GetIndex();

            if (ViewModel == null || ViewModel.PlannerSteps.Count == 0)
                return -1;

            DataGridRow last = PlannerGrid.ItemContainerGenerator
                .ContainerFromIndex(ViewModel.PlannerSteps.Count - 1)
                as DataGridRow;
            if (last == null) return -1;

            Point bottom = last.TranslatePoint(
                new Point(0, last.ActualHeight), PlannerGrid);
            return point.Y >= bottom.Y
                ? ViewModel.PlannerSteps.Count : -1;
        }

        private static T FindVisualParent<T>(DependencyObject child)
            where T : DependencyObject
        {
            while (child != null)
            {
                T parent = child as T;
                if (parent != null) return parent;
                child = VisualTreeHelper.GetParent(child);
            }
            return null;
        }

        private void AddAction_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null) ViewModel.AddPlannerAction();
        }

        private void AddMove_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null) ViewModel.AddPlannerMove();
        }

        private void ForceAction_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null) ViewModel.ForcePlannerAction();
        }

        private void ForceMove_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null) ViewModel.ForcePlannerMove();
        }

        private void RemoveStep_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null) ViewModel.RemovePlannerStep();
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null) ViewModel.ClearPlanner();
        }

        private void Copy_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null) ViewModel.CopyPlanner();
        }

        private void Paste_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null) ViewModel.PastePlanner();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null) ViewModel.SavePlanner();
        }

        private void Load_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null) ViewModel.LoadPlanner();
        }
    }
}
