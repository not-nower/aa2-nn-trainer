using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace AA2Trainer
{
    public sealed class GameMemory : IDisposable
    {
        public const uint PROCESS_VM_READ = 0x0010;
        public const uint PROCESS_VM_WRITE = 0x0020;
        public const uint PROCESS_VM_OPERATION = 0x0008;
        public const uint PROCESS_QUERY_INFORMATION = 0x0400;
        private const uint PAGE_READWRITE = 0x04;

        public int Pid { get; private set; }
        public string ProcessName { get; private set; }
        public string GamePath { get; private set; }
        public uint ModuleBase { get; private set; }
        public uint ModuleSize { get; private set; }
        public bool IsAttached { get { return handle != IntPtr.Zero; } }
        public bool CanWrite { get; private set; }
        public string LastError { get; private set; }
        public string WriteError { get; private set; }

        private IntPtr handle = IntPtr.Zero;
        private Process process;

        private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        public bool Attach()
        {
            Detach();
            LastError = null;
            try
            {
                process = FindGameProcess();
                if (process == null)
                {
                    LastError = "AA2Play.exe is not running.";
                    return false;
                }

                Pid = process.Id;
                ProcessName = process.ProcessName;
                try { GamePath = process.MainModule.FileName; } catch { GamePath = null; }

                CanWrite = false;
                WriteError = null;
                handle = OpenProcess(
                    PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION | PROCESS_QUERY_INFORMATION,
                    false, Pid);
                if (handle != IntPtr.Zero)
                {
                    CanWrite = true;
                }
                else
                {
                    int writeAccessError = Marshal.GetLastWin32Error();
                    // Fall back to a read-only handle so the dashboard still works
                    // when the game is running elevated or is otherwise protected.
                    handle = OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFORMATION, false, Pid);
                    if (handle == IntPtr.Zero)
                        handle = OpenProcess(PROCESS_VM_READ, false, Pid);
                    if (handle != IntPtr.Zero)
                        WriteError = "Write access was denied (error " + writeAccessError +
                                     "). Run the trainer as administrator to edit.";
                }
                if (handle == IntPtr.Zero)
                {
                    LastError = "OpenProcess failed (error " + Marshal.GetLastWin32Error() +
                                "). Try running the tester as administrator.";
                    return false;
                }

                uint moduleBase;
                uint moduleSize;
                if (!TryGetModule(process, out moduleBase, out moduleSize))
                {
                    LastError = "Could not locate the AA2Play.exe module.";
                    Detach();
                    return false;
                }
                ModuleBase = moduleBase;
                ModuleSize = moduleSize;
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Detach();
                return false;
            }
        }

        public void Detach()
        {
            if (handle != IntPtr.Zero)
            {
                CloseHandle(handle);
                handle = IntPtr.Zero;
            }
            process = null;
            Pid = 0;
            ProcessName = null;
            GamePath = null;
            ModuleBase = 0;
            ModuleSize = 0;
            CanWrite = false;
            WriteError = null;
        }

        public void Dispose()
        {
            Detach();
        }

        private static Process FindGameProcess()
        {
            Process[] all;
            try { all = Process.GetProcesses(); }
            catch { return null; }

            Process fallback = null;
            foreach (Process p in all)
            {
                string name;
                try { name = p.ProcessName; }
                catch { continue; }
                if (name.Equals("AA2Play", StringComparison.OrdinalIgnoreCase))
                    return p;
                if (name.StartsWith("AA2Play", StringComparison.OrdinalIgnoreCase) && fallback == null)
                    fallback = p;
            }
            return fallback;
        }

        private static bool TryGetModule(Process p, out uint baseAddress, out uint size)
        {
            baseAddress = 0;
            size = 0;
            try
            {
                ProcessModule m = p.MainModule;
                if (m != null && m.ModuleName.StartsWith("AA2Play", StringComparison.OrdinalIgnoreCase))
                {
                    baseAddress = unchecked((uint)m.BaseAddress.ToInt64());
                    size = unchecked((uint)m.ModuleMemorySize);
                    return baseAddress != 0;
                }
            }
            catch { }

            // Fallback for cases where Process.MainModule is inaccessible.
            IntPtr snap = CreateToolhelp32Snapshot(TH32CS_SNAPMODULE | TH32CS_SNAPMODULE32, (uint)p.Id);
            if (snap == INVALID_HANDLE_VALUE) return false;
            try
            {
                MODULEENTRY32 me = new MODULEENTRY32();
                me.dwSize = (uint)Marshal.SizeOf(typeof(MODULEENTRY32));
                if (Module32First(snap, ref me))
                {
                    do
                    {
                        if (me.szModule.StartsWith("AA2Play", StringComparison.OrdinalIgnoreCase))
                        {
                            baseAddress = unchecked((uint)me.modBaseAddr.ToInt64());
                            size = me.modBaseSize;
                            return baseAddress != 0;
                        }
                        me.dwSize = (uint)Marshal.SizeOf(typeof(MODULEENTRY32));
                    }
                    while (Module32Next(snap, ref me));
                }
            }
            finally
            {
                CloseHandle(snap);
            }
            return false;
        }

        public byte[] ReadBytes(uint address, int count)
        {
            if (!IsAttached || count <= 0) return null;
            byte[] buffer = new byte[count];
            IntPtr read;
            if (!ReadProcessMemory(handle, new IntPtr(unchecked((int)address)), buffer,
                    new IntPtr(count), out read))
                return null;
            if (read.ToInt64() != count) return null;
            return buffer;
        }

        public byte[] ReadBytes(IntPtr address, int count)
        {
            return ReadBytes(unchecked((uint)address.ToInt64()), count);
        }

        public bool TryReadBytes(uint address, int count, out byte[] bytes)
        {
            bytes = ReadBytes(address, count);
            return bytes != null;
        }

        public bool WriteBytes(uint address, byte[] data)
        {
            WriteError = null;
            if (!IsAttached)
            {
                WriteError = "The game is not attached.";
                return false;
            }
            if (!CanWrite)
            {
                WriteError = string.IsNullOrEmpty(WriteError)
                    ? "The process handle was opened read-only."
                    : WriteError;
                return false;
            }
            if (data == null || data.Length == 0)
            {
                WriteError = "Nothing to write.";
                return false;
            }

            IntPtr target = new IntPtr(unchecked((int)address));
            uint oldProtect;
            bool protectionChanged = VirtualProtectEx(handle, target, (UIntPtr)data.Length,
                PAGE_READWRITE, out oldProtect);
            int protectError = protectionChanged ? 0 : Marshal.GetLastWin32Error();

            IntPtr written;
            bool ok = WriteProcessMemory(handle, target, data, new IntPtr(data.Length), out written);

            if (protectionChanged)
                VirtualProtectEx(handle, target, (UIntPtr)data.Length, oldProtect, out oldProtect);

            if (!ok || written.ToInt64() != data.Length)
            {
                int writeError = ok ? 0 : Marshal.GetLastWin32Error();
                WriteError = "WriteProcessMemory failed" +
                    (writeError != 0 ? " (error " + writeError + ")" :
                     protectionChanged ? "" : " (VirtualProtectEx error " + protectError + ")") +
                    " at 0x" + address.ToString("X8") + ".";
                return false;
            }
            return true;
        }

        public byte? ReadU8(uint address)
        {
            byte[] b = ReadBytes(address, 1);
            return b == null ? (byte?)null : b[0];
        }

        public ushort? ReadU16(uint address)
        {
            byte[] b = ReadBytes(address, 2);
            return b == null ? (ushort?)null : BitConverter.ToUInt16(b, 0);
        }

        public uint? ReadU32(uint address)
        {
            byte[] b = ReadBytes(address, 4);
            return b == null ? (uint?)null : BitConverter.ToUInt32(b, 0);
        }

        public int? ReadI32(uint address)
        {
            byte[] b = ReadBytes(address, 4);
            return b == null ? (int?)null : BitConverter.ToInt32(b, 0);
        }

        public float? ReadFloat(uint address)
        {
            byte[] b = ReadBytes(address, 4);
            return b == null ? (float?)null : BitConverter.ToSingle(b, 0);
        }

        public string ReadAnsiString(uint address, int maxBytes)
        {
            byte[] b = ReadBytes(address, maxBytes);
            if (b == null) return null;
            int len = Array.IndexOf(b, (byte)0);
            if (len < 0) len = b.Length;
            if (len == 0) return string.Empty;
            try
            {
                return Encoding.GetEncoding(932).GetString(b, 0, len).Trim();
            }
            catch
            {
                return Encoding.Default.GetString(b, 0, len).Trim();
            }
        }

        public string ReadUnicodeString(uint address, int maxBytes)
        {
            byte[] b = ReadBytes(address, maxBytes);
            if (b == null) return null;
            int len = b.Length;
            for (int i = 0; i + 1 < b.Length; i += 2)
            {
                if (b[i] == 0 && b[i + 1] == 0)
                {
                    len = i;
                    break;
                }
            }
            if (len < 2) return string.Empty;
            return Encoding.Unicode.GetString(b, 0, len).Trim();
        }

        public bool IsPlausiblePointer(uint value)
        {
            return value >= 0x00010000 && value < 0x80000000;
        }

        public bool IsInModule(uint address)
        {
            return ModuleBase != 0 && address >= ModuleBase && address < ModuleBase + ModuleSize;
        }

        #region Native

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint access, bool inheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory(IntPtr process, IntPtr baseAddress,
            [Out] byte[] buffer, IntPtr size, out IntPtr bytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr process, IntPtr baseAddress,
            byte[] buffer, IntPtr size, out IntPtr bytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualProtectEx(IntPtr process, IntPtr address,
            UIntPtr size, uint newProtect, out uint oldProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        private const uint TH32CS_SNAPMODULE = 0x00000008;
        private const uint TH32CS_SNAPMODULE32 = 0x00000010;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct MODULEENTRY32
        {
            public uint dwSize;
            public uint th32ModuleID;
            public uint th32ProcessID;
            public uint GlblcntUsage;
            public uint ProccntUsage;
            public IntPtr modBaseAddr;
            public uint modBaseSize;
            public IntPtr hModule;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szModule;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szExePath;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern bool Module32First(IntPtr snapshot, ref MODULEENTRY32 entry);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern bool Module32Next(IntPtr snapshot, ref MODULEENTRY32 entry);

        #endregion
    }
}
