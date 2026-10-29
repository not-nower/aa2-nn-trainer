using System;
using System.Collections.Generic;
using System.Text;

namespace AA2Trainer
{
    /// <summary>
    /// One module stored in a character's AAU card data.
    /// </summary>
    public sealed class AauModuleInfo
    {
        public string Name;
        public string Description;
    }

    public enum AauModuleStatus
    {
        Ok,
        NoAauData,
        NoModules,
        Error
    }

    public sealed class AauModuleReadResult
    {
        public AauModuleStatus Status = AauModuleStatus.NoAauData;
        public readonly List<AauModuleInfo> Modules = new List<AauModuleInfo>();
        public string Error;
    }

    /// <summary>
    /// Reads the AAU module list from a character's card data. This is a
    /// read-only path: it never writes to the game and does not need AAU's
    /// runtime objects. Modules are stored in the character's PNG buffer as
    /// an "aaUd" PNG chunk containing a "TrMd" member.
    /// </summary>
    public static class AauModuleReader
    {
        private const uint CharacterDataOffset = 0x28;
        private const uint PngBufferSizeOffset = 0x30;
        private const uint PngBufferOffset = 0x34;

        private const int MaxPngBufferSize = 32 * 1024 * 1024;
        private const int MaxAauChunkSize = 8 * 1024 * 1024;
        private const int MaxModules = 128;
        private const int MaxStringChars = 16384;
        private const int MaxElements = 100000;
        private const int MaxDepth = 80;
        private const long MaxSteps = 1000000;
        private const int MaxCandidates = 64;

        // The PNG chunk is "aaUd" in the file. "dUaa" is accepted as an
        // alternate spelling used by some card layouts.
        private static readonly byte[] AauChunkId = { 0x61, 0x61, 0x55, 0x64 };
        private static readonly byte[] AauChunkIdAlt = { 0x64, 0x55, 0x61, 0x61 };
        private static readonly byte[] IendId = { 0x49, 0x45, 0x4E, 0x44 };

        // MSVC stores multi-character constants big-endian, so 'TrMd' is
        // written to the card as the bytes "dMrT". The plain form is kept as
        // a fallback for cards produced by other tools.
        private static readonly byte[] TrMdId = { 0x64, 0x4D, 0x72, 0x54 };
        private static readonly byte[] TrMdIdAlt = { 0x54, 0x72, 0x4D, 0x64 };
        private static readonly byte[] TrAtId = { 0x74, 0x41, 0x72, 0x54 };
        private static readonly byte[] TrAtIdAlt = { 0x54, 0x72, 0x41, 0x74 };

        public static AauModuleReadResult Read(GameMemory mem, uint characterPointer)
        {
            AauModuleReadResult result = new AauModuleReadResult();
            if (mem == null || !mem.IsAttached || characterPointer == 0)
            {
                result.Status = AauModuleStatus.Error;
                result.Error = "The game is not attached.";
                return result;
            }

            try
            {
                uint? characterData = mem.ReadU32(characterPointer + CharacterDataOffset);
                if (!characterData.HasValue || characterData.Value == 0 ||
                    !mem.IsPlausiblePointer(characterData.Value))
                {
                    result.Status = AauModuleStatus.NoAauData;
                    return result;
                }

                uint? sizeValue = mem.ReadU32(characterData.Value + PngBufferSizeOffset);
                uint? bufferValue = mem.ReadU32(characterData.Value + PngBufferOffset);
                if (!sizeValue.HasValue || !bufferValue.HasValue ||
                    sizeValue.Value == 0 || bufferValue.Value == 0 ||
                    !mem.IsPlausiblePointer(bufferValue.Value) ||
                    sizeValue.Value > MaxPngBufferSize)
                {
                    result.Status = AauModuleStatus.NoAauData;
                    return result;
                }

                // Walk the PNG chunk headers and read only the AAU chunk. The
                // card image itself can be several MB and is never copied.
                int pngSize = (int)sizeValue.Value;
                int offset = 8;
                while (offset + 12 <= pngSize)
                {
                    byte[] header = mem.ReadBytes(bufferValue.Value + (uint)offset, 8);
                    if (header == null || header.Length != 8) break;

                    long length = ((long)header[0] << 24) |
                        ((long)header[1] << 16) |
                        ((long)header[2] << 8) | header[3];
                    if (length <= 0 || length > MaxAauChunkSize) break;
                    if ((long)offset + 12 + length > pngSize) break;

                    if (Matches(header, 4, AauChunkId) ||
                        Matches(header, 4, AauChunkIdAlt))
                    {
                        byte[] chunk = mem.ReadBytes(
                            bufferValue.Value + (uint)offset + 8, (int)length);
                        if (chunk == null || chunk.Length != (int)length)
                        {
                            result.Status = AauModuleStatus.Error;
                            result.Error = "The AAU card chunk is unreadable.";
                            return result;
                        }
                        return ParseChunk(chunk);
                    }

                    if (Matches(header, 4, IendId)) break;
                    offset = (int)((long)offset + 12 + length);
                }

                result.Status = AauModuleStatus.NoAauData;
                return result;
            }
            catch (Exception ex)
            {
                result.Status = AauModuleStatus.Error;
                result.Error = ex.Message;
                return result;
            }
        }

