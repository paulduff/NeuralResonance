# Isolated CPU cost baseline before CUDA work

This study measures existing DNNE calculations and Avatar's isolated motor-unit
plant. It does not implement CUDA, run a live world or qualify the biology. It is
the first measurement stage of `DNNE_Compute_Direction_2026-11-09.md`; live service
transport, whole-system scheduling and Unity physics remain separate work.

## Implementation and workload

`StructurePerformanceEvidence` is an internal opt-in collector. Its constructor
guard requires an absolute fresh state directory and an `isolated-` instance.
The public host constructor cannot select it. Stopwatch values never feed neural
integration, source events, synaptic updates or biological clocks. The default
path executes null checks but no timing calls; that overhead is not claimed to be
zero or benchmarked against the previous binary. No model equation, parameter,
save fingerprint or protocol was changed.

`DnneComputeProbe` runs full configured populations with a reference substrate
bath, a fixed classical microtubule mode and deterministic diagnostic inputs.
The workload uses one biological second, fifty 20 ms host ticks and sixteen
inputs per tick. Inputs have fixed identity and fractional event times; they are
workload fixtures, not a qualified anatomical input pattern. It includes:

* TemporalAssociation's current Izhikevich/STDP host profile.
* A separate TemporalAssociation STDP/tag-capture fixture with explicit local
  modulation to cell 166, to exercise that candidate workload.
* CerebellarGranule's current LIF/MossyFiberLTP profile.
* InferiorOlive's current HH/STDP profile.
* PurkinjeCellLayer's current HH/PurkinjeCalciumLTD profile.
* SpinalCordMotor's current Izhikevich/STDP profile.

These use the current global event-aligned engine and its existing limitations.
The cortical double-state repair is not silently promoted. The spinal reference
motoneuron candidate is not substituted for the existing live host model.

Each case has one instrumented warmup, then three plain/instrumented pairs, with
alternating order: 42 runs and 18 observation comparisons. Warmups are retained
but excluded from reported medians. Timing includes the existing runtime's GC
and JIT behavior. This .NET 10 diagnostic executable is not an absolute throughput
prediction for every deployed host/runtime or for an otherwise idle machine.
Background contention can affect wall time; no existing experiment is stopped or
monitored to manufacture an idle measurement.

## What the times mean

| Measurement | Included work |
| --- | --- |
| Enqueue | Validate incoming batches and queue their deliveries, in process |
| DueInputs | Dequeue/deliver inputs, receptor integration and presynaptic learning |
| CellDynamics | Supply assignment, existing intrinsic drives and cell stepping |
| ContactLearning | Continuous modulation and learning at actual postsynaptic spikes |
| SpikeRouting | Existing local/outgoing event construction and reflex bookkeeping |
| Diagnostics | Per-cell diagnostic aggregation at host endpoints and final tick diagnostics |
| TickTotal | Complete successful `ProcessTickAsync`, including the above and unbucketed scheduling/timer overhead |
| Drain | Drain outgoing spikes, in process |

The five phase buckets do not overlap. TickTotal contains them; it must not be
added to them. Presynaptic learning belongs to DueInputs, not ContactLearning.
Raw tick times with and without instrumentation and their overhead ratio are
reported separately. The phase costs are observed with instrumentation enabled;
they are not corrected estimates of uninstrumented per-operation latency.

Current-thread allocation and process-wide GC collection deltas surround the
three timed calls. Process CPU time and the broader complete-run time additionally
include construction, evidence capture and shutdown, but exclude final file
serialization. Evidence capture and JSON output are outside the timed tick calls.
Keep these scopes separate when choosing what to accelerate.

## Equality evidence

Before accepting timing results, each pair must agree exactly on every saved
tick acknowledgement and outgoing event payload/order, plus:

* every private instance field in each `ModelNeuron` and its nested state;
* inbound and outbound contact state;
* cell postsynaptic traces and their clocks;
* pending feedforward/feedback envelopes and their delivery priorities;
* the last biological timestamp and processed tick.

Floating fields in state evidence are encoded as IEEE bits. This is a selected
state audit, not an exact checkpoint of every engine field or a persistence
continuation claim. Current generated event MessageIds are random between runs:
only that field is excluded from semantic comparison. Raw outgoing IDs are saved
and checked for uniqueness; source/target cell, projection/contact identity,
timestamps, payload and event ordering remain compared. Original input IDs are
fixed and saved separately. Do not mistake excluded generated UUID equality for
proof of replayable distributed event identity.

## Existing Avatar plant and serialization

Before extending Avatar, its existing `AvatarIsometricMotorUnitPlant`,
`AvatarMotorUnitCalcium`, motor publisher/receiver and integration tests were read.
The study calls those implementations without changing them.

At 1, 32 and 256 explicitly declared units, a deterministic fixture supplies 25
events per unit over one second, with fractional conduction delay and the existing
0.125 ms solver grid. Each size gets a warmup and three direct/JSON-round-trip
pairs: 21 runs and nine equality comparisons. Measure publisher, serialization,
receiver and plant advancement separately. Require exact frame/event and private
plant state agreement, all events delivered and no pending/faulted state.

JSON round-trip measures serialization cost only. There is no socket, gRPC,
ControlProgram queue or cluster latency in this study. Fixed-length force from
fixture events does not establish a neuronal source, dynamic muscle/tendon
behavior, Unity collision cost, standing or recovery.

## Execution and acceptance

Release builds and Python/PowerShell syntax checks are development checks. Actual
tests and measurements run only in the independent visible PowerShell window.
The original frozen source required 53 selected cases first: 27 biological-contact,
eight explicit-recipient, two new timing guard/default, fifteen isometric-plant
and one solution-manifest case. The retry adds three evidence-encoding regressions,
for 56 cases; see `DNNE_Compute_Failure_Review_2026-11-09.md`. This is not the full unit suite; the old
intermittent consolidation assertion is unchanged and outside this selection.

`analyse-dnne-compute.py` audits saved files independently: 42 neural runs, 21
motor runs, all 27 equality contrasts, SHA-256 linkage to saved state/output,
finite nonnegative times, clock conversion, phase call accounting and event
counts. It reports all samples and medians. There is no pass/fail speed threshold
and no inferred CUDA speedup. A failed equality or incomplete capture is a failure,
even if the timing looked attractive.

Prepare only from clean pushed source using `prepare-dnne-compute.py`, verify the
initial D: copy, then launch `start-dnne-compute-window.ps1`. Builds and tests run
under frozen `source/artifacts` so repository-reading tests resolve the frozen
solution. The runner records source and binary hashes, preserves failure evidence,
and verifies the completed D: copy. Do not live-monitor; inspect after the operator
reports the outcome. Earlier outstanding experiments remain untouched.

Next, use the measured breakdown and correctness constraints to choose the first
batched CPU/CUDA execution boundary. Continue live service/Unity profiling before
making whole-system acceleration or usable-Avatar claims.
