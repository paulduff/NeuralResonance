# Several sensory fibres, one receiving vestibular population

The existing connectome already declares vestibular outputs to the cerebellar
vermis, fastigial nucleus, oculomotor nucleus, parietal cortex, reticular formation,
spinal cord and vestibular efferent path. The coordinator expands an original
neuronal event along those routes; the engine's single preferred output target
does not imply that the other live connectome edges are absent. The new isolated
canal/nucleus owner currently retains output but does not attach those downstream
owners. Its receiving side also supports only one original afferent fibre.

`ConvergentAfferentIngress` adds an explicit isolated option for several original
observed fibres to contact the same full vestibular nucleus. It reuses the
existing observed-source contracts, contact identities, receptor integration,
neurons and local learning. It does not sum rates, synthesize events, re-label
physical cells or allocate one duplicate nucleus per fibre. No live service,
Avatar, Unity or editor selector is enabled.

## Declared anatomy and causality

Declare between two and 256 distinct physical cells and source-neuron identities,
sharing one session and neural epoch. Each fibre retains its own sampling step,
observed cable site, original ordinal sequence, contact routes and remaining
conduction delays. Different fibres may target the same receiving cell, but their
actual contacts remain distinct. Duplicate observation of one physical cell is
rejected rather than counted as independent sensory evidence. At most 4,096
contacts are declared across the sources; normal queue limits still apply.

The receiving engine must have fresh isolated state. A single-source ingress,
another diagnostic population or reused state cannot be combined silently with
this owner. The convergence fingerprint enters the save identity. Existing
single-source ingress and live selection retain their previous behavior.

A packet is admitted atomically with its source's progress. A quiet packet moves
that fibre's known time without creating a receptor release. Unknown sources,
changed epochs, missing ordinals, skipped intervals, cancellation and queue
overflow leave its progress uncommitted. Exact retries return their original
receipt without re-enqueuing events. That receipt records the global arrival
horizon **at admission**; current progress is obtained from the convergence
snapshot after other fibres advance.

The receiving tick must end strictly before the minimum of every fibre's known
source time plus its shortest remaining delay. Strictness matters: the existing
packet contract permits the next interval to contain a crossing exactly at its
starting endpoint. Finishing a receiving tick at that still-unknown arrival
could miss an event at the boundary. This candidate waits for the next packet;
it does not shift, drop or retime that event. The old single-source contract is
preserved separately and is not retroactively described as providing this check.

Equal-time queued releases are processed in a declared stable order: arrival,
source instance, source neuron, projection, contact and event identity. This is
a deterministic arithmetic convention, not a biological priority or added delay.
It prevents packet arrival order from selecting a different ordering of otherwise
simultaneous input. Continuous neuronal integration still runs only on the
existing engine clock and event boundaries.

## Changes and prospective checks

`StructureEngine` adds only optional construction/binding, persistence identity,
packet-only admission, horizon validation, actual recipient lookup and the
convergence-only equal-time ordering hook. Its cell, receptor, learning, energy
and original integration equations are unchanged. The default path does not
select the new owner. The component package retains the original engine,
the exact reviewed source change, both source hashes and all earlier preserved
biological kernels; its gate rejects any engine text outside this reviewed edit.

Fourteen new cases exercise canonical anatomy, invalid/duplicate sources, a
slower companion, distinct contacts on one cell, explicit quiet progress,
original retry receipts, packet rejection, shared overload, cancellation and
legacy-admission rejection, existing-save protection, simultaneous arrivals in
reversed packet order, exact-boundary events, differing source grids/nonzero
epochs, and two actual passive source observations converging on one neuron.
The passive two-capacitor controls establish source/delivery accounting only;
they are not active physiological afferent firing.

`afferent_convergence_gate.py` and `start-afferent-convergence-window.ps1`
prepare a visible 89-case run: fourteen new cases and all 75 previous
source/observer/ingress/canal-owner cases. They use only completed, reviewed
`1047b8` evidence and its preserved earlier kernels. Pending independent sensory
comparison `2a402c` is never accessed. Frozen source, exact case multiplicities,
managed runtime, binaries and final D: backup are retained.

The test project builds with zero warnings/errors at
`D:/DNNE-build-work/afferent-convergence-authoring-20261119`; static Python and
PowerShell syntax and the 89-case inventory pass. No new tests/models have yet
executed. Record the actual package/task and outcome separately. The complete
brain/body/world goal still requires active sensory accuracy and calibration,
downstream neuronal and muscle coupling, balance and fall recovery. Those
requirements are not replaced by this component's admission checks.

## Visible component launch

Package `D:/DNNE-build-work/afferent-convergence-20261119-b208e4` is frozen at
pushed `bd3ecec953d1b8ea46054fd0c326065dac772c53`. Both projects build with zero
warnings/errors. Frozen preflight passes, and both initial 2,108-file copies
plus manifest verify, including the original engine and exact reviewed change.
Visible independent task `DNNE-Afferent-Convergence-889075f7e34f` started for
89 cases. No tests or models ran during preparation.

Its final result remains pending along with continuous reference `2a402c`.
Neither post-launch output, status nor process was inspected. Await the operator's
final footer, then verify the complete results and backup before drawing any
convergence conclusion. Head-input `1047b8` is already fully reviewed.
