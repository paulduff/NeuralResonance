# Avatar migration audit — 12 October 2026

Avatar is the source implementation for the Unity migration. Inspect its code,
tests and DNNE receptor paths before adding a replacement. The operator explicitly
requires this review because the existing Avatar contains substantial prior work.
Unity provides a dynamic physical solver; it does not replace DNNE's authority.

| Existing implementation | Unity status and next qualification |
| --- | --- |
| `AvatarMuscle.cs` activation, force-length/velocity, antagonism, fatigue | Generated scalar plant matches canonical traces. Opt-in 19 physical axes expose 38 measured muscles. Recruitment alternatives are isolated diagnostics only. |
| `AvatarArticulatedBody.cs` 42 muscles and posture coordination | Four axial muscles need physical spine pitch/roll. Initial decoder omits crouch/sit/lie, canonical standing/gait ankle coordination, some basal recruitment and arm/neck coordination. It is not the complete Avatar controller. |
| `AvatarSpinalLocomotorCircuit.cs` | Generated circuit matches canonical load/stop traces; actual Unity foot loads feed it. Full gait/posture decoder parity remains pending. |
| `AvatarBalanceDynamics.cs`, `AvatarColliderRig.cs` mass, support and contact mechanics | Replaced pose/support integration with PhysX masses, free-root gravity and solver contacts. Root anchoring/pose resets exist only in isolated calibration fixtures. COM, COM velocity, support hull and margin are measured. |
| Four plantar pressure fields in `AvatarArticulatedBody.CaptureFrame()` | Previously omitted from Unity. Candidate now classifies real floor contacts into heel/forefoot and medial/lateral fields, conserving measured foot load. Aggregate impulse is distributed uniformly; this is an estimate, not per-point impulse measurement. |
| Mass-to-pressure directional proprioception in `PhysicalBodyTransducerRuntime` | Existing DNNE path retained. Candidate corrects Unity pressure center from first contact to the load-weighted mean of contact manifolds. Support geometry stays outside the vestibular nerve. |
| `VestibularLabyrinthTransducer`, documented in Entry 147 | Existing bilateral canals, cupula/adaptation and otolith organ retained in ControlProgram. Unity supplies physical body motion, relative trunk motion and neck angles. End-to-end delivery/motor correction is not yet qualified. |
| Root versus spinal inclination in `AvatarArticulatedBody.CaptureFrame()` and `HeadlessWorldRuntime.CreatePhysicalBodyFrameCore()` | Migration bug found: Unity sent pelvis tilt as both fall and trunk angles, causing duplicated tilt/derivatives in the organ. Candidate sends chest orientation relative to pelvis as trunk inclination; fall fields retain full root inclination. Offline tests verify a rigid 0.3-rad tilt reaches otoliths once. |
| `AvatarCochlearFrame*`, `AvatarAcousticRenderer` | Hearing is separate from vestibular balance. Unity acoustic/ear input is not migrated. |
| `AvatarBinocularVision`, physical retinal input | Unity stereo eye images admitted during the previous live trial. Full optical parity and confirmed neuronal delivery remain pending. |
| `AvatarNervousSystem`, `AvatarNeuronalMotorBridge`, `AvatarEffectorCatalog` | Unity reads existing signed motor channels and applies fresh authorized output. Retained fields do not establish parity of all peripheral decoding. No host controller selects live posture. |
| Hand withdrawal, grip, slip and fatigue | General contact input exists; dedicated hand plant and local withdrawal/arm coupling remain unmigrated. |
| `AvatarWorldDynamics` energy, hydration and tissue | Generated physiology port and reference respawn state tested. Contact damage/capacity are measured. Temperature/oxygen are references; food/water ecology remains absent. |
| `AvatarInputPressurePolicy/Gate`, retry/backoff and dispatch telemetry | Unity request gating/timeouts exist, but full critical-event pressure policy is not migrated. API admission/generated spikes do not prove deferred neuronal delivery. |
| `AvatarMazeEnvironment`, wall sensing and existing World environment | Unity currently contains a flat qualification arena. Maze, full World ecology and interaction tasks are not claimed as migrated. CIV remains separate. |

## Completed evidence

The 19-axis torque baseline qualified in commit `550a4db`. The canonical measured
muscle port was finite and settled in the four-case comparison
`unity-muscle-comparison-20261012-104831-aeff04`; it held supported upright posture
for 1.07 s at best and then fell. Both final muscle frames passed actual DNNE body
validation and generated additional proprioceptive populations offline.

The six-case comparison `unity-muscle-comparison-20261012-110023-bd21aa` reproduced
all four controls exactly. Full active capacity held only 0.95 s at stand=1, versus
1.07 s canonical. It reduced maximum limit overshoot to 0.001414 rad but tipped in
the opposite direction; stand=0 failed settling (maximum late joint speed 1.342
rad/s). Therefore that candidate is not adopted. Original files and trace analysis
are verified on D:. Greater force alone does not establish balance.

## Completed isolated feedback qualification

