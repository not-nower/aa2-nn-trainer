using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace AA2Trainer.WpfPrototype
{
    public sealed class MainViewModel : ObservableObject
    {
        private readonly GameSessionService session;
        private readonly List<CardEntry> cardIndex = new List<CardEntry>();
        private readonly Dictionary<string, CardImages> loadedCards =
            new Dictionary<string, CardImages>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> failedCards =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool cardLoadRunning;
        private bool disposed;

        private SeatItem selectedSeat;
        private CharacterDetail selectedCharacter;
        private bool railCollapsed;
        private bool applyingSnapshot;
        private bool isAttached;
        private string attachState = "Not attached";
        private string gameTime = "Game time: -";
        private string pointerHealth = "Pointers: not checked";
        private string namesState = "Names: not loaded";
        private string updatedText = "Updated -";
        private string writeGate = "Write gate: read-only";
        private string cardStatus = "Cards: not loaded";
        private string rosterSignature = "";
        private string rosterFilter = "";
        private int seatFilterIndex;

        public MainViewModel()
        {
            Seats = new ObservableCollection<SeatItem>();
            PlanSteps = new ObservableCollection<PlanStepItem>();
            LiveLines = new ObservableCollection<LogLine>();
            PlannerLines = new ObservableCollection<LogLine>();
            DebugFields = new ObservableCollection<DebugFieldItem>();

            SeedSeats();
            selectedSeat = Seats[0];
            SeedPlan();
            SeedLogs();
            SeedDebug();

            Dispatcher dispatcher = Application.Current != null
                ? Application.Current.Dispatcher
                : Dispatcher.CurrentDispatcher;
            session = new GameSessionService(dispatcher);
            session.SnapshotReady += ApplySnapshot;
            LoadCardsAsync();
        }

        public ObservableCollection<SeatItem> Seats { get; private set; }
        public ObservableCollection<PlanStepItem> PlanSteps { get; private set; }
        public ObservableCollection<LogLine> LiveLines { get; private set; }
        public ObservableCollection<LogLine> PlannerLines { get; private set; }
        public ObservableCollection<DebugFieldItem> DebugFields { get; private set; }

        public SeatItem SelectedSeat
        {
            get { return selectedSeat; }
            set
            {
                if (!Set(ref selectedSeat, value)) return;
                if (!applyingSnapshot && isAttached && value != null &&
                    value.SeatIndex >= 0)
                {
                    session.RequestSeat(value.SeatIndex);
                }
            }
        }

        public bool RailCollapsed
        {
            get { return railCollapsed; }
            set { Set(ref railCollapsed, value); }
        }

        public CharacterDetail SelectedCharacter
        {
            get { return selectedCharacter; }
            private set { Set(ref selectedCharacter, value); }
        }

        public string RosterFilter
        {
            get { return rosterFilter; }
            set
            {
                if (Set(ref rosterFilter, value)) ApplyRosterFilter();
            }
        }

        public int SeatFilterIndex
        {
            get { return seatFilterIndex; }
            set
            {
                if (Set(ref seatFilterIndex, value)) ApplyRosterFilter();
            }
        }

        public bool IsAttached
        {
            get { return isAttached; }
            private set
            {
                if (!Set(ref isAttached, value)) return;
                OnPropertyChanged(nameof(AttachButtonText));
            }
        }

        public string AttachState
        {
            get { return attachState; }
            private set { Set(ref attachState, value); }
        }

        public string AttachButtonText
        {
            get { return isAttached ? "Detach" : "Attach"; }
        }

        public string GameTime
        {
            get { return gameTime; }
            private set { Set(ref gameTime, value); }
        }

        public string PointerHealth
        {
            get { return pointerHealth; }
            private set { Set(ref pointerHealth, value); }
        }

        public string NamesState
        {
            get { return namesState; }
            private set { Set(ref namesState, value); }
        }

        public string UpdatedText
        {
            get { return updatedText; }
            private set { Set(ref updatedText, value); }
        }

        public string WriteGate
        {
            get { return writeGate; }
            private set { Set(ref writeGate, value); }
        }

        public string CardStatus
        {
            get { return cardStatus; }
            private set { Set(ref cardStatus, value); }
        }

        public string HookState { get { return "Hook: not installed"; } }
        public string PlanStatus { get { return "Plan: not running  |  no steps"; } }

        public void Refresh()
        {
            if (session != null) session.RequestRefresh();
        }

        public void ToggleAttach()
        {
            if (session == null) return;
            if (session.IsAttached)
                session.RequestDetach();
            else
                session.RequestAttach();
        }

        public void Dispose()
        {
            disposed = true;
            if (session == null) return;
            session.SnapshotReady -= ApplySnapshot;
            session.Dispose();
        }

        private void ApplySnapshot(SessionSnapshot snapshot)
        {
            if (snapshot == null) return;

            applyingSnapshot = true;
            try
            {
                IsAttached = snapshot.IsAttached;
                AttachState = snapshot.AttachState;
                GameTime = snapshot.GameTime;
                PointerHealth = snapshot.PointerHealth;
                NamesState = snapshot.NamesState;
                WriteGate = snapshot.WriteGate;
                UpdatedText = snapshot.UpdatedText;
                SelectedCharacter = snapshot.Character;

                for (int i = 0; i < Seats.Count; i++)
                {
                    if (i >= snapshot.Seats.Count) break;
                    SeatSnapshot seatSnapshot = snapshot.Seats[i];
                    SeatItem seat = Seats[i];
                    if (seatSnapshot == null) continue;

                    seat.ApplyLive(
                        seatSnapshot.Name,
                        seatSnapshot.FamilyName,
                        seatSnapshot.GivenName,
                        seatSnapshot.Room,
                        seatSnapshot.Action,
                        seatSnapshot.Status,
                        seatSnapshot.Relationship,
                        seatSnapshot.HeaderRelationship,
                        seatSnapshot.IsPlayer);
                }

                ApplyRosterFilter();

                string signature = BuildRosterSignature();
                if (!string.Equals(signature, rosterSignature,
                    StringComparison.Ordinal))
                {
                    rosterSignature = signature;
                    AssignCards();
                }

                if (snapshot.SelectedSeat >= 0 &&
                    snapshot.SelectedSeat < Seats.Count)
                {
                    SelectedSeat = Seats[snapshot.SelectedSeat];
                }
            }
            finally
            {
                applyingSnapshot = false;
            }
        }

        private void ApplyRosterFilter()
        {
            string filter = (rosterFilter ?? "").Trim();
            bool hasFilter = filter.Length > 0 || seatFilterIndex > 0;

            foreach (SeatItem seat in Seats)
            {
                bool typeMatch = seatFilterIndex == 1
                    ? seat.SeatIndex != 24
                    : seatFilterIndex == 2
                        ? seat.SeatIndex == 24
                        : true;
                bool textMatch = filter.Length == 0 ||
                    MatchesFilter(seat, filter);
                seat.IsFilteredOut = hasFilter && !(typeMatch && textMatch);
            }
        }

        private static bool MatchesFilter(SeatItem seat, string filter)
        {
            return Contains(seat.Name, filter) ||
                   Contains(seat.Room, filter) ||
                   Contains(seat.Action, filter) ||
                   Contains(seat.Status, filter) ||
                   Contains(seat.Relationship, filter) ||
                   Contains(seat.Seat, filter);
        }

        private static bool Contains(string value, string filter)
        {
            return !string.IsNullOrEmpty(value) &&
                value.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private string BuildRosterSignature()
        {
            StringBuilder builder = new StringBuilder(25 * 24);
            foreach (SeatItem seat in Seats)
            {
                builder.Append(seat.SeatIndex);
                builder.Append(':');
                builder.Append(seat.Name);
                builder.Append('|');
            }
            return builder.ToString();
        }

        private async void LoadCardsAsync()
        {
            string[] roots = FindCardRoots();
            if (roots.Length == 0)
            {
                CardStatus = "Cards: none";
                return;
            }

            List<string> files = new List<string>();
            foreach (string root in roots)
            {
                try
                {
                    string[] found = Directory.GetFiles(
                        root, "*.png", SearchOption.TopDirectoryOnly);
                    Array.Sort(found, StringComparer.OrdinalIgnoreCase);
                    files.AddRange(found);
                }
                catch { }
            }

            if (files.Count == 0)
            {
                CardStatus = "Cards: none";
                return;
            }

            CardStatus = "Cards: loading...";
            List<CardEntry> entries;
            try
            {
                entries = await Task.Run(delegate
                {
                    List<CardEntry> result = new List<CardEntry>();
                    HashSet<string> seen = new HashSet<string>(
                        StringComparer.OrdinalIgnoreCase);
                    foreach (string file in files)
                    {
                        CardEntry entry = CardImageLoader.ReadEntry(file);
                        if (entry == null) continue;
                        if (!seen.Add(entry.Key)) continue;
                        result.Add(entry);
                    }
                    return result;
                });
            }
            catch
            {
                CardStatus = "Cards: index failed";
                return;
            }

            if (disposed || Application.Current == null) return;
            Application.Current.Dispatcher.Invoke(delegate
            {
                if (disposed) return;
                cardIndex.Clear();
                cardIndex.AddRange(entries);
                loadedCards.Clear();
                failedCards.Clear();
                if (cardIndex.Count == 0)
                {
                    CardStatus = "Cards: none";
                    return;
                }
                AssignCards();
            });
        }

        /// <summary>
        /// Matches cards to live names exactly. Card names are never applied to
        /// seats and are never translated; unmatched seats keep the initial
        /// tile. Images are decoded only for matched cards.
        /// </summary>
        private void AssignCards()
        {
            if (disposed || cardIndex.Count == 0) return;

            HashSet<CardEntry> used = new HashSet<CardEntry>();
            List<CardEntry> pending = new List<CardEntry>();
            int shown = 0;

            foreach (SeatItem seat in Seats)
            {
                if (!seat.HasName)
                {
                    ClearCardArt(seat);
                    continue;
                }

                CardEntry match = cardIndex.FirstOrDefault(
                    card => !used.Contains(card) &&
                            !failedCards.Contains(card.Path) &&
                            NamesMatch(seat, card));
                if (match == null)
                {
                    ClearCardArt(seat);
                    continue;
                }

                used.Add(match);

                CardImages images;
                if (loadedCards.TryGetValue(match.Path, out images))
                {
                    AssignImages(seat, images);
                    shown++;
                }
                else
                {
                    ClearCardArt(seat);
                    pending.Add(match);
                }
            }

            if (pending.Count > 0) LoadCardImages(pending);

            CardStatus = "Cards: " + shown + " shown";
        }

        private void LoadCardImages(List<CardEntry> entries)
        {
            if (cardLoadRunning || entries.Count == 0) return;
            cardLoadRunning = true;

            List<CardEntry> toLoad = new List<CardEntry>(entries);
            Task.Run(delegate
            {
                List<KeyValuePair<CardEntry, CardImages>> loaded =
                    new List<KeyValuePair<CardEntry, CardImages>>();
                List<string> failed = new List<string>();

                foreach (CardEntry entry in toLoad)
                {
                    CardImages images = CardImageLoader.Load(entry.Path);
                    if (images == null)
                        failed.Add(entry.Path);
                    else
                        loaded.Add(new KeyValuePair<CardEntry, CardImages>(
                            entry, images));
                }

                if (disposed || Application.Current == null) return;
                Application.Current.Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (disposed) return;
                    foreach (KeyValuePair<CardEntry, CardImages> pair in loaded)
                        loadedCards[pair.Key.Path] = pair.Value;
                    foreach (string path in failed)
                        failedCards.Add(path);
                    cardLoadRunning = false;
                    AssignCards();
                }));
            });
        }

        private static void ClearCardArt(SeatItem seat)
        {
            seat.RosterImage = null;
            seat.CardFaceImage = null;
        }

        private static string[] FindCardRoots()
        {
            List<string> roots = new List<string>();
            string local = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "Cards");
            AddCardRoot(roots, local);
            AddCardRoot(roots, Path.Combine(local, "Female"));
            AddCardRoot(roots, Path.Combine(local, "Male"));

            string gameRoot = NameMap.FindGameRoot();
            if (!string.IsNullOrEmpty(gameRoot))
            {
                string save = Path.Combine(gameRoot, "data", "save");
                AddCardRoot(roots, Path.Combine(save, "Female"));
                AddCardRoot(roots, Path.Combine(save, "Male"));
                AddCardRoot(roots, Path.Combine(
                    save, "Male", "Illusion Default"));
            }

            return roots.ToArray();
        }

        private static void AddCardRoot(List<string> roots, string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return;
            if (roots.Any(root => string.Equals(root, path,
                StringComparison.OrdinalIgnoreCase))) return;
            roots.Add(path);
        }

        private static void AssignImages(SeatItem seat, CardImages card)
        {
            seat.RosterImage = CardImageLoader.ToImage(card.RosterBytes, 136);
            seat.CardFaceImage = CardImageLoader.ToImage(card.FaceBytes, 240);
        }

        private static bool NamesMatch(SeatItem seat, CardEntry card)
        {
            if (Same(seat.Name, card.Name)) return true;
            return (Same(seat.FirstName, card.FirstName) &&
                    Same(seat.LastName, card.LastName)) ||
                   (Same(seat.FirstName, card.LastName) &&
                    Same(seat.LastName, card.FirstName));
        }

        private static bool Same(string left, string right)
        {
            if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
                return false;
            return string.Equals(left.Trim(), right.Trim(),
                StringComparison.OrdinalIgnoreCase);
        }

        private void SeedSeats()
        {
            for (int i = 0; i < 25; i++)
            {
                Seats.Add(new SeatItem
                {
                    SeatIndex = i,
                    Seat = i.ToString("00"),
                    IsTeacher = i == 24,
                    Status = "Empty",
                    Action = "None",
                    Relationship = "-",
                    HeaderRelationship = "-",
                    Mood = "-",
                    Energy = "-",
                    Study = "-",
                    Training = "-",
                    Club = "-"
                });
            }
        }

        private void SeedPlan()
        {
            PlanSteps.Add(new PlanStepItem
            {
                Number = 1,
                Description = "Pet -> seat 00",
                State = "done"
            });
            PlanSteps.Add(new PlanStepItem
            {
                Number = 2,
                Description = "Hug -> seat 00",
                State = "skipped",
                Enabled = false,
                Note = "disabled"
            });
            PlanSteps.Add(new PlanStepItem
            {
                Number = 3,
                Description = "Encourage -> seat 00",
                State = "running",
                Note = "queued for key 0x211B11C8"
            });
            PlanSteps.Add(new PlanStepItem
            {
                Number = 4,
                Description = "Talk About Love -> seat 00",
                State = "pending"
            });
        }

        private void SeedLogs()
        {
            string[] live =
            {
                "seat 00  move=7 action=23 partner=03 conv=0 talk=1 loc=19",
                "seat 03  move=7 action=23 partner=00 conv=1 talk=0 loc=19",
                "seat 01  move=1 action=-1 partner=00 conv=0 talk=0 loc=19",
                "seat 02  move=2 action=-1 partner=- conv=0 talk=0 loc=21",
                "seat 03  move=3 action=44 partner=00 conv=0 talk=0 loc=10",
                "seat 00  move=7 action=-1 partner=03 conv=0 talk=0 loc=10",
                "seat 03  move=0 action=-1 partner=- conv=0 talk=0 loc=10",
                "seat 02  move=2 action=108 partner=- conv=0 talk=0 loc=1",
                "seat 01  move=8 action=-1 partner=00 conv=0 talk=0 loc=19"
            };
            for (int i = 0; i < live.Length; i++)
            {
                string time = "12:34:" + (10 + i).ToString("00") + "." +
                    (120 + i * 7).ToString("000");
                LiveLines.Add(new LogLine(time, live[i]));
            }

            PlannerLines.Add(new LogLine("12:34:10.120",
                "Plan started for seat 03 (4 steps, Forced perform)."));
            PlannerLines.Add(new LogLine("12:34:10.220",
                "  seat 03 step 1/4: queued for key 0x211B11C8 (slot 0)"));
            PlannerLines.Add(new LogLine("12:34:10.340",
                "  seat 03 direct perform action 44 in place."));
            PlannerLines.Add(new LogLine("12:34:10.460",
                "  seat 03 step 1/4 done: Pet -> seat 00."));
            PlannerLines.Add(new LogLine("12:34:10.480",
                "  seat 03 skipped 1 disabled step(s)."));
            PlannerLines.Add(new LogLine("12:34:11.020",
                "  seat 03 step 3/4: queued for key 0x211B11C8 (slot 0)"));
            PlannerLines.Add(new LogLine("12:34:11.140",
                "  seat 03 step 3/4 running: Encourage -> seat 00."));
        }

        private void SeedDebug()
        {
            DebugFields.Add(new DebugFieldItem
            {
                Name = "Status", Address = "0x21DE2E28", Type = "ptr",
                Value = "0x21DE2F10", Note = "character status"
            });
            DebugFields.Add(new DebugFieldItem
            {
                Name = "Action", Address = "0x21DE2F50", Type = "i32",
                Value = "44", Note = "Pet"
            });
            DebugFields.Add(new DebugFieldItem
            {
                Name = "MovementType", Address = "0x21DE2F4C", Type = "i32",
                Value = "3", Note = "walk to character"
            });
            DebugFields.Add(new DebugFieldItem
            {
                Name = "Location", Address = "0x21DE2F58", Type = "i32",
                Value = "10", Note = "Outside Toilets"
            });
            DebugFields.Add(new DebugFieldItem
            {
                Name = "Response", Address = "0x21DE2F6C", Type = "i32",
                Value = "1", Note = "Yes"
            });
            DebugFields.Add(new DebugFieldItem
            {
                Name = "TargetNpc", Address = "0x21DE2F7C", Type = "u32",
                Value = "0x211B11C8", Note = "activity"
            });
            DebugFields.Add(new DebugFieldItem
            {
                Name = "WalkTarget", Address = "0x21DE2F80", Type = "u32",
                Value = "0x21DE2E58", Note = "interface"
            });
            DebugFields.Add(new DebugFieldItem
            {
                Name = "RoomPointer", Address = "0x21DE2F84", Type = "u32",
                Value = "0x21DE32A0", Note = "room block"
            });
            DebugFields.Add(new DebugFieldItem
            {
                Name = "AnimLocked", Address = "0x21DE2F24", Type = "u8",
                Value = "0", Note = "byte"
            });
            DebugFields.Add(new DebugFieldItem
            {
                Name = "InConversation", Address = "0x21DE2F29", Type = "u8",
                Value = "1", Note = "byte"
            });
        }
    }
}
