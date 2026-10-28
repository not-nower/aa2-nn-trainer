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
        private TabPage BuildRosterTab()
        {
            TabPage page = new TabPage("Class Roster");
            page.BackColor = Color.FromArgb(248, 249, 251);

            roster = new ClassRosterControl();
            roster.Dock = DockStyle.Fill;
            roster.CharacterClicked += Roster_CharacterClicked;
            roster.CharacterActivated += Roster_CharacterActivated;
            roster.PlayRequested += Roster_PlayRequested;
            roster.RelationshipActionRequested += Roster_RelationshipActionRequested;
            roster.IsRelationshipFrozen = delegate(uint pointer, int targetSeat)
            {
                return gameplay != null &&
                    gameplay.IsRelationshipFrozen(pointer, targetSeat);
            };
            page.Controls.Add(roster);
            return page;
        }

        private Control BuildPortraitGroup()
        {
            GroupBox box = new GroupBox();
            box.Text = "Portrait";
            box.Dock = DockStyle.Fill;
            box.Padding = new Padding(6);

            TableLayoutPanel inner = new TableLayoutPanel();
            inner.Dock = DockStyle.Fill;
            inner.ColumnCount = 1;
            inner.RowCount = 2;
            inner.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            inner.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            inner.RowStyles.Add(new RowStyle(SizeType.Absolute, 24F));

            portraitBox = new PictureBox();
            portraitBox.Dock = DockStyle.Fill;
            portraitBox.BorderStyle = BorderStyle.FixedSingle;
            portraitBox.SizeMode = PictureBoxSizeMode.Zoom;
            portraitBox.BackColor = Color.FromArgb(235, 235, 235);
            inner.Controls.Add(portraitBox, 0, 0);

            portraitName = new Label();
            portraitName.Dock = DockStyle.Fill;
            portraitName.TextAlign = ContentAlignment.MiddleCenter;
            portraitName.Font = new Font(Font, FontStyle.Bold);
            portraitName.AutoEllipsis = true;
            portraitName.Text = "No character";
            inner.Controls.Add(portraitName, 0, 1);

            box.Controls.Add(inner);
            return box;
        }

        private void PopulateSeats()
        {
            if (!mem.IsAttached || ctx.Selected == null)
            {
                HideHoverPortrait();
                return;
            }
            uint selectedPointer = ctx.Selected.Pointer;

            string signature = "";
            foreach (CharacterInfo info in ctx.Characters)
                signature += info.Pointer.ToString("X") + ":" + info.Seat.ToString() + ":" + info.Name + "|";
            if (signature == lastSeatSignature && seatList.Items.Count > 0)
            {
                suppressSeatEvent = true;
                try
                {
                    foreach (ListViewItem item in seatList.Items)
                    {
                        CharacterInfo info = item.Tag as CharacterInfo;
                        if (info != null && info.Pointer == selectedPointer)
                        {
                            if (!item.Selected)
                            {
                                item.Selected = true;
                                item.Focused = true;
                                item.EnsureVisible();
                            }
                            break;
                        }
                    }
                }
                finally { suppressSeatEvent = false; }
                return;
            }
            HideHoverPortrait();
            lastSeatSignature = signature;

            suppressSeatEvent = true;
            seatList.BeginUpdate();
            try
            {
                seatList.Items.Clear();
                foreach (CharacterInfo info in ctx.Characters)
                {
                    uint? cd = mem.ReadU32(info.Pointer + 0x28);
                    string personality = "?";
                    string sex = "?";
                    if (cd.HasValue && mem.IsPlausiblePointer(cd.Value))
                    {
                        byte? p = mem.ReadU8(cd.Value + 0x445);
                        if (p.HasValue) personality = PersonalityName(p.Value);
                        byte? g = mem.ReadU8(cd.Value + 0x3C);
                        if (g.HasValue) sex = g.Value == 0 ? "M" : "F";
                    }
                    ListViewItem item = new ListViewItem(info.Seat >= 0 ? info.Seat.ToString("D2") : "??");
                    item.SubItems.Add(info.Name);
                    item.SubItems.Add(personality);
                    item.SubItems.Add(sex);
                    item.SubItems.Add(info.IsPlayer ? "PC" : "");
                    item.Tag = info;
                    if (info.IsPlayer) item.Font = new Font(seatList.Font, FontStyle.Bold);
                    seatList.Items.Add(item);
                }

                foreach (ListViewItem item in seatList.Items)
                {
                    CharacterInfo info = item.Tag as CharacterInfo;
                    if (info != null && info.Pointer == selectedPointer)
                    {
                        item.Selected = true;
                        item.Focused = true;
                        item.EnsureVisible();
                        break;
                    }
                }
            }
            finally
            {
                seatList.EndUpdate();
                suppressSeatEvent = false;
            }
            SizeSeatColumn();
        }

        private void SeatList_DrawItem(object sender, DrawListViewItemEventArgs e)
        {
            bool selected = e.Item.Selected;
            Color back = selected ? SystemColors.Highlight : seatList.BackColor;
            Color fore = selected ? SystemColors.HighlightText : seatList.ForeColor;

            using (SolidBrush brush = new SolidBrush(back))
                e.Graphics.FillRectangle(brush, e.Bounds);

            string seat = e.Item.Text;
            string name = e.Item.SubItems.Count > 1 ? e.Item.SubItems[1].Text : "";
            string personality = e.Item.SubItems.Count > 2 ? e.Item.SubItems[2].Text : "";

            string line = seat + " " + name;
            if (!string.IsNullOrEmpty(personality))
                line += " (" + personality.Replace(" (", "(") + ")";

            Rectangle textRect = new Rectangle(e.Bounds.Left + 5, e.Bounds.Top,
                Math.Max(0, e.Bounds.Width - 10), e.Bounds.Height);
            TextFormatFlags flags = TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix;
            Font rowFont = e.Item.Font.Bold ? seatRowTextBoldFont : seatRowTextFont;
            if (rowFont == null) rowFont = e.Item.Font;
            TextRenderer.DrawText(e.Graphics, line, rowFont, textRect, fore, flags);

            if (selected && e.Item.Focused)
                e.DrawFocusRectangle();
            e.DrawDefault = false;
        }

        private void SeatList_DrawSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            // DrawItem paints the whole row; suppress the default column text
            // so it does not overdraw it.
            e.DrawDefault = false;
        }

        private void SizeSeatColumn()
        {
            if (seatList == null || seatList.Columns.Count == 0) return;
            int width = seatList.ClientSize.Width - 2;
            int rowHeight = SeatRowHeight + 2;
            if (seatList.Items.Count * rowHeight > seatList.ClientSize.Height)
                width -= SystemInformation.VerticalScrollBarWidth;
            if (width < 80) width = 80;
            seatList.Columns[0].Width = width;
        }

        private void SeatList_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (suppressSeatEvent) return;
            if (seatList.SelectedItems.Count == 0) return;
            CharacterInfo info = seatList.SelectedItems[0].Tag as CharacterInfo;
            if (info == null) return;
            ApplySelection(info);
            seatList.Invalidate();
        }

        private void Roster_CharacterClicked(object sender, CharacterSelectedEventArgs e)
        {
            if (e.Character == null) return;
            CharacterInfo info = ctx.FindByPointer(e.Character.Pointer);
            if (info == null) info = e.Character;
            SyncSeatList(info);
            ApplySelection(info);
        }

        private void Roster_CharacterActivated(object sender, CharacterSelectedEventArgs e)
        {
            if (e.Character == null) return;
            CharacterInfo info = ctx.FindByPointer(e.Character.Pointer);
            if (info == null) info = e.Character;

            SyncSeatList(info);
            ApplySelection(info);

            // Keep whichever editor sub-tab was last open; only the top-level
            // tab changes.
            if (tabs != null) tabs.SelectedIndex = 2; // Character Editor
            RefreshCurrentTabValues();
        }

        private void Roster_PlayRequested(object sender, CharacterSelectedEventArgs e)
        {
            if (e.Character == null || !mem.IsAttached) return;

            if (e.Character.IsPlayer)
            {
                SetSummaryText(e.Character.Name + " is already the player character.");
                return;
            }

            RunHealthCheck();
            if (!writesAllowed || !mem.CanWrite)
            {
                MessageBox.Show(this,
                    "The write gate is locked. Wait for a clean pointer check before switching the player character.",
                    "Play this character", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string error;
            if (!PlayerSwitch.TrySetPlayer(mem, ctx, e.Character, out error))
            {
                MessageBox.Show(this, error, "Play this character",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!TryRefreshContext())
            {
                SetSummaryText("Player switched, but the class data could not be refreshed.");
                return;
            }

            CharacterInfo info = ctx.FindByPointer(e.Character.Pointer);
            if (info == null) info = e.Character;

            SyncSeatList(info);
            ApplySelection(info);
            PopulateSeats();
            RefreshCurrentTabValues();
            SetSummaryText("Now playing: " + info.Name);
        }

        private void Roster_RelationshipActionRequested(object sender,
            RosterRelationshipEventArgs e)
        {
            if (e == null || e.Source == null || e.Target == null ||
                !mem.IsAttached)
                return;

            CharacterInfo source = ctx.FindByPointer(e.Source.Pointer);
            if (source == null) source = e.Source;
            CharacterInfo target = ctx.FindByPointer(e.Target.Pointer);
            if (target == null) target = e.Target;

            if (source.Pointer == target.Pointer) return;
            if (target.Seat < 0 || target.Seat > 24)
            {
                SetSummaryText("The selected character has no class seat.");
                return;
            }
            if (gameplay == null)
            {
                SetSummaryText("The relationship editor is not available.");
                return;
            }

            RunHealthCheck();
            if (!writesAllowed || !mem.CanWrite)
            {
                MessageBox.Show(this,
                    "The write gate is locked. Wait for a clean pointer check before changing this relationship.",
                    "Relationship", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            bool ok;
            string error;
            string summary;
            switch (e.Action)
            {
                case RosterRelationshipAction.SetLove:
                    ok = gameplay.ApplyRelationshipFeeling(mem, source, target, 0,
                        writesAllowed, out error);
                    summary = "Set " + source.Name + "'s Love toward " +
                        target.Name + " to 30.";
                    break;
                case RosterRelationshipAction.SetLike:
                    ok = gameplay.ApplyRelationshipFeeling(mem, source, target, 1,
                        writesAllowed, out error);
                    summary = "Set " + source.Name + "'s Like toward " +
                        target.Name + " to 30.";
                    break;
                case RosterRelationshipAction.SetDislike:
                    ok = gameplay.ApplyRelationshipFeeling(mem, source, target, 2,
                        writesAllowed, out error);
                    summary = "Set " + source.Name + "'s Dislike toward " +
                        target.Name + " to 30.";
                    break;
                case RosterRelationshipAction.SetHate:
                    ok = gameplay.ApplyRelationshipFeeling(mem, source, target, 3,
                        writesAllowed, out error);
                    summary = "Set " + source.Name + "'s Hate toward " +
                        target.Name + " to 30.";
                    break;
                case RosterRelationshipAction.SetLover:
                    ok = LoverEditor.TryWriteMutualLovers(mem, source, target,
                        true, out error);
                    summary = "Made " + source.Name + " and " + target.Name +
                        " lovers.";
                    break;
                case RosterRelationshipAction.BreakUp:
                    ok = LoverEditor.TryWriteMutualLovers(mem, source, target,
                        false, out error);
                    summary = "Broke up " + source.Name + " and " + target.Name + ".";
                    break;
                case RosterRelationshipAction.ToggleFreeze:
                    bool frozen = gameplay.IsRelationshipFrozen(source.Pointer,
                        target.Seat);
                    ok = gameplay.SetRelationshipFrozen(mem, source, target,
                        !frozen, writesAllowed, out error);
                    summary = frozen
                        ? "Unfroze " + source.Name + " toward " + target.Name + "."
                        : "Froze " + source.Name + " toward " + target.Name + ".";
                    break;
                default:
                    return;
            }

            if (!ok)
            {
                MessageBox.Show(this, error ?? "The relationship change failed.",
                    "Relationship", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            RefreshCurrentTabValues();
            SetSummaryText(summary);
        }

        private void SyncSeatList(CharacterInfo info)
        {
            if (seatList == null || info == null) return;
            suppressSeatEvent = true;
            try
            {
                bool found = false;
                foreach (ListViewItem item in seatList.Items)
                {
                    CharacterInfo itemInfo = item.Tag as CharacterInfo;
                    if (itemInfo == null || itemInfo.Pointer != info.Pointer) continue;
                    item.Selected = true;
                    item.Focused = true;
                    item.EnsureVisible();
                    found = true;
                    break;
                }
                if (!found) seatList.SelectedItems.Clear();
            }
            finally
            {
                suppressSeatEvent = false;
            }
        }

        private void ApplySelection(CharacterInfo info)
        {
            if (info == null) return;
            HideHoverPortrait();
            ctx.Select(mem, info);
            PublishLiveRoster();
            UpdateSelectedGender();
            MaybeRunHealthCheck();
            UpdateOverview();
            RefreshCurrentTabValues();
            SetSummary();
            RefreshSaveViewer();
        }

        private void SeatList_MouseMove(object sender, MouseEventArgs e)
        {
            if (!mem.IsAttached || ctx.Selected == null)
            {
                ScheduleHideHoverPortrait();
                return;
            }

            ListViewItem item = seatList.GetItemAt(e.X, e.Y);
            if (item == null)
            {
                ScheduleHideHoverPortrait();
                return;
            }

            if (item == hoverItem)
            {
                if (hoverHideTimer != null) hoverHideTimer.Stop();
                if (hoverPopupVisible)
                {
                    if (hoverWatchTimer != null) hoverWatchTimer.Start();
                }
                else
                {
                    hoverTimer.Stop();
                    hoverTimer.Start();
                }
                return;
            }

            HideHoverPortrait();
            hoverItem = item;
            hoverCharacter = item.Tag as CharacterInfo;
            hoverControl = seatList;
            hoverAnchorScreen = seatList.RectangleToScreen(item.Bounds);
            hoverTimer.Stop();
            hoverTimer.Start();
        }

        private void ShowHoverPortrait()
        {
            if (hoverCharacter == null || hoverItem == null) return;
            if (!mem.IsAttached || ctx.Selected == null)
            {
                HideHoverPortrait();
                return;
            }

            Point listClient = seatList.PointToClient(Cursor.Position);
            if (!seatList.ClientRectangle.Contains(listClient) ||
                seatList.GetItemAt(listClient.X, listClient.Y) != hoverItem)
            {
                HideHoverPortrait();
                return;
            }

            CharacterInfo info = hoverCharacter;
            EnsureHoverPopup();
            LoadHoverPortrait(info);

            hoverName.Text = string.IsNullOrEmpty(info.Name) ? "(unnamed)" : info.Name;
            string seat = info.Seat >= 0 ? info.Seat.ToString() : "?";
            string sex = hoverItem.SubItems.Count > 3 ? hoverItem.SubItems[3].Text : "";
            string meta = "Seat " + seat + "  -  " + PersonalityFor(info);
            if (!string.IsNullOrEmpty(sex)) meta += "  -  " + sex;
            hoverMeta.Text = meta;

            ShowHoverPopupAtAnchor();
        }

        private string PersonalityFor(CharacterInfo info)
        {
            GameContext view = ctx.CreateCharacterView(mem, info);
            EvalResult r = DashboardData.Eval(mem, view, "Personality", 0);
            if (r == null || r.Bytes == null || r.Bytes.Length < 1) return "?";
            int id = r.Bytes[0];
            string name = names == null ? null : names.Get("PERSONALITY", id);
            if (string.IsNullOrEmpty(name)) return "P" + id.ToString();
            return id.ToString("D2") + "  " + name;
        }

        private void EnsureHoverPopup()
        {
            if (hoverPopup != null) return;

            TableLayoutPanel panel = new TableLayoutPanel();
            panel.ColumnCount = 1;
            panel.RowCount = 3;
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200F));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 300F));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
            panel.Size = new Size(200, 346);
            panel.BackColor = Color.White;
            panel.Margin = new Padding(0);
            panel.Padding = new Padding(0);

            Panel picturePanel = new Panel();
            picturePanel.Dock = DockStyle.Fill;
            picturePanel.BackColor = Color.FromArgb(236, 238, 242);

            hoverPicture = new PictureBox();
            hoverPicture.Dock = DockStyle.Fill;
            hoverPicture.SizeMode = PictureBoxSizeMode.Zoom;
            hoverPicture.BackColor = Color.FromArgb(236, 238, 242);
            picturePanel.Controls.Add(hoverPicture);

            hoverPlaceholder = new Label();
            hoverPlaceholder.Dock = DockStyle.Fill;
            hoverPlaceholder.TextAlign = ContentAlignment.MiddleCenter;
            hoverPlaceholder.Text = "No portrait";
            hoverPlaceholder.ForeColor = Color.DimGray;
            hoverPlaceholder.BackColor = Color.FromArgb(236, 238, 242);
            hoverPlaceholder.Visible = false;
            picturePanel.Controls.Add(hoverPlaceholder);

            panel.Controls.Add(picturePanel, 0, 0);

            hoverName = new Label();
            hoverName.Dock = DockStyle.Fill;
            hoverName.TextAlign = ContentAlignment.MiddleCenter;
            hoverName.AutoEllipsis = true;
            hoverName.Font = new Font(Font, FontStyle.Bold);
            panel.Controls.Add(hoverName, 0, 1);

            hoverMeta = new Label();
            hoverMeta.Dock = DockStyle.Fill;
            hoverMeta.TextAlign = ContentAlignment.MiddleCenter;
            hoverMeta.AutoEllipsis = true;
            hoverMeta.Font = new Font("Segoe UI", 8F);
            hoverMeta.ForeColor = Color.DimGray;
            panel.Controls.Add(hoverMeta, 0, 2);

            ToolStripControlHost host = new ToolStripControlHost(panel);
            host.AutoSize = false;
            host.Size = panel.Size;
            host.Margin = new Padding(0);
            host.Padding = new Padding(0);

            hoverPopup = new ToolStripDropDown();
            hoverPopup.AutoSize = false;
            hoverPopup.AutoClose = false;
            hoverPopup.DropShadowEnabled = true;
            hoverPopup.Padding = new Padding(1);
            hoverPopup.BackColor = Color.FromArgb(190, 195, 205);
            hoverPopup.Items.Add(host);
            hoverPopup.Size = new Size(202, 348);
            hoverPopup.Closed += delegate
            {
                hoverPopupVisible = false;
                ClearHoverSource();
                if (hoverWatchTimer != null) hoverWatchTimer.Stop();
                if (hoverHideTimer != null) hoverHideTimer.Stop();
            };
        }

        private void LoadHoverPortrait(CharacterInfo info)
        {
            GameContext view = ctx.CreateCharacterView(mem, info);
            uint addr;
            uint size;
            PortraitLoader.ReadFullInfo(mem, view, out addr, out size);

            if (hoverPortrait != null && hoverPortraitPointer == info.Pointer &&
                hoverPortrait.Address == addr && hoverPortrait.Size == size)
                return;

            if (hoverPicture != null) hoverPicture.Image = null;
            if (hoverPortrait != null)
            {
                hoverPortrait.Dispose();
                hoverPortrait = null;
            }

            hoverPortrait = PortraitLoader.Load(mem, addr, size);
            hoverPortraitPointer = info.Pointer;

            bool hasImage = hoverPortrait.Bitmap != null;
            if (hoverPicture != null)
            {
                hoverPicture.Image = hasImage ? hoverPortrait.Bitmap : null;
                hoverPicture.Visible = hasImage;
            }
            if (hoverPlaceholder != null) hoverPlaceholder.Visible = !hasImage;
        }

        private void ShowHoverPopupAtAnchor()
        {
            if (hoverPopup == null || hoverCharacter == null) return;
            if (hoverHideTimer != null) hoverHideTimer.Stop();

            int width = hoverPopup.Width > 0 ? hoverPopup.Width : 202;
            int height = hoverPopup.Height > 0 ? hoverPopup.Height : 348;
            Rectangle area = Screen.FromPoint(Cursor.Position).WorkingArea;
            Rectangle anchor = hoverAnchorScreen;
            if (anchor.Width <= 0 || anchor.Height <= 0)
                anchor = new Rectangle(Cursor.Position, new Size(1, 1));

            // Keep the preview beside the hovered row or portrait so it
            // never covers the thing being pointed at.
            int x = anchor.Right + 8;
            int y = anchor.Top;
            if (x + width > area.Right) x = anchor.Left - width - 8;
            if (y + height > area.Bottom) y = area.Bottom - height;
            if (x < area.Left) x = area.Left;
            if (y < area.Top) y = area.Top;

            if (hoverPopupVisible) hoverPopup.Location = new Point(x, y);
            else hoverPopup.Show(new Point(x, y));
            hoverPopupVisible = true;
            if (hoverWatchTimer != null) hoverWatchTimer.Start();
        }

        private bool IsHoverStillValid()
        {
            if (hoverCharacter == null || hoverControl == null || hoverItem == null)
                return false;

            Point client = hoverControl.PointToClient(Cursor.Position);
            if (!hoverControl.ClientRectangle.Contains(client)) return false;
            return seatList.GetItemAt(client.X, client.Y) == hoverItem;
        }

        private void CheckHoverStillValid()
        {
            if (!hoverPopupVisible)
            {
                if (hoverWatchTimer != null) hoverWatchTimer.Stop();
                return;
            }
            if (!IsHoverStillValid())
                ScheduleHideHoverPortrait();
        }

        private void ScheduleHideHoverPortrait()
        {
            if (!hoverPopupVisible || hoverPopup == null)
            {
                HideHoverPortrait();
                return;
            }

            // A leave event from the previous row/portrait can arrive after
            // the new source has already taken over.
            if (IsHoverStillValid())
            {
                if (hoverWatchTimer != null) hoverWatchTimer.Start();
                return;
            }

            if (hoverWatchTimer != null) hoverWatchTimer.Stop();
            if (hoverHideTimer != null)
            {
                hoverHideTimer.Stop();
                hoverHideTimer.Start();
            }
        }

        private void ClearHoverSource()
        {
            hoverItem = null;
            hoverCharacter = null;
            hoverControl = null;
            hoverAnchorScreen = Rectangle.Empty;
        }

        private void HideHoverPortrait()
        {
            if (hoverTimer != null) hoverTimer.Stop();
            if (hoverWatchTimer != null) hoverWatchTimer.Stop();
            if (hoverHideTimer != null) hoverHideTimer.Stop();
            ClearHoverSource();
            if (hoverPopup != null && (hoverPopupVisible || hoverPopup.Visible))
                hoverPopup.Close();
            hoverPopupVisible = false;
        }

        private void DisposeHoverPopup()
        {
            if (hoverPicture != null) hoverPicture.Image = null;
            if (hoverPortrait != null)
            {
                hoverPortrait.Dispose();
                hoverPortrait = null;
            }
            if (hoverPopup != null)
            {
                hoverPopup.Dispose();
                hoverPopup = null;
            }
            hoverPicture = null;
            hoverPlaceholder = null;
            hoverName = null;
            hoverMeta = null;
            hoverPortraitPointer = 0;
            hoverPopupVisible = false;
            ClearHoverSource();
        }

        private void LoadSeatPortrait()
        {
            if (seatPortraitName == null) return;
            if (!mem.IsAttached || ctx.Selected == null)
            {
                ClearSeatPortrait();
                return;
            }

            string seat = ctx.Selected.Seat >= 0 ? ctx.Selected.Seat.ToString("D2") : "??";
            string name = string.IsNullOrEmpty(ctx.Selected.Name) ? "(unnamed)" : ctx.Selected.Name;
            seatPortraitName.Text = seat + "  " + name + (ctx.Selected.IsPlayer ? "  [PC]" : "");

            uint address;
            uint size;
            PortraitLoader.ReadFullInfo(mem, ctx, out address, out size);

            if (seatPortrait != null && seatPortraitPointer == ctx.Selected.Pointer &&
                seatPortrait.Address == address && seatPortrait.Size == size)
                return;

            if (seatPortraitBox != null) seatPortraitBox.Image = null;
            if (seatPortrait != null)
            {
                seatPortrait.Dispose();
                seatPortrait = null;
            }

            seatPortrait = PortraitLoader.Load(mem, address, size);
            seatPortraitPointer = ctx.Selected.Pointer;

            bool hasImage = seatPortrait != null && seatPortrait.Bitmap != null;
            if (seatPortraitBox != null)
            {
                seatPortraitBox.Image = hasImage ? seatPortrait.Bitmap : null;
                seatPortraitBox.Visible = hasImage;
            }
            if (seatPortraitPlaceholder != null)
                seatPortraitPlaceholder.Visible = !hasImage;
        }

        private void ClearSeatPortrait()
        {
            if (seatPortraitBox != null) seatPortraitBox.Image = null;
            if (seatPortrait != null)
            {
                seatPortrait.Dispose();
                seatPortrait = null;
            }
            seatPortraitPointer = 0;
            if (seatPortraitName != null) seatPortraitName.Text = "No character";
            if (seatPortraitBox != null) seatPortraitBox.Visible = false;
            if (seatPortraitPlaceholder != null) seatPortraitPlaceholder.Visible = true;
        }

        private byte[] ReadPortraitBytes(string sizeName, string addrName, out uint addr, out uint size)
        {
            addr = 0;
            size = 0;
            EvalResult sizeResult = DashboardData.Eval(mem, ctx, sizeName, 0);
            EvalResult addrResult = DashboardData.Eval(mem, ctx, addrName, 0);
            if (sizeResult == null || addrResult == null) return null;
            if (sizeResult.Bytes == null || sizeResult.Bytes.Length < 4) return null;
            if (addrResult.Bytes == null || addrResult.Bytes.Length < 4) return null;

            size = BitConverter.ToUInt32(sizeResult.Bytes, 0);
            addr = BitConverter.ToUInt32(addrResult.Bytes, 0);
            if (size == 0 || size > 8 * 1024 * 1024 || !mem.IsPlausiblePointer(addr)) return null;
            return mem.ReadBytes(addr, (int)size);
        }
    }
}
