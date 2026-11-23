# Full Avatar force-to-motion diagnosis — 23 November 2026

The reviewed `tendon-force-20261123-e764e3` experiment verifies 46,320 native
force/torque accumulator comparisons and preserves the original endpoints.
Despite this, its largest target-joint angle difference from quiet is only
2.5319364453935123e-6 radians. Successful force delivery has not established
useful motion. The prescribed 40 N pulls bypassed all neurons; this observation
does not justify increasing brainstem complexity or neural gain.

The next experiment isolates mechanical settings in the actual 15-link body.
It is not a repair, an anatomical calibration, or a standing controller.
The live Avatar, ordinary bridge and all neuronal/muscle equations are unchanged.

## Prospective protocol

Use the original float 0.005-second step, original eight-step/40 N pulls,
unchanged masses, shapes, initial pose, attachment locations and force code.
Select both directions of left hip, knee, elbow and shoulder abduction. This
includes revolute and two-axis spherical joints, a second native coordinate,
upper and lower limbs, and joints initially near a lower limit. Include a quiet
case for each mode. Every case gets a separate fresh Unity process.

| Mode | Only requested intervention before ownership |
| --- | --- |
| original | None; must reproduce completed original observed endpoints exactly |
| no-joint-friction | Set joint friction to zero |
| no-body-damping | Set linear and angular body damping to zero |
| joint-speed-floor | Raise each active joint's maximum speed to at least 1000 rad/s; retain a higher existing value |
| free-active-axes | Change the existing active limited rotations to free rotations; retain locked axes |
| no-gravity | Disable gravity on the 15 bodies |

These six modes and nine conditions give **54 native cases, 432 physical steps,
486 endpoint records**, preceded by the same **112 component checks**. The
speed floor is an artificial diagnostic intervention, not a proposed biological
speed. Removing angular limits is similarly an isolated causal probe, not a
change to anatomical acceptance criteria. Settings may have no effect at this
short duration. Record that result honestly; do not require a particular variant
to improve movement and do not infer causation from successful execution alone.

`TendonMechanicsDiagnostic` is available only under the explicit isolated tendon
qualification flag, never the live/bridge selector. It retains named exact-bit
settings before intervention, after intervention and after the ordinary tendon
owner has disabled the existing active spring/damper drives. It includes all
drive types, max joint/linear/angular speeds, mass/inertia, anchors, locks,
solver iteration counts, damping, friction and gravity. Settings are checked
before and after each physical step; changes during capture fail the case.

The analysis independently applies the declared settings transformation to the
saved original description. Any extra settings change fails. It checks that
only the ordinary active drive fields change when ownership begins. All modes
must retain the original initial pose/velocity/paths. Original-mode states,
point applications, path measurements and contacts must equal the saved original
at every endpoint. Preserve every baseline and failed result.

Every case retains the actual before/per-nonzero-application/after-all/after-
physics force trace. The original force bounds remain: 2e-4 N and 2e-5 Nm
absolute, plus 2e-5 times the largest expected component. The same native
accumulation and clearing checks apply to every variant. An independent
line-of-action calculation verifies the prescribed 40 N or zero tension and
the original float application points (force conversion allowance 2e-5 N).

Each target joint's signed angle/speed history is compared against the quiet
history **in the same mode**. This removes that mode's unforced drift from the
reported response. Preserve raw state for every link, including motion outside
the targeted joint. No new "movement passed" threshold is invented. Numerical
convergence, useful full-axis control, anatomy, neuronal authority and standing
remain separate and unqualified.

## API basis and limitations

Unity documents [joint friction](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/articulationbody/jointfriction)
as load-dependent and acting alongside the other damping terms. Its
[maximum joint velocity](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/articulationbody/maxjointvelocity)
is a reduced-coordinate speed limit applied before simulation. These are
plausible diagnostic factors; the documentation does not identify our cause.
Actual installed values are retained rather than assumed from defaults.
The previously qualified packed spherical-coordinate map is unchanged.

No hidden pose reset, external joint-torque substitute, force amplification or
new movement controller is introduced. If all variants remain weak, investigate
native articulated constraints and compare equivalent external wrenches versus
generalized efforts in a separate declared experiment. A full-body diagnosis
cannot be replaced by a free-root force check or a two-link hinge passing alone.

## Execution and current evidence

`native_tendon_mechanics_study.py prepare` freezes clean pushed Git source,
the installed attributed rig, selected completed captures verified against both
protected copies, original force limits, and executable/runtime identities.
The existing visible PowerShell launcher uses its own build workspace and
retains all case logs, endpoints, settings and analysis before verifying the
final D: backup. No test/model/analysis runs during preparation or preflight.

The Unity API compilation at
`D:/DNNE-build-work/tendon-mechanics-authoring-20261123-final` passed with zero errors
and seven existing reflection-field warnings. It executed no Unity player,
physics, neuron or test. The native diagnostic is authored but not yet launched
at this checkpoint. Pending attachment `526071` has not been accessed.

Static metadata validation found that the earlier bridge script sidecar had a
malformed asset GUID. It is replaced with a valid unique identifier; no scene
or prefab referenced the malformed value. Frozen earlier component packages
are retained. Those component-only checks did not validate a Unity import.
Python/PowerShell syntax, the exact static inventories and all 45 Unity script
metadata records pass after that correction. Native behavior remains untested.
