# Actual head coordinates for the isolated vestibular path

## Existing Avatar path inspected

Unity `BodyFrame.Capture` supplies pelvis-local linear/angular velocities, joint
angles and reduced balance angles. `VestibularLabyrinthTransducer` combines those
with joint-angle derivatives and differentiates the supplied velocity components.
That is a reduced approximation. Components in a rotating pelvis frame can change
when world velocity does not; their simple difference is not by itself inertial
acceleration. Adding Euler-angle derivatives is also not a general composition
of finite head rotations. This source review is not a measured live error budget.

The Avatar already has an actual head `ArticulationBody`. The new read-only
`SkullMotionCapture` helper reads that body, an explicitly located material point,
its world basis, point velocity, angular velocity and current gravity. Redundant
center-of-mass pose/velocity is retained for an independent native audit. It does
not step the world or send neural input. Existing body/contact schemas, saves,
live sensory selection and editor streams remain unchanged.

Unity documents [angular velocity](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/ArticulationBody-angularVelocity.html)
and [linear velocity](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/ArticulationBody-linearVelocity.html)
in world coordinates. Its [point-velocity query](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/ArticulationBody.GetPointVelocity.html)
includes angular motion. Native execution must still check those semantics for
our actual articulated head, point placement and physical-owner timing.

## Adapter and initial availability

`AvatarSkullKinematics` owns one head/point/clock on one thread. It retains original
serialized observations and both endpoint hashes used by each derivative. A fixed
local point must match its world location under the supplied right/up/forward
basis. Basis norm, orthogonality and orientation are checked with 1e-5 coordinate
tolerance; point geometry uses 1e-5 m absolute component tolerance. These allow
declared float native measurements without silently renormalizing them. They are
prospective engineering bounds, not anatomical accuracy claims.

The first observation provides no acceleration. It cannot initialize the otolith
mechanism by pretending that acceleration is zero. After the next actual sample,
the adapter computes

```text
a_average_world = (point_velocity_now - point_velocity_previous) / physical_dt
gravito_inertial_world = gravity_world - a_average_world
skull_component = dot(world_vector, measured_skull_axis)
```

The stored vertical gravito-inertial component is positive downward; angular
components retain the existing pitch/X, yaw/Y, roll/Z convention. Acceleration is
an interval-average estimate available only at its final endpoint. Subsequent
mechanics hold this available estimate forward. This introduces sampling and
availability approximations; it is not reconstructed instantaneous acceleration,
an exact collision impulse, or a full continuous labyrinth trajectory.

The mechanics constructor now optionally accepts the first observation step.
For this adapter that is one step after the physical initial sample. It preserves
the original clock and sample identities, including nonbinary epoch arithmetic;
it does not rebase epochs or invent an elapsed interval. Later-start initialization
has a separate version suffix/fingerprint. Default initialization and transition
equations remain unchanged and must reproduce their completed original records.

Gaps, foreign head/point/session, malformed geometry, changed retries and reversed
steps are rejected before mutation. Exact last retries return the original
receipt. Arithmetic failure preserves the previous completed endpoint and faults
the epoch. No reset, clipping, neutral filling, generated neural spikes or motor
commands are provided by this adapter.

## Visible component and regression check

Twelve new cases cover initial unavailability, gravity, free fall, tilted skulls,
turning with constant world velocity, off-centre rotational acceleration, delayed
mechanics on the original clock, endpoint causality, retries/gaps/reversal,
identity/geometry validation, arithmetic-fault preservation and thread ownership.
Together with the previous 62 cases, the visible rig requires exactly 74 cases.

It also rebuilds the original C# vestibular probe and requires byte-identical
reproduction of all 61 saved native-output files: forty trajectories plus twenty
case results and their aggregate. The twenty prescribed histories and default
candidate remain the same. This compares with reported, independently reviewed
evidence rather than rerunning the previously completed ODE references. The new
head-coordinate cases are component checks, not native Unity or independent
continuous-motion qualification.

The .NET authoring build has zero warnings/errors; syntax and static inventory
checks pass. No new tests or models have run inline. The Unity capture helper
is authored but has not been built or executed in Unity. Freeze clean pushed
source, preserve the baseline and verify D: before a visible launch.

## Required next integration

After the component outcome, attach the helper to an isolated physical owner and
capture the actual Avatar head and declared sensor points at every native step.
Preserve native poses, world velocities, point-velocity cross-checks, initial
unavailability and every original payload. Include static/tilted control,
translation/free fall and head rotation, then compare physical-step refinements.
Qualify the body-to-skull transform and sampling before coupling any receptor.
Hair-cell currents, release, vestibular afferent firing, neuronal balance control
and fall recovery remain separate unfinished requirements. The point locations
and inherited reduced mechanical constants are not calibrated human anatomy.

## Launch record

Launched as `skull-kinematics-20261117-f740bd`, frozen at
`5aa5f28c2fc0a2252c64a02defa098902a141e26`, visible task
`DNNE-Skull-Kinematics-92195589d752`. All 1,826 initial files plus manifest are
verified on D:. The 74-case source inventory, Python/PowerShell syntax and both
.NET builds passed without inline model/test execution. No post-launch output,
status or process has been inspected. This run and rubral-ten `0419e8` await
separate operator completion reports; neither is live-monitored.

## Completed component review and native sampling candidate

The operator reported `f740bd` complete. All 2,430 C:/D: files, frozen Git,
binaries and managed runtime reconcile. All 74 exact test cases passed and all
61 original default vestibular output files are byte-identical. The saved audit
reproduces without executing a model or test. The separate review is
`artifacts/skull-kinematics-review-20261117/review.json`, verified on D:.

