using System;
using System.Collections.Generic;

namespace AA2Trainer
{
    /// <summary>
    /// Planner hook based on the validated AA Unlimited approach.
    ///
    /// The game plans an action per character in its per-character tick
    /// (moduleBase + 0x117C80), which calls the planner (moduleBase + 0x1908F0).
    ///
    ///   esi = [AI + 0x1C]        ; per-character key
    ///   edi = ActionParamStruct* ; output buffer on the stack
    ///
    /// The hook injects a queued action for a matching character and lets the
    /// game's own apply path build and run the plan. Direct remote apply calls
    /// cannot win the race against the AI tick clearing the walk target.
    ///
    /// Two hook points are chosen at install time:
    ///   CallSite     moduleBase + 0x117CFE  (vanilla)
    ///   PlannerEntry moduleBase + 0x1908F0  (AA Unlimited present)
    ///
    /// The original bytes are restored by Uninstall(). A crash leaves the game
    /// patched until it is restarted.
    /// </summary>
    public sealed class PlannerHook : IDisposable
    {
        private enum Mode
        {
            None,
            CallSite,
            PlannerEntry
        }

        public const int CallSiteRva = 0x117CFE;
        public const int PlannerEntryRva = 0x1908F0;
        public const int PlannerAfterPrologueRva = 0x1908F8;
        public const int PlannerInternalCallRva = 0x190CD0;

        private const int TrampolineOffset = 0x000;
        private const int WrapperOffset = 0x080;
        private const int HandlerOffset = 0x100;
        private const int DataOffset = 0x200;
        private const int ParamsOffset = DataOffset + 0x08;
        private const int PendingOffset = DataOffset + 0x04;
        private const int HitCountOffset = DataOffset + 0x40;
        private const int PageSize = 0x1000;

        private static readonly byte[] VanillaCallSite =
            { 0xE8, 0xED, 0x8B, 0x07, 0x00 };
        private static readonly byte[] VanillaPlannerEntry =
            { 0x53, 0x8B, 0xCE, 0xE8, 0xD8, 0x03, 0x00, 0x00 };

        private readonly GameMemory mem;
        private uint page;
        private uint hookAddress;
        private byte[] originalBytes;
        private Mode mode;
        private bool installed;
        private string lastError;

        public bool Installed { get { return installed; } }
        public string LastError { get { return lastError; } }
        public uint Page { get { return page; } }
        public string ModeName
        {
            get
            {
                if (mode == Mode.CallSite) return "call-site 0x117CFE";
                if (mode == Mode.PlannerEntry) return "planner-entry 0x1908F0";
                return "none";
            }
        }

        public PlannerHook(GameMemory mem)
        {
            this.mem = mem;
        }

