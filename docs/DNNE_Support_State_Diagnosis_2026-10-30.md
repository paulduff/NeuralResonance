# Support-state diagnosis — 30 October 2026

The isolated intervention identifies a causal problem in the tested cells:
the combined reserve/fatigue feedback makes sustained quiet activity collapse.
Clearing potassium accumulation alone does not prevent that collapse. A separate
post-inhibition recovery limitation remains even when both support variables are
held. These findings require mechanism/calibration work before another balance run.

## Completed and preserved experiment

`artifacts/cell-support-20261030-29b24e` finished at 14:53:36 UTC. All 48 full
conditions passed accounting, intervention, identity and trace checks. Each used
32 fresh disconnected HH cells over 24 simulated seconds. The native vermis
cells exactly reproduced the previous first-eight-second spike times at both
0.25 and 0.125 ms. All 296 captured files verify locally and on D:, as do 94
packaged files and 6,448 protected files; the manifests match. Review:
`artifacts/cell-support-review-20261030/review.json`.

Mean firing during seconds 20–24 in the continuously quiet arm, at 0.25 ms:

| Mode | Vermis | Purkinje | Inferior olive |
| --- | ---: | ---: | ---: |
| Ordinary support state | 0 Hz | 0 Hz | 0 Hz |
| Reserve/fatigue held at 1/0 | 66.500 Hz | 62.391 Hz | 62.961 Hz |
| Potassium cleared before each step | 0 Hz | 0 Hz | 0 Hz |
| Both interventions | 67.000 Hz | 63.109 Hz | 63.695 Hz |

The same quiet rates occur at the finer step. Across all phases and challenge
conditions the largest mean-rate difference between steps is 0.422 Hz. The
intervention holds reserve and fatigue together, so it does not distinguish their
individual contributions. Potassium changes the trajectory and rates, but its
removal is insufficient to sustain firing under the native energy feedback.
The held modes are diagnostic counterfactuals, not replacements for biology.

## Recovery is a separate acceptance criterion

The challenge applies glutamate during seconds 8–12 and GABA during seconds
16–20. All tested modes remain silent during seconds 20–24, including the cells
with reserve/fatigue and potassium held. Fixing the energy budget alone therefore
does not establish post-inhibition recovery. This observation is bounded to the
four-second window, not a claim of permanent loss of excitability.

A short follow-up `PosturalCellProbe --reference-recovery` tests only the bare
reference HH equations, without metabolic feedback, cell compartments or learning.
Its 18 conditions vary bias current (8, 10, 12 microamp/cm²), outer step (0.25,
0.125 ms), and inhibition (absent, abruptly released, exponentially released with
a 120 ms time constant). All receive a diagnostic 2 ms depolarizing pulse at 3 s.
The final capture is `reference-recovery-extended` inside the review directory.
Earlier, shorter checks and their source snapshots are retained separately.

At bias 8, abrupt release gives 144 spikes during the 2.3-second recovery window;
slow release gives zero. A subsequent brief pulse restarts sustained firing,
producing 62 spikes in the following 998 ms. The counts agree at both steps.
Bias 10 shows the same qualitative distinction. At bias 12, slow-release activity
resumes without the pulse, with 79 versus 82 recovery spikes at the two steps;
phase/count differences there remain visible, rather than being called exact
numerical agreement. All final voltage/gate traces contain 4,000 observations.

This supports a contribution from the reference membrane's operating regime and
release trajectory. It does not completely isolate every wrapper mechanism in
ModelNeuron. Guttman, Lewis and Rinzel experimentally and theoretically described
coexisting firing/steady states and pulse-induced switching in a squid-axon/HH
preparation. That is a relevant mechanism, not validation of human cerebellar cells.
[Guttman et al., 1980](https://pubmed.ncbi.nlm.nih.gov/7441560/).

## Repair requirements

The existing energy update charges fixed per-spike, synaptic and plasticity costs
against slow refill. Its normalized reserve is not an ATP concentration or a
validated biochemical budget. The previous review quantified the imbalance.
Activity-dependent energy production must be represented and constrained, not
replaced by the diagnostic constant reserve. Rangaraju and colleagues measured
activity-linked ATP synthesis and consumption in cultured hippocampal presynaptic
terminals. Their result supports including regulated production, but does not
supply fitted parameters for every DNNE cell or a whole-neuron metabolic model.
[Rangaraju et al., 2014](https://pmc.ncbi.nlm.nih.gov/articles/PMC3955179/).

Source inspection revisited Avatar's existing physiology. Avatar already tracks
body energy/oxygen and the physical transducer carries energy-deficit/hypoxia
receptor signals. ModelNeuron's local reserve update has no explicit blood-fuel,
oxygen-delivery or perfusion input. TickSignal's MetabolicWakeReserve currently
affects sleep-circuit drive, not this local supply equation. Interoceptive sensory
messages and substrate delivery need distinct contracts; neither body stored
joules nor a sleep/wake score is automatically local available ATP.

The next implementation needs these explicit acceptance criteria:

1. A bounded supply/demand mechanism with declared units, costs, capacity and
   recovery dynamics. Adequate supply must sustain the selected activity range;
   restricted supply must produce reversible deficits. Supply terms, expenditure
   and boundary losses must be auditable. Parameters remain provisional until
   fitted against an identified preparation.
2. Cell-specific membrane/pacemaker calibration with onset, sustained firing,
   excitation, inhibition and recovery protocols. A generic squid reference
   driven into one firing regime is insufficient to qualify a Purkinje or olive
   population. Khaliq and colleagues provide experimental/model evidence for
   Purkinje-specific sodium kinetics and channel interactions; their mouse
   preparation must not be relabeled human or applied wholesale to the olive.
   [Khaliq et al., 2003](https://pmc.ncbi.nlm.nih.gov/articles/PMC6741194/).
3. Preserve Avatar's existing physiology and neuronal authority. Any substrate
   transport must be timestamped, distinguish unavailable/stale data, and remain
   separate from sensory spikes and motor commands. Editor status must expose
   the modeled supply and calibration scope when that integration is implemented.
4. Compare fresh candidates against the frozen current baseline across supported,
   restricted-supply, inhibitory and recovery conditions. Only then return to
   connected disturbance discrimination and the independently unresolved decoder.

No production model, Avatar function, editor service, checkpoint or decoder was
changed in this review. The new short diagnostic builds without warnings/errors;
the full production regression from the preceding repair is unchanged. Findings
and diagnostic source are committed and the review receives a verified D: backup.
No new long experiment was launched. Successful reference calculations and artificial
state interventions do not qualify biological equivalence or physical recovery.
