using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using RhythmSlashRunner.Core;

namespace RhythmSlashRunner.Presentation
{
    /// <summary>
    /// DSP-scheduled music and effects (review/round0/committee_sound.md).
    /// - Five loop stems start on the same DSP time and only their volumes change, so the music clock never moves.
    /// - Every sound category owns its own voice pool and Unity priority; the timing cues have a reserved pool of 24.
    /// - Synthesis runs on a worker thread at the output sample rate; <see cref="Ready"/> turns true when clips exist.
    /// </summary>
    public sealed class AudioDirector : MonoBehaviour
    {
        enum Category { Cue, Material, Tone, Perfect, Deco, Coin, Stinger, Ui, Tease, Long, Blade, Chop }

        sealed class Pool
        {
            public readonly List<AudioSource> Sources = new List<AudioSource>();
            public int Next;
            public AudioSource Take()
            {
                for (int i = 0; i < Sources.Count; i++)
                {
                    AudioSource free = Sources[(Next + i) % Sources.Count];
                    if (!free.isPlaying) { Next = (Next + i + 1) % Sources.Count; return free; }
                }
                AudioSource s = Sources[Next]; Next = (Next + 1) % Sources.Count; s.Stop(); return s;
            }
        }

        static readonly Dictionary<Category, int[]> PoolSpec = new Dictionary<Category, int[]>
        {
            // { voices, unity priority }
            { Category.Cue, new[] { 24, 0 } }, { Category.Material, new[] { 4, 64 } }, { Category.Tone, new[] { 4, 64 } }, { Category.Perfect, new[] { 2, 96 } },
            { Category.Deco, new[] { 2, 128 } }, { Category.Coin, new[] { 6, 160 } }, { Category.Stinger, new[] { 4, 80 } }, { Category.Ui, new[] { 3, 100 } },
            { Category.Tease, new[] { 1, 70 } }, { Category.Long, new[] { 2, 150 } }, { Category.Blade, new[] { 3, 60 } }, { Category.Chop, new[] { 4, 110 } },
        };

        // dB relative to the cue, per sound (committee_sound §4/§5)
        static float Db(Sfx id)
        {
            switch (id)
            {
                case Sfx.Cue: case Sfx.CountHigh: case Sfx.CountLow: return 0;
                case Sfx.OffbeatTick: return 0;
                // round 9: the material layer is louder so each object really sounds different
                case Sfx.MatSoft: case Sfx.MatJuicy: case Sfx.MatWood: case Sfx.MatCrystal: case Sfx.MatMetal:
                case Sfx.MatSplash: case Sfx.MatCrisp: case Sfx.MatBread: case Sfx.MatSqueak: case Sfx.MatSpring: case Sfx.MatDice: case Sfx.MatSlurp: case Sfx.MatClack: return -2;
                // round 17 (owner: "聞こえづらい"): the blade leads every cut; chopping sits a little under it
                case Sfx.Blade: return 0;
                case Sfx.ChopCrisp: case Sfx.ChopWood: case Sfx.ChopBread: case Sfx.ChopNoodle: return -6;
                case Sfx.ChopBlade: return -9;
                case Sfx.ParryClang: return -1;
                case Sfx.ParryFinish: return 0;
                case Sfx.MashHit: return -6;
                case Sfx.PassWhoosh: return -15;
                case Sfx.DroneSlash: return -10;
                case Sfx.GiantBeep: return -8;
                case Sfx.BombFuse: return -9;
                case Sfx.GiantStomp: return -5;
                case Sfx.BombDodge: return -8;
                case Sfx.HoldTick: return -12;
                case Sfx.SliceTone: return -12; // round 17: a quiet musical sweetener under the recorded cut
                case Sfx.PerfectShimmer: return -8;
                case Sfx.WeaponEnergy: case Sfx.WeaponWood: case Sfx.WeaponBanana: return -14;
                case Sfx.WeaponRoller: case Sfx.WeaponSaibashi: case Sfx.WeaponIce: case Sfx.WeaponLasso: case Sfx.WeaponCardboard: case Sfx.WeaponSmoke: return -11;
                case Sfx.Coin: case Sfx.CoinBurst: case Sfx.Jara: return -10;
                case Sfx.Miss: return -10;
                case Sfx.ComboBreak: return -14;
                case Sfx.UiSelect: return -12;
                case Sfx.UiConfirm: return -10;
                case Sfx.UiBack: case Sfx.UiDenied: return -13;
                case Sfx.ResultTick: return -14;
                default: return -6; // stingers, fever, record, purchase
            }
        }

