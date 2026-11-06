using System;
using NUnit.Framework;
using UnityEngine;
using RhythmSlashRunner.Core;

namespace RhythmSlashRunner.Tests
{
    /// <summary>Round 11: slides — a held object snaking between lanes that the finger follows.</summary>
    public sealed class SlideTests
    {
        static PatternEventDefinition Slide(string id, float beat, int lane, float length, int[] path, float[] turns)
        {
            return new PatternEventDefinition { id = id, beat = beat, lane = lane, objectId = "object_slide", gesture = "slide", length = length, path = path, turns = turns };
        }

        /// <summary>One slide R → C (beat 1) → L (beat 3), held from beat 0 to 4.</summary>
        static RunEngine Engine(out ScheduledEvent slide)
        {
            var pattern = new PatternDefinition
            {
                id = "s", durationBeats = 8, difficultyTier = 0, weight = 1,
                events = new[] { Slide("a", 0, 2, 4, new[] { 1, 0 }, new[] { 1f, 3f }), new PatternEventDefinition { id = "b", beat = 6, lane = 0, objectId = "object_strawberry", gesture = "tap" } },
            };
            var engine = new RunEngine(new[] { pattern }, 1, new RunConfig { ChunksPerStage = 8, BossChunks = 0, ItemsPerStage = 0 });
            engine.EnsureScheduled(0);
            slide = engine.Events[0];
            Assert.IsTrue(slide.IsSlide);
            Assert.AreEqual(ResultKind.HoldHead, engine.JudgeTap(2, 0).Kind);
            return engine;
        }

        [Test]
        public void FollowingThePathCompletesTheSlide()
        {
            ScheduledEvent slide;
            RunEngine engine = Engine(out slide);
            Assert.AreEqual(2, slide.LaneAt(.5)); Assert.AreEqual(1, slide.LaneAt(1)); Assert.AreEqual(0, slide.LaneAt(3.2)); Assert.AreEqual(0, slide.FinalLane);
            for (double b = 0; b < 4; b += .05) Assert.AreEqual(Rating.Ignored, engine.Steer(slide.LaneAt(b), b).Rating, "on the path at " + b);
            JudgeResult end = engine.Expire(4).Find(r => r.Event == slide);
            Assert.IsNotNull(end);
            Assert.AreEqual(ResultKind.HoldTail, end.Kind);
            Assert.AreEqual(1, engine.HoldsCompleted);
        }

        [Test]
        public void StayingInTheOldLaneAfterATurnBreaksItWithoutLosingAHeart()
        {
            ScheduledEvent slide;
            RunEngine engine = Engine(out slide);
            JudgeResult broke = null;
            double at = 0;
            for (double b = 0; b < 2 && broke == null; b += .02) { JudgeResult r = engine.Steer(2, b); if (r.Rating != Rating.Ignored) { broke = r; at = b; } }
            Assert.IsNotNull(broke);
            Assert.AreEqual(ResultKind.HoldBreak, broke.Kind);
            Assert.AreEqual("slid_off", broke.Reason);
            // the turn is at beat 1: both lanes count for 150 ms (0.3 beat at 120 BPM) after it, then 150 ms more off the path
            Assert.Greater(engine.Tempo.Ms(1, at), 300 - 1, "not before the grace runs out");
            Assert.Less(engine.Tempo.Ms(1, at), 300 + 25, "but right after it");
            Assert.AreEqual(4, engine.Hp, "a broken slide costs no heart (like an early release)");
            Assert.AreEqual(0, engine.Combo);
            Assert.IsTrue(slide.Resolved);
        }

        [Test]
        public void AroundATurnBothLanesCountAndAShortSlipIsForgiven()
        {
            ScheduledEvent slide;
            RunEngine engine = Engine(out slide);
            Assert.AreEqual(Rating.Ignored, engine.Steer(1, .8).Rating, "moving a little early is fine");
            Assert.AreEqual(Rating.Ignored, engine.Steer(2, 1.2).Rating, "moving a little late is fine");
            Assert.AreEqual(Rating.Ignored, engine.Steer(0, 1.6).Rating, "a slip starts the grace timer");
            Assert.AreEqual(Rating.Ignored, engine.Steer(0, 1.8).Rating, "100 ms off: still forgiven");
            Assert.AreEqual(Rating.Ignored, engine.Steer(1, 1.9).Rating, "back on the path resets it");
            Assert.AreEqual(Rating.Ignored, engine.Steer(0, 2.0).Rating);
            Assert.AreEqual(ResultKind.HoldBreak, engine.Steer(0, 2.4).Kind, "200 ms off breaks it");
        }

