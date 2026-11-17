# Independent continuous-movement spindle reference candidate

The completed coupled population study `spindle-coupling-20261117-85ae1d`
matches fixed-length independent references, but its moving distribution differs
by up to 0.01497 across sampled time steps. Force alone concealed some of that
shape discrepancy. The next calculation investigates the numerical composition
without changing biological rates or promoting the finest previous step.

## Independent spatial formulation

`tools/spindle_eulerian_reference.py` imports no retained-interval transport,
reaction or candidate solver. It independently implements the same pinned
Bag/Chain equations from Neuromechanics-Lab/spindleModelTutorial revision
`721ba03cb775261548766ed1349c4bf2e0e35385`. The AGPL-3.0 source/license, canonical
MATLAB changes and completed native constructor records remain preserved.

For physical strain x and imposed half-sarcomere length L:

```
v = 0.5 dL/dt
dn/dt + v dn/dx = f(x) D (A-B) - g(x) n
B = integral n dx
dD/dt = -integral [f(x) D (A-B) - g(x)n] dx + boundary outflow
dA/dt = 8e7 * 10^(-pCa) * (F(L)-A) * (1+A/F(L))
        - off_rate * (A-B) * (2-A/F(L))
```

A includes occupied active actin. Detached-plus-bound myosin and inactive-plus-
active actin have separate conservation ledgers. Left/right outward flux is
recorded and returned to detached myosin; inward flux is zero. This finite-domain
convention remains a numerical approximation, not calibrated physical ripping.

The new calculation stores cell masses on a fixed strain grid. A first-order
upwind flux transfers mass between adjacent cells, simultaneously with reaction
and actin kinetics in an adaptive ODE solve. Movement reversals and calcium
changes are explicit segment boundaries; state continues from each accepted
endpoint. No reaction/transport splitting or retained-interval rebinner is used.

First-order upwinding introduces numerical diffusion, including for smooth
advection; it cannot be assumed exact because it is a different implementation.
This limitation and the need for fine grids are described in the
[Clawpack Riemann-book discussion](https://www.clawpack.org/riemann_book/html/Approximate_solvers.html).
The reference is therefore an independently discretized **candidate**, with its
own temporal, spatial and range comparisons before any accuracy qualification.

Original rates stay piecewise constant on the same 0.5 nm cells as the current
candidate. Spatial subdivisions repeat those rates; they do not sample a new
biological rate surface. Four subdivisions (8, 16, 32, 64) give cell widths
0.0625, 0.03125, 0.015625 and 0.0078125 nm. Narrow and doubled domains retain
their original half-ranges of 20.25 and 40.25 nm. Constructor-rate controls read
the completed native MAT parameters, independently of either new Python solver.

## Prospective experiment

One visible PowerShell window runs:

- Twelve controls: Bag/Chain analytic detachment and actin activation; positive
  and negative upwind transport against the exact semidiscrete Poisson solution;
  outflow followed by reversal; analytic attachment; two inherited-rate checks;
  independent derivative ledgers; and invalid-input rejection. The pure-transport
  controls also record their error against continuum translated cell masses,
  explicitly exposing numerical diffusion. Those continuum errors are not
  hidden inside the semidiscrete analytic pass.
- Six fixed-length reductions against all eighteen completed independent
  references. The original 1e-8 fraction and 0.01 Pa reference bounds are retained.
- Forty-two moving solves: both fibers, the three existing 40 ms/4 nm/pCa 6.5
  protocols, four narrow-domain resolutions; two additional tight temporal
  solvers at the finest grid; and a doubled-domain finest-grid solve.
- Twelve temporal contrasts, eighteen adjacent spatial contrasts, six range
  contrasts and eighteen comparisons with the saved candidate trajectories.
  The original candidate is read, never rerun or overwritten.

DOP853 strict/tighter and RK45 tighter use distinct tolerances/step limits
recorded in `eulerian-input.json`. Absolute tolerances scale cell-mass components
with their width; scalar ledgers have separate tolerances. A maximum advective
Courant interval supplements adaptive error control. This is not an explicit
Euler method or a claim that a Courant bound proves coupled accuracy.

Every accepted ODE node, complete sampled state, edge coordinate, prescribed
length and boundary ledger is saved. No negative clipping, population
renormalization or tolerance retry is permitted. Full piecewise-distribution L1
differences accompany scalar-state and force differences. Candidate/source sample
times must match within 1e-15 s, and algebraically equivalent prescribed lengths
within 2e-12 nm; these are coordinate-roundoff checks, not model error bounds.

Reference temporal/range criteria are L1 <= 1e-8, scalar <= 1e-8 and force <=
0.01 Pa. The finest spatial-pair criteria are L1 <= 1e-5, scalar <= 1e-6 and force
<= 0.01 Pa. These new prospective spatial bounds do not replace any historical
bound. They assess sampled discretization differences, not an exact continuum
error theorem. Older coarser spatial contrasts are retained even when they fail.
Domain/conservation bounds remain 1e-8/1e-10; analytic controls use 1e-9.

`DIAGNOSTICS_COMPLETE` means the controls/reductions and declared comparison
matrix completed. `SampledReferenceDiscretizationCriteriaPassed` separately
reports whether the temporal, finest spatial and range criteria all passed.
A false flag leaves the reference unresolved and must not be interpreted as a
candidate failure against exact truth. A true flag still needs review of the
full distributions and refinement pattern. No automatic candidate promotion
occurs; `candidateMovingAccuracyQualified` remains false in this diagnostic.

## Boundaries and execution

This is prescribed intrafusal mechanics. It does not model slack equilibrium,
stretch-receptor membrane firing, gamma innervation, calibrated human spindle
physiology, live Avatar sensory replacement or recovery after a fall. The
existing Avatar, Unity, editor, neuron and motor-cell implementations are unchanged.
The independent rubral neuronal/motor experiment has its own pending report.

Preparation requires clean pushed source, D:-verified completed evidence and
unchanged NumPy/SciPy dependencies. It performs metadata/source copying only.
The separate visible launcher performs all controls, integrations and comparisons,
then preserves results with a verified D: backup. No live monitoring is allowed;
review only after the operator reports completion. Concurrent study timing is
not a benchmark or evidence for CPU/CUDA speedup.

## Launch record

Launched as `spindle-eulerian-20261117-f0020c`, frozen at
`857518d9048c0c50a81d43540e7f95cac461bb8a`, in independent visible task
`DNNE-Spindle-Eulerian-b17aecc18cf3`. Its 1,859 initial files plus manifest
were verified on D:. Python/PowerShell syntax was parsed before packaging;
no controls, derivatives, integrations or comparisons executed inline.
No post-launch outputs, status or processes were inspected. This spindle run
and rubral replacement `rubral-motor-20261117-844975` await separate operator
completion reports. The broader brain/body/world goal remains active.