The first capability fixture compared three recruitment rules: canonical,
full position recruitment with canonical velocity opposition, and full active
recruitment. Each runs fixed stand=1 plus three measured-sway feedback gain pairs,
giving twelve fresh free-root cases of twenty seconds. Small external pushes of
1.5 N s occur at eight and fourteen seconds. Feedback commands only existing
muscle targets; it never teleports, anchors or supplies a balancing root force.
It stops sway correction when unsupported or substantially fallen. DNNE connection
is forbidden in all isolated qualification modes.

This deliberately tests whether the body can express a standing correction before
we diagnose the live neuronal loop. It is a test fixture, not a proposed live
balance policy and not learned standing. All cases save body/command traces and
final frames. Successful diagnostic control would identify a mechanically useful
command pattern, not prove that DNNE produces it. Failed cases require further
mechanical/decoder review. The corrected vestibular/pressure encoding must also
be checked on captured physical traces before reconnecting the muscle candidate.

Run `unity-balance-capability-20261012-112144-4cdfab` completed all twelve cases;
all were finite, isolated and settled, but none held standing at the end. Full
position capacity with feedback achieved longest standing intervals of 5.61,
8.03 and 9.23 s as gains increased, versus 1.07 s for canonical fixed recruitment.
The best case maintained fore-aft motion but developed sideways drift; coronal
ankle commands saturated while opposing hip muscle forces increased. This supports
testing hip/ankle contribution; it does not establish a sole cause of falling.

All 2,400 captured body frames were tested against the actual input validator and
offline transducer. Eighteen were rejected: seventeen dimensionless `balanceError`
values exceeded one because Unity sent metres, and one hand impact exceeded the
5,000 N body receptor range. The candidate uses Avatar's 0.22 m support-error
normalization component, bounded to [0,1], and saturates only the serialized hand
receptor loads. Raw support margin, contact forces and damage calculations remain
physical. The full canonical balance time/tilt/phase aggregation is not claimed.
Applying these encoding functions offline makes all 2,400 frames valid, with
conserved plantar loads and 38 muscles. This is an explicit projection, not a
fresh player capture or live delivery. Original rejected frames and reports remain
unchanged, with regression fixtures covering both errors. Relative trunk tilt in
all physical traces stayed below 0.000001 rad, verifying root tilt is not repeated.
All run outputs and subsequent analyses are verified on D:.

## Next isolated contribution comparison

Protocol version 2 runs eight thirty-second cases using full position recruitment
and the existing hip abductor/adductor and ankle muscle pairs. Fore-aft feedback
remains at the best tested gains; lateral gains are 6/1.2 and 12/2.4. Hip shares
are 0, 25, 50 and 100 percent of the target correction. Three 1.5 N s pushes occur
at 8/14/22 seconds, forward/right/left. Each fresh root stays free; no pose is reset
within a case, and no controller can connect to DNNE. It records joint/time
attribution for limit violations and saves corrected body frames for review.
This diagnostic distribution is not proposed as a live host controller.

The completed protocol 2 run `unity-balance-capability-20261012-121048-8ee25f`
was finite, isolated and settled in all eight cases, with no final standing hold.
Ankle-only control repeated 9.23 s exactly. At lateral gains 12/2.4, hip shares
25/50/100 percent reduced the longest interval to 4.67/0.72/0.19 s. Lower lateral
gains did not establish a hold either. This particular hip distribution is not
adopted. Joint-limit excursions occurred during loss of support; the report records
their joint, axis and time. The result does not prove hips cannot assist balance.

All 2,400 fresh captured frames passed the actual validator and offline body
transducer without projection: conserved plantar loads, 38 muscles, zero rejected
frames, relative trunk tilt below 0.000001 rad. Live neuronal delivery is still
unassessed. Original traces and analyses remain backed up on D:.

## Next isolated response measurement

`--response-qualify` measures small command effects before selecting another
feedback sign or gain. Ten fresh free-root cases last four seconds each, with the
same full-position muscle policy and ankle feedback as the 9.23 s control.
Controls bracket eight 0.03-rad target pulses: positive/negative bilateral hips,
bilateral ankles, left hip alone and right hip alone. Pulses last 0.5 s from 1 s;
there are no external pushes. Bilateral pulses use the mirrored coordinates from
`AvatarColliderRig.AddLeg`; signed drives convert radians through each original
muscle profile's asymmetric range. The fixture records COM, support center,
velocity, commands, measured joint state, muscles, contacts and actuator torque
at 50 Hz. Those actuator forces exclude passive drive/constraint contributions.
Compare each response with the repeated controls, including pre-pulse equality,
before interpreting direction or selecting a correction. Anchored individual
joint tests do not establish this supported whole-body response.

The software checkpoint retains canonical recruitment and opt-in muscle mode;
all noncanonical recruitment/distribution is isolated diagnostic work. Standing,
walking and live neuronal balance remain unqualified. The next physical results
must be reviewed before adopting a recruitment change.
