## Entry 031 - Biology Validation Harness
- Added atlas validation DTOs and validation harness in NRE.Core
- Added engine anatomy validation report method and API endpoint `/api/engine/anatomy/validate`
- Added anatomy validation tests covering default layout and coordinate roundtrip
- Added Folded Archive entry documenting the harness

# Neural Resonance Engine - Changelog

## v12.0.0 - P0 Cognitive Systems (Entry 028)

This release implements the four highest-priority improvements from the Entry 028 roadmap, creating a coherent learning system capable of autonomous knowledge acquisition and consolidation.

### Biological Neuron Proportions (NEW)
- **Accurate neuron counts** based on Azevedo et al. 2009, Herculano-Houzel 2009
- **Region-specific allocation**: Cerebellum 80%, Cortex 18.6%, Brainstem 1.2%
- **Neuron type breakdown** per region (pyramidal, PV+, SOM+, VIP+, MSN)
- **Synapses per neuron**: Granule cells ~5, Pyramidal ~7000, Purkinje ~100000
- **BiologicalNeuronCounts.cs**: Static class with all proportions and utilities
- **Adjustable weighting**: Balance biological accuracy vs cognitive simulation needs

### Unified Coordinate System (NEW)
- **Standard neuroanatomical axes** following imaging conventions:
  - X: Lateral (left -1, right +1)
  - Y: Superior/Inferior (+1 up, -1 down)  
  - Z: Anterior/Posterior (-1 front, +1 back)
- **NeuroCoord.cs**: Helper class with coordinate conversion utilities
- **Anatomical landmark constants** for structure placement
- **Coordinate_System_Specification.md**: Complete documentation

### Basal Ganglia Circuit (NEW)
- **Direct/Indirect pathways** for action selection (Frank 2005)
- **D1/D2 medium spiny neurons** in striatum
- **Hyperdirect pathway** via STN for global braking
- **Dopamine modulation**: D1 enhances Go, D2 enhances NoGo
- **Action value learning** via reward prediction error

