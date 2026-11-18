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
