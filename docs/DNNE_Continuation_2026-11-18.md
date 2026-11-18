# DNNE / Avatar / World — 18 November continuation

The operator resumed the goal and requested reruns of yesterday's rubral
comparison and skull sampling, confirming their windows stopped/disappeared.
One administrative process check found no matching old workers. This allowed
review of those stopped runs; it is not permission to monitor new experiments.

## Open cleft launched

Yesterday's prepared `cleft-open-20261117-db4794`, frozen at
`04c4fd61f4df36ba438dfff26b41db28d278d625`, passed its initial package/C:/D: checks
and launched as visible task `DNNE-Cleft-Open-12f472eca915`. It owns 51 focused
cases, 624 exact closed regression records and 192 independent open-boundary
comparisons. No post-launch output, status or process has been inspected.
It awaits the operator's completion report.

## Stopped skull run: request-file sharing violation

`unity-skull-20261117-1e2f21` finished FAILED. The new
`artifacts/stopped-studies-review-20261118/review.json` verifies all 3,505 files
in each C:/D: copy, frozen Git/player/probe/test binaries, 74 passing tests and
unchanged managed runtime. Its Unity failure reports a sharing violation reading
`request-000202.json` while at physical step 198. The request asks for the next
step, but it was not read/executed and no matching reply exists. The old source
used a single `File.ReadAllText` after an existence check. Atomic publication
already prevented half-written files; it did not prevent a later exclusive lock.
The evidence does not identify which process held the conflicting lock.

The repair reuses the existing `DiagnosticBridgeFiles` reader in the Unity
coroutine, host reply wait and skull sidecar reader. Missing files and Windows
sharing/region locks are retried against the same immutable path under bounded
wall-clock deadlines. Permission, parse and other I/O failures remain failures.
No command is resent, no extra physics occurs during a read wait, and physical
time/identity/measurement values are unchanged. This is the isolated diagnostic
transport, not the live DNNE sensory connection.

The new v3 skull protocol retains the 33 scale controls and all original
coordinate/velocity/derived bounds, endpoint counts and sensor locations. It adds
two deliberate exclusive-lock read failures on the first physical request inside
Unity and two on its completed reply in the host. Both actual read paths must
recover on sequence 2 and yield exactly physical step 1; saved control records
and the complete original sequence/endpoint audit check no duplicated step.
Six existing reader cases join the original 74 tests, giving 80 focused cases.
The unchanged body, sampling producer and physical sources remain prerequisites;
the diagnostic adapter's explicit read-policy change is now declared separately.

The .NET probe authoring build passes with zero warnings/errors. Static Python,
PowerShell and exact 80-case inventory checks pass. No test, Unity build/capture
or numerical model ran inline. The independent window must exercise the native
lock controls and full head-sampling comparison before this repair is qualified.

## Stopped rubral comparison: numerical failure followed by interrupted audit

`rubral-comparison-20261117-30755e` retained all fifteen completed reference solves
and its analysis before stopping during the final read-only preservation audit.
Its status file still says RUNNING because the previous launcher wrote the final
status only after that audit. No matching process remains, and no final
`input-postflight.json` was written. Do not treat stale initial status flags as
proof that the references were never attempted or that final preservation passed.

The recorded analysis passes quiet, unit-left and strong-cut. Strong-left,
strong-right, strong-fast and strong-retry fail only `yieldFraction` against all
three reference settings: the maximum saved discrepancy is
`1.039360238175302e-5`, with the original bound `1e-5`. This identifies the failed
quantity; it does not yet identify the numerical mechanism or justify widening
the bound. Upstream neuronal accuracy, physiology and recovery stay unqualified.

The review verifies frozen source/package and initial C:/D: backup files and
copies every one of the 1,907 stopped-run files under a new
`rubral-stopped-evidence` subtree, including the unmodified status and original
initial manifest. All 1,908 review/snapshot files plus the new manifest are
verified on D:. The original interrupted folder and original native `4cdba5`
capture are untouched. This snapshot is preservation, not a substitute for the
interrupted final audit or a complete reanalysis of every native endpoint.

The requested fresh comparison repeats the full native C:/D: audit, fifteen
references and original bounds. It is a reproducibility check and may repeat the
recorded failure. Calculations, biological parameters and tolerances are unchanged.
The runner now records each stage and any failure before the final audit, and
prints preservation progress every 10,000 files per drive. The full audit still
runs before and after reference work. Saved comparisons also name failed metrics
and bounds so the final failure is informative. No automatic agent monitoring is
added; progress is for the operator's PowerShell window.

After this comparison is reviewed, investigate the yield-state error using the
preserved trajectories and declared refinement, rather than changing acceptance
bounds. These reruns and the open-cleft study do not establish usable sensory
feedback, neuronal balance or recovery. The authored local membrane/cleft
derivative assembly remains a separate unrun stage as noted yesterday.

## Fresh reruns launched

Both packages are frozen at pushed revision
`40a78fd9f2cf30d29c4d67452fc3b84f5491d935`:

| Package | Initial D:-verified files | Visible task |
|---|---:|---|
| `rubral-comparison-20261118-f5e01c` | 1,876 plus manifest | `DNNE-Rubral-Comparison-d305b8b7b292` |
| `unity-skull-20261118-ac78c5` | 1,836 plus manifest | `DNNE-Unity-Skull-3ea6546f0be2` |

The launchers verified initial copies and opened independent visible PowerShell
windows. No post-launch results, status or processes have been inspected. These
two and open-cleft `db4794` await operator completion reports. The user requested
the rubral repeat with its original bounds; its previous yield-state failure
remains evidence even if a new process completes. The skull repair's new native
controls and 80 focused cases remain unqualified until the saved run is reviewed.

## Reported skull retry: lock-control admission defect

The operator reported `unity-skull-20261118-ac78c5` FAILED. Saved-data review
verifies all 2,626 C:/D: files, frozen source/player/test/probe binaries, unchanged
managed runtime and all 80 passing focused cases. Unity produced the initial
inspection and first physical step, then its new guard raised “Native request
lock control incomplete.” Sequence 2's request, reply and skull sidecar are
retained at physical step 1. No successful request/reply lock-control record was
written. This is a failed diagnostic fixture, not a head-kinematics result.

The counters were not included in the saved failure, so their values cannot be
reconstructed. Source inspection identifies an admission race: `FileStream.Open`
can see a missing request, then `TryRead` can see the newly published complete
file. The previous branch admitted any Ready result even if the injected-lock
test had not finished. The completion guard was too late, after the physical
step. This is a fixture integration defect; no physiological cause is inferred.

The v4 repair holds sequence 2 before execution until its control is complete.
It deliberately observes at least one readable request without the injected
lock and verifies the physical step remains zero. Only afterward may it acquire
the exclusive lock, observe two real sharing violations, release it and admit
the unchanged request. A second guard checks all conditions before physics.
The retained control includes the number of readable-but-held observations;
the independent audit requires at least one, two lock rejections, and a single
step from zero to one. The host's existing locked-reply control still applies.

