# Unity world physical prototype

This is the first Unity physical plant for DNNE. It uses Unity 6000.3.19f1,
a dynamic 15-link, approximately 73.4 kg humanoid and native articulation joints.
Gravity, joint constraints and floor contacts participate in one PhysX solve.
The world starts paused. Its Play button advances physics; it does not start a
walking animation or promise learned standing/walking.

DNNE remains the brain and action authority. Avatar supplies the body and
peripheral spinal machinery. Unity supplies the environment and physical solver.
Dyad and Entity remain the interaction route. CIV is independent.

## Build and run

Unity 6000.3.19f1 with Windows Mono build support must be installed. The operator's
`rigged-t-pose-human-male-w-50-face-blendshapes.zip` is expected beside this repo.
The FBX and texture remain local, excluded from Git. Their exact hashes and
attribution are in `Assets/Avatar/provenance.json`.

```powershell
.\tools\prepare-unity-avatar.ps1
.\tools\build-unity-world.ps1 -Output C:\path\to\local\build
& 'C:\path\to\local\build\DNNE Unity World.exe'
```

Open this directory as a Unity project to inspect/edit the scene. The saved scene
is `Assets/Scenes/QualificationWorld.unity`. Runtime code constructs the physical
body from the validated humanoid skeleton. The Animator is disabled, and skin
bones follow the measured physical links. No Animator or navigation agent moves
the physical root. Initial placement and offline test-fixture reset are explicit
exceptions, not production locomotion.

## DNNE connection

The default mode is an isolated mechanics preview. **Connect DNNE** acquires an
exclusive physical-world lease through the Blazor editor on localhost:5090.
That pauses legacy WorldSim and blocks its Resume/Reset while the lease is held.
A 10-second drain allows already-running sensory requests to finish. Unity renews
the lease every two seconds. It pauses if the last acknowledgment is over seven
seconds old or motor ticks stop advancing for 1.25 seconds. Lease expiration or
Disconnect leaves legacy WorldSim paused until the operator explicitly resumes it.
The updated Blazor editor must be running for this handoff to work.

Only fresh `/api/v1/neuronal-motor` output, with active neuronal authority and
sufficient output confidence, supplies active joint torques. Goals, action labels
and semantic directives are not read. The existing four-population peripheral
spinal circuit is ported mechanically from `NRE.SimAvatar`; a cross-implementation
trace test checks agreement through recruitment, loading and stopping.
Run `python tools/prepare-unity-spinal.py --check` from the repository root to
check the generated port; omit `--check` to regenerate after canonical changes.
Left/right recruitment retains independent direction and strength; opposing
commands are not averaged into a shared hip direction.
The two-axis joints expose three native slots in Unity 6000.3.19f1, but pack their
movable coordinates into slots 0 and 1. The head's physical axes are X/Y, the
limbs' X/Z; slot 2 is not a Z-axis command. Torque, position, velocity and fixture
reset use the same packed mapping, with the unused slot zero. An independent
two-link boundary probe checks these slots against physical quaternion rotations
and named Y/Z drives at build time and during qualification.

Physics advances at 100 Hz. Joint angles, contact impulses and velocities are
physical measurements. Body frames and somatic contacts use existing ControlProgram
receptor APIs. Two physical eye cameras send 64 x 40 RGB24 retinal frames with the
correct top-row ordering. Accepted counters acknowledge API acceptance, not actual
neuronal delivery or comprehension. Paused worlds do not emit sensory frames.
Trunk pitch/roll encode principal gravity inclination within the existing wire
range. Balance retains full fallen orientation. COM, pressure and support geometry
use a gravity-aligned horizontal frame; body/contact receptors retain anatomical
coordinates. Captured fall/contact examples in `Qualification` are tested against
the actual DNNE input validators without injecting test measurements into the brain.

## Qualification

```powershell
.\tools\start-unity-qualification-window.ps1 -Player 'C:\path\to\local\build\DNNE Unity World.exe'
```

