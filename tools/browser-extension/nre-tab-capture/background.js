const OFFSCREEN_PATH = 'offscreen.html';
const OFFSCREEN_URL = chrome.runtime.getURL(OFFSCREEN_PATH);

const defaultSettings = {
  apiBase: 'http://localhost:5005/',
  fps: 6,
  width: 96,
  height: 96,
  enableVisual: true,
  enableAudio: true,
  allowAudioPlayback: false,
  enableText: true
};

let lastKnownState = {
  running: false,
  tabId: null,
  startedAtUtc: null,
  lastError: null,
  lastPostUtc: null,
  health: {
    visual: { state: 'idle' },
    audio: { state: 'idle' },
    post: { state: 'idle' },
    text: { state: 'idle' }
  }
};

let activeCaptureConfig = { ...defaultSettings };
let textRelay = {
  lastSeenAt: 0,
  lastAcceptedAt: 0,
  lastSentAt: 0,
  lastSentText: '',
  lastError: null,
  recent: []
};

chrome.runtime.onInstalled.addListener(async () => {
  const current = await chrome.storage.local.get(Object.keys(defaultSettings));
  const missing = {};
  for (const [key, value] of Object.entries(defaultSettings)) {
    if (typeof current[key] === 'undefined') missing[key] = value;
  }
  if (Object.keys(missing).length > 0) {
    await chrome.storage.local.set(missing);
  }
});

