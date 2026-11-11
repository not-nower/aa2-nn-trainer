using System;
using System.Collections.Generic;
using System.Windows.Media.Imaging;
using AA2Trainer;

namespace AA2Trainer.WpfPrototype
{
    /// <summary>
    /// Reads the live thumbnail and full portrait that the game stores on each
    /// character-data block. Layout matches the WinForms PortraitLoader:
    /// ThumbSize/ThumbAddr at +0x28/+0x2C and PotraitSize/PotraitAddr at
    /// +0x30/+0x34.
    ///
    /// Thumbnails are small and are read for every seat. Full portraits are
    /// large, so the selected seat is read immediately and the rest fill in one
    /// per poll. Decoded images are frozen before they leave the poll thread.
    /// </summary>
    public sealed class PortraitService
    {
        private const uint MaxPortraitSize = 8 * 1024 * 1024;
        private const int FullPortraitsPerPoll = 1;
        private const int RetrySeconds = 5;

        private sealed class Entry
        {
            public uint ThumbAddress;
            public uint ThumbSize;
            public uint FullAddress;
            public uint FullSize;
            public BitmapImage Thumbnail;
            public BitmapImage FullPortrait;
            public bool ThumbFailed;
            public bool FullFailed;
            public DateTime LastThumbAttemptUtc;
            public DateTime LastFullAttemptUtc;
        }

        private readonly GameMemory mem;
        private readonly Dictionary<uint, Entry> entries =
            new Dictionary<uint, Entry>();
        private int rosterVersion = -1;

        public PortraitService(GameMemory memory)
        {
            mem = memory;
        }

        public void Clear()
        {
            entries.Clear();
            rosterVersion = -1;
        }

        public void Refresh(GameContext ctx, CharacterInfo selected)
        {
            if (mem == null || !mem.IsAttached || ctx == null)
            {
                Clear();
                return;
            }

            // A new roster means a new class/save; drop every portrait so a
            // stale image can never be shown for a reused pointer.
            if (ctx.RosterVersion != rosterVersion)
            {
                rosterVersion = ctx.RosterVersion;
                entries.Clear();
            }

            if (entries.Count > 0)
            {
                HashSet<uint> live = new HashSet<uint>();
                foreach (CharacterInfo info in ctx.Characters)
                {
                    if (info != null && info.Pointer != 0)
                        live.Add(info.Pointer);
                }

                if (entries.Count > live.Count)
                {
                    List<uint> stale = new List<uint>();
                    foreach (uint pointer in entries.Keys)
                    {
                        if (!live.Contains(pointer)) stale.Add(pointer);
                    }
                    foreach (uint pointer in stale) entries.Remove(pointer);
                }
            }

            foreach (CharacterInfo info in ctx.Characters)
            {
                if (info == null || info.Pointer == 0) continue;
                Entry entry = GetEntry(info.Pointer);
                RefreshThumbnail(info, entry);
            }

            if (selected != null && selected.Pointer != 0)
                RefreshFull(GetEntry(selected.Pointer));

            // Fill the remaining full portraits slowly so hovering any seat
            // shows the real card portrait without a burst of large reads.
            int budget = FullPortraitsPerPoll;
            foreach (CharacterInfo info in ctx.Characters)
            {
                if (budget <= 0) break;
                if (info == null || info.Pointer == 0) continue;
                if (selected != null && info.Pointer == selected.Pointer)
                    continue;
                Entry entry = GetEntry(info.Pointer);
                if (entry.FullPortrait != null) continue;
                if (RefreshFull(entry)) budget--;
            }
        }

        public BitmapImage ThumbnailFor(CharacterInfo info)
        {
            if (info == null) return null;
            Entry entry;
            return entries.TryGetValue(info.Pointer, out entry)
                ? entry.Thumbnail : null;
        }

        public BitmapImage FullPortraitFor(CharacterInfo info)
        {
            if (info == null) return null;
            Entry entry;
            return entries.TryGetValue(info.Pointer, out entry)
                ? entry.FullPortrait : null;
        }

