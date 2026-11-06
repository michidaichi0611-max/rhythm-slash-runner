using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using RhythmSlashRunner.Presentation;

namespace RhythmSlashRunner.Tests
{
    /// <summary>Objective checks for the synthesized audio (we cannot listen in CI): finite, unclipped, seamless, fast.</summary>
    public sealed class SynthBankTests
    {
        const int Rate = 48000;
        static readonly Dictionary<MusicTrack, Dictionary<Stem, float[]>> cache = new Dictionary<MusicTrack, Dictionary<Stem, float[]>>();

        static Dictionary<Stem, float[]> Track(MusicTrack t)
        {
            Dictionary<Stem, float[]> d;
            if (!cache.TryGetValue(t, out d)) cache[t] = d = SynthBank.BuildTrack(t, Rate, 120);
            return d;
        }

        static IEnumerable<MusicTrack> AllTracks() { return (MusicTrack[])Enum.GetValues(typeof(MusicTrack)); }

        [TestCaseSource(nameof(AllTracks))]
        public void EveryTrackIsSixteenBeatsOfStereoFiniteAndUnclipped(MusicTrack track)
        {
            Dictionary<Stem, float[]> stems = Track(track);
            int frames = (int)Math.Round(SynthBank.TrackBeats * .5 * Rate);
            Assert.AreEqual(5, stems.Count);
            foreach (KeyValuePair<Stem, float[]> pair in stems)
            {
                Assert.AreEqual(frames * 2, pair.Value.Length, track + " " + pair.Key + " (interleaved stereo)");
                foreach (float v in pair.Value) Assert.IsFalse(float.IsNaN(v) || float.IsInfinity(v), track + " " + pair.Key);
            }
            float peak = 0;
            for (int i = 0; i < frames * 2; i++) { float sum = 0; foreach (float[] d in stems.Values) sum += d[i]; peak = Math.Max(peak, Math.Abs(sum)); }
            Assert.LessOrEqual(peak, .95f, track + ": all layers together must not clip");
            Assert.Greater(peak, .4f, track + ": should be reasonably loud");
        }

        [TestCaseSource(nameof(AllTracks))]
        public void EveryTrackLoopsWithoutAClick(MusicTrack track)
        {
            foreach (KeyValuePair<Stem, float[]> pair in Track(track))
            {
                float[] d = pair.Value;
                int frames = d.Length / 2;
                for (int ch = 0; ch < 2; ch++)
                {
                    float typical = 0;
                    for (int i = frames - 200; i < frames - 1; i++) typical = Math.Max(typical, Math.Abs(d[2 * (i + 1) + ch] - d[2 * i + ch]));
                    float seam = Math.Abs(d[ch] - d[2 * (frames - 1) + ch]);
                    Assert.LessOrEqual(seam, Math.Max(typical * 2.5f, .02f), track + " " + pair.Key + " ch" + ch + " seam " + seam + " vs typical " + typical);
                }
            }
        }

        [Test]
        public void TheTenTracksSoundDifferentFromEachOther()
        {
            // proof of "different impressions": the mixes are weakly correlated pairwise, and the stereo field is really wide
            var mixes = new Dictionary<MusicTrack, float[]>();
            foreach (MusicTrack t in AllTracks())
            {
                Dictionary<Stem, float[]> stems = Track(t);
                var m = new float[stems[Stem.Base].Length];
                foreach (float[] d in stems.Values) for (int i = 0; i < m.Length; i++) m[i] += d[i];
                mixes[t] = m;
            }
            var tracks = new List<MusicTrack>(mixes.Keys);
            for (int a = 0; a < tracks.Count; a++)
                for (int b = a + 1; b < tracks.Count; b++)
                {
                    // a boss remix is meant to share its stage piece's tune (owner round 16): those pairs are exempt
                    if (SynthBank.SourceOf(tracks[a]) == tracks[b] || SynthBank.SourceOf(tracks[b]) == tracks[a] || (SynthBank.SourceOf(tracks[a]) != tracks[a] && SynthBank.SourceOf(tracks[a]) == SynthBank.SourceOf(tracks[b]))) continue;
                    float[] x = mixes[tracks[a]], y = mixes[tracks[b]];
                    int n = Math.Min(x.Length, y.Length);
                    double xy = 0, xx = 0, yy = 0;
                    for (int i = 0; i < n; i++) { xy += x[i] * y[i]; xx += x[i] * x[i]; yy += y[i] * y[i]; }
                    double corr = xy / Math.Sqrt(Math.Max(1e-12, xx * yy));
                    Assert.Less(Math.Abs(corr), .6, tracks[a] + " vs " + tracks[b]);
                }
            foreach (KeyValuePair<MusicTrack, float[]> pair in mixes)
            {
                double side = 0, mid = 0;
                for (int i = 0; i < pair.Value.Length; i += 2) { double l = pair.Value[i], r = pair.Value[i + 1]; side += (l - r) * (l - r); mid += (l + r) * (l + r); }
                // round 16: kick and bass are deliberately heavier (and centred, for phone speakers), which lowers this whole-mix ratio
                Assert.Greater(side / Math.Max(1e-12, mid), .006, pair.Key + " should not be mono");
            }
        }

