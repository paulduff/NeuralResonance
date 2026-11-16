# Observed neuronal source for the next native-body connection

The new isolated source adapter exposes actual somatic crossings from the
previously checked five-compartment motor-neuron reference. It is preparation
for connecting that source to the moving Avatar muscle/native-hinge path. It
does not change the live brain, Avatar, editor or World selection.

The earlier `motor-unit-circuit-20261109-83e7ee` already established delivery of
these crossings through Avatar's motor transport, calcium and isometric force
path. Its 88 tests and eight conditions passed; see
[the completed circuit review](DNNE_Somatic_Motor_Unit_Results_2026-11-09.md).
The subsequent native-hinge studies used fixture events. The remaining connection
is the running neuronal source to the moving body, without letting native body
steps or transport scheduling alter the neuron's integration.

## Source ownership and delivery

`tools/NativeMotorBridgeProbe/ObservedMotorSource.cs` owns one fresh reference
cell with the existing compensated RK4 integration, maximum step 0.003125 ms,
0.1 ms source sampling, and six-second horizon. The quiet and stimulated input
protocols are unchanged: the latter injects 6,000 pA from 1,000 to 3,000 ms.
Endpoint-difference scheduling and exact integrated-duration accounting use the
existing `ClockAudit` implementation. The source advances on its own grid;
publication, acknowledgements and body endpoints cannot split that grid.

Each original fractional somatic crossing becomes one immutable emission. Its
identity includes the evidence run, source scenario, cell, projection, synapse
and ordinal. These route-qualified IDs deliberately differ from the older
circuit probe's ID construction. No population rate, prescribed movement or
force is converted into a neuronal event.

The adapter uses the existing `MotorAxonReplayBuffer` and Avatar receiver. One
declared source instance owns exactly one contact in this diagnostic. A full
retained-packet/event buffer blocks further source advancement. Publication
failure preserves the unpublished events and watermark; retry returns the same
retained batch. This is a bounded in-memory experiment, not crash recovery or a
durable neuronal checkpoint. If a single interval itself exceeds a deliberately
small event capacity, publication stops intact and the experiment requires
review; events are never clipped to make it continue.

## Prospective source gate

`NativeMotorBridgeProbe --source-check ABS_REFERENCE_FOLDER NEW_ABS_OUTPUT_FOLDER`
is an isolated qualification mode, not yet executed. It requires the completed
boundary source protocol, original numerical acceptance report, original
integration accounting and original CSV traces. It starts no native Unity player
and advances no muscles.

Five six-second conditions are declared before execution: quiet at 10 ms packet
cadence, active at 1/10/50 ms cadence, and active at 10 ms with every seventh
packet deliberately retained and retried. Each condition must:

- Reproduce every saved CSV byte for all 30 state traces, every crossing time,
  integration-call count and exact integrated duration from the checked source.
- Preserve unique, ordered event identities and fractional times. All active
  packet cadences must produce the same original emission sequence.
- Deliver the exact expected event records through Avatar, including source,
  contact, motor unit, session and original time plus the declared 2.0625 ms delay.
- Account for accepted, delivered and pending events through the complete horizon.
- Leave neuronal state unchanged during publication, acknowledgement, blocked
  advancement and exact retry; exercise every scheduled retry rather than merely
  finish a partial run.

Six new focused tests cover fresh interval ownership, output backpressure/retry,
quiet cadence invariance, route/population completeness, detached snapshots and
single-thread ownership. These short checks do not establish active firing; the
full source gate is required. The source gate must pass before using this adapter
in a native-body experiment, and that experiment must also use a reviewed physical
resolution. No live-source promotion follows automatically from either result.

## Evidence and current status

The static provenance check `tools/inspect-observed-motor-source.py` verifies
selected files against the reviewed circuit's C:/D: manifest and compares their
normalized text with the working source. All seven selected neuron/channel,
scheduler and transport implementations are unchanged. The original capture's
manifest is
`ABFC1F69D15EE010E2EDD8E6980C373CECEB53A896BB6D66CDD68FCB0CFCBCF0`.
The derived report is
`artifacts/observed-motor-source-preparation-20261116/source-review.json`.
The report and both build logs have a verified three-file-plus-manifest backup
at `D:\DNNE-desktop-runs\UnityWorld\observed-motor-source-preparation-20261116`.
This is source provenance, not execution of the new adapter.

Authoring builds are recorded under `artifacts/observed-motor-source-authoring`.
Both builds succeeded with zero warnings and zero errors. The adapter and tests
are compiled preparation; no new test, source trajectory,
muscle solve or native run has been executed. The operator has since reported
native refinement `native-hinge-refinement-20261116-421387` complete; consult its
separate review. Freeze and back up the source experiment before running it in a
visible independent PowerShell window; inspect outcomes only after the operator
reports completion.

The source gate now has a frozen-package preparer
`tools/prepare-observed-motor-source.py`, a visible-window launcher
`tools/start-observed-motor-source-window.ps1`, and an independent saved-data
auditor `tools/analyse-observed-motor-source.py`. They preserve the source inputs
and original reference CSVs from the reviewed circuit capture, verify selected
C:/D: bytes and unchanged cell/transport source, and require a clean pushed
revision before freezing. The 66-case focused suite comprises six new ownership
checks, 32 transport, 16 replay, 11 motor-cell and one solution-manifest check;
the exact per-method multiplicities are checked in the TRX report.

The visible run executes those tests and the five full source conditions, then
independently checks the saved CSV hashes, baseline crossing times, packet
ordering, cell/contact identity, declared conduction delay, exact replay records
and complete event accounting. It captures built binaries and final protected
file hashes and verifies D: backup on success or failure. It does not start
Unity, integrate muscle dynamics or rerun an independent ODE reference. The
earlier isometric circuit remains valid evidence; this gate checks the newly
factored ownership/publication adapter before reusing it with moving muscles.

The source is a cat MG reference under current injection; the previous recipient
uses a human tibialis-anterior slow-unit row. This mixed pairing tests interfaces
and numerical behavior. Fixed conduction delay, ideal endplate transmission and
reference bath remain approximations. Active axonal propagation, physiological
calibration, spinal synaptic recruitment, sensory feedback and whole-brain motor
authority remain open. Avatar balance and fall recovery are not qualified.

## Launch record

`observed-motor-source-20261116-badc1b`, frozen revision
`3cd9c72ba0c53578a694e7a5a4b32b2a27edea28`, is launched in independent visible
PowerShell task `DNNE-Observed-Motor-Source-6619bb55f4a0`. All 1,649 initial files
plus manifest are D: verified and rechecked by the launcher. Preparation verified
the original source traces, unchanged neuronal/transport implementations and
66-case method coverage. This is the only pending operator report. No post-launch
output has been inspected; do not monitor. The completed native refinement is
reviewed separately and does not establish this new adapter's behavior.

The operator subsequently reported `badc1b` complete. The full C:/D:/source and
saved-analysis review passed; see
[the completed source results](DNNE_Observed_Motor_Source_Results_2026-11-16.md).
This run is no longer pending. The subsequent quiet-epoch ownership extension
adds two tests (eight source tests, 68 in a standalone source gate). The combined
neuronal/native experiment reruns the full source gate before using that extension;
the earlier 66-case result does not qualify the new ownership path by itself.
