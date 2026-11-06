import { RunEngine, configFromGameConfig, patternsFromJson, isOffbeat, coinsForRun } from './engine.js';
import { ClockBridge } from './clock.js';

const $ = selector => document.querySelector(selector);
const $$ = selector => [...document.querySelectorAll(selector)];
const screens = $$('.screen');
const canvas = $('#canvas');
const ctx = canvas.getContext('2d');
const storageKey = 'rhythmSlashRunnerPrototypeV1';
const defaults = { coins: 0, bestDistance: 0, bestScore: 0, character: 'hamster', weapon: 'energy', stage: 'kitchen', music: 60, sfx: 80, cue: 80, vibration: true, tierOverride: -1, calibrationMs: 0, diagnostics: false, appliedRunIds: [] };
const profile = Object.assign({}, defaults, readProfile());
const OBJECT_ICON = { strawberry: '🍓', watermelon: '🍉', barrel: '🛢️' };
const LANE_X = [1 / 6, 1 / 2, 5 / 6];
const SLASH_Y = 0.72, CHARACTER_Y = 0.85, HORIZON_Y = 230;
const RESUME_BEATS = 3;
const STALL_SECONDS = 0.5;
const SCHEDULE_AHEAD = 0.2;

let config = null, patterns = null;
let engine = null, runId = null, state = 'home';
let audio = null;
const bridge = new ClockBridge();
let musicStart = 0, pausedBeat = 0, resumeEnd = 0, scheduledUpTo = -Infinity, scheduledSources = [];
let lastFrameTime = 0, lastTapTime = -Infinity, animationId = 0;
let slices = [], laneFlash = [0, 0, 0], lastResult = null, frameTimes = [];

function readProfile() { try { return JSON.parse(localStorage.getItem(storageKey) || '{}'); } catch { return {}; } }
function save() { try { localStorage.setItem(storageKey, JSON.stringify(profile)); } catch { /* storage blocked: keep playing */ } $('#coinHome').textContent = profile.coins; }
function show(id) { screens.forEach(s => s.classList.toggle('active', s.id === id)); }
const spb = () => 60 / config.bpm;
const forcedTier = () => (profile.tierOverride >= 0 ? profile.tierOverride : null);

// ---- clock -------------------------------------------------------------
function syncBridge() {
  if (!audio) return;
  if (audio.getOutputTimestamp) {
    const stamp = audio.getOutputTimestamp();
    if (stamp.contextTime > 0) { bridge.sync(stamp); return; }
  }
  bridge.sync({ contextTime: audio.currentTime - (audio.outputLatency || audio.baseLatency || 0), performanceTime: performance.now() });
}
function audioNow() { return audio ? bridge.toAudioSeconds(performance.now()) : performance.now() / 1000; }
function beatAt(audioSeconds) { return (audioSeconds - musicStart) / spb(); }
function displayBeat(now) { return state === 'paused' || state === 'resuming' ? pausedBeat : beatAt(now); }

async function initAudio() {
  if (!audio) {
    const Context = window.AudioContext || window.webkitAudioContext;
    if (!Context) return;
    audio = new Context({ latencyHint: 'interactive' });
  }
  if (audio.state === 'suspended') await audio.resume().catch(() => {});
  for (let i = 0; i < 30 && !bridge.ready; i++) { syncBridge(); if (!bridge.ready) await new Promise(r => setTimeout(r, 16)); }
}

// Sounds are placed on the audio clock ahead of time instead of being fired from the frame loop.
function tone(at, freq, duration = 0.055, volume = 0.04, type = 'sine', channel = 'sfx') {
  if (!audio) return;
  const level = (channel === 'cue' ? profile.cue : channel === 'music' ? profile.music : profile.sfx) / 100;
  if (level <= 0) return;
  const oscillator = audio.createOscillator(), gain = audio.createGain();
  const start = Math.max(audio.currentTime, at);
  oscillator.type = type; oscillator.frequency.value = freq;
  gain.gain.setValueAtTime(volume * level, start);
  gain.gain.exponentialRampToValueAtTime(0.0001, start + duration);
  oscillator.connect(gain).connect(audio.destination);
  oscillator.start(start); oscillator.stop(start + duration);
  if (channel !== 'sfx') {
    scheduledSources.push(oscillator);
    oscillator.onended = () => { scheduledSources = scheduledSources.filter(s => s !== oscillator); };
  }
}
function stopScheduled() { for (const s of scheduledSources) { try { s.stop(); } catch { /* already stopped */ } } scheduledSources = []; }

