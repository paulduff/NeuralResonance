# Independent fixed-length spindle accuracy

The completed canonical-state repair established one authoritative population
state. It did not establish the accuracy of the native `ode23` trajectories.
The next isolated study compares the already reported and reviewed `a43f1a`
recordings with independent continuous integration of the same equations.
There is no new Octave execution, live Avatar selection or sensory event output.

This follows the actual Avatar source: `PhysicalBodyTransducerRuntime` still
uses absolute muscle length/velocity deviations and frame-generated stimuli.
The repaired physical-sample transport preserves observations; it does not turn
those stimuli into mechanistic spindle afferent firings. Existing vestibular,
proprioceptive and physical-body mechanisms remain preserved. Fixed-length
accuracy is one prerequisite for the mechanistic replacement, not its completion.

## Source and scope

Reuse 18 native MAT recordings: bag and chain, quiet hold, active hold and
activation on/off, each at 1, 0.5 and 0.25 ms. All run for 100 ms at an imposed
half-sarcomere length of 1,250 nm. The on/off input is quiet before 20 ms, active
from 20 through 80 ms, then quiet. This is the original prescribed calcium input,
not gamma-neuron recruitment. It does not test shortening, overlap loss, the
active slack driver, mechanotransduction or afferent membrane dynamics.

The source is the pinned author tutorial at
`721ba03cb775261548766ed1349c4bf2e0e35385`, with the previously reviewed Octave
compatibility and canonical-state adaptations. Every original source and save
remains untouched. The derived `spindle_fixed_reference.py` carries AGPL-3.0
attribution; the package preserves the author license, original/derived classes,
defaults, source driver and transformation declarations. It is an isolated
reference tool, not part of the live DNNE or Avatar solver.

Use original binary MAT observations for all 84 state fractions, clock endpoints,
constructor rates and forces. The JSON export rounds some tiny values to zero.
For example, the inspected bag recording retains a nonzero forward rate near
1.61e-313 in MAT. The MAT/JSON maximum state discrepancy in that recording is
about 1.17e-16. That is not a failed accuracy result; preserving original values
avoids introducing a serialization approximation into a tighter comparison.

Both constructors are explicitly checked. Chain detachment differs from bag:
its positive-side cubic coefficient is 0.4 rather than 0.2, its additive rate
is 10 rather than 0.5, and its actin deactivation coefficient is 200 rather than
20. The independent constructor audit caught the initial mistaken assumption
that detachment differed only by its baseline. The candidate was corrected
before any new numerical solve. No biological parameter was changed.

## Independent calculation and prospective criteria

The reference integrates the conserved detached/bound and inactive/active actin
fractions directly, without clipping, renormalization or resets. Both fibres
retain the original cross-bridge equations and recorded binary64 constants.
The reference independently reconstructs stress from the saved distributions.
It does not reuse native `ode23`, interpolation or the original derivative code.

For each of the six physical conditions, run DOP853 at two tolerances and Radau
with an analytic Jacobian: 18 new solves. State remains continuous across exact
input edges, with no extra initial step. Each solve spans the same full physical
interval independently of native outer-step size. Preserve all accepted solver
nodes and evaluate at the union of every native recorded endpoint. At a solver
segment endpoint, use the accepted state that actually continues the integration,
rather than its potentially differently rounded dense interpolant.

Before the full matrix, check six analytic controls: detachment-only exponential
decay and actin-only tanh activation, each under all three solver settings.
These are mathematical limiting systems, not physiological ablations. Also check
the analytic Jacobian against central finite differences. The original physical
parameters are restored by constructing a fresh model for every production solve.

| Quantity | Prospective absolute limit |
| --- | ---: |
| Analytic control state error | 1e-9 |
| Jacobian element discrepancy | 2e-5 |
| Reference-to-reference state discrepancy | 1e-8 |
| Reference-to-reference stress discrepancy | 0.01 Pa |
| Native-to-reference state discrepancy | 1e-5 |
| Native-to-reference stress discrepancy | 1 Pa |
| Myosin/actin conservation residual | 1e-10 |
| Population domain violation | 1e-8 |

These are declared engineering acceptance limits, not measured physiological
variability. All 18 pairwise reference contrasts and all 54 native/reference
comparisons must pass. Check positivity, available binding sites and conservation
at accepted internal reference nodes and all sampled endpoints, as well as native
endpoints. This is sampled numerical evidence; it is not a proof between every
solver node. Do not widen bounds after observing the outcome. If the independent
methods disagree, do not treat one strict trace as exact. If native trajectories
fail, retain per-resolution errors to decide the subsequent solver repair.

## Execution and authoring evidence

`prepare-spindle-fixed.py` requires clean pushed source, verifies selected C:/D:
evidence against the completed state review, freezes source and observations, and
pins the already qualified numerical environment. The visible PowerShell launcher
checks analytic controls, solves, audits, verifies dependencies/source again and
backs up the final evidence. No model, test or benchmark runs inline. No timing
measurement is made, so this comparison need not wait for a separate accuracy
experiment to finish. Its launch must be recorded separately; never inspect
outputs before the operator reports completion.

Before freezing, source/syntax inspection and a read-only audit parsed all 18
completed MAT recordings. Constructor constants, time grids, prescribed input,
population views and existing accounting matched. Maximum independently
reconstructed stress difference was 5.820766091346741e-11 Pa. This is a saved-state
force calculation, not a new trajectory or independent solver result. The
authoring report is `artifacts/spindle-fixed-preparation-20261116`, one file plus
manifest with a verified D: copy. Numerical controls and reference solves remain
unexecuted until the separately recorded window launch.

The neuronal/native body study `observed-native-body-20261116-bb10dc` remains
unmonitored while this work is prepared. Its result cannot be inferred from any
spindle preparation check. Neither experiment qualifies balance or fall recovery.
