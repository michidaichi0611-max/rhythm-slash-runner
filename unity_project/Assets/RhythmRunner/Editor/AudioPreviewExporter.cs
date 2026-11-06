using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using RhythmSlashRunner.Presentation;

namespace RhythmSlashRunner.EditorTools
{
    /// <summary>Writes the ten music pieces (all layers, two loops each) and every sound effect as WAV files for listening checks.</summary>
    public static class AudioPreviewExporter
    {
        const int Rate = 48000;

        static readonly Dictionary<MusicTrack, int> PreviewBpm = new Dictionary<MusicTrack, int>
        {
            { MusicTrack.Kitchen, 120 }, { MusicTrack.ToyRoom, 130 }, { MusicTrack.SkyOrchard, 130 }, { MusicTrack.NeonMarket, 140 }, { MusicTrack.CandyLand, 140 },
            { MusicTrack.Freezer, 150 }, { MusicTrack.Fever, 130 }, { MusicTrack.BossOnion, 120 }, { MusicTrack.BossToaster, 130 }, { MusicTrack.Home, 92 },
            { MusicTrack.BossIcecream, 140 }, { MusicTrack.BossDonut, 140 },
            { MusicTrack.BossZenmai, 130 }, { MusicTrack.BossKaminari, 130 }, { MusicTrack.BossYukidaruma, 150 },
        };

        [MenuItem("Rhythm Slash Runner/Export Audio Preview")]
        public static void Export()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../review/audio_preview"));
            string musicDir = Path.Combine(dir, "music");
            Directory.CreateDirectory(musicDir);
            foreach (string old in Directory.GetFiles(dir, "music_*.wav")) File.Delete(old); // the pre-round-6 mono loops
            int index = 1;
            foreach (MusicTrack track in (MusicTrack[])Enum.GetValues(typeof(MusicTrack)))
            {
                int bpm;
                if (!PreviewBpm.TryGetValue(track, out bpm)) bpm = 140; // the stage remixes (unused since round 17)
                float[] mix = SynthBank.BuildTrackMix(track, Rate, bpm);
                var twice = new float[mix.Length * 2];
                Array.Copy(mix, twice, mix.Length); Array.Copy(mix, 0, twice, mix.Length, mix.Length); // two loops: the seam is audible
                Write(Path.Combine(musicDir, index.ToString("00") + "_" + track + "_" + bpm + "bpm.wav"), twice, 2);
                index++;
            }
            foreach (Sfx id in (Sfx[])Enum.GetValues(typeof(Sfx))) Write(Path.Combine(dir, "sfx_" + id + ".wav"), SynthBank.BuildSfx(id, Rate), 1);
            Debug.Log("Audio preview written to " + dir);
        }

        static void Write(string path, float[] data, int channels)
        {
            using (var w = new BinaryWriter(File.Create(path)))
            {
                int bytes = data.Length * 2;
                w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write(36 + bytes); w.Write(new[] { 'W', 'A', 'V', 'E' });
                w.Write(new[] { 'f', 'm', 't', ' ' }); w.Write(16); w.Write((short)1); w.Write((short)channels); w.Write(Rate); w.Write(Rate * 2 * channels); w.Write((short)(2 * channels)); w.Write((short)16);
                w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write(bytes);
                foreach (float v in data) w.Write((short)Mathf.Clamp(Mathf.RoundToInt(v * 32767f), -32768, 32767));
            }
        }
    }
}
