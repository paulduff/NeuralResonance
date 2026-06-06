// audioFeatures.worklet.js
// Lightweight AudioWorkletProcessor for compact auditory features.
//
// Goals:
// - Keep CPU use minimal (no FFT, no autocorrelation sweep).
// - Provide stable, "good enough" intensity + a pitch-ish value.
//
// Features:
// - intensity01: RMS loudness mapped to 0..1
// - toneHz: rough fundamental proxy from Zero-Crossing Rate (ZCR)
//
// Emits messages: { type:'audioFeatures', intensity01, toneHz }

class AudioFeaturesProcessor extends AudioWorkletProcessor {
  constructor(options) {
    super();
    const po = (options && options.processorOptions) || {};
    this.intensityGain = typeof po.intensityGain === 'number' ? po.intensityGain : 3.0;
    this.maxToneHz = typeof po.maxToneHz === 'number' ? po.maxToneHz : 2000;

    // Smaller buffer reduces latency and CPU.
    this._buffer = new Float32Array(1024);
    this._bufPos = 0;
    this._lastSentAt = 0;

    this._toneSmooth = 220;
  }

  _clamp(v, lo, hi) { return Math.min(hi, Math.max(lo, v)); }

  _rms(buf) {
    let s = 0;
    for (let i = 0; i < buf.length; i++) {
      const v = buf[i];
      s += v * v;
    }
    return Math.sqrt(s / buf.length);
  }

  _zcrToHz(buf, sr) {
    let crossings = 0;
    let prev = buf[0];
    for (let i = 1; i < buf.length; i++) {
      const v = buf[i];
      // count sign changes
      if ((v >= 0 && prev < 0) || (v < 0 && prev >= 0)) crossings++;
      prev = v;
    }
    // crossings per second / 2 gives approx fundamental for clean tones
    const seconds = buf.length / sr;
    if (seconds <= 0) return 0;
    return (crossings / seconds) * 0.5;
  }

  process(inputs) {
    const input = inputs[0];
    if (!input || input.length === 0) return true;
    const ch0 = input[0];
    if (!ch0) return true;

    for (let i = 0; i < ch0.length; i++) {
      this._buffer[this._bufPos++] = ch0[i];
      if (this._bufPos >= this._buffer.length) {
        const tmp = new Float32Array(this._buffer);

        const r = this._rms(tmp);
        const intensity01 = this._clamp(r * this.intensityGain, 0, 1);

        // Gate tone when quiet.
        let toneHz = this._toneSmooth;
        if (r > 0.01) {
          const z = this._zcrToHz(tmp, sampleRate);
          // ZCR tends to overestimate for noisy signals; clamp into useful band.
          const cand = this._clamp(z, 60, this.maxToneHz);
          // Smooth hard so the engine sees a stable driver.
          toneHz = 0.85 * this._toneSmooth + 0.15 * cand;
        }
        this._toneSmooth = toneHz;

        // Throttle messages to ~10Hz to avoid flooding main thread.
        const now = currentTime;
        if (now - this._lastSentAt > 0.10) {
          this._lastSentAt = now;
          this.port.postMessage({ type: 'audioFeatures', intensity01, toneHz });
        }

        this._bufPos = 0;
      }
    }
    return true;
  }
}

registerProcessor('audio-features', AudioFeaturesProcessor);
