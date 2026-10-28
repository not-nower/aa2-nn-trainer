using System;
using System.Drawing;
using System.Windows.Forms;

namespace AA2Trainer
{
    /// <summary>
    /// Gameplay features. Action/Planner, Relationship, Behavior History,
    /// Lover and Promises are implemented.
    /// </summary>
    public sealed class GameplayControl : UserControl
    {
        public event EventHandler<CharacterSelectedEventArgs> CharacterActivated;
        public event Action<bool> PlannerActiveChanged;

        public Func<bool> ConfirmWriteAllowed { get; set; }

        private TabControl tabs;
        private ActionPlannerControl actionPlanner;
        private RelationshipControl relationship;
        private BehaviorHistoryControl behaviorHistory;
        private LoverControl lover;
        private PromisesControl promises;

        private GameMemory cachedMem;
        private GameContext cachedCtx;
        private CharacterInfo cachedSelected;
        private NameMap cachedNames;
        private bool cachedWriteAllowed;

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
            TabPage actionPage = new TabPage("Action/Planner");
            actionPage.BackColor = Color.FromArgb(248, 249, 251);
            actionPlanner = new ActionPlannerControl();
            actionPlanner.Dock = DockStyle.Fill;
            actionPlanner.ConfirmWriteAllowed = delegate
            {
                return ConfirmWriteAllowed == null || ConfirmWriteAllowed();
            };
            actionPlanner.PlannerActiveChanged += delegate(bool active)
            {
                if (PlannerActiveChanged != null)
                    PlannerActiveChanged(active);
            };
            actionPage.Controls.Add(actionPlanner);
            tabs.TabPages.Add(actionPage);

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
            tabs.SelectedIndexChanged += delegate { RefreshActiveTab(); };
            Controls.Add(tabs);
        }

        /// <summary>
        /// Refreshes only the visible sub-tab. Hidden pages keep their last
        /// write-gate state and are refreshed when they are selected.
        /// </summary>
        private void RefreshActiveTab()
        {
            if (tabs == null || cachedMem == null || cachedCtx == null ||
                cachedSelected == null)
                return;

            switch (tabs.SelectedIndex)
            {
                case 0:
                    if (actionPlanner != null)
                        actionPlanner.SetTarget(cachedMem, cachedCtx,
                            cachedSelected, cachedNames, cachedWriteAllowed);
                    break;
                case 1:
                    if (relationship != null)
                        relationship.SetTarget(cachedMem, cachedCtx,
                            cachedSelected, cachedNames, cachedWriteAllowed);
                    break;
                case 2:
                    if (behaviorHistory != null)
                        behaviorHistory.SetTarget(cachedMem, cachedCtx,
                            cachedSelected);
                    break;
                case 3:
                    if (lover != null)
                        lover.SetTarget(cachedMem, cachedCtx, cachedSelected,
                            cachedWriteAllowed);
                    break;
                case 4:
                    if (promises != null)
                        promises.SetTarget(cachedMem, cachedCtx, cachedSelected,
                            cachedNames);
                    break;
            }
        }

        public void SetTarget(GameMemory mem, GameContext ctx, CharacterInfo selected,
            NameMap names, bool writeAllowed)
        {
            cachedMem = mem;
            cachedCtx = ctx;
            cachedSelected = selected;
            cachedNames = names;
            cachedWriteAllowed = writeAllowed;
            RefreshActiveTab();
        }

        public void SetWriteAllowed(bool allowed)
        {
            cachedWriteAllowed = allowed;
            if (actionPlanner != null) actionPlanner.SetWriteAllowed(allowed);
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
            cachedMem = null;
            cachedCtx = null;
            cachedSelected = null;
            cachedNames = null;
            cachedWriteAllowed = false;
            if (actionPlanner != null) actionPlanner.Clear();
            if (relationship != null) relationship.Clear();
            if (behaviorHistory != null) behaviorHistory.Clear();
            if (lover != null) lover.Clear();
            if (promises != null) promises.Clear();
        }
    }
}
