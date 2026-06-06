using System.Numerics;

namespace NRE.Core.Engine;

public sealed record EngineStatusDto(
    bool Running,
    long StepIndex,
    float DtSeconds,
    int VolumeW,
    int VolumeH,
    int VolumeD,
    NeuromodulatorDto Neuromodulators,
    PonsDto Pons,
    int TotalNeurons = 0,
    int TotalSynapses = 0,
    ThalamicStatusDto? Thalamus = null,
    SleepStatusDto? Sleep = null,
    HippocampusStatusDto? Hippocampus = null,
    AmygdalaStatusDto? Amygdala = null,
    CerebellumStatusDto? Cerebellum = null,
    PredictionStatusDto? Prediction = null,
    // Entry 025: New subsystem DTOs
    MicrocircuitStatusDto? Microcircuit = null,
    NeuromodSystemStatusDto? NeuromodSystem = null,
    SynapticTaggingStatusDto? SynapticTagging = null,
    SensoryHierarchyStatusDto? Sensory = null,
    AttentionStatusDto? Attention = null,
    // Entry 027: Language System
    LanguageStatusDto? Language = null,
    // Entry 028: P0 Systems
    BasalGangliaStatusDto? BasalGanglia = null,
    RewardPredictionStatusDto? RewardPrediction = null,
    WorkingMemoryStatusDto? WorkingMemory = null,
    ConsolidationStatusDto? Consolidation = null,
    // Entry 029: Diphthong Vocal Tract
    VocalTractStatusDto? VocalTract = null,
    // Entry 030: Peer Bridge
    PeerBridgeStatusDto? PeerBridge = null,
    // Performance metrics
    float WallClockUptimeSeconds = 0,
    float AvgStepMs = 0,
    float SimRatio = 1);

// Subsystem DTOs
public sealed record ThalamicStatusDto(
    float FrequencyHz, float Phase, bool IsAtPulse, float PulseAmplitude, long PulseCount,
    string Mode, float TRNInhibition, bool SpindleActive, float SpindleAmplitude);
public sealed record SleepStatusDto(string Phase, float PhaseTimerSeconds, float SleepPressure, int ConsolidationCycles, int ReplaysTriggered, bool IsDreaming, bool SensorsConnected);
public sealed record HippocampusStatusDto(
    int EpisodeCount, int AssociationCount, long OldestEpisodeAge, int TotalVoxelsCaptured,
    float DGSparsity, float CA3Coherence, float CA1NoveltySignal);
public sealed record AmygdalaStatusDto(
    int SalientVoxelCount, bool NoradrenalinePulseActive, float PulseIntensity, float PulseRemainingMs, 
    int SalientEventsPerSecond, float LAActivity, float BasalActivity, float CeAOutput, float ITCGating);
public sealed record CerebellumStatusDto(
    float GlobalPredictionError, float SmoothedActivity, int RegionCount,
    float ClimbingFiberSignal, float MossyFiberActivity, float PurkinjeOutput, float DeepNucleiOutput);
public sealed record PredictionStatusDto(float GlobalError, float GlobalPrecision);

// Entry 025: New subsystem DTOs
public sealed record MicrocircuitStatusDto(
    int PyramidalCount, int PVCount, int SOMCount, int VIPCount, int SubcorticalCount, float MeanAdaptation);
    
public sealed record NeuromodSystemStatusDto(
    float DopamineTonic, float DopaminePhasic, float D1Sensitivity, float D2Sensitivity,
    float NETonic, float NEPhasic, float BetaSensitivity,
    float SerotoninTonic, float SerotoninPhasic,
    float AChTonic, float AChPhasic,
    float EffectiveThresholdMod, float EffectiveLearningMod, float EffectiveGainMod);
    
public sealed record SynapticTaggingStatusDto(
    int UnconsolidatedTags, int ConsolidatedTags, int NeuronsWithPRP, 
    float MeanTagStrength, int TotalConsolidations);
    
public sealed record SensoryHierarchyStatusDto(
    float V1Activity, float V2Activity, float V4Activity, float ITActivity,
    float A1Activity, float BeltActivity, float ParabeltActivity,
    int DominantObjectChannel, float DominantObjectActivation);
    
public sealed record AttentionStatusDto(
    int ActiveFoci, float MeanPriority, float MaxPriority, int IORLocations);

// Entry 027: Language System
public sealed record LanguageStatusDto(
    int LexiconSize, int PhonologicalBufferCount, int ActiveSemanticNodes,
    float ComprehensionConfidence, float ProductionReadiness,
    float WernickeActivity, float BrocaActivity, int PendingOutputWords);