function scheduleAudio(now) {
  const horizon = beatAt(now + SCHEDULE_AHEAD);
  const first = Math.max(scheduledUpTo, beatAt(now) - 0.05);
  for (let half = Math.ceil(first * 2 + 1e-6); half / 2 <= horizon; half++) {
    const beat = half / 2;
    const at = musicStart + beat * spb();
    if (Number.isInteger(beat)) {
      if (beat < 0) tone(at, beat === -4 ? 880 : 660, 0.06, 0.05, 'square', 'music');
      else tone(at, beat % 4 === 0 ? 760 : 520, 0.035, 0.025, 'square', 'music');
    } else if (beat > 0 && (engine.isOffbeatPreview(beat) || chunkHasOffbeat(beat))) {
      tone(at, 1240, 0.025, 0.012, 'triangle', 'music');
    }
    for (const e of engine.events) if (!e.resolved && Math.abs(e.beat - 1 - beat) < 1e-6) tone(at, 300 + e.lane * 140, 0.05, 0.02, 'sine', 'cue');
  }
  scheduledUpTo = Math.max(scheduledUpTo, horizon);
}
function chunkHasOffbeat(beat) { const c = engine.chunks.find(c => beat >= c.startBeat && beat < c.startBeat + config.phraseBeats); return !!c?.hasOffbeat; }

// ---- run flow ------------------------------------------------------------
async function startRun() {
  if (!config) return;
  await initAudio();
  stopScheduled();
  engine = new RunEngine({ config, patterns, seed: (Math.random() * 0x7fffffff) | 0 });
  runId = `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 8)}`;
  engine.ensureScheduled(0, forcedTier());
  syncBridge();
  const now = audioNow();
  musicStart = now + 4 * spb() + 0.1;
  scheduledUpTo = -4.0001;
  state = 'countin'; slices = []; lastResult = null; lastFrameTime = now;
  show('game'); setupBeatDots(); updateHud(0);
  cancelAnimationFrame(animationId); animationId = requestAnimationFrame(loop);
}

function pauseRun(atBeat = null) {
  if (state !== 'running' && state !== 'countin' && state !== 'resuming') return;
  if (state !== 'resuming') pausedBeat = atBeat ?? beatAt(audioNow());
  state = 'paused';
  stopScheduled();
  show('pause');
}

function resumeRun() {
  if (state !== 'paused') return;
  syncBridge();
  const now = audioNow();
  resumeEnd = now + RESUME_BEATS * spb();
  for (let i = 0; i < RESUME_BEATS; i++) tone(now + i * spb(), i === 0 ? 880 : 660, 0.06, 0.05, 'square', 'music');
  state = 'resuming';
  show('game');
  lastFrameTime = now;
  animationId = requestAnimationFrame(loop);
}

function loop() {
  if (state === 'home' || state === 'paused' || state === 'ended') return;
  syncBridge();
  const now = audioNow();
  const frameGap = now - lastFrameTime;
  frameTimes.push(frameGap); if (frameTimes.length > 60) frameTimes.shift();
  // A long stall (tab throttled, audio device lost) becomes a pause, never a burst of timeout MISSes.
  if (frameGap > STALL_SECONDS && state === 'running') { pauseRun(beatAt(lastFrameTime)); return; }
  lastFrameTime = now;

  if (state === 'resuming' && now >= resumeEnd) {
    musicStart = resumeEnd - pausedBeat * spb();
    scheduledUpTo = pausedBeat;
    state = 'running';
  }
  if (state === 'countin' && beatAt(now) >= 0) state = 'running';

  const beat = displayBeat(now);
  if (state === 'running' || state === 'countin') {
    engine.ensureScheduled(Math.max(0, beat), forcedTier());
    scheduleAudio(now);
    if (state === 'running') for (const result of engine.expire(beat)) feedback(result, now);
  }
  draw(beat, now); updateHud(beat); updateBeatDots(beat); updateCountIn(beat, now);
  if (engine.ended) { finish(beat); return; }
  animationId = requestAnimationFrame(loop);
}

