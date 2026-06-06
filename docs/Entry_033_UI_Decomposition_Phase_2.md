# Entry 033 — UI Decomposition Phase 2

This entry continues the operator-console decomposition started in Entry 032 and compile-fixed in Entry 032a.

## Aim

Reduce `Home.razor` further by extracting the remaining presentation-heavy surfaces while preserving the page as the orchestration layer for:

- polling
- renderer interop
- HTTP transport
- playback / voice loops
- state transitions

## Changes

### Extracted panels

Added:

- `src/NRE.Blazor/Shared/OperatorConsole/Tabs/VoiceTabPanel.razor`
- `src/NRE.Blazor/Shared/OperatorConsole/Tabs/PeerTabPanel.razor`
- `src/NRE.Blazor/Shared/OperatorConsole/Tabs/ViewTabPanel.razor`
- `src/NRE.Blazor/Shared/OperatorConsole/Tabs/MonitorTabPanel.razor`

These now contain the UI markup for:

- voice / motor controls and voice log
- peer bridge and vocal tract display
- renderer view presets, connection filters, and circuit toggles
- telemetry / monitor views and legends

### Shared UI models

Added:

- `src/NRE.Blazor/Shared/OperatorConsole/OperatorConsoleDtos.cs`

This moves page-local DTO and lightweight view-model declarations out of `Home.razor`, including:

- telemetry DTOs used by the monitor panel
- renderer packed frame DTOs
- peer / vocal tract DTOs
- body state DTO
- `VoiceLogEntry`

### Home page role tightened

`Home.razor` now acts more clearly as:

- state container
- callback host
- API/JS interop coordinator

rather than also owning every major presentation block inline.

## Outcome

- `Home.razor` reduced materially in size
- markup responsibilities are more isolated
- future UI work can proceed by tab/panel rather than editing a single monolithic page
- UI-only models are now separated from page orchestration code

## Notes

This pass intentionally does **not** move polling loops, renderer interop, or HTTP orchestration out of `Home.razor` yet. That is still the next natural stabilisation seam if further decomposition is desired.
