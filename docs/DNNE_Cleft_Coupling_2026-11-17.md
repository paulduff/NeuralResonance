# Vestibular cleft transport and membrane charge balance

The next sensory step needs a shared electrical and chemical environment between
the hair cell and calyx. `AvatarCleftEdgeKernel` and `AvatarCleftChargeBalance`
provide its local constitutive equations. `AvatarCleftSpatialCoupling` assembles
them around one shared hair-cell voltage. They do not advance time, set anatomical
geometry, choose initial equilibrium, emit spikes or replace Avatar's live sensory
path. The hair-cell and calyx kernels remain unchanged. Calyx component result
`920328` is now reported and reviewed; its current/gate and clamp checks pass.

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
replacement for the source's spatial cleft. The authored spatial assembly shares
the hair-cell voltage and accepts explicit per-patch capacitances without copying
the apical/uncovered capacitance into each patch.
Nonmatching facing patch areas are rejected by the convenience mapper.

All five capacitor charges and stored energy are available for accounting.
`pF*mV = fC`, `pF*mV² = aJ`, and `pA*mV = fW`. The energy-rate identity includes
capacitive current into held baths. It covers these electrical elements only;
it does not claim a complete thermodynamic balance of pumps, maintained ion
reservoirs, chemical sources or a freely integrated sensory neuron.

## Spatial assembly with one shared hair cell

`AvatarCleftSpatialCoupling` owns an immutable, explicitly named set of
compartments and internal connections. Each compartment supplies its volume,
hair-facing and calyx capacitances; each connection supplies cleft face area,
distance and calyx axial conductance. Identified input states and membrane-current
ports must cover every compartment exactly once. Duplicate/unknown identities,
repeated faces and nonfinite input fail explicitly. Canonical ordinal ordering
makes caller ordering and reversed connection declarations represent the same
topology/fingerprint. No anatomical geometry is inferred from the fixture graph.

Every connection transfers K/Na amount and electrical current between its two
cleft sites; calyx current is the declared axial conductance times the difference
of the two calyx potentials. Each transfer is applied with opposite signs at the
two endpoints. Membrane K/Na fluxes remain separate from net electrical current,
including electrically neutral transport. Inner currents feed the local cleft;
outer/axon currents remain named external ports. A missing chemical species
is not invented from net current.

For patch `i`, eliminate its calyx voltage using `alpha_i=Ci/(Ci+Co)` and define
`g_i=alpha_i*Co`, `D_i=Ch+g_i`, `beta_i=Ch/D_i`. With local charge-rate right-hand
sides `rS_i`, `rC_i` and one whole-hair right-hand side `rH`:

```
t_i = rS_i + alpha_i*rC_i
dH = (rH + sum(beta_i*t_i)) / (Ca + Cb + sum(beta_i*g_i))
dS_i = t_i/D_i + beta_i*dH
dC_i = rC_i/(Ci+Co) + alpha_i*dS_i
```

This eliminates the same complete capacitance system without a dense solve or
subtraction of nearly equal positive capacitances. The apical and uncovered
hair capacitance occurs once. Even with no spatial connection between two patches,
they remain capacitively connected through their shared hair-cell voltage.
All capacitor charges, charge rates, stored electrical energy and power remain
observable, including charge delivered to the two held bath potentials.

The assembly is a derivative evaluator. It does not yet own channel gates,
integrate a receptor trajectory, solve stationary initial state or implement
an afferent cable. The subsequently authored open-boundary owner is described
below. In particular, current fixtures
are explicit membrane-port inputs, not new neuronal observations or a live
replacement. The next integration must evaluate actual local hair/calyx currents,
qualify the open boundary and its ion ledger, and qualify space/time refinement.

## Verification and remaining work

Prospective component tests cover Fick diffusion, electric drift, equilibrium,
edge reversal, species conservation, gauge shifts, geometry scaling, an analytic
constant-field profile split into two edges, constitutive dissipation, extreme
fields, parameter identity and invalid inputs. Charge tests check all three
Kirchhoff equations, an independently solvable unit-capacitance circuit, internal
current cancellation, bath charges, energy/power and large capacitance ratios.
These tests execute only in a visible independent PowerShell window.

