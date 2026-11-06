// Maps input-event timestamps (performance.now / Event.timeStamp, ms) onto the audio clock (AudioContext seconds).
// Web counterpart of the Unity ClockBridge: never subtract the two clocks directly, always go through the bridge.

export class ClockBridge {
  constructor({ smoothing = 0.1, resetThresholdSeconds = 0.05 } = {}) {
    this.smoothing = smoothing;
    this.resetThreshold = resetThresholdSeconds;
    this.offset = null; // audioSeconds - performanceSeconds
  }

  /** Feed one paired sample, e.g. AudioContext.getOutputTimestamp(). */
  sync({ contextTime, performanceTime }) {
    if (!Number.isFinite(contextTime) || !Number.isFinite(performanceTime) || contextTime <= 0) return;
    const sample = contextTime - performanceTime / 1000;
    if (this.offset === null || Math.abs(sample - this.offset) > this.resetThreshold) this.offset = sample;
    else this.offset += (sample - this.offset) * this.smoothing;
  }

  get ready() { return this.offset !== null; }

  toAudioSeconds(performanceMs) { return performanceMs / 1000 + (this.offset ?? 0); }

  toPerformanceMs(audioSeconds) { return (audioSeconds - (this.offset ?? 0)) * 1000; }
}
