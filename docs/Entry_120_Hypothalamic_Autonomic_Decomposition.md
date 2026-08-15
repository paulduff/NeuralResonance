# Entry 120 - Hypothalamic and Autonomic Decomposition

## Decision

DNNE's generic hypothalamus is being decomposed into named neuronal nuclei. The
umbrella service remains temporarily as a compatibility bridge for persisted
synapses and older routes, but new pathways target a specific nucleus.

This rung contains no machine-learning model, classifier, reward policy, or
scripted behavioral substitute. Function emerges from Izhikevich neuronal
populations, routed spikes, local inhibition, neuromodulation, conduction delays,
and STDP.

## Nuclei and neuronal functions

| Structure | Neuronal function | Principal circuit |
| --- | --- | --- |
| Ventrolateral preoptic nucleus | Sleep-promoting inhibition | SCN/homeostatic input -> VLPO -| LC, raphe, basal forebrain, reticular formation |
| Suprachiasmatic nucleus | Retinal circadian phase entrainment | Retina -> SCN -> VLPO, DMH, PVN |
| Paraventricular hypothalamic nucleus | Descending autonomic set-point control | NTS/insula/amygdala -> PVN -> NTS, medulla, PAG |
| Supraoptic nucleus | Osmotic and visceral state relay | NTS/PVN -> SON -> PVN/NTS |
| Arcuate nucleus | Nutrient and energy-state competition | NTS/insula/raphe -> arcuate -> LHA, VMH, PVN |
| Lateral hypothalamic area | Hunger, seeking, and wake drive | Arcuate/insula/amygdala -> LHA -> arousal and defense systems |
| Ventromedial hypothalamic nucleus | Satiety and defensive gating | Arcuate/LHA/amygdala -> VMH -> LHA/PAG |
| Dorsomedial hypothalamic nucleus | Circadian autonomic and arousal output | SCN/PVN -> DMH -> LC, reticular formation, PVN |
| Mammillary bodies | Episodic-navigation relay | Subiculum/CA1 -> mammillary bodies -> anterior thalamus/retrosplenial cortex |

The present neurotransmitter protocol represents the fast neuronal component of
these circuits. Peptide and endocrine consequences are deliberately not invented
as hidden scalar controls; they require an explicit receptor and body-endocrine
protocol in a later rung.

## Completion criteria

1. Every nucleus has an atlas identity and bilateral anatomical placement.
2. Every nucleus runs a dedicated spiking service and hypothalamic circuit kernel.
3. Every nucleus participates as both a connectome source and target.
4. Inhibitory outputs are explicit GABA routes; other fast outputs are explicit
   glutamatergic routes.
5. Homeostasis and sleep/wake telemetry includes the new neuronal populations.
6. Deployment, browser atlas, circuit audit, and automated tests cover the rung.

## Compatibility and next work

The generic hypothalamus is retained until old routes and persisted synapses can
be migrated safely. A later protocol rung should add peptide, histamine, endocrine,
and peripheral receptor signaling before adding tuberomammillary and other
neurosecretory populations with their full transmitter identities.

## Implementation status

Implemented on 2026-08-11. All nine nuclei now have protocol identities,
bilateral atlas geometry, Izhikevich/STDP services, dedicated hypothalamic
projection kernels, explicit fast-transmitter routes, runtime diagnostics,
distributed deployment entries, browser-editor geometry, and automated tests.
The exported editor atlas contains 105 definitions and 199 anatomical instances.
