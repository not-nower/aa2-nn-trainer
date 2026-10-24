using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace AA2Trainer
{
    public sealed class PointerHealth
    {
        public const int ExpectedDefinitionCount = 882;

        public int Total;
        public int Ok;
        public int Empty;
        public int Suspicious;
        public int Broken;
        public int Unreadable;
        public int Optional;
        public long ElapsedMs;
        public DateTime CheckedAt;
        public readonly List<string> Failures = new List<string>();
        public readonly List<string> EmptyFields = new List<string>();
        public readonly List<string> OptionalFields = new List<string>();

        public bool CountOk { get { return Total == ExpectedDefinitionCount; } }
        public bool NoFailures { get { return Broken == 0 && Unreadable == 0; } }
        public bool Clean { get { return CountOk && NoFailures && Suspicious == 0; } }

        public string Summary
        {
            get
            {
                return Ok.ToString() + " OK / " + Empty.ToString() + " empty / " +
                       Suspicious.ToString() + " suspicious / " + Broken.ToString() +
                       " broken / " + Unreadable.ToString() + " unreadable / " +
                       Optional.ToString() + " optional / " + Total.ToString() + " definitions";
            }
        }

        public string ShortStatus
        {
            get
            {
                if (!CountOk)
                    return "definition map " + Total.ToString() + "/" +
                           ExpectedDefinitionCount.ToString();
                if (Broken > 0 || Unreadable > 0)
                    return Broken.ToString() + " broken / " + Unreadable.ToString() + " unreadable";
                if (Suspicious > 0)
                    return Suspicious.ToString() + " suspicious";
                string healthy = "healthy (" + Ok.ToString() + " OK / " + Empty.ToString() + " empty";
                if (Optional > 0)
                    healthy += " / " + Optional.ToString() + " optional";
                return healthy + ")";
            }
        }

        public string FailureText(int maxLines)
        {
            if (Failures.Count == 0) return "";
            StringBuilder sb = new StringBuilder();
            int count = Math.Min(Failures.Count, Math.Max(0, maxLines));
            for (int i = 0; i < count; i++)
            {
                sb.AppendLine(Failures[i]);
            }
            if (Failures.Count > count)
                sb.AppendLine("... and " + (Failures.Count - count).ToString() + " more");
            return sb.ToString();
        }

        public string OptionalText(int maxLines)
        {
            if (OptionalFields.Count == 0) return "";
            StringBuilder sb = new StringBuilder();
            int count = Math.Min(OptionalFields.Count, Math.Max(0, maxLines));
            for (int i = 0; i < count; i++)
                sb.AppendLine(OptionalFields[i]);
            if (OptionalFields.Count > count)
                sb.AppendLine("... and " + (OptionalFields.Count - count).ToString() + " more");
            return sb.ToString();
        }

        public static PointerHealth Run(GameMemory mem, GameContext ctx)
        {
            PointerHealth h = new PointerHealth();
            h.CheckedAt = DateTime.Now;
            Stopwatch watch = Stopwatch.StartNew();

            foreach (PointerDef def in Definitions.All)
            {
                h.Total++;
                try
                {
                    EvalResult r = Evaluator.Evaluate(mem, ctx, def, 0);
                    if (def.Optional)
                    {
                        h.Optional++;
                        h.OptionalFields.Add(def.Name + "  [" + r.Status + "]");
                        continue;
                    }
                    switch (r.Status)
                    {
                        case "OK":
                            h.Ok++;
                            break;
                        case "EMPTY":
                            h.Empty++;
                            h.EmptyFields.Add(def.Name);
                            break;
                        case "SUSPICIOUS":
                            h.Suspicious++;
                            h.Failures.Add(def.Name + "  [" + r.Status + "]  " + r.Value);
                            break;
                        case "UNREADABLE":
                            h.Unreadable++;
                            h.Failures.Add(def.Name + "  [" + r.Status + "]");
                            break;
                        default:
                            h.Broken++;
                            h.Failures.Add(def.Name + "  [" + r.Status + "]");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    h.Broken++;
                    h.Failures.Add(def.Name + "  [exception]  " + ex.Message);
                }
            }

            watch.Stop();
            h.ElapsedMs = watch.ElapsedMilliseconds;
            return h;
        }
    }
}
