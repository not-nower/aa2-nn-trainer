using System;

namespace AA2Trainer
{
    // Every write in the trainer is followed by a read-back. Keeping the
    // mechanics in one place means the verification rule cannot drift between
    // the individual editors.
    public static class MemoryWriter
    {
        public static bool TryWriteByte(GameMemory mem, uint address, byte value,
            string label, out string error)
        {
            error = null;

            if (!mem.WriteBytes(address, new byte[] { value }))
            {
                error = label + " write failed: " + (mem.WriteError ?? "unknown error");
                return false;
            }

            byte? check = mem.ReadU8(address);
            if (!check.HasValue || check.Value != value)
            {
                error = label + " verification failed at 0x" + address.ToString("X8") + ".";
                return false;
            }
            return true;
        }

        public static bool TryWriteInt32(GameMemory mem, uint address, int value,
            string label, out string error)
        {
            error = null;

            if (!mem.WriteBytes(address, BitConverter.GetBytes(value)))
            {
                error = label + " write failed: " + (mem.WriteError ?? "unknown error");
                return false;
            }

            int? check = mem.ReadI32(address);
            if (!check.HasValue || check.Value != value)
            {
                error = label + " verification failed at 0x" + address.ToString("X8") + ".";
                return false;
            }
            return true;
        }

        public static bool TryWriteBlock(GameMemory mem, uint address, byte[] buffer,
            string label, out string error)
        {
            error = null;

            if (!mem.WriteBytes(address, buffer))
            {
                error = label + " write failed: " + (mem.WriteError ?? "unknown error");
                return false;
            }

            byte[] check = mem.ReadBytes(address, buffer.Length);
            if (check == null || !BytesEqual(buffer, check))
            {
                error = label + " verification failed at 0x" + address.ToString("X8") + ".";
                return false;
            }
            return true;
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }
    }
}
