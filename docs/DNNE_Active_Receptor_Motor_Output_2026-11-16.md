# Active receptor-driven motor output comparison — 16 November 2026

The deferred coupled comparison now includes the active motor-source adapter
and Avatar's actual motor-axon receiver. This extends the 14 component checks,
whose event-delivery controls use passive voltage crossings. The new experiment
has been authored and compiled; it has **not run**. No pending capture has been
read and no additional window has opened.

## Same active cell, two transport cadences

`tools/receptor_motor_output_protocol.py` declares 36 source conditions: the
existing six receptor scenarios at three membrane resolutions, each repeated
with publication every one or 17 source samples. The source sample interval is
fixed at `0.1 ms` in both runs. Every cell starts fresh at `-65 mV`, uses the
full active model without channel blocks, and runs the same complete `1500 ms`
input, quiet prehistory and post-stimulus tail as the raw recorder.

`NativeMotorBridgeProbe --receptor-motor-output MEMBRANE_INPUT OUTPUT_INPUT
BASELINE_DIRECTORY NEW_OUTPUT_DIRECTORY` reconstructs the declared original
presynaptic messages, verifies their original receipts, and gives them to a
fresh private `ReceptorMotorSource`. The saved baseline states and crossings
are comparison evidence only: they are never installed into that cell or
used to generate its outgoing events.

At every `0.1 ms` observation, including time zero, the new source must match
the raw candidate bit for bit in all thirty membrane/accounting states and
every receptor occupancy. Its cumulative accepted-step count must also match.
All crossings and the complete final cell snapshot must agree exactly. The two
packet cadences must have identical neuronal observations and event identities.
There are **540,036 source observations** and **1,296 original input-contact
deliveries** across the 36 conditions.

At least one excitation condition must contain actual observed crossings and
all quiet conditions must remain without them, preserving the original matrix
requirements. Empty output everywhere cannot qualify transport through this
active-source comparison.

This observes the source at its complete sample grid, not at every internal
accepted step. The raw candidate still retains every accepted endpoint for the
independent numerical reference. Neither the source comparison nor numerical
refinement proves a continuous uniform error bound between recorded points.

## Existing Avatar receiver and retained transport

Each source declares one motor-unit contact with an explicit `2.0625 ms`
conduction delay and a zero-to-zero neural/body epoch. The motor unit and delay
are test fixtures, not anatomical estimates. The receiver consumes only the
new source's actual observed events. Native Unity, muscle dynamics and the live
brain are not executed by this transport comparison.

Only one output batch may be outstanding. Every 37th batch and the final batch
are retried exactly, including acknowledgement replay. Every 113th batch and
the final batch exercise backpressure: attempting to advance while the batch is
retained must fail without changing the neuronal snapshot. Publication and
acknowledgement must also leave that snapshot unchanged. Across the matrix this
gives 7,740 retry controls and 2,538 blocked-advancement controls.

All batches, canonical wire JSON, receipts, Avatar arrivals and receiver
completeness/pending/delivered counts are retained in `transport.jsonl`. The
receiver advances only through the reported source watermark. If an event's
delayed arrival lies beyond the final horizon, it remains explicitly queued;
the experiment does not invent additional neuronal completeness to flush it.
Its final accounting requires delivered plus queued events to equal all emitted
crossings, with no unpublished source events.

## Independent saved-data audit

`tools/analyse-receptor-motor-output.py` checks the evidence without executing a
model. It reconstructs output identities, source ordinals, anatomy, exact expected
batch intervals and delayed arrivals from the already audited raw candidate.
It verifies each wire hash from the exact retained canonical JSON, so differing
JSON float formatting cannot produce a misleading comparison. Original wire
values must independently match the expected fields and observed crossing times.

The audit checks all source observation arrays against the raw candidate and
between cadences, exact final state, input delivery counts, every receipt and
retry/control occurrence, receiver timing and retained tail events. It writes
`motor-output-analysis.json` before independent ODE controls/solves begin and
embeds the same report in the numerical comparison. A failure stops the study
and preserves the evidence; acceptance bounds are not relaxed.

## Execution and qualification

The 111-case/five-source prerequisite gate remains first and still awaits the
reported component result. After that gate is reported and successfully reviewed,
the frozen comparison window runs in this order:

1. Eighteen raw receptor/membrane candidate conditions.
2. Thirty-six active source/Avatar receiver conditions.
3. Saved-data audits of both sets of captures.
4. Six analytic receptor and 36 Jacobian controls.
5. Eighteen independent numerical reference integrations and the original
   reference/candidate/refinement contrasts.

The comparison reuses the exact compiled prerequisite probe. Its new input,
recorder and audit are included in protected package evidence. Managed runtime
dependencies are checked after the additional source runs too. The numerical
equations, amplitudes, durations, bounds and solver settings remain unchanged.
All execution stays in the independent visible PowerShell window, without live
monitoring; the operator reports completion before any later review.

A passing source/receiver comparison would establish sampled state preservation
and exact transport for active model output under these inputs. Presynaptic
events remain fixtures. Independent numerical accuracy, a biological presynaptic
source, spinal calibration, source-to-muscle/native coupling, sensory feedback
and physical recovery still need their own evidence.

Authoring evidence is `artifacts/receptor-active-output-authoring-20261116`,
including a zero-warning/error probe build, syntax/source inspection and the
declared matrix counts. Its D: copy is verified. No neuronal solve, test or
comparison ran during authoring, and no live component was selected.
