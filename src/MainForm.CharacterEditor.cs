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
            characterEditorTabs.SelectedIndexChanged += delegate { RefreshCurrentTabValues(); };

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

            page.Controls.Add(characterEditorTabs);
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

        private void OnCharacterSaved()
        {
            if (!mem.IsAttached) return;
            if (!TryRefreshContext()) return;

            UpdateSelectedGender();
            PopulateSeats();
            UpdateOverview();
            RefreshCurrentTabValues();
            SetSummary();
            RefreshSaveViewer();
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
            List<CharacterChoice> list = new List<CharacterChoice>();
            HashSet<int> seen = new HashSet<int>();

            if (names != null)
            {
                foreach (KeyValuePair<int, string> pair in names.Entries(key))
                {
                    if (pair.Key < 0 || pair.Key > 255) continue;
                    if (string.IsNullOrEmpty(pair.Value)) continue;
                    list.Add(new CharacterChoice(pair.Key,
                        pair.Key.ToString("D2") + "  " + pair.Value));
                    seen.Add(pair.Key);
                }
            }

            for (int i = 0; i <= fallbackMax; i++)
            {
                if (seen.Contains(i)) continue;
                list.Add(new CharacterChoice(i, i.ToString("D2") + "  (unnamed)"));
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
