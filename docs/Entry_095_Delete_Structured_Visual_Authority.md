# Entry 095 - Delete Structured Visual Authority

Date: 2026-08-06

## Purpose

This rung completes the raw-retina migration begun in Entry 094. Visual hosts
may now expose photons as bounded pixel frames, but they cannot name a pattern,
choose a cortical target, assign salience, select a hemisphere, or construct
visual feature spikes.

The production visual path is now:

`world or camera -> raw Bgra32/Rgb24 frame -> retinal transducer -> Retina spikes -> visual connectome`

There is no structured visual compatibility path.

## Producer Migration

The maze and world simulators send their rendered avatar eye frames through
`AvatarControlApi.PostRetinalFrameAsync`. The editor webcam converts captured
BGR pixels to a raw RGB sight frame and uses the same retinal route.

Frame producers retain only physical responsibilities:

- render or capture the scene;
- describe dimensions, stride, and pixel format;
- bound capture and dispatch frequency;
- coalesce stale frames under pressure;
- display transport and receptor diagnostics.

The editor attention reticle remains an observation of neuronal attention
telemetry. A retinal response cannot set attention, and the reticle cannot
alter the input frame or its routing.

## Deleted Authority

The following code and contracts are physically removed:

- `POST /api/v1/admin/input/visual`;
- `VisualInputRequest` and `VisualInputDispatchClient`;
- `AvatarVisualSignal` and `AvatarVisualSignalFactory`;
- direct `BuildVisualStimulusSpikes` injection into V1;
- host-provided intensity, burst count, salience, target, and hemisphere;
- automatic left/right hemisphere fallback;
- world and webcam hemifield-salience calculations;
- map-editor terrain-to-brain stimulus injection;
- the retired localhost NRE.Api grayscale-frame route;
- the obsolete `AvatarPixelVision` compatibility helper.

The world editor still edits physical terrain. The brain can learn about that
change only when it reaches the avatar's eyes and retinal circuitry.

## Neural Boundary

Retina owns luminance adaptation, center-surround contrast, temporal change,
ON/OFF receptor populations, and contralateral projection. V1 and downstream
circuits own orientation, motion, integration, binding, recognition,
familiarity, valuation, and attention. Missing or weak neural evidence yields
missing or weak perception; host code cannot fill the gap with a label.

Input gates, ingress backpressure, transport, rendering, and diagnostics remain
infrastructure. They can admit, delay, reject, or report a frame, but they
cannot interpret it.

## Enforcement

`HostVisualAuthorityBoundaryTests` proves that:

- structured visual transport types are absent from compiled assemblies;
- Control Program exposes raw retinal ingress but no structured visual route;
- the direct visual spike builder and salience fields are absent;
- maze, world, and editor use raw retinal dispatch;
- simulator and webcam sources contain no semantic visual transport or host
  hemifield-salience logic.

The integration suite also verifies that the former structured endpoint
returns HTTP 404.

## Verification

- DNNE tests: 325 passed, zero failed, zero skipped.
- Control Program build: passed with zero warnings.
- Maze simulator build: passed with zero warnings.
- World simulator build: passed with zero warnings.
- Editor build: passed with zero warnings.
- Cortical functional benchmark: PASS, 100% overall, stream separation,
  learning, persistence, and adaptive output gating.

## Next Audit

Audit every remaining external input and decoder for host-authored category,
valuation, attention, memory, action, or cognitive fallback. Physical
transducers and read-only neural diagnostics remain; semantic authority does
not.
