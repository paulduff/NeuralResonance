function clamp(v, lo, hi) {
  return Math.min(hi, Math.max(lo, v));
}

function normalizeApiBase(apiBase) {
  const s = String(apiBase || '').trim();
  if (!s) return 'http://localhost:5005/';
  return s.endsWith('/') ? s : s + '/';
}

function isoNow() {
  return new Date().toISOString();
}

function ageMs(iso) {
  if (!iso) return Number.POSITIVE_INFINITY;
  const t = Date.parse(iso);
  return Number.isFinite(t) ? (Date.now() - t) : Number.POSITIVE_INFINITY;
}

function channelHealth(enabled, lastUtc, lastError, staleMs) {
  if (!enabled) return { state: 'disabled' };
  if (lastError) return { state: 'error', detail: lastError, lastUtc: lastUtc || null };
  if (!lastUtc) return { state: 'warming' };
  if (ageMs(lastUtc) <= staleMs) return { state: 'ok', lastUtc };
  return { state: 'stale', lastUtc };
}

function postBackoffMs(failures) {
  const n = Math.max(1, Number(failures) || 1);
  return Math.min(3000, 250 * Math.pow(2, n - 1));
}

const state = {
  running: false,
  tabId: null,
  apiBase: 'http://localhost:5005/',
  config: null,
  mediaStream: null,
  video: null,
  canvas: null,
  ctx: null,
  visualTimer: null,
  audioTimer: null,
  postTimer: null,
  audioCtx: null,
  audioSource: null,
  analyser: null,
  audioGain: null,
  prevGray: null,
  toneSmooth: 220,
  latestVisual: { enabled: false, intensity01: 0, speedHz: 0, spatialFreq: 0.1 },
  latestAuditory: { enabled: false, intensity01: 0, toneHz: 220 },
  postInFlight: false,
  postAbortCtrl: null,
  postBackoffUntilMs: 0,
  postConsecutiveFailures: 0,
  lastError: null,
  lastPostUtc: null,
  lastVisualUtc: null,
  lastAudioUtc: null,
  lastVisualError: null,
  lastAudioError: null,
  lastPostError: null
};

const defaults = {
  fps: 6,
  width: 96,
  height: 96,
  enableVisual: true,
  enableAudio: true,
  allowAudioPlayback: false,
  maxSpeedHz: 6,
  maxSpatialFreq: 2,
  intensityGain: 1.25,
  motionGain: 2.25,
  edgeGain: 2.0,
  audioIntensityGain: 3.0,
  maxToneHz: 2000
};

async function postSensory() {
  if (!state.running) return;
  if (!state.latestVisual.enabled && !state.latestAuditory.enabled) return;
  if (state.postInFlight) return;

  const nowMs = Date.now();
  if (nowMs < state.postBackoffUntilMs) return;

  const payload = {
    visual: state.latestVisual,
    auditory: state.latestAuditory
  };

  state.postInFlight = true;
  const ctrl = new AbortController();
  state.postAbortCtrl = ctrl;
  const timeoutId = setTimeout(() => {
    try { ctrl.abort('post-timeout'); } catch { }
  }, 3500);

  try {
    const response = await fetch(state.apiBase + 'api/engine/sensory', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
      signal: ctrl.signal
    });

    if (!response.ok) {
      throw new Error(`POST ${response.status}`);
    }

    state.lastPostUtc = isoNow();
    state.lastPostError = null;
    state.postConsecutiveFailures = 0;
    state.postBackoffUntilMs = 0;
    state.lastError = null;
  } catch (err) {
    const msg = err && err.message ? err.message : String(err);
    state.lastPostError = msg;
    state.lastError = msg;
    state.postConsecutiveFailures = Math.min(10, state.postConsecutiveFailures + 1);
    state.postBackoffUntilMs = Date.now() + postBackoffMs(state.postConsecutiveFailures);
  } finally {
    clearTimeout(timeoutId);
    if (state.postAbortCtrl === ctrl) {
      state.postAbortCtrl = null;
    }
    state.postInFlight = false;
  }
}

