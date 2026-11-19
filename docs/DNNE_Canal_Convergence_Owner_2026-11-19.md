# Actual canal sources sharing one vestibular nucleus

The completed `b208e4` controls establish admission and timing for several
observed fibres converging on one receiving population. `CanalConvergenceConnection`
now owns the actual `AvatarCanalAfferentInput` objects and that receiving engine
together. It extends the isolated one-source ownership work to multiple source
cells without replaying a capture or replacing the cells with firing rates.

Each source retains its original mechanics, full sensory membrane/ion/gate state,
compensation and flux ledgers, observation site, events and individual sampling
grid. The connection attaches before any source's first mechanical interval.
All sources share one physical clock/session/epoch, but their subdivisions and
sensing points may differ. Existing source, convergence, receiving-neuron and
learning equations remain byte-identical to the completed dependency. No live
service, Unity or editor selector is enabled.

## Shared head and source ownership

A head batch must contain exactly one observation for every declared physical
cell at the next original physical endpoint. All observations must describe the
same body, origin, orientation, angular velocity and gravity. Each preserves its
own sensor identity and local material point. World point velocities must imply
the same rigid-body origin velocity within the explicitly declared 1e-5 m/s
rounding allowance, after subtracting omega cross radius. No observation is
averaged, normalized or repaired. This is a consistency check, not anatomical
calibration. An exact repeated batch returns the same mechanical receipts.

Every source consumes and acknowledges its current physical trace before the
shared head can advance. Within that interval, callers can advance source cells
in different orders on their original integer grids. The source's existing
packet and event limits still apply. Source advancement, head consumption or
acknowledgement outside the shared owner is detected before the recipient moves.

Publication seals every source with new completed progress. It admits the
original packets into the single actual vestibular nucleus, retaining all
admission receipts. The receiver advances on its fixed integer-indexed grid,
strictly before the minimum known arrival horizon across all fibres. A faster
source cannot turn an unknown companion interval into silence. Packet endings
do not force partial receiving integration steps.

The immutable output retains original packets, admission evidence, source
progress, actual contact deliveries and every original receiving step/output.
It is bounded to 4,096 receiving ticks. Publication retries return the same
frame; while it awaits acknowledgement, every attached source and the shared
head are blocked. Only acknowledgement of that exact output releases the
included source packets and their completed physical traces. Sources with no
new progress are not fabricated into empty packets or reset.

Mechanical or receiving work can fail after a valid prefix. The owner retains
that prefix, all sealed packets, completed output and actual source states and
then rejects resumption. It never rolls biology back and repeats a partial
step. Partial source-acknowledgement failure likewise stops the owner with its
actual progress retained. This is serial in-process ownership, not durable
cluster recovery or a live brain scheduler.

## Prospective component run

Twelve new cases cover unchanged attachment/environment, two actual passive
source events reaching separate contacts on one neuron, a slower companion,
shared output backpressure, explicit quiet progress, contradictory head poses
and velocities, distinct sensing points, exact head retry, partial mechanical
and admission failure, external source advancement, packet/interleaving
invariance, existing-save protection and stale acknowledgements.

The source fixtures use the existing analytic two-capacitor threshold control,
with explicit diagnostic mechanics. They are not physiological afferent firing
or calibrated bilateral organs. Both projects build with zero warnings/errors
at `D:/DNNE-build-work/canal-convergence-authoring-20261119`. Static syntax,
the 101-case inventory and preservation of earlier source text pass; the new
tests have not run yet.

`canal_convergence_gate.py` and `start-canal-convergence-window.ps1` prepare a
visible 101-case window: twelve owner cases plus all 89 completed convergence
and earlier connection cases. Preparation requires the reported, fully reviewed
and D:-verified `b208e4` evidence. It never accesses the pending independent
continuous sensory comparison `2a402c`. Frozen source, exact test identities,
managed runtime, binaries and final backup remain recorded. Record the actual
launch and result separately.

The next active sensory protocol still depends on the continuous accuracy
review. Shared ownership does not establish excitatory firing, physiological
balance coding, downstream neuronal/muscle accuracy or Avatar recovery. The
full brain/body/world objective remains unchanged.
