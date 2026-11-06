using System.Linq;
using NUnit.Framework;
using UnityEngine;
using RhythmSlashRunner.Core;

namespace RhythmSlashRunner.Tests
{
    /// <summary>Round 27: bombs (never cut them) and big melons that split into halves.</summary>
    public sealed class BombSplitTests
    {
        static PatternEventDefinition Tap(string id, float beat, int lane, string obj = "object_strawberry", string from = null)
        {
            return new PatternEventDefinition { id = id, beat = beat, lane = lane, objectId = obj, gesture = "tap", from = from };
        }

        static PatternDefinition Pattern(params PatternEventDefinition[] events)
        {
            return new PatternDefinition { id = "p", durationBeats = 8, difficultyTier = 0, weight = 1, events = events };
        }

        static RunEngine Engine(PatternDefinition p)
        {
            var engine = new RunEngine(new[] { p }, 1, new RunConfig { ChunksPerStage = 8, BossChunks = 0, ItemsPerStage = 0 });
            engine.EnsureScheduled(0);
            return engine;
        }

        // taps in the centre lane on 0 and 1, a bomb there on 2, the next tap on 3
        static PatternDefinition BombPattern() { return Pattern(Tap("a", 0, 1), Tap("b", 1, 1), Tap("x", 2, 1, RunEngine.ObjectBomb), Tap("c", 3, 1), Tap("d", 2, 0)); }

        [Test]
        public void CuttingABombBreaksTheComboButCostsNoHeart()
        {
            RunEngine engine = Engine(BombPattern());
            engine.JudgeTap(1, 0); engine.JudgeTap(1, 1);
            Assert.AreEqual(2, engine.Combo);
            JudgeResult boom = engine.JudgeTap(1, 2);
            Assert.IsTrue(boom.BombHit);
            Assert.AreEqual(0, engine.Combo);
            Assert.AreEqual(4, engine.Hp, "owner round 27: a bomb never costs a heart");
            Assert.AreEqual(1, engine.BombHits);
        }

        [Test]
        public void LettingABombGoByIsADodgeThatKeepsTheCombo()
        {
            RunEngine engine = Engine(BombPattern());
            engine.JudgeTap(1, 0); engine.JudgeTap(1, 1);
            Assert.AreEqual(Rating.Perfect, engine.JudgeTap(0, 2).Rating, "the other lane's target on the bomb's beat");
            int score = engine.Score, combo = engine.Combo;
            JudgeResult dodge = engine.Expire(2.5).Single(r => r.Event != null && r.Event.Bomb);
            Assert.IsTrue(dodge.Dodged);
            Assert.AreEqual(combo, engine.Combo);
            Assert.AreEqual(score + engine.Config.BombDodgeScore, engine.Score);
            Assert.AreEqual(1, engine.BombsDodged);
        }

        [Test]
        public void ATapAwayFromABombIsNeverAnEmptyTap()
        {
            RunEngine engine = Engine(Pattern(Tap("a", 0, 1), Tap("x", 2, 1, RunEngine.ObjectBomb), Tap("c", 4, 1)));
            engine.JudgeTap(1, 0);
            Assert.AreEqual(Rating.Ignored, engine.JudgeTap(2, 2).Rating, "a finger kept off the bomb's lane");
            Assert.AreEqual(1, engine.Combo);
        }

        [Test]
        public void TheBombRulesAreChecked()
        {
            Assert.IsNull(PatternValidator.Validate(BombPattern()));
            Assert.IsNotNull(PatternValidator.Validate(Pattern(Tap("a", 1, 1), Tap("x", 1.5f, 1, RunEngine.ObjectBomb))), "a beat of room in its lane");
            var hold = Pattern(Tap("a", 0, 1), Tap("x", 2, 1, RunEngine.ObjectBomb));
            hold.events[1].gesture = "hold"; hold.events[1].length = 1;
            Assert.IsNotNull(PatternValidator.Validate(hold), "a bomb is a tap");
            var boss = BombPattern(); boss.role = RunEngine.RoleBoss;
            Assert.IsNotNull(PatternValidator.Validate(boss), "no bombs in boss fights");
        }

