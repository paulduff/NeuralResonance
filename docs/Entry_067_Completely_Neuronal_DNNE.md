# Entry 067 - Completely Neuronal DNNE

Date: 2026-08-06

## Purpose

This entry defines the migration from a hybrid brain, where spiking structures coexist with a large symbolic cognition scaffold, to a DNNE in which cognition and voluntary action emerge from neuronal dynamics.

The target is not to turn operating-system scheduling, network transport, persistence, rendering, physics, or continuous body chemistry into neurons. Those are substrate and environment. The target is that perception, valuation, action selection, attention, working memory, recall, consolidation, planning, language grounding, and motor commands are selected by neuronal populations and learned synapses rather than by central string and rule dispatch.

The governing boundary is:

`world physics and receptors -> sensory spike populations -> neuronal brain circuits -> descending motor populations -> avatar body -> world physics`

Worlds may report physical facts and receptors may encode them. Worlds must not select goals, steer the body, disclose hidden state, or encode semantic answers into motor events.

## Current Position

DNNE already contains a substantial neuronal substrate:

- Izhikevich, leaky integrate-and-fire, and Hodgkin-Huxley neuron models;
- receptor and neuromodulator currents;
- STDP, BCM, eligibility traces, dopamine-modulated three-factor learning, and synaptic tagging/capture;
- bilateral distributed structure services and explicit connectome routes;
- hippocampal, basal-ganglia, cerebellar, thalamic, cortical, brainstem, and spinal structures;
- sensory and body-state spike ingress;
- embodied avatar feedback and world physics.

The principal remaining hybrid boundary is `SimulationState`. It computes goals, attention, memory summaries, sleep decisions, narration, and motor directives with central scalar rules and semantic keys. Voluntary motor output is currently converted into spikes whose neuron identifiers contain strings such as `motor_seek_food`. The avatar decodes those strings back into movement. The spikes are transported neurally, but the decision crossing that boundary is symbolic.

## Migration Principles

1. **No semantic leakage.** Goal names and expected answers may be used by an evaluator, never as inputs to a neuronal decoder.
2. **Population codes over labels.** Direction, magnitude, confidence, and inhibition are represented by firing populations and timing, not action words in neuron identifiers.
3. **Causal proof.** Silencing, lesioning, stimulation, and pathway ablation must change behavior in the predicted direction.
4. **Closed-loop proof.** A behavior is accepted only when it survives body and world feedback, not merely when a diagnostic scalar looks plausible.
5. **Shadow before authority.** New neuronal paths first run without control, then assist, then become primary after objective gates pass.
6. **Fallbacks are visible and temporary.** Every symbolic fallback has telemetry, an owner, a retirement gate, and no claim to be neuronal.
7. **One body contract.** Maze and world simulators consume the same motor population language and return consistent sensory/body encodings.
8. **Reproducibility.** Seeds, configurations, checkpoints, lesions, and evaluation traces are retained for every promotion decision.

## Control Modes

- `Shadow`: the neuronal motor decoder runs and is evaluated, but the existing symbolic route controls the avatar.
- `Assist`: population-coded neuronal output is blended with the existing route for embodied trials.
- `Primary`: symbolic locomotion spikes are removed at the avatar boundary and symbolic motor injection into the brain is disabled. Only neuronal population output controls locomotion. Tool actions remain explicit until their own neuronal populations are implemented.

`Primary` cannot be selected until the configured evidence gate is satisfied. A failed or low-confidence neuronal output in `Primary` produces no movement; it does not silently fall back to a symbolic command.

## Migration Rungs

### Rung 1 - Neuronal Motor Output

Decode bilateral premotor, SMA, M1, motor-thalamic, reticular, and spinal firing rates into left/right descending drive. Gate output with basal-ganglia inhibition/disinhibition and report cerebellar and postural support. Emit population-coded body events without semantic action names.

Acceptance requires coverage of the expected bilateral circuit, stable confidence, agreement with the reference route during shadow evaluation, correct sleep gating, and causal changes under motor or basal-ganglia ablation.

### Rung 2 - Neuronal Action Selection

Replace central goal ranking and `ResolveIntentionalMotorDirective` with competing corticostriatal action channels. Direct, indirect, and hyperdirect pathways select, suppress, or interrupt actions. Dopamine prediction error trains channel values through eligibility traces.

Acceptance requires learned choice reversal after reward contingency changes, suppression under GPi/SNr stimulation, disinhibition under direct-pathway stimulation, and no dependency on action-name strings.

#### Implemented vertical slice

The first action-selection slice uses four stable, interleaved population lanes. Lane identity is numeric and is preserved across cortical proposals, striatum, pallidal/nigral output, and motor thalamus. It is never named after an action. Every striatal lane contains paired D1-dominant and D2-dominant medium spiny populations. The transport layer now respects their anatomy: D1 spikes use direct GPi/SNr output routes and D2 spikes use the indirect GPe route, while non-striatal axonal collaterals retain ordinary fan-out.