        static Category CategoryOf(Sfx id)
        {
            switch (id)
            {
                case Sfx.Cue: case Sfx.OffbeatTick: case Sfx.CountHigh: case Sfx.CountLow: return Category.Cue;
                case Sfx.MatSoft: case Sfx.MatJuicy: case Sfx.MatWood: case Sfx.MatCrystal: case Sfx.MatMetal: case Sfx.Miss: case Sfx.ComboBreak: case Sfx.MashHit: return Category.Material;
                case Sfx.MatSplash: case Sfx.MatCrisp: case Sfx.MatBread: case Sfx.MatSqueak: case Sfx.MatSpring: case Sfx.MatDice: case Sfx.MatSlurp: case Sfx.MatClack: return Category.Material;
                case Sfx.HoldTick: case Sfx.PassWhoosh: case Sfx.DroneSlash: return Category.Deco;
                case Sfx.Blade: return Category.Blade;
                case Sfx.ChopCrisp: case Sfx.ChopWood: case Sfx.ChopBread: case Sfx.ChopNoodle: case Sfx.ChopBlade: return Category.Chop;
                case Sfx.ParryClang: case Sfx.ParryFinish: return Category.Material;
                case Sfx.SliceTone: return Category.Tone;
                case Sfx.PerfectShimmer: return Category.Perfect;
                case Sfx.WeaponEnergy: case Sfx.WeaponWood: case Sfx.WeaponBanana: return Category.Deco;
                case Sfx.WeaponRoller: case Sfx.WeaponSaibashi: case Sfx.WeaponIce: case Sfx.WeaponLasso: case Sfx.WeaponCardboard: case Sfx.WeaponSmoke: return Category.Deco;
                case Sfx.Coin: case Sfx.CoinBurst: case Sfx.ResultTick: return Category.Coin;
                case Sfx.Jara: return Category.Long;  // 1.5 s rain is never cut by rapid ticks (sound P1)
                case Sfx.Kyuin: return Category.Tease; // own voice: FEVER stingers can never stop the tease (sound P0)
                case Sfx.UiSelect: case Sfx.UiConfirm: case Sfx.UiBack: case Sfx.UiDenied: case Sfx.LoadDone: case Sfx.Purchase: return Category.Ui;
                default: return Category.Stinger;
            }
        }

        readonly Dictionary<Category, Pool> pools = new Dictionary<Category, Pool>();
        readonly Dictionary<Sfx, AudioClip> clips = new Dictionary<Sfx, AudioClip>();
        // Two sets of stem voices: at a stage change the next tempo starts on the other set at the chunk boundary's DSP time.
        readonly AudioSource[][] stemSets = { new AudioSource[5], new AudioSource[5] };
        readonly float[] stemGain = new float[5];
        readonly float[] stemTarget = new float[5];
        readonly Dictionary<int, AudioClip[]> stemClips = new Dictionary<int, AudioClip[]>();
        readonly Dictionary<int, Task<Dictionary<Stem, float[]>>> tempoTasks = new Dictionary<int, Task<Dictionary<Stem, float[]>>>();
        readonly List<int> clipOrder = new List<int>();
        int activeSet, scheduledSegment, pendingSet = -1, pendingBpm;
        double setSwitchDsp, pendingPhase, trackStartBeat;
        /// <summary>The piece playing now (stage, FEVER or boss). Changes land on a bar line.</summary>
        MusicTrack track = MusicTrack.Kitchen;
        AudioSource menu;
        Task<float[]> menuTask;
        float menuLevel, menuTarget;
        static int Key(MusicTrack t, int bpm) { return (int)t * 1000 + bpm; }
        TempoMap tempo = new TempoMap(SynthBank.Bpm);
        Task<Dictionary<Stem, float[]>> stemTask;
        AudioSource wind;
        float windLevel, windTarget;
        Task<Dictionary<Sfx, float[]>> sfxTask;
        int rate;
        float musicVolume = .6f, sfxVolume = .8f, cueVolume = .8f;
        int layer, pendingLayer = -1;
        double layerSwitchDsp;
        double duckUntilDsp, duckDb;
        readonly List<double> cueTimes = new List<double>(32);
        bool playing;

