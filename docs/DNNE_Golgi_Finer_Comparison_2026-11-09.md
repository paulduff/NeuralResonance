# One further Golgi refinement — 9 November 2026

**Current status, 13 November:** the first launched package failed during the
environment preflight, before finer simulations. All 43 focused tests passed.
See `DNNE_Golgi_Runtime_Transition_2026-11-13.md` for the verified cause and retry
design. The original protocol and launch history are preserved below.

The verified [third-order comparison](DNNE_Golgi_Third_Order_Comparison_2026-11-09.md)
completed all 38 cases but failed voltage, gate and calcium accuracy in the five
firing protocols. Errors fell approximately eightfold on the last timestep
halving. This follow-up asks whether one additional halving satisfies the
unchanged full-state criteria; the extrapolated improvement is not a result.

## New work and preserved controls

Fourteen new cases use 0.0009765625 ms: the six original active protocols at
ordinary and eightfold tighter nonlinear tolerances, plus the two original
passive protocols. Initial state, temperature, inputs, durations, 0.25 ms output
grid, threshold crossing interpolation, morphology and cell/channel equations
remain unchanged. The native `--sdirk3-fine` mode checks every declared condition
in order before creating an output directory. The integration and sampling body
after protocol validation is source-identical to the completed 38-case probe.

The preparer requires the operator-reported, backed-up review of that failed
capture. It re-verifies the original C:/D: files and executed binaries and copies
the required frozen source, references, 38 traces, accounting and metadata into
a new package. The retained `baseline` is an explicitly documented input subset,
not a new full execution or a replacement for the complete original capture.
The original backup manifest is retained for provenance; excluded build products
remain in the unchanged original C:/D: capture. The new package has its own
complete hash manifest and D: backup.

Before new integration, the window must reproduce the old analysis exactly from
those copied arrays, including its failure. Missing references cannot trigger
new ODE solves. The original analyzers, protocol and relevant Golgi mechanism
sources must match the completed capture. Different mechanisms require a new
comparison design instead of reusing these coarse traces.

## Unchanged acceptance

Both fine active tolerances must meet both original numerical references: voltage
0.05 mV, gates/SK 0.0005, calcium 5e-7 mM and corresponding crossing time 0.01 ms,
with equal crossing counts. Physical domains, complete sample grids, residuals,
charge/calcium ledgers, SK mass and nonlinear sensitivity retain their existing
criteria. No phase alignment or dropped spikes are permitted.

The prior refinement steps remain visible and must still pass. Each new ordinary
fine trace is compared with the retained ordinary 0.001953125 ms trace using the
original refinement ratio, reference-uncertainty allowance and numerical floor.
The old finest absolute failures remain recorded; they do not masquerade as
successes at that resolution. Partial or failed new cases remain preserved and
cannot qualify the result. There are 14 new cases and 38 retained cases, not 52
fresh simulations.

## Execution and present status

`prepare-golgi-sdirk3-fine.py --review REVIEW --cost-study-finished` requires clean,
pushed source and an operator completion report for timing. Initial C:/D: backup
precedes `start-golgi-sdirk3-fine-window.ps1 -PackageDirectory PACKAGE
-CostStudyFinished`. The independent visible window runs the same 43 focused
component/manifest tests, checks frozen evidence, performs the 14 conditions,
records full outcomes and verifies the final D: backup. No live monitoring,
automatic retry or artificial runtime deadline is introduced.

The probe builds with zero warnings/errors. Python and PowerShell syntax checks
pass and static comparison confirms the integration/sampling body and old
analysis sources are unchanged. New tests, protocol execution, array-analysis
regression and the finer simulations have not run. This rig is coded only: it
has not been frozen, backed up as an experiment, or launched.

The first CUDA cost attempt failed in schedule parsing before timed trials.
Its corrected retry takes priority; keep all competing builds, preparation
hashing, offline analysis and simulations out of that benchmark. The existing
motor scheduling package still follows its completion report. This candidate
does not alter live DNNE, Avatar, Unity, editor labels or saved brains, and even
numerical success would not qualify physiology, circuit function or recovery.

## Preparation, pause and launch

The CUDA cost, motor scheduling, somatic motor-unit and CUDA host accuracy
windows were all operator-reported complete and reviewed before this launch.
The latter two reviews are `DNNE_Somatic_Motor_Unit_Results_2026-11-09.md` and
`DNNE_Olive_CUDA_Host_Results_2026-11-09.md`. No timing window is outstanding.

The package `golgi-sdirk3-fine-20261109-4b5c0f` was frozen on 9 November at
revision `69bc40f1593ec5cab71883be705cdbe2a8cfbec4`. Preparation reverified the
original failed Golgi capture, its review and all retained source/evidence.
All 3,089 initial files plus manifest were verified on D:. The operator then
paused the goal before launch; the original package and date were preserved.

On 13 November the operator resumed work. The repository was clean, the frozen
launcher matched the working copy, the configured executables existed, and no
prior launch or execution-status file existed. The launcher rechecked every
initial C:/D: hash before starting independent visible PowerShell task
`DNNE-Golgi-Sdirk3-Fine-b5e096c0065f`. Its 43 focused tests, preserved-analysis
preflight and 14 new conditions remain unreviewed. No live status or output is
being inspected.

This is the only pending experiment. Do not start CUDA host timing until the
operator reports this window complete and its evidence has been reviewed.
No runtime selection or biological qualification follows from launch.
