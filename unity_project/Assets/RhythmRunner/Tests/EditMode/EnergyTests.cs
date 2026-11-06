using System;
using NUnit.Framework;
using RhythmSlashRunner.Core;

namespace RhythmSlashRunner.Tests
{
    /// <summary>Round 32: plays (10, one back every 15 minutes), energy drinks, free first days, the weekly best.</summary>
    public sealed class EnergyTests
    {
        static readonly DateTime T0 = new DateTime(2026, 10, 19, 9, 0, 0, DateTimeKind.Utc);

        static PlayerProfile AfterFreeDays()
        {
            var p = new PlayerProfile();
            Energy.Current(p, T0); // starts the clocks
            p.firstPlayTicks = T0.AddDays(-Energy.FreeDays - 1).Ticks;
            return p;
        }

        [Test]
        public void ANewPlayerStartsFullAndPlaysFreeForTheFirstDays()
        {
            var p = new PlayerProfile();
            Assert.AreEqual(Energy.Max, Energy.Current(p, T0));
            for (int i = 0; i < 30; i++) Assert.IsTrue(Energy.TryUse(p, T0.AddHours(1)));
            Assert.AreEqual(Energy.Max, Energy.Current(p, T0.AddHours(1)), "free days cost no plays");
            Assert.IsFalse(Energy.FreePeriod(p, T0.AddDays(Energy.FreeDays)));
        }

        [Test]
        public void TenPlaysThenOneBackEveryFifteenMinutes()
        {
            PlayerProfile p = AfterFreeDays();
            for (int i = 0; i < Energy.Max; i++) Assert.IsTrue(Energy.TryUse(p, T0));
            Assert.IsFalse(Energy.TryUse(p, T0), "no plays left");
            Assert.AreEqual(TimeSpan.FromMinutes(15), Energy.UntilNext(p, T0));
            Assert.AreEqual(0, Energy.Current(p, T0.AddMinutes(14)));
            Assert.AreEqual(1, Energy.Current(p, T0.AddMinutes(15)));
            Assert.AreEqual(TimeSpan.FromMinutes(10), Energy.UntilNext(p, T0.AddMinutes(20)), "the partial wait carries over");
            Assert.AreEqual(Energy.Max, Energy.Current(p, T0.AddHours(10)), "never above the maximum");
        }

        [Test]
        public void ADrinkRefillsAndDrinksComeFromCoinsLoginAndBosses()
        {
            PlayerProfile p = AfterFreeDays();
            Assert.IsFalse(Energy.Drink(p, T0), "no drink");
            p.coinBalance = Energy.DrinkCoinPrice;
            Assert.IsTrue(Energy.BuyDrinkWithCoins(p));
            Assert.AreEqual(0, p.coinBalance);
            Assert.IsFalse(Energy.Drink(p, T0), "already full: the drink is kept");
            Assert.AreEqual(1, p.drinks);
            for (int i = 0; i < 4; i++) Energy.TryUse(p, T0);
            Assert.IsTrue(Energy.Drink(p, T0));
            Assert.AreEqual(Energy.Max, Energy.Current(p, T0));
            Assert.AreEqual(0, p.drinks);
            Assert.IsTrue(Energy.LoginBonus(p, "20261019"));
            Assert.IsFalse(Energy.LoginBonus(p, "20261019"), "once a day");
            Assert.IsTrue(Energy.BossBonus(p, "20261019"));
            Assert.IsFalse(Energy.BossBonus(p, "20261019"));
            Assert.AreEqual(2, p.drinks);
        }

        [Test]
        public void ARunLostInABossFightGivesItsPlayBack()
        {
            PlayerProfile p = AfterFreeDays();
            Energy.TryUse(p, T0); Energy.TryUse(p, T0);
            Energy.Refund(p, T0);
            Assert.AreEqual(Energy.Max - 1, Energy.Current(p, T0));
            Energy.Refund(p, T0); Energy.Refund(p, T0);
            Assert.AreEqual(Energy.Max, Energy.Current(p, T0));
        }

        [Test]
        public void OldSavesStartFull()
        {
            var old = UnityEngine.JsonUtility.FromJson<PlayerProfile>("{\"coinBalance\":10}");
            Assert.AreEqual(Energy.Max, Energy.Current(old, T0));
        }

        [Test]
        public void TheWeeklyBestResetsOnMonday()
        {
            var p = new PlayerProfile();
            var sunday = new DateTime(2026, 10, 18, 20, 0, 0);
            Assert.IsTrue(Weekly.Record(p, 300, sunday));
            Assert.IsFalse(Weekly.Record(p, 200, sunday));
            Assert.AreEqual(300, Weekly.Best(p, sunday));
            var monday = new DateTime(2026, 10, 19, 8, 0, 0);
            Assert.AreEqual(0, Weekly.Best(p, monday), "a new week");
            Assert.IsTrue(Weekly.Record(p, 120, monday));
            Assert.AreEqual(7, Weekly.DaysLeft(monday));
            Assert.AreEqual(1, Weekly.DaysLeft(sunday));
        }

        [Test]
        public void TheReviewIsAskedOnlyAfterAGoodMomentAndRarely()
        {
            var p = new PlayerProfile { runsPlayed = 2 };
            Assert.IsFalse(ReviewPrompt.ShouldAsk(p, true, T0), "not in the first runs");
            p.runsPlayed = 6;
            Assert.IsFalse(ReviewPrompt.ShouldAsk(p, false, T0), "only after a new best or a boss");
            Assert.IsTrue(ReviewPrompt.ShouldAsk(p, true, T0));
            p.reviewAskedTicks = T0.Ticks;
            Assert.IsFalse(ReviewPrompt.ShouldAsk(p, true, T0.AddDays(10)), "at most once a month");
            Assert.IsTrue(ReviewPrompt.ShouldAsk(p, true, T0.AddDays(31)));
            p.reviewDone = true;
            Assert.IsFalse(ReviewPrompt.ShouldAsk(p, true, T0.AddDays(400)), "never again after rating");
        }
    }
}
