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
