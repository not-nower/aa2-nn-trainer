using System;
using System.Globalization;
using System.Text;

namespace AA2Trainer
{
    public sealed class EvalResult
    {
        public PointerDef Def;
        public uint Address;
        public bool HasAddress;
        public string Value = "";
        public string Status = "BROKEN";
        public string Trace = "";
        public byte[] Bytes;
        public uint? PointerValue;
        public bool IsPointerLike;
    }

    public static class Evaluator
    {
        public static EvalResult Evaluate(GameMemory mem, GameContext ctx, PointerDef def, int index)
        {
            EvalResult r = new EvalResult();
            r.Def = def;
            r.IsPointerLike = IsPointerName(def.Name);
            StringBuilder tr = new StringBuilder();
            tr.AppendLine("Definition: " + def.Raw);
            tr.AppendLine("Context:    " + def.Context + "   Field: +0x" + def.FieldOffset.ToString("X") +
                          "   Chain: " + ChainText(def.Chain) + "   Stride: 0x" + def.Stride.ToString("X"));

            uint address = 0;
            bool ok = true;

            try
            {
                if (def.Context == "charaddress")
                {
                    if (ctx.SelectedChar == 0)
                    {
                        ok = false;
                        tr.AppendLine("selected character pointer is null");
                    }
                    else
                    {
                        address = ctx.SelectedChar;
                        tr.AppendLine("special: selected character base = 0x" + address.ToString("X8"));
                    }
                }
                else if (def.Context == "screen")
                {
                    address = ctx.ModuleBase + (uint)def.BaseOffset + (uint)def.FieldOffset;
                    tr.AppendLine("module 0x" + ctx.ModuleBase.ToString("X8") + " + 0x" +
                                  def.BaseOffset.ToString("X") + " + 0x" + def.FieldOffset.ToString("X") +
                                  " = 0x" + address.ToString("X8"));
                }
                else if (def.Context == "selected")
                {
                    address = ctx.GlobalRoot + (uint)def.FieldOffset;
                    tr.AppendLine("global S 0x" + ctx.GlobalRoot.ToString("X8") + " + 0x" +
                                  def.FieldOffset.ToString("X") + " = 0x" + address.ToString("X8"));
                }
                else
                {
                    uint root = 0;
                    string rootName = "";
                    if (def.Context == "character")
                    {
                        root = ctx.SelectedChar;
                        rootName = "character (" + ctx.SelectedName + ")";
                    }
                    else if (def.Context == "global")
                    {
                        root = ctx.GlobalRoot;
                        rootName = "global S";
                    }
                    else if (def.Context == "chartarget")
                    {
                        uint? targetRoot = mem.ReadU32(ctx.ModuleBase + (uint)def.BaseOffset);
                        if (targetRoot.HasValue) root = targetRoot.Value;
                        rootName = "CharTarget root";
                    }
                    else
                    {
                        ok = false;
                        tr.AppendLine("unknown context: " + def.Context);
                    }

                    if (ok)
                    {
                        if (root == 0)
                        {
                            ok = false;
                            tr.AppendLine(rootName + " = null");
                        }
                        else
                        {
                            tr.AppendLine(rootName + " = 0x" + root.ToString("X8"));
                            address = root;
                            foreach (int parent in def.Chain)
                            {
                                uint slot = address + (uint)parent;
                                uint? value = mem.ReadU32(slot);
                                if (!value.HasValue)
                                {
                                    tr.AppendLine("  read32(0x" + slot.ToString("X8") + ") = <unreadable>  [+0x" +
                                                  parent.ToString("X") + "]");
                                    ok = false;
                                    break;
                                }
                                if (value.Value == 0)
                                {
                                    tr.AppendLine("  read32(0x" + slot.ToString("X8") + ") = 0x00000000  [+0x" +
                                                  parent.ToString("X") + "]  NULL POINTER");
                                    ok = false;
                                    break;
                                }
                                tr.AppendLine("  read32(0x" + slot.ToString("X8") + ") = 0x" +
                                              value.Value.ToString("X8") + "  [+0x" + parent.ToString("X") + "]");
                                address = value.Value;
                            }
                        }
                    }

                    if (ok)
                    {
                        uint final = address + (uint)def.FieldOffset;
                        if (def.Stride > 1)
                            final += (uint)(def.Stride * index);
                        address = final;
                        tr.AppendLine("  + field 0x" + def.FieldOffset.ToString("X") +
                                      (def.Stride > 1 ? " + stride 0x" + def.Stride.ToString("X") +
                                                        " * " + index.ToString() : "") +
                                      " = 0x" + address.ToString("X8"));
                    }
                }

                if (!ok)
                {
                    r.Status = "BROKEN";
                    r.Trace = tr.ToString();
                    return r;
                }

                // CharTarget's ^4 token is a real extra dereference, unlike the
                // final ^4-006C marker on character fields.
                if (def.Name == "CharTarget")
                {
                    uint? target = mem.ReadU32(address);
                    tr.AppendLine("  ^4 read32(0x" + address.ToString("X8") + ") = " +
                                  (target.HasValue ? "0x" + target.Value.ToString("X8") : "<unreadable>"));
                    if (!target.HasValue || target.Value == 0)
                    {
                        r.Status = "BROKEN";
                        r.Trace = tr.ToString();
                        return r;
                    }
                    address = target.Value;
                }

                r.Address = address;
                r.HasAddress = true;

                // These two definitions return an address/pointer value directly;
                // there is no field at the computed address to read.
                if (def.Name == "CharAddress" || def.Name == "CharTarget")
                {
                    r.Value = "0x" + address.ToString("X8");
                    if (address == 0)
                        r.Status = "EMPTY";
                    else if (!mem.IsPlausiblePointer(address))
                        r.Status = "SUSPICIOUS";
                    else
                        r.Status = "OK";
                    tr.AppendLine("value: " + r.Value + "  (address result, no final dereference)");
                    r.Trace = tr.ToString();
                    return r;
                }

                int readSize = def.Type == 7 ? 260 : 4;
                r.Bytes = mem.ReadBytes(address, readSize);
                if (r.Bytes == null)
                {
                    r.Status = "UNREADABLE";
                    tr.AppendLine("final read at 0x" + address.ToString("X8") + " failed");
                    r.Trace = tr.ToString();
                    return r;
                }

                Decode(mem, r, tr);
                tr.AppendLine("value: " + r.Value);
                r.Trace = tr.ToString();
                return r;
            }
            catch (Exception ex)
            {
                r.Status = "BROKEN";
                tr.AppendLine("error: " + ex.Message);
                r.Trace = tr.ToString();
                return r;
            }
        }

