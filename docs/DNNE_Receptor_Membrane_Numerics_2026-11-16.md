# Prospective coupled receptor/motor comparison — 16 November 2026

This is the independent numerical comparison that follows the 97-case
receptor/membrane gate and its five original-source regressions. The candidate
recorder, saved-data audit, input protocol and independent reference are
authored, together with the deferred preparer and visible PowerShell launcher.
No numerical comparison has run or comparison package been created. The three
earlier operator reports remain pending.

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
D: backup. The dedicated visible PowerShell launcher is authored but has not run.
The reference expects `membrane-study`, `reference/motor-source`,
`reference/receptor-source`, `reference-preflight.json`, the input protocol and
frozen `configuration.json`. It verifies the complete numerical environment
and protected inputs again on successful or failed completion. No dependency
update is part of this work. Nothing reads the pending studies automatically.

## Deferred packaging and execution

The sequence remains: operator reports `motor-receptor-20261116-501da5`, review
its saved 74-case evidence, prepare and run the 97-case/five-source gate, then
wait for its separate operator completion report. Only after that report may
`receptor_membrane_gate.py review-gate PACKAGE NEW_REVIEW
--operator-reported-complete` inspect the new gate. This reviewer verifies the
complete C:/D: capture, source archive and binaries, exact test inventory,
original source analysis and managed runtime fingerprints without executing a
model. Its review and runtime metadata must receive a verified D: backup.

`prepare-receptor-membrane-comparison.py --gate-review REVIEW_DIRECTORY` then
runs with the existing qualified numerical Python. It requires a clean pushed
revision, that successful review, unchanged compilation inputs and the previously
qualified numerical environment. It copies the **exact compiled probe** from
the gate rather than rebuilding it. The original probe source archive and its
revision are retained separately from the new comparison package's source.
All copied binaries are hashed and protected. The independent motor reference
files must still match the already reviewed circuit capture.

The preparer extracts seven motor source files at the pinned Git revision and
checks their Git blob identities. It copies the verified receptor sources,
original README and provenance. It protects the protocol, numerical preflight,
reference code, all copied inputs and binaries. It requires at least 20 GiB free
on each capture/backup drive; this is a conservative startup requirement, not
a guarantee of maximum disk consumption. Preparation runs no cell or test.

After the fresh package is D: backed and verified,
`start-receptor-membrane-comparison-window.ps1 -PackageDirectory PACKAGE` opens
an independent visible PowerShell window. It checks dependencies, runs the 18
candidate conditions and then the independent reference. Exact matrix counts,
all contact deliveries, scope flags and the original numerical bounds are
mandatory. Numerical and managed dependencies are checked again after execution,
including on failure; frozen files and final backup are also checked. The window
stays open for the operator's report. There is no live-monitoring path.

`NativeMotorBridgeProbe --runtime-fingerprint NEW_ABSOLUTE_JSON` is a metadata
operation: it creates no cell or test. It records the .NET host, external managed
runtime files, architecture and selected numerical/JIT environment switches.
App-local files are covered by the separate complete binary manifest. This is
not a claim to fingerprint every operating-system component. Both the original
source gate and the comparison capture this metadata, so a changed runtime
cannot silently inherit the gate's result.

Authoring evidence is `receptor-comparison-rig-authoring-20261116`: a successful
probe build with zero warnings/errors, Python/PowerShell syntax inspection,
97 statically counted cases and five unchanged qualified reference files.
No new test, model, comparison package or window was executed during authoring.
The runtime capture and new launchers still require their future visible-window
execution; compilation is not a numerical result.
