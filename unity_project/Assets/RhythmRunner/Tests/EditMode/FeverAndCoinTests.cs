using NUnit.Framework;
using RhythmSlashRunner.Core;

namespace RhythmSlashRunner.Tests
{
    public sealed class FeverAndCoinTests
    {
        static RunEngine Engine()
        {
            var pattern = new PatternDefinition
            {
                id = "steady", durationBeats = 8, difficultyTier = 0, weight = 1,
                events = new[]
                {
                    E("a", 0, 1), E("b", 1, 1), E("c", 2, 1), E("d", 3, 1), E("e", 4, 1), E("f", 5, 1), E("g", 6, 1), E("h", 7, 1)
                }
            };
            return new RunEngine(new[] { pattern }, 1);
        }

        static PatternEventDefinition E(string id, float beat, int lane) { return new PatternEventDefinition { id = id, beat = beat, lane = lane, objectId = "object_strawberry", gesture = "tap" }; }

        static void HitAll(RunEngine engine, int count)
        {
            for (int i = 0; i < count; i++) { engine.EnsureScheduled(i); Assert.AreEqual(Rating.Perfect, engine.JudgeTap(1, i).Rating); }
        }

        [Test]
        public void FeverStartsOnTheFiftiethHitAndDoublesCoinUnits()
        {
            RunEngine engine = Engine();
            HitAll(engine, 49);
            Assert.IsFalse(engine.Fever);
            Assert.AreEqual(49, engine.CoinUnits);
            HitAll2(engine, 49, 1);
            Assert.IsTrue(engine.Fever);
            Assert.AreEqual(1, engine.FeverLevel);
            Assert.AreEqual(49 + 2, engine.CoinUnits);
            Assert.AreEqual(1, engine.FeverHits);
        }

        static void HitAll2(RunEngine engine, int from, int count)
        {
            for (int i = from; i < from + count; i++) { engine.EnsureScheduled(i); Assert.AreEqual(Rating.Perfect, engine.JudgeTap(1, i).Rating); }
        }

        [Test]
        public void FeverLevelRisesEveryFiftyAndEndsOnMiss()
        {
            RunEngine engine = Engine();
            HitAll(engine, 100);
            Assert.AreEqual(2, engine.FeverLevel);
            Assert.AreEqual(49 + 51 * 2, engine.CoinUnits);
            engine.EnsureScheduled(100);
            Assert.AreEqual(Rating.Miss, engine.JudgeTap(0, 100).Rating); // wrong lane
            Assert.IsFalse(engine.Fever);
            Assert.AreEqual(0, engine.FeverLevel);
        }

        [Test]
        public void EmptyTapAlsoEndsFever()
        {
            RunEngine engine = Engine();
            HitAll(engine, 55);
            Assert.IsTrue(engine.Fever);
            Assert.AreEqual(Rating.Empty, engine.JudgeTap(1, 55.4).Rating);
            Assert.IsFalse(engine.Fever);
        }

        [Test]
        public void SnapshotCoinsMatchTheRewardFormula()
        {
            RunEngine engine = Engine();
            HitAll(engine, 60);
            RunSnapshot s = engine.Snapshot(120);
            // units: 49 normal + 11 fever×2 = 71 → 23 coins; 120 m → 2 × 3 distance coins.
            Assert.AreEqual(71, s.CoinUnits);
            Assert.AreEqual(23 + 6, s.Coins);
            Assert.AreEqual(s.Coins, RunEngine.CoinsForRun(s));
        }

        [Test]
        public void WithoutFeverCoinUnitsEqualSuccesses()
        {
            RunEngine engine = Engine();
            HitAll(engine, 30);
            RunSnapshot s = engine.Snapshot(30);
            Assert.AreEqual(s.Perfect + s.Good, s.CoinUnits);
            Assert.AreEqual(0, s.FeverHits);
        }

        [Test]
        public void PerfectStreakResetsOnGood()
        {
            RunEngine engine = Engine();
            HitAll(engine, 5);
            Assert.AreEqual(5, engine.PerfectStreak);
            engine.EnsureScheduled(5);
            Assert.AreEqual(Rating.Good, engine.JudgeTap(1, 5 + 0.25).Rating); // 125 ms late
            Assert.AreEqual(0, engine.PerfectStreak);
        }

        [Test]
        public void FlashBudgetCapsPulsesAtTwoPerRollingSecond()
        {
            var budget = new FlashBudget();
            Assert.IsTrue(budget.TryFlash(FlashBudget.Region.Edge, 0.0));
            Assert.IsTrue(budget.TryFlash(FlashBudget.Region.Edge, 0.25));
            Assert.IsFalse(budget.TryFlash(FlashBudget.Region.Edge, 0.5));   // offbeat rate (4 Hz) is refused
            Assert.IsTrue(budget.TryFlash(FlashBudget.Region.TopBand, 0.5)); // regions are independent
            Assert.IsTrue(budget.TryFlash(FlashBudget.Region.Edge, 1.0));
            int allowed = 0;
            for (int i = 0; i < 100; i++) if (budget.TryFlash(FlashBudget.Region.Lane, 10 + i * 0.25)) allowed++;
            Assert.LessOrEqual(allowed, 2 * 25 + 2);                          // 25 s of 4 Hz requests → ≤ 2 Hz
        }
    }
}