        public bool Ready { get; private set; }
        public double MusicStartDsp { get; private set; }
        public int Layer { get { return layer; } }

        public void Initialize(float music01, float sfx01, float cue01)
        {
            rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
            foreach (KeyValuePair<Category, int[]> spec in PoolSpec)
            {
                var pool = new Pool();
                for (int i = 0; i < spec.Value[0]; i++)
                {
                    AudioSource s = gameObject.AddComponent<AudioSource>();
                    s.playOnAwake = false; s.priority = spec.Value[1];
                    pool.Sources.Add(s);
                }
                pools[spec.Key] = pool;
            }
            foreach (AudioSource[] set in stemSets)
                for (int i = 0; i < set.Length; i++)
                {
                    set[i] = gameObject.AddComponent<AudioSource>();
                    set[i].playOnAwake = false; set[i].loop = true; set[i].priority = 16;
                }
            wind = gameObject.AddComponent<AudioSource>();
            wind.playOnAwake = false; wind.loop = true; wind.priority = 40;
            menu = gameObject.AddComponent<AudioSource>();
            menu.playOnAwake = false; menu.loop = true; menu.priority = 20;
            int r = rate;
            RecordedMusic.Load(); // round 34: on the main thread, before any piece is built
            stemTask = Task.Run(() => SynthBank.BuildTrack(MusicTrack.Kitchen, r, SynthBank.Bpm));
            menuTask = Task.Run(() => SynthBank.BuildTrackMix(MusicTrack.Home, r, 92));
            sfxTask = Task.Run(() =>
            {
                var result = new Dictionary<Sfx, float[]>();
                foreach (Sfx id in (Sfx[])Enum.GetValues(typeof(Sfx))) if (!result.ContainsKey(id)) result[id] = SynthBank.BuildSfx(id, r);
                return result;
            });
            SetVolumes(music01, sfx01, cue01);
        }

        public void SetVolumes(float music01, float sfx01, float cue01) { musicVolume = music01; sfxVolume = sfx01; cueVolume = cue01; }

        void FinishLoading()
        {
            if (Ready || stemTask == null || !stemTask.IsCompleted || !sfxTask.IsCompleted) return;
            if (stemTask.IsFaulted || sfxTask.IsFaulted) { Debug.LogException(stemTask.Exception ?? sfxTask.Exception); Ready = true; return; }
            Store(Key(MusicTrack.Kitchen, (int)SynthBank.Bpm), MakeStemClips(stemTask.Result, Key(MusicTrack.Kitchen, (int)SynthBank.Bpm)));
            float[] windData;
            if (sfxTask.Result.TryGetValue(Sfx.WindLoop, out windData))
            {
                wind.clip = AudioClip.Create("wind", windData.Length, 1, rate, false);
                wind.clip.SetData(windData, 0);
            }
            foreach (KeyValuePair<Sfx, float[]> pair in sfxTask.Result)
            {
                AudioClip clip = AudioClip.Create("sfx_" + pair.Key, Math.Max(1, pair.Value.Length), 1, rate, false);
                clip.SetData(pair.Value, 0);
                clips[pair.Key] = clip;
            }
            // round 17: recorded cut sounds (CC0 recordings layered offline) replace the synthesised ones where present
            int recorded = 0;
            foreach (Sfx id in (Sfx[])Enum.GetValues(typeof(Sfx)))
            {
                AudioClip rec = Resources.Load<AudioClip>("Audio/Sfx/" + id);
                if (rec == null) continue;
                if (rec.loadState == AudioDataLoadState.Unloaded) rec.LoadAudioData();
                clips[id] = rec; recorded++;
            }
            Debug.Log("[Audio] recorded sfx " + recorded);
            Ready = true;
        }

        /// <summary>Stereo stems (interleaved L/R) become 2-channel clips.</summary>
        AudioClip[] MakeStemClips(Dictionary<Stem, float[]> data, int key)
        {
            var result = new AudioClip[5];
            foreach (KeyValuePair<Stem, float[]> pair in data)
            {
                AudioClip clip = AudioClip.Create("music_" + (MusicTrack)(key / 1000) + "_" + key % 1000 + "_" + pair.Key, pair.Value.Length / 2, 2, rate, false);
                clip.SetData(pair.Value, 0);
                result[(int)pair.Key] = clip;
            }
            return result;
        }

