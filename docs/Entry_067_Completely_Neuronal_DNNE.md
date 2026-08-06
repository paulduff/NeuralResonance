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
5. **Evidence before deployment.** New neuronal paths are tested offline and in isolated embodied trials before entering the live circuit.
6. **No symbolic fallback.** Missing or weak neuronal evidence produces no action; it never restores a semantic command path.
7. **One body contract.** Maze and world simulators consume the same motor population language and return consistent sensory/body encodings.
8. **Reproducibility.** Seeds, configurations, checkpoints, lesions, and evaluation traces are retained for every promotion decision.

## Neuronal Authority

Live motor authority has no selectable mode. The avatar accepts only population-coded descending output. Semantic locomotion and tool messages are removed at the avatar boundary, and missing, stale, sleeping, incomplete, or low-confidence neuronal evidence produces no movement.

Tool actions are intentionally unavailable until dedicated manipulation populations and their causal tests exist. This prevents an unimplemented neuronal capability from being concealed behind a text parser.

## Migration Rungs

### Rung 1 - Neuronal Motor Output

Decode bilateral premotor, SMA, M1, motor-thalamic, reticular, and spinal firing rates into left/right descending drive. Gate output with basal-ganglia inhibition/disinhibition and report cerebellar and postural support. Emit population-coded body events without semantic action names.

Acceptance requires coverage of the expected bilateral circuit, stable confidence, correct sleep gating, and causal changes under motor or basal-ganglia ablation.

### Rung 2 - Neuronal Action Selection

Replace central goal ranking and `ResolveIntentionalMotorDirective` with competing corticostriatal action channels. Direct, indirect, and hyperdirect pathways select, suppress, or interrupt actions. Routed dopamine spikes train channel values through local D1/D2 receptor balance and eligibility traces.

Acceptance requires learned choice reversal after reward contingency changes, suppression under GPi/SNr stimulation, disinhibition under direct-pathway stimulation, and no dependency on action-name strings.

#### Implemented vertical slice

The first action-selection slice uses four stable, interleaved population lanes. Lane identity is numeric and is preserved across cortical proposals, striatum, pallidal/nigral output, and motor thalamus. It is never named after an action. Every striatal lane contains paired D1-dominant and D2-dominant medium spiny populations. The transport layer now respects their anatomy: D1 spikes use direct GPi/SNr output routes and D2 spikes use the indirect GPe route, while non-striatal axonal collaterals retain ordinary fan-out.

Each participating structure reports per-lane measured firing, pathway role, output inhibition, motor-thalamic relay, corticostriatal eligibility trace, and learned synaptic strength. The controller can decode a winning lane from these measurements and shape the existing bilateral descending motor population at the actuator boundary. Numeric lane `0` preserves bilateral advance, `1` and `2` apply differential drive, and `3` applies bilateral withdrawal. These meanings exist only at the body boundary; no goal or action text is present in neuron identifiers, channel state, routing, or learning.

Missing action-channel data preserves raw descending-population observation behavior, but once action-channel telemetry is present an incomplete or inhibited action circuit cannot silently fall back to unselected movement. Evidence includes lane coverage, selection confidence, and selection margin.

The causal test set pins:

- lane identity through cortex, striatum, GPi, and motor thalamus;
- paired D1/D2 populations in every lane;
- D1/direct and D2/indirect route separation;
- winner-take-all competition without semantic action labels;
- movement suppression under GPi stimulation;
- lane disinhibition under direct-pathway stimulation;
- loss of authority after core-circuit ablation;
- corticostriatal synaptic preference reversal after reward contingency reversal.

#### Neuronal affect and valuation

The affect monitor now decodes measured amygdala/insula/ACC salience, hypothalamic and visceral homeostasis, descending defense, and VTA/SNc/accumbens/striatum/habenula/OFC reward populations into four anonymous observational lanes. Diagnostic mode strings and central goal names are ignored. The monitor has no write path to action selection.

Affect changes behavior only through spikes on the connectome: limbic and interoceptive routes reach prefrontal, orbitofrontal, accumbal, striatal, pallidal, thalamic, PAG, reticular, and brainstem populations. Dopamine, serotonin, acetylcholine, and norepinephrine now alter each neuron through that neuron's local receptor currents. Local receptor state controls excitability, burst selection, intracellular support, and synaptic plasticity. The legacy global neuromodulator and reward-prediction fields are neutralized on the tick wire and cannot change firing or learning.