        private static AauModuleReadResult ParseChunk(byte[] chunk)
        {
            AauModuleReadResult result = new AauModuleReadResult();
            if (chunk == null || chunk.Length < 8)
            {
                result.Status = AauModuleStatus.NoModules;
                return result;
            }

            List<AauModuleInfo> best = null;
            int bestScore = -1;
            int candidates = 0;

            for (int i = 0; i + 4 <= chunk.Length && candidates < MaxCandidates; i++)
            {
                if (!Matches(chunk, i, TrMdId) && !Matches(chunk, i, TrMdIdAlt))
                    continue;

                candidates++;
                List<AauModuleInfo> modules;
                int end;
                if (!TryParseModules(chunk, i, out modules, out end))
                    continue;

                int score = modules.Count;
                if (NextIsTrAt(chunk, end)) score += 1000;
                else if (end == chunk.Length) score += 500;
                else if (NextIsMemberId(chunk, end)) score += 100;
                else continue;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = modules;
                }
            }

            if (best == null)
            {
                bool hasTrMd = FindId(chunk, TrMdId) >= 0 ||
                    FindId(chunk, TrMdIdAlt) >= 0;
                if (hasTrMd)
                {
                    result.Status = AauModuleStatus.Error;
                    result.Error = "The AAU module list could not be parsed.";
                }
                else
                {
                    result.Status = AauModuleStatus.NoModules;
                }
                return result;
            }

            result.Modules.AddRange(best);
            result.Status = best.Count == 0
                ? AauModuleStatus.NoModules : AauModuleStatus.Ok;
            return result;
        }

