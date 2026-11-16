# Active spindle force balance before slack integration

The reported fixed-length mechanics comparison passes. The next missing
mechanical step is handling unloading during movement while preserving the
cross-bridge state. This work does not replace Avatar's sensory path or turn
mechanical stress into prescribed sensory spikes.

The first reported run and the version-2 root-identification repair are now
recorded in `DNNE_Receptor_And_Slack_Results_2026-11-16.md`. The original six-control
run below remains failed and preserved. Its replacement requires eight controls
and the same full 2,812-state matrix, with unchanged acceptance bounds.

## Existing Avatar and source findings

`ControlProgram/PhysicalBodyTransducerRuntime.cs: AddMusclePopulations` still
maps absolute muscle-length deviation, absolute velocity and force into
frame-generated populations. `src/NRE.SimAvatar/AvatarMuscle.cs` supplies the
legacy length/force measurements. The newer dynamic muscle and physical-sample
transport remain separate, preserved mechanisms. The source requirements in
`DNNE_Afferent_Source_Gap_2026-11-16.md` remain open: no reproducible Ia membrane
has been adopted, and future measurements must not revise earlier firings.

Read-only inspection of the twelve already reported moving recordings from
`spindle-state-20261113-a43f1a` verifies their MAT bytes against both C:/D: copies.
Their recorded populations retain the declared nonnegative/site/conservation
bounds. They nevertheless contain negative stress, because those experiments
explicitly impose length and omit the slack driver. At the finest 0.25 ms step,
minimum bag stress is about -149,990 Pa in stretch/shorten and -280,039 Pa in
the below-slack protocol; chain below-slack reaches approximately -72,891 Pa.
These are uncalibrated model outputs, not measured physiological stresses.
They do not contradict the narrower fixed-length result.

The pinned author's `sarcSimDriverIntrafusal20250627.m` attempts unloaded
force balance, but its bag/chain slack flags share an `if/elseif` and it compares
old command length before applying the current command increment. Its root
helper uses a scalar initial guess and cached populations. The canonical state
repair and pure `spindle_trial_stress.m` query already address the state boundary;
they do not establish an active slack trajectory or a unique force-balance root.
Earlier unloaded root controls explain why the scalar guess can miss even a
simple passive root. Preserve those original captures and source files.

## New isolated force-inversion component

`tools/spindle_slack_roots.py` derives the complete piecewise-affine trial force
from the pinned interpolation, cross-bridge and passive equations. Its source
revision is `721ba03cb775261548766ed1349c4bf2e0e35385`; the AGPL-3.0 license and
original/derived source accompany every package.

The source uses linear interpolation with zero outside its finite bin support.
Consequently it can have discontinuities when an occupied endpoint bin leaves
support. A sign change across such a jump need not contain a zero. The new
component separates every open affine interval from the exact knot values and
retains both one-sided force limits. It enumerates zero-load candidates over
the source's open positive-overlap length domain, 305 to 1,935 nm. It does not
pick one root, assume the nearest root is physiological, modify populations,
clip force, or integrate a new trajectory.

Each root retains its residual, local slope where defined, overlap, shifted bound
and detached populations, conservation residual and state-domain status. A slope
is not a stability claim for the full active system. Inadmissible or multiple
roots stay visible. Flat intervals and numerical ambiguity prevent a passing
numerical check rather than silently becoming a selected equilibrium.

An independent NumPy interpolation evaluator plus bracketed Brent search checks
each continuous interval separately. Quarter/midpoint and exact-knot force
comparisons check the affine construction. Six explicit analytic controls cover
both passive fibres, nonzero target load, an exact knot root, a central occupied
bin with three known roots, and an occupied edge bin with discontinuities and
analytically enumerated roots. These artificial controls test mathematics only.

## Frozen experiment and acceptance

The experiment reads all **2,812 states in twelve completed moving traces**.
No Octave trajectory or ODE is rerun. Each saved state gets a fresh pure force
query; outputs retain all roots, state-domain checks and significant jumps.
The numerical limits are inherited from the previous root/force controls:

| Quantity | Absolute limit |
|---|---:|
| Force reconstruction and root residual | 1e-7 Pa |
| Analytic/independent root difference | 1e-9 nm |
| Population conservation residual | 1e-10 |
| State-domain violation | 1e-8 |

Six controls and every saved-state comparison must pass. A failed comparison
preserves its full affine pieces and stops with incomplete coverage recorded.
Successful enumeration can still expose multiple or no admissible roots; those
are scientific findings, not grounds to manufacture a passing slack controller.

`prepare-spindle-slack-roots.py` requires clean pushed source, the reported and
D:-verified canonical-state and fixed-length reviews, matching saved MAT files,
source/license provenance and the pinned numerical environment. Preparation
only parses existing observations and reconstructs their already recorded force;
it runs no root controls, search or kinetics. The visible independent launcher
runs controls and all queries, rechecks frozen files/dependencies and verifies
the final D: backup. No timing benchmark or live monitoring is performed.

The result will determine whether an active slack update can retain a unique
admissible branch or needs additional mechanics. Moving-trajectory accuracy,
continuous-time slack dynamics, fusimotor recruitment, sensory membrane firing,
live brain authority, physiological calibration and Avatar recovery remain
unqualified. The receptor/motor gate is independent and remains unmonitored.

## Authoring and preparation

Three Python files and the visible PowerShell launcher pass syntax parsing.
`spindle-slack-authoring-20261116` preserves six files plus manifest on D:;
no control, root search or kinetics ran during authoring. The first preparation
parsed all twelve reported traces successfully, then exposed a field-name
mismatch while writing its preparation report. The corrected preparer reads
`forceReconstructionErrorPa`. Its partial package `9e00e9` is preserved and D:
verified (1,740 files plus manifest), with no window launched. The two-file
`spindle-slack-authoring-20261116-r2` records the correction and is D: verified.
A new package is required; the partial package is not reused.

## Launch

`spindle-slack-roots-20261116-527839` is frozen from
`cd5b775a4090f18ced1697ae025c7b71fd1f51ea`. Its 1,741 initial files plus manifest
were D: verified, and rechecked by the independent visible launcher. The task is
`DNNE-Spindle-Slack-9f7199654b99`. No post-launch output or process was inspected.
Await the operator's completion report before reviewing its numerical outcome.

There are now two pending reports: this active force check and receptor/motor
gate `receptor-membrane-20261116-f2c9a7`. Neither is monitored. The larger coupled
receptor comparison still requires its own passing gate review before launch.
