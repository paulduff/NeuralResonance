# DNNE Biological Comparison Audit (DNN)
Date: 2026-03-26  
Scope: `NeuralResonanceEngine.DNN` (Control, Protocol, Structures, connectome, WPF mapping)

## Current Status Update - 2026-05-15
Several earlier critical gaps in this audit have now been closed or materially reduced:

- Persistent biological synapses are implemented through `SynapsePersistenceStore`, with inbound and outbound synapse state saved per structure under `%LOCALAPPDATA%\NeuralResonanceEngine\synapses` or `NRE_SYNAPSE_STATE_DIR`.
- Trace-based plasticity is implemented across all structure engines, including pre/post traces, signed eligibility traces, synaptic tag traces, neuromodulated consolidation, and tagging/capture.
- Projection mapping is no longer generic hash scatter only. Shared circuit kernels now provide deterministic topographic mappings for sensory grids, auditory channels, thalamic relay layers, hippocampal lamellae, basal-ganglia action channels, cerebellar microzones, diffuse neuromodulatory projections, and cortical columns/layers.
- Conduction timing now uses tract windows plus biological timing adjustments for topographic distance, feedback, spike type, transmitter class, local circuitry, brainstem/modulatory paths, and cortical/thalamic/cerebellar pathways.
- The language loop now has a `DialogueTurnManager` in the Control Program with turn phase, confidence, clarification pressure, delivery outcome, repair count, pause count, and admin snapshot/reset endpoints.

The main remaining biological gap is still depth of per-structure internal cell-type diversity. Shared kernels now differentiate pathways better, but many nuclei still use the same underlying `ModelNeuron` class and would benefit from structure-specific interneuron/principal-cell mixtures.

## Executive Summary
The DNN now has strong structural coverage (64 named structures in `StructureId`, all represented in connectome I/O), neurotransmitter-aware inter-service signaling, and strict service isolation.  
The biggest biological gap is that many structure services still share the same generic microcircuit and neuron implementation, with structure differences expressed through profiles, shared circuit kernels, pathway mappings, and neuromodulatory context rather than fully distinct per-nucleus cell populations.

This means the system is anatomically broad and now has persistent synapses, trace-based plasticity, topographic projection helpers, and conduction timing rules, but it is still physiologically shallow in the harder places: cell-type diversity, local recurrent motifs, and per-structure calibration.

## What Is Biologically Aligned
- Neurotransmitter identity is first-class on inter-service spikes (`SpikeMessage` fields include NT + vesicle quanta + reuptake).
  - Evidence: `Protocol/SpikeMessage.cs`
- Inhibitory/modulatory identity checks exist at connectome load time.
  - Evidence: `ControlProgram/Program.cs:4426`, `ControlProgram/Program.cs:4469`
- Service isolation pattern is preserved (per-structure ASP.NET host, contract-only sharing).
  - Evidence: `Structures/*/Program.cs`, `Shared.Contracts/Contracts.cs`
- Connectome has full participation (64 sources and 64 targets in connectivity file).
  - Evidence: `connectivity/dnne-connectivity.json`

## Critical Gaps (Too Far from Biology)

### 1) Structure microcircuits are functionally cloned
All 64 services share identical `StructureEngine`, `ModelNeuron`, and `PlasticityRules`; only profile strings differ.
- Evidence:
  - `Structures/A1/StructureEngine.cs` (same hash as all structure engines)
  - `Structures/A1/ModelNeuron.cs` (same hash as all model neuron files)
  - `Structures/A1/PlasticityRules.cs` (same hash as all plasticity files)
  - Profile-only differences in per-service startup:
    - `Structures/V1/Program.cs:38`
    - `Structures/Thalamus/Program.cs:38`
    - `Structures/Striatum/Program.cs:38`
    - `Structures/Cerebellum.PurkinjeCellLayer/Program.cs:38`

Impact:
- V1, TRN, CA3, Purkinje, Striatum, etc. are not differentiated by real internal circuit topology or neuron class composition.

### 2) Persistent biological synapses are implemented; projection calibration remains
Inbound and outbound synapse state is now persisted per structure through `SynapsePersistenceStore`, including synaptic state that can survive process restarts.
- Evidence:
  - `Structures/Shared/SynapsePersistenceStore.cs`
  - Shared structure engines save and restore synapse state under `%LOCALAPPDATA%\NeuralResonanceEngine\synapses` or `NRE_SYNAPSE_STATE_DIR`.

Impact:
- Synapse-level continuity is now present. The remaining work is to calibrate long-term projection strength and pruning rules per biological circuit.

