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
        private TabPage BuildCharacterEditorTab()
        {
            TabPage page = new TabPage("Character Editor");
            page.BackColor = Color.FromArgb(248, 249, 251);

            characterEditorTabs = new TabControl();
            characterEditorTabs.Dock = DockStyle.Fill;
            characterEditorTabs.Padding = new Point(12, 6);
            characterEditorTabs.SelectedIndexChanged += delegate
            {
                RefreshCurrentTabValues();
                UpdateLiveState();
            };

            TabPage dataPage = new TabPage("Character Data");
            dataPage.BackColor = Color.FromArgb(248, 249, 251);

            Panel scrollHost = new Panel();
            scrollHost.Dock = DockStyle.Fill;
            scrollHost.AutoScroll = true;
            scrollHost.BackColor = Color.FromArgb(248, 249, 251);

            characterData = new CharacterDataControl();
            characterData.Dock = DockStyle.None;
            characterData.ConfirmWriteAllowed = delegate
            {
                RunHealthCheck();
                return writesAllowed && mem.CanWrite;
            };
            characterData.CharacterSaved += delegate { OnCharacterSaved(); };
            scrollHost.Controls.Add(characterData);

            EventHandler resizeContent = delegate
            {
                const int minimumHeight = 470;
                int width = scrollHost.ClientSize.Width;
                int height = Math.Max(minimumHeight, scrollHost.ClientSize.Height);
                if (scrollHost.ClientSize.Height < minimumHeight)
                    width -= SystemInformation.VerticalScrollBarWidth;
                width = Math.Max(0, width);
                if (characterData.Width == width && characterData.Height == height) return;
                characterData.SuspendLayout();
                try
                {
                    characterData.SetBounds(0, 0, width, height);
                }
                finally
                {
                    characterData.ResumeLayout(true);
                }
            };
            scrollHost.Resize += resizeContent;
            resizeContent(scrollHost, EventArgs.Empty);

            dataPage.Controls.Add(scrollHost);

            characterEditorTabs.TabPages.Add(dataPage);
            characterEditorTabs.TabPages.Add(BuildTraitsTab());
            characterEditorTabs.TabPages.Add(BuildBodyTab());
            characterEditorTabs.TabPages.Add(BuildFaceTab());
            characterEditorTabs.TabPages.Add(BuildHairTab());
            characterEditorTabs.TabPages.Add(BuildOutfitsTab());
            characterEditorTabs.TabPages.Add(BuildImportReplaceTab());

            page.Controls.Add(characterEditorTabs);
            return page;
        }

        private TabPage BuildImportReplaceTab()
        {
            TabPage page = new TabPage("Import/Replace");
            page.BackColor = Color.FromArgb(248, 249, 251);

            ImportReplaceControl importReplace = new ImportReplaceControl();
            importReplace.Dock = DockStyle.Fill;
            page.Controls.Add(importReplace);
            return page;
        }

        private TabPage BuildTraitsTab()
        {
            TabPage page = new TabPage("Traits");
            page.BackColor = Color.FromArgb(248, 249, 251);

            traits = new TraitsControl();
            traits.Dock = DockStyle.Fill;
            traits.ConfirmWriteAllowed = delegate
            {
                RunHealthCheck();
                return writesAllowed && mem.CanWrite;
            };
            traits.CharacterSaved += delegate { OnCharacterSaved(); };
            page.Controls.Add(traits);
            return page;
        }

        private TabPage BuildBodyTab()
        {
            TabPage page = new TabPage("Body");
            page.BackColor = Color.FromArgb(248, 249, 251);

            body = new BodyControl();
            body.Dock = DockStyle.Fill;
            body.ConfirmWriteAllowed = delegate
            {
                RunHealthCheck();
                return writesAllowed && mem.CanWrite;
            };
            body.CharacterSaved += delegate { OnCharacterSaved(); };
            page.Controls.Add(body);
            return page;
        }

        private TabPage BuildFaceTab()
        {
            TabPage page = new TabPage("Face");
            page.BackColor = Color.FromArgb(248, 249, 251);

            face = new FaceControl();
            face.Dock = DockStyle.Fill;
            face.ConfirmWriteAllowed = delegate
            {
                RunHealthCheck();
                return writesAllowed && mem.CanWrite;
            };
            face.CharacterSaved += delegate { OnCharacterSaved(); };
            page.Controls.Add(face);
            return page;
        }

        private TabPage BuildHairTab()
        {
            TabPage page = new TabPage("Hair");
            page.BackColor = Color.FromArgb(248, 249, 251);

            hair = new HairControl();
            hair.Dock = DockStyle.Fill;
            hair.ConfirmWriteAllowed = delegate
            {
                RunHealthCheck();
                return writesAllowed && mem.CanWrite;
            };
            hair.CharacterSaved += delegate { OnCharacterSaved(); };
            page.Controls.Add(hair);
            return page;
        }

        private TabPage BuildOutfitsTab()
        {
            TabPage page = new TabPage("Outfits");
            page.BackColor = Color.FromArgb(248, 249, 251);

            outfits = new OutfitsControl();
            outfits.Dock = DockStyle.Fill;
            outfits.ConfirmWriteAllowed = delegate
            {
                RunHealthCheck();
                return writesAllowed && mem.CanWrite;
            };
            outfits.CharacterSaved += delegate { OnCharacterSaved(); };
            page.Controls.Add(outfits);
            return page;
        }

        private void OnCharacterSaved()
        {
            if (!mem.IsAttached) return;
            if (!TryRefreshContext()) return;

            UpdateSelectedGender();
            PopulateSeats();
            UpdateOverview();
            RefreshCurrentTabValues();
            SetSummary();
        }

        private Dictionary<string, List<CharacterChoice>> BuildBodyChoices()
        {
            Dictionary<string, List<CharacterChoice>> map =
                new Dictionary<string, List<CharacterChoice>>(StringComparer.Ordinal);
            map["BodyType"] = BuildBodyTypeChoices();
            // The name data already lists every body value; -1 means "do not
            // invent placeholder entries" so the dropdowns stay exact.
            map["Heights"] = BuildBodyHeightChoices();
            map["Figure"] = BuildBodyFigureChoices();
            map["BreastShape"] = BuildEnumChoices("BREASTSHAPE", -1, false);
            map["BreastSoftness"] = BuildEnumChoices("BREASTFIRMNESS", -1, false);
            map["PubHairType"] = BuildEnumChoices("PUBICHAIR", -1, false);
            map["NippleType"] = BuildEnumChoices("NIPPLETYPE", -1, false);
            map["NippleColor"] = BuildEnumChoices("NIPPLECOLOR", -1, false);
            map["TanMark"] = BuildEnumChoices("TANMARKS", -1, false);
            map["PussyMosaic"] = BuildEnumChoices("MOSAICCOLOR", -1, false);
            return map;
        }

        private List<CharacterChoice> BuildBodyTypeChoices()
        {
            List<CharacterChoice> list = new List<CharacterChoice>();
            list.Add(new CharacterChoice(0, "Delicate"));
            list.Add(new CharacterChoice(1, "Standard"));
            list.Add(new CharacterChoice(2, "Tall"));
            list.Add(new CharacterChoice(3, "Fat"));
            return list;
        }

        private List<CharacterChoice> BuildBodyHeightChoices()
        {
            List<CharacterChoice> list = new List<CharacterChoice>();
            list.Add(new CharacterChoice(0, "Low"));
            list.Add(new CharacterChoice(1, "Standard"));
            list.Add(new CharacterChoice(2, "High"));
            return list;
        }

        private List<CharacterChoice> BuildBodyFigureChoices()
        {
            List<CharacterChoice> list = new List<CharacterChoice>();
            list.Add(new CharacterChoice(0, "Slim"));
            list.Add(new CharacterChoice(1, "Standard"));
            list.Add(new CharacterChoice(2, "Fat"));
            return list;
        }

        private Dictionary<string, List<CharacterChoice>> BuildFaceChoices()
        {
            Dictionary<string, List<CharacterChoice>> map =
                new Dictionary<string, List<CharacterChoice>>(StringComparer.Ordinal);
            map["FaceType"] = BuildEnumChoices(selectedGender == 0 ? "FACETYPE_M" : "FACETYPE_F",
                -1, false);
            map["IrisShape"] = BuildEnumChoices("IRISSHAPE", -1, false);
            map["IrisType"] = BuildEnumChoices("IRISTYPE", -1, false);
            map["HightlightType"] = BuildEnumChoices("HIGHLIGHTTYPE", -1, false);
            map["BrowShape"] = BuildEnumChoices("EYEBROWSHAPE", -1, false);
            map["Eyelid"] = BuildEnumChoices("EYELIDTYPE", -1, false);
            map["UpperEyelid"] = BuildEnumChoices("EYELIDTYPEU", -1, false);
            map["LowerEyelid"] = BuildEnumChoices("EYELIDTYPEL", -1, false);
            map["Glasses"] = BuildEnumChoices("GLASSESTYPE", -1, false);
            map["LipsticColor"] = BuildEnumChoices("LIPSTICKCOLOR", -1, false);
            return map;
        }

        private Dictionary<string, List<CharacterChoice>> BuildHairChoices()
        {
            // The game keeps no name table for hair styles, so list the raw
            // byte values instead of inventing labels.
            List<CharacterChoice> styles = new List<CharacterChoice>();
            for (int i = 0; i <= 255; i++)
                styles.Add(new CharacterChoice(i, i.ToString()));

            Dictionary<string, List<CharacterChoice>> map =
                new Dictionary<string, List<CharacterChoice>>(StringComparer.Ordinal);
            map["FrontHair"] = styles;
            map["SideHair"] = styles;
            map["BackHair"] = styles;
            map["ExtHair"] = styles;
            return map;
        }

        private string HairChoiceSignature()
        {
            return "hair";
        }

        private Dictionary<string, List<CharacterChoice>> BuildOutfitChoices()
        {
            string clothes = selectedGender == 0 ? "CLOTHES_M" : "CLOTHES_F";
            string socks = selectedGender == 0 ? "SOCKS_M" : "SOCKS_F";
            string shoes = selectedGender == 0 ? "SHOES_M" : "SHOES_F";

            List<CharacterChoice> outfitTypes = BuildEnumChoices(clothes, -1, true);
            List<CharacterChoice> sockTypes = BuildEnumChoices(socks, 5, false);
            List<CharacterChoice> shoeTypes = BuildEnumChoices(shoes, 4, false);

            Dictionary<string, List<CharacterChoice>> map =
                new Dictionary<string, List<CharacterChoice>>(StringComparer.Ordinal);
            string[] prefixes = new string[] { "Outfit", "Sport", "Swim", "Club" };
            foreach (string prefix in prefixes)
            {
                map[prefix + "UType"] = outfitTypes;
                map[prefix + "Socks"] = sockTypes;
                map[prefix + "InShoes"] = shoeTypes;
                map[prefix + "OutShoes"] = shoeTypes;
            }
            return map;
        }

        private string OutfitChoiceSignature()
        {
            return "o" + selectedGender.ToString() + "/" +
                   (names == null ? 0 : names.EntryCount).ToString();
        }

        private string FaceChoiceSignature()
        {
            return "g" + selectedGender.ToString() + "/" +
                   (names == null ? 0 : names.EntryCount).ToString();
        }

        private string BodyChoiceSignature()
        {
            return "n" + (names == null ? 0 : names.EntryCount).ToString();
        }

        private List<CharacterChoice> BuildPersonalityChoices()
        {
            List<CharacterChoice> list = new List<CharacterChoice>();
            HashSet<int> seen = new HashSet<int>();

            // Root XMLs can define modded personalities well above the 0-29
            // range used by the base game (for example 78 Succubus).
            if (names != null)
            {
                foreach (KeyValuePair<int, string> pair in names.Entries("PERSONALITY"))
                {
                    if (pair.Key < 0 || pair.Key > 255) continue;
                    if (string.IsNullOrEmpty(pair.Value)) continue;
                    list.Add(new CharacterChoice(pair.Key,
                        pair.Key.ToString("D2") + "  " + pair.Value));
                    seen.Add(pair.Key);
                }
            }

            for (int i = 0; i < PersonalityNames.Length; i++)
            {
                if (seen.Contains(i)) continue;
                string name = PersonalityNames[i];
                if (string.IsNullOrEmpty(name))
                    name = "Personality " + i.ToString();
                list.Add(new CharacterChoice(i, i.ToString("D2") + "  " + name));
                seen.Add(i);
            }

            list.Sort(delegate(CharacterChoice a, CharacterChoice b)
            {
                return a.Value.CompareTo(b.Value);
            });
            return list;
        }

        private List<CharacterChoice> BuildClubChoices()
        {
            List<CharacterChoice> list = new List<CharacterChoice>();
            for (int i = 0; i < 8; i++)
            {
                string name = DashboardData.Value(mem, ctx,
                    "Club" + (i + 1).ToString() + "Name", 0);
                name = StripQuotes(name == null ? "" : name).Trim();
                if (string.IsNullOrEmpty(name) || name == "-" || name == "BROKEN")
                    name = "Club " + (i + 1).ToString();
                list.Add(new CharacterChoice(i, i.ToString() + "  " + name));
            }
            return list;
        }

        private CharacterChoiceSet BuildCharacterChoices()
        {
            CharacterChoiceSet set = new CharacterChoiceSet();
            set.Personality = BuildPersonalityChoices();
            set.Club = BuildClubChoices();

            List<CharacterChoice> lowHigh = BuildEnumChoices("LOW_HIGH", 4);
            set.Social = lowHigh;
            set.Virtue = lowHigh;
            set.VoicePitch = lowHigh;
            set.FightingStyle = BuildEnumChoices("FIGHTSTYLE", 2);
            set.SexualOrientation = BuildEnumChoices("SEXUALITY", 4);
            set.Intelligence = BuildEnumChoices("LOW_HIGHPLUS", 5);
            set.Grade = BuildGradeChoices();
            return set;
        }

        private List<CharacterChoice> BuildGradeChoices()
        {
            List<CharacterChoice> list = new List<CharacterChoice>();
            list.Add(new CharacterChoice(255, "None"));
            string[] grades = new string[] { "F", "E", "D", "C", "B", "A" };
            for (int i = 0; i < grades.Length; i++)
                list.Add(new CharacterChoice(i, grades[i]));
            return list;
        }

        private List<CharacterChoice> BuildEnumChoices(string key, int fallbackMax)
        {
            return BuildEnumChoices(key, fallbackMax, true);
        }

        private List<CharacterChoice> BuildEnumChoices(string key, int fallbackMax, bool showIndex)
        {
            List<CharacterChoice> list = new List<CharacterChoice>();
            HashSet<int> seen = new HashSet<int>();

            if (names != null)
            {
                foreach (KeyValuePair<int, string> pair in names.Entries(key))
                {
                    if (pair.Key < 0 || pair.Key > 255) continue;
                    if (string.IsNullOrEmpty(pair.Value)) continue;
                    list.Add(new CharacterChoice(pair.Key, showIndex
                        ? pair.Key.ToString("D2") + "  " + pair.Value
                        : pair.Value));
                    seen.Add(pair.Key);
                }
            }

            for (int i = 0; i <= fallbackMax; i++)
            {
                if (seen.Contains(i)) continue;
                list.Add(new CharacterChoice(i, showIndex
                    ? i.ToString("D2") + "  (unnamed)"
                    : "Value " + i.ToString()));
                seen.Add(i);
            }

            list.Sort(delegate(CharacterChoice a, CharacterChoice b)
            {
                return a.Value.CompareTo(b.Value);
            });
            return list;
        }
    }
}
