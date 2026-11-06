using System;
using System.Collections.Generic;

namespace RhythmSlashRunner.Core
{
    /// <summary>
    /// Pure run rules: chunk scheduling, stages (tempo steps + boss fights), judgement of taps / holds / mashes, HP, score.
    /// Timing windows are always compared in milliseconds through the run's <see cref="TempoMap"/>, so a faster stage
    /// keeps the same PERFECT/GOOD windows. The tap-only rules mirror prototype_web/src/engine.js.
    /// </summary>
    public sealed class RunEngine
    {
        // Kept for callers that need the MVP defaults without a config instance.
        public const int Bpm = 120;
        public const int PhraseBeats = 8;
        public const int InitialHp = 4;
        public const int PerfectMs = 80;
        public const int GoodMs = 160;
        public const double SpawnLeadBeats = 4.0;
        const double Epsilon = 1e-6;

        public const string RoleBoss = "boss";
        public const string RoleBossFinish = "boss_finish";
        public const string RoleBossCharge = "boss_charge";
        public const string RoleTutorial = "tutorial";
        /// <summary>Round 17: a soldier walks in and attacks on the beat (taps that parry); placed a few times per stage.</summary>
        public const string RoleSoldier = "soldier";
        public const string SoldierPrefix = "object_soldier_";
        /// <summary>Round 27: a bomb (never cut it) and a big fruit that splits into two halves (their objects).</summary>
        public const string ObjectBomb = "object_bomb";
        public const string ObjectSplit = "object_melon_big";
        public const string ObjectHalf = "object_melon_half";
        public static bool IsBossRole(string role) { return role == RoleBoss || role == RoleBossCharge || role == RoleBossFinish; }
        const string RoleBreather = "breather";

        readonly PatternDefinition[] patterns;
        readonly PatternDefinition fallback;
        readonly RunConfig config;
        readonly int seed;
        readonly TempoMap tempo;
        readonly List<ScheduledEvent> events = new List<ScheduledEvent>(64);
        readonly List<ChunkInfo> chunks = new List<ChunkInfo>(16);
        readonly List<PatternDefinition> pool = new List<PatternDefinition>(32);
        readonly List<string> recentPatternIds = new List<string>(4);
        readonly List<int> themes = new List<int>(8);
        readonly List<int> themeBag = new List<int>(8);
        Random random, themeRandom, itemRandom;
        readonly HashSet<int> itemChunks = new HashSet<int>();
        readonly HashSet<int> heartChunks = new HashSet<int>();
        readonly HashSet<int> soldierChunks = new HashSet<int>();
        readonly List<ScheduledEvent> rushQueue = new List<ScheduledEvent>(16);
        readonly List<double> rushBonusAt = new List<double>(8);
        double rushFrom = -1, rushUntil = -1, rushNextBeat = -1;
        double giantFrom = -1, giantUntil = -1;
        int nextChunk;
        bool hasLastEvent, lastWasMash;
        double lastEventBeat;
        int lastEventLane;
        double bossHp, mashGraceUntilSeconds;
        string[] tutorialIds = new string[0];
        int tutorialSpacing = 1;
        // round 29: what the player already knows (null = no pacing), when the last new thing came, the practice after it
        HashSet<string> known, knownAtStart;
        double introGapSeconds, lastIntroSeconds = double.NegativeInfinity, chunkSeconds;
        string practiceKey;
        int practiceChunk = -1;
        int bossStage;

        public RunConfig Config { get { return config; } }
        public TempoMap Tempo { get { return tempo; } }
        public int Seed { get { return seed; } }
        public int Hp { get; private set; }
        public int Score { get; private set; }
        public int Combo { get; private set; }
        public int MaxCombo { get; private set; }
        public int PerfectCount { get; private set; }
        public int GoodCount { get; private set; }
        public int MissCount { get; private set; }
        public int EmptyCount { get; private set; }
        public int CoinUnits { get; private set; }
        public int FeverHits { get; private set; }
        public int FeverCount { get; private set; }
        public int PerfectStreak { get; private set; }
        public int HoldsCompleted { get; private set; }
        public int MashesCleared { get; private set; }
        public bool Fever { get { return Combo >= config.FeverCombo; } }
        public int FeverLevel { get { return Fever ? 1 + (Combo - config.FeverCombo) / config.FeverLevelStep : 0; } }
        public bool Ended { get; private set; }
        public double? FirstOffbeatChunkBeat { get; private set; }
        public IReadOnlyList<ScheduledEvent> Events { get { return events; } }
        public IReadOnlyList<ChunkInfo> Chunks { get { return chunks; } }

        public bool BossActive { get; private set; }
        public int BossStage { get { return bossStage; } }
        public float BossHp01 { get { return (float)Math.Max(0, Math.Min(1, bossHp / Math.Max(1, config.BossMaxHp))); } }
        public int BossesDefeated { get; private set; }
        public int BossesEscaped { get; private set; }
        /// <summary>Below half HP the boss gets angry (presentation + the louder music layer).</summary>
        public bool BossRage { get { return BossActive && bossHp <= config.BossMaxHp * .5; } }
        public int Shields { get; private set; }
        public int ItemsCollected { get; private set; }
        /// <summary>Round 27: bombs cut (combo broken) and bombs let go by.</summary>
        public int BombHits { get; private set; }
        public int BombsDodged { get; private set; }

        public RunEngine(PatternDefinition[] sourcePatterns, int seed) : this(sourcePatterns, seed, RunConfig.Default) { }

        public RunEngine(PatternDefinition[] sourcePatterns, int seed, RunConfig runConfig)
        {
            if (sourcePatterns == null) throw new ArgumentNullException(nameof(sourcePatterns));
            if (sourcePatterns.Length == 0) throw new ArgumentException("At least one pattern is required.");
            config = runConfig ?? RunConfig.Default;
            foreach (PatternDefinition pattern in sourcePatterns)
            {
                string error = PatternValidator.Validate(pattern, config);
                if (error != null) throw new ArgumentException(error);
            }
            patterns = sourcePatterns;
            fallback = Array.Find(patterns, p => p.id == config.FallbackPatternId) ?? Array.Find(patterns, p => string.IsNullOrEmpty(p.role)) ?? patterns[0];
            this.seed = seed;
            tempo = new TempoMap(config.BpmForStage(0));
            Reset();
        }

        public void Reset()
        {
            random = new Random(seed);
            themeRandom = new Random(seed ^ 0x5EED);
            itemRandom = new Random(seed ^ 0x17E);
            itemChunks.Clear(); heartChunks.Clear(); soldierChunks.Clear(); rushQueue.Clear(); rushBonusAt.Clear(); rushFrom = rushUntil = rushNextBeat = -1;
            giantFrom = giantUntil = -1; Shields = 0; ItemsCollected = 0; BombHits = 0; BombsDodged = 0;
            tempo.Reset(config.BpmForStage(0));
            Hp = config.InitialHp; Score = 0; Combo = 0; MaxCombo = 0;
            PerfectCount = 0; GoodCount = 0; MissCount = 0; EmptyCount = 0;
            CoinUnits = 0; FeverHits = 0; FeverCount = 0; PerfectStreak = 0; HoldsCompleted = 0; MashesCleared = 0;
            if (knownAtStart != null) known = new HashSet<string>(knownAtStart);
            lastIntroSeconds = double.NegativeInfinity; practiceKey = null; practiceChunk = -1;
            Ended = false; nextChunk = 0; hasLastEvent = false; lastWasMash = false; FirstOffbeatChunkBeat = null;
            BossActive = false; bossStage = -1; bossHp = 0; BossesDefeated = 0; BossesEscaped = 0;
            mashGraceUntilSeconds = double.NegativeInfinity;
            events.Clear(); chunks.Clear(); recentPatternIds.Clear(); themes.Clear(); themeBag.Clear();
        }

