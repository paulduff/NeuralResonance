# Entry 102 - Delete Host-Generated Editor Speech

Date: 2026-08-08

## Decision

The editor may inspect neuronal speech-pathway activity, but it may not infer
that activity is an utterance. Audible speech is presentation infrastructure,
not a second language generator.

## Removed Pipeline

The WPF editor no longer contains:

- a Windows SAPI worker or speech queue;
- dispatch-spike speech triggers or thresholds;
- a language-structure allowlist used to select speech;
- retained language utterance text;
- host phrase construction, normalization, duplicate suppression, or cooldown;
- speech trigger, rate, volume, and enable controls.

The old path was dormant after semantic narration deletion, but it still
encoded an invalid authority boundary: dispatch activity could authorize the
playback of remembered host text. Neural activity alone does not identify the
words a brain emitted.

## Preserved Neuronal Function

Broca, Wernicke, arcuate, speech-motor, basal-ganglia, thalamic, and related
circuits are unchanged. Their spikes and diagnostic population rates remain
visible as measurements. This rung removes only the editor's interpretation
and playback authority.

## Future Audible Output

A future voice adapter may speak the exact candidate text only after the Dyad
v2 generation response reports an emitted candidate accepted by its
prompt-bound neuronal review. That adapter must not inspect raw dispatch
spikes, recover user input, synthesize fallback prose, or feed spoken text back
into DNNE as semantic state.

Until such an explicit accepted-emission consumer exists, silence is the
honest result.

## Regression Boundary

The structured-language authority tests require the editor speech partial,
host phrase symbols, speech queue, SAPI integration, and speech controls to
remain absent.

Verification completed with all 383 tests passing, every declared circuit
reporting `OK`, a 100% cortical functional benchmark score, and a full Release
build with zero warnings and zero errors.