        public bool Install(out string message)
        {
            message = null;
            lastError = null;

            if (mem == null || !mem.IsAttached)
            {
                message = "The game is not attached.";
                lastError = message;
                return false;
            }
            if (!mem.CanWrite)
            {
                message = "The trainer does not have write access. Run it as administrator.";
                lastError = message;
                return false;
            }
            if (installed)
            {
                message = "Already installed (" + ModeName + ").";
                return true;
            }

            uint callSite = mem.ModuleBase + CallSiteRva;
            uint plannerEntry = mem.ModuleBase + PlannerEntryRva;

            byte[] callSiteBytes = mem.ReadBytes(callSite, 5);
            byte[] plannerBytes = mem.ReadBytes(plannerEntry, 8);

            if (Matches(callSiteBytes, VanillaCallSite))
            {
                mode = Mode.CallSite;
                hookAddress = callSite;
                originalBytes = callSiteBytes;
            }
            else if (Matches(plannerBytes, VanillaPlannerEntry))
            {
                mode = Mode.PlannerEntry;
                hookAddress = plannerEntry;
                originalBytes = plannerBytes;
            }
            else
            {
                message = "No usable hook point. 0x" + callSite.ToString("X8") +
                    " = " + Describe(callSiteBytes) + "; 0x" +
                    plannerEntry.ToString("X8") + " = " + Describe(plannerBytes) +
                    ". Another mod may be hooking both.";
                lastError = message;
                return false;
            }

            page = mem.Allocate(PageSize);
            if (page == 0)
            {
                message = "VirtualAllocEx failed. Run the trainer as administrator.";
                lastError = message;
                return false;
            }

            try
            {
                uint trampolineAddress = page + TrampolineOffset;
                uint wrapperAddress = page + WrapperOffset;
                uint handlerAddress = page + HandlerOffset;

                byte[] handler = BuildHandler(page);
                byte[] data = new byte[0x80];
                byte[] code;
                byte[] patch;

                if (mode == Mode.CallSite)
                {
                    code = BuildCallSiteTrampoline(page,
                        mem.ModuleBase + PlannerEntryRva, handlerAddress);
                    patch = BuildCallPatch(hookAddress, trampolineAddress);
                }
                else
                {
                    code = BuildPlannerEntryTrampoline(trampolineAddress,
                        mem.ModuleBase + PlannerInternalCallRva,
                        mem.ModuleBase + PlannerAfterPrologueRva);
                    byte[] wrapper = BuildWrapper(wrapperAddress,
                        trampolineAddress, handlerAddress);
                    if (!mem.WriteRawBytes(wrapperAddress, wrapper))
                    {
                        message = "Writing the hook wrapper failed: " + mem.WriteError;
                        lastError = message;
                        mem.Free(page);
                        page = 0;
                        return false;
                    }
                    patch = BuildPlannerEntryPatch(hookAddress, wrapperAddress);
                }

                if (!mem.WriteRawBytes(trampolineAddress, code) ||
                    !mem.WriteRawBytes(handlerAddress, handler) ||
                    !mem.WriteRawBytes(page + DataOffset, data))
                {
                    message = "Writing the hook code/data failed: " + mem.WriteError;
                    lastError = message;
                    mem.Free(page);
                    page = 0;
                    return false;
                }

                if (!mem.WriteCodeBytes(hookAddress, patch))
                {
                    message = "Writing the planner patch failed: " + mem.WriteError;
                    lastError = message;

                    // A failed patch may still have changed the target bytes.
                    // Restore them before freeing the trampoline page.
                    if (mem.WriteCodeBytes(hookAddress, originalBytes))
                        mem.Free(page);
                    page = 0;
                    mode = Mode.None;
                    return false;
                }

                installed = true;
                message = "Planner hook installed (" + ModeName + ") at 0x" +
                    hookAddress.ToString("X8") + " -> 0x" +
                    trampolineAddress.ToString("X8") + ".";
                return true;
            }
            catch (Exception ex)
            {
                message = "Install failed: " + ex.Message;
                lastError = message;
                if (page != 0)
                {
                    mem.Free(page);
                    page = 0;
                }
                mode = Mode.None;
                return false;
            }
        }

        /// <summary>
        /// Restores the original game bytes and frees the trampoline page. The
        /// page is only freed after the restore has been read back successfully.
        /// </summary>
        public bool Uninstall(out string message)
        {
            message = null;
            if (!installed || page == 0 || originalBytes == null)
            {
                message = "The planner hook is not installed.";
                installed = false;
                return true;
            }

            if (!mem.IsAttached)
            {
                message = "Cannot restore the hook: the game is no longer attached.";
                lastError = message;
                return false;
            }

            if (!mem.WriteCodeBytes(hookAddress, originalBytes))
            {
                message = "Restoring the original game code failed: " + mem.WriteError +
                    ". Restart AA2Play.exe to remove the hook.";
                lastError = message;
                return false;
            }

            mem.Free(page);
            page = 0;
            originalBytes = null;
            installed = false;
            mode = Mode.None;
            message = "Planner hook uninstalled; original game code restored.";
            return true;
        }

        public void Dispose()
        {
            string ignored;
            Uninstall(out ignored);
        }

