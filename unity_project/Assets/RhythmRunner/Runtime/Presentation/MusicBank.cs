using System;
using System.Collections.Generic;

namespace RhythmSlashRunner.Presentation
{
    /// <summary>The ten pieces of music (owner round 6: "10パターンぐらい、違う印象を与える音楽").</summary>
    public enum MusicTrack
    {
        Kitchen, ToyRoom, SkyOrchard, NeonMarket, CandyLand, Freezer, Fever, BossOnion, BossToaster, Home, BossIcecream,
        // round 16: boss versions of the stage pieces (the stage's own tune over a heavy boss beat), and Count Donut's piece
        KitchenRemix, ToyRoomRemix, SkyOrchardRemix, NeonMarketRemix, CandyLandRemix, FreezerRemix, BossDonut,
        // round 30: the non-food bosses (all on the onion king's low end)
        BossZenmai, BossKaminari, BossYukidaruma,
    }

    /// <summary>
    /// Stereo music synthesis (round 6). Every track is a 16-beat loop (A section = bars 1-2, B section = bars 3-4) in five
    /// stems that the combo layers in (Base, Drums, Bass, Lead, Extra), rendered at any tempo so stage speed-ups stay in
    /// sync. Stems are interleaved stereo; instruments are panned and share a loop-aware reverb, which is what makes the
    /// mix sound wide and heavy compared with the old mono 8-beat loops. Everything is original material.
    /// </summary>
    public static partial class SynthBank
    {
        public const int TrackBeats = 16;

        public static MusicTrack TrackForStage(string stageId)
        {
            switch (stageId)
            {
                case "stage_toy_room": return MusicTrack.ToyRoom;
                case "stage_sky_orchard": return MusicTrack.SkyOrchard;
                case "stage_neon_market": return MusicTrack.NeonMarket;
                case "stage_candy_land": return MusicTrack.CandyLand;
                case "stage_freezer": return MusicTrack.Freezer;
                default: return MusicTrack.Kitchen;
            }
        }

        public static MusicTrack TrackForBoss(string bossId)
        {
            switch (bossId)
            {
                case "boss_toaster": return MusicTrack.BossToaster;
                case "boss_icecream": return MusicTrack.BossIcecream;
                case "boss_donut": return MusicTrack.BossDonut;
                case "boss_zenmai": return MusicTrack.BossZenmai;
                case "boss_kaminari": return MusicTrack.BossKaminari;
                case "boss_yukidaruma": return MusicTrack.BossYukidaruma;
                default: return MusicTrack.BossOnion;
            }
        }

        /// <summary>The boss version of a stage piece (owner round 16: "リミックスみたいにボス曲が流れてくる").</summary>
        public static MusicTrack RemixOf(MusicTrack stage)
        {
            switch (stage)
            {
                case MusicTrack.Kitchen: return MusicTrack.KitchenRemix;
                case MusicTrack.ToyRoom: return MusicTrack.ToyRoomRemix;
                case MusicTrack.SkyOrchard: return MusicTrack.SkyOrchardRemix;
                case MusicTrack.NeonMarket: return MusicTrack.NeonMarketRemix;
                case MusicTrack.CandyLand: return MusicTrack.CandyLandRemix;
                case MusicTrack.Freezer: return MusicTrack.FreezerRemix;
                default: return stage;
            }
        }

        /// <summary>The stage piece a remix is made from (itself for anything else).</summary>
        public static MusicTrack SourceOf(MusicTrack remix)
        {
            switch (remix)
            {
                case MusicTrack.KitchenRemix: return MusicTrack.Kitchen;
                case MusicTrack.ToyRoomRemix: return MusicTrack.ToyRoom;
                case MusicTrack.SkyOrchardRemix: return MusicTrack.SkyOrchard;
                case MusicTrack.NeonMarketRemix: return MusicTrack.NeonMarket;
                case MusicTrack.CandyLandRemix: return MusicTrack.CandyLand;
                case MusicTrack.FreezerRemix: return MusicTrack.Freezer;
                default: return remix;
            }
        }

        public static bool IsBossPiece(MusicTrack t) { return t == MusicTrack.BossOnion || t == MusicTrack.BossToaster || t == MusicTrack.BossIcecream || t == MusicTrack.BossDonut || t == MusicTrack.BossZenmai || t == MusicTrack.BossKaminari || t == MusicTrack.BossYukidaruma || SourceOf(t) != t; }

        // ---------------------------------------------------------------- stereo buffers

        sealed class Mix
        {
            public readonly float[] L, R;
            public Mix(int n) { L = new float[n]; R = new float[n]; }
            public int N { get { return L.Length; } }
            /// <summary>Constant-power pan: -1 left … +1 right. Writes wrap around the loop.</summary>
            public void Put(int index, double v, double gl, double gr)
            {
                int n = L.Length, i = ((index % n) + n) % n;
                L[i] += (float)(v * gl); R[i] += (float)(v * gr);
            }
        }

        static void PanGains(double pan, out double gl, out double gr)
        {
            double a = (Math.Max(-1, Math.Min(1, pan)) + 1) * Math.PI / 4;
            gl = Math.Cos(a) * 1.414; gr = Math.Sin(a) * 1.414;
        }

        sealed class Ctx
        {
            public int Rate, N;
            public double Spb;
            public Rng Rng;
            public int At(double step16) { return (int)Math.Round(step16 * Spb * .25 * Rate); }
            public double Sec(double steps16) { return steps16 * Spb * .25; }
        }

        static double Mtof(double midi) { return 440.0 * Math.Pow(2, (midi - 69) / 12.0); }

        // ---------------------------------------------------------------- instruments (stereo)

        static void SKick(Mix m, Ctx c, int at, double gain, double pitch = 1)
        {
            int n = (int)(.3 * c.Rate); double ph = 0;
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / c.Rate;
                ph += 2 * Math.PI * (48 + 140 * Math.Exp(-t / .03)) * pitch / c.Rate;
                double v = Math.Tanh(3.0 * Math.Sin(ph)) * Math.Exp(-t / .12) + (t < .004 ? Math.Sin(2 * Math.PI * 2800 * t) * (1 - t / .004) * .3 : 0);
                m.Put(at + i, v * gain * .82 * Math.Min(1, t / .0015), 1, 1); // eased onset: no click on a loop seam (round 16: heavier)
            }
        }

