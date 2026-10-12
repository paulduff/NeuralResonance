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

## Isolated contribution comparison

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

## Isolated response measurement

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

Run `unity-balance-response-20261012-123549-b738d1` completed ten finite isolated
cases. Both control traces and all pre-pulse physical states matched exactly.
At 1.31 s, positive/negative mirrored hip pulses changed lateral COM-to-foot
error by -0.0355/+0.0396 mm against the control. Corresponding ankle pulses changed
it by -0.0458/+0.0840 mm. Actuator torques and individual hip responses confirmed
that commands reach the physical plant. Effects are small and include the retained
ankle feedback; these are not open-loop transfer functions or standing qualification.
Every case stayed upright for the short four-second observation. All 2,000 body
frames passed real offline validation/transduction without projection. Live delivery
remains unassessed. Analysis and captured outputs are verified on D:.

## Hip reference versus ankle correction

Protocol 3 separates two changes previously coupled in the hip-share test:
reducing ankle correction and moving the hip target with measured hip drift.
The original `AvatarArticulatedBody` retains a signed hip target referenced to
neutral, plus excursion-dependent stabilization tone. The current Unity decoder
does not yet reproduce the complete canonical posture/tone coordination.
Seven thirty-second isolated cases compare the ankle-only control, the previous
25-percent split, relative versus neutral-referenced hip targets, and reduced
versus full ankle correction. Additional neutral-referenced hip gains are 10 and
50 percent, with full ankle correction. Muscle policy, gains, free-root mechanics
and prescribed 8/14/22 s pushes remain fixed. A software plant test demonstrates
that a neutral hip target retains restoring muscle torque at zero sway error;
the previous measured-relative target releases that active posture reference.
These diagnostic alternatives are not adopted as a live DNNE controller.

Run `unity-balance-capability-20261012-124422-fbc4f7` completed all seven cases
finitely and in isolation; every case settled after falling, and none held final
standing. The ankle-only control again held 9.23 s. At 25-percent hip gain,
neutral-referenced targets improved 4.67/4.94 s (reduced/full ankle correction)
to 6.36/6.47 s, but did not beat ankle-only control. Neutral hip gains of 10 and
50 percent held 8.46 and 1.19 s. Hip assistance is not adopted. All 2,100 fresh
body frames passed the actual validator and offline transducer without projection,
with conserved plantar loads and 38 muscles. Outputs and analyses are verified on D:.

## Ankle reference, correction strength and damping

Protocol 4 compares sixteen ankle-only cases, each sixty seconds: coronal targets
referenced to measured pose or neutral, position gains 6/12/24/48, and velocity
gains 2.4/9.6. The original Avatar's coronal ankle target is a signed neutral-based
target; this fixture compares that reference while keeping the current muscle
policy and fore-aft feedback fixed at 12/2.4. Hip commands remain neutral. Existing
joint limits, physical force capacities, gravity, contacts and the 8/14/22 s pushes
are unchanged. A reported diagnostic hold requires ten continuous final seconds
of supported upright posture, less than 0.05 rad maximum limit excursion and a
finite, settled body. Even a successful case remains an isolated command pattern,
not neuronal or learned standing.

The PowerShell window now checks all captured frames automatically with the
tracked `tools/UnityBodyValidation` console, using the actual internal validator
and offline receptors. This requires the already-installed .NET 10 SDK. Reports
remain separate from live neuronal delivery, and are included in the D: run backup.
The command-response analyser is tracked in `tools/analyse-unity-response.py`.

Run `unity-balance-capability-20261012-125543-0f0299` completed all sixteen finite,
isolated cases. None maintained final standing. The best measured-target case at
lateral gains 48/2.4 held 10.51 s; the neutral-target counterpart held 10.50 s.
The 12/2.4 measured-target control reproduced 9.23 s. Higher gains gave diminishing
improvement, and increased damping did not establish a hold. All 9,600 captured
frames passed the automatic actual validator/offline transducer, with conserved
plantar loads, 38 muscles and no projected corrections. This is mechanical fixture
evidence; neuronal control, learning and live sensory delivery remain unassessed.