        public bool Queue(CharacterState actor, ActionParams parameters,
            out string message)
        {
            message = null;
            if (!installed)
            {
                message = "The planner hook is not installed.";
                return false;
            }
            if (actor == null || actor.Activity == 0)
            {
                message = "The actor has no activity block.";
                return false;
            }

            uint ai = mem.ReadU32(actor.Activity + 0xEC) ?? 0;
            if (ai == 0)
            {
                message = "The AI wrapper is unreadable.";
                return false;
            }

            uint key = mem.ReadU32(ai + 0x1C) ?? 0;
            if (key == 0)
            {
                message = "The planner key is unreadable ([AI+0x1C] = 0).";
                return false;
            }

            // Disarm, write params and key, then arm. The handler only reads the
            // params when pending == 1.
            if (!mem.WriteRawBytes(page + PendingOffset,
                    BitConverter.GetBytes(0u)) ||
                !mem.WriteRawBytes(page + ParamsOffset, parameters.ToBytes()) ||
                !mem.WriteRawBytes(page + DataOffset, BitConverter.GetBytes(key)))
            {
                message = "Writing the queued action failed: " + mem.WriteError;
                return false;
            }

            if (!mem.WriteRawBytes(page + PendingOffset,
                BitConverter.GetBytes(1u)))
            {
                message = "Arming the queued action failed: " + mem.WriteError;
                return false;
            }

            message = "queued for key 0x" + key.ToString("X8") +
                " (" + ModeName + ", page 0x" + page.ToString("X8") + ")";
            return true;
        }

        public bool IsPending()
        {
            if (!installed || page == 0) return false;
            uint? pending = mem.ReadU32(page + PendingOffset);
            return pending.HasValue && pending.Value != 0;
        }

        public void Clear()
        {
            if (!installed || page == 0) return;
            mem.WriteRawBytes(page + PendingOffset, BitConverter.GetBytes(0u));
        }

        public uint HitCount()
        {
            if (!installed || page == 0) return 0;
            return mem.ReadU32(page + HitCountOffset) ?? 0;
        }

        private static byte[] BuildCallPatch(uint hookAddress, uint trampolineAddress)
        {
            byte[] patch = new byte[5];
            patch[0] = 0xE8;
            WriteI32(patch, 1, (int)(trampolineAddress - (hookAddress + 5)));
            return patch;
        }

        private static byte[] BuildPlannerEntryPatch(uint hookAddress,
            uint wrapperAddress)
        {
            byte[] patch = new byte[8];
            patch[0] = 0xE9;
            WriteI32(patch, 1, (int)(wrapperAddress - (hookAddress + 5)));
            patch[5] = 0x90;
            patch[6] = 0x90;
            patch[7] = 0x90;
            return patch;
        }

        private static byte[] BuildCallSiteTrampoline(uint page, uint planner,
            uint handlerAddress)
        {
            uint trampolineAddress = page + TrampolineOffset;
            List<byte> code = new List<byte>();

            code.Add(0xB8);
            AddU32(code, planner);
            code.Add(0xFF); code.Add(0xD0);
            code.Add(0x50);
            code.Add(0x57);
            code.Add(0x56);

            int callOffset = code.Count;
            code.Add(0xE8);
            AddI32(code, (int)(handlerAddress -
                (trampolineAddress + callOffset + 5)));

            code.Add(0x83); code.Add(0xC4); code.Add(0x0C);
            code.Add(0xC3);
            return code.ToArray();
        }

        private static byte[] BuildPlannerEntryTrampoline(uint trampolineAddress,
            uint internalCall, uint afterPrologue)
        {
            List<byte> code = new List<byte>();

            code.Add(0x53);
            code.Add(0x8B); code.Add(0xCE);
            code.Add(0xB8);
            AddU32(code, internalCall);
            code.Add(0xFF); code.Add(0xD0);

            int jmpOffset = code.Count;
            code.Add(0xE9);
            AddI32(code, (int)(afterPrologue -
                (trampolineAddress + jmpOffset + 5)));
            return code.ToArray();
        }

