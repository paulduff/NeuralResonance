# Entry 096 - Delete Structured Auditory Authority

Date: 2026-08-06

## Purpose

This rung makes hearing a physical receptor pathway. External processes may
produce or capture pressure samples, but they cannot name a sound, choose its
meaning, select a brain target, choose a hemisphere, or manufacture language.

The production auditory path is now:

`world or microphone -> raw PCM16 frame -> cochlear filter bank -> Cochlea spikes -> auditory connectome`

There is no structured auditory compatibility path.

## Raw Cochlear Boundary

`POST /api/v1/admin/input/audio-frame` accepts only bounded physical metadata
and an exact binary payload:

- sample rate from 8 to 48 kHz;
- one or two interleaved channels;
- samples per channel;
- little-endian signed 16-bit PCM;
- a transport source identity used only for temporal receptor adaptation.

The request cannot carry pattern, class, text, intensity, burst count,
hemisphere, source structure, target structure, confidence, reward, attention,
memory, or action fields. Invalid formats, dimensions, and payload lengths are
rejected before transduction.

## Cochlear Transduction

`CochlearFrameTransducerRuntime` applies a 24-band logarithmic filter bank to
each physical ear. It derives only receptor-level quantities:

- band amplitude;
- temporal onset;
- root-mean-square and peak pressure;
- left and right auditory-nerve fibre activity;
- stable synapse identities for recurring tonotopic channels.

Silence creates no auditory features. Mono pressure reaches both ears. Stereo
pressure remains ipsilateral at the cochlear boundary, leaving binaural timing,
localization, association, recognition, language, value, attention, and action
to the neuronal pathway.

Every generated receptor spike has `Cochlea` as both its source and target and
contains no modulation context. Existing connectivity then carries activity
through Cochlear Nucleus, Superior Olive, Inferior Colliculus, Thalamus, A1,
auditory association, and language circuits. Olivocochlear feedback remains
part of that neuronal loop.

## Producer Migration

The editor microphone now forwards captured PCM frames. Its level meter remains
passive instrumentation, but the editor no longer converts RMS or zero-crossing
rate into tone names, syllables, language modes, or remembered utterances.
Speech recognition must emerge from auditory and language circuits.

Maze and world simulations now render numerical acoustic sources into stereo
PCM using frequency, amplitude, phase, harmonic content, noise content, pulse
shape, and physical pan. The world may simulate how an event changes air
pressure, just as it simulates how an object changes pixels, but no event label
crosses the avatar-brain boundary.

The burn-in tool likewise sends raw edge pixels and a raw tone waveform rather
than structured sensory JSON.

## Deleted Authority

The following compatibility mechanisms are physically removed:

- `POST /api/v1/admin/input/auditory`;
- `AuditoryInputRequest`;
- `AvatarAuditoryCue` and `AvatarAuditoryDispatchResult`;
- `PostAuditoryCueAsync` and the semantic avatar cue queue;
- `BuildAuditoryStimulusSpikes` and direct host injection into A1;
- host-selected auditory pattern, intensity, burst count, target, source, and
  hemisphere;
- named maze and world cues such as footsteps, growls, goals, and checkpoints;
- microphone feature classification;
- microphone-generated pseudo-syllables and direct language injection.

Infrastructure may bound, delay, reject, transport, or report a PCM frame. It
cannot interpret the frame or compensate for missing neuronal understanding.

## Enforcement

`HostAuditoryAuthorityBoundaryTests` proves that:

- semantic auditory transport types are absent from compiled assemblies;
- Control Program exposes raw cochlear ingress but no structured auditory
  route or direct A1 spike builder;
- the editor microphone can only send captured PCM;
- maze and world can only send physically rendered acoustic frames;
- the burn-in tool uses raw visual and auditory frames.

`CochlearFrameTransducerTests` verifies silence, bilateral mono response,
stereo lateralization, stable continuing-fibre synapses, malformed descriptor
rejection, Cochlea-only receptor targets, and absence of modulation context.
The integration suite verifies malformed PCM rejection and HTTP 404 from the
retired structured endpoint.

## Verification

- DNNE tests: 348 passed, zero failed, zero skipped.
- Control Program build: passed with zero warnings.
- Maze simulator build: passed with zero warnings.
- World simulator build: passed with zero warnings.
- Editor build: passed with zero warnings.
- Cortical functional benchmark: PASS, 100% overall, stream separation,
  learning, persistence, and adaptive output gating.

## Next Audit

Replace structured collision injection with physical mechanoreceptor and
nociceptor packets, then replace body-state target selection with fixed
vestibular, proprioceptive, somatic, and interoceptive receptor transducers.
The same rule applies: the body may report measurements; only neurons may infer
what those measurements mean.