Original 80 focused cases, 33 scale controls, 201 physical steps, all coordinate/
velocity bounds, body mechanics and source identities are unchanged. The new
protocol declares the added admission requirement explicitly. The completed
failed capture and its separately backed-up review remain prerequisites for a
fresh run. Static syntax/protocol/inventory checks pass; native behavior awaits
the next independent visible PowerShell run. No model or test ran inline.

## Open-cleft result verified

The operator reported `cleft-open-20261117-db4794` complete. Its new
`artifacts/cleft-open-review-20261118/review.json` verifies all 2,404 C:/D: files,
frozen Git source/binaries, unchanged runtimes and 51 passing focused cases.
All five closed output files preserve the original 624 records byte for byte.
Every one of the 192 open evaluations matches the saved independent 60/90-digit
references under the original limits; the maximum error uses about 1.0694% of
its allowed bound. The saved analysis was reproduced exactly without executing
model equations or tests. Both this review and the separate skull admission
failure review have verified D: copies.

This qualifies the tested instantaneous open-boundary calculations and closed
regression. It does not qualify the later local membrane-current assembly,
coupled trajectories, sensory firing or Avatar balance. Open `db4794` and failed
skull `ac78c5` are now reviewed, not pending. Rubral `f5e01c` alone still awaits
its completion report until the new skull retry launches.

The v4 retry is now launched as `unity-skull-20261118-0ada47`, frozen at
`f1ee1845b236afcce8171473be0e6cd6c5108424`. All 1,837 initial files plus manifest
were D: verified before visible task `DNNE-Unity-Skull-e4ffd1a3a92f` opened.
No post-launch outputs, status or processes were inspected. Only this new skull
run and rubral `f5e01c` now await operator reports; the open-cleft result is fully
reviewed. New skull launch is not proof that its native admission controls pass.

## Native skull sampling verified

The operator reported `unity-skull-20261118-0ada47` complete. The saved-data
review in `artifacts/native-skull-review-20261118/review.json` verifies all
3,598 files on both C: and D:, the frozen Git source, built Unity scripts/player,
test/probe binaries and unchanged managed runtime. All 80 focused cases pass.
Recomputing the saved kinematic/accounting checks exactly reproduces
`skull-analysis.json`, without rerunning physics or tests.

All 33 scale controls pass. The native request fixture deliberately observes a
readable request once while keeping physics at step zero, then records two real
sharing violations before admitting that same command. The host independently
records two locked-reply rejections. There is exactly one transition from step
zero to one; no command or physical step is resent. The full capture retains
201 physical steps, 201 delivered endpoints through step 200, 207 skull sidecars,
603 point receipts and four unchanged held inspections.

Maximum point-velocity discrepancy is `2.5193605157625143e-7 m/s`; maximum
point-position discrepancy is `1.844757985258738e-7 m`. The original coordinate,
velocity and derived arithmetic limits pass unchanged. At step 178 the old
computed-scale rule would reject all three sensor snapshots with squared
deviation `1.8332002582610585e-12`, while every configured physical ancestor
remains exactly unit scale. This is evidence for the declared local-scale policy;
it does not retrospectively make the original failed rule pass.

The review has its own verified D: copy. Native head sampling and its isolated
file transport are qualified for this study. Continuous-motion accuracy,
anatomical sensor placement, sensory firing and Avatar balance remain open.
`0ada47` is no longer pending. Rubral comparison `f5e01c` still awaits the
operator and has not been inspected.

## Complete sensory-membrane derivative comparison prepared in source

The next authored workflow combines the already declared hair-cell, calyx and
open-cleft equations in one instantaneous comparison. It retains 108 focused
cases (including the twelve previously unrun membrane-coupling cases), requires
all 624 closed and 192 open regression records to remain byte-identical, and
compares 216 complete current/gate/voltage/ion derivatives against independent
60/90-digit equations and a full capacitance-matrix solve. The reference derives
its own currents; it does not use the candidate's current ports as reference
inputs. The new reference and candidate limits are declared before execution;
no earlier model, source constant or bound is changed.

Fixtures cover one/two/eight patches, uniform/graded geometry and local states,
gate extremes, shifted voltages and chemistry, MET/active-current removals,
opposite explicit terminal currents and reversed input order. The prescribed
state and geometric extremes are numerical fixtures, not physiological claims.
The reviewed open capture supplies the preserved source, regression records,
attributed biological source evidence and exact Python/.NET runtime fingerprints.

The capture authoring build has zero warnings/errors. Python/PowerShell syntax,
fixture shape and static test inventory checks are authoring checks only. The
visible frozen workflow will execute all tests and equations; no new numerical
comparison, test or model has run inline. See
`DNNE_Cleft_Membrane_Coupling_2026-11-17.md` for scope and the subsequent joint
time-integration requirement. No live Avatar, World or editor selection changes.

The membrane workflow is now launched as `cleft-membranes-20261118-74e956`,
frozen at pushed revision `274214bc6c44538fbf0312ef730dcac843d437e3`. All 1,864
initial files plus manifest were verified on D: before the launcher opened
visible task `DNNE-Cleft-Membranes-8551693db200`. No post-launch results, status
or processes have been inspected. Two completion reports are pending: this new
membrane study and rubral comparison `f5e01c`. Skull `0ada47` is reviewed and
passing within its sampled-measurement scope. Full goal remains active.

## Complete membrane derivative result reviewed

The operator reported membrane `74e956` complete. New saved-data review
`artifacts/cleft-membranes-review-20261118/review.json` verifies all 2,424 C:/D:
files, frozen Git source/test/probe binaries, exact prior and unchanged managed
runtime, numerical environment and all 108 focused cases. All eight regression
files preserve the 624 closed plus 192 open records byte for byte. All 216
complete derivative evaluations agree with their saved independent 60/90-digit
references under the original bounds; the largest error uses about 4.2044% of
its allowance. Saved analysis reproduces exactly without evaluating model or
reference equations. The new review has a verified D: copy.

`74e956` is no longer pending. Rubral `f5e01c` remains uninspected awaiting the
operator. The reviewed membrane result establishes sampled instantaneous
coupling, not time integration, anatomical calibration or afferent firing.

Independent authoring adds `AvatarVestibularCleftDynamics`, a complete coupled
SDIRK2 integrator with current-stage voltages/ions/gates, explicit held forcing,
integer local clock, compensated state/flux quadrature, local charge/K/Na ledgers
and atomic step acceptance. Invalid states are not clipped; failures retain the
accepted prefix and failed phase/stage/forcing. Initialization takes explicit
actual gates and makes no equilibrium claim. Biological source files are
unchanged and no live body/editor path selects it.

The test-project authoring build passes with zero warnings/errors. Twelve new
cases (120 with earlier components) await a fresh visible workflow. See
`DNNE_Cleft_Coupled_Dynamics_2026-11-18.md` for the solver, bookkeeping, exact
scope and next independent trajectory/stationarity work. No model/test was run
inline, and no pending study was read. Full goal remains active.

