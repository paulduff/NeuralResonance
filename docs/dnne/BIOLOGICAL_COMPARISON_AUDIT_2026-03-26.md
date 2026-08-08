# DNNE Biological Comparison Audit

Original audit: 2026-03-26

Re-audited: 2026-08-08
Scope: current `NeuralResonanceEngine.DNN` source, contracts, connectome,
qualification tests, and laptop runtime evidence.

## Executive Summary

DNNE is now a broad, explicitly neuronal simulation architecture. It contains
90 named structures, 180 registered left/right service instances, persistent
synapses, spike timing, neurotransmitter-specific receptor currents,
structure-family circuit kernels, region-scaled neuron populations, local
plasticity, and neuronal perception-memory-attention-action loops. The current
circuit audit reports all 90 structures as mapped, implemented, and connected.

It is not a biological replica of a human brain. Its structure counts,
connectome, cell populations, kinetics, geometry, and simulator transduction are
engineered abstractions. The correct claim is that DNNE provides a testable,
distributed spiking substrate inspired by identified neural systems. Claims of
human-equivalent anatomy, physiology, cognition, consciousness, or personhood
are not supported by the current evidence.

## Measured Inventory

| Measure | Current result | Evidence |
| --- | ---: | --- |
| Named `StructureId` values | 90 | `Protocol/StructureId.cs` |
| Registered bilateral instances | 180 | `ControlProgram/appsettings.json` |
| Deployment assignments | 90/90 | `deploy/distributed/dnne-deploy.manifest.json` |
| Circuit audit status | 90 OK | `tools/audit-dnne-circuits.ps1` |
| Structure-family kernels | 14 | `Structures/_SharedRuntime/CircuitKernels.cs` |
| Neurons per structure instance | 160-512 | `Structures/M1/StructureCircuitProfile.cs` |
| Authority/causality regression suite | executable | `tests/NeuralResonanceEngine.DNNE.Tests` |

`OK` in the circuit audit means a structure has a service mapping, project,
inbound route, outbound route, and explicit drive profile. It does not mean the
structure is experimentally validated as a model of its biological namesake.

## Mechanisms Present

### Neurons and local physiology

- Izhikevich, leaky integrate-and-fire, and Hodgkin-Huxley execution modes are
  available through one shared `ModelNeuron` implementation.
- Axon-initial-segment, basal-dendrite, and apical-dendrite state is represented,
  together with refractory, adaptation, calcium, plateau, metabolic, and
  astrocyte-proxy terms.
- Region families select different subtype and receptor profiles. Cortical
  populations include deterministic fast-spiking-like and slow-adapting bands;
  striatal populations alternate D1- and D2-dominant receptor profiles.
- Population sizes vary by family rather than using one global count.

### Synapses and plasticity

- Synapses have stable identities and persistent per-structure state.
- Pre/post traces, eligibility, tagging/capture, consolidation, pruning, and
  transmitter-aware plasticity are implemented.
- Receptor currents distinguish AMPA, NMDA, metabotropic glutamate, GABA-A,
  GABA-B, dopamine D1/D2, serotonin, nicotinic/muscarinic, and adrenergic paths.
- Conduction timing uses pathway and topographic delay rules rather than a
  synchronous global update alone.

### Systems and closed loops

- Sensory, thalamic, hippocampal, basal-ganglia, cerebellar, neuromodulatory,
  association, self-context, executive, sensorimotor, and body-schema kernels
  provide distinct projection behavior.
- Live host inputs are numeric sensor transduction only. Tests prohibit host
  semantic object labels, typed language authority, scalar affect/cognition
  authority, and host-authored action decisions.
- Memory recall, attention, sleep consolidation, valuation, locomotion, and
  manipulation are decoded from designated neuronal population activity.
- Entity remains the external language model. DNNE supplies and reviews numeric
  neuronal grounding; accepted text is not evidence that DNNE itself generated
  language neuronally.

## Remaining Biological Gaps

### 1. Scale and density

The simulated populations are tiny relative to mammalian structures. A
160-512-neuron service is useful for dynamics and integration testing, not a
scaled anatomical estimate. Relative family sizes are heuristic and do not
preserve human neuron counts, density, cortical surface area, or white-matter
volume.

### 2. Cell-type diversity

All neurons execute one implementation with parameterized subtype profiles.
There are no explicit morphologically distinct pyramidal, basket, chandelier,
stellate, Purkinje, granule, relay, medium-spiny, or glial object classes with
validated proportions and wiring motifs. Receptor-profile diversity is a useful
step, but it is not cell census fidelity.

### 3. Connectome resolution

The connectome is manually curated at structure and projection level. It is not
registered to a tractography atlas and does not encode cortical layers,
cell-type-specific termini, axon arbor geometry, or empirically measured
connection probabilities at neuron scale.

### 4. Parameter validation

Thresholds, gains, kinetics, delay windows, plasticity constants, spontaneous
drive, and homeostatic bounds are engineered for stable behavior. They require
systematic comparison with published electrophysiology and ablation evidence.

### 5. Sensory and bodily transduction

WorldSim provides a synthetic body and numeric sensors. Retina, cochlea,
vestibular organs, viscera, nociception, endocrine state, and muscle mechanics
are functional interfaces, not biophysical receptor models.

### 6. Development, glia, and vascular support

Metabolic and astrocyte-like terms exist, but developmental wiring,
myelination, oligodendrocytes, microglia, vascular coupling, immune signaling,
neurogenesis, and structural morphology are not represented at biological
depth.

### 7. Anatomy visualization

The editor uses hand-tuned millimetre anchors and procedural cortical sheets.
It is suitable for inspecting activity and connectivity, but it is not
atlas-registered and must not be used for quantitative anatomical claims.

### 8. Distributed timing

Process and network scheduling add latency and jitter unrelated to axonal
conduction. The cluster must record wall-clock jitter, tick lag, queue pressure,
and dropped work separately from modeled biological delay.

### 9. Behavioral validation

Healthy services and visible movement do not establish learning or cognition.
Progress must be demonstrated with seeded held-out worlds, replayable episodes,
ablation controls, baseline comparisons, and repeated-run confidence intervals.

## Laptop Runtime Evidence

The 2026-08-08 full-brain WorldSim preflight brought all registered structures
online on the current laptop. The run reached 100% CPU, produced accepted
auditory/body/somatic input, attempted an interaction, and moved 0.032 m. It did
not pass behavioral qualification because movement was too small and retinal
dispatch was rejected during the observed window. This is useful integration
evidence and a clear performance limit, not a biological success claim.

## Priorities

1. Preserve deterministic headless and visible WorldSim qualification gates.
2. Add held-out multi-seed behavioral baselines and ablations before expanding
   cognitive claims.
3. Calibrate spontaneous drive, timing, and plasticity per circuit family from
   cited experimental ranges.
4. Introduce explicit cell populations and local motifs one biological system
   at a time, beginning with retina/V1, hippocampus, basal ganglia, and
   cerebellum.
5. Record cluster timing separately from simulated conduction.
6. Keep rendering coordinates labelled as visualization geometry until an atlas
   registration pipeline exists.

## Reproduction

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\audit-dnne-circuits.ps1
dotnet test .\tests\NeuralResonanceEngine.DNNE.Tests\NeuralResonanceEngine.DNNE.Tests.csproj -c Release
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\run-neuronal-only-qualification.ps1
```

The live qualification command can launch WorldSim when explicitly requested;
the offline tests and circuit audit are safe to run without the brain services.
