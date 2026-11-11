using System.Windows.Media.Imaging;

namespace AA2Trainer.WpfPrototype
{
    public sealed class SeatItem : ObservableObject
    {
        private string firstName;
        private string lastName;
        private string nickname;
        private string profile;
        private string room;
        private string action;
        private string status;
        private string relationship;
        private string headerRelationship;
        private string mood;
        private string energy;
        private string study;
        private string training;
        private string club;
        private BitmapImage rosterImage;
        private BitmapImage cardFaceImage;
        private BitmapImage liveRosterImage;
        private BitmapImage liveCardFaceImage;
        private bool liveNameApplied;
        private string liveName;
        private bool isFilteredOut;

        public int SeatIndex { get; set; } = -1;
        public string Seat { get; set; } = "";
        public bool IsPc { get; set; }
        public bool IsTeacher { get; set; }
        public bool HasRelationship { get; set; }
        public bool IsLover { get; set; }
        public bool OtherIsLover { get; set; }
        public bool IsFrozen { get; set; }

        /// <summary>
        /// True once a live session snapshot has supplied this seat's name.
        /// Card metadata must never replace a live name.
        /// </summary>
        public bool LiveNameApplied
        {
            get { return liveNameApplied; }
            private set
            {
                if (liveNameApplied == value) return;
                liveNameApplied = value;
                OnPropertyChanged(nameof(Name));
                OnPropertyChanged(nameof(Initial));
                OnPropertyChanged(nameof(HasName));
            }
        }

        public bool HasName
        {
            get
            {
                return !string.IsNullOrEmpty(liveName) ||
                       !string.IsNullOrEmpty(firstName) ||
                       !string.IsNullOrEmpty(lastName);
            }
        }

        public string FirstName
        {
            get { return firstName; }
            set
            {
                if (!Set(ref firstName, value)) return;
                OnPropertyChanged(nameof(Name));
                OnPropertyChanged(nameof(Initial));
                OnPropertyChanged(nameof(HasName));
            }
        }

        public string LastName
        {
            get { return lastName; }
            set
            {
                if (!Set(ref lastName, value)) return;
                OnPropertyChanged(nameof(Name));
                OnPropertyChanged(nameof(Initial));
                OnPropertyChanged(nameof(HasName));
            }
        }

        public string Name
        {
            get
            {
                // The live name is shown exactly as the game stores it. No
                // reordering, romanization, or translation is applied.
                if (!string.IsNullOrEmpty(liveName)) return liveName;

                string text = liveNameApplied
                    ? ((lastName ?? "") + " " + (firstName ?? "")).Trim()
                    : ((firstName ?? "") + " " + (lastName ?? "")).Trim();
                return string.IsNullOrEmpty(text) ? "Empty" : text;
            }
        }

        public string Initial
        {
            get
            {
                string text = liveNameApplied ? lastName : firstName;
                if (string.IsNullOrEmpty(text))
                    text = liveNameApplied ? firstName : lastName;
                if (string.IsNullOrEmpty(text)) text = liveName;
                if (string.IsNullOrEmpty(text)) return "";
                return text.Substring(0, 1).ToUpperInvariant();
            }
        }

        public string Nickname
        {
            get { return nickname; }
            set { Set(ref nickname, value); }
        }

        public string Profile
        {
            get { return profile; }
            set { Set(ref profile, value); }
        }

        public string Room
        {
            get { return room; }
            set
            {
                if (!Set(ref room, value)) return;
                OnPropertyChanged(nameof(RoomAction));
            }
        }

        public string Action
        {
            get { return action; }
            set
            {
                if (!Set(ref action, value)) return;
                OnPropertyChanged(nameof(RoomAction));
            }
        }

        public string RoomAction
        {
            get
            {
                if (string.IsNullOrEmpty(room) &&
                    (string.IsNullOrEmpty(action) || action == "None"))
                    return "No character";

                return "room " + (string.IsNullOrEmpty(room) ? "?" : room) +
                    " | " + (string.IsNullOrEmpty(action) ? "idle" : action);
            }
        }

        public string Status
        {
            get { return status; }
            set { Set(ref status, value); }
        }

        public string Relationship
        {
            get { return relationship; }
            set { Set(ref relationship, value); }
        }

        /// <summary>
        /// Relation of this seat to the player character, shown in the header.
        /// </summary>
        public string HeaderRelationship
        {
            get { return headerRelationship; }
            set { Set(ref headerRelationship, value); }
        }

        public string Mood
        {
            get { return mood; }
            set { Set(ref mood, value); }
        }

        public string Energy
        {
            get { return energy; }
            set { Set(ref energy, value); }
        }

        public string Study
        {
            get { return study; }
            set { Set(ref study, value); }
        }

        public string Training
        {
            get { return training; }
            set { Set(ref training, value); }
        }

        public string Club
        {
            get { return club; }
            set { Set(ref club, value); }
        }