Joint dynamics components are now launched as `cleft-dynamics-20261118-ca2f31`,
frozen at `b4f0d57fa93fab7f935547345d8e078a05270608`; initial 1,847 files plus
manifest D: verified. Visible task: `DNNE-Cleft-Dynamics-9c501d09763f`. No
post-launch outputs/status/process inspection. Two reports are pending: this
new 120-case component run and rubral `f5e01c`. Membrane `74e956` and skull
`0ada47` are reviewed/passing within their stated scopes. Full goal active.

## Joint dynamics components reviewed; full comparison authored

The operator reported `ca2f31` complete. Full saved-data review verifies 2,391
files per C:/D: copy, frozen source/test/probe binaries, exact managed runtime
and all 120 component cases. The saved analysis reproduces exactly without a
model or test rerun. New `cleft-dynamics-review-20261118` has a verified D: copy.
Only rubral `f5e01c` remains pending at this point, still untouched.

The independent full comparison now has seven conditions at three candidate
steps, all accepted states/compensation/ledgers retained, and 21 separately
integrated Radau/BDF references using the independent Decimal/full-matrix RHS.
Every reference accepted node and comparison sample must remain in the physical
domain. Saved 60/90-digit witnesses check RHS precision; an independent capacitor/
incidence audit checks every candidate endpoint and reference node. Reference
agreement, candidate accuracy, halving behavior and conservation are separately
assessed under prospectively declared bounds. No candidate source or previous
bound changed; no inline numerical execution occurred. The capture build has
zero warnings/errors and static checks pass. See `DNNE_Cleft_Trajectories_2026-11-18.md`.

This remains a numerical qualification stage. Quiet duration is not equilibrium;
stationary solution/stability, longer responses, physiological calibration,
afferent firing and live brain-body return remain open. Full goal active.

The full trajectory workflow is now launched: `cleft-trajectories-20261118-87562a`,
frozen at `22148e322fe862e8b69c2f42388aa26534b8539a`; initial 1,866 files plus
manifest D: verified. Visible task: `DNNE-Cleft-Trajectories-8ff1f1475f1e`. No
post-launch outputs/status/process inspection. TWO reports are pending: this new
trajectory comparison and rubral `f5e01c`. Dynamics `ca2f31`, membranes `74e956`
and skull `0ada47` are reviewed/passing within their separate scopes. Full goal
active; stationary physiology, afferent return, balance and recovery remain open.

## Both pending comparisons completed and reviewed

The operator reported sensory `87562a` and rubral `f5e01c` finished. Both are now
reviewed; neither is pending. See `DNNE_Coupled_Outcomes_2026-11-18.md` for the
verified saved-data results and D: review backups. Sensory passes six of seven
conditions; eight-patch apex-charge error falls about fourfold per halving but
remains 1.501873 times its bound. All reference/refinement/conservation checks
pass. Motor fails only yield fraction, about 3.94% over its limit under strong
drive, peaking in the opposite passive muscle. Original failures/bounds remain.

A finer sensory workflow is authored, reusing all completed references and
preserving every equation/initial state/limit. It captures 0.001/0.0005/0.00025 ms
and exactly compares overlapping saved metrics. The stationary-search initializer
and twelve analytic/failure controls are also authored; the combined workflow
runs 132 focused cases before trajectories. No active root, stability or
physiological rest claim is made. See `DNNE_Cleft_Stationary_Search_2026-11-18.md`.
Authoring builds pass with zero warnings/errors; no new test/model has yet run.
No motor rerun is launched: next separate physical/muscle step refinements to
distinguish partition error from muscle integration error. Full goal active.

Finer sensory study launched as `cleft-refinement-20261118-884934`, frozen at
pushed revision `fb61de2fd0af6b2ea01ec9b136c5e96fa0873415`. Initial 2,185 files
plus manifest verified on D:. Visible task `DNNE-Cleft-Refinement-96fcdeb342d8`.
This ONE window runs 132 component cases (including stationary-search controls)
and 21 native trajectories against copied saved references, with no reference
ODE reruns. No post-launch outputs, status or processes have been inspected.
Await operator completion. No other study is pending: sensory `87562a` and rubral
`f5e01c` are reviewed failures with original evidence preserved. No separate
stationary or motor window launched. Full goal remains active.

## Finer sensory result verified

The operator reported `cleft-refinement-20261118-884934` complete. Review
`artifacts/coupled-finer-review-20261118` verifies all 2,820 files on both C: and
D:, frozen Git source/test/probe binaries, exact prior and unchanged runtime, and
all 132 focused cases (including all twelve stationary controls). Recomputing
only saved-array/accounting comparisons exactly reproduces the final analysis.
The two-file review also has a verified D: copy.

All seven numerical conditions pass at the finest 0.00025 ms step. Worst allowed
fraction is `0.37435413377613114` for eight patches, down from
`1.5018732660967322` at 0.0005 ms. All repeated overlapping metrics, independent
reference, sampled refinement and ledger criteria pass unchanged. No fresh
reference solve was needed. This confirms the diagnosed finite-step error for
these sampled trajectories, not active physiological rest, afferent firing or
balance. All previous studies are now reviewed; `884934` is no longer pending.

A targeted motor/body clock matrix is now authored in
`docs/DNNE_Motor_Partition_Refinement_2026-11-18.md`: five strong-left neuronal
epochs, 95,988 native steps, 287 focused cases, unchanged biological equations
and original accuracy bounds, and three copied completed references. Physical
step refinement is separated from muscle substep refinement at a common duration.
The original seven-case saved accounting must reproduce before new execution.
Source-only authoring builds and static checks pass; no new model/test has run.
No full-direction or physiological qualification is implied. C: space is limited;
new build scratch uses D:, and prospective free-space guards retain old evidence.
No pending runs at this preparation checkpoint. Full goal remains active.

Motor partition diagnosis launched as `rubral-refinement-20261118-3d8835`, frozen
at pushed revision `778c7f2fddcd385922fc62e95719bb0e3754f88d`. All 1,947 initial
files plus manifest verified on D: before visible task
`DNNE-Rubral-Refinement-561e9ba124b9` was started. Build scratch is the fresh
`D:/DNNE-build-work/rubral-refinement-20261118-3d8835` directory. No post-launch
outputs, status or processes have been inspected; await operator completion.

There is ONE pending window: this five-epoch strong-left motor diagnosis.
Sensory `884934` is fully reviewed/passing, and old motor `f5e01c` is reviewed
with its original yield failure retained. No separate sensory or stationary
window is pending. Full goal remains active; physiological resting state,
afferent return, complete feedback, balance and recovery are still open.

Active sensory resting-state workflow is now authored; see
`DNNE_Cleft_Active_Rest_2026-11-18.md`. It preserves the reviewed biological
kernels and stationary search, and compares 18 native searches with independent
60-digit roots and 90-digit residual witnesses. Three guesses for six geometries
are followed by full voltage/ion/free-gate Jacobians at three difference steps.
Stable, unstable, nonunique and inconclusive outcomes remain separate from
whether a numerical root was found. No time perturbations, afferent cable or
live initialization is introduced. Authoring probe build: zero warnings/errors;
Python/static 132-case inventory and PowerShell syntax checks passed. No new
model/test/control executed inline. Motor `3d8835` remains pending and untouched.
At this authoring point there is still only that ONE pending window. Prepare,
verify D: and launch the sensory study next; await operator completion for both.

