# NRE Tab Capture Stimulus Bridge (Chrome MV3)

This extension captures the active browser tab's video/audio, extracts compact sensory features, and posts them to the NRE API:

- `POST /api/engine/sensory`
- Visual: `intensity01`, `speedHz`, `spatialFreq`
- Auditory: `intensity01`, `toneHz`
- Optional language relay: tab captions/text -> `POST /api/engine/voice/reafferent`

## Install (Unpacked)

1. Open Chrome: `chrome://extensions`
2. Turn on **Developer mode**
3. Click **Load unpacked**
4. Select this folder:
   - `tools/browser-extension/nre-tab-capture`

## Use

1. Open the website/tab you want to capture.
2. Click the extension icon.
3. Set API base (default: `http://localhost:5005/`).
4. Choose Visual/Audio/Text options.
5. Click **Start Capture**.
6. Return to NRE and watch visual/auditory drive update.

## Notes

- Text/caption relay only forwards text while capture is running on the selected tab.
- Visual extraction needs stream pixel access. Some sources block this (CORS/DRM), in which case audio can still work.
- `tabCapture` is user-consent gated and starts only from an explicit click.
- Keep FPS moderate (`4-10`) for stable CPU usage.

## Next Improvements

- Add stream-health flags in popup (video-ok/audio-ok/cors-blocked/text-ok).
- Add optional endpoint auth header/token support.
- Add richer text normalization and de-duplication for long streams.
