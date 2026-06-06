// Lightweight bootstrap for Blazor JS interop.
// Purpose: make init resilient in ServerPrerendered mode and avoid race conditions
// where the Blazor circuit starts before neuralRenderer.js is available.

(function () {
  function sleep(ms) { return new Promise(r => setTimeout(r, ms)); }

  window.NreInterop = window.NreInterop || {};

  /**
   * Waits for window.NeuralRenderer.initSafe and then calls it.
   * This prevents "NeuralRenderer was undefined" errors caused by timing/caching.
   */
  window.NreInterop.initNeuralRendererSafe = async function (canvasId, volumeW, volumeH, volumeD, options) {
    const t0 = (typeof performance !== "undefined" && performance.now) ? performance.now() : Date.now();
    const timeoutMs = 8000;
    while (!window.NeuralRenderer || typeof window.NeuralRenderer.initSafe !== "function") {
      const t = (typeof performance !== "undefined" && performance.now) ? performance.now() : Date.now();
      if ((t - t0) > timeoutMs) {
        throw new Error("NeuralRenderer not loaded. Check _Host.cshtml script tags and browser console for 404/syntax errors.");
      }
      await sleep(25);
    }
    return window.NeuralRenderer.initSafe(canvasId, volumeW, volumeH, volumeD, options);
  };
})();