        [Test]
        public void LettingGoNearTheEndCompletesItWhereverTheFingerIs()
        {
            ScheduledEvent slide;
            RunEngine engine = Engine(out slide);
            for (double b = 0; b < 3.7; b += .05) engine.Steer(slide.LaneAt(b), b);
            Assert.AreEqual(ResultKind.HoldTail, engine.Release(0, 3.7).Kind, "released in the lane it ended in, 150 ms early");

            RunEngine lifted = Engine(out slide);
            for (double b = 0; b < 3.6; b += .05) lifted.Steer(slide.LaneAt(b), b);
            Assert.AreEqual(ResultKind.HoldTail, lifted.Steer(-1, 3.6).Kind, "no finger 200 ms before the end still completes");

            RunEngine early = Engine(out slide);
            JudgeResult r = early.Release(2, .5);
            Assert.AreEqual(ResultKind.HoldBreak, r.Kind, "letting go early breaks it");
        }

        [Test]
        public void KeyboardPlayersGetMoreSlackWhenSwitchingKeys()
        {
            ScheduledEvent slide;
            RunEngine engine = Engine(out slide);
            // at 120 BPM a beat is 500 ms: off the path from beat 2.0 to 2.5 = 250 ms
            Assert.AreEqual(Rating.Ignored, engine.Steer(-1, 2.0, 150).Rating, "keys released between two lanes");
            Assert.AreEqual(Rating.Ignored, engine.Steer(-1, 2.5, 150).Rating, "250 ms: within the keyboard grace (150 + 150)");
            Assert.AreEqual(Rating.Ignored, engine.Steer(1, 2.6, 150).Rating, "the next key is down: on the path again");
            RunEngine touch = Engine(out slide);
            touch.Steer(-1, 2.0);
            Assert.AreEqual(ResultKind.HoldBreak, touch.Steer(-1, 2.5).Kind, "a finger gets the normal 150 ms");
        }

        [Test]
        public void TheValidatorKeepsSlidesReadable()
        {
            Func<PatternEventDefinition, int, string> check = (e, tier) => PatternValidator.Validate(new PatternDefinition { id = "v", durationBeats = 8, difficultyTier = tier, weight = 1, events = new[] { e } });
            Assert.IsNull(check(Slide("a", 0, 2, 4, new[] { 1, 0, 1 }, new[] { 1f, 2f, 3f }), 4));
            StringAssert.Contains("whole beats", check(Slide("a", 0, 2, 3, new[] { 1 }, new[] { 1.5f }), 4));
            StringAssert.Contains("whole beats", check(Slide("a", 0, 2, 4, new[] { 1, 0 }, new[] { 1f, 1f }), 4));
            StringAssert.Contains("change", check(Slide("a", 0, 2, 3, new[] { 2 }, new[] { 1f }), 4));
            StringAssert.Contains("tier 4", check(Slide("a", 0, 0, 3, new[] { 2 }, new[] { 1f }), 3));
            Assert.IsNull(check(Slide("a", 0, 0, 3, new[] { 2 }, new[] { 1f }), 4));
        }

        [Test]
        public void SlidesAreRetiredAndTheSecondLongObjectIsStraight()
        {
            // round 27 (owner): no snaking objects for now; the slide rules stay for a later return
            PatternDefinition[] patterns = JsonUtility.FromJson<PatternFile>(Resources.Load<TextAsset>("patterns").text).patterns;
            int long2 = 0;
            foreach (PatternDefinition p in patterns)
            {
                foreach (PatternEventDefinition e in p.events)
                {
                    Assert.AreNotEqual("slide", e.gesture, p.id);
                    if (e.objectId == "object_slide") { long2++; Assert.AreEqual("hold", e.gesture); }
                }
                Assert.IsNull(PatternValidator.Validate(p), p.id);
            }
            Assert.GreaterOrEqual(long2, 5);
            var catalog = JsonUtility.FromJson<ContentCatalog>(Resources.Load<TextAsset>("content_catalog").text);
            int ramen = 0;
            foreach (CatalogItem stage in catalog.stages)
            {
                string kind = CatalogQueries.Kind(CatalogQueries.DisplayObject(stage, "object_slide"));
                Assert.AreNotEqual("candy", kind, stage.id + ": the long candy is retired");
                if (kind == "ramen") ramen++;
            }
            Assert.Greater(ramen, 0, "straight ramen stays");
        }
    }
}