`/api/v1/neuronal-affect-valuation` is a read-only observation of the distributed circuit. Limbic, emotion, interoceptive-core, motivation, goal-intent, and dopamine-learning endpoints remain human-readable legacy telemetry and cannot authorize valuation or movement.

### Rung 3 - Neuronal Perception

Replace symbolic object/category injection with sensory feature populations, recurrent cortical binding, salience competition, and hippocampal indexing. Labels may be attached by the language bridge after a percept exists; labels may not create the percept.

Acceptance requires recognition under noise and viewpoint change, novelty responses, object permanence, and predictable failures after pathway ablation.

#### Implemented vertical slice

The first perceptual slice uses eight numeric feature ensembles. Each lane consists of local bands of neurons rather than object names, and lane identity is preserved through visual, auditory, somatosensory, thalamic, association-cortical, perirhinal, and hippocampal projections. Local index jitter remains within a feature band, while projection maps preserve the lane across structures of different sizes.

Every participating structure now reports measured lane activity with role-specific evidence: visual and motion features, auditory and somatic features, recurrent binding, pulvinar/thalamic salience, perirhinal familiarity, hippocampal indexing, novelty, confidence, and short persistence. Recurrent traces live in the structure engine and decay without input, providing a bounded neuronal object-permanence signal. Familiarity grows only from repeated neuronal evidence.

The controller combines these read-only measurements into a dominant percept and reports coverage, confidence, persistence, novelty, and competition margin. It cannot write an ensemble back into the brain. Percept authority requires both a sensory feature population and recurrent cortical binding; removing the binding pathway prevents a category candidate from becoming an active percept.

`/api/v1/admin/input/object` is now an annotation boundary. Simulator object IDs and labels generate no spikes, excite no ventral-stream stage, and create no conventional object memory. A label can be attached for language/evaluation telemetry only when an active neuronal ensemble already exists. Changing or contradicting that label leaves the neuronal decision unchanged. The perception-language bridge is likewise silent until a bound neuronal percept exists.

This is a feature-binding foundation, not a claim of open-world image recognition. Current worlds still need richer receptor encodings and learned visual experience before the lanes can support robust natural categories. The causal tests pin topology, moderate perturbation tolerance, novelty decay, short persistence, binding-pathway ablation, and semantic isolation.

### Rung 4 - Synaptic Memory

Move episodic, semantic, spatial, action, and autobiographical memory authority from dictionaries into learned synaptic ensembles. Conventional storage may checkpoint neuronal and synaptic state but must not answer cognition queries itself.

Acceptance requires one-shot episodic traces, cue-dependent recall, interference, extinction/relearning, hippocampal dependence for new episodes, and gradual cortical consolidation.

#### Implemented vertical slice

The first authoritative memory slice reads eight numeric memory ensembles directly from persisted inbound synapses in object, spatial, hippocampal, semantic, autobiographical, and action circuits. Each synapse now retains its neutral baseline weight, current weight, target population, eligibility trace, synaptic tag, update count, and timing state. Existing checkpoints without a baseline migrate conservatively by treating their loaded weight as the baseline, preventing old transport amplitudes from being misreported as learning.

Receiving synapses start at a neutral local weight. Presynaptic vesicle release remains part of immediate signal strength, but a caller can no longer install a postsynaptic memory merely by transmitting a large vesicle payload. Local coactivity, acetylcholine/norepinephrine encoding gates, burst timing, local D1/D2 appetitive teaching, and local 5-HT aversive teaching alter eligibility, tags, and weights. Aversive receptor balance drives extinction; later appetitive receptor balance supplies a reacquisition term so a suppressed association can be learned again rather than remaining trapped at the synaptic floor.

Every memory structure reports per-ensemble cue drive, learned engram strength relative to its own baseline, recall activation, eligibility, tagging, competing-trace interference, extinction, consolidation, and supporting-synapse count. CA3 and downstream hippocampal stages provide bounded recurrent pattern-completion persistence. Cortical consolidation depends on both potentiation and repeated updates, so it rises gradually rather than appearing fully formed after one event.

The controller's `/api/v1/neuronal-memory` decoder is read-only. It can report active recall only when a current population cue, persisted learned synapses, nonzero recall activity, and a competition margin agree. It separately reports hippocampal dependence and cortical consolidation. Removing hippocampal diagnostics removes authority to claim new episodic encoding, while established cortical traces can still support non-episodic recall.

