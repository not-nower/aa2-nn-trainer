using System.Collections.Generic;

namespace AA2Trainer.WpfPrototype
{
    public enum GameplayWriteKind
    {
        RelationshipValues,
        RelationshipBatch,
        HistoryEvents,
        HistoryAll,
        LoverBatch
    }

    public sealed class RelationshipEdit
    {
        public uint SourcePointer;
        public int TargetSeat;
        public int[] Counts;
        public int[] Points;
        public int[] Levels;
        public int Latest;
        public bool WriteLatest;
    }

    public sealed class LoverEdit
    {
        public uint SourcePointer;
        public int TargetSeat;
        public int Days;
        public int Compatibility;
        public bool WriteDays;
        public bool WriteCompatibility;
    }

    public sealed class GameplayWriteRequest
    {
        public GameplayWriteKind Kind;
        public uint SourcePointer;
        public int TargetSeat;
        public int[] Counts;
        public int[] Points;
        public int[] Levels;
        public int Latest;
        public bool WriteLatest;
        public int[] EventIndices;
        public int[] EventFeelings;
        public int Feeling;
        public List<RelationshipEdit> RelationshipEdits;
        public List<LoverEdit> LoverEdits;
    }

    public sealed class GameplayWriteResult
    {
        public GameplayWriteKind Kind;
        public uint SourcePointer;
        public int TargetSeat;
        public bool Success;
        public string Error = "";
        public string Changed = "";
    }
}
