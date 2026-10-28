using System;
using System.Collections.Generic;

namespace AA2Trainer
{
    public sealed class CharacterInfo
    {
        public uint Pointer;
        public int Seat;
        public string Name;
        public bool IsPlayer;

        public override string ToString()
        {
            string seat = Seat >= 0 ? Seat.ToString("D2") : "??";
            return seat + " - " + (string.IsNullOrEmpty(Name) ? "(unnamed)" : Name) +
                   (IsPlayer ? "  [Player]" : "");
        }
    }

    public sealed class GameContext
    {
        public uint ModuleBase;
        public uint GlobalRoot;       // S = [AA2Play.exe + 0x376164]
        public uint ClassData;        // [S + 0x28]
        public uint ClassArray;       // [S + 0x6C]
        public uint ClassArrayEnd;    // [S + 0x70]
        public uint SelectedChar;     // [S + 0x88] (or the character picked in the UI)
        public uint SelectedCharData; // [SelectedChar + 0x28]
        public uint Status;           // [SelectedChar + 0xF6C]
        public uint Activity;         // [Status + 0x28]
        public uint Relations;        // [[SelectedChar + 0xF64] + 0x16A18]

        public List<CharacterInfo> Characters = new List<CharacterInfo>();
        public CharacterInfo Selected;

        /// <summary>
        /// Increments whenever the cached roster is rebuilt. UI consumers use
        /// this instead of building a pointer/seat/name signature every tick.
        /// </summary>
        public int RosterVersion { get; private set; }

        private const int RosterRescanMs = 5000;
        private uint cachedModuleBase;
        private uint cachedClassArray;
        private uint cachedClassArrayEnd;
        private uint cachedPlayer;
        private bool rosterCached;
        private DateTime rosterCachedUtc;

        public int ClassArrayCount
        {
            get
            {
                if (ClassArray == 0 || ClassArrayEnd <= ClassArray) return 0;
                long n = ((long)ClassArrayEnd - (long)ClassArray) / 4L;
                if (n < 0 || n > 64) return 0;
                return (int)n;
            }
        }

        public bool Refresh(GameMemory mem, uint preferredPointer)
        {
            ModuleBase = mem.ModuleBase;

            uint? root = mem.ReadU32(ModuleBase + 0x376164);
            if (!root.HasValue || root.Value == 0)
            {
                ResetRoster();
                return false;
            }
            GlobalRoot = root.Value;

            uint? classData = mem.ReadU32(GlobalRoot + 0x28);
            ClassData = classData.HasValue ? classData.Value : 0;

            uint? array = mem.ReadU32(GlobalRoot + 0x6C);
            ClassArray = array.HasValue ? array.Value : 0;
            uint? arrayEnd = mem.ReadU32(GlobalRoot + 0x70);
            ClassArrayEnd = arrayEnd.HasValue ? arrayEnd.Value : 0;

            uint? player = mem.ReadU32(GlobalRoot + 0x88);
            uint playerPointer = player.HasValue ? player.Value : 0;
            uint selectedPointer = preferredPointer;
            if (selectedPointer == 0 && playerPointer != 0)
                selectedPointer = playerPointer;

            // The roster only changes on a class load or character swap. Most
            // 1 s refreshes can reuse the cached CharacterInfo objects instead
            // of re-reading every pointer and name. A periodic full rescan is
            // kept as a backstop for a reload that reuses the same addresses.
            DateTime now = DateTime.UtcNow;
            bool rebuild = !rosterCached ||
                ModuleBase != cachedModuleBase ||
                ClassArray != cachedClassArray ||
                ClassArrayEnd != cachedClassArrayEnd ||
                playerPointer != cachedPlayer ||
                (now - rosterCachedUtc).TotalMilliseconds >= RosterRescanMs;

            if (rebuild)
            {
                List<CharacterInfo> rebuilt = new List<CharacterInfo>();
                HashSet<uint> seen = new HashSet<uint>();

                // Collect every loaded class member. The array order is not the seat order.
                int count = ClassArrayCount;
                if (count <= 0) count = 25;
                for (int i = 0; i < count; i++)
                {
                    uint? p = mem.ReadU32(ClassArray + (uint)(i * 4));
                    if (!p.HasValue || !mem.IsPlausiblePointer(p.Value)) continue;
                    if (!seen.Add(p.Value)) continue;

                    uint? cd = mem.ReadU32(p.Value + 0x28);
                    if (!cd.HasValue || !mem.IsPlausiblePointer(cd.Value)) continue;

                    CharacterInfo info = new CharacterInfo();
                    info.Pointer = p.Value;
                    byte? seat = mem.ReadU8(p.Value + 0x3C);
                    info.Seat = seat.HasValue && seat.Value < 25 ? (int)seat.Value : -1;

                    string first = mem.ReadAnsiString(cd.Value + 0x3D, 96);
                    string last = mem.ReadAnsiString(cd.Value + 0x141, 96);
                    first = first == null ? "" : first.Trim();
                    last = last == null ? "" : last.Trim();
                    info.Name = (first + " " + last).Trim();
                    info.IsPlayer = playerPointer != 0 && p.Value == playerPointer;
                    rebuilt.Add(info);
                }

                // Always include the current player pointer, even if the array is unreadable.
                if (playerPointer != 0 && mem.IsPlausiblePointer(playerPointer) &&
                    !seen.Contains(playerPointer))
                {
                    CharacterInfo info = new CharacterInfo();
                    info.Pointer = playerPointer;
                    info.Seat = -1;
                    info.IsPlayer = true;
                    uint? cd = mem.ReadU32(playerPointer + 0x28);
                    if (cd.HasValue && mem.IsPlausiblePointer(cd.Value))
                    {
                        string first = mem.ReadAnsiString(cd.Value + 0x3D, 96);
                        string last = mem.ReadAnsiString(cd.Value + 0x141, 96);
                        info.Name = ((first == null ? "" : first.Trim()) + " " +
                                     (last == null ? "" : last.Trim())).Trim();
                    }
                    if (info.Name.Length == 0) info.Name = "(player)";
                    rebuilt.Add(info);
                }

                rebuilt.Sort(delegate (CharacterInfo a, CharacterInfo b)
                {
                    if (a.IsPlayer != b.IsPlayer) return a.IsPlayer ? -1 : 1;
                    int c = a.Seat.CompareTo(b.Seat);
                    if (c != 0) return c;
                    return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                });

                // A periodic rescan can return the same roster. Only replace the
                // cached objects and bump the version when something changed, so
                // the seat list and planner dropdowns are not rebuilt every 5 s.
                if (!rosterCached || !SameRoster(Characters, rebuilt))
                {
                    Characters.Clear();
                    Characters.AddRange(rebuilt);
                    RosterVersion++;
                }

                cachedModuleBase = ModuleBase;
                cachedClassArray = ClassArray;
                cachedClassArrayEnd = ClassArrayEnd;
                cachedPlayer = playerPointer;
                rosterCached = true;
                rosterCachedUtc = now;
            }

            Selected = null;
            if (selectedPointer != 0) Selected = FindByPointer(selectedPointer);
            if (Selected == null && Characters.Count > 0) Selected = Characters[0];
            if (Selected == null) return false;

            SelectedChar = Selected.Pointer;
            UpdateDerivedPointers(mem);
            return true;
        }