Sensory active rest launched as `cleft-rest-20261118-760702`, frozen at pushed
revision `e4e3cf2243d108a49a49162b3ef5173605989c25`. All 1,880 initial files plus
manifest verified on D: before visible task `DNNE-Cleft-Rest-b9015911e2fc`
started. No post-launch outputs, processes or status have been inspected.

There are now TWO pending windows: motor partition `3d8835` and sensory rest
`760702`. Await the operator's completion reports. Sensory refinement `884934`
remains fully reviewed/passing. The active goal remains unfinished: no resting
physiology, afferent firing, closed-loop balance or fall recovery is qualified.

Independent work while `3d8835` and `760702` remain untouched: authored the
actual sensory nerve membrane, identified finite-volume axon and reciprocal
same-state calyx/axon derivative owner. See `DNNE_Vestibular_Afferent_2026-11-18.md`.
The named candidate uses deposited channel expressions and explicitly selected
May14 sodium timing tables; COMSOL defaults are not inferred. All 46 timing
values match the retained source data; C:/D: source evidence was verified.
Physical axial/current signs follow conservation with explicit terminal owners.
The test-project authoring build passed with zero warnings/errors. Static checks
declare 166 focused cases, 34 new. No model/test/reference executed inline.
Prepare, backup and launch the new component window next. Subsequent independent
derivative/trajectory comparison and loaded stationary state remain required;
do not promote these components directly to live Avatar, Unity or DNNE.

Sensory nerve component checks launched as `afferent-components-20261118-d3fbc9`,
frozen at pushed revision `751f92fc6fa1101f36229481744c62ee0e82f8e4`. All 1,891
initial files plus manifest were verified on D: before visible task
`DNNE-Afferent-Components-3cde74ea5376` started. No post-launch outcome, status
or process was inspected. There are now THREE pending reports: motor partition
`3d8835`, sensory rest `760702`, and nerve components `d3fbc9`. Await operator
completion. The full goal remains active; component construction and test
launch do not establish independent coupled accuracy, firing or Avatar balance.

Independent nerve derivative workflow is now authored, not launched. See
`DNNE_Afferent_Numerics_2026-11-18.md`: 1,686 native prescribed cases, complete
60/90-digit independent witnesses, channel/clamp/cable/joint comparisons and
explicit charge/power accounting. Candidate biology and earlier sensory
equations remain unchanged. The new component reviewer requires an operator
completion report; the freezer requires its passing, D:-verified review.
The probe builds with zero warnings/errors, and Python fixture/inventory and
PowerShell static checks pass. No model, test or numerical control ran inline.

Still THREE pending reports: motor partition `3d8835`, sensory rest `760702`,
nerve components `d3fbc9`. None has been inspected since launch. No fourth
window or numerical package was started. Review reported results next, then
freeze/backup/launch the derivative comparison if the component gate passes.
Free sensory trajectories, loaded rest, firing, physiology and Avatar usability
remain open; the full goal stays active.

Further independent authoring: `AvatarVestibularAfferentDynamics` now advances
hair cell, cleft/calyx and active nerve voltage/gates in one implicit stage,
with reciprocal current and per-compartment/terminal charge ledgers. See
`DNNE_Afferent_Dynamics_2026-11-18.md`. Live sensory ingress still uses legacy
frame-driven stimuli; no live route or editor/Unity selection was changed.
Fifteen new component cases are authored (181 total). Probe/test-project
builds and static checks pass; no new model, control, reference or test ran.
The component freezer requires the passing, reported/reviewed derivative
comparison first. No dynamics package or additional window was started.

Pending reports remain exactly THREE: motor `3d8835`, rest `760702`, nerve
components `d3fbc9`. An asynchronous completion question was sent; no reply or
post-launch result inspection yet at this checkpoint. The previous goal turn
and this turn made source/code progress, not a verified process wait. Full
goal active. Next: review any operator-reported results, launch the already
authored derivative study after its gate passes, then the joint dynamics
components. Independent active trajectories/loaded rest and actual sensory
spikes/declared DNNE delivery still follow before balance/recovery claims.

Operator now reports nerve components `d3fbc9` and sensory rest `760702`
complete, and confirms motor `3d8835` still running. Both completed results are
fully reviewed; see `DNNE_Sensory_Rest_And_Nerve_Results_2026-11-18.md`.
Nerve: 2,435 files per copy, 166 passing cases. Rest: 2,525 files, 132 passing
cases, all eighteen native/independent roots agree and recorded local spectra
are decaying estimates. Saved analysis/matrix checks reproduced without model,
root/eigen solve or test reruns. New review folders are D: verified. Eigenvectors
were not retained, so the review does not claim independent vector verification.
The sealed terminal result does not initialize the newly attached active nerve.

Only ONE prior window remains pending: motor `3d8835`, untouched. Prepare and
launch the independent nerve derivative rig next. Before these reports arrived,
fast-transient fixtures/native capture were authored; their independent driver,
analysis and launcher remain unfinished. See `DNNE_Afferent_Fast_Transient_2026-11-18.md`.
No transient package or test was run. Full goal active and unqualified for live
afferent return, physiological rest, balance or recovery.

The derivative comparison is now launched as `afferent-numerics-20261118-4ab20e`,
frozen at `3f49cb952d90b124ab84cb7a727a2a7d4f196593`. Initial 1,914 files plus
manifest are D: verified; visible task `DNNE-Afferent-Numerics-0dc1320646a5`.
There are TWO pending reports: this new nerve calculation study and motor
refinement `3d8835`, which the operator explicitly says is still running.
No post-launch outputs/status/processes inspected. The new study runs 166
focused cases and 1,686 native derivative/clamp cases with 60/90-digit
references; it is a correctness comparison, not a competing timing benchmark.
On reported completion, review both complete backup copies and saved results
before the authored joint-dynamics component gate. Fast-transient fixtures and
capture are checkpointed, but their independent comparison remains unfinished.
No live selection changes or physiological/Avatar qualification. Full goal active.

Nerve comparison `4ab20e` is now operator-reported and fully reviewed: all 2,473
C:/D: files, 166 focused cases and 1,686 derivative/clamp comparisons pass.
Saved analysis exactly reproduces, with maximum candidate allowed fraction
0.01588438; its new review has a verified D: copy. See
`DNNE_Afferent_Numerical_Results_2026-11-18.md`. No model/test reran during review.
Prepare and visibly launch the existing joint dynamics 181-case workflow next.

The subsequent fast-transient rig is now fully authored: independent Decimal
joint RHS, Radau/BDF trajectories, full saved-state/ledger comparisons, guarded
freezer/PowerShell launcher and completed-data reviewers. Static syntax, fixture
identity mappings, 181-case inventory and 46 preserved-source paths pass. No
new numerical/test execution. Its preparation remains gated on the forthcoming
reported/reviewed dynamics component result. No transient package exists yet.

