using System;
using System.Collections.Generic;

namespace AA2Trainer.WpfPrototype
{
    /// <summary>
    /// Builds UI field descriptions from the Core appearance editors.
    /// </summary>
    public static class AppearanceFields
    {
        public static List<FieldDef> Face()
        {
            List<FieldDef> list = new List<FieldDef>();
            IList<FaceFieldDef> defs = FaceEditor.Fields;
            for (int i = 0; i < defs.Count; i++)
            {
                FaceFieldDef def = defs[i];
                string tip = def.Name + "   (char + 0x" +
                    def.Offset.ToString("X") + ")";
                if (def.Kind == FaceFieldKind.Text)
                {
                    tip += "\nMaximum " + (def.FieldSize - 1).ToString() +
                        " bytes (Shift-JIS).";
                }

                list.Add(new FieldDef
                {
                    Name = def.Name,
                    Label = def.Label,
                    Tip = tip,
                    Kind = FaceKind(def.Kind),
                    Group = FaceGroupTitle(def.Group),
                    Index = i,
                    Minimum = 0,
                    Maximum = def.Kind == FaceFieldKind.Color ||
                        def.Kind == FaceFieldKind.Text ? 0 : 255
                });
            }
            return list;
        }

        public static List<FieldDef> Hair()
        {
            List<FieldDef> list = new List<FieldDef>();
            IList<HairFieldDef> defs = HairEditor.Fields;
            for (int i = 0; i < defs.Count; i++)
            {
                HairFieldDef def = defs[i];
                list.Add(new FieldDef
                {
                    Name = def.Name,
                    Label = def.Label,
                    Tip = def.Name + "   (char + 0x" +
                        def.Offset.ToString("X") + ")",
                    Kind = HairKind(def.Kind),
                    Group = HairGroupTitle(def.Group),
                    Index = i,
                    Minimum = 0,
                    Maximum = def.Kind == HairFieldKind.Color ? 0 : 255
                });
            }
            return list;
        }

        public static List<FieldDef> Outfits(int setIndex)
        {
            List<FieldDef> list = new List<FieldDef>();
            IList<OutfitFieldDef> defs = OutfitEditor.Fields;
            for (int i = 0; i < defs.Count; i++)
            {
                OutfitFieldDef def = defs[i];
                if (def.SetIndex != setIndex) continue;
                string label = def.Label;
                if (def.Kind == OutfitFieldKind.Opacity)
                    label += " opacity";
                else if (def.Kind == OutfitFieldKind.Color)
                    label += " color";

                list.Add(new FieldDef
                {
                    Name = def.Name,
                    Label = label,
                    Tip = def.Name + "   (char + 0x" +
                        def.Offset.ToString("X") + ")",
                    Kind = OutfitKind(def.Kind),
                    Group = OutfitGroupTitle(def.Group),
                    Index = i,
                    Minimum = def.Minimum,
                    Maximum = def.Maximum
                });
            }
            return list;
        }

        private static FieldKind FaceKind(FaceFieldKind kind)
        {
            switch (kind)
            {
                case FaceFieldKind.Choice: return FieldKind.Choice;
                case FaceFieldKind.Color: return FieldKind.Color;
                case FaceFieldKind.Toggle: return FieldKind.Toggle;
                case FaceFieldKind.Text: return FieldKind.Text;
                default: return FieldKind.Number;
            }
        }

        private static FieldKind HairKind(HairFieldKind kind)
        {
            switch (kind)
            {
                case HairFieldKind.Choice: return FieldKind.Choice;
                case HairFieldKind.Toggle: return FieldKind.Toggle;
                case HairFieldKind.Color: return FieldKind.Color;
                default: return FieldKind.Number;
            }
        }

        private static FieldKind OutfitKind(OutfitFieldKind kind)
        {
            switch (kind)
            {
                case OutfitFieldKind.Choice: return FieldKind.Choice;
                case OutfitFieldKind.Toggle: return FieldKind.Toggle;
                case OutfitFieldKind.Color: return FieldKind.Color;
                case OutfitFieldKind.Opacity: return FieldKind.Opacity;
                case OutfitFieldKind.Int32: return FieldKind.Int32;
                default: return FieldKind.Number;
            }
        }

        private static string FaceGroupTitle(FaceGroup group)
        {
            switch (group)
            {
                case FaceGroup.Face: return "Face";
                case FaceGroup.Eyes: return "Eyes";
                case FaceGroup.Iris: return "Iris";
                case FaceGroup.Brow: return "Brow";
                case FaceGroup.Glasses: return "Glasses";
                case FaceGroup.Mole: return "Mole";
                default: return "External textures";
            }
        }

        private static string HairGroupTitle(HairGroup group)
        {
            switch (group)
            {
                case HairGroup.Style: return "Style";
                case HairGroup.Length: return "Length";
                case HairGroup.Flip: return "Flip";
                default: return "Color";
            }
        }

        private static string OutfitGroupTitle(OutfitFieldGroup group)
        {
            switch (group)
            {
                case OutfitFieldGroup.Garment: return "Uniform";
                case OutfitFieldGroup.Top: return "Tops";
                case OutfitFieldGroup.Bottom: return "Bottoms";
                case OutfitFieldGroup.Accessories: return "Shoes & socks";
                default: return "Underwear";
            }
        }
    }
}
