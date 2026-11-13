# Independent coupled-muscle numerical comparison

The reported component gate passed all 93 cases; see
`DNNE_Muscle_And_Spindle_Results_2026-11-13.md`. This next rig keeps its exact
calcium/muscle implementation and all old isometric/transport sources unchanged.
The first full comparison completed its candidate traces but failed in reference
recording; see the reviewed outcome below. Overall numerical acceptance remains
unqualified pending the corrected full comparison.

## Inputs and scope

`DynamicMuscleProbe` records **33 candidate traces**: eleven cases at 0.125,
0.0625 and 0.03125 ms, sampled every 0.25 ms. Each trace retains complete states,
forces, flux/impulse/work ledgers and every delivered arrival. Solver grid,
actual arrivals and prescribed-length slope changes are separate boundaries.

| Cases | Input and purpose |
| --- | --- |
| Quiet, single arrival, repeated arrivals | Free fiber at resting total length |
| Fixed optimal, short and long fiber | Separate calcium length effects from motion |
| Passive and active stretch/return | Known piecewise-linear external length, including fractional boundaries |
| Two motor units | Distinct first/second author parameter rows and staggered arrivals sharing mechanics |
| Recorded free and recorded fixed | Replay all 28 actual somatic arrivals from the passing six-second circuit capture |

Synthetic cases last two seconds; recorded cases last six. Recorded events retain
their original session, source/cell/projection/synapse/contact/message identities,
ordinal, firing time and arrival time. These are preserved events, not a newly
running neuron or live motor authority. The prescribed length is a diagnostic
input, not interpolation of unseen live body frames.

The pinned MAT data supplies geometry and both parameter rows independently of
candidate constants. The recorded fixed-fiber case at 0.125 ms must reproduce
the earlier complete isometric calcium/activation-equivalent force trace within
1e-10. The original force remains distinct from the dynamic passive-2 correction.

## Independent references and declared acceptance

`dynamic_muscle_reference.py` implements the continuous equations separately,
evaluates release by summing causal waveforms, and uses ordinary/tight DOP853
and Radau: **33 independent solves**. All restart at actual event/load boundaries
and retain full sampled states. Radau uses a bounded central-difference Jacobian
to avoid unbounded numerical differentiation factors for constant states; this
is not an analytic-Jacobian claim. Negative binding trial iterates use a declared
even continuation of the activation power, while accepted states stay unchanged
and undergo separate physical-domain checks.

Both cross-method agreement and same-method tolerance tightening must pass.
Finest candidates compare to **both** tight DOP853 and Radau. Predeclared absolute
bounds are in `dynamic_muscle_protocol.py`: reference/candidate limits respectively
are 1e-9/1e-7 m for length, 1e-7/1e-5 m/s for velocity, 1e-8/1e-6 for calcium,
activation and yield, and 1e-5/1e-3 N for force. Flux integrals, acceleration,
impulse and work have their own explicit units and bounds. Refinement requires
finest error at most 0.2 times coarse error plus 0.02 times its absolute bound,
for each quantity against each reference. No alignment, parameter fitting or
retrospective relaxation is allowed.

Every candidate must preserve event delivery, direct-sum release and independent
calcium/momentum accounting. Finest kinetic-work error must be at most 1e-7 J;
that checks this numerical mechanical reduction, not metabolic conservation.
Reference accepted internal points and dense samples require domain checks.
Complete environment file fingerprints must match before and after the study.

## Source-ordering diagnostic

Five free-fiber cases at three steps also run **15 source-ordering controls**.
They use Euler yield, Euler calcium, activation from updated binding/yield, then
mechanical RK4 holding that activation and the initial tendon length. This
reproduces the inspected ordering under controlled first-row inputs. Equilibrium
initial yield, explicit event boundaries, prescribed external length and common
endpoint readout are declared adaptations; it is not native 200-unit MATLAB
execution or MATLAB/Octave equivalence.

These controls report their differences from the continuous reference. They are
not required to match the new candidate's numerical bounds. Failed diagnostic
attempts retain their sampled partial states and last completed state/time;
they are never clipped or treated as passing original-source evidence. The
independent reference/candidate gates remain mandatory regardless of these
diagnostic outcomes.

