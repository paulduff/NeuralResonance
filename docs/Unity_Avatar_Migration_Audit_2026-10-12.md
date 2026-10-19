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
| `AvatarBalanceDynamics.ResolveBalanceError` | Generated canonical margin/tilt/phase encoding now preserves a fallen error of one even inside a floor-contact support hull. Unity still has its simpler phase classifier; instability time and control allowance are zero. This is not full balance-dynamics parity. |
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

Run `unity-balance-capability-20261012-134335-eff5b1` completed all 25 finite,
isolated cases. None held final standing. The best offset (sagittal +0.04 rad,
coronal -0.06 rad) extended standing to 17.41 s, versus the reproduced 10.51 s
zero-offset control. Every case reached maximum activation one while standing.
All 30,000 captured frames passed actual automatic validation/offline transduction.
Outputs and analysis are verified on D:. Modest static posture tuning did not
produce a sustainable stance, and no offset is adopted.

## Force reserve under physical limb loading

The original `AvatarMuscleJoint.Advance` integrates active torque and passive
spring/damping torque; it has no explicit limb-gravity term. Unity applies gravity
to every physical link. Matching those scalar muscle equations therefore does
not establish that inherited force capacities are calibrated for the migrated
physical loading. This is a model difference, not proof of a particular biological
strength value or the sole cause of falling.

Protocol 7 tests four 300-second cases: maximum isometric force scales 1/0.5/2/3,
with normal fatigue and zero posture offsets. The nominal control runs first.
Controller, moment arms, limb masses, passive drives, joint/torque limits and
prescribed pushes remain fixed. Both antagonists' capacities scale together, so
relative tone balance and activation/fatigue equations are retained. Actual fiber
forces are captured from the scaled plant; requested versus applied torque maxima
and clipped-axis counts expose the unchanged actuator limits. Force reserve is
varied as a causal diagnostic, not adopted as a live body calibration.

The maximum tested force is 3 times the original 3,200 N quadriceps capacity,
within the existing 10,000 N body-frame range. Golgi encoding still normalizes at
3,500 N, so larger forces can saturate that existing receptor channel; passing
offline validation does not establish sensory calibration or live neuronal delivery.
Live policy checks reject non-unit strength scales. Tests verify proportional
force/torque changes for identical measured input, unchanged activation/length/
velocity/fatigue, and rejection of invalid or overflowing capacities.

The software checkpoint retains canonical recruitment and opt-in muscle mode;
all noncanonical recruitment/distribution is isolated diagnostic work. Standing,
walking and live neuronal balance remain unqualified. The next physical results
must be reviewed before adopting a recruitment change.

Run `unity-balance-capability-20261012-141256-dd75f1` completed four finite,
isolated, settled cases. Half/nominal/double/triple force capacities held standing
for 1.28/10.51/34.19/53.96 s respectively; none held at the end. The nominal
control reproduced exactly. Triple strength clipped 16 requested actuator axes;
the other cases clipped none. All 12,000 body frames passed actual offline
validation/transduction without projection. More force delays failure but does
not produce sustainable standing. No strength scale is adopted.

## Motor-unit fatigue candidate

Reviewed original `AvatarMuscle.Advance`, its fatigue/relaxation tests, the measured
port, and the original physical muscle spindle/Golgi and group III/IV fatigue
receptor paths. Above activation 0.10 the inherited scalar fatigue derivative is
`0.020*a - 0.032*0.04*(1-a)`, positive throughout that range. Constant activation
above the threshold therefore drives scalar fatigue to one and reduces force
by `(1-fatigue)^2.2`. It has no partial-load steady state in that range. The
fatigue-free diagnostic held 120 s, while greater capacity and modest posture
changes only delayed failure. This motivates a structural muscle comparison;
it does not establish that balance control or physical calibration is correct.