        private static byte[] BuildWrapper(uint wrapperAddress,
            uint trampolineAddress, uint handlerAddress)
        {
            List<byte> code = new List<byte>();

            int call1 = code.Count;
            code.Add(0xE8);
            AddI32(code, (int)(trampolineAddress - (wrapperAddress + call1 + 5)));

            code.Add(0x50);
            code.Add(0x57);
            code.Add(0x56);

            int call2 = code.Count;
            code.Add(0xE8);
            AddI32(code, (int)(handlerAddress - (wrapperAddress + call2 + 5)));

            code.Add(0x83); code.Add(0xC4); code.Add(0x0C);
            code.Add(0xC3);
            return code.ToArray();
        }

        private static byte[] BuildHandler(uint page)
        {
            uint data = page + DataOffset;
            List<byte> code = new List<byte>();

            code.Add(0x53);
            code.Add(0x56);
            code.Add(0x57);
            code.Add(0x55);

            code.AddRange(new byte[] { 0x8B, 0x44, 0x24, 0x14 });
            code.AddRange(new byte[] { 0x8B, 0x54, 0x24, 0x18 });
            code.AddRange(new byte[] { 0x8B, 0x6C, 0x24, 0x1C });

            code.Add(0xBB);
            AddU32(code, data);

            code.AddRange(new byte[] { 0x39, 0x03 });
            code.Add(0x75);
            int jneDisp = code.Count;
            code.Add(0x00);

            code.AddRange(new byte[] { 0x83, 0x7B, 0x04, 0x00 });
            code.Add(0x74);
            int jeDisp = code.Count;
            code.Add(0x00);

            code.AddRange(new byte[] { 0x8D, 0x73, 0x08 });
            code.AddRange(new byte[] { 0x8B, 0xFA });
            code.Add(0xB9);
            AddU32(code, 7);
            code.Add(0xFC);
            code.Add(0xF3); code.Add(0xA5);

            code.AddRange(new byte[] { 0xC7, 0x43, 0x04, 0x00, 0x00, 0x00, 0x00 });
            code.AddRange(new byte[] { 0xC7, 0x03, 0x00, 0x00, 0x00, 0x00 });
            code.AddRange(new byte[] { 0xFF, 0x43, 0x40 });
            code.Add(0xB8);
            AddU32(code, 1);
            code.Add(0xEB);
            int jmpDisp = code.Count;
            code.Add(0x00);

            int noOffset = code.Count;
            code.AddRange(new byte[] { 0x8B, 0xC5 });

            int doneOffset = code.Count;
            code.Add(0x5D);
            code.Add(0x5F);
            code.Add(0x5E);
            code.Add(0x5B);
            code.Add(0xC3);

            code[jneDisp] = (byte)(noOffset - (jneDisp + 1));
            code[jeDisp] = (byte)(noOffset - (jeDisp + 1));
            code[jmpDisp] = (byte)(doneOffset - (jmpDisp + 1));

            return code.ToArray();
        }

        private static bool Matches(byte[] actual, byte[] expected)
        {
            if (actual == null || actual.Length < expected.Length) return false;
            for (int i = 0; i < expected.Length; i++)
                if (actual[i] != expected[i]) return false;
            return true;
        }

        private static void AddU32(List<byte> code, uint value)
        {
            code.Add((byte)(value & 0xFF));
            code.Add((byte)((value >> 8) & 0xFF));
            code.Add((byte)((value >> 16) & 0xFF));
            code.Add((byte)((value >> 24) & 0xFF));
        }

        private static void AddI32(List<byte> code, int value)
        {
            AddU32(code, unchecked((uint)value));
        }

        private static void WriteI32(byte[] buffer, int offset, int value)
        {
            buffer[offset + 0] = (byte)(value & 0xFF);
            buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
            buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
            buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
        }

        private static string Describe(byte[] bytes)
        {
            if (bytes == null) return "(unreadable)";
            string s = "";
            for (int i = 0; i < bytes.Length; i++)
            {
                if (i > 0) s += " ";
                s += bytes[i].ToString("X2");
            }
            return s;
        }
    }
}
