using System;
using System.Linq;
using NUnit.Framework;
using RhythmSlashRunner.Core;

namespace RhythmSlashRunner.Tests
{
    /// <summary>Round 17: soldiers walk in and strike on the beat; each strike is parried with a tap, the last one counters.</summary>
    public sealed class SoldierTests
    {
        static PatternEventDefinition Tap(string id, float beat, int lane, string obj)
        {
            return new PatternEventDefinition { id = id, beat = beat, lane = lane, objectId = obj, gesture = "tap" };
        }

        static PatternDefinition Normal()
        {
            return new PatternDefinition { id = "n", durationBeats = 8, difficultyTier = 0, weight = 1, events = new[] { Tap("a", 0, 1, "object_strawberry"), Tap("b", 4, 1, "object_strawberry") } };
        }

        static PatternDefinition Soldier()
        {
            const string s = "object_soldier_toy";
            return new PatternDefinition { id = "sol", durationBeats = 8, difficultyTier = 0, weight = 1, role = RunEngine.RoleSoldier, events = new[] { Tap("a", 2, 1, s), Tap("b", 3, 1, s), Tap("c", 4, 1, s), Tap("d", 6, 1, s) } };
        }

        static RunEngine Engine()
        {
            var engine = new RunEngine(new[] { Normal(), Soldier() }, 3, new RunConfig { ChunksPerStage = 8, BossChunks = 0, ItemsPerStage = 0, SoldiersFirstStage = 1, SoldiersPerStage = 2 });
            engine.EnsureScheduled(0);
            return engine;
        }

        [Test]
        public void OneSoldierChunkIsPlacedInTheFirstStageAndItsLastStrikeIsTheCounter()
        {
            RunEngine engine = Engine();
            engine.EnsureScheduled(8 * 7);
            var strikes = engine.Events.Where(e => e.Soldier != null).ToList();
            Assert.AreEqual(4, strikes.Count, "exactly one soldier chunk in the first stage");
            Assert.IsTrue(strikes.All(e => e.Soldier == "toy" && e.SoldierGroup == strikes[0].SoldierGroup));
            Assert.AreEqual(1, strikes.Count(e => e.SoldierFinal));
            Assert.AreEqual(strikes.Max(e => e.Beat), strikes.Single(e => e.SoldierFinal).Beat);
            Assert.GreaterOrEqual(strikes[0].Beat, 2 * 8, "never in the first two chunks");
        }

        [Test]
        public void ParryingCountsAsAHitAndAMissedStrikeCostsAHeart()
        {
            RunEngine engine = Engine();
            engine.EnsureScheduled(8 * 7);
            var strikes = engine.Events.Where(e => e.Soldier != null).OrderBy(e => e.Beat).ToList();
            // everything before the soldier is cut cleanly
            foreach (ScheduledEvent e in engine.Events.Where(e => e.Beat < strikes[0].Beat).OrderBy(e => e.Beat).ToList()) engine.JudgeTap(e.Lane, e.Beat);
            int combo = engine.Combo;
            Assert.AreEqual(Rating.Perfect, engine.JudgeTap(strikes[0].Lane, strikes[0].Beat).Rating);
            Assert.AreEqual(combo + 1, engine.Combo);
            int hp = engine.Hp;
            var missed = engine.Expire(strikes[1].Beat + 1);
            Assert.IsTrue(missed.Any(r => r.Event == strikes[1] && r.Rating == Rating.Miss));
            Assert.AreEqual(hp - 1, engine.Hp, "owner round 17: a strike that is not parried costs one heart");
        }

