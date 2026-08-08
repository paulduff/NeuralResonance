# Entry 099 - Delete Structured Typed-Language Authority

Date: 2026-08-08

## Decision

DNNE no longer accepts host-interpreted typed language. The structured
language command contract and endpoint are deleted rather than deprecated.
Typed interaction remains available, but the brain receives it as light on a
retinal display:

`typed characters -> visible pixels -> Retina -> visual cortex -> learned language circuits`

There is no compatibility switch that restores direct text-to-language spikes.

## Deleted Authority

The removed `POST /api/v1/admin/input/language` route allowed a host client to
choose language mode, hemisphere, intensity, burst count, target and backoff
route, semantic or phonetic tokenization, grammar, motor intent, and immediate
narration. It therefore supplied interpretation and action before the neural
system could perceive the stimulus.

`LanguageInputRequest`, `AvatarLanguageCommand`,
`AvatarLanguageCommandResult`, the direct language-spike builder, client
command APIs, and the obsolete command-path test tool are physically absent.
Maze, world, and editor no longer display language-mode or hemisphere routing
controls.

## Visual Text Boundary

`AvatarTextSightRenderer` draws a bounded external message into a deterministic
384 by 192 BGRA32 frame. The frame carries pixels, dimensions, format,
generation, and capture time only. It cannot name text tokens, language mode,
hemisphere, neural target, spike type, intent, value, or motor directive.

The rendered frame uses the existing
`POST /api/v1/admin/input/visual-frame` boundary. `RetinalFrameTransducerRuntime`
derives retinotopic ON/OFF activity and the connectome owns every downstream
effect. The `avatar_text_display` source label identifies physical transport
provenance; it does not alter routing or interpretation.

## Producer Migration

Maze, world, and editor now expose **Present to Retina**. Submission renders
the visible text locally and posts only the resulting byte buffer. The input
response reports retinal transport counts, not inferred grammar, an action, or
brain-authored speech.

Passive narration remains output telemetry. A client may display neuronal
narration observed in a later frame, but presenting text does not manufacture
or immediately apply narration.

## Expected Behaviour

Removing the structured shortcut may initially reduce apparent language
comprehension. That regression is honest: DNNE must learn orthography, visual
word forms, lexical associations, grounding, and action consequences through
neural activity. Restoring a host parser would improve demonstrations while
weakening the neuronal claim.

## Verification

Automated boundary and integration tests verify:

- the structured language endpoint returns `404 Not Found`;
- structured language transport types are absent from `NRE.SimAvatar`;
- desktop clients use the shared text renderer and retinal API only;
- retinal frames expose no text, token, mode, hemisphere, target, or motor
  metadata;
- different visible messages produce different valid pixel buffers;
- typed display frames are sent as `application/octet-stream` to the raw visual
  route;
- passive neuronal narration remains readable from frame telemetry.

## Remaining Authority

At the completion of this rung, the simulation loop still contained automatic
perception-language conditioning that could manufacture host tokens and
language spikes from telemetry. Entry 100 deletes that path, its routing
policy, and its administrative support surfaces.
