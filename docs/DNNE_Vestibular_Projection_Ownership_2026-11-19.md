# Original vestibular output to declared downstream pathways

The shared canal/nucleus owner now has a separate outgoing hand-off. It retains
the actual nucleus's completed steps and original spikes while recipients take
ownership of their individual packets. This is a prerequisite for connecting
the sensory path to the existing neuronal motor circuit; it is not yet that
complete circuit or a live host selection.

## Existing anatomy and routing

The current connectome lists seven vestibular projections: cerebellar vermis,
fastigial nucleus, oculomotor nucleus, posterior parietal cortex, reticular
formation, spinal motor population and vestibular afferents. The new outbox uses
the existing coordinator's route and recipient selection. Only access to two
helper methods changes from private to internal; their implementations remain
identical. Every declared pathway must resolve a recipient before attachment.
All seven are exercised by the prospective transport controls. This retains the
current connectome hypothesis, without claiming calibrated cellular arbors,
bilateral anatomy or a physiological peripheral efferent mechanism.

Each routed event retains the source's time, cell, release amount, transmitter,
reuptake, spike kind and modulation/context fields. Routing changes the target
structure and projection as before. A route that would coerce original chemistry
or replace missing cell names is rejected. Separate delivery identifiers are
derived from the original event, projection and recipient instance, so two
collaterals to one recipient cannot collapse in its duplicate-message check.
The full original event remains beside each routed copy. Existing recipient-cell
mapping and conduction calculations are unchanged; this work does not establish
which receiving cells ultimately integrate a particular route.

## Source ownership and clocks

`CanalNucleusProjection` attaches before the shared sensory owner advances. It
reads only that owner's immutable pending output, validates its identity and
completed integer tick sequence, and passes every original step to
`VestibularProjectionOutbox`. The producer's actual completed time accompanies
each recipient packet. No time is rebased or inferred from a firing rate. The
outbox does not advance any receiving engine or grant permission beyond this
source's completed time; a future multi-input recipient must still respect all
of its inputs and conduction delays.

Every recipient receives a packet even when there are no spikes or no new
receiving ticks. Such a packet communicates explicit progress. Publication
retries return the same retained frame. A receipt must match the recipient,
source sequence, original source-frame digest, complete packet digest and event
count. Partial, stale or contradictory receipts cannot release the source.
Only after all receipts arrive does the adapter acknowledge the original shared
owner, which then releases its sensory packets and physical traces. A staging
or release failure retains the actual source output and prevents replay.

This is serial, in-process ownership. Recipients are not started by the outbox;
its caller must obtain receipts from actual receiving owners. A receipt means
admission/ownership, not completed integration, downstream firing or durable
cluster recovery. No live HTTP service, motor-command decoder, Unity actuator
or editor selector is enabled.

## Qualification and preservation

The operator reported shared owner `6ceeae` complete. Its full review verifies
both 2,643-file copies, frozen `6210b4263e782cd2ef613862656af1d7fe486da0` source,
runtime/binaries, all 101 exact cases and exact saved analysis. The three-file
review plus manifest is D: verified. No tests or models were rerun during review.

Fourteen new controls cover seven-path routing, original spike preservation,
two collaterals admitted distinctly into an actual spinal engine queue,
idempotent retries, quiet progress, partial/wrong acknowledgements, immutable
packets, stale releases, timing/identity failures, route ordering and packet
partitions. Three of these attach to actual shared passive sensory owners and
check deferred source release and failure retention. Serialized spike fixtures
are explicitly transport controls; passive cells are not physiological afferents.

Both the test and native probe projects build with zero warnings/errors at
`D:/DNNE-build-work/vestibular-projection-authoring-20261119`. The prospective
visible run contains all 119 cases: fourteen new controls, four unchanged routing
cases and all 101 shared source/connection cases. Static inventory handles the
existing routing test comments without editing those tests or relaxing case
identity checks. Tests have not run at this authoring checkpoint.

`vestibular_projection_gate.py` requires the operator-reported, fully reviewed
and D:-verified `6ceeae` result before preparation. Original biological kernels,
shared sensory owner, connectome and earlier tests are byte-pinned. The exact
two-helper visibility change is recorded separately. Reusing the coordinator
requires the ASP.NET framework, without starting the host. Inspection of the
completed baseline confirms ASP.NET 10.0.15 was already part of its runtime.
The gate declares and hashes that installed framework before launch and requires
the original framework files and runtime settings to remain exact; no runtime is
installed or changed implicitly. The visible launcher rebuilds, verifies all exact cases,
preserves inputs/runtime/binaries and makes the final verified backup.

Continuous reference `2a402c` remains pending and untouched. This work does not
qualify active sensory accuracy, physiological firing, downstream coupled
neuronal/muscle accuracy, live brain authority or Avatar balance. Record any
actual preparation/launch and result separately.

The first preparation `vestibular-projection-20261119-515c08` stopped before any
test/model or launch because its declaration incorrectly assumed ASP.NET was
absent from the qualified runtime. The original frozen files and rejection
record are preserved in both locations. Corrected preparation accepts an already
qualified framework only when its full directory manifest remains exact; it
does not duplicate the directory or weaken comparison. Never launch `515c08`.
