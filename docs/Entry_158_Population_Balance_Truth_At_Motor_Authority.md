# Entry 158 - Population Balance Truth at Motor Authority

Date: 2026-08-23

## Status

Implemented and verified in tests and a fresh live observation. The corrected
stack remains live for continued observation.

## Triggering evidence

The Entry 157 observation reached brain tick 8,700 with 119 healthy services,
live sensory traffic, full bilateral motor coverage, and continuing head
orientation. The avatar remained upright and double-foot supported with a
physical balance error of zero, but travelled zero metres and produced no
locomotor dispatches.

The emergency righting latch entered at brain tick 97 and remained active with
zero of four recovery updates. It forced the selected action channel to -1 and
blocked every voluntary body drive despite a healthy basal-ganglia competition
and available motor populations.

## Root cause

The motor decoder consumed raw per-instance
`VestibuloReticularDiagnostics.BalanceError` values and selected their maximum.
An individual vestibular-nucleus instance can report vestibular mismatch but
cannot locally observe the compensating cerebellar-vermis and spinal-motor
populations. Its local value was therefore treated incorrectly as a complete
whole-circuit balance prediction error.

The editor telemetry followed a different path. It first aggregated the
vestibular, reticular, cerebellar, and spinal populations and then calculated a
composite error. That complete circuit correctly reported zero. Motor authority
and operator telemetry consequently disagreed about the same named signal.

## Repair

1. Introduce one vestibulo-reticular population decoder that averages the
   bilateral firing rates of the vestibular nuclei, reticular formation,
   cerebellar vermis, and spinal motor cord.
2. Calculate arousal, balance error, posture stability, and posture mode from
   that integrated neural population.
3. Use the population result for righting entry, stable recovery, action
   persistence suppression, postural support, confidence, and evidence.
4. Use the same composite function when enriching the public editor snapshot,
   preventing telemetry and motor authority from acquiring different signal
   semantics again.
5. Preserve local structure diagnostics as local observations; they no longer
   receive whole-body authority by taking a maximum across incomparable local
   values.
6. Expose `balance-error=population:<value>` in motor evidence for direct live
   diagnosis.

The righting thresholds, four-update recovery requirement, fresh neuronal
selection boundary, and all genuine-instability protection remain unchanged.
No scripted movement, semantic steering, ML model, or host-selected action was
introduced.

## Acceptance

- A compensated neural population can recover even when a vestibular instance
  retains a high unintegrated local error.
- A genuine high population error still enters and maintains emergency
  righting.
- Stable population error releases the latch after four updates.
- Voluntary movement requires a fresh basal-ganglia winner after recovery.
- Motor evidence and editor telemetry report the same population-level balance
  semantics.

## Verification

- Focused neuronal motor tests: 77 passed, 0 failed.
- Complete DNNE test suite: 831 passed, 0 failed.
- Release solution build: succeeded with 0 warnings and 0 errors.

## Next observation

Start the full stack from a fresh generation-three network. Confirm that the
population balance error falls below 0.18 while the body is supported, the
righting recovery counter reaches four, the latch releases, a fresh neuronal
action receives motor authority, and locomotor dispatch and travelled distance
become non-zero. A genuine induced instability must still re-enter righting.

## Live acceptance result

The fresh stack reached brain tick 1,425 and world tick 11,916 with the
population balance error fixed at zero and the emergency righting latch open.
The old failure point at brain tick 97 passed without re-entry. Fresh neuronal
actions received motor authority beginning at tick 84.

After the one expected startup overload pause was resumed, WorldSim recorded 49
locomotor dispatches, 262 manipulator dispatches, and 0.307 metres of travelled
distance. No host movement command or behavioural fallback was used. The false
balance veto is therefore repaired. Deliberate-instability re-entry remains a
separate safety observation for a later controlled run.
