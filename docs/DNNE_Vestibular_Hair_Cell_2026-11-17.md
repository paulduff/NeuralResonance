# Isolated vestibular hair-cell candidate

Avatar needs a physical sensory transduction path before vestibular activity can
serve as neuronal feedback. The existing clocked vestibular mechanism retains
physical sample identity and time, but its cupula output is a reduced angular
velocity signal in rad/s. It is not measured bundle displacement in nanometres.
The existing normalized population activation also is not an afferent spike.

`AvatarVestibularHairCell` adds an isolated, nonspiking receptor-cell candidate.
It accepts explicitly held physical bundle displacement, integrates voltage and
three membrane gates, and retains the charge through seven ionic current terms.
No live Avatar, Unity, editor or DNNE host selects it. No body-to-bundle conversion
factor or sensory action-potential generator is invented to connect the stages.

## Source and reduction

The equations are independently expressed from Govindaraju et al., PNAS 2023,
[doi:10.1073/pnas.2207466120](https://doi.org/10.1073/pnas.2207466120), supporting
information S1/S2 and the hair-cell channel descriptions on pages 16–18. The
source audit is `DNNE_Vestibular_Equation_Audit_2026-11-17.md`. Original evidence
is pinned at authors' repository revision
`47d499449df66c6676ee43cbb08ea1995469a817`, with original licensing retained.
No COMSOL source code or executable is imported into Avatar.

This candidate selects the mutually supported hair-cell MET, K,L, calcium,
HCN1 and Na/K-pump terms. It does not select the disputed calyx HCN2, cable or
nodal sodium equations. A fixed extracellular bath and fixed intracellular ion
reservoirs explicitly replace the spatial cleft and ion balance in this isolated
reduction. There is no claim to reproduce the full deposited COMSOL model.

| Quantity | Declared value or treatment |
|---|---|
| Basolateral / cleft-facing area | 340.21 / 294.63 square micrometres |
| Capacitance | 3 pF apical + 0.01 pF/um² over the entire basolateral area = 6.4021 pF |
| Whole-cell MET / K,L / Ca / HCN1 conductance | 5 / 80 / 0.5 / 4.2 nS |
| HCN1 current split | 4/5 potassium, 1/5 sodium |
| Na/K pump | Source density 1000/um² on cleft-facing area; 2 K inward per 3 Na outward |
| Outside K / Na / Ca | 5 / 140 / 1.3 mM |
| Inside K / Na / Ca | 150 / 12 / 0.001 mM, fixed |
| Endolymph / basolateral outside potential | +5 / 0 mV; explicit separate voltage references |
| Temperature | Unscaled source room-temperature kinetics |
| Initial voltage | −75 mV, gates conditioned to that voltage, zero accumulated charge |

The initial state is not asserted to be free electrical equilibrium. Quiescent
settling is retained, not hidden in an unrecorded initialization. K,L and HCN1
time constants are voltage dependent; the calcium gate uses the source's 0.6 ms
approximation. Gate powers are one as in the retained hair-cell expressions.
Whole-cell conductances must not acquire another area multiplier; pump density
does require its declared membrane area. Above-calyx membrane contributes
capacitance, while this source assigns its channel currents zero.

Positive current denotes outward positive charge. The equation is
`C dV/dt = -sum(I)`, with each gate obeying `(gate_inf(V)-gate)/tau(V)`.
Seven charge integrals retain MET, K,L, Ca, HCN-K, HCN-Na, pump-K and pump-Na
separately. Both pA·ms and pF·mV are femtocoulombs, giving an independent charge
ledger. The pump has no implemented ATP supply: fixed reservoirs do not qualify
metabolic sustainability. MET adaptation, vesicle release, spatial cleft/calyx,
afferent membrane, efferent modulation and human physiological calibration
remain absent from this candidate.

## Ownership and failure behavior

Version `isolated-type-I-hair-cell-fixed-bath-v1` plus the versioned fixed-step
RK4/compensated-addition solver enters the fingerprint with parameters, bath,
initial voltage and declared clock. Cell and session identities are explicit.
One owner advances integer cell steps from an explicit epoch. Call partitioning
cannot change integration boundaries. The caller declares complete held physical
input; the cell does not infer silence from a missing body sample.

No probability clipping, voltage clamps, silent time-step changes or restart
are used. An invalid numerical stage preserves its rejected candidate and the
last accepted state, then faults the cell. Captures clone state and compensation
arrays. There is no checkpoint restore or production sensory transport yet.
The provisional maximum accepted step of 0.05 ms is a numerical configuration
limit, not an accuracy qualification.

## Prospective qualification

All execution belongs to the visible independent PowerShell window. Inline
authoring checks are builds, syntax parsing and static test inventory only.
The frozen protocol fixes the numerical bounds before observing results:

- 47 exact .NET cases: 23 new hair-cell cases, 16 physical-clock regressions,
  seven existing labyrinth regressions and one solution manifest check.
- 7,260 channel arithmetic cases cover three baths, 121 voltages, five bundle
  displacements and four independent gate initializations.
- Six complete 1,000 ms histories: quiet, positive, negative, strong, reversal
  and paired displacement. Native steps are 0.03125, 0.015625 and 0.0078125 ms,
  with 8,001 recorded endpoints per trajectory. Positive, negative and strong
  inputs are +300, −300 and +1,000 nm. Changes occur only at declared boundaries.
- Twelve reference controls: passive RC and zero-conductance checks at three
  solver settings; three complex-step versus symmetric-difference Jacobian
  checks; three instantaneous electrical/pump conservation checks.
- Eighteen independent Python references: DOP853, tighter DOP853 and Radau for
  each input history. All accepted nodes, full states, segment boundaries and
  failures are retained. Radau receives a complex-step Jacobian; the seven
  charge-integral columns are explicitly zero, avoiding finite-difference
  perturbation escalation in state-independent directions.
- Reference agreement: 1e-6 mV, 2e-8 in gates, 1e-4 fC. Candidate bounds:
  0.005 mV, 2e-5, 0.05 fC; finest bounds 0.00005 mV, 2e-7, 0.0005 fC.
  Each halving must reduce error to at most half, subject only to prospective
  roundoff floors 1e-9 mV, 1e-11 and 1e-7 fC. Charge ledger bound is 1e-7 fC.

Independent references restart only at prescribed input changes, carrying every
state across the boundary. Endpoint state uses the preceding held input; currents
reported at that endpoint use the newly available input. No future displacement
acts backward over the completed interval.

Packaging requires clean pushed source, exact source evidence and D: copies,
original Avatar pathway preservation, exact test-method multiplicity, frozen
executables and a complete numerical dependency inventory. This is a new baseline
using the currently installed Python binary build, not a claim of equivalence to
the older binary build. Pre/post inventories must match exactly. Built assemblies
and the managed runtime are also hashed. A fresh directory is mandatory; failures
and original numerical bounds are retained. Initial and final backups are verified.

Passing permits work on a declared physical displacement interface and local
hair-cell-to-afferent coupling. It does not qualify afferent firing, sensory
physiology, live brain authority, balance or recovery after a fall.

## Preparation status

The candidate and native capture builds succeed with zero warnings/errors.
Python and PowerShell syntax and the 47-case static inventory pass. These are
authoring checks; no new model/test has run inline. Numerical results are pending
execution and subsequent operator-reported review.

The study is now launched as `vestibular-hair-cell-20261117-959475`, frozen at
revision `59b586bdb85a190797b077a9644b2e88496012f0`, in visible task
`DNNE-Hair-Cell-47ad90abec01`. Its initial 1,801 files plus manifest were verified
on D:. No post-launch output, status or process has been inspected. Await the
operator's completion report before reading results; launch is not a pass.