        /// <summary>
        /// First-run practice: the run opens with these patterns (role "tutorial") in order; their MISSes cost no heart.
        /// Call before the first <see cref="EnsureScheduled"/>.
        /// </summary>
        public void EnableTutorial(params string[] patternIds) { EnableTutorial(1, patternIds); }

        /// <summary>Round 29: the practice chunks come every <paramref name="spacing"/> chunks (easy chunks in between).</summary>
        public void EnableTutorial(int spacing, params string[] patternIds)
        {
            var found = new List<string>();
            foreach (string id in patternIds) if (Array.Exists(patterns, p => p.id == id)) found.Add(id);
            tutorialIds = found.ToArray();
            tutorialSpacing = Math.Max(1, spacing);
        }

        string TutorialAt(int chunk)
        {
            if (chunk % tutorialSpacing != 0) return null;
            int i = chunk / tutorialSpacing;
            return i < tutorialIds.Length ? tutorialIds[i] : null;
        }

        public int TutorialChunks { get { return tutorialIds.Length == 0 ? 0 : (tutorialIds.Length - 1) * tutorialSpacing + 1; } }

        /// <summary>
        /// Round 29 pacing (owner: "連続で出てくるのでゲームがかなりの頻度で止まる"): a pattern that brings something the player
        /// has not met yet (<see cref="NoveltyKeys"/>, the same keys as the one-time hints) may only come
        /// <paramref name="gapSeconds"/> after the previous new thing, and only one new thing at a time. The chunk after a
        /// new thing is a practice chunk built around it; both cost no heart on a MISS.
        /// </summary>
        public void TrackNovelty(IEnumerable<string> alreadyKnown, double gapSeconds)
        {
            knownAtStart = new HashSet<string>(alreadyKnown ?? new string[0]);
            known = new HashSet<string>(knownAtStart);
            introGapSeconds = gapSeconds;
        }

        /// <summary>What a pattern asks of the player that may be new: hold, mash, offbeat, bomb, split, soldier, crow.</summary>
        public static List<string> NoveltyKeys(PatternDefinition p)
        {
            var keys = new List<string>(2);
            foreach (PatternEventDefinition e in p.events)
            {
                string key = null;
                bool soldier = e.objectId != null && e.objectId.StartsWith(SoldierPrefix);
                if (e.objectId == ObjectBomb) key = "bomb";
                else if (e.objectId == ObjectSplit) key = "split";
                else if (soldier) key = e.objectId == SoldierPrefix + "crow" ? "crow" : "soldier";
                else if (Gestures.Parse(e.gesture) == Gesture.Hold) key = "hold";
                else if (Gestures.Parse(e.gesture) == Gesture.Mash) key = "mash";
                if (key != null && !keys.Contains(key)) keys.Add(key);
                if (IsOffbeat(e.beat) && e.objectId != ObjectBomb && !keys.Contains("offbeat")) keys.Add("offbeat");
            }
            return keys;
        }

        int UnknownKeys(PatternDefinition p, out string first)
        {
            first = null;
            if (known == null) return 0;
            int n = 0;
            foreach (string k in NoveltyKeys(p)) if (!known.Contains(k)) { if (first == null) first = k; n++; }
            return n;
        }

        /// <summary>Pacing filter: nothing new, or exactly one new thing once the gap since the last one has passed.</summary>
        bool PacedOk(PatternDefinition p)
        {
            string key;
            int n = UnknownKeys(p, out key);
            if (nextChunk < TutorialChunks) return n == 0; // the first-run practice brings the basics in its own order
            return n == 0 || (n == 1 && chunkSeconds - lastIntroSeconds >= introGapSeconds - Epsilon);
        }

        public static int TierForSeconds(double seconds) { return RunConfig.Default.StepForSeconds(seconds).tier; }

        public static bool IsOffbeat(double beat) { return Math.Abs(beat - Math.Floor(beat) - 0.5) < Epsilon; }

        /// <summary>Lane jumps of two lanes need at least one beat.</summary>
        public static bool Reachable(double gap, int laneA, int laneB, RunConfig config)
        {
            if (gap < config.MinimumIntervalBeats - Epsilon) return false;
            return !(gap < 1 - Epsilon && Math.Abs(laneA - laneB) > 1);
        }

        // ------------------------------------------------------------------ stages

        /// <summary>Chunks in a stage: stage 1 can be shorter (first boss within a minute).</summary>
        public int ChunksIn(int stage) { return stage == 0 && config.FirstStageChunks > 0 ? config.FirstStageChunks : config.ChunksPerStage; }
        int FirstChunkOf(int stage) { return stage <= 0 ? 0 : ChunksIn(0) + (stage - 1) * config.ChunksPerStage; }
        int StageOfChunk(int chunk) { return chunk < ChunksIn(0) ? 0 : 1 + (chunk - ChunksIn(0)) / config.ChunksPerStage; }

        public int StageAt(double beat) { return beat < 0 ? 0 : StageOfChunk((int)Math.Floor(beat / config.PhraseBeats + Epsilon)); }
        public double StageStartBeat(int stage) { return (double)FirstChunkOf(stage) * config.PhraseBeats; }
        public double StageLengthBeats(int stage) { return (double)ChunksIn(stage) * config.PhraseBeats; }
        /// <summary>First beat of the boss fight that closes <paramref name="stage"/> (no fight when BossChunks is 0).</summary>
        public double BossStartBeat(int stage) { return StageStartBeat(stage + 1) - config.BossChunks * config.PhraseBeats; }
        public bool HasBossFights { get { return config.BossChunks > 0 && config.BossChunks < Math.Min(config.ChunksPerStage, ChunksIn(0)); } }

        /// <summary>Scenery theme per stage: stage 0 is theme 0, then a shuffle bag without immediate repeats.</summary>
        /// <summary>Test hook (screenshots): every stage uses this theme. Null in play.</summary>
        public int? ThemeOverride;

        public int ThemeForStage(int stage)
        {
            if (ThemeOverride.HasValue) return ThemeOverride.Value;
            if (stage < 0) stage = 0;
            while (themes.Count <= stage)
            {
                if (themes.Count == 0 || config.ThemeCount < 2) { themes.Add(0); continue; }
                int previous = themes[themes.Count - 1];
                if (themeBag.Count == 0)
                {
                    // the first cycle shows every other theme before the opening one returns
                    for (int t = themes.Count < config.ThemeCount ? 1 : 0; t < config.ThemeCount; t++) themeBag.Add(t);
                }
                int pick = themeRandom.Next(themeBag.Count);
                if (themeBag[pick] == previous && themeBag.Count > 1) pick = (pick + 1) % themeBag.Count;
                themes.Add(themeBag[pick]);
                themeBag.RemoveAt(pick);
            }
            return themes[stage];
        }

        // ------------------------------------------------------------------ scheduling

        public bool CanFollow(PatternDefinition pattern)
        {
            if (!hasLastEvent || pattern.events.Length == 0) return true;
            PatternEventDefinition first = null;
            foreach (PatternEventDefinition e in pattern.events) if (e.objectId != ObjectBomb && (first == null || e.beat < first.beat)) first = e;
            if (first == null) return true;
            double gap = config.PhraseBeats + first.beat - lastEventBeat;
            if ((lastWasMash || Gestures.Parse(first.gesture) == Gesture.Mash) && gap < 1 - Epsilon) return false;
            return Reachable(gap, lastEventLane, first.lane, config);
        }

        static string RoleFor(int indexInStage, int chunksInStage, int chunkIndex, RunConfig config, bool bossFights)
        {
            if (bossFights && indexInStage >= chunksInStage - config.BossChunks)
            {
                if (indexInStage == chunksInStage - 1) return RoleBossFinish;
                // the fight's second-to-last chunk carries the charge attack (when the fight is long enough)
                if (config.BossChunks >= 3 && indexInStage == chunksInStage - 2) return RoleBossCharge;
                return RoleBoss;
            }
            return indexInStage == 0 && chunkIndex > 0 ? RoleBreather : "";
        }

