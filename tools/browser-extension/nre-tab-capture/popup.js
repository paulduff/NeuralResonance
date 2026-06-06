const ids = {
  apiBase: document.getElementById('apiBase'),
  fps: document.getElementById('fps'),
  width: document.getElementById('width'),
  height: document.getElementById('height'),
  enableVisual: document.getElementById('enableVisual'),
  enableAudio: document.getElementById('enableAudio'),
  allowAudioPlayback: document.getElementById('allowAudioPlayback'),
  enableText: document.getElementById('enableText'),
  startBtn: document.getElementById('startBtn'),
  stopBtn: document.getElementById('stopBtn'),
  status: document.getElementById('status'),
  healthVisual: document.getElementById('healthVisual'),
  healthAudio: document.getElementById('healthAudio'),
  healthText: document.getElementById('healthText'),
  healthPost: document.getElementById('healthPost')
};

const defaults = {
  apiBase: 'http://localhost:5005/',
  fps: 6,
  width: 96,
  height: 96,
  enableVisual: true,
  enableAudio: true,
  allowAudioPlayback: false,
  enableText: true
};

const healthMeta = {
  visual: { el: ids.healthVisual, label: 'Visual' },
  audio: { el: ids.healthAudio, label: 'Audio' },
  text: { el: ids.healthText, label: 'Text' },
  post: { el: ids.healthPost, label: 'Post' }
};

const healthLabel = {
  ok: 'OK',
  warming: 'Warm',
  stale: 'Stale',
  error: 'Error',
  disabled: 'Off',
  idle: 'Idle'
};

function statusText(text, isError = false) {
  ids.status.textContent = text;
  ids.status.style.color = isError ? '#ffb3b3' : '#b7f5d5';
}

function collectSettings() {
  return {
    apiBase: String(ids.apiBase.value || '').trim() || defaults.apiBase,
    fps: Math.max(1, Math.min(30, Number(ids.fps.value) || defaults.fps)),
    width: Math.max(32, Math.min(256, Number(ids.width.value) || defaults.width)),
    height: Math.max(32, Math.min(256, Number(ids.height.value) || defaults.height)),
    enableVisual: !!ids.enableVisual.checked,
    enableAudio: !!ids.enableAudio.checked,
    allowAudioPlayback: !!ids.allowAudioPlayback.checked,
    enableText: !!ids.enableText.checked
  };
}

function applySettings(s) {
  ids.apiBase.value = s.apiBase;
  ids.fps.value = s.fps;
  ids.width.value = s.width;
  ids.height.value = s.height;
  ids.enableVisual.checked = !!s.enableVisual;
  ids.enableAudio.checked = !!s.enableAudio;
  ids.allowAudioPlayback.checked = !!s.allowAudioPlayback;
  ids.enableText.checked = !!s.enableText;
}

async function saveSettings(s) {
  await chrome.storage.local.set(s);
}

async function loadSettings() {
  const s = await chrome.storage.local.get(Object.keys(defaults));
  applySettings({ ...defaults, ...s });
}

async function send(message) {
  return chrome.runtime.sendMessage({ target: 'background', ...message });
}

function normalizeHealthState(info, fallbackState) {
  const state = (info && typeof info.state === 'string') ? info.state.toLowerCase() : fallbackState;
  if (healthLabel[state]) return state;
  return fallbackState;
}

function setHealthBadge(channel, info, fallbackState) {
  const meta = healthMeta[channel];
  if (!meta || !meta.el) return;

  const state = normalizeHealthState(info, fallbackState);
  const label = healthLabel[state] || state;

  meta.el.className = `health-badge state-${state}`;
  meta.el.textContent = `${meta.label}: ${label}`;
  meta.el.title = info && info.detail ? String(info.detail) : '';
}

function renderHealth(health, isRunning) {
  const safe = (health && typeof health === 'object') ? health : {};
  const fallback = isRunning ? 'warming' : 'idle';

  setHealthBadge('visual', safe.visual, fallback);
  setHealthBadge('audio', safe.audio, fallback);
  setHealthBadge('text', safe.text, fallback);
  setHealthBadge('post', safe.post, fallback);
}

async function refreshStatus() {
  try {
    const res = await send({ type: 'POPUP_STATUS' });
    if (!res || !res.ok) {
      statusText(res && res.error ? res.error : 'Status unavailable', true);
      renderHealth(null, false);
      return;
    }

    const st = res.state || {};
    renderHealth(st.health, !!st.running);

    if (st.running) {
      if (st.lastError) {
        statusText(`Running with warning: ${st.lastError}`, true);
      } else {
        statusText(`Running on tab ${st.tabId ?? '?'}`);
      }
    } else {
      statusText(st.lastError ? `Idle (last error: ${st.lastError})` : 'Idle');
    }
  } catch (err) {
    statusText(err && err.message ? err.message : String(err), true);
    renderHealth({
      visual: { state: 'error', detail: String(err) },
      audio: { state: 'error', detail: String(err) },
      text: { state: 'error', detail: String(err) },
      post: { state: 'error', detail: String(err) }
    }, true);
  }
}

ids.startBtn.addEventListener('click', async () => {
  const settings = collectSettings();
  await saveSettings(settings);
  statusText('Starting capture...');

  try {
    const res = await send({ type: 'POPUP_START', settings });
    if (!res || !res.ok) {
      statusText(res && res.error ? res.error : 'Failed to start capture', true);
      return;
    }
    statusText(res.message || 'Capture started');
  } catch (err) {
    statusText(err && err.message ? err.message : String(err), true);
  }

  await refreshStatus();
});

ids.stopBtn.addEventListener('click', async () => {
  statusText('Stopping capture...');
  try {
    const res = await send({ type: 'POPUP_STOP' });
    if (!res || !res.ok) {
      statusText(res && res.error ? res.error : 'Failed to stop capture', true);
      return;
    }
    statusText('Capture stopped');
  } catch (err) {
    statusText(err && err.message ? err.message : String(err), true);
  }

  await refreshStatus();
});

(async function init() {
  await loadSettings();
  renderHealth(null, false);
  await refreshStatus();
  setInterval(refreshStatus, 1000);
})();