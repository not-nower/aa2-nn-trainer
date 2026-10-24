using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;


namespace AA2Trainer
{
    public partial class MainForm
    {
        private TabPage BuildSaveViewerTab()
        {
            TabPage page = new TabPage("Save Viewer");
            page.BackColor = Color.FromArgb(248, 249, 251);

            saveViewer = new SaveViewerControl(names);
            saveViewer.Dock = DockStyle.Fill;
            page.Controls.Add(saveViewer);
            return page;
        }

        private void RefreshSaveViewer()
        {
            if (saveViewer == null) return;
            saveViewer.SetGameRoot(GetGameRoot());
            saveViewer.RefreshIfChanged();
            if (mem.IsAttached && ctx.Selected != null)
            {
                saveViewer.SetLiveCharacters(ctx.Characters);
                if (ctx.Selected.Seat != lastSaveViewerSeat)
                {
                    saveViewer.SetSelectedSeat(ctx.Selected.Seat);
                    lastSaveViewerSeat = ctx.Selected.Seat;
                }
            }
            else
            {
                saveViewer.SetLiveCharacters(null);
            }
        }
    }
}