        private static void Decode(GameMemory mem, EvalResult r, StringBuilder tr)
        {
            PointerDef def = r.Def;
            byte[] b = r.Bytes;
            if (def.Type == 7)
            {
                string text;
                if (def.Unicode)
                    text = mem.ReadUnicodeString(r.Address, 260);
                else
                    text = mem.ReadAnsiString(r.Address, 260);
                if (text == null)
                {
                    r.Status = "UNREADABLE";
                    r.Value = "<unreadable>";
                    return;
                }
                text = text.Replace("\r", " ").Replace("\n", " ");
                r.Value = "\"" + Truncate(text, 100) + "\"";
                if (text.Length == 0)
                    r.Status = "EMPTY";
                else if (ContainsControl(text))
                    r.Status = "SUSPICIOUS";
                else
                    r.Status = "OK";
                return;
            }

            if (def.Type == 3)
            {
                byte v = b[0];
                r.Value = v.ToString() + " (0x" + v.ToString("X2") + ")";
                r.Status = "OK";
                return;
            }

            uint raw = BitConverter.ToUInt32(b, 0);
            if (def.Type == 5)
            {
                float f = BitConverter.ToSingle(b, 0);
                r.PointerValue = null;
                r.Value = f.ToString("0.###", CultureInfo.InvariantCulture) + "f  [0x" + raw.ToString("X8") + "]";
                if (float.IsNaN(f) || float.IsInfinity(f) || Math.Abs(f) > 1000000f)
                    r.Status = "SUSPICIOUS";
                else
                    r.Status = "OK";
                return;
            }

            int signed = unchecked((int)raw);
            r.PointerValue = raw;

            if (r.IsPointerLike)
            {
                r.Value = "0x" + raw.ToString("X8");
                if (raw == 0 || raw == 0xFFFFFFFF)
                    r.Status = "EMPTY";
                else if (!mem.IsPlausiblePointer(raw))
                    r.Status = "SUSPICIOUS";
                else
                    r.Status = "OK";
                return;
            }

            if (def.Name == "ScreenType")
            {
                r.Value = signed.ToString() + " (0x" + raw.ToString("X8") + ")";
                r.Status = (signed >= 0 && signed < 40) ? "OK" : "SUSPICIOUS";
                return;
            }

            r.Value = signed.ToString() + " (0x" + raw.ToString("X8") + ")";
            r.Status = "OK";
        }

        private static bool ContainsControl(string s)
        {
            foreach (char c in s)
                if (c < 0x20 && c != '\t') return true;
            return false;
        }

        private static string Truncate(string s, int n)
        {
            if (s == null) return "";
            if (s.Length <= n) return s;
            return s.Substring(0, n) + "...";
        }

        public static bool IsPointerName(string name)
        {
            if (name == "SelectedChar" || name == "SelectedTarget" ||
                name == "CharAddress" || name == "NPCAddress" ||
                name == "CharTarget")
                return true;
            if (name.EndsWith("Addr", StringComparison.Ordinal)) return true;
            if (name.Contains("Address")) return true;
            return false;
        }

        public static string ChainText(int[] chain)
        {
            if (chain == null || chain.Length == 0) return "(none)";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < chain.Length; i++)
            {
                if (i > 0) sb.Append(" -> ");
                sb.Append("0x");
                sb.Append(chain[i].ToString("X"));
            }
            return sb.ToString();
        }

        public static string TypeName(int type)
        {
            switch (type)
            {
                case 3: return "byte";
                case 4: return "int32";
                case 5: return "float";
                case 7: return "string";
                case 8: return "int32";
                default: return "type " + type.ToString();
            }
        }
    }
}
