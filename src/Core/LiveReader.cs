using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace AA2Trainer
{
    /// <summary>
    /// Fast-changing per-character state used by the live roster poller. This
    /// intentionally reads only the action/activity block and skips names,
    /// relationships, portraits, and the rest of the character editor.
    /// </summary>
    public sealed class LiveCharacterState
    {
        public uint Character;
        public uint CharacterData;
        public uint NpcData;
        public uint Status;
        public uint Activity;
        public int Seat = -1;
        public string Name = "";

        // "refto" / m_target on the status block.
        public uint ReffTo;
        public uint NpcTarget;

        public int ActionId = -1;
        public int MovementType = -1;
        public int Location = -1;
        public int MoodAction = -1;

        public byte AnimLocked;
        public byte Loading;
        public byte InConversation;

        public bool IsBusy
        {
            get { return AnimLocked != 0 || Loading != 0 || InConversation != 0; }
        }
    }

    /// <summary>
    /// Status counters for the selected character. These are the fields shown
    /// in Character Editor -> Character Data -> Status / H History.
    /// </summary>
    public sealed class LiveCharacterStatus
    {
        public uint Pointer;
        public int VictoryCount;
        public int ClassSkip;
        public int WinningOver;
        public int Partner;
        public int Rejected;
        public int IntelligenceGrade;
        public int StrengthGrade;
        public int ClubGrade;
        public int IntelligenceTopRank;
        public int StrengthTopRank;
        public int SexIntercourse;
        public int SexPartner;
        public bool SexExperience;
        public bool AnalExperience;
        public string FirstTime = "";
        public string FirstAnal = "";
        public string LastTime = "";
    }

    public sealed class LiveSnapshot
    {
        public long Sequence;
        public long Generation;
        public DateTime TimestampUtc;
        public readonly List<LiveCharacterState> Characters = new List<LiveCharacterState>();
        public LiveCharacterStatus SelectedStatus;
    }

    /// <summary>
    /// Reads compact live state on a background thread. The UI publishes an
    /// immutable roster (pointer/seat/name) and the reader only performs raw
    /// memory reads; no GameContext, NameMap, or controls are touched here.
    /// </summary>
    public sealed class LiveReader
    {
        private sealed class RosterEntry
        {
            public uint Pointer;
            public int Seat;
            public string Name;
        }

        private static readonly RosterEntry[] Empty = new RosterEntry[0];
        private static readonly Encoding Sjis = CreateEncoding();

        private readonly GameMemory mem;
        private readonly object sync = new object();
        private RosterEntry[] roster = Empty;
        private uint selectedPointer;
        private long generation;
        private long sequence;

        public LiveReader(GameMemory memory)
        {
            mem = memory;
        }

        public long Generation
        {
            get { lock (sync) { return generation; } }
        }

        /// <summary>
        /// Replaces the roster the poller reads. Increments the generation so
        /// snapshots built from the previous roster are discarded by the UI.
        /// </summary>
        public long PublishRoster(IList<CharacterInfo> characters, CharacterInfo selected)
        {
            int count = characters == null ? 0 : characters.Count;
            RosterEntry[] next = new RosterEntry[count];
            for (int i = 0; i < count; i++)
            {
                CharacterInfo info = characters[i];
                if (info == null) continue;
                RosterEntry entry = new RosterEntry();
                entry.Pointer = info.Pointer;
                entry.Seat = info.Seat;
                entry.Name = info.Name ?? "";
                next[i] = entry;
            }

            lock (sync)
            {
                uint nextSelected = selected == null ? 0 : selected.Pointer;
                if (nextSelected == selectedPointer && SameRoster(next, roster))
                    return generation;

                roster = next;
                selectedPointer = nextSelected;
                generation++;
                return generation;
            }
        }

        private static bool SameRoster(RosterEntry[] a, RosterEntry[] b)
        {
            if (a == null || b == null) return false;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                RosterEntry x = a[i];
                RosterEntry y = b[i];
                if (x == null || y == null)
                {
                    if (x != y) return false;
                    continue;
                }
                if (x.Pointer != y.Pointer || x.Seat != y.Seat ||
                    !string.Equals(x.Name, y.Name, StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        public void Clear()
        {
            lock (sync)
            {
                roster = Empty;
                selectedPointer = 0;
                generation++;
            }
        }

        /// <summary>
        /// Reads one snapshot. Returns null when there is no roster or the
        /// process is no longer attached.
        /// </summary>
        public LiveSnapshot Read()
        {
            RosterEntry[] entries;
            uint selected;
            long snapshotGeneration;
            lock (sync)
            {
                entries = roster;
                selected = selectedPointer;
                snapshotGeneration = generation;
            }

            if (entries.Length == 0 || mem == null || !mem.IsAttached)
                return null;

            LiveSnapshot snapshot = new LiveSnapshot();
            snapshot.Generation = snapshotGeneration;
            snapshot.Sequence = Interlocked.Increment(ref sequence);
            snapshot.TimestampUtc = DateTime.UtcNow;

            LiveCharacterState selectedState = null;
            for (int i = 0; i < entries.Length; i++)
            {
                RosterEntry entry = entries[i];
                if (entry == null) continue;

                LiveCharacterState state = new LiveCharacterState();
                state.Character = entry.Pointer;
                state.Seat = entry.Seat;
                state.Name = entry.Name;

                uint? status = mem.ReadU32(entry.Pointer + 0xF6C);
                if (status.HasValue && status.Value != 0 && mem.IsPlausiblePointer(status.Value))
                {
                    state.Status = status.Value;
                    ReadActivity(state);
                }

                uint? characterData = mem.ReadU32(entry.Pointer + 0x28);
                if (characterData.HasValue && characterData.Value != 0 &&
                    mem.IsPlausiblePointer(characterData.Value))
                {
                    state.CharacterData = characterData.Value;
                }

                uint? npcData = mem.ReadU32(entry.Pointer + 0xF58);
                if (npcData.HasValue && npcData.Value != 0 &&
                    mem.IsPlausiblePointer(npcData.Value))
                {
                    state.NpcData = npcData.Value;
                    uint? npcTarget = mem.ReadU32(state.NpcData + 0x26C);
                    if (npcTarget.HasValue) state.NpcTarget = npcTarget.Value;
                }

                snapshot.Characters.Add(state);
                if (entry.Pointer == selected) selectedState = state;
            }

            if (selectedState != null && selectedState.Status != 0 &&
                selectedState.CharacterData != 0)
            {
                snapshot.SelectedStatus = ReadStatus(selectedState);
            }

            return snapshot;
        }

        private void ReadActivity(LiveCharacterState state)
        {
            uint? activity = mem.ReadU32(state.Status + 0x28);
            if (!activity.HasValue || activity.Value == 0 ||
                !mem.IsPlausiblePointer(activity.Value))
                return;

            state.Activity = activity.Value;

            // One block read covers the busy flags, reffTo, movement, action,
            // location, and mood. Offsets are relative to activity + 0x14.
            byte[] block = mem.ReadBytes(state.Activity + 0x14, 0x40);
            if (block == null) return;

            state.AnimLocked = block[0x00];
            state.Loading = block[0x01];
            state.InConversation = block[0x05];
            state.ReffTo = BitConverter.ToUInt32(block, 0x24);
            state.MovementType = BitConverter.ToInt32(block, 0x28);
            state.ActionId = BitConverter.ToInt32(block, 0x2C);
            state.Location = BitConverter.ToInt32(block, 0x34);
            state.MoodAction = BitConverter.ToInt32(block, 0x3C);
        }

        private LiveCharacterStatus ReadStatus(LiveCharacterState state)
        {
            // Status block 0x1DC..0x26B: counters, grades, top ranks, and the
            // three 32-byte H history strings.
            byte[] block = mem.ReadBytes(state.Status + 0x1DC, 0x90);
            if (block == null) return null;

            byte[] experience = mem.ReadBytes(state.CharacterData + 0x6C8, 2);

            LiveCharacterStatus status = new LiveCharacterStatus();
            status.Pointer = state.Character;
            status.VictoryCount = BitConverter.ToInt32(block, 0x00);
            status.ClassSkip = BitConverter.ToInt32(block, 0x04);
            status.WinningOver = BitConverter.ToInt32(block, 0x08);
            status.Partner = BitConverter.ToInt32(block, 0x0C);
            status.Rejected = BitConverter.ToInt32(block, 0x10);
            status.IntelligenceGrade = block[0x14];
            status.StrengthGrade = block[0x18];
            status.ClubGrade = block[0x1C];
            status.IntelligenceTopRank = block[0x20];
            status.StrengthTopRank = block[0x24];
            status.SexIntercourse = BitConverter.ToInt32(block, 0x28);
            status.SexPartner = BitConverter.ToInt32(block, 0x2C);
            status.FirstTime = Decode(block, 0x30, 0x20);
            status.FirstAnal = Decode(block, 0x50, 0x20);
            status.LastTime = Decode(block, 0x70, 0x20);
            status.SexExperience = experience != null && experience[0] != 0;
            status.AnalExperience = experience != null && experience[1] != 0;
            return status;
        }

        private static string Decode(byte[] bytes, int offset, int size)
        {
            if (bytes == null || offset < 0 || offset >= bytes.Length) return "";
            int limit = Math.Min(bytes.Length, offset + size);
            int end = offset;
            while (end < limit && bytes[end] != 0) end++;
            if (end == offset) return "";
            try { return Sjis.GetString(bytes, offset, end - offset).Trim(); }
            catch { return ""; }
        }

        private static Encoding CreateEncoding()
        {
            try { return Encoding.GetEncoding(932); }
            catch { return Encoding.Default; }
        }
    }
}
