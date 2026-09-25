using System;
using System.Collections.Generic;
using UnityEngine;

namespace DinoNet
{
    /// <summary>
    /// The child's saved progress on this headset: stars per level, how well each networking idea
    /// is understood, badges and the play streak. This is what drives the gamification, the level
    /// select screen and the adaptive coach. It stores nothing personal - there are no accounts.
    /// </summary>
    public static class ProgressStore
    {
        [Serializable]
        public class LevelRecord
        {
            public int level;
            public int bestStars;
            public int completions;
            public int attempts;
            public float bestTime;
        }

        [Serializable]
        public class ConceptRecord
        {
            public string concept;
            public int questions;
            public int firstTry;
            public int attempts;
        }

        [Serializable]
        class Data
        {
            public List<LevelRecord> levels = new List<LevelRecord>();
            public List<ConceptRecord> concepts = new List<ConceptRecord>();
            public List<string> badges = new List<string>();
            public string lastPlayDay = string.Empty;
            public int streak;
            public int sandboxFixes;
            public string lastFailedLevels = string.Empty;
        }

        public struct BadgeInfo
        {
            public string id;
            public string name;
            public string description;
        }

        const string k_Key = "dn_progress_v1";

        static readonly BadgeInfo[] s_Badges =
        {
            new BadgeInfo { id = "first_delivery", name = "First Delivery", description = "Finish your first level" },
            new BadgeInfo { id = "safe_surfer", name = "Safe Surfer", description = "Pick HTTPS on the first try" },
            new BadgeInfo { id = "password_guardian", name = "Password Guardian", description = "Keep a password private first try" },
            new BadgeInfo { id = "speedy_router", name = "Speedy Router", description = "Finish with lots of time left" },
            new BadgeInfo { id = "backup_hero", name = "Backup Hero", description = "Solve the blocked road" },
            new BadgeInfo { id = "network_builder", name = "Network Builder", description = "Fix 3 problems in the sandbox" },
            new BadgeInfo { id = "streak_3", name = "3-Day Streak", description = "Play 3 days in a row" },
            new BadgeInfo { id = "big_brain", name = "Big Brain", description = "Finish all 10 levels" },
        };

        static Data s_Data;

        public static event Action<BadgeInfo> BadgeEarned;

        public static IReadOnlyList<BadgeInfo> AllBadges => s_Badges;

        static Data D
        {
            get
            {
                if (s_Data == null)
                    Load();
                return s_Data;
            }
        }

        static void Load()
        {
            try
            {
                var json = PlayerPrefs.GetString(k_Key, string.Empty);
                s_Data = string.IsNullOrEmpty(json) ? new Data() : JsonUtility.FromJson<Data>(json) ?? new Data();
            }
            catch (Exception)
            {
                s_Data = new Data();
            }
        }

        public static void Save()
        {
            PlayerPrefs.SetString(k_Key, JsonUtility.ToJson(D));
            PlayerPrefs.Save();
        }

        /// <summary>Wipes saved progress. Used by the test tools and the progress screen's reset.</summary>
        public static void ResetAll()
        {
            s_Data = new Data();
            Save();
        }

        // ---------------------------------------------------------------- levels

        static LevelRecord Rec(int level)
        {
            foreach (var r in D.levels)
            {
                if (r.level == level)
                    return r;
            }

            var made = new LevelRecord { level = level };
            D.levels.Add(made);
            return made;
        }

        public static int BestStars(int level) => Rec(level).bestStars;

        public static bool IsCompleted(int level) => Rec(level).completions > 0;

        public static int Attempts(int level) => Rec(level).attempts;

        public static int TotalStars
        {
            get
            {
                var total = 0;
                foreach (var r in D.levels)
                    total += r.bestStars;
                return total;
            }
        }

        public static int LevelsCompleted
        {
            get
            {
                var n = 0;
                foreach (var r in D.levels)
                {
                    if (r.level > 0 && r.completions > 0)
                        n++;
                }

                return n;
            }
        }

        public static void RecordLevelStart(int level)
        {
            Rec(level).attempts++;
            Save();
        }

        public static void RecordLevelFailed(int level)
        {
            var text = D.lastFailedLevels ?? string.Empty;
            var marker = "," + level + ",";
            if (!text.Contains(marker))
                D.lastFailedLevels = (string.IsNullOrEmpty(text) ? "," : text) + level + ",";
            Save();
        }

