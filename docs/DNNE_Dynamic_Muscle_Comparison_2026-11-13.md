# Independent coupled-muscle numerical comparison

The reported component gate passed all 93 cases; see
`DNNE_Muscle_And_Spindle_Results_2026-11-13.md`. This next rig keeps its exact
calcium/muscle implementation and all old isometric/transport sources unchanged.
It is coded and the C# probe builds, but no new comparison result is claimed.

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
outstanding report. Await operator completion without inspecting live outputs;
launch establishes no candidate/reference result. All preceding muscle, spindle
and profile windows have completed evidence reviews.
