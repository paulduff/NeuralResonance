# Entry 040: Experimental Microtubule Quantum-Biology Approximation

Date reviewed: 2026-05-27

## Purpose

This entry evaluates whether recent work on neuronal microtubules, sometimes described informally as "quantum tubes", provides a biologically defensible addition to DNNE.

The safe conclusion is:

- Microtubules are established intracellular components relevant to neuronal structure, transport, synaptic remodeling, and plasticity.
- There is emerging experimental evidence that tubulin or microtubule assemblies can exhibit effects consistent with quantum-biological mechanisms in controlled preparations.
- There is not established evidence that neuronal microtubules implement cognition, consciousness, symbolic reasoning, or a hidden computational layer.
- DNNE should therefore model a microtubule state only as an optional intracellular influence on existing spiking neurons and synapses.

This preserves the project rule that brain function must arise from named biological circuits and neuronal firing.

## Current Evidence

### Established biological basis

Dynamic microtubules enter dendritic spines in an activity-dependent manner. Excitatory activity and plasticity protocols alter microtubule spine entry, and microtubule dynamics contribute to spine morphology and postsynaptic organization.

Implementation confidence: high.

DNNE relevance:

- Microtubule state may influence a synapse's eligibility for consolidation.
- Microtubule invasion may be approximated as a slower postsynaptic process triggered by recent glutamatergic coincidence, NMDA-like activity, and neuromodulatory context.
- This belongs beside existing STDP and synaptic-tagging behavior, not above circuit activity.

Primary evidence:

- Hu et al. (2008), activity-dependent dynamic microtubule invasion of dendritic spines: https://pmc.ncbi.nlm.nih.gov/articles/PMC6671621/
- Hu et al. (2011), BDNF-induced PSD-95 increase requires dynamic microtubule invasions: https://pubmed.ncbi.nlm.nih.gov/22031905/
- Merriam et al. (2011), dynamic microtubules promote NMDAR-dependent spine enlargement: https://pmc.ncbi.nlm.nih.gov/articles/PMC3214068/

### Emerging quantum-biological findings

Zadeh-Haghighi et al. reported in 2026 that tubulin polymerization in vitro changes with magnesium isotope nuclear-spin properties and an applied weak magnetic field, with results consistent with a radical-pair mechanism. The work concerns microtubule assembly dynamics in a biochemical assay. It does not demonstrate a cognitive computation in living neurons.

Implementation confidence: exploratory only.

DNNE relevance:

- A reduced-order microtubule stability variable could include a very small stochastic or environmental sensitivity term.
- That term must be off by default and must not be described as conscious processing.
- It would be useful for experiments asking whether intracellular structural noise changes learning stability or resilience.

Primary evidence:

- Zadeh-Haghighi et al. (2026), Science Advances, `10.1126/sciadv.ady8317`: https://pmc.ncbi.nlm.nih.gov/articles/PMC12904203/

Babcock et al. reported ultraviolet superradiance behavior from large tryptophan networks in biological architectures, including microtubule-related assemblies. This is relevant photophysics, but it does not show that such behavior drives neuronal decisions or conscious state.

Implementation confidence: exploratory only.

Primary evidence:

- Babcock et al. (2024), Journal of Physical Chemistry B, `10.1021/acs.jpcb.3c07936`: https://doi.org/10.1021/acs.jpcb.3c07936

### Anesthesia-linked findings

Khan et al. reported that the microtubule-stabilizing drug epothilone B delayed isoflurane-induced loss of righting reflex in eight male rats. This supports microtubules as potentially functionally relevant anesthesia targets, but it does not isolate a quantum mechanism and does not prove a microtubule basis of consciousness.

In 2026, the group reported a mouse follow-up with brain-penetrant epothilone B and delayed isoflurane-induced unconsciousness. The repeated direction of effect makes an anesthesia sensitivity experiment more worthwhile, while the same interpretive limitation remains: the results concern microtubule involvement, not demonstrated quantum cognition.

