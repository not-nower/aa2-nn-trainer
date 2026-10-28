using System;

namespace AA2Trainer
{
    /// <summary>
    /// Mirrors the game's 28-byte ActionParamStruct:
    ///   +0x00 actionId
    ///   +0x04 movementType
    ///   +0x08 roomTarget
    ///   +0x0C unknown (room-about for action 51)
    ///   +0x10 target1  (CharacterActivity*)
    ///   +0x14 target2  (about CharacterActivity*)
    ///   +0x18 unknown2
    /// </summary>
    public sealed class ActionParams
    {
        public int ActionId;
        public int MovementType;
        public int RoomTarget;
        public int Unknown;
        public uint Target1;
        public uint Target2;
        public int Unknown2;

        public const int MoveToRoomType = 2;
        public const int WalkToCharacterType = 3;
        public const int NoTargetType = 0;
        public const int MeetAtAction = 51;
        public const int StareActionId = 107;

        public ActionParams()
        {
            ActionId = -1;
            MovementType = WalkToCharacterType;
            RoomTarget = -1;
            Unknown = -1;
            Target1 = 0;
            Target2 = 0;
            Unknown2 = 1;
        }

        public static ActionParams SelfAction(int actionId)
        {
            ActionParams p = new ActionParams();
            p.ActionId = actionId;
            p.MovementType = NoTargetType;
            p.RoomTarget = -1;
            p.Unknown = -1;
            p.Target1 = 0;
            p.Target2 = 0;
            p.Unknown2 = 1;
            return p;
        }

        public static ActionParams TargetAction(int actionId, uint targetActivity)
        {
            ActionParams p = new ActionParams();
            p.ActionId = actionId;
            p.MovementType = WalkToCharacterType;
            p.RoomTarget = -1;
            p.Unknown = -1;
            p.Target1 = targetActivity;
            p.Target2 = 0;
            p.Unknown2 = 1;
            return p;
        }

        public static ActionParams MoveToRoom(int roomId)
        {
            ActionParams p = new ActionParams();
            p.ActionId = 0;
            p.MovementType = MoveToRoomType;
            p.RoomTarget = roomId;
            p.Unknown = -1;
            p.Target1 = 0;
            p.Target2 = 0;
            p.Unknown2 = 1;
            return p;
        }

        public static ActionParams MeetAt(int actionId, uint targetActivity, int roomId)
        {
            ActionParams p = TargetAction(actionId, targetActivity);
            p.Unknown = roomId;
            return p;
        }

        public byte[] ToBytes()
        {
            byte[] b = new byte[28];
            WriteInt(b, 0x00, ActionId);
            WriteInt(b, 0x04, MovementType);
            WriteInt(b, 0x08, RoomTarget);
            WriteInt(b, 0x0C, Unknown);
            WriteUInt(b, 0x10, Target1);
            WriteUInt(b, 0x14, Target2);
            WriteInt(b, 0x18, Unknown2);
            return b;
        }

        public string Describe()
        {
            return "action=" + ActionId +
                   " movement=" + MovementType +
                   " room=" + RoomTarget +
                   " about=" + Unknown +
                   " target1=0x" + Target1.ToString("X8") +
                   " target2=0x" + Target2.ToString("X8") +
                   " unknown2=" + Unknown2;
        }

        private static void WriteInt(byte[] buffer, int offset, int value)
        {
            byte[] raw = BitConverter.GetBytes(value);
            Array.Copy(raw, 0, buffer, offset, 4);
        }

        private static void WriteUInt(byte[] buffer, int offset, uint value)
        {
            byte[] raw = BitConverter.GetBytes(value);
            Array.Copy(raw, 0, buffer, offset, 4);
        }
    }
}
