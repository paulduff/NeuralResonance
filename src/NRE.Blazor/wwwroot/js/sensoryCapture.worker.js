// sensoryCapture.worker.js (module)
// Receives ImageBitmap frames, downsamples, computes simple retinal/V1-ish features,
// and emits compact parameters for the engine.
//
// Output message:
//  { type:'visualFeatures', intensity01, speedHz, spatialFreq }

let cfg = {
  width: 96,
  height: 96,
  maxSpeedHz: 6.0,
  maxSpatialFreq: 2.0,
  intensityGain: 1.25,
  motionGain: 2.25,
  edgeGain: 2.0
};

let canvas = null;
let ctx = null;
let prevGray = null;
let lastAt = 0;

function clamp(v, lo, hi) { return Math.min(hi, Math.max(lo, v)); }

function ensureCanvas() {
  if (!canvas) {
    canvas = new OffscreenCanvas(cfg.width, cfg.height);
    ctx = canvas.getContext('2d', { willReadFrequently: true });
  } else if (canvas.width !== cfg.width || canvas.height !== cfg.height) {
    canvas.width = cfg.width;
    canvas.height = cfg.height;
  }
}

// intensity: mean luminance (0..1)
// motion: mean abs delta vs previous frame (0..1)
// edge: mean abs gradient (0..1-ish)
function computeFeatures(imgData, w, h) {
  const data = imgData.data;
  const gray = new Float32Array(w * h);

  let sum = 0;
  for (let i = 0, p = 0; i < data.length; i += 4, p++) {
    const g = (0.299 * data[i] + 0.587 * data[i + 1] + 0.114 * data[i + 2]) / 255.0;
    gray[p] = g;
    sum += g;
  }

  const intensity01 = sum / (w * h);

  let motion01 = 0;
  if (prevGray) {
    let dsum = 0;
    for (let i = 0; i < gray.length; i++) dsum += Math.abs(gray[i] - prevGray[i]);
    motion01 = dsum / gray.length;
  }

  let esum = 0;
  for (let y = 1; y < h - 1; y++) {
    for (let x = 1; x < w - 1; x++) {
      const gx = gray[y * w + (x + 1)] - gray[y * w + (x - 1)];
      const gy = gray[(y + 1) * w + x] - gray[(y - 1) * w + x];
      esum += Math.abs(gx) + Math.abs(gy);
    }
  }
  const edge01 = clamp(esum / ((w - 2) * (h - 2) * 2.0), 0, 1);

  prevGray = gray;
  return { intensity01, motion01, edge01 };
}

self.onmessage = (ev) => {
  const msg = ev.data || {};

  if (msg.type === 'configureVisual') {
    cfg = Object.assign(cfg, msg);
    ensureCanvas();
    prevGray = null;
    return;
  }

  if (msg.type === 'visualFrame') {
    const bitmap = msg.bitmap;
    if (!bitmap) return;

    // Optional: throttle worker compute if frames arrive too fast.
    const now = performance.now();
    if (now - lastAt < 30) { // ~33 fps max compute
      try { bitmap.close(); } catch { }
      return;
    }
    lastAt = now;

    ensureCanvas();

    ctx.drawImage(bitmap, 0, 0, cfg.width, cfg.height);
    try { bitmap.close(); } catch { }

    const imgData = ctx.getImageData(0, 0, cfg.width, cfg.height);
    const f = computeFeatures(imgData, cfg.width, cfg.height);

    const intensity01 = clamp(f.intensity01 * cfg.intensityGain, 0, 1);
    const speedHz = clamp(f.motion01 * cfg.motionGain * cfg.maxSpeedHz, 0, cfg.maxSpeedHz);
    const spatialFreq = clamp(0.05 + (f.edge01 * cfg.edgeGain * cfg.maxSpatialFreq), 0.01, cfg.maxSpatialFreq);

    self.postMessage({
      type: 'visualFeatures',
      intensity01,
      speedHz,
      spatialFreq
    });
  }
};