Each participating structure reports per-lane measured firing, pathway role, output inhibition, motor-thalamic relay, corticostriatal eligibility trace, and learned synaptic strength. The controller can decode a winning lane from these measurements and shape the existing bilateral descending motor population at the actuator boundary. Numeric lane `0` preserves bilateral advance, `1` and `2` apply differential drive, and `3` applies bilateral withdrawal. These meanings exist only at the body boundary; no goal or action text is present in neuron identifiers, channel state, routing, or learning.

This slice remains under the rung 1 `Shadow -> Assist -> Primary` evidence gate. Missing action-channel data preserves rung 1 observation behavior, but once action-channel telemetry is present an incomplete or inhibited action circuit cannot silently fall back to unselected movement. Promotion evidence now includes lane coverage, selection confidence, and selection margin.

The causal test set pins:

- lane identity through cortex, striatum, GPi, and motor thalamus;
- paired D1/D2 populations in every lane;
- D1/direct and D2/indirect route separation;
- winner-take-all competition without semantic action labels;
- movement suppression under GPi stimulation;
- lane disinhibition under direct-pathway stimulation;
- loss of authority after core-circuit ablation;
- corticostriatal synaptic preference reversal after reward contingency reversal.

### Rung 3 - Neuronal Perception

Replace symbolic object/category injection with sensory feature populations, recurrent cortical binding, salience competition, and hippocampal indexing. Labels may be attached by the language bridge after a percept exists; labels may not create the percept.

Acceptance requires recognition under noise and viewpoint change, novelty responses, object permanence, and predictable failures after pathway ablation.

### Rung 4 - Synaptic Memory

Move episodic, semantic, spatial, action, and autobiographical memory authority from dictionaries into learned synaptic ensembles. Conventional storage may checkpoint neuronal and synaptic state but must not answer cognition queries itself.

Acceptance requires one-shot episodic traces, cue-dependent recall, interference, extinction/relearning, hippocampal dependence for new episodes, and gradual cortical consolidation.

### Rung 5 - Neuronal Attention And Workspace

Replace central focus selection and workspace fields with thalamocortical competition, TRN inhibition, pulvinar routing, recurrent PFC maintenance, and oscillatory broadcast.

Acceptance requires limited capacity, distractor competition, attention-dependent perception, and causal effects from TRN/pulvinar/PFC perturbation.

### Rung 6 - Neuronal Sleep And Consolidation

Let hypothalamic and brainstem sleep-wake populations respond to homeostatic chemistry; let hippocampal replay, spindles, slow oscillation, and neuromodulatory state drive consolidation. The scheduler may advance time but may not decide dream content or memories to retain.

Acceptance requires state-dependent replay, improved delayed recall, reduced online interference, and predictable consolidation loss after replay disruption.

### Rung 7 - Neural Language Bridge

Entity remains the trained language cortex and teacher while DNNE supplies grounded percepts, memories, affect, and intentions through learned latent adapters. Entity proposes language; DNNE grounds, values, remembers, and authorizes embodied actions. Training later distils recurrent language representations into cortical populations where practical.

Acceptance requires grounded reference, continuity across sessions, uncertainty reporting, memory-source inspection, and no language text directly commanding muscles.

### Rung 8 - Remove The Symbolic Scaffold

Delete or demote central cognition fields only after their neuronal replacements pass regression, causal, and embodied benchmarks. Retained diagnostics become read-only decoders of neural state. They cannot write decisions back into the brain.

Acceptance requires all cognition-authority checks to pass with symbolic cognition disabled and no measurable benchmark regression outside documented biological limitations.

## First Vertical Slice

The first implemented slice adds:

- a bilateral population-rate decoder over actual motor-structure snapshots;
- a bounded freshness window for time-sliced services, with cluster replicas averaged per biological population;
- basal-ganglia output inhibition and thalamic disinhibition gating;
- confidence, circuit coverage, agreement, and promotion telemetry;
- `Shadow`, `Assist`, and evidence-gated `Primary` control modes;
- population-coded avatar events that contain direction and polarity but no goal/action labels;
- shared integration in both rendered worlds;
- sleep and low-confidence motor suppression;
- unit and causal tests, including proof that changing the symbolic reference does not change neural output.

The symbolic motor path remains temporarily available for shadow comparison and rollback. This is not the final neuronal action-selection circuit; it is the first honest removal of semantic motor labels from the brain-to-body authority path.

## Evidence Gate

Promotion to `Primary` requires all configured conditions:

- sufficient active evaluation samples;
- bilateral motor-circuit coverage above threshold;
- confidence EMA above threshold;
- agreement EMA above threshold during comparable symbolic-reference windows;
- a sustained qualified streak;
- no active sleep gate;
- passing population-code, lesion, and simulator integration tests.

Agreement is a migration measurement, not a demand that the neuronal system permanently imitate the symbolic system. After primary control stabilizes, success, survival, learning speed, generalization, and causal integrity replace symbolic agreement as the leading measures.

## Safety And Scientific Honesty

The migration is capability work, not a claim that the software is conscious or biologically complete. Diagnostics must distinguish measured neuronal activity, decoded inference, symbolic fallback, and simulator fact. A mode change is logged and externally inspectable. Silent fallback is forbidden.

The Folded Archive will preserve failed trials as well as successful ones. A neuronal claim is accepted only when the implemented circuit, its perturbation behavior, and its embodied consequences agree.
