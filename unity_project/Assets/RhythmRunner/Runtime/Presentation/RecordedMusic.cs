using System;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmSlashRunner.Presentation
{
    /// <summary>
    /// Round 34: real music in place of the synthesised pieces. Put a seamless loop (WAV/OGG) in
    /// Resources/Audio/Music/ and list it in Resources/Audio/Music/music.json:
    ///   { "entries": [ { "track": "BossOnion", "file": "boss_onion_120", "bpm": 120, "beats": 64 } ] }
    /// "track" is a <see cref="MusicTrack"/> name, "beats" the loop's length in beats (a whole number of bars, starting on
    /// a downbeat). Several files may serve one track at different tempos; the nearest tempo is used and stretched to the
    /// stage's tempo (which also shifts its pitch a little — give one file per stage tempo, 120/130/140/150, to avoid that).
    /// A recorded piece plays as one layer (the combo layers and the FEVER layer stay silent for it).
    /// Loaded on the main thread at start-up; the music builder (a worker thread) only reads the samples.
    /// </summary>
    public static class RecordedMusic
    {
        [Serializable] sealed class Entry { public string track; public string file; public float bpm; public int beats; }
        [Serializable] sealed class Manifest { public Entry[] entries; }

        public sealed class Loop
        {
            public float[] Stereo; // interleaved L/R
            public int Rate;
            public double Bpm;
            public int Beats;
        }

        static readonly Dictionary<MusicTrack, List<Loop>> loops = new Dictionary<MusicTrack, List<Loop>>();
        static bool loaded;

        public static void Load()
        {
            if (loaded) return;
            loaded = true;
            TextAsset json = Resources.Load<TextAsset>("Audio/Music/music");
            if (json == null) return;
            Manifest m = JsonUtility.FromJson<Manifest>(json.text);
            if (m == null || m.entries == null) return;
            foreach (Entry e in m.entries)
            {
                MusicTrack track;
                if (!Enum.TryParse(e.track, out track) || e.bpm <= 0 || e.beats <= 0) { Debug.LogWarning("[Music] bad entry " + e.track + " / " + e.file); continue; }
                AudioClip clip = Resources.Load<AudioClip>("Audio/Music/" + e.file);
                if (clip == null) { Debug.LogWarning("[Music] missing file " + e.file); continue; }
                if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
                var data = new float[clip.samples * clip.channels];
                if (!clip.GetData(data, 0)) { Debug.LogWarning("[Music] cannot read " + e.file + " (import it as Decompress On Load)"); continue; }
                float[] stereo = data;
                if (clip.channels == 1)
                {
                    stereo = new float[clip.samples * 2];
                    for (int i = 0; i < clip.samples; i++) stereo[2 * i] = stereo[2 * i + 1] = data[i];
                }
                else if (clip.channels > 2)
                {
                    stereo = new float[clip.samples * 2];
                    for (int i = 0; i < clip.samples; i++) { stereo[2 * i] = data[i * clip.channels]; stereo[2 * i + 1] = data[i * clip.channels + 1]; }
                }
                List<Loop> list;
                if (!loops.TryGetValue(track, out list)) loops[track] = list = new List<Loop>();
                list.Add(new Loop { Stereo = stereo, Rate = clip.frequency, Bpm = e.bpm, Beats = e.beats });
                Debug.Log("[Music] recorded " + track + " ← " + e.file + " (" + e.bpm + " BPM, " + e.beats + " beats)");
            }
        }

        public static void Add(MusicTrack track, Loop loop)
        {
            List<Loop> list;
            if (!loops.TryGetValue(track, out list)) loops[track] = list = new List<Loop>();
            list.Add(loop);
        }

        public static void Clear() { loops.Clear(); loaded = false; }

        /// <summary>The loop for a track at a tempo (nearest tempo), or null for the synthesised piece.</summary>
        public static Loop Find(MusicTrack track, double bpm)
        {
            List<Loop> list;
            if (!loops.TryGetValue(track, out list) || list.Count == 0) return null;
            Loop best = list[0];
            foreach (Loop l in list) if (Math.Abs(l.Bpm - bpm) < Math.Abs(best.Bpm - bpm)) best = l;
            return best;
        }

        /// <summary>Beats in one loop of a track (16 for the synthesised pieces).</summary>
        public static int BeatsOf(MusicTrack track, double bpm)
        {
            Loop l = Find(track, bpm);
            return l != null ? l.Beats : SynthBank.TrackBeats;
        }

        /// <summary>The loop stretched to <paramref name="beats"/> beats at <paramref name="bpm"/> and <paramref name="rate"/> (linear, interleaved stereo).</summary>
        public static float[] Stretch(Loop loop, int rate, double bpm)
        {
            int outFrames = (int)Math.Round(loop.Beats * 60.0 / bpm * rate);
            int inFrames = loop.Stereo.Length / 2;
            var result = new float[outFrames * 2];
            double step = (double)inFrames / outFrames;
            for (int i = 0; i < outFrames; i++)
            {
                double p = i * step;
                int a = (int)p, b = (a + 1) % inFrames;
                float t = (float)(p - a);
                a %= inFrames;
                result[2 * i] = loop.Stereo[2 * a] + (loop.Stereo[2 * b] - loop.Stereo[2 * a]) * t;
                result[2 * i + 1] = loop.Stereo[2 * a + 1] + (loop.Stereo[2 * b + 1] - loop.Stereo[2 * a + 1]) * t;
            }
            return result;
        }
    }
}