        /// <summary>Keeps at most a handful of rendered pieces in memory (a piece is ~15 MB); the oldest unused one goes.</summary>
        void Store(int key, AudioClip[] value)
        {
            stemClips[key] = value;
            clipOrder.Remove(key); clipOrder.Add(key);
            while (clipOrder.Count > 6)
            {
                int victim = -1;
                foreach (int k in clipOrder) if (!InUse(k)) { victim = k; break; }
                if (victim < 0) break;
                clipOrder.Remove(victim);
                foreach (AudioClip c in stemClips[victim]) if (c != null) Destroy(c);
                stemClips.Remove(victim);
            }
        }

        bool InUse(int key)
        {
            if (key == Key(MusicTrack.Kitchen, (int)SynthBank.Bpm)) return true; // the always-ready fallback
            foreach (AudioSource[] set in stemSets) foreach (AudioSource s in set) if (s.clip != null && s.clip.name.StartsWith("music_" + (MusicTrack)(key / 1000) + "_" + key % 1000 + "_")) return true;
            return false;
        }

        /// <summary>Renders a piece at a tempo in the background, ahead of when it is needed (stage change, boss, FEVER).</summary>
        public void Prepare(MusicTrack piece, int bpm)
        {
            int key = Key(piece, bpm), r = rate;
            if (stemClips.ContainsKey(key) || tempoTasks.ContainsKey(key)) return;
            tempoTasks[key] = Task.Run(() => SynthBank.BuildTrack(piece, r, bpm));
        }

        public bool IsPrepared(MusicTrack piece, int bpm) { return stemClips.ContainsKey(Key(piece, bpm)); }

        // ---------------------------------------------------------------- menu music (home, shop, results)

        public void PlayMenuMusic() { menuTarget = 1f; }
        public void StopMenuMusic(bool immediate = false) { menuTarget = 0f; if (immediate) { menuLevel = 0; menu.Stop(); } }

        void UpdateMenu()
        {
            if (menu.clip == null && menuTask != null && menuTask.IsCompleted)
            {
                if (!menuTask.IsFaulted)
                {
                    menu.clip = AudioClip.Create("music_home", menuTask.Result.Length / 2, 2, rate, false);
                    menu.clip.SetData(menuTask.Result, 0);
                }
                menuTask = null;
            }
            menuLevel = Mathf.MoveTowards(menuLevel, menuTarget, Time.unscaledDeltaTime / (menuTarget > menuLevel ? 1.2f : .3f));
            if (menu.clip == null) return;
            if (menuLevel > .001f && !menu.isPlaying) menu.Play();
            if (menuLevel <= .001f && menu.isPlaying) menu.Stop();
            menu.volume = musicVolume * .7f * menuLevel;
        }

        void CollectTempos()
        {
            if (tempoTasks.Count == 0) return;
            int done = -1;
            foreach (KeyValuePair<int, Task<Dictionary<Stem, float[]>>> pair in tempoTasks) if (pair.Value.IsCompleted) { done = pair.Key; break; }
            if (done < 0) return;
            Task<Dictionary<Stem, float[]>> task = tempoTasks[done];
            tempoTasks.Remove(done);
            if (task.IsFaulted) { Debug.LogException(task.Exception); return; }
            Store(done, MakeStemClips(task.Result, done)); // one piece per frame keeps the upload hitch small
        }

