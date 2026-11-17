# Vestibular cleft transport and membrane charge balance

The next sensory step needs a shared electrical and chemical environment between
the hair cell and calyx. `AvatarCleftEdgeKernel` and `AvatarCleftChargeBalance`
provide its local constitutive equations. They do not advance time, set anatomical
geometry, choose initial equilibrium, emit spikes or replace Avatar's live sensory
path. The hair-cell and calyx kernels remain unchanged. Calyx component result
`920328` must still be reported and reviewed before coupled use.

## Source and explicit reduction

The pinned Govindaraju et al. deposited model uses potassium/sodium diffusion and
electric drift in its cleft weak equations, plus membrane displacement current.
See [the source audit](DNNE_Vestibular_Equation_Audit_2026-11-17.md) and the
[authors' pinned model](https://github.com/acgsci/vestibularHairCellCalyxNQT/tree/47d499449df66c6676ee43cbb08ea1995469a817).
The retained `xml-inspection/template.json` records `wb/wfeq1` and the source
parameters. Its conductivity is `sigma_cleft*.6 + (Dk*ck+Dna*cna)*F/26`;
the K/Na concentration-gradient terms contribute diffusion current separately.

| Quantity | Source convention |
|---|---|
| K / Na diffusion | 0.81 / 0.56 um²/ms |
| Thermal voltage | 26 mV, room-temperature reduction |
| Other-ion conductivity | 600 nS/um: 60% of the source's 1,000 nS/um parameter |
| Charge conversion | 96.485 pA per attomole/ms for a monovalent ion |
| Capacitance | Explicit apical, uncovered hair, facing hair, inner calyx and outer calyx values |

For a spatial edge the caller supplies perpendicular face area, center distance
and both control volumes in physical units. No default gap geometry or leakage
multiplier is installed. All chemical concentrations must be positive and finite.
Invalid/overflowed values fail explicitly; no concentration clipping is present.
Only K and Na have explicit concentration state. The other-ion conductivity is
the source's reduced treatment, not a chloride/calcium or metabolic model.

## Conservative edge flux

The classical, nonsteric specialization of the exponentially fitted edge flux
in [Liu and Eisenberg, Eq. 56](https://www.mdpi.com/1099-4300/22/5/550) assumes a
constant electric field within each edge. We independently express that flux as

```
psi = (Vright - Vleft) / thermalVoltage
B(x) = x / (exp(x) - 1), B(0) = 1
amountLeftToRight = D * area / distance * (B(psi)*cleftLeft - B(-psi)*cleftRight)
```

Each amount flow is subtracted from the left volume and added to the right.
Concentration in mM times volume in um³ is an amount in attomoles. Multiplying
the amount flow by 96.485 gives its electrical current in pA. Background current
is `600*area/distance*(Vleft-Vright)` for the deposited material. Adding the full
1,000 conductivity again would double-count the explicit K/Na electric drift.

The implementation uses the Bernoulli identity and a small-argument series to
avoid exponential overflow and cancellation near zero field. It retains electric
drift even for tiny voltage differences at equal concentrations. A flux estimate
is not a guarantee that a later explicit time step preserves positive state.
Spatial resolution and time integration still require independent assessment.

## Simultaneous membrane charge equations

Three dynamic potentials are hair cell H, cleft S and calyx C. Endolymph E and
perilymph P are held reference potentials during each derivative evaluation.
Their capacitor charge remains explicit; neither is silently treated as the
other. The five capacitances connect H-E, H-P, H-S, C-S and C-P.

For capacitances `Ca, Cb, Ch, Ci, Co` on those respective faces:

```
A = Ca + Cb + Ch
C = Ci + Co
[ A    -Ch       0 ] [dH/dt]   [ -(I_HtoE + I_HtoP + I_HtoS) ]
[-Ch   Ch+Ci   -Ci ] [dS/dt] = [ I_HtoS + I_CtoS - I_Sout     ]
[ 0    -Ci       C ] [dC/dt]   [ -(I_CtoS + I_CtoP + I_CtoAxon)]
```

The implementation eliminates H/C using the positive Schur denominator
`Ch*(Ca+Cb)/A + Ci*Co/C`, avoiding cancellation in the equivalent subtractive
expression. It adds no artificial leak or diagonal regularizer. Internal face
currents cancel in total local charge; external ports remain identifiable.
The axon port is a required explicit current if used, not an implemented axon.

Whole-membrane geometry maps to 3 pF apical, 0.4558 pF uncovered hair,
2.9463 pF hair facing cleft, 2.9463 pF calyx facing cleft and 2.8833 pF outer calyx.
This is a bookkeeping map of declared areas, not a calibrated single-compartment
replacement for the source's spatial cleft. A future spatial assembly must share
the hair-cell voltage and distribute area/capacitance without counting it twice.
Nonmatching facing patch areas are rejected by the convenience mapper.

All five capacitor charges and stored energy are available for accounting.
`pF*mV = fC`, `pF*mV² = aJ`, and `pA*mV = fW`. The energy-rate identity includes
capacitive current into held baths. It covers these electrical elements only;
it does not claim a complete thermodynamic balance of pumps, maintained ion
reservoirs, chemical sources or a freely integrated sensory neuron.

## Verification and remaining work

Prospective component tests cover Fick diffusion, electric drift, equilibrium,
edge reversal, species conservation, gauge shifts, geometry scaling, an analytic
constant-field profile split into two edges, constitutive dissipation, extreme
fields, parameter identity and invalid inputs. Charge tests check all three
Kirchhoff equations, an independently solvable unit-capacitance circuit, internal
current cancellation, bath charges, energy/power and large capacitance ratios.
These tests execute only in a visible independent PowerShell window.

There are 25 prospective component cases in 21 test methods, plus the solution
manifest for a 26-case focused suite. The authoring test-project build succeeds
with zero warnings/errors; no tests ran. The probe authoring build also succeeds
with zero warnings/errors. Static Python syntax checks pass.

`CleftCouplingStudy` and `--cleft-coupling INPUT NEW_OUTPUT` now record complete
native evaluations without advancing a model. `cleft_coupling_protocol.py`
declares 468 edge inputs across three materials, three synthetic geometries,
13 voltage differences and four concentration pairs. Zero-field, tiny-field,
series-boundary and extreme-field conditions are retained. Sixty charge inputs
span three capacitance sets, five directed-current patterns and four electrical
reference configurations. Neither geometry nor voltage extremes are asserted
to be physiological operating conditions.

`analyse-cleft-coupling.py` independently evaluates the direct exponential flux
and builds a complete five-node capacitor graph, solving its three dynamic-node
matrix by pivoted elimination instead of the candidate's Schur formula. Each
input has both 60- and 90-digit Decimal references, with all expected values
saved. Prospective reference tolerance is 1e-40 absolute plus 1e-40 relative;
candidate tolerance is 2e-10 absolute plus 2e-12 relative. It compares every
reported quantity, reconciles the exact input/test inventory and checks runtime
preservation. References use the exact binary64 inputs, avoiding a different
rounded-input problem. No reference arithmetic has executed during authoring.

No new window is launched while the current calyx qualification awaits its
operator report. The prerequisite reviewer, frozen preparer and independent
PowerShell pipeline are now authored as described below. Then choose an
explicit spatial discretization and qualify coupled trajectories/refinement.
Free afferent firing, physical-to-bundle conversion, physiological calibration,
sensory return and Avatar balance/recovery remain unresolved.

## Review and launch sequence

1. Wait for the operator to report the calyx window's final footer and completed
   backup. `review-calyx-membrane.py CAPTURE NEW_REVIEW --operator-reported-complete`
   checks every C:/D: file, source archive, binaries, runtime, exact 23-case suite
   and all 15,984/224 saved comparison pairs. It reconstructs the original report
   using saved independent values; it does not evaluate channels or clamps again.
   A failed/incomplete capture is rejected and requires diagnosis.
2. Back up the new review with `backup-unity-run.ps1`, retaining the original
   capture and its review separately.
3. From clean pushed source, run `prepare-cleft-coupling.py CALYX_REVIEW` in the
   pinned numerical environment. It verifies the review's D: copy and capture
   identity, existing Avatar/hair/calyx source preservation, and exact runtime
   equality with the reviewed calyx study. It freezes the new protocol, source,
   original source/licensing evidence, review and binary inventories. Preparation
   executes no tests or constitutive equations.
4. Back up the new package and invoke its frozen
   `start-cleft-coupling-window.ps1 -PackageDirectory PACKAGE`. Only that visible
   window builds and executes the 26 tests, native arithmetic capture and two
   high-precision references. It verifies prerequisite metadata, exact test
   multiplicities, immutable inputs, numerical and managed runtimes, and final
   C:/D: preservation. It cannot resume an already launched package in place.

Python/PowerShell syntax and static input counts pass. The reviewer/preparer/
launcher have not been executed: the calyx report is still required. No pending
window's logs, status or process have been inspected. The check proves at most
local constitutive arithmetic; no source flag can promote it to integrated
sensory physiology or balance.
