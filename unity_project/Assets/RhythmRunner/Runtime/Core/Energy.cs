using System;
using System.Globalization;

namespace RhythmSlashRunner.Core
{
    /// <summary>
    /// Round 32 (owner): plays are limited — up to <see cref="Max"/>, one back every <see cref="RegenMinutes"/> minutes.
    /// An energy drink refills them at once; drinks come from the shop (coins or real money), the daily login bonus, all of
    /// today's goals, and the first boss beaten each day. A new player plays freely for <see cref="FreeDays"/> days, and a
    /// run lost in a boss fight gives its play back. Times are UTC ticks (the device clock).
    /// </summary>
    public static class Energy
    {
        public const int Max = 10;
        public const int RegenMinutes = 15;
        public const int FreeDays = 3;
        public const int DrinkCoinPrice = 150;
        static readonly long RegenTicks = TimeSpan.FromMinutes(RegenMinutes).Ticks;

        /// <summary>Starts the clock for a new (or pre-round-32) save: full plays, the free days from now.</summary>
        static void Init(PlayerProfile p, DateTime nowUtc)
        {
            if (p.energyUpdatedTicks > 0) return;
            p.energy = Max;
            p.energyUpdatedTicks = nowUtc.Ticks;
            if (p.firstPlayTicks <= 0) p.firstPlayTicks = nowUtc.Ticks;
        }

        /// <summary>Plays available now (regeneration applied).</summary>
        public static int Current(PlayerProfile p, DateTime nowUtc)
        {
            Init(p, nowUtc);
            if (p.energy >= Max) { p.energyUpdatedTicks = nowUtc.Ticks; return p.energy; }
            long elapsed = nowUtc.Ticks - p.energyUpdatedTicks;
            if (elapsed < 0) { p.energyUpdatedTicks = nowUtc.Ticks; return p.energy; } // the clock went back: restart the count
            int gained = (int)(elapsed / RegenTicks);
            if (gained > 0)
            {
                p.energy = Math.Min(Max, p.energy + gained);
                p.energyUpdatedTicks = p.energy >= Max ? nowUtc.Ticks : p.energyUpdatedTicks + gained * RegenTicks;
            }
            return p.energy;
        }

        /// <summary>Time until the next play comes back (zero when full).</summary>
        public static TimeSpan UntilNext(PlayerProfile p, DateTime nowUtc)
        {
            if (Current(p, nowUtc) >= Max) return TimeSpan.Zero;
            long left = RegenTicks - (nowUtc.Ticks - p.energyUpdatedTicks);
            return TimeSpan.FromTicks(Math.Max(0, left));
        }

        /// <summary>The first days after install cost no plays.</summary>
        public static bool FreePeriod(PlayerProfile p, DateTime nowUtc)
        {
            Init(p, nowUtc);
            return nowUtc.Ticks - p.firstPlayTicks < TimeSpan.FromDays(FreeDays).Ticks;
        }

        /// <summary>Starting a run: true when it may start (one play is used unless in the free days).</summary>
        public static bool TryUse(PlayerProfile p, DateTime nowUtc)
        {
            if (FreePeriod(p, nowUtc)) return true;
            if (Current(p, nowUtc) <= 0) return false;
            if (p.energy >= Max) p.energyUpdatedTicks = nowUtc.Ticks; // the regeneration clock starts now
            p.energy--;
            return true;
        }

        /// <summary>A run lost in a boss fight gives its play back (never above the maximum).</summary>
        public static void Refund(PlayerProfile p, DateTime nowUtc)
        {
            if (FreePeriod(p, nowUtc)) return;
            Current(p, nowUtc);
            p.energy = Math.Min(Max, p.energy + 1);
        }

        /// <summary>Drinks one energy drink: plays back to full. False with no drink or when already full.</summary>
        public static bool Drink(PlayerProfile p, DateTime nowUtc)
        {
            if (p.drinks <= 0 || Current(p, nowUtc) >= Max) return false;
            p.drinks--;
            p.energy = Max;
            p.energyUpdatedTicks = nowUtc.Ticks;
            return true;
        }

        public static bool BuyDrinkWithCoins(PlayerProfile p)
        {
            if (p.coinBalance < DrinkCoinPrice) return false;
            p.coinBalance -= DrinkCoinPrice;
            p.drinks++;
            return true;
        }

        /// <summary>Once per local day: one drink. True when it was given now.</summary>
        public static bool LoginBonus(PlayerProfile p, string today)
        {
            if (p.loginBonusDate == today) return false;
            p.loginBonusDate = today;
            p.drinks++;
            return true;
        }

        /// <summary>The first boss beaten each local day: one drink. True when it was given now.</summary>
        public static bool BossBonus(PlayerProfile p, string today)
        {
            if (p.bossDrinkDate == today) return false;
            p.bossDrinkDate = today;
            p.drinks++;
            return true;
        }
    }

    /// <summary>
    /// Round 34: when to ask for a store rating — only after a good moment (a new best or a boss beaten), never in the first
    /// five runs, at most once every 30 days, and never again once the player went to rate.
    /// </summary>
    public static class ReviewPrompt
    {
        public const int MinRuns = 5;
        public const int DaysBetween = 30;

        public static bool ShouldAsk(PlayerProfile p, bool goodMoment, DateTime nowUtc)
        {
            if (p.reviewDone || !goodMoment || p.runsPlayed < MinRuns) return false;
            return p.reviewAskedTicks <= 0 || nowUtc.Ticks - p.reviewAskedTicks >= TimeSpan.FromDays(DaysBetween).Ticks;
        }
    }

    /// <summary>Round 32: this week's best distance (weeks start on Monday, local time) for the weekly ranking.</summary>
    public static class Weekly
    {
        public static string Key(DateTime local)
        {
            int back = ((int)local.DayOfWeek + 6) % 7; // Monday = 0
            return local.Date.AddDays(-back).ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        }

        /// <summary>Records a run; true when it is a new best for this week.</summary>
        public static bool Record(PlayerProfile p, int distance, DateTime local)
        {
            string key = Key(local);
            if (p.weeklyKey != key) { p.weeklyKey = key; p.weeklyBestDistance = 0; }
            if (distance <= p.weeklyBestDistance) return false;
            p.weeklyBestDistance = distance;
            return true;
        }

        public static int Best(PlayerProfile p, DateTime local) { return p.weeklyKey == Key(local) ? p.weeklyBestDistance : 0; }

        /// <summary>Days until the week resets (Monday 0:00).</summary>
        public static int DaysLeft(DateTime local) { return 7 - ((int)local.DayOfWeek + 6) % 7; }
    }
}
