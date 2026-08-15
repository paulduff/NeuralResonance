# Entry 119 - Structural Completion Roadmap

## Decision

DNNE will be expanded as a biologically organized neuronal connectome, not as a
flat list of named services. A structure is complete only when it has all of the
following:

1. An atlas identity with a parent region, anatomical level, and cardinality.
2. A running spiking-neuron service with an explicit neuronal population profile.
3. Defined afferent, efferent, reciprocal, and inhibitory connectome routes.
4. A positioned editor representation and live neuronal telemetry.
5. Automated integrity, deployment, and functional pathway tests.

No machine-learning model, classifier, policy, or scripted behavioral substitute
may stand in for a neuronal structure. Learning remains local synaptic plasticity
within the spiking engine.

## Atlas rules

- The hierarchy is `region -> area/nucleus -> layer or population -> pathway`.
- White-matter tracts and sensory interfaces are typed separately from neural
  nuclei so they are not mistaken for equivalent anatomical units.
- Paired, midline, cross-hemisphere, and distributed anatomy is recorded
  explicitly. Runtime hosting remains bilateral, while editor rendering respects
  atlas cardinality.
- Every runtime identity is a concrete area, nucleus, tract, or peripheral
  interface. Broad regional names are organizational atlas groups, not services.
- Structural correction takes priority over save compatibility until a network's
  learned weights have been explicitly accepted as worth preserving.

## Rung 1 - Specific thalamic relays

This rung adds six functional nuclei:

| Nucleus | Primary neuronal role | Principal routes |
| --- | --- | --- |
| Lateral geniculate nucleus | Retinotopic visual relay and tonic/burst gating | Retina <-> LGN <-> V1, TRN inhibition |
| Medial geniculate nucleus | Tonotopic auditory relay | Inferior colliculus <-> MGN <-> A1, TRN inhibition |
| Ventral posterolateral thalamus | Somatotopic body relay | Somatic/proprioceptive afferents <-> VPL <-> S1 |
| Ventral posteromedial thalamus | Craniofacial and visceral relay | NTS/insula <-> VPM <-> S1/insula |
| Anterior thalamic nuclei | Limbic navigation and contextual relay | Subiculum/retrosplenial/ACC loop |
| Nucleus reuniens | Midline prefrontal-hippocampal coordination | PFC/MD <-> reuniens <-> CA1/entorhinal cortex |

All six use Izhikevich spiking neurons, local STDP, topographic or channel-preserving
projection kernels, explicit delays, and the existing neuromodulated tonic/burst
thalamic dynamics. They do not use ML inference.

### Implementation status

Rung 1 is implemented. At that milestone DNNE defined 96 protocol structures,
192 bilateral runtime instances, and 181 anatomically rendered instances after
respecting the midline cardinality of nucleus reuniens in the atlas. The six nuclei have running
services, neuronal profiles, connectome participation, TRN inhibition, distributed
deployment placement, editor geometry, live thalamic telemetry, and automated
structural/connectomic tests. Its temporary broad thalamus bridge was retired in
Rung 6.

## Rung 2 - Hypothalamic and autonomic decomposition

Rung 2 is implemented. DNNE now defines 105 protocol structures, 210 bilateral
runtime instances, and 199 anatomically rendered instances. Nine explicit
hypothalamic nuclei provide circadian timing, sleep inhibition, visceral and
autonomic regulation, metabolic competition, arousal, defense, and the
mammillary/anterior-thalamic memory relay. Its temporary generic service was
retired in Rung 6.

## Rung 3 - Amygdala and septo-hippocampal decomposition

Rung 3 is implemented. DNNE now defines 112 protocol structures, 224 bilateral
runtime instances, and 212 anatomically rendered instances after respecting the
midline cardinality of the medial septal nucleus. Seven explicit neuronal
populations provide conditioned affect, acute defense, olfactory-visceral
salience, sustained contextual threat, and septo-hippocampal theta coordination.
All seven are spiking Izhikevich/STDP services with explicit transmitter routes;
none uses ML inference or a classifier.

