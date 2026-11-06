# Cell-local scheduling repair: isolated locality passes, accuracy remains open

The operator reported completion of `cell-local-timing-20261106-0910bb`.
Its frozen revision is `897f9b3fef5bfaceddc6b6b9e7f287625851b624`;
the capture finished at 2026-11-06 10:48:10 UTC with exit 0. The isolated
candidate removes the demonstrated numerical influence of unrelated arrivals
on another Purkinje cell. It does not establish accurate cell dynamics, biological
calibration, general recurrent scheduling or Avatar recovery.

## Evidence and preservation

`tools/review-cell-local-timing.py` re-reads the reported completed capture and
its D: copy, checks their manifest/source/binary hashes, parses the saved TRX,
and re-runs the frozen read-only analysis over both the eight-second preflight
and sixty-second study. It starts no simulation and does not overwrite evidence.
The review is retained in `artifacts/cell-local-timing-review-20261106/review.json`.
Each original copy contains 2,447 manifest-listed files totalling 3,952,564,297
bytes. The original window also recorded all 48,434 protected older paths
unchanged; the review verifies the new capture and backup, not a fresh audit of
every historical run. Original saves remain untouched.

All 24 global-mode cases reproduce the corresponding earlier spike trains and
recorded state/contact/voltage files exactly in each stage. The candidate has
24 cases per stage: two fixed input offsets, four receiving step ceilings and
three connection/marker conditions. Thus each stage contains 48 conditions.
The twelve deliberately damaged-capture cases were rejected by the run's saved
validator checks. They are not rerun as tests during the result review.

## What passes

All 15 timing unit cases pass. Across all eight source/step combinations in the
candidate, the cell receiving no climbing input has exactly matching recorded
states and spike times whether climbing events reach the other cell, are removed,
or are replaced with no-op boundaries. Both cells likewise match exactly between
the disconnected and no-op conditions. The complete recorded contact trajectories
match in the corresponding controls, not merely their final weights.

Actual climbing input still changes the taught cell's recorded states and spike
train in all eight comparisons. Declared incoming messages, fractional arrival
times and contact ownership remain accounted for. The invariance was not obtained
by dropping inputs. The long local study contains 204,592 event records,
1,440,000 cell-state rows and 3,593,568 contact rows. Maximum energy and calcium
accounting residuals are respectively 4.364e-12 and 1.695e-11 in model units.

This resolves the specific causal defect established by the preceding no-op
boundary study, within the candidate's isolated scope. The production scheduler
is still on its previous path. Multi-target arbors, recurrent routing, concurrent
ingress, arbitrary host endpoints and cluster scheduling are not qualified here.

## What does not pass

The full suite is **1,074 passed / 1,075 total**, with the known
`NeuronalMemoryTests.RepeatedCorticalExperienceGraduallyRaisesConsolidation`
failure recurring. It reports initial consolidation 0.000189 versus final
0.000035, with engram strength 0.008270. The frozen runner explicitly allowed
only this already-documented failure to continue to the timing study. The
solution-manifest failure is repaired. The whole DNNE suite is not green, and
the fact that the cortical case passed in the earlier attempt did not repair it.

The receiving electrical dynamics remain sensitive to step refinement. For the
local candidate's connected condition, spike counts over sixty seconds are:

| Input offset | Cell | 0.125 ms | 0.0625 ms | 0.03125 ms | 0.015625 ms |
| --- | --- | ---: | ---: | ---: | ---: |
| 0 ms | Taught | 3,663 | 3,661 | 3,661 | 3,661 |
| 0 ms | Other | 3,538 | 3,537 | 3,536 | 3,541 |
| 60 ms | Taught | 3,661 | 3,661 | 3,660 | 3,660 |
| 60 ms | Other | 3,544 | 3,544 | 3,544 | 3,544 |

At the finest pair, offset-0 taught-cell ordinal times differ by up to 0.328125 ms,
while the other cell gains five spikes. For offset 60, equal counts still have
maximum ordinal differences of 15.109375 ms and 2.390625 ms. Ordinal comparisons
are descriptive, not a calibrated spike-distance metric. The disconnected
offset-60 taught cell also changes from 3,653 to 3,659 spikes at the finest pair.
These observations do not qualify convergence or establish chaos.

The earlier learning limitations remain. At the finest step, active-contact
weights are 0.99995154 and 0.99996924 for the two offsets; dormant contacts also
decrease to 0.9999858 and 0.99998707. Small, similar final weights cannot validate
electrical accuracy, selective learning or repeated teaching. Imposed Purkinje
drive and the retained-maximum induction approximation have not changed.

## Consequences for development

Keep the isolated cell-local route as the tested scheduling candidate and use it
in subsequent receiving-cell diagnostics. Before production adoption, qualify a
general causal scheduler and version its changed dynamics explicitly. Do not
restore the global boundary coupling to obtain an old trace, and do not call the
new trace exact merely because it is invariant to unrelated events.

The next receiving-cell work must separate the membrane/receptor/support coupling
error from cell physiology, using matched short intervals and complete dynamic
states. Independently justified Purkinje pacemaking and local repeated-teaching
mechanisms remain required. The cortical-consolidation failure needs its own
causal investigation; a forced increase of the displayed score is not a repair.

The independently prepared motor-unit calcium study has been launched in its own
visible PowerShell window, without live monitoring. It addresses the separate
missing motor-event-to-force mechanism;
it is not evidence that the cerebellar loop or a neuronal motor source is ready.
The Golgi reference window remains awaiting the operator's report and is not
inspected here. No live Avatar/editor deployment or brain-state migration occurs.

For future timing windows, the launcher now prints a prominent final warning
when a known full-suite failure coexists with a passed timing study, records
those outcomes separately, labels pipeline interruption durably, and has no
automatic task-duration ceiling. The completed capture is unchanged.
