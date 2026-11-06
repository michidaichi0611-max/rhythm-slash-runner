using System;

namespace RhythmSlashRunner.Core
{
    // JsonUtility mirror of data/game_config.json (only the fields the runtime uses).
    [Serializable] public sealed class GameConfigFile { public int schemaVersion; public RunSection run; public PlayfieldSection playfield; public TimingSection timing; public ScoreSection score; public DifficultyStep[] difficulty; public DefaultsSection defaults; public StagesSection stages; public HoldSection hold; public MashSection mash; public BossSection boss; public ItemsSection items; public SoldiersSection soldiers; }
    [Serializable] public sealed class RunSection { public int bpm; public int phraseBeats; public int countInBeats; public double spawnLeadBeats; public int initialHp; public double distanceMetersPerBeat; public int maxEventsPerChunk; public double minimumIntervalBeats; }
    [Serializable] public sealed class PlayfieldSection { public int laneCount; public float[] laneCentersX; public float slashLineY; public float characterY; }
    [Serializable] public sealed class TimingSection { public int perfectMs; public int goodMs; public int inputDebounceMs; public int calibrationMinMs; public int calibrationMaxMs; public int calibrationStepMs; }
    [Serializable] public sealed class ScoreSection { public int perfectBase; public int goodBase; public int missBase; public int comboStep; public double comboMaxMultiplier; public bool emptyTapBreaksCombo; public bool emptyTapDamagesHp; }
    [Serializable] public sealed class DifficultyStep { public double fromSeconds; public int tier; public int eventsMin; public int eventsMax; public bool allowOffbeat; }
    [Serializable] public sealed class StagesSection { public int chunksPerStage; public int firstStageChunks; public int[] bpmSteps; }
    [Serializable] public sealed class HoldSection { public int releaseLeniencyMs; public int slideGraceMs; }
    [Serializable] public sealed class MashSection { public float tapsPerBeat; public int afterGraceMs; }
    [Serializable] public sealed class BossSection { public int chunks; public int maxHp; public int shotDamagePerfect; public int shotDamageGood; public int mashDamageTotal; public int healHp; public int bonusCoinUnits; public int bonusScore; public int chargeDamage; public int goldDamagePerfect; public int goldDamageGood; }
    [Serializable] public sealed class ItemsSection { public int perStage; public int beats; public int maxShields; public float boosterWeight; public float giantWeight; public float barrierWeight; public int heartsPerStage; public int heartCoinUnits; public double giantGapBeats; public int bombDodgeScore; }
    [Serializable] public sealed class SoldiersSection { public int perStage; public int firstStage; }
    [Serializable] public sealed class DefaultsSection { public float musicVolume; public float sfxVolume; public float cueVolume; public bool vibration; public string effectIntensity; public int calibrationMs; }

