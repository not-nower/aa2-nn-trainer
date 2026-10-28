using System;
using System.Drawing;
using System.Windows.Forms;

namespace AA2Trainer
{
    /// <summary>
    /// Gameplay features. Relationship, Behavior History, Lover and Promises
    /// are implemented; Action/Planner remains a placeholder.
    /// </summary>
    public sealed class GameplayControl : UserControl
    {
        public event EventHandler<CharacterSelectedEventArgs> CharacterActivated;

        public Func<bool> ConfirmWriteAllowed { get; set; }

        private TabControl tabs;
        private RelationshipControl relationship;
        private BehaviorHistoryControl behaviorHistory;
        private LoverControl lover;
        private PromisesControl promises;

        public GameplayControl()
        {
            DoubleBuffered = true;
            Dock = DockStyle.Fill;
            BackColor = Color.FromArgb(248, 249, 251);
            Padding = new Padding(8, 6, 8, 6);
            BuildUi();
        }

        private void BuildUi()
        {
            tabs = new TabControl();
            tabs.Dock = DockStyle.Fill;
            tabs.Padding = new Point(12, 6);
            tabs.TabPages.Add(BuildPlaceholderPage("Action/Planner",
                "Action controls and the action planner will be added here."));

            TabPage relationshipPage = new TabPage("Relationship");
            relationshipPage.BackColor = Color.FromArgb(248, 249, 251);
            relationship = new RelationshipControl();
            relationship.Dock = DockStyle.Fill;
            relationship.ConfirmWriteAllowed = delegate
            {
                return ConfirmWriteAllowed == null || ConfirmWriteAllowed();
            };
            relationship.OpenCharacterRequested += delegate(object sender,
                CharacterSelectedEventArgs e)
            {
                if (CharacterActivated != null)
                    CharacterActivated(this, e);
            };
            relationshipPage.Controls.Add(relationship);
            tabs.TabPages.Add(relationshipPage);

            TabPage behaviorPage = new TabPage("Behavior History");
            behaviorPage.BackColor = Color.FromArgb(248, 249, 251);
            behaviorHistory = new BehaviorHistoryControl();
            behaviorHistory.Dock = DockStyle.Fill;
            behaviorHistory.ConfirmWriteAllowed = delegate
            {
                return ConfirmWriteAllowed == null || ConfirmWriteAllowed();
            };
            behaviorPage.Controls.Add(behaviorHistory);
            tabs.TabPages.Add(behaviorPage);

            TabPage loverPage = new TabPage("Lover");
            loverPage.BackColor = Color.FromArgb(248, 249, 251);
            lover = new LoverControl();
            lover.Dock = DockStyle.Fill;
            lover.ConfirmWriteAllowed = delegate
            {
                return ConfirmWriteAllowed == null || ConfirmWriteAllowed();
            };
            lover.OpenCharacterRequested += delegate(object sender,
                CharacterSelectedEventArgs e)
            {
                if (CharacterActivated != null)
                    CharacterActivated(this, e);
            };
            loverPage.Controls.Add(lover);
            tabs.TabPages.Add(loverPage);

            TabPage promisesPage = new TabPage("Promises");
            promisesPage.BackColor = Color.FromArgb(248, 249, 251);
            promises = new PromisesControl();
            promises.Dock = DockStyle.Fill;
            promises.OpenCharacterRequested += delegate(object sender,
                CharacterSelectedEventArgs e)
            {
                if (CharacterActivated != null)
                    CharacterActivated(this, e);
            };
            promisesPage.Controls.Add(promises);
            tabs.TabPages.Add(promisesPage);
            Controls.Add(tabs);
        }

        public void SetTarget(GameMemory mem, GameContext ctx, CharacterInfo selected,
            NameMap names, bool writeAllowed)
        {
            if (relationship != null)
                relationship.SetTarget(mem, ctx, selected, names, writeAllowed);
            if (behaviorHistory != null)
                behaviorHistory.SetTarget(mem, ctx, selected);
            if (lover != null)
                lover.SetTarget(mem, ctx, selected, writeAllowed);
            if (promises != null)
                promises.SetTarget(mem, ctx, selected, names);
        }

        public void SetWriteAllowed(bool allowed)
        {
            if (relationship != null) relationship.SetWriteAllowed(allowed);
            if (behaviorHistory != null) behaviorHistory.SetWriteAllowed(allowed);
            if (lover != null) lover.SetWriteAllowed(allowed);
        }

        public bool ApplyRelationshipFeeling(GameMemory mem, CharacterInfo source,
            CharacterInfo target, int feeling, bool canWrite, out string error)
        {
            error = "The relationship editor is unavailable.";
            return relationship != null && relationship.ApplyRosterFeeling(mem,
                source, target, feeling, canWrite, out error);
        }

        public bool IsRelationshipFrozen(uint sourcePointer, int targetSeat)
        {
            return relationship != null &&
                relationship.IsRosterFrozen(sourcePointer, targetSeat);
        }

        public bool SetRelationshipFrozen(GameMemory mem, CharacterInfo source,
            CharacterInfo target, bool frozen, bool canWrite, out string error)
        {
            error = "The relationship editor is unavailable.";
            return relationship != null && relationship.SetRosterFrozen(mem,
                source, target, frozen, canWrite, out error);
        }

        public void Clear()
        {
            if (relationship != null) relationship.Clear();
            if (behaviorHistory != null) behaviorHistory.Clear();
            if (lover != null) lover.Clear();
            if (promises != null) promises.Clear();
        }

        private static TabPage BuildPlaceholderPage(string title, string description)
        {
            TabPage page = new TabPage(title);
            page.BackColor = Color.FromArgb(248, 249, 251);

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 3;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            root.Padding = new Padding(16, 8, 16, 8);
            page.Controls.Add(root);

            Label heading = new Label();
            heading.Dock = DockStyle.Fill;
            heading.TextAlign = ContentAlignment.BottomCenter;
            heading.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
            heading.ForeColor = Color.FromArgb(45, 50, 60);
            heading.Text = title;
            root.Controls.Add(heading, 0, 0);

            Label subtext = new Label();
            subtext.AutoSize = true;
            subtext.Anchor = AnchorStyles.None;
            subtext.MaximumSize = new Size(620, 0);
            subtext.TextAlign = ContentAlignment.TopCenter;
            subtext.Font = new Font("Segoe UI", 9F);
            subtext.ForeColor = Color.Gray;
            subtext.Text = description;
            root.Controls.Add(subtext, 0, 1);

            return page;
        }
    }
}