Operator subsequently reported `3d8835` apparently stalled and requested a
diagnostic. One bounded inspection found analysis failed at the saved event
comparison (analyse-rubral-refinement.py line 48), then entered final backup.
The same PowerShell PID 30552 and interactive task were still active; a three-
second sample recorded 44,398,488 bytes read and 14,799,496 written, plus 0.203125
CPU seconds. Thus backup activity was observed, not a proven stall. No restart,
termination or package edit. Await its verified backup/footer before full review;
do not claim numerical success or final preservation from the intermediate status.
No routine live monitoring resumed. This turn made source and evidence progress.

Joint sensory nerve dynamics now launched: `afferent-dynamics-20261118-0e0e99`,
frozen `f7c0ae8697e21f9f0f1107abc9d9c107f0fec501`, initial 1,915 files plus
manifest D: verified; task `DNNE-Afferent-Dynamics-2761f19f203e`. No post-launch
inspection. TWO pending final reports: this 181-case component study and motor
`3d8835` (analysis failed; final backup observed active during the operator-
requested bounded diagnostic). Nerve derivative `4ab20e` is fully reviewed.
After a passing dynamics review, freeze/backup/launch the authored fast joint
trajectory comparison. It has not been prepared or run. Full goal active;
loaded rest, sensory firing, DNNE return, balance and recovery remain open.

Independent authoring now adds `AvatarAfferentStationarySearch` and fifteen
controls for the connected resting-state problem; see
`DNNE_Afferent_Loaded_Rest_2026-11-18.md`. It uses the full reciprocal joint
derivative, stable sensory/cable identity, bath-relative voltages, log ions and
gate elimination only in the algebraic search. It never resets a live cell.
The existing biological kernels and numerical solvers are unchanged. Failed,
singular and nonunique cases remain explicit; no active physiological rest claim.

Full test-project authoring build passes zero warnings/errors at
`D:/DNNE-build-work/afferent-stationary-authoring-20261118`. Python/PowerShell
syntax, 15 new / 196 total source cases and 46 preserved paths pass static
inspection. A guarded component freezer, visible runner and completed-data
reviewer are authored; no new package, model or test execution. They require a
passing reviewed `0e0e99` first. Active loaded-root independent comparison and
full free-gate stability (including retained eigenvectors) are still future work.
TWO pending reports remain `0e0e99` and motor `3d8835` final backup; neither
was inspected this turn. This is source progress; the full goal remains active.

The operator has now reported both final footers. Joint nerve dynamics
`0e0e99` passed its completed-data review: 2,459 files in each C:/D: copy,
181 focused cases, exact saved analysis, source/binary/runtime preservation.
Its new review is D: verified; see `DNNE_Afferent_Dynamics_Results_2026-11-18.md`.

Motor `3d8835` finished the 583,562-file backup with an analysis failure. The
five native conditions and 287 tests completed, but the analyser demanded equal
contact IDs across fresh sessions. Source and six saved initial/topology records
confirm those IDs must differ. The complete original audit and corrected
accuracy outcome remain pending. A new saved-only continuation preserves all
original IDs and bounds, validates the per-session identity derivation, and
compares physical source/target/event timing exactly. Twelve prospective
controls execute visibly before full saved-record comparison. No simulation
needs rerunning. See `DNNE_Motor_Epoch_Comparison_2026-11-18.md`.

Python AST/import graphs, PowerShell parsing and diff checks pass authoring
validation. No model or control executed inline. Prepare and back up the new
motor continuation and the already-authored sensory trajectory study, then
launch both visibly without polling. Connected resting-state component work
is also unlocked but remains unrun here. Full goal remains active; numerical
trajectory accuracy, sensory firing, physiology and Avatar usability are open.

Launch checkpoint: both new windows are now started from pushed revision
`c383fcdb4ffe7ba4485ccbb7dbe253de9dcfc6eb`. Saved motor comparison
`rubral-epoch-comparison-20261118-b224a6` has 1,923 initial files plus manifest
D: verified; task `DNNE-Rubral-Epoch-704c9ee951ce`. Sensory comparison
`afferent-trajectories-20261118-cf2f38` has 1,930 initial files plus manifest
D: verified; task `DNNE-Afferent-Trajectories-dbc29d955f18`. Both launch commands
returned successfully. No post-launch outputs, status or processes inspected.

Exactly TWO pending completion reports. The motor window performs the large
original file audit, twelve mapping controls and saved-record comparisons;
it launches no model. The sensory window performs the prospective short joint
trajectory study, not a firing or physiological qualification. Loaded stationary
components remain authored and unlocked but unprepared/unrun. No old evidence
or save was deleted or overwritten. Full goal remains active and unfinished.

The next stationary component window was launched as `54d343` at revision
`f8b5bf77863f719cb918ad0c30245d1ef1cd8ba7`, with 1,928 initial D:-verified files
and task `DNNE-Afferent-Stationary-4cff47bb0de6`. The operator then reported
completion. Its full saved-data review verified 2,472 files per C:/D: copy,
196 focused cases, exact analysis and source/binary/runtime preservation.
The new review folder has a verified D: copy. No model/test reran in review.

Active connected rest is now fully authored: eighteen native searches and
eighteen independent 60-digit derivative/90-digit witness comparisons from
three guesses per geometry. All actual cable gates are included in the free
dynamic Jacobian, with retained eigenvectors and saved-matrix audits. Original
bounds and failed/alternative outcomes remain visible. Source build and static
checks passed; no root, eigen solve, equation or control ran inline. See
`DNNE_Afferent_Active_Rest_2026-11-18.md`. Next freeze, back up and launch its
isolated visible window. Motor `b224a6` and trajectories `cf2f38` remain the TWO
pending reports, untouched. Full goal active; no live selection changes or
physiological/Avatar qualification.

Connected active rest is now launched as `afferent-rest-20261118-62949f` from
pushed revision `2923baf4ede902ad174ba72deb5d2f202e030ed7`. Its initial 1,936 files
plus manifest were D: verified before task `DNNE-Afferent-Rest-ebea164525b3`
started. The launch command returned successfully; no post-launch output,
status or process was inspected. Exactly THREE pending reports: `b224a6`,
`cf2f38`, `62949f`. Component `54d343` is fully reviewed/passing. No simulation,
root/eigen solve or test ran inline, and no live selection or old evidence was
changed. Full goal remains active and unfinished.


The operator subsequently reported BOTH rest `62949f` and fast trajectories
`cf2f38` FAILED and complete. Full saved reviews verified 2,601/2,964 C:/D:
files and 196/181 focused cases, with runtime/source preservation. Reviews are
D: verified. See `docs/DNNE_Afferent_Precision_2026-11-18.md`: the rest audit's
NPZ memory-layout mismatch is repaired without changing any numerical bound;
all 18 independent roots and 12 native roots verify, with six native line-search
failures preserved. All seven finest trajectory errors meet direct limits, but
six reference and six refinement checks fail. The unchanged-model, tighter
solver-tolerance experiment is being prepared. No new model/test ran inline.
Event observer source is build-checked but its 20 cases are UNRUN and it has no
live caller; see `docs/DNNE_Afferent_Event_Observer_Draft_2026-11-18.md`.
Exactly ONE older report remains pending: saved motor `b224a6`, untouched.
Full goal active; no sensory firing, physiology or Avatar qualification.


