# CUDA olive host workspace candidate

The completed cost review in `DNNE_Olive_CUDA_Cost_Results_2026-11-09.md` found
that packing and report processing account for approximately 30–43% of the
larger-batch host-profiled time. This candidate removes repeated host work at
that boundary. It does not change the kernel, numerical precision, cell equations,
parameters, per-cell intervals, transfer API or synchronous launch policy.

`tools/OliveCudaHostProbe` builds two separate executables. The `reference` variant
links the unchanged reviewed `OlivaryCudaBatch.cs`. The `candidate` variant links
`OlivaryCudaBatch.Host.cs` instead. Both use the same original driver, GPU kernel,
CPU definitions, accuracy inputs and test harness. No production host selects
either probe, and the previous cost/timing projects still link their original
implementation.

## Implementation and invariants

The candidate owns request, lane, duplicate-detection, checked-counter and report
scratch arrays for its lifetime. Inputs are written directly into the existing
component layout without a new nine-element array for every cell. A separate
buffer retains the last successfully uploaded inputs: a subsequent rejected
request must not overwrite evidence of what is on the device. Every nonempty
request clears the next input payload and duplicate marks, so an old selected
lane cannot leak into a later subset. Guard regions remain unchanged.

Immutable instance/cell strings receive ordinal ranks at construction. Sorting
still uses each report's actual end time first and that ordinal identity rank
second. It never assumes uniform intervals, fixed subsets or caller order. The
request is copied before validation. Every returned report array is newly owned
by the caller, never a view of reusable scratch space.

Validation, complete GPU report checking, checked counter additions, sorting and
return-array allocation all precede the public-state commit. Failed validation
or a failed numerical lane leaves all accepted state, clocks and counters intact.
Thread ownership, context ownership, input/output guards, immutable parameters,
state staging and explicit disposal retain their prior checks. The implementation
still allocates the report download through the original driver; changing that
transfer interface is outside this candidate.

## Accuracy experiment

The frozen runner first executes the same 11 focused CPU/manifest tests. Each
executable then executes all six original GPU control groups plus six added
groups:

* Changing subsets and request order over 40 frames, with differing intervals,
  independently changing cell clocks and case-sensitive/non-ASCII identities.
* A partially validated rejected request must preserve the last uploaded inputs.
* Empty requests after valid and rejected work preserve state and device inputs.
* Retained report arrays survive later calls; caller mutation cannot alter state.
* Failed numerical work followed by a different subset must leave old lanes idle.
* Empty requests still reject a disposed owner; disposal remains idempotent.

The original groups also check seed ownership, invalid inputs, foreign threads,
untouched cells, permutations and explicit valid requests after numerical failure.
Both variants save all control evidence; the offline analyzer requires exact
cross-variant equality, complete group names/counts and complete frame coverage.

Each variant then executes the original six population/step cases: 192, 3,072 and
12,288 cells at 0.025 and 0.00625 ms, advancing saved frames 256 through 383.
Alternate frames reverse request order. Every report is checked for chronological/
ordinal identity order and serialized back into original lane order. Initial
state, final identity/parameters and buffer guards are checked. All reports and
final states must reproduce the reviewed cost capture's uninstrumented round-zero
bytes exactly. Thus the existing CPU bounds remain applicable without changing
them or rerunning the adaptive references. Complete decompressed lengths and
hashes are independently verified; partial failures remain saved.

This is **accuracy only**: no elapsed-time or allocation-speed result is collected.
It may run alongside the pending motor numerical comparison without treating that
shared machine load as performance evidence. A later matched timing comparison
requires both operator completion reports and a passing accuracy review first.
Passing this experiment alone cannot establish an optimization benefit.

## Preparation and status

Both variants build with zero warnings/errors. Python and PowerShell syntax
checks pass. No new runtime check or GPU case has yet run. The preparer requires
clean pushed source, the verified completed cost review, unchanged original
mechanism/driver/reference sources and matching C:/D: input files. It freezes the
exact reviewed PTX rather than recompiling it, complete CPU input traces, all six
recorded report/state pairs and the new sources/protocol. The launcher requires
a verified initial D: backup and preserves its final source/binary/result evidence.

Run only in an independent visible PowerShell window and inspect its output only
after the operator reports completion. No live DNNE, Avatar, Unity, editor,
biological parameters or old-save behavior changes. Motor-cell qualification and
the closed sensory/motor/body loop remain required. The finer Golgi rig remains
unlaunched; the somatic motor-unit circuit still requires a passing motor-boundary
review.
