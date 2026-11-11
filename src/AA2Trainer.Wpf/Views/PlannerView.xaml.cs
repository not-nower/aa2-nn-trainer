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

        private void AddAction_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null) ViewModel.AddPlannerAction();
        }

        private void AddMove_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null) ViewModel.AddPlannerMove();
        }

        private void RemoveStep_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null) ViewModel.RemovePlannerStep();
        }

        private void MoveUp_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null) ViewModel.MovePlannerStep(-1);
        }

        private void MoveDown_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel != null) ViewModel.MovePlannerStep(1);
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
