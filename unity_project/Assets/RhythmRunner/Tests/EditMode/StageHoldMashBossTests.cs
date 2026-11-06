using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using RhythmSlashRunner.Core;

namespace RhythmSlashRunner.Tests
{
    /// <summary>Round 2 rules: tempo steps per stage, holds, mashes, boss fights, scenery shuffle.</summary>
    public sealed class StageHoldMashBossTests
    {
        static PatternEventDefinition Tap(string id, float beat, int lane, string obj = "object_strawberry") { return new PatternEventDefinition { id = id, beat = beat, lane = lane, objectId = obj, gesture = "tap" }; }
        static PatternEventDefinition Hold(string id, float beat, int lane, float length) { return new PatternEventDefinition { id = id, beat = beat, lane = lane, objectId = "object_hold", gesture = "hold", length = length }; }
        static PatternEventDefinition Mash(string id, float beat, float length) { return new PatternEventDefinition { id = id, beat = beat, lane = 1, objectId = "object_mash", gesture = "mash", length = length }; }

        static PatternDefinition Pattern(string id, string role, params PatternEventDefinition[] events)
        {
            return new PatternDefinition { id = id, durationBeats = 8, difficultyTier = 0, weight = 1, role = role, events = events };
        }

        static RunConfig ShortStages()
        {
            return new RunConfig { ChunksPerStage = 4, BossChunks = 2, StageBpm = new[] { 120, 150 }, ThemeCount = 6 };
        }

        static PatternDefinition[] WithBoss(PatternDefinition normal)
        {
            return new[]
            {
                normal,
                Pattern("volley", RunEngine.RoleBoss, Tap("a", 0, 1, "object_boss_shot"), Tap("b", 2, 1, "object_boss_shot"), Tap("c", 4, 1, "object_boss_shot"), Tap("d", 6, 1, "object_boss_shot")),
                Pattern("finish", RunEngine.RoleBossFinish, Tap("a", 0, 1, "object_boss_shot"), Mash("m", 2, 4)),
            };
        }

        // ------------------------------------------------------------------ tempo

        [Test]
        public void TempoMapKeepsPastBeatsWhenTheTempoChanges()
        {
            var tempo = new TempoMap(120);
            double before = tempo.SecondsAt(10);
            tempo.Add(16, 150);
            Assert.AreEqual(before, tempo.SecondsAt(10), 1e-9);
            Assert.AreEqual(8.0, tempo.SecondsAt(16), 1e-9);
            Assert.AreEqual(8.0 + 0.4, tempo.SecondsAt(17), 1e-9);
            Assert.AreEqual(17.0, tempo.BeatAt(8.4), 1e-9);
            Assert.AreEqual(-1.0, tempo.BeatAt(-.5), 1e-9, "count-in uses the first tempo");
            Assert.Throws<ArgumentException>(() => tempo.Add(8, 130));
        }

        [Test]
        public void EveryStageStartsFasterAndTheWindowsStayInMilliseconds()
        {
            var config = ShortStages();
            var engine = new RunEngine(WithBoss(Pattern("p", null, Tap("a", 0, 1), Tap("b", 4, 1))), 1, config);
            engine.EnsureScheduled(40);
            Assert.AreEqual(120, engine.Tempo.BpmAt(31.9), 1e-9);
            Assert.AreEqual(150, engine.Tempo.BpmAt(32), 1e-9);
            Assert.AreEqual(1, engine.StageAt(33));
            // a tap 100 ms late at 150 BPM is GOOD (window is 80/160 ms, not beats)
            ScheduledEvent e = null;
            foreach (ScheduledEvent x in engine.Events) if (x.Beat >= 32 && x.Gesture == Gesture.Tap && e == null) e = x;
            Assert.IsNotNull(e);
            double late = engine.Tempo.BeatAfterMs(e.Beat, 100);
            JudgeResult r = engine.JudgeTap(e.Lane, late);
            Assert.AreEqual(Rating.Good, r.Rating);
            Assert.AreEqual(100, r.ErrorMs, .5);
        }

