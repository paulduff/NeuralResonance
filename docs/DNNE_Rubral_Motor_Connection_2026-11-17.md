# Actual DNNE source to the isolated motor receiver

The completed cubic comparison (`receptor-cubic-20261117-94d05d`) passed the
original receptor/membrane numerical bounds, twelve refinement checks and the
36 source/Avatar receiver conditions. It used prescribed presynaptic events.
The next experiment supplies those events from actual DNNE neurons, preserving
the qualified postsynaptic equations and existing Avatar receiver implementation.

## Candidate and scope

`IsolatedRubralSource` privately owns the existing 256-cell `RedNucleus`
`StructureEngine`, with the service's Izhikevich/STDP profile. This is the
existing reduced DNNE model, not a new anatomically calibrated red nucleus.
Existing intrinsic drives, metabolism, synapse rules and routing remain intact.
An explicit reference substrate bath is used. This does not validate the source's
physiology or transfer its learned synaptic efficacy into the motor reference.

The owner accepts the entire declared diagnostic input fixture before execution,
copies it, and admits each event before the engine handles its conduction delay.
It advances exactly 0.125 ms, obtains the existing atomic tick/drain result and
retains immutable original event JSON with a SHA-256 fingerprint. Every one of
the 256 primary routes is described before observing activity. The target is the
lexicographically first existing spinal recipient, with all its rubral contacts;
it is not chosen after finding a neuron that fires.

Original instance/neuron/projection/synapse/target identities, message IDs,
timestamps and all message fields are retained. Presynaptic timestamps still
mean the existing engine's completed integration substep; they are not cubic
crossing estimates. The private owner cannot advance until its exact pending
frame is acknowledged. Exact acknowledgement replay is inert. Faulted sources
cannot resume; failed observations distinguish the engine's requested tick time
from successfully validated completed frames and retain drained or undrained
original output. This is an in-memory experiment, not a crash-restart protocol.

The receiver is the previously compared optional cubic `ReceptorMotorSource`,
using the cat low-threshold reference parameters, 0.003125 ms maximum steps and
0.1 ms samples. Selected contacts use the existing two-state AMPA reference,
50 nS conductance, 0.125 ms delay, and dendritic compartment `1 + sourceIndex % 4`.
Those choices are diagnostic, not anatomical fits. Original quanta/modulation
fields are preserved but are **not** converted into contact strength: each event
uses the existing fixed unit pulse. Unsupported overlapping pulses must fail
without deleting or retiming events. Motor output comes only from the motor
cell's actual crossings, through the existing motor protocol and actual
`AvatarMotorAxonReceiver`, with a declared 2.0625 ms conduction delay.

## Prospective experiment

Four fresh 1,500 ms source conditions retain identical source/contact/input
identities and separate sessions/storage: no evoked fixture, and fixture quanta
0.25, 1 and 4. These declared inputs stay below the existing cell's 5-quanta
input clamp; downstream routing/contact processing remains unchanged. Active
conditions have twenty 256-event pulses starting at
1,000.125 ms, spaced by 10 ms. The M1-labelled input is explicitly a fixture;
there is no simulated upstream cortical population. The no-evoked condition
can still have endogenous or existing intrinsic-drive activity.

Each source executes once and broadcasts the same original frames into three
independent postsynaptic branches: packets of one frame, packets of seventeen,
and seventeen-frame packets with every selected receptor contact disabled.
All branches must account for explicit source completeness, including silence.
Missing completeness must reject advancement before changing state. Exact input
retries, output retries and deliberately full source/motor buffers check replay
and backpressure without substituting rates or desired motor spike schedules.

The two connected branches must have byte-identical sampled motor trajectories
and identical motor events. The disabled branch must have zero admitted events
and zero motor output. Every selected original input must be accounted for;
arrivals beyond the final motor horizon remain pending. Likewise, motor axonal
events arriving after the source's final proven horizon are retained explicitly.
The experiment never declares future neuronal silence to drain those queues.

All 48,000 source frames retain full original output and rounded telemetry for
all 256 cells, with energy diagnostics. Rounded telemetry is not a complete
accepted-state numerical trajectory. The twelve motor branches retain 180,012
sampled states, exact input payloads/receipts, motor packets/receipts, arrivals,
and terminal pending events. The saved-data audit checks these records directly,
including fingerprints, contact mapping, interval coverage and final accounting.

The visible PowerShell window rebuilds and requires exactly **136 focused test
cases** (124 preserved cases plus twelve ownership tests), then runs the four
conditions and audit. It checks frozen source and executable hashes, built binary
hashes and the managed-runtime fingerprint against the completed cubic capture.
The package and final evidence are verified on D:. No tests or model integrations
are run inline; outputs are reviewed only after the operator reports completion.

`DIAGNOSTICS_COMPLETE` means the specified accounting/causality controls finished.
`ObservedNeuronalMotorResponse` separately records whether any connected branch
actually produced motor spikes. A false result is not neuronal motor success.
A true result still does not qualify upstream numerical accuracy, physiology,
learned contact strength, live whole-brain authority, muscle/native body coupling,
sensory feedback, balance or Avatar recovery. No live service or editor selector
changes, and no World/Unity process is started.

Authoring: source builds and Python/PowerShell syntax checks are separate from
the pending tests. The independent continuously moving spindle reference remains
the other outstanding numerical task.

## Launch record

Launched as `rubral-motor-20261117-e42efc`, frozen at
`4cffbcc2a05e8e8233131c01c7c22b7e0975fee9`, in independent visible task
`DNNE-Rubral-Motor-f79577aed07e`. The initial 1,734 files plus manifest were
verified on D:. The authoring probe/test projects built with zero warnings or
errors; Python/PowerShell syntax and the 136-case source inventory were checked.
These checks did not execute tests or neuronal integrations. No post-launch
output, status or process has been inspected. This is the only pending operator
completion report; the goal remains active and unfinished.