    /// <summary>Immutable rule values. Defaults equal data/game_config.json; FromFile reads the shipped JSON.</summary>
    public sealed class RunConfig
    {
        public int Bpm = 120;
        public int PhraseBeats = 8;
        public int CountInBeats = 4;
        public double SpawnLeadBeats = 4.0;
        public int InitialHp = 4;
        public double DistanceMetersPerBeat = 1.0;
        public double MinimumIntervalBeats = 0.5;
        public int PerfectMs = 80;
        public int GoodMs = 160;
        public int InputDebounceMs = 100;
        public int PerfectBase = 1000;
        public int GoodBase = 650;
        public int ComboStep = 50;
        public double ComboMaxMultiplier = 1.5;
        public bool EmptyTapDamagesHp;
        public string FallbackPatternId = "basic_center_01";
        // FEVER and coins (DECISIONS D-011). Score formula is unchanged; FEVER only doubles coin units.
        public int FeverCombo = 50;
        public int FeverLevelStep = 50;
        public int DistanceBonusMeters = 50;
        public int DistanceBonusCoins = 3;
        public int NewBestBonusCoins = 10;
        // Endless stages (owner feedback round 2): every stage ends with a boss, the next one starts faster.
        public int ChunksPerStage = 24;
        /// <summary>Length of stage 1 (0 = same as the others). Shorter so the first boss shows up within a minute.</summary>
        public int FirstStageChunks;
        public int[] StageBpm = { 120, 130, 140, 150 };
        /// <summary>Number of scenery themes (set from content_catalog stages). Theme 0 is always the first stage.</summary>
        public int ThemeCount = 6;
        public int HoldReleaseLeniencyMs = 250;
        /// <summary>Slides: how long the finger may be off the path (and how far around a turn both lanes count).</summary>
        public int SlideGraceMs = 150;
        public double MashTapsPerBeat = 2;
        public int MashAfterGraceMs = 300;
        public int BossChunks = 3;
        public int BossMaxHp = 100;
        public int BossShotDamagePerfect = 3;
        public int BossShotDamageGood = 2;
        public int BossMashDamageTotal = 60;
        public int BossHealHp = 1;
        public int BossBonusCoinUnits = 15;
        public int BossBonusScore = 5000;
        public int BossChargeDamage = 15;
        public int BossGoldDamagePerfect = 9;
        public int BossGoldDamageGood = 3;
        // power-up items (owner round 3)
        public int ItemsPerStage = 2;
        /// <summary>Booster = rush (owner round 4): targets of the next RushReachBeats are cut one per RushStepBeats (a 16th).</summary>
        public double RushReachBeats = 4, RushStepBeats = .25;
        public int RushBonusMeters = 20;
        public double ItemBeats = 8;
        public int MaxShields = 1;
        public float BoosterWeight = .4f, GiantWeight = .3f, BarrierWeight = .3f;
        /// <summary>Giant (round 27): beats left empty right after it ends, so the hand can find its lane again.</summary>
        public double GiantGapBeats = 1;
        /// <summary>Bombs (round 27): score for letting one go by (a dodge never touches the combo).</summary>
        public int BombDodgeScore = 300;
        /// <summary>Heart capsules per stage (owner round 9): one in the middle of the stage restores a heart; at full HP it pays coin units instead.</summary>
        public int HeartsPerStage;
        public int HeartCoinUnits = 15;
        /// <summary>Soldier chunks per stage (owner round 17: "たまに出てくる"); the first stage has its own (smaller) count.</summary>
        public int SoldiersPerStage, SoldiersFirstStage;
        public DifficultyStep[] Difficulty =
        {
            new DifficultyStep { fromSeconds = 0, tier = 0, eventsMin = 4, eventsMax = 4, allowOffbeat = false },
            new DifficultyStep { fromSeconds = 16, tier = 1, eventsMin = 4, eventsMax = 6, allowOffbeat = false },
            new DifficultyStep { fromSeconds = 32, tier = 2, eventsMin = 6, eventsMax = 8, allowOffbeat = false },
            new DifficultyStep { fromSeconds = 64, tier = 3, eventsMin = 8, eventsMax = 10, allowOffbeat = true },
            new DifficultyStep { fromSeconds = 96, tier = 4, eventsMin = 10, eventsMax = 12, allowOffbeat = true },
            new DifficultyStep { fromSeconds = 128, tier = 5, eventsMin = 10, eventsMax = 12, allowOffbeat = true },
        };

        /// <summary>Tempo at beat 0 (stage 0). Later stages use <see cref="BpmForStage"/>; the run's <see cref="TempoMap"/> is the truth.</summary>
        public double SecondsPerBeat { get { return 60.0 / Bpm; } }

        public double BpmForStage(int stage)
        {
            if (StageBpm == null || StageBpm.Length == 0) return Bpm;
            return StageBpm[Math.Max(0, Math.Min(stage, StageBpm.Length - 1))];
        }

        public int StageBeats { get { return ChunksPerStage * PhraseBeats; } }
        public double MsPerBeat { get { return 60000.0 / Bpm; } }

        public static readonly RunConfig Default = new RunConfig();

