# Active sensory stationary-state study

The finer sensory run `cleft-refinement-20261118-884934` passes its seven short
trajectory comparisons and 132 component cases. Those trajectories began from
prescribed guesses, not a demonstrated resting state. The pinned biological
source solves a stationary problem before dynamics; its initial values are
also guesses. We now need to distinguish an inaccurate initializer from an
unstable or implausible equilibrium of the declared reduced equations.

This study changes no Avatar biological kernel, dynamic integrator, learning
rule, live model, editor selection or old save. It uses the already qualified
stationary-search component and existing independent Decimal membrane equations.
The motor partition window `3d8835` remains untouched. No pending output is read.

## Prospective conditions and methods

`cleft_rest_protocol.py` fixes 18 cases: one, two and eight cleft patches;
uniform and graded geometries; original, hyperpolarized and depolarized seeds.
The latter two shift hair/calyx voltages by -20/+20 mV and scale the initial
potassium by 0.8/1.2 and sodium by 0.9/1.1. All seed voltages, concentrations,
gates, identities, geometry, channel parameters and explicit ports are saved.
Bundle displacement and every axonal terminal current are explicitly zero.
This is an isolated sealed terminal, **not** an afferent cable or body feedback.
Graded geometry retains the existing 32 mV bath-reference shift.

The native search retains its unchanged row-scaled Newton solver, log-ion
coordinates and residual-decreasing line search. Its prospective settings are
80 Newton steps, 28 halvings, and tolerances `1e-10 mV/ms`, `1e-12 mM/ms`,
`1e-13/ms` for voltage, ion and gate rates. No clipping, artificial holding
current, new conductance or diagonal regularization is introduced.

An independent SciPy trust-region least-squares search starts from each same
prescribed guess, never from a native result. It uses the separate 60-digit
Decimal currents, transport and full capacitance-matrix solve, a three-point
Jacobian and at most 2,000 function evaluations as counted by SciPy. Unknown
scales are 50 mV and one log-concentration unit; residual scales are 1 mV/ms
and 0.01 mM/ms. These numerical scales do not change the equations or physical
acceptance criteria. Optimizer success alone does not qualify a root.

For stationary solving only, the seven gates per patch equal their local
equilibrium values. This follows exactly from zero gate derivative with finite
positive time constant; it is not a reset during dynamics.

Every reported native root is independently reevaluated with its **captured
actual gates**, at 60 and 90 digits. The physical root limits are `1e-9 mV/ms`,
`1e-11 mM/ms` and `1e-12/ms`. Complete derivative comparisons retain the earlier
absolute `2e-10` plus relative `2e-12` bounds. Precision witnesses retain
`1e-40` absolute plus relative bounds. Native and independent roots must be
finite, positive in ions and within [0,1] for gates. Failed searches, accepted
iterates, last trials, errors and independently checked residuals are retained.

## Full dynamic local stability

At every qualifying independent root, the study differentiates the **full**
`1 + 11N` voltage/ion/gate dynamics. Actual gate coordinates remain free; no
gate equilibrium substitution is made during these perturbations. Ledger
integrals are excluded because their neutral accumulation is not cell-state
instability. Reference scales are 100 mV, 100 mM and one gate unit; ionic scales
are capped at the positive root concentration so symmetric stencils cannot
cross zero. A smooth extension of gate equations outside [0,1] is allowed only
for derivative stencils, not accepted states or time integration.

Central differences use steps `2^-12`, `2^-14`, `2^-16` in these scaled
coordinates, with Decimal differences before binary64 matrix conversion.
The exact free-gate block must equal diagonal `-1/tau` within absolute and
relative `1e-12`; eliminating the gates would fail this check. Successive
Jacobian difference norms must shrink below `0.4 * previous + 1e-10/ms`.
Fourfold Richardson pairs and their spectral-norm difference are retained.

Eigenvalue residuals must be at most `1e-10`. Classification uses the largest
real eigenvalue, eigenvector conditioning, the **empirical** Richardson
difference and a declared binary64 roundoff allowance. The margin is
`1e-10/ms`. Outcomes are locally-decaying estimate, locally-growing estimate,
or inconclusive. Four analytic matrix controls cover stable, unstable, neutral
and oscillatory cases. Saved Jacobian matrices, spectra and uncertainty details
support later review. This is not a rigorous nonlinear stability proof or
observed perturbation recovery; no trajectories are integrated in this study.

Both searches finding valid roots and completing the numerical Jacobian checks
determines the numerical study outcome. Stability sign is reported separately;
an unstable valid root is not hidden by requiring a stable answer. Cross-method
and cross-seed distances use `1e-4 mV`, `1e-6 mM`, `1e-7` gate units to label
nearby branches. Different valid branches are retained, not forced together.
Agreement from three guesses cannot prove uniqueness. Physiological rest,
afferent firing, anatomical calibration and Avatar balance remain unqualified.

## Frozen execution and review

`cleft_rest_gate.py` requires the completed, independently reviewed `884934`
evidence, 132-case static inventory, unchanged source kernels/reference equations,
and exact reviewed managed/numerical runtimes. Preparation uses a clean pushed
revision and new package, preserves original evidence and verifies the initial
D: copy. The visible PowerShell runner builds from that frozen source, executes
132 focused tests, captures 18 native searches, runs 18 independent searches
and all numerical controls, compares saved results, fingerprints binaries/runtime
again and verifies the final D: backup. Failures retain their evidence and bounds.
The agent does not monitor the window; the operator reports completion.

Authoring probe build passed with zero warnings/errors. Python AST, protocol
schema, 132-case source inventory and PowerShell parsing passed. No new model,
root search, spectrum, numerical control or test executed inline. Build scratch
for authoring is `D:/DNNE-build-work/cleft-rest-authoring-20261118`.

If roots and local stability are credible, the next step is matched native and
independent perturbation trajectories from those roots before defining an
afferent connection. If they fail, diagnose the retained residuals/branches
before changing biological parameters. Existing source-equation uncertainties
about the afferent cable remain explicit in the 17 November equation audit.

Launched `cleft-rest-20261118-760702` from pushed revision
`e4e3cf2243d108a49a49162b3ef5173605989c25`, after verifying all 1,880 initial
files plus manifest on D:. Visible task: `DNNE-Cleft-Rest-b9015911e2fc`.
No post-launch monitoring or outcome inspection. Await operator completion.