        [TestCase(130)]
        [TestCase(140)]
        [TestCase(150)]
        public void TracksRenderAtStageTempos(int bpm)
        {
            Dictionary<Stem, float[]> fast = SynthBank.BuildTrack(MusicTrack.Freezer, Rate, bpm);
            Assert.AreEqual((int)Math.Round(SynthBank.TrackBeats * 60.0 / bpm * Rate) * 2, fast[Stem.Base].Length);
        }

        [Test]
        public void EverySfxIsFiniteAndUnclipped()
        {
            foreach (Sfx id in (Sfx[])Enum.GetValues(typeof(Sfx)))
            {
                float[] d = SynthBank.BuildSfx(id, Rate);
                Assert.Greater(d.Length, 10, id.ToString());
                float peak = 0;
                foreach (float v in d) { Assert.IsFalse(float.IsNaN(v), id.ToString()); peak = Math.Max(peak, Math.Abs(v)); }
                Assert.LessOrEqual(peak, 1f, id.ToString());
                Assert.Greater(peak, .01f, id + " is silent");
            }
        }

        [Test]
        public void CueBandIsReservedForTimingSounds()
        {
            // share of energy at the cue's own partials (2.2 / 3.4 kHz): the cue owns them, the slash tone and BGM-like sounds do not
            foreach (Sfx other in new[] { Sfx.SliceTone, Sfx.MatSoft, Sfx.PerfectShimmer, Sfx.Coin })
            {
                float cue = CueShare(SynthBank.BuildSfx(Sfx.Cue, Rate));
                float o = CueShare(SynthBank.BuildSfx(other, Rate));
                Assert.Greater(cue, o * 2f, other.ToString());
            }
        }

        [Test]
        public void SlashLadderStaysOnCMajorPentatonic()
        {
            int[] allowed = { 0, 2, 4, 7, 9 };
            for (int combo = 1; combo <= 120; combo++)
                foreach (bool fever in new[] { false, true })
                {
                    int semis = AudioDirector.LadderSemitones(combo, fever);
                    Assert.Contains(((semis % 12) + 12) % 12, allowed, "combo " + combo);
                }
        }

        [Test]
        public void PitchedSlashTonesStayOutOfTheCueBand()
        {
            // cue partials at 2.2 / 3.4 kHz keep ±300 Hz for themselves (sound round 1 P1)
            for (int combo = 1; combo <= 200; combo++)
                foreach (bool fever in new[] { false, true })
                {
                    double f = 523.25 * Math.Pow(2, AudioDirector.LadderSemitones(combo, fever) / 12.0);
                    foreach (double layer in new[] { f, f * .5 })
                    {
                        Assert.IsFalse(Math.Abs(layer - 2200) < 300, "combo " + combo + " " + layer);
                        Assert.IsFalse(Math.Abs(layer - 3400) < 300, "combo " + combo + " " + layer);
                    }
                }
        }

        [Test]
        public void SynthesisIsFastEnoughForBoot()
        {
            // pieces render on a worker thread well ahead of use; even the heaviest must finish in a few seconds
            foreach (MusicTrack t in AllTracks())
            {
                var watch = Stopwatch.StartNew();
                SynthBank.BuildTrack(t, Rate, 150);
                watch.Stop();
                Assert.Less(watch.ElapsedMilliseconds, 5000, t + " (editor-side budget)");
            }
            var sfx = Stopwatch.StartNew();
            foreach (Sfx id in (Sfx[])Enum.GetValues(typeof(Sfx))) SynthBank.BuildSfx(id, Rate);
            Assert.Less(sfx.ElapsedMilliseconds, 4000);
        }

        static float CueShare(float[] d)
        {
            double total = 0;
            foreach (float v in d) total += v * v;
            return (float)((BandEnergy(d, 2200) + BandEnergy(d, 3400)) / Math.Max(1e-9, total * d.Length));
        }

        static float BandEnergy(float[] d, double freq)
        {
            double w = 2 * Math.PI * freq / Rate, c = 2 * Math.Cos(w), s0, s1 = 0, s2 = 0;
            for (int i = 0; i < d.Length; i++) { s0 = d[i] + c * s1 - s2; s2 = s1; s1 = s0; }
            return (float)(s1 * s1 + s2 * s2 - c * s1 * s2);
        }
    }
}
