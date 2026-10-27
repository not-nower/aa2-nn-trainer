using System;
using System.Collections.Generic;
using System.Text;

namespace AA2Trainer
{
    public enum OptionKind
    {
        Toggle,
        InvertedToggle,
        ByteSlider,
        Choice,
        VolumeSlider,
        PercentFloat,
        DirectFloat
    }

    public sealed class OptionFieldDef
    {
        public string Name;
        public string Label;
        public int Offset;
        public OptionKind Kind;
        public int Min;
        public int Max;
        public int DecimalPlaces;
        public string[] Choices;
    }

    public sealed class OptionSnapshot
    {
        public int[] Values;

        public OptionSnapshot Clone()
        {
            OptionSnapshot copy = new OptionSnapshot();
            copy.Values = Values == null ? null : (int[])Values.Clone();
            return copy;
        }
    }

    // Game options live in a 0x330-byte block hanging off [global + 0x38].
    // The normalized UI values use 0/1 for toggles, 0-100 for percentages and
    // audio levels, and direct values for the remaining sliders and choices.
    public static class OptionEditor
    {
        public const int ConfigRootChainOffset = 0x38;
        public const int ConfigSpanSize = 0x330;

        // The game stores audio as attenuation: 0 is full volume and -4000 is
        // silence. The UI exposes the matching 0-100 volume percentage.
        private const int VolumeSilence = -4000;
        private const int VolumeUnitsPerPercent = 40;

