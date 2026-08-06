# Entry 094 - Raw Retinal Frame Ingress

Date: 2026-08-06

## Purpose

This rung establishes a live visual receptor boundary for DNNE. Rendered or
camera pixels can now enter the Retina services without a host process naming
objects, selecting a salient field, choosing a hemisphere, or injecting
orientation-coded spikes directly into V1.

Entry 095 subsequently migrated every producer and deleted the older
structured visual endpoint and its transport types.

## Receptor Boundary

`POST /api/v1/admin/input/visual-frame` accepts only physical frame metadata
and raw pixel bytes:

- width, height, and stride;
- `Bgra32` or `Rgb24` pixel format;
- a transport source identity used for temporal adaptation and input gating;
- an exact, bounded binary payload.

It does not accept pattern, semantic class, intensity, burst count, target
structure, source structure, hemisphere, salience, confidence, reward,
attention, memory, or action fields.

Malformed dimensions, strides, formats, and payload lengths are rejected. The
existing video ingress backpressure and avatar-vision gate apply to this route.

## Retinal Transduction

The receptor transducer samples a fixed 16 by 12 retinotopic lattice and
derives:

- photopic luminance from RGB channels;
- local center-surround contrast;
- frame-to-frame luminance change;
- separate ON and OFF retinal ganglion populations;
- contralateral visual-field projection.

Image-left channels reach the right Retina instance and image-right channels
reach the left Retina instance. A uniform first frame adapts without inventing
features. Spatial edges and temporal changes generate bounded spikes. Stable
receptor channels use stable synapse identifiers so structure-local plasticity
can learn repeated visual features.

The transducer is intentionally physiological rather than cognitive. It does
not classify the frame or decide what deserves attention. Salience, feature
binding, recognition, familiarity, value, and orienting must emerge in Retina,
V1/V2/V3/V4/MT, temporal association, pulvinar, superior colliculus,
hippocampal, limbic, and prefrontal circuits.

## Transport

`AvatarControlApi.PostRetinalFrameAsync` sends `AvatarSightFrame` pixels as a
bounded binary body rather than Base64 JSON. Responses report receptor-channel
counts and dispatch state for instrumentation only.

Retinal spikes are delivered to live Retina services through the existing
binary spike-batch transport. Successful per-frame dispatch is intentionally
quiet to avoid flooding the runtime log; errors remain visible.

## Tests

`RetinalFrameTransducerTests` verifies:

- uniform-frame adaptation;
- spatial ON/OFF responses;
- contralateral field projection;
- stable learnable synapse identities;
- malformed descriptor rejection;
- no modulation context on receptor spikes.

ControlProgram integration tests verify that the avatar-vision gate blocks raw
frames and malformed payload lengths return a bad request.

## Verification

- Tests: 325 passed, zero failed, zero skipped.
- ControlProgram Release build: passed with zero warnings.
- SimAvatar Release build: passed with zero warnings.
- Maze simulator Release build: passed with zero warnings.
- World simulator Release build: passed with zero warnings.
- Editor Release build: passed with zero warnings.
- Cortical functional benchmark: PASS, 100% overall, stream separation,
  learning, persistence, and adaptive output gating.

## Completion

Entry 095 moves maze, world, and editor webcam producers to this route and
physically deletes the structured visual request, direct-to-V1 spike builder,
host salience/intensity calculations, dead NRE.Api transport, and map-editor
brain stimulus.