There are 25 prospective local component cases in 21 methods and 14 spatial
cases in 12 methods, plus the solution manifest for a 40-case focused suite.
Spatial tests check one-patch reduction, uniform subdivision at 2/8/32 patches,
species/charge conservation, current direction, neutral transport, shared-cell
coupling, input-order independence, gauge shifts, energy/power and identity errors.
The authoring test-project build succeeds
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

The unlaunched protocol is now explicitly version 2. It retains every original
edge/local input and numerical bound unchanged, and adds 96 spatial cases:
1/2/4/8 compartments, uniform/graded synthetic geometry, four electrical/chemical
state patterns and quiet/localized/distributed membrane-port inputs. No previous
capture was edited or run under the changed protocol.

`analyse-cleft-coupling.py` independently evaluates the direct exponential flux
and builds a complete five-node capacitor graph, solving its three dynamic-node
matrix by pivoted elimination instead of the candidate's Schur formula. Each
input has both 60- and 90-digit Decimal references, with all expected values
saved. Prospective reference tolerance is 1e-40 absolute plus 1e-40 relative;
candidate tolerance is 2e-10 absolute plus 2e-12 relative. It compares every
reported quantity, reconciles the exact input/test inventory and checks runtime
preservation. References use the exact binary64 inputs, avoiding a different
rounded-input problem. No reference arithmetic has executed during authoring.
For spatial inputs, `cleft_spatial_reference.py` constructs the entire
`(1+2N+2)`-node graph, includes all conserved transfers, solves the `1+2N` dynamic
voltages by dense elimination and compares every voltage/concentration rate,
charge, charge rate, energy and connection transfer. It does not reuse the
candidate's shared-hair elimination.

The prerequisite reviewer, frozen preparer and independent PowerShell pipeline
are implemented as described below. After component qualification, choose an
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
   window builds and executes the 40 tests, native arithmetic capture and two
   high-precision references. It verifies prerequisite metadata, exact test
   multiplicities, immutable inputs, numerical and managed runtimes, and final
   C:/D: preservation. It cannot resume an already launched package in place.

Python/PowerShell syntax and static input counts pass. No pending window's logs,
status or process have been inspected. The check proves at most
local constitutive arithmetic; no source flag can promote it to integrated
sensory physiology or balance.

## First completed run: input schema mismatch

After the calyx review and its D: backup passed, `cleft-coupling-20261117-ca4412`
was frozen at `49382968fa151341aa3abd1fb4d6e1ff93c5afa8` and launched in visible
task `DNNE-Cleft-Coupling-8b8a3e51fb91`. The operator subsequently reported its
FAILED footer. `artifacts/cleft-input-review-20261117/review.json` verifies all
2,380 C:/D: files, 1,813 Git files, preserved binaries/runtimes and all 40 passing
focused cases. Native output contains all 468 edge, 60 charge and 96 spatial
records. No independent reference evaluation began.

System.Text.Json serialized three read-only port properties in every charge
input: `hairChargeRatePa`, `cleftChargeRatePa`, `calyxChargeRatePa`. The analyser
incorrectly required a seven-field-only port object. The repair constructs the
expected serialized object explicitly, verifying those three derived values
alongside all seven declared currents, both in the protocol and per-row input.
It does not ignore arbitrary fields. Candidate equations, original input vectors,
fingerprints and numerical bounds remain unchanged. A fresh isolated window
will exercise the repaired comparison. The failed capture remains preserved.

Open-apex reservoir/ion accounting was inspected in deposited `wb/cons1`
(`ck-5`, `cna-140`, `V_cleft`, `0`) but is not implemented in this frozen study.
That extension, actual channel/cleft integration and spatial/time refinement
remain subsequent work; a closed component graph is not the completed receptor.

