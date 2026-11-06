// Pure rules for the browser prototype. Mirrors unity_project RunEngine; data comes from data/*.json.

export const DEFAULT_CONFIG = Object.freeze({
  bpm: 120,
  phraseBeats: 8,
  spawnLeadBeats: 4,
  initialHp: 4,
  perfectMs: 80,
  goodMs: 160,
  minimumIntervalBeats: 0.5,
  distanceMetersPerBeat: 1,
  emptyTapDamagesHp: false,
  perfectBase: 1000,
  goodBase: 650,
  comboStep: 50,
  comboMaxMultiplier: 1.5,
  fallbackPatternId: 'basic_center_01',
  feverCombo: 50,
  feverLevelStep: 50,
  distanceBonusMeters: 50,
  distanceBonusCoins: 3,
  difficulty: Object.freeze([
    { fromSeconds: 0, tier: 0, eventsMin: 4, eventsMax: 4, allowOffbeat: false },
    { fromSeconds: 16, tier: 1, eventsMin: 4, eventsMax: 6, allowOffbeat: false },
    { fromSeconds: 32, tier: 2, eventsMin: 6, eventsMax: 8, allowOffbeat: false },
    { fromSeconds: 64, tier: 3, eventsMin: 8, eventsMax: 10, allowOffbeat: true },
    { fromSeconds: 96, tier: 4, eventsMin: 10, eventsMax: 12, allowOffbeat: true },
    { fromSeconds: 128, tier: 5, eventsMin: 10, eventsMax: 12, allowOffbeat: true },
  ]),
});

const KIND_BY_OBJECT = { object_strawberry: 'strawberry', object_watermelon: 'watermelon', object_barrel: 'barrel' };

/** Builds the engine config from data/game_config.json. */
export function configFromGameConfig(json) {
  return Object.freeze({
    ...DEFAULT_CONFIG,
    bpm: json.run.bpm,
    phraseBeats: json.run.phraseBeats,
    spawnLeadBeats: json.run.spawnLeadBeats,
    initialHp: json.run.initialHp,
    minimumIntervalBeats: json.run.minimumIntervalBeats,
    distanceMetersPerBeat: json.run.distanceMetersPerBeat,
    perfectMs: json.timing.perfectMs,
    goodMs: json.timing.goodMs,
    emptyTapDamagesHp: json.score.emptyTapDamagesHp,
    perfectBase: json.score.perfectBase,
    goodBase: json.score.goodBase,
    comboStep: json.score.comboStep,
    comboMaxMultiplier: json.score.comboMaxMultiplier,
    difficulty: Object.freeze(json.difficulty.map(d => Object.freeze({ ...d }))),
  });
}

/**
 * Converts data/patterns.json into engine patterns. The browser prototype covers the tap rules only: hold/mash
 * patterns and boss-fight patterns (Unity round 2) are skipped here.
 */
export function patternsFromJson(json) {
  // the browser prototype plays plain taps only (no bombs or split melons: round 27 rules live in the Unity engine)
  const plain = e => (e.gesture ?? 'tap') === 'tap' && !['object_bomb', 'object_melon_big', 'object_melon_half'].includes(e.objectId);
  return json.patterns.filter(p => !p.role && p.events.every(plain)).map(p => Object.freeze({
    id: p.id,
    tier: p.difficultyTier,
    weight: p.weight ?? 1,
    durationBeats: p.durationBeats,
    events: Object.freeze(p.events.map(e => Object.freeze({ id: e.id, beat: Number(e.beat), lane: e.lane, objectId: e.objectId, gesture: e.gesture }))),
  }));
}

export function isOffbeat(beat) { return Math.abs(beat % 1 - 0.5) < 1e-6; }

export function difficultyForSeconds(seconds, config = DEFAULT_CONFIG) {
  let current = config.difficulty[0];
  for (const step of config.difficulty) if (seconds >= step.fromSeconds) current = step;
  return current;
}

export function tierForSeconds(seconds, config = DEFAULT_CONFIG) { return difficultyForSeconds(seconds, config).tier; }

export function classifyTiming(errorMs, config = DEFAULT_CONFIG) {
  const absolute = Math.abs(errorMs);
  if (absolute <= config.perfectMs) return 'perfect';
  if (absolute <= config.goodMs) return 'good';
  return 'miss';
}

/** Two consecutive events are reachable when lane jumps of 2 get at least one beat. */
function reachable(gap, laneA, laneB, config) {
  if (gap < config.minimumIntervalBeats - 1e-6) return false;
  return !(gap < 1 - 1e-6 && Math.abs(laneA - laneB) > 1);
}