### Reward Prediction Error System (NEW)
- **VTA dopamine signaling** (Schultz et al. 1997)
- **TD learning**: RPE = R + γV(s') - V(s)
- **Phasic bursts** for unexpected rewards
- **Phasic pauses** for reward omission
- **State and action value learning**

### Working Memory PFC (NEW)
- **Attractor dynamics** with NMDA-like recurrence (Goldman-Rakic 1995, Wang 2001)
- **Bistable slots**: DOWN (empty) vs UP (maintaining)
- **Dopamine gating**: Phasic DA opens gate for encoding
- **Lateral inhibition**: Natural capacity limit ~7±2
- **Decay without maintenance**

### Systems Consolidation (NEW)
- **Sleep triplet coordination** (Diekelmann & Born 2010)
- **Slow oscillations** (0.5-1Hz cortical)
- **Sleep spindles** (12-14Hz thalamic)
- **Sharp-wave ripples** (hippocampal)
- **Triplet bonus**: Enhanced learning when all three align
- **Gradual transfer**: Hippocampus → Cortex over repeated sleep

### Integration
- All four systems integrated into main Step() loop
- Cross-system interactions: Amygdala→RPE→WM→BG
- Systems consolidation coordinates with Sleep and Hippocampus
- 28 new configuration options in NreEngineOptions

### References
- Bogacz & Gurney 2007 (basal ganglia)
- Frank 2005 (D1/D2 modulation)
- Goldman-Rakic 1995, Wang 2001 (working memory)
- McClelland et al. 1995, Frankland & Bontempi 2005 (consolidation)
- Schultz et al. 1997 (reward prediction error)

---

## v11.0.0 - Biologically Accurate Neural Circuits + Language System

This release refactors all core neural subsystems to match established neuroanatomy and physiology, plus adds a complete language processing system. All grounded in peer-reviewed literature.

### Language System (Entry 027)
- **Dual-stream architecture** (Hickok & Poeppel 2007)
- **Wernicke's Area**: Lexical access, word recognition
- **Broca's Area**: Syntactic planning, production
- **Angular Gyrus**: Semantic integration hub
- **Arcuate Fasciculus**: Bidirectional stream connection
- **Phonological Loop**: 7±2 item verbal working memory
- **Four processing levels**: Phonological, Lexical, Semantic, Syntactic
- **10,000+ word lexicon** with 128-dimensional semantic vectors

### Hippocampus: Trisynaptic Circuit (Entry 027)
- **DG→CA3→CA1 pathway** implementing pattern separation and completion
- **Dentate Gyrus**: Sparse coding with 10:1 compression via lateral inhibition
- **CA3**: Autoassociative network with recurrent collaterals for pattern completion
- **CA1**: Comparator computing novelty/mismatch signal
- **New metrics**: DGSparsity, CA3Coherence, CA1NoveltySignal

### Cerebellum: Cortex and Deep Nuclei
- **Three-layer cortex**: Granular layer, Purkinje cell layer, Molecular layer
- **Purkinje cells**: The sole cortical output neurons (GABAergic)
- **Input pathways**: Mossy fibers (motor/sensory) and Climbing fibers (error signal)
- **Deep nuclei**: Dentate, Interposed, Fastigial with proper output distribution
- **LTD learning rule**: Climbing fiber + parallel fiber = weaken synapse

### Thalamus: TRN and Relay Nuclei
- **Thalamic Reticular Nucleus (TRN)**: GABAergic shell for lateral inhibition
- **Tonic vs Burst modes**: Based on T-type Ca²⁺ channel availability
- **Sleep spindles**: 12-14Hz oscillations during NREM
- **Attention searchlight**: TRN-mediated selective disinhibition

### Amygdala: LA→B→CeA Pathway
- **Lateral Nucleus (LA)**: Primary sensory input, fear conditioning
- **Basal Nucleus (B)**: Integration with hippocampal context
- **Intercalated Cells (ITC)**: GABAergic gating for extinction
- **Central Nucleus (CeA)**: Output to brainstem for autonomic responses

### Brainstem Modulation (renamed from PonsNucleus)
- **Locus Coeruleus (LC)**: Noradrenaline - HIGH wake, LOW REM
- **Raphe Nuclei**: Serotonin - HIGH quiet wake, LOW REM
- **Cholinergic Nuclei (PPT/LDT)**: Acetylcholine - HIGH wake AND REM
- **VTA**: Dopamine reward signals

### References
Amaral & Witter 1989, Eccles/Ito/Szentágothai 1967, Jones 2007, LeDoux 2000, Sherman & Guillery 2006

---

## v3.0.0 - Full Subsystem Integration

This release implements the complete brain subsystem architecture from spec.md and the Folded Archive research documentation.

### New Subsystems

#### 1. Thalamus (40Hz Master Clock)
- **Location**: `NRE.Core/Engine/Thalamus.cs`
- **Function**: Pulses at ~40Hz (configurable 20-80Hz). Neurons firing in sync with this pulse receive "Binding Priority" - their signals propagate 2x faster.
- **API**: `POST /api/engine/thalamus?frequencyHz=40&bindingWindow=0.35&speedBoost=2`
- **Theory**: Creates gamma-band synchronization for conscious binding (spec.md Section II.2.A)

#### 2. Hippocampus (Episodic Memory)
- **Location**: `NRE.Core/Engine/Hippocampus.cs`
- **Function**: 
  - One-shot episodic capture (spike pattern snapshots)
  - Hebbian plasticity (co-firing strengthens associations)
  - Gradual decay of episodes
  - Replay during REM for memory consolidation
- **API**: `GET /api/engine/hippocampus/episodes`
- **Theory**: "The hippocampus teaches the cortex, then forgets" (Folded Archive Entry 014)

#### 3. Amygdala (Salience Gating)
- **Location**: `NRE.Core/Engine/Amygdala.cs`
- **Function**: 
  - Flags specific coordinates/regions as "important"
  - Triggers system-wide NoradrenalinePulse (~500ms) when salient spikes occur
  - Lowers all neural thresholds during pulse (attention capture)
- **API**: `POST /api/engine/amygdala/salience?regionId=9&salience=0.8`
- **Theory**: Creates attention capture for significant events (spec.md Section II.2.B)

#### 4. Cerebellum (Error Correction)
- **Location**: `NRE.Core/Engine/Cerebellum.cs`
- **Function**:
  - Tracks activity variance per region
  - Applies adaptive inhibition to high-variance areas
  - Reduces "jitter" in spike patterns
  - Learning predictable patterns vs noise
- **API**: `POST /api/engine/cerebellum/reset`
- **Theory**: "The Cerebellum must reduce the jitter of the 3D particles" (spec.md Section II.2.C)

#### 5. Sleep Controller (REM/Wake Cycle)
- **Location**: `NRE.Core/Engine/SleepController.cs`
- **Function**:
  - Monitors GlobalATP pool
  - When ATP < 30%, triggers sleep state
  - NREM: Higher thresholds, reduced activity
  - REM: Disconnects external sensors, activates dream noise, triggers hippocampal replay
- **API**: `POST /api/engine/sleep/force?phase=Rem`
- **Theory**: "The Pons triggers REM sleep to 'bake' new associations into the Hippocampus" (spec.md Section II.2.A)

### Enhanced Neural Dynamics

#### Thalamic Binding Integration
- Spikes occurring during thalamic pulse receive 2x propagation speed
- Creates temporal coherence across brain regions
- Visible in UI as pulsing indicator

#### Sleep-Dependent Plasticity
- Sensory input disconnected during sleep
- Dream noise (stochastic resonance) during REM
- Automatic hippocampal replay triggers
- Memory consolidation tracking

#### Salience-Driven Memory
- High-salience events automatically captured as episodes
- Noradrenaline pulses create attention windows
- Region-specific salience configuration

### UI Enhancements

#### New "Systems" Tab
- Thalamus frequency control with binding indicator
- Sleep/Wake cycle forcing (Awake/NREM/REM)
- Hippocampus episode and association counts
- Amygdala salience controls
- Cerebellum smoothing metrics

#### Status Bar Updates
- Real-time sleep phase display
- Thalamic pulse indicator (● active / ○ inactive)
- Episode and association counts
- Visual feedback for subsystem states

### API Additions

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/engine/thalamus` | POST | Configure thalamic 40Hz clock |
| `/api/engine/sleep/force` | POST | Force sleep phase transition |
| `/api/engine/amygdala/salience` | POST | Set region salience level |
| `/api/engine/hippocampus/episodes` | GET | List stored episodes |
| `/api/engine/cerebellum/reset` | POST | Reset cerebellum learning |

### Technical Changes

- `NreEngine.Step()` now integrates all 5 subsystems in proper sequence
- `StepHemisphere()` accepts thalamic pulse and sleep modifiers
- `PropagateTo()` applies binding speedup to delay calculations
- Extended DTOs for full subsystem status reporting
- Spike tracking includes region information for subsystem processing

### Bug Fixes (from v2.0)
- Fixed JavaScript scope errors (isMidlineRegion, regionColor)
- Fixed script loading order (Three.js before neuralRenderer.js)
- Fixed HttpClient injection consistency
- Added favicon

---

## v2.0.0 - OrbitControls + Connections + Mirrored Hemispheres

Initial release with:
- Dual hemisphere architecture
- Corpus callosum connectivity
- Exploded view visualization
- Pons modulation system
- Visual/Auditory cortex stimulation
- Resonance detection
- Thought clustering

---

## Architecture Summary

```
┌─────────────────────────────────────────────────────────────┐
│                    NreEngine (Coordinator)                   │
├─────────────────────────────────────────────────────────────┤
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐    │
│  │ Thalamus │  │Hippocampus│  │ Amygdala │  │Cerebellum│    │
│  │  40Hz    │  │  Memory   │  │ Salience │  │ Smoothing│    │
│  └────┬─────┘  └────┬─────┘  └────┬─────┘  └────┬─────┘    │
│       │             │             │             │           │
│  ┌────┴─────────────┴─────────────┴─────────────┴────┐     │
│  │                  Sleep Controller                  │     │
│  │              (REM/Wake State Machine)              │     │
│  └────────────────────────┬──────────────────────────┘     │
│                           │                                 │
│  ┌────────────────────────┴──────────────────────────┐     │
│  │                    Pons Nucleus                    │     │
│  │        (Arousal, Stability, Reset, Theta)          │     │
│  └────────────────────────┬──────────────────────────┘     │
│                           │                                 │
│  ┌────────────────────────┴──────────────────────────┐     │
│  │           Left Hemisphere  ←→  Right Hemisphere    │     │
│  │              (Corpus Callosum Connectivity)        │     │
│  └───────────────────────────────────────────────────┘     │
└─────────────────────────────────────────────────────────────┘
```

## Principles (from Folded Archive)

1. **Emergence over Scripting** - Let patterns arise from physics
2. **Structure before Scale** - Get the architecture right first
3. **Observation before Optimization** - Understand before tweaking
4. **The hippocampus teaches the cortex, then forgets**
5. **Binding through synchrony** - 40Hz gamma for conscious integration

## Entry 030 - Anatomical Reference Pack

- Added `docs/images/` as the canon anatomy reference folder.
- Added four baseline visual references covering cortex overview, superior gyri/sulci, lobe layout, and superior labeled anatomy.
- Added `docs/images/README.md` to define how the reference pack should be used for geometry and renderer validation.
- Added `docs/Entry_030_Anatomical_Reference_Pack.md`.

## Entry 032 - UI Decomposition Pass
- Extracted operator console sidebar summary, toolbar, tab bar, and five sidebar tab panels into focused Razor components.
- Added shared console tab definitions to reduce string drift in the Blazor UI.
- Kept transport, polling, JS interop, and advanced monitor/view/voice/peer orchestration in Home.razor for an incremental, lower-risk split.

## Entry 032a — UI decomposition compile fix

- Added the missing `NRE.Blazor.Shared.OperatorConsole.Tabs` import to `Home.razor` so extracted tab components resolve correctly.
- Replaced inline `OnForceSleep` lambdas in `SystemsTabPanel.razor` with named handlers to avoid Razor source-generator parse errors.
- Removed the unused local helper `R` from `AnatomyValidationHarness.cs`.
## Entry 033 — UI decomposition phase 2

- extracted Voice, Peer, View, and Monitor tab surfaces into dedicated operator-console components
- moved UI-facing DTO/view-model records and VoiceLogEntry out of Home.razor into OperatorConsoleDtos.cs
- reduced Home.razor further toward an orchestration-only page



## 2026-03-08 — Entry 033a UI decomposition phase 2 compile fix
- Fixed `double` to `float` callback assignment errors in `Home.razor` for modulators, pons, and voice controls.
- Removed unused helper from `AnatomyValidationHarness.cs`.


## 2026-03-08 — Entry 034 transport, polling, and interop extraction
- Added `EngineApiClient`, `RendererInteropService`, and `ConsoleRefreshCoordinator` to move the main runtime glue out of `Home.razor`.
- Switched Blazor API base URL loading to configuration-backed `Api:BaseUrl`.
- Reduced direct HTTP and JS calls in `Home.razor` so the page is more of a state coordinator than a transport/interop host.


## Entry 035 - Transport / orchestration test guardrails
- Added `FastFrameParser` to isolate fast-frame binary decoding from refresh loop orchestration.
- Added transport request-formation tests for peer naming, visual stimulus payloads, and load-brain uploads.
- Added fast-frame parser tests for valid, truncated, and malformed payloads.
- Extended the test project to reference `NRE.Blazor` and `NRE.Contracts`.


## Entry 036 - Coordinator Behavior Test Guardrails
- Added `IEngineApiClient` and `IRendererInteropService` abstractions for coordinator testability.
- Added coordinator loop-delay controls for test-time cadence reduction.
- Added coordinator behavior tests for voice ordering, frame forwarding, monitor telemetry cadence, and monitor-tab gating.


## Entry 037 - Visual Signal Rendering Pass
- Added live renderer visual modes: anatomy, activity, connectivity, validation
- Added translucent hemisphere shell overlays driven by live layout bounds
- Added stronger spike glow and animated fibre pulse rendering
- Added View-tab controls for render mode, shell visibility, and fibre pulse visibility


## Entry 038 - Anatomy Driven Shell Refinement
- Replaced the shell overlay sphere with a hemisphere-specific anatomical shell surface.
- Added frontal fullness, occipital taper, inferior temporal bulge, and a flatter medial wall to the shell renderer.
- Tuned live shell fitting offsets and scaling so the shell sits more naturally around the active hemisphere bounds.
## Entry 038a - Fast-frame parser test compile fix
- Fixed `FastFrameParserTests` to assert against canonical `RenderFrameFastDto` property names (`StepIndex`, `ThalamicPulseActive`).

