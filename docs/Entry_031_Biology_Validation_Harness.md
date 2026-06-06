# Entry 031 — Biology Validation Harness

## Purpose

This entry adds a **biology validation harness** to the Neural Resonance Engine canon so the current anatomical atlas can be checked automatically instead of being judged only by eye.

The harness converts the voxel atlas into measurable region summaries and validates a stable set of canonical spatial invariants.

## What was added

- `src/NRE.Core/Engine/AnatomyValidationDtos.cs`
- `src/NRE.Core/Engine/AnatomyValidationHarness.cs`
- `NreEngine.GetAnatomyValidationReport()`
- API endpoint: `GET /api/engine/anatomy/validate`
- test suite: `tests/NRE.Tests/AnatomyValidationHarnessTests.cs`

## Validation approach

For each hemisphere volume, the harness builds a region summary containing:

- voxel count
- normalized centroid
- normalized min bounds
- normalized max bounds

Those summaries are then checked against canonical biological ordering rules, including:

- corpus callosum remains near the midline
- corpus callosum is superior to thalamus
- thalamus remains near midline
- hypothalamus lies inferior to thalamus
- hippocampus lies posterior to amygdala
- amygdala lies inferior to thalamus
- cerebellum lies posterior and inferior to thalamus
- brainstem lies inferior to thalamus
- pons lies anterior to brainstem
- precentral gyrus lies anterior to postcentral gyrus
- frontal cortex lies anterior to occipital cortex
- left/right hemisphere atlas parity is preserved

## Why this matters

The Folded Archive has reached the point where anatomical drift is a real risk.

A visual renderer can look plausible while still being wrong in one of the following ways:

- a midline structure can drift laterally
- a temporal structure can move too far superiorly
- the central sulcus relationship can invert
- a posterior structure can be accidentally pulled forward by a refactor

This harness gives the canon a reusable **non-visual guard rail**.

## Intended use

- run as part of the automated test suite
- query through the API when checking a build
- use alongside the anatomical reference pack and fixed camera screenshots

## Canon judgement

This is a stabilisation and truth-maintenance notch, not a visual flourish.
It makes future anatomical work safer because we now have measurable invariants to defend.