The next native sampling candidate extends only the isolated physical-sample
probe. `--skull-motion-capture material-points-v1` makes Unity retain a separate
head observation file before each normal bridge reply. The body/contact wire
schema is unchanged. The actual Avatar head supplies its origin, basis,
quaternion, centre of mass, world linear/angular velocities and `GetPointVelocity`
at three material points: centre and local X = +/-0.0625 m. These binary-exact
offsets exercise rigid-body kinematics; they are not calibrated labyrinth anatomy.

The existing falling-body experiment retains pause, backpressure, owner rejection
and post-movement capture-failure checks. There are 201 delivered endpoints
(0..200), an additional preserved failure endpoint 201, four held inspections
and a stop: 207 native sidecars. Three adapters preserve 603 endpoint receipts,
with no acceleration assigned at step zero. Every later derivative names both
original endpoints. No vestibular mechanics, afferent membrane or DNNE executes.

An independent Python audit checks every sidecar and adapter receipt against:

- Quaternion-derived head axes and material-point world location.
- `v_point = v_COM + omega cross (point - COM)` using redundant native data.
- Unchanged head measurements during all held inspections and stop.
- Native/typed hashes, original sample identity and both clock epochs.
- World velocity differences, `g-a`, and projection into the current head axes.

Prospective bounds are in `skull-protocol.json`: 1e-5 basis and position bounds;
point velocity 1e-5 m/s plus 1e-6 relative; derived arithmetic 1e-8 plus 1e-10
relative. Motion must exercise angular velocity above .01 rad/s and point-speed
difference above .0001 m/s. Missing motion fails coverage; bounds are not changed
after observing the run. These checks concern sampled coordinates and ownership,
not independent continuous physics accuracy. Prescribed tilt/free-fall/rotation
controls, step refinement and sensor anatomy remain later qualification work.

The new probe needs only CoreCLR; the previous motor probe also loaded ASP.NET.
The gate requires exactly the reviewed CoreCLR directory and identical host,
runtime options and hashes, while also rechecking all original ASP.NET files used
by the test project. Before/after runtime inventories of the new probe must
match exactly. No existing dependency manifest is normalized or overwritten.

Preparation requires the reviewed component gate and unchanged adapter source.
The visible-window runner repeats the 74-case suite, builds an isolated Unity
player, executes the native study, checks the saved observations and backs up the
result. Authoring compilation and syntax checks are separate from execution;
native sampling remains unqualified until the operator reports its outcome.

The native study is launched as `unity-skull-20261117-733e7c`, revision
`8734689b82aee169dd346911e3bb354b0e8dcda8`, visible task
`DNNE-Unity-Skull-c31b288fda04`. All 1,770 initial files plus manifest are verified
on D:. The .NET authoring build has zero warnings/errors; static inventory and
Python/PowerShell syntax passed. No new test, physics or model ran inline.
No post-launch status, output or process was inspected. This is the only pending
operator report; both earlier skull and ten-input captures are reviewed.

## Native scale-guard failure and corrected capture

The operator reported `733e7c` failed. The separate
`artifacts/native-skull-failure-review-20261117/review.json` verifies all 3,403
C:/D: files, frozen Git source, built Unity scripts/player, 74 focused tests and
unchanged managed files. Unity compiled and executed 178 physical steps. There
are 178 acknowledged endpoints (0..177), 534 typed point receipts and three held
inspections. The next capture failed after physical step 178, before reply 181;
the command was not retried. No completed sampling qualification is claimed.

The failing guard required `(lossyScale - one).sqrMagnitude <= 1e-12`. The largest
retained pre-failure squared deviation was 8.810729923425242e-13 at step 153.
Retained point-velocity, position and quaternion-axis discrepancies were at most
2.5193605157625143e-7 m/s, 1.6452348150508556e-7 m and
7.02933746765666e-8 respectively, inside the original coordinate/velocity bounds.
The rejected step's scale and ancestor scales were not retained, so its exact
value and cause cannot be reconstructed from this capture.

Unity's [lossyScale documentation](https://docs.unity3d.com/kr/6000.0/ScriptReference/Transform-lossyScale.html)
describes a world-scale convenience representation; configured scale is local to
the parent. The Avatar physical hierarchy is created independently of its scaled
visual mesh, with head/chest/pelvis GameObjects initially at unit local scale.
The small retained deviations suggest float transform composition, but do not
prove the missing endpoint's cause. The corrected capture tests the intended
precondition directly: every physical ancestor must retain exactly unit local
scale. It records the hierarchy, computed world scale and whether the old guard
would reject, without changing or normalizing the physical measurements.

This is an explicit scale-acceptance policy change in snapshot/study v2, not a
claim that the failed v1 condition passed. Quaternion, point-coordinate, point-
velocity and derived arithmetic bounds remain unchanged. An actual hierarchy
rejection now includes its raw snapshot in the saved failure. The retry must
show all configured scales, including at the previously missing endpoint.

Thirty-three native transform controls precede capture: fifteen rotated unit
hierarchies and six non-unit scales at each of three levels, including one-float-
step deviations, reflection, zero and nonuniform scale. All eighteen invalid
hierarchies must reject. These temporary transforms have no physics components
and do not change Avatar. The original 74 focused tests and full 201-step study
then run normally. The reviewed failure and unchanged physical sources are new
preparation prerequisites. No test or native control has executed during authoring.