function finish(beat) {
  state = 'ended';
  stopScheduled();
  const snapshot = engine.snapshot(beat);
  const coins = coinsForRun(snapshot);
  // Rewards are applied once per runId, no matter how often the result is shown.
  if (!profile.appliedRunIds.includes(runId)) {
    profile.coins += coins;
    profile.bestDistance = Math.max(profile.bestDistance, snapshot.distance);
    profile.bestScore = Math.max(profile.bestScore, snapshot.score);
    profile.appliedRunIds = [...profile.appliedRunIds, runId].slice(-20);
    save();
  }
  $('#resultDistance').textContent = `${snapshot.distance} m`;
  $('#resultScore').textContent = snapshot.score.toLocaleString();
  $('#resultCombo').textContent = snapshot.maxCombo;
  $('#resultJudges').textContent = `${snapshot.counts.perfect} / ${snapshot.counts.good} / ${snapshot.counts.miss}`;
  $('#resultCoins').textContent = `+${coins}`;
  $('#resultBest').textContent = `${profile.bestDistance} m`;
  setTimeout(() => { if (state === 'ended') show('result'); }, 600);
}

function quitToHome() { state = 'home'; stopScheduled(); cancelAnimationFrame(animationId); show('home'); }

// ---- input ---------------------------------------------------------------
function tap(lane, eventTimeStamp) {
  if (!engine || engine.ended || (state !== 'running' && state !== 'countin')) return;
  if (eventTimeStamp - lastTapTime < 100) return; // inputDebounceMs
  lastTapTime = eventTimeStamp;
  // Event time → audio clock via the bridge; positive calibration treats input as earlier.
  const inputAudio = bridge.toAudioSeconds(eventTimeStamp) - profile.calibrationMs / 1000;
  const beat = beatAt(inputAudio);
  if (beat < -1) return;
  const result = engine.judgeTap(lane, beat);
  laneFlash[lane] = performance.now();
  if (result.rating === 'miss' || result.rating === 'empty') missPoseUntil = performance.now() + (result.rating === 'miss' ? 350 : 0);
  if (result.rating !== 'miss') slashPoseUntil = performance.now() + 200;
  feedback(result, audioNow());
  if (result.rating === 'perfect' || result.rating === 'good') {
    slices.push({ lane, kind: result.event.kind, beat: result.event.beat, born: performance.now(), rating: result.rating });
    tone(audio ? audio.currentTime : 0, result.rating === 'perfect' ? 950 : 720, 0.09, 0.06);
    if (navigator.vibrate && profile.vibration) navigator.vibrate(18);
  } else if (result.rating === 'miss') {
    tone(audio ? audio.currentTime : 0, 180, 0.09, 0.06, 'sawtooth');
  }
}

function feedback(result, now) {
  if (result.rating === 'ignored') return;
  lastResult = { ...result, at: now };
  const el = $('#judge');
  const text = { perfect: 'PERFECT!', good: 'GOOD', miss: 'MISS', empty: '' }[result.rating] ?? '';
  const timing = result.rating === 'good' && result.errorMs != null ? `<small>${result.errorMs < 0 ? 'FAST' : 'SLOW'}</small>` : '';
  el.innerHTML = text + timing;
  el.style.color = result.rating === 'perfect' ? '#ffe77b' : result.rating === 'good' ? '#54edff' : '#ff5b61';
  clearTimeout(el.timer); el.timer = setTimeout(() => { el.textContent = ''; }, 380);
}

// ---- drawing ---------------------------------------------------------------
// Sprites are shared with the Unity build (/art/ → unity_project Resources/Art); emoji are the fallback.
const SPRITES = {};
['kitchen_backdrop', 'wood_road_tile', 'hamster_run_a', 'hamster_run_b', 'hamster_slash', 'hamster_miss', 'vfx_slash', 'vfx_juice', 'vfx_chips',
  'strawberry_intact', 'watermelon_intact', 'barrel_intact', 'strawberry_split_l', 'strawberry_split_r', 'watermelon_split_l', 'watermelon_split_r', 'barrel_split_l', 'barrel_split_r',
  'weapon_energy_blade', 'weapon_wooden_sword', 'weapon_banana', 'prop_jar', 'prop_tomatoes', 'prop_cheese', 'prop_pepper_mill', 'prop_crate', 'prop_herbs']
  .forEach(name => { const img = new Image(); img.onload = () => { SPRITES[name] = img; }; img.src = `/art/${name}.png`; });
