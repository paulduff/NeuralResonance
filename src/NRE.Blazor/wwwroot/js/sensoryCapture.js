// sensoryCapture.js
// Browser-side webcam + microphone capture with background processing.
// - Webcam processing is offloaded to a Web Worker (image -> features).
// - Microphone feature extraction is offloaded to an AudioWorklet (audio -> RMS + pitch-ish).
//
// We POST compact features to the NRE.Api endpoints:
//   POST {apiBase}api/engine/visual    { intensity01, speedHz, spatialFreq }
//   POST {apiBase}api/engine/auditory  { intensity01, toneHz }
//
// NOTE: getUserMedia must be called on the main thread; we only offload processing.

(function (global) {
  const SensoryCapture = {};

  function clamp(v, lo, hi) { return Math.min(hi, Math.max(lo, v)); }

  function postJson(url, obj) {
    return fetch(url, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(obj)
    });
  }

  // -----------------------------------------
  // Combined sensory post loop
  // -----------------------------------------
  let sensoryTimer = null;
  let sensoryApiBase = null;
  let sensoryLastPostAt = 0;
  let sensoryCfg = { fps: 10 };
  let sensoryInFlight = false;
  let latestVisual = { enabled: false, intensity01: 0, speedHz: 0, spatialFreq: 0.1 };
  let latestAuditory = { enabled: false, intensity01: 0, toneHz: 220 };

  function ensureSensoryTimer() {
    if (sensoryTimer) return;

    const intervalMs = Math.max(80, Math.floor(1000 / (sensoryCfg.fps || 10)));
    sensoryTimer = setInterval(async () => {
      try {
        if (!sensoryApiBase) return;
        if (!latestVisual.enabled && !latestAuditory.enabled) return;

        const now = performance.now();
        if (now - sensoryLastPostAt < intervalMs - 2) return;
        sensoryLastPostAt = now;

        if (sensoryInFlight) return;
        sensoryInFlight = true;
        try {
          await postJson(sensoryApiBase + 'api/engine/sensory', {
            visual: latestVisual,
            auditory: latestAuditory
          });
        } finally {
          sensoryInFlight = false;
        }
      } catch {
        // swallow
      }
    }, intervalMs);
  }

  function stopSensoryTimerIfIdle() {
    if (latestVisual.enabled || latestAuditory.enabled) return;
    if (sensoryTimer) {
      clearInterval(sensoryTimer);
      sensoryTimer = null;
    }
    sensoryLastPostAt = 0;
    sensoryInFlight = false;
  }

  // -----------------------------------------
  // Webcam (Visual) - main thread + worker
  // -----------------------------------------
  let camStream = null;
  let camVideo = null;
  let camTimer = null;
  let camWorker = null;
  let camApiBase = null;
  // Posting is done by the combined sensory timer.

  // We still need a <video> element on the main thread to receive the stream.
  async function ensureHiddenVideo() {
    if (camVideo) return;
    camVideo = document.createElement('video');
    camVideo.playsInline = true;
    camVideo.muted = true;
    camVideo.autoplay = true;
    camVideo.style.display = 'none';
    document.body.appendChild(camVideo);
  }

  function ensureCamWorker() {
    if (camWorker) return;

    camWorker = new Worker('js/sensoryCapture.worker.js', { type: 'module' });

    camWorker.onmessage = (ev) => {
      const msg = ev.data || {};
      if (msg.type !== 'visualFeatures') return;

      latestVisual.enabled = true;
      latestVisual.intensity01 = msg.intensity01;
      latestVisual.speedHz = msg.speedHz;
      latestVisual.spatialFreq = msg.spatialFreq;
    };
  }

  SensoryCapture.startWebcam = async function (apiBase, opts) {
    if (camTimer) return;

    camApiBase = apiBase || '';
    sensoryApiBase = camApiBase;

    const cfg = Object.assign({
      fps: 10,
      // worker downscale
      width: 96,
      height: 96,

      // mapping (keep aligned with earlier)
      maxSpeedHz: 6.0,
      maxSpatialFreq: 2.0,
      intensityGain: 1.25,
      motionGain: 2.25,
      edgeGain: 2.0
    }, opts || {});

    await ensureHiddenVideo();
    ensureCamWorker();

    camStream = await navigator.mediaDevices.getUserMedia({
      video: {
        width: { ideal: 640 },
        height: { ideal: 480 },
        facingMode: 'user'
      },
      audio: false
    });

    camVideo.srcObject = camStream;
    await camVideo.play();

    // Inform worker of config
    camWorker.postMessage({
      type: 'configureVisual',
      width: cfg.width,
      height: cfg.height,
      maxSpeedHz: cfg.maxSpeedHz,
      maxSpatialFreq: cfg.maxSpatialFreq,
      intensityGain: cfg.intensityGain,
      motionGain: cfg.motionGain,
      edgeGain: cfg.edgeGain
    });

    const intervalMs = Math.max(66, Math.floor(1000 / cfg.fps));

    sensoryCfg.fps = cfg.fps;
    ensureSensoryTimer();

    camTimer = setInterval(async () => {
      try {
        if (!camVideo || camVideo.readyState < 2) return;

        // Create a *small* bitmap to avoid resizing huge frames in the worker.
        const bitmap = await createImageBitmap(camVideo, {
          resizeWidth: cfg.width,
          resizeHeight: cfg.height,
          resizeQuality: 'low'
        });

        // Transfer the bitmap to the worker (zero-copy transfer in most browsers)
        camWorker.postMessage({ type: 'visualFrame', bitmap }, [bitmap]);
      } catch {
        // swallow
      }
    }, intervalMs);
  };

  SensoryCapture.stopWebcam = function () {
    if (camTimer) {
      clearInterval(camTimer);
      camTimer = null;
    }
    if (camStream) {
      camStream.getTracks().forEach(t => { try { t.stop(); } catch { } });
      camStream = null;
    }
    if (camWorker) {
      try { camWorker.terminate(); } catch { }
      camWorker = null;
    }
    latestVisual.enabled = false;
    stopSensoryTimerIfIdle();
  };

  // -----------------------------------------
  // Microphone (Auditory) - AudioWorklet
  // -----------------------------------------
  let micStream = null;
  let micAudioCtx = null;
  let micSource = null;
  let micNode = null; // AudioWorkletNode
  let micApiBase = null;
  let micLast = { intensity01: 0, toneHz: 220 };

  SensoryCapture.startMic = async function (apiBase, opts) {
    if (micNode) return;

    micApiBase = apiBase || '';
    sensoryApiBase = micApiBase;

    const cfg = Object.assign({
      fps: 10,           // how often we POST to server (combined)
      intensityGain: 3.0,
      maxToneHz: 2000
    }, opts || {});

    micStream = await navigator.mediaDevices.getUserMedia({
      audio: {
        echoCancellation: true,
        noiseSuppression: true,
        autoGainControl: true
      },
      video: false
    });

    micAudioCtx = new (window.AudioContext || window.webkitAudioContext)();

    // Load worklet module
    await micAudioCtx.audioWorklet.addModule('js/audioFeatures.worklet.js');

    micSource = micAudioCtx.createMediaStreamSource(micStream);

    micNode = new AudioWorkletNode(micAudioCtx, 'audio-features', {
      numberOfInputs: 1,
      numberOfOutputs: 0,
      processorOptions: {
        intensityGain: cfg.intensityGain,
        maxToneHz: cfg.maxToneHz
      }
    });

    micNode.port.onmessage = (ev) => {
      const msg = ev.data || {};
      if (msg.type !== 'audioFeatures') return;

      micLast.intensity01 = msg.intensity01;
      micLast.toneHz = msg.toneHz;

      latestAuditory.enabled = true;
      latestAuditory.intensity01 = micLast.intensity01;
      latestAuditory.toneHz = micLast.toneHz;
    };

    // Connect input -> worklet (no outputs)
    micSource.connect(micNode);

    sensoryCfg.fps = cfg.fps;
    ensureSensoryTimer();
  };

  SensoryCapture.stopMic = function () {
    if (micNode) {
      try { micNode.disconnect(); } catch { }
      try { micNode.port.close(); } catch { }
      micNode = null;
    }
    if (micSource) {
      try { micSource.disconnect(); } catch { }
      micSource = null;
    }
    if (micAudioCtx) {
      try { micAudioCtx.close(); } catch { }
      micAudioCtx = null;
    }
    if (micStream) {
      micStream.getTracks().forEach(t => { try { t.stop(); } catch { } });
      micStream = null;
    }
    micLast = { intensity01: 0, toneHz: 220 };
    latestAuditory.enabled = false;
    stopSensoryTimerIfIdle();
  };

  // -----------------------------------------
  // Internet stream (Visual + Auditory)
  // -----------------------------------------
  let netVideo = null;
  let netTimer = null;
  let netWorker = null;
  let netAudioCtx = null;
  let netSource = null;
  let netNode = null;
  let netGain = null;
  let netApiBase = null;
  let netRunning = false;

  function ensureHiddenNetVideo() {
    if (netVideo) return;
    netVideo = document.createElement('video');
    netVideo.playsInline = true;
    netVideo.autoplay = true;
    netVideo.style.display = 'none';
    document.body.appendChild(netVideo);
  }

  function ensureNetWorker() {
    if (netWorker) return;

    netWorker = new Worker('js/sensoryCapture.worker.js', { type: 'module' });
    netWorker.onmessage = (ev) => {
      const msg = ev.data || {};
      if (msg.type !== 'visualFeatures') return;

      latestVisual.enabled = true;
      latestVisual.intensity01 = msg.intensity01;
      latestVisual.speedHz = msg.speedHz;
      latestVisual.spatialFreq = msg.spatialFreq;
    };
  }

  function stopNetVisual() {
    if (netTimer) {
      clearInterval(netTimer);
      netTimer = null;
    }
    if (netWorker) {
      try { netWorker.terminate(); } catch { }
      netWorker = null;
    }
  }

  function stopNetAudio() {
    if (netNode) {
      try { netNode.disconnect(); } catch { }
      try { netNode.port.close(); } catch { }
      netNode = null;
    }
    if (netSource) {
      try { netSource.disconnect(); } catch { }
      netSource = null;
    }
    if (netGain) {
      try { netGain.disconnect(); } catch { }
      netGain = null;
    }
    if (netAudioCtx) {
      try { netAudioCtx.close(); } catch { }
      netAudioCtx = null;
    }
  }

  SensoryCapture.startInternetStream = async function (apiBase, streamUrl, opts) {
    if (netRunning) return;

    const url = String(streamUrl || '').trim();
    if (!url) throw new Error('Internet stream URL is required.');

    // Keep one active source class to avoid feature contention.
    if (camTimer) SensoryCapture.stopWebcam();
    if (micNode) SensoryCapture.stopMic();

    netApiBase = apiBase || '';
    sensoryApiBase = netApiBase;

    const cfg = Object.assign({
      fps: 10,
      width: 96,
      height: 96,
      enableVisual: true,
      enableAudio: true,
      allowAudioPlayback: false,
      crossOrigin: 'anonymous',
      maxSpeedHz: 6.0,
      maxSpatialFreq: 2.0,
      intensityGain: 1.25,
      motionGain: 2.25,
      edgeGain: 2.0,
      audioIntensityGain: 3.0,
      maxToneHz: 2000
    }, opts || {});

    try {
      await ensureHiddenNetVideo();

    netVideo.crossOrigin = cfg.crossOrigin;
    netVideo.loop = false;
    netVideo.muted = !cfg.allowAudioPlayback;
    netVideo.srcObject = null;
    netVideo.src = url;

    // User gesture comes from the button click; this should satisfy autoplay policies.
    await netVideo.play();

    if (cfg.enableVisual) {
      ensureNetWorker();

      netWorker.postMessage({
        type: 'configureVisual',
        width: cfg.width,
        height: cfg.height,
        maxSpeedHz: cfg.maxSpeedHz,
        maxSpatialFreq: cfg.maxSpatialFreq,
        intensityGain: cfg.intensityGain,
        motionGain: cfg.motionGain,
        edgeGain: cfg.edgeGain
      });

      const intervalMs = Math.max(66, Math.floor(1000 / cfg.fps));
      netTimer = setInterval(async () => {
        try {
          if (!netVideo || netVideo.readyState < 2 || netVideo.videoWidth < 2 || netVideo.videoHeight < 2) return;

          const bitmap = await createImageBitmap(netVideo, {
            resizeWidth: cfg.width,
            resizeHeight: cfg.height,
            resizeQuality: 'low'
          });

          netWorker.postMessage({ type: 'visualFrame', bitmap }, [bitmap]);
        } catch {
          // swallow: common for cross-origin streams without CORS for pixel access.
        }
      }, intervalMs);
    }

    if (cfg.enableAudio) {
      netAudioCtx = new (window.AudioContext || window.webkitAudioContext)();
      await netAudioCtx.audioWorklet.addModule('js/audioFeatures.worklet.js');

      netSource = netAudioCtx.createMediaElementSource(netVideo);
      netNode = new AudioWorkletNode(netAudioCtx, 'audio-features', {
        numberOfInputs: 1,
        numberOfOutputs: 0,
        processorOptions: {
          intensityGain: cfg.audioIntensityGain,
          maxToneHz: cfg.maxToneHz
        }
      });

      netNode.port.onmessage = (ev) => {
        const msg = ev.data || {};
        if (msg.type !== 'audioFeatures') return;

        latestAuditory.enabled = true;
        latestAuditory.intensity01 = msg.intensity01;
        latestAuditory.toneHz = msg.toneHz;
      };

      netGain = netAudioCtx.createGain();
      netGain.gain.value = cfg.allowAudioPlayback ? 1.0 : 0.0;

      // Keep the graph alive while optionally muting user playback.
      netSource.connect(netNode);
      netSource.connect(netGain);
      netGain.connect(netAudioCtx.destination);

      try { await netAudioCtx.resume(); } catch { }
    }

    sensoryCfg.fps = cfg.fps;
    ensureSensoryTimer();
    netRunning = true;
    } catch (err) {
      SensoryCapture.stopInternetStream();
      throw err;
    }
  };

  SensoryCapture.stopInternetStream = function () {
    stopNetVisual();
    stopNetAudio();

    if (netVideo) {
      try { netVideo.pause(); } catch { }
      try { netVideo.removeAttribute('src'); } catch { }
      try { netVideo.load(); } catch { }
    }

    netRunning = false;

    // Only disable channels if no other local source is active.
    if (!camTimer) latestVisual.enabled = false;
    if (!micNode) latestAuditory.enabled = false;

    stopSensoryTimerIfIdle();
  };
  global.SensoryCapture = SensoryCapture;
})(window);