        void Update()
        {
            FinishLoading();
            if (!Ready) return;
            CollectTempos();
            UpdateMenu();
            double dsp = AudioSettings.dspTime;
            ScheduleTempoChange(dsp);
            if (pendingSet >= 0 && dsp >= setSwitchDsp) { activeSet = pendingSet; pendingSet = -1; }
            if (pendingLayer >= 0 && dsp >= layerSwitchDsp) { ApplyLayerTargets(pendingLayer); pendingLayer = -1; }
            // ducking: −3 dB around each timing cue, −6 dB for big stingers
            float duck = 1f;
            for (int i = cueTimes.Count - 1; i >= 0; i--)
            {
                double dt = dsp - cueTimes[i];
                if (dt > .15) { cueTimes.RemoveAt(i); continue; }
                if (dt >= -.005) duck = Mathf.Min(duck, Mathf.Lerp(.71f, 1f, (float)(dt / .12)));
            }
            if (dsp < duckUntilDsp) duck = Mathf.Min(duck, Mathf.Pow(10f, (float)duckDb / 20f));
            // wind: rises with the run's speed, under the music
            windLevel = Mathf.MoveTowards(windLevel, playing ? windTarget : 0f, Time.unscaledDeltaTime / .4f);
            if (wind.clip != null)
            {
                if (windLevel > .001f && !wind.isPlaying) wind.Play();
                if (windLevel <= .001f && wind.isPlaying) wind.Stop();
                wind.volume = sfxVolume * .32f * windLevel * duck;
                wind.pitch = .85f + .45f * windLevel;
            }
            int active = 0;
            for (int i = 0; i < stemTarget.Length; i++) if (stemTarget[i] > 0) active++;
            // boss pieces a little louder (owner round 16)
            float trackGain = SynthBank.IsBossPiece(track) ? 1.22f : 1f;
            float master = musicVolume * .95f * trackGain * Mathf.Pow(10f, -1.2f * Mathf.Max(0, active - 1) / 20f);
            float step = Time.unscaledDeltaTime / .08f; // ~80 ms ramps
            for (int i = 0; i < stemTarget.Length; i++)
            {
                stemGain[i] = Mathf.MoveTowards(stemGain[i], stemTarget[i], step);
                foreach (AudioSource[] set in stemSets) set[i].volume = stemGain[i] * master * duck;
            }
        }

        double DspOfBeat(double beat) { return MusicStartDsp + tempo.SecondsAt(beat); }

        int SegmentOf(double beat)
        {
            int index = 0;
            for (int i = 1; i < tempo.Count; i++) if (beat >= tempo.BeatOf(i) - 1e-6) index = i;
            return index;
        }

        /// <summary>
        /// Loads the current piece at <paramref name="bpm"/> into a voice set. If that tempo is still rendering, the same piece
        /// at another tempo (or the always-ready kitchen loop) plays re-pitched, so the beat never drifts.
        /// </summary>
        void Load(AudioSource[] set, int bpm, double phaseBeats)
        {
            AudioClip[] clipsForTempo;
            float pitch = 1f;
            if (!stemClips.TryGetValue(Key(track, bpm), out clipsForTempo))
            {
                int fallback = -1;
                foreach (int k in stemClips.Keys) if (k / 1000 == (int)track) { fallback = k; break; }
                if (fallback < 0) fallback = Key(MusicTrack.Kitchen, (int)SynthBank.Bpm);
                clipsForTempo = stemClips[fallback];
                pitch = (float)bpm / (fallback % 1000);
                bpm = fallback % 1000;
            }
            double spb = 60.0 / bpm;
            for (int i = 0; i < set.Length; i++)
            {
                set[i].clip = clipsForTempo[i];
                set[i].pitch = pitch;
                if (set[i].clip != null) set[i].timeSamples = (int)(phaseBeats * spb * rate) % set[i].clip.samples;
            }
        }

        void ScheduleTempoChange(double dsp)
        {
            if (!playing || scheduledSegment + 1 >= tempo.Count) return;
            double boundary = tempo.BeatOf(scheduledSegment + 1);
            double at = DspOfBeat(boundary);
            if (at - dsp > 1.0) return;
            scheduledSegment++;
            if (at <= dsp + .02) return; // too late to switch cleanly (a hitch); the next resume picks the right tempo
            trackStartBeat = boundary; // a new stage starts its piece from the top
            SwitchAt(at, (int)Math.Round(tempo.BpmOf(scheduledSegment)), 0);
        }

        /// <summary>Starts the other voice set at <paramref name="at"/> and ends the current one there (sample-exact handover).</summary>
        void SwitchAt(double at, int bpm, double phase)
        {
            int next = 1 - activeSet;
            foreach (AudioSource s in stemSets[next]) s.Stop();
            Load(stemSets[next], bpm, phase);
            foreach (AudioSource s in stemSets[next]) if (s.clip != null) s.PlayScheduled(at);
            foreach (AudioSource s in stemSets[activeSet]) if (s.isPlaying) s.SetScheduledEndTime(at);
            pendingSet = next; pendingBpm = bpm; pendingPhase = phase;
            setSwitchDsp = at;
        }

        public MusicTrack Track { get { return track; } }

