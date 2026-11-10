using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace AA2Trainer.WpfPrototype.Views
{
    public partial class ImportExportView : UserControl
    {
        private readonly NameMap names = new NameMap();
        private readonly ObservableCollection<ImportDiffRow> rows =
            new ObservableCollection<ImportDiffRow>();
        private ClassSaveFile save;
        private CardSnapshot sourceCard;
        private SaveCardEntry selectedEntry;
        private string lastSaveDirectory = "";
        private string lastCardDirectory = "";
        private bool updating;

        public ImportExportView()
        {
            InitializeComponent();
            DiffGrid.ItemsSource = rows;

            string root = NameMap.FindGameRoot();
            if (!string.IsNullOrEmpty(root)) names.Load(root);

            BuildSectionChecks();
            UpdateComparison();
        }

        private void BuildSectionChecks()
        {
            foreach (string section in CardFields.Sections)
                AddSectionCheck(section, section);
            AddSectionCheck("Card Face", "Card Face");
            AddSectionCheck("Roster Image", "Roster Image");
            AddSectionCheck("AAUnlimited Data", "AAUnlimited Data");
        }

        private void AddSectionCheck(string name, string label)
        {
            CheckBox box = new CheckBox();
            box.Content = label;
            box.Tag = name;
            box.IsChecked = true;
            box.Margin = new Thickness(0, 0, 12, 4);
            box.Checked += Section_Changed;
            box.Unchecked += Section_Changed;
            SectionsPanel.Children.Add(box);
        }

        private void Section_Changed(object sender, RoutedEventArgs e)
        {
            if (!updating) UpdateComparison();
        }

        private List<string> SelectedSections()
        {
            List<string> list = new List<string>();
            foreach (object child in SectionsPanel.Children)
            {
                CheckBox box = child as CheckBox;
                if (box != null && box.IsChecked == true)
                    list.Add(box.Tag as string);
            }
            return list;
        }

        private void SelectSave_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Title = "Select an AA2 class save";
            dialog.Filter = "AA2 class saves (*.sav)|*.sav|All files (*.*)|*.*";
            dialog.InitialDirectory = string.IsNullOrEmpty(lastSaveDirectory)
                ? SaveBackup.DefaultDirectory() : lastSaveDirectory;
            if (dialog.ShowDialog() != true) return;
            LoadSave(dialog.FileName);
            lastSaveDirectory = Path.GetDirectoryName(dialog.FileName);
        }

        private void LoadSave(string path)
        {
            try
            {
                save = ClassSaveFile.Load(path);
                SavePathBox.Text = path;
                CharacterCombo.ItemsSource = save.Cards;

                SaveCardEntry initial = save.FindCard(save.PcSeat);
                if (initial == null && save.Cards.Count > 0)
                    initial = save.Cards[0];
                CharacterCombo.SelectedItem = initial;
                selectedEntry = initial;

                UpdateFaces();
                UpdateComparison();
                SetStatus("Save loaded. Select a character and a card.", false);
            }
            catch (Exception ex)
            {
                save = null;
                selectedEntry = null;
                CharacterCombo.ItemsSource = null;
                SavePathBox.Text = path;
                UpdateComparison();
                SetStatus(ex.Message, true);
            }
        }

        private void SelectCard_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Title = "Select an AA2 character card";
            dialog.Filter = "AA2 cards (*.png)|*.png|All files (*.*)|*.*";
            dialog.InitialDirectory = string.IsNullOrEmpty(lastCardDirectory)
                ? (save == null ? "" : Path.GetDirectoryName(save.Path))
                : lastCardDirectory;
            if (dialog.ShowDialog() != true) return;

            try
            {
                sourceCard = CardSnapshot.ParseStandalone(dialog.FileName);
                CardPathBox.Text = dialog.FileName;
                lastCardDirectory = Path.GetDirectoryName(dialog.FileName);
                UpdateFaces();
                UpdateComparison();
            }
            catch (Exception ex)
            {
                sourceCard = null;
                CardPathBox.Text = dialog.FileName;
                UpdateComparison();
                SetStatus(ex.Message, true);
            }
        }

        private void CharacterCombo_SelectionChanged(object sender,
            SelectionChangedEventArgs e)
        {
            if (updating) return;
            selectedEntry = CharacterCombo.SelectedItem as SaveCardEntry;
            UpdateFaces();
            UpdateComparison();
        }

        private void UpdateFaces()
        {
            SaveFaceImage.Source = selectedEntry == null ||
                selectedEntry.Card == null
                ? null
                : CardImageLoader.ToImage(selectedEntry.Card.FacePng, 200);
            CardFaceImage.Source = sourceCard == null
                ? null
                : CardImageLoader.ToImage(sourceCard.FacePng, 200);
        }

        private void UpdateComparison()
        {
            rows.Clear();
            if (save == null || sourceCard == null || selectedEntry == null)
            {
                SetStatus("Select a save file, a character from that save, and a card to import.", false);
                return;
            }

            List<string> sections = SelectedSections();
            HashSet<string> applies =
                new HashSet<string>(sections, StringComparer.Ordinal);
            bool showAll = ShowUnchangedCheck.IsChecked == true;
            CardSnapshot target = selectedEntry.Card;
            int gender = target.Gender;
            int changes = 0;

            foreach (CardFieldDef field in CardFields.All)
            {
                if (!CardFields.MatchesGender(field, gender)) continue;
                bool same = CardFields.Same(target.EditData, sourceCard.EditData, field);
                bool appliesHere = applies.Contains(field.Section);
                if (!same && appliesHere) changes++;
                if (!showAll && (same || !appliesHere)) continue;

                rows.Add(new ImportDiffRow
                {
                    Section = field.Section,
                    Field = field.Label,
                    Current = CardFields.Format(target.EditData, field, names),
                    Incoming = CardFields.Format(sourceCard.EditData, field, names),
                    Changed = !same && appliesHere,
                    AppliesHere = appliesHere
                });
            }

            changes += AddSpecialRow("Images", "Card Face", "Card Face",
                target.FacePng, sourceCard.FacePng, applies);
            changes += AddSpecialRow("Images", "Roster Image", "Roster Image",
                target.RosterPng, sourceCard.RosterPng, applies);
            changes += AddSpecialRow("AAUnlimited Data", "AAU chunk",
                "AAUnlimited Data", target.AauChunk, sourceCard.AauChunk, applies);

            if (sections.Count == 0)
                SetStatus("Select at least one section to see what will change.", false);
            else if (changes == 0)
                SetStatus("No changes in the selected sections.", false);
            else
                SetStatus(changes + (changes == 1 ? " change" : " changes") +
                    " will be applied.", false);
        }

        private int AddSpecialRow(string displaySection, string field,
            string appliesTo, byte[] current, byte[] incoming,
            HashSet<string> applies)
        {
            bool same = BytesEqual(current, incoming);
            bool appliesHere = applies.Contains(appliesTo);
            bool changed = !same && appliesHere;
            if (ShowUnchangedCheck.IsChecked != true && !changed) return 0;

            rows.Add(new ImportDiffRow
            {
                Section = displaySection,
                Field = field,
                Current = ByteText(current),
                Incoming = ByteText(incoming),
                Changed = changed,
                AppliesHere = appliesHere
            });
            return changed ? 1 : 0;
        }

        private static string ByteText(byte[] data)
        {
            return data == null || data.Length == 0
                ? "none" : "present (" + data.Length + " bytes)";
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a == null || b == null) return a == b;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
        }

        private void ApplyChanges_Click(object sender, RoutedEventArgs e)
        {
            if (save == null || sourceCard == null || selectedEntry == null)
            {
                SetStatus("Select a save, a character from it, and a card first.", true);
                return;
            }

            List<string> sections = SelectedSections();
            if (sections.Count == 0)
            {
                SetStatus("Select at least one section to replace.", true);
                return;
            }

            MessageBoxResult answer = MessageBox.Show(
                "Replace the selected sections?\n\nSave: " + save.Path +
                "\nCharacter: " + selectedEntry.Seat.ToString("D2") + " " +
                selectedEntry.Card.Name +
                "\nCard: " + sourceCard.SourcePath +
                "\nSections: " + string.Join(", ", sections.ToArray()) +
                "\n\nA timestamped .bak backup will be created. " +
                "Reload the class in AA2Play afterward.",
                "Replace Character Data", MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes) return;

            SaveCardEntry entry = selectedEntry;
            CardSnapshot target = entry.Card;
            try
            {
                foreach (string section in sections)
                {
                    if (section == "Card Face")
                        target.FacePng = sourceCard.FacePng;
                    else if (section == "Roster Image")
                        target.RosterPng = sourceCard.RosterPng;
                    else if (section == "AAUnlimited Data")
                        target.AauChunk = sourceCard.AauChunk;
                    else
                        CardFields.CopySection(sourceCard.EditData,
                            target.EditData, section);
                }

                target.RefreshIdentity();
                if (target.Gender >= 0 && target.Gender <= 1)
                    entry.Gender = (byte)target.Gender;
                entry.Dirty = true;

                int seat = entry.Seat;
                string backup = save.Save();
                LoadSave(save.Path);
                SelectSeat(seat);
                SetStatus("Saved. Backup: " + backup +
                    "  Reload the class in AA2Play.", false);
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
            }
        }

        private void SelectSeat(int seat)
        {
            if (save == null) return;
            SaveCardEntry entry = save.FindCard(seat);
            if (entry != null)
            {
                CharacterCombo.SelectedItem = entry;
                selectedEntry = entry;
            }
        }

        private void ExportCard_Click(object sender, RoutedEventArgs e)
        {
            if (selectedEntry == null)
            {
                SetStatus("Select a character from a save before exporting.", true);
                return;
            }

            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Title = "Export AA2 character card";
            dialog.Filter = "AA2 cards (*.png)|*.png|All files (*.*)|*.*";
            dialog.DefaultExt = "png";
            dialog.AddExtension = true;
            dialog.OverwritePrompt = true;
            dialog.InitialDirectory = string.IsNullOrEmpty(lastCardDirectory)
                ? (save == null ? "" : Path.GetDirectoryName(save.Path))
                : lastCardDirectory;
            dialog.FileName = DefaultCardName(selectedEntry);
            if (dialog.ShowDialog() != true) return;

            try
            {
                File.WriteAllBytes(dialog.FileName,
                    selectedEntry.Card.BuildCardBytes(null));
                lastCardDirectory = Path.GetDirectoryName(dialog.FileName);
                SetStatus("Exported " + selectedEntry.Seat.ToString("D2") + " " +
                    selectedEntry.Card.Name + " to " + dialog.FileName + ".", false);
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, true);
            }
        }

        private static string DefaultCardName(SaveCardEntry entry)
        {
            string name = string.IsNullOrEmpty(entry.Card.Name)
                ? "" : entry.Card.Name;
            char[] invalid = Path.GetInvalidFileNameChars();
            foreach (char c in invalid) name = name.Replace(c, '_');
            name = name.Trim().TrimEnd('.');
            if (name.Length == 0) name = "card";
            if (name.Length > 80) name = name.Substring(0, 80).TrimEnd();
            return "AA2_" + entry.Seat.ToString("D2") + "_" + name + ".png";
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            SetAllSections(true);
        }

        private void SelectNone_Click(object sender, RoutedEventArgs e)
        {
            SetAllSections(false);
        }

        private void SetAllSections(bool selected)
        {
            updating = true;
            try
            {
                foreach (object child in SectionsPanel.Children)
                {
                    CheckBox box = child as CheckBox;
                    if (box != null) box.IsChecked = selected;
                }
            }
            finally
            {
                updating = false;
            }
            UpdateComparison();
        }

        private void ShowUnchanged_Changed(object sender, RoutedEventArgs e)
        {
            if (!updating) UpdateComparison();
        }

        private void SetStatus(string text, bool error)
        {
            StatusText.Text = text ?? "";
            Brush brush = TryFindResource(error ? "Danger" : "TextMuted") as Brush;
            if (brush != null) StatusText.Foreground = brush;
        }
    }
}