        /// <summary>
        /// Parses one std::vector&lt;Module&gt; starting after the TrMd id.
        /// Returns the position just past the vector so the caller can check
        /// that it is followed by TrAt (card storage) or the chunk end.
        /// </summary>
        private static bool TryParseModules(byte[] chunk, int offset,
            out List<AauModuleInfo> modules, out int end)
        {
            modules = null;
            end = 0;
            try
            {
                Reader reader = new Reader(chunk, offset + 4, chunk.Length);
                int count = reader.ReadCount();
                if (count > MaxModules) return false;

                List<AauModuleInfo> list =
                    new List<AauModuleInfo>(Math.Min(count, 16));
                for (int i = 0; i < count; i++)
                    list.Add(ReadModule(reader));

                end = reader.Position;
                if (end < offset + 8 || end > chunk.Length) return false;
                modules = list;
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        private static AauModuleInfo ReadModule(Reader reader)
        {
            AauModuleInfo module = new AauModuleInfo();
            module.Name = reader.ReadWString();
            module.Description = reader.ReadWString();

            if (string.IsNullOrEmpty(module.Name) || module.Name.Length > 256)
                throw new FormatException("Invalid AAU module name.");

            int triggers = reader.ReadCount();
            for (int i = 0; i < triggers; i++)
                SkipTrigger(reader);

            int globals = reader.ReadCount();
            for (int i = 0; i < globals; i++)
                SkipGlobalVariable(reader);

            int dependencies = reader.ReadCount();
            for (int i = 0; i < dependencies; i++)
                reader.ReadWString();

            return module;
        }

        private static void SkipTrigger(Reader reader)
        {
            reader.Enter();
            try
            {
                reader.ReadWString(); // trigger name

                int events = reader.ReadCount();
                for (int i = 0; i < events; i++)
                {
                    reader.ReadI32(); // event id
                    int arguments = reader.ReadCount();
                    for (int j = 0; j < arguments; j++)
                        SkipExpression(reader);
                }

                int variables = reader.ReadCount();
                for (int i = 0; i < variables; i++)
                {
                    reader.ReadI32(); // type
                    reader.ReadWString(); // name
                    SkipExpression(reader); // defaultValue
                }

                int actions = reader.ReadCount();
                for (int i = 0; i < actions; i++)
                    SkipGuiAction(reader);
            }
            finally
            {
                reader.Leave();
            }
        }

        private static void SkipGuiAction(Reader reader)
        {
            reader.Enter();
            try
            {
                SkipParameterisedAction(reader);
                int subactions = reader.ReadCount();
                for (int i = 0; i < subactions; i++)
                    SkipGuiAction(reader);
            }
            finally
            {
                reader.Leave();
            }
        }

        private static void SkipParameterisedAction(Reader reader)
        {
            reader.ReadI32(); // action id
            int arguments = reader.ReadCount();
            for (int i = 0; i < arguments; i++)
                SkipExpression(reader);
        }

        private static void SkipExpression(Reader reader)
        {
            reader.Enter();
            try
            {
                reader.ReadI32(); // return type
                int id = reader.ReadI32();
                if (id == 1)
                {
                    SkipValue(reader); // EXPR_CONSTANT
                }
                else if (id == 2)
                {
                    reader.ReadWString(); // EXPR_VAR
                }
                else if (id == 3)
                {
                    reader.ReadI32(); // EXPR_NAMEDCONSTANT
                }
                else
                {
                    int arguments = reader.ReadCount();
                    for (int i = 0; i < arguments; i++)
                        SkipExpression(reader);
                }
            }
            finally
            {
                reader.Leave();
            }
        }

        private static void SkipValue(Reader reader)
        {
            int type = reader.ReadI32();
            if (type == 1) reader.ReadI32();       // TYPE_INT
            else if (type == 2) reader.ReadU8();   // TYPE_BOOL
            else if (type == 3) reader.ReadF32();  // TYPE_FLOAT
            else if (type == 4) reader.ReadWString(); // TYPE_STRING
        }

        private static void SkipGlobalVariable(Reader reader)
        {
            reader.ReadI32(); // type
            reader.ReadWString(); // name
            SkipValue(reader); // defaultValue
            SkipValue(reader); // currentValue
            reader.ReadU8(); // initialized
        }

        private static bool NextIsTrAt(byte[] chunk, int offset)
        {
            return Matches(chunk, offset, TrAtId) ||
                Matches(chunk, offset, TrAtIdAlt);
        }

        private static bool NextIsMemberId(byte[] chunk, int offset)
        {
            if (offset < 0 || offset > chunk.Length - 4) return false;
            for (int i = 0; i < 4; i++)
            {
                byte value = chunk[offset + i];
                if (value < 0x20 || value > 0x7E) return false;
            }
            return true;
        }

        private static int FindId(byte[] data, byte[] pattern)
        {
            for (int i = 0; i + pattern.Length <= data.Length; i++)
            {
                if (Matches(data, i, pattern)) return i;
            }
            return -1;
        }

        private static bool Matches(byte[] data, int offset, byte[] pattern)
        {
            if (data == null || pattern == null) return false;
            if (offset < 0 || offset > data.Length - pattern.Length) return false;
            for (int i = 0; i < pattern.Length; i++)
            {
                if (data[offset + i] != pattern[i]) return false;
            }
            return true;
        }

        /// <summary>
        /// Bounds-checked reader for AAU's serialized std::vector&lt;Module&gt;
        /// format. Scalar values are little-endian; strings are UTF-16LE with a
        /// 4-byte character count.
        /// </summary>
        private sealed class Reader
        {
            private readonly byte[] data;
            private int position;
            private readonly int end;
            private int depth;
            private long steps;

            public Reader(byte[] data, int position, int end)
            {
                this.data = data;
                this.position = position;
                this.end = end;
            }

            public int Position
            {
                get { return position; }
            }

            private void Need(int count)
            {
                if (count < 0 || position > end - count)
                    throw new FormatException("Unexpected end of AAU data.");
                steps++;
                if (steps > MaxSteps)
                    throw new FormatException("AAU module data is too large.");
            }

            public byte ReadU8()
            {
                Need(1);
                return data[position++];
            }

            public int ReadI32()
            {
                Need(4);
                int value = data[position] |
                    (data[position + 1] << 8) |
                    (data[position + 2] << 16) |
                    (data[position + 3] << 24);
                position += 4;
                return value;
            }

            public float ReadF32()
            {
                Need(4);
                float value = BitConverter.ToSingle(data, position);
                position += 4;
                return value;
            }

            public string ReadWString()
            {
                int length = ReadI32();
                if (length < 0 || length > MaxStringChars)
                    throw new FormatException("Invalid AAU string length.");
                Need(length * 2);
                string value = Encoding.Unicode.GetString(data, position, length * 2);
                position += length * 2;
                return value;
            }

            public int ReadCount()
            {
                int count = ReadI32();
                if (count < 0 || count > MaxElements)
                    throw new FormatException("Invalid AAU list length.");
                return count;
            }

            public void Enter()
            {
                depth++;
                if (depth > MaxDepth)
                    throw new FormatException("AAU module data is nested too deeply.");
            }

            public void Leave()
            {
                depth--;
            }
        }
    }
}
