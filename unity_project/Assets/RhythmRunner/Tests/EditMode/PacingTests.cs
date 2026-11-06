using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using RhythmSlashRunner.Core;

namespace RhythmSlashRunner.Tests
{
    /// <summary>Round 29 (owner: the game stopped too often): new things come one at a time, 30 s apart, each followed by practice.</summary>
    public sealed class PacingTests
    {
        static PatternDefinition[] Shipped() { return JsonUtility.FromJson<PatternFile>(Resources.Load<TextAsset>("patterns").text).patterns; }

        static RunConfig Config() { return RunConfig.FromFile(JsonUtility.FromJson<GameConfigFile>(Resources.Load<TextAsset>("game_config").text)); }

        [Test]
        public void NewThingsComeOneAtATimeAtLeastThirtySecondsApartWithAPracticeChunkAfter()
        {
            for (int seed = 1; seed <= 40; seed++)
            {
                var engine = new RunEngine(Shipped(), seed, Config());
                engine.EnableTutorial(4, "tutorial_tap", "tutorial_hold", "tutorial_mash");
                engine.TrackNovelty(new string[0], 30);
                var seen = new HashSet<string>();
                var introSeconds = new List<double>();
                var chunks = new List<ChunkInfo>();
                for (int beat = 0; beat <= 800; beat += 8)
                {
                    engine.EnsureScheduled(beat);
                    foreach (ChunkInfo c in engine.Chunks) if (!chunks.Any(x => x.Index == c.Index)) chunks.Add(c);
                }
                chunks.Sort((a, b) => a.Index.CompareTo(b.Index));
                PatternDefinition[] all = Shipped();
                for (int i = 0; i < chunks.Count; i++)
                {
                    ChunkInfo c = chunks[i];
                    PatternDefinition p = all.First(x => x.id == c.PatternId);
                    if (RunEngine.IsBossRole(p.role)) continue;
                    List<string> fresh = RunEngine.NoveltyKeys(p).Where(k => !seen.Contains(k)).ToList();
                    if (fresh.Count == 0) continue;
                    Assert.AreEqual(1, fresh.Count, "seed " + seed + " chunk " + c.Index + ": one new thing at a time");
                    seen.Add(fresh[0]);
                    foreach (string k in RunEngine.NoveltyKeys(p)) seen.Add(k);
                    double at = engine.Tempo.SecondsAt(c.StartBeat);
                    bool basic = p.id.StartsWith("tutorial_");
                    if (!basic && introSeconds.Count > 0) Assert.GreaterOrEqual(at - introSeconds.Last(), 30 - 1e-6, "seed " + seed + ": 30 s between new things");
                    introSeconds.Add(at);
                    if (!basic) Assert.IsTrue(c.Practice, "the new thing's own chunk forgives misses");
                }
            }
        }

        [Test]
        public void PracticeChunksCostNoHeart()
        {
            var engine = new RunEngine(Shipped(), 3, Config());
            engine.TrackNovelty(new[] { "hold", "mash" }, 30);
            ChunkInfo practice = null;
            for (int beat = 0; beat <= 800 && practice == null; beat += 8)
            {
                engine.EnsureScheduled(beat);
                practice = engine.Chunks.FirstOrDefault(c => c.Practice);
            }
            Assert.IsNotNull(practice, "a new thing comes with practice");
            ScheduledEvent e = engine.Events.First(x => x.Beat >= practice.StartBeat && x.Beat < practice.StartBeat + 8);
            Assert.IsTrue(e.Tutorial);
            int hp = engine.Hp;
            engine.Resolve(e, Rating.Miss);
            Assert.AreEqual(hp, engine.Hp);
        }

        [Test]
        public void WithoutPacingNothingChanges()
        {
            var a = new RunEngine(Shipped(), 7, Config());
            var b = new RunEngine(Shipped(), 7, Config());
            b.TrackNovelty(new[] { "hold", "mash", "offbeat", "bomb", "split", "soldier", "crow" }, 30);
            a.EnsureScheduled(300); b.EnsureScheduled(300);
            CollectionAssert.AreEqual(a.Chunks.Select(c => c.PatternId).ToList(), b.Chunks.Select(c => c.PatternId).ToList(), "a player who knows everything plays the same run");
        }
    }
}