        static void SSnare(Mix m, Ctx c, int at, double gain, double pan = 0, double tone = 190)
        {
            double gl, gr; PanGains(pan, out gl, out gr);
            int n = (int)(.25 * c.Rate); double lp = 0;
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / c.Rate, x = Noise(ref c.Rng);
                lp += (x - lp) * .4;
                double v = (x - lp) * Math.Exp(-t / .07) * .8 + Math.Sin(2 * Math.PI * tone * t) * Math.Exp(-t / .03) * .5;
                m.Put(at + i, v * gain * .5 * Math.Min(1, t / .001), gl, gr);
            }
        }

        static void SHat(Mix m, Ctx c, int at, double gain, double tau, double pan)
        {
            double gl, gr; PanGains(pan, out gl, out gr);
            int n = (int)(tau * 6 * c.Rate); double prev = 0, hp = 0;
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / c.Rate, x = Noise(ref c.Rng);
                hp = .8 * (hp + x - prev); prev = x;
                m.Put(at + i, hp * Math.Exp(-t / tau) * gain * Math.Min(1, t / .0008), gl, gr);
            }
        }

        static void SClap(Mix m, Ctx c, int at, double gain, double pan = 0)
        {
            double gl, gr; PanGains(pan, out gl, out gr);
            int n = (int)(.2 * c.Rate); double lp = 0;
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / c.Rate, x = Noise(ref c.Rng);
                lp += (x - lp) * .35;
                double env = Math.Exp(-t / .05) + (t < .03 ? .7 * Math.Exp(-(t % .009) * 280) : 0);
                m.Put(at + i, (x - lp) * env * gain * .5 * Math.Min(1, t / .001), gl, gr);
            }
        }

        static void STom(Mix m, Ctx c, int at, double freq, double gain, double pan)
        {
            double gl, gr; PanGains(pan, out gl, out gr);
            int n = (int)(.4 * c.Rate); double ph = 0;
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / c.Rate;
                ph += 2 * Math.PI * freq * (1 + .6 * Math.Exp(-t / .04)) / c.Rate;
                m.Put(at + i, Math.Tanh(1.6 * Math.Sin(ph)) * Math.Exp(-t / .16) * gain * Math.Min(1, t / .001), gl, gr);
            }
        }

        static void SCrash(Mix m, Ctx c, int at, double gain)
        {
            int n = (int)(1.4 * c.Rate); double prevL = 0, hpL = 0;
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / c.Rate, x = Noise(ref c.Rng);
                hpL = .85 * (hpL + x - prevL); prevL = x;
                double v = hpL * Math.Exp(-t / .45) * gain * Math.Min(1, t / .003);
                m.Put(at + i, v, 1.1, .9);
                m.Put(at + i + 37, v * .9, .8, 1.1); // a hair later on the right: wide
            }
        }

        /// <summary>Subtractive voice: detuned saws (or square) through a 2-pole low-pass with an envelope on the cutoff.</summary>
        static void SSynth(Mix m, Ctx c, int at, double midi, double gain, double seconds, double pan, double cutFrom, double cutTo, double attack = .004,
                           double detune = .006, bool square = false, double vibrato = 0, double release = .03)
        {
            double gl, gr; PanGains(pan, out gl, out gr);
            double f = Mtof(midi);
            int n = (int)((seconds + release) * c.Rate); double lp = 0, lp2 = 0;
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / c.Rate;
                double vib = 1 + vibrato * Math.Sin(2 * Math.PI * 5.5 * t) * Math.Min(1, t / .25);
                double x = square ? Math.Sign(Math.Sin(2 * Math.PI * f * vib * t)) * .7 + Math.Sign(Math.Sin(2 * Math.PI * f * vib * (1 + detune) * t)) * .5
                                  : Saw(f * vib * (1 + detune), t) + Saw(f * vib * (1 - detune), t) + .6 * Saw(f * vib, t);
                double cut = cutTo + (cutFrom - cutTo) * Math.Exp(-t / .12);
                double a = Math.Min(.99, cut * 2 * Math.PI / c.Rate);
                lp += (x - lp) * a; lp2 += (lp - lp2) * a;
                double env = Math.Min(1, t / attack) * (t < seconds ? 1 : Math.Max(0, 1 - (t - seconds) / release));
                m.Put(at + i, lp2 * env * gain, gl, gr);
            }
        }

        static void SPluck(Mix m, Ctx c, int at, double midi, double gain, double seconds, double pan, double bright = 3000, double decay = .25)
        {
            double gl, gr; PanGains(pan, out gl, out gr);
            double f = Mtof(midi);
            int n = (int)(seconds * c.Rate); double lp = 0, lp2 = 0;
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / c.Rate;
                double x = Saw(f, t) + .5 * Math.Sin(2 * Math.PI * f * t);
                double cut = 150 + bright * Math.Exp(-t / .06);
                double a = Math.Min(.99, cut * 2 * Math.PI / c.Rate);
                lp += (x - lp) * a; lp2 += (lp - lp2) * a;
                double env = Math.Min(1, t / .002) * Math.Exp(-t / decay) * Math.Min(1, (seconds - t) / .01);
                m.Put(at + i, Math.Tanh(1.5 * lp2) * env * gain, gl, gr);
            }
        }

        static void SSub(Mix m, Ctx c, int at, double midi, double gain, double seconds)
        {
            double f = Mtof(midi);
            int n = (int)(seconds * c.Rate);
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / c.Rate, w = 2 * Math.PI * f * t;
                double env = Math.Min(1, t / .006) * Math.Min(1, (seconds - t) / .02);
                // round 16: 2nd and 3rd harmonics so a phone speaker (no real low end) still carries the bass line
                m.Put(at + i, Math.Tanh(2.6 * (Math.Sin(w) + .45 * Math.Sin(2 * w) + .2 * Math.Sin(3 * w))) * env * gain * 1.25, 1, 1);
            }
        }

        /// <summary>FM electric piano / bell (ratio 1 = e-piano, 3.5 = bell).</summary>
        static void SKeys(Mix m, Ctx c, int at, double midi, double gain, double seconds, double pan, double ratio = 1, double index = 1.6, double decay = .6)
        {
            double gl, gr; PanGains(pan, out gl, out gr);
            double f = Mtof(midi);
            int n = (int)(seconds * c.Rate);
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / c.Rate, w = 2 * Math.PI * f * t;
                double v = Math.Sin(w + index * Math.Exp(-t / .15) * Math.Sin(w * ratio)) + .25 * Math.Sin(2 * w) * Math.Exp(-t / .08);
                double env = Math.Min(1, t / .002) * Math.Exp(-t / decay) * Math.Min(1, (seconds - t) / .02);
                m.Put(at + i, v * env * gain, gl, gr);
            }
        }

        /// <summary>Bright acoustic-like piano: stacked decaying partials with a hammer click.</summary>
        static void SPiano(Mix m, Ctx c, int at, double midi, double gain, double seconds, double pan)
        {
            double gl, gr; PanGains(pan, out gl, out gr);
            double f = Mtof(midi);
            int n = (int)(seconds * c.Rate);
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / c.Rate, v = 0;
                for (int h = 1; h <= 6; h++) v += Math.Sin(2 * Math.PI * f * h * (1 + .0006 * h * h) * t) * Math.Exp(-t * (1.5 + h * 1.6)) / h;
                v += Noise(ref c.Rng) * Math.Exp(-t / .004) * .15;
                m.Put(at + i, v * gain * Math.Min(1, t / .001) * Math.Min(1, (seconds - t) / .02), gl, gr);
            }
        }

        static void SFlute(Mix m, Ctx c, int at, double midi, double gain, double seconds, double pan)
        {
            double gl, gr; PanGains(pan, out gl, out gr);
            double f = Mtof(midi); double lp = 0;
            int n = (int)((seconds + .05) * c.Rate);
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / c.Rate;
                double vib = 1 + .008 * Math.Sin(2 * Math.PI * 5 * t) * Math.Min(1, t / .3);
                lp += (Noise(ref c.Rng) - lp) * .1;
                double v = Math.Sin(2 * Math.PI * f * vib * t) + .2 * Math.Sin(4 * Math.PI * f * vib * t) + lp * .25;
                double env = Math.Min(1, t / .04) * (t < seconds ? 1 : Math.Max(0, 1 - (t - seconds) / .05));
                m.Put(at + i, v * env * gain, gl, gr);
            }
        }

        static void SGuitar(Mix m, Ctx c, int at, double root, double gain, double seconds, bool mute, double pan, bool single = false)
        {
            double gl, gr; PanGains(pan, out gl, out gr);
            int n = (int)(seconds * c.Rate);
            double lp = 0, lp2 = 0, cut = mute ? .08 : .2, f = Mtof(root);
            for (int i = 0; i < n; i++)
            {
                double t = (double)i / c.Rate;
                double x = Saw(f * 1.003, t) + Saw(f * .997, t);
                if (!single) x += .8 * Saw(f * 1.4983, t) + .6 * Saw(f * 2.002, t);
                double drive = Math.Tanh(6.0 * x);
                lp += (drive - lp) * cut; lp2 += (lp - lp2) * cut;
                double env = Math.Min(1, t / .002) * (mute ? Math.Exp(-t / .07) : Math.Min(1, (seconds - t) / .01));
                m.Put(at + i, lp2 * env * gain, gl, gr);
            }
        }

        static void SChord(Mix m, Ctx c, int at, int[] chord, double gain, double seconds, double cutFrom, double cutTo, double attack, double spread)
        {
            for (int k = 0; k < chord.Length; k++) SSynth(m, c, at, chord[k], gain, seconds, (k - (chord.Length - 1) * .5) * spread, cutFrom, cutTo, attack, .008, false, 0, .15);
        }

        // ---------------------------------------------------------------- effects

        /// <summary>Loop-aware Schroeder reverb (combs + allpasses per side): the tail wraps into the loop start.</summary>
        static void Reverb(Mix src, Mix dst, double wet, double size, int rate)
        {
            wet *= .82; // round 16: drier, so the tune keeps a clear outline
            int n = src.N;
            int[] combL = { 1116, 1188, 1277, 1356 }, combR = { 1139, 1211, 1300, 1379 };
            int[] apL = { 556, 441 }, apR = { 579, 464 };
            double scale = rate / 44100.0;
            foreach (bool left in new[] { true, false })
            {
                float[] input = left ? src.L : src.R, output = left ? dst.L : dst.R;
                int[] combs = left ? combL : combR, aps = left ? apL : apR;
                var acc = new double[n];
                foreach (int baseLen in combs)
                {
                    int len = Math.Max(1, (int)(baseLen * scale * size));
                    var buf = new double[len]; int idx = 0; double damp = 0;
                    for (int pass = 0; pass < 2; pass++)
                        for (int i = 0; i < n; i++)
                        {
                            double y = buf[idx];
                            damp = y * .6 + damp * .4;
                            buf[idx] = input[i] + damp * .82;
                            idx = (idx + 1) % len;
                            if (pass == 1) acc[i] += y;
                        }
                }
                foreach (int baseLen in aps)
                {
                    int len = Math.Max(1, (int)(baseLen * scale));
                    var buf = new double[len]; int idx = 0;
                    var outp = new double[n];
                    for (int pass = 0; pass < 2; pass++)
                        for (int i = 0; i < n; i++)
                        {
                            double b = buf[idx], x = acc[i];
                            double y = -x + b;
                            buf[idx] = x + b * .5;
                            idx = (idx + 1) % len;
                            if (pass == 1) outp[i] = y;
                        }
                    acc = outp;
                }
                for (int i = 0; i < n; i++) output[i] += (float)(acc[i] * wet * .12);
            }
        }

        static void PingPong(Mix m, Ctx c, double steps16, double feedback, double wet)
        {
            int n = m.N, lag = Math.Max(1, c.At(steps16));
            var dl = (float[])m.L.Clone(); var dr = (float[])m.R.Clone();
            var el = new float[n]; var er = new float[n];
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < n; i++)
                {
                    int j = ((i - lag) % n + n) % n;
                    el[i] = (float)(dr[j] * .6 + er[j] * feedback);
                    er[i] = (float)(dl[j] * .6 + el[j] * feedback);
                }
            for (int i = 0; i < n; i++) { m.L[i] += el[i] * (float)wet; m.R[i] += er[i] * (float)wet; }
        }

        /// <summary>Stereo widener: each side takes a little of the other side delayed by a different 11-17 ms (loop-aware).</summary>
        static void Widen(Mix m, Ctx c, double amount)
        {
            int n = m.N, dl = (int)(.011 * c.Rate), dr = (int)(.017 * c.Rate);
            var l = (float[])m.L.Clone(); var r = (float[])m.R.Clone();
            float keep = (float)(1 - amount * .5), cross = (float)amount;
            for (int i = 0; i < n; i++)
            {
                m.L[i] = l[i] * keep - r[((i - dr) % n + n) % n] * cross;
                m.R[i] = r[i] * keep - l[((i - dl) % n + n) % n] * cross;
            }
        }

        static void SideChain(Mix m, Ctx c, double depth)
        {
            int beat = Math.Max(1, (int)(c.Spb * c.Rate));
            // the duck eases in over 5 ms: an instant drop clicked on every beat (and on the loop seam)
            for (int i = 0; i < m.N; i++) { double t = (double)(i % beat) / c.Rate; float g = (float)(1 - depth * Math.Min(1, t / .005) * Math.Exp(-t / .1)); m.L[i] *= g; m.R[i] *= g; }
        }

        // ---------------------------------------------------------------- track rendering

        public static Dictionary<Stem, float[]> BuildTrack(MusicTrack track, int rate, double bpm)
        {
            // round 34: a recorded loop for this piece replaces the synthesis (one layer; the others stay silent)
            RecordedMusic.Loop recorded = RecordedMusic.Find(track, bpm);
            if (recorded != null)
            {
                float[] music = RecordedMusic.Stretch(recorded, rate, bpm);
                var only = new Dictionary<Stem, float[]>();
                for (int s = 0; s < 5; s++) only[(Stem)s] = s == (int)Stem.Base ? music : new float[music.Length];
                return only;
            }
            var c = new Ctx { Rate = rate, Spb = 60.0 / bpm, Rng = new Rng(500 + (int)track * 31) };
            c.N = (int)Math.Round(TrackBeats * c.Spb * rate);
            var stems = new Mix[5];
            for (int i = 0; i < 5; i++) stems[i] = new Mix(c.N);
            Compose(track, stems, c);
            // melodic layers spread wide (cross-fed short delays), the low end stays in the middle
            Widen(stems[(int)Stem.Lead], c, .5); Widen(stems[(int)Stem.Fever], c, .55); // round 16: wider, since the reverb is drier
            return Normalise(stems, c);
        }

        static void Compose(MusicTrack track, Mix[] stems, Ctx c)
        {
            if (SourceOf(track) != track)
            {
                // a remix: the stage's tune and harmony, a heavy boss beat underneath, the bass driven harder
                Compose(SourceOf(track), stems, c);
                Grit(stems[2], 1.8);
                BossBeat(stems[1], c, (int)SourceOf(track));
                return;
            }
            switch (track)
            {
                case MusicTrack.Kitchen: Kitchen(stems, c); break;
                case MusicTrack.ToyRoom: ToyRoom(stems, c); break;
                case MusicTrack.SkyOrchard: SkyOrchard(stems, c); break;
                case MusicTrack.NeonMarket: NeonMarket(stems, c); break;
                case MusicTrack.CandyLand: CandyLand(stems, c); break;
                case MusicTrack.Freezer: Freezer(stems, c); break;
                case MusicTrack.Fever: Fever(stems, c); break;
                case MusicTrack.BossOnion: BossMetal(stems, c); break;
                case MusicTrack.BossToaster: BossBattle(stems, c); break;
                case MusicTrack.BossIcecream: BossFrost(stems, c); break;
                case MusicTrack.BossDonut: BossGothic(stems, c); break;
                case MusicTrack.BossZenmai: BossClockwork(stems, c); break;
                case MusicTrack.BossKaminari: BossStorm(stems, c); break;
                case MusicTrack.BossYukidaruma: BossBlizzard(stems, c); break;
                default: Home(stems, c); break;
            }
        }

        static Dictionary<Stem, float[]> Normalise(Mix[] stems, Ctx c)
        {
            // one shared gain from the summed peak so the layer balance stays as written
            float peak = 0;
            for (int i = 0; i < c.N; i++)
            {
                float l = 0, r = 0;
                foreach (Mix s in stems) { l += s.L[i]; r += s.R[i]; }
                peak = Math.Max(peak, Math.Max(Math.Abs(l), Math.Abs(r)));
            }
            float k = peak > 0 ? .9f / peak : 1f;
            var result = new Dictionary<Stem, float[]>();
            for (int s = 0; s < 5; s++)
            {
                var inter = new float[c.N * 2];
                for (int i = 0; i < c.N; i++) { inter[2 * i] = stems[s].L[i] * k; inter[2 * i + 1] = stems[s].R[i] * k; }
                result[(Stem)s] = inter;
            }
            return result;
        }

        /// <summary>A whole track mixed to one stereo loop (menu music).</summary>
        public static float[] BuildTrackMix(MusicTrack track, int rate, double bpm)
        {
            Dictionary<Stem, float[]> stems = BuildTrack(track, rate, bpm);
            var mix = new float[stems[Stem.Base].Length];
            foreach (float[] s in stems.Values) for (int i = 0; i < mix.Length; i++) mix[i] += s[i];
            float peak = 0; foreach (float v in mix) peak = Math.Max(peak, Math.Abs(v));
            if (peak > .9f) for (int i = 0; i < mix.Length; i++) mix[i] *= .9f / peak;
            return mix;
        }

        static void WithVerb(Mix target, Ctx c, double wet, double size, Action<Mix> write)
        {
            var dry = new Mix(c.N);
            write(dry);
            for (int i = 0; i < c.N; i++) { target.L[i] += dry.L[i]; target.R[i] += dry.R[i]; }
            Reverb(dry, target, wet, size, c.Rate);
        }

        static void Melody(Mix m, Ctx c, int[,] notes, Action<int, int, double> play)
        {
            for (int i = 0; i < notes.GetLength(0); i++) play(c.At(notes[i, 0]), notes[i, 1], c.Sec(notes[i, 2]));
        }

        // ------------------------------------------------ 1. Kitchen: bright funk-pop (F major)
        static void Kitchen(Mix[] s, Ctx c)
        {
            int[][] chords = { new[] { 65, 69, 72 }, new[] { 62, 65, 69 }, new[] { 58, 62, 65 }, new[] { 60, 64, 67 } };
            int[] roots = { 41, 38, 34, 36 };
            for (int bar = 0; bar < 4; bar++)
            {
                int b0 = bar * 16;
                for (int beat = 0; beat < 4; beat++) { SKick(s[0], c, c.At(b0 + beat * 4), .85); SSub(s[0], c, c.At(b0 + beat * 4), roots[bar], .22, c.Sec(3.6)); }
                for (int st = 0; st < 16; st++)
                {
                    double swing = st % 2 == 1 ? .25 : 0;
                    SHat(s[1], c, c.At(b0 + st + swing), st % 4 == 2 ? .16 : .08, st % 4 == 2 ? .03 : .012, st % 2 == 0 ? -.55 : .55);
                }
                SClap(s[1], c, c.At(b0 + 4), .55); SClap(s[1], c, c.At(b0 + 12), .55);
                SSnare(s[1], c, c.At(b0 + 7), .12, -.2); SSnare(s[1], c, c.At(b0 + 15), .12, -.2);
                int[] bassSteps = { 0, 3, 6, 8, 10, 11, 14 }; int[] bassOff = { 0, 12, 0, 7, 12, 0, 10 };
                for (int k = 0; k < bassSteps.Length; k++) SPluck(s[2], c, c.At(b0 + bassSteps[k]), roots[bar] + 12 + bassOff[k], .3, c.Sec(1.6), 0, 2200, .12);
                WithVerb(s[3], c, .35, 1, m =>
                {
                    foreach (int st in new[] { 6, 14 }) for (int k = 0; k < 3; k++) SKeys(m, c, c.At(b0 + st), chords[bar][k], .07, c.Sec(2.5), -.7 + .7 * k);
                    if (bar % 2 == 1) SChord(m, c, c.At(b0 + 14), chords[bar], .05, c.Sec(1.5), 4000, 1800, .01, .5);
                });
            }
            WithVerb(s[4], c, .4, 1.1, m => Melody(m, c, new[,] { { 0, 77, 2 }, { 2, 74, 2 }, { 4, 72, 4 }, { 10, 74, 2 }, { 12, 77, 4 }, { 16, 79, 2 }, { 18, 77, 2 }, { 20, 74, 4 }, { 26, 72, 2 }, { 28, 70, 4 },
                { 32, 70, 2 }, { 34, 72, 2 }, { 36, 74, 4 }, { 42, 77, 2 }, { 44, 74, 4 }, { 48, 72, 2 }, { 50, 74, 2 }, { 52, 77, 6 }, { 60, 79, 4 } },
                (at, note, sec) => SSynth(m, c, at, note, .07, sec, .2, 5000, 2500, .005, .004, true, .006)));
            PingPong(s[4], c, 3, .3, .4);
            SideChain(s[3], c, .3);
        }

        // ------------------------------------------------ 2. Toy room: chiptune (C major)
        static void ToyRoom(Mix[] s, Ctx c)
        {
            int[][] chords = { new[] { 60, 64, 67 }, new[] { 59, 62, 67 }, new[] { 57, 60, 64 }, new[] { 57, 60, 65 } };
            int[] roots = { 48, 43, 45, 41 };
            for (int bar = 0; bar < 4; bar++)
            {
                int b0 = bar * 16;
                for (int e = 0; e < 8; e++)
                {
                    SSynth(s[0], c, c.At(b0 + e * 2), roots[bar] - 12 + (e % 2 == 1 ? 12 : 0), .18, c.Sec(1.7), 0, 900, 900, .002, 0, true);
                    if (e % 2 == 0) SKick(s[0], c, c.At(b0 + e * 2), .5, 1.6);
                }
                SSnare(s[1], c, c.At(b0 + 4), .35, 0, 260); SSnare(s[1], c, c.At(b0 + 12), .35, 0, 260);
                for (int e = 0; e < 8; e++) SHat(s[1], c, c.At(b0 + e * 2 + 1), .07, .01, e % 2 == 0 ? -.6 : .6);
                for (int st = 0; st < 16; st++) SSynth(s[2], c, c.At(b0 + st), chords[bar][st % 3] + 12, .045, c.Sec(.9), -.45, 6000, 6000, .001, 0, true);
            }
            WithVerb(s[3], c, .25, .7, m => Melody(m, c, new[,] { { 0, 72, 2 }, { 2, 76, 2 }, { 4, 79, 2 }, { 6, 76, 2 }, { 8, 84, 4 }, { 12, 79, 4 }, { 16, 79, 2 }, { 18, 83, 2 }, { 20, 86, 4 }, { 24, 83, 2 }, { 26, 79, 2 }, { 28, 74, 4 },
                { 32, 76, 2 }, { 34, 72, 2 }, { 36, 69, 4 }, { 40, 72, 2 }, { 42, 76, 2 }, { 44, 81, 4 }, { 48, 77, 2 }, { 50, 81, 2 }, { 52, 84, 4 }, { 56, 81, 2 }, { 58, 77, 2 }, { 60, 79, 4 } },
                (at, note, sec) => SSynth(m, c, at, note, .08, sec * .9, 0, 7000, 7000, .002, .002, true, .004)));
            PingPong(s[3], c, 3, .3, .35);
            for (int st = 0; st < 64; st += 2) SSynth(s[4], c, c.At(st), new[] { 84, 88, 91, 96 }[(st / 2) % 4] - (st >= 32 ? 3 : 0), .03, c.Sec(.8), .7, 8000, 8000, .001, 0, true);
            PingPong(s[4], c, 3, .35, .5);
            PingPong(s[2], c, 1.5, .2, .3);
        }

        // ------------------------------------------------ 3. Sky orchard: tropical house (G major)
        static void SkyOrchard(Mix[] s, Ctx c)
        {
            int[][] chords = { new[] { 55, 59, 62 }, new[] { 52, 55, 59 }, new[] { 52, 55, 60 }, new[] { 54, 57, 62 } };
            int[] roots = { 43, 40, 36, 38 };
            for (int bar = 0; bar < 4; bar++)
            {
                int b0 = bar * 16;
                SKick(s[0], c, c.At(b0), .8); SKick(s[0], c, c.At(b0 + 8), .8);
                WithVerb(s[0], c, .5, 1.3, m => SChord(m, c, c.At(b0), chords[bar], .022, c.Sec(15.5), 1400, 1400, .3, .8));
                for (int st = 0; st < 16; st++) SHat(s[1], c, c.At(b0 + st), st % 2 == 0 ? .05 : .09, .02, .6);
                SClap(s[1], c, c.At(b0 + 4), .45, -.1); SClap(s[1], c, c.At(b0 + 12), .45, -.1);
                foreach (int st in new[] { 2, 6, 10, 14 }) SSnare(s[1], c, c.At(b0 + st), .08, -.5, 800);
                foreach (int st in new[] { 0, 3, 6, 10 }) SPluck(s[2], c, c.At(b0 + st), roots[bar], .34, c.Sec(2.5), 0, 900, .25);
                WithVerb(s[3], c, .45, 1.2, m => { foreach (int st in new[] { 2, 6, 10, 14 }) for (int k = 0; k < 3; k++) SKeys(m, c, c.At(b0 + st), chords[bar][k] + 12, .05, c.Sec(1.5), -.5 + .5 * k, 3, .8, .12); });
            }
            SideChain(s[0], c, .45);
            WithVerb(s[4], c, .5, 1.4, m => Melody(m, c, new[,] { { 0, 79, 6 }, { 6, 81, 2 }, { 8, 83, 4 }, { 12, 81, 4 }, { 16, 79, 6 }, { 22, 76, 2 }, { 24, 74, 8 }, { 32, 76, 6 }, { 38, 79, 2 }, { 40, 83, 4 }, { 44, 86, 4 }, { 48, 84, 6 }, { 54, 83, 2 }, { 56, 81, 8 } },
                (at, note, sec) => SFlute(m, c, at, note, .1, sec * .95, .15)));
        }

        // ------------------------------------------------ 4. Neon market: synthwave (A minor)
        static void NeonMarket(Mix[] s, Ctx c)
        {
            int[][] chords = { new[] { 57, 60, 64 }, new[] { 57, 60, 65 }, new[] { 55, 60, 64 }, new[] { 55, 59, 62 } };
            int[] roots = { 33, 29, 36, 31 };
            for (int bar = 0; bar < 4; bar++)
            {
                int b0 = bar * 16;
                for (int beat = 0; beat < 4; beat++) SKick(s[0], c, c.At(b0 + beat * 4), .9);
                WithVerb(s[0], c, .4, 1.3, m => SChord(m, c, c.At(b0), chords[bar], .03, c.Sec(15.8), 2200, 900, .15, .9));
                WithVerb(s[1], c, .7, 1.6, m => { SSnare(m, c, c.At(b0 + 4), .6, 0, 180); SSnare(m, c, c.At(b0 + 12), .6, 0, 180); });
                for (int e = 0; e < 8; e++) SHat(s[1], c, c.At(b0 + e * 2), .08, .015, .4);
                if (bar == 3) for (int t = 0; t < 4; t++) STom(s[1], c, c.At(b0 + 12 + t), 180 - t * 25, .35, -.6 + .4 * t);
                for (int e = 0; e < 8; e++) SSynth(s[2], c, c.At(b0 + e * 2), roots[bar] + (e % 2 == 1 ? 12 : 0), .2, c.Sec(1.8), 0, 1600, 500, .002, .004);
                for (int st = 0; st < 16; st++) { int[] ch = chords[bar]; int note = st % 6 < 3 ? ch[st % 3] : ch[2 - st % 3]; SSynth(s[3], c, c.At(b0 + st), note + 12, .04, c.Sec(.8), (st % 2 == 0 ? -.4 : .4), 5000, 1500, .002, .005); }
            }
            SideChain(s[0], c, .35); SideChain(s[2], c, .5);
            PingPong(s[3], c, 3, .35, .4);
            WithVerb(s[4], c, .5, 1.3, m => Melody(m, c, new[,] { { 0, 76, 6 }, { 6, 74, 2 }, { 8, 72, 4 }, { 12, 71, 4 }, { 16, 72, 8 }, { 24, 69, 8 }, { 32, 67, 6 }, { 38, 69, 2 }, { 40, 72, 4 }, { 44, 76, 4 }, { 48, 74, 12 }, { 60, 71, 4 } },
                (at, note, sec) => SSynth(m, c, at, note, .07, sec * .95, .1, 3500, 2400, .01, .007, false, .007, .08)));
        }

        // ------------------------------------------------ 5. Candy land: electro swing (D minor)
        static void CandyLand(Mix[] s, Ctx c)
        {
            int[][] chords = { new[] { 62, 65, 69 }, new[] { 62, 67, 70 }, new[] { 61, 64, 67 }, new[] { 62, 65, 69 } };
            int[][] walk = { new[] { 38, 41, 45, 43 }, new[] { 43, 46, 50, 48 }, new[] { 45, 49, 52, 44 }, new[] { 38, 45, 41, 37 } };
            const double sw = 2.0 / 3.0; // swung offbeat at 2/3 of the beat
            for (int bar = 0; bar < 4; bar++)
            {
                int b0 = bar * 16;
                SKick(s[0], c, c.At(b0), .8); SKick(s[0], c, c.At(b0 + 8), .8); SKick(s[0], c, c.At(b0 + 10 + sw * 4 - 2), .5);
                for (int beat = 0; beat < 4; beat++)
                {
                    SPiano(s[0], c, c.At(b0 + beat * 4), walk[bar][0] + 12, .1, c.Sec(2), -.15);
                    foreach (int note in chords[bar]) SPiano(s[0], c, c.At(b0 + beat * 4 + 2), note, .05, c.Sec(1.5), (note % 3 - 1) * .3);
                }
                for (int beat = 0; beat < 4; beat++) { SHat(s[1], c, c.At(b0 + beat * 4), .1, .03, .3); SHat(s[1], c, c.At(b0 + beat * 4 + sw * 4), .07, .015, .3); }
                SClap(s[1], c, c.At(b0 + 4), .5); SClap(s[1], c, c.At(b0 + 12), .5);
                for (int beat = 0; beat < 4; beat++) SPluck(s[2], c, c.At(b0 + beat * 4), walk[bar][beat], .3, c.Sec(3.6), 0, 700, .4);
                WithVerb(s[3], c, .35, 1, m => { foreach (double st in new[] { sw * 4, 8 + sw * 4 }) SChord(m, c, c.At(b0 + st), chords[bar], .045, c.Sec(1.2), 5000, 1800, .008, .7); });
            }
            WithVerb(s[4], c, .4, 1.1, m => Melody(m, c, new[,] { { 0, 74, 3 }, { 3, 77, 3 }, { 6, 81, 2 }, { 8, 79, 4 }, { 12, 77, 4 }, { 16, 79, 3 }, { 19, 76, 3 }, { 22, 73, 2 }, { 24, 76, 8 }, { 32, 74, 3 }, { 35, 77, 3 }, { 38, 81, 2 }, { 40, 84, 4 }, { 44, 81, 4 }, { 48, 79, 3 }, { 51, 76, 3 }, { 54, 73, 2 }, { 56, 74, 8 } },
                (at, note, sec) => SSynth(m, c, at, note, .08, sec * .9, .25, 1800, 1300, .02, .002, true, .01, .06)));
        }

        // ------------------------------------------------ 6. Freezer: trance (E minor)
        static void Freezer(Mix[] s, Ctx c)
        {
            int[][] chords = { new[] { 64, 67, 71 }, new[] { 64, 67, 72 }, new[] { 62, 67, 71 }, new[] { 62, 66, 69 } };
            int[] roots = { 40, 36, 43, 38 };
            int[] gate = { 1, 0, 1, 1, 0, 1, 1, 0, 1, 0, 1, 1, 0, 1, 1, 1 };
            for (int bar = 0; bar < 4; bar++)
            {
                int b0 = bar * 16;
                for (int beat = 0; beat < 4; beat++) SKick(s[0], c, c.At(b0 + beat * 4), .95);
                WithVerb(s[0], c, .5, 1.5, m => SChord(m, c, c.At(b0), chords[bar], .02, c.Sec(15.8), 1800, 1800, .4, 1));
                for (int beat = 0; beat < 4; beat++) { SHat(s[1], c, c.At(b0 + beat * 4 + 2), .14, .06, .2); SHat(s[1], c, c.At(b0 + beat * 4 + 1), .05, .012, -.3); SHat(s[1], c, c.At(b0 + beat * 4 + 3), .05, .012, -.3); }
                SClap(s[1], c, c.At(b0 + 4), .5); SClap(s[1], c, c.At(b0 + 12), .5);
                if (bar == 0) SCrash(s[1], c, c.At(b0), .25);
                for (int st = 0; st < 16; st++) if (st % 4 != 0) SSynth(s[2], c, c.At(b0 + st), roots[bar], .2, c.Sec(.85), 0, 1400, 400, .002, .003);
                WithVerb(s[3], c, .45, 1.3, m => { for (int st = 0; st < 16; st++) if (gate[st] == 1) SChord(m, c, c.At(b0 + st), chords[bar], .03, c.Sec(.8), 6000, 3000, .003, .9); });
            }
            WithVerb(s[4], c, .5, 1.5, m => { for (int st = 0; st < 64; st++) { int[] ch = chords[st / 16]; if (st % 2 == 0) SKeys(m, c, c.At(st), ch[(st / 2) % 3] + 24, .035, c.Sec(3), (st % 4 == 0 ? -.5 : .5), 3.5, 1.2, .4); } });
            PingPong(s[4], c, 3, .4, .45);
            SideChain(s[0], c, .5); SideChain(s[2], c, .6); SideChain(s[3], c, .45);
        }

        // ------------------------------------------------ 7. FEVER: eurobeat (B minor)
        static void Fever(Mix[] s, Ctx c)
        {
            int[][] chords = { new[] { 66, 71, 74 }, new[] { 67, 71, 74 }, new[] { 64, 69, 73 }, new[] { 66, 71, 74 } };
            int[] roots = { 35, 31, 33, 35 };
            for (int bar = 0; bar < 4; bar++)
            {
                int b0 = bar * 16;
                for (int beat = 0; beat < 4; beat++) SKick(s[0], c, c.At(b0 + beat * 4), 1);
                for (int e = 0; e < 8; e++) SSynth(s[0], c, c.At(b0 + e * 2), roots[bar] + (e % 2 == 1 ? 12 : 0), .2, c.Sec(1.7), 0, 2000, 700, .002, .004);
                SClap(s[1], c, c.At(b0 + 4), .6); SClap(s[1], c, c.At(b0 + 12), .6);
                for (int st = 0; st < 16; st++) SHat(s[1], c, c.At(b0 + st), st % 4 == 2 ? .15 : .06, st % 4 == 2 ? .05 : .012, .3);
                SCrash(s[1], c, c.At(b0), bar == 0 ? .3 : .12);
                WithVerb(s[2], c, .35, 1.1, m => { foreach (int st in new[] { 0, 3, 6, 8, 11, 14 }) SChord(m, c, c.At(b0 + st), chords[bar], .04, c.Sec(1.4), 7000, 2500, .003, .8); });
                for (int st = 0; st < 16; st++) SSynth(s[4], c, c.At(b0 + st), chords[bar][st % 3] + 12 + (st % 6 >= 3 ? 12 : 0), .025, c.Sec(.7), (st % 2 == 0 ? -.5 : .5), 8000, 4000, .001, .003, true);
            }
            WithVerb(s[3], c, .4, 1.2, m => Melody(m, c, new[,] { { 0, 78, 2 }, { 2, 78, 2 }, { 4, 81, 2 }, { 6, 78, 2 }, { 8, 83, 4 }, { 12, 81, 2 }, { 14, 78, 2 }, { 16, 79, 2 }, { 18, 79, 2 }, { 20, 83, 2 }, { 22, 79, 2 }, { 24, 86, 4 }, { 28, 83, 4 },
                { 32, 81, 2 }, { 34, 81, 2 }, { 36, 85, 2 }, { 38, 81, 2 }, { 40, 88, 4 }, { 44, 85, 4 }, { 48, 83, 4 }, { 52, 81, 4 }, { 56, 78, 8 } },
                (at, note, sec) => SSynth(m, c, at, note, .06, sec * .92, 0, 8000, 4000, .003, .012, false, .004)));
            SideChain(s[0], c, .25); SideChain(s[2], c, .4);
        }

        // ------------------------------------------------ 8. Boss (onion king): death-metal style (E minor)
        static readonly double[] RiffA = { 40, 0, 0, 0, 0, 0, 43, 0, 40, 0, 0, 0, 0, 0, 42, 0, 40, 0, 0, 0, 0, 0, 46, 0, 40, 0, 0, 0, 43, 0, 42, 0 };
        static readonly double[] RiffB = { 40, 0, 41, 0, 40, 0, 41, 0, 43, 0, 0, 0, 45, 0, 46, 0, 40, 0, 41, 0, 40, 0, 46, 0, 45, 0, 43, 0, 42, 0, 41, 0 };

        static void BossMetal(Mix[] s, Ctx c)
        {
            double[] melody = { 64, 67, 66, 64, 71, 69, 67, 66, 64, 67, 70, 69, 71, 72, 71, 70 };
            for (int st = 0; st < 64; st++)
            {
                int at = c.At(st);
                double[] riff = st < 32 ? RiffA : RiffB;
                double root = riff[st % 32];
                // double-tracked rhythm guitars, hard left and right
                if (root > 0) { SGuitar(s[0], c, at, root, .16, c.Sec(1.9), false, -.85); SGuitar(s[0], c, at + 23, root, .16, c.Sec(1.9), false, .85); }
                else { SGuitar(s[0], c, at, 40, st % 4 == 0 ? .16 : .12, c.Sec(.8), true, -.85); SGuitar(s[0], c, at + 23, 40, st % 4 == 0 ? .16 : .12, c.Sec(.8), true, .85); }
                SKick(s[1], c, at, st % 2 == 0 ? .6 : .48);
                bool blast = (st / 4) % 8 >= 4;
                if (blast ? st % 2 == 1 : st % 8 == 4) SSnare(s[1], c, at, blast ? .45 : .75, 0, 200);
                if (st % 16 == 0) SCrash(s[1], c, at, .3);
                else SHat(s[1], c, at, st % 2 == 0 ? .07 : .045, .012, .4);
                SSub(s[2], c, at, (root > 0 ? root : 40) - 12, .22, c.Sec(.9));
                SGuitar(s[3], c, at, melody[st / 4] + (st >= 32 ? 0 : 0), .06, c.Sec(.9), false, .2, true);
            }
            var lead = new Mix(c.N);
            for (int i = 0; i < c.N; i++) { lead.L[i] = s[3].L[i]; lead.R[i] = s[3].R[i]; }
            Reverb(lead, s[3], .3, 1.1, c.Rate);
            Reverb(s[1], s[1], .15, .9, c.Rate);
            // rage layer: pinch squeals + growling drone
            for (int bar = 0; bar < 4; bar++) SSynth(s[4], c, c.At(bar * 16 + 14), 86 + (bar % 2) * 3, .05, c.Sec(6), bar % 2 == 0 ? -.5 : .5, 4000, 2500, .005, .01, false, .02, .1);
            for (int st = 0; st < 64; st += 8) SSynth(s[4], c, c.At(st), 28, .12, c.Sec(7.8), 0, 300, 300, .05, .02, false, .03, .05);
        }

        /// <summary>
        /// Round 27 (owner: "最初のボスの曲が一番いい…低いベースがメインの感じ"): the onion king's foundation for every boss —
        /// double-tracked palm-muted low guitars chugging 16ths with accented riff notes, a double kick on every 16th, a
        /// snare that turns into blast beats, and a sub bass doubling the riff an octave down. <paramref name="riff"/> gives
        /// the accent notes as semitones above the bar's root on the 32-step (two-bar) grid; the rest are muted chugs on the
        /// root. Each boss keeps its own melody and its signature sound on top.
        /// </summary>
        static void BossLowEnd(Mix[] s, Ctx c, int[] roots, Dictionary<int, int> riff, double kickLift = 0)
        {
            for (int st = 0; st < 64; st++)
            {
                int at = c.At(st), root = roots[st / 16], step = st % 32;
                int accent;
                bool hit = riff.TryGetValue(step, out accent);
                double note = root + (hit ? accent : 0);
                if (hit) { SGuitar(s[0], c, at, note, .16, c.Sec(1.9), false, -.85); SGuitar(s[0], c, at + 23, note, .16, c.Sec(1.9), false, .85); }
                else { SGuitar(s[0], c, at, root, st % 4 == 0 ? .16 : .12, c.Sec(.8), true, -.85); SGuitar(s[0], c, at + 23, root, st % 4 == 0 ? .16 : .12, c.Sec(.8), true, .85); }
                SKick(s[1], c, at, (st % 2 == 0 ? .6 : .48) + kickLift);
                bool blast = (st / 4) % 8 >= 4;
                if (blast ? st % 2 == 1 : st % 8 == 4) SSnare(s[1], c, at, blast ? .45 : .75, 0, 200);
                if (st % 16 == 0) SCrash(s[1], c, at, .3);
                else SHat(s[1], c, at, st % 2 == 0 ? .07 : .045, .012, .4);
                SSub(s[2], c, at, note - 12, .22, c.Sec(.9));
            }
            Reverb(s[1], s[1], .15, .9, c.Rate);
        }

        /// <summary>Adds a boss's signature layer under the chugging guitars, quieter so the low end leads.</summary>
        static void Under(Mix target, Ctx c, double gain, Action<Mix> write)
        {
            var layer = new Mix(c.N);
            write(layer);
            for (int i = 0; i < c.N; i++) { target.L[i] += (float)(layer.L[i] * gain); target.R[i] += (float)(layer.R[i] * gain); }
        }

        // ------------------------------------------------ 9. Boss (toaster): orchestral-style battle (D minor)
        static void BossBattle(Mix[] s, Ctx c)
        {
            int[][] chords = { new[] { 62, 65, 69 }, new[] { 62, 65, 70 }, new[] { 60, 64, 67 }, new[] { 61, 64, 69 } };
            int[] roots = { 38, 34, 36, 33 };
            // round 27: the onion king's low end in D minor (riff: minor third, fourth, fifth pick-ups)
            BossLowEnd(s, c, roots, new Dictionary<int, int> { { 0, 0 }, { 6, 3 }, { 8, 0 }, { 12, 5 }, { 16, 0 }, { 22, 7 }, { 24, 0 }, { 28, 5 }, { 30, 3 } });
            for (int bar = 0; bar < 4; bar++)
            {
                int b0 = bar * 16;
                // signature: timpani on the beats + low strings ostinato in 8ths, under the guitars
                Under(s[0], c, .55, m =>
                {
                    for (int beat = 0; beat < 4; beat++) STom(m, c, c.At(b0 + beat * 4), Mtof(roots[bar] + 12) * .5, .45, 0);
                    for (int e = 0; e < 8; e++) SSynth(m, c, c.At(b0 + e * 2), roots[bar] + 12 + (e % 4 == 3 ? 7 : 0), .07, c.Sec(1.6), -.3, 1800, 900, .004, .006);
                });
                if (bar == 3) for (int r = 0; r < 8; r++) SSnare(s[1], c, c.At(b0 + 8 + r), .1 + .04 * r, .2);
                // lead: a fast piano ostinato (16ths, octave leaps) – original pattern
                int[] ch = chords[bar];
                int[] ost = { ch[0], ch[2], ch[0] + 12, ch[2], ch[1], ch[2], ch[0] + 12, ch[1] + 12 };
                for (int st = 0; st < 16; st++) SPiano(s[3], c, c.At(b0 + st), ost[st % 8] + 12, .085, c.Sec(1.2), -.35);
            }
            Reverb(s[3], s[3], .25, 1.2, c.Rate);
            // extra: brass melody + string tremolo
            WithVerb(s[4], c, .45, 1.4, m =>
            {
                Melody(m, c, new[,] { { 0, 74, 4 }, { 4, 77, 4 }, { 8, 81, 6 }, { 14, 79, 2 }, { 16, 77, 6 }, { 22, 74, 2 }, { 24, 70, 8 }, { 32, 72, 4 }, { 36, 76, 4 }, { 40, 79, 6 }, { 46, 77, 2 }, { 48, 76, 8 }, { 56, 73, 8 } },
                    (at, note, sec) => { SSynth(m, c, at, note - 12, .06, sec * .95, .35, 2600, 1400, .03, .005, false, .004, .08); SSynth(m, c, at, note, .035, sec * .95, .5, 3000, 1800, .03, .006, false, .004, .08); });
                for (int st = 0; st < 64; st++) SSynth(m, c, c.At(st), chords[st / 16][2] + 12, .012, c.Sec(.9), .6, 3000, 3000, .004, .01);
            });
        }

        // ------------------------------------------------ 11. Boss (soft-serve general): frozen drum & bass (F# minor)
        static void BossFrost(Mix[] s, Ctx c)
        {
            int[][] chords = { new[] { 66, 69, 73 }, new[] { 62, 66, 69 }, new[] { 64, 68, 71 }, new[] { 61, 64, 68 } };
            int[] roots = { 30, 26, 28, 25 };
            // round 27: the onion king's low end in F# minor, an octave up into guitar range (riff: fifth and minor-sixth stabs)
            BossLowEnd(s, c, new[] { roots[0] + 12, roots[1] + 12, roots[2] + 12, roots[3] + 12 }, new Dictionary<int, int> { { 0, 0 }, { 4, 7 }, { 8, 0 }, { 14, 8 }, { 16, 0 }, { 20, 7 }, { 24, 0 }, { 26, 3 }, { 30, 5 } });
            for (int bar = 0; bar < 4; bar++)
            {
                int b0 = bar * 16;
                // signature: a cold pad, and the wobble bass a little under the guitars
                Under(s[0], c, .6, m => WithVerb(m, c, .5, 1.5, w => SChord(w, c, c.At(b0), chords[bar], .025, c.Sec(15.8), 2600, 1200, .25, 1)));
                Under(s[2], c, .45, m => { for (int e = 0; e < 4; e++) SSynth(m, c, c.At(b0 + e * 4), roots[bar] + 12, .2, c.Sec(3.8), 0, e % 2 == 0 ? 600 : 1800, e % 2 == 0 ? 1800 : 500, .005, .01); });
                // lead: icy bells arpeggiated high, left and right
                WithVerb(s[3], c, .4, 1.3, m => { for (int st = 0; st < 16; st += 2) SKeys(m, c, c.At(b0 + st), chords[bar][(st / 2) % 3] + 24, .045, c.Sec(1.2), st % 4 == 0 ? -.6 : .6, 4, 1.5, .3); });
            }
            PingPong(s[3], c, 3, .35, .45);
            // extra: a cold sliding synth line
            WithVerb(s[4], c, .45, 1.3, m => Melody(m, c, new[,] { { 0, 78, 4 }, { 4, 81, 2 }, { 6, 80, 2 }, { 8, 78, 6 }, { 14, 76, 2 }, { 16, 74, 8 }, { 24, 73, 8 }, { 32, 76, 4 }, { 36, 78, 2 }, { 38, 80, 2 }, { 40, 81, 6 }, { 46, 83, 2 }, { 48, 80, 8 }, { 56, 78, 8 } },
                (at, note, sec) => SSynth(m, c, at, note, .06, sec * .95, -.2, 4500, 2000, .01, .008, false, .006, .07)));
        }

        // ------------------------------------------------ boss beat for the remixes (key-free: drums only)
        // a different heavy beat for each stage's remix (16 steps per bar: kick steps, extra snare steps, hat density)
        static readonly int[][] RemixKicks =
        {
            new[] { 0, 2, 3, 4, 6, 7, 8, 10, 11, 12, 14, 15 }, // galloping double kick
            new[] { 0, 4, 8, 12, 14, 15 },                     // four on the floor with a double at the end
            new[] { 0, 3, 6, 8, 11, 14 },                       // syncopated
            new[] { 0, 2, 4, 6, 8, 10, 12, 14 },                // driving eighths
            new[] { 0, 6, 8, 10 },                              // breakbeat
            new[] { 0, 1, 4, 8, 9, 12 },                        // stomping doubles
        };
        static readonly int[][] RemixSnares = { new[] { 4, 12 }, new[] { 4, 12, 15 }, new[] { 4, 10, 12 }, new[] { 4, 12 }, new[] { 4, 7, 12, 14 }, new[] { 4, 12, 13 } };

        static void BossBeat(Mix m, Ctx c, int variant)
        {
            var beat = new Mix(c.N);
            int[] kicks = RemixKicks[variant % RemixKicks.Length], snares = RemixSnares[variant % RemixSnares.Length];
            bool sixteenths = variant % 2 == 0;
            for (int st = 0; st < 64; st++)
            {
                int at = c.At(st), inBar = st % 16;
                if (Array.IndexOf(kicks, inBar) >= 0) SKick(beat, c, at, inBar % 4 == 0 ? .62 : .4);
                if (Array.IndexOf(snares, inBar) >= 0) SSnare(beat, c, at, inBar == 4 || inBar == 12 ? .8 : .35, 0, 200 + 10 * variant);
                if (inBar == 0 && (st == 0 || variant % 3 != 1)) SCrash(beat, c, at, .32);
                else if (sixteenths || st % 2 == 0) SHat(beat, c, at, st % 2 == 0 ? .1 : .06, .01, st % 4 < 2 ? -.7 : .7); // hats bounce left and right
                if (st >= 60) STom(beat, c, at, 160 - (st - 60) * 25 + 8 * variant, .45, -.5 + .33 * (st - 60));
            }
            Reverb(beat, beat, .12, .8, c.Rate);
            // the stage's own drums step back so the boss beat leads
            for (int i = 0; i < c.N; i++) { m.L[i] = m.L[i] * .45f + beat.L[i]; m.R[i] = m.R[i] * .45f + beat.R[i]; }
        }

        /// <summary>Soft overdrive: more harmonics, more presence (the remix bass).</summary>
        static void Grit(Mix m, double drive)
        {
            double k = Math.Tanh(drive);
            for (int i = 0; i < m.N; i++) { m.L[i] = (float)(Math.Tanh(drive * m.L[i] * 2) / k * .48); m.R[i] = (float)(Math.Tanh(drive * m.R[i] * 2) / k * .48); }
        }

        // ------------------------------------------------ 12. Boss (Count Donut): gothic harpsichord-and-organ battle (C minor)
        static void BossGothic(Mix[] s, Ctx c)
        {
            int[][] chords = { new[] { 60, 63, 67 }, new[] { 56, 60, 63 }, new[] { 55, 59, 62 }, new[] { 60, 63, 67 } };
            int[] roots = { 36, 32, 31, 36 };
            // round 27: the onion king's low end in C minor (riff: a creeping flat second, minor third, tritone)
            BossLowEnd(s, c, roots, new Dictionary<int, int> { { 0, 0 }, { 6, 1 }, { 8, 0 }, { 14, 3 }, { 16, 0 }, { 22, 6 }, { 24, 0 }, { 28, 1 }, { 30, 0 } });
            for (int bar = 0; bar < 4; bar++)
            {
                int b0 = bar * 16;
                // signature: a church organ holding the chord, under the guitars
                Under(s[0], c, .6, m => WithVerb(m, c, .45, 1.6, w => SChord(w, c, c.At(b0), chords[bar], .035, c.Sec(15.8), 2400, 2400, .05, 1)));
                // lead: a harpsichord running 16th arpeggios
                int[] ch = chords[bar];
                int[] arp = { ch[0] + 12, ch[1] + 12, ch[2] + 12, ch[0] + 24, ch[2] + 12, ch[1] + 12, ch[0] + 12, ch[2] };
                for (int st = 0; st < 16; st++) SPluck(s[3], c, c.At(b0 + st), arp[st % 8], .07, c.Sec(.9), st % 2 == 0 ? -.4 : .4, 6000, .12);
            }
            Reverb(s[3], s[3], .25, 1.3, c.Rate);
            // extra: a menacing organ melody
            WithVerb(s[4], c, .4, 1.4, m => Melody(m, c, new[,] { { 0, 72, 4 }, { 4, 75, 4 }, { 8, 74, 2 }, { 10, 72, 2 }, { 12, 71, 4 }, { 16, 68, 6 }, { 22, 70, 2 }, { 24, 71, 8 },
                { 32, 72, 4 }, { 36, 79, 4 }, { 40, 77, 2 }, { 42, 75, 2 }, { 44, 74, 4 }, { 48, 75, 4 }, { 52, 74, 4 }, { 56, 72, 8 } },
                (at, note, sec) => SSynth(m, c, at, note, .06, sec * .95, -.15, 3000, 2200, .01, .006, false, .005, .05)));
        }

        // ------------------------------------------------ 13. Boss (Wind-up King): clockwork metal (A minor)
        static void BossClockwork(Mix[] s, Ctx c)
        {
            int[][] chords = { new[] { 57, 60, 64 }, new[] { 53, 57, 60 }, new[] { 55, 59, 62 }, new[] { 52, 56, 59 } };
            int[] roots = { 45, 41, 43, 40 };
            // the onion king's low end; the riff ticks like a winding spring (repeated fifths and a chromatic climb)
            BossLowEnd(s, c, roots, new Dictionary<int, int> { { 0, 0 }, { 4, 7 }, { 8, 0 }, { 12, 7 }, { 16, 0 }, { 20, 1 }, { 22, 2 }, { 24, 3 }, { 28, 7 } });
            for (int bar = 0; bar < 4; bar++)
            {
                int b0 = bar * 16;
                // signature: a ratchet ticking 16ths (the key being wound) and a music box ringing the chord
                Under(s[1], c, .5, m => { for (int st = 0; st < 16; st++) STom(m, c, c.At(b0 + st), 1800 + 200 * (st % 2), .05, st % 2 == 0 ? -.5 : .5); });
                WithVerb(s[3], c, .35, 1.1, m => { for (int st = 0; st < 16; st += 2) SKeys(m, c, c.At(b0 + st), chords[bar][(st / 2) % 3] + 24, .04, c.Sec(1.4), st % 4 == 0 ? -.5 : .5, 3.5, 1.8, .35); });
            }
            // extra: a square-wave toy-trumpet march
            WithVerb(s[4], c, .35, 1.1, m => Melody(m, c, new[,] { { 0, 69, 2 }, { 2, 69, 2 }, { 4, 72, 2 }, { 6, 76, 2 }, { 8, 74, 4 }, { 12, 72, 4 }, { 16, 69, 2 }, { 18, 72, 2 }, { 20, 74, 4 }, { 24, 71, 8 },
                { 32, 72, 2 }, { 34, 72, 2 }, { 36, 76, 2 }, { 38, 79, 2 }, { 40, 77, 4 }, { 44, 76, 4 }, { 48, 74, 4 }, { 52, 71, 4 }, { 56, 69, 8 } },
                (at, note, sec) => SSynth(m, c, at, note, .05, sec * .9, .15, 3200, 2000, .004, .004, true, .004)));
        }

        // ------------------------------------------------ 14. Boss (Thunder Cloud General): storm metal (G minor)
        static void BossStorm(Mix[] s, Ctx c)
        {
            int[][] chords = { new[] { 55, 58, 62 }, new[] { 51, 55, 58 }, new[] { 53, 57, 60 }, new[] { 50, 54, 57 } };
            int[] roots = { 43, 39, 41, 38 };
            // the onion king's low end; the riff rolls like thunder (minor third, fourth, flat fifth)
            BossLowEnd(s, c, roots, new Dictionary<int, int> { { 0, 0 }, { 6, 3 }, { 8, 0 }, { 10, 5 }, { 16, 0 }, { 22, 6 }, { 24, 0 }, { 26, 5 }, { 30, 3 } });
            for (int bar = 0; bar < 4; bar++)
            {
                int b0 = bar * 16;
                // signature: rolling thunder (low toms rumbling down) at the start of each bar, a storm pad under the guitars
                Under(s[0], c, .6, m =>
                {
                    for (int r = 0; r < 6; r++) STom(m, c, c.At(b0 + r * .5), 90 - r * 8, .25 - .03 * r, r % 2 == 0 ? -.6 : .6);
                    WithVerb(m, c, .55, 1.6, w => SChord(w, c, c.At(b0), chords[bar], .025, c.Sec(15.8), 1800, 700, .3, 1));
                });
                // lead: crackling "lightning" stabs, high and bright
                foreach (int st in new[] { 3, 7, 11, 14 }) SSynth(s[3], c, c.At(b0 + st), chords[bar][st % 3] + 24, .05, c.Sec(.5), st % 2 == 0 ? -.6 : .6, 7000, 2500, .001, .02, true);
            }
            Reverb(s[3], s[3], .3, 1.3, c.Rate);
            // extra: a heroic brass melody fighting the storm
            WithVerb(s[4], c, .45, 1.4, m => Melody(m, c, new[,] { { 0, 67, 4 }, { 4, 70, 4 }, { 8, 74, 6 }, { 14, 72, 2 }, { 16, 70, 6 }, { 22, 69, 2 }, { 24, 67, 8 },
                { 32, 70, 4 }, { 36, 74, 4 }, { 40, 77, 6 }, { 46, 75, 2 }, { 48, 74, 8 }, { 56, 69, 8 } },
                (at, note, sec) => { SSynth(m, c, at, note - 12, .05, sec * .95, -.3, 2600, 1400, .03, .005, false, .004, .08); SSynth(m, c, at, note, .03, sec * .95, .3, 3000, 1800, .03, .006, false, .004, .08); }));
        }

        // ------------------------------------------------ 15. Boss (Snowman General): frozen march metal (B minor)
        static void BossBlizzard(Mix[] s, Ctx c)
        {
            int[][] chords = { new[] { 59, 62, 66 }, new[] { 55, 59, 62 }, new[] { 57, 61, 64 }, new[] { 54, 58, 61 } };
            int[] roots = { 47, 43, 45, 42 };
            // the onion king's low end; a stomping march riff (root, octave-less fifth and minor sixth)
            BossLowEnd(s, c, roots, new Dictionary<int, int> { { 0, 0 }, { 4, 0 }, { 8, 7 }, { 12, 0 }, { 16, 0 }, { 20, 8 }, { 24, 7 }, { 28, 5 }, { 30, 3 } });
            for (int bar = 0; bar < 4; bar++)
            {
                int b0 = bar * 16;
                // signature: sleigh bells shaking in 8ths and an icy celesta arpeggio
                Under(s[1], c, .55, m => { for (int e = 0; e < 8; e++) for (int k = 0; k < 3; k++) SHat(m, c, c.At(b0 + e * 2 + k * .15), .05, .03, e % 2 == 0 ? -.4 : .4); });
                WithVerb(s[3], c, .45, 1.4, m => { for (int st = 0; st < 16; st += 2) SKeys(m, c, c.At(b0 + st), chords[bar][(st / 2) % 3] + 24 + (st >= 8 ? 12 : 0), .035, c.Sec(1.6), st % 4 == 0 ? -.6 : .6, 4, 1.5, .4); });
            }
            PingPong(s[3], c, 3, .35, .45);
            // extra: a cold, proud march melody
            WithVerb(s[4], c, .45, 1.4, m => Melody(m, c, new[,] { { 0, 71, 3 }, { 3, 71, 1 }, { 4, 74, 4 }, { 8, 78, 6 }, { 14, 76, 2 }, { 16, 74, 4 }, { 20, 73, 4 }, { 24, 71, 8 },
                { 32, 74, 3 }, { 35, 74, 1 }, { 36, 78, 4 }, { 40, 81, 6 }, { 46, 79, 2 }, { 48, 78, 4 }, { 52, 76, 4 }, { 56, 73, 8 } },
                (at, note, sec) => SSynth(m, c, at, note, .055, sec * .95, -.15, 4000, 2200, .01, .006, false, .006, .06)));
        }

        // ------------------------------------------------ 10. Home / results: chill lo-fi (C major 7ths)
        static void Home(Mix[] s, Ctx c)
        {
            int[][] chords = { new[] { 60, 64, 67, 71 }, new[] { 57, 60, 64, 67 }, new[] { 57, 60, 62, 65 }, new[] { 55, 59, 62, 65 } };
            int[] roots = { 36, 33, 38, 31 };
            for (int bar = 0; bar < 4; bar++)
            {
                int b0 = bar * 16;
                SKick(s[0], c, c.At(b0), .55); SKick(s[0], c, c.At(b0 + 10), .45);
                WithVerb(s[0], c, .5, 1.3, m => { for (int k = 0; k < 4; k++) SKeys(m, c, c.At(b0 + k * .3), chords[bar][k], .05, c.Sec(15), -.4 + .27 * k, 1, 1.2, 1.6); });
                SSnare(s[1], c, c.At(b0 + 4), .22, 0, 220); SSnare(s[1], c, c.At(b0 + 12), .22, 0, 220);
                for (int e = 0; e < 8; e++) SHat(s[1], c, c.At(b0 + e * 2 + (e % 2 == 1 ? .4 : 0)), .05, .02, .35);
                SPluck(s[2], c, c.At(b0), roots[bar], .3, c.Sec(7), 0, 500, .6); SPluck(s[2], c, c.At(b0 + 10), roots[bar] + 7, .2, c.Sec(5), 0, 500, .5);
            }
            WithVerb(s[3], c, .55, 1.4, m => Melody(m, c, new[,] { { 4, 76, 4 }, { 8, 79, 4 }, { 12, 83, 8 }, { 24, 81, 4 }, { 28, 79, 4 }, { 36, 77, 4 }, { 40, 81, 4 }, { 44, 84, 8 }, { 52, 83, 4 }, { 56, 79, 8 } },
                (at, note, sec) => SKeys(m, c, at, note, .07, sec, .2, 1, 1.4, .9)));
            WithVerb(s[4], c, .6, 1.6, m => { for (int bar = 0; bar < 4; bar++) SKeys(m, c, c.At(bar * 16 + 14), chords[bar][3] + 24, .025, c.Sec(4), .6, 3.5, 1, .6); });
        }
    }
}