        public void EnsureScheduled(double beatNow, int? forcedTier = null)
        {
            if (events.Count > 64) events.RemoveAll(e => e.Resolved && e.EndBeat < beatNow - config.PhraseBeats);
            if (chunks.Count > 8) chunks.RemoveAll(c => c.StartBeat < beatNow - config.PhraseBeats * 2);
            while (nextChunk * config.PhraseBeats < beatNow + config.SpawnLeadBeats + config.PhraseBeats * 2)
            {
                double chunkBeat = nextChunk * config.PhraseBeats;
                int stage = StageOfChunk(nextChunk), inStage = nextChunk - FirstChunkOf(stage);
                if (inStage == 0 && nextChunk > 0) tempo.Add(chunkBeat, config.BpmForStage(stage));
                if (inStage == 0) PlanItems(nextChunk, ChunksIn(stage));
                string role = RoleFor(inStage, ChunksIn(stage), nextChunk, config, HasBossFights);
                DifficultyStep step = forcedTier.HasValue ? config.StepForTier(forcedTier.Value) : config.StepForSeconds(tempo.SecondsAt(chunkBeat));
                string tutorialId = role == "" ? TutorialAt(nextChunk) : null;
                bool tutorial = tutorialId != null;
                if (!tutorial && role == "" && soldierChunks.Contains(nextChunk)) role = RoleSoldier;
                chunkSeconds = tempo.SecondsAt(chunkBeat);
                PatternDefinition pattern = tutorial ? Array.Find(patterns, p => p.id == tutorialId) : PickPattern(step, role);
                // round 29: a new thing is remembered; its chunk and the next one (built around it) are practice
                bool practice = known != null && practiceChunk == nextChunk && !IsBossRole(pattern.role);
                string newKey;
                if (UnknownKeys(pattern, out newKey) > 0 && !IsBossRole(pattern.role))
                {
                    foreach (string k in NoveltyKeys(pattern)) known.Add(k);
                    lastIntroSeconds = chunkSeconds;
                    practice = !tutorial;
                    practiceKey = pattern.role == RoleSoldier ? null : newKey;
                    practiceChunk = practiceKey != null ? nextChunk + 1 : -1;
                }
                bool hasOffbeat = HasOffbeat(pattern);
                if (hasOffbeat && !FirstOffbeatChunkBeat.HasValue) FirstOffbeatChunkBeat = chunkBeat;
                bool boss = IsBossRole(pattern.role);
                int firstNew = events.Count;

                PatternEventDefinition[] sorted = (PatternEventDefinition[])pattern.events.Clone();
                Array.Sort(sorted, (a, b) => a.beat.CompareTo(b.beat));
                foreach (PatternEventDefinition source in sorted)
                {
                    Gesture gesture = Gestures.Parse(source.gesture);
                    double length = gesture == Gesture.Tap ? 0 : source.length;
                    events.Add(new ScheduledEvent
                    {
                        Id = nextChunk + ":" + pattern.id + ":" + source.id,
                        Beat = chunkBeat + source.beat,
                        Lane = gesture == Gesture.Mash ? 1 : source.lane,
                        ObjectId = source.objectId,
                        Offbeat = IsOffbeat(source.beat) && source.objectId != ObjectBomb,
                        Resolved = false,
                        Rating = Rating.Ignored,
                        Gesture = gesture,
                        Length = length,
                        MashRequired = gesture == Gesture.Mash ? Math.Max(1, (int)Math.Ceiling(length * config.MashTapsPerBeat - Epsilon)) : 0,
                        Boss = boss,
                        Gold = source.objectId == "object_boss_gold",
                        // round 26: the crow cook's long strike is a charge too (hold to guard), outside boss fights as well
                        Charge = (boss || (source.objectId != null && source.objectId.StartsWith(SoldierPrefix))) && gesture == Gesture.Hold,
                        Tutorial = tutorial || practice,
                        Stage = stage,
                        SlideLanes = source.gesture == "slide" && source.path != null ? (int[])source.path.Clone() : null,
                        SlideBeats = source.gesture == "slide" && source.turns != null ? Array.ConvertAll(source.turns, t => chunkBeat + source.beat + t) : null,
                        Soldier = source.objectId != null && source.objectId.StartsWith(SoldierPrefix) ? source.objectId.Substring(SoldierPrefix.Length) : null,
                        SoldierGroup = source.objectId != null && source.objectId.StartsWith(SoldierPrefix) ? nextChunk + ":" + source.objectId : null,
                        Bomb = source.objectId == ObjectBomb,
                        Splits = source.objectId == ObjectSplit,
                    });
                }
                // round 27: the halves of a split fruit pop out where (and when) their big fruit reaches the line
                foreach (PatternEventDefinition source in sorted)
                {
                    if (string.IsNullOrEmpty(source.from)) continue;
                    ScheduledEvent half = events.Find(x => x.Id == nextChunk + ":" + pattern.id + ":" + source.id);
                    ScheduledEvent whole = events.Find(x => x.Id == nextChunk + ":" + pattern.id + ":" + source.from);
                    if (half == null || whole == null) continue;
                    half.SplitFromBeat = whole.Beat;
                    half.SplitFromLane = whole.Lane;
                }
                MarkGiant(firstNew);
                // the last strike of each soldier is its counter (sorted by beat: the final one wins)
                for (int i = events.Count - 1; i >= firstNew; i--)
                {
                    ScheduledEvent s = events[i];
                    if (s.SoldierGroup == null) continue;
                    bool later = false;
                    for (int j = i + 1; j < events.Count; j++) if (events[j].SoldierGroup == s.SoldierGroup) { later = true; break; }
                    s.SoldierFinal = !later;
                }
                bool soldier = pattern.role == RoleSoldier;
                if (!boss && !tutorial && !soldier && itemChunks.Contains(nextChunk)) PlaceItem(firstNew);
                if (!boss && !tutorial && !soldier && heartChunks.Contains(nextChunk)) PlaceItem(firstNew, ItemKind.Heart);
                chunks.Add(new ChunkInfo { Index = nextChunk, StartBeat = chunkBeat, PatternId = pattern.id, Tier = pattern.difficultyTier, HasOffbeat = hasOffbeat, Role = boss ? pattern.role : "", Stage = stage, Practice = practice });
                PatternEventDefinition last = null;
                foreach (PatternEventDefinition e in sorted) if (e.objectId != ObjectBomb && (last == null || e.beat + e.length >= last.beat + last.length)) last = e;
                if (last != null)
                {
                    hasLastEvent = true;
                    lastWasMash = Gestures.Parse(last.gesture) == Gesture.Mash;
                    lastEventBeat = last.beat + (Gestures.Parse(last.gesture) == Gesture.Tap ? 0 : last.length);
                    lastEventLane = lastWasMash ? 1 : last.gesture == "slide" && last.path != null && last.path.Length > 0 ? last.path[last.path.Length - 1] : last.lane;
                }
                else if (hasLastEvent) lastEventBeat -= config.PhraseBeats;
                recentPatternIds.Add(pattern.id);
                if (recentPatternIds.Count > 4) recentPatternIds.RemoveAt(0);
                nextChunk++;
            }
        }

        // ------------------------------------------------------------------ items

