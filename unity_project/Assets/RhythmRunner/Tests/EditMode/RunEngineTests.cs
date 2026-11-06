using NUnit.Framework;
using RhythmSlashRunner.Core;

namespace RhythmSlashRunner.Tests
{
    public sealed class RunEngineTests
    {
        [TestCase(80, Rating.Perfect)]
        [TestCase(-80, Rating.Perfect)]
        [TestCase(81, Rating.Good)]
        [TestCase(160, Rating.Good)]
        [TestCase(161, Rating.Miss)]
        public void TimingBoundaries(double errorMs, Rating expected)
        {
            Assert.AreEqual(expected, RunEngine.Classify(errorMs));
        }

        [Test]
        public void EventCannotResolveTwice()
        {
            RunEngine engine = NewEngine();
            engine.EnsureScheduled(0, 0);
            ScheduledEvent item = engine.Events[0];
            Assert.AreEqual(Rating.Perfect, engine.JudgeTap(item.Lane, item.Beat).Rating);
            Assert.AreEqual(Rating.Ignored, engine.Resolve(item, Rating.Perfect).Rating);
            Assert.AreEqual(1, engine.PerfectCount);
        }

        [Test]
        public void FourMissesEndRunAndBlockLaterScore()
        {
            RunEngine engine = NewEngine();
            engine.EnsureScheduled(0, 0);
            for (int i = 0; i < 3; i++) engine.Resolve(engine.Events[i], Rating.Miss);
            Assert.IsFalse(engine.Ended, "HP 4 since round 2");
            engine.Resolve(engine.Events[3], Rating.Miss);
            Assert.IsTrue(engine.Ended);
            int score = engine.Score;
            Assert.AreEqual(Rating.Ignored, engine.Resolve(engine.Events[4], Rating.Perfect).Rating);
            Assert.AreEqual(score, engine.Score);
        }

        [Test]
        public void EmptyTapBreaksComboWithoutDamage()
        {
            RunEngine engine = NewEngine();
            engine.EnsureScheduled(0, 0);
            ScheduledEvent item = engine.Events[0];
            engine.JudgeTap(item.Lane, item.Beat);
            Assert.Greater(engine.Combo, 0);
            Assert.AreEqual(Rating.Empty, engine.JudgeTap(2, 999).Rating);
            Assert.AreEqual(0, engine.Combo);
            Assert.AreEqual(4, engine.Hp);
        }

        [Test]
        public void WrongLaneConsumesOneEventAndDamagesOnlyOnce()
        {
            RunEngine engine = NewEngine();
            engine.EnsureScheduled(0, 0);
            ScheduledEvent item = engine.Events[0];
            int wrongLane = (item.Lane + 1) % 3;
            JudgeResult first = engine.JudgeTap(wrongLane, item.Beat);
            JudgeResult second = engine.Resolve(item, Rating.Miss);
            Assert.AreEqual(Rating.Miss, first.Rating);
            Assert.AreEqual("wrong_lane", first.Reason);
            Assert.AreEqual(Rating.Ignored, second.Rating);
            Assert.AreEqual(3, engine.Hp);
        }

        static RunEngine NewEngine()
        {
            PatternDefinition pattern = new PatternDefinition
            {
                id = "test",
                durationBeats = 8,
                difficultyTier = 0,
                weight = 1,
                events = new[]
                {
                    E("a",0,1), E("b",2,1), E("c",4,1), E("d",6,1)
                }
            };
            return new RunEngine(new[] { pattern }, 123);
        }

        static PatternEventDefinition E(string id, float beat, int lane)
        {
            return new PatternEventDefinition { id = id, beat = beat, lane = lane, objectId = "object_strawberry", gesture = "tap" };
        }
    }
}