const PROPS = ['prop_jar', 'prop_tomatoes', 'prop_cheese', 'prop_pepper_mill', 'prop_crate', 'prop_herbs'];
const WEAPON_SPRITE = { energy: 'weapon_energy_blade', wood: 'weapon_wooden_sword', banana: 'weapon_banana' };
// Same perspective as Unity's Perspective class (normalized y from the top).
const P = { horizon: 0.255, line: SLASH_Y, halfH: 0.055, halfL: 0.5, exp: 1.7 };
const depthOf = ahead => { const p = 1 - ahead / config.spawnLeadBeats; return p <= 0 ? 0 : p ** P.exp; };
const yOf = d => P.horizon + (P.line - P.horizon) * d;
const halfOf = d => P.halfH + (P.halfL - P.halfH) * d;
const xOf = (lateral, d) => 0.5 + lateral * halfOf(d);
const laneLateral = lane => (lane - 1) * 2 / 3;
let slashPoseUntil = 0, missPoseUntil = 0;

function project(lane, progress, w) {
  const d = Math.max(0, progress) ** P.exp;
  return { x: xOf(laneLateral(lane), d) * w, y: yOf(d) * canvas.height, size: 0.21 * w * halfOf(d) / P.halfL };
}

function drawImageCover(img, w, h) {
  const scale = Math.max(w / img.width, h / img.height);
  ctx.drawImage(img, (w - img.width * scale) / 2, (h - img.height * scale) / 2, img.width * scale, img.height * scale);
}

function drawRoad(beat, w, h) {
  const tile = SPRITES.wood_road_tile;
  const bottom = (1.08 - P.horizon) / (P.line - P.horizon);
  const strips = 70;
  for (let i = 0; i < strips; i++) {
    const t0 = i / strips, t1 = (i + 1) / strips;
    const d0 = bottom * t0 * t0, d1 = bottom * t1 * t1;
    const y0 = yOf(d0) * h, y1 = yOf(d1) * h + 1;
    const half = halfOf((d0 + d1) / 2) * w;
    if (tile) {
      // Each strip samples the slice of board it covers (2 beats per tile), so grain stays continuous.
      const ahead0 = config.spawnLeadBeats * (1 - Math.pow(d0, 1 / P.exp));
      const ahead1 = config.spawnLeadBeats * (1 - Math.pow(d1, 1 / P.exp));
      const v0 = (((beat + ahead0) / 2) % 1 + 1) % 1;
      const span = Math.max(1, Math.min(tile.height * 0.5, (ahead0 - ahead1) / 2 * tile.height));
      const sy = Math.min(tile.height - span, (1 - v0) * tile.height);
      ctx.drawImage(tile, 0, sy, tile.width, span, w / 2 - half, y0, half * 2, y1 - y0);
    } else { ctx.fillStyle = '#c8955a'; ctx.fillRect(w / 2 - half, y0, half * 2, y1 - y0); }
  }
  // inlaid lane strips + darker rims
  for (const [lateral, width, color] of [[-1 / 3, 0.014, 'rgba(255,238,200,.55)'], [1 / 3, 0.014, 'rgba(255,238,200,.55)'], [-1, 0.03, 'rgba(84,46,20,.6)'], [1, 0.03, 'rgba(84,46,20,.6)']]) {
    ctx.fillStyle = color; ctx.beginPath();
    ctx.moveTo((xOf(lateral, 0) - width * 0.055) * w, yOf(0) * h); ctx.lineTo((xOf(lateral, 0) + width * 0.055) * w, yOf(0) * h);
    ctx.lineTo((xOf(lateral, bottom) + width * halfOf(bottom)) * w, yOf(bottom) * h); ctx.lineTo((xOf(lateral, bottom) - width * halfOf(bottom)) * w, yOf(bottom) * h);
    ctx.closePath(); ctx.fill();
  }
}

