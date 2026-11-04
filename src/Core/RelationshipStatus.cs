using System;
using System.Collections.Generic;

namespace AA2Trainer
{
    /// <summary>
    /// Read-only view of the 61 thought-bubble statuses. The game does not keep
    /// one stored bubble ID: the roster builds a small candidate set from the
    /// stored LLDH tiers (plus lover/mood/special flags) and displays one of
    /// them each time the screen is opened. This class reproduces the candidate
    /// derivation for the Debug tab; it is deliberately conservative and does
    /// not write to memory.
    /// </summary>
    public static class RelationshipStatus
    {
        private static readonly string[] Names =
        {
            "TOGETHER FOREVER",                         // 00
            "I'll get you",                             // 01
            "Unforgivable!",                            // 02
            "Cheater!",                                 // 03
            "I hate that I...",                         // 04
            "The lowest trash...",                      // 05
            "Gone too far...",                          // 06
            "What shall I do next",                     // 07
            "Haven't spoken to",                        // 08
            "Don't know",                               // 09
            "Who?",                                     // 10
            "No interest",                              // 11
            "I'm curious!",                             // 12
            "Wanna get back together",                  // 13
            "Former BF/GF",                             // 14
            "Let's get married",                        // 15
            "Mai waifu",                                // 16
            "Lover, for now.",                          // 17
            "I want to break up",                       // 18
            "Overflowing love!",                        // 19
            "Angel",                                    // 20
            "Like, but can't say it",                   // 21
            "One-sided love...",                        // 22
            "No doubt it's love!",                      // 23
            "I think I like them...",                   // 24
            "Shameless!",                               // 25
            "Hate but love",                            // 26
            "Best friend",                              // 27
            "Good buddies",                             // 28
            "Hole brothers/rod sisters",                // 29
            "Friend",                                   // 30
            "Sex friend",                               // 31
            "Closer than friends, not quite lovers",    // 32
            "Normalfags go die",                        // 33
            "Love rivals",                              // 34
            "Classmate",                                // 35
            "Piece of shit...",                         // 36
            "Fated enemies",                            // 37
            "You should die...",                        // 38
            "10000 deaths not enough",                  // 39
            "Love but hate",                            // 40
            "Physically impossible",                    // 41
            "Don't want to get involved",               // 42
            "Not good with these types",                // 43
            "Homeroom teacher",                         // 44
            "Incompetent teacher",                      // 45
            "Ideal teacher",                            // 46
            "Teacher I've longed for",                  // 47
            "Normal student",                           // 48
            "Problem student",                          // 49
            "Favorite student",                         // 50
            "Special student",                          // 51
            "Forbidden relationship",                   // 52
            "Secret relationship",                      // 53
            "Makes my heart throb",                     // 54
            "Really pisses me off",                     // 55
            "(´・ω・`)",                                 // 56
            "Really wanna fuck",                        // 57
            "Slut",                                     // 58
            "Lewd promise",                             // 59
            "Good chemistry"                            // 60
        };

        public static int Count { get { return Names.Length; } }

        public static string Label(int id)
        {
            if (id < 0 || id >= Names.Length) return "Unknown (" + id + ")";
            return Names[id];
        }

        public static string LabelWithId(int id)
        {
            return Label(id) + " (" + id + ")";
        }

        /// <summary>0 = Lots, 1 = Some, 2 = Very little.</summary>
        public static string TierName(int tier)
        {
            if (tier == 0) return "Lots";
            if (tier == 1) return "Some";
            if (tier == 2) return "Very little";
            return "-";
        }

        /// <summary>
        /// Candidate statuses for one source -> target relationship. This is a
        /// wiki-based approximation, not a byte-for-byte copy of the game's
        /// selection function. The Debug tab labels it experimental.
        /// </summary>
        public static List<int> BuildCandidates(RelationshipRecord rec,
            bool isLover, bool wasLover, bool hasHuh)
        {
            List<int> list = new List<int>();
            if (rec == null || !rec.HasRecord)
            {
                list.Add(8); // Haven't spoken to
                return list;
            }

            int[] lv = rec.Levels;
            int[] cnt = rec.Counts;
            int[] pts = rec.Points;

            int total = cnt[0] + cnt[1] + cnt[2] + cnt[3];
            bool anyPoints = pts[0] + pts[1] + pts[2] + pts[3] > 0;

            if (total == 0 && !anyPoints)
            {
                list.Add(9); // Don't know
                return list;
            }

            // Love axis.
            if (lv[0] == 0)
            {
                if (isLover) AddUnique(list, 16);      // Mai waifu
                else if (wasLover) AddUnique(list, 13); // Wanna get back together
                else AddUnique(list, 19);              // Overflowing love!
            }
            else if (lv[0] == 1)
            {
                AddUnique(list, isLover ? 17 : 24);    // Lover, for now / I think I like them...
            }

            // Like axis.
            if (lv[1] == 0)
            {
                AddUnique(list, hasHuh ? 31 : 27);     // Sex friend / Best friend
            }
            else if (lv[1] == 1)
            {
                AddUnique(list, 30);                   // Friend
                if (lv[0] <= 1) AddUnique(list, 12);   // I'm curious!
            }
            else
            {
                AddUnique(list, 35);                   // Classmate
            }

            // Dislike axis.
            if (lv[2] == 0) AddUnique(list, 11);       // No interest
            else if (lv[2] == 1) AddUnique(list, 10);  // Who?

            // Hate axis.
            if (lv[3] == 0) AddUnique(list, 39);       // 10000 deaths not enough
            else if (lv[3] == 1) AddUnique(list, 38);  // You should die...

            if (list.Count == 0) list.Add(9);
            if (list.Count > 4) list.RemoveRange(4, list.Count - 4);
            return list;
        }

        private static void AddUnique(List<int> list, int id)
        {
            if (!list.Contains(id)) list.Add(id);
        }
    }
}