export function validatePattern(pattern, config = DEFAULT_CONFIG) {
  const errors = [];
  if (pattern.durationBeats !== undefined && pattern.durationBeats !== config.phraseBeats) errors.push('duration');
  const ids = new Set();
  const sorted = [...pattern.events].sort((a, b) => a.beat - b.beat);
  sorted.forEach((event, index) => {
    const { beat, lane } = event;
    if (beat < 0 || beat >= config.phraseBeats) errors.push(`event ${index}: beat`);
    if (![0, 1, 2].includes(lane)) errors.push(`event ${index}: lane`);
    if (beat * 2 !== Math.trunc(beat * 2)) errors.push(`event ${index}: subdivision`);
    if (event.gesture !== undefined && event.gesture !== 'tap') errors.push(`event ${index}: gesture`);
    if (event.id !== undefined) { if (ids.has(event.id)) errors.push(`event ${index}: duplicate id`); ids.add(event.id); }
    if (index) {
      const prev = sorted[index - 1];
      const gap = beat - prev.beat;
      if (gap < config.minimumIntervalBeats) errors.push(`event ${index}: interval`);
      else if (!reachable(gap, prev.lane, lane, config)) errors.push(`event ${index}: unreachable lane jump`);
    }
  });
  return errors;
}

/** True when `next` may directly follow a chunk whose last event was `prevLast`. */
export function canFollow(prevLast, pattern, config = DEFAULT_CONFIG) {
  if (!prevLast || pattern.events.length === 0) return true;
  const first = pattern.events.reduce((a, b) => (b.beat < a.beat ? b : a));
  return reachable(config.phraseBeats + first.beat - prevLast.beat, prevLast.lane, first.lane, config);
}

function mulberry32(seed) {
  return () => {
    seed |= 0; seed = seed + 0x6D2B79F5 | 0;
    let t = Math.imul(seed ^ seed >>> 15, 1 | seed);
    t = t + Math.imul(t ^ t >>> 7, 61 | t) ^ t;
    return ((t ^ t >>> 14) >>> 0) / 4294967296;
  };
}

export class RunEngine {
  constructor({ config = DEFAULT_CONFIG, patterns, seed = 12345 } = {}) {
    if (!patterns || patterns.length === 0) throw new Error('patterns are required (load data/patterns.json)');
    this.config = config;
    this.patterns = patterns.filter(p => validatePattern(p, config).length === 0);
    if (this.patterns.length === 0) throw new Error('no valid patterns');
    this.fallback = this.patterns.find(p => p.id === config.fallbackPatternId) ?? this.patterns[0];
    this.seed = seed;
    this.reset();
  }

  reset() {
    this.random = mulberry32(this.seed);
    this.hp = this.config.initialHp;
    this.score = 0;
    this.combo = 0;
    this.maxCombo = 0;
    this.counts = { perfect: 0, good: 0, miss: 0, empty: 0 };
    this.coinUnits = 0;
    this.feverHits = 0;
    this.events = [];
    this.chunks = [];
    this.nextChunk = 0;
    this.lastEvent = null;
    this.recentPatternIds = [];
    this.firstOffbeatChunkBeat = null;
    this.ended = false;
  }

  pickPattern(step, forcedTier) {
    const tier = forcedTier ?? step.tier;
    const range = forcedTier == null ? step : this.config.difficulty.find(d => d.tier === forcedTier) ?? step;
    const offbeatOk = p => range.allowOffbeat || !p.events.some(e => isOffbeat(e.beat));
    const followOk = p => canFollow(this.lastEvent, p, this.config);
    const [a, b] = this.recentPatternIds.slice(-2);
    const notRepeated = p => !(a === p.id && b === p.id);
    const inTier = this.patterns.filter(p => p.tier <= tier && offbeatOk(p) && followOk(p));
    const sized = inTier.filter(p => p.events.length >= range.eventsMin && p.events.length <= range.eventsMax);
    for (const pool of [sized.filter(notRepeated), sized, inTier.filter(notRepeated), inTier]) {
      if (pool.length) return this.weightedPick(pool);
    }
    return this.fallback;
  }

  weightedPick(pool) {
    const total = pool.reduce((sum, p) => sum + p.weight, 0);
    let roll = this.random() * total;
    for (const p of pool) { roll -= p.weight; if (roll < 0) return p; }
    return pool[pool.length - 1];
  }

  ensureScheduled(beatNow, forcedTier = null) {
    const { phraseBeats, spawnLeadBeats, bpm } = this.config;
    if (this.events.length > 64) this.events = this.events.filter(e => !e.resolved || e.beat >= beatNow - phraseBeats);
    if (this.chunks.length > 8) this.chunks = this.chunks.filter(c => c.startBeat >= beatNow - phraseBeats * 2);
    while (this.nextChunk * phraseBeats < beatNow + spawnLeadBeats + phraseBeats * 2) {
      const chunkBeat = this.nextChunk * phraseBeats;
      const step = difficultyForSeconds(chunkBeat * 60 / bpm, this.config);
      const pattern = this.pickPattern(step, forcedTier);
      const hasOffbeat = pattern.events.some(e => isOffbeat(e.beat));
      if (hasOffbeat && this.firstOffbeatChunkBeat === null) this.firstOffbeatChunkBeat = chunkBeat;
      const sorted = [...pattern.events].sort((x, y) => x.beat - y.beat);
      sorted.forEach((source, index) => {
        this.events.push({
          id: `${this.nextChunk}:${pattern.id}:${source.id ?? index}`,
          beat: chunkBeat + source.beat,
          lane: source.lane,
          objectId: source.objectId,
          kind: KIND_BY_OBJECT[source.objectId] ?? 'strawberry',
          offbeat: isOffbeat(source.beat),
          resolved: false,
        });
      });
      this.chunks.push({ index: this.nextChunk, startBeat: chunkBeat, patternId: pattern.id, tier: pattern.tier, hasOffbeat });
      this.lastEvent = sorted.length ? { beat: sorted.at(-1).beat, lane: sorted.at(-1).lane } : this.lastEvent;
      if (this.lastEvent && !sorted.length) this.lastEvent = { ...this.lastEvent, beat: this.lastEvent.beat - phraseBeats };
      this.recentPatternIds.push(pattern.id);
      if (this.recentPatternIds.length > 4) this.recentPatternIds.shift();
      this.nextChunk++;
    }
  }

