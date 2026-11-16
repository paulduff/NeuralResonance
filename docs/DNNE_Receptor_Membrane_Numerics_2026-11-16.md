# Prospective coupled receptor/motor comparison — 16 November 2026

This is the independent numerical comparison that follows the 97-case
receptor/membrane gate and its five original-source regressions. The candidate
recorder, saved-data audit, input protocol and independent reference are
authored. No numerical comparison has run, no comparison package or launcher
has been prepared, and the three earlier operator reports remain pending.

The purpose is to qualify the new coupling's numerical behaviour before it
supplies motor events to a body. It does not qualify the source of the input
spikes, spinal physiology, a live brain or recovery. Inputs here are explicit
presynaptic **fixtures**. The postsynaptic cell is actually integrated; its
crossings are observed, not prescribed.

## Fixed inputs

`tools/receptor_membrane_protocol.py` declares six fresh-cell conditions:

| Condition | Contact conductances and locations |
|---|---|
| Quiet | One declared AMPA contact, no releases |
| Soma excitation | One 400 nS AMPA contact at the soma |
| Local excitation | One 200 nS AMPA contact on dendrite 1 |
| Distributed excitation | Four 50 nS AMPA contacts, one per dendrite |
| Distributed excitation with inhibition | Those four contacts plus 200 nS GABA-A at the soma |
| Inhibition only | One 200 nS GABA-A contact at the soma |

These amplitudes are numerical stress inputs, not estimates of individual
anatomical synapses. They must not become runtime anatomical defaults. The
large soma input deliberately challenges threshold crossing. A passing matrix
must contain a nonempty excited crossing train in at least one excitation
condition; quiet reference conditions must remain without crossings. Failure
to exercise firing is reported as a failed requirement, not silently omitted.

Each condition runs from the original `-65 mV` initialization through 1,500 ms.
There is a full quiet first second; state is never reset at stimulation. AMPA
fixtures fire at `1000.125 + 10*i ms`, `i=0..19`. Inhibitory fixtures fire at
`1050.375 + 20*i ms`, `i=0..7`. Every declared axonal delay is `0.125 ms`.
The receptor pulses remain `1 mM` for `1 ms`. The post-input tail is retained.

All six conditions run at maximum membrane steps `0.0125`, `0.00625` and
`0.003125 ms`, giving **18 candidate conditions**. Original events, receipts,
stable contact identity, accepted endpoints and exact supplied durations are
saved. Distributed contacts receive the same original excitation events rather
than new identities generated for each recipient. All 648 contact deliveries
across the matrix must be accounted for.

## Candidate and independent reference

`NativeMotorBridgeProbe --receptor-membrane INPUT NEW_OUTPUT` uses the isolated
owner directly. Each saved CSV row represents one actual accepted membrane
step, including additional release/pulse-end and 0.1 ms observation boundaries.
The recorder checks that an outer observation did not conceal several internal
steps. It also checks exact binary elapsed time in the existing `2^-80 ms`
audit units and retains all thirty membrane/accounting states and every
contact's occupancy. This command is only for the future visible study window.

`tools/audit-receptor-membrane.py` independently reconstructs source/contact
identities, original message hashes, batch receipts, supplied durations,
observational crossings and expected delivery counts from saved evidence. It
checks every input/observation boundary is present and that state/identity
ordering is unchanged across resolutions. It does not execute a model.

`tools/receptor_membrane_reference.py` reuses the existing independent
source-derived motor RHS and reads the pinned receptor declarations. It adds
each receptor as an ODE state, `dr/dt=alpha*C*(1-r)-beta*r`, and adds its current
only to its physical compartment and the external-charge ledger. It does not
use the candidate's closed-form receptor evaluator. It carries continuous
state across every transmitter boundary, without resets or clipping.

The reference runs the existing tight DOP853, tighter DOP853, and Radau settings
for every condition: **18 reference integrations**. Radau uses the checked
bounded motor Jacobian with analytic receptor/coupling derivatives; three
accounting columns remain zero. Six analytic receptor controls and 36
directional Jacobian controls precede the full comparisons. Analytic controls
retain their accepted states; full references retain accepted states and dense
evaluation on the union of all candidate endpoints.

All candidate accepted endpoints are compared directly against independent
reference values at those exact times. No candidate trajectory is interpolated
to conceal a missing point. All three reference pairs must agree before the
tighter DOP853 trajectory can support the candidate comparison. This checks
accepted endpoints and solver refinement; it is not a proof of a uniform error
bound at every continuous instant between them.

## Prospective bounds and execution prerequisites

The existing motor voltage/gate/calcium/crossing bounds are unchanged:

| Quantity | Reference agreement | Candidate agreement |
|---|---:|---:|
| Voltage | 0.005 mV | 0.05 mV |
| Gate occupancy | 5e-5 | 5e-4 |
| Calcium | 5e-8 mM | 5e-7 mM |
| Crossing time, with equal counts | 0.001 ms | 0.01 ms |
| Receptor occupancy (new) | 1e-8 | 1e-6 |

The existing calcium and charge ledger bounds remain `1e-10 mM` and `1e-5 fC`.
Reference gate/receptor domain slack is `1e-10`; calcium slack is `1e-13 mM`.
The candidate's saved gates/receptors must remain strictly within `[0,1]` and
calcium nonnegative. Each refinement contrast requires every error component
to be no greater than `max(1e-12, 1.1*coarser_error)`. There are 18 reference
contrasts, 18 candidate contrasts and 12 refinement contrasts. No bound may be
changed after inspecting a failed matrix.

The motor source remains ModelDB 239582 revision
`0d11354490fad5fcd940d016a8d69b000693fd81`; receptor source is ModelDB 18500
revision `937ed5d0d04519d4cd7af474a0f6c53a6e437e06`. These combine the declared cat
motor model with hippocampal reference receptor kinetics; they are a numerical
fixture, not a physiological spinal calibration.

Before running: review the prerequisite component/source gate, freeze a clean
pushed revision and source inputs, pin the unchanged numerical environment,
check available C:/D: space for all endpoint traces, and create a fresh verified
D: backup. A dedicated visible PowerShell launcher still needs to be prepared.
The reference expects `membrane-study`, `reference/motor-source`,
`reference/receptor-source`, `reference-preflight.json`, the input protocol and
frozen `configuration.json`. It verifies the complete numerical environment
and protected inputs again on successful or failed completion. No dependency
update is part of this work. Nothing reads the pending studies automatically.
