using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace AA2Trainer.WpfPrototype
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            ThemeService.Apply(TrainerSettings.Current.Theme, null);
        }

        private static void OnDispatcherUnhandledException(object sender,
            DispatcherUnhandledExceptionEventArgs e)
        {
            string path = WriteCrashLog(e.Exception, "UI thread");
            try
            {
                MessageBox.Show(
                    "AA2 Trainer hit an unexpected error.\n\n" +
                    (e.Exception == null ? "Unknown error" : e.Exception.Message) +
                    "\n\nDetails were written to:\n" + path,
                    "AA2 Trainer error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch
            {
                // Never let the error dialog hide the original failure.
            }
            e.Handled = true;
        }

        private static void OnDomainUnhandledException(object sender,
            UnhandledExceptionEventArgs e)
        {
            WriteCrashLog(e.ExceptionObject as Exception, "background thread");
        }

        private static void OnUnobservedTaskException(object sender,
            UnobservedTaskExceptionEventArgs e)
        {
            WriteCrashLog(e.Exception, "task");
            e.SetObserved();
        }

        private static string WriteCrashLog(Exception exception, string source)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "crash.log");
            try
            {
                StringBuilder text = new StringBuilder();
                text.AppendLine("AA2 Trainer crash log");
                text.AppendLine("Version: " +
                    Assembly.GetExecutingAssembly().GetName().Version);
                text.AppendLine("Time: " + DateTime.Now.ToString(
                    "yyyy-MM-dd HH:mm:ss.fff"));
                text.AppendLine("Source: " + source);
                text.AppendLine();
                text.AppendLine(exception == null
                    ? "Unknown error" : exception.ToString());
                File.WriteAllText(path, text.ToString());
            }
            catch
            {
                // Crash logging is best-effort; never mask the original error.
            }
            return path;
        }
    }
}
