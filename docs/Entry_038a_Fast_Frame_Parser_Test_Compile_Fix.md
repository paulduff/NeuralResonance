# Entry 038a — Fast-Frame Parser Test Compile Fix

## Purpose
Correct a compile break in the test project caused by parser tests asserting against obsolete `RenderFrameFastDto` property names.

## Applied changes
- Updated `tests/NRE.Tests/FastFrameParserTests.cs` to use `StepIndex` instead of `Step`.
- Updated `tests/NRE.Tests/FastFrameParserTests.cs` to use `ThalamicPulseActive` instead of `ThalamicPulse`.

## Rationale
The canonical fast-frame DTO already uses the shared contract names:
- `StepIndex`
- `ThalamicPulseActive`

The parser and DTO were aligned; the test expectations were not. This notch restores test/contract consistency without changing runtime behaviour.
