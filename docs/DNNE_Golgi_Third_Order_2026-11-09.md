# Third-order coupled Golgi numerical candidate

The completed second-order comparison failed absolute waveform/timing bounds in
five firing conditions despite passing stage convergence, accounting, reference
uncertainty and refinement checks. Halving its step reduced timing error about
fourfold. See `DNNE_Golgi_Coupled_Results_2026-11-09.md`. This change adds explicit
third-order integration to the same coupled model, retaining the failed control
and its evidence. No channel, geometry or physiological parameter is retuned.

## Numerical method

The candidate uses Alexander's three-stage, third-order, stiffly accurate SDIRK
tableau. The coefficient choice and L-stability are documented by
[INL MOOSE LStableDirk3](https://mooseframework.inl.gov/source/timeintegrators/LStableDirk3.html),
with the explicit coefficients in its
[implementation reference](https://mooseframework.inl.gov/docs/doxygen/moose/LStableDirk3_8C_source.html).
These mathematical properties do not establish this implementation's accuracy.

With `g = 0.435866521508458999416019451193556843`, the rows are `(g,0,0)`,
`((1-g)/2,g,0)` and `(b1,b2,g)`, where `b1=-1.5*g*g+4*g-.25` and
`b2=1.5*g*g-5*g+1.25`. The weights are the last row. Each stage uses the existing
coupled soma-voltage/two-calcium Newton solve, algebraic gate updates, implicit
six-state SK system and 135-node Hines cable solve. Massless junctions remain
algebraic constraints. The common stage solver and biological RHS are unchanged.

Stage increments form the next stage's right-hand side. Its coefficients include
a negative weight, so intermediate right-hand sides must not be mistaken for
physical states and clipped. Each solved stage still has to satisfy the original
finite/domain/residual checks. The accepted state is the third solved stage.
Independent charge/calcium ledgers integrate all three stage RHSs using the
same tableau weights, with compensated accumulation. No ledger is reconstructed
from the final physical state. A failed step faults the candidate without
committing physical state, clock, ledgers or events; diagnostic counters may
record attempted work.

`GolgiSdirkCell` defaults to its existing second-order path. Third order requires
the explicit internal `GolgiSdirkOrder.Third` option and reports a distinct solver
version. The existing long-study probe remains on the second-order default.
No live host selects either candidate. The fixed step limits, nonlinear
tolerances, biological equations and all prior failed acceptance bounds remain.
There is no automatic retry, hidden smaller step, or error-controlled solver claim.

Somatic threshold events retain the existing endpoint linear interpolation for
both methods, so the first comparison changes integration order alone. Third-order
state accuracy does not imply third-order event-time interpolation. Quantify that
separate interpolation contribution before making a long-run timing claim; the
events remain somatic observations rather than qualified active axonal output.

## Visible component qualification

Eleven new cases check the order conditions, initial biological state and explicit
selection, both passive input locations against an independent dense tableau
evaluation, passive equilibrium, four active short-refinement conditions, invalid
arguments and failed-step isolation. They join all 32 prior component cases,
giving **43 required tests** with exact class counts.

The active tests use ordinary, no-NaP, no-HCN and no-SK conditions with a constant
50 pA soma input for 4 ms. They compare steps 0.015625, 0.0078125 and 0.00390625 ms
against an explicitly finer 0.00048828125 ms calculation. For each voltage, gate,
SK and calcium group, successive error must be at most 0.2 times its preceding
error plus a declared numerical floor: 2e-7 mV, 2e-9, 2e-9 and 2e-7 micromolar
respectively. Every error is written to test output. Floor-dominated results do
not establish observed cubic order. This is self-refinement over a short interval,
not an independent long-trajectory reference. Accounting and stage-residual limits
remain the existing component limits and apply at every tested resolution.

`prepare-golgi-sdirk3-components.py` verifies the reviewed failed comparison and
its D: evidence, confirms the biological files and common stage solver unchanged,
then freezes clean pushed source. The existing component launcher now checks the
initial D: manifest, records the selected order, requires all class counts and
opens a visible independent PowerShell window. Source and executed binaries are
hashed, and final results/failures receive a verified backup. No live monitoring.

Test and probe builds succeed with zero warnings/errors; preparation/PowerShell
syntax checks pass. Runtime tests have not yet been reviewed. Component completion
will not qualify the six-second spike timing, physiological calibration, granular
layer circuit, full unit suite or Avatar recovery. Review this outcome before
preparing the full third-order comparison against the unchanged saved references.
Avatar, Unity and editor behavior and all old brain saves remain unchanged.
