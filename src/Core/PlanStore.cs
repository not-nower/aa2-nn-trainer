using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

namespace AA2Trainer
{
    /// <summary>
    /// Portable plan storage. The file lives next to the trainer executable and
    /// stores seats + parameters only: no character pointers, so it survives
    /// scene/class reloads and can be moved with the app folder.
    /// </summary>
    public static class PlanStore
    {
        public const string FileName = "AA2TrainerPlans.xml";
        private const int FormatVersion = 1;

        /// <summary>Plans file in the application folder.</summary>
        public static string DefaultPath
        {
            get
            {
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    FileName);
            }
        }

        public static void Save(string path, IEnumerable<PlanQueue> plans)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("A plans file path is required.");

            XmlDocument document = new XmlDocument();
            document.AppendChild(document.CreateXmlDeclaration("1.0", "utf-8", null));

            XmlElement root = document.CreateElement("plans");
            root.SetAttribute("version", FormatVersion.ToString(
                CultureInfo.InvariantCulture));
            root.SetAttribute("saved", DateTime.UtcNow.ToString("o",
                CultureInfo.InvariantCulture));
            document.AppendChild(root);

            if (plans != null)
            {
                foreach (PlanQueue plan in plans)
                {
                    if (plan == null || plan.Steps.Count == 0) continue;

                    XmlElement planNode = document.CreateElement("plan");
                    planNode.SetAttribute("seat", plan.Seat.ToString(
                        CultureInfo.InvariantCulture));
                    planNode.SetAttribute("name", plan.CharacterName ?? "");
                    planNode.SetAttribute("mode", plan.Mode.ToString());
                    planNode.SetAttribute("trigger", plan.Trigger.ToString());
                    planNode.SetAttribute("repeat", plan.Repeat ? "true" : "false");
                    planNode.SetAttribute("shuffle", plan.Shuffle ? "true" : "false");

                    foreach (PlanStep step in plan.Steps)
                    {
                        if (step == null || step.Params == null) continue;

                        XmlElement stepNode = document.CreateElement("step");
                        ActionParams p = step.Params;
                        stepNode.SetAttribute("action", p.ActionId.ToString(
                            CultureInfo.InvariantCulture));
                        stepNode.SetAttribute("movement", p.MovementType.ToString(
                            CultureInfo.InvariantCulture));
                        stepNode.SetAttribute("room", p.RoomTarget.ToString(
                            CultureInfo.InvariantCulture));
                        stepNode.SetAttribute("unknown", p.Unknown.ToString(
                            CultureInfo.InvariantCulture));
                        stepNode.SetAttribute("unknown2", p.Unknown2.ToString(
                            CultureInfo.InvariantCulture));
                        stepNode.SetAttribute("targetSeat", step.TargetSeat.ToString(
                            CultureInfo.InvariantCulture));
                        stepNode.SetAttribute("aboutSeat", step.AboutSeat.ToString(
                            CultureInfo.InvariantCulture));
                        stepNode.SetAttribute("label", step.Label ?? "");
                        planNode.AppendChild(stepNode);
                    }

                    if (planNode.ChildNodes.Count > 0)
                        root.AppendChild(planNode);
                }
            }

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            string temporary = path + ".tmp";
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.Encoding = new UTF8Encoding(false);
            using (XmlWriter writer = XmlWriter.Create(temporary, settings))
            {
                document.Save(writer);
            }

            // Write-then-replace so a crash cannot leave a half-written file.
            if (File.Exists(path))
            {
                try
                {
                    File.Replace(temporary, path, null);
                }
                catch (Exception)
                {
                    File.Copy(temporary, path, true);
                    File.Delete(temporary);
                }
            }
            else
            {
                File.Move(temporary, path);
            }
        }

        public static List<PlanQueue> Load(string path)
        {
            List<PlanQueue> result = new List<PlanQueue>();
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return result;

            XmlDocument document = new XmlDocument();
            document.XmlResolver = null;
            document.Load(path);

            XmlElement root = document.DocumentElement;
            if (root == null || root.Name != "plans")
                throw new InvalidDataException("Not an AA2Trainer plans file.");

            int version;
            if (int.TryParse(root.GetAttribute("version"), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out version) &&
                version > FormatVersion)
            {
                throw new InvalidDataException("Plans file version " + version +
                    " is newer than this trainer.");
            }

            HashSet<int> seenSeats = new HashSet<int>();
            foreach (XmlNode node in root.ChildNodes)
            {
                XmlElement planNode = node as XmlElement;
                if (planNode == null || planNode.Name != "plan") continue;

                int seat = ReadInt(planNode, "seat", -1);
                if (seat < 0 || seat > 24) continue;
                if (!seenSeats.Add(seat)) continue;

                PlanQueue plan = new PlanQueue();
                plan.Seat = seat;
                plan.CharacterName = planNode.GetAttribute("name") ?? "";
                plan.Character = 0;
                plan.Mode = ReadEnum<PlanMode>(planNode, "mode", PlanMode.Normal);
                plan.Trigger = ReadEnum<PlanTrigger>(planNode, "trigger",
                    PlanTrigger.Actor);
                plan.Repeat = ReadBool(planNode, "repeat", false);
                plan.Shuffle = ReadBool(planNode, "shuffle", false);

                foreach (XmlNode stepNodeRaw in planNode.ChildNodes)
                {
                    XmlElement stepNode = stepNodeRaw as XmlElement;
                    if (stepNode == null || stepNode.Name != "step") continue;

                    ActionParams p = new ActionParams();
                    p.ActionId = ReadInt(stepNode, "action", -1);
                    p.MovementType = ReadInt(stepNode, "movement", 0);
                    p.RoomTarget = ReadInt(stepNode, "room", -1);
                    p.Unknown = ReadInt(stepNode, "unknown", -1);
                    p.Unknown2 = ReadInt(stepNode, "unknown2", 1);
                    p.Target1 = 0;
                    p.Target2 = 0;

                    PlanStep step = new PlanStep(p,
                        stepNode.GetAttribute("label") ?? "");
                    step.TargetSeat = ReadInt(stepNode, "targetSeat", -1);
                    step.AboutSeat = ReadInt(stepNode, "aboutSeat", -1);
                    step.TargetCharacter = 0;
                    step.AboutCharacter = 0;
                    plan.Add(step);
                }

                if (plan.Count > 0) result.Add(plan);
            }

            return result;
        }

        private static int ReadInt(XmlElement element, string name, int fallback)
        {
            string text = element.GetAttribute(name);
            int value;
            return int.TryParse(text, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out value) ? value : fallback;
        }

        private static bool ReadBool(XmlElement element, string name,
            bool fallback)
        {
            string text = element.GetAttribute(name);
            bool value;
            return bool.TryParse(text, out value) ? value : fallback;
        }

        private static T ReadEnum<T>(XmlElement element, string name,
            T fallback) where T : struct
        {
            T value;
            return Enum.TryParse<T>(element.GetAttribute(name), true, out value)
                ? value : fallback;
        }
    }
}
