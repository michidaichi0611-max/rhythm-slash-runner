using System;
using System.Collections.Generic;

namespace RhythmSlashRunner.Core
{
    [Serializable]
    public sealed class PatternFile
    {
        public int schemaVersion;
        public PatternDefinition[] patterns;
    }

    [Serializable]
    public sealed class PatternDefinition
    {
        public string id;
        public int durationBeats;
        public int difficultyTier;
        public float weight;
        /// <summary>"" = normal; "boss" / "boss_finish" are only used inside boss fights (picked by the engine, never randomly).</summary>
        public string role;
        public PatternEventDefinition[] events;
    }

    [Serializable]
    public sealed class PatternEventDefinition
    {
        public string id;
        public float beat;
        public int lane;
        public string objectId;
        public string gesture;
        /// <summary>Beats a hold is held / a mash stays open (0 for taps).</summary>
        public float length;
        /// <summary>Slides (round 11): the lanes the held object turns to, and when (beats after its head).</summary>
        public int[] path;
        public float[] turns;
        /// <summary>Round 27: a half of a split fruit names the event id of the big fruit it breaks off from.</summary>
        public string from;
    }

    /// <summary>Tick = an accepted input that is not a judgement of its own (mash hits).</summary>
    public enum Rating { Ignored, Empty, Perfect, Good, Miss, Tick }

    public enum Gesture { Tap, Hold, Mash }

    /// <summary>
    /// Power-ups carried by a target (owner round 3). Work the same for every character. Giant (round 27) replaced the
    /// auto-cutter drone: for a few beats any lane cuts.
    /// </summary>
    public enum ItemKind { None, Booster, Giant, Barrier, Heart }

    /// <summary>What a <see cref="JudgeResult"/> describes, for presentation.</summary>
    public enum ResultKind { Tap, HoldHead, HoldTail, HoldBreak, MashTap, MashEnd }

    public static class Gestures
    {
        /// <summary>A "slide" is a hold whose object snakes between lanes (the finger follows it).</summary>
        public static Gesture Parse(string gesture) { return gesture == "hold" || gesture == "slide" ? Gesture.Hold : gesture == "mash" ? Gesture.Mash : Gesture.Tap; }
    }

    public sealed class ScheduledEvent
    {
        public string Id;
        public double Beat;
        public int Lane;
        public string ObjectId;
        public bool Offbeat;
        public bool Resolved;
        public Rating Rating;
        public Gesture Gesture;
        public double Length;
        /// <summary>Hold: the head was hit and the lane is being held.</summary>
        public bool Holding;
        public bool TailBroken;
        public int MashCount;
        public int MashRequired;
        /// <summary>Part of a boss fight (shots damage the boss; the finisher mash finishes it).</summary>
        public bool Boss;
        /// <summary>Boss weak-point shot (PERFECT = critical).</summary>
        public bool Gold;
        /// <summary>Boss charge attack: a hold that guards and throws the ball back.</summary>
        public bool Charge;
        public ItemKind Item;
        /// <summary>Part of the first-run practice chunks: a MISS costs no heart.</summary>
        public bool Tutorial;
        public int Stage;
        /// <summary>Round 27: reaches the line while the hero is a giant, so any lane cuts it (drawn gold).</summary>
        public bool Giant;
        /// <summary>Round 27: a bomb. Never cut it: a hit breaks the combo (no heart), letting it pass is a dodge.</summary>
        public bool Bomb;
        /// <summary>Round 27: a big fruit that breaks in two when cut (its halves are events of their own).</summary>
        public bool Splits;
        /// <summary>Round 27: taken off the road unplayed (the breathing beat right after the giant ends).</summary>
        public bool Skipped;
        /// <summary>Round 27: a half of a split fruit pops out where the big fruit was cut (NaN = not a half).</summary>
        public double SplitFromBeat = double.NaN;
        public int SplitFromLane;
        /// <summary>
        /// Round 17: a strike of a soldier ("toy", "ginger", "carrot", "frog"; null = not a soldier). Every strike of one
        /// soldier shares <see cref="SoldierGroup"/>; the last one (<see cref="SoldierFinal"/>) is the counter that sends it flying.
        /// </summary>
        public string Soldier;
        public string SoldierGroup;
        public bool SoldierFinal;
        public double EndBeat { get { return Beat + Length; } }
        /// <summary>Slide (round 11): it turns to <see cref="SlideLanes"/>[i] at beat <see cref="SlideBeats"/>[i].</summary>
        public int[] SlideLanes;
        public double[] SlideBeats;
        /// <summary>Since when the finger has been off the slide's path (NaN = on it).</summary>
        public double OffSince = double.NaN;
        public bool IsSlide { get { return SlideLanes != null && SlideLanes.Length > 0; } }