        /// <summary>
        /// Changes the piece on the next bar line, starting it from its top. A handover already scheduled (stage change) is
        /// re-loaded with the new piece instead, so both land on the same beat.
        /// </summary>
        public void SetTrack(MusicTrack piece)
        {
            if (piece == track) return;
            track = piece;
            Debug.Log("[Music] -> " + piece + (IsPrepared(piece, (int)Math.Round(tempo.BpmAt(tempo.BeatAt(AudioSettings.dspTime - MusicStartDsp)))) ? "" : " (still rendering: re-pitched fallback)"));
            if (!playing || !Ready) return;
            if (pendingSet >= 0)
            {
                foreach (AudioSource s in stemSets[pendingSet]) s.Stop();
                Load(stemSets[pendingSet], pendingBpm, 0);
                foreach (AudioSource s in stemSets[pendingSet]) if (s.clip != null) s.PlayScheduled(setSwitchDsp);
                return;
            }
            double now = AudioSettings.dspTime;
            double beatNow = tempo.BeatAt(now + .05 - MusicStartDsp);
            double bar = Math.Ceiling(beatNow / 4 + 1e-6) * 4;
            trackStartBeat = bar;
            SwitchAt(DspOfBeat(bar), (int)Math.Round(tempo.BpmAt(bar)), 0);
        }

        /// <summary>Every run starts on its first stage's piece.</summary>
        public void ResetTrack(MusicTrack first) { track = first; trackStartBeat = 0; }

        /// <summary>
        /// Starts every stem so that beat <paramref name="startBeat"/> sounds at <paramref name="startDsp"/>, following the
        /// run's tempo map (later stage changes are picked up as the engine appends them).
        /// </summary>
        public void StartMusic(double startDsp, double startBeat, TempoMap runTempo)
        {
            StopAll();
            if (!Ready) return;
            tempo = runTempo ?? new TempoMap(SynthBank.Bpm);
            MusicStartDsp = startDsp - tempo.SecondsAt(startBeat);
            double playDsp = Math.Max(startDsp, MusicStartDsp);
            double beatAtPlay = tempo.BeatAt(playDsp - MusicStartDsp);
            scheduledSegment = SegmentOf(beatAtPlay);
            int loopBeats = RecordedMusic.BeatsOf(track, tempo.BpmOf(SegmentOf(beatAtPlay))); // round 34: recorded loops may be longer than 16 beats
            double phase = (((beatAtPlay - trackStartBeat) % loopBeats) + loopBeats) % loopBeats;
            activeSet = 0; pendingSet = -1;
            Load(stemSets[0], (int)Math.Round(tempo.BpmOf(scheduledSegment)), phase);
            foreach (AudioSource s in stemSets[0]) if (s.clip != null) s.PlayScheduled(playDsp);
            ApplyLayerTargets(layer);
            for (int i = 0; i < stemGain.Length; i++) stemGain[i] = stemTarget[i];
            playing = true;
        }

        /// <summary>0 base, 1 +drums, 2 +bass, 3 +lead, 4 FEVER. Takes effect at the next bar (4 beats).</summary>
        public void SetLayer(int level, bool immediate = false)
        {
            level = Mathf.Clamp(level, 0, 4);
            if (level == layer && pendingLayer < 0) return;
            layer = level;
            if (immediate || !playing) { ApplyLayerTargets(level); pendingLayer = -1; return; }
            double beatNow = tempo.BeatAt(AudioSettings.dspTime - MusicStartDsp);
            layerSwitchDsp = DspOfBeat(Math.Ceiling(beatNow / 4 + 1e-6) * 4);
            pendingLayer = level;
        }

        void ApplyLayerTargets(int level)
        {
            // owner round 16 ("曲が聞こえづらくてリズムが途切れる"): the beat (drums, bass) and the tune never drop out; a combo
            // only makes it fuller. 0 base+drums+bass+tune · 1-3 the tune and the extra layer grow · 4 everything
            stemTarget[0] = 1;
            stemTarget[1] = 1;
            stemTarget[2] = 1;
            stemTarget[3] = level >= 2 ? 1 : level >= 1 ? .9f : .8f;
            stemTarget[4] = level >= 4 ? 1 : level >= 3 ? .85f : level >= 2 ? .7f : .55f;
        }

        /// <summary>0 = still, 1 = full rush (boost). Smoothed over 0.4 s.</summary>
        public void SetWind(float level01) { windTarget = Mathf.Clamp01(level01); }

        public void Duck(double db, double seconds)
        {
            double now = AudioSettings.dspTime;
            // a short slash duck never shortens or weakens a deeper one already running (stingers)
            if (now < duckUntilDsp && duckDb <= db) { duckUntilDsp = Math.Max(duckUntilDsp, now + seconds); return; }
            duckDb = db; duckUntilDsp = now + seconds;
        }