        [Test]
        public void TheFirstChunkOfAFasterStageIsABreather()
        {
            var hard = Pattern("hard", null, Hold("h", 0, 1, 2), Tap("a", 4, 1), Tap("b", 5, 1), Tap("c", 6, 1));
            var easy = Pattern("easy", null, Tap("a", 0, 1), Tap("b", 4, 1));
            hard.difficultyTier = 0;
            var config = ShortStages();
            var patterns = WithBoss(hard);
            Array.Resize(ref patterns, patterns.Length + 1);
            patterns[patterns.Length - 1] = easy;
            for (int seed = 1; seed <= 20; seed++)
            {
                var engine = new RunEngine(patterns, seed, config);
                engine.EnsureScheduled(40);
                foreach (ChunkInfo c in engine.Chunks) if (c.Index == 4) Assert.AreEqual("easy", c.PatternId, "seed " + seed);
            }
        }

        [Test]
        public void SceneryShowsEveryThemeBeforeRepeatingAndNeverTwiceInARow()
        {
            var engine = new RunEngine(WithBoss(Pattern("p", null, Tap("a", 0, 1))), 77, ShortStages());
            Assert.AreEqual(0, engine.ThemeForStage(0));
            var firstCycle = new HashSet<int>();
            for (int s = 1; s <= 5; s++) firstCycle.Add(engine.ThemeForStage(s));
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3, 4, 5 }, firstCycle);
            for (int s = 1; s < 60; s++) Assert.AreNotEqual(engine.ThemeForStage(s - 1), engine.ThemeForStage(s), "stage " + s);
            var again = new RunEngine(WithBoss(Pattern("p", null, Tap("a", 0, 1))), 77, ShortStages());
            for (int s = 0; s < 20; s++) Assert.AreEqual(engine.ThemeForStage(s), again.ThemeForStage(s), "same seed, same scenery");
        }

        // ------------------------------------------------------------------ holds

        static RunEngine HoldEngine() { return new RunEngine(new[] { Pattern("h", null, Hold("h", 1, 1, 2), Tap("t", 5, 1)) }, 1); }

        [Test]
        public void HoldHeldToItsEndScoresHeadAndTail()
        {
            RunEngine engine = HoldEngine();
            engine.EnsureScheduled(0);
            JudgeResult head = engine.JudgeTap(1, 1);
            Assert.AreEqual(ResultKind.HoldHead, head.Kind);
            Assert.AreEqual(1, engine.Combo);
            Assert.AreEqual(Rating.Ignored, engine.JudgeTap(0, 2).Rating, "a stray finger while holding never breaks the combo");
            Assert.AreEqual(1, engine.Combo);
            List<JudgeResult> done = engine.Expire(3.0);
            Assert.AreEqual(1, done.Count);
            Assert.AreEqual(ResultKind.HoldTail, done[0].Kind);
            Assert.AreEqual(2, engine.Combo);
            Assert.AreEqual(1, engine.HoldsCompleted);
        }

        [Test]
        public void ReleasingNearTheEndCountsReleasingEarlyBreaksWithoutDamage()
        {
            RunEngine a = HoldEngine();
            a.EnsureScheduled(0);
            a.JudgeTap(1, 1);
            JudgeResult ok = a.Release(1, a.Tempo.BeatAfterMs(3, -200)); // 200 ms early: within the 250 ms leniency
            Assert.AreEqual(ResultKind.HoldTail, ok.Kind);
            Assert.AreEqual(2, a.Combo);

            RunEngine b = HoldEngine();
            b.EnsureScheduled(0);
            b.JudgeTap(1, 1);
            JudgeResult broken = b.Release(1, 2);
            Assert.AreEqual(ResultKind.HoldBreak, broken.Kind);
            Assert.AreEqual(0, b.Combo);
            Assert.AreEqual(4, b.Hp, "a broken hold costs no heart");
            Assert.AreEqual(0, b.MissCount);
        }

        [Test]
        public void AnUnpressedHoldIsOneMiss()
        {
            RunEngine engine = HoldEngine();
            engine.EnsureScheduled(0);
            List<JudgeResult> results = engine.Expire(1.5);
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(Rating.Miss, results[0].Rating);
            Assert.AreEqual(3, engine.Hp);
            Assert.AreEqual(0, engine.Expire(3.5).Count, "the tail is not judged again");
        }

        // ------------------------------------------------------------------ mashes

        static RunEngine MashEngine() { return new RunEngine(new[] { Pattern("m", null, Tap("t", 0, 1), Mash("m", 2, 3)) }, 1); }

        [TestCase(6, Rating.Perfect)]
        [TestCase(3, Rating.Good)]
        [TestCase(2, Rating.Miss)]
        public void MashIsRatedByHitsAgainstTwoPerBeat(int hits, Rating expected)
        {
            RunEngine engine = MashEngine();
            engine.EnsureScheduled(0);
            engine.JudgeTap(1, 0);
            JudgeResult last = null;
            for (int i = 0; i < hits; i++)
            {
                last = engine.JudgeTap(i % 3, 2.1 + i * .4);
                if (i == hits - 1 && expected == Rating.Perfect) break; // the hit that reaches MAX breaks it at once
                Assert.AreEqual(Rating.Tick, last.Rating);
                Assert.AreEqual(ResultKind.MashTap, last.Kind);
            }
            if (expected == Rating.Perfect)
            {
                // round 29 (owner): it breaks the moment MAX is reached; the leftover presses are never empty taps
                Assert.AreEqual(Rating.Perfect, last.Rating);
                Assert.AreEqual(ResultKind.MashEnd, last.Kind);
                Assert.AreEqual(2, engine.Combo);
                Assert.AreEqual(Rating.Ignored, engine.JudgeTap(0, 4.5).Rating, "mashing on after the break costs nothing");
                Assert.AreEqual(2, engine.Combo);
                return;
            }
            Assert.AreEqual(1, engine.Combo, "mash hits never touch the combo on their own");
            List<JudgeResult> end = engine.Expire(5.1);
            Assert.AreEqual(1, end.Count);
            Assert.AreEqual(expected, end[0].Rating);
            Assert.AreEqual(ResultKind.MashEnd, end[0].Kind);
            Assert.AreEqual(expected == Rating.Miss ? 3 : 4, engine.Hp);
        }

        [Test]
        public void TapsRightAfterAMashAreNotEmptyTaps()
        {
            RunEngine engine = MashEngine();
            engine.EnsureScheduled(0);
            engine.JudgeTap(1, 0);
            for (int i = 0; i < 6; i++) engine.JudgeTap(1, 2.1 + i * .4);
            engine.Expire(5.1);
            int combo = engine.Combo;
            Assert.AreEqual(Rating.Ignored, engine.JudgeTap(1, engine.Tempo.BeatAfterMs(5, 200)).Rating);
            Assert.AreEqual(combo, engine.Combo);
        }

        // ------------------------------------------------------------------ first stage & tutorial (round 5)

        static RunConfig Shipped() { return RunConfig.FromFile(JsonUtility.FromJson<GameConfigFile>(Resources.Load<TextAsset>("game_config").text)); }
        static PatternDefinition[] ShippedPatterns() { return JsonUtility.FromJson<PatternFile>(Resources.Load<TextAsset>("patterns").text).patterns; }

        [Test]
        public void TheFirstBossComesWithinAMinute()
        {
            var engine = new RunEngine(ShippedPatterns(), 1, Shipped());
            engine.EnsureScheduled(300);
            Assert.Less(engine.Tempo.SecondsAt(engine.BossStartBeat(0)), 50, "first boss fight starts before 50 s");
            Assert.LessOrEqual(engine.Tempo.SecondsAt(engine.StageStartBeat(1)), 60, "stage 2 starts within a minute");
            // later stages keep the full length
            Assert.AreEqual(engine.StageLengthBeats(1), engine.StageLengthBeats(2));
            Assert.Greater(engine.StageLengthBeats(1), engine.StageLengthBeats(0));
            Assert.AreEqual(1, engine.StageAt(engine.StageStartBeat(1)));
            Assert.AreEqual(0, engine.StageAt(engine.StageStartBeat(1) - .01));
            Assert.AreEqual(130, engine.Tempo.BpmAt(engine.StageStartBeat(1)), 1e-9);
            // owner round 9: the second boss comes within about a minute and a half more
            Assert.Less(engine.Tempo.SecondsAt(engine.BossStartBeat(1)), 110, "second boss fight starts before 110 s");
        }

        [Test]
        public void TheTutorialOpensTheFirstRunAndItsMissesCostNoHeart()
        {
            var engine = new RunEngine(ShippedPatterns(), 1, Shipped());
            engine.EnableTutorial("tutorial_tap", "tutorial_hold", "tutorial_mash");
            engine.EnsureScheduled(0);
            Assert.AreEqual("tutorial_tap", engine.Chunks[0].PatternId);
            Assert.AreEqual("tutorial_hold", engine.Chunks[1].PatternId);
            Assert.AreEqual("tutorial_mash", engine.Chunks[2].PatternId);
            List<JudgeResult> misses = engine.Expire(23.9);
            Assert.Greater(misses.Count, 3);
            Assert.AreEqual(4, engine.Hp, "practice misses are free");
            foreach (ScheduledEvent e in engine.Events) if (e.Beat < 24) Assert.AreEqual(ItemKind.None, e.Item, "no items while practising");
            var normal = new RunEngine(ShippedPatterns(), 1, Shipped());
            normal.EnsureScheduled(0);
            Assert.AreNotEqual("tutorial_tap", normal.Chunks[0].PatternId, "later runs start normally");
        }

        // ------------------------------------------------------------------ items

        static RunEngine ItemEngine(int seed)
        {
            var config = new RunConfig { ChunksPerStage = 8, BossChunks = 0, ItemsPerStage = 6 };
            var p = Pattern("p", null, Tap("a", 0, 1), Tap("b", 2, 1), Tap("c", 4, 1), Tap("d", 6, 1));
            return new RunEngine(new[] { p }, seed, config);
        }

        static RunEngine EngineWithItem(ItemKind kind, out ScheduledEvent item)
        {
            for (int seed = 1; seed < 200; seed++)
            {
                RunEngine engine = ItemEngine(seed);
                engine.EnsureScheduled(40);
                foreach (ScheduledEvent e in engine.Events) if (e.Item == kind) { item = e; return engine; }
            }
            item = null;
            Assert.Fail("no " + kind);
            return null;
        }

        [Test]
        public void ItemsReplaceATapAndNeverAppearInBreathersOrBossFights()
        {
            var config = new RunConfig { ChunksPerStage = 8, BossChunks = 3, ItemsPerStage = 2 };
            var engine = new RunEngine(WithBoss(Pattern("p", null, Tap("a", 0, 1), Tap("b", 2, 1), Tap("c", 4, 1), Tap("d", 6, 1))), 5, config);
            engine.EnsureScheduled(200);
            int items = 0;
            foreach (ScheduledEvent e in engine.Events)
            {
                if (e.Item == ItemKind.None) continue;
                items++;
                Assert.IsFalse(e.Boss);
                Assert.AreEqual(Gesture.Tap, e.Gesture);
                int inStage = (int)(e.Beat / 8) % 8;
                Assert.GreaterOrEqual(inStage, 2, "not in the first two chunks of a stage");
                Assert.Less(inStage, 5, "not in the boss fight");
            }
            Assert.Greater(items, 0);
        }

        [Test]
        public void AMissedItemCostsNothing()
        {
            ScheduledEvent item;
            RunEngine engine = EngineWithItem(ItemKind.Booster, out item);
            foreach (ScheduledEvent e in engine.Events) if (e.Beat < item.Beat) engine.JudgeTap(e.Lane, e.Beat);
            int combo = engine.Combo;
            engine.Expire(item.Beat + .5);
            Assert.IsTrue(item.Resolved);
            Assert.AreEqual(4, engine.Hp);
            Assert.AreEqual(combo, engine.Combo);
        }

        [Test]
        public void BoosterRushCutsTheNextFourBeatsOneSixteenthApart()
        {
            ScheduledEvent item;
            RunEngine engine = EngineWithItem(ItemKind.Booster, out item);
            foreach (ScheduledEvent e in engine.Events) if (e.Beat < item.Beat) engine.Resolve(e, Rating.Perfect);
            JudgeResult r = engine.JudgeTap(item.Lane, item.Beat);
            Assert.AreEqual(ItemKind.Booster, r.ItemGot);
            Assert.AreEqual(Rating.Perfect, r.Rating, "an item is also a normal hit");
            double b = item.Beat;
            var expected = new List<ScheduledEvent>();
            foreach (ScheduledEvent e in engine.Events) if (e.Beat > b && e.Beat <= b + 4) expected.Add(e);
            Assert.Greater(expected.Count, 0);
            Assert.AreEqual(0, engine.Expire(b + .2).Count, "the first cut comes a 16th after the pickup");
            var cut = new List<JudgeResult>();
            for (double beat = b + .25; beat <= b + .25 * expected.Count + 1e-9; beat += .25)
            {
                List<JudgeResult> step = engine.Expire(beat).FindAll(x => x.Rush);
                Assert.AreEqual(1, step.Count, "one target per 16th");
                cut.AddRange(step);
            }
            foreach (JudgeResult c in cut) { Assert.IsTrue(c.Rush); Assert.AreEqual(Rating.Perfect, c.Rating); }
            foreach (ScheduledEvent e in expected) Assert.IsTrue(e.Resolved);
            Assert.GreaterOrEqual(engine.Snapshot(b + 1).Distance, (int)Math.Floor(b + 1 + 20), "+20 m at once");
            Assert.IsTrue(engine.Snapshot(b + .3).Boost);
            Assert.IsFalse(engine.Snapshot(b + 4).Boost);
        }

        [Test]
        public void BarrierTakesOneMissInsteadOfAHeart()
        {
            ScheduledEvent item;
            RunEngine engine = EngineWithItem(ItemKind.Barrier, out item);
            engine.JudgeTap(item.Lane, item.Beat);
            Assert.AreEqual(1, engine.Shields);
            ScheduledEvent next = null;
            foreach (ScheduledEvent e in engine.Events) if (!e.Resolved && e.Item == ItemKind.None && e.Beat > item.Beat && next == null) next = e;
            JudgeResult miss = engine.Resolve(next, Rating.Miss);
            Assert.IsTrue(miss.ShieldBlocked);
            Assert.AreEqual(4, engine.Hp);
            Assert.AreEqual(0, engine.Shields);
            Assert.AreEqual(0, engine.Combo, "the combo still breaks");
        }

        static RunEngine HeartEngine(out ScheduledEvent heart)
        {
            var config = new RunConfig { ChunksPerStage = 8, BossChunks = 0, ItemsPerStage = 0, HeartsPerStage = 1 };
            var engine = new RunEngine(new[] { Pattern("p", null, Tap("a", 0, 1), Tap("b", 2, 1), Tap("c", 4, 1), Tap("d", 6, 1)) }, 3, config);
            engine.EnsureScheduled(70);
            heart = null;
            foreach (ScheduledEvent e in engine.Events) if (e.Item == ItemKind.Heart && heart == null) heart = e;
            return engine;
        }

        [Test]
        public void EveryStageHasOneHeartCapsuleAroundItsMiddle()
        {
            ScheduledEvent heart;
            RunEngine engine = HeartEngine(out heart);
            Assert.IsNotNull(heart);
            int hearts = 0;
            foreach (ScheduledEvent e in engine.Events) if (e.Item == ItemKind.Heart) { hearts++; Assert.AreEqual(Gesture.Tap, e.Gesture); }
            Assert.AreEqual(1, hearts, "one per stage (the first 64 beats are one stage)");
            int inStage = (int)(heart.Beat / 8) % 8;
            Assert.GreaterOrEqual(inStage, 3); Assert.LessOrEqual(inStage, 5);
            Assert.AreEqual("object_item_heart", heart.ObjectId);
        }

        [Test]
        public void AHeartCapsuleRestoresAHeartOrPaysCoinsWhenFull()
        {
            ScheduledEvent heart;
            RunEngine engine = HeartEngine(out heart);
            ScheduledEvent first = engine.Events[0];
            engine.Resolve(first, Rating.Miss);
            Assert.AreEqual(3, engine.Hp);
            foreach (ScheduledEvent e in engine.Events) if (!e.Resolved && e.Beat < heart.Beat) engine.Resolve(e, Rating.Perfect);
            JudgeResult r = engine.JudgeTap(heart.Lane, heart.Beat);
            Assert.AreEqual(ItemKind.Heart, r.ItemGot);
            Assert.IsTrue(r.Healed);
            Assert.AreEqual(4, engine.Hp);
            Assert.AreEqual(4, r.Hp);

            RunEngine full = HeartEngine(out heart);
            foreach (ScheduledEvent e in full.Events) if (!e.Resolved && e.Beat < heart.Beat) full.Resolve(e, Rating.Perfect);
            int units = full.CoinUnits;
            JudgeResult f = full.JudgeTap(heart.Lane, heart.Beat);
            Assert.IsFalse(f.Healed);
            Assert.AreEqual(4, full.Hp, "never above the maximum");
            Assert.AreEqual(units + 1 + new RunConfig().HeartCoinUnits, full.CoinUnits, "the cut itself (1) plus the payout");
        }

        // ------------------------------------------------------------------ bosses

        [Test]
        public void GoldShotsAreCriticalAndAGuardedChargeHitsHard()
        {
            var config = new RunConfig { ChunksPerStage = 6, BossChunks = 3, ItemsPerStage = 0 };
            var patterns = new[]
            {
                Pattern("p", null, Tap("a", 0, 1)),
                Pattern("volley", RunEngine.RoleBoss, Tap("a", 0, 1, "object_boss_gold"), Tap("b", 2, 1, "object_boss_shot")),
                Pattern("charge", RunEngine.RoleBossCharge, new PatternEventDefinition { id = "c", beat = 1, lane = 1, objectId = "object_boss_charge", gesture = "hold", length = 2 }),
                Pattern("finish", RunEngine.RoleBossFinish, Tap("a", 0, 1, "object_boss_shot"), Mash("m", 2, 4)),
            };
            var engine = new RunEngine(patterns, 1, config);
            engine.EnsureScheduled(30);
            engine.Expire(24);
            Assert.IsTrue(engine.BossActive);
            JudgeResult gold = engine.JudgeTap(1, 24);
            Assert.IsTrue(gold.Critical);
            Assert.AreEqual(config.BossGoldDamagePerfect, gold.BossDamage);
            Assert.AreEqual(config.BossShotDamagePerfect, engine.JudgeTap(1, 26).BossDamage);
            JudgeResult head = engine.JudgeTap(1, 33);
            Assert.AreEqual(ResultKind.HoldHead, head.Kind);
            Assert.AreEqual(0, head.BossDamage, "guarding alone does no damage");
            List<JudgeResult> tail = engine.Expire(35);
            Assert.AreEqual(ResultKind.HoldTail, tail[0].Kind);
            Assert.AreEqual(config.BossChargeDamage, tail[0].BossDamage);
            Assert.AreEqual(1f - (9 + 3 + 15) / 100f, engine.BossHp01, 1e-5);
        }

        [Test]
        public void ABossFightHealsAndPaysWhenTheBossFalls()
        {
            var config = ShortStages();
            var engine = new RunEngine(WithBoss(Pattern("p", null, Tap("a", 0, 1), Tap("b", 4, 1))), 3, config);
            engine.EnsureScheduled(0);
            // lose a heart first
            ScheduledEvent first = engine.Events[0];
            engine.Resolve(first, Rating.Miss);
            Assert.AreEqual(3, engine.Hp);
            Assert.AreEqual(16, engine.BossStartBeat(0), 1e-9);
            bool sawActive = false, defeated = false;
            for (double beat = 1; beat < 32 && !defeated; beat += .05)
            {
                engine.EnsureScheduled(beat);
                foreach (JudgeResult r in engine.Expire(beat)) defeated |= r.BossDefeated;
                sawActive |= engine.BossActive;
                foreach (ScheduledEvent e in new List<ScheduledEvent>(engine.Events))
                {
                    if (e.Resolved || e.Beat > beat) continue;
                    if (e.Gesture == Gesture.Mash) { if (beat <= e.EndBeat) defeated |= engine.JudgeTap(1, beat).BossDefeated; }
                    else defeated |= engine.JudgeTap(e.Lane, e.Beat).BossDefeated;
                }
            }
            Assert.IsTrue(sawActive);
            Assert.IsTrue(defeated, "perfect shots + fast mashing bring the boss down");
            Assert.AreEqual(1, engine.BossesDefeated);
            Assert.AreEqual(4, engine.Hp, "+1 heart (capped at the maximum)");
            Assert.IsFalse(engine.BossActive);
        }

        [Test]
        public void AnIgnoredBossRunsAwayAtTheEndOfTheFinisher()
        {
            var config = ShortStages();
            config.InitialHp = 99;
            var engine = new RunEngine(WithBoss(Pattern("p", null, Tap("a", 0, 1))), 3, config);
            bool escaped = false;
            for (double beat = 0; beat < 33; beat += .1)
            {
                engine.EnsureScheduled(beat);
                foreach (JudgeResult r in engine.Expire(beat)) escaped |= r.BossEscaped;
            }
            Assert.IsTrue(escaped);
            Assert.AreEqual(0, engine.BossesDefeated);
            Assert.AreEqual(1, engine.BossesEscaped);
        }

        [Test]
        public void ShippedBossFightCanBeWonAndShotsAloneCannotWinIt()
        {
            var config = RunConfig.FromFile(JsonUtility.FromJson<GameConfigFile>(Resources.Load<TextAsset>("game_config").text));
            PatternDefinition[] patterns = JsonUtility.FromJson<PatternFile>(Resources.Load<TextAsset>("patterns").text).patterns;
            // best case per chunk role: every shot PERFECT (gold = critical), every charge guarded to the end
            Func<string, int> best = role =>
            {
                int max = 0;
                foreach (PatternDefinition p in patterns)
                {
                    if (p.role != role) continue;
                    int sum = 0;
                    foreach (PatternEventDefinition e in p.events)
                    {
                        if (e.gesture == "mash") continue;
                        if (e.gesture == "hold") sum += config.BossChargeDamage;
                        else sum += e.objectId == "object_boss_gold" ? config.BossGoldDamagePerfect : config.BossShotDamagePerfect;
                    }
                    max = Math.Max(max, sum);
                }
                return max;
            };
            Assert.IsTrue(Array.Exists(patterns, p => p.role == RunEngine.RoleBossCharge), "every fight has a charge attack");
            int shotDamage = best(RunEngine.RoleBoss) + best(RunEngine.RoleBossCharge) + best(RunEngine.RoleBossFinish);
            Assert.Less(shotDamage, config.BossMaxHp, "the finisher always matters");
            Assert.GreaterOrEqual(shotDamage + config.BossMashDamageTotal, config.BossMaxHp, "a clean fight wins");
        }
    }
}
