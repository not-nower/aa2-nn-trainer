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

        // Per-key slot table (D2). The handler linearly scans the table and
        // only injects when the planner key matches and the slot is armed, so
        // two characters can be queued at the same time.
        //
        //   +0x00 uint   key        [AI + 0x1C]
        //   +0x04 uint   pending    1 = armed
        //   +0x08 byte28 params     ActionParamStruct
        //   +0x24 uint   hitCount   consumed by this key
        //   +0x28 byte24 reserved
        private const int SlotCount = 32;
        private const int SlotSize = 0x40;
        private const int SlotKeyOffset = 0x00;
        private const int SlotPendingOffset = 0x04;
        private const int SlotParamsOffset = 0x08;
        private const int SlotHitOffset = 0x24;
        private const int SlotCallOffset = 0x28;
        private const int ParamsSize = 28;

        // Phase 0 instrumentation (observe-only). It lives after the slot
        // table so the two features cannot overwrite each other.
        private const int ObserveBaseOffset = 0xA00;
        private const int ObservedKeyOffset = ObserveBaseOffset + 0x00;
        private const int ObservedParamsOffset = ObserveBaseOffset + 0x04;
        private const int ObservedCounterOffset = ObserveBaseOffset + 0x20;
        private const int ObserveFlagOffset = ObserveBaseOffset + 0x24;

        // Total planner calls, incremented on every handler entry. This is the
        // reliable liveness/pacing signal: the world clock stops during
        // conversations, but the planner thread does not.
        private const int CallCountOffset = ObserveBaseOffset + 0x28;

        private const int PageSize = 0x1000;
        private const int DataSize = PageSize - DataOffset;

        private int nextReclaimSlot;
        private readonly object queueGate = new object();

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
                byte[] data = new byte[DataSize];
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

            // Do not free the trampoline page here. A game thread may have
            // been suspended inside the handler/trampoline while the original
            // bytes were restored; freeing the page would crash it on resume.
            // The page dies with the game process, and the next Install
            // allocates a fresh one (4 KB per re-install, negligible).
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

        /// <summary>
        /// Resolves the planner key the game uses for an activity block:
        /// [activity + 0xEC] -> AI, [AI + 0x1C] -> key.
        /// </summary>
        private uint ResolveKey(uint actorActivity)
        {
            if (actorActivity == 0) return 0;
            uint ai = mem.ReadU32(actorActivity + 0xEC) ?? 0;
            if (ai == 0) return 0;
            return mem.ReadU32(ai + 0x1C) ?? 0;
        }

        /// <summary>
        /// Queues an action in the per-key slot for the actor. The slot is
        /// disarmed, rewritten key + params as one block, then armed, so the
        /// handler can never observe a partially written parameter set.
        /// </summary>
        public bool Queue(uint actorActivity, ActionParams parameters,
            out uint key, out string message)
        {
            key = 0;
            message = null;
            if (!installed)
            {
                message = "The planner hook is not installed.";
                return false;
            }
            if (ObserveOnly)
            {
                message = "Observe-only is on: the game plans normally and queued " +
                    "actions are not injected. Switch observe-only off first.";
                return false;
            }
            if (actorActivity == 0)
            {
                message = "The actor has no activity block.";
                return false;
            }
            if (parameters == null)
            {
                message = "The action parameters are missing.";
                return false;
            }

            key = ResolveKey(actorActivity);
            if (key == 0)
            {
                message = "The planner key is unreadable ([AI+0x1C] = 0).";
                return false;
            }

            lock (queueGate)
            {
                int slot;
                bool reused;
                if (!FindSlot(key, out slot, out reused))
                {
                    message = "No free planner slot: " + SlotCount +
                        " characters are already queued or armed.";
                    return false;
                }

                uint slotAddress = page + DataOffset + (uint)(slot * SlotSize);

                // Disarm first. Writing the key/params block afterwards also
                // writes pending = 0, so the handler cannot inject a half-written
                // parameter set during the update.
                if (!mem.WriteRawBytes(slotAddress + SlotPendingOffset,
                    BitConverter.GetBytes(0u)))
                {
                    message = "Disarming the queued action failed: " + mem.WriteError;
                    return false;
                }

                if (!reused &&
                    !mem.WriteRawBytes(slotAddress + SlotHitOffset, new byte[8]))
                {
                    message = "Resetting the slot counters failed: " + mem.WriteError;
                    return false;
                }

                byte[] block = new byte[SlotHitOffset];
                Buffer.BlockCopy(BitConverter.GetBytes(key), 0, block,
                    SlotKeyOffset, 4);
                Buffer.BlockCopy(parameters.ToBytes(), 0, block,
                    SlotParamsOffset, ParamsSize);

                if (!mem.WriteRawBytes(slotAddress, block))
                {
                    message = "Writing the queued action failed: " + mem.WriteError;
                    return false;
                }

                if (!mem.WriteRawBytes(slotAddress + SlotPendingOffset,
                    BitConverter.GetBytes(1u)))
                {
                    message = "Arming the queued action failed: " + mem.WriteError;
                    return false;
                }

                message = "queued for key 0x" + key.ToString("X8") +
                    " (slot " + slot + ", " + ModeName + ", page 0x" +
                    page.ToString("X8") + ")";
                return true;
            }
        }

        /// <summary>
        /// Finds the slot owned by <paramref name="key"/>, or an unused slot.
        /// When the table is full, an idle slot is reclaimed round-robin.
        /// </summary>
        private bool FindSlot(uint key, out int slot, out bool reused)
        {
            slot = -1;
            reused = false;

            byte[] table = mem.ReadBytes(page + DataOffset,
                SlotCount * SlotSize);
            if (table == null) return false;

            int free = -1;
            for (int i = 0; i < SlotCount; i++)
            {
                int offset = i * SlotSize;
                uint slotKey = BitConverter.ToUInt32(table, offset + SlotKeyOffset);
                if (slotKey == key)
                {
                    slot = i;
                    reused = true;
                    return true;
                }
                if (slotKey == 0 && free < 0) free = i;
            }

            if (free >= 0)
            {
                slot = free;
                return true;
            }

            for (int n = 0; n < SlotCount; n++)
            {
                int i = (nextReclaimSlot + n) % SlotCount;
                uint pending = BitConverter.ToUInt32(table,
                    i * SlotSize + SlotPendingOffset);
                if (pending == 0)
                {
                    nextReclaimSlot = (i + 1) % SlotCount;
                    slot = i;
                    return true;
                }
            }
            return false;
        }

        /// <summary>True when any key has an armed action.</summary>
        /// <summary>True when this key still has an armed action.</summary>
        public bool IsPending(uint key)
        {
            int slot = FindSlotIndex(key);
            if (slot < 0) return false;
            uint? pending = mem.ReadU32(page + DataOffset +
                (uint)(slot * SlotSize) + SlotPendingOffset);
            return pending.HasValue && pending.Value != 0;
        }

        /// <summary>Disarms every slot. Safe to call repeatedly.</summary>
        public void Clear()
        {
            if (!installed || page == 0) return;
            byte[] zero = BitConverter.GetBytes(0u);
            for (int i = 0; i < SlotCount; i++)
            {
                mem.WriteRawBytes(page + DataOffset +
                    (uint)(i * SlotSize) + SlotPendingOffset, zero);
            }
        }

        /// <summary>Disarms one key's slot (leaves other characters alone).</summary>
        public void Clear(uint key)
        {
            int slot = FindSlotIndex(key);
            if (slot < 0) return;
            mem.WriteRawBytes(page + DataOffset +
                (uint)(slot * SlotSize) + SlotPendingOffset,
                BitConverter.GetBytes(0u));
        }

        private int FindSlotIndex(uint key)
        {
            if (!installed || page == 0 || key == 0) return -1;
            byte[] table = mem.ReadBytes(page + DataOffset,
                SlotCount * SlotSize);
            if (table == null) return -1;
            for (int i = 0; i < SlotCount; i++)
            {
                if (BitConverter.ToUInt32(table, i * SlotSize + SlotKeyOffset) == key)
                    return i;
            }
            return -1;
        }

        /// <summary>Total number of actions consumed by every key.</summary>
        /// <summary>Number of actions consumed by this key.</summary>
        public uint HitCount(uint key)
        {
            int slot = FindSlotIndex(key);
            if (slot < 0) return 0;
            return mem.ReadU32(page + DataOffset +
                (uint)(slot * SlotSize) + SlotHitOffset) ?? 0;
        }

        /// <summary>Planner calls seen for this key, armed or not.</summary>
        public uint CallCount(uint key)
        {
            int slot = FindSlotIndex(key);
            if (slot < 0) return 0;
            return mem.ReadU32(page + DataOffset +
                (uint)(slot * SlotSize) + SlotCallOffset) ?? 0;
        }

        /// <summary>
        /// When on, the handler records the plan the game produced and never
        /// replaces it. Used to measure the game's own decisions (Phase 0).
        /// </summary>
        public bool ObserveOnly
        {
            get
            {
                if (!installed || page == 0) return false;
                return (mem.ReadU32(page + ObserveFlagOffset) ?? 0) != 0;
            }
        }

        public bool SetObserveOnly(bool enabled)
        {
            if (!installed || page == 0) return false;
            if (enabled) Clear();
            return mem.WriteRawBytes(page + ObserveFlagOffset,
                BitConverter.GetBytes(enabled ? 1u : 0u));
        }

        /// <summary>Total planner calls seen by the handler since install.</summary>
        public uint CallCount()
        {
            if (!installed || page == 0) return 0;
            return mem.ReadU32(page + CallCountOffset) ?? 0;
        }

        /// <summary>
        /// Last plan observed by the handler (key, raw 28-byte struct, total
        /// observation counter). Only advances while <see cref="ObserveOnly"/> is on.
        /// </summary>
        public bool ReadObservation(out uint key, out byte[] parameters,
            out uint counter)
        {
            key = 0;
            parameters = null;
            counter = 0;
            if (!installed || page == 0) return false;

            uint? observedKey = mem.ReadU32(page + ObservedKeyOffset);
            byte[] observedParams = mem.ReadBytes(page + ObservedParamsOffset, ParamsSize);
            uint? observedCounter = mem.ReadU32(page + ObservedCounterOffset);
            if (!observedKey.HasValue || !observedCounter.HasValue ||
                observedParams == null)
            {
                return false;
            }

            key = observedKey.Value;
            parameters = observedParams;
            counter = observedCounter.Value;
            return true;
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

        /// <summary>
        /// Handler contract (same for both hook modes):
        ///   [esp+4]  = planner key
        ///   [esp+8]  = ActionParamStruct*
        ///   [esp+0xC]= original planner return value
        /// Returns eax = 1 when a slot was injected, otherwise the original.
        /// </summary>
        private static byte[] BuildHandler(uint page)
        {
            uint data = page + DataOffset;
            List<byte> code = new List<byte>();

            code.Add(0x53);                                   // push ebx
            code.Add(0x56);                                   // push esi
            code.Add(0x57);                                   // push edi
            code.Add(0x55);                                   // push ebp

            // ebp = original result, eax = key, edx = params
            code.AddRange(new byte[] { 0x8B, 0x6C, 0x24, 0x1C });
            code.AddRange(new byte[] { 0x8B, 0x44, 0x24, 0x14 });
            code.AddRange(new byte[] { 0x8B, 0x54, 0x24, 0x18 });

            code.Add(0xBB);                                   // mov ebx, data
            AddU32(code, data);

            code.Add(0xFF); code.Add(0x83);                   // inc [ebx+callCount]
            AddU32(code, CallCountOffset - DataOffset);

            // Phase 0: while observe-only is on, record the game's own plan and
            // return without modifying it.
            // cmp dword [ebx + ObserveFlagOffset], 0
            code.Add(0x83); code.Add(0xBB);
            AddU32(code, ObserveFlagOffset - DataOffset);
            code.Add(0x00);
            code.Add(0x0F); code.Add(0x84);                   // je scan
            int jneScanAt = code.Count;
            AddI32(code, 0);

            code.Add(0x89); code.Add(0x83);                   // mov [ebx+obs], eax
            AddU32(code, ObservedKeyOffset - DataOffset);
            code.AddRange(new byte[] { 0x8B, 0xF2 });         // mov esi, edx
            code.Add(0x8D); code.Add(0xBB);                   // lea edi,[ebx+obs]
            AddU32(code, ObservedParamsOffset - DataOffset);
            code.Add(0xB9);                                   // mov ecx, 7
            AddU32(code, 7);
            code.Add(0xFC);                                   // cld
            code.Add(0xF3); code.Add(0xA5);                   // rep movsd
            code.Add(0xFF); code.Add(0x83);                   // inc [ebx+obsCounter]
            AddU32(code, ObservedCounterOffset - DataOffset);
            code.Add(0xE9);                                   // jmp noMatch
            int jmpNoAt = code.Count;
            AddI32(code, 0);

            int scanAt = code.Count;
            code.Add(0xB9);                                   // mov ecx, SlotCount
            AddU32(code, SlotCount);
            int loopAt = code.Count;
            code.AddRange(new byte[] { 0x39, 0x03 });         // cmp [ebx], eax
            code.Add(0x74);                                   // je found
            int jeFoundAt = code.Count;
            code.Add(0x00);
            code.Add(0x83); code.Add(0xC3); code.Add((byte)SlotSize);
            code.Add(0x49);                                   // dec ecx
            code.Add(0x75);                                   // jnz loop
            int jnzAt = code.Count;
            code.Add(0x00);
            code.Add(0xEB);                                   // jmp noMatch
            int jmpNo2At = code.Count;
            code.Add(0x00);

            int foundAt = code.Count;
            code.Add(0xFF); code.Add(0x43);                   // inc [ebx+calls]
            code.Add((byte)SlotCallOffset);
            code.Add(0x83); code.Add(0x7B);
            code.Add((byte)SlotPendingOffset); code.Add(0x00); // cmp [ebx+4],0
            code.Add(0x74);                                   // je noMatch
            int jeNoAt = code.Count;
            code.Add(0x00);

            code.Add(0x8D); code.Add(0x73);                   // lea esi,[ebx+8]
            code.Add((byte)SlotParamsOffset);
            code.AddRange(new byte[] { 0x8B, 0xFA });         // mov edi, edx
            code.Add(0xB9);                                   // mov ecx, 7
            AddU32(code, 7);
            code.Add(0xFC);                                   // cld
            code.Add(0xF3); code.Add(0xA5);                   // rep movsd

            code.Add(0xC7); code.Add(0x43);                   // mov [ebx+4], 0
            code.Add((byte)SlotPendingOffset);
            code.Add(0x00); code.Add(0x00); code.Add(0x00); code.Add(0x00);
            code.Add(0xFF); code.Add(0x43);                   // inc [ebx+hit]
            code.Add((byte)SlotHitOffset);
            code.Add(0xB8);                                   // mov eax, 1
            AddU32(code, 1);
            code.Add(0xEB);                                   // jmp done
            int jmpDoneAt = code.Count;
            code.Add(0x00);

            int noMatchAt = code.Count;
            code.AddRange(new byte[] { 0x8B, 0xC5 });         // mov eax, ebp

            int doneAt = code.Count;
            code.Add(0x5D);                                   // pop ebp
            code.Add(0x5F);                                   // pop edi
            code.Add(0x5E);                                   // pop esi
            code.Add(0x5B);                                   // pop ebx
            code.Add(0xC3);                                   // ret

            PatchI32(code, jneScanAt, scanAt - (jneScanAt + 4));
            PatchI32(code, jmpNoAt, noMatchAt - (jmpNoAt + 4));
            code[jeFoundAt] = (byte)(foundAt - (jeFoundAt + 1));
            code[jnzAt] = (byte)(loopAt - (jnzAt + 1));
            code[jmpNo2At] = (byte)(noMatchAt - (jmpNo2At + 1));
            code[jeNoAt] = (byte)(noMatchAt - (jeNoAt + 1));
            code[jmpDoneAt] = (byte)(doneAt - (jmpDoneAt + 1));

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

        private static void PatchI32(List<byte> code, int offset, int value)
        {
            code[offset + 0] = (byte)(value & 0xFF);
            code[offset + 1] = (byte)((value >> 8) & 0xFF);
            code[offset + 2] = (byte)((value >> 16) & 0xFF);
            code[offset + 3] = (byte)((value >> 24) & 0xFF);
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
