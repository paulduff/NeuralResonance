# Isolated somatic motor source to muscle unit

The missing motor-path evidence is a membrane-generated firing train reaching
Avatar's existing event/calcium/force implementation. Earlier plant checks used
fixture events. This candidate connects the five-compartment motor reference to
the already tested publisher, replay buffer, receiver and isometric plant, with
no change to their equations or live selection.

This is a prospective experiment. The motor scheduling comparison must first pass
its original numerical bounds and receive an operator-reported, verified review.
The clock-only result does not satisfy that prerequisite. Nothing in this change
qualifies active axonal propagation, a neuromuscular junction or Avatar recovery.

## Source and body

`tools/MotoneuronUnitCircuitProbe` uses the original compensated motor cell at
0.003125 ms maximum step and its qualified endpoint-difference scheduling helper.
The source sample grid remains 0.1 ms. It repeats the original six-second quiet
and soma-6000 protocols: -65 mV initial state, 6,000 pA injection from 1,000 to
3,000 ms for the active case. No input, threshold, conductance or channel changes.
Every 30-state source sample must reproduce the passing baseline CSV bytes, with
exact original crossing times, integration calls and accumulated duration.

Each newly observed somatic threshold crossing creates one message, preserving
its fractional firing time. Source cell, projection, outbound synapse, receiving
contact and motor unit have explicit identities. Packet cadence determines only
when the model's crossings are harvested and transported. It does not alter the
source integration grid, generate a rate-derived train, or quantize firing times.
Different isolated trials have distinct session IDs. Equivalent active trials
use the same event identities within the study to permit exact comparisons.

One declared contact adds a fixed 2.0625 ms delay. Existing
`AvatarIsometricMotorUnitPlant` then uses its per-unit grid and actual fractional
arrivals. Its existing slow-unit calcium, binding and activation calculation
produces force at fixed optimal length. No second activation/fatigue filter is
added. Outputs are sampled every 0.25 ms; body steps are 0.125, 0.0625 or 0.03125 ms.

These are deliberately reduced reference components. The source uses cat MG
parameters and the recipient uses the pinned human tibialis-anterior slow-unit
parameter row. Their pairing is an interface/numerical experiment, **not** a
physiologically matched human motor unit. Current injection substitutes for a
future calibrated spinal synaptic input. The cell has no active axon compartment;
somatic crossings plus a declared delay do not establish propagation. Perfect
endplate transmission, an unlimited reference bath and fixed muscle length omit
NMJ failures, muscle action potentials, supply limitations and dynamic mechanics.

## Eight prospective conditions

| Condition | Source | Packet interval | Muscle step | Intervention |
| --- | --- | ---: | ---: | --- |
| packet-1 | soma-6000 | 1 ms | 0.125 ms | none |
| packet-10 | soma-6000 | 10 ms | 0.125 ms | none |
| packet-50 | soma-6000 | 50 ms | 0.125 ms | none |
| retry-10 | soma-6000 | 10 ms | 0.125 ms | withhold every seventh receipt; retry original packet |
| refined-1 | soma-6000 | 10 ms | 0.0625 ms | muscle refinement |
| refined-2 | soma-6000 | 10 ms | 0.03125 ms | muscle refinement |
| quiet | quiet | 10 ms | 0.125 ms | remove current injection |
| disconnected | soma-6000 | 10 ms | 0.125 ms | intentionally cut the declared output projection |

In the retry condition, an attempted body advance before receipt of the next
packet must be rejected without changing its state. The original packet is then
accepted, its receipt withheld, and the identical retained packet retried. Exact
replay must preserve all plant state and acknowledge the same bytes. This is an
in-process delivery experiment, not a socket, durable journal or cluster restart.

The disconnection leaves the active neuron completely unchanged. Every removed
emission is explicitly recorded. Empty packets establish completeness for the
cut projection, not neuronal silence. Both this condition and the truly quiet
source must produce zero muscle release/force. Connected conditions must produce
nonzero force only after actual arrivals. No physiological force target is fitted.

The six-second horizon is strict. Events whose declared arrivals lie beyond it
remain accepted and pending; no extra silence is invented to drain them. Source
emissions, intentional cuts, packet events, due arrivals, consumed events and
pending tail must account for every original crossing exactly once.

## Evidence and numerical checks

Every condition saves the complete source trace, muscle trace, original emissions,
accepted/retried packets and receipts, and delivered arrivals. Failed conditions
preserve partial evidence and do not suppress the remaining conditions. The
analyzer independently checks source bytes, identities, timestamps, ordering,
completeness, counters, final state and calcium/force accounting. All three packet
cadences and the retry must give identical muscle samples; the source must also
remain identical under muscle refinement and disconnection.

The muscle reference uses the previously reviewed independent Python equations,
with parameters loaded from the pinned authors' MAT file. Its release waveform
is the direct sum of the actual delivered events, independent of the candidate's
recurrence. DOP853, tighter DOP853 and Radau each integrate this new train. Their
arrays are saved for subsequent read-only review; a review never solves again.

All calcium criteria are reused from the earlier motor-calcium comparison:
reference state agreement 1e-8, flux-integral agreement 1e-7, finest candidate
state and force errors 1e-6, calcium ledger 1e-9, and finest state error no greater
than 0.2 times coarse error plus its existing 2e-8 numerical floor. The waveform
check remains 1e-10. These muscle criteria do not replace or relax any neuronal
bound. General physiological calibration and live recovery remain separate.

## Preparation and execution

The probe builds with zero warnings/errors; Python and PowerShell syntax checks
pass. The static review in
`artifacts/motor-unit-circuit-preparation-20261109/source-review.json` verifies ten
unchanged neuronal/body source files and unchanged calcium criteria; its D: copy
is verified. The neuronal baseline package itself remains unrun. No test, neuronal integration or
reference solve has run for this candidate. The visible runner requires 88
focused cases: 32 transport, 16 replay-buffer, 13 calcium, 15 isometric-plant,
11 motor-cell and one solution-manifest case. It then performs reference
preflight, all eight conditions and the three independent muscle solves, verifies
source/binary integrity, and creates the final verified D: backup.

After the operator reports the pending motor-boundary comparison complete,
`tools/review-motor-boundary.py` verifies its capture and recomputes saved-state
analysis. Back up that review on D:. Only a passing review permits
`tools/prepare-motor-unit-circuit.py --motor-review REVIEW_FOLDER` to freeze clean
pushed source. It verifies the source cell/scheduler against that passing capture
and the unchanged body/transport components against the earlier 87-test capture.
It copies the exact baseline traces and pinned calcium reference dependencies.
Verify the initial package on D: before launching
`tools/start-motor-unit-circuit-window.ps1 -PackageDirectory PACKAGE`.

This package is not yet frozen or launched. Preserve the existing execution
order: reported Golgi completion, CUDA cost window without competing work,
reported CUDA completion, then the already frozen motor scheduling comparison.
The new circuit window may follow only after that comparison passes review.
All experiments remain visible independent PowerShell windows with no live
monitoring. Existing saves, Avatar dynamics, Unity and editor authority labels
remain unchanged; worlds continue to start paused.
