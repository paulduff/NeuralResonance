(function () {
  function normalize(text) {
    return String(text || '').replace(/\s+/g, ' ').trim();
  }

  function isVisible(el) {
    if (!el) return false;
    const style = window.getComputedStyle(el);
    return style && style.display !== 'none' && style.visibility !== 'hidden' && Number(style.opacity || 1) > 0;
  }

  function collectTrackText() {
    const chunks = [];
    const videos = document.querySelectorAll('video');
    for (const video of videos) {
      const tracks = video.textTracks;
      if (!tracks) continue;

      for (let i = 0; i < tracks.length; i++) {
        const tr = tracks[i];
        const cues = tr && tr.activeCues ? tr.activeCues : null;
        if (!cues) continue;

        for (let j = 0; j < cues.length; j++) {
          const cue = cues[j];
          if (!cue) continue;
          const t = normalize(cue.text || cue.id || '');
          if (t.length >= 2) chunks.push(t);
        }
      }
    }
    return chunks;
  }

  function collectDomCaptionText() {
    const selectors = [
      '[aria-live="polite"]',
      '[aria-live="assertive"]',
      '[role="status"]',
      '[role="alert"]',
      '.ytp-caption-segment',
      '.caption-window',
      '.captions-text',
      '[class*="caption"]',
      '[class*="subtitle"]',
      '[id*="caption"]',
      '[id*="subtitle"]'
    ];

    const nodes = document.querySelectorAll(selectors.join(','));
    const chunks = [];
    let count = 0;
    for (const node of nodes) {
      if (++count > 100) break;
      if (!isVisible(node)) continue;

      const t = normalize(node.innerText || node.textContent || '');
      if (t.length >= 2) chunks.push(t);
    }
    return chunks;
  }

  function splitPhrases(text) {
    return normalize(text)
      .split(/[.!?;:\n]+|\s[-\u2013\u2014]\s/g)
      .map((s) => normalize(s).replace(/^[-,.;:!?]+/, '').replace(/[-,.;:!?]+$/, ''))
      .filter((s) => s.length >= 6);
  }

  function buildCandidate() {
    const bucket = new Set();

    for (const t of collectTrackText()) {
      for (const p of splitPhrases(t)) bucket.add(p);
    }
    for (const t of collectDomCaptionText()) {
      for (const p of splitPhrases(t)) bucket.add(p);
    }

    const arr = Array.from(bucket)
      .map(normalize)
      .filter((t) => t.length >= 6)
      .sort((a, b) => b.length - a.length)
      .slice(0, 5);

    return normalize(arr.join(' ')).slice(0, 260);
  }

  let lastText = '';
  let lastSentAt = 0;

  function maybeSend() {
    const text = buildCandidate();
    if (!text || text.length < 4) return;

    const now = Date.now();
    if (text === lastText && (now - lastSentAt) < 3500) return;
    if ((now - lastSentAt) < 650) return;

    lastText = text;
    lastSentAt = now;

    try {
      chrome.runtime.sendMessage({
        target: 'background',
        type: 'CONTENT_TEXT',
        text,
        pageUrl: location.href
      });
    } catch {
      // ignore
    }
  }

  const intervalMs = 800;
  setInterval(maybeSend, intervalMs);
})();