        private Entry GetEntry(uint pointer)
        {
            Entry entry;
            if (!entries.TryGetValue(pointer, out entry))
            {
                entry = new Entry();
                entries[pointer] = entry;
            }
            return entry;
        }

        private void RefreshThumbnail(CharacterInfo info, Entry entry)
        {
            uint thumbAddress;
            uint thumbSize;
            uint fullAddress;
            uint fullSize;
            if (!ReadPortraitInfo(info, entry, out thumbAddress, out thumbSize,
                out fullAddress, out fullSize))
                return;

            if (thumbAddress == 0)
            {
                if (entry.Thumbnail != null)
                {
                    entry.Thumbnail = null;
                    entry.ThumbFailed = false;
                }
                return;
            }

            if (entry.Thumbnail != null &&
                entry.ThumbAddress == thumbAddress &&
                entry.ThumbSize == thumbSize)
                return;

            DateTime now = DateTime.UtcNow;
            if (entry.ThumbFailed &&
                entry.ThumbAddress == thumbAddress &&
                entry.ThumbSize == thumbSize &&
                (now - entry.LastThumbAttemptUtc).TotalSeconds < RetrySeconds)
                return;

            entry.ThumbAddress = thumbAddress;
            entry.ThumbSize = thumbSize;
            entry.LastThumbAttemptUtc = now;
            entry.Thumbnail = CardImageLoader.ToImage(
                ReadImage(thumbAddress, thumbSize), 0);
            entry.ThumbFailed = entry.Thumbnail == null;
        }

        private bool RefreshFull(Entry entry)
        {
            if (entry.FullAddress == 0 || entry.FullSize == 0) return false;
            if (entry.FullPortrait != null) return false;

            DateTime now = DateTime.UtcNow;
            if (entry.FullFailed &&
                (now - entry.LastFullAttemptUtc).TotalSeconds < RetrySeconds)
                return false;

            entry.LastFullAttemptUtc = now;
            entry.FullPortrait = CardImageLoader.ToImage(
                ReadImage(entry.FullAddress, entry.FullSize), 0);
            entry.FullFailed = entry.FullPortrait == null;
            return true;
        }

        private bool ReadPortraitInfo(CharacterInfo info, Entry entry,
            out uint thumbAddress, out uint thumbSize,
            out uint fullAddress, out uint fullSize)
        {
            thumbAddress = 0;
            thumbSize = 0;
            fullAddress = 0;
            fullSize = 0;

            uint characterData;
            string error;
            if (!CharacterEditor.TryGetCharacterData(mem, info,
                out characterData, out error) || characterData == 0)
                return false;

            byte[] block = mem.ReadBytes(characterData + 0x28, 0x10);
            if (block == null || block.Length < 0x10) return false;

            thumbSize = BitConverter.ToUInt32(block, 0x00);
            thumbAddress = BitConverter.ToUInt32(block, 0x04);
            fullSize = BitConverter.ToUInt32(block, 0x08);
            fullAddress = BitConverter.ToUInt32(block, 0x0C);

            if (!IsValid(thumbAddress, thumbSize))
            {
                thumbAddress = 0;
                thumbSize = 0;
            }
            if (!IsValid(fullAddress, fullSize))
            {
                fullAddress = 0;
                fullSize = 0;
            }

            if (entry.FullAddress != fullAddress ||
                entry.FullSize != fullSize)
            {
                entry.FullAddress = fullAddress;
                entry.FullSize = fullSize;
                entry.FullPortrait = null;
                entry.FullFailed = false;
            }
            return true;
        }

        private bool IsValid(uint address, uint size)
        {
            return address != 0 && size > 0 && size <= MaxPortraitSize &&
                mem.IsPlausiblePointer(address);
        }

        private byte[] ReadImage(uint address, uint size)
        {
            if (!IsValid(address, size)) return null;
            return mem.ReadBytes(address, (int)size);
        }
    }
}