        private void ResetRoster()
        {
            bool hadRoster = rosterCached || Characters.Count > 0;
            Characters.Clear();
            Selected = null;
            rosterCached = false;
            cachedModuleBase = 0;
            cachedClassArray = 0;
            cachedClassArrayEnd = 0;
            cachedPlayer = 0;
            if (hadRoster) RosterVersion++;
        }

        private static bool SameRoster(List<CharacterInfo> a, List<CharacterInfo> b)
        {
            if (a == null || b == null) return false;
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
            {
                CharacterInfo x = a[i];
                CharacterInfo y = b[i];
                if (x == null || y == null)
                {
                    if (x != y) return false;
                    continue;
                }
                if (x.Pointer != y.Pointer || x.Seat != y.Seat ||
                    x.IsPlayer != y.IsPlayer ||
                    !string.Equals(x.Name, y.Name, StringComparison.Ordinal))
                    return false;
            }
            return true;
        }

        public void Select(GameMemory mem, CharacterInfo info)
        {
            Selected = info;
            if (info == null) return;
            SelectedChar = info.Pointer;
            UpdateDerivedPointers(mem);
        }

        // Read-only view of one character without changing the global selection.
        public GameContext CreateCharacterView(GameMemory mem, CharacterInfo info)
        {
            GameContext view = new GameContext();
            view.ModuleBase = ModuleBase;
            view.GlobalRoot = GlobalRoot;
            view.Selected = info;
            view.SelectedChar = info == null ? 0 : info.Pointer;
            if (view.SelectedChar != 0) view.UpdateDerivedPointers(mem);
            return view;
        }

        private void UpdateDerivedPointers(GameMemory mem)
        {
            uint? cd = mem.ReadU32(SelectedChar + 0x28);
            SelectedCharData = cd.HasValue ? cd.Value : 0;

            uint? status = mem.ReadU32(SelectedChar + 0xF6C);
            Status = status.HasValue ? status.Value : 0;
            uint? activity = Status != 0 ? mem.ReadU32(Status + 0x28) : null;
            Activity = activity.HasValue ? activity.Value : 0;

            Relations = 0;
            uint? moreData = mem.ReadU32(SelectedChar + 0xF64);
            if (moreData.HasValue && moreData.Value != 0)
            {
                uint? rel = mem.ReadU32(moreData.Value + 0x16A18);
                if (rel.HasValue) Relations = rel.Value;
            }
        }

        public CharacterInfo FindByPointer(uint pointer)
        {
            foreach (CharacterInfo c in Characters)
                if (c.Pointer == pointer) return c;
            return null;
        }

        public string SelectedName
        {
            get
            {
                if (Selected == null) return "(no character)";
                return Selected.Seat.ToString("D2") + " " + Selected.Name;
            }
        }
    }
}