        /// <summary>Chooses which normal chunks of a stage carry an item (never the breather or the boss fight).</summary>
        void PlanItems(int stageFirstChunk, int chunksInStage)
        {
            int first = Math.Max(stageFirstChunk + 2, TutorialChunks), last = stageFirstChunk + chunksInStage - (HasBossFights ? config.BossChunks : 0) - 1;
            var candidates = new List<int>();
            for (int c = first; c <= last; c++) candidates.Add(c);
            // heart capsules sit around the middle of the stage: a fixed chance to recover before the boss (owner round 9)
            for (int i = 0; i < config.HeartsPerStage && candidates.Count > 0; i++)
            {
                int mid = Math.Min(candidates.Count - 1, (candidates.Count * (i + 1)) / (config.HeartsPerStage + 1));
                heartChunks.Add(candidates[mid]);
                candidates.RemoveAt(mid);
            }
            for (int i = 0; i < config.ItemsPerStage && candidates.Count > 0; i++)
            {
                int pick = itemRandom.Next(candidates.Count);
                itemChunks.Add(candidates[pick]);
                candidates.RemoveAt(pick);
            }
            // soldiers (round 17): a few chunks of the stage, never next to each other, never in the first two chunks
            int soldiers = stageFirstChunk == 0 ? config.SoldiersFirstStage : config.SoldiersPerStage;
            for (int i = 0; i < soldiers && candidates.Count > 0; i++)
            {
                int pick = itemRandom.Next(candidates.Count), chunk = candidates[pick];
                soldierChunks.Add(chunk);
                candidates.RemoveAll(c => Math.Abs(c - chunk) <= 1);
            }
        }

        /// <summary>Turns one tap of the chunk into an item capsule (same beat, same lane: the rhythm never changes).</summary>
        void PlaceItem(int firstNew, ItemKind kind = ItemKind.None)
        {
            var taps = new List<ScheduledEvent>();
            for (int i = firstNew; i < events.Count; i++)
            {
                ScheduledEvent t = events[i];
                // bombs and split fruit keep what they are (a capsule there would change the move)
                if (t.Gesture == Gesture.Tap && !t.Offbeat && t.Item == ItemKind.None && !t.Bomb && !t.Splits && double.IsNaN(t.SplitFromBeat) && t.Soldier == null) taps.Add(t);
            }
            if (taps.Count == 0) return;
            ScheduledEvent e = taps[itemRandom.Next(taps.Count)];
            double total = config.BoosterWeight + config.GiantWeight + config.BarrierWeight, roll = itemRandom.NextDouble() * Math.Max(1e-6, total);
            e.Item = kind != ItemKind.None ? kind : roll < config.BoosterWeight ? ItemKind.Booster : roll < config.BoosterWeight + config.GiantWeight ? ItemKind.Giant : ItemKind.Barrier;
            e.ObjectId = "object_item_" + e.Item.ToString().ToLowerInvariant();
        }

        /// <summary>
        /// Round 27 giant: everything reaching the line inside the giant window can be cut from any lane (it glows gold so the
        /// end is visible on the road), and the beat right after the window is cleared so the hand can find its lane again.
        /// </summary>
        void MarkGiant(int fromIndex)
        {
            if (giantUntil < 0) return;
            for (int i = fromIndex; i < events.Count; i++)
            {
                ScheduledEvent e = events[i];
                if (e.Resolved || e.Boss) continue;
                if (e.Beat > giantFrom + Epsilon && e.Beat < giantUntil - Epsilon) { e.Giant = true; continue; }
                bool gap = e.Beat >= giantUntil - Epsilon && e.Beat < giantUntil + config.GiantGapBeats - Epsilon;
                // the breathing beat: plain targets there are taken off the road (never a soldier, a mash or a capsule)
                if (gap && !e.Holding && e.Soldier == null && e.Gesture != Gesture.Mash && e.Item == ItemKind.None)
                {
                    e.Resolved = true; e.Rating = Rating.Ignored; e.Skipped = true;
                }
            }
        }

        public bool GiantActive(double beat) { return beat >= giantFrom && beat < giantUntil; }

        void ApplyItem(ScheduledEvent e, JudgeResult into)
        {
            ItemsCollected++;
            into.ItemGot = e.Item;
            switch (e.Item)
            {
                case ItemKind.Booster:
                    // a booster cut during a rush only adds its metres (no rush inside a rush)
                    if (rushQueue.Count > 0) rushBonusAt.Add(e.Beat); else StartRush(e.Beat);
                    break;
                case ItemKind.Giant:
                    if (!GiantActive(e.Beat)) giantFrom = e.Beat;
                    giantUntil = Math.Max(giantUntil, e.Beat) + config.ItemBeats;
                    // the boss is always fought at normal size
                    int stage = StageAt(e.Beat);
                    if (HasBossFights && e.Beat < BossStartBeat(stage)) giantUntil = Math.Min(giantUntil, BossStartBeat(stage));
                    MarkGiant(0);
                    break;
                case ItemKind.Barrier:
                    Shields = Math.Min(config.MaxShields, Shields + 1);
                    break;
                case ItemKind.Heart:
                    if (Hp < config.InitialHp) { Hp++; into.Healed = true; into.Hp = Hp; }
                    else CoinUnits += config.HeartCoinUnits; // full: the capsule pays out instead
                    break;
            }
        }

        /// <summary>
        /// Booster rush: every target of the next <see cref="RunConfig.RushReachBeats"/> beats (never the boss's) is pulled in
        /// and cut, one per 16th note, so a beat's worth of music becomes "ja-ja-ja-ja". +RushBonusMeters at once.
        /// </summary>
        void StartRush(double from)
        {
            rushQueue.Clear();
            foreach (ScheduledEvent e in events)
                if (!e.Resolved && !e.Holding && !e.Boss && !e.Bomb && e.Beat > from && e.Beat <= from + config.RushReachBeats) rushQueue.Add(e);
            rushQueue.Sort((a, b) => a.Beat.CompareTo(b.Beat));
            rushFrom = from;
            rushNextBeat = from + config.RushStepBeats;
            rushUntil = rushNextBeat + Math.Max(1, rushQueue.Count) * config.RushStepBeats;
            rushBonusAt.Add(from);
        }

        void Rush(double beatNow, List<JudgeResult> results)
        {
            while (rushQueue.Count > 0 && beatNow >= rushNextBeat && !Ended)
            {
                ScheduledEvent e = rushQueue[0];
                rushQueue.RemoveAt(0);
                if (e.Resolved) continue;
                rushNextBeat += config.RushStepBeats;
                JudgeResult r;
                if (e.Gesture == Gesture.Mash) { e.MashCount = Math.Max(e.MashCount, e.MashRequired); r = CloseMash(e); }
                else { e.Resolved = true; e.Holding = false; e.Rating = Rating.Perfect; r = Hit(e, Rating.Perfect, "rush", ResultKind.Tap); }
                r.Rush = true;
                results.Add(r);
            }
        }

        public bool BoostActive(double beat) { return beat >= rushFrom && beat < rushUntil; }
        /// <summary>Round 27: the beat the giant shrinks back (negative when it never grew).</summary>
        public double GiantUntil { get { return giantUntil; } }

        /// <summary>Extra metres from rushes up to <paramref name="beat"/> (each rush jumps ahead at once).</summary>
        public double BoostBonusBeats(double beat)
        {
            double bonus = 0;
            foreach (double s in rushBonusAt) if (beat >= s) bonus += config.RushBonusMeters / Math.Max(1e-6, config.DistanceMetersPerBeat);
            return bonus;
        }

        /// <summary>Density used by the per-tier size filter: a hold counts as two taps, a mash as three.</summary>
        public static int Load(PatternDefinition p)
        {
            int load = 0;
            foreach (PatternEventDefinition e in p.events)
            {
                Gesture g = Gestures.Parse(e.gesture);
                // a slide counts its turns too: following a snaking object is busier than holding a straight one
                load += g == Gesture.Hold ? 2 + (e.gesture == "slide" && e.path != null ? 2 * e.path.Length : 0) : g == Gesture.Mash ? 3 : 1;
            }
            return load;
        }

        static bool TapOnly(PatternDefinition p)
        {
            foreach (PatternEventDefinition e in p.events) if (Gestures.Parse(e.gesture) != Gesture.Tap) return false;
            return true;
        }

