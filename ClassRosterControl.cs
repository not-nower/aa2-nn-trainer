using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
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
        public event EventHandler<CharacterSelectedEventArgs> CharacterActivated;

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
                    card.DoubleClicked += Card_DoubleClicked;
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
            int w = Math.Max(ClientSize.Width - Padding.Horizontal, 850);
            int h = Math.Max(ClientSize.Height - Padding.Vertical, 800);
            grid.Location = new Point(Padding.Left, Padding.Top);
            grid.Size = new Size(w, h);
        }

        public void RefreshRoster(GameMemory mem, GameContext ctx, CharacterInfo selected, NameMap names)
        {
            if (mem == null || ctx == null || !mem.IsAttached)
            {
                Clear();
                return;
            }

            Dictionary<int, CharacterInfo> bySeat = new Dictionary<int, CharacterInfo>();
            Dictionary<uint, CharacterInfo> byF58 = new Dictionary<uint, CharacterInfo>();
            Dictionary<uint, CharacterInfo> byActivity = new Dictionary<uint, CharacterInfo>();
            if (ctx.Characters != null)
            {
                foreach (CharacterInfo info in ctx.Characters)
                {
                    if (info == null) continue;

                    uint? f58 = mem.ReadU32(info.Pointer + 0xF58);
                    if (f58.HasValue && f58.Value != 0 && !byF58.ContainsKey(f58.Value))
                        byF58[f58.Value] = info;

                    uint activity = ReadActivity(mem, info.Pointer);
                    if (activity != 0 && !byActivity.ContainsKey(activity))
                        byActivity[activity] = info;

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
                int? gender = ReadByte(mem, view, "Gender");
                int? personalityId = ReadByte(mem, view, "Personality");
                uint? npcTarget = ReadUInt(mem, view, "NPCTarget");
                uint? reffTo = ReadUInt(mem, view, "ReffTo");

                string actionText = action.HasValue ? ActionName(names, action.Value) : "-";
                string locationText = location.HasValue ? LocationName(names, location.Value) : "-";
                string moodText = mood.HasValue ? mood.Value.ToString() : "-";
                string targetText = TargetText(mem, ctx, names, npcTarget, reffTo, byF58, byActivity);

                string personalityText = "";
                if (personalityId.HasValue)
                {
                    personalityText = names == null ? null :
                        names.Get("PERSONALITY", personalityId.Value);
                    if (string.IsNullOrEmpty(personalityText))
                        personalityText = "P" + personalityId.Value.ToString();
                }

                string statusText = "";
                string statusTooltip = "";
                bool isLover = false;
                if (selected != null)
                {
                    if (selected.Pointer == info.Pointer)
                    {
                        statusText = "Self";
                        statusTooltip = "Selected character.";
                    }
                    else
                    {
                        RelationshipSnapshot relation = RelationshipReader.Read(mem, info, selected.Seat);
                        statusText = RelationshipLevelText(relation);
                        statusTooltip = RelationshipLevelTooltip(relation);
                        isLover = relation != null && relation.IsLover;
                    }
                }

                card.SetCharacter(info, info.Name, personalityText, actionText,
                    targetText, locationText, moodText, statusText, statusTooltip,
                    gender.HasValue ? gender.Value : -1, isLover, info.IsPlayer);
                card.SetSelected(selected != null && selected.Pointer == info.Pointer);

                uint addr;
                uint size;
                PortraitLoader.ReadInfo(mem, view, out addr, out size);

                if (!card.MatchesPortrait(addr, size, info.Name))
                {
                    card.SetPortrait(PortraitLoader.LoadBitmap(mem, addr, size), addr, size, info.Name);
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

        private void Card_DoubleClicked(object sender, EventArgs e)
        {
            ClassRosterCard card = sender as ClassRosterCard;
            if (card == null || card.Character == null) return;
            if (CharacterActivated != null)
                CharacterActivated(this, new CharacterSelectedEventArgs(card.Character));
        }

        private static int? ReadInt(GameMemory mem, GameContext view, string name)
        {
            EvalResult r = DashboardData.Eval(mem, view, name, 0);
            if (r == null || r.Bytes == null || r.Bytes.Length < 4) return null;
            return BitConverter.ToInt32(r.Bytes, 0);
        }

        private static string RelationshipLevelText(RelationshipSnapshot relation)
        {
            if (relation == null) return "-";
            return "L" + relation.LoveCount + "  K" + relation.LikeCount +
                   "  D" + relation.DislikeCount + "  H" + relation.HateCount;
        }

        private static string RelationshipLevelTooltip(RelationshipSnapshot relation)
        {
            if (relation == null) return "No relationship record.";
            return "Love: " + LevelName(relation.LoveStatus) + " (" + relation.LoveCount + ")\r\n" +
                   "Like: " + LevelName(relation.LikeStatus) + " (" + relation.LikeCount + ")\r\n" +
                   "Dislike: " + LevelName(relation.DislikeStatus) + " (" + relation.DislikeCount + ")\r\n" +
                   "Hate: " + LevelName(relation.HateStatus) + " (" + relation.HateCount + ")";
        }

        private static string LevelName(int status)
        {
            switch (status)
            {
                case 0: return "Lots";
                case 1: return "Some";
                case 2: return "Very Little";
                default: return status.ToString();
            }
        }

        private static string ActionName(NameMap names, int id)
        {
            string name = names == null ? null : names.Get("ACTION_KEY", id);
            if (!string.IsNullOrEmpty(name)) return name;
            return DisplayText.ActionLabel(id);
        }

        private static string LocationName(NameMap names, int id)
        {
            string name = names == null ? null : names.Get("LOCATION", id);
            return string.IsNullOrEmpty(name) ? id.ToString() : name;
        }

        private static uint? ReadUInt(GameMemory mem, GameContext view, string name)
        {
            EvalResult r = DashboardData.Eval(mem, view, name, 0);
            if (r == null || r.Bytes == null || r.Bytes.Length < 4) return null;
            return BitConverter.ToUInt32(r.Bytes, 0);
        }

        private static int? ReadByte(GameMemory mem, GameContext view, string name)
        {
            EvalResult r = DashboardData.Eval(mem, view, name, 0);
            if (r == null || r.Bytes == null || r.Bytes.Length < 1) return null;
            return r.Bytes[0];
        }

        private static uint ReadActivity(GameMemory mem, uint character)
        {
            uint? status = mem.ReadU32(character + 0xF6C);
            if (!status.HasValue || status.Value == 0) return 0;
            uint? activity = mem.ReadU32(status.Value + 0x28);
            return activity.HasValue ? activity.Value : 0;
        }

        private static string TargetText(GameMemory mem, GameContext ctx, NameMap names,
            uint? npcTarget, uint? reffTo,
            Dictionary<uint, CharacterInfo> byF58,
            Dictionary<uint, CharacterInfo> byActivity)
        {
            CharacterInfo target = null;

            if (npcTarget.HasValue && npcTarget.Value != 0)
            {
                if (!byF58.TryGetValue(npcTarget.Value, out target))
                    target = ctx.FindByPointer(npcTarget.Value);
            }

            if (target == null && reffTo.HasValue && reffTo.Value != 0)
            {
                if (!byActivity.TryGetValue(reffTo.Value, out target))
                    target = ctx.FindByPointer(reffTo.Value);
            }

            if (target == null)
            {
                bool hasReference =
                    (npcTarget.HasValue && npcTarget.Value != 0) ||
                    (reffTo.HasValue && reffTo.Value != 0);
                return hasReference ? "NPC" : "";
            }

            string text = target.Seat >= 0
                ? target.Seat.ToString("D2") + " " + target.Name
                : target.Name;

            int? targetLocation = ReadLocation(mem, target.Pointer);
            if (targetLocation.HasValue && targetLocation.Value != 0)
            {
                string locationName = names == null ? null :
                    names.Get("LOCATION", targetLocation.Value);
                if (string.IsNullOrEmpty(locationName))
                    locationName = targetLocation.Value.ToString();
                text += " -> " + locationName;
            }

            return text;
        }

        private static int? ReadLocation(GameMemory mem, uint character)
        {
            uint? status = mem.ReadU32(character + 0xF6C);
            if (!status.HasValue || status.Value == 0) return null;
            uint? activity = mem.ReadU32(status.Value + 0x28);
            if (!activity.HasValue) return null;
            return mem.ReadI32(activity.Value + 0x48);
        }

    }

    public sealed class ClassRosterCard : Panel
    {
        public int Seat { get; private set; }
        public CharacterInfo Character { get; private set; }
        public event EventHandler DoubleClicked;

        private readonly Label seatLabel;
        private readonly Label badgeLabel;
        private readonly Label nameLabel;
        private readonly Label actionLabel;
        private readonly Label targetLabel;
        private readonly Label locationLabel;
        private readonly Label moodLabel;
        private readonly Label statusLabel;
        private readonly Label personalityLabel;
        private readonly PictureBox portraitBox;
        private readonly ToolTip tooltip;

        private readonly Font seatFont;
        private readonly Font badgeFont;
        private readonly Font nameFont;
        private readonly Font detailFont;
        private readonly Font targetFont;
        private readonly Font personalityFont;
        private readonly Font statusFont;

        private bool selected;
        private int gender = -1;
        private bool isLover;
        private bool portraitChecked;
        private bool portraitFailed;
        private DateTime lastPortraitAttempt;
        private uint lastPortraitAddr;
        private uint lastPortraitSize;
        private string lastPortraitName;
        private DateTime lastDoubleClick;

        private static readonly Font HeartFont = new Font("Segoe UI Symbol", 21F, FontStyle.Bold);

        public ClassRosterCard(int seat)
        {
            Seat = seat;
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = Color.White;
            Margin = new Padding(4);
            Padding = new Padding(5);
            MinimumSize = new Size(140, 154);

            seatFont = new Font("Segoe UI", 8F, FontStyle.Bold);
            badgeFont = new Font("Segoe UI", 7F, FontStyle.Bold);
            nameFont = new Font("Segoe UI", 9F, FontStyle.Bold);
            detailFont = new Font("Segoe UI", 7F);
            targetFont = new Font("Segoe UI", 7F);
            personalityFont = new Font("Segoe UI", 7F);
            statusFont = new Font("Segoe UI", 7F);

            tooltip = new ToolTip();
            tooltip.AutoPopDelay = 10000;
            tooltip.InitialDelay = 300;
            tooltip.ReshowDelay = 100;
            DoubleClick += delegate { RaiseDoubleClicked(); };

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 3;
            root.RowCount = 2;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80F));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 1F));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 92F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            root.Margin = new Padding(0);
            root.Padding = new Padding(0);

            TableLayoutPanel portraitColumn = new TableLayoutPanel();
            portraitColumn.Dock = DockStyle.Fill;
            portraitColumn.ColumnCount = 1;
            portraitColumn.RowCount = 1;
            portraitColumn.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            portraitColumn.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            portraitColumn.Margin = new Padding(0);

            portraitBox = new PictureBox();
            portraitBox.Dock = DockStyle.Fill;
            portraitBox.SizeMode = PictureBoxSizeMode.Zoom;
            portraitBox.BorderStyle = BorderStyle.FixedSingle;
            portraitBox.BackColor = Color.FromArgb(236, 238, 242);
            portraitBox.Margin = new Padding(0);
            portraitBox.Paint += PortraitBox_Paint;
            portraitColumn.Controls.Add(portraitBox, 0, 0);

            root.Controls.Add(portraitColumn, 0, 0);

            Panel divider = new Panel();
            divider.Dock = DockStyle.Fill;
            divider.BackColor = Color.FromArgb(210, 215, 225);
            divider.Margin = new Padding(0, 2, 0, 2);
            root.Controls.Add(divider, 1, 0);

            TableLayoutPanel topDetails = new TableLayoutPanel();
            topDetails.Dock = DockStyle.Fill;
            topDetails.ColumnCount = 1;
            topDetails.RowCount = 4;
            topDetails.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            topDetails.RowStyles.Add(new RowStyle(SizeType.Percent, 34F));
            topDetails.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
            topDetails.RowStyles.Add(new RowStyle(SizeType.Percent, 22F));
            topDetails.RowStyles.Add(new RowStyle(SizeType.Percent, 24F));
            topDetails.Margin = new Padding(6, 0, 0, 0);

            nameLabel = MakeDetailLabel(nameFont);
            topDetails.Controls.Add(nameLabel, 0, 0);

            TableLayoutPanel meta = new TableLayoutPanel();
            meta.Dock = DockStyle.Fill;
            meta.ColumnCount = 2;
            meta.RowCount = 1;
            meta.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            meta.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 30F));
            meta.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            meta.Margin = new Padding(0);

            badgeLabel = new Label();
            badgeLabel.Dock = DockStyle.Fill;
            badgeLabel.AutoSize = false;
            badgeLabel.TextAlign = ContentAlignment.MiddleLeft;
            badgeLabel.Font = badgeFont;
            badgeLabel.Margin = new Padding(0);
            meta.Controls.Add(badgeLabel, 0, 0);

            seatLabel = new Label();
            seatLabel.Dock = DockStyle.Fill;
            seatLabel.AutoSize = false;
            seatLabel.TextAlign = ContentAlignment.MiddleRight;
            seatLabel.Font = seatFont;
            seatLabel.Text = seat.ToString();
            seatLabel.Margin = new Padding(0);
            meta.Controls.Add(seatLabel, 1, 0);

            topDetails.Controls.Add(meta, 0, 2);

            personalityLabel = new Label();
            personalityLabel.Dock = DockStyle.Fill;
            personalityLabel.AutoSize = false;
            personalityLabel.TextAlign = ContentAlignment.MiddleLeft;
            personalityLabel.Font = personalityFont;
            personalityLabel.ForeColor = Color.DimGray;
            personalityLabel.Margin = new Padding(0);
            topDetails.Controls.Add(personalityLabel, 0, 1);

            root.Controls.Add(topDetails, 2, 0);

            TableLayoutPanel liveDetails = new TableLayoutPanel();
            liveDetails.Dock = DockStyle.Fill;
            liveDetails.ColumnCount = 1;
            liveDetails.RowCount = 4;
            liveDetails.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (int i = 0; i < 4; i++)
                liveDetails.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));
            liveDetails.Margin = new Padding(0, 2, 0, 0);

            actionLabel = MakeDetailLabel(detailFont);
            targetLabel = MakeDetailLabel(targetFont);
            targetLabel.AutoEllipsis = true;
            locationLabel = MakeDetailLabel(detailFont);
            moodLabel = MakeDetailLabel(detailFont);
            liveDetails.Controls.Add(actionLabel, 0, 0);
            liveDetails.Controls.Add(targetLabel, 0, 1);
            liveDetails.Controls.Add(locationLabel, 0, 2);
            liveDetails.Controls.Add(moodLabel, 0, 3);

            root.Controls.Add(liveDetails, 0, 1);
            root.SetColumnSpan(liveDetails, 3);

            statusLabel = new Label();
            statusLabel.Dock = DockStyle.Fill;
            statusLabel.AutoSize = false;
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            statusLabel.Font = statusFont;
            statusLabel.ForeColor = Color.FromArgb(72, 76, 86);
            statusLabel.BackColor = Color.FromArgb(247, 248, 250);
            statusLabel.Padding = new Padding(0);
            statusLabel.Margin = new Padding(0);
            statusLabel.Text = "";
            topDetails.Controls.Add(statusLabel, 0, 3);

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
                child.DoubleClick += delegate { RaiseDoubleClicked(); };
                WireChildClicks(child);
            }
        }

        private void RaiseDoubleClicked()
        {
            if (Character == null) return;
            if ((DateTime.UtcNow - lastDoubleClick).TotalMilliseconds < 250) return;
            lastDoubleClick = DateTime.UtcNow;
            if (DoubleClicked != null) DoubleClicked(this, EventArgs.Empty);
        }

        public void SetCharacter(CharacterInfo info, string name, string personality,
            string action, string target, string location, string mood, string status,
            string statusTooltip, int genderValue, bool isLoverValue, bool isPlayer)
        {
            if (Character == null || Character.Pointer != info.Pointer)
                ClearPortrait();

            Character = info;
            gender = genderValue;
            if (isLover != isLoverValue)
            {
                isLover = isLoverValue;
                portraitBox.Invalidate();
            }
            seatLabel.Text = Seat.ToString();
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
            personalityLabel.Text = personality == null ? "" : personality;
            actionLabel.Text = "Action: " + action;
            targetLabel.Text = string.IsNullOrEmpty(target) ? "" : "Target: " + target;
            locationLabel.Text = "Loc: " + location;
            moodLabel.Text = "Mood: " + mood;
            statusLabel.Text = status == null ? "" : status;

            tooltip.SetToolTip(statusLabel, statusTooltip);
            tooltip.SetToolTip(actionLabel, actionLabel.Text);
            tooltip.SetToolTip(targetLabel, targetLabel.Text);
            tooltip.SetToolTip(locationLabel, locationLabel.Text);
            tooltip.SetToolTip(moodLabel, moodLabel.Text);

            SetHandCursor(true);
            ApplyBackColor();
        }

        public void SetEmpty()
        {
            Character = null;
            selected = false;
            gender = -1;
            if (isLover)
            {
                isLover = false;
                portraitBox.Invalidate();
            }
            ClearPortrait();
            seatLabel.Text = Seat.ToString();
            badgeLabel.Text = Seat == 24 ? "TEACHER" : "";
            badgeLabel.ForeColor = Seat == 24 ? Color.FromArgb(150, 100, 0) : Color.DimGray;
            nameLabel.Text = "Empty";
            nameLabel.ForeColor = Color.Gray;
            personalityLabel.Text = "";
            actionLabel.Text = "";
            targetLabel.Text = "";
            locationLabel.Text = "";
            moodLabel.Text = "";
            statusLabel.Text = "";
            tooltip.SetToolTip(statusLabel, null);
            tooltip.SetToolTip(actionLabel, null);
            tooltip.SetToolTip(targetLabel, null);
            tooltip.SetToolTip(locationLabel, null);
            tooltip.SetToolTip(moodLabel, null);
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
            Color color;
            if (Character == null)
            {
                color = Color.FromArgb(244, 245, 247);
            }
            else if (gender == 0)
            {
                color = selected ? Color.FromArgb(188, 216, 244) : Color.FromArgb(210, 230, 250);
            }
            else if (gender == 1)
            {
                color = selected ? Color.FromArgb(255, 218, 230) : Color.FromArgb(255, 235, 242);
            }
            else
            {
                color = selected ? Color.FromArgb(232, 241, 255) : Color.White;
            }

            BackColor = color;
            if (statusLabel != null) statusLabel.BackColor = color;
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

        private void PortraitBox_Paint(object sender, PaintEventArgs e)
        {
            if (!isLover) return;

            Image image = portraitBox.Image;
            if (image == null || image.Width <= 0 || image.Height <= 0) return;

            Rectangle client = portraitBox.ClientRectangle;
            if (client.Width <= 0 || client.Height <= 0) return;

            // PictureBox.SizeMode is Zoom, so the image may be letterboxed
            // inside the control. Put the heart on the rendered image, not on
            // the control's corner.
            float scale = Math.Min((float)client.Width / image.Width,
                                   (float)client.Height / image.Height);
            int drawWidth = (int)(image.Width * scale);
            int drawHeight = (int)(image.Height * scale);
            int drawX = client.Left + (client.Width - drawWidth) / 2;
            int drawY = client.Top + (client.Height - drawHeight) / 2;
            Rectangle imageRect = new Rectangle(drawX, drawY, drawWidth, drawHeight);

            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
            const string heart = "\u2665";
            using (GraphicsPath path = new GraphicsPath())
            using (StringFormat format = StringFormat.GenericTypographic)
            {
                path.AddString(heart, HeartFont.FontFamily, (int)HeartFont.Style,
                    HeartFont.Size, PointF.Empty, format);
                RectangleF bounds = path.GetBounds();
                if (bounds.Width <= 0F || bounds.Height <= 0F) return;

                float x = imageRect.Right - bounds.Width - 2F;
                float y = imageRect.Top + 1F;
                if (x < imageRect.Left) x = imageRect.Left;
                if (y + bounds.Height > imageRect.Bottom)
                    y = Math.Max(imageRect.Top, imageRect.Bottom - bounds.Height);

                using (Matrix move = new Matrix())
                {
                    move.Translate(x - bounds.X, y - bounds.Y);
                    path.Transform(move);
                }

                using (GraphicsPath shadow = (GraphicsPath)path.Clone())
                using (Matrix offset = new Matrix())
                {
                    offset.Translate(1F, 1F);
                    shadow.Transform(offset);
                    using (Brush shadowBrush = new SolidBrush(Color.FromArgb(220, 255, 255, 255)))
                        e.Graphics.FillPath(shadowBrush, shadow);
                }
                using (Brush brush = new SolidBrush(Color.FromArgb(225, 35, 85)))
                    e.Graphics.FillPath(brush, path);
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
                if (targetFont != null) targetFont.Dispose();
                if (personalityFont != null) personalityFont.Dispose();
                if (statusFont != null) statusFont.Dispose();
                if (tooltip != null) tooltip.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