function computeVisualFeatures(imgData) {
  const cfg = state.config;
  const data = imgData.data;
  const pixels = cfg.width * cfg.height;

  if (!state.prevGray || state.prevGray.length !== pixels) {
    state.prevGray = new Uint8Array(pixels);
  }

  let lumSum = 0;
  let motionSum = 0;
  let edgeSum = 0;

  for (let y = 0; y < cfg.height; y++) {
    const row = y * cfg.width;
    for (let x = 0; x < cfg.width; x++) {
      const i = row + x;
      const j = i * 4;
      const r = data[j];
      const g = data[j + 1];
      const b = data[j + 2];
      const gray = (77 * r + 150 * g + 29 * b) >> 8;

      lumSum += gray;
      motionSum += Math.abs(gray - state.prevGray[i]);

      if (x > 0) edgeSum += Math.abs(gray - state.prevGray[i - 1]);
      if (y > 0) edgeSum += Math.abs(gray - state.prevGray[i - cfg.width]);

      state.prevGray[i] = gray;
    }
  }

  const lumNorm = lumSum / (pixels * 255);
  const motionNorm = motionSum / (pixels * 255);
  const edgeNorm = edgeSum / (pixels * 255 * 2);

  state.latestVisual.enabled = true;
  state.latestVisual.intensity01 = clamp(lumNorm * cfg.intensityGain, 0, 1);
  state.latestVisual.speedHz = clamp(motionNorm * cfg.motionGain * cfg.maxSpeedHz, 0, cfg.maxSpeedHz);
  state.latestVisual.spatialFreq = clamp(Math.max(0.1, edgeNorm * cfg.edgeGain * cfg.maxSpatialFreq), 0.1, cfg.maxSpatialFreq);
  state.lastVisualUtc = isoNow();
  state.lastVisualError = null;
}

function computeAuditoryFeatures() {
  if (!state.analyser || !state.config) return;
  const cfg = state.config;

  const n = state.analyser.fftSize;
  const buf = new Float32Array(n);
  state.analyser.getFloatTimeDomainData(buf);

  let sum = 0;
  let crossings = 0;
  let prev = buf[0];

  for (let i = 0; i < n; i++) {
    const v = buf[i];
    sum += v * v;
    if (i > 0 && ((v >= 0 && prev < 0) || (v < 0 && prev >= 0))) {
      crossings++;
    }
    prev = v;
  }

  const rms = Math.sqrt(sum / n);
  const intensity01 = clamp(rms * cfg.audioIntensityGain, 0, 1);

  let toneHz = state.toneSmooth;
  if (rms > 0.01 && state.audioCtx) {
    const sec = n / state.audioCtx.sampleRate;
    const zcrHz = sec > 0 ? (crossings / sec) * 0.5 : toneHz;
    const cand = clamp(zcrHz, 60, cfg.maxToneHz);
    toneHz = 0.85 * state.toneSmooth + 0.15 * cand;
  }

  state.toneSmooth = toneHz;

  state.latestAuditory.enabled = true;
  state.latestAuditory.intensity01 = intensity01;
  state.latestAuditory.toneHz = toneHz;
  state.lastAudioUtc = isoNow();
  state.lastAudioError = null;
}
function ensureVideoElement() {
  if (state.video) return;
  state.video = document.createElement('video');
  state.video.autoplay = true;
  state.video.playsInline = true;
  state.video.muted = true;
  state.video.style.display = 'none';
  document.body.appendChild(state.video);
}

async function startCapture(msg) {
  await stopCapture();

  state.config = { ...defaults, ...(msg.config || {}) };
  state.apiBase = normalizeApiBase(state.config.apiBase);
  state.tabId = msg.tabId || null;

  const constraints = {
    video: state.config.enableVisual
      ? {
          mandatory: {
            chromeMediaSource: 'tab',
            chromeMediaSourceId: msg.streamId,
            minWidth: state.config.width,
            minHeight: state.config.height,
            maxFrameRate: 30
          }
        }
      : false,
    audio: state.config.enableAudio
      ? {
          mandatory: {
            chromeMediaSource: 'tab',
            chromeMediaSourceId: msg.streamId
          }
        }
      : false
  };

  state.mediaStream = await navigator.mediaDevices.getUserMedia(constraints);

  ensureVideoElement();
  state.video.srcObject = state.mediaStream;
  await state.video.play();

  if (state.config.enableVisual) {
    state.canvas = document.createElement('canvas');
    state.canvas.width = state.config.width;
    state.canvas.height = state.config.height;
    state.ctx = state.canvas.getContext('2d', { willReadFrequently: true });

    const visualMs = Math.max(66, Math.floor(1000 / state.config.fps));
    state.visualTimer = setInterval(() => {
      try {
        if (!state.video || state.video.readyState < 2 || !state.ctx) return;
        state.ctx.drawImage(state.video, 0, 0, state.config.width, state.config.height);
        const imgData = state.ctx.getImageData(0, 0, state.config.width, state.config.height);
        computeVisualFeatures(imgData);
      } catch (err) {
        const msg = err && err.message ? err.message : String(err);
        state.lastVisualError = msg;
        state.lastError = msg;
      }
    }, visualMs);
  }

  if (state.config.enableAudio) {
    try {
      state.audioCtx = new (window.AudioContext || window.webkitAudioContext)();
      state.audioSource = state.audioCtx.createMediaStreamSource(state.mediaStream);
      state.analyser = state.audioCtx.createAnalyser();
      state.analyser.fftSize = 2048;

      state.audioSource.connect(state.analyser);

      if (state.config.allowAudioPlayback) {
        state.audioGain = state.audioCtx.createGain();
        state.audioGain.gain.value = 1;
        state.audioSource.connect(state.audioGain);
        state.audioGain.connect(state.audioCtx.destination);
      }

      const audioMs = Math.max(80, Math.floor(1000 / state.config.fps));
      state.audioTimer = setInterval(computeAuditoryFeatures, audioMs);
      try { await state.audioCtx.resume(); } catch { }
    } catch (err) {
      const msg = err && err.message ? err.message : String(err);
      state.lastAudioError = msg;
      state.lastError = msg;
    }
  }

  const postMs = Math.max(80, Math.floor(1000 / state.config.fps));
  state.postTimer = setInterval(postSensory, postMs);

  state.postInFlight = false;
  state.postAbortCtrl = null;
  state.postBackoffUntilMs = 0;
  state.postConsecutiveFailures = 0;

  state.running = true;
  state.lastError = null;
}