        static PatternDefinition SplitPattern()
        {
            return Pattern(Tap("m", 1, 1, RunEngine.ObjectSplit), Tap("l", 2, 0, RunEngine.ObjectHalf, "m"), Tap("r", 3, 2, RunEngine.ObjectHalf, "m"), Tap("t", 5, 2));
        }

        [Test]
        public void ASplitMelonsHalvesComeFromWhereItWasCut()
        {
            RunEngine engine = Engine(SplitPattern());
            ScheduledEvent whole = engine.Events.Single(e => e.Splits && e.Beat < 8);
            var halves = engine.Events.Where(e => !double.IsNaN(e.SplitFromBeat) && e.Beat < 8).ToList();
            Assert.AreEqual(2, halves.Count);
            foreach (ScheduledEvent h in halves) { Assert.AreEqual(whole.Beat, h.SplitFromBeat); Assert.AreEqual(whole.Lane, h.SplitFromLane); }
            // halves are ordinary targets
            Assert.AreEqual(Rating.Perfect, engine.JudgeTap(1, 1).Rating);
            Assert.AreEqual(Rating.Perfect, engine.JudgeTap(0, 2).Rating);
            Assert.AreEqual(Rating.Perfect, engine.JudgeTap(2, 3).Rating);
        }

        [Test]
        public void SplitRulesAreChecked()
        {
            Assert.IsNull(PatternValidator.Validate(SplitPattern()));
            Assert.IsNotNull(PatternValidator.Validate(Pattern(Tap("m", 1, 1, RunEngine.ObjectSplit), Tap("t", 3, 1))), "a big melon needs halves");
            Assert.IsNotNull(PatternValidator.Validate(Pattern(Tap("m", 1, 1, RunEngine.ObjectSplit), Tap("l", 5, 0, RunEngine.ObjectHalf, "m"))), "halves come one or two beats later");
            Assert.IsNotNull(PatternValidator.Validate(Pattern(Tap("m", 1, 1), Tap("l", 2, 0, RunEngine.ObjectHalf, "m"))), "halves come from a big melon");
        }

        [Test]
        public void TheGiantStompsBombsFlat()
        {
            var p = Pattern(Tap("a", 0, 1), Tap("b", 2, 1), Tap("x", 3, 1, RunEngine.ObjectBomb), Tap("c", 4, 1), Tap("d", 6, 1));
            var engine = new RunEngine(new[] { p }, 1, new RunConfig { ChunksPerStage = 8, BossChunks = 0, ItemsPerStage = 0 });
            engine.EnsureScheduled(0);
            ScheduledEvent first = engine.Events.First();
            first.Item = ItemKind.Giant; first.ObjectId = "object_item_giant";
            engine.JudgeTap(1, 0);
            engine.JudgeTap(1, 2);
            int combo = engine.Combo;
            Assert.AreEqual(Rating.Ignored, engine.JudgeTap(1, 3).Rating, "a giant never sets a bomb off");
            JudgeResult crushed = engine.Expire(3.5).Single(r => r.Event != null && r.Event.Bomb);
            Assert.IsTrue(crushed.Crushed);
            Assert.AreEqual(combo, engine.Combo);
        }

        [Test]
        public void ShippedBombsAndSplitsAreValidAndBombsWaitForTierTwo()
        {
            PatternDefinition[] patterns = JsonUtility.FromJson<PatternFile>(Resources.Load<TextAsset>("patterns").text).patterns;
            Assert.IsTrue(patterns.Any(p => p.events.Any(e => e.objectId == RunEngine.ObjectSplit)));
            foreach (PatternDefinition p in patterns.Where(p => p.events.Any(e => e.objectId == RunEngine.ObjectBomb)))
            {
                Assert.IsNull(PatternValidator.Validate(p), p.id);
                Assert.GreaterOrEqual(p.difficultyTier, 2, p.id + ": bombs only once the player is warmed up");
            }
        }
    }
}
