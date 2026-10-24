using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;


namespace AA2Trainer
{
    public partial class MainForm
    {
        private string FormatFieldValue(PointerDef def, EvalResult r)
        {
            if (def == null || r == null) return r != null ? r.Value : "";
            if (r.Bytes == null) return r.Value;

            int value;
            if (def.Type == 3)
            {
                if (r.Bytes.Length < 1) return r.Value;
                value = r.Bytes[0];
            }
            else if (def.Type == 4 || def.Type == 8)
            {
                if (r.Bytes.Length < 4) return r.Value;
                value = BitConverter.ToInt32(r.Bytes, 0);
            }
            else
            {
                return r.Value;
            }
            if (value < 0) return r.Value;

            if (def.Name == "Club")
            {
                string club = ClubName(mem, ctx);
                if (!string.IsNullOrEmpty(club) && club != "-")
                    return r.Value + "   " + club;
            }

            string name = names.DescribeName(def.Name, selectedGender, value);
            if (string.IsNullOrEmpty(name)) return r.Value;
            return r.Value + "   " + name;
        }

        private string NamedValue(string field, string raw)
        {
            int? n = LeadingInt(raw);
            if (!n.HasValue) return raw;
            string name = names.DescribeName(field, selectedGender, n.Value);
            if (string.IsNullOrEmpty(name)) return raw;
            return raw + "  " + name;
        }

        private string StudyText(string levelName, string rankName, string valueName)
        {
            int? level = LeadingInt(DashboardData.Value(mem, ctx, levelName, 0));
            int? rank = rankName == null ? null : LeadingInt(DashboardData.Value(mem, ctx, rankName, 0));
            int? value = LeadingInt(DashboardData.Value(mem, ctx, valueName, 0));
            if (rankName == null)
                return "rank " + (level.HasValue ? level.Value.ToString() : "-") +
                       ", value " + (value.HasValue ? value.Value.ToString() : "-");
            return "Lv " + (level.HasValue ? level.Value.ToString() : "-") +
                   ", rank " + (rank.HasValue ? rank.Value.ToString() : "-") +
                   ", value " + (value.HasValue ? value.Value.ToString() : "-");
        }

        private string TraitSummary()
        {
            int count = 0;
            string first = null;
            foreach (PointerDef d in DashboardData.Personal("Traits & preferences"))
            {
                EvalResult r = DashboardData.Eval(mem, ctx, d, 0);
                if (r != null && r.Bytes != null && r.Bytes.Length >= 1 && r.Bytes[0] != 0)
                {
                    count++;
                    if (first == null) first = d.Name;
                }
            }
            if (count == 0) return "none";
            return count.ToString() + " active" + (first != null ? "  (" + first + ")" : "");
        }

        private string ClubName(GameMemory mem, GameContext ctx)
        {
            EvalResult club = DashboardData.Eval(mem, ctx, "Club", 0);
            if (club == null || club.Bytes == null || club.Bytes.Length < 1) return "-";
            int index = club.Bytes[0];
            string defName = "Club" + (index + 1).ToString() + "Name";
            string name = DashboardData.Value(mem, ctx, defName, 0);
            if (name == null || name == "-" || name == "\"\"") return "Club " + index.ToString();
            return name.Trim('"') + "  [" + index.ToString() + "]";
        }

        private string PersonalityName(int value)
        {
            string name = names.Get("PERSONALITY", value);
            if (string.IsNullOrEmpty(name) && value >= 0 && value < PersonalityNames.Length)
                name = PersonalityNames[value];
            if (!string.IsNullOrEmpty(name))
                return value.ToString("D2") + "  " + name;
            return value.ToString() + "  (custom)";
        }

        private string PersonalityFromValue(string value)
        {
            int? n = LeadingInt(value);
            if (n.HasValue) return PersonalityName(n.Value);
            return value;
        }

        private string GenderName(string value)
        {
            int? n = LeadingInt(value);
            if (n.HasValue)
            {
                string name = names.Get("GENDER", n.Value);
                if (!string.IsNullOrEmpty(name)) return name + "  (" + n.Value.ToString() + ")";
                if (n.Value == 0) return "Male";
                if (n.Value == 1) return "Female";
            }
            return value;
        }

        private static string ResponseText(string value)
        {
            int? n = LeadingInt(value);
            if (!n.HasValue) return value;
            string name;
            if (ResponseNames.TryGetValue(n.Value, out name)) return n.Value.ToString() + "  " + name;
            return value;
        }

        private static string PlayTimeText(string value)
        {
            int? n = LeadingInt(value);
            if (!n.HasValue || n.Value < 0) return value;
            TimeSpan t = TimeSpan.FromSeconds(n.Value);
            return value + "  (" + ((int)t.TotalHours).ToString("D2") + ":" +
                   t.Minutes.ToString("D2") + ":" + t.Seconds.ToString("D2") + ")";
        }

        private static string AsciiDigitText(string value)
        {
            int? n = LeadingInt(value);
            if (n.HasValue && n.Value >= 0x30 && n.Value <= 0x39)
                return ((char)n.Value).ToString() + "  (0x" + n.Value.ToString("X2") + ")";
            return value;
        }

        private static string StripQuotes(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
                return value.Substring(1, value.Length - 2);
            return value;
        }

        private static int? LeadingInt(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            int i = 0;
            if (value[0] == '-') i = 1;
            int start = i;
            while (i < value.Length && value[i] >= '0' && value[i] <= '9') i++;
            if (i == start) return null;
            int n;
            if (int.TryParse(value.Substring(0, i), NumberStyles.Integer, CultureInfo.InvariantCulture, out n))
                return n;
            return null;
        }

        private static string Addr(uint value)
        {
            return value == 0 ? "0x00000000" : "0x" + value.ToString("X8");
        }

        private static string PointerOrNumberText(string value)
        {
            int? n = LeadingInt(value);
            if (n.HasValue && n.Value >= 0x10000)
                return "0x" + ((uint)n.Value).ToString("X8");
            return value;
        }

        private static void SetKV(Dictionary<string, Label> map, string key, string value, bool ok)
        {
            if (map == null) return;
            Label l;
            if (!map.TryGetValue(key, out l)) return;
            l.Text = value;
            l.ForeColor = ok ? Color.Black : Color.DimGray;
        }
    }
}