// Entry 028: Basal Ganglia Circuit
public sealed record BasalGangliaStatusDto(
    int SelectedChannel, float SelectionConfidence,
    float DirectPathwayActivity, float IndirectPathwayActivity,
    float STNActivity, float ThalamicGating,
    float DopamineLevel, int TotalSelections,
    float D1Activation, float D2Activation);

// Entry 028: Reward Prediction Error System
public sealed record RewardPredictionStatusDto(
    float TonicDopamine, float PhasicDopamine, float EffectiveDopamine,
    float LastRPE, float CurrentStateValue,
    int TotalRewards, float CumulativeReward,
    float MeanRPE, float RPEVariance,
    int LearnedStates, int LearnedActions);

// Entry 028: Working Memory PFC
public sealed record WorkingMemoryStatusDto(
    int ActiveSlots, float GateOpenness,
    float CurrentDopamine, float MeanPersistence,
    int TotalUpdates, int TotalEvictions,
    SlotStatusDto[] Slots);

public sealed record SlotStatusDto(
    bool IsActive, float Strength, float Age, string Label);

// Entry 028: Systems Consolidation
public sealed record ConsolidationStatusDto(
    string SlowOscPhase, bool SpindleActive, bool RippleActive,
    int TotalTraces, int HippocampusDependent,
    int CorticalOnly, int Transitional,
    int TotalReplays, int TotalConsolidations, int TotalTransfers,
    int TripletAlignments, int ConsolidationQueueSize,
    float CurrentConsolidationStrength);

// Entry 029: Diphthong Vocal Tract
public sealed record VocalTractStatusDto(
    bool IsSpeaking,
    int PendingGestures,
    float F0, float F1, float F2, float F3,
    float Amplitude,
    float JawOpen, float TongueHeight, float TongueAdvance,
    float LipRound, float VelumHeight, float Voicing);

// Entry 030: Peer Bridge
public sealed record PeerBridgeStatusDto(
    string InstanceId,
    string? InstanceName,
    int PeerCount,
    int PendingIncomingSpeech,
    PeerStatusDto[] Peers);

public sealed record PeerStatusDto(
    string Id, string? Name,
    string LastHeardAt,
    int SpeechReceived, int SpeechSent,
    float Arousal, float Valence, bool IsSpeaking);

public sealed record NeuromodulatorDto(float Dopamine, float Serotonin, float Noradrenaline);

// PonsDto is defined in PonsNucleus.cs

public sealed record InjectRequestDto(int X, int Y, int Z, float Intensity, int DelayTicks = 0, string Hemisphere = "left");

public sealed record ResonantClustersDto(long StepIndex, Vector3[] PointsLeft, Vector3[] PointsRight);

public sealed record RenderFrameDto(
    long StepIndex,
    PackedPoints Spikes,
    PackedHeatmap MetabolicLeft,
    PackedHeatmap MetabolicRight,
    PackedHeatmap VmDensityLeft,
    PackedHeatmap VmDensityRight,
    PackedTrafficEvents CrossModuleTraffic,
    float CallosalTraffic01,
    string? SleepPhase = null,
    bool? ThalamicPulseActive = null);

/// <summary>
/// Small, high-frequency UI frame: spikes + traffic + a few scalars.
/// Intentionally excludes heatmaps to keep server publish + JSON very cheap.
/// </summary>
public sealed record RenderFrameFastDto(
    long StepIndex,
    PackedPoints Spikes,
    PackedTrafficEvents CrossModuleTraffic,
    float CallosalTraffic01,
    string? SleepPhase = null,
    bool? ThalamicPulseActive = null,
    float[]? Body = null);

/// <summary>
/// Low-frequency UI payload: volumetric heatmaps.
/// Kept separate because the byte arrays are expensive to base64 encode.
/// </summary>
public sealed record RenderHeatmapsDto(
    long StepIndex,
    PackedHeatmap VmDensityLeft,
    PackedHeatmap VmDensityRight);

/// <summary>
/// Packed point layout per point (6 bytes):
/// [hemiByte, x, y, z, energyByte, regionByte]
/// hemiByte: 0=Left, 1=Right
/// regionByte: 0=Cortex/default, 1=Thalamus, 2=Hippocampus, 3=MemoryNuclei
/// </summary>
public sealed record PackedPoints(int Count, byte[] Data);

public sealed record PackedHeatmap(int W, int H, int D, byte[] Data);

/// <summary>
/// Cross-module traffic events packed as 11 bytes per event:
/// [preHemi, preX, preY, preZ, preRegion,
///  postHemi, postX, postY, postZ, postRegion,
///  strengthByte]
///
/// strengthByte is 0..255 (approx synaptic efficacy * 255).
/// </summary>
public sealed record PackedTrafficEvents(int Count, byte[] Data);

