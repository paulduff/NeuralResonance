# Golgi coupled SDIRK candidate

The checked reference now satisfies the original numerical bounds, while the
preserved C# split solver has millisecond spike-timing errors. See
`DNNE_Golgi_Reference_Results_2026-11-09.md`. `GolgiSdirkCell` introduces a separate
coupled numerical candidate; `GolgiReferenceCell` remains the original control.
No host or live Golgi population selects the new class.

## Method and unchanged biology

The method is Alexander's two-stage, second-order diagonally implicit RK scheme,
with gamma = `1 - 1/sqrt(2)`, rows `(gamma, 0)` and `(1-gamma, gamma)`, and final
weights `(1-gamma, gamma)`. The scalar-method stability motivation is documented
by [INL's LStableDirk2 implementation](https://openpronghorn.inl.gov/source/timeintegrators/LStableDirk2.html)
and [Alexander's original paper](https://epubs.siam.org/doi/10.1137/0714068).
Those properties do not establish accuracy of this nonlinear implementation.

Each stage solves its soma voltage and both local calcium concentrations
simultaneously. For a trial triple, the ordinary gate equations are solved
algebraically, SK uses its six-state implicit Markov system, and the entire
135-node cable uses the existing Hines elimination. Newton solves the remaining
three equations. The two massless junctions remain Kirchhoff constraints; their
voltages do not acquire invented capacitance.

Geometry, channel rates/densities, Nernst reversals, temperature, calcium pumps,
SK transitions and diagnostic blocks remain the previously declared model.
Ordinary rate evaluation and conductance expressions reuse `GolgiChannelKinetics`.
Its conductance expression now also accepts a supplied state, with the same
arithmetic order; the old instance method delegates to that expression. Existing
channel/cell checks remain required. No physiology is fitted to the numerical
reference, and no oscillator, voltage reset or replacement spike source is added.

Cable voltage is stored relative to the leak reversal to reduce cancellation
from a common DC term. Exported voltage uses the original mV convention. Calcium
Newton unknowns use micromolar scaling; physical pools remain mM. Charge and
calcium accounting integrate the actual two stage RHSs with the RK weights and
compensated ledger addition. Ledger values are not reconstructed from the final
voltage or calcium state.

## Declared numerical controls

The caller supplies a constant-input step from `1e-6` through `0.015625 ms` and
must split at input changes. The solver is fixed-step; no automatic step retry,
hidden substepping or error-controlled trajectory claim is made. Newton uses
bounded-count central differences, partial-pivot dense solves and a damped line
search. At most twelve Newton corrections and twelve line-search halvings are
allowed. Residual targets are `1e-10 mV` and `1e-11 micromolar`; the constructor
also permits explicitly tighter scales down to 0.125 for sensitivity checks.

All accepted gates and SK occupancies must stay in [0,1], both calcium pools
must stay positive and at most 0.1 mM, and SK mass error must remain below 1e-8.
No clipping or renormalization is used. A failed numerical stage faults the cell
without committing its physical state, clock, ledger or events. Diagnostic
evaluation counters can record failed work. Invalid arguments are rejected before
the cell is faulted. Resource exhaustion is not an atomic-checkpoint guarantee.

Independent stage residuals for cable current, ordinary/SK gates and calcium are
recorded, alongside nonlinear convergence, charge/calcium balances and SK mass.
Somatic crossings retain the existing -20 mV observational threshold and endpoint
interpolation; they remain somatic events, not qualified active axonal outputs.
Event interpolation and long-run timing must be checked separately.

## First visible component window

Seven new cases compare initial state with the original cell, both passive
injection locations against a separate dense RK matrix solve, passive equilibrium,
active accounting/stage residuals, invalid inputs, and failed-stage isolation.
They join the sixteen original channel cases, eight original cable/cell cases and
solution-manifest check: **32 required cases**. The independent dense passive
calculation uses a different solver and RK coefficient expression, retaining all
135 nodes. It is not an independent anatomical calibration.

Release builds of both the probe and test assembly succeed with zero warnings or
errors. Runtime component checks are pending. They run only through
`prepare-golgi-sdirk-components.py` and the visible frozen-source PowerShell
launcher, with initial/final D: backups and no live monitoring. This first window
does not run a six-second candidate comparison or qualify long-run accuracy.

## Required continuation

After the component outcome, add the full six-protocol capture and compare every
saved state and somatic crossing against the already verified reference arrays.
Include step refinement and a tighter nonlinear-tolerance control to distinguish
time-discretization from incomplete stage convergence. Preserve the original
split traces, full error histories and all failed conditions. Predeclare the
candidate acceptance protocol before executing it; do not infer long-run success
from component tests, formal order or the method's scalar stability function.

The calcium pump/reference bath, passive axon, native NEURON reproduction,
physiological calibration and granular-layer circuit remain open. Production
DNNE, Avatar, World/Unity, editor controls and all old saves remain unchanged.