The independent visible PowerShell window performs isolated gravity, contact,
settling, joint response/limits and authority-gate checks. The expanded candidate
also pulses all 19 movable axes in both directions. Each anchored, airborne,
gravity-disabled fixture begins at the midpoint of each configured joint range;
it requires signed movement over 0.01 rad in both reduced coordinates and measured
joint-frame rotation, and limit violation below 0.05 rad. The separate native
boundary probe must pass all 40 force, position, velocity and drive samples.
These fixture resets/poses never run in production. It writes the report,
example wire frames, logs and completion status into a new artifact directory,
and verifies their D: backups. It neither connects to DNNE nor trains it.
`FINISHED` means these mechanical criteria passed; `walkingAssessed` remains false.
No agent needs to watch the run. Report a completion/failure when further review
is wanted.

The first prototype passed basic gravity, contact and elbow checks but the
operator observed shaking. Finite coordinates and floor containment did not
exclude oscillation. Passive spring/damper feedback now runs inside Unity's
joint solver, rather than explicit velocity-opposition torque. A 10 ms step and
final-two-second joint/linear-speed checks qualify passive settling. Build v3
passed on 12 October: maximum settled linear speed 0.00316 m/s, joint speed
0.01211 rad/s and minimum collider height -1.15 mm. The operator also confirmed
that the visible fall looked normal. This validates this passive fixture, not all
poses, active control or learned locomotion.

Expanded qualification `unity-qualification-20261012-091859-6cb04a` also passed
all 19 axes in both directions and all 40 native boundary samples. Passive settling
was unchanged; maximum joint limit violation was 0.009744 rad. The captured
reports and sensory frames are retained in `Qualification`. This establishes
the tested mechanical baseline; standing/weight transfer under DNNE remains next.

## Bounded DNNE standing observation

```powershell
.\tools\start-unity-standing-window.ps1 -Player 'C:\path\to\local\build\DNNE Unity World.exe' -DurationSec 30
```

This separate visible PowerShell window rechecks the mechanical body and DNNE
health, saves coordinator/timing exports, then opens Unity paused and connects
the exclusive world lease. After the ten-second drain, press **Play in Unity**.
The trial advances 30 simulation seconds under ordinary fresh neuronal output,
with no scripted balance/gait commands, then pauses/disconnects and keeps the final
view open. Progress appears in PowerShell; JSONL traces and separate integration/
standing outcomes are saved and backed up to D:. No agent monitoring is required.
FINISHED means completed integration; the measured standing criterion is printed
separately. Neither it nor API input acceptance proves learned standing or neuronal
delivery. See the [observation protocol](../docs/Unity_Standing_Observation_2026-10-12.md).

## Current limits

The opt-in `--muscles` candidate ports Avatar activation, antagonist force,
force-length/velocity and fatigue for 19 supported axes and reports 38 named
muscles from measured joint state. The four extra axial muscles still need
physical spine pitch/roll axes. The direct-torque baseline remains the default.
Run `tools/start-unity-qualification-window.ps1 -Assessment muscles -Player 'C:\path\to\build\DNNE Unity World.exe'`
for an isolated raw/canonical-muscle/full-active-muscle comparison with zero/full
fixed standing recruitment. The alternative preserves each agonist's active
capacity while balancing resting tone; it is a diagnostic candidate, not the
default. The first four-case comparison was finite and settled but held standing
for only 1.07 s at best. Full active capacity subsequently held only 0.95 s and
failed zero-command settling; that rule is not adopted. Captured muscle frames
pass offline receptor validation.
See the [candidate protocol](../docs/Unity_Muscle_Migration_2026-10-12.md).

Always review the existing Avatar implementation before adding or replacing its
features. The [migration audit](../docs/Unity_Avatar_Migration_Audit_2026-10-12.md)
maps the preserved, partial and missing components. The sensor candidate separates
relative trunk motion from whole-body tilt, restoring the intended input to the
existing vestibular labyrinth; it also restores four measured plantar pressure
fields and estimates pressure center over each contact manifold.