The corrected package `cleft-coupling-20261117-3076da` is frozen at
`c53783a17e9ab26ab5968bca5d267d0c1a277fa5`. Its initial 1,829 files plus manifest
were D: verified before visible task `DNNE-Cleft-Coupling-4a1705e0b117` launched.
The source equation files, 468/60/96 inputs and reference limits are unchanged.
The operator subsequently reported its completed footer. The saved-data review
below verifies its outcome; the earlier failed `ca4412` remains preserved.

## Corrected closed-cleft result

`cleft-coupling-review-20261117/review.json` verifies all 2,384 files in each
C:/D: copy, 1,813 Git source files, frozen binaries and unchanged runtimes.
All 40 focused cases passed with exact expected method multiplicities. All
468 edge, 60 local capacitance and 96 spatial cases reconciled against both
60- and 90-digit independent references under the original bounds. The largest
candidate error consumed about 1.524% of its allowed absolute/relative error.
Every saved comparison and the analysis summary were reproduced from retained
values; the review executed no model equations or tests. The new review has its
own verified D: backup.

This qualifies the tested closed constitutive/spatial calculations, not open
boundary exchange, coupled channel trajectories, physiological calibration,
afferent firing or Avatar balance. The repeated `ca4412` failure message refers
to the older input-serialization failure, not a regression in this corrected run.

## Open cleft-apex owner: authored after the frozen comparison

`AvatarCleftOpenCoupling` composes the same spatial capacitance system with a
named held perilymph reservoir and explicit opening faces. This implements the
deposited `wb/cons1` boundary convention: K = 5 mM, Na = 140 mM, cleft potential
equal to perilymph at the aperture. Those concentrations are explicit constructor
values, not silent defaults. Other values remain distinguishable fixtures.

The reservoir has no invented finite volume. Each face supplies area perpendicular
to transport and distance from the finite compartment centre to the bath boundary.
The bath is a Dirichlet boundary at the face; the adjacent finite compartment
retains its own concentrations and voltage. A finite-volume half-cell distance
must be supplied for a uniform mesh, not a full centre-to-centre spacing. Actual
aperture area, mesh and anatomical calibration are still unresolved.

The existing constant-field electrodiffusion expression is extracted into an
internal face evaluator. Interior edges still convert its amounts into both
finite-volume concentration rates. The open owner subtracts the same signed
amount/current from the owning cleft compartment and records the delivered
amount/current in its reservoir exchange. Positive flow is cleft to bath;
negative flow is bath to cleft. There is no concentration reset, clipping,
fabricated bath volume, neuronal input or membrane-current substitution.

The spatial solver receives an explicit internal boundary-transfer list. Its
original public `Evaluate` passes an empty list; its original outputs and
fingerprint convention remain unchanged. The open owner's version/fingerprint
includes `shared-hair-open-cleft-perilymph-v1`, the spatial fingerprint, reservoir
identity/composition and all canonical face identities/geometries. The shared
face refactor preserves the expression and operation order, but still needs
regression comparison against the completed closed capture before qualification.

The reservoir exchange keeps these distinct:

- K/Na amounts through the cleft aperture, in attomoles/ms.
- Total aperture current, including the reduced other-ion conductivity.
- Direct hair/calyx membrane current into perilymph, in pA.
- The perilymph side's capacitor charge rate, in pA.
- External holding current supplied into the maintained bath.

For that sign convention:

```
perilymphHolding = perilymphCapacitiveRate - directMembraneCurrent - apertureCurrent
endolymphHolding = endolymphCapacitiveRate - hairToEndolymphCurrent
```

With all named terminals accounted for, their sum equals the external outgoing
axon current. A held bath's capacitor charge rate is not automatically equal to
the direct current delivered to that bath. The electrical energy check retains
both held-bath capacitor terms. This is not a complete metabolic/thermodynamic
model: outer membrane ports do not yet report every ionic species, and the bath
is maintained externally. Aperture K/Na accounting is explicitly limited to that
opening; it is not presented as a complete reservoir chemical balance.

