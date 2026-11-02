# Inferior olive: isolated-cell calibration candidate

The v3 energy comparison sustained approximately 73 Hz quiet olivary firing. That
is an unsuitable teaching input for the cerebellum. Dynamics
`substrate-energy-olivary-v4` replaces the olive's generic driven squid-HH cell
with a three-compartment olivary reference. The new candidate has endogenous
subthreshold oscillations and evoked somatic spikes with axonal bursts. It is
**not yet qualified for connected learning, balance, or human physiology**.

## Biological reference and numerical choices

The independently written equations follow [de Gruijl et al. (2012)](https://journals.plos.org/ploscompbiol/article?id=10.1371/journal.pcbi.1002814),
using the vanilla-cell parameter path accompanying [Loyola et al. (2023)](https://elifesciences.org/articles/83239).
The latter's Figure 7 and authors' code provide an approximately 8 Hz isolated
oscillator. Slow voltage oscillations are distinct from action potentials;
isolated cells can oscillate without spontaneous spikes. A silent, unresponsive
cell is not a successful calibration.

Author code was inspected at [OliveTree revision
223253255359b86be98126ef54b37c560d9968d8](https://github.com/MRIO/OliveTree/tree/223253255359b86be98126ef54b37c560d9968d8):
`figures/Loyola_PRC.m`, `functions/analysis/IO_PRC.m`, and simulation files
`IOcell_vanilla.m`, `createDefaultNeurons.m`, `initNetState.m`, and receptor
kinetics. Their archived reference copies and README are in the experiment's
preparation directory. MATLAB source is not compiled or incorporated into the
runtime. The references contain generalized, mixed-species fitted mechanisms,
not a temperature-matched human olive. No squid-temperature conversion is applied.

| Component | Parameters / mechanisms |
|---|---|
| Soma | Na 150; Kdr 9; fast K 5; low-threshold Ca 1.1 |
| Dendrite | high-threshold Ca 4.5; calcium-dependent K 35; HCN 0.12 |
| Axon | Na 240; K 240 |
| Leak / coupling | leak 0.016 in each compartment; internal coupling 0.13; area fractions 0.25 and 0.15 |
| Reversals | Na +55, K -75, Ca +120, HCN -43, reference leak +10 mV |
| Synaptic maxima | AMPA dendrite 0.12; fast GABA dendrite 0.1; slow GABA soma 0.05 |

Conductances are mS/cm², capacitance is 1 µF/cm², time is ms. The unusual positive
effective leak reversal is retained from the reference, not interpreted as a
single biological leak channel. Axonal K is **240**, from the actual default
parameter function called by Figure 7; the vanilla function's stale comment
says 20. These values must not be interchanged without a documented comparison.

Fourteen state variables include compartment voltages, dynamic channel gates and
dendritic calcium. Sodium activation is instantaneous as in this published
reduction; sodium inactivation and potassium/calcium/HCN gates evolve in time.
Coupled RK4 uses at most 0.025 ms, with smaller steps for stiff conductance or
rapid voltage change. Rate singularities have analytic finite limits. The
reference author's Euler update is not reproduced bit for bit: calcium uses the
current state's calcium current in the coupled ODE. Initial conditions are
explicit; there is no hidden warm-up, membrane reset, voltage clamp or forced spike.
Synaptic conductances and the energy ledger also advance at most every 0.025 ms.
An intermediate pilot exposed aliasing when a 0.18 ms AMPA rise was held constant
through a 1 ms host step: the four-contact response doubled. The coupled update
removes that artifact; a separate inner-step refinement test checks its accuracy.

The runtime reports the soma voltage and emits each axonal upward 0 mV crossing.
The probe also records somatic crossings separately. More than one outgoing
crossing within an outer step is an error, not a silently dropped event. HH host
packets now subdivide to at most 1 ms; the previous engine-wide 4 ms cap conflicted
with the membrane's existing 1 ms event limit. Reduced LIF/Izh cells retain their
existing maximum. Packet subdivision is covered by a production-engine test.

## Inputs, supply and boundaries of the repair

Glutamate/GABA events release transmitter for one physical millisecond. AMPA and
GABA occupancy follows the reference opening/closing equations. Somatic GABA has
the reference frequency-dependent slow tail. Its auxiliary calcium-like variable
retains the author's numerical baseline and fitted constants; it is not asserted
to be a measured cytosolic concentration. Dendritic membrane calcium is separate.

Each incoming contact has independent receptor occupancy, identified by projection,
source instance/cell, transmitter and tract flags, including the legacy projection
fallback used by contact learning. Conductances add across contacts. Quanta scales
released transmitter, allowing saturation per contact without capping the entire
cell at one contact's conductance. Applying a published aggregate conductance to
each simulated contact is an **explicit provisional input-density conversion**;
the one/four/sixteen-contact conditions test its consequences. Both GABA locations
are currently represented for each GABA contact; anatomical contact placement is
not yet fitted. This is not a claim to reproduce Figure 7's 60 ms optogenetic
inhibitory stimulus: our paired condition uses discrete 1 ms synaptic releases.

The olive bypasses generic tonic/pacemaker sine waves, voltage kicks, generic
dendritic plateau/AIS shortcuts, generic excitability/homeostatic gains, and
generic neuromodulator membrane currents. Its low/high calcium, HCN, Na and K
currents produce its dynamics. Locally arriving modulation, contact learning and
identity remain in the existing engine. Their broader reduced models have not
been biologically requalified by this cell experiment.

The v3 substrate-limited energy ledger remains active in production, including
outward potassium feedback and per-axonal-spike cost. Additional calcium, HCN and
synaptic current demand uses compartment-area weighting. Energy units and the
extra-current cost conversion remain normalized proxies, not measured ATP flux.
The separate membrane-only reference omits the energy model as an isolated
diagnostic control; it cannot be selected by a live service setting. Channel
ablations and altered CaT values are likewise probe-only constructor choices.

Avatar already supplies glucose, oxygen and perfusion availability. This repair
uses that contract and changes no body physics or supply proxy. The editor's
existing model-status telemetry identifies the new olive and its limitations;
existing energy source/reserve/unmet-demand display is retained. No live service
or world is started by the cell experiment.

Electrical gap-junction coupling, population heterogeneity, in-vivo background
input, connected climbing-fibre/LTD behavior, pump failure and human physiological
fitting remain open. Fixing the isolated membrane cannot by itself qualify the
whole olivary nucleus or Avatar recovery.

## Validation before the full run

The final short five-second pilot compared production and membrane-only cells at
1/0.25/0.125 ms. Quiet cells had no spikes and approximately **8.20 Hz** subthreshold
oscillations. A one-contact 20 Hz input stayed subthreshold; four contacts evoked
9 somatic / 27 axonal spikes; sixteen contacts evoked 18 / 70 during the pilot.
All three steps agreed on these counts. These are diagnostic inputs, not a desired
live firing rate. Inhibition changed the membrane trajectory and subsequently
released it; the full run will quantify the later response.

Tests cover gate singularities, retained oscillations without imposed drive,
evoked unreset spikes, finer internal-step convergence, independent receptor
contacts, physical-time receptor tails, event accounting at 1/0.25/0.125 ms,
energy conservation, packet subdivision and rejection/preservation of v3 saves.
The old test requiring the olive to fire tonically within six seconds was replaced
by explicit oscillation and evoked-response tests; it encoded the wrong criterion.
The complete regression suite passed **1,034 tests**. The analysis validator also
accepted a real smoke fixture and rejected corrupted energy, missing samples,
incorrect incoming/outgoing counts and duplicated conditions.

## Frozen PowerShell experiment

The full matrix has **180 conditions**, each a fresh deterministic isolated cell
for 60 simulated seconds:

- Six variants: production, membrane-only reference, CaT 0.7, CaT 1.5, no HCN,
  and no CaT. These parameter controls do not change live configuration.
- Ten protocols: quiet; 20 Hz excitation through one/four/sixteen contacts;
  inhibition; GABA-to-glutamate pairs separated by 50/100/150 ms; glucose
  withdrawal/restoration; oxygen withdrawal/restoration.
- Three outer steps: 1, 0.25 and 0.125 ms, with the same inner membrane solver.

Baseline is 0–10 s; excitation is 10–20 s; inhibition is 20–30 s. Pair starts occur
every 997 ms during 10–40 s to sample different oscillator phases. Fuel withdrawal
is 10–30 s and restoration 30–60 s. Inputs and timings are saved in `protocol.json`.
Repeated deterministic cells are not presented as independent biological subjects.

All crossings, 2 ms voltage/gate/calcium/energy traces, 10 s segments and input
counts are saved. The analyser checks complete coverage, finite/bounded states,
ledger conservation, incoming events and outgoing counts. Exploratory physiology
screens include sparse quiet firing, an STO band of 1–12 Hz and evoked output;
these deliberately broad screens do **not** confer physiological qualification.
Inhibition/recovery, paired timing, ablations, fuel response and numerical
sensitivity must be assessed from the completed records.

`tools/prepare-olive-calibration.py` freezes committed/pushed code, binaries,
references, preparation checks and the previous results. It inherits the full
protected-save manifest, adds the completed energy run/review and checks both C:
and D: hashes. A unique D: package backup is verified before launch and again at
completion. Dynamics v4 changes the save fingerprint; previous saves are never
loaded, rewritten, silently upgraded or deleted. Schema-4 state remains a learned
weight warm start, not exact membrane continuation.

The independent visible PowerShell performs a short preflight then the full
matrix. It writes native console logs explicitly, leaves its window open, and
distinguishes FINISHED from physiological success. **No live monitoring.** Inspect
the saved results only after the operator reports completion.
