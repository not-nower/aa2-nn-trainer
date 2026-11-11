using System.Windows;

namespace AA2Trainer.WpfPrototype
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            ThemeService.Apply(TrainerSettings.Current.Theme, null);
        }
    }
}
