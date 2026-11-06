using NUnit.Framework;
using RhythmSlashRunner.Presentation;

namespace RhythmSlashRunner.Tests
{
    /// <summary>Round 34: a recorded loop replaces a synthesised piece, stretched to the stage's tempo.</summary>
    public sealed class RecordedMusicTests
    {
        static RecordedMusic.Loop Loop(double bpm, int beats, int rate = 1000)
        {
            int frames = (int)(beats * 60.0 / bpm * rate);
            var stereo = new float[frames * 2];
            for (int i = 0; i < frames; i++) stereo[2 * i] = stereo[2 * i + 1] = (float)i / frames;
            return new RecordedMusic.Loop { Stereo = stereo, Rate = rate, Bpm = bpm, Beats = beats };
        }

        [TearDown] public void Reset() { RecordedMusic.Clear(); }

        [Test]
        public void ARecordedLoopReplacesThePieceAsOneLayer()
        {
            RecordedMusic.Clear();
            RecordedMusic.Add(MusicTrack.BossOnion, Loop(120, 32));
            var stems = SynthBank.BuildTrack(MusicTrack.BossOnion, 1000, 150);
            Assert.AreEqual((int)System.Math.Round(32 * 60.0 / 150 * 1000) * 2, stems[Stem.Base].Length, "32 beats at the stage's 150 BPM");
            foreach (float v in stems[Stem.Drums]) Assert.AreEqual(0f, v);
            Assert.AreEqual(32, RecordedMusic.BeatsOf(MusicTrack.BossOnion, 150));
            Assert.AreEqual(SynthBank.TrackBeats, RecordedMusic.BeatsOf(MusicTrack.BossToaster, 150), "other pieces stay synthesised");
        }

        [Test]
        public void TheNearestTempoIsUsed()
        {
            RecordedMusic.Clear();
            RecordedMusic.Add(MusicTrack.Kitchen, Loop(120, 16));
            RecordedMusic.Add(MusicTrack.Kitchen, Loop(150, 16));
            Assert.AreEqual(150, RecordedMusic.Find(MusicTrack.Kitchen, 140).Bpm);
            Assert.AreEqual(120, RecordedMusic.Find(MusicTrack.Kitchen, 125).Bpm);
            Assert.IsNull(RecordedMusic.Find(MusicTrack.Freezer, 120));
        }
    }
}
