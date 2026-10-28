using System;
using System.Collections.Generic;

namespace AA2Trainer
{
    public sealed class CharacterState
    {
        public uint Character;
        public uint CharacterData;
        public uint Status;
        public uint Activity;
        public uint Ai;
        public uint NpcData;

        // "refto" / m_target: the conversation about target, not the walk target.
        public uint TargetActivity;
        public uint TargetChar;

        // Walk target is stored in the movement descriptor:
        //   descriptor = [activity + 0xE4]
        //   base       = descriptor + [[descriptor] + 8]
        //   holder     = [[base + 4] + 4]
        //   movement   = [holder + base + 0x130]
        //   target     = [holder + base + 0x158]
        public uint Descriptor;
        public uint MovementHolder;
        public uint WalkTargetInterface;
        public uint WalkTargetChar;

        // Action 107 writes the look-at target descriptor to activity + 0xD8.
        public uint StareTargetInterface;
        public uint StareTargetChar;

        public uint RoomPointer;
        public int Room = -1;
        public uint TargetNpc;

        public int MovementMirror;
        public int MovementType = -1;
        public int ActionId = -1;
        public int ConversationId = -1;
        public int Location = -1;
        public int MoodAction = -1;
        public int Response = -1;
        public int InstructionWait = -1;
        public int InstructionTime = -1;

        public byte Gender;
        public byte AnimLocked;
        public byte Loading;
        public byte InConversation;
        public byte Talking;

        public bool IsBusy
        {
            get { return AnimLocked != 0 || Loading != 0 || InConversation != 0; }
        }
    }

    public sealed class CharacterSummary
    {
        public uint Character;
        public uint Status;
        public uint Activity;
        public uint NpcData;
        public int Room = -1;
        public int ActionId = -1;
        public int MovementType = -1;
        public byte AnimLocked;
        public byte Loading;
        public byte InConversation;

        public bool IsBusy
        {
            get { return AnimLocked != 0 || Loading != 0 || InConversation != 0; }
        }
    }

    /// <summary>
    /// Reads the per-character activity block used by the action planner.
    /// The roster itself comes from GameContext; this reader only resolves the
    /// activity/movement/room details for a character pointer.
    /// </summary>
    public sealed class ActionStateReader
    {
        private readonly GameMemory mem;

        public ActionStateReader(GameMemory mem)
        {
            this.mem = mem;
        }

        public CharacterState ReadState(uint character,
            IList<CharacterInfo> characters)
        {
            if (mem == null || character == 0) return null;

            uint? status = mem.ReadU32(character + 0xF6C);
            if (!status.HasValue || status.Value == 0) return null;

            uint? activity = mem.ReadU32(status.Value + 0x28);
            if (!activity.HasValue || activity.Value == 0) return null;

            CharacterState s = new CharacterState();
            s.Character = character;
            s.Status = status.Value;
            s.Activity = activity.Value;
            s.CharacterData = mem.ReadU32(character + 0x28) ?? 0;
            s.Gender = s.CharacterData != 0
                ? (mem.ReadU8(s.CharacterData + 0x3C) ?? (byte)0)
                : (byte)0;

            s.Ai = mem.ReadU32(activity.Value + 0xEC) ?? 0;
            s.TargetActivity = mem.ReadU32(activity.Value + 0x38) ?? 0;
            s.TargetChar = s.TargetActivity != 0
                ? (mem.ReadU32(s.TargetActivity + 0xE0) ?? 0)
                : 0;

            ReadMovement(activity.Value, s, characters);

            s.StareTargetInterface = mem.ReadU32(activity.Value + 0xD8) ?? 0;
            if (s.StareTargetInterface != 0)
            {
                CharacterInfo stare = FindByInterface(s.StareTargetInterface,
                    characters);
                s.StareTargetChar = stare != null ? stare.Pointer : 0;
            }

            s.NpcData = mem.ReadU32(character + 0xF58) ?? 0;
            if (s.NpcData != 0)
            {
                s.RoomPointer = mem.ReadU32(s.NpcData + 0x1F4) ?? 0;
                if (s.RoomPointer != 0)
                    s.Room = mem.ReadI32(s.RoomPointer + 0x14) ?? -1;
                s.TargetNpc = mem.ReadU32(s.NpcData + 0x26C) ?? 0;
            }

            s.MovementType = mem.ReadI32(activity.Value + 0x3C) ?? -1;
            s.ActionId = mem.ReadI32(activity.Value + 0x40) ?? -1;
            s.ConversationId = mem.ReadI32(activity.Value + 0x44) ?? -1;
            s.Location = mem.ReadI32(activity.Value + 0x48) ?? -1;
            s.MoodAction = mem.ReadI32(activity.Value + 0x50) ?? -1;
            s.Response = mem.ReadI32(activity.Value + 0x5C) ?? -1;
            s.InstructionWait = mem.ReadI32(activity.Value + 0x74) ?? -1;
            s.InstructionTime = mem.ReadI32(activity.Value + 0x78) ?? -1;

            s.AnimLocked = mem.ReadU8(activity.Value + 0x14) ?? 0;
            s.Loading = mem.ReadU8(activity.Value + 0x15) ?? 0;
            s.InConversation = mem.ReadU8(activity.Value + 0x19) ?? 0;
            s.Talking = mem.ReadU8(activity.Value + 0x1B) ?? 0;
            return s;
        }