        PatternDefinition PickPattern(DifficultyStep step, string role)
        {
            if (role == RoleSoldier)
            {
                // round 23: harder soldiers (gingerbread's offbeat doubles) only once the run allows them; else a normal chunk
                pool.Clear();
                foreach (PatternDefinition p in patterns)
                {
                    if (p.role != RoleSoldier || !CanFollow(p) || !PacedOk(p)) continue;
                    if (p.difficultyTier > step.tier || (!step.allowOffbeat && HasOffbeat(p))) continue;
                    pool.Add(p);
                }
                if (pool.Count > 0) return WeightedPick(pool);
                role = "";
            }
            if (role == RoleBossCharge && !Array.Exists(patterns, p => p.role == RoleBossCharge)) role = RoleBoss;
            if (IsBossRole(role))
            {
                // boss fights use their own patterns; with none in the data the stage simply carries on
                for (int pass = 0; pass < 2; pass++)
                {
                    pool.Clear();
                    foreach (PatternDefinition p in patterns)
                    {
                        if (p.role != role) continue;
                        if (pass == 0 && (p.difficultyTier > step.tier || (!step.allowOffbeat && HasOffbeat(p)) || !CanFollow(p))) continue;
                        if (pass == 1 && !CanFollow(p)) continue;
                        pool.Add(p);
                    }
                    if (pool.Count > 0) return WeightedPick(pool);
                }
                role = "";
            }
            bool breather = role == RoleBreather;
            if (known != null && practiceKey != null && practiceChunk == nextChunk && !breather)
            {
                // round 29: the practice chunk brings the new thing again, with nothing else new
                pool.Clear();
                foreach (PatternDefinition p in patterns)
                {
                    if (!string.IsNullOrEmpty(p.role) || p.difficultyTier > step.tier || !CanFollow(p) || !PacedOk(p)) continue;
                    if (!step.allowOffbeat && HasOffbeat(p)) continue;
                    if (NoveltyKeys(p).Contains(practiceKey)) pool.Add(p);
                }
                if (pool.Count > 0) return WeightedPick(pool);
            }
            string a = recentPatternIds.Count >= 2 ? recentPatternIds[recentPatternIds.Count - 2] : null;
            string b = recentPatternIds.Count >= 1 ? recentPatternIds[recentPatternIds.Count - 1] : null;
            // Preference order: sized + not tripled, sized, in tier + not tripled, in tier.
            for (int pass = 0; pass < 4; pass++)
            {
                bool needSize = pass < 2 && !breather, avoidRepeat = pass % 2 == 0;
                pool.Clear();
                foreach (PatternDefinition p in patterns)
                {
                    if (!string.IsNullOrEmpty(p.role)) continue; // boss and tutorial patterns are never picked at random
                    // the first chunk of a faster stage is a breather: easy taps only, so the speed-up can sink in
                    if (breather && (p.difficultyTier > Math.Min(step.tier, 1) || !TapOnly(p))) continue;
                    if (p.difficultyTier > step.tier) continue;
                    if (!step.allowOffbeat && HasOffbeat(p)) continue;
                    if (!CanFollow(p) || !PacedOk(p)) continue;
                    int load = Load(p);
                    if (needSize && (load < step.eventsMin || load > step.eventsMax)) continue;
                    if (avoidRepeat && a == p.id && b == p.id) continue;
                    pool.Add(p);
                }
                if (pool.Count > 0) return WeightedPick(pool);
            }
            return breather ? PickPattern(step, "") : fallback;
        }

        PatternDefinition WeightedPick(List<PatternDefinition> candidates)
        {
            double total = 0;
            foreach (PatternDefinition p in candidates) total += Math.Max(0.0001, p.weight);
            double roll = random.NextDouble() * total;
            foreach (PatternDefinition p in candidates) { roll -= Math.Max(0.0001, p.weight); if (roll < 0) return p; }
            return candidates[candidates.Count - 1];
        }

        static bool HasOffbeat(PatternDefinition p)
        {
            foreach (PatternEventDefinition e in p.events) if (IsOffbeat(e.beat) && e.objectId != ObjectBomb) return true;
            return false;
        }

        /// <summary>True during the chunk right before the first offbeat chunk.</summary>
        public bool IsOffbeatPreview(double beatNow)
        {
            if (!FirstOffbeatChunkBeat.HasValue || FirstOffbeatChunkBeat.Value <= 0) return false;
            double start = FirstOffbeatChunkBeat.Value;
            return beatNow >= start - config.PhraseBeats && beatNow < start;
        }

        public ChunkInfo ChunkAt(double beat)
        {
            foreach (ChunkInfo c in chunks) if (beat >= c.StartBeat && beat < c.StartBeat + config.PhraseBeats) return c;
            return null;
        }

        // ------------------------------------------------------------------ judgement

        ScheduledEvent OpenMash(double beatNow)
        {
            double now = tempo.SecondsAt(beatNow);
            foreach (ScheduledEvent e in events)
                if (!e.Resolved && e.Gesture == Gesture.Mash && now >= tempo.SecondsAt(e.Beat) - config.GoodMs / 1000.0 && beatNow <= e.EndBeat + Epsilon) return e;
            return null;
        }

        bool AnyHolding()
        {
            foreach (ScheduledEvent e in events) if (e.Holding && !e.Resolved) return true;
            return false;
        }

        /// <summary>A press on a lane. Taps and hold heads are judged; inside an open mash every press counts as a mash hit.</summary>
        public JudgeResult JudgeTap(int lane, double beatNow)
        {
            if (Ended) return Ignored();
            ScheduledEvent mash = OpenMash(beatNow);
            if (mash != null) return MashTap(mash);
            // presses right after a mash are leftovers of the mashing, never empty taps
            if (tempo.SecondsAt(beatNow) < mashGraceUntilSeconds) return Result(Rating.Ignored, "mash_grace", null);
            ScheduledEvent correct = null, nearest = null;
            double correctDistance = double.MaxValue, nearestDistance = double.MaxValue;
            bool bombNear = false;
            foreach (ScheduledEvent e in events)
            {
                if (e.Resolved || e.Holding || e.Gesture == Gesture.Mash) continue;
                double distance = Math.Abs(tempo.Ms(e.Beat, beatNow));
                if (distance > config.GoodMs) continue;
                if (e.Bomb)
                {
                    // round 27: only the bomb's own lane sets it off; a giant stomps bombs flat instead (never a candidate)
                    bombNear = true;
                    if (!e.Giant && e.Lane == lane && distance < correctDistance) { correct = e; correctDistance = distance; }
                    continue;
                }
                // an item is a bonus: tapping the wrong lane never "misses" it
                if (e.Item == ItemKind.None && !e.Giant && distance < nearestDistance) { nearest = e; nearestDistance = distance; }
                // round 27: a giant cuts from any lane
                if ((e.Lane == lane || e.Giant) && distance < correctDistance) { correct = e; correctDistance = distance; }
            }
            if (correct != null && correct.Bomb) return HitBomb(correct);
            if (correct != null)
            {
                double errorMs = tempo.Ms(correct.Beat, beatNow);
                Rating rating = Classify(errorMs, config);
                JudgeResult hit;
                if (correct.Gesture == Gesture.Hold && (rating == Rating.Perfect || rating == Rating.Good))
                {
                    correct.Holding = true;
                    correct.Rating = rating;
                    hit = Hit(correct, rating, null, ResultKind.HoldHead);
                }
                else hit = Resolve(correct, rating, null);
                hit.ErrorMs = errorMs;
                hit.Giant = correct.Giant;
                return hit;
            }
            if (nearest != null) return Resolve(nearest, Rating.Miss, "wrong_lane");
            // a stray finger while holding never breaks the hold's combo
            if (AnyHolding()) return Result(Rating.Ignored, "holding", null);
            // round 27: a giant's swing at nothing, or a finger kept off a bomb's lane, never breaks the combo
            if (GiantActive(beatNow)) { JudgeResult swing = Result(Rating.Ignored, "giant_swing", null); swing.Giant = true; return swing; }
            if (bombNear) return Result(Rating.Ignored, "near_bomb", null);
            Combo = 0; EmptyCount++; PerfectStreak = 0;
            if (config.EmptyTapDamagesHp) Damage();
            return Result(Rating.Empty, "no_candidate", null);
        }

