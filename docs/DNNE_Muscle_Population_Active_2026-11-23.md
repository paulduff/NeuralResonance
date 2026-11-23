# Active slow muscle population comparison

This study follows the completed `muscle-population-initial-20261123-370cb9`
review. It tests the existing coupled muscle equations with all 147 original
slow TA units. It does not implement the 53 fast units or recruit motor neurons.
Execution belongs in an independent visible PowerShell window with no live
monitoring. A final footer is required before reviewing results.

## Inputs fixed before execution

Both sides retain their exact saved candidate fiber lengths, measured muscle
path lengths and physical epoch `4.999999888241291 ms`. Zero unit activity,
zero fiber velocity and unit yield come from the completed fresh initial-state
records. No evolved state is reset. All 147 per-unit parameters, their order
and force coefficients remain unchanged; source data and initial review are
checked against their D: backups and copied into the new package.

The dimensions remain declared candidates. They combine mapped OpenSim TA
lengths with the original muscle model's nominal tendon reference ratio and
constant pennation. Mass follows fiber length to preserve reference cross-section.
They are not anatomical calibration, and TA parameters must not be copied to
unrelated muscles without justification.

Eight conditions last 200 ms each:

| Condition | Arrivals | Mechanical boundary |
|---|---:|---|
| Left quiet | 0 | Fiber free, total length held |
| Right quiet | 0 | Fiber free, total length held |
| Left first unit | 1 to source row 1 | Fiber free, total length held |
| Left last slow unit | 1 to source row 147 | Fiber free, total length held |
| Left mixed | 588 | Fiber free, total length held |
| Right mixed | 588 | Fiber free, total length held |
| Left mixed stretch | 588 | Fiber free, prescribed total length |
| Left mixed fixed | 588 | Diagnostic fiber clamp |

Single arrivals occur at `5 + .015625*(zeroBasedUnitIndex % 7)` ms. Mixed cases
deliver one original diagnostic event per unit at each of 5, 25, 45 and 65 ms,
plus that same seven-group offset. There is no generated neural rate command.
Each unit has its own declared contact, event identities and ordinal sequence.
These prescribed arrivals explicitly do **not** establish neuronal authority.
The remainder of each trace includes relaxation after the last arrival.

The stretch case uses total-length offsets at the following knots:
`(0,0), (30.015625,0), (80.015625,.001), (130.015625,-.001),
(180.015625,0), (200,0)` in ms and meters. These are known prospective test
inputs, not interpolation across unavailable future live-body frames.

## Numerical comparison

The original C# solver runs at .125, .0625 and .03125 ms, splitting at every
arrival and length knot. All 24 traces contain 801 complete samples at .25 ms.
Each contains the shared mechanical state, forces and ledgers, plus release,
calcium, activation, flux ledgers and arrival counts for every unit. A gzip
stream preserves little-endian binary64 samples with a declared header and
column inventory. Full initial declarations, delivered arrivals, final states
and any failure states are retained separately.

The independent reference retains the earlier scalar force equations, direct
causal release sums and original calcium equations. Independent unit calcium
operations are vectorized. Radau uses the existing bounded central difference
Jacobian, stored sparsely; only structurally zero cumulative-ledger columns are
omitted from differentiation. There is no thresholding of nonzero entries.
Before trajectories, 108 comparisons to the original scalar derivative and six
selected-column Jacobian controls must pass. Field limits are absolute 2e-12
plus relative 2e-13; Jacobian limits are absolute 1e-7 plus relative 2e-8. These
are implementation controls, not biological validity criteria.

Each condition has DOP853, tighter DOP853 and Radau solutions with the exact
settings and physical error bounds inherited from `dynamic_muscle_protocol.py`.
Reference cross-method agreement, candidate finest-step error, refinement,
accepted-state domain, release/count ownership, calcium accounting and
momentum/work ledgers are all required. Quiet and clamped controls are included.
Failed references retain partial traces and the study attempts the remaining
declared reference conditions. No tolerance is changed in response to outcomes.

The visible runner also retains the 379 existing managed regressions, eight
packaging controls and eight original independent initial equilibria. Biological
runtime and learning equations are unchanged. Source, Python/NumPy/SciPy,
selected managed runtime and executed binary identities are checked and backed
up to D: before the final footer. Preparation, syntax checks and the authoring
build execute no models or tests.

Passing would qualify the sampled numerical behavior of this slow-population
candidate. Actual neuronal recruitment, fast fibers, attachment to the moving
native body, anatomical/physiological calibration and standing remain separate.
The next integration decision must use the saved force and motion results.
