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
            Characters.Clear();
            ModuleBase = mem.ModuleBase;

            uint? root = mem.ReadU32(ModuleBase + 0x376164);
            if (!root.HasValue || root.Value == 0) return false;
            GlobalRoot = root.Value;

            uint? classData = mem.ReadU32(GlobalRoot + 0x28);
            ClassData = classData.HasValue ? classData.Value : 0;

            uint? array = mem.ReadU32(GlobalRoot + 0x6C);
            ClassArray = array.HasValue ? array.Value : 0;
            uint? arrayEnd = mem.ReadU32(GlobalRoot + 0x70);
            ClassArrayEnd = arrayEnd.HasValue ? arrayEnd.Value : 0;

            uint? player = mem.ReadU32(GlobalRoot + 0x88);
            uint selectedPointer = preferredPointer;
            if (selectedPointer == 0 && player.HasValue) selectedPointer = player.Value;

            // Collect every loaded class member. The array order is not the seat order.
            int count = ClassArrayCount;
            if (count <= 0) count = 25;
            for (int i = 0; i < count; i++)
            {
                uint? p = mem.ReadU32(ClassArray + (uint)(i * 4));
                if (!p.HasValue || !mem.IsPlausiblePointer(p.Value)) continue;
                if (FindByPointer(p.Value) != null) continue;

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
                info.IsPlayer = player.HasValue && p.Value == player.Value;
                Characters.Add(info);
            }

            // Always include the current player pointer, even if the array is unreadable.
            if (player.HasValue && mem.IsPlausiblePointer(player.Value) && FindByPointer(player.Value) == null)
            {
                CharacterInfo info = new CharacterInfo();
                info.Pointer = player.Value;
                info.Seat = -1;
                info.IsPlayer = true;
                uint? cd = mem.ReadU32(player.Value + 0x28);
                if (cd.HasValue && mem.IsPlausiblePointer(cd.Value))
                {
                    string first = mem.ReadAnsiString(cd.Value + 0x3D, 96);
                    string last = mem.ReadAnsiString(cd.Value + 0x141, 96);
                    info.Name = ((first == null ? "" : first.Trim()) + " " +
                                 (last == null ? "" : last.Trim())).Trim();
                }
                if (info.Name.Length == 0) info.Name = "(player)";
                Characters.Add(info);
            }

            Characters.Sort(delegate (CharacterInfo a, CharacterInfo b)
            {
                if (a.IsPlayer != b.IsPlayer) return a.IsPlayer ? -1 : 1;
                int c = a.Seat.CompareTo(b.Seat);
                if (c != 0) return c;
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });

            Selected = null;
            if (selectedPointer != 0) Selected = FindByPointer(selectedPointer);
            if (Selected == null && Characters.Count > 0) Selected = Characters[0];
            if (Selected == null) return false;

            SelectedChar = Selected.Pointer;
            UpdateDerivedPointers(mem);
            return true;
        }

        public void Select(GameMemory mem, CharacterInfo info)
        {
            Selected = info;
            if (info == null) return;
            SelectedChar = info.Pointer;
            UpdateDerivedPointers(mem);
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