        public static RunConfig FromFile(GameConfigFile file)
        {
            if (file == null || file.run == null || file.timing == null || file.score == null || file.difficulty == null || file.difficulty.Length == 0)
                throw new ArgumentException("game_config.json is missing required sections.");
            var config = new RunConfig
            {
                Bpm = file.run.bpm,
                PhraseBeats = file.run.phraseBeats,
                CountInBeats = file.run.countInBeats,
                SpawnLeadBeats = file.run.spawnLeadBeats,
                InitialHp = file.run.initialHp,
                DistanceMetersPerBeat = file.run.distanceMetersPerBeat,
                MinimumIntervalBeats = file.run.minimumIntervalBeats,
                PerfectMs = file.timing.perfectMs,
                GoodMs = file.timing.goodMs,
                InputDebounceMs = file.timing.inputDebounceMs,
                PerfectBase = file.score.perfectBase,
                GoodBase = file.score.goodBase,
                ComboStep = file.score.comboStep,
                ComboMaxMultiplier = file.score.comboMaxMultiplier,
                EmptyTapDamagesHp = file.score.emptyTapDamagesHp,
                Difficulty = file.difficulty,
            };
            if (file.stages != null && file.stages.chunksPerStage > 0)
            {
                config.ChunksPerStage = file.stages.chunksPerStage;
                config.FirstStageChunks = file.stages.firstStageChunks;
                if (file.stages.bpmSteps != null && file.stages.bpmSteps.Length > 0) config.StageBpm = file.stages.bpmSteps;
            }
            if (file.hold != null && file.hold.releaseLeniencyMs > 0) config.HoldReleaseLeniencyMs = file.hold.releaseLeniencyMs;
            if (file.hold != null && file.hold.slideGraceMs > 0) config.SlideGraceMs = file.hold.slideGraceMs;
            if (file.mash != null && file.mash.tapsPerBeat > 0) { config.MashTapsPerBeat = file.mash.tapsPerBeat; config.MashAfterGraceMs = file.mash.afterGraceMs; }
            if (file.boss != null && file.boss.maxHp > 0)
            {
                config.BossChunks = file.boss.chunks; config.BossMaxHp = file.boss.maxHp;
                config.BossShotDamagePerfect = file.boss.shotDamagePerfect; config.BossShotDamageGood = file.boss.shotDamageGood;
                config.BossMashDamageTotal = file.boss.mashDamageTotal; config.BossHealHp = file.boss.healHp;
                config.BossBonusCoinUnits = file.boss.bonusCoinUnits; config.BossBonusScore = file.boss.bonusScore;
                if (file.boss.chargeDamage > 0) { config.BossChargeDamage = file.boss.chargeDamage; config.BossGoldDamagePerfect = file.boss.goldDamagePerfect; config.BossGoldDamageGood = file.boss.goldDamageGood; }
            }
            if (file.items != null && file.items.beats > 0)
            {
                config.ItemsPerStage = file.items.perStage; config.ItemBeats = file.items.beats; config.MaxShields = file.items.maxShields;
                config.BoosterWeight = file.items.boosterWeight; config.GiantWeight = file.items.giantWeight; config.BarrierWeight = file.items.barrierWeight;
                if (file.items.giantGapBeats > 0) config.GiantGapBeats = file.items.giantGapBeats;
                if (file.items.bombDodgeScore > 0) config.BombDodgeScore = file.items.bombDodgeScore;
                config.HeartsPerStage = file.items.heartsPerStage;
                if (file.items.heartCoinUnits > 0) config.HeartCoinUnits = file.items.heartCoinUnits;
            }
            if (file.soldiers != null) { config.SoldiersPerStage = file.soldiers.perStage; config.SoldiersFirstStage = file.soldiers.firstStage; }
            return config;
        }

        public DifficultyStep StepForSeconds(double seconds)
        {
            DifficultyStep current = Difficulty[0];
            foreach (DifficultyStep step in Difficulty) if (seconds >= step.fromSeconds) current = step;
            return current;
        }

        public DifficultyStep StepForTier(int tier)
        {
            foreach (DifficultyStep step in Difficulty) if (step.tier == tier) return step;
            return Difficulty[Difficulty.Length - 1];
        }
    }
}
