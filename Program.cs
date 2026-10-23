using System;
using System.IO;
using System.Windows.Forms;

namespace AA2Trainer
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += delegate (object sender, System.Threading.ThreadExceptionEventArgs e)
            {
                LogAndShow(e.Exception);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate (object sender, UnhandledExceptionEventArgs e)
            {
                LogAndShow(e.ExceptionObject as Exception);
            };
            try
            {
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                LogAndShow(ex);
            }
        }

        private static void LogAndShow(Exception ex)
        {
            string text = ex == null ? "Unknown error" : ex.ToString();
            try
            {
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"), text);
            }
            catch { }
            MessageBox.Show(text, "AA2 Trainer error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
