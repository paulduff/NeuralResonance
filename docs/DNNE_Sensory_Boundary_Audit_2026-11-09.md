# Avatar-to-DNNE sensory boundary: source audit

This inspection followed the existing Avatar/Unity/body-transducer/receiving-cell
path before selecting the next closed-loop repair. It executes no model, test,
benchmark or live service and does not inspect the pending Golgi output. The
motor-clock discrepancy is a separate issue: the defects below are visible in
the actual sensory implementation and can affect the amount and timing of input.
Their contribution to observed falls has not been measured.

## Findings in the owning source

| Boundary | Current implementation | Consequence |
| --- | --- | --- |
| Physical time at capture | `UnityWorld/Assets/NRE/Runtime/SensoryFrames.cs`, `BodyFrame.Capture` and `ContactFrame.Capture`, stamp UTC wall milliseconds. `WorldController.Step` advances physics by a fixed float `StepSeconds`, counts `physicsSteps`, and pauses independently. The frame does not carry that physical step/epoch. | Body derivatives are not explicitly tied to the physical time advanced. Pause duration, execution speed and request delays cannot be separated from simulated time using the current frame contract. |
| Capture cadence | `WorldController.BodyLoop` waits 50 ms of wall time, captures one body frame, awaits its HTTP response, then awaits contact requests serially. | Sampling interval depends on HTTP and contact workload as well as the nominal wait. This is not a fixed biological sampling clock. |
| Brain time at ingress | `ControlProgram/Program.cs`, the body-frame endpoint, calls `Transduce(descriptor, state.Tick, state.SimulationClockMs)`. `AddPopulation` uses that supplied brain timestamp for every generated message. | The physical sample's original clock is replaced for neuronal input. There is no declared body-to-brain epoch mapping or inclusive source-completeness boundary in this endpoint. |
| Derivative clock | `PhysicalBodyTransducerRuntime.Transduce` computes elapsed time from successive descriptor timestamps, clamps it, and uses it for acceleration/joint derivatives. `VestibularLabyrinthTransducer` uses the resulting interval for adaptation/filter updates, with its own clamp and first-frame fallback. | Kinematic derivatives and receptor dynamics follow capture timing, while emitted stimuli follow the brain's different clock. A faster or paused physical run is not represented explicitly. |
| Receptor-to-event conversion | `PhysicalBodyTransducerRuntime.AddPopulation` creates one to five new messages for every active population on every frame. Activation controls count, vesicle quanta and reuptake. Tick/sequence parity also influences the BURST label. | Equal physical activation can supply different total input when frame count changes. These are frame-generated receptor stimuli, not observed afferent membrane firings. Merely renaming them neuronal events would preserve the defect. |
| Muscle spindle direction | `AddMusclePopulations` uses `abs(lengthFraction - 1)` and `abs(velocity)` for its primary/dynamic spindle channels. | Equal departures above/below optimal length, or equal shortening/lengthening speeds, map to the same activation in these specific channels. Some joint/opponent channels do preserve sign; the entire sensory system is not directionless. |
| Duplicate/order handling | `ExchangePrevious` stores the incoming descriptor before checking for non-increasing sequence/time, then returns no previous frame on an invalid order. `AddPopulation` nevertheless creates fresh message UUIDs. | A duplicate or backward body frame is neither an exact acknowledged replay nor rejected before state change. Its new UUIDs bypass the receiving cell's duplicate-message protection. |
| Source identity | Previous-frame and labyrinth state are indexed by `InputSource`. Output `SourceInstanceKey` and `CreateStableSynapseId` include side/structure/receptor/fiber but omit `InputSource` and an explicit session. | Two distinct sources can maintain separate transducer state yet alias neuronal source/contact identity. Exclusive-world ownership reduces exposure but does not repair the contract or delayed traffic from an earlier epoch. |
| Receiving event boundary | `StructureEngine.EnqueueSpikeBatchAsync` preserves a message's timestamp plus conduction delay and deduplicates message UUIDs. `ProcessDueQueue` delivers due events into actual receiving cells. Late transport is integrated at the current boundary. | Downstream neurons do exist. Their existence does not establish that upstream stimuli were afferent spikes or arrived on a causally complete common clock. Preserving this receiver is necessary but insufficient. |