async function stopCapture() {
  if (state.visualTimer) {
    clearInterval(state.visualTimer);
    state.visualTimer = null;
  }
  if (state.audioTimer) {
    clearInterval(state.audioTimer);
    state.audioTimer = null;
  }
  if (state.postTimer) {
    clearInterval(state.postTimer);
    state.postTimer = null;
  }

  if (state.postAbortCtrl) {
    try { state.postAbortCtrl.abort('stop-capture'); } catch { }
    state.postAbortCtrl = null;
  }
  state.postInFlight = false;

  if (state.mediaStream) {
    state.mediaStream.getTracks().forEach((t) => {
      try { t.stop(); } catch { }
    });
    state.mediaStream = null;
  }

  if (state.video) {
    try { state.video.pause(); } catch { }
    try { state.video.srcObject = null; } catch { }
  }

  if (state.audioSource) {
    try { state.audioSource.disconnect(); } catch { }
    state.audioSource = null;
  }
  if (state.analyser) {
    try { state.analyser.disconnect(); } catch { }
    state.analyser = null;
  }
  if (state.audioGain) {
    try { state.audioGain.disconnect(); } catch { }
    state.audioGain = null;
  }
  if (state.audioCtx) {
    try { await state.audioCtx.close(); } catch { }
    state.audioCtx = null;
  }

  state.prevGray = null;
  state.running = false;
  state.tabId = null;
  state.latestVisual = { enabled: false, intensity01: 0, speedHz: 0, spatialFreq: 0.1 };
  state.latestAuditory = { enabled: false, intensity01: 0, toneHz: 220 };
  state.lastError = null;
  state.lastPostUtc = null;
  state.lastVisualUtc = null;
  state.lastAudioUtc = null;
  state.lastVisualError = null;
  state.lastAudioError = null;
  state.lastPostError = null;
  state.postBackoffUntilMs = 0;
  state.postConsecutiveFailures = 0;
}
function statusPayload() {
  const cfg = state.config || defaults;
  return {
    running: state.running,
    tabId: state.tabId,
    lastError: state.lastError,
    lastPostUtc: state.lastPostUtc,
    health: {
      visual: channelHealth(!!cfg.enableVisual, state.lastVisualUtc, state.lastVisualError, 5500),
      audio: channelHealth(!!cfg.enableAudio, state.lastAudioUtc, state.lastAudioError, 5500),
      post: channelHealth(true, state.lastPostUtc, state.lastPostError, 5500)
    }
  };
}

chrome.runtime.onMessage.addListener((msg, sender, sendResponse) => {
  if (!msg || msg.target !== 'offscreen') return;

  (async () => {
    try {
      switch (msg.type) {
        case 'OFFSCREEN_START':
          await startCapture(msg);
          sendResponse({ ok: true, ...statusPayload() });
          return;
        case 'OFFSCREEN_STOP':
          await stopCapture();
          sendResponse({ ok: true, ...statusPayload() });
          return;
        case 'OFFSCREEN_STATUS':
          sendResponse({ ok: true, ...statusPayload() });
          return;
        default:
          sendResponse({ ok: false, error: `Unknown offscreen message type: ${msg.type}` });
      }
    } catch (err) {
      state.lastError = err && err.message ? err.message : String(err);
      await stopCapture();
      sendResponse({ ok: false, error: state.lastError, ...statusPayload() });
    }
  })();

  return true;
});
