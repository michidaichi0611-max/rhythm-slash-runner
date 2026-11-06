using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using RhythmSlashRunner.Core;

namespace RhythmSlashRunner.Tests
{
    /// <summary>Round 10: daily goals, and offbeats that are always announced (owner: "急に来る裏拍でミスる").</summary>
    public sealed class DailyGoalsAndOffbeatTests
    {
        [Test]
        public void TheSameDateGivesTheSameThreeDifferentGoals()
        {
            List<DailyGoals.Goal> a = DailyGoals.ForDate("20261012"), b = DailyGoals.ForDate("20261012");
            Assert.AreEqual(DailyGoals.Count, a.Count);
            for (int i = 0; i < a.Count; i++) Assert.AreEqual(a[i].Id, b[i].Id);
            Assert.AreEqual(a.Count, new HashSet<string>(a.ConvertAll(g => g.Id)).Count, "three different goals");
            bool anyDayDiffers = false;
            for (int d = 13; d < 20 && !anyDayDiffers; d++)
                anyDayDiffers = DailyGoals.ForDate("202610" + d)[0].Id != a[0].Id || DailyGoals.ForDate("202610" + d)[1].Id != a[1].Id;
            Assert.IsTrue(anyDayDiffers, "the goals change from day to day");
        }

        [Test]
        public void ProgressAddsUpOrKeepsTheBestRunAndPaysOnce()
        {
            var profile = new PlayerProfile();
            DailyGoals.Refresh(profile, "x");
            profile.dailyIds[0] = "offbeat_perfect"; profile.dailyIds[1] = "distance"; profile.dailyIds[2] = "boss";
            List<DailyGoals.Goal> first = DailyGoals.Apply(profile, "x", new RunStats { OffbeatPerfect = 12, Distance = 250 });
            Assert.AreEqual(0, first.Count);
            Assert.AreEqual(12, profile.dailyProgress[0]);
            Assert.AreEqual(250, profile.dailyProgress[1]);
            List<DailyGoals.Goal> second = DailyGoals.Apply(profile, "x", new RunStats { OffbeatPerfect = 9, Distance = 120, BossesDefeated = 1 });
            Assert.AreEqual(2, second.Count, "offbeats added up to 21, one boss");
            Assert.AreEqual(250, profile.dailyProgress[1], "in-one-run goals keep the best run, they do not add up");
            Assert.AreEqual(0, DailyGoals.Apply(profile, "x", new RunStats { OffbeatPerfect = 50, BossesDefeated = 3 }).Count, "a goal pays only once");
            Assert.AreEqual(2, DailyGoals.DoneCount(profile));
            // a new day starts fresh
            DailyGoals.Refresh(profile, "y");
            Assert.AreEqual(0, DailyGoals.DoneCount(profile));
            Assert.AreEqual("y", profile.dailyDate);
        }

        [Test]
        public void OldSavesWithoutDailyFieldsLoadCleanly()
        {
            PlayerProfile p = JsonUtility.FromJson<PlayerProfile>("{\"coinBalance\":5}");
            p.Migrate();
            DailyGoals.Refresh(p, "20261012");
            Assert.AreEqual(DailyGoals.Count, p.dailyIds.Count);
            Assert.AreEqual(5, p.coinBalance);
        }

        [Test]
        public void EveryShippedOffbeatIsAnnouncedHalfABeatEarlierInTheSameLane()
        {
            PatternDefinition[] patterns = JsonUtility.FromJson<PatternFile>(Resources.Load<TextAsset>("patterns").text).patterns;
            foreach (PatternDefinition p in patterns)
                foreach (PatternEventDefinition e in p.events)
                {
                    if (Math.Abs(e.beat - Math.Round(e.beat)) < 1e-6) continue;
                    if (e.objectId == RunEngine.ObjectBomb) continue; // round 27: a bomb is never cut, it needs no lead-in
                    PatternEventDefinition lead = Array.Find(p.events, x => Math.Abs(x.beat - (e.beat - .5)) < 1e-6);
                    Assert.IsNotNull(lead, p.id + ": the offbeat at " + e.beat + " comes without a lead-in");
                    if (e.gesture == "tap") Assert.AreEqual(lead.lane, e.lane, p.id + ": the offbeat at " + e.beat + " changes lane on the half beat");
                }
        }
    }
}
