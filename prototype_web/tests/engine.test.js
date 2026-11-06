import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { classifyTiming, validatePattern, canFollow, RunEngine, DEFAULT_CONFIG, configFromGameConfig, patternsFromJson, difficultyForSeconds, isOffbeat, coinsForRun } from '../src/engine.js';
import { ClockBridge } from '../src/clock.js';

const readJson = rel => JSON.parse(readFileSync(new URL(`../../data/${rel}`, import.meta.url), 'utf8'));
const PATTERNS = patternsFromJson(readJson('patterns.json'));
const GAME_CONFIG = readJson('game_config.json');
const engineFor = (seed, patterns = PATTERNS) => new RunEngine({ seed, patterns });

test('timing boundaries are inclusive', () => {
  assert.equal(classifyTiming(80), 'perfect');
  assert.equal(classifyTiming(-80), 'perfect');
  assert.equal(classifyTiming(81), 'good');
  assert.equal(classifyTiming(160), 'good');
  assert.equal(classifyTiming(161), 'miss');
});

test('engine defaults match data/game_config.json', () => {
  const fromJson = configFromGameConfig(GAME_CONFIG);
  for (const key of Object.keys(DEFAULT_CONFIG)) {
    if (key === 'fallbackPatternId') continue;
    assert.deepEqual(fromJson[key], DEFAULT_CONFIG[key], key);
  }
});

test('shipped patterns are valid and cover every difficulty tier', () => {
  assert.ok(PATTERNS.length >= 12 && PATTERNS.length <= 20, `MVP wants 12-20 patterns, got ${PATTERNS.length}`);
  for (const pattern of PATTERNS) assert.deepEqual(validatePattern(pattern), [], pattern.id);
  for (const step of DEFAULT_CONFIG.difficulty) {
    const usable = PATTERNS.filter(p => p.tier <= step.tier && p.events.length >= step.eventsMin && p.events.length <= step.eventsMax);
    assert.ok(usable.length >= 2, `tier ${step.tier} needs at least two sized patterns`);
  }
  const catalog = new Set(readJson('content_catalog.json').objects.map(o => o.id));
  for (const p of PATTERNS) for (const e of p.events) assert.ok(catalog.has(e.objectId), `${p.id} ${e.objectId}`);
});

test('unity Resources copy of patterns is in sync', () => {
  const unity = readFileSync(new URL('../../unity_project/Assets/RhythmRunner/Resources/patterns.json', import.meta.url));
  const data = readFileSync(new URL('../../data/patterns.json', import.meta.url));
  assert.ok(unity.equals(data));
});

test('one event resolves only once', () => {
  const engine = engineFor(1);
  engine.ensureScheduled(0, 0);
  const event = engine.events[0];
  const first = engine.judgeTap(event.lane, event.beat);
  const second = engine.resolve(event, 'perfect');
  assert.equal(first.rating, 'perfect');
  assert.equal(second.rating, 'ignored');
  assert.equal(engine.counts.perfect, 1);
});

test('four misses end the run and later score is blocked', () => {
  const engine = engineFor(2);
  engine.ensureScheduled(0, 0);
  for (const event of engine.events.slice(0, 4)) engine.resolve(event, 'miss', 'test');
  assert.equal(engine.hp, 0);
  assert.equal(engine.ended, true);
  const score = engine.score;
  assert.equal(engine.resolve(engine.events[4], 'perfect').rating, 'ignored');
  assert.equal(engine.judgeTap(engine.events[4].lane, engine.events[4].beat).rating, 'ignored');
  assert.equal(engine.score, score);
});

test('expire stops at HP 0 instead of over-counting misses', () => {
  const engine = engineFor(6);
  engine.ensureScheduled(0, 2);
  const results = engine.expire(40);
  assert.equal(results.length, 4);
  assert.equal(engine.counts.miss, 4);
});

test('same seed produces the same schedule', () => {
  const a = engineFor(99);
  const b = engineFor(99);
  a.ensureScheduled(100);
  b.ensureScheduled(100);
  assert.deepEqual(a.events, b.events);
  assert.deepEqual(a.chunks, b.chunks);
});

test('empty tap breaks combo without HP damage', () => {
  const engine = engineFor(3);
  engine.combo = 10;
  const result = engine.judgeTap(1, 999);
  assert.equal(result.rating, 'empty');
  assert.equal(engine.combo, 0);
  assert.equal(engine.hp, 4);
});

test('wrong lane consumes one event and damages only once', () => {
  const engine = engineFor(4);
  engine.ensureScheduled(0, 0);
  const event = engine.events[0];
  const wrongLane = (event.lane + 1) % 3;
  const first = engine.judgeTap(wrongLane, event.beat);
  const second = engine.resolve(event, 'miss');
  assert.equal(first.rating, 'miss');
  assert.equal(first.reason, 'wrong_lane');
  assert.equal(second.rating, 'ignored');
  assert.equal(engine.hp, 3);
});

test('offbeat events retain half-beat precision', () => {
  const offbeat = PATTERNS.filter(pattern => pattern.id === 'offbeat_intro_01');
  const engine = engineFor(5, offbeat);
  engine.ensureScheduled(0, 3);
  assert.ok(engine.events.some(event => event.beat % 1 === 0.5 && event.offbeat));
});

test('difficulty curve follows game_config and offbeat starts at 64s', () => {
  assert.equal(difficultyForSeconds(0).tier, 0);
  assert.equal(difficultyForSeconds(63.9).tier, 2);
  assert.equal(difficultyForSeconds(64).tier, 3);
  assert.equal(difficultyForSeconds(500).tier, 5);
  const engine = engineFor(7);
  engine.ensureScheduled(400);
  const spb = 60 / DEFAULT_CONFIG.bpm;
  for (const e of engine.events) {
    const seconds = Math.floor(e.beat / 8) * 8 * spb;
    if (e.offbeat) assert.ok(seconds >= 64, `offbeat at ${seconds}s`);
  }
  assert.equal(engine.firstOffbeatChunkBeat * spb >= 64, true);
});

