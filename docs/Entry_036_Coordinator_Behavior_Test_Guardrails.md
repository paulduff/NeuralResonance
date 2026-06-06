# Entry 036 — Coordinator Behavior Test Guardrails

This entry adds focused regression protection around the Blazor operator-console coordinator layer introduced in Entries 034 and 035.

## Purpose

The previous notch established tests for request formation and fast-frame parsing. This notch hardens the orchestration seam itself:

- voice speak/reafference/callback sequencing
- fast-frame forwarding through the frame loop
- monitor telemetry cadence in the status loop
- monitor-tab gating for telemetry refresh

## Structural changes

To make the coordinator testable without dragging JS runtime or live HTTP into unit tests, two small service abstractions were introduced:

- `IEngineApiClient`
- `IRendererInteropService`

`EngineApiClient` and `RendererInteropService` now implement those interfaces, while `ConsoleRefreshCoordinator` depends on the abstractions.

The coordinator also now exposes loop delay properties for test control:

- `VoiceLoopDelayMs`
- `FrameLoopDelayMs`
- `StatusLoopDelayMs`

These default to the previous production timings and can be shortened during tests.

## New tests

`ConsoleRefreshCoordinatorTests.cs` now covers:

1. Voice loop ordering
   - renderer speech occurs first
   - voice reafference is posted second
   - page callback is invoked third

2. Frame loop forwarding
   - a valid binary fast-frame is parsed and passed onward

3. Status loop telemetry cadence
   - telemetry ticks once every twelfth cycle when the selected tab is `Monitor`

4. Status loop tab gating
   - telemetry is not ticked when another tab is active

## Rationale

This is a plumbing-quality notch rather than a biology notch. The engine is now rich enough that orchestration regressions can silently damage user confidence even when the neural model itself is sound. These tests reduce that risk.
