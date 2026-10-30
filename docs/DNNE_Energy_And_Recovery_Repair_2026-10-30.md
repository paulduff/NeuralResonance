# Energy supply and membrane recovery — 30 October 2026

The candidate replaces the unsustainable local reserve refill rule with bounded,
substrate-limited production. It adds voltage-dependent recovery currents and
adjusts the provisional HH input-to-current conversion. It is ready for an
isolated comparison, not qualified as a human cerebellar cell or physical balance.
No live DNNE, Unity or editor service is redeployed by this experiment.

## Energy mechanism and boundaries

`NeuralEnergyBudget` uses normalized reserve units, not ATP molarity or joules.
Reserve starts at 1 and may fall to zero; the former .2 floor is removed.
Production capacity is .45 units/s oxidative plus .05 units/s glycolytic.
Glucose availability and perfusion limit both, oxygen limits oxidative production.
Demand has .01 units/s maintenance, .0028 per spike, absolute receptor/inward-channel
current cost of .000002 units per current-density-ms, and the existing calcium/
plateau proxy cost of .000012 per trace-ms. These coefficients are provisional.
Opposite currents no longer cancel their energy charges.

Demand is filtered over 200 ms. Production activation follows demand and reserve
deficit over 250 ms; synthesis is capped by capacity and extracellular availability.
It is not an instantaneous refund of spike costs. Every cell records requested
demand, production, actual consumption, unmet demand and overflow at the upper
boundary. The invariant is `reserve = 1 + produced - consumed - overflow`;
`requested = consumed + unmet`. Neither missing fuel nor numerical clipping can
create an unreported reserve refill.

The former generic fatigue offset and reserve-dependent input gain are removed.
An ATP-sensitive potassium reduction provides outward current with a potassium
reversal potential. Its density/affinity are provisional, not a fitted KATP channel
for every cell type. HH integrates that conductance within the membrane solver;
LIF/Izh use the corresponding reduced current approximation. Fixed ion gradients,
pump failure, regional vasculature, terminal ischemic injury and full ATP chemistry
remain unmodeled. Recovery here means reversible model deficits, not survival
after an equivalent duration of real ischemia.