        public CharacterSummary ReadSummary(uint character)
        {
            if (mem == null || character == 0) return null;

            uint? status = mem.ReadU32(character + 0xF6C);
            if (!status.HasValue || status.Value == 0) return null;
            uint? activity = mem.ReadU32(status.Value + 0x28);
            if (!activity.HasValue || activity.Value == 0) return null;

            CharacterSummary s = new CharacterSummary();
            s.Character = character;
            s.Status = status.Value;
            s.Activity = activity.Value;
            s.NpcData = mem.ReadU32(character + 0xF58) ?? 0;

            if (s.NpcData != 0)
            {
                uint? room = mem.ReadU32(s.NpcData + 0x1F4);
                if (room.HasValue && room.Value != 0)
                    s.Room = mem.ReadI32(room.Value + 0x14) ?? -1;
            }

            s.ActionId = mem.ReadI32(s.Activity + 0x40) ?? -1;
            s.MovementType = mem.ReadI32(s.Activity + 0x3C) ?? -1;
            s.AnimLocked = mem.ReadU8(s.Activity + 0x14) ?? 0;
            s.Loading = mem.ReadU8(s.Activity + 0x15) ?? 0;
            s.InConversation = mem.ReadU8(s.Activity + 0x19) ?? 0;
            return s;
        }

        public CharacterInfo FindByInterface(uint interfacePointer,
            IList<CharacterInfo> characters)
        {
            if (mem == null || interfacePointer == 0 || characters == null)
                return null;

            for (int i = 0; i < characters.Count; i++)
            {
                CharacterInfo info = characters[i];
                if (info == null || info.Pointer == 0) continue;

                uint? status = mem.ReadU32(info.Pointer + 0xF6C);
                if (!status.HasValue || status.Value == 0) continue;
                uint? activity = mem.ReadU32(status.Value + 0x28);
                if (!activity.HasValue || activity.Value == 0) continue;
                uint? descriptor = mem.ReadU32(activity.Value + 0xE4);
                if (descriptor.HasValue && descriptor.Value == interfacePointer)
                    return info;
            }
            return null;
        }

        public CharacterInfo FindByActivity(uint activityPointer,
            IList<CharacterInfo> characters)
        {
            if (mem == null || activityPointer == 0 || characters == null)
                return null;

            for (int i = 0; i < characters.Count; i++)
            {
                CharacterInfo info = characters[i];
                if (info == null || info.Pointer == 0) continue;
                uint? status = mem.ReadU32(info.Pointer + 0xF6C);
                if (!status.HasValue || status.Value == 0) continue;
                uint? activity = mem.ReadU32(status.Value + 0x28);
                if (activity.HasValue && activity.Value == activityPointer)
                    return info;
            }
            return null;
        }

        private void ReadMovement(uint activity, CharacterState s,
            IList<CharacterInfo> characters)
        {
            uint descriptor = mem.ReadU32(activity + 0xE4) ?? 0;
            s.Descriptor = descriptor;
            if (descriptor == 0) return;

            uint vtable = mem.ReadU32(descriptor) ?? 0;
            if (vtable == 0) return;

            int offset = mem.ReadI32(vtable + 8) ?? 0;
            uint baseAddress = descriptor + (uint)offset;

            uint first = mem.ReadU32(baseAddress + 4) ?? 0;
            if (first == 0) return;

            uint holder = mem.ReadU32(first + 4) ?? 0;
            if (holder == 0) return;
            s.MovementHolder = holder;

            s.MovementMirror = mem.ReadI32(holder + baseAddress + 0x130) ?? -1;
            s.WalkTargetInterface = mem.ReadU32(holder + baseAddress + 0x158) ?? 0;

            CharacterInfo target = FindByInterface(s.WalkTargetInterface,
                characters);
            s.WalkTargetChar = target != null ? target.Pointer : 0;
        }
    }
}