The tighter solver study is now launched as
`afferent-precision-20261118-ddef1a`, frozen at pushed revision
`f2dc6db879860be54a888584fd6b91d9f33af3a9`. Frozen preflight passed and its
1,948 initial files plus manifest were D: verified before visible task
`DNNE-Afferent-Precision-48c2161ea474` started. The launch command returned
successfully. No post-launch output, status or process was inspected.
Exactly TWO pending reports: older motor `b224a6` and new precision `ddef1a`.
The observer's 20 proposed cases remain unrun; it is excluded from this study's
181 focused cases. Eight spectrum controls execute in the new window before
trajectory comparisons. No live selection, old save or original bound changed.
Full goal remains active and unfinished.


Motor saved review `b224a6` was subsequently reported FAILED and complete.
Its completed-failure audit verifies 1,929 C:/D: files and 1,919 Git source
files; the review is D: verified. Both original 583,562-file hash loops appear
complete in its preserved log, but the next source-archive stage could not
spawn Git from the independent window PATH. No identity controls or numerical
comparison executed. See `docs/DNNE_Motor_Epoch_Comparison_2026-11-18.md`.
The freezer now packages an independent original-revision archive and checks
it before the long audit, with separate phase receipts. Corrected retry is
being prepared; no model or native Unity rerun is required. `ddef1a` is the
ONLY pending report and remains untouched. Static sensory ingress findings
are in `docs/DNNE_Observed_Sensory_Ingress_Review_2026-11-18.md`; no new ingress
implementation or live selection change is claimed. Full goal active.


Corrected saved motor comparison is now launched as
`rubral-epoch-comparison-20261118-e9bed6`, frozen at pushed revision
`dfe3464f0b8ae3844120c06087a23c6459999e64`. The frozen source/dependency
preflight passed with PATH deliberately empty (no model/test executed), then
1,943 initial files plus manifest were D: verified. Visible task
`DNNE-Rubral-Epoch-46fc1982967a` started successfully. No post-launch output,
status or process was inspected. The window performs saved-data review and
12 identity controls; no neural, muscle, Unity or ODE run is started.
Exactly TWO pending completion reports: motor `e9bed6` and sensory precision
`ddef1a`. Earlier `b224a6` is failed/reviewed, not pending. All original evidence
and bounds remain preserved; full DNNE/Avatar/World goal active and unfinished.


Observed sensory packet/receiving ingress is now authored, with a clean full
project authoring build (zero warnings/errors). It preserves original crossing
IDs/times, quiet source progress, exact retries, bounded acknowledged outbox,
explicit target contacts and remaining fractional delays. The isolated target
cannot advance beyond the known sensory horizon. Fresh vestibular-only opt-in;
no live service selector or duplicate legacy input. Cell/learning equations are
unchanged. See `docs/DNNE_Observed_Sensory_Ingress_Review_2026-11-18.md`.
A 264-case visible component workflow is prepared in source, including the
previously unrun 20 observation cases and 20 new ingress cases. Static inventory,
48 preserved sources, Python/PowerShell parsing and build checks pass; tests
have NOT executed. This checks connection mechanics, not physiological firing,
loaded rest, spatial calibration or Avatar balance. Older windows `e9bed6` and
`ddef1a` are still the TWO pending reports and remain untouched. Full goal active.


Observed sensory ingress controls are now launched as
`afferent-ingress-20261118-d519cf`, frozen at pushed revision
`3afd4e7623c524bedde78c1d304ac3abbfc92ac5`. Frozen preflight passed; 1,953
initial files plus manifest were D: verified before visible task
`DNNE-Afferent-Ingress-fa7811807879` started. Launch returned successfully;
no post-launch output, status or process inspected. The window includes all
264 declared cases, including the previously unrun observation controls.
Exactly THREE pending reports: sensory ingress `d519cf`, sensory precision
`ddef1a`, and saved motor comparison `e9bed6`. No completion or test success
is inferred. Live sensory firing, physiological calibration, closed-loop
World control and Avatar recovery remain unqualified. Full goal active.


The operator reported sensory ingress `d519cf` complete. Its saved review
verifies 2,497 C:/D: files, 1,944 Git files and all 264 focused cases, with exact
analysis reproduction and preserved runtime/neuronal equations. The one-file
review is D: verified. See the completed section of
`docs/DNNE_Observed_Sensory_Ingress_Review_2026-11-18.md`. No model/test reran inline.
Exactly TWO older reports remain pending: precision `ddef1a` and motor `e9bed6`.
The repeated `b224a6` footer is the old reviewed Git/PATH failure, not the retry.

New physical bundle input authoring is documented in
`docs/DNNE_Vestibular_Bundle_Input_2026-11-18.md`. It requires explicit dimensional
coefficients and provenance; none is a physiological preset. It preserves the
existing mechanics, original samples and causal availability, and bounds pending
physical intervals. Twenty-five new cases plus 28 skull/mechanics regressions
join the reviewed 264 cases in a prospective 317-case visible workflow. Tests
have NOT run during authoring. No live selection, save or physiology claim
changed. Both pending captures remain untouched; full goal active.


The physical bundle component window is now launched as
`vestibular-bundle-20261118-092238`, frozen at pushed revision
`2debce1486ded90f94d3f585f8fa07b2a71b4f94`. Frozen static preflight passed;
1,959 initial files plus manifest were D: verified before visible task
`DNNE-Vestibular-Bundle-471197ff7f26` started. Launch returned successfully.
No post-launch output, status or process was inspected. All 317 declared cases
execute only in that independent PowerShell window; no success is inferred.
Exactly THREE pending reports: bundle `092238`, sensory precision `ddef1a` and
saved motor comparison `e9bed6`. Ingress `d519cf` is reviewed/passing, not pending.
No physiological calibration or live brain/body selection changed. Full goal
remains active and unfinished; retain all original evidence and saves.


Independent mechanical source review is complete while all THREE reports
(`092238`, `ddef1a`, `e9bed6`) remain pending and untouched. See
`docs/DNNE_Vestibular_Mechanical_Source_Audit_2026-11-18.md`. Five original article
bodies and source equations are retained in the 36-file D:-verified artifact
`vestibular-mechanics-source-20261118`. A static hash-bound transcription saves
Ifediba 2007's three 6x6 matrices/three forcing columns and exact decimal SI
conversions in `docs/reference-data/vestibular-ifediba-2007.json`.
No model, test, root, eigensystem, trajectory or benchmark executed. No live
consumer or calibration preset was added. Cupula mechanical relaxation must
remain distinct from neuronal adaptation; a gain on the inherited 5 s/45 ms
filters is insufficient physiological calibration. The inherited 80 ms otolith
filter likewise is not qualified by the literature. Source prime directions,
posterior/right/superior axial-vector conventions, bilateral placement and local
bundle deformation remain explicit next-model requirements. The turtle otolith
measurements are not human calibration. Full goal active and unfinished.