        [Test]
        public void SoldierChunksAreTapsOrSoldierGuardsAndNeedASoldier()
        {
            PatternDefinition bad = Soldier();
            bad.events = new[] { Tap("a", 0, 1, "object_soldier_toy"), new PatternEventDefinition { id = "h", beat = 2, lane = 1, objectId = "object_baguette", gesture = "hold", length = 2 } };
            Assert.IsNotNull(PatternValidator.Validate(bad), "only a soldier's own strike may be held");
            PatternDefinition guard = Soldier();
            guard.events = new[] { new PatternEventDefinition { id = "h", beat = 1, lane = 1, objectId = "object_soldier_crow", gesture = "hold", length = 2 } };
            Assert.IsNull(PatternValidator.Validate(guard), "round 26: the crow cook's held flame");
            PatternDefinition none = Soldier();
            none.events = new[] { Tap("a", 1, 1, "object_strawberry") };
            Assert.IsNotNull(PatternValidator.Validate(none));
            Assert.IsNull(PatternValidator.Validate(Soldier()));
        }

        [Test]
        public void TheCrowCooksHeldFlameIsAGuardAndHoldingItToTheEndIsTheCounter()
        {
            const string c = "object_soldier_crow";
            var crow = new PatternDefinition { id = "crow", durationBeats = 8, difficultyTier = 0, weight = 1, role = RunEngine.RoleSoldier,
                events = new[] { Tap("a", 1, 0, c), Tap("b", 2, 1, c), new PatternEventDefinition { id = "h", beat = 3, lane = 1, objectId = c, gesture = "hold", length = 2 } } };
            var engine = new RunEngine(new[] { Normal(), crow }, 3, new RunConfig { ChunksPerStage = 8, BossChunks = 0, ItemsPerStage = 0, SoldiersFirstStage = 1, SoldiersPerStage = 2 });
            engine.EnsureScheduled(8 * 7);
            var strikes = engine.Events.Where(e => e.Soldier != null).OrderBy(e => e.Beat).ToList();
            Assert.AreEqual(3, strikes.Count);
            ScheduledEvent flame = strikes[2];
            Assert.IsTrue(flame.Charge && !flame.Boss, "a held soldier strike is a charge outside boss fights too");
            Assert.IsTrue(flame.SoldierFinal && !strikes[0].Charge);
            foreach (ScheduledEvent e in engine.Events.Where(e => e.Beat < flame.Beat).OrderBy(e => e.Beat).ToList()) engine.JudgeTap(e.Lane, e.Beat);
            Assert.AreEqual(ResultKind.HoldHead, engine.JudgeTap(flame.Lane, flame.Beat).Kind);
            Assert.AreEqual(ResultKind.HoldTail, engine.Release(flame.Lane, flame.EndBeat).Kind);
            Assert.AreEqual(4, engine.Hp);
        }

        [Test]
        public void ShippedCrowPatternsMixMovesAndComeMirrored()
        {
            var patterns = ShippedPatterns();
            var crows = patterns.Where(p => p.id.StartsWith("soldier_crow_")).ToList();
            Assert.GreaterOrEqual(crows.Count, 6);
            foreach (PatternDefinition p in crows)
            {
                Assert.IsNull(PatternValidator.Validate(p), p.id);
                Assert.IsTrue(p.events.Any(e => e.gesture == "hold"), p.id + ": every crow chunk has a held flame");
                Assert.IsTrue(p.events.Select(e => e.lane).Distinct().Count() > 1, p.id + ": and moves between lanes");
                Assert.IsTrue(crows.Any(m => m.id == (p.id.EndsWith("_m") ? p.id.Substring(0, p.id.Length - 2) : p.id + "_m")), p.id + ": has its mirror");
            }
        }

        static PatternDefinition[] ShippedPatterns()
        {
            var text = UnityEngine.Resources.Load<UnityEngine.TextAsset>("patterns");
            return UnityEngine.JsonUtility.FromJson<PatternFile>(text.text).patterns;
        }

        [Test]
        public void WithoutSoldierPatternsTheStageCarriesOnNormally()
        {
            var engine = new RunEngine(new[] { Normal() }, 3, new RunConfig { ChunksPerStage = 8, BossChunks = 0, ItemsPerStage = 0, SoldiersFirstStage = 1, SoldiersPerStage = 2 });
            engine.EnsureScheduled(8 * 7);
            Assert.IsFalse(engine.Events.Any(e => e.Soldier != null));
            Assert.IsTrue(engine.Chunks.All(c => c.PatternId == "n"), "soldier chunks fall back to normal patterns");
        }
    }
}
