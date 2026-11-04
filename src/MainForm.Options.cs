using System.Drawing;
using System.Windows.Forms;

namespace AA2Trainer
{
    public sealed partial class MainForm
    {
        private TabPage BuildOptionTab()
        {
            TabPage page = new TabPage("Option");
            page.BackColor = Color.FromArgb(248, 249, 251);

            lastClassSavePath = SaveBackup.LoadLastSavePath();
            optionsControl = new OptionsControl();
            optionsControl.Dock = DockStyle.Fill;
            optionsControl.SetSavePath(lastClassSavePath);
            optionsControl.ConfirmWriteAllowed = delegate
            {
                RunHealthCheck();
                return writesAllowed && mem.CanWrite;
            };
            optionsControl.NameSnapshot = delegate { return names.Snapshot(); };
            optionsControl.RefreshRequested += delegate
            {
                DiscardScheduleEdits();
                AttachAndRefresh();
            };
            optionsControl.RecheckRequested += delegate { RunHealthCheck(); };
            optionsControl.DetailsRequested += delegate { ShowHealthDetails(); };
            optionsControl.ReloadNamesRequested += delegate { ReloadNames(); };
            optionsControl.LiveRefreshChanged += delegate { ApplyLiveSettings(); };
            optionsControl.SavePathChanged += delegate
            {
                lastClassSavePath = optionsControl.SavePath;
                SaveBackup.SaveLastSavePath(lastClassSavePath);
            };
            optionsControl.SaveRestored += delegate
            {
                if (importReplace != null)
                    importReplace.ReloadIfOpen(optionsControl.LastRestoredSavePath);
            };
            page.Controls.Add(optionsControl);
            return page;
        }
    }
}
