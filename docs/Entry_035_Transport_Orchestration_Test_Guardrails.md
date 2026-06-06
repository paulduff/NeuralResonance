# Entry 035 — Transport / Orchestration Test Guardrails

This entry adds focused regression protection around the seams introduced in Entry 034.

## Scope

The goal of this notch is not full end-to-end coverage. It is to protect the two highest-risk areas introduced by the extraction pass:

1. HTTP request formation in `EngineApiClient`
2. Binary frame parsing for the fast renderer stream

## Changes

- extracted `FastFrameParser` from `ConsoleRefreshCoordinator`
- added request-formation tests for peer naming, visual stimulus payloads, and load-brain content type
- added binary parser tests for valid payloads and malformed/truncated payload rejection
- extended the test project to reference `NRE.Blazor` and `NRE.Contracts`

## Rationale

These tests provide immediate value because they harden the exact seams most likely to regress during future UI and transport work:

- query parameter formatting
- JSON payload shape
- binary frame decoding assumptions
- malformed payload handling

## Next

The next useful guard-rail notch is coordinator behavior coverage:

- loop cancellation behavior
- telemetry cadence behavior
- voice reafference flow
- renderer invocation sequencing