The operator reported bundle `092238` and sensory precision `ddef1a` complete.
The bundle review verifies 2,503 C:/D: files and all 317 focused cases, exactly
reproducing analysis. Precision review verifies 2,983 C:/D: files, 181 tests,
eight spectrum controls and 21 native/21 reference captures. Both review
artifacts are D: verified. Precision remains FAILED: all references and finest
direct errors pass, but six of seven refinement criteria fail. No bounds changed.
Saved-only boundary diagnosis shows smooth-prefix refinement and error growth
after the -55 mV activation-table discontinuity; its two-file artifact is D: verified.
See `docs/DNNE_Afferent_Activation_Continuation_2026-11-18.md` for evidence and
an explicit endpoint-hold kinetic candidate. This changes an assumption, not the
original result. Legacy APIs/defaults remain. A separate 332-case window with
21 exact legacy replays and new candidate references is prepared; tests have
NOT run during authoring. Both authoring builds pass without warnings/errors.
Only ONE older report is pending: motor `e9bed6`, untouched. Live selection,
old saves and physiological qualification remain unchanged. Full goal active.


The explicit activation continuation comparison is now launched as
`afferent-endpoint-20261118-2b5a68`, frozen at pushed revision
`314a072e72831ea479ecb134049b9400c21ea9e9`. Static frozen preflight passed;
2,065 initial files plus manifest were D: verified before visible task
`DNNE-Afferent-Endpoint-19a507a1d31c` started. Launch returned successfully.
No post-launch output, process or status was inspected. The window runs 332
focused cases, 21 exact original native replays, 21 candidate trajectories and
21 independent references under the original acceptance bounds. No success is
inferred. Original defaults, failed captures, saves and live selection remain.
The operator has now reported older motor `e9bed6` complete; its saved review is
underway. Exactly ONE completion report is pending: new sensory `2b5a68`.
Full DNNE/Avatar/World goal remains active and unfinished.


Completed motor epoch comparison `e9bed6` is now fully reviewed: 1,961 C:/D:
package files and frozen Git source verified; all saved numerical analysis and
five epoch mappings reproduce exactly from the 95,988 original physical steps.
The original window's structured 583,562-file full-audit receipt is retained;
that full hash loop was not repeated. All twelve identity controls passed.
The ten-file reviewer artifact is D: verified. No model or test reran in review.
See `docs/DNNE_Motor_Epoch_Comparison_2026-11-18.md`.
The finer body configurations pass every original absolute/refinement limit on
the common 8,000-point grid. Yield error falls from 1.03936 to 0.26142 times its
unchanged bound; extra internal muscle steps have negligible influence. This
supports body/muscle exchange spacing as the dominant tested error. Original
failures remain. Other directions/controls at the refined resolution, upstream
accuracy, physiology and recovery remain open. Next motor work: full condition
matrix at explicit h/4 body and h/4 muscle, after non-destructive storage planning.
C: had 5.83 GiB free at this checkpoint, below the old motor freezer's 8 GiB gate;
do not bypass that gate or delete old evidence. D: had 253.12 GiB free.
Exactly ONE pending completion report: sensory candidate `2b5a68`, unmonitored.
The bundle and precision reports are both reviewed, not pending. Code/notes are
committed and pushed regularly. Full DNNE/Avatar/World goal active, unfinished.


The operator reported sensory activation window `2b5a68` FAILED. Its completed
review verifies 2,609 C:/D: files, exact 332-case inventory (331 pass, one fail),
eight spectrum controls, frozen source/binaries and runtime. The one-file review
is D: verified. No original replay, candidate trajectory or independent reference
started. The failed case is the one-ULP interior neighbour of +60 mV: subtraction
rounds the last interval index up to the final knot, then indexes beyond the
array. This defect exists in the shared original table lookup and was exposed
by the new boundary control; it is not a candidate trajectory outcome.
The repair bounds the interval index to the final valid segment, preserving
voltage, knots, interpolation, outside continuation and scientific limits.
A second regression checks both activation/inactivation tables and both policies.
The retry now has 333 focused cases; the original boundary test remains intact.
Its original 21-trajectory byte replay still guards previously successful v1.
No tests/models executed during authoring. No completion report is pending
before retry launch; all older windows are reviewed.


The boundary-index repair retry is launched as
`afferent-endpoint-20261118-501244`, frozen at pushed revision
`a3474953a1aff5b26c63cffce3eaaab884b902ef`. Full test-project authoring build
passed with zero warnings/errors; static 333-case inventory, Python/PowerShell
parsing, original protocol identity and 56 protected-source comparisons passed.
Frozen preflight passed and 2,069 initial files plus manifest were D: verified
before visible task `DNNE-Afferent-Endpoint-11b2b1598b62` started. Launch returned
successfully. No post-launch output, status or process inspected. All tests and
models execute only in the independent PowerShell window. Exactly ONE pending
completion report: retry `501244`. Failed `2b5a68`, precision `ddef1a`, bundle
`092238` and motor `e9bed6` are all reviewed, not pending. Failed traces, old
saves, original successful-replay requirement and numerical limits remain.
Full DNNE/Avatar/World goal remains active and unfinished.


Independent work while sensory `501244` remains pending: a lossless native
recording container is authored in `tools/native_evidence_archive.py`. See
`docs/DNNE_Native_Evidence_Storage_2026-11-18.md`. It preserves every file byte,
requires source/index/hash verification before atomic non-overwriting publication,
and exposes a checked read-only decoder view. It has no source deletion or
migration API. No neuronal/muscle/native model, clock, live selector or old
capture changed. The next full motor matrix needs bounded recording storage;
C: cannot safely repeat the prior half-million-file layout.
A visible qualification is prepared: 24 controls plus two already completed
motor captures, 241,906 files/1,200,072,296 original bytes, comparing all 8,000 and
31,997 decoded physical endpoints. Tests/conversion have NOT run during authoring.
Static Python/PowerShell parsing, control inventory and environment checks pass.
New qualification work and its verification copy use D: (same physical drive),
while the original independent C:/D: raw copies remain untouched. This avoids
consuming the pending sensory run's reserved C: space. Source and failed-prefix
preservation remain mandatory. Only ONE pending completion report before launch:
sensory `501244`, never inspected. Full goal active; no blocked audit applies.


Compact evidence qualification is launched as `native-evidence-20261118-bc0501`,
with workspace `D:/DNNE-build-work/native-evidence-20261118-bc0501`, frozen at
pushed revision `9eff479fb3ceb01dbe429e7941c61335ea248422`. Static frozen
preflight passed; 1,976 initial files plus manifest were verified at
`D:/DNNE-desktop-runs/UnityWorld/native-evidence-20261118-bc0501` before visible
task `DNNE-Native-Evidence-956f6d8a1733` started. These new qualification copies
share D:; the original raw motor captures still retain their C:/D: copies.
Launch returned successfully. No post-launch status, output or process inspected.
The window runs 24 recording controls and the two completed-capture comparisons;
no neural/Unity/ODE model reruns. No success, size reduction or integration is
inferred. Exact file bytes and decoded arrays must pass before adoption.
Exactly TWO completion reports are now pending: sensory `501244` and compact
evidence `bc0501`. All older windows are reviewed. Original records and saves
remain untouched. Full DNNE/Avatar/World goal remains active and unfinished.

