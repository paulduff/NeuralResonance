# Entry 041: Biological Neuron Upgrade Plan

Date created: 2026-05-27

This list records the next neuron-level upgrades for making DNNE's simulated neurons closer to biological neurons while keeping all brain function neuron/circuit based.

## Upgrade List

1. Compartmental neurons
   - Add simplified soma, dendrite, and axon initial segment behavior.
   - Let distal/contextual input and proximal/somatic input have different effects.

2. Axon initial segment firing
   - Gate spike emission through a separate threshold/recovery state.
   - Avoid treating every membrane crossing as an immediate whole-cell output.

3. Calcium dynamics
   - Add a slow calcium trace driven by NMDA-like input and spiking.
   - Use calcium for plasticity support, burst probability, fatigue, and consolidation tuning.

4. Dendritic plateau/NMDA events
   - Let strong clustered dendritic excitation create local plateau support.
   - Keep this as a contributor to soma firing and plasticity, not as direct spike output.

5. Neuron subtype parameters
   - Differentiate pyramidal, PV, SOM, VIP, MSN, Purkinje, granule, relay, and neuromodulatory cells.
   - Tune leak, adaptation, refractory period, burst tendency, and receptor balance by subtype.

6. Refractory and fatigue biology
   - Add absolute/relative refractory behavior.
   - Add spike-frequency adaptation and sodium-channel style recovery.

7. Receptor-specific synapses
   - Split glutamate into AMPA/NMDA/metabotropic effects.
   - Split GABA into GABA-A/GABA-B.
   - Route dopamine, serotonin, acetylcholine, and norepinephrine through receptor-like modulation rather than broad gain only.

8. Homeostatic plasticity
   - Let neurons slowly preserve a target firing range.
   - Too quiet increases excitability; too active lowers excitability or strengthens inhibition.

9. Energy/metabolic constraint
   - Add an ATP/metabolic reserve state.
   - Firing and plasticity consume energy; sleep/rest restores it.

10. Glial support approximation
   - Add astrocyte-like local support for glutamate cleanup, potassium buffering, and lactate/energy support.
   - Keep glia supportive rather than cognitive.

## Implementation Start

Start with items 1, 2, 3, and 6 together:

- simplified dendrite/soma/axon-initial-segment flow;
- calcium trace;
- refractory recovery;
- spike-frequency adaptation.

These changes should improve spike realism and reduce unrealistic continuous firing while preserving performance.

## Progress

- Items 1, 2, 3, and 6 are implemented as a conservative first pass across all structure services.
- Item 4 is implemented as a second pass: proximal/distal clustered excitation, NMDA coincidence, local inhibitory shunting, dendritic plateau recovery, plateau-supported calcium, and plateau-supported soma/AIS drive. The plateau supports firing and learning but does not emit spikes directly.
- Item 5 is implemented as a third pass: structure-aware neuron subtype tuning for cortical pyramidal/interneuron mixtures, hippocampal cells, thalamic relay cells, basal ganglia/MSN-like cells, cerebellar granule/Purkinje/deep nuclei cells, and neuromodulatory cells.
- Item 7 is implemented as a fourth pass: glutamate now separates into AMPA, NMDA, and metabotropic currents; GABA separates into GABA-A and GABA-B currents; dopamine, serotonin, acetylcholine, and norepinephrine now route through receptor-like D1/D2, 5-HT1/5-HT2, nicotinic/muscarinic, and alpha/beta adrenergic currents instead of one broad modulator bucket.
- Item 8 is implemented as a fifth pass: neurons now keep a slow homeostatic activity trace and gently bias excitability/inhibition toward structure-aware target firing ranges. Quiet cells become slightly easier to recruit, while persistently active cells lower excitability and strengthen inhibition over time.
- Item 9 is implemented as a sixth pass: neurons now track an ATP-like metabolic reserve. Spiking, synaptic load, calcium, and plateau activity consume reserve; low-activity/serotonin-weighted recovery restores it. Low reserve softly reduces excitability and plasticity support through fatigue rather than abruptly silencing neurons.
- Item 10 is implemented as a seventh pass: neurons now include astrocyte-like local support for glutamate cleanup, potassium buffering, and lactate-style metabolic recovery. This remains supportive biology only; glial state does not emit spikes or choose behavior.