public sealed record ThoughtClustersDto(long StepIndex, ThoughtClusterDto[] Left, ThoughtClusterDto[] Right);

public sealed record ThoughtClusterDto(
    int Id,
    int Size,
    Vector3 Centroid,
    float MeanDensity01,
    float Coherence01);

public sealed record PackedLines(int Count, byte[] Data);

// ── Telemetry: deep insight into what the network has absorbed ──

/// <summary>Per-region firing rate and synapse statistics.</summary>
public sealed record RegionTelemetryDto(
    byte RegionId,
    string Name,
    int NeuronCount,
    int SynapseCount,
    float MeanFiringRate,    // spikes per neuron per second
    float MeanWeight,        // average absolute synaptic weight
    float WeightStdDev,      // weight distribution spread (learning indicator)
    float MeanUsage,         // average UsageEma01 (how active are connections)
    float StrongestWeight,   // max |W| in region (strongest learned connection)
    int PrunedSinceStart);   // total pruned synapses (structural adaptation)

/// <summary>Per-pathway connectivity health.</summary>
public sealed record PathwayTelemetryDto(
    string Name,
    int SynapseCount,
    float MeanWeight,
    float MeanUsage,
    float WeightSkew,        // positive = potentiated, negative = depressed
    int ActiveConnections);  // connections with usage > threshold

/// <summary>Episode memory summary with richness metrics.</summary>
public sealed record EpisodeTelemetryDto(
    int TotalEpisodes,
    int TotalVoxelsCaptured,
    float MeanEpisodeSize,
    float MeanSalience,
    float MeanStrength,
    int StrongEpisodes,      // episodes with strength > 0.5
    int WeakEpisodes,        // episodes with strength < 0.2
    float OldestAgeSeconds,
    float NewestAgeSeconds);

/// <summary>Network-level adaptation and learning metrics.</summary>
public sealed record NetworkTelemetryDto(
    float GlobalMeanFiringRate,    // overall activity level
    float LeftRightCoherence,      // how similar L/R hemisphere activity is
    float CallosalTrafficMean,     // average callosal transfer
    float SynapticDiversity,       // entropy of weight distribution (higher = more differentiated)
    float StructuralTurnover,      // rate of synapse creation/pruning
    int TotalConsolidations,       // synaptic tags consolidated into LTM
    float LearningProgress,        // composite metric: weight change rate
    float NetworkStability);       // 1 - coefficient of variation of firing rates

/// <summary>Complete telemetry snapshot.</summary>
public sealed record TelemetrySnapshotDto(
    long StepIndex,
    float UptimeSeconds,
    NetworkTelemetryDto Network,
    EpisodeTelemetryDto Episodes,
    RegionTelemetryDto[] Regions,
    PathwayTelemetryDto[] Pathways,
    float[] FiringRateHistory);    // last N firing rate samples for sparkline

// ── Body Avatar: motor cortex → stick figure joints ──

/// <summary>
/// Maps neural activity to a humanoid stick figure.
/// Motor cortex (M1/precentral) is somatotopically organized:
///   dorsal-medial → leg/foot, middle → trunk/arm, lateral → hand/face
/// We sample firing rates from M1 voxel strips to drive joint angles.
/// </summary>
public sealed record BodyStateDto(
    long StepIndex,
    // Joint activations: 0 = resting, positive = flexion, negative = extension
    // Each is -1..+1 range representing muscle activation
    float HeadTilt,        // lateral head tilt (curiosity)
    float HeadNod,         // up/down (attention/drowsiness)
    float ShoulderL,       // left shoulder raise
    float ShoulderR,       // right shoulder raise
    float ElbowL,          // left elbow flex
    float ElbowR,          // right elbow flex
    float WristL,          // left wrist rotation
    float WristR,          // right wrist rotation
    float HipL,            // left hip flex
    float HipR,            // right hip flex
    float KneeL,           // left knee flex
    float KneeR,           // right knee flex
    float TorsoLean,       // forward/back lean
    float TorsoTwist,      // left/right rotation
    // Facial expression drives
    float Curiosity,       // 0..1 (dopamine + novelty signal)
    float Fear,            // 0..1 (amygdala CeA output)
    float Pleasure,        // 0..1 (positive valence)
    float Surprise,        // 0..1 (prediction error)
    float Drowsiness,      // 0..1 (sleep pressure)
    float Arousal,         // 0..1 (pons arousal)
    // Overall energy/animation speed
    float AnimationEnergy  // 0..1 (global arousal → movement speed)
);