Eleven new `CleftOpenBoundaryTests` cases cover equal-state inactivity, analytic
Fick/drift limits, inward supply, the unit-capacitor graph response, holding-current
signs, split-opening invariance, whole-network charge/species accounting, gauge
shifts/input order, electrical energy and invalid/mutable identity. Test and probe
authoring builds pass with zero warnings/errors. **No new tests or model/reference
evaluation had run at authoring.** The reviewed `3076da` package predates this code and cannot
qualify it. No live Avatar, editor or Unity caller selects the open owner.

Next prepare an independent open-graph comparison retaining the original closed
vectors/results as regression evidence, then couple actual local membrane currents
and gates, stationary initialization and time integration. The aperture derivative
alone does not establish free-cell sensory dynamics or afferent firing.

## Independent open-boundary qualification workflow

The new workflow is authored but not executed. `cleft_open_protocol.py` retains
each of the 96 original synthetic spatial inputs with either one opening face
or two half-area faces at the last compartment: 192 complete cases. Total aperture
area is 0.02 um², centre-to-boundary distance 0.25 um, and the explicitly named
perilymph reservoir has K=5/Na=140 mM. These are declared numerical fixtures,
not an anatomical aperture calibration or a new physiological fit. Original
closed inputs and all absolute/relative limits remain unchanged.

`CleftOpenStudy` / `--cleft-open CLOSED_INPUT OPEN_INPUT NEW_OUTPUT` records the
complete open evaluations and re-evaluates the original 468 edge, 60 local charge
and 96 spatial cases. `analyse-cleft-open.py` requires all five closed output files
(input, summary and three record streams) to match the reviewed capture byte for
byte, including fingerprints and floating values. This is a separate regression
criterion; a tolerance match cannot replace it silently.

For each new open case, `cleft_open_reference.py` independently constructs the
entire capacitor graph and solves its dynamic-node matrix by dense elimination.
Aperture flux uses the direct exponential expression and explicit face geometry,
without a reservoir volume or the candidate's stable Bernoulli/Schur evaluation.
Both 60- and 90-digit references are retained. Every reported voltage/concentration
rate, charge/rate, electrical energy, internal/aperture transfer and reservoir
holding-current quantity is compared. Reference agreement uses 1e-40 absolute
plus relative; candidate agreement uses 2e-10 absolute plus 2e-12 relative.

The sequence is:

1. After the operator reports closed-cleft completion, `review-cleft-coupling.py`
   verifies full C:/D: files, frozen source/binaries, runtime, exact 40-test
   inventory and all saved 60/90-digit/native comparison values. It reproduces
   the saved analysis without reevaluating a candidate or reference equation.
2. Back up that new review to D:. `prepare-cleft-open.py CLOSED_REVIEW` requires
   its passing outcome and pinned source revision, preserved original inputs,
   closed evaluator/reference/test sources, existing hair/calyx/live sensory
   sources and exact runtime equality. The transport/spatial extension is qualified
   through the required closed replay plus new open cases, not claimed unchanged.
3. From clean pushed source, freeze the new package and verify its initial D:
   backup. `start-cleft-open-window.ps1` owns 51 focused cases, 624 closed replay
   records, 192 open evaluations, both independent precisions and final backups.
   It starts a separate visible PowerShell window with no live monitoring.

The capture project builds with zero warnings/errors; five Python files and the
PowerShell launcher parse, and static inputs/test inventory match 192/51.
The closed reviewer has now passed as recorded above; preparation and visible
launch follow from that reviewed evidence. Rubral `30755e` and skull `1e2f21`
still await confirmed completion. The operator says a window remains working;
no pending output/status/process was inspected.
Passing this workflow would qualify an instantaneous open spatial operator and
closed-code preservation, not a time-integrated receptor, afferent or balance.

The subsequent actual membrane-current/gate assembly is now authored separately
as `AvatarVestibularCleftMembranes`; see
`DNNE_Cleft_Membrane_Coupling_2026-11-17.md`. It is not selected by this component
workflow. Its 12 new cases and independent coupled derivative/time qualification
remain unrun/unimplemented as detailed there.