Compact evidence `bc0501` reviewed: 2,021 files in each package/backup verified,
all 24 controls passed, all 241,906 archived members match their original hashes,
and 39,997 decoded physical samples reproduce exactly. Archives total 409,936,598
bytes versus 1,200,072,296 original bytes, a 65.8407% reduction. Four-file review
is D: verified. Original raw C:/D: copies remain; new archive copies both use D:.
See `docs/DNNE_Native_Evidence_Storage_2026-11-18.md`. Live capture/accounting
integration and full refined motor matrix remain unqualified. Only ONE report
pending: sensory retry `501244`, unmonitored. Repeated `2b5a68` FAILED message
is the already repaired table-index defect. Source-based coupled canal mechanics
are being authored independently; no tests/models have run for that candidate.
Full DNNE/Avatar/World goal remains active and unfinished.

A source-declared coupled canal mechanical candidate is authored independently
of sensory retry `501244`. See `docs/DNNE_Coupled_Canal_Mechanics_2026-11-18.md`.
It uses the retained Ifediba specimen's six-volume M/C/K equations in SI, with
source-prime acceleration input. Radau IIA stages retain physical and numerical
energy loss separately. Existing Avatar/neuronal/Unity kernels and live selectors
remain. Source-to-skull conversion, bilateral/local bundle maps, physical-clock
adapter and independent published-matrix trajectory accuracy remain unqualified.
SimAvatar and full test-project builds pass without warnings/errors. No tests or
models executed during authoring. A 76-case isolated visible window is prepared:
23 new mechanical/analytic controls plus 53 existing clock/skull/bundle cases.
Only ONE completion report remains pending before launch: sensory retry `501244`.
Use D: workspace and backup to retain C: reserve; new copies share D:. Full goal
active; neither component arithmetic nor the source label qualifies physiology.

Coupled canal window `0fe70b` is launched from pushed revision
`2f93d4c55816a30b205f64a758388eddc32432dd`, at
`D:/DNNE-build-work/coupled-canal-20261118-0fe70b`. Static frozen preflight
passed and 1,986 initial files plus manifest were D: verified before visible
task `DNNE-Coupled-Canal-89689213d13c` started. No post-launch inspection.
See `docs/DNNE_Coupled_Canal_Mechanics_2026-11-18.md` for the 76 declared
mechanical/analytic/regression cases and remaining limits. Exactly TWO reports
pending: sensory `501244` and canal `0fe70b`, both unmonitored. Compact storage
`bc0501` is reviewed/passing. Next motor work is archive-aware source/packet/native
accounting before the full refined matrix; next canal work follows its component
review with independent published-model accuracy and explicit coordinate mapping.
Full biological brain/body/world goal remains active and unfinished.

Both reported sensory windows are now fully reviewed. `501244`: all 3,200 C:/D:
files, 333 focused cases, eight spectrum controls, 21 byte-identical original
replays, 21 candidate runs and 21 references verify. Saved analysis reproduces;
all seven candidate cases pass original error/refinement/conservation limits.
The explicit EndpointHold kinetic assumption remains separate from the old
default. Durations are only 0.015625–0.0625 ms, so sustained firing, physiological
rest and balance remain unqualified. `0fe70b`: 2,517 files in each D: package
copy and all 76 component/analytic/regression cases verify, with exact saved
analysis. Full source-specimen trajectory accuracy and skull/bundle mapping
remain open. Both three-file review artifacts are D: verified. No models/tests
reran during review. See their activation-continuation and coupled-canal docs.
There are NO pending completion reports at this checkpoint. Archived motor
accounting is in authoring: unchanged assertions, explicit checked archive
mounts, 12 controls and full raw/archive saved-capture comparison. It has not
launched. Old evidence/defaults/saves remain; full DNNE/Avatar/World goal active.

Archived motor accounting is authored; see
`docs/DNNE_Archived_Motor_Accounting_2026-11-18.md`. The only change to the
reviewed causal auditor is opening compressed records through an explicit
binary stream; static AST comparison proves every other statement/assertion is
unchanged. Checked read-only mounts cannot fall back to raw filesystem records.
The prospective visible run has 12 controls and compares complete raw/archive
source, contact, packet, muscle effort and native reply accounting for the
already reviewed repeat/body-finer captures (39,995 steps plus initial samples).
All decoded arrays and reports must match exactly. No new models or tests ran
during authoring; syntax, static inventory and AST preservation pass. No original
capture or raw staging cleanup is enabled. There are no pending windows before
this launch; both sensory reports are now reviewed. Full goal remains active.

Archived motor accounting window `13c9cf` is launched from pushed revision
`32e2e637a15a71aeeb28b00f137b33f2ae97e94a` at
`D:/DNNE-build-work/native-accounting-20261118-13c9cf`. Static preflight passed;
2,050 initial files plus manifest D: verified before visible task
`DNNE-Native-Accounting-5c590a7b6a8a` started. No post-launch inspection.
Only ONE pending completion report: `13c9cf`, 12 archive-view controls and two
full saved-record audits, no new models. Sensory `501244` and canal `0fe70b`
are fully reviewed/passing; source/default preservation and remaining physiology
limits are in their documents. Next motor work depends on this accounting result;
next canal work is independent source-specimen trajectory comparison, and next
sensory work must address loaded rest/stability and longer free dynamics before
claiming usable afferent firing. Original evidence and saves remain untouched.
Full DNNE/Avatar/World goal active, unfinished; no blocked condition applies.

Archived accounting `13c9cf` is now reviewed/passing: all 2,075 files per D:
package copy, frozen source and twelve controls verify. Original/archive reports
and specifications are byte-identical and source reports exactly reproduce the
reviewed motor comparison. Array digests match the prior recording review.
Scope: 24,000 neuronal frames, 30,002 motor rows, 39,995 physical steps and both
initial samples. Four-file review is D: verified; no model/test reran. Original
raw and archived records remain. New capture staging/verified cleanup and the
full refined motor matrix remain next motor requirements. No pending completion
reports now. Published canal numerical comparison is being authored, with fast
resolution around input changes and larger steps only after resolved transients;
no new mechanical reference/model/test has run. Full goal active, unfinished.


Published canal trajectory comparison is authored; see
`docs/DNNE_Published_Canal_Trajectories_2026-11-18.md`. It reuses the 76 reviewed
component checks and preserves the production mechanical kernels. The separate
window will run six analytic reference controls, 45 native captures across
fast/middle/slow regimes, and 15 independent cases at Decimal 60/90 digits plus
SciPy expm. Every native endpoint and both physical/numerical energy losses are
accounted. No new model, reference or test has executed during authoring. The
probe build passes without warnings/errors. Work/backup use D:; old evidence
and all live defaults remain. No pending reports before launch. Full goal active.