        /// <summary>A lane was released. Ends a hold: near (or after) its end = success, clearly early = the hold breaks (no HP loss).</summary>
        public JudgeResult Release(int lane, double beatNow)
        {
            if (Ended) return Ignored();
            foreach (ScheduledEvent e in events)
            {
                // a slide is let go wherever the finger has slid to
                if (!e.Holding || e.Resolved || (e.Lane != lane && !e.IsSlide && !e.Giant)) continue;
                if (tempo.Ms(beatNow, e.EndBeat) <= config.HoldReleaseLeniencyMs) return CompleteHold(e);
                return BreakHold(e, "released_early");
            }
            return Ignored();
        }

        JudgeResult BreakHold(ScheduledEvent e, string reason)
        {
            e.Holding = false; e.Resolved = true; e.TailBroken = true;
            Combo = 0; PerfectStreak = 0;
            JudgeResult broken = Result(Rating.Miss, reason, e);
            broken.Kind = ResultKind.HoldBreak; // like an early release: no heart is lost
            return broken;
        }

        /// <summary>A slide is being held right now (its finger may wander to other lanes).</summary>
        public bool SlideHolding { get { foreach (ScheduledEvent e in events) if (e.Holding && !e.Resolved && e.IsSlide) return true; return false; } }

        /// <summary>
        /// Slides (owner round 11): every frame, the lane under the finger (-1 = none). Off the path for longer than
        /// <see cref="RunConfig.SlideGraceMs"/> breaks the slide (no heart, like an early release); close to a turn both the
        /// lane before and the lane after count; no finger near the end completes it.
        /// </summary>
        public JudgeResult Steer(int lane, double beatNow, int extraGraceMs = 0)
        {
            if (Ended) return Ignored();
            foreach (ScheduledEvent e in events)
            {
                if (!e.Holding || e.Resolved || !e.IsSlide) continue;
                if (lane < 0 && tempo.Ms(beatNow, e.EndBeat) <= config.HoldReleaseLeniencyMs) return CompleteHold(e);
                if (OnPath(e, lane, beatNow)) { e.OffSince = double.NaN; continue; }
                if (double.IsNaN(e.OffSince)) { e.OffSince = beatNow; continue; }
                if (tempo.Ms(e.OffSince, beatNow) <= config.SlideGraceMs + extraGraceMs) continue;
                return BreakHold(e, lane < 0 ? "released_early" : "slid_off");
            }
            return Ignored();
        }

        bool OnPath(ScheduledEvent e, int lane, double beatNow)
        {
            if (lane < 0) return false;
            if (lane == e.LaneAt(beatNow)) return true;
            for (int i = 0; i < e.SlideBeats.Length; i++)
            {
                int before = i == 0 ? e.Lane : e.SlideLanes[i - 1];
                if (Math.Abs(tempo.Ms(e.SlideBeats[i], beatNow)) <= config.SlideGraceMs && (lane == before || lane == e.SlideLanes[i])) return true;
            }
            return false;
        }

        JudgeResult CompleteHold(ScheduledEvent e)
        {
            e.Holding = false; e.Resolved = true;
            HoldsCompleted++;
            return Hit(e, Rating.Perfect, null, ResultKind.HoldTail);
        }

        JudgeResult MashTap(ScheduledEvent e)
        {
            e.MashCount++;
            // round 29 (owner: "MAXと出てからまだ何回も叩く"): a hard object breaks the moment the count is reached;
            // the presses left over until its end fall into the after-mash grace (never empty taps)
            if (!e.Boss && e.MashCount >= e.MashRequired) return CloseMash(e);
            JudgeResult tick = Result(Rating.Tick, "mash", e);
            tick.Kind = ResultKind.MashTap;
            if (e.Boss && BossActive && bossHp > 0)
            {
                bossHp -= (double)config.BossMashDamageTotal / Math.Max(1, e.MashRequired);
                if (bossHp <= Epsilon) DefeatBoss(tick);
            }
            return tick;
        }

        readonly List<ScheduledEvent> expiredScratch = new List<ScheduledEvent>(8);

        readonly List<JudgeResult> expireResults = new List<JudgeResult>(8);

        /// <summary>
        /// Advances time: starts boss fights, auto-completes holds held to their end, closes mashes and resolves overdue
        /// taps as MISS. The returned list is reused on the next call.
        /// </summary>
        public List<JudgeResult> Expire(double beatNow)
        {
            var results = expireResults;
            results.Clear();
            if (Ended) return results;
            UpdateBoss(beatNow, results);
            Rush(beatNow, results);
            if (Ended) return results;
            expiredScratch.Clear();
            foreach (ScheduledEvent e in events)
            {
                if (e.Resolved) continue;
                if (e.Holding) { if (beatNow >= e.EndBeat - Epsilon) expiredScratch.Add(e); }
                else if (e.Gesture == Gesture.Mash) { if (beatNow > e.EndBeat + Epsilon) expiredScratch.Add(e); }
                else if (tempo.Ms(e.Beat, beatNow) > config.GoodMs) expiredScratch.Add(e);
            }
            if (expiredScratch.Count == 0) return results;
            expiredScratch.Sort((x, y) => x.Beat.CompareTo(y.Beat));
            foreach (ScheduledEvent item in expiredScratch)
            {
                if (Ended) break;
                if (item.Holding) results.Add(CompleteHold(item));
                else if (item.Gesture == Gesture.Mash) results.Add(CloseMash(item));
                else if (item.Bomb) results.Add(PassBomb(item));
                else results.Add(Resolve(item, Rating.Miss, "timeout"));
            }
            return results;
        }

        /// <summary>Round 27: a bomb was cut. The combo breaks, but no heart is lost (the barrier is not used either).</summary>
        JudgeResult HitBomb(ScheduledEvent e)
        {
            e.Resolved = true; e.Rating = Rating.Miss;
            Combo = 0; PerfectStreak = 0; BombHits++;
            JudgeResult r = Result(Rating.Miss, "bomb", e);
            r.BombHit = true;
            return r;
        }

        /// <summary>Round 27: a bomb went by uncut — a dodge (a little score, the combo untouched); a giant stomps it flat.</summary>
        JudgeResult PassBomb(ScheduledEvent e)
        {
            e.Resolved = true; e.Rating = Rating.Ignored;
            BombsDodged++;
            Score += config.BombDodgeScore;
            JudgeResult r = Result(Rating.Ignored, e.Giant ? "bomb_crushed" : "bomb_dodged", e);
            if (e.Giant) r.Crushed = true; else r.Dodged = true;
            return r;
        }

        JudgeResult CloseMash(ScheduledEvent e)
        {
            mashGraceUntilSeconds = tempo.SecondsAt(e.EndBeat) + config.MashAfterGraceMs / 1000.0;
            Rating rating = e.MashCount >= e.MashRequired ? Rating.Perfect : e.MashCount * 2 >= e.MashRequired ? Rating.Good : Rating.Miss;
            if (rating != Rating.Miss) MashesCleared++;
            JudgeResult result = Resolve(e, rating, "mash_closed");
            result.Kind = ResultKind.MashEnd;
            // the finisher is over: a boss that is still standing runs away
            if (e.Boss && BossActive && !Ended) EscapeBoss(result);
            return result;
        }