The older episodic, unified-event, semantic, object, place, and action memory endpoints remain available for checkpoint compatibility and human-readable audit records, but their responses are marked `LegacyTelemetry`, `CanAuthorizeRecall=false`, and point to the neuronal endpoint. They are not evidence for a neuronal recall claim. Later rung-8 work will remove their remaining internal advisory uses after downstream workspace, sleep, and language consumers have neuronal replacements.

The causal test set pins:

- one-event CA3 burst encoding followed by cue-dependent recall;
- reduced recall strength and margin under competing-trace interference;
- loss of new-episode authority when the hippocampal path is absent;
- negative-prediction-error extinction followed by positive-evidence relearning;
- synaptic memory survival across a structure-engine restart;
- gradual temporal-association cortical consolidation across repeated experience.

### Rung 5 - Neuronal Attention And Workspace

Replace central focus selection and workspace fields with thalamocortical competition, TRN inhibition, pulvinar routing, recurrent PFC maintenance, and oscillatory broadcast.

Acceptance requires limited capacity, distractor competition, attention-dependent perception, and causal effects from TRN/pulvinar/PFC perturbation.

#### Implemented vertical slice

Rung 5 replaces the authoritative named-channel ranking with seven anonymous neuronal lanes. Sensory populations, pulvinar, thalamic relay, TRN inhibition, mediodorsal support, recurrent PFC maintenance, and intralaminar broadcast now contribute firing-derived lane evidence. Human-readable channel names exist only in the controller's read-only compatibility projection.

The authoritative endpoint is `/api/v1/neuronal-attention-workspace`. The older attention, prefrontal-working-memory, consciousness-rhythm, and global-workspace routes and controller update roots have been deleted. Missing neuronal evidence produces no selection and cannot restore a scalar winner.

The first vertical slice deliberately claims bounded access competition, not consciousness. It distinguishes:

- local thalamocortical selection;
- TRN-mediated distractor suppression;
- a maximum of four concurrently maintained PFC lanes;
- intralaminar broadcast, which can be lesioned without erasing local selection.

The existing populations are sufficient for the first slice: each thalamic structure provides 320 neurons, about 45 cells per lane, while PFC provides 384 neurons, about 54 cells per lane. Population growth remains available if measured lane starvation or maintenance instability appears; it is not used as a substitute for causal evidence.

Causal tests cover stable lane projection, distractor competition, targeted TRN suppression, pulvinar ablation, PFC ablation, intralaminar ablation, core thalamic ablation, and the inability of semantic labels to choose a neuronal lane.

### Rung 6 - Neuronal Sleep And Consolidation

Let hypothalamic and brainstem sleep-wake populations respond to homeostatic chemistry; let hippocampal replay, spindles, slow oscillation, and neuromodulatory state drive consolidation. The scheduler may advance time but may not decide dream content or memories to retain.

Acceptance requires state-dependent replay, improved delayed recall, reduced online interference, and predictable consolidation loss after replay disruption.

#### Implemented vertical slice

Rung 6 now separates homeostatic chemistry from sleep authority. ATP reserve and accumulated sleep pressure remain non-neuronal metabolic substrate, but they enter the relevant populations as bounded intrinsic excitatory and inhibitory current. They no longer directly choose a sleep transition whenever neuronal circuit evidence is present.

Three anonymous state populations represent wake, NREM, and REM. The read-only controller decoder combines measured hypothalamic sleep drive, reticular/LC/basal-forebrain/intralaminar wake activity, pontine REM activity, thalamic-TRN spindle synchrony, cortical slow-wave activity, and hippocampal replay gating. A partially observed circuit holds the previous state and forbids replay; it cannot silently restore the threshold state machine.

Replay uses the same eight numeric ensembles as perception and synaptic memory. CA3/CA1 bursts nominate an ensemble, both TRN and thalamus must supply spindle coupling, and cortical slow-wave/echo activity supplies consolidation evidence. The transport replay path then filters engrams by numeric neuronal population membership. It does not rank action names, goals, categories, dream themes, or other semantic fields.

The former dream-consolidation path is retained as `LegacyTelemetry`. Under neuronal authority it cannot reinforce action dictionaries, world-map records, semantic concepts, autobiographical summaries, or cerebellar scalar state. Authoritative consolidation occurs through replay spikes and the persisted synaptic plasticity implemented in rung 4. `/api/v1/neuronal-sleep-consolidation` exposes the authoritative state; the sleep-memory admin endpoint configures metabolic substrate only.