function drawProps(beat, w, h) {
  // deterministic side props every 0.75 beats, alternating sides, placed from the beat like everything else
  const first = Math.floor((beat - 3) / 0.75), last = Math.ceil((beat + config.spawnLeadBeats) / 0.75);
  for (let k = last; k >= first; k--) {
    const img = SPRITES[PROPS[((k * 7) % PROPS.length + PROPS.length) % PROPS.length]];
    if (!img) continue;
    const d = depthOf(k * 0.75 - beat);
    const y = yOf(d) * h;
    if (y > h * 1.35) continue;
    const side = k % 2 === 0 ? -1 : 1;
    const size = (0.3 + (((k * 13) % 10 + 10) % 10) / 80) * w * halfOf(d) / P.halfL;
    const x = xOf(side * (1.55 + (((k * 29) % 10 + 10) % 10) / 20), d) * w;
    const ratio = img.width / img.height;
    ctx.globalAlpha = Math.min(1, d * 6);
    ctx.drawImage(img, x - size * ratio / 2, y - size, size * ratio, size);
    ctx.globalAlpha = 1;
  }
}

function draw(beat, now) {
  const w = canvas.width, h = canvas.height, lineY = SLASH_Y * h;
  if (SPRITES.kitchen_backdrop) drawImageCover(SPRITES.kitchen_backdrop, w, h);
  else { const sky = ctx.createLinearGradient(0, 0, 0, h); sky.addColorStop(0, '#17396a'); sky.addColorStop(0.5, '#ce8240'); sky.addColorStop(1, '#5c321b'); ctx.fillStyle = sky; ctx.fillRect(0, 0, w, h); }
  drawRoad(beat, w, h);
  drawProps(beat, w, h);
  const flashAge = performance.now();
  laneFlash.forEach((t, lane) => { const a = 1 - (flashAge - t) / 160; if (a > 0) { ctx.fillStyle = `rgba(101,243,255,${0.18 * a})`; ctx.fillRect(lane * w / 3, lineY - 60, w / 3, h - lineY + 60); } });
  const preview = engine.isOffbeatPreview(beat);
  ctx.strokeStyle = preview ? '#e98cff' : '#65f3ff'; ctx.lineWidth = 5; ctx.shadowColor = preview ? '#c04dff' : '#20d9f3'; ctx.shadowBlur = 16;
  ctx.beginPath(); ctx.moveTo(0, lineY); ctx.lineTo(w, lineY); ctx.stroke(); ctx.shadowBlur = 0;

  const lead = config.spawnLeadBeats;
  const goodBeats = config.goodMs / (60000 / config.bpm);
  const upcoming = engine.events.filter(e => !e.resolved && e.beat - beat <= lead && e.beat - beat >= -0.5).sort((a, b) => b.beat - a.beat);
  ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
  for (const e of upcoming) {
    const remaining = e.beat - beat;
    const { x, y: base, size } = project(e.lane, 1 - remaining / lead, w);
    const y = base - size * 0.45;
    const fade = Math.min(1, depthOf(remaining) * 8) * (remaining < -goodBeats ? Math.max(0, 1 - (-remaining - goodBeats) / 0.25) : 1);
    ctx.globalAlpha = fade;
    const inWindow = Math.max(0, 1 - Math.abs(remaining) / goodBeats);
    if (inWindow > 0) { const g = ctx.createRadialGradient(x, y, 0, x, y, size * 0.85); g.addColorStop(0, `rgba(${e.offbeat ? '240,150,255' : '100,240,255'},${0.6 * inWindow})`); g.addColorStop(1, 'rgba(100,240,255,0)'); ctx.fillStyle = g; ctx.fillRect(x - size, y - size, size * 2, size * 2); }
    if (e.offbeat) { // offbeat: diamond + double ring so it is not color-only
      ctx.strokeStyle = '#e98cff'; ctx.lineWidth = 4;
      ctx.beginPath(); ctx.moveTo(x, y - size * 0.75); ctx.lineTo(x + size * 0.75, y); ctx.lineTo(x, y + size * 0.75); ctx.lineTo(x - size * 0.75, y); ctx.closePath(); ctx.stroke();
      ctx.beginPath(); ctx.arc(x, y, size * 0.62, 0, Math.PI * 2); ctx.stroke();
    }
    const img = SPRITES[`${e.kind}_intact`];
    if (img) { const r = img.width / img.height; ctx.drawImage(img, x - size * r / 2, y - size / 2, size * r, size); }
    else { ctx.font = `${size}px system-ui`; ctx.fillStyle = '#fff'; ctx.fillText(OBJECT_ICON[e.kind] ?? '🍓', x, y); }
    if (remaining <= 1 && remaining >= 0) { // closing ring during the final beat
      ctx.strokeStyle = e.offbeat ? '#e98cffcc' : '#ffffffcc'; ctx.lineWidth = 4;
      ctx.beginPath(); ctx.arc(x, y, size * (0.55 + remaining * 0.75), 0, Math.PI * 2); ctx.stroke();
    }
    ctx.globalAlpha = 1;
  }
  const t = performance.now();
  slices = slices.filter(s => t - s.born < 450);
  for (const s of slices) {
    const age = (t - s.born) / 450, { x, y: base, size } = project(s.lane, 1, w), y = base - size * 0.45;
    const l = SPRITES[`${s.kind}_split_l`], r = SPRITES[`${s.kind}_split_r`];
    ctx.globalAlpha = 1 - age * age;
    if (l && r) {
      const fly = age * w * 0.25, drop = age * age * w * 0.5 - age * w * 0.2;
      ctx.save(); ctx.translate(x - size * 0.2 - fly, y + drop); ctx.rotate(-age * 2.5); ctx.drawImage(l, -size * 0.28, -size * 0.5, size * 0.55, size); ctx.restore();
      ctx.save(); ctx.translate(x + size * 0.2 + fly, y + drop); ctx.rotate(age * 2.5); ctx.drawImage(r, -size * 0.28, -size * 0.5, size * 0.55, size); ctx.restore();
    }
    const burst = SPRITES[s.kind === 'barrel' ? 'vfx_chips' : 'vfx_juice'];
    if (burst && age < 0.8) { const b = size * (0.9 + age * 1.2); ctx.globalAlpha = 1 - age / 0.8; ctx.drawImage(burst, x - b, y - b, b * 2, b * 2); }
    const slash = SPRITES.vfx_slash;
    if (slash && age < 0.5) { ctx.globalAlpha = 1 - age / 0.5; ctx.save(); ctx.translate(x, y); ctx.rotate(-0.3); ctx.drawImage(slash, -size * 1.1, -size * 0.72, size * 2.2, size * 1.45); ctx.restore(); }
    ctx.globalAlpha = 1;
  }
  drawHero(beat, w, h);
  if (preview) { ctx.font = 'bold 30px system-ui'; ctx.fillStyle = '#f2b8ff'; ctx.shadowColor = '#000'; ctx.shadowBlur = 6; ctx.fillText('♪ 裏拍がくるよ！ 拍と拍のあいだ', w / 2, 120); ctx.shadowBlur = 0; }
  if (profile.diagnostics) drawDiagnostics(beat, now);
}