Activity-linked ATP production has experimental support, but the hippocampal
terminal experiments do not calibrate this whole-cell model.
[Rangaraju et al., 2014](https://pmc.ncbi.nlm.nih.gov/articles/PMC3955179/).
Metabolic inhibition can recruit outward KATP current in a preparation-dependent
way; it is not universal evidence for the same channel density across DNNE.
[Matsumoto et al., 2002](https://pmc.ncbi.nlm.nih.gov/articles/PMC2290338/).

`NeuralSubstrateSupply` is separate from visceral spikes, rewards and the sleep/wake
score. Avatar preserves its existing energy/hydration/tissue physiology and adds an
explicit reduced availability estimate: glucose availability saturates at 400 kJ
stored energy, oxygen at saturation .98, and perfusion is hydration/.5 capped at 1,
times tissue integrity. These are declared proxies, not blood glucose, pressure or
local ATP measurements. The canonical Avatar API and generated Unity physiology
share these equations; existing labyrinth, muscles and sensory routing remain.

The coordinator timestamps accepted supply with its simulation and monotonic
receipt clocks. Supply older than 1,000 simulated ms or 5,000 wall-clock ms is stale;
missing/stale supply has zero availability and an explicit source label. Reset
clears it. Standalone direct-cell diagnostics explicitly use a reference bath.
Live StructureEngine does not silently substitute that bath for absent body input.
Consequently, future connected startup must establish body supply before a long
brain-only wait; existing launch readiness alone does not establish nourishment.

The editor shows normalized mean/minimum reserve, source and unmet demand, with
the existing current/stale snapshot distinction. Aggregates weight mean reserve
by cell count and retain missing telemetry rather than borrowing healthy status.

## Recovery currents and operating regime

The bare reference HH model is retained when no channel profile is supplied.
Selected HH populations add distinct, continuously evolving gates:

| Population | HCN/Ih | Additional inward current |
|---|---|---|
| Purkinje, reduced vermis/lobule population | .1 mS/cm², reversal -30 mV; KGR gate | persistent sodium, 4 mS/cm², reversal +50 mV; De Schutter/Bower gate |
| Inferior olive | .12 mS/cm², reversal -43 mV; olivary gate | low-threshold calcium, .75 mS/cm², reversal +120 mV; olivary activation/inactivation |

All gates evolve with voltage inside coupled RK4 integration. There is no
post-inhibition timer, restart pulse, forced spike or voltage reset. NaP kinetics
include the source model's Q10=3 conversion from 37°C to the reference HH host's
6.3°C. The combined channel set still lacks a validated common physiological
temperature and full compartment/calcium handling. It is a hybrid reduction,
not the full published models. The vermis service remains a mixed-population
abstraction; it is not anatomically equivalent to a Purkinje-only nucleus.

HCN can limit Purkinje bistability in an experimental preparation.
[Williams et al., 2002](https://pmc.ncbi.nlm.nih.gov/articles/PMC2290163/).
The gate equations are independently implemented from
[KGR Ih equations](https://raw.githubusercontent.com/ModelDBRepository/48332/master/ihpkj.mod),
[De Schutter/Bower NaP equations](https://raw.githubusercontent.com/ModelDBRepository/149000/master/NaP.mod),
and the [olivary model equations](https://raw.githubusercontent.com/MRIO/OliveTree/master/functions/simulation/IOcell.m).
The published complete models are not imported. Mathematical equations inform
this implementation; channel interactions and densities still require calibration.
The olivary evidence includes low-threshold calcium/HCN rebound effects;
[Loyola et al., 2023](https://pmc.ncbi.nlm.nih.gov/articles/PMC10393294/).

The normalized HH pacemaker conversion changes from 10 to 12 µA/cm² per input
unit, continuously during ordinary operation. This is a provisional operating-regime
choice informed by the preceding reference recovery probe and the short candidate
checks, not an extra input triggered by silence. Its effect is explicitly isolated
by the next experiment. It must not be credited to HCN without the ablation result.

## Preparation evidence and limits

The energy-only/HCN pilot sustained quiet activity but failed post-inhibition
recovery. Diagnostic HCN density multipliers 10 and 30 did not repair all profiles;
neither multiplier is used in the candidate. Adding the distinct inward currents
improved recovery, but the original current conversion still left the sampled
Purkinje cells silent in the short return window. A NaP density of 6 was also
examined and discarded; the candidate uses 4.

In `artifacts/energy-recovery-preparation-20261030/quick-drive12`, four cells per
profile were observed for six simulated seconds at .25 and .125 ms. At .25 ms,
the final one-second candidate quiet/challenge rates were respectively:

| Profile | Quiet Hz | After inhibition Hz |
|---|---:|---:|
| Vermis | 60.50 | 60.00 |
| Purkinje | 58.00 | 42.75 |
| Olive | 72.50 | 72.75 |

Inhibition suppressed all three to zero during the sampled inhibitory phase.
Fuel withdrawal suppressed activity and replenishment allowed it to return.
Purkinje recovery was slower/partial in that short window. These are small pilot
samples, not final acceptance. In particular, the olive's high firing rate is
not validated olivary physiology. The preparatory captures are exploratory, not
a replacement for the frozen previous-model baseline or the committed final run.

The regression suite passes 1,023 tests, including conservation, limited glycolysis,
fuel withdrawal/restore, gate history, physical-step composition, supply freshness,
Avatar/Unity equation parity, tick serialization, incompatible-save preservation,
and a supported Purkinje inhibition/recovery check at both step sizes. Nine editor
display tests and JavaScript syntax checks pass. The generated Unity physiology
matches its canonical source. A short matrix preflight validates execution and
accounting; it cannot establish sustained physiological recovery.

## Independent next experiment

The frozen package runs 120 fresh-cell conditions: three profiles, five mechanism
controls, two integration steps (.25/.125 ms) and four input/supply conditions.
Each full condition uses 32 stable unique cell indices for 60 simulated seconds.
Controls are candidate, HCN blocked, slow inward current blocked, both new currents
blocked, and prior pacemaker conversion. Ablations exist only in the isolated
diagnostic, never as live clamping or control settings.

Six ten-second phases provide quiet baseline, glutamate challenge in phase 1,
GABA challenge in phase 3 and a twenty-second return period. Separate oxygen and
glucose withdrawal cases remove substrate in phases 1–2 and restore it in phase 3;
they have no imposed receptor challenge. Phases are zero-based. It saves per-cell
spike counts, first/last spike times, reserve and voltage extrema, gate and energy
traces, all production/debit/boundary terms, and numerical-step comparisons.

Completion/accounting and response quality are separate. Inspect active-cell
fractions, sustained quiet firing, inhibitory suppression, recovery latency/rate,
fuel sensitivity and step convergence. A positive mean must not hide silent cells.
The previous v2 support-study package and reference-recovery review are preserved
and hash protected. The same-energy channel/drive ablations supply the controlled
comparison for the new mechanism; older captures have different durations and
must not be treated as a matched 60-second trial.

Dynamics version `substrate-energy-hcn-v3` changes the save fingerprint. Earlier
schema-4 saves are retained and rejected as incompatible, never silently reset or
overwritten. The run loads no weights, contacts or live brain state. Source, binaries,
checks and inputs are frozen and mirrored to D: before launch; completed results
are hash-verified to D: by the independent visible PowerShell window. No live
monitoring follows launch. Connected learning/decoder qualification and physical
fall recovery remain later work.
