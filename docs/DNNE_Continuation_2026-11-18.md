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