  /** True while the chunk before the first offbeat chunk plays (offbeat preview). */
  isOffbeatPreview(beatNow) {
    const start = this.firstOffbeatChunkBeat;
    return start !== null && start > 0 && beatNow >= start - this.config.phraseBeats && beatNow < start;
  }

  judgeTap(lane, beatNow) {
    if (this.ended) return { rating: 'ignored' };
    const msPerBeat = 60000 / this.config.bpm;
    const distance = e => Math.abs(e.beat - beatNow);
    let correct = null, nearest = null;
    for (const e of this.events) {
      if (e.resolved || distance(e) * msPerBeat > this.config.goodMs) continue;
      if (!nearest || distance(e) < distance(nearest)) nearest = e;
      if (e.lane === lane && (!correct || distance(e) < distance(correct))) correct = e;
    }
    if (correct) return this.resolve(correct, classifyTiming((beatNow - correct.beat) * msPerBeat, this.config), null, (beatNow - correct.beat) * msPerBeat);
    if (nearest) return this.resolve(nearest, 'miss', 'wrong_lane');
    this.combo = 0;
    this.counts.empty++;
    this.perfectStreak = 0;
    if (this.config.emptyTapDamagesHp) this.damage();
    return { rating: 'empty', hp: this.hp, combo: this.combo };
  }

  expire(beatNow) {
    if (this.ended) return [];
    const lateBeats = this.config.goodMs / (60000 / this.config.bpm);
    const results = [];
    for (const e of this.events.filter(e => !e.resolved && beatNow > e.beat + lateBeats).sort((x, y) => x.beat - y.beat)) {
      if (this.ended) break;
      results.push(this.resolve(e, 'miss', 'timeout'));
    }
    return results;
  }

  resolve(event, rating, reason = null, errorMs = null) {
    if (event.resolved || this.ended) return { rating: 'ignored' };
    event.resolved = true;
    event.rating = rating;
    if (rating === 'perfect' || rating === 'good') {
      this.combo++;
      this.maxCombo = Math.max(this.maxCombo, this.combo);
      // FEVER (combo >= feverCombo) doubles coin units; the score formula is unchanged.
      if (this.fever) { this.coinUnits += 2; this.feverHits++; } else this.coinUnits += 1;
      this.perfectStreak = rating === 'perfect' ? (this.perfectStreak ?? 0) + 1 : 0;
      const base = rating === 'perfect' ? this.config.perfectBase : this.config.goodBase;
      const multiplier = 1 + Math.min(this.combo / this.config.comboStep, this.config.comboMaxMultiplier - 1);
      this.score += Math.round(base * multiplier);
      this.counts[rating]++;
    } else {
      this.combo = 0;
      this.perfectStreak = 0;
      this.counts.miss++;
      this.damage();
    }
    return { rating, reason, event, errorMs, hp: this.hp, score: this.score, combo: this.combo, ended: this.ended };
  }

  damage() {
    this.hp = Math.max(0, this.hp - 1);
    if (this.hp === 0) this.ended = true;
  }

  get fever() { return this.combo >= this.config.feverCombo; }
  get feverLevel() { return this.fever ? 1 + Math.floor((this.combo - this.config.feverCombo) / this.config.feverLevelStep) : 0; }

  snapshot(beatNow) {
    const distance = Math.max(0, Math.floor(beatNow * this.config.distanceMetersPerBeat));
    return { hp: this.hp, score: this.score, combo: this.combo, maxCombo: this.maxCombo, distance, counts: { ...this.counts }, ended: this.ended,
      coinUnits: this.coinUnits, feverHits: this.feverHits, fever: this.fever, feverLevel: this.feverLevel, coins: coinsFor(this.coinUnits, distance, this.config) };
  }
}

/** floor(coin units / 3) + 3 coins per 50 m (DECISIONS D-011). Without FEVER, units = successes. */
export function coinsFor(coinUnits, distance, config = DEFAULT_CONFIG) {
  return Math.floor(coinUnits / 3) + config.distanceBonusCoins * Math.floor(distance / config.distanceBonusMeters);
}

export function coinsForRun(snapshot) { return coinsFor(snapshot.coinUnits ?? snapshot.counts.perfect + snapshot.counts.good, snapshot.distance); }
