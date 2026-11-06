using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using RhythmSlashRunner.Core;

namespace RhythmSlashRunner.Tests
{
    /// <summary>Rules that need the real Resources/patterns.json and game_config.json.</summary>
    public sealed class ShippedDataTests
    {
        static PatternDefinition[] Patterns()
        {
            TextAsset asset = Resources.Load<TextAsset>("patterns");
            Assert.IsNotNull(asset, "Resources/patterns.json");
            return JsonUtility.FromJson<PatternFile>(asset.text).patterns;
        }

        static RunConfig Config()
        {
            TextAsset asset = Resources.Load<TextAsset>("game_config");
            Assert.IsNotNull(asset, "Resources/game_config.json");
            return RunConfig.FromFile(JsonUtility.FromJson<GameConfigFile>(asset.text));
        }

        [Test]
        public void ConfigFileMatchesBuiltInDefaults()
        {
            RunConfig file = Config(), d = RunConfig.Default;
            Assert.AreEqual(d.Bpm, file.Bpm);
            Assert.AreEqual(d.PhraseBeats, file.PhraseBeats);
            Assert.AreEqual(d.InitialHp, file.InitialHp);
            Assert.AreEqual(d.PerfectMs, file.PerfectMs);
            Assert.AreEqual(d.GoodMs, file.GoodMs);
            Assert.AreEqual(d.SpawnLeadBeats, file.SpawnLeadBeats);
            Assert.AreEqual(d.Difficulty.Length, file.Difficulty.Length);
            for (int i = 0; i < d.Difficulty.Length; i++)
            {
                Assert.AreEqual(d.Difficulty[i].fromSeconds, file.Difficulty[i].fromSeconds);
                Assert.AreEqual(d.Difficulty[i].eventsMin, file.Difficulty[i].eventsMin);
                Assert.AreEqual(d.Difficulty[i].allowOffbeat, file.Difficulty[i].allowOffbeat);
            }
        }

        [Test]
        public void ShippedPatternsAreValidAndCoverEveryTier()
        {
            PatternDefinition[] patterns = Patterns();
            // Keep a sanity ceiling without rejecting legitimate content growth.
            // Round 26 added mirrored crow-soldier patterns, bringing the shipped set above 60.
            Assert.That(patterns.Length, Is.InRange(12, 80));
            foreach (PatternDefinition p in patterns) Assert.IsNull(PatternValidator.Validate(p), p.id);
            foreach (DifficultyStep step in RunConfig.Default.Difficulty)
            {
                int usable = Array.FindAll(patterns, p => string.IsNullOrEmpty(p.role) && p.difficultyTier <= step.tier && RunEngine.Load(p) >= step.eventsMin && RunEngine.Load(p) <= step.eventsMax).Length;
                Assert.GreaterOrEqual(usable, 2, "tier " + step.tier);
            }
            // every tier of boss fight has volleys and a finisher; a full fight can bring the boss down
            Assert.IsNotNull(Array.Find(patterns, p => p.role == RunEngine.RoleBoss && p.difficultyTier == 0));
            Assert.IsNotNull(Array.Find(patterns, p => p.role == RunEngine.RoleBossFinish && p.difficultyTier == 0));
        }

        [Test]
        public void ChunkBoundariesKeepIntervalAndLaneReach()
        {
            PatternDefinition[] patterns = Patterns();
            for (int seed = 1; seed <= 100; seed++)
            {
                var engine = new RunEngine(patterns, seed, Config());
                for (int beat = 0; beat <= 600; beat += 8) engine.EnsureScheduled(beat);
                var events = new List<ScheduledEvent>(engine.Events);
                events.RemoveAll(e => e.Bomb); // round 27: bombs are never cut, they may share a beat with a target
                events.Sort((a, b) => a.Beat.CompareTo(b.Beat));
                for (int i = 1; i < events.Count; i++)
                {
                    // measured from the end of a hold / mash: nothing overlaps them
                    double gap = events[i].Beat - events[i - 1].EndBeat;
                    Assert.GreaterOrEqual(gap, 0.5 - 1e-9, "seed " + seed + " beat " + events[i].Beat);
                    if (events[i].Gesture == Gesture.Mash || events[i - 1].Gesture == Gesture.Mash) Assert.GreaterOrEqual(gap, 1 - 1e-9, "mash spacing, seed " + seed);
                    if (gap < 1) Assert.LessOrEqual(Math.Abs(events[i].Lane - events[i - 1].Lane), 1, "seed " + seed + " beat " + events[i].Beat);
                }
            }
        }

        [Test]
        public void OffbeatsStartAt64SecondsWithAPreviewChunk()
        {
            var engine = new RunEngine(Patterns(), 8, Config());
            engine.EnsureScheduled(300);
            foreach (ScheduledEvent e in engine.Events)
                if (e.Offbeat) Assert.GreaterOrEqual(Math.Floor(e.Beat / 8) * 8 * 0.5, 64.0);
            Assert.IsTrue(engine.FirstOffbeatChunkBeat.HasValue);
            double start = engine.FirstOffbeatChunkBeat.Value;
            Assert.IsTrue(engine.IsOffbeatPreview(start - 8));
            Assert.IsFalse(engine.IsOffbeatPreview(start));
        }

        [Test]
        public void SameSeedSameChunks()
        {
            var a = new RunEngine(Patterns(), 99, Config());
            var b = new RunEngine(Patterns(), 99, Config());
            a.EnsureScheduled(200); b.EnsureScheduled(200);
            Assert.AreEqual(a.Chunks.Count, b.Chunks.Count);
            for (int i = 0; i < a.Chunks.Count; i++) Assert.AreEqual(a.Chunks[i].PatternId, b.Chunks[i].PatternId);
        }

        [Test]
        public void SameTimestampsGiveSameResultsAt30_60_120Hz()
        {
            string Run(int hz)
            {
                var engine = new RunEngine(Patterns(), 11, Config());
                engine.EnsureScheduled(30); // schedule past the tapped range up front
                var taps = new List<KeyValuePair<double, int>>();
                foreach (ScheduledEvent e in engine.Events) if (e.Beat < 46) taps.Add(new KeyValuePair<double, int>(e.Beat * 0.5 + 0.03, e.Lane));
                var log = new System.Text.StringBuilder();
                int next = 0;
                for (int frame = 0; frame / (double)hz < 22; frame++)
                {
                    double now = frame / (double)hz;
                    while (next < taps.Count && taps[next].Key <= now) { log.Append(engine.JudgeTap(taps[next].Value, taps[next].Key / 0.5).Rating).Append(','); next++; }
                    engine.EnsureScheduled(now / 0.5);
                    foreach (JudgeResult r in engine.Expire(now / 0.5)) log.Append("T").Append(r.Rating).Append(',');
                }
                return log + "|" + engine.Score + "|" + engine.Hp;
            }
            string at30 = Run(30);
            Assert.AreEqual(at30, Run(60));
            Assert.AreEqual(at30, Run(120));
            StringAssert.DoesNotContain("Miss", at30);
        }

        [Test]
        public void ExpireStopsAtHpZero()
        {
            var engine = new RunEngine(Patterns(), 6, Config());
            engine.EnsureScheduled(0, 2);
            Assert.AreEqual(4, engine.Expire(40).Count);
            Assert.AreEqual(4, engine.MissCount);
        }
    }
}
