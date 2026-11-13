# Independent coupled motor/body comparison in preparation

The reported component package `causal-motor-20261113-848e50` passes all 120
checks: 27 new causal motor/body cases and 93 muscle/transport regressions.
`tools/review-spindle-root-and-causal-motor-20261113.py` verifies all 1,953 files
per C:/D: copy, 93,968,535 bytes, all 1,557 frozen Git files, test binaries,
distinct per-method coverage and preservation of the qualified muscle core.
Its review is `artifacts/causal-motor-review-20261113/review.json`, with a verified
D: copy. The original capture manifest is
`8DA604C237A7D392D95D7C72E0B0765DFEB9B0D120F38830E5FA863586B10E91`.

This establishes component behavior within the tested fixtures. It does not
establish the full coupled trajectory's accuracy or select a live Avatar path.

## Prospective numerical study

The new `tools/CausalMotorProbe` project is in the solution and builds with zero
warnings/errors. `causal_motor_protocol.py`, `causal_motor_reference.py` and
`analyse-causal-motor.py` define the planned comparison. Their source has been
syntax checked; no candidate trajectory or reference solve has been executed.
The study still needs analytic reference/recorder checks, a frozen preparer and
visible PowerShell runner before it can be launched.

The fixture is a free joint with inertia 1 kg mÂ² and opposing constant moment
arms +0.02 m and -0.02 m. Each path has one slow motor unit using the previously
qualified author geometry and first parameter row. They are deliberately matched
reference muscles, not an anatomically calibrated human antagonist pair. Joint
path length is `L_rest - arm * angle`, and applied joint torque is `arm * tension`.
The body integrates the candidate's held torque exactly for each interval.

Eight conditions are declared before execution: quiet, single event, unilateral
train, alternating trains, matched bilateral trains, an initial angular-velocity
disturbance with/without subsequent input, and all 28 recorded somatic motor
events. The latter retain the original session, cell/contact/message identities,
firing times, conduction delay and arrivals. No neuronal model is rerun by this
probe. Recorded source identity is distinct from sensory feedback or live source
qualification.

The first four resolutions halve physical steps from 1 to 0.5, 0.25 and 0.125 ms
while retaining a 0.0625 ms muscle grid. A fifth uses the same 0.125 ms physical
step and halves only the muscle grid to 0.03125 ms. There are 40 candidate
conditions. Every physical endpoint and every arrival is recorded; the force
available before each interval and its start/end geometry are retained too.

The Python reference uses the established independent muscle equations, with
joint position, angular velocity and both muscle states coupled at every
adaptive derivative evaluation. It does not hold start geometry/tension over
an interval. Loose/tight DOP853 and Radau settings yield 24 reference solves;
their agreement and accepted internal-state domains must pass first. Event
boundaries are exact; short intervals with no requested samples still advance
the state. The reference records at 0.125 ms and retains partial evidence if a
condition fails.

Original bounds are prospective and expressed in physical units in
`causal_motor_protocol.py`. The finest physical and muscle-refined candidates
must both meet the same absolute bounds against tight DOP853 and Radau. Physical
refinement must reduce each measured error by at least 20% per halving unless
already below 2% of that variable's absolute bound. Those refinement comparisons
use the same 1 ms grid at every resolution; absolute accuracy checks include
every candidate endpoint. The separate muscle-grid contrast must be within 5%
of each absolute bound. A failure is retained, not fixed by widening its limit.

## Mechanical accounting

The reference independently integrates joint torque impulse, joint work, muscle
net impulse/work, calcium fluxes and tendon force times tendon velocity. Tendon
potential changes are also calculated from the constitutive force by 64/128
point Gauss-Legendre quadrature; the orders must agree within 1e-12 J and the
reference's independently integrated tendon ledger within 1e-8 J.

For each candidate interval, the body receives `-F_start * delta(path_length)`
of work. After the muscle evolves at its held start path, accepting the new path
changes tendon potential at the final fiber length. The sum of that potential
change and body work measures the energy defect introduced specifically by the
partition boundary. Both maximum cumulative defect and total absolute defect
are recorded, so cancellation cannot hide alternating errors. The finest
resolutions must each stay within 1e-6 J, with the declared physical refinement
criterion also applied to the defect. This diagnoses the coupling approximation;
it is not an ATP, fatigue or whole-tissue energy model.

Physiological calibration, native Unity geometry/step ownership, actual
afferents, live neuronal feedback and Avatar recovery remain subsequent gates.
No pending experiment has been monitored and no runtime/save/editor selection
is changed by preparing this comparison.

## Reference controls authored, not executed

`test-causal-motor-reference.py` now defines two artificial constant-tension
joint fixtures, each solved with tight DOP853 and Radau and checked against the
closed-form Newtonian angle, velocity, impulse, work and tendon-work solutions.
Clustered event boundaries exercise intervals without requested output samples.
Four signed/zero tendon-length integrals compare 64/128 point Gauss-Legendre
quadrature with QUADPACK. These checks have only been syntax checked; none has
run. The analysis now requires their saved eight-check passing record before
it can qualify a coupled trajectory. Frozen preparation and the visible runner
are still to be implemented; the 120-case component pass remains the latest
executed motor/body evidence.

## Frozen preparation and visible runner

`prepare-causal-motor-comparison.py` and
`start-causal-motor-comparison-window.ps1` now implement the complete workflow.
Preparation requires clean pushed source, the operator-reported 120-case review,
the passing independent muscle review, matching current/frozen motor and muscle
implementations and tests, unchanged reference equations, and verified C:/D:
copies of the selected evidence. Parameters and all 28 original arrivals are
copied from the qualified capture. Numerical dependency hashes must still match
that capture; preparation decodes parameters but performs no simulation.

The new package contains its full frozen source archive, reviewed prerequisites,
prospective protocol, parameter/event inputs, environment fingerprint and exact
test-method coverage. An initial D: backup is required and checked before the
independent visible PowerShell window starts. It runs the 120 regressions, four
analytic joint checks and four energy quadrature checks before the 40 candidate
and 24 reference conditions. Compiled binaries and frozen inputs are hashed;
numerical dependencies are checked before and after reference work, including
when a later stage fails. All results and partial evidence receive a verified
D: backup. Nothing polls the running experiment.

The reference controls now preserve their actual/analytic traces and any partial
state on exception. Their source and the complete runner/preparer parse cleanly;
no outcome is inferred from syntax checks or the successful probe compilation.
