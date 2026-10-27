using System;
using System.Collections.Generic;

namespace AA2Trainer
{
    internal static class DisplayText
    {
        private static readonly Dictionary<int, string> ActionNames = BuildActionNames();

        public static string ActionLabel(int value)
        {
            string name;
            if (ActionNames.TryGetValue(value, out name)) return name;
            return "#" + value.ToString();
        }

        public static string ActionText(string value)
        {
            int? n = LeadingInt(value);
            if (!n.HasValue) return value;
            string name;
            if (ActionNames.TryGetValue(n.Value, out name))
                return n.Value.ToString() + "  " + name;
            return value;
        }

        private static int? LeadingInt(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            int i = 0;
            bool negative = false;
            if (value[0] == '-') { negative = true; i = 1; }
            int start = i;
            while (i < value.Length && value[i] >= '0' && value[i] <= '9') i++;
            if (i == start) return null;
            int n;
            if (!int.TryParse(value.Substring(start, i - start), out n)) return null;
            return negative ? -n : n;
        }

        private static Dictionary<int, string> BuildActionNames()
        {
            Dictionary<int, string> d = new Dictionary<int, string>();
            d[-1] = "None";
            d[0] = "Cheer Up";
            d[1] = "Calm";
            d[5] = "Encourage to Study";
            d[10] = "Good Rumor";
            d[11] = "Get Along";
            d[12] = "Need to Get Along";
            d[17] = "Food";
            d[18] = "Love Life";
            d[20] = "Try Study";
            d[21] = "Try Exercise";
            d[23] = "Message";
            d[24] = "Let's Go to Class";
            d[40] = "Ask Date";
            d[41] = "Confess";
            d[42] = "Ask to Be Lovers";
            d[48] = "Wants H";
            d[76] = "Show Panties";
            d[77] = "Had Sex?";
            d[84] = "Morning Kiss";
            d[86] = "Kiss";
            d[93] = "Seems ... likes you";
            d[94] = "Confessed";
            d[105] = "Changing Clothes";
            d[108] = "To Study";
            d[109] = "To Exercise";
            d[114] = "Gust of Wind";
            return d;
        }
    }
}