The first `-Assessment balance` run tested twelve isolated mechanical cases and
held supported upright posture for 9.23 s at best; none held at the end. The current
protocol uses `--balance-qualify` for eight thirty-second hip/ankle contribution
cases. An explicit diagnostic feedback controller varies existing muscle
targets from measured sway; it cannot connect to DNNE, anchor the root or write
poses. Reports separate mechanical holding from neuronal/learned standing, which
remain unassessed. Protocol 6 compares 25 small sagittal/coronal hip posture offsets
with normal fatigue, each for 120 seconds. Targets ramp through existing muscles;
standing duration, effort proxies and fatigue are recorded. Feedback, nominal muscle
forces, passive drives and mechanics remain fixed. A diagnostic hold
requires ten continuous final standing seconds, finite settled mechanics and joint
limit excursions below 0.05 rad. Each case has small pushes at 8/14/22 s. The inherited
fatigue equation remains the default, and live steps reject diagnostic posture,
fatigue and recruitment overrides. The prior fatigue ablation held 120 seconds
only when all muscle fatigue was suppressed; suppression is not adopted.
Body encoding
normalizes support-loss error with Avatar's 0.22 m scale and bounds hand receptor
loads to 5,000 N; raw contact forces and support distances remain available.

For command-response identification, use the qualification launcher with
`-Assessment response`. Ten isolated four-second cases bracket positive/negative
0.03-rad hip and ankle target pulses with repeated controls. Traces record free-body
COM motion, contacts, muscle state and actuator torques at 50 Hz. This measures
the effect of existing Avatar commands; it does not qualify standing or learning.
Balance/response windows run the tracked offline body validator automatically
with the installed .NET 10 SDK and save its report before verifying the D: backup.
Offline receptor validation does not establish live neuronal delivery.

This is a physical migration prototype, not the whole former ecology. It has a
flat contact arena, not food, hydration interactions, predators or terrain tasks.
The default plant uses bounded joint torque approximations; its fiber dynamics, fatigue
and grip are not yet migrated. Energy, hydration and tissue now follow the
canonical Avatar simulation, including metabolic stress, contact damage and
capacity reduction. Initial reserves match its respawn state: 6 MJ energy,
hydration 0.75 and intact tissue. Pausing freezes physiology with physical time.
Temperature and oxygen remain reference values. No food, water or shelter is
present in this arena, and biological calibration of the inherited rates remains
outstanding. Contact areas are coarse collider estimates.
Impact momentum is estimated only on contact onset with pre-solve approach speed
over 0.2 m/s, after removing one whole-body gravity impulse from the solver impulse.
This conservative estimate distinguishes static support from an impact; complex
manifolds, moving surfaces and repeated impacts still require physical calibration.
Run `python tools/prepare-unity-physiology.py --check` to verify the generated
equations and inherited world settings. Omit `--check` to regenerate.
Center of pressure estimates distribute solver impulse over the contact manifold.
Adjacent/body self-collisions are currently disabled pending collider calibration.
No gait animation, fixed production root, kinematic movement or host balancing
policy substitutes for neuronal control. Calibration fixtures may anchor the root
only during their explicitly isolated actuator tests.

Before a DNNE motor-learning run, qualify passive settling and actuator response,
then calibrate masses/limits/friction, migrate physiological consequences, verify
sensor reception and assess standing/weight transfer under fresh neuronal output.
Do not use a completed mechanical test as evidence of learned walking.

## Model credit

**Rigged T-Pose Human Male w 50 Face Blendshapes**, by Mike Alger.
[Source](https://sketchfab.com/3d-models/rigged-t-pose-human-male-w-50-face-blendshapes-cc7e4596bcd145208a6992c757854c07),
[CC BY 4.0](https://creativecommons.org/licenses/by/4.0/).
The source credits scanning to Doob and optimization/rigging to High Fidelity.
Changes: uniform height calibration, relaxed initial shoulder pose, disabled
animation and a physics-driven presentation. Original rig and blendshapes remain.