function drawHero(beat, w, h) {
  const now = performance.now();
  const pose = now < slashPoseUntil ? 'hamster_slash' : now < missPoseUntil ? 'hamster_miss' : (Math.floor(beat * 2) & 1) ? 'hamster_run_b' : 'hamster_run_a';
  const img = SPRITES[pose] ?? SPRITES.hamster_run_a;
  const bob = Math.abs(Math.sin(beat * Math.PI)) * 0.012 * w;
  const footY = (CHARACTER_Y + 0.115) * h;
  ctx.fillStyle = 'rgba(50,25,8,.35)'; ctx.beginPath(); ctx.ellipse(w / 2, footY - 4, w * 0.13, w * 0.03, 0, 0, Math.PI * 2); ctx.fill();
  if (!img) { ctx.font = '108px system-ui'; ctx.fillStyle = '#fff'; ctx.fillText('🐹', w / 2, CHARACTER_Y * h - bob); return; }
  const height = 0.36 * w, width = height * img.width / img.height;
  const left = w / 2 - width / 2, top = footY - height - bob;
  ctx.drawImage(img, left, top, width, height);
  const weapon = SPRITES[WEAPON_SPRITE[profile.weapon]];
  if (weapon) { // shared grip pivot, held beside the body
    const slashing = pose === 'hamster_slash';
    const hand = slashing ? [0.95, 0.42] : pose === 'hamster_run_b' ? [0.94, 0.54] : [0.94, 0.56];
    const wh = height * 0.62, ww = wh * weapon.width / weapon.height;
    const angle = slashing ? (-10 + 105 * Math.min(1, 1 - (slashPoseUntil - now) / 200)) : pose === 'hamster_run_b' ? -22 : -28;
    ctx.save(); ctx.translate(left + hand[0] * width, top + hand[1] * height); ctx.rotate(-angle * Math.PI / 180); ctx.drawImage(weapon, -ww / 2, -wh * 0.9, ww, wh); ctx.restore();
  }
}