        /// <summary>True if the child failed this level last time they tried it.</summary>
        public static bool FailedLastTime(int level) => (D.lastFailedLevels ?? string.Empty).Contains("," + level + ",");

        /// <summary>Stars for a finished level: fewer mistakes and more time left earn more.</summary>
        public static int StarsFor(float timeLeftFraction, int mistakes)
        {
            if (mistakes <= 1 && timeLeftFraction >= 0.4f)
                return 3;
            if (mistakes <= 3 && timeLeftFraction >= 0.15f)
                return 2;
            return 1;
        }

        /// <summary>Saves a completion; returns the new best star count.</summary>
        public static int RecordCompletion(int level, float seconds, int stars)
        {
            var r = Rec(level);
            r.completions++;
            if (stars > r.bestStars)
                r.bestStars = stars;
            if (seconds > 0f && (r.bestTime <= 0f || seconds < r.bestTime))
                r.bestTime = seconds;

            D.lastFailedLevels = (D.lastFailedLevels ?? string.Empty).Replace("," + level + ",", ",");
            Save();
            return r.bestStars;
        }

        // ---------------------------------------------------------------- concepts

        static ConceptRecord Concept(string concept)
        {
            foreach (var c in D.concepts)
            {
                if (c.concept == concept)
                    return c;
            }

            var made = new ConceptRecord { concept = concept };
            D.concepts.Add(made);
            return made;
        }

        public static void RecordAnswer(string concept, bool correct, int attempt)
        {
            if (string.IsNullOrEmpty(concept))
                return;

            var c = Concept(concept);
            c.attempts++;
            if (correct)
            {
                c.questions++;
                if (attempt <= 1)
                    c.firstTry++;
            }

            Save();
        }

        /// <summary>0..1 share answered right first time, or -1 if the concept hasn't been met yet.</summary>
        public static float Mastery(string concept)
        {
            var c = Concept(concept);
            return c.questions == 0 ? -1f : c.firstTry / (float)c.questions;
        }

        public static IReadOnlyList<ConceptRecord> Concepts => D.concepts;

        /// <summary>The concept the child most needs to revisit, or null if none stands out.</summary>
        public static string WeakestConcept()
        {
            string worst = null;
            var worstValue = 0.6f;
            foreach (var c in D.concepts)
            {
                if (c.questions == 0)
                    continue;

                var m = c.firstTry / (float)c.questions;
                if (m < worstValue)
                {
                    worstValue = m;
                    worst = c.concept;
                }
            }

            return worst;
        }

        /// <summary>Which level to suggest next: revisit a shaky idea, otherwise the first unfinished level.</summary>
        public static int RecommendedLevel()
        {
            var weak = WeakestConcept();
            var level = ConceptCatalog.LevelFor(weak);
            if (level > 0)
                return level;

            for (var i = 1; i <= GameFlow.LastLevel; i++)
            {
                if (!IsCompleted(i))
                    return i;
            }

            return GameFlow.LastLevel;
        }

        // ---------------------------------------------------------------- streak & badges

        /// <summary>Call once per launch: keeps the consecutive-days-played streak.</summary>
        public static int TouchStreak()
        {
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            if (D.lastPlayDay == today)
                return D.streak;

            var yesterday = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");
            D.streak = D.lastPlayDay == yesterday ? D.streak + 1 : 1;
            D.lastPlayDay = today;
            Save();

            if (D.streak >= 3)
                TryAward("streak_3");

            return D.streak;
        }

        public static int Streak => D.streak;

        public static bool Has(string badgeId) => D.badges.Contains(badgeId);

        public static IReadOnlyList<string> EarnedBadges => D.badges;

        public static bool TryAward(string badgeId)
        {
            if (D.badges.Contains(badgeId))
                return false;

            foreach (var b in s_Badges)
            {
                if (b.id != badgeId)
                    continue;

                D.badges.Add(badgeId);
                Save();
                BadgeEarned?.Invoke(b);
                SessionTelemetry.Log("badge_earned", 0, "id", b.id, "name", b.name);
                return true;
            }

            return false;
        }

        public static void RecordSandboxFix()
        {
            D.sandboxFixes++;
            Save();
            if (D.sandboxFixes >= 3)
                TryAward("network_builder");
        }

        public static int SandboxFixes => D.sandboxFixes;
    }
}
