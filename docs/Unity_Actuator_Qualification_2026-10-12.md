# Expanded Unity body qualification — 12 October 2026

The passive v3 body qualified, but only its left elbow actuator had been exercised.
The next candidate adds positive/negative pulses for all 19 movable reduced-space
axes: trunk yaw, head pitch/yaw, bilateral hip flexion/abduction, knee, ankle
flexion/roll, shoulder flexion/abduction and elbow.

Each fixture anchors the pelvis above the floor, disables gravity and resets all
joint positions and spring targets to their range midpoints. A 35% torque-capacity
pulse lasts 0.25 seconds. Acceptance requires movement over 0.01 rad in the correct
reduced-coordinate direction and physical joint-frame rotation, finite position/orientation/velocity/joint state,
and no joint limit violation exceeding 0.05 rad. These tests establish physical
response in an isolated fixture; they do not validate anatomical calibration,
whole-body standing, fatigue or learned walking. Production remains unanchored
and starts paused.

The motor adapter previously averaged the magnitudes and signs of the two leg
commands, allowing equal opposing commands to cancel both hips. It now retains
each leg's direction and recruitment while sharing the canonical spinal circuit.
A stopped leg receives no residual active sagittal torque. Physiological capacity
scales active effort and neuronally recruited postural stiffness, and cannot
choose a direction or activate a resting body. Passive joint elasticity remains.

Energy, hydration, tissue stress and vital capacity are generated from the existing
AvatarWorldDynamics equations and WorldSim options. They now advance with Unity
physical time, and are measured in body frames and displayed. Respawn values are
6 MJ energy, hydration 0.75 and intact tissue. Oxygen and core temperature remain
reference values. The inherited simulation burn/damage rates are not biologically
calibrated. Food, water, shelter, muscle fiber fatigue and grip remain outstanding.

The contact adapter caches pre-solve point velocity. Only a new contact with closing
speed over 0.2 m/s can contribute estimated impact momentum; one whole-body gravity
impulse is subtracted from the solver impulse. Canonical damage counts the first
sample only and treats prolonged pressure separately. This is a conservative
simulation estimate, not a calibrated impact model. Multi-contact injury and
moving surfaces need further qualification.

38 targeted .NET tests pass, covering native coordinate layout and captured engine
rotations, the new motor mapping/gates, physiological
capacity, a 4,000-step canonical-versus-port trace through metabolism/pressure/
sleep/recovery, support-versus-impact injury, existing Avatar physiology, spinal
compatibility and captured sensory-frame validation. The expanded physical
qualification passed in independent PowerShell run
`unity-qualification-20261012-091859-6cb04a` with player exit 0:

- All 19 axes moved in both commanded directions, in native readings and physical rotation.
- All 40 independent native-boundary samples passed.
- Largest joint limit violation: 0.009744 rad, below the 0.05 rad acceptance bound.
- Smallest positive physical rotation: 0.219425 rad; least negative: -0.219426 rad.
- Passive settling retained 0.003161 m/s maximum linear speed and 0.012108 rad/s maximum joint speed.
- Finite state, gravity/contact, elbow limits, authority gate and isolation passed.
- `walkingAssessed` remains false. DNNE was not connected or trained.

The captured body/contact examples and the full expanded qualification report
are retained under `UnityWorld/Qualification`. Run files have a hash-verified
backup under `D:\DNNE-desktop-runs\UnityWorld\unity-qualification-20261012-091859-6cb04a`.

The first expanded run, `unity-qualification-20261012-085114-f077f7`, completed the
passive test and trunk pulse but aborted at the head's native coordinate count.
The adapter had confused two intended movable axes with two native slots. Unity's
[reduced-space constructor documentation](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/ArticulationReducedSpace-ctor.html)
specifies three slots for a spherical joint with more than one unlocked axis.
An initial interpretation treated them as fixed X/Y/Z slots. The subsequent
`unity-qualification-20261012-090030-1746ad` completed all 19 axes but passed only
13: both shoulders, hips and ankles failed their second-axis pulses. The head
passed both axes. That correction was incomplete and is superseded below.

An independent two-link fixture on the installed Unity 6000.3.19f1 directly pulsed
each native slot and wrote positions and velocities, in both directions, for X/Y
and X/Z lock configurations. Named Y/Z drive targets provided a separate control.
Physical quaternion rotations confirmed that BOTH configurations pack their
movable coordinates into slots 0 and 1; slot 2 does not actuate either joint.
The corrected motor, proprioception and reset adapter therefore packs
`[first, second, 0]` for both configurations, retaining three-slot storage and
distinguishing the second physical axis (Y/Z) from its native index (1).
Unexpected counts still fail explicitly. The unused-slot reading is diagnostic;
it is not a measurement of anatomical locked-axis rotation.

The boundary fixture is retained as a build/qualification prerequisite: 40 samples
check physical response, signed rotation, packed readings and inactive-slot
behavior for force, position, velocity and named drives. A captured engine report
under `UnityWorld/Qualification` grounds the adapter regression tests in observed
physical rotations rather than another copy of the adapter's assumption. Each of
the 19 avatar axes must additionally produce signed physical joint-frame rotation.
No movement threshold or limit criterion was relaxed.

The qualification connects to neither DNNE nor a live learning run. Its report
contains per-axis displacement/limits and the passive body's physiological state.
`completed` and `qualified` remain separate outcomes; all 19 axes must pass before
the next candidate can qualify. Standing is the subsequent feedback-loop experiment.