### 3) Afferent/efferent mapping is improved but not atlas-grade
Shared circuit kernels now provide deterministic mappings for sensory grids, auditory channels, thalamic relay layers, hippocampal lamellae, basal-ganglia action channels, cerebellar microzones, diffuse neuromodulatory projections, and cortical columns/layers.
- Evidence:
  - `Structures/Shared/CircuitKernel.cs`
  - `Structures/Shared/StructureEngine.cs`

Impact:
- Topography is no longer purely random. The remaining biological gap is atlas-level laminar specificity and cell-type-specific targeting inside each nucleus.

### 4) Trace-based plasticity is implemented; local rule tuning remains
Shared plasticity now includes pre/post traces, eligibility traces, synaptic tag traces, neuromodulated consolidation, and tagging/capture.
- Evidence:
  - `Structures/Shared/PlasticityRules.cs`
  - `Structures/Shared/SynapseState.cs`

Impact:
- Learning is now closer to biological synaptic adaptation. The remaining work is to tune rule parameters per structure, especially for cerebellar LTD, hippocampal sequence learning, and corticostriatal dopamine timing.

### 5) Conduction timing is broadened; empirical calibration remains
Conduction timing now uses tract windows plus biological timing adjustments for topographic distance, feedback, spike type, transmitter class, local circuitry, brainstem/modulatory paths, and cortical/thalamic/cerebellar pathways.
- Evidence:
  - `Structures/Shared/ConductionTiming.cs`
  - `Structures/Shared/DelayWindow.cs`

Impact:
- Timing is no longer feedback-only. The remaining gap is validating the delay ranges against the intended biological pathways and observed runtime dynamics.

## High-Priority Gaps

### 6) Uniform neuron count and no cell-type proportions
- Each structure starts with exactly 256 neurons regardless of biological scale.
  - Evidence: `Structures/A1/StructureEngine.cs:18`

Impact:
- No region-specific ratios (e.g., granule abundance, cortical pyramidal/interneuron mixes, nucleus size differences).

### 7) TRN/attention loop not driving dynamic global attention bias
- `GlobalAttentionBias` exists but is effectively static unless externally rewritten.
  - Evidence:
    - `ControlProgram/Program.cs:1324`
    - `ControlProgram/Program.cs:1366`
    - `ControlProgram/Program.cs:3913`

Impact:
- TRN attentional spotlight is not operating as an active closed-loop gating controller.

### 8) Spontaneous activity policy is synthetic and dominant
- Per-tick spontaneous generation plus fallback injection if none generated.
  - Evidence:
    - `ControlProgram/Program.cs:4882`
    - `ControlProgram/Program.cs:4976`
    - `ControlProgram/Program.cs:5063`
- Current configuration keeps spontaneous noise enabled and scaled.
  - Evidence: `ControlProgram/appsettings.json` (`SpontaneousNoise.Enabled=true`, `Scale=2.5`)

Impact:
- Baseline activity is maintained, but can obscure true structure-driven sensory/associative dynamics.

### 9) Hemisphere behavior is mostly mirrored with limited lateralization
- Left/right instances are generated symmetrically by port offset.
  - Evidence: `ControlProgram/Program.cs:4701`, `:4702`

Impact:
- No strong left-language/right-prosody or other asymmetry profiles unless manually introduced in connectome/circuit internals.

## Medium Gaps

### 10) Documentation fidelity requires ongoing synchronization
The architecture overview has been regenerated into readable structure summaries, but the documentation needs regular synchronization after large biological or rendering changes.
- Evidence: `docs/dnne/01-architecture-overview.md`

Impact:
- Biology claims are easier to review, but stale audit notes can still mislead roadmap decisions if they are not kept current.

### 11) Visual anatomy is heuristic, not atlas-registered
WPF cortical and subcortical placement uses custom procedural warps and hand-tuned anchors.
- Evidence: `src/NRE.WpfEditor/MainWindow.xaml.cs:3116`, `:3151`, `:3438`, `:3538`

Impact:
- Useful visualization, but not a reliable anatomical ground truth for quantitative biological validation.

## System-by-System Comparison

### Sensory (V1/A1/S1/Olfactory)
- Present: yes (service + connectome).
- Biological miss: no true columnar maps, no receptor-stream-specific routing, random target neuron assignment.
- Priority: Critical.

### Thalamic relay + TRN
- Present: yes (Thalamus + Pulvinar + MD + Intralaminar + MotorThalamus + TRN).
- Biological miss: no explicit relay vs matrix populations, tonic/burst switching is generic, TRN spotlight loop not dynamically shaping attention bias.
- Priority: High.

