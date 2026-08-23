# Entry 150 - Afferent Extinction, Gait Evidence, and Somatic Resilience

Date: 2026-08-21

## Status

Implemented and verified offline with a fresh-network boundary. A new live
acceptance run remains required.

## Evidence from Entry 149

The fresh acceptance run completed 157,805 ticks over 1.45 hours without a
tick failure, physical death, or loss of tissue integrity. Stable balance rose
to 71.49%, falling fell to 2.02%, posture conflict remained at zero, and the
dynamic stability allowance reached 75 mm. Those gates passed.

Contact release remained incomplete. Physical hand contact shortened to a
49.4 second maximum, but an attributed spinal withdrawal source remained
active for 353.4 seconds and the avatar ended with unilateral hand support.
Three somatic requests also timed out. The v4 report could not distinguish
true alternating swing-foot clearance from load redistribution or shuffling.

## Corrections

- Every spinal withdrawal source now carries the age of its latest afferent
  evidence.
- A source is neuronally released after 500 ms without fresh nociceptive
  afference. Its channel trace is cleared when no fresh source supports that
  channel, preventing stale contact evidence from retaining motor authority.
- World-run protocol v5 records left and right stance and swing time, stance
  and swing entries, double support, unsupported time, alternating and
  repeated swing transitions, maximum swing duration, and sole clearance from
  the articulated collider geometry.
- Somatic contacts use a dedicated two-second client and bounded four-way
  dispatch. A transient timeout receives one retry with the same source,
  sequence, and physical-frame timestamp identity; retry and recovery totals
  are exposed in the world snapshot.

These changes do not choose a movement, destination, or response. The host
reports physical measurements and transport health. Neuronal populations
retain exclusive authority over withdrawal and locomotion.

## Biological imperative

Energy, hydration, tissue integrity, food recovery, and viability are already
implemented in the physical body. Their measurements become visceral
chemoreceptor spikes, including glucose-energy deficit and tissue damage, and
feed the hypothalamic and interoceptive network. Ordinary world mode retains
metabolic burn; only explicit motor-training mode suppresses it. The next live
run should use ordinary mode with predators suspended so hunger and bodily
consequence are present without allowing predation to obscure gait learning.

## Fresh-network boundary

Entry 149's 238 synapse files and graceful checkpoint are preserved at:

`C:\Users\User\AppData\Local\NeuralResonanceEngine\synapses-runs\entry149-final-20260821T182319Z`

The active `synapses-action34-axial-v1` directory is empty. Verification must
not start the stack or populate that directory.

## Acceptance gates

- No withdrawal source may exceed the afferent-silence bound without renewed
  nociceptive input.
- Both feet must enter stance and swing, with alternating swing transitions
  and measurable sole clearance above 15 mm.
- Unsupported time must correspond to genuine dynamic gait or a physical fall,
  not collider clipping.
- Somatic retry counters may rise during congestion, but final rejection and
  body-input failure counts should remain zero in a healthy run.
- Posture conflict must remain zero and balance should not regress materially
  from the Entry 149 baseline.

## Offline verification

- Focused withdrawal, gait telemetry, somatic replay, transport-boundary, and
  closed-loop tests: 74 passed, 0 failed.
- Full non-integration suite: 723 passed, 0 failed.
- Verification kept the DNNE stack stopped and did not populate the fresh
  active synapse directory.