Implementation confidence: exploratory, suitable only for an anesthesia or arousal sensitivity experiment.

Primary evidence:

- Khan et al. (2024), eNeuro, `10.1523/ENEURO.0291-24.2024`: https://www.eneuro.org/content/11/8/ENEURO.0291-24.2024
- Khan et al. (2026), Neuropharmacology, `10.1016/j.neuropharm.2026.110834`: https://doi.org/10.1016/j.neuropharm.2026.110834

### Recent computational modeling

A 2026 theoretical paper modeled phase-coherent excitation transport in microtubule tryptophan networks. This helps define candidate mathematical sensitivity tests, but is not experimental evidence that living neurons use the modeled quantum information flow for behavior.

Implementation confidence: hypothesis-generation only.

Primary source:

- Choi et al. (2026), Entropy, `10.3390/e28020204`: https://doi.org/10.3390/e28020204

## What DNNE Should Not Add

- No quantum decision engine.
- No global consciousness variable sourced from microtubules.
- No bypass around spikes, synapses, structure services, thalamic gating, or neuromodulatory nuclei.
- No claim that the approximation reproduces quantum coherence or Orch OR.
- No simulator-owned brain behavior.

## Proposed Simulacrum

### Name

`IntracellularMicrotubuleState`

### Placement

The state belongs inside each simulated neuron, with effects limited to its membrane and its synapses. The minimum credible first target is pyramidal-like and Purkinje-like cells because the existing DNNE model already gives these cells plasticity- and timing-sensitive roles.

### State Variables

| Variable | Range | Meaning | Evidence tier |
| --- | ---: | --- | --- |
| `Stability` | 0 to 1 | Slow polymerization/stability reserve | established-to-plausible |
| `SpineInvasionEligibility` | 0 to 1 | Activity-dependent ability to support local strengthening | established approximation |
| `TransportSupport` | 0 to 1 | Slow support for receptor/protein delivery represented through consolidation | established approximation |
| `OpticalCollectiveBias` | -1 to 1 | Experimental latent term for reported collective optical behavior | exploratory |
| `RadicalPairSensitivity` | 0 to 1 | Experimental sensitivity affecting slow stability dynamics only | exploratory |

The last two variables should be disabled unless an experimental profile is explicitly selected.

### Allowed Effects

Microtubule state may adjust only:

- synaptic-tag capture probability or strength;
- eligibility trace persistence;
- slow receptor-support proxy represented as a small change in consolidated synaptic efficacy;
- very small membrane integration-window or threshold modulation in experimental runs;
- vulnerability to an anesthesia-like neuromodulatory test state.

Microtubule state must not directly generate motor commands, language, memories, reward, goals, or dispatch spikes.

### Suggested Update Dynamics

On each neuron tick:

1. Decay `SpineInvasionEligibility` slowly toward baseline.
2. Increase it when recent glutamatergic input, postsynaptic activation, and acetylcholine or BDNF-like plasticity context coincide.
3. Update `Stability` much more slowly from invasion eligibility, consolidated activity, stress, and optional experimental modifiers.
4. Feed a bounded multiplier into the existing synaptic-tagging and eligibility-trace calculation.
5. Publish diagnostic summaries only, not new behavior.

Example bounds:

| Effect | Normal profile | Experimental profile |
| --- | ---: | ---: |
| Tag capture multiplier | `0.95` to `1.05` | `0.90` to `1.10` |
| Eligibility trace duration multiplier | `0.97` to `1.03` | `0.92` to `1.08` |
| Membrane threshold modulation | none | at most `+/- 1%` |
| Direct spike production | prohibited | prohibited |

## Integration With Current DNNE

DNNE already contains the right biological landing points:

- `ModelNeuron` integrates currents, excitability, thresholding, and firing.
- `SynapseState` and `PlasticityRules` represent plasticity-related traces.
- `StructureEngine` applies per-neuron integration and synaptic updates.
- Existing hippocampal, cortical, cerebellar, and neuromodulatory circuits remain the source of behavior.

One implementation risk must be resolved first: the current structure model source is duplicated across the 74 structure services. A microtubule addition should not be manually pasted into 74 copies. The neuron, synapse, plasticity, and structure-engine primitives should be extracted into a shared biological runtime library, or the structure generation process must become the single authoritative source and regenerate deterministically.

## Staged Implementation Plan

### Phase 1: Classical microtubule support

- Add `IntracellularMicrotubuleState` with only `Stability`, `SpineInvasionEligibility`, and `TransportSupport`.
- Integrate it only with synaptic tagging and consolidation.
- Default enabled only where plasticity is already represented.
- Add telemetry showing mean stability, active spine-invasion eligibility, and consolidation contributions per structure.

Rationale: this rests on the strongest biological evidence and can improve the existing learning model without speculative claims.

### Phase 2: Arousal and anesthesia experiment

- Add an explicitly experimental run profile.
- Permit an anesthesia-like perturbation to reduce microtubule support and compare arousal/spike stability behavior.
- Keep this distinct from ordinary sleep, which remains governed by biological sleep/arousal circuits.

Rationale: this allows the eNeuro result to inform an experiment without confusing anesthesia with sleep or consciousness itself.

### Phase 3: Quantum-biology sensitivity sandbox

- Add disabled-by-default `RadicalPairSensitivity` and `OpticalCollectiveBias`.
- Keep their influence weak, local, stochastic, and restricted to slow stability/plasticity modifiers.
- Report them as hypothesis parameters, not measured brain states.

Rationale: this creates a test bench for emerging findings without turning uncertain claims into architecture.

## Required Validation

- With all microtubule options disabled, behavior and spike output must be unchanged.
- Classical mode must alter only learning/consolidation metrics over repeated trials, not produce unexplained immediate decisions.
- Experimental mode must show bounded parameter sensitivity and no transport or performance instability.
- The editor should make the feature visibly marked `Experimental: intracellular microtubule approximation`.
- Behavioral trials should compare food learning, shelter recall, fear extinction, motor refinement, and post-rest consolidation with the feature on and off.

## Recommendation

Phase 1 has been implemented as a conservative shared-runtime addition:

- `IntracellularMicrotubuleState` is linked into all structure services through `Directory.Build.props`.
- Each `ModelNeuron` now maintains local stability, spine-invasion eligibility, and transport-support state.
- Structure plasticity uses microtubule support only as a bounded modifier for eligibility persistence and synaptic-tag capture.
- Membrane integration receives only a tiny bounded stability gain.
- No microtubule state can directly emit spikes, select actions, create memories, generate language, drive reward, or bypass named brain circuits.
- Set `NRE_MICROTUBULE_MODE=off` to disable the approximation, `classical` for the normal Phase 1 model, or `experimental` to enable the weak hypothesis-only optical/radical-pair terms.
- Structure tick acknowledgements and snapshots now include microtubule diagnostics: mode, enabled/experimental flags, mean stability, spine-invasion eligibility, transport support, experimental terms, and support multipliers.
- The WPF editor inspector now visibly marks the feature as `Experimental: intracellular microtubule approximation` and shows live diagnostic summaries when structure snapshots are available.
- Automated runtime validation now checks that `NRE_MICROTUBULE_MODE=off` leaves plasticity, trace persistence, and integration multipliers neutral.

The duplicated per-structure runtime has been resolved: neuron, synapse, plasticity, circuit-kernel, structure-engine, transport, and host primitives now live in `Structures/_SharedRuntime`, with each structure retaining only its profile entry point.

Keep Phases 2 and 3 documented and disabled until Phase 1 has measurable learning benefits and stable burn-in performance.