        public JudgeResult Resolve(ScheduledEvent item, Rating rating, string reason = null)
        {
            if (Ended || item == null || item.Resolved) return Ignored();
            item.Resolved = true;
            item.Holding = false;
            item.Rating = rating;
            if (rating == Rating.Perfect || rating == Rating.Good) return Hit(item, rating, reason, ResultKind.Tap);
            // a missed item capsule simply goes by: no heart, no combo break
            if (item.Item != ItemKind.None && rating == Rating.Miss) return Result(Rating.Ignored, "item_missed", item);
            Combo = 0; MissCount++; PerfectStreak = 0;
            bool blocked = !item.Tutorial && Damage(); // practice MISSes cost no heart
            JudgeResult miss = Result(rating, reason, item);
            miss.ShieldBlocked = blocked;
            return miss;
        }

        JudgeResult Hit(ScheduledEvent item, Rating rating, string reason, ResultKind kind)
        {
            Combo++; MaxCombo = Math.Max(MaxCombo, Combo);
            if (Combo == config.FeverCombo) FeverCount++;
            // FEVER covers the hit that reaches FeverCombo and every hit after it until the combo breaks.
            if (Fever) { CoinUnits += 2; FeverHits++; } else CoinUnits += 1;
            PerfectStreak = rating == Rating.Perfect ? PerfectStreak + 1 : 0;
            int baseScore = rating == Rating.Perfect ? config.PerfectBase : config.GoodBase;
            double multiplier = 1.0 + Math.Min((double)Combo / config.ComboStep, config.ComboMaxMultiplier - 1.0);
            Score += (int)Math.Round(baseScore * multiplier);
            if (rating == Rating.Perfect) PerfectCount++; else GoodCount++;
            JudgeResult result = Result(rating, reason, item);
            result.Kind = item.Gesture == Gesture.Mash ? ResultKind.MashEnd : kind;
            if (item.Item != ItemKind.None) ApplyItem(item, result);
            if (item.Boss && BossActive && bossHp > 0)
            {
                int damage = 0;
                if (item.Soldier != null)
                    // round 17: a parried soldier's last strike sends it flying into the boss
                    damage = item.SoldierFinal ? (rating == Rating.Perfect ? config.BossGoldDamagePerfect : config.BossGoldDamageGood) : (rating == Rating.Perfect ? config.BossShotDamagePerfect : config.BossShotDamageGood);
                else if (item.Gesture == Gesture.Tap)
                    damage = item.Gold ? (rating == Rating.Perfect ? config.BossGoldDamagePerfect : config.BossGoldDamageGood) : (rating == Rating.Perfect ? config.BossShotDamagePerfect : config.BossShotDamageGood);
                else if (item.Charge && kind == ResultKind.HoldTail) damage = config.BossChargeDamage; // guarded to the end: the ball goes back
                result.Critical = item.Gold && rating == Rating.Perfect;
                result.BossDamage = damage;
                bossHp -= damage;
                if (bossHp <= Epsilon) DefeatBoss(result);
            }
            return result;
        }

        // ------------------------------------------------------------------ boss fights

        void UpdateBoss(double beatNow, List<JudgeResult> results)
        {
            if (BossActive && beatNow >= StageStartBeat(bossStage + 1))
            {
                // safety net: a fight without a finisher ends with the stage
                var escape = Result(Rating.Ignored, "boss_timeout", null);
                EscapeBoss(escape);
                results.Add(escape);
            }
            if (BossActive) return;
            foreach (ChunkInfo c in chunks)
            {
                if (!IsBossRole(c.Role) || c.Stage <= bossStage) continue;
                if (beatNow < c.StartBeat || beatNow >= StageStartBeat(c.Stage + 1)) continue;
                bossStage = c.Stage;
                BossActive = true;
                bossHp = config.BossMaxHp;
                return;
            }
        }

        void DefeatBoss(JudgeResult into)
        {
            BossActive = false;
            bossHp = 0;
            BossesDefeated++;
            Hp = Math.Min(config.InitialHp, Hp + config.BossHealHp);
            CoinUnits += config.BossBonusCoinUnits;
            Score += config.BossBonusScore;
            into.BossDefeated = true;
            into.Hp = Hp; into.Score = Score;
        }

        void EscapeBoss(JudgeResult into)
        {
            BossActive = false;
            BossesEscaped++;
            into.BossEscaped = true;
        }

        /// <summary>Takes a heart, or the barrier if one is up. Returns true when the barrier took it.</summary>
        bool Damage()
        {
            if (Shields > 0) { Shields--; return true; }
            Hp = Math.Max(0, Hp - 1);
            if (Hp == 0) Ended = true;
            return false;
        }

        public static Rating Classify(double errorMs) { return Classify(errorMs, RunConfig.Default); }

        public static Rating Classify(double errorMs, RunConfig config)
        {
            double absolute = Math.Abs(errorMs);
            if (absolute <= config.PerfectMs) return Rating.Perfect;
            if (absolute <= config.GoodMs) return Rating.Good;
            return Rating.Miss;
        }

        public RunSnapshot Snapshot(double beatNow) { return Snapshot(beatNow, new RunSnapshot()); }

        /// <summary>Fills an existing snapshot (no per-frame allocation).</summary>
        public RunSnapshot Snapshot(double beatNow, RunSnapshot into)
        {
            into.Hp = Hp; into.MaxHp = config.InitialHp; into.Score = Score; into.Combo = Combo; into.MaxCombo = MaxCombo;
            into.Distance = Math.Max(0, (int)Math.Floor((beatNow + BoostBonusBeats(beatNow)) * config.DistanceMetersPerBeat));
            into.Perfect = PerfectCount; into.Good = GoodCount; into.Miss = MissCount; into.Empty = EmptyCount; into.Ended = Ended;
            into.CoinUnits = CoinUnits; into.FeverHits = FeverHits; into.Fever = Fever; into.FeverLevel = FeverLevel; into.PerfectStreak = PerfectStreak;
            into.FeverCount = FeverCount;
            into.Coins = CoinsFor(CoinUnits, into.Distance, config);
            into.Stage = StageAt(Math.Max(0, beatNow));
            into.Theme = ThemeForStage(into.Stage);
            into.Bpm = tempo.BpmAt(beatNow);
            into.BossActive = BossActive; into.BossHp01 = BossHp01;
            into.BossesDefeated = BossesDefeated; into.BossesEscaped = BossesEscaped;
            into.BossRage = BossRage;
            into.Boost = BoostActive(beatNow);
            into.Boost01 = into.Boost ? (float)Math.Max(0, Math.Min(1, (rushUntil - beatNow) / Math.Max(.01, rushUntil - rushFrom))) : 0f;
            into.Giant = GiantActive(beatNow);
            into.GiantBeatsLeft = into.Giant ? giantUntil - beatNow : 0;
            into.Giant01 = into.Giant ? (float)Math.Max(0, Math.Min(1, (giantUntil - beatNow) / Math.Max(.01, giantUntil - giantFrom))) : 0f;
            into.Shields = Shields;
            return into;
        }

        /// <summary>floor(units / 3) + DistanceBonusCoins per DistanceBonusMeters (D-011). Without FEVER units = successes.</summary>
        public static int CoinsFor(int coinUnits, int distance, RunConfig config)
        {
            return coinUnits / 3 + config.DistanceBonusCoins * (distance / config.DistanceBonusMeters);
        }

        public static int CoinsForRun(RunSnapshot snapshot) { return CoinsFor(snapshot.CoinUnits, snapshot.Distance, RunConfig.Default); }

        JudgeResult Ignored() { return Result(Rating.Ignored, "already_resolved_or_ended", null); }
        JudgeResult Result(Rating rating, string reason, ScheduledEvent item)
        {
            return new JudgeResult { Rating = rating, Reason = reason, Event = item, Hp = Hp, Score = Score, Combo = Combo, Ended = Ended, Kind = ResultKind.Tap };
        }
    }

    public static class PatternValidator
    {
        public static string Validate(PatternDefinition pattern) { return Validate(pattern, RunConfig.Default); }