test('chunk boundaries keep minimum interval and lane reach across 200 seeds', () => {
  for (let seed = 1; seed <= 200; seed++) {
    const engine = engineFor(seed);
    for (let beat = 0; beat <= 600; beat += 8) engine.ensureScheduled(beat);
    const events = [...engine.events].sort((a, b) => a.beat - b.beat);
    for (let i = 1; i < events.length; i++) {
      const gap = events[i].beat - events[i - 1].beat;
      assert.ok(gap >= 0.5, `seed ${seed} gap ${gap} at ${events[i].beat}`);
      if (gap < 1) assert.ok(Math.abs(events[i].lane - events[i - 1].lane) <= 1, `seed ${seed} jump at ${events[i].beat}`);
    }
    for (let i = 2; i < engine.chunks.length; i++) {
      const ids = engine.chunks.slice(i - 2, i + 1).map(c => c.patternId);
      assert.ok(!(ids[0] === ids[1] && ids[1] === ids[2]) || engine.patterns.length === 1, `seed ${seed} repeats ${ids[0]}`);
    }
  }
});

test('canFollow rejects a half-beat edge-to-edge jump across chunks', () => {
  const startsRight = { events: [{ beat: 0, lane: 2 }] };
  assert.equal(canFollow({ beat: 7.5, lane: 0 }, startsRight), false);
  assert.equal(canFollow({ beat: 7.5, lane: 1 }, startsRight), true);
  assert.equal(canFollow({ beat: 7, lane: 0 }, startsRight), true);
});

test('offbeat preview covers the chunk before the first offbeat chunk', () => {
  const engine = engineFor(8);
  engine.ensureScheduled(200);
  const start = engine.firstOffbeatChunkBeat;
  assert.ok(start >= 128);
  assert.equal(engine.isOffbeatPreview(start - 8), true);
  assert.equal(engine.isOffbeatPreview(start - 8.01), false);
  assert.equal(engine.isOffbeatPreview(start), false);
});

test('half-beat helper', () => {
  assert.equal(isOffbeat(2.5), true);
  assert.equal(isOffbeat(3), false);
});

test('coin formula', () => {
  assert.equal(coinsForRun({ counts: { perfect: 4, good: 2 }, distance: 250 }), 2 + 15);
  assert.equal(coinsForRun({ coinUnits: 9, counts: {}, distance: 49 }), 3);
});

test('clock bridge maps input timestamps onto the audio clock', () => {
  const bridge = new ClockBridge();
  bridge.sync({ contextTime: 10, performanceTime: 5000 });
  assert.equal(bridge.toAudioSeconds(5000), 10);
  assert.equal(bridge.toAudioSeconds(5250), 10.25);
  // A later sample with jitter is smoothed, not jumped to.
  bridge.sync({ contextTime: 20.004, performanceTime: 15000 });
  assert.ok(Math.abs(bridge.toAudioSeconds(15000) - 20) < 0.004);
  // A large discontinuity (device change, resume) resets the offset immediately.
  bridge.sync({ contextTime: 50, performanceTime: 16000 });
  assert.equal(bridge.toAudioSeconds(16000), 50);
});

test('same input timestamps give the same judgements regardless of frame rate', () => {
  const runAt = hz => {
    const engine = engineFor(11);
    const spb = 60 / DEFAULT_CONFIG.bpm;
    engine.ensureScheduled(0);
    // Taps 30ms late on every event in the first 40 beats, processed when the next frame arrives.
    const taps = engine.events.filter(e => e.beat < 40).map(e => ({ t: e.beat * spb + 0.03, lane: e.lane }));
    const ratings = [];
    let next = 0;
    for (let frame = 0; frame * (1 / hz) < 22; frame++) {
      const now = frame / hz;
      while (next < taps.length && taps[next].t <= now) { ratings.push(engine.judgeTap(taps[next].lane, taps[next].t / spb).rating); next++; }
      engine.ensureScheduled(now / spb);
      engine.expire(now / spb);
    }
    return { ratings, score: engine.score, hp: engine.hp };
  };
  const at30 = runAt(30);
  assert.deepEqual(runAt(60), at30);
  assert.deepEqual(runAt(120), at30);
  assert.ok(at30.ratings.every(r => r === 'perfect'));
});

test('fever starts on the 50th hit, doubles coin units and ends on a miss', () => {
  const steady = [{ id: 'steady', tier: 0, weight: 1, durationBeats: 8, events: [0, 1, 2, 3, 4, 5, 6, 7].map(b => ({ id: `e${b}`, beat: b, lane: 1, objectId: 'object_strawberry', gesture: 'tap' })) }];
  const engine = new RunEngine({ seed: 1, patterns: steady });
  for (let i = 0; i < 49; i++) { engine.ensureScheduled(i); engine.judgeTap(1, i); }
  assert.equal(engine.fever, false);
  engine.ensureScheduled(49); engine.judgeTap(1, 49);
  assert.equal(engine.fever, true);
  assert.equal(engine.coinUnits, 51);
  for (let i = 50; i < 100; i++) { engine.ensureScheduled(i); engine.judgeTap(1, i); }
  assert.equal(engine.feverLevel, 2);
  engine.ensureScheduled(100);
  assert.equal(engine.judgeTap(0, 100).rating, 'miss');
  assert.equal(engine.fever, false);
  assert.equal(engine.snapshot(100).coins, Math.floor((49 + 51 * 2) / 3) + 3 * 2);
});
