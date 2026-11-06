using System;
using NUnit.Framework;
using RhythmSlashRunner.Core;

namespace RhythmSlashRunner.Tests
{
    public sealed class ClockBridgeTests
    {
        // Simulated audio clock: true dsp = input + offset, but readable only in buffer-sized steps.
        static double ReadDsp(double inputTime, double offset, double buffer)
        {
            return Math.Floor((inputTime + offset) / buffer) * buffer;
        }

        static ClockBridge Warm(double offset, double buffer, double frame, double seconds, int seed, double startTime = 3.0)
        {
            var bridge = new ClockBridge();
            var random = new Random(seed);
            for (double t = startTime; t < startTime + seconds; t += frame * (0.8 + random.NextDouble() * 0.4))
                bridge.Sample(t, ReadDsp(t, offset, buffer));
            return bridge;
        }

        [TestCase(1024 / 48000.0, 1 / 60.0)]
        [TestCase(256 / 48000.0, 1 / 120.0)]
        [TestCase(2048 / 44100.0, 1 / 30.0)]
        public void ConvertsInputTimeToDspWithinTwoMilliseconds(double buffer, double frame)
        {
            const double offset = 123.456;
            ClockBridge bridge = Warm(offset, buffer, frame, 4.0, 1);
            Assert.IsTrue(bridge.Ready);
            for (double t = 7.0; t < 8.0; t += 0.0371)
                Assert.AreEqual(t + offset, bridge.ToDsp(t), 0.002, "t=" + t);
        }

        [Test]
        public void NeverAheadOfTheTrueDspClock()
        {
            const double offset = 5.0, buffer = 1024 / 48000.0;
            ClockBridge bridge = Warm(offset, buffer, 1 / 60.0, 2.0, 2);
            Assert.LessOrEqual(bridge.Offset, offset + 1e-9);
        }

        [Test]
        public void NotReadyUntilTheDspClockTicks()
        {
            var bridge = new ClockBridge();
            bridge.Sample(1.0, 10.0);
            bridge.Sample(1.01, 10.0);
            Assert.IsFalse(bridge.Ready);
            bridge.Sample(1.03, 10.021);
            Assert.IsTrue(bridge.Ready);
        }

        [Test]
        public void ReseedsAfterAnAudioDeviceJump()
        {
            const double buffer = 1024 / 48000.0;
            var bridge = new ClockBridge();
            double t = 1.0;
            for (; t < 3.0; t += 1 / 60.0) bridge.Sample(t, ReadDsp(t, 10.0, buffer));
            // dsp restarts from a different origin (e.g. Bluetooth headset connected).
            for (; t < 4.0; t += 1 / 60.0) bridge.Sample(t, ReadDsp(t, -0.5, buffer));
            Assert.AreEqual(-0.5, bridge.Offset, 0.003);
        }

        [Test]
        public void TracksSlowDriftBetweenClocks()
        {
            const double buffer = 512 / 48000.0;
            var bridge = new ClockBridge();
            // The audio clock runs 0.01% fast: 1 ms of drift every 10 seconds.
            for (double t = 0; t < 60; t += 1 / 60.0) bridge.Sample(t, Math.Floor((t * 1.0001 + 2.0) / buffer) * buffer);
            Assert.AreEqual(60 * 1.0001 + 2.0, bridge.ToDsp(60), 0.003);
        }
    }
}