The existing structure sizes remain adequate for this first slice. State structures provide hundreds of cells per state role, while each replay structure provides dozens of cells per ensemble. Population resizing is reserved for measured lane starvation, unstable state separation, or replay capacity limits.

The causal test set pins:

- homeostatic excitation of sleep-promoting neurons and inhibition of wake populations;
- distributed NREM selection and numeric replay-ensemble selection;
- wake-system stimulation preventing sleep and replay;
- loss of spindle coupling and replay after TRN ablation;
- loss of replay authority after CA3 ablation;
- no fallback to central thresholds after incomplete neuronal evidence;
- absence of semantic replay selectors from the neuronal payload.

### Rung 7 - Neural Language Bridge

Entity remains the trained language cortex and teacher while DNNE supplies grounded percepts, memories, affect, and intentions through learned latent adapters. Entity proposes language; DNNE grounds, values, remembers, and authorizes embodied actions. Training later distils recurrent language representations into cortical populations where practical.

Acceptance requires grounded reference, continuity across sessions, uncertainty reporting, memory-source inspection, and no language text directly commanding muscles.

#### Implemented vertical slice

Rung 7 now introduces `DistributedGroundedLanguageCircuits`, a read-only adapter between numeric DNNE populations and Entity. A reference may come only from an active percept ensemble or persisted synaptic recall ensemble. When both are active, agreement improves confidence and a population mismatch raises uncertainty. Human-readable object labels are optional annotations attached after percept selection; they do not participate in ensemble, attention, confidence, or speech decisions.

Grounding requires the measured A1-Wernicke-arcuate comprehension chain, a neuronal percept or recall reference, and the neuronal attention workspace. Emission additionally requires channel 5 to win and broadcast, the Broca-premotor-M1 expression chain, basal-ganglia gating, motor-thalamic relay, acceptable uncertainty, and a wake state. Removing a comprehension structure removes grounding authority. Removing an expression structure preserves a reference but closes speech.

Every Entity prompt now carries numeric population IDs, confidence, uncertainty, circuit coverage, sleep state, and bounded source provenance. DNNE records the prompt fingerprint against its session and turn before Entity is called. A candidate with a valid self-generated hash but no DNNE-issued prompt is deferred. The candidate contract remains text-only and cannot express motor, reward, action, or memory-write operations.

When any neuronal language circuit is observed, an incomplete circuit cannot fall back to the older cognitive-language workspace, semantic memory dictionaries, or symbolic speech gate. Those fields remain available only as `LegacySymbolicTelemetry` when no neuronal language evidence exists, preserving old checkpoints while making the authority transition explicit. `/api/v1/neuronal-language-grounding` exposes the new decoder and `/api/v1/dyad/language/generate` applies it to both Entity output and DNNE fallback narration.

This is a grounded language adapter, not a claim that DNNE already contains a complete cortical language model. Entity still performs trained token generation. Future work must replace fixed population alignment with learned latent adapters, carry affect and intention through neuronal ensembles, and test continuity over long embodied sessions.

No neuron-count change was required for this slice. The existing cortical and relay structures provide sufficient population coverage for an eight-reference prototype; resizing remains justified only by measured population collision, lane starvation, or unstable lesion/benchmark results.

The causal test set pins:

- numeric percept/recall agreement and bounded provenance;
- label changes having no effect on numeric selection or confidence;
- loss of grounding after Wernicke or arcuate ablation;
- loss of speech after Broca, premotor, or motor-thalamic ablation;
- language-channel attention and wake-state requirements;
- increased uncertainty under percept/recall conflict;
- no legacy fallback after incomplete neuronal evidence;
- issued-prompt binding and isolation from neuronal motor state.

### Rung 8 - Remove The Symbolic Scaffold

Delete or demote central cognition fields only after their neuronal replacements pass regression, causal, and embodied benchmarks. Retained diagnostics become read-only decoders of neural state. They cannot write decisions back into the brain.

Acceptance requires all cognition-authority checks to pass with symbolic cognition disabled and no measurable benchmark regression outside documented biological limitations.

#### Implemented authority boundary

Rung 8 now makes neuronal-only cognition authority explicit. `/api/v1/cognition-authority` reports perception, memory, attention/workspace, sleep/consolidation, language grounding, affect/valuation, action selection, and motor output as separate domains. Every corresponding central record is `LegacyTelemetry`, and every domain reports `LegacyCanAuthorize=false`. `/api/v1/state` repeats the global authority flags so clients cannot mistake the large compatibility snapshot for a control surface.

The final semantic actuation paths have been removed from the runtime:

- English parsing can stimulate auditory and language populations but cannot construct or dispatch motor spikes.
- The live motor decoder does not receive the central intentional-action record and cannot compare against or imitate a named directive.
- The avatar discards all motor-structure traffic that is not a numeric population code, including semantic tool commands.
- Runtime `Shadow`, `Assist`, promotion, rollback, and motor-mode administration paths have been deleted.
- Entity and DNNE fallback narration cannot emit without a grounded neuronal language circuit, neuronal language attention, a complete speech chain, acceptable uncertainty, and wake-state authorization.
- The deterministic symbolic survival replay and its hosted routes have been deleted.
- Missing neuronal attention produces no selection rather than restoring the old scalar winner.
- The production sleep overload can update metabolic substrate but cannot enter or exit sleep through ATP/pressure thresholds; only the neuronal state circuit may transition it. The one-argument threshold overload remains solely for the historical sleep harness.
- The maze's goal-coordinate navigator is disabled by default and its runtime endpoint returns HTTP 410. Its session code remains an offline benchmark artifact, not a brain or world authority. The rendered maze consumes raw brain/avatar motor traffic.

The old narration, speech-intention, cognitive-language-workspace, inner-speech, intentional-action, self-monitoring, autobiographical-self, narrative-self, identity, room-state, and inhabitance endpoints have been deleted. The synthetic `ObserveCognitiveRuntime` driver and deterministic survival benchmark have also been deleted, so tests cannot manufacture cognition through a fixed scalar update sequence. Predictive perception, persistent percepts, scalar attention, prefrontal working memory, consciousness rhythm, and global workspace have already lost their public routes, update roots, diagnostics, and checkpoint state. Conventional memory dictionaries remain checkpoint and audit material pending their own deletion rung; language, replay, attention, and action decisions do not read them as authority.

This rung does not delete every central descriptive model. Body chemistry, evaluator state, checkpoint serialization, physics, and human-readable audit summaries remain because they are substrate/environment or compatibility telemetry. Retaining them is not an authority claim. A future deletion pass can remove their storage cost after checkpoint migration and long embodied regression runs on the RTX workstation.

No population resizing was required. The defects were authority leaks, not evidence of population starvation. Neuron counts should change only after firing, collision, capacity, or lesion measurements justify it.

The authority test set pins:

- no symbolic cognition domain can authorize a decision, even while a neuronal circuit is absent;
- production sleep cannot use threshold fallback;
- missing neuronal attention cannot restore a legacy winner;
- the goal-aware maze navigator is unavailable at runtime;
- semantic language and intentional-action motor-spike builders are absent from the runtime assembly;
- no-neuronal Entity candidates and fallback narration remain deferred;
- all prior neuronal, simulator, memory, and regression tests continue to pass.

## First Vertical Slice

The first implemented slice adds:

- a bilateral population-rate decoder over actual motor-structure snapshots;
- a bounded freshness window for time-sliced services, with cluster replicas averaged per biological population;
- basal-ganglia output inhibition and thalamic disinhibition gating;
- confidence and circuit-coverage telemetry;
- population-coded avatar events that contain direction and polarity but no goal/action labels;
- shared integration in both rendered worlds;
- sleep and low-confidence motor suppression;
- unit and causal tests, including proof that the decoder has no symbolic-action input.

The symbolic motor path, runtime mode switch, and old mode-qualification executable have been removed. Historical reports remain as provenance, but they cannot affect a running brain.

## Runtime Invariants

- Only numeric population codes can actuate the body.
- Bilateral circuit coverage and confidence must clear configured thresholds.
- Sleep silences descending output.
- Semantic motor and tool identifiers are rejected even if emitted by retained diagnostic code.
- No administrative endpoint or configuration value can restore legacy motor authority.
- Success, survival, learning speed, generalization, and causal integrity are the leading embodied measures.

## Safety And Scientific Honesty

The migration is capability work, not a claim that the software is conscious or biologically complete. Diagnostics must distinguish measured neuronal activity, decoded inference, symbolic fallback, and simulator fact. A mode change is logged and externally inspectable. Silent fallback is forbidden.

The Folded Archive will preserve failed trials as well as successful ones. A neuronal claim is accepted only when the implemented circuit, its perturbation behavior, and its embodied consequences agree.

The authority migration is followed by the live, multi-seed qualification protocol in [Entry 068](Entry_068_Embodied_Neuronal_Motor_Qualification.md). Offline decoder tests remain necessary but cannot unlock embodied authority.
