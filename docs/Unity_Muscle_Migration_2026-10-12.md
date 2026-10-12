# Avatar muscle migration — candidate, 12 October 2026

The live standing observation completed 30 simulation seconds, with 188 accepted
body frames, 20,424 generated body spikes, 350 accepted retinal frames, fresh motor
output and zero rejected inputs. All 119 services were healthy afterward. Its
longest standing interval was 0.14 s and maximum joint-limit overshoot 0.103676 rad.
The sampled trace is fallen by 0.85 s; standing drive first appears at 1.48 s.
Admission/generated spikes do not establish deferred neuronal delivery or learning.

PowerShell reported FAILED separately because Get-FileHash could not open Unity's
live log. The entire run has now been verified on D:. The original failed status
is retained alongside `backup-recovery.json`. The shared backup helper hashes
bounded log byte snapshots using read/write sharing. A locked-writer/append
reproduction passes; it does not demand that a live log remain unchanged.

The operator correctly pointed out that Avatar already has a muscle framework.
The first Unity adapter bypassed it with direct torques and empty muscle feedback.
Its actuator tests qualified mechanical response, not muscle physiology or standing.

The candidate generator `prepare-unity-muscles.py` ports the canonical activation,
force-length/velocity, antagonist excitation, fatigue/recovery and 19 muscle-pair
profiles from `AvatarMuscle.cs`/`AvatarArticulatedBody.cs`. Measured native joint
position/velocity determine muscle state and active torque. No joint pose is
integrated or written by the port. Canonical passive stiffness/damping use native
implicit drives; there is no second explicit passive damping loop. Old joint
inertia constants remain provenance but do not replace physical link inertia.

An initial decoder adapts signed neuronal fields to bounded joint targets/gain
and neuronally recruited standing tone. Independent bilateral spinal recruitment
is preserved. Hip/knee locomotor targets use the canonical working excursions
rather than full passive joint ranges. This is not the complete old controller:
crouch/sit/lie sequencing and ankle gait coordination remain outstanding, and the
former kinematic root/ground-support mechanisms are intentionally not copied.
Local antagonist/velocity feedback belongs to the inherited muscle equations;
no host selects a standing action. Rest at neutral with zero excitation and
exhausted physiological capacity do not preserve an active reserve.

The supported 19 axes expose **38 named muscles**, including activation, force,
length/velocity from measured joint state, and fatigue in body frames. The four
extra axial muscles cannot truthfully act through the current yaw-only chest:
physical spine pitch/roll axes remain necessary. Their forces/measurements are not
invented. Muscle mode is opt-in (`--muscles`); the qualified torque baseline and
paused default remain available.

The first isolated PowerShell comparison ran four fresh unanchored bodies for
ten seconds each: raw versus muscle mode, each
with zero versus full fixed standing recruitment. The commands are explicit test
fixtures, not neuronal output. Reports include muscle counts/force/fatigue, support,
joint violation attribution by axis/time, and final-two-second settling. A final
five-second standing hold is a mechanical result, not learned standing. No sensory
fixtures go to DNNE and its live network is not restarted, reset or imported.

The completed comparison `unity-muscle-comparison-20261012-104831-aeff04` was finite,
isolated and settled in all four cases. No case held standing for the final five
seconds. Longest continuously supported upright intervals were:

| Plant | Fixed stand drive | Longest interval | Maximum limit overshoot |
| --- | ---: | ---: | ---: |
| Direct torque | 0 | 0.14 s | 0.10166 rad |
| Direct torque | 1 | 0.65 s | 0.05169 rad |
| Canonical muscles | 0 | 0.83 s | 0.03974 rad |
| Canonical muscles | 1 | 1.07 s | 0.03291 rad |

All muscle cases reported 38 muscles. They improve this fixture's brief support
and limit behavior; the result does not establish standing under other commands.
Both captured final muscle frames pass the real ControlProgram validator and
generate additional proprioceptive populations compared with the same frames
without muscle measurements. This is an offline receptor test, not live delivery.
Captured JSON is retained in `UnityWorld/Qualification` and the full run on D:.

The six-case comparison added two explicit recruitment-policy cases. The
canonical law multiplies resting tone, active position feedback and velocity
opposition by the weaker antagonist's capacity. In the ankle pair this scales
the 2,900 N calf's active recruitment by 720/2,900. The alternative balances only
resting tone, leaving active recruitment at each muscle's declared capacity.
Activation, force-length/velocity and fatigue equations remain unchanged; the
canonical law stays the default and the alternative is enabled only in this
isolated comparison. Unit tests verify balanced neutral force, relaxation to
zero, stronger-calf recruitment and unchanged weaker-flexor capacity. This may
improve support but does not supply global balance control or establish the cause
of falling. Ten-Hz measured body traces are saved for all six cases.

That comparison completed in `unity-muscle-comparison-20261012-110023-bd21aa`.
The four controls reproduced their prior results exactly. Full active capacity
held only 0.95 s at stand=1 and tipped backward, versus the canonical case's
1.07 s and forward tipping. Its limit overshoot dropped to 0.001414 rad, but
stand=0 failed settling (late joint speed 1.342 rad/s). It is not adopted.
All saved outputs and trace analysis are verified on D:.

The operator's reminder to check Avatar led to the
[migration audit](Unity_Avatar_Migration_Audit_2026-10-12.md). The existing vestibular
organ was already present: Unity incorrectly supplied root tilt as relative trunk
tilt too, duplicating its effect. The candidate now separates chest-to-pelvis
motion, restores the canonical four plantar pressure fields from real contacts,
and estimates pressure center from the contact manifold. These estimates preserve
the existing DNNE receptor interfaces rather than adding a live host controller.

The completed twelve-case isolated fixture compared fixed standing recruitment with
measured-sway diagnostic feedback, including a position-only recruitment variant
that retains the original velocity opposition. It tests mechanical capability;
the feedback controller cannot connect to DNNE and cannot become live authority.
It achieved 9.23 s of supported standing at best, but every case eventually fell.
The best traces show lateral drift despite controlled fore-aft motion. All twelve
cases were finite and settled. See the audit for the exact results, sensor range
corrections and the prepared eight-case hip/ankle contribution comparison.

104 focused .NET tests pass. These include a 6,000-step canonical/ported muscle trace
and a 3,000-step joint-force comparison verifying that measured poses are not
integrated by the port. The Unity build reports zero warnings/errors. Physical
qualification of the next contribution comparison remains pending. Tests also cover vestibular transduction, single-count rigid-body
tilt, relative spinal motion and conserved bilateral plantar pressures.
Canonical muscle/sensor ports and isolated instrumentation form the software
checkpoint. Alternative policies remain diagnostics; standing/walking are not claimed.