function normalizeApiBase(apiBase) {
  const s = String(apiBase || '').trim();
  if (!s) return defaultSettings.apiBase;
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

function normalizeText(raw) {
  return String(raw || '')
    .replace(/https?:\/\/\S+/gi, ' ')
    .replace(/[\u2018\u2019]/g, "'")
    .replace(/[\u201C\u201D]/g, '"')
    .replace(/\s+/g, ' ')
    .trim();
}

function cleanPhrase(raw) {
  return normalizeText(raw)
    .replace(/^[-,.;:!?]+/, '')
    .replace(/[-,.;:!?]+$/, '')
    .trim();
}

function phraseScore(text) {
  if (!text) return 0;
  const len = text.length;
  const letters = (text.match(/[A-Za-z]/g) || []).length;
  const words = text.split(/\s+/).filter(Boolean).length;
  const alphaRatio = letters / Math.max(1, len);
  const wordScore = Math.min(12, words) / 12;
  const lenScore = Math.min(1, len / 90);
  return alphaRatio * 0.45 + wordScore * 0.35 + lenScore * 0.20;
}

function pickBestPhrase(raw) {
  const normalized = normalizeText(raw);
  if (!normalized) return '';

  const pieces = normalized
    .split(/[.!?;:\n]+|\s[-\u2013\u2014]\s/g)
    .map(cleanPhrase)
    .filter((s) => s.length >= 8);

  const candidates = (pieces.length > 0 ? pieces : [normalized])
    .map((p) => p.slice(0, 180));

  let best = '';
  let bestScore = -1;
  for (const cand of candidates) {
    if (cand.length < 12) continue;
    const score = phraseScore(cand);
    if (score > bestScore) {
      bestScore = score;
      best = cand;
    }
  }

  return best;
}

function tokenize(text) {
  return text
    .toLowerCase()
    .replace(/[^a-z0-9\s]/g, ' ')
    .split(/\s+/)
    .filter((w) => w.length >= 2);
}

function jaccard(tokensA, tokensB) {
  const a = new Set(tokensA);
  const b = new Set(tokensB);
  if (a.size === 0 || b.size === 0) return 0;

  let inter = 0;
  for (const t of a) {
    if (b.has(t)) inter++;
  }
  const union = a.size + b.size - inter;
  return union > 0 ? inter / union : 0;
}

function isNearDuplicate(text, nowMs) {
  const tokens = tokenize(text);

  for (const item of textRelay.recent) {
    const dt = nowMs - item.at;
    if (dt > 45000) continue;

    if (item.text === text && dt < 12000) return true;

    const sim = jaccard(tokens, item.tokens);
    if (sim >= 0.88 && dt < 20000) return true;
  }

  return false;
}

function rememberPhrase(text, nowMs) {
  textRelay.recent.push({
    text,
    at: nowMs,
    tokens: tokenize(text)
  });

  if (textRelay.recent.length > 16) {
    textRelay.recent.splice(0, textRelay.recent.length - 16);
  }
}

function computeTextHealth() {
  if (!lastKnownState.running) return { state: 'idle' };
  if (!activeCaptureConfig.enableText) return { state: 'disabled' };
  if (textRelay.lastError) return { state: 'error', detail: textRelay.lastError };

  const seenAge = Date.now() - textRelay.lastSeenAt;
  const sentAge = Date.now() - textRelay.lastAcceptedAt;

  if (!textRelay.lastSeenAt) return { state: 'warming' };
  if (seenAge <= 5000) {
    if (textRelay.lastAcceptedAt && sentAge <= 8000) return { state: 'ok' };
    return { state: 'warming', detail: 'capturing text' };
  }
  return { state: 'stale', detail: 'no recent text' };
}

function mergeHealth(offscreenHealth) {
  const safeOff = offscreenHealth && typeof offscreenHealth === 'object' ? offscreenHealth : {};
  return {
    visual: safeOff.visual || { state: lastKnownState.running ? 'warming' : 'idle' },
    audio: safeOff.audio || { state: lastKnownState.running ? 'warming' : 'idle' },
    post: safeOff.post || { state: lastKnownState.running ? 'warming' : 'idle' },
    text: computeTextHealth()
  };
}

async function hasOffscreenDocument() {
  if (!chrome.runtime.getContexts) return false;
  const contexts = await chrome.runtime.getContexts({
    contextTypes: ['OFFSCREEN_DOCUMENT'],
    documentUrls: [OFFSCREEN_URL]
  });
  return contexts.length > 0;
}

async function ensureOffscreenDocument() {
  if (await hasOffscreenDocument()) return;
  await chrome.offscreen.createDocument({
    url: OFFSCREEN_PATH,
    reasons: ['USER_MEDIA'],
    justification: 'Capture active tab media and extract compact visual/audio features for NRE stimuli.'
  });
}

async function sendToOffscreen(message) {
  await ensureOffscreenDocument();
  return chrome.runtime.sendMessage({ target: 'offscreen', ...message });
}

async function getActiveTab() {
  const tabs = await chrome.tabs.query({ active: true, currentWindow: true });
  return tabs && tabs.length > 0 ? tabs[0] : null;
}

async function startCapture(settings) {
  const tab = await getActiveTab();
  if (!tab || typeof tab.id !== 'number') {
    throw new Error('No active tab found to capture.');
  }

  const merged = { ...defaultSettings, ...(settings || {}) };
  merged.apiBase = normalizeApiBase(merged.apiBase);
  activeCaptureConfig = merged;

  const streamId = await chrome.tabCapture.getMediaStreamId({ targetTabId: tab.id });
  await sendToOffscreen({
    type: 'OFFSCREEN_START',
    streamId,
    tabId: tab.id,
    config: merged
  });

  textRelay = {
    lastSeenAt: 0,
    lastAcceptedAt: 0,
    lastSentAt: 0,
    lastSentText: '',
    lastError: null,
    recent: []
  };

  lastKnownState = {
    running: true,
    tabId: tab.id,
    startedAtUtc: isoNow(),
    lastError: null,
    lastPostUtc: null,
    health: {
      visual: { state: merged.enableVisual ? 'warming' : 'disabled' },
      audio: { state: merged.enableAudio ? 'warming' : 'disabled' },
      post: { state: 'warming' },
      text: merged.enableText ? { state: 'warming' } : { state: 'disabled' }
    }
  };

  return { ok: true, message: `Capturing tab ${tab.id}`, state: lastKnownState };
}

async function stopCapture() {
  try {
    await sendToOffscreen({ type: 'OFFSCREEN_STOP' });
  } catch {
    // If offscreen doc is already gone, we still consider stop successful.
  }

  lastKnownState = {
    running: false,
    tabId: null,
    startedAtUtc: null,
    lastError: null,
    lastPostUtc: null,
    health: {
      visual: { state: 'idle' },
      audio: { state: 'idle' },
      post: { state: 'idle' },
      text: { state: 'idle' }
    }
  };

  activeCaptureConfig = { ...defaultSettings };
  textRelay = {
    lastSeenAt: 0,
    lastAcceptedAt: 0,
    lastSentAt: 0,
    lastSentText: '',
    lastError: null,
    recent: []
  };

  return { ok: true, message: 'Capture stopped', state: lastKnownState };
}

async function getStatus() {
  try {
    if (await hasOffscreenDocument()) {
      const status = await chrome.runtime.sendMessage({ target: 'offscreen', type: 'OFFSCREEN_STATUS' });
      if (status && typeof status === 'object') {
        lastKnownState = {
          ...lastKnownState,
          running: !!status.running,
          tabId: typeof status.tabId === 'number' ? status.tabId : lastKnownState.tabId,
          lastError: status.lastError || null,
          lastPostUtc: status.lastPostUtc || null,
          health: mergeHealth(status.health)
        };
      }
    } else {
      lastKnownState.health = mergeHealth(null);
    }
  } catch {
    lastKnownState.health = mergeHealth(null);
  }

  return { ok: true, state: lastKnownState };
}

async function relayHeardText(rawText, pageUrl, senderTabId) {
  const now = Date.now();
  textRelay.lastSeenAt = now;

  if (!lastKnownState.running) return;
  if (typeof senderTabId !== 'number' || senderTabId !== lastKnownState.tabId) return;
  if (!activeCaptureConfig.enableText) return;

  const phrase = pickBestPhrase(rawText);
  if (!phrase || phrase.length < 12) return;

  if (phrase === textRelay.lastSentText && (now - textRelay.lastSentAt) < 12000) return;
  if ((now - textRelay.lastSentAt) < 850) return;
  if (isNearDuplicate(phrase, now)) return;

  textRelay.lastSentText = phrase;
  textRelay.lastSentAt = now;
  textRelay.lastError = null;

  try {
    const apiBase = normalizeApiBase(activeCaptureConfig.apiBase);
    await fetch(apiBase + 'api/engine/voice/reafferent', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        text: phrase,
        rate: 1.0,
        pitch: 1.0,
        volume: 0.55,
        holdSeconds: 0.75,
        gloss: pageUrl ? `tab:${String(pageUrl).slice(0, 120)}` : null
      })
    });

    textRelay.lastAcceptedAt = now;
    rememberPhrase(phrase, now);
  } catch (err) {
    textRelay.lastError = err && err.message ? err.message : String(err);
    lastKnownState.lastError = textRelay.lastError;
  }
}

chrome.runtime.onMessage.addListener((msg, sender, sendResponse) => {
  if (!msg || msg.target !== 'background') return;

  (async () => {
    try {
      switch (msg.type) {
        case 'POPUP_START': {
          const result = await startCapture(msg.settings || {});
          sendResponse(result);
          return;
        }
        case 'POPUP_STOP': {
          const result = await stopCapture();
          sendResponse(result);
          return;
        }
        case 'POPUP_STATUS': {
          const result = await getStatus();
          sendResponse(result);
          return;
        }
        case 'CONTENT_TEXT': {
          await relayHeardText(msg.text, msg.pageUrl, sender && sender.tab ? sender.tab.id : null);
          sendResponse({ ok: true });
          return;
        }
        default:
          sendResponse({ ok: false, error: `Unknown message type: ${msg.type}` });
      }
    } catch (err) {
      const message = err && err.message ? err.message : String(err);
      lastKnownState.lastError = message;
      sendResponse({ ok: false, error: message, state: lastKnownState });
    }
  })();

  return true;
});