function drawDiagnostics(beat, now) {
  const avg = frameTimes.reduce((a, b) => a + b, 0) / Math.max(1, frameTimes.length);
  const lines = [
    `beat ${beat.toFixed(2)}  state ${state}`,
    `fps ${avg ? (1 / avg).toFixed(0) : '-'}  seed ${engine.seed}`,
    `chunk ${engine.chunks.find(c => beat >= c.startBeat && beat < c.startBeat + 8)?.patternId ?? '-'}`,
    `last ${lastResult ? `${lastResult.rating} ${lastResult.errorMs != null ? lastResult.errorMs.toFixed(0) + 'ms' : lastResult.reason ?? ''}` : '-'}`,
    `cal ${profile.calibrationMs}ms  lat ${audio ? ((audio.outputLatency || audio.baseLatency || 0) * 1000).toFixed(0) : '-'}ms`,
  ];
  ctx.font = '16px monospace'; ctx.textAlign = 'left'; ctx.fillStyle = '#000a'; ctx.fillRect(8, 120, 300, lines.length * 20 + 10);
  ctx.fillStyle = '#9ff'; lines.forEach((l, i) => ctx.fillText(l, 14, 138 + i * 20)); ctx.textAlign = 'center';
}

function setupBeatDots() { $('#beats').innerHTML = Array.from({ length: 8 }, () => '<i class="beat-dot"></i>').join(''); }
function updateBeatDots(beat) {
  const local = ((beat % 8) + 8) % 8, whole = Math.floor(local);
  const offbeatChunk = beat >= 0 && (chunkHasOffbeat(beat) || engine.isOffbeatPreview(beat));
  const half = offbeatChunk && local - whole >= 0.5 && local - whole < 0.7;
  $$('.beat-dot').forEach((d, i) => { d.classList.toggle('on', beat >= 0 && i === whole); d.classList.toggle('offbeat', offbeatChunk); d.classList.toggle('half', half && i === whole); });
}
function updateHud(beat) {
  const s = engine.snapshot(beat);
  $('#distance').textContent = `${s.distance} m`; $('#score').textContent = s.score.toLocaleString(); $('#combo').textContent = `×${s.combo}`;
  $('#hearts').textContent = '♥'.repeat(s.hp) + '♡'.repeat(config.initialHp - s.hp);
}
function updateCountIn(beat, now) {
  const el = $('#countIn');
  let text = '';
  if (state === 'countin') text = beat < -1 ? String(Math.ceil(-beat) - 1) : beat < 0 ? 'GO!' : '';
  else if (state === 'resuming') { const left = (resumeEnd - now) / spb(); text = left > 0 ? String(Math.ceil(left)) : ''; }
  el.style.display = text ? 'grid' : 'none'; el.textContent = text;
}