The isolated candidate uses the resting/active/fatigued three-compartment
controller (3CC) and zero-load rest recovery extension (3CC-r):
`dMR=-C+R*MF`, `dMA=C-F*MA`, `dMF=F*MA-R*MF`.
Recruitment/deactivation rate is 10/s; recruitment cannot use more than the
resting reserve. Rest multiplier applies only when filtered target load is zero.
Conservative simultaneous transfers use substeps <=0.001 s and preserve the
total pool. Actual MA sets force; MF is reported as unavailable fatigued units.
The old scalar `(1-fatigue)^2.2` penalty is not also applied. Original force-length,
velocity, measured pose, target/reflex and 0.045/0.090 s demand filter are retained;
3CC adds its own recruitment controller. This extra recruitment lag is an explicit
model difference, not a hidden mechanical assistance. No root or joint pose is set.

Primary sources:
- [Frey-Law et al. 2012, equations and Table 1](https://pmc.ncbi.nlm.nih.gov/articles/PMC3397684/).
- [Looft et al. 2018, 3CC-r rest recovery and parameter table](https://pmc.ncbi.nlm.nih.gov/articles/PMC6092960/).
- [Rakshit et al. 2022, explicit bounded controller equations](https://par.nsf.gov/servlets/purl/10379517).

Protocol 8 tests six 300-second cases with the same feedback, pushes, gravity,
joint limits, torques and zero posture offsets: legacy nominal/triple controls;
nominal 3CC using original F=.020/R=.032 constants with rest multiplier one;
nominal/triple 3CC-r using published F/R fits, rest multiplier 15; and nominal
3CC with F=R=0 as an explicit recruitment-timing ablation. The latter distinguishes
fatigue effects from the candidate's extra activation delay; suppression is not adopted. The
inherited-rate pool case changes how recovery/recruitment are interpreted and
is explicitly uncalibrated. Published ankle/knee/shoulder/elbow/trunk rates map
to foot/shin/arm/forearm/chest; hip and neck use the general fit. These are
joint-level sustained-isometric fits, provisionally assigned to individual
dynamic fibers. The rest multiplier for trunk/general regions is also provisional.
No human endurance or dynamic-body calibration is claimed by using those fits.

Fatigue stays enabled in the five model/strength cases. Pool traces expose requested activation,
resting, active and fatigued fractions alongside the 38 actual muscle signals.
Offline validation checks unique identities, bounds, conservation and agreement
with body activation/fatigue. Those semantics differ from the old scalar distress
index: existing fatigue-pain/interoceptor thresholds need calibration before any
live adoption, just as high-force Golgi saturation does. Live stepping rejects
all three pool policies; original Avatar/default Unity fatigue remains unchanged.
Software tests cover pool bounds/conservation, low-demand analytical equilibrium,
zero-recovery exhaustion, rest-only recovery, force without double penalty,
reset, timestep convergence, actual 38-fiber capture and live rejection.

The checkpoint passes 125 focused Unity/Avatar/body-receptor tests. Unity build
has zero warnings/errors and all 40 native-coordinate probe samples pass. Seven
synthetic validator fixtures accept valid matching pools and reject broken sums,
activation mismatch, duplicate identities, missing fibers, and absent/empty
expected pool captures. The actual completed 12,000 strength-run frames also pass
the extended validator. Physical results for the new six cases are pending;
software verification alone does not qualify standing or this fatigue model.

Run `unity-balance-capability-20261012-144514-173e62` completed all six finite,
isolated, settled cases; none held standing at the end. Legacy nominal/triple
controls reproduced 10.51/53.96 s and their recorded mechanics exactly. The
nominal no-fatigue pool held only 1.80 s, inherited-rate pools 1.81 s, published
nominal pools 1.80 s, and published triple-strength pools 0.60 s. Published
nominal fatigue was below 0.007 while standing. All 18,000 actual body frames
and 12,000 pool captures passed validation; maximum pool conservation error was
4.47e-8. The pool equations are finite/conservative, but this implementation's
activation timing is unsuitable for the unchanged balance fixture. Early failure
of its zero-fatigue control prevents attributing that failure to fatigue.
No pool policy is adopted. Results and original traces are preserved on D:.

## Preserve original activation timing within pool recruitment

Reviewed original Avatar muscle activation/relaxation constants, zero-force tests,
measured joint recruitment and muscle receptor paths again. Protocol 8 applied
the 0.045/0.090 s filter and then another 10/s pool recruitment controller. The
new isolated adaptation sends raw excitation into one exponential recruitment
step with the original rise/fall constants. Recruitment is bounded by the resting
reserve. Fatigue/recovery transfers remain simultaneous and conservative, followed
by bounded exponential recruitment in <=0.001 s substeps. Zero-load relaxation
uses Avatar's original 0.0001 threshold, returning released units to the resting
pool. Raw excitation and the counterfactual unbounded filtered demand are both
captured alongside actual active/fatigued/resting units.

With F=R=0 and no depleted units, recruitment uses the original filter exactly,
and resting units are its analytic complement. Tests require bit-for-bit matching
of scalar activation/force over changing excitation/length, and identical measured
torques/forces across the 38-muscle plant. Fatigue-enabled tests cover conservation,
zero-recovery exhaustion, true-zero relaxation/recovery and convergence against
a finer numerical step. This removes the additional delay; it does not establish
biological calibration or successful physical standing.

This is an adaptation of pool fatigue/recovery, not an unchanged published 3CC-r
model: recruitment rates follow Avatar's original constants, and the transfer
integrator changes accordingly. Published F/R and rest multipliers remain
provisional for this dynamic per-fiber use; no human endurance calibration follows
from the published fits. Default Avatar/Unity behavior and the prior diagnostic
3CC implementations are preserved. Live policy checks reject both new adaptations.

Protocol 9 tests six 300-second cases: legacy nominal/triple fatigue; nominal
legacy and adapted-pool fatigue-free controls; and adapted pools with published
fatigue/recovery at nominal/triple strength. Feedback, posture, masses, passive
drives, joint/torque limits, gravity and pushes remain fixed. The two ablations
check that the original and new timing can both support the same physical task;
they are not adopted as live physiology. Qualification and actual sensor/pool
validation still follow automatically in the independent PowerShell window.

The timing checkpoint passes 131 focused tests; the undepleted control matches
the original scalar activation/force bit for bit and all 38-muscle plant forces/
joint torques exactly. The Unity build has zero warnings/errors and passes all
40 native-coordinate probe samples. Nine synthetic offline-validator fixtures
cover valid pools and rejection of broken conservation, mismatched activation,
duplicate/missing identities, absent/empty expected captures, and missing/out-of-
range expected raw excitation. All 18,000 prior actual body frames still validate
with the extended checks. The completed comparison is recorded below.

Run `unity-balance-capability-20261012-151946-44770e` completed all six finite,
isolated, settled cases. The inherited nominal/triple controls reproduced their
recorded mechanics and 10.51/53.96 s standing intervals exactly. The two nominal
fatigue-free controls held 299.94 s and matched all 3,000 corresponding physical
trace samples exactly, excluding capture sequence/timestamp and pool-only fields.
This confirms that the single activation filter removes the earlier timing defect
under this fixture. Fatigue-enabled adapted pools held 63.89 s at nominal capacity
and 299.45 continuous final seconds at triple capacity; the latter met the isolated
standing criterion and clipped 16 actuator axes during the full run. Maximum
standing fatigue was 0.5031. Its bounded active/resting/fatigued states remained
finite throughout five minutes. No asymptotic endurance calibration is implied.

All 18,000 captured body frames and 9,000 pool captures passed actual offline
validation/transduction without projection; maximum pool conservation error was
4.47e-8. Original traces, report, validation and subsequent analysis are SHA-verified
on D:. This establishes a mechanically useful isolated candidate, not DNNE balance,
learned behavior or calibrated physiology. No pool policy, recruitment change or
strength scale is admitted to live operation.

## Return to the actual DNNE loop and observe recovery

The next visible observation uses `--muscles`: the original nominal-capacity,
canonical-recruitment/fatigue plant, exposing 38 physical muscles on 19 axes.
Reviewed original `AvatarMuscle`, its relaxation/fatigue tests, canonical measured
port tests, and spindle/Golgi body transduction before selecting this baseline.
It is not full decoder parity: four axial muscles and crouch/sit/lie transitions
remain missing as listed above. A failed get-up cannot by itself diagnose DNNE.
The successful triple-capacity pool fixture remains an isolated candidate.

The operator requires recovery after a fall. The live 60-second observation keeps
running after falling, retaining DNNE authority and natural physical/sensory traffic.
It introduces no get-up script, root lift, animation, teleport or episode reset.
New 100 Hz measurements record first fall/recovery times, threshold-entry counts,
post-fall neural actuation and supported recovery. Ten uninterrupted seconds meeting
the existing support/height/upright/local-limit bounds, with two distinct authorized
motor ticks and actual leg actuation within that supported interval, constitute an
observed recovery. Earlier standing commands cannot qualify a passive rebound.
The final recovery criterion also requires supported standing at completion.
This is an observation with neural influence, not proof of causal or learned recovery.
Historical limit excursions can fail the separate whole-run standing criterion
without hiding a subsequent locally valid recovery. No observed fall leaves
recovery untested. Non-finite physics invalidates both recovery results.

The launcher preserves its simpler torque baseline unless `-Muscles` is explicitly
selected. It passes that switch into the detached window/player, records body mode,
player/assembly hashes and recovery criteria before the trial, and verifies 38
unique actual muscle captures afterward. Fresh lease/motor readiness and operator
Play are still required. Brain state is preserved; independent PowerShell saves
reports and verified D: backups without live agent monitoring. Recovery-specific
tests cover passive/stale support, interrupted holds, repeated falls, historical
limit excursions and non-finite invalidation. Runtime recovery remains unassessed
until the operator completes this observation. The checkpoint passes 135 focused
tests; the Unity player builds with zero warnings/errors.

## Completed live recovery observation and balance-feedback repair

Run `unity-standing-20261012-154017-30be00` completed 60.01 simulation seconds
with finite physics and passed its integration checks. It fell at 1.50 seconds,
held standing for 0.92 seconds in total, and never recovered. DNNE continued
actuating the actual leg muscles for 5,487 physics steps after falling, with 436
distinct authorized motor ticks. Four hundred body frames were accepted and
generated 46,326 receptor spikes; no body/contact frames were rejected. These
are admission/transduction results, not proof of delivered neuronal traffic.

The saved 286 physical samples expose an adapter error: 275 of 278 fallen samples
have `balanceError=0`, although their root remains fallen. The whole-body floor
contact hull can contain the COM; a positive support margin alone does not mean
upright balance. The final motor snapshot also reports zero population balance
error and zero descending righting drive. Endpoint snapshots do not establish
the entire latch history. Maximum captured muscle fatigue before the first fall
was only 0.01593, so initial exhaustion does not explain that early fall.

Reviewed original Avatar balance dynamics, articulated-body recovery tests,
physical body transduction, vestibular input, neuronal motor control and righting
tests before this repair. `prepare-unity-balance-sensor.py` extracts the original
phase enum, constants and `ResolveBalanceError` function into `BalanceErrorCore.cs`.
Unity now encodes its measured support margin, root pitch/roll and physical phase
through that function. The existing simpler classifier remains; its absent
instability timer and control allowance are explicitly zero. Support geometry,
joint state, muscle force/fatigue/recruitment and DNNE state remain unchanged.
No host posture choice or recovery motion is introduced.

All 299 focused Unity/Avatar/receptor/motor tests pass, including 40 original
articulated-body tests. The generated function exactly matches the original over
648 combinations of phase, margin, allowance, time and tilt. A regression using
the actual floor-supported fallen frame preserves its raw geometry and 38 muscle
measurements, sets balance error to one, and prevents a false positive
`BalanceImprovement` delta while still fallen. All 286 original frames pass
offline validation/transduction; a separate explicit encoding projection makes
all 278 fallen samples report error one and remains valid. Original captures are
unchanged. This projection does not assess live delivery or physical recovery.

The new player builds with zero warnings/errors and passes all 40 native boundary
probe samples. The next observation repeats the nominal canonical 38-muscle,
60-second trial with corrected feedback. It records each advancing valid motor
API response, including full evidence, action channels and righting-latch fields,
in `motor-telemetry.jsonl` using the existing polling cadence. This gives a saved
motor history without live agent monitoring or additional HTTP requests.
Recovery after this repair is pending. Missing axial muscles/posture transitions,
descending righting generation and end-to-end sensory delivery remain open;
this one defect is not asserted to be the sole cause of failure.

## Corrected live result and prepared transport comparison

Run `unity-standing-20261012-160513-8ed073` passed integration and finite-physics
checks, but fell at 1.41 seconds without recovery. All 279 fallen trace samples
now encode error one. All 286 actual body frames validate/transduce without any
projection or missing canonical muscle identity. The feedback repair is confirmed;
the four missing axial muscles and omitted posture transitions remain unresolved.

The active saved motor journal has no righting latch in 455 advancing responses.
Rounded afferent drive peaks at 0.804 and descending drive at 0.030, below the
0.040 floor. Its zero population balance-error estimate is distinct from the
physical measurement, not proof that the corrected body signal was lost.
The post-trial transport cap is six spikes per service/160 total per tick.
FIFO prefix selection is biased under saturation, but the bounded exported routing
window does not overlap active trial ticks. This finding does not establish the
cause of the weak descending output or prove end-to-end delivery.

An optional `-BrainProfile headless` launcher setting prepares the operator's
higher-budget comparison: 24 per service/1,536 total per tick, applied without
restart and with automatic profile selection temporarily pinned. It also changes
concurrency/timing settings. Saved periodic transport diagnostics will expose actual
effective limits and latency; the separate observation protocol documents sampling
scope and restoration. No muscle, receptor, decoder or brain source changed here.
The operator asked to finish coding without running, then to shut services down.
No new experiment/profile mutation occurred. Graceful shutdown saved coordinator
state and persisted synapses, SHA-verified on D:. Those files are not a complete
checkpoint of every neuron's transient membrane state.

## 19 October commissioning correction

Restart verified all 239 synapse files against the shutdown backup and reused the
same Unity executable/assembly. The first `headless` attempt passed mechanical
qualification but never started the physical trial: zero physics steps/body frames,
with a motor/lease readiness error. The later "38 muscles" message was a launcher
validation ordering error, now replaced by the actual incomplete-run reason.

The broad performance preset also raises per-tick participant work: RPC concurrency
72 permits 144 warmup or 216 normal instances at zero pressure. Saved timing shows
a 2.05-second tick and delayed motor updates, incompatible with the unchanged
1.25-second body freshness gate. A new opt-in `embodied` profile retains every
stable scheduling/timing setting and raises only the spike limits to 24/1,536.
Actual scheduler regression tests preserve stable's 20/30 selected instances;
all 53 focused profile/scheduler/standing tests pass. This does not establish a
causal recovery benefit. Body, muscle, receptor, decoder and gate behavior are
unchanged, and full axial/posture parity remains unresolved. The current brain
was gracefully saved and backed up on D: before the coordinator rebuild/restart.

The initial `embodied` guard expired during cold start: all 62 requests succeeded,
but motor ticks advanced only from 2 to 16 before the 60-second limit, below the
configured 80-tick warmup. Later, the same running brain recorded a 30.32 ms tick
and 11–50 ms motor HTTP samples with the higher caps still effective. The launcher
now waits through warmup, then requires ten fresh advancing motor intervals within
a bounded ten-minute readiness stage. Cached ticks and stale/slow responses cannot
qualify, and the body's 1.25-second guard is unchanged. Six offline readiness
regression scenarios pass. This is a startup sequencing correction; it does not
claim any standing/recovery result or change original Avatar behavior.

## Higher-transport live result

The warm-brain run `unity-standing-20261019-071224-50cdeb` completed 60.01 seconds
with integration checks passed, then ended paused/disconnected with lease release.
It fell at 1.44 seconds and did not recover. All 286 captured physical frames
validate/transduce unchanged; all 279 fallen samples retain error one. The actual
38-muscle plant continued neural leg actuation for 5,381 steps after falling.

All 11 in-trial transport samples retained the 24/1,536 caps and 30 selected
instances. Sampled tick times were 22.44–51.05 ms, with zero sampled dispatch errors
or queue spike drops; source-budget losses still occur and full delivery remains
unverified. Descending righting activity now reaches a rounded 0.391, but the
latch remains inactive and population balance error is 0.000 in all 461 active
journal responses. In 410 post-fall responses, combined bilateral drive clearly
exceeds its entry floor while the population-error gate is closed. This localizes
an immediate decoder condition; it does not prove that removing it would restore
recovery or establish the cause of the low estimate. Post-trial aggregate rates
and the bounded routing trace are outside the active window.

The 113 existing motor/righting/original balance tests pass, including stable
tonic-drive release and neural authority controls. The next diagnostic target is
sensory-to-neural balance-error representation using isolated stable/fallen replay
and negative controls. No host-selected recovery, gate bypass or unqualified
physical get-up sequence was added. Missing axial muscles and posture transitions
remain open, and the current run's result is not learned standing or recovery.

## Isolated postural-signal replay

The next study uses captured stable/fallen physical vectors and the existing body
transducer with fourteen isolated engines from the seven-region bilateral postural
circuit. Reviewed original Avatar balance, physical body and labyrinth transduction,
righting topology, motor/righting tests and actual structure-host profiles before
preparation. Models, routes and decoders are reused; no Avatar mechanics, muscle
plant, sensory encoding, neuronal authority or live recovery gate is altered.

Twenty cases compare saved/fresh starting synapses and 24/unrestricted source spikes
across stable, fallen, phase-step, muted and left-only sensory controls. The saved
state is copied from the verified D: shutdown snapshot into a separate directory
for every case, with actual loaded counts and protected input hashes checked.
Membranes are initialized afresh and normal plasticity remains possible in each
copy. Regularized sensory timing, excluded brain regions/noise/global scheduler
and incomplete cortical authority limit conclusions to the isolated circuit.
There is no live DNNE connection or physical recovery assessment. Six short
preflight cases pass with finite outputs, correct controls and unchanged source
files. The full diagnostic runs independently in PowerShell with verified backups.

## Postural replay result — 19 October

All twenty cases in `postural-replay-20261019-082543-c12aa7` completed with finite
outputs, unchanged source captures/synapses and zero queue rejections. A reproducible
analysis checks all 12,000 samples and separates initial/returned stable phases.
The ten saved-synapse cases have zero decoded error throughout; both vestibular
nuclei receive traffic but emit zero spikes, even without the source cap. Identical
vermis output across all cases includes its existing intrinsic pacemaker activity;
that activity cannot by itself certify sensory compensation. Fresh unrestricted
cases produce error excursions during stable and muted input as well as fallen
input, so neither resetting synapses nor removing transport caps qualifies a fix.

The held captured stable frame contains motion and is not a quiet standing control.
The next target is isolated sensory input/membrane/spike transfer and neural posture
discrimination with stationary, motion, tilt/support-loss and bilateral controls.
See the standing-observation document for measured counts and scope. This is an
analysis finding, not a replacement of original Avatar balance/labyrinth mechanics
or a physical recovery result. No live body, neural authority, safeguards or weights
were changed; missing axial muscles and posture transitions remain open.

## Vestibular signal-transfer instrumentation — 19 October

Reviewed the existing Avatar balance implementation, physical receptor and
labyrinth paths, righting topology and their tests before extending diagnostics.
The new transfer comparison uses those original mechanisms. Forty isolated cases
cross eight stationary/motion/tilt/support/trajectory/input-control protocols with
intact saved, fresh and three selective baseline-strength interventions. Both
broad and normal support widths are included, retaining the latter's existing
tonic narrowing signal. Synthetic fixtures are explicitly sensory controls, not
Avatar-generated dynamics or recovery commands.

An internal observer, unused by live hosts, records the strength actually applied
at integration before plasticity. The probe logs vestibular nucleus currents,
voltages and spikes, checks integrated/pending/admitted accounting and distinguishes
initial/stimulus/returned quiet stages. Existing body mechanics, receptor encoding,
neuronal models and motor authority are preserved. No gate or live weight changes
are introduced. The observer transparency check and 121 focused tests pass; twenty
short preflight cases verify instrumentation and input/state isolation. The full
comparison runs in independent visible PowerShell with verified D: backups. Cross-
host statistics will require replication because production timing/cell-bias hash
salts differ across processes. Physical standing/get-up remains unassessed here.
