# Receptor-driven motor output candidate — 16 November 2026

The new isolated `ReceptorMotorSource` connects the receptor-driven motor cell's
observed voltage crossings to the existing motor-axon protocol. Its companion
`ReceptorMotorLane` publishes retained events to Avatar's existing receiver.
This is authored, compiled code; its 14 new checks have **not run**. No live host,
Avatar, Unity world, editor selection or saved brain has changed.

This closes a specific implementation gap: the receptor-driven cell previously
retained its crossings but did not own an outgoing motor lane. It does not
establish a biologically valid presynaptic source, spinal receptor calibration,
active axonal propagation, a closed sensory loop or recovery after a fall.

## Cell ownership and observation

The source privately owns one `ReceptorDrivenMotoneuron`. Every receptor contact's
target instance and neuron must match the output identity. The membrane,
receptor equations, arrival boundaries and compensated integration are unchanged.
Inputs still require explicit per-contact completeness; a missing batch cannot
be interpreted as silence. The input session is also the motor output session.

Advancement follows the source's declared sample grid, default `0.1 ms`, with a
default maximum internal step of `0.003125 ms`. Only this operation advances the
cell. Publication cadence, retry and acknowledgement never integrate it. The
sample interval and maximum step are immutable configuration: changing them is
a different numerical configuration, not a transport adaptation. Large sample
intervals used in a diagnostic control are not recommended live body timings.

Each observed upward soma crossing becomes one retained output record containing
its original interpolated crossing time, ordinal and stable source, projection,
synapse and message identities. The source checks its earlier crossing history
is unchanged. It adds no rate-to-spike conversion, threshold reset or prescribed
output train. The existing crossing detector remains an observational readout
of the five-compartment model, not an independently modelled active axon.

The configuration fingerprint records the declared model/receptor versions,
cell parameters, contact definitions, identity, initial voltage, sample interval,
solver step and diagnostic block. Captured states are detached copies; emission
lists are read-only. This fingerprint is configuration evidence, not a durable
joint checkpoint or a substitute for the frozen source/binary manifests.

## Transport and Avatar

A motor lane can attach to a source only at its exact current neural epoch and
only if its entire retained prehistory contains no crossings. This permits
explicit quiet priming without resetting the neuron or moving its timestamps.
The anatomy must declare exactly this cell for its source instance; one cell
cannot declare completeness for a population. Binding transfers exclusive input
admission and advancement to the lane. Read-only capture remains available.

Output uses the existing `MotorAxonReplayBuffer`, `MotorAxonPublisher` and
`AvatarMotorAxonReceiver`. Their implementations and wire format are unchanged.
The new tests exercise Avatar's actual receiver, including its existing explicit
conduction delay and original-event acknowledgement. Receiver acceptance is still
separate from muscle consumption, force generation and native physics.

Backpressure stops subsequent cell advancement before changing neuronal state.
If a completed source interval contains more crossings than fit one packet, the
lane can publish a prefix through its last included original crossing. It cannot
claim the source's later completion time while withholding an intervening event.
Remaining crossings stay retained and are drained after capacity returns. Exact
retries use the retained batch. Exhausting the declared epoch preserves the
unpublished history and stops progress; no event is dropped or reidentified.

All mutating operations have one simulation-thread owner. An invalid receipt
cannot release capacity. Missing input can be supplied and the same cell can
continue; a failed integration remains faulted in its captured state.

## Prospective checks and order

The 14 new cases cover:

- Input completeness, propagation allowance and recovery after missing input.
- Bitwise state preservation against the directly owned active membrane for an
  accounted quiet interval.
- Target/output identity, source session, epoch and population rejection.
- Quiet priming, exclusive ownership and retention of nonquiet prehistory.
- Explicit diagnostic opt-in, observed crossing identity and Avatar delay.
- Small-packet prefixes, capacity backpressure, epoch exhaustion and exact retry.
- Publication cadence, wrong receipts, detached snapshots and thread ownership.

The transport controls use explicitly labelled passive cable depolarizations and
inhibitions to produce observed crossings. These are **not active neuronal firing
qualification**. A lane rejects diagnostic channel blocks by default; the tests
must explicitly opt in. Active receptor-driven firing still requires the pending
coupled numerical comparison, followed by full active output/body evidence.

These cases join the deferred receptor/membrane gate: **111 focused cases**, then
the same five complete original six-second source conditions. The prior 97-case
authoring captures remain intact. The gate preparer verifies both that original
membrane authoring evidence and the new source authoring capture. Its reviewer
and the later numerical-comparison preparer now require all 111 cases.

First wait for the operator's completion report for receptor component `501da5`
and review that evidence. Then prepare/backup/run the 111-case gate in a visible
PowerShell window. After its separate completion report and successful review,
the 18-candidate/18-reference numerical comparison can follow. No pending output
was inspected and no extra window was opened while authoring this connection.

`artifacts/receptor-motor-source-authoring-20261116` records the successful test
project build, static inventory and source snapshots, with a verified D: copy.
Build success is not a passed test or numerical comparison. Existing numerical
bounds, old saves, pending packages and their frozen source remain unchanged.