// ---- menus -----------------------------------------------------------------
function openPanel(kind) {
  show('panel');
  const titles = { character: 'キャラクター', weapon: '武器', stage: 'ステージ', settings: '設定' };
  $('#panelTitle').textContent = titles[kind];
  const content = $('#panelContent');
  if (kind === 'settings') {
    const sliders = [['music', '音楽'], ['sfx', '効果音'], ['cue', '合図音']].map(([k, n]) => `<div class="setting"><label>${n}<span>${profile[k]}%</span></label><input data-setting="${k}" type="range" min="0" max="100" value="${profile[k]}"></div>`).join('');
    const tiers = [0, 1, 2, 3, 4, 5].map(n => `<option value="${n}" ${profile.tierOverride === n ? 'selected' : ''}>Tier ${n}</option>`).join('');
    content.innerHTML = `<div class="settings">${sliders}
      <div class="setting"><label>判定補正<span>${profile.calibrationMs > 0 ? '+' : ''}${profile.calibrationMs}ms</span></label><input data-setting="calibrationMs" type="range" min="-250" max="250" step="10" value="${profile.calibrationMs}"><small>音より遅れて判定されるなら＋へ</small></div>
      <div class="setting"><label>振動 <input data-setting="vibration" type="checkbox" ${profile.vibration ? 'checked' : ''}></label></div>
      <div class="setting"><label>診断表示 <input data-setting="diagnostics" type="checkbox" ${profile.diagnostics ? 'checked' : ''}></label></div>
      <div class="setting"><label>テスト難易度<select data-setting="tierOverride"><option value="-1">自動</option>${tiers}</select></label></div></div>`;
  } else {
    const data = { character: [['hamster', '🐹', 'ハムスター'], ['cat', '🐈‍⬛', '黒猫'], ['runner', '🧑', 'ランナー'], ['robot', '🤖', 'ロボット']], weapon: [['energy', '🗡️', 'エネルギー'], ['wood', '🪵', '木刀'], ['banana', '🍌', 'バナナ']], stage: [['kitchen', '🍳', '巨大キッチン'], ['toy', '🧸', '玩具部屋'], ['sky', '☁️', '空中果樹園'], ['neon', '🌃', 'ネオン市場']] }[kind];
    content.innerHTML = `<div class="cards">${data.map(([id, icon, name], i) => `<button class="choice ${profile[kind] === id ? 'selected' : ''} ${i > 0 && kind !== 'weapon' ? 'locked' : ''}" data-choice="${id}">${icon}<span>${name}${i > 0 && kind !== 'weapon' ? ' 🔒' : ''}</span></button>`).join('')}</div>`;
    $$('[data-choice]').forEach(b => { b.onclick = () => { if (b.classList.contains('locked')) return; profile[kind] = b.dataset.choice; save(); openPanel(kind); }; });
  }
  $$('[data-setting]').forEach(input => {
    input.oninput = () => {
      const k = input.dataset.setting;
      profile[k] = input.type === 'checkbox' ? input.checked : Number(input.value);
      save();
      if (input.type === 'range') input.previousElementSibling.querySelector('span').textContent = k === 'calibrationMs' ? `${profile[k] > 0 ? '+' : ''}${profile[k]}ms` : `${profile[k]}%`;
    };
  });
}

// ---- boot ------------------------------------------------------------------
async function loadData() {
  const play = $('#playButton');
  play.disabled = true;
  try {
    const [gameConfig, patternJson] = await Promise.all(['/data/game_config.json', '/data/patterns.json'].map(url => fetch(url).then(r => { if (!r.ok) throw new Error(`${url}: ${r.status}`); return r.json(); })));
    config = configFromGameConfig(gameConfig);
    patterns = patternsFromJson(patternJson);
    new RunEngine({ config, patterns, seed: 1 }); // rejects invalid data before any run starts
    play.disabled = false;
  } catch (error) {
    // Invalid data refuses the run instead of becoming player MISSes.
    $('.hero-card p').textContent = `データ読込エラー: ${error.message}`;
    console.error(error);
  }
}

$('#playButton').onclick = startRun;
$$('[data-panel]').forEach(b => { b.onclick = () => openPanel(b.dataset.panel); });
$('#panelBack').onclick = () => show('home');
$$('[data-lane]').forEach(b => b.addEventListener('pointerdown', e => { e.preventDefault(); tap(Number(b.dataset.lane), e.timeStamp); }));
document.addEventListener('keydown', e => {
  if (e.repeat) return;
  const map = { a: 0, ArrowLeft: 0, s: 1, ArrowDown: 1, d: 2, ArrowRight: 2 };
  if (map[e.key] !== undefined) { e.preventDefault(); tap(map[e.key], e.timeStamp); }
  else if (e.key === 'Escape' || e.key === 'p') { state === 'paused' ? resumeRun() : pauseRun(); }
});
document.addEventListener('visibilitychange', () => { if (document.hidden) pauseRun(); });
window.addEventListener('blur', () => pauseRun());
$('#pauseButton').onclick = () => pauseRun();
$('#resumeButton').onclick = resumeRun;
$('#restartButton').onclick = startRun;
$('#quitButton').onclick = quitToHome;
$('#retryButton').onclick = startRun;
$('#resultHome').onclick = quitToHome;
// Read-only hook for automated play checks (diagnostics / replay bots).
window.__rhythmDebug = { get engine() { return engine; }, get state() { return state; }, get musicStart() { return musicStart; }, bridge, spb: () => spb() };
save();
loadData();