        private static readonly OptionFieldDef[] fields = new OptionFieldDef[]
        {
            // System
            F("DisplayShadow", "Show Shadow", 0x2D4, OptionKind.Toggle),
            F("DisplayOutline", "Show Outline", 0x2D5, OptionKind.Toggle),
            F("DisplayCrosshair", "Show Crosshair", 0x2D7, OptionKind.Toggle),

            F("DisplayPlayer", "Show Controlled Character", 0x2BC, OptionKind.Toggle),
            F("DisplayNPC", "Show Partner", 0x2BD, OptionKind.Toggle),
            F("DisplayPenis", "Show Penis", 0x2BE, OptionKind.Toggle),
            F("CameraAngle", "Auto Reset Camera", 0x2D8, OptionKind.Toggle),
            F("DisplayBGChar", "Display BG Character", 0x2BF, OptionKind.Toggle),
            F("Display3D", "Display 3D Objects", 0x2C0, OptionKind.Toggle),
            F("ClickFocus", "Click to move Focus Camera", 0x2D6, OptionKind.Toggle),

            F("MonoMan", "Man Monochrome", 0x2C1, OptionKind.Toggle),
            F("MonoManRed", "Red", 0x2C4, OptionKind.PercentFloat, 0, 100),
            F("MonoManGreen", "Green", 0x2C8, OptionKind.PercentFloat, 0, 100),
            F("MonoManBlue", "Blue", 0x2CC, OptionKind.PercentFloat, 0, 100),
            F("MonoManOpa", "Opacity", 0x2D0, OptionKind.PercentFloat, 0, 100),

            // Sound
            F("BGM", "Background Music", 0x38, OptionKind.InvertedToggle),
            F("Sound", "Sound Effect", 0x39, OptionKind.InvertedToggle),
            F("Voice", "Voice", 0x3B, OptionKind.InvertedToggle),
            F("HSound", "H-Sound", 0x3A, OptionKind.InvertedToggle),
            F("BGMVal", "Background Music volume", 0x28, OptionKind.VolumeSlider, 0, 100),
            F("SoundVal", "Sound Effect volume", 0x2C, OptionKind.VolumeSlider, 0, 100),
            F("VoiceVal", "Voice volume", 0x34, OptionKind.VolumeSlider, 0, 100),
            F("HSoundVal", "H-Sound volume", 0x30, OptionKind.VolumeSlider, 0, 100),

            // Additional (1)
            F("NPCConversion", "Background NPC Volume", 0x2E0, OptionKind.InvertedToggle),
            F("NPCConversionVal", "Background NPC Volume level", 0x2DC,
                OptionKind.PercentFloat, 0, 100),
            F("BGExternal", "Use custom BGM", 0x2E1, OptionKind.Toggle),
            F("ReleaseMouse", "Unlock cursor from game window", 0x2E2,
                OptionKind.Toggle),

            F("PlayerMove", "Player Movement Speed", 0x2E4, OptionKind.Toggle),
            F("PlayerMoveVal", "Player movement speed", 0x2E8,
                OptionKind.DirectFloat, 1, 50, 1),
            F("NPCMove", "NPC Movement Speed", 0x2EC, OptionKind.Toggle),
            F("NPCMoveVal", "NPC movement speed", 0x2F0,
                OptionKind.DirectFloat, 1, 50, 1),
            F("WorldMove", "Game World Speed", 0x2F4, OptionKind.Toggle),
            F("WorldMoveVal", "Game world speed", 0x2F8,
                OptionKind.DirectFloat, 10, 50, 1),

            F("CustomBell", "Period Length", 0x2FC, OptionKind.Toggle),
            F("CustomBellVal", "Period length value", 0x2FE,
                OptionKind.ByteSlider, 1, 60),
            F("OverrideRisk", "Override Pregnancy Risk", 0x300, OptionKind.Toggle),
            F("OverrideRiskVal", "Pregnancy risk mode", 0x301,
                OptionKind.Choice, 0, 2, 0, "Normal", "Safe", "Risk"),
            F("OverrideCondom", "Override Condom Use", 0x302, OptionKind.Toggle),
            F("OverrideCondomVal", "Condom mode", 0x303,
                OptionKind.Choice, 0, 1, 0, "Don't", "Use"),

            // Additional (2)
            F("DisableInteractionNPCtoPlayer", "Disable Interaction NPC-Player",
                0x304, OptionKind.Toggle),
            F("DisableInteruptionNPCtoPlayer", "Disable Interruption NPC-Player",
                0x305, OptionKind.Toggle),
            F("DisableInteractionNPCtoNPC", "Disable Interaction NPC-NPC",
                0x306, OptionKind.Toggle),
            F("DisableInteruptionNPCtoNPC", "Disable Interruption NPC-NPC",
                0x307, OptionKind.Toggle),
            F("DisableInteractionAutoPCtoNPC", "Disable Interaction Auto Player-NPC",
                0x308, OptionKind.Toggle),
            F("DisableInteruptionAutoPCtoNPC", "Disable Interruption Auto Player-NPC",
                0x309, OptionKind.Toggle),

            F("DisableABUSEActions", "Disable \"ABUSE\" Actions",
                0x30A, OptionKind.Toggle),
            F("DisableViolenceActions", "Disable \"VIOLENCE\" Actions",
                0x30B, OptionKind.Toggle),
            F("DisableContestActions", "Disable \"CONTEST\" Actions",
                0x30C, OptionKind.Toggle),
            F("DisableNPCEscape", "Disable NPC Escaping",
                0x30D, OptionKind.Toggle),
            F("DisableCOMMANDActions", "Disable \"COMMAND\" Actions",
                0x30E, OptionKind.Toggle),
            F("DisableJOINCLUBActions", "Disable \"CLUB JOIN\" Action",
                0x30F, OptionKind.Toggle),
            F("DisableBELOVERAction", "Disable \"BE LOVERS\" Action",
                0x310, OptionKind.Toggle),
            F("DisableBREAKUPAction", "Disable \"BREAK-UP\" Action",
                0x311, OptionKind.Toggle),
            F("DisableSKINSHIP", "Disable \"SKINSHIP\" Action",
                0x312, OptionKind.Toggle),
            F("DisableSEXAction", "Disable \"SEX\" Action",
                0x313, OptionKind.Toggle),
            F("DisableSexualExceptSex", "Disable H except for \"SEX\" Action",
                0x314, OptionKind.Toggle),
            F("DisableEveryoneCommand", "Disable \"Everyone ..\" Action",
                0x315, OptionKind.Toggle),
            F("DisableNPCGoingHome", "Disable NPC Going Home",
                0x316, OptionKind.Toggle),
            F("DisableNPCTraining", "Disable Personal Training Actions",
                0x317, OptionKind.Toggle),

            // Additional (3)
            F("LOVEorLIKEModPlayertoNPC", "Player -> NPC Love / Like",
                0x318, OptionKind.Toggle),
            F("LOVEorLIKEModPlayertoNPCVal", "Player -> NPC Love / Like rate",
                0x319, OptionKind.ByteSlider, 0, 10),
            F("DISLIKEorHATEModPlayertoNPC", "Player -> NPC Dislike / Hate",
                0x31A, OptionKind.Toggle),
            F("DISLIKEorHATEModPlayertoNPCVal", "Player -> NPC Dislike / Hate rate",
                0x31B, OptionKind.ByteSlider, 0, 10),
            F("LOVEorLIKEModNPCtoPlayer", "NPC -> Player Love / Like",
                0x31C, OptionKind.Toggle),
            F("LOVEorLIKEModNPCtoPlayerVal", "NPC -> Player Love / Like rate",
                0x31D, OptionKind.ByteSlider, 0, 10),
            F("DISLIKEorHATEModNPCtoPlayer", "NPC -> Player Dislike / Hate",
                0x31E, OptionKind.Toggle),
            F("DISLIKEorHATEModNPCtoPlayerVal", "NPC -> Player Dislike / Hate rate",
                0x31F, OptionKind.ByteSlider, 0, 10),
            F("LOVEorLIKEModNPCtoNPC", "NPC -> NPC Love / Like",
                0x320, OptionKind.Toggle),
            F("LOVEorLIKEModNPCtoNPCval", "NPC -> NPC Love / Like rate",
                0x321, OptionKind.ByteSlider, 0, 10),
            F("DISLIKEorHATEModNPCtoNPC", "NPC -> NPC Dislike / Hate",
                0x322, OptionKind.Toggle),
            F("DISLIKEorHATEModNPCtoNPCval", "NPC -> NPC Dislike / Hate rate",
                0x323, OptionKind.ByteSlider, 0, 10),

            F("PositiveRespontoPlayer", "Positive Response to Player 999%",
                0x324, OptionKind.Toggle),
            F("SuccessfulTraining", "Training Always Succeeds",
                0x325, OptionKind.Toggle),
            F("SkipHScane", "Skip H-Scenes",
                0x326, OptionKind.Toggle),
            F("EveryoneHMood", "Everybody is in H-Mood",
                0x327, OptionKind.Toggle)
        };