In the best trace, both lateral ankle commands saturate. Left TibialisPosterior
activation reaches one; its measured force declines from roughly 788 N at 8 s to
707 N at 10 s as inherited fatigue rises from 0.122 to 0.162. Length and velocity
also affect force, so this association does not establish fatigue as the sole cause.
Further gain tuning is not the next diagnostic step.

## Isolated muscle-fatigue ablation

Protocol 5 uses three otherwise-matched 120-second cases: normal muscle fatigue,
fatigue disabled in ankle muscles only, and fatigue disabled in all muscles.
The best tested feedback, full-position recruitment, nominal forces/moment arms,
passive drives, joint limits, gravity, contacts, physiology and 8/14/22 s pushes
are fixed. Energy/hydration/contact damage still evolve. The ablation changes only
the muscle fatigue fraction before computing force; activation, length/velocity
laws and resting-force recruitment are unchanged. Actual captured muscle force
and fatigue reflect each diagnostic policy. A whole-body comparison distinguishes
ankle fatigue from a wider plant limitation.

The generated Unity port keeps the inherited fatigue equation as its default;
canonical Avatar source/rates are unchanged. A live Unity step explicitly rejects
any non-normal diagnostic fatigue policy. These cases cannot connect to DNNE, and
no ablation is exposed as a live command-line option. Software checks verify
canonical default equivalence, unchanged activation/length/velocity, zero force
after relaxation, and preservation of other muscles under ankle-only suppression.
Disabling fatigue is a causal test, not an adopted treatment or biological claim.

Run `unity-balance-capability-20261012-131529-2df26d` completed all three cases
finitely and in isolation. Normal fatigue reproduced 10.51 s. Suppressing ankle
fatigue extended supported standing to 57.93 s before falling. Suppressing all
muscle fatigue held standing throughout the 120 s trial (119.94 s after initial
floor contact), including all three pushes; maximum limit excursion was 0.000171
rad and settling passed. All 3,600 captured frames passed automatic actual
validation/offline transduction. Outputs and analysis are verified on D:.

This establishes a causal fatigue contribution in the tested command/plant
configuration. It does not prove the existing fatigue rates are biologically wrong.
In the ankle-only ablation, left Quadriceps fatigue rose from 0.044 at 10 s to
0.634 at 57 s; left knee flexion increased from 0.187 to 0.530 rad and quadriceps
force fell from 1,113 to 346 N. The all-muscle ablation still used full left lateral
ankle activation near the end. The body can express a standing correction, but
this operating posture demands substantial sustained effort. Suppression is not
adopted; neuronal/learned standing and live sensory delivery remain unassessed.

## Posture alignment with normal fatigue

Protocol 6 tests 25 combinations of small sagittal hip base offsets
(-0.08/-0.04/0/0.04/0.08 rad) and mirrored coronal hip targets
(-0.12/-0.06/0/0.06/0.12 rad). All use normal fatigue, fixed feedback/force
capacities/joint limits, 120 s duration and the same three pushes. The zero-offset
baseline runs first. Targets ramp smoothly from 0.5 to 2.5 s and are released if
support is lost; poses and roots are never written during a case.

`AvatarArticulatedBody.ResolvePosture` already separates a hip posture base from
gait; its standing reference is hip=0/knee=0.06/ankle=0. The diagnostic sagittal
offset uses that existing mechanism in the measured muscle plant without creating
a locomotor command. Coronal offsets use existing signed hip channels. Applied
offsets are recorded explicitly in each trace. All remain isolated, and the shared
plant's live-policy check rejects posture offsets, fatigue ablations and the
noncanonical recruitment alternatives. Ordinary Avatar source/rates are unchanged.

Reports measure mean squared activation across 38 muscles during supported
standing, peak activation and peak fatigue during those same observations. These
are effort proxies, not calibrated metabolic energy. Stability is assessed first;
effort among surviving stances can then guide the next mechanical improvement.
Incomplete falling cases do not establish a low-effort standing solution. The
PowerShell player allowance is twenty minutes for this larger isolated batch,
and automatic wire validation/D: backup still follow process completion.

The software checkpoint retains canonical recruitment and opt-in muscle mode;
all noncanonical recruitment/distribution is isolated diagnostic work. Standing,
walking and live neuronal balance remain unqualified. The next physical results
must be reviewed before adopting a recruitment change.