        /// <summary>The lane a slide runs in at <paramref name="beat"/> (the head lane for anything else).</summary>
        public int LaneAt(double beat)
        {
            int lane = Lane;
            if (SlideLanes != null) for (int i = 0; i < SlideLanes.Length; i++) if (beat >= SlideBeats[i]) lane = SlideLanes[i];
            return lane;
        }

        public int FinalLane { get { return IsSlide ? SlideLanes[SlideLanes.Length - 1] : Lane; } }
    }

    public sealed class ChunkInfo
    {
        public int Index;
        public double StartBeat;
        public string PatternId;
        public int Tier;
        public bool HasOffbeat;
        public string Role;
        public int Stage;
        /// <summary>Round 29: a new thing's first chunk or the practice chunk after it (MISSes cost no heart).</summary>
        public bool Practice;
    }

    public sealed class JudgeResult
    {
        public Rating Rating;
        public string Reason;
        public ScheduledEvent Event;
        public double ErrorMs;
        public int Hp;
        public int Score;
        public int Combo;
        public bool Ended;
        public ResultKind Kind;
        public bool BossDefeated;
        public bool BossEscaped;
        /// <summary>Round 27: cut by the giant hero (any lane).</summary>
        public bool Giant;
        /// <summary>Round 27: a bomb was cut (combo broken, no heart).</summary>
        public bool BombHit;
        /// <summary>Round 27: a bomb went by uncut ("よけた！").</summary>
        public bool Dodged;
        /// <summary>Round 27: a bomb was stomped flat by the giant hero (harmless).</summary>
        public bool Crushed;

        /// <summary>Cut during a booster rush (the hero dashes ahead and cuts everything in reach).</summary>
        public bool Rush;
        public ItemKind ItemGot;
        /// <summary>A heart capsule restored one heart (false: HP was full, it became coins).</summary>
        public bool Healed;
        /// <summary>The barrier took this MISS instead of a heart.</summary>
        public bool ShieldBlocked;
        public bool Critical;
        public int BossDamage;
    }

    public sealed class RunSnapshot
    {
        public int Hp;
        public int Score;
        public int Combo;
        public int MaxCombo;
        public int Distance;
        public int Perfect;
        public int Good;
        public int Miss;
        public int Empty;
        public bool Ended;
        /// <summary>Success units: +1 per hit, +2 per hit during FEVER. Coins = units / 3 + distance bonus.</summary>
        public int CoinUnits;
        public int Coins;
        public int FeverHits;
        public bool Fever;
        public int FeverLevel;
        public int PerfectStreak;
        public int MaxHp;
        public int Stage;
        public int Theme;
        public double Bpm;
        public int FeverCount;
        public bool BossActive;
        public float BossHp01;
        public int BossesDefeated;
        public int BossesEscaped;
        public bool BossRage;
        public bool Boost;
        public float Boost01;
        /// <summary>Round 27: the hero is a giant; <see cref="Giant01"/> is the time left (1 → 0), <see cref="GiantBeatsLeft"/> in beats.</summary>
        public bool Giant;
        public float Giant01;
        public double GiantBeatsLeft;
        public int Shields;
    }

