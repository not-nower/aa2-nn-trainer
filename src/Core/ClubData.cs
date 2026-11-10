using System;
using System.Collections.Generic;

namespace AA2Trainer
{
    /// <summary>
    /// Reads the current class's club names from the live game data. The
    /// trainer's static CLUB name table is only a fallback; clubs can be
    /// renamed per save/class.
    /// </summary>
    public static class ClubData
    {
        private static readonly string[] Fallback =
        {
            "Club 1", "Club 2", "Club 3", "Club 4",
            "Club 5", "Club 6", "Club 7", "Club 8"
        };

        public static string[] ReadNames(GameMemory mem, GameContext ctx)
        {
            string[] names = new string[Fallback.Length];
            for (int i = 0; i < names.Length; i++)
                names[i] = ReadName(mem, ctx, i);
            return names;
        }

        private static string ReadName(GameMemory mem, GameContext ctx, int index)
        {
            string field = "Club" + (index + 1).ToString() + "Name";
            PointerDef def = Find(field);
            if (def == null || mem == null || ctx == null || !mem.IsAttached)
                return Fallback[index];

            EvalResult result = Evaluator.Evaluate(mem, ctx, def, 0);
            string name = result == null ? "" : (result.Value ?? "");
            name = StripQuotes(name).Trim();
            if (name.Length == 0 || name == "-" || name == "BROKEN")
                return Fallback[index];
            return name;
        }

        private static PointerDef Find(string name)
        {
            foreach (PointerDef def in Definitions.All)
                if (def.Name == name) return def;
            return null;
        }

        private static string StripQuotes(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
                return value.Substring(1, value.Length - 2);
            return value;
        }
    }
}