## Frozen independent execution

`prepare-dynamic-muscle-comparison.py --component-review PATH` verifies the
operator-reported 93-case review and C:/D: hashes, unchanged candidate sources,
the earlier neuronal/isometric result, original arrivals/trace and author MAT
archive. It requires clean pushed source. The resulting package must receive
a verified D: backup before its visible PowerShell launcher is started.

The runner repeats all 93 focused tests, checks the frozen prerequisites and
environment, builds the probe, records every candidate, solves references and
audits all evidence. Final source/binary/environment checks precede backup.
Review begins only after the operator reports completion, with no live monitoring.
Numerical success would permit causal dynamic-source/body work; physiology,
endplate/muscle action potentials, recruitment, live Unity coupling and recovery
remain separate requirements. No live service, editor or saved brain changes.

## Launch record

The frozen package is `dynamic-muscle-comparison-20261113-cd1577`, revision
`66e136dc464254d7936c77332085a6ae6f75bb9d`. All 1,555 initial files plus manifest
have a verified D: copy, rechecked before launch. Independent visible PowerShell
task `DNNE-Dynamic-Muscle-Comparison-90d60cea3207` was started. This is the only
comparison launched at that revision. Its operator-reported failure is reviewed
below. All preceding muscle, spindle and profile windows have completed reviews.

## Reported failure and recording repair

The operator reported `cd1577` FAILED. The separate offline audit is
`artifacts/dynamic-muscle-failure-review-20261113/review.json`, reproduced by
`tools/review-dynamic-muscle-failure-20261113.py`. All **2,259 files per copy**
(264,453,156 bytes), the frozen Git source and executed binaries match C:/D:.
Manifest SHA256 is
`4DAA755FBFECB3D7969DC4A7ECF66F0819B80CCF25A8B3F0F7962BB502E14C86`.
The separate review also has a verified D: copy. Neither capture is overwritten.

All **93 tests and 33 candidate executions** completed with complete finite
sample arrays and the recorded events preserved. **21 reference solves and nine
source-ordering controls** completed before the reference recorder failed during
the active-stretch case. The finest candidate is within the predeclared absolute
bounds against those 21 saved references. This partial result does not establish
all-case reference agreement, refinement or complete numerical acceptance.

The preserved partial trace isolates the failure: a load knot at **500.015625 ms**
and the next motor arrival at **500.03125 ms** enclose no requested 0.25-ms output
sample. SciPy integrated that interval successfully and its retained states are
finite and in the declared domain. The recorder then passed an empty time array
to dense interpolation, which raised `ValueError: need at least one array to
concatenate`. The last 2,001 requested samples remain preserved. This failure
does not identify a muscle-dynamics defect; complete numerical qualification is
still required.

The repair only guards dense interpolation with a nonempty sample mask. Endpoint
state advancement, domain checks, statistics and boundary bookkeeping always run,
including intervals with no output samples. No boundaries, equations, parameters,
solver tolerances, recording times or acceptance bounds change.

The corrected visible runner adds three analytic regression cases, each with
DOP853 and Radau (six method checks), before the full comparison. They check
clustered unsampled boundaries, coincident events/knots/final samples and the
exact failed active-stretch partition. Their analytic event-count integral
detects skipped physical time and lost events without using muscle equations.
All three require complete state carry and internal-point validation. Python
and PowerShell syntax checks pass; these new tests await execution in the fresh
window. The retry repeats the unchanged 93/33/33/15 study under frozen source.

The corrected package is now launched as
`dynamic-muscle-comparison-20261113-95e59f`, frozen revision
`cc84abe349f7fbd4a442b2da2ad7e120611ba272`, task
`DNNE-Dynamic-Muscle-Comparison-f76ff58863e5`. All 1,557 initial files plus manifest
have a verified D: copy, rechecked before launch. It is the only pending operator
report. Await completion without inspecting live outputs; launch establishes
neither passing regressions nor numerical qualification.