## Rung 4 - Brainstem, cranial sensorimotor, and deep cerebellar nuclei

Rung 4 is implemented. DNNE now defines 125 protocol structures, 250 bilateral
runtime instances, and 238 anatomically rendered instances after respecting all
recorded midline cardinalities. Thirteen explicit spiking populations now cover
rubral motor correction, cholinergic wake/REM coordination, visceral alarm,
craniofacial touch, pain and proprioception, facial/ocular/oral motor output, and
the dentate, interposed, and fastigial cerebellar output channels. All use
Izhikevich neurons, receptor-aware dynamics, local STDP, and explicit axonal
routes. No ML inference, classifier, or host-authored behavioral policy is used.

The temporary pons, medulla, and deep-cerebellar aggregate services were retired
in Rung 6. The spinal motor service remains a concrete peripheral neural interface.

## Rung 5 - Cortical laminar microcircuits

Rung 5 is implemented. All 35 atlas-backed cortical areas now contain stable L1,
L2/3, L4, L5, L6, PV, SST, and VIP populations with population-specific spiking
dynamics and receptors. Thalamic, feedforward, feedback, and neuromodulatory
afferents target appropriate populations after retaining their established
topographic or functional channel. Bounded delayed local collaterals run through
the receptor and STDP machinery, while only pyramidal populations use long-range
cortical outputs. Live laminar telemetry is visible in the Blazor editor.

At that milestone the refinement kept 125 structures and 250 bilateral services.
Synapse persistence
generation 2 deliberately starts a fresh network instead of reinterpreting
pre-laminar weights. It introduces no ML model, classifier, or scripted
behavioral policy.

## Rung 6 - Umbrella service retirement

Rung 6 is implemented. DNNE now has 119 concrete protocol structures, 238
bilateral runtime instances, 228 anatomically rendered instances, and 450
strongly connected projection routes. Generic thalamus, amygdala, hypothalamus,
globus pallidus, deep cerebellar nuclei, and medulla services were removed.
`Pons` became the anatomically specific `PontineNuclei`; `BasalForebrain` became
`NucleusBasalis`.

All registry, deployment, atlas, project, diagnostic, and connectome references
now resolve to concrete structures. Sleep/REM state uses the pedunculopontine and
laterodorsal tegmental nuclei; autonomic summaries use NTS and parabrachial
activity; deep cerebellar output uses dentate, interposed, and fastigial nuclei.
The shared neuronal circuit profile is compiled once rather than duplicated in
each service. This rung intentionally starts fresh neuronal state and introduces
no ML model, classifier, or scripted behavioral policy.

## Following rungs

Further growth is judged by missing functional circuits, cellular specialization,
connectome evidence, and observable behavior rather than by maximizing a count.
The next structural rung should add evidenced cell populations or missing nuclei
only where they produce a testable change in neuronal function.

## Evidence base

- Human Connectome Project multimodal cortical parcellation: 180 areas per
  hemisphere: https://www.nature.com/articles/nature18933
- Probabilistic in-vivo human thalamic atlas: 26 nuclei:
  https://pmc.ncbi.nlm.nih.gov/articles/PMC6215335/
- MRI atlas of 13 human hypothalamic nuclei:
  https://pmc.ncbi.nlm.nih.gov/articles/PMC7492465/
- Human amygdala atlas with nine nuclei:
  https://pmc.ncbi.nlm.nih.gov/articles/PMC5557007/
- Brainstem nuclei structural connectome and atlas:
  https://pmc.ncbi.nlm.nih.gov/articles/PMC9188976/
- Hippocampal subfield atlas:
  https://pmc.ncbi.nlm.nih.gov/articles/PMC5910869/
- Deep cerebellar nuclei review:
  https://pmc.ncbi.nlm.nih.gov/articles/PMC4429588/