        /// <summary>Ends the loop on the next beat and rings the end chord there (no hard cut).</summary>
        public void EndMusic()
        {
            if (!playing) return;
            double beatNow = tempo.BeatAt(AudioSettings.dspTime - MusicStartDsp);
            double next = DspOfBeat(Math.Ceiling(beatNow + 1e-6));
            foreach (AudioSource[] set in stemSets) foreach (AudioSource s in set) if (s.isPlaying) s.SetScheduledEndTime(next);
            PlayAt(Sfx.EndChord, next);
            playing = false;
            pendingLayer = -1;
        }

        public void StopAll()
        {
            foreach (AudioSource[] set in stemSets) foreach (AudioSource s in set) s.Stop();
            pendingSet = -1;
            foreach (Pool p in pools.Values) foreach (AudioSource s in p.Sources) s.Stop();
            cueTimes.Clear();
            playing = false;
            pendingLayer = -1;
        }

        public void StopTease() { foreach (AudioSource s in pools[Category.Tease].Sources) s.Stop(); }
        public void StopStingers() { foreach (AudioSource s in pools[Category.Stinger].Sources) s.Stop(); StopTease(); }

        float Volume(Sfx id)
        {
            Category c = CategoryOf(id);
            float bus = c == Category.Cue ? cueVolume : sfxVolume;
            return bus * Mathf.Pow(10f, Db(id) / 20f);
        }

        /// <summary>Plays a clip exactly at a DSP time (count-in, approach cue, tease, stem-synced stingers).</summary>
        public void PlayAt(Sfx id, double dsp, float volumeScale = 1f, float pitch = 1f)
        {
            AudioClip clip;
            if (!Ready || !clips.TryGetValue(id, out clip) || dsp < AudioSettings.dspTime) return;
            AudioSource s = pools[CategoryOf(id)].Take();
            s.clip = clip; s.pitch = pitch; s.volume = Volume(id) * volumeScale; s.panStereo = 0;
            s.PlayScheduled(dsp);
            if (id == Sfx.Cue) cueTimes.Add(dsp);
        }

        public void Play(Sfx id, float volumeScale = 1f, float pitch = 1f, float pan = 0f)
        {
            AudioClip clip;
            if (!Ready || !clips.TryGetValue(id, out clip)) return;
            AudioSource s = pools[CategoryOf(id)].Take();
            s.clip = clip; s.pitch = pitch; s.volume = Volume(id) * volumeScale; s.panStereo = pan;
            s.Play();
        }

        // ---------------------------------------------------------------- slash pitch ladder (C major pentatonic)

        static readonly int[] LadderLow = { 0, 2, 4, 7, 9, 12, 14, 16 };      // C5 D5 E5 G5 A5 C6 D6 E6
        static readonly int[] LadderMid = { 7, 9, 12, 14, 16, 19, 21, 19 };   // G5 … A6 (capped below the cue band)
        static readonly int[] LadderFever = { 12, 14, 16, 19, 21, 19, 21, 16 }; // C6 … A6, bouncing at the top

        /// <summary>Semitones above C5 for this combo: one step per hit, eight steps per cycle, higher sets with combo tiers.</summary>
        public static int LadderSemitones(int combo, bool fever)
        {
            int step = Math.Max(0, combo - 1) % 8;
            if (fever) return LadderFever[step];
            return combo >= 25 ? LadderMid[step] : LadderLow[step];
        }

        public void PlaySlash(Sfx material, int combo, bool fever, bool perfect, Sfx weaponDeco)
        {
            // round 17: blade "ズバッ" + the material, with the music stepping back for a moment so the cut is heard
            Play(Sfx.Blade, perfect ? 1f : .85f, perfect ? 1.04f : 1f);
            Play(material);
            Duck(-2.5, .09);
            float pitch = Mathf.Pow(2f, LadderSemitones(combo, fever) / 12f);
            Play(Sfx.SliceTone, perfect ? 1f : .8f, pitch);
            if (perfect)
            {
                Play(Sfx.SliceTone, .32f, pitch * .5f); // octave-below layer at −10 dB (keeps 2-3.5 kHz free for cues)
                Play(Sfx.PerfectShimmer);
            }
            Play(weaponDeco);
        }
    }
}
