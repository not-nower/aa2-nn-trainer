namespace AA2Trainer
{
    /// <summary>
    /// Shared state predicates used by the single-action planner and the plan
    /// runner. Kept free of game calls so both paths agree on "idle" and
    /// "the step's action is visible".
    /// </summary>
    public static class PlannerPrimitives
    {
        public static bool IsIdle(CharacterSummary s)
        {
            if (s == null) return false;
            if (s.AnimLocked != 0 || s.Loading != 0 || s.InConversation != 0)
                return false;
            return s.MovementType == 0 || s.MovementType == 1;
        }

        public static bool MatchesStep(CharacterSummary s, ActionParams p)
        {
            if (s == null || p == null) return false;
            if (p.MovementType == ActionParams.MoveToRoomType)
                return s.MovementType == ActionParams.MoveToRoomType ||
                       s.Location == p.RoomTarget;
            return p.ActionId >= 0 && s.ActionId == p.ActionId;
        }
    }
}
