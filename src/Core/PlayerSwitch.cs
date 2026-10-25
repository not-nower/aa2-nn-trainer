using System;

namespace AA2Trainer
{
    // The game keeps the active player character pointer at GlobalRoot + 0x88.
    // AA2Unlimited uses the same slot for its setPlayerCharacter helper.
    public static class PlayerSwitch
    {
        public const int PlayerOffset = 0x88;

        public static bool TrySetPlayer(GameMemory mem, GameContext ctx,
            CharacterInfo info, out string error)
        {
            error = null;

            if (mem == null || !mem.IsAttached)
            {
                error = "The game is not attached.";
                return false;
            }
            if (!mem.CanWrite)
            {
                error = string.IsNullOrEmpty(mem.WriteError)
                    ? "The game handle is read-only."
                    : mem.WriteError;
                return false;
            }
            if (ctx == null || ctx.GlobalRoot == 0)
            {
                error = "The class data is not available.";
                return false;
            }
            if (info == null || info.Pointer == 0)
            {
                error = "No character is selected.";
                return false;
            }
            if (!mem.IsPlausiblePointer(info.Pointer))
            {
                error = "The character pointer is not valid.";
                return false;
            }

            uint address = ctx.GlobalRoot + (uint)PlayerOffset;
            uint? current = mem.ReadU32(address);
            if (current.HasValue && current.Value == info.Pointer)
                return true;

            return MemoryWriter.TryWriteInt32(mem, address, unchecked((int)info.Pointer),
                "Player switch", out error);
        }
    }
}
