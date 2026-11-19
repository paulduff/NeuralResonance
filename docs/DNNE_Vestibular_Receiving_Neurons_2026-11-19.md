# Owned vestibular input into actual reticular and spinal neurons

`VestibularProjectionReceiver` adds the receiving side of the reviewed outgoing
ownership boundary. It constructs a fresh, full-size reticular formation or
spinal motor population using the existing service's Izhikevich profile, existing
HomeostaticGain or STDP rule, and original conduction/contact calculations. No
live host selector or body actuator changes. The isolated initial state at the
declared epoch is explicit; there is no hidden settling, warm start or replay.

## Admission, receiving time and output

The recipient declares every participating vestibular source, its outbox
fingerprint, original epoch and step, and its permitted projections before
admission. Sources share an epoch but may have different grids. Every packet
must preserve the source sequence, completed ticks, original frame digest,
recipient identity, event count and immutable payload. The receiver reconstructs
each routed copy using the existing helper and requires its original event,
chemistry, delivery identity and projection to agree. It rejects normalization
that would change serialized data. A receipt means the actual receiving owner
has retained the complete packet, not that its neurons have already processed it.

The receiver advances only through the minimum completed time of all declared
sources. It uses its own fixed integer grid; packet boundaries do not introduce
extra neuronal steps. This conservative horizon does not add conduction delay
to permission to advance. Quiet packets communicate explicit progress. A faster
source cannot substitute silence for an unfinished companion.

Packets and original events remain in the owner's bounded buffer. The existing
engine's read-only conduction calculation determines each event's arrival. Only
events due in the next receiving interval enter its actual queue, in a declared
order by arrival and serialized original routed payload. This avoids changing
the engine's equal-time queue ordering with input packet partitions. Nothing
changes the original delay calculation or shared queue/integration implementation.

Every successful step retains its original `StructureStepResult`, including
actual neuronal output, and observes actual receptor/contact delivery. The
observer verifies each original message, contact identity, receiving cell,
scheduled arrival and applied time. A subsequent comparison still has to
establish coupled numerical accuracy; observing a delivered contact is not that
independent comparison or evidence of physiological firing.

The output frame blocks further admission until its exact acknowledgement;
exact input/output retries are inert. Events with future conduction times retain
their original packet even after earlier output is acknowledged. Each output
contains at most 4,096 receiving ticks; further known time can continue in the
next acknowledged frame. Admission and held-packet limits fail explicitly.
No whole-brain or cross-host synchronization is implied by this serial owner.

If an actual step fails after partial contact work, the owner retains the
dispatched events, remaining source packets, completed step prefix, actual
contact observations and engine state. It cannot replay or reset that partial
step. `IsolatedProjectionObservation.cs` adds read-only access to the existing
delay and state for these two isolated recipients; the existing engine methods
and all biological equations remain unchanged.

## Evidence and prospective checks

The operator reported outgoing ownership `ae88a4` complete. Its review verifies
both 2,710-file copies, frozen `ab70753bcca31033111ea6434e40234257db3386` source,
runtime/binaries, all 119 exact cases and exact saved analysis. The three-file
review plus manifest is D: verified. No tests/models were rerun during review.

Eleven new controls cover fresh full populations, actual reticular contacts,
two vestibular sources reaching spinal contacts, unknown companion time,
retained future events, retries/backpressure, tampered packets, whole-packet
capacity rejection, packet/source-order invariance, failure after a real contact,
and preservation of prior saves and source contracts. Inputs are explicitly
serialized transport fixtures. The recipient is an actual neuronal population;
these inputs do not establish that an active sensory cell generated the spikes.

Both projects build with zero warnings/errors at
`D:/DNNE-build-work/vestibular-recipient-authoring-20261119`. The prospective
visible workflow contains all 130 cases: eleven new receiving controls and all
119 reviewed source/outbox/routing cases. Tests have not run at this authoring
checkpoint. `vestibular_recipient_gate.py` requires the operator-reported, fully
reviewed and D:-verified outgoing dependency. It preserves all preceding kernels,
controllers, connectome, source owners, outbox and runtime, including the two
original receiving service profiles. Record an actual launch/result separately.

Continuous sensory reference `2a402c` remains pending and untouched. Other
incoming brain regions, recurrent whole-brain coupling, physiological sensory
firing, independently accurate downstream trajectories, neuronal muscle control
from this sensory source and Avatar balance remain later requirements. No
language-model, Dyad or CIV work is resumed.
