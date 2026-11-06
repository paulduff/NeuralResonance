# Isolate the shared arrival-boundary effect

The preceding [split-study review](DNNE_Split_Numerical_Results_2026-11-06.md)
found that disconnecting olive input slightly changes the timing of a cell
which never receives that input. StructureEngine splits every cell's numerical
step at any scheduled arrival. This experiment tests whether the removed
boundaries account for that difference before implementing a scheduler repair.

## Matched intervention

Each condition replays one of the two immutable olive/parallel-input schedules
from `split-numerics-20261102-1e49f9`. Three fresh two-cell populations are compared:

1. Connected: the original parallel and climbing inputs.
2. Disconnected: identical parallel inputs, with all climbing inputs removed.
3. Markers: disconnected inputs, with a no-op boundary at each removed climbing
   arrival. A marker only partitions elapsed time. It never enters a spike queue,
   creates a synapse, releases transmitter, changes a learning weight or supplies
   neural excitation. All receptor, membrane, support and chemistry dynamics
   still evolve normally across the partitioned intervals.

Two offsets and four receiving ceilings (0.125, 0.0625, 0.03125, 0.015625 ms)
give 24 conditions. Host cadence stays at 0.125 ms. The independent visible
PowerShell window runs an eight-second preflight followed by 60 seconds per full
condition. There is no live monitoring and no live brain, Avatar or world access.

The hook is an internal constructor argument restricted to fresh isolated
Purkinje populations. Its copied schedule must be finite, nonnegative and strictly
increasing. Diagnostic schedules contribute to the isolated save fingerprint;
the default production fingerprint and mathematical path remain unchanged.
The public service constructor, API and environment settings cannot enable it.
Old saves are preserved and never loaded by the experiment.

This is a causal diagnostic, not an implemented production repair. In particular,
retaining meaningless markers is not a proposed biological mechanism or a fix.
The biological cell/learning parameters are unchanged. The imposed Purkinje drive,
reduced electrical compartments, maximum-history induction and missing LTP remain
unqualified approximations. The olive solver is not changed in this study.

## Evidence and acceptance

All incoming messages and marker times are saved and checked independently.
Markers must occur exactly once at their scheduled times, with no corresponding
synaptic input or extra contact. All ordinary contact identities, target cells,
emission timestamps and conduction delays remain unchanged. Energy/calcium
accounting, finite state and complete matrices are required.

Every connected and disconnected case must reproduce its archived spike train
and its voltage/energy and contact CSV bytes exactly, for the matching duration.
There are 16 baseline checks at each stage. Source hashes and separate eight-
and sixty-second prefix hashes establish the archived baselines without rewriting
the old capture.

Additional traces record unrounded HH voltage/gates, receptor currents, reduced
dendrites, phase, glial/homeostatic support and energy state every 2 ms for both
cells. These are selected diagnostic states, not a complete continuation snapshot.
The report retains the first sampled difference and its fields, spike-count and
timing differences, and maximum sampled voltage difference.

Support for the boundary hypothesis requires the cell without climbing input to
have exactly matching recorded states and spikes between connected and marker
cases, while at least one connected/disconnected state comparison differs.
This outcome is reported separately from accounting success. A negative result
must remain a valid captured experiment, not be hidden by an assertion. The
taught cell's responses are also retained; connected and marker cases have
different biological inputs there and should not be assumed equivalent.

## Completed preparation checks

The eight-second 24-condition preflight passed all 16 archived baseline checks:
96,000 voltage/energy rows, 473,568 contact rows, 192,000 additional cell-state
rows and 26,562 events. Maximum energy ledger error was approximately 1.31e-13;
maximum calcium mass error was approximately 1.16e-12.

All eight comparisons of connected versus markers match exactly in the cell
without climbing input, including the additional sampled states and spike times.
Connected versus disconnected first differs at sampled times 2.028/2.088 s for
the two offsets at 0.125 ms, and 2.032/2.292 s at 0.0625 ms. Both finer grids
already align with these marker arrivals and remain identical across conditions
in that cell. This supports the proposed mechanism in the short run; the full
minute still requires review after the operator reports completion.

Dedicated tests verify exact start/fractional/end marker processing, schedule-copy
isolation, no synaptic release/contact creation, rejection of invalid schedules
and rejection outside isolated populations. Nine deliberately damaged captures
check marker timing/counts, fake releases, missing state rows, wrong ownership,
archived baselines, energy, missing cases and marker schedules.

The full DNNE suite ran 1,051 tests: 1,050 passed and one failed:
`NeuronalMemoryTests.RepeatedCorticalExperienceGraduallyRaisesConsolidation`.
The preserved pre-change `connected-timing-tests-20261102` binary also fails
that test on this machine. Its fresh identities are random; the failure cannot
be presented as fixed or the entire suite called green. This pre-existing
cortical-consolidation issue is recorded separately for further investigation.
No test assertion or learning rule was weakened to obtain a passing result.

## Freezing and preservation

The committed source, tested binary, immutable bundle, archived baseline hashes,
short capture, validators, test reports and runtime versions are frozen in a
unique package and initially backed up to a unique D: directory. This rig uses
only Python's standard library; no new Python dependencies are needed.

The older protected-file expectations are carried forward from the previous
capture and its review. Preparation verifies the source files it uses and both
manifest copies. The PowerShell window performs the full older-file hash audit
before running any condition, again after stages, and on completion. Carrying
forward a manifest is not labelled a new full preservation audit. A changed or
missing protected file stops the run. The window has a 24-hour execution ceiling
and retains its final status and transcript; results are verified onto D:.

After reviewing the full result, the next repair should address cell-local event
integration or measured integration accuracy, preserving real synaptic causality.
Short intervals from complete identical diagnostic states and the independent
olive-reference work remain necessary. This experiment alone does not qualify
spike convergence, biological Purkinje learning or physical balance.
