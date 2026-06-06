# Entry 034 — Transport, Polling, and Interop Extraction

## Purpose

This pass continues the stabilization line by moving the highest-risk runtime glue out of `Home.razor` and into focused Blazor services.

## Changes

### New services

- `src/NRE.Blazor/Services/EngineApiClient.cs`
  - owns HTTP transport to the NRE API
  - centralizes endpoint calls and request payloads
  - exposes the configured API base address to the UI

- `src/NRE.Blazor/Services/RendererInteropService.cs`
  - owns browser/JS interop for renderer, theme, avatar, voice, and sensory capture
  - isolates `NeuralRenderer`, `VoiceOut`, `SensoryCapture`, and related calls from the page

- `src/NRE.Blazor/Services/ConsoleRefreshCoordinator.cs`
  - owns the fast-frame loop, status loop, and voice loop
  - parses binary fast-frame packets away from the page
  - feeds updates back into the page through callbacks

### Home.razor shift

`Home.razor` now acts more clearly as an operator-console coordinator:

- applies state updates from status and frame callbacks
- handles user intent and panel interactions
- delegates transport, polling, and renderer/browser interop to services

### Configuration

- `src/NRE.Blazor/Program.cs` now registers the new services in DI
- `src/NRE.Blazor/appsettings.json` and `appsettings.Development.json` carry `Api:BaseUrl`

## Why this matters

This removes the most failure-prone seam from the page layer:

- API drift is easier to contain
- JS interop breakage is isolated behind one boundary
- refresh cadence and cancellation become easier to reason about
- future UI changes are less likely to destabilize live rendering or polling

## Expected next value

With this split in place, the next strongest notch is a **transport/orchestration test pass** around:

- status fetch + apply
- fast-frame parse/update
- voice reafference round trip
- renderer command smoke coverage