        public static IList<OptionFieldDef> Fields { get { return fields; } }

        public static int IndexOf(string name)
        {
            for (int i = 0; i < fields.Length; i++)
                if (fields[i].Name == name) return i;
            return -1;
        }

        public static bool TryRead(GameMemory mem, GameContext ctx,
            out OptionSnapshot snapshot, out string error)
        {
            snapshot = null;
            error = null;

            uint configRoot;
            if (!TryResolveRoot(mem, ctx, out configRoot, out error))
                return false;

            byte[] block = mem.ReadBytes(configRoot, ConfigSpanSize);
            if (block == null || block.Length < ConfigSpanSize)
            {
                error = "The game-option block is unreadable.";
                return false;
            }

            int[] values = new int[fields.Length];
            for (int i = 0; i < fields.Length; i++)
            {
                OptionFieldDef def = fields[i];
                switch (def.Kind)
                {
                    case OptionKind.Toggle:
                        values[i] = block[def.Offset] == 0 ? 0 : 1;
                        break;
                    case OptionKind.InvertedToggle:
                        values[i] = block[def.Offset] == 0 ? 1 : 0;
                        break;
                    case OptionKind.ByteSlider:
                    case OptionKind.Choice:
                        values[i] = block[def.Offset];
                        break;
                    case OptionKind.VolumeSlider:
                    {
                        int raw = BitConverter.ToInt32(block, def.Offset);
                        values[i] = (int)Math.Round(
                            (raw - VolumeSilence) / (double)VolumeUnitsPerPercent);
                        break;
                    }
                    case OptionKind.PercentFloat:
                    {
                        float raw = BitConverter.ToSingle(block, def.Offset);
                        values[i] = (int)Math.Round(raw * 100f);
                        break;
                    }
                    case OptionKind.DirectFloat:
                    {
                        float raw = BitConverter.ToSingle(block, def.Offset);
                        int factor = def.DecimalPlaces > 0
                            ? (int)Math.Pow(10, def.DecimalPlaces) : 1;
                        values[i] = (int)Math.Round(raw * factor);
                        break;
                    }
                }
                values[i] = Clamp(values[i], def.Min, def.Max);
            }

            snapshot = new OptionSnapshot();
            snapshot.Values = values;
            return true;
        }

