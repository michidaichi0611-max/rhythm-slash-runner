using System;
using System.Collections.Generic;

namespace RhythmSlashRunner.Core
{
    /// <summary>What one run achieved, as far as the daily goals care.</summary>
    public struct RunStats
    {
        public int Distance, Score, Perfect, OffbeatPerfect, MaxCombo, HoldsCompleted, BossesDefeated, HeartsTaken;
    }

    /// <summary>
    /// Owner round 10: three small goals a day ("裏拍を20回PERFECT"…), the same for everyone on the same date, each
    /// paying a few coins once. No randomness the player pays for, no ads: just a reason to play today. Progress of
    /// "total" goals adds up over the day's runs; "in one run" goals keep the best run.
    /// </summary>
    public static class DailyGoals
    {
        public sealed class Goal
        {
            public string Id, Text;
            public int Target;
            public bool InOneRun;
            public Func<RunStats, int> Measure;
        }

        public const int Count = 3;
        public const int RewardCoins = 20;

        public static readonly Goal[] Pool =
        {
            new Goal { Id = "offbeat_perfect", Text = "裏拍を20回PERFECT", Target = 20, Measure = s => s.OffbeatPerfect },
            new Goal { Id = "distance", Text = "1回で300m走る", Target = 300, InOneRun = true, Measure = s => s.Distance },
            new Goal { Id = "boss", Text = "ボスを1体たおす", Target = 1, Measure = s => s.BossesDefeated },
            new Goal { Id = "perfect", Text = "PERFECTを150回", Target = 150, Measure = s => s.Perfect },
            new Goal { Id = "combo", Text = "最大コンボ60", Target = 60, InOneRun = true, Measure = s => s.MaxCombo },
            new Goal { Id = "holds", Text = "長い物を8本斬りきる", Target = 8, Measure = s => s.HoldsCompleted },
            new Goal { Id = "hearts", Text = "回復カプセルを2個取る", Target = 2, Measure = s => s.HeartsTaken },
            new Goal { Id = "score", Text = "1回で8万点", Target = 80000, InOneRun = true, Measure = s => s.Score },
        };

        public static Goal Find(string id) { return Array.Find(Pool, g => g.Id == id); }

        /// <summary>Today's three goals: the date alone decides them (stable on every device and every launch).</summary>
        public static List<Goal> ForDate(string date)
        {
            int seed = 17;
            foreach (char ch in date ?? "") seed = unchecked(seed * 31 + ch);
            var random = new Random(seed);
            var bag = new List<Goal>(Pool);
            var picked = new List<Goal>(Count);
            while (picked.Count < Count && bag.Count > 0) { int i = random.Next(bag.Count); picked.Add(bag[i]); bag.RemoveAt(i); }
            return picked;
        }

        /// <summary>Starts a new day's goals when the date changed (progress of yesterday is dropped).</summary>
        public static void Refresh(PlayerProfile profile, string date)
        {
            profile.Migrate();
            if (profile.dailyDate == date && profile.dailyIds.Count == Count) return;
            profile.dailyDate = date;
            profile.dailyIds.Clear(); profile.dailyProgress.Clear(); profile.dailyDone.Clear();
            foreach (Goal g in ForDate(date)) { profile.dailyIds.Add(g.Id); profile.dailyProgress.Add(0); profile.dailyDone.Add(false); }
        }

        /// <summary>Adds a run; returns the goals completed by it (each pays <see cref="RewardCoins"/> once).</summary>
        public static List<Goal> Apply(PlayerProfile profile, string date, RunStats run)
        {
            Refresh(profile, date);
            var done = new List<Goal>();
            for (int i = 0; i < profile.dailyIds.Count; i++)
            {
                Goal g = Find(profile.dailyIds[i]);
                if (g == null || profile.dailyDone[i]) continue;
                int value = g.Measure(run);
                profile.dailyProgress[i] = g.InOneRun ? Math.Max(profile.dailyProgress[i], value) : profile.dailyProgress[i] + value;
                if (profile.dailyProgress[i] >= g.Target) { profile.dailyDone[i] = true; done.Add(g); }
            }
            return done;
        }

        /// <summary>"裏拍を20回PERFECT  12/20" lines for the home screen.</summary>
        public static string Describe(PlayerProfile profile)
        {
            var lines = new List<string>();
            for (int i = 0; i < profile.dailyIds.Count; i++)
            {
                Goal g = Find(profile.dailyIds[i]);
                if (g == null) continue;
                lines.Add((profile.dailyDone[i] ? "★ " : "・") + g.Text + "  " + (profile.dailyDone[i] ? "達成！" : Math.Min(profile.dailyProgress[i], g.Target) + " / " + g.Target));
            }
            return string.Join("\n", lines.ToArray());
        }

        public static int DoneCount(PlayerProfile profile) { int n = 0; foreach (bool b in profile.dailyDone) if (b) n++; return n; }
    }
}
