using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AA2Trainer
{
    public sealed class CharacterSelectedEventArgs : EventArgs
    {
        public CharacterInfo Character { get; private set; }

        public CharacterSelectedEventArgs(CharacterInfo character)
        {
            Character = character;
        }
    }

    public sealed class ClassRosterControl : UserControl
    {
        // Visual classroom, left-to-right columns. Seat numbering runs
        // right-to-left by column, top-to-bottom, with the teacher at 24.
        private static readonly int[,] SeatGrid = new int[,]
        {
            { 19, 14, 24,  5, 0 },
            { 20, 15, 10,  6, 1 },
            { 21, 16, 11,  7, 2 },
            { 22, 17, 12,  8, 3 },
            { 23, 18, 13,  9, 4 }
        };

        private readonly Dictionary<int, ClassRosterCard> cards =
            new Dictionary<int, ClassRosterCard>();
        private TableLayoutPanel grid;

        public event EventHandler<CharacterSelectedEventArgs> CharacterClicked;

        public ClassRosterControl()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(248, 249, 251);
            Padding = new Padding(4);
            AutoScroll = true;
            BuildGrid();
        }

        private void BuildGrid()
        {
            grid = new TableLayoutPanel();
            grid.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            grid.ColumnCount = 5;
            grid.RowCount = 5;
            grid.Margin = new Padding(0);
            grid.Padding = new Padding(0);

            for (int c = 0; c < 5; c++)
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            for (int r = 0; r < 5; r++)
                grid.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));

            for (int r = 0; r < 5; r++)
            {
                for (int c = 0; c < 5; c++)
                {
                    int seat = SeatGrid[r, c];
                    ClassRosterCard card = new ClassRosterCard(seat);
                    card.Click += Card_Click;
                    cards[seat] = card;
                    grid.Controls.Add(card, c, r);
                }
            }

            Controls.Add(grid);
            LayoutGrid();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutGrid();
        }

        private void LayoutGrid()
        {
            if (grid == null) return;
            int w = Math.Max(ClientSize.Width - Padding.Horizontal, 800);
            int h = Math.Max(ClientSize.Height - Padding.Vertical, 560);
            grid.Location = new Point(Padding.Left, Padding.Top);
            grid.Size = new Size(w, h);
        }

        public void RefreshRoster(GameMemory mem, GameContext ctx, CharacterInfo selected)
        {
            if (mem == null || ctx == null || !mem.IsAttached)
            {
                Clear();
                return;
            }

            Dictionary<int, CharacterInfo> bySeat = new Dictionary<int, CharacterInfo>();
            if (ctx.Characters != null)
            {
                foreach (CharacterInfo info in ctx.Characters)
                {
                    if (info == null) continue;
                    if (info.Seat < 0 || info.Seat > 24) continue;
                    if (!bySeat.ContainsKey(info.Seat)) bySeat[info.Seat] = info;
                }
            }

            foreach (KeyValuePair<int, ClassRosterCard> pair in cards)
            {
                int seat = pair.Key;
                ClassRosterCard card = pair.Value;
                CharacterInfo info;
                if (!bySeat.TryGetValue(seat, out info))
                {
                    card.SetEmpty();
                    card.SetSelected(false);
                    continue;
                }

                GameContext view = ctx.CreateCharacterView(mem, info);
                int? action = ReadInt(mem, view, "Action");
                int? location = ReadInt(mem, view, "Location");
                int? mood = ReadInt(mem, view, "MoodAction");

                string actionText = action.HasValue ? DisplayText.ActionLabel(action.Value) : "-";
                string locationText = location.HasValue ? location.Value.ToString() : "-";
                string moodText = mood.HasValue ? mood.Value.ToString() : "-";

                card.SetCharacter(info, info.Name, actionText, locationText, moodText, info.IsPlayer);
                card.SetSelected(selected != null && selected.Pointer == info.Pointer);

                uint thumbAddr;
                uint thumbSize;
                uint portraitAddr;
                uint portraitSize;
                ReadPortraitInfo(mem, view, out thumbAddr, out thumbSize, out portraitAddr, out portraitSize);

                uint addr = thumbAddr;
                uint size = thumbSize;
                if (addr == 0 || size == 0 || !mem.IsPlausiblePointer(addr))
                {
                    addr = portraitAddr;
                    size = portraitSize;
                }

                if (!card.MatchesPortrait(addr, size, info.Name))
                {
                    card.SetPortrait(LoadPortrait(mem, addr, size), addr, size, info.Name);
                }
            }
        }

        public void SetSelected(CharacterInfo selected)
        {
            foreach (ClassRosterCard card in cards.Values)
            {
                card.SetSelected(selected != null && card.Character != null &&
                                 card.Character.Pointer == selected.Pointer);
            }
        }

        public void Clear()
        {
            foreach (ClassRosterCard card in cards.Values)
            {
                card.SetEmpty();
                card.SetSelected(false);
            }
        }

        public void DisposePortraits()
        {
            foreach (ClassRosterCard card in cards.Values)
                card.ClearPortrait();
        }

        private void Card_Click(object sender, EventArgs e)
        {
            ClassRosterCard card = sender as ClassRosterCard;
            if (card == null || card.Character == null) return;
            if (CharacterClicked != null)
                CharacterClicked(this, new CharacterSelectedEventArgs(card.Character));
        }

        private static int? ReadInt(GameMemory mem, GameContext view, string name)
        {
            EvalResult r = DashboardData.Eval(mem, view, name, 0);
            if (r == null || r.Bytes == null || r.Bytes.Length < 4) return null;
            return BitConverter.ToInt32(r.Bytes, 0);
        }

        private static uint ReadUInt(GameMemory mem, GameContext view, string name)
        {
            EvalResult r = DashboardData.Eval(mem, view, name, 0);
            if (r == null || r.Bytes == null || r.Bytes.Length < 4) return 0;
            return BitConverter.ToUInt32(r.Bytes, 0);
        }

        private static void ReadPortraitInfo(GameMemory mem, GameContext view,
            out uint thumbAddr, out uint thumbSize,
            out uint portraitAddr, out uint portraitSize)
        {
            thumbSize = ReadUInt(mem, view, "ThumbSize");
            thumbAddr = ReadUInt(mem, view, "ThumbAddr");
            portraitSize = ReadUInt(mem, view, "PotraitSize");
            portraitAddr = ReadUInt(mem, view, "PotraitAddr");
        }

        private static Bitmap LoadPortrait(GameMemory mem, uint addr, uint size)
        {
            if (size == 0 || size > 8 * 1024 * 1024) return null;
            if (!mem.IsPlausiblePointer(addr)) return null;

            byte[] data = mem.ReadBytes(addr, (int)size);
            if (data == null || data.Length < 8) return null;

            try
            {
                using (MemoryStream stream = new MemoryStream(data))
                using (Image image = Image.FromStream(stream))
                {
                    return new Bitmap(image);
                }
            }
            catch
            {
                return null;
            }
        }
    }

    public sealed class ClassRosterCard : Panel
    {
        public int Seat { get; private set; }
        public CharacterInfo Character { get; private set; }

        private readonly Label seatLabel;
        private readonly Label badgeLabel;
        private readonly Label nameLabel;
        private readonly Label actionLabel;
        private readonly Label locationLabel;
        private readonly Label moodLabel;
        private readonly PictureBox portraitBox;

        private readonly Font seatFont;
        private readonly Font badgeFont;
        private readonly Font nameFont;
        private readonly Font detailFont;

        private bool selected;
        private bool portraitChecked;
        private bool portraitFailed;
        private DateTime lastPortraitAttempt;
        private uint lastPortraitAddr;
        private uint lastPortraitSize;
        private string lastPortraitName;

        public ClassRosterCard(int seat)
        {
            Seat = seat;
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = Color.White;
            Margin = new Padding(4);
            Padding = new Padding(5);
            MinimumSize = new Size(120, 90);

            seatFont = new Font("Segoe UI", 9F, FontStyle.Bold);
            badgeFont = new Font("Segoe UI", 7F, FontStyle.Bold);
            nameFont = new Font("Segoe UI", 9F, FontStyle.Bold);
            detailFont = new Font("Segoe UI", 8F);

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 2;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 18F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.Margin = new Padding(0);
            root.Padding = new Padding(0);

            TableLayoutPanel header = new TableLayoutPanel();
            header.Dock = DockStyle.Fill;
            header.ColumnCount = 2;
            header.RowCount = 1;
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 62F));
            header.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            header.Margin = new Padding(0);

            seatLabel = new Label();
            seatLabel.Dock = DockStyle.Fill;
            seatLabel.AutoSize = false;
            seatLabel.TextAlign = ContentAlignment.MiddleLeft;
            seatLabel.Font = seatFont;
            seatLabel.Text = "Seat " + seat.ToString();
            header.Controls.Add(seatLabel, 0, 0);

            badgeLabel = new Label();
            badgeLabel.Dock = DockStyle.Fill;
            badgeLabel.AutoSize = false;
            badgeLabel.TextAlign = ContentAlignment.MiddleRight;
            badgeLabel.Font = badgeFont;
            header.Controls.Add(badgeLabel, 1, 0);

            root.Controls.Add(header, 0, 0);

            TableLayoutPanel content = new TableLayoutPanel();
            content.Dock = DockStyle.Fill;
            content.ColumnCount = 2;
            content.RowCount = 1;
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58F));
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            content.Margin = new Padding(0);

            portraitBox = new PictureBox();
            portraitBox.Dock = DockStyle.Fill;
            portraitBox.SizeMode = PictureBoxSizeMode.Zoom;
            portraitBox.BorderStyle = BorderStyle.FixedSingle;
            portraitBox.BackColor = Color.FromArgb(236, 238, 242);
            portraitBox.Margin = new Padding(0, 2, 4, 0);
            content.Controls.Add(portraitBox, 0, 0);

            TableLayoutPanel details = new TableLayoutPanel();
            details.Dock = DockStyle.Fill;
            details.ColumnCount = 1;
            details.RowCount = 4;
            details.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            details.RowStyles.Add(new RowStyle(SizeType.Percent, 31F));
            details.RowStyles.Add(new RowStyle(SizeType.Percent, 23F));
            details.RowStyles.Add(new RowStyle(SizeType.Percent, 23F));
            details.RowStyles.Add(new RowStyle(SizeType.Percent, 23F));
            details.Margin = new Padding(0);

            nameLabel = MakeDetailLabel(nameFont);
            actionLabel = MakeDetailLabel(detailFont);
            locationLabel = MakeDetailLabel(detailFont);
            moodLabel = MakeDetailLabel(detailFont);
            details.Controls.Add(nameLabel, 0, 0);
            details.Controls.Add(actionLabel, 0, 1);
            details.Controls.Add(locationLabel, 0, 2);
            details.Controls.Add(moodLabel, 0, 3);

            content.Controls.Add(details, 1, 0);
            root.Controls.Add(content, 0, 1);
            Controls.Add(root);

            WireChildClicks(this);
            SetEmpty();
        }

        private Label MakeDetailLabel(Font font)
        {
            Label label = new Label();
            label.Dock = DockStyle.Fill;
            label.AutoSize = false;
            label.AutoEllipsis = true;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Font = font;
            return label;
        }

        private void WireChildClicks(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                child.Click += delegate { OnClick(EventArgs.Empty); };
                WireChildClicks(child);
            }
        }

        public void SetCharacter(CharacterInfo info, string name, string action,
            string location, string mood, bool isPlayer)
        {
            if (Character == null || Character.Pointer != info.Pointer)
                ClearPortrait();

            Character = info;
            seatLabel.Text = "Seat " + Seat.ToString();
            if (Seat == 24)
            {
                badgeLabel.Text = "TEACHER";
                badgeLabel.ForeColor = Color.FromArgb(150, 100, 0);
            }
            else if (isPlayer)
            {
                badgeLabel.Text = "PC";
                badgeLabel.ForeColor = Color.FromArgb(0, 90, 180);
            }
            else
            {
                badgeLabel.Text = "";
                badgeLabel.ForeColor = Color.DimGray;
            }

            nameLabel.Text = string.IsNullOrEmpty(name) ? "(unnamed)" : name;
            nameLabel.ForeColor = Color.FromArgb(30, 30, 30);
            actionLabel.Text = "Action: " + action;
            locationLabel.Text = "Loc: " + location;
            moodLabel.Text = "Mood: " + mood;

            SetHandCursor(true);
            ApplyBackColor();
        }

        public void SetEmpty()
        {
            Character = null;
            selected = false;
            ClearPortrait();
            seatLabel.Text = "Seat " + Seat.ToString();
            badgeLabel.Text = Seat == 24 ? "TEACHER" : "";
            badgeLabel.ForeColor = Seat == 24 ? Color.FromArgb(150, 100, 0) : Color.DimGray;
            nameLabel.Text = "Empty";
            nameLabel.ForeColor = Color.Gray;
            actionLabel.Text = "";
            locationLabel.Text = "";
            moodLabel.Text = "";
            SetHandCursor(false);
            ApplyBackColor();
            Invalidate();
        }

        public void SetSelected(bool value)
        {
            if (selected == value) return;
            selected = value;
            ApplyBackColor();
            Invalidate();
        }

        private void ApplyBackColor()
        {
            if (selected) BackColor = Color.FromArgb(232, 241, 255);
            else if (Character == null) BackColor = Color.FromArgb(244, 245, 247);
            else BackColor = Color.White;
        }

        private void SetHandCursor(bool hand)
        {
            Cursor cursor = hand ? Cursors.Hand : Cursors.Default;
            Cursor = cursor;
            SetCursorRecursive(this, cursor);
        }

        private static void SetCursorRecursive(Control parent, Cursor cursor)
        {
            foreach (Control child in parent.Controls)
            {
                child.Cursor = cursor;
                SetCursorRecursive(child, cursor);
            }
        }

        public bool MatchesPortrait(uint addr, uint size, string name)
        {
            if (!portraitChecked) return false;
            if (lastPortraitAddr != addr || lastPortraitSize != size) return false;
            if (!string.Equals(lastPortraitName, name, StringComparison.Ordinal)) return false;

            // A failed decode is retried after a short delay instead of
            // re-reading the image on every refresh.
            if (portraitFailed)
                return (DateTime.UtcNow - lastPortraitAttempt).TotalSeconds < 3.0;
            return true;
        }

        public void SetPortrait(Bitmap bitmap, uint addr, uint size, string name)
        {
            if (portraitBox.Image != null)
            {
                Image old = portraitBox.Image;
                portraitBox.Image = null;
                old.Dispose();
            }

            portraitBox.Image = bitmap;
            lastPortraitAddr = addr;
            lastPortraitSize = size;
            lastPortraitName = name;
            portraitChecked = true;
            portraitFailed = bitmap == null;
            lastPortraitAttempt = DateTime.UtcNow;
        }

        public void ClearPortrait()
        {
            if (portraitBox.Image != null)
            {
                Image old = portraitBox.Image;
                portraitBox.Image = null;
                old.Dispose();
            }
            lastPortraitAddr = 0;
            lastPortraitSize = 0;
            lastPortraitName = null;
            portraitChecked = true;
            portraitFailed = false;
            lastPortraitAttempt = DateTime.MinValue;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Color border = selected ? Color.FromArgb(0, 120, 215) : Color.FromArgb(205, 210, 220);
            float width = selected ? 2F : 1F;
            float half = width / 2F;
            float w = Math.Max(0F, Width - width);
            float h = Math.Max(0F, Height - width);
            using (Pen pen = new Pen(border, width))
            {
                e.Graphics.DrawRectangle(pen, half, half, w, h);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ClearPortrait();
                if (seatFont != null) seatFont.Dispose();
                if (badgeFont != null) badgeFont.Dispose();
                if (nameFont != null) nameFont.Dispose();
                if (detailFont != null) detailFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
