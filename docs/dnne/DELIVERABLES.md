# DNNE Deliverables Index

1. Architecture overview and connectivity diagram:
- docs/dnne/01-architecture-overview.md

2. Protocol library:
- Protocol/spike_message.proto
- Protocol/SpikeMessage.cs
- Protocol/NeuromodState.cs
- Protocol/SpikeProtocol.cs
- Protocol/StructureId.cs
- Protocol/NTEnum.cs
- Protocol/SpikeTypeEnum.cs

3. Control Program:
- ControlProgram/Program.cs
- ControlProgram/appsettings.json

4. Structure services (biological processing order):
- Sensory: Structures/V1, Structures/A1, Structures/S1, Structures/OlfactoryBulb
- Thalamic relay: Structures/Thalamus, Structures/TRN
- Hippocampal: Structures/EntorhinalCortex, Structures/Hippocampus.DG, Structures/Hippocampus.CA3, Structures/Hippocampus.CA1, Structures/Subiculum
- Association: Structures/PFC, Structures/PPC, Structures/TemporalAssociation
- Subcortical/basal ganglia: Structures/Striatum, Structures/GlobusPallidus, Structures/STN, Structures/SNr, Structures/SNc
- Limbic: Structures/Amygdala, Structures/ACC
- Cerebellum: Structures/Cerebellum.GranuleCellLayer, Structures/Cerebellum.PurkinjeCellLayer, Structures/Cerebellum.DCN, Structures/InferiorOlive
- Neuromodulatory nuclei: Structures/LocusCoeruleus, Structures/RapheNuclei, Structures/BasalForebrainCholinergic, Structures/VTA
- Motor: Structures/M1, Structures/SMA
- Each structure includes SERVICE_SPEC.md and Program.cs with biological justification + neuron model + circuit + plasticity + spike handler/dispatcher + feedback queue handling.

5. Connectivity configuration:
- connectivity/dnne-connectivity.json

6. Tests:
- Tests/NeuralResonanceEngine.DNNE.Tests/MajorPathwayIntegrationTests.cs

Solution entry point:
- NeuralResonanceEngine.DNNE.slnx
