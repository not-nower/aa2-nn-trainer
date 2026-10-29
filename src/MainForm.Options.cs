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

            optionsControl = new OptionsControl();
            optionsControl.Dock = DockStyle.Fill;
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
            page.Controls.Add(optionsControl);
            return page;
        }
    }
}
