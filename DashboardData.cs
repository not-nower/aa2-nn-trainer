using System;
using System.Collections.Generic;
using System.Drawing;

namespace AA2Trainer
{
    public static class DashboardData
    {
        public static PointerDef Find(string name)
        {
            foreach (PointerDef d in Definitions.All)
                if (d.Name == name) return d;
            return null;
        }

        public static List<PointerDef> ByCategory(string category)
        {
            List<PointerDef> list = new List<PointerDef>();
            foreach (PointerDef d in Definitions.All)
                if (d.Category == category) list.Add(d);
            return list;
        }

        public static EvalResult Eval(GameMemory mem, GameContext ctx, PointerDef def, int index)
        {
            if (def == null) return null;
            return Evaluator.Evaluate(mem, ctx, def, index);
        }

        public static EvalResult Eval(GameMemory mem, GameContext ctx, string name, int index)
        {
            return Eval(mem, ctx, Find(name), index);
        }

        public static string Value(GameMemory mem, GameContext ctx, string name, int index)
        {
            EvalResult r = Eval(mem, ctx, name, index);
            if (r == null) return "-";
            return r.Value;
        }

        public static uint? U32(EvalResult r)
        {
            if (r == null || r.Bytes == null || r.Bytes.Length < 4) return null;
            return BitConverter.ToUInt32(r.Bytes, 0);
        }

        public static Color StatusColor(string status)
        {
            switch (status)
            {
                case "OK": return Color.FromArgb(242, 255, 242);
                case "EMPTY": return Color.FromArgb(244, 244, 244);
                case "SUSPICIOUS": return Color.FromArgb(255, 252, 220);
                default: return Color.FromArgb(255, 232, 232);
            }
        }

        public static readonly string[] PersonalGroups = new string[]
        {
            "Identity", "Traits & preferences", "Body", "Face", "Hair",
            "Outfits", "Study & club", "Media", "Other", "All personal"
        };

        public static readonly string[] RelationGroups = new string[]
        {
            "Relationship", "Moods", "Behavior levels", "Feeling levels",
            "Promises", "All relation data"
        };

        public static List<PointerDef> Personal(string group)
        {
            List<PointerDef> all = ByCategory("Character");
            if (group == "All personal") return all;
            List<PointerDef> result = new List<PointerDef>();
            foreach (PointerDef d in all)
            {
                if (PersonalGroupOf(d) == group) result.Add(d);
            }
            return result;
        }

        public static string PersonalGroupOf(PointerDef d)
        {
            string n = d.Name;
            if (n == "Gender" || n == "FirstName" || n == "LastName" || n == "Profile" ||
                n == "Personality" || n == "Club" || n == "Social" || n == "FightingStyle" ||
                n == "Virtue" || n == "Oriented" || n == "SexExp" || n == "AnalExp" ||
                n == "VoicePitch")
                return "Identity";

            if (IsTraitOrPreference(n) || n.StartsWith("Risk"))
                return "Traits & preferences";

            if (n == "BodyType" || n == "Heights" || n == "Figure" || n == "HeadSize" ||
                n == "HeadLength" || n == "Waist" || n.StartsWith("Breast") ||
                n == "SkinColor" || n.StartsWith("PubHair") || n.StartsWith("Nipple") ||
                n.StartsWith("Tan") || n == "PussyMosaic")
                return "Body";

            if (n == "FaceType" || n.StartsWith("Eye") || n.StartsWith("Iris") ||
                n.StartsWith("Hightlight") || n.StartsWith("ExtIris") || n.StartsWith("ExtH") ||
                n == "LeftEyeColor" || n == "RightEyeColor" || n.StartsWith("Brow") ||
                n.StartsWith("Mole") || n == "Eyelid" || n == "UpperEyelid" ||
                n == "LowerEyelid" || n == "Glasses" || n.StartsWith("Lipstic") ||
                n == "GlassesColor")
                return "Face";

            if (n == "FrontHair" || n == "SideHair" || n == "BackHair" || n == "ExtHair" ||
                n.EndsWith("HairL") || n.StartsWith("Flip"))
                return "Hair";

            if (n.StartsWith("Outfit") || n.StartsWith("Sport") || n.StartsWith("Swim") ||
                (n.StartsWith("Club") && n != "Club" && n != "ClubRank" && n != "ClubValue" && n != "ClubGrade"))
                return "Outfits";

            if (n.StartsWith("Int") || n.StartsWith("Str") || n == "ClubRank" ||
                n == "ClubValue" || n == "ClubGrade" || n == "Victory" ||
                n == "ClassSkip" || n == "WinningOver" || n == "Partner" ||
                n == "Rejected" || n == "SexIntercouse" || n == "SexPartner" ||
                n == "FirstSex" || n == "FirstAnal" || n == "LastSex")
                return "Study & club";

            if (n.StartsWith("Thumb") || n.StartsWith("Potrait"))
                return "Media";

            return "Other";
        }