For a fixed active receptor, the number of messages is proportional to accepted
frame count because `AddPopulation` has no elapsed-time or membrane state. This
is a deduction from that function, not a measured whole-brain firing-rate result.
Similarly, the absolute-value mapping loses the specified signs algebraically;
this audit has not run a physiological spindle comparison.

The shared `PhysicalBodyFrameRequest` currently contains a long timestamp and
sequence but no physical session, step duration, completed-step count or clock
mapping. The older headless/WPF World and WPF Maze clients also populate this
contract. A repair must handle each producer explicitly, not interpret their
existing wall timestamps as simulation time or silently change their semantics.

## Existing Avatar mechanisms to preserve

The body has measured muscle length/velocity/force, bilateral canal/otolith
processing, plantar-pressure fields, contact mechanics, physiology and
vestibular/proprioceptive routes. The Unity migration record and existing
`PhysicalBodyTransducerTests`, `VestibularLabyrinthTransducerTests`,
`UnityBodySensorTests`, `UnitySensoryWireTests` and authority-boundary tests cover
useful components. They do not establish a common physical/neural clock,
per-afferent membrane firing, or frame-cadence independence of total input.

Do not remove physical sensors, bypass the afferent circuit with a balance policy,
or multiply quanta by elapsed time and call that a neuronal repair. Muscle
feedback must retain direction, history and eventual fusimotor input. The
existing measured-force/isometric motor work remains relevant to closing that
loop; no sensory approximation is promoted by its success.

## Next implementation boundary

First establish an explicitly isolated physical-sample transport and timing
contract. It needs a declared body/session identity, physical timestamps derived
from actual completed physics steps, an explicit mapping to the neural epoch,
immutable original samples and exact retry receipts. A frame is a physical sample,
not a spike. Missing samples/completeness must stop dependent advancement;
duplicates, order conflicts and clock changes must not silently reset receptor
state. Wall time remains useful for operational freshness only.

Keep capture resolution separate from transport packet size. Preserve every
sample on the fixed physical grid when changing batching; changing the actual
physical sampling resolution is a separate numerical experiment. Sparse endpoint
samples cannot reconstruct unobserved collisions or acceleration, so interpolated
continuity must not be asserted as measured biology.

Next qualify receptor mechanics and individual afferent membrane/event generation
on that clock. Transport cannot supply the missing physiology. The isolated
closed loop must demonstrate physical perturbation â†’ receptor state â†’ actual
afferent firings â†’ declared spinal contacts â†’ motor-cell firings â†’ muscle force,
with appropriate delays and identity retained. Connect it to Unity and update
editor provenance only after those component and coupled checks pass. A shared
clock alone is not evidence of standing or recovery.

## Primary-source direction for the spindle replacement

[Mileusnic et al. (2006)](https://pubmed.ncbi.nlm.nih.gov/16672301/) model intrafusal
fibers and fusimotor effects; their mechanics offer a more justified starting
point than absolute length/velocity channels. A firing-rate output still needs
an explicit neuronal encoding mechanism for the present objective.

[Blum et al. (2020)](https://pmc.ncbi.nlm.nih.gov/articles/PMC7769569/) link spindle
response history to intrafusal cross-bridge mechanics and demonstrate a
conductance-based spike-generation example. Their article identifies a public
[author repository](https://github.com/kylepblum/MechanisticSpindleManuscript).

[Housley et al. (published online 2023)](https://pmc.ncbi.nlm.nih.gov/articles/PMC10988694/)
combine intrafusal mechanics with a spatially distributed conductance model of
rat Ia encoding. Their mechanotransduction remains partly phenomenological.
The article says code would be deposited in ModelDB; this inspection did not
locate a verified corresponding deposit. That does not prove none exists. Do not
invent missing morphology/channel files or call this a calibrated human spindle.
Obtain and pin implementable source and parameters before choosing a port.

## Evidence and current status

`artifacts/sensory-boundary-audit-20261109` records the inspected source revision,
file hashes and relevant function anchors, with a verified D: backup. The source
comment now identifies the legacy transducer's true status. There is no behavior,
model, parameter, save-schema, Unity deployment or editor-label change in this
audit, and no new test window has launched. Worlds retain paused startup.

Keep the pending Golgi â†’ CUDA cost â†’ motor scheduling execution order. The new
motor-unit circuit remains held behind a passing motor scheduling review. These
findings add a concrete sensory-side repair; they neither discard those studies
nor make the overall brain/body/world goal complete.
