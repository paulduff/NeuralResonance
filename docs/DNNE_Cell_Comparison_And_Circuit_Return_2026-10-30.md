# Corrected cell comparison and return to connected circuits — 30 October 2026

The corrected cells respond to the tested excitation and inhibition. The old
executable reproduced its earlier results exactly on the currently installed
runtime. This supports the cell repair, but does not establish calibrated human
physiology, useful learning or physical balance.

## Completed comparison

`artifacts/postural-cells-20261030-66d373` completed its 144-condition preflight,
72-condition frozen baseline and 144-condition corrected study. Each full condition
used 32 fresh cells over eight simulated seconds. All 767 captured files match
their local/D: hashes, as do 2,450 protected files and 393 packaged files. The
manifest matches too. The review is saved in
`artifacts/circuit-return-preparation-20261030/completed-cell-review.json`.

Mean stimulus-period firing in the corrected cerebellar HH cells with pacemaker
enabled, at a 0.25 ms outer step:

| Input | Mean firing |
| --- | ---: |
| None | 48.391 Hz |
| Glutamate, 20 Hz | 59.297 Hz |
| Glutamate, 100 Hz | 85.891 Hz |
| Glutamate, 100 Hz and triple quanta | 99.914 Hz |
| GABA, 100 Hz | 0.008 Hz |
| Mixed excitation/inhibition, 100 Hz | 0.008 Hz |

All 32 cells change their stimulus spike timing under each input relative to the
matched no-input control. With the pacemaker disabled, 20 Hz glutamate produces
20 Hz firing and the stronger 100 Hz input produces 100 Hz firing. This is a
material change from the old input-insensitive membrane. HH AIS boundary samples
are zero; its spikes now come from dynamic membrane crossings.

Across the tested conditions, the largest stimulus mean-rate difference between
0.25 and 0.125 ms is 0.859 Hz; the largest relative difference where the finer
rate is nonzero is 2.123%. These finite-step comparisons support a 0.25 ms next
diagnostic, not a claim of complete convergence. Reset-model inhibition still
reaches AIS boundaries in some LIF/Izhikevich conditions.

There is an unresolved within-run decline: the pacemaker-on HH no-input mean
falls from 48.391 Hz during seconds 2–6 to 7.203 Hz during seconds 6–8. The cell
capture does not contain energy/fatigue state, so it cannot identify the cause.
Metabolic reserve is a candidate mechanism to measure, not an established
explanation. The reference HH channels, temperature, conductance conversion,
pacemaker amplitudes and metabolic support remain physiologically uncalibrated.

## Clock correction

The cell probe already held the reduced circuit pacemaker on a one-millisecond
biological grid. StructureEngine instead supplied its host packet sequence as
the pacemaker clock. Changing the integration cadence could therefore change
the drive's frequency. StructureEngine now uses the substep biological timestamp
on the same held grid. A regression gives otherwise identical cells different
packet sequences and verifies identical voltages and spike/rate histories.

`biological-pacemaker-clock-v1` joins the model fingerprint and editor model-status
telemetry. Saves from the earlier clock are rejected without overwrite; existing
contact identities remain stable. This clock fix does not turn the imposed
oscillatory drive into a calibrated biological pacemaker.

## Next connected comparison

Both arms use fresh production cells and contacts, the corrected clock, classical
intracellular-support mode and a 0.25 ms integration step. Original Avatar
physical receptor/labyrinth paths and the frozen coordinator routes are retained.
No saved weights are loaded. The arms differ only in selected circuit membership:

- Reduced: the previous seven bilateral regions (14 instances).
- Expanded: those regions plus bilateral granule, Purkinje and inferior-olive
  populations (20 instances).

The added routes include mossy input to granule cells, granule parallel fibres
to Purkinje cells, olivary climbing-fibre input, Purkinje inhibition of fastigial
cells and fastigial inhibition of the olive. These are existing production routes;
the experiment neither introduces a motor controller nor claims complete brain
anatomy. Vermis and Purkinje are separate modeled populations; the granule-contact
teaching rule is assessed in the receiving Purkinje population in this circuit.

Each arm runs the same eight eight-second controls: broad/normal quiet support,
tilt and return, support displacement and return, head motion and return,
captured trajectory, muted input and left-only input. Input updates retain their
140 ms cadence. The existing decoder runs every biological millisecond, preserving
its timing independently of neural substeps; its gates are unchanged.

Every selected instance now has complete delivery and electrical aggregates.
Selected voltages and energy/fatigue ranges are recorded every 10 ms, with
source-specific contact-learning counts at the end. Energy values are model
variables, not measured metabolism. Matched analysis verifies complete energy
timelines and identical receptor-input counts and records phase-specific signal,
learning and decoder results. A changed contact alone does not prove useful learning.

## Validation and operation

All 1,009 regression checks pass, including the packet-clock and old-clock-save
preservation checks. The Release diagnostic builds without warnings or errors.
Four-case preflights for both arms close delivery accounting across all 14/20
instances and verify fresh state, input controls and electrical/spike agreement.
Their primary input counts match, and every energy observation timeline is complete.
Evidence lives under `artifacts/circuit-return-preparation-20261030`.

The package freezes source, binaries, receptor inputs, scripts and hashes. Its
visible independent PowerShell window runs preflight and both full arms, validates
each, saves the paired analysis and creates a verified D: backup. No live agent
monitoring. Earlier captures and saves remain protected. Completion is separate
from the interpretation of neural quality.

Avatar's original neuronal motor bridge and balance implementation were revisited;
mechanics, receptors, authority and paused-start behaviour remain intact. The
editor's existing learning inspector displays the added clock version. No live
brain, Avatar, editor or world is redeployed by this diagnostic. Full neuronal
balance control and recovery remain unqualified.
