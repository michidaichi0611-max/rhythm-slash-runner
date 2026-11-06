namespace RhythmSlashRunner.Core
{
    /// <summary>
    /// Photosensitivity guard (review/round0/committee_release.md F1/F5): every effect that pulses brightness asks this
    /// budget first. At most <see cref="MaxPerSecond"/> flashes per region inside any rolling one-second window, measured in
    /// real time (not beats), so a faster BPM or dense offbeats can never exceed 3 Hz.
    /// </summary>
    public sealed class FlashBudget
    {
        public enum Region { Edge = 0, TopBand = 1, Lane = 2, Hud = 3, Screen = 4 }

        public const int MaxPerSecond = 2;
        const int RegionCount = 5;
        readonly double[,] recent = new double[RegionCount, MaxPerSecond];

        public FlashBudget() { Reset(); }

        public void Reset()
        {
            for (int r = 0; r < RegionCount; r++)
                for (int i = 0; i < MaxPerSecond; i++) recent[r, i] = double.NegativeInfinity;
        }

        /// <summary>Returns true and records the flash when allowed; false means draw without a brightness pulse.</summary>
        public bool TryFlash(Region region, double timeSeconds)
        {
            int r = (int)region, oldest = 0;
            for (int i = 1; i < MaxPerSecond; i++) if (recent[r, i] < recent[r, oldest]) oldest = i;
            if (timeSeconds - recent[r, oldest] < 1.0) return false;
            recent[r, oldest] = timeSeconds;
            return true;
        }
    }
}