        /// <summary>
        /// Live thumbnail preferred, card-file image as fallback.
        /// </summary>
        public BitmapImage RosterImage
        {
            get { return liveRosterImage ?? rosterImage; }
            set
            {
                if (ReferenceEquals(rosterImage, value)) return;
                rosterImage = value;
                OnPropertyChanged(nameof(RosterImage));
                OnPropertyChanged(nameof(HasCardFallback));
            }
        }

        /// <summary>
        /// Live full portrait preferred, then the live thumbnail, then the
        /// card-file face.
        /// </summary>
        public BitmapImage CardFaceImage
        {
            get { return liveCardFaceImage ?? cardFaceImage; }
            set
            {
                if (ReferenceEquals(cardFaceImage, value)) return;
                cardFaceImage = value;
                OnPropertyChanged(nameof(CardFaceImage));
                OnPropertyChanged(nameof(HasCardFace));
                OnPropertyChanged(nameof(HasCardFallback));
            }
        }

        public bool HasCardFace
        {
            get { return liveCardFaceImage != null || cardFaceImage != null; }
        }

        public bool HasLiveRosterImage
        {
            get { return liveRosterImage != null; }
        }

        public bool HasCardFallback
        {
            get { return rosterImage != null || cardFaceImage != null; }
        }

        /// <summary>
        /// Applies the live portrait read from the character-data block. The
        /// thumbnail keeps the roster card populated immediately; the full
        /// portrait replaces it for the hover popup when it arrives.
        /// </summary>
        public void ApplyLivePortrait(BitmapImage thumbnail,
            BitmapImage fullPortrait)
        {
            BitmapImage face = fullPortrait ?? thumbnail;
            bool rosterChanged = !ReferenceEquals(liveRosterImage, thumbnail);
            bool faceChanged = !ReferenceEquals(liveCardFaceImage, face);

            liveRosterImage = thumbnail;
            liveCardFaceImage = face;

            if (rosterChanged)
            {
                OnPropertyChanged(nameof(RosterImage));
                OnPropertyChanged(nameof(HasLiveRosterImage));
                OnPropertyChanged(nameof(HasCardFallback));
            }
            if (faceChanged)
            {
                OnPropertyChanged(nameof(CardFaceImage));
                OnPropertyChanged(nameof(HasCardFace));
                OnPropertyChanged(nameof(HasCardFallback));
            }
        }

        /// <summary>
        /// Clears only the card-file fallback; a live portrait stays visible.
        /// </summary>
        public void ClearCardArt()
        {
            if (rosterImage == null && cardFaceImage == null) return;
            rosterImage = null;
            cardFaceImage = null;
            OnPropertyChanged(nameof(RosterImage));
            OnPropertyChanged(nameof(CardFaceImage));
            OnPropertyChanged(nameof(HasCardFace));
            OnPropertyChanged(nameof(HasCardFallback));
        }

        /// <summary>
        /// True when the current roster filter hides this seat. The card stays
        /// in the fixed classroom grid but is collapsed.
        /// </summary>
        public bool IsFilteredOut
        {
            get { return isFilteredOut; }
            set { Set(ref isFilteredOut, value); }
        }

        /// <summary>
        /// Applies one live seat snapshot. The name is authoritative while
        /// attached; an empty name clears the seat.
        /// </summary>
        public void ApplyLive(string name, string familyName, string givenName,
            string roomText, string actionText, string statusText,
            string relationshipText, string headerText, bool isPlayer,
            bool hasRelationship, bool isLover, bool otherIsLover,
            bool isFrozen)
        {
            SetLiveName(name, familyName, givenName);
            Room = roomText;
            Action = actionText;
            Status = statusText;
            Relationship = relationshipText;
            HeaderRelationship = headerText;
            IsPc = isPlayer;
            HasRelationship = hasRelationship;
            IsLover = isLover;
            OtherIsLover = otherIsLover;
            IsFrozen = isFrozen;
        }

        public void SetLiveName(string fullName, string familyName,
            string givenName)
        {
            string name = fullName ?? "";
            string family = familyName ?? "";
            string given = givenName ?? "";

            if (string.IsNullOrEmpty(family) && string.IsNullOrEmpty(given))
            {
                // Snapshot fallback for older data: split the display string
                // at the first space, family name first. No translation.
                string text = name.Trim();
                int space = text.IndexOf(' ');
                if (space < 0)
                {
                    family = text;
                }
                else
                {
                    family = text.Substring(0, space);
                    given = text.Substring(space + 1).Trim();
                }
            }

            liveName = name;
            if (string.IsNullOrEmpty(liveName) &&
                (!string.IsNullOrEmpty(family) || !string.IsNullOrEmpty(given)))
            {
                liveName = (family + " " + given).Trim();
            }

            FirstName = given;
            LastName = family;
            LiveNameApplied = true;

            // The display name can change even when the split fields do not
            // (for example, extra internal spacing in the game string).
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(Initial));
            OnPropertyChanged(nameof(HasName));
        }
    }
}
