using System.Drawing;
using System.Windows.Forms;

namespace AA2Trainer
{
    public sealed partial class MainForm
    {
        private TabPage BuildGameplayTab()
        {
            TabPage page = new TabPage("Gameplay");
            page.BackColor = Color.FromArgb(248, 249, 251);

            gameplay = new GameplayControl();
            gameplay.Dock = DockStyle.Fill;
            gameplay.ConfirmWriteAllowed = delegate
            {
                RunHealthCheck();
                return writesAllowed && mem.CanWrite;
            };
            gameplay.CharacterActivated += Gameplay_CharacterActivated;
            page.Controls.Add(gameplay);
            return page;
        }

        private void Gameplay_CharacterActivated(object sender,
            CharacterSelectedEventArgs e)
        {
            if (e == null || e.Character == null || !mem.IsAttached) return;

            CharacterInfo info = ctx.FindByPointer(e.Character.Pointer);
            if (info == null) info = e.Character;

            SyncSeatList(info);
            ApplySelection(info);
        }
    }
}
