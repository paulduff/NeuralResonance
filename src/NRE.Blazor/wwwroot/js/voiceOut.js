(function (global) {
  const VoiceOut = {};

  let _ready = false;
  let _voices = [];
  let _preferred = null;

  function pickPreferredVoice() {
    const byLang = (langPrefix) => _voices.find(v => (v.lang || '').toLowerCase().startsWith(langPrefix));
    return byLang('en-gb') || byLang('en-') || _voices[0] || null;
  }

  function clamp(value, lo, hi, fallback) {
    return (typeof value === 'number' && isFinite(value)) ? Math.min(hi, Math.max(lo, value)) : fallback;
  }

  function phonemeToSurface(phoneme) {
    switch (phoneme) {
      case '_': return ' ';
      case 'sh': return 'sh';
      case 'ch': return 'ch';
      case 'ng': return 'ng';
      case 'j': return 'y';
      default: return phoneme || '';
    }
  }

  function phonemesToSpeakableText(phonemes) {
    if (!Array.isArray(phonemes) || phonemes.length === 0) return '';
    return phonemes.map(phonemeToSurface).join('').replace(/\s+/g, ' ').trim();
  }

  function normalizeUtterance(payload, rate, pitch, volume) {
    if (payload && typeof payload === 'object' && !Array.isArray(payload)) {
      const text = payload.text ?? payload.Text ?? '';
      const gloss = payload.gloss ?? payload.Gloss ?? '';
      const phonemes = payload.phonemes ?? payload.Phonemes ?? [];
      return {
        text: String(text).trim(),
        gloss: String(gloss).trim(),
        phonemes: Array.isArray(phonemes) ? phonemes : [],
        rate: payload.rate ?? payload.Rate,
        pitch: payload.pitch ?? payload.Pitch,
        volume: payload.volume ?? payload.Volume
      };
    }

    return {
      text: typeof payload === 'string' ? payload.trim() : '',
      gloss: '',
      phonemes: [],
      rate,
      pitch,
      volume
    };
  }

  VoiceOut.initSafe = function () {
    try {
      if (!('speechSynthesis' in window) || typeof SpeechSynthesisUtterance === 'undefined')
        return 'Web Speech API not available in this browser.';

      const load = () => {
        _voices = window.speechSynthesis.getVoices() || [];
        _preferred = pickPreferredVoice();
        _ready = true;
      };

      load();
      if (typeof window.speechSynthesis.onvoiceschanged !== 'undefined') {
        window.speechSynthesis.onvoiceschanged = load;
      }

      return null;
    } catch (e) {
      return (e && e.message) ? e.message : String(e);
    }
  };

  VoiceOut.speak = function (payload, rate, pitch, volume) {
    try {
      if (!_ready) VoiceOut.initSafe();

      const utterance = normalizeUtterance(payload, rate, pitch, volume);
      const spokenText = utterance.text || phonemesToSpeakableText(utterance.phonemes) || utterance.gloss;
      if (!spokenText) return;

      const u = new SpeechSynthesisUtterance(spokenText);
      if (_preferred) u.voice = _preferred;

      u.rate = clamp(utterance.rate, 0.6, 1.6, 1.0);
      u.pitch = clamp(utterance.pitch, 0.6, 1.6, 1.0);
      u.volume = clamp(utterance.volume, 0.0, 1.0, 1.0);

      window.speechSynthesis.speak(u);
    } catch {
    }
  };

  VoiceOut.cancel = function () {
    try { if ('speechSynthesis' in window) window.speechSynthesis.cancel(); } catch { }
  };

  global.VoiceOut = VoiceOut;
})(window);
