using System;

namespace RhythmSlashRunner.Core
{
    /// <summary>
    /// Converts input-clock timestamps (Input System event time, same timeline as Time.realtimeSinceStartupAsDouble)
    /// into AudioSettings.dspTime. Never subtract the two clocks directly; go through this bridge.
    /// </summary>
    /// <remarks>
    /// dspTime only advances once per audio buffer, so a value read on the main thread is stale by up to one buffer.
    /// Every sample therefore under-estimates the true offset (dsp - input); the bridge keeps the maximum of the
    /// samples taken at the moments dspTime ticked, over a sliding window, which tracks slow drift while rejecting staleness.
    /// A jump beyond <see cref="ResetThresholdSeconds"/> (audio device change, resume from background) re-seeds the window.
    /// </remarks>
    public sealed class ClockBridge
    {
        public const double ResetThresholdSeconds = 0.05;
        readonly double[] window;
        int count;
        int head;
        double lastDsp = double.NaN;

        public ClockBridge(int windowSize = 32)
        {
            if (windowSize < 1) throw new ArgumentOutOfRangeException(nameof(windowSize));
            window = new double[windowSize];
        }

        public bool Ready { get { return count > 0; } }

        /// <summary>Current estimate of dspTime - inputTime, in seconds.</summary>
        public double Offset { get; private set; }

        public int SampleCount { get { return count; } }

        /// <summary>Call once per frame with both clocks read back to back.</summary>
        public void Sample(double inputNow, double dspNow)
        {
            if (double.IsNaN(inputNow) || double.IsNaN(dspNow)) return;
            if (!double.IsNaN(lastDsp) && dspNow == lastDsp) return; // dsp did not tick: sample carries no new information
            bool firstTick = double.IsNaN(lastDsp);
            lastDsp = dspNow;
            if (firstTick) return; // the first read may be arbitrarily stale; wait for a tick edge
            double candidate = dspNow - inputNow;
            if (count > 0 && Math.Abs(candidate - Offset) > ResetThresholdSeconds) { count = 0; head = 0; }
            window[head] = candidate;
            head = (head + 1) % window.Length;
            if (count < window.Length) count++;
            double max = double.MinValue;
            for (int i = 0; i < count; i++) if (window[i] > max) max = window[i];
            Offset = max;
        }

        public double ToDsp(double inputTime) { return inputTime + Offset; }

        public double ToInput(double dspTime) { return dspTime - Offset; }

        public void Reset() { count = 0; head = 0; lastDsp = double.NaN; Offset = 0; }
    }
}