    /// <summary>Presentation strength. Weak keeps only the guides needed to judge (ART_BIBLE 7).</summary>
    public enum EffectLevel { Weak = 0, Standard = 1, Strong = 2 }

    [Serializable]
    public sealed class PlayerProfile
    {
        public int schemaVersion = 1;
        public int coinBalance;
        public int bestDistance;
        public int bestScore;
        public string equippedCharacter = "character_hamster";
        public string equippedWeapon = "weapon_energy_blade";
        public string equippedStage = "stage_kitchen";
        public float musicVolume = 0.6f;
        public float sfxVolume = 0.8f;
        public float cueVolume = 0.8f;
        public bool vibration = true;
        public int calibrationMs;
        public bool weakEffects; // legacy (≤ round 0); migrated to effectLevel on load
        public int effectLevel = (int)EffectLevel.Strong;
        public bool screenShake = true;
        public bool photosensitivityNoticeShown;
        /// <summary>The first run opens with practice chunks; set after it.</summary>
        public bool tutorialDone;
        /// <summary>Settings: calmer background motion (motion-sickness option).</summary>
        public bool calmMotion;
        /// <summary>One-time hints already shown (tap, hold, mash, offbeat, item, boss, gold).</summary>
        public List<string> seenTips = new List<string>();
        public bool diagnostics;
        public List<string> seenIds = new List<string> { "weapon_energy_blade" };
        public int generation;
        public List<string> ownedIds = new List<string> { "character_hamster", "weapon_energy_blade", "stage_kitchen" };
        public List<string> appliedRewardRunIds = new List<string>();
        /// <summary>Today's goals (round 10): the date they belong to, their ids, progress and whether each was paid.</summary>
        public string dailyDate = "";
        public List<string> dailyIds = new List<string>();
        public List<int> dailyProgress = new List<int>();
        public List<bool> dailyDone = new List<bool>();
        // round 32: plays and energy drinks (see Energy), this week's best (see Weekly)
        public int energy;
        public long energyUpdatedTicks;
        public long firstPlayTicks;
        public int drinks;
        public string loginBonusDate = "";
        public string bossDrinkDate = "";
        public string weeklyKey = "";
        public int weeklyBestDistance;
        // round 34: play-data consent (0 = not asked, 1 = yes, 2 = no), the store-review prompt, runs played
        public int analyticsConsent;
        public long reviewAskedTicks;
        public bool reviewDone;
        public int runsPlayed;
        /// <summary>Round 34: "ja", "en", or empty = follow the device language.</summary>
        public string language = "";

        public bool Owns(string id) { return ownedIds.Contains(id); }

        public EffectLevel Effects { get { return (EffectLevel)Math.Max(0, Math.Min(2, effectLevel)); } }

        /// <summary>Upgrades older saves in place.</summary>
        public void Migrate()
        {
            if (weakEffects) { effectLevel = (int)EffectLevel.Weak; weakEffects = false; }
            if (seenIds == null) seenIds = new List<string>();
            if (seenTips == null) seenTips = new List<string>();
            if (ownedIds == null) ownedIds = new List<string> { "character_hamster", "weapon_energy_blade", "stage_kitchen" };
            if (dailyIds == null) dailyIds = new List<string>();
            if (dailyProgress == null) dailyProgress = new List<int>();
            if (dailyDone == null) dailyDone = new List<bool>();
            if (dailyProgress.Count != dailyIds.Count || dailyDone.Count != dailyIds.Count) { dailyIds.Clear(); dailyProgress.Clear(); dailyDone.Clear(); dailyDate = ""; }
        }

        public PlayerProfile Clone() { return UnityEngine.JsonUtility.FromJson<PlayerProfile>(UnityEngine.JsonUtility.ToJson(this)); }
    }
}

