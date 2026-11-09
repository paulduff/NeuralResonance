# Motor-cell comparison with endpoint-based step durations

The reviewed clock diagnostic in `DNNE_Motor_Clock_Diagnosis_2026-11-09.md`
confirmed a small discrepancy between the sum of durations supplied to the cell
and the sample timestamps produced by the original harness. It reached about
0.2 ns after six seconds at the finest requested step. Endpoint differences
removed that discrepancy in exact binary accounting. This does not establish
that the clock defect caused the remaining calcium refinement failures.

This experiment changes only the duration scheduling in the isolated neuronal
study. It preserves the cell's RK4 equations, compensated state/ledger additions,
channel kinetics, parameters, initial state, sample grid, physical inputs and
original acceptance bounds. No live brain or Avatar path selects this change.

## Original controls and scheduling candidate

The existing `--active` mode retains its original duration expression. A new
explicit `--active-boundary` mode uses the **same function** that passed the clock
diagnostic: choose a representable endpoint within the maximum step, then supply
its difference from the preceding clock. Only the accessibility of the two clock
helper functions changes. Preparation verifies their full source against the
completed diagnostic, plus unchanged cell and channel source.

Both modes execute the same ten protocols at 0.0125, 0.00625 and 0.003125 ms:
**30 original controls and 30 endpoint-based candidates**. Each starts fresh and
runs for 6,000 ms with the original 0.1 ms sample grid. There is no timestep
expansion, phase alignment, conductance adjustment or automatic retry. An endpoint
may require a final smaller substep; the original maximum always remains enforced.

The rerun controls must reproduce every original CSV state byte and every original
case-result field. All thirty original trace hashes are verified on C:/D: during
preparation, then frozen alongside the original metadata. This checks that the
newly built probe still reproduces the failed control before interpreting a causal
comparison. Extra clock-accounting fields do not replace the original results.

During both neuronal runs the harness sums supplied durations in the same exact
2^-80 ms units. Endpoint mode requires equality with the represented cell clock
after **every actual integration step**. Every completed condition must also match
the qualified clock diagnostic's step count and exact final elapsed-time totals
for its schedule and resolution. The diagnostic's command-boundary union lies on
the same sample grid, so different stimulus segments do not add split locations.

## Preserved references and criteria

The thirty original DOP853-tight, DOP853-tighter and Radau state/event arrays are
copied with verified hashes. Their original reference code and metadata are
retained. Before integration, the window recomputes all reference metrics and
contrasts from these arrays. It does not solve those ODEs again. NumPy/SciPy must
remain at 2.2.6/1.16.3, and analysis records the Python/dependency environment.

The primary target remains DOP853-tighter. Absolute limits remain 0.05 mV voltage,
0.0005 gate probability, 5e-7 mM calcium and 0.01 ms matching crossing time.
The original charge/calcium ledgers, physical-domain checks, spike-count agreement,
reference uncertainty and refinement rule remain unchanged. In particular, a fine
error still must not exceed the greater of twice the measured reference spread
and 1.1 times the preceding error. No new floor is introduced for tiny calcium
differences. Every case must pass the original requirements; quiet agreement alone
cannot qualify the study. Physiological calibration remains a separate task.

The new analyzer reproduces **all ten original scenario analyses exactly** from
the completed saved arrays, including the two original failures. That offline
regression is saved in `artifacts/motor-boundary-preparation-20261109/regression.json`.
It executes no membrane model or reference solve. The final report compares old
and new gates and finest errors while leaving the original FAILED capture intact.

## Validation and execution

The probe builds with zero warnings/errors and Python/PowerShell syntax checks
pass. Runtime qualification remains pending. The frozen visible window requires
twelve focused motor-cell/manifest regressions, reference preflight, both sets of
thirty conditions, independent saved-state analysis, source/binary integrity and
a final verified D: backup. Failed/partial conditions remain evidence.

`prepare-motor-boundary.py --clock-review REVIEW` requires clean pushed source,
the passing operator-reported clock review and verified original captures. The
launcher deliberately requires `-CostStudyFinished`: the already prepared CUDA
cost benchmark should complete before this new CPU study starts. Golgi is still
pending its own completion report; neither it nor any new study is monitored live.

Passing this comparison would establish the tested scheduling repair's numerical
outcome. It would not establish human motor-cell calibration, physiological axonal
transmission, live neuronal muscle authority or physical Avatar recovery. All old
saves, Avatar/Unity/editor behavior and the parked language work remain preserved.

The prepared package is `motor-boundary-20261109-a79744`, frozen revision
`2465fdb8026768f2348e7bda53a4375ff203eb28`. Its 1,480 initial files plus manifest
have a verified D: copy. It has **not launched**. The existing cost package
`olive-cuda-cost-20261109-06f78b` must run after the Golgi completion report, then
be reported finished before this motor comparison starts. No result is inferred
from preparation, elapsed time or the completed clock-only diagnostic.

## Launched after the corrected CUDA cost review

The operator reported `olive-cuda-cost-20261109-fedff9` complete. Its full C:/D:
and saved-analysis review passed; the separate review also has a verified D:
backup. The timing hold is therefore satisfied.

The existing `motor-boundary-20261109-a79744` package is now launched at its
unchanged frozen revision `2465fdb8026768f2348e7bda53a4375ff203eb28`, task
`DNNE-Motor-Boundary-c3550f7fed43`. The launcher rechecked all 1,480 initial
files plus the manifest against D: before starting the independent visible
PowerShell window. It is the only pending window. Await the operator's report;
do not inspect live status/logs or infer a result from launch. The finer Golgi
comparison remains unlaunched, and the somatic motor-unit circuit remains gated
on a passing motor-boundary outcome.

## Completed numerical result

The operator has now reported completion. The verified review in
`DNNE_Motor_Boundary_Results_2026-11-09.md` confirms all 12 tests and all ten
endpoint-scheduled scenario gates pass, with exact reproduction of the thirty
original control traces and their two failed scenario gates. All 2,057 C:/D:
files verify and saved analysis reproduces exactly. No bounds or biology changed.
This satisfies the numerical prerequisite for the isolated somatic motor-unit
experiment; live authority and physiological/body qualification remain open.
