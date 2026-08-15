# Entry 117 - Blazor Editor Foundation

## Decision

The DNNE Editor is moving from WPF to a separately hosted Blazor application. The
first web rung runs on the same command machine as ControlProgram, but it is not
embedded in ControlProgram and does not change the neuronal runtime.

```text
Browser -> NRE.BlazorEditor -> loopback ControlProgram API -> DNNE services
```

The existing WPF Editor remains available while feature parity is built.

## First Rung

- Added `src/NRE.BlazorEditor` as a .NET 8 interactive-server Blazor application.
- Added a full-viewport Three.js anatomy workspace using a generated 90-structure,
  170-instance atlas derived from the existing WPF anatomy definitions.
- Added search, structure selection, anatomy/activity modes, shell opacity,
  pathway visibility, view presets, runtime status, and telemetry panels.
- Vendored Three.js and Lucide locally so the editor has no runtime CDN dependency.
- Added a read-only, allowlisted telemetry gateway. The browser cannot choose an
  arbitrary ControlProgram path and never receives the Control shared secret.

## Security Boundary

- The editor listens on loopback by default.
- Its ControlProgram endpoint must be loopback even when the editor is visible on
  the LAN.
- `NRE_EDITOR_LISTEN_ANY_IP=true` is rejected unless
  `NRE_EDITOR_ACCESS_KEY` is set.
- LAN sessions use an HTTP-only, same-site authentication cookie.
- Login and logout posts use antiforgery validation.
- Browser responses receive CSP, frame, MIME-sniffing, referrer, and permissions
  headers.
- This rung exposes telemetry only. Restart, import, mutation, and shutdown routes
  are deliberately absent.

For access outside the trusted Tartarus LAN, place the editor behind an HTTPS
reverse proxy or VPN. Do not port-forward its plain HTTP listener to the internet.

## Start

Local-only:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\start-blazor-editor.ps1 -OpenBrowser
```

Trusted LAN:

```powershell
$env:NRE_EDITOR_ACCESS_KEY = '<a long random key>'
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\start-blazor-editor.ps1 -ListenAnyIp -OpenBrowser
```

## Authoritative world runtime

The Blazor host now runs `NRE.WorldSim` as its server-side embodied world. The
browser is a renderer and control surface only. World time, terrain, avatar
kinematics, physiology, physical interactions, retinal/audio/body input, and
neuronal motor intake continue when no browser is connected. The old WPF JSON
snapshot is no longer the World tab's authority.

The embodied path is therefore:

`brain -> neuronal motor frame -> avatar body -> headless world -> sensory frames -> brain`

Starting the Blazor editor is mutually exclusive with the WPF world and maze
simulators, preventing two environments from driving the same brain.

The local URL is `http://localhost:5090/editor`. ControlProgram remains at the
configured loopback URL, normally `http://127.0.0.1:5080`.

## Next Rung

Move the remaining WPF diagnostic panes into read-only web panels, then add narrow
authenticated command endpoints one at a time with explicit authorization and
audit logging. Webcam, microphone, and speech responsibilities should remain at
the browser edge and must not restore symbolic authority to DNNE.
