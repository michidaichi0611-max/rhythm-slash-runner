using System;
using System.Collections.Generic;

namespace RhythmSlashRunner.Core
{
    /// <summary>
    /// Piecewise-constant tempo: beat ↔ seconds since beat 0. Segments are only ever appended for beats that are still in
    /// the future (stage changes are scheduled two chunks ahead), so the mapping of every beat already heard never moves.
    /// Beats before 0 (count-in) use the first tempo.
    /// </summary>
    public sealed class TempoMap
    {
        struct Segment { public double Beat, Seconds, Bpm; }

        readonly List<Segment> segments = new List<Segment>(8);

        public TempoMap(double bpm) { Reset(bpm); }

        public void Reset(double bpm)
        {
            if (bpm <= 0) throw new ArgumentOutOfRangeException(nameof(bpm));
            segments.Clear();
            segments.Add(new Segment { Beat = 0, Seconds = 0, Bpm = bpm });
        }

        public int Count { get { return segments.Count; } }
        public double BeatOf(int index) { return segments[index].Beat; }
        public double BpmOf(int index) { return segments[index].Bpm; }

        /// <summary>Changes the tempo from <paramref name="beat"/> on. Must not be earlier than the last change.</summary>
        public void Add(double beat, double bpm)
        {
            if (bpm <= 0) throw new ArgumentOutOfRangeException(nameof(bpm));
            Segment last = segments[segments.Count - 1];
            if (beat < last.Beat) throw new ArgumentException("tempo changes must be appended in order");
            if (Math.Abs(bpm - last.Bpm) < 1e-9) return;
            double seconds = last.Seconds + (beat - last.Beat) * 60.0 / last.Bpm;
            if (Math.Abs(beat - last.Beat) < 1e-9) segments[segments.Count - 1] = new Segment { Beat = beat, Seconds = last.Seconds, Bpm = bpm };
            else segments.Add(new Segment { Beat = beat, Seconds = seconds, Bpm = bpm });
        }

        int IndexForBeat(double beat)
        {
            for (int i = segments.Count - 1; i > 0; i--) if (beat >= segments[i].Beat) return i;
            return 0;
        }

        int IndexForSeconds(double seconds)
        {
            for (int i = segments.Count - 1; i > 0; i--) if (seconds >= segments[i].Seconds) return i;
            return 0;
        }

        public double SecondsAt(double beat)
        {
            Segment s = segments[IndexForBeat(beat)];
            return s.Seconds + (beat - s.Beat) * 60.0 / s.Bpm;
        }

        public double BeatAt(double seconds)
        {
            Segment s = segments[IndexForSeconds(seconds)];
            return s.Beat + (seconds - s.Seconds) * s.Bpm / 60.0;
        }

        public double BpmAt(double beat) { return segments[IndexForBeat(beat)].Bpm; }
        public double SecondsPerBeatAt(double beat) { return 60.0 / BpmAt(beat); }

        /// <summary>Signed milliseconds from beat <paramref name="from"/> to beat <paramref name="to"/>.</summary>
        public double Ms(double from, double to) { return (SecondsAt(to) - SecondsAt(from)) * 1000.0; }

        /// <summary>The beat that sounds <paramref name="ms"/> after <paramref name="beat"/>.</summary>
        public double BeatAfterMs(double beat, double ms) { return BeatAt(SecondsAt(beat) + ms / 1000.0); }
    }
}