### Hippocampal formation
- Present: yes (EC/DG/CA3/CA2/CA1/Subiculum/Presubiculum/Parasubiculum).
- Biological miss: no perforant/Schaffer/mossy pathways at neuron-population granularity; CA3 recurrence exists but as generic self-feedback add-on.
- Priority: High.

### Cortical association + language
- Present: yes (PFC/PPC/Temporal + language network nodes).
- Biological miss: weak laminarity, no cortical microcolumn specialization, no hemispheric linguistic dominance in local circuit internals.
- Priority: High.

### Basal ganglia/limbic
- Present: yes (Striatum/GPe/GPi/STN/SNr/SNc + NAcc/VP/Habenula + Amygdala/ACC/Hypothalamus).
- Biological miss: D1/D2, striosome/matrix, pallidal subclass behavior and amygdala LA/B/ITC/CeA chain are not explicitly implemented as distinct populations.
- Priority: Critical.

### Cerebellum/brainstem
- Present: yes (Granule/Purkinje/DCN/Vermis/Lobules/InferiorOlive/Pons/Medulla).
- Biological miss: cerebellar microzone architecture and climbing/parallel fiber convergence rules are not explicitly encoded; LTD gating is simplified.
- Priority: High.

### Neuromodulatory nuclei
- Present: yes (LC/Raphe/BF/VTA/SNc).
- Biological miss: modulation mostly global and scalar; receptor subtype and region-specific diffusion kinetics are absent.
- Priority: Medium.

## Remediation Plan (Phased)

## Phase 1 (Foundation, 1-2 weeks): Make biology enforceable
1. Introduce stable outbound synapse objects per `(sourceNeuron, targetStructure, targetNeuron, projectionType)`.
2. Remove per-spike `Guid.NewGuid()` synapse generation for routine pathways.
3. Replace mean-only vesicle quanta with per-synapse quanta state.
4. Add connectome invariants test suite:
   - inhibitory source NT constraints
   - required feedback pathway existence
   - hemispheric/lateralization metadata validity

Exit criteria:
- Synapse IDs persist across ticks for recurring pathways.
- Plasticity changes are measurable per stable synapse.

## Phase 2 (Core physiology, 2-4 weeks): Split generic engine into structure kernels
1. Add `ICircuitKernel` per structure family:
   - sensory cortical kernel
   - thalamic/TRN kernel
   - hippocampal kernel
   - basal ganglia kernel
   - cerebellar kernel
2. Add population classes (excitatory, inhibitory, modulatory) with region-specific ratios.
3. Replace random inbound neuron assignment with projection maps/topographies.

Exit criteria:
- At least one structure in each family has distinct circuit code path and tests.

## Phase 3 (Plasticity realism, 2-3 weeks)
1. Implement trace-based pair STDP (pre/post traces with time constants).
2. Implement BCM sliding threshold (`theta_M`) as running postsynaptic average.
3. Implement dopamine-modulated three-factor rule (eligibility + RPE).
4. Implement cerebellar LTD requiring climbing + parallel coincidence.

Exit criteria:
- Plasticity tests verify timing dependence and co-activation conditions.

## Phase 4 (Timing and transport realism, 1-2 weeks)
1. Apply delay distributions to all long-range projections, not feedback-only.
2. Use tract class delay priors (`intra-cortical`, `thalamo-cortical`, `cerebello-thalamic`, `brainstem`).
3. Keep queue/backpressure telemetry and enforce bounded jitter windows.

Exit criteria:
- Delay histograms per projection type match configured biological priors.

## Phase 5 (Systems-level behavior, 2-3 weeks)
1. Implement dynamic TRN-driven attention bias updates.
2. Add hemisphere lateralization profiles (language dominance, asymmetry coefficients).
3. Tune spontaneous noise into constrained physiological baseline (no forced fallback spike every tick unless explicitly in benchmark mode).

Exit criteria:
- Attention vector changes with TRN/PFC traffic.
- Left/right language pathway utilization diverges under language tasks.

## Phase 6 (Validation and observability, 1-2 weeks)
1. Add biology conformance panel/API:
   - structural conformance score
   - plasticity realism score
   - delay realism score
   - neuromod realism score
2. Replace broken architecture placeholders with generated, verified per-structure docs from source of truth.

Exit criteria:
- CI emits conformance report and fails below thresholds.

## Immediate Next Actions (Recommended order)
1. Fix persistent synapse identity and per-synapse quanta first (critical blocker for realistic learning).
2. Break out structure-family kernels (stop cloned dynamics).
3. Implement trace-based STDP + coincidence-gated LTD.
4. Update documentation generator to eliminate placeholder text artifacts.
