# Purkinje calcium candidate and isolated circuit comparison

The operator requested an olive-to-Purkinje contact test, then explicitly asked
to replace the direct coincidence shortcut with a biological calcium mechanism.
The new Purkinje host uses `PurkinjeCalciumLTD`. Other nuclei keep their existing
rules. This is a reduced mechanistic candidate, not a fitted human Purkinje cell
or a reproduction of any complete published model. No world or live brain is
started by this experiment.

## What changed

Previously, an incoming olive event directly depressed granule contacts whose
20 ms presynaptic trace remained above 0.1. The new rule never decrements a
weight merely because the sender is the olive. Actual parallel-fibre releases
activate a contact-local metabotropic receptor state and delayed IP3 production.
The receiving dendritic voltage drives P-channel gates and calcium entry. IP3
and calcium coactivate finite internal-store release. Calcium clearance, store
refilling and receptor inactivation evolve in biological time. A local calcium
integrator drives persistent LTD induction and slow AMPAR efficacy expression.

The first short development check exposed insufficient climbing-fibre voltage
coupling in the existing membrane. A new receiving compartment now represents
the climbing fibre's dense AMPA contact field. Its receptor occupancy is finite,
its conductance rises and decays, and voltage follows conductance and axial
coupling. Its axial current reaches the existing somatic membrane and absolute
current contributes to the existing energy ledger. No spike, voltage reset,
calcium pulse or weight change is imposed by the experiment.

The calcium channels currently read that compartment's voltage. Their charge
and calcium-dependent potassium feedback are **not** yet coupled back into a
complete active dendritic membrane. The underlying HH soma, background drive,
ordinary receptor scaling and bounded generic dendritic states remain reduced.
The new compartment is a passive electrical reduction, not a full calcium spike
generator or dendritic tree. Chemical pump costs are not separately calibrated
into ATP demand. Reference bath conditions in this experiment do not assess
energy-starved Purkinje chemistry. These are material remaining limitations.

## Parameters and sources

Primary sources:

- [De Schutter and Bower model archive](https://github.com/ModelDBRepository/7176):
  P-channel alpha/beta equations and AMPA 0.5/1.2 ms rise/decay constants.
- [Doi et al., 2005](https://pubmed.ncbi.nlm.nih.gov/15673676/): delayed PF/IP3
  signalling and calcium-dependent internal release as a timing mechanism.
- [Sarkisov and Wang, 2008](https://pmc.ncbi.nlm.nih.gov/articles/PMC6671165/):
  order-sensitive IP3/calcium coincidence. Chemical eligibility is not a
  universal symmetric spike-pair window.
- [Tanaka et al., 2007](https://doi.org/10.1016/j.neuron.2007.05.014): calcium
  amplitude/duration dependence, approximately 0.56 s leaky integration,
  cooperative LTD and slow expression.
- [Coesmans et al., 2004](https://pubmed.ncbi.nlm.nih.gov/15541316/): calcium
  controls the direction of PF plasticity. Low-calcium LTP is **not** implemented
  here; calcium alone cannot justify a complete bidirectional learning rule.

| Component | Candidate setting | Status |
|---|---|---|
| P-type activation/inactivation rates | De Schutter/Bower reference equations, converted from V/s to mV/ms | Reference gates; not human calibration |
| Climbing AMPA rise/decay | 0.5/1.2 ms | Reference kinetics |
| Effective climbing conductance / axial conductance | 4 / 0.2 in reduced capacitance-normalized units | Provisional density/geometry conversion |
| mGluR release occupancy / decay | `1-exp(-0.35*quanta)` / 80 ms | Provisional contact conversion |
| IP3 basal / response time / receptor-driven increment | 0.1 uM / 150 ms / 3.75 uM | Reduced timing candidate, not Doi's full reaction network |
| Rest calcium / store | 0.05 / 5 uM cytosolic-volume-equivalent | Provisional geometry/buffering |
| Free calcium entry | `0.001*m*h*max(0,120-V)` uM/ms | Provisional conductance/buffering conversion |
| Store release | 0.02/ms, IP3 Hill3 K1uM, Ca Hill2 K1uM | Provisional reduced IP3R kinetics |
| Store inactivation | `0.3/(0.3+Ca)`, 100 ms | Provisional reduction |
| Calcium clearance / store uptake | 90 ms / 0.002 per ms with available-store fraction | Reference-inspired clearance, provisional uptake |
| Calcium integration | 560 ms | Tanaka leaky-integrator timescale |
| LTD dose response | Hill3.8, half-point1.8uM, maximum0.372 | Adapted from an uncaging condition; using filtered excess calcium is our reduction, **not** a reproduced fit |
| Persistent induction | Maximum attained dose response | Explicit approximation to molecular consolidation; not a simulated kinase feedback network |
| Efficacy expression | 390 seconds toward induced target | Inspired by slow experimental expression; not universal |

All integration uses substeps no larger than 0.125 ms. Cytosolic/store transfer
conserves calcium; entry and extrusion are accounted separately and independently
checked. Candidate LTD uses a double-precision efficacy state and finite response
fraction. It bypasses the old numerical per-event plasticity budget. Other rules
retain that budget; this change does not biologically validate them.
The existing 0.05 minimum persisted efficacy remains a numerical compatibility
limit, explicitly applied to the candidate target as well; it is not a measured
biological lower bound. Fresh-test contacts start at1, far from that limit.

## Isolated test

`tools/PosturalCellProbe --olive-purkinje` runs one olive and two Purkinje cells
inside production StructureEngines with internal diagnostic population sizes.
Only fresh empty absolute state directories with isolated identities are allowed.
The public constructor still creates the normal populations. Diagnostic saves
have an extra fingerprint and cannot load into a live full nucleus.

The matrix is 4 rules/ablations x 8 input plans x 2 oscillator offsets x 2 steps
= **128 conditions**, each **240 biological seconds**. Numerical steps are 1 and
0.125 ms. Offsets 0/60 ms are deterministic oscillator conditions, not independent
biological subjects. The short preflight is 4 seconds per condition. `--quick`
uses only the first offset (64 conditions), and is only valid with `--smoke`.

Variants: legacy coincidence learning, calcium candidate, blocked P-channel
entry, blocked IP3-dependent store release. The legacy comparison uses the **same
new receiving compartment**, isolating the learning-rule difference; it is not a
replay of the previous membrane implementation. The earlier frozen packages
remain available unchanged.

Plans: PF leads olive stimulation by100ms,20ms,500ms, or follows by100ms; disconnected
olive; unstimulated olive; four-contact inhibition or glucose withdrawal10–30s.
These are stimulus offsets, not presumed actual PF/CF arrival intervals. Production
conduction delays and every arrival timestamp are retained.

Each full case has58 four-contact olive probes at2000+offset+i*997ms. PF fixtures
are five releases10ms apart at each named contact. Active and unrelated-cell PF
contacts receive matched inputs; another same-cell PF contact is delayed300ms;
a dormant contact receives only the100ms establishment event. A non-granule
glutamate contact on the taught cell checks source specificity. All five contacts
are established by a real initial input, never fabricated in the learned store.
The last180s largely observe slow expression and recovery. Four minutes may
still be insufficient for fully expressed LTD; small changes are retained.

Every actual olive axonal event is delivered once, including burst tails, except
in the explicitly disconnected control. No source budget, event merge, forced
spike or artificial learning pulse is used. PF inputs are diagnostic axonal
fixtures, not a simulated granule population. The single feed-forward circuit
does not qualify gap junctions, cerebellar feedback, movement or balance.

Saved evidence includes all fixture, olive, Purkinje and receiving events, contact
identity and before/after snapshots, 20ms electrical/energy frames, and each
contact's calcium/IP3/store/dose/weight trajectory. Validation independently checks
the complete schedule, event conservation, cross-cell ownership, no direct candidate
weight change on arrival, calcium mass and energy ledgers. It reports successful
accounting separately from biological quality. Compare dose/weight differences,
unrelated-contact effects, ablations and numerical sensitivity after completion;
do not call every nonzero depression a useful teaching signal.

## Preservation and operation

The Purkinje save fingerprint includes the new electrical and chemical versions.
Existing saves are rejected without modification. New persistence remains a
learned-weight warm start: voltage and transient chemistry are not resumed.
The editor's Purkinje rule label and runtime model-status explanation are updated.
Avatar/environment physiology and authority are unchanged.

Freeze committed source, binaries, provenance and successful preflight/validator
tests, verify protected C:/D: evidence, and launch the frozen script in visible
PowerShell. No live monitoring; inspect results only after the operator reports
completion. The window saves logs/status and verifies its D: copy at the end.