        public static string Validate(PatternDefinition pattern, RunConfig config)
        {
            if (pattern == null || pattern.events == null) return "Pattern or events are null.";
            if (pattern.durationBeats != config.PhraseBeats) return pattern.id + ": duration must be " + config.PhraseBeats + ".";
            if (!string.IsNullOrEmpty(pattern.role) && !RunEngine.IsBossRole(pattern.role) && pattern.role != RunEngine.RoleTutorial && pattern.role != RunEngine.RoleSoldier) return pattern.id + ": unknown role.";
            if (pattern.role == RunEngine.RoleSoldier)
            {
                // a soldier chunk is taps (a soldier's own strike may also be a held guard, round 26), and at least one strike is a soldier's
                foreach (PatternEventDefinition e in pattern.events)
                    if (e.gesture != "tap" && !(e.gesture == "hold" && e.objectId != null && e.objectId.StartsWith(RunEngine.SoldierPrefix))) return pattern.id + ": soldier chunks are taps (and soldier guards) only.";
                if (!Array.Exists(pattern.events, e => e.objectId != null && e.objectId.StartsWith(RunEngine.SoldierPrefix))) return pattern.id + ": a soldier chunk needs a soldier.";
            }
            var ids = new HashSet<string>();
            string special = ValidateBombsAndSplits(pattern, config, ids);
            if (special != null) return special;
            // bombs are never reached for: the lane-chain rules below only see what is cut
            PatternEventDefinition[] ordered = Array.FindAll(pattern.events, x => x.objectId != RunEngine.ObjectBomb);
            Array.Sort(ordered, (a, b) => a.beat.CompareTo(b.beat));
            int mashes = 0;
            for (int i = 0; i < ordered.Length; i++)
            {
                PatternEventDefinition e = ordered[i];
                if (e.beat < 0 || e.beat >= config.PhraseBeats) return pattern.id + ": beat out of range.";
                if (Math.Abs(e.beat * 2 - Math.Round(e.beat * 2)) > 0.0001) return pattern.id + ": unsupported subdivision.";
                if (e.lane < 0 || e.lane > 2) return pattern.id + ": lane out of range.";
                if (e.gesture != "tap" && e.gesture != "hold" && e.gesture != "mash" && e.gesture != "slide") return pattern.id + ": gesture must be tap, hold, slide or mash.";
                if (e.gesture == "slide")
                {
                    // a slide turns on whole beats, at least a beat apart, one lane at a time (edge to edge only from tier 4)
                    if (e.path == null || e.turns == null || e.path.Length == 0 || e.path.Length != e.turns.Length) return pattern.id + ": a slide needs matching path and turns.";
                    int from = e.lane; double last = 0;
                    for (int k = 0; k < e.path.Length; k++)
                    {
                        double at = e.turns[k];
                        if (at < 1 - 1e-6 || at > e.length - 1 + 1e-6 || at - last < 1 - 1e-6 || Math.Abs((e.beat + at) - Math.Round(e.beat + at)) > 1e-6) return pattern.id + ": slide turns must be on whole beats, a beat apart, inside the slide.";
                        if (e.path[k] < 0 || e.path[k] > 2 || e.path[k] == from) return pattern.id + ": slide lanes must change at every turn.";
                        if (Math.Abs(e.path[k] - from) == 2 && pattern.difficultyTier < 4) return pattern.id + ": edge-to-edge slides only from tier 4.";
                        from = e.path[k]; last = at;
                    }
                }
                Gesture g = Gestures.Parse(e.gesture);
                if (g != Gesture.Tap && Math.Abs(e.length * 2 - Math.Round(e.length * 2)) > 0.0001) return pattern.id + ": unsupported length.";
                if (g == Gesture.Hold && (e.length < 1 || e.length > 4 || e.beat + e.length > config.PhraseBeats - .5 + 1e-6)) return pattern.id + ": hold length must be 1-4 beats and end by beat 7.5.";
                if (g == Gesture.Mash && (e.length < 2 || e.length > 4 || e.beat + e.length > config.PhraseBeats - 1 + 1e-6)) return pattern.id + ": mash length must be 2-4 beats and end by beat 7.";
                if (g == Gesture.Mash) mashes++;
                if (!ids.Add(e.id)) return pattern.id + ": duplicate event id.";
                if (i > 0)
                {
                    PatternEventDefinition prev = ordered[i - 1];
                    Gesture pg = Gestures.Parse(prev.gesture);
                    double gap = e.beat - (prev.beat + (pg == Gesture.Tap ? 0 : prev.length));
                    if (gap < config.MinimumIntervalBeats - 1e-6) return pattern.id + ": interval below " + config.MinimumIntervalBeats + " beat.";
                    if ((pg == Gesture.Mash || g == Gesture.Mash) && gap < 1 - 1e-6) return pattern.id + ": a mash needs a beat of space on both sides.";
                    int prevLane = pg == Gesture.Mash ? 1 : prev.gesture == "slide" && prev.path != null && prev.path.Length > 0 ? prev.path[prev.path.Length - 1] : prev.lane;
                    if (!RunEngine.Reachable(gap, prevLane, g == Gesture.Mash ? 1 : e.lane, config)) return pattern.id + ": unreachable half-beat lane jump.";
                }
            }
            if (mashes > 1) return pattern.id + ": at most one mash per chunk.";
            if (pattern.role == RunEngine.RoleBossFinish && mashes != 1) return pattern.id + ": a boss finisher ends with exactly one mash.";
            return null;
        }

        /// <summary>
        /// Round 27. Bombs: plain taps on whole or half beats, never in a boss fight, at least a beat away from anything cut in
        /// their own lane (so a bomb is never mistaken for the next target). Split fruit: each half names the big fruit it
        /// breaks off from, comes one or two beats after it, and every big fruit has a half.
        /// </summary>
        static string ValidateBombsAndSplits(PatternDefinition pattern, RunConfig config, HashSet<string> bombIds)
        {
            foreach (PatternEventDefinition b in pattern.events)
            {
                if (b.objectId != RunEngine.ObjectBomb) continue;
                if (b.gesture != "tap") return pattern.id + ": a bomb is a tap.";
                if (RunEngine.IsBossRole(pattern.role) || pattern.role == RunEngine.RoleSoldier) return pattern.id + ": no bombs in boss or soldier chunks.";
                if (b.beat < 0 || b.beat >= config.PhraseBeats || b.lane < 0 || b.lane > 2) return pattern.id + ": bomb out of range.";
                if (!bombIds.Add(b.id)) return pattern.id + ": duplicate event id.";
                foreach (PatternEventDefinition e in pattern.events)
                {
                    if (e == b || e.lane != b.lane) continue;
                    double end = e.beat + (Gestures.Parse(e.gesture) == Gesture.Tap ? 0 : e.length);
                    if (b.beat > e.beat - 1 + 1e-6 && b.beat < end + 1 - 1e-6) return pattern.id + ": a bomb needs a beat of space in its lane.";
                }
            }
            foreach (PatternEventDefinition h in pattern.events)
            {
                bool half = h.objectId == RunEngine.ObjectHalf;
                if (half != !string.IsNullOrEmpty(h.from)) return pattern.id + ": a split half (and only a half) names the fruit it comes from.";
                if (!half) continue;
                PatternEventDefinition whole = Array.Find(pattern.events, x => x.id == h.from);
                if (whole == null || whole.objectId != RunEngine.ObjectSplit) return pattern.id + ": a half must come from a big fruit.";
                if (h.gesture != "tap" || whole.gesture != "tap") return pattern.id + ": split fruit are taps.";
                double after = h.beat - whole.beat;
                if (after < 1 - 1e-6 || after > 2 + 1e-6) return pattern.id + ": a half comes one or two beats after its fruit.";
            }
            foreach (PatternEventDefinition w in pattern.events)
                if (w.objectId == RunEngine.ObjectSplit && !Array.Exists(pattern.events, x => x.from == w.id)) return pattern.id + ": a big fruit needs its halves.";
            return null;
        }
    }
}
