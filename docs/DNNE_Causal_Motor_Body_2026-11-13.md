# Causal motor-to-body integration candidate

## Purpose and current status

The completed `dynamic-muscle-comparison-20261113-95e59f` capture qualifies the
isolated continuous muscle equations against independent numerical references
within its declared inputs and tolerances. It does not connect muscle tension to
physical motion. This candidate adds that connection at an explicit mechanical
boundary, retaining the existing identified motor-event transport and muscle
state. It is not selected by live Avatar, Unity, a neuronal host or the editor.

`AvatarCausalMotorPlant` owns one physical step and the muscles attached to that
body. `IAvatarMotorMechanicalBody` requires measured path lengths in metres,
velocities in metres/second, axial tendon forces in newtons, an explicit clock,
immutable geometry identity, and an exact applied-effort receipt. A real adapter
must derive its path lengths and force-to-joint mapping from the same geometry.
The existing Avatar normalized angle/length heuristic is not converted into
anatomical metres by this change. Existing activation/fatigue filters must not
be applied a second time to the new explicit muscle tension.

## Causal partition

The initial body must be captured without evolution; the owner starts paused.
For each requested physical interval:

1. Validate unchanged current body geometry, clock and exclusive step ownership.
2. Require all declared neuronal sources to be complete through the next physical
   endpoint. Incomplete input leaves both physics and the arrival queue untouched.
3. Validate every represented muscle substep before consuming events or advancing
   physics. Adjacent timestamps must remain distinct and each actual represented
   duration must be at most the qualified muscle limit of 0.125 ms. Reject an
   exhausted clock rather than dropping a substep. The declared resource ceiling
   is 16,384 muscle substeps per physical step; this is not a biological constant.
4. Capture current axial tendon tension and apply it unchanged for that physical
   interval. No later event or future geometry contributes to this effort.
5. Capture and validate the actual completed body endpoint and effort receipt.
6. Advance each muscle at its known start-of-interval path length, splitting its
   integration at each original fractional arrival. Events in another muscle do
   not alter this muscle's numerical partition.
7. At the completed muscle time, accept the observed endpoint path length without
   changing fiber/calcium state, advancing time or rewriting earlier forces.

This is a first-order explicit partition. Muscle response reaches physics at a
subsequent physical boundary, with a transfer delay up to one physical interval.
Its stability and accuracy depend on that interval; causality alone does not
qualify the numerical approximation. Independent continuous coupled references
and physical-step refinement are required before live or native Unity promotion.
Velocity is preserved in the mechanical observation, but is not used to invent
an interpolated future muscle path in this partition.

The effort ID includes the session clock, model/body fingerprint and physical
step. Faults after events have been consumed or physics may have advanced stop
the epoch; uncertain physical steps cannot be retried. Completed and last
observed step counters are separate. If the body throws before a new observation,
the last observed step is not evidence that physics failed to advance.

`HeldTensionWorkOnBodyJoules` records the external boundary work
`sum(F_start * (L_start - L_end))`. An analytic constant-moment-arm joint provides
an independent virtual-work/kinetic-energy check for this held-force interval.
That quantity is not a claim of total tendon, fiber and metabolic energy
conservation across the partition. Tendon constitutive energy and partition
defect need separate accounting in the coupled numerical study.

## Checks and evidence workflow

The first component package contains 27 new cases and the previous 93 muscle,
calcium, transport, replay and solution-manifest regressions. Synthetic neuronal
fixtures and an exact constant-torque joint cover:

- Paused unevolved initialization and all-source completeness before stepping.
- Actual fractional event/contact identity and no retrospective physical effect.
- Endpoint events, fixed-body equivalence to the qualified direct muscle solver,
  and exact replay/packet-partition invariance.
- Free joint motion from the muscle response; opposite moment arms produce
  opposite motion under matched input; axial force and path geometry obey the
  same virtual-work relation.
- Unrelated-muscle event isolation, immutable snapshots and same-time body-length
  updates that preserve fiber/calcium/accounting state.
- Wrong endpoint, session, body identity, effort receipt, missing/duplicate paths,
  nonfinite geometry, uncertain steps, external pose/step changes, reentrancy,
  wrong-thread calls and clock-resolution exhaustion.

`prepare-causal-motor.py` requires clean pushed source and the verified passing
muscle comparison review. It verifies the selected evidence against both C: and
D: manifests and proves that removing the exact declared observation method
leaves the qualified muscle source unchanged. It freezes source, evidence,
per-class and per-method coverage into a fresh package.

`start-causal-motor-window.ps1` requires an initial verified D: backup, launches a
visible independent PowerShell window, builds only frozen source, records binary
hashes and TRX results, requires all 120 distinct cases and declared method
coverage, then verifies frozen inputs/binaries and backs up all results. The
operator reports completion before Orion inspects the outcome. No live monitoring.

Authoring build succeeded with zero warnings/errors; Python and PowerShell syntax
checks passed. Component tests have not been inspected or claimed passed at this
point. The earlier draft remains preserved in its Git stash and verified D: copy.

## Promotion work remaining

After the component result, compare the actual coupled body/muscle trajectory
against independent error-controlled integration using the same geometry,
inertia, original neuronal arrivals and explicit initial conditions. Refine the
physical partition independently of the muscle integration step. Record joint
state, all muscle states, tension, arrival identity, boundary work and energy
defect; preserve failed bounds as evidence. Use quiet, single/train, antagonistic
and disturbed-joint conditions, and the already recorded somatic neuronal output.

Then connect the qualified boundary to native Unity physical stepping and actual
musculotendon geometry. The existing physical-sample producer and this motor
owner cannot independently step the same body: one owner must produce motor
effort and the complete body/contact sample for each physical interval. Extend
editor diagnostics when that native path exists. Full-body anatomy, afferent
physiology, feedback through actual neuronal cells, balance, falls and recovery
remain unqualified. This component does not bypass any of those requirements.

## Causal motor component launch

`causal-motor-20261113-848e50` was frozen from
`4c234d51d0134845283ad1f169fc5ce57e7d8f7e`. Its 1,563 initial files plus manifest
were verified on D: and rechecked by the launcher. Visible independent task
`DNNE-Causal-Motor-a847a7d5df52` was launched for 27 causal motor/body cases and
93 regressions. It has not been monitored; await the operator's completion
report before inspecting logs, status or test outcomes. The spindle root run
`spindle-state-20261113-ae7ae4` also awaits its report. These are the two pending
packages; no additional experiment was launched. No live runtime was selected.

## Reported component outcome

The operator subsequently reported `848e50` complete. Full C:/D: and frozen-source
review confirms all **120 distinct cases passed**, including all 27 new coupling
checks and 93 regressions. Current plant/muscle source still matches that capture.
The qualified muscle core remains exact after removing only the declared
observation method. See `DNNE_Causal_Motor_Comparison_2026-11-13.md` for review
counts, manifest and the next independent coupled comparison in preparation.
The component run is no longer pending. Compilation of the new probe is complete;
the numerical comparison has not been launched or qualified.
