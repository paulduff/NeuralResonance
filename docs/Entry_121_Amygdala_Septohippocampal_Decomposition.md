# Entry 121 - Amygdala and Septo-Hippocampal Decomposition

## Decision

DNNE's broad amygdala and basal-forebrain compatibility services are now
decomposed into seven named neuronal populations. Existing umbrella services
remain temporarily available so persisted synapses and older routes retain their
meaning, but new connectome work should target the most specific population.

No machine-learning model, classifier, host-authored fear state, reward policy,
or scripted memory substitute is used. Function emerges from Izhikevich neurons,
GABAergic, glutamatergic, and cholinergic routes, conduction delays,
neuromodulation, recurrent activity, and STDP.

## Neuronal populations

| Structure | Primary function | Principal circuit |
| --- | --- | --- |
| Basolateral amygdala | Conditioned sensory-context association and affective tagging | Cortical/hippocampal context -> BLA -> central amygdala, BNST, PFC, ventral striatum |
| Central amygdala | Acute defensive, autonomic, and arousal output | BLA -> central amygdala -> PAG, PVN, LHA, LC |
| Medial amygdala | Olfactory-visceral and social salience | Cortical amygdala -> medial amygdala -> BNST, VMH, LHA |
| Cortical amygdala | Odor identity and affective association | Olfactory/entorhinal context -> cortical amygdala -> BLA, medial amygdala, OFC |
| Bed nucleus of the stria terminalis | Sustained contextual threat and vigilance | BLA/central/medial amygdala -> BNST -> PVN, PAG, LHA |
| Medial septal nucleus | Hippocampal theta pacing and encoding-state coordination | Basal forebrain -> medial septum -> DG, CA3, CA1, entorhinal cortex |
| Diagonal band nucleus | Entorhinal, olfactory, and septal timing | Basal forebrain/medial septum <-> diagonal band -> entorhinal cortex, olfactory bulb, CA1 |

Central-amygdala, medial-amygdala, and BNST service defaults are GABAergic.
Basolateral and cortical-amygdala defaults are glutamatergic. Medial-septal and
diagonal-band defaults are cholinergic, with explicit GABAergic pacing routes
where interneuron coordination is required.

## Runtime integration

- A dedicated `AmygdalaSeptalCircuitKernel` preserves seven neuronal channels.
- Norepinephrine can recruit brief threat bursts in BLA, central amygdala, and
  BNST populations.
- Acetylcholine can recruit theta-timed bursts in medial-septal and
  diagonal-band populations.
- Affect, homeostasis, descending defense, olfactory memory, attention, and
  theta diagnostics now read the specific populations as well as the temporary
  compatibility bridges.
- Every new population is both a source and a target in the biological
  connectome; no source is orphaned and no synapse identifier is duplicated.

## Anatomy and hosting

DNNE now has 112 protocol structures and 224 bilateral runtime instances. The
browser atlas exports 212 anatomical instances because the medial septal nucleus
is correctly represented as a single midline population. The five amygdala and
extended-limbic additions are paired, while the diagonal-band nucleus remains
paired. Dedicated service ports `52303` through `52309` and distributed bundle
ownership are recorded for all seven.

## Verification

Implemented on 2026-08-11. The complete solution builds with zero warnings and
zero errors. All 466 DNNE tests pass. The circuit audit reports every added
population `OK`, with inbound and outbound routes, a neuronal profile, and a
mapped service project. The exported browser atlas contains 112 definitions and
212 instances.

## Next structural rung

The next recommended expansion is brainstem and cerebellar decomposition:
parabrachial complex, pedunculopontine and laterodorsal tegmental nuclei, red
nucleus, cranial sensory/motor relays, and named deep cerebellar nuclei. Those
populations should be added only with explicit neuronal transmitter identities,
afferent/efferent routes, atlas geometry, live telemetry, and structural tests.