        public static bool TryWrite(GameMemory mem, GameContext ctx,
            OptionSnapshot original, OptionSnapshot edited,
            out string changed, out string error)
        {
            changed = "";
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
            if (edited == null || edited.Values == null ||
                edited.Values.Length != fields.Length)
            {
                error = "The game-option values are incomplete.";
                return false;
            }

            for (int i = 0; i < fields.Length; i++)
            {
                OptionFieldDef def = fields[i];
                int value = edited.Values[i];
                if (value < def.Min || value > def.Max)
                {
                    error = def.Label + " is out of range.";
                    return false;
                }
                if (def.Kind == OptionKind.Choice && def.Choices != null &&
                    value >= def.Choices.Length)
                {
                    error = def.Label + " is not a valid choice.";
                    return false;
                }
            }

            uint configRoot;
            if (!TryResolveRoot(mem, ctx, out configRoot, out error))
                return false;

            StringBuilder written = new StringBuilder();
            for (int i = 0; i < fields.Length; i++)
            {
                OptionFieldDef def = fields[i];
                int value = edited.Values[i];
                int previous = original == null || original.Values == null
                    ? int.MinValue : original.Values[i];
                if (value == previous) continue;

                uint address = configRoot + (uint)def.Offset;
                bool ok;
                switch (def.Kind)
                {
                    case OptionKind.Toggle:
                        ok = MemoryWriter.TryWriteByte(mem, address, (byte)value,
                            def.Label, out error);
                        break;
                    case OptionKind.InvertedToggle:
                        ok = MemoryWriter.TryWriteByte(mem, address,
                            (byte)(value == 0 ? 1 : 0), def.Label, out error);
                        break;
                    case OptionKind.ByteSlider:
                    case OptionKind.Choice:
                        ok = MemoryWriter.TryWriteByte(mem, address, (byte)value,
                            def.Label, out error);
                        break;
                    case OptionKind.VolumeSlider:
                        ok = MemoryWriter.TryWriteInt32(mem, address,
                            VolumeSilence + value * VolumeUnitsPerPercent,
                            def.Label, out error);
                        break;
                    case OptionKind.PercentFloat:
                        ok = MemoryWriter.TryWriteFloat(mem, address, value / 100f,
                            def.Label, out error);
                        break;
                    case OptionKind.DirectFloat:
                    {
                        int factor = def.DecimalPlaces > 0
                            ? (int)Math.Pow(10, def.DecimalPlaces) : 1;
                        ok = MemoryWriter.TryWriteFloat(mem, address,
                            value / (float)factor, def.Label, out error);
                        break;
                    }
                    default:
                        ok = false;
                        error = "Unknown option type for " + def.Label + ".";
                        break;
                }

                if (!ok)
                {
                    changed = written.Length == 0 ? "" : written.ToString();
                    return false;
                }
                if (written.Length > 0) written.Append(", ");
                written.Append(def.Label.ToLowerInvariant());
            }

            changed = written.Length == 0 ? "No changes." : written.ToString();
            return true;
        }

        private static bool TryResolveRoot(GameMemory mem, GameContext ctx,
            out uint configRoot, out string error)
        {
            configRoot = 0;
            error = null;

            if (mem == null || !mem.IsAttached)
            {
                error = "The game is not attached.";
                return false;
            }
            if (ctx == null || ctx.GlobalRoot == 0)
            {
                error = "The class data is not loaded.";
                return false;
            }

            uint? root = mem.ReadU32(ctx.GlobalRoot + ConfigRootChainOffset);
            if (!root.HasValue || !mem.IsPlausiblePointer(root.Value))
            {
                error = "The game-option pointer is not valid.";
                return false;
            }

            configRoot = root.Value;
            return true;
        }

        private static OptionFieldDef F(string name, string label, int offset,
            OptionKind kind, int min = 0, int max = 1, int decimalPlaces = 0,
            params string[] choices)
        {
            OptionFieldDef def = new OptionFieldDef();
            def.Name = name;
            def.Label = label;
            def.Offset = offset;
            def.Kind = kind;
            def.Min = min;
            def.Max = max;
            def.DecimalPlaces = decimalPlaces;
            def.Choices = choices;
            return def;
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum) return minimum;
            if (value > maximum) return maximum;
            return value;
        }
    }
}
