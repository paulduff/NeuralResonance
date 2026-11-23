# Fresh muscle force balance — 23 November 2026

The detailed Avatar muscle previously required a caller-chosen initial fiber
length. With a separately measured path length, that can put the passive
fiber and tendon at incompatible initial forces before any motor event. The
new `AvatarMuscleInitialBalance` selects a fresh fiber length from an explicit
bracket, using `AvatarDynamicMuscle`'s existing force equations unchanged.

The residual is projected tendon force minus projected fiber force. At zero
initial fiber velocity, the geometric inertial term is zero. Every evaluation
constructs a fresh passive muscle at the declared clock epoch; no calcium,
activation, release or arrival is added. A successful result supplies a
`CausalMuscleDefinition` with the chosen fiber length. A bracket without a
root, or an exhausted iteration budget, supplies no definition. Invalid
mechanical/activation domains still reject through the original kernel.

The bisection retains both endpoint states. A returned diagnostic candidate
must lie in the final bracket, including at iteration/representability limits.
Ordinary convergence requires both a fiber bracket no wider than 1e-13 m and
force residual at most 1e-9 N + 1e-12 times reference muscle force. Exact
floating-point equality is reported separately as `FloatingPointForceBalance`;
it is not a claim of exact continuous-equation equilibrium. An independent
80/110-digit calculation measures constitutive floating-point error.

This is a one-time initial-condition calculation. It accepts no evolved
muscle to reset, does not equilibrate after each step, and adds no neural drive
or activation floor. An instantaneous force balance does not make a moving
body stationary and does not produce a standing controller.

## Checks and execution

Twenty new component cases cover algebraically inverted passive balance at
five normalized lengths (0.5, 0.75, 1, 1.25, 1.4), three dimensional geometry
scales, original clock/ledger and motor-unit identity, invalid inputs,
incompatible short paths, bounded failure and subsequent event-driven muscle
evolution. Eight initial states are retained as complete JSON records in TRX.

`muscle_initial_balance_reference.py` independently evaluates the original
passive/tendon equations with Decimal arithmetic and a cosine series, then
bisects at 80 and 110 digits. The references must agree within 1e-60 m. Each
native fiber must be within 1e-11 m of the high-precision root, with independent
force residual within that case's original declared force allowance. The
1e-11 m accuracy criterion is distinct from bisection bracket precision; it
includes floating-point error in the unchanged constitutive evaluator.

The existing `fresh_motor_epoch_study.py --initial-balance` workflow freezes
20 new cases plus 243 established neuronal/motor/plant regressions. The
existing independent PowerShell runner builds the frozen source, captures
selected .NET runtime identities, runs all 263 cases and the eight independent
equilibria, preserves failures and verifies the D: backup. Python's Decimal
implementation is additionally hashed. No live monitoring.

Authoring build `D:/DNNE-build-work/muscle-initial-authoring-20261123-v1`
completed with zero warnings/errors; no test or model executed during that
build. Python/PowerShell syntax and declared test inventories are checked
without running tests. Scientific results remain pending until the window
completes and Paul reports its footer.

## Full-body work still required

This helper does not select a whole-body muscle geometry or population. The
same tibialis reference length and one slow unit cannot represent every
existing Avatar muscle. Anatomical attachment/frame mapping, wrapping,
reference-versus-slack tendon conventions and identified motor-unit capacities
remain explicit work in `DNNE_Muscle_Anatomy_Reference_2026-11-23.md`.

No original muscle, calcium, neuronal, learning, transport or native Unity
equation changes in this study. No live body/editor setting changes. The host
can use the result when a separately qualified anatomy and initial body frame
are ready; it is not yet selected in live Avatar. Coupled trajectory accuracy,
physiological calibration, neuronal muscle authority and standing remain
unqualified.

Launched: `D:/DNNE-build-work/muscle-initial-20261123-4c4377`, frozen at
`22f7ed894ff131f9a67b5eea5d7f3030a15e1591`. Preparation/preflight passed, with
both initial 2,252-file copies plus manifest verified. Interactive task
`DNNE-Muscle-Initial-4019dc56c094` started the visible PowerShell window. No
post-launch process, output or status inspected. This is the only pending
report; the earlier saved native/sensory comparison is completed and reviewed.
