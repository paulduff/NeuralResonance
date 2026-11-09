# Isolated olive membrane batching candidate

The reviewed CPU baseline identifies olive/Purkinje cell calculations as the
first acceleration candidates. This change prepares the CPU state/identity boundary
needed for a comparable batched backend. It does not build on an assumed GPU pass.

`tools/OliveBatchProbe` contains a flat-state transcription of the existing
`OlivaryMembrane` calculation. The original runtime file is unchanged and remains
the reference. The preparer requires its Git-exported bytes to match the frozen
source in the reviewed `dnne-compute-20261109-fb9716` capture. The candidate retains
the same arithmetic order, adaptive RK4 choices, parameter values, gate/calcium
bounds and threshold-crossing rule. It makes no claim to repair the original
solver's unresolved long-trajectory accuracy or physiological calibration.

## State and causal boundary

The batch owns fourteen double-precision state planes, indexed by component and
cell, plus each cell's clock, parameters, two spike counters and ionic-load/current
measurements. Identity is the pair of structure instance and neuron identifier;
all lanes here are explicitly olive membranes. Identical neuron identifiers in
different instances remain distinct. There is no averaging across cells.

Each requested cell supplies its own start time, duration, held input and maximum
internal step. The start must equal that cell's clock. Duplicate, unknown, stale,
gapped or invalid requests fail before computation. Unrequested cells do not
advance. All computed changes are staged; if any lane fails numerically, no cell
state, counter or clock is committed. State is owned by the creating thread;
parallel CPU workers only calculate distinct staged lanes.

Results are ordered by interval end, instance and neuron identity, independently
of request order or worker completion order. They report the existing solver's
crossing counts over an interval. An interval endpoint is not an interpolated
spike time, and these reports are not distributed SpikeMessages or a replacement
for the brain's event scheduler.

Receptor occupancy, contact identity, release arrivals, energy feedback and
learning remain in their existing implementations. This narrow membrane API must
eventually be coupled at their real integration boundaries; passing held-input
comparisons alone is not permission to omit those mechanisms or enable a host.
Membrane seed import is a diagnostic transfer, not a schema-4 save upgrade or
an exact checkpoint of a whole ModelNeuron or brain.

## Prepared comparison

Ten new unit cases cover serial/parallel reference equality, unrelated-cell
exclusion, lane permutation, instance identity, malformed intervals, failure
atomicity, copied state ownership, actual reference-generated crossing transfer,
resumption of membrane counters/loads and rejection of foreign-thread access.
The initial prepared package required these ten cases plus the solution-manifest
case. The next package additionally checks the new motor replay buffer and all
existing transport/calcium/isometric-plant cases: 87 selected cases total; see
`DNNE_Motor_Replay_Buffer_2026-11-09.md`. The probe project is included in the solution.

After those pass, six recorded comparisons use populations of 1, 32 and 192 with
maximum membrane steps of 0.025 and 0.00625 ms. Each executes 1,024 frames using
the unchanged original solver, the serial batch and a four-worker batch. Even
lanes use 0.125 ms intervals, odd lanes 0.0625 ms; independent clocks and reversed
lane/request orders exercise identity and timing. Deterministic held AMPA/GABA,
current and metabolic-potassium fixtures include parameter ablations; they are
numerical workloads, not qualified anatomical inputs.

Each method saves every initial/intermediate membrane value, clock, spike counter
and load/current value in a declared little-endian binary trace. All twelve
batch/reference contrasts must match exactly. The independent Python audit checks
every returned byte, trace size, finite values, declared clocks, gate/calcium
bounds and event accounting. Matching at both step sizes does not establish that
the two refinements agree with each other or with an independent physical model.
There is no speedup threshold, GPU invocation or performance claim in this run.

`prepare-olive-batch.py` freezes clean pushed source. Verify its initial D: backup
before using its frozen `start-olive-batch-window.ps1`. The window builds under
frozen `source/artifacts`, verifies selected TRX counts, captures binary hashes,
runs the comparison/audit, rechecks source and binaries and verifies the completed
D: copy. It remains visible for the operator, with no live monitoring.

Both Release builds and Python/PowerShell syntax checks pass. **Runtime checks
are pending.** The CUDA retry has now passed its isolated arithmetic checks; see
`DNNE_Compute_And_Cell_Results_2026-11-09.md`. Run a fresh package with the expanded
87-test selection, preserving the earlier unlaunched eleven-test package.
Review this CPU comparison before
extending it into a CUDA membrane backend. Live DNNE, Avatar, World and old saves
remain unchanged.