        private static bool IsTraitOrPreference(string n)
        {
            string[] names = new string[]
            {
                "Easygoing","Affable","BadwithGuys","BadwithGirls","Charming","Tsundare",
                "Chivalrous","Trendy","Obedient","Positive","Shy","Jealous","Melancholy",
                "Preverted","Serious","Calm","Impulsive","Absentmind","Violent","Passive",
                "Maddlesome","ClassInco","Chatty","Hungry","Romantic","Singlemind",
                "Indencisive","Competitive","Scheming","Diligent","Wild","Masochist",
                "Sweaty","Evil","Deaf","Exploitable","ASexual","Lucky","RainbowCard",
                "Kissing","BreastCares","VaginaCares","PenisCares","Cunninglus","Fellation",
                "DoggyStyle","Fendom","Anal","NoCondom","Swallow","Creampies","Bukake"
            };
            foreach (string x in names)
                if (n == x) return true;
            return false;
        }

        public static List<PointerDef> Relations(string group)
        {
            List<PointerDef> result = new List<PointerDef>();
            foreach (PointerDef d in Definitions.All)
            {
                bool include = false;
                if (group == "Relationship")
                    include = IsRelationshipField(d.Name);
                else if (group == "Moods")
                    include = d.Category == "Mood";
                else if (group == "Behavior levels")
                    include = d.Name.StartsWith("Level") && d.Stride == 0x58;
                else if (group == "Feeling levels")
                    include = d.Name.StartsWith("Level") && d.Stride == 0x5C;
                else if (group == "Promises")
                    include = d.Category == "Promise";
                else if (group == "All relation data")
                    include = d.Category == "Relation" || d.Category == "Mood" || d.Category == "Promise";
                if (include) result.Add(d);
            }
            return result;
        }

        private static bool IsRelationshipField(string n)
        {
            return n == "LoveMax" || n == "LikeMax" || n == "DislikeMax" || n == "HateMax" ||
                   n == "LovePoin" || n == "LikePoin" || n == "DislikePoin" || n == "HatePoin" ||
                   n == "compatible";
        }

        public static List<PointerDef> Activity()
        {
            List<PointerDef> result = ByCategory("Activity");
            string[] extra = new string[]
            {
                "NPCAddress", "NPCTarget", "NPCTargetOth", "CharAddress", "CharTarget"
            };
            foreach (string name in extra)
            {
                PointerDef d = Find(name);
                if (d != null) result.Add(d);
            }
            return result;
        }

        public static List<PointerDef> ClassInfo()
        {
            return ByCategory("Class");
        }

        public static List<PointerDef> Config()
        {
            return ByCategory("Config");
        }

        public static List<PointerDef> Pointers()
        {
            List<PointerDef> all = new List<PointerDef>();
            foreach (PointerDef d in Definitions.All) all.Add(d);
            return all;
        }

        public static string ChainText(PointerDef d)
        {
            return Evaluator.ChainText(d.Chain);
        }

        public static string TypeText(PointerDef d)
        {
            return Evaluator.TypeName(d.Type);
        }
    }
}
