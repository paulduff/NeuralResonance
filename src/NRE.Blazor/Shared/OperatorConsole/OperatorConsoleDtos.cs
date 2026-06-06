namespace NRE.Blazor.Shared.OperatorConsole;

public readonly record struct VoiceLogEntry(string Time, string Text);

public sealed record EngineStatusDto(
    bool Running, long StepIndex, float DtSeconds,
    int VolumeW, int VolumeH, int VolumeD,
    NeuromodulatorDto Neuromodulators, PonsDto Pons,
    int TotalNeurons = 0, int TotalSynapses = 0,
    ThalamicStatusDto? Thalamus = null,
    SleepStatusDto? Sleep = null,
    HippocampusStatusDto? Hippocampus = null,
    AmygdalaStatusDto? Amygdala = null,
    CerebellumStatusDto? Cerebellum = null,
    VocalTractStatusDto? VocalTract = null,
    PeerBridgeStatusDto? PeerBridge = null);

public sealed record NeuromodulatorDto(float Dopamine, float Serotonin, float Noradrenaline);
public sealed record PonsDto(float Arousal, float Stability, float ResetPressure, float ThetaHz);
public sealed record ThalamicStatusDto(float FrequencyHz, float Phase, bool IsAtPulse, float PulseAmplitude, long PulseCount);
public sealed record SleepStatusDto(string Phase, float PhaseTimerSeconds, float SleepPressure, int ConsolidationCycles, int ReplaysTriggered, bool IsDreaming, bool SensorsConnected);
public sealed record HippocampusStatusDto(int EpisodeCount, int AssociationCount, long OldestEpisodeAge, int TotalVoxelsCaptured);
public sealed record AmygdalaStatusDto(int SalientVoxelCount, bool NoradrenalinePulseActive, float PulseIntensity, float PulseRemainingMs, int SalientEventsPerSecond);
public sealed record CerebellumStatusDto(float GlobalPredictionError, float SmoothedActivity, int RegionCount);
public sealed record VocalTractStatusDto(
    bool IsSpeaking, int PendingGestures,
    float F0, float F1, float F2, float F3, float Amplitude,
    float JawOpen, float TongueHeight, float TongueAdvance,
    float LipRound, float VelumHeight, float Voicing);
public sealed record PeerBridgeStatusDto(
    string InstanceId, string? InstanceName, int PeerCount,
    int PendingIncomingSpeech, PeerStatusDto[] Peers);
public sealed record PeerStatusDto(
    string Id, string? Name, string LastHeardAt,
    int SpeechReceived, int SpeechSent,
    float Arousal, float Valence, bool IsSpeaking);

public sealed record InjectRequestDto(int X, int Y, int Z, float Intensity, int DelayTicks = 0, string Hemisphere = "left");
public sealed record ResonantClustersDto(long StepIndex, System.Numerics.Vector3[] PointsLeft, System.Numerics.Vector3[] PointsRight);
public sealed record ThoughtClustersDto(long StepIndex, ThoughtClusterDto[] Left, ThoughtClusterDto[] Right);
public sealed record ThoughtClusterDto(int Id, int Size, System.Numerics.Vector3 Centroid, float MeanDensity01, float Coherence01);
public sealed record PackedLinesDto(int Count, byte[] Data);
public sealed record RenderFrameFastDto(long StepIndex, PackedPoints Spikes, PackedTrafficEvents CrossModuleTraffic, float CallosalTraffic01, string? SleepPhase = null, bool? ThalamicPulseActive = null, float[]? Body = null);
public sealed record RenderHeatmapsDto(long StepIndex, PackedHeatmap VmDensityLeft, PackedHeatmap VmDensityRight);
public sealed record PackedTrafficEvents(int Count, byte[] Data);
public sealed record PackedPoints(int Count, byte[] Data);
public sealed record PackedHeatmap(int W, int H, int D, byte[] Data);
public sealed record TelemetrySnapshotDto(long StepIndex, float UptimeSeconds, NetworkTelemetryDto Network, EpisodeTelemetryDto Episodes, RegionTelemetryDto[] Regions, PathwayTelemetryDto[] Pathways, float[] FiringRateHistory);
public sealed record NetworkTelemetryDto(float GlobalMeanFiringRate, float LeftRightCoherence, float CallosalTrafficMean, float SynapticDiversity, float StructuralTurnover, int TotalConsolidations, float LearningProgress, float NetworkStability);
public sealed record EpisodeTelemetryDto(int TotalEpisodes, int TotalVoxelsCaptured, float MeanEpisodeSize, float MeanSalience, float MeanStrength, int StrongEpisodes, int WeakEpisodes, float OldestAgeSeconds, float NewestAgeSeconds);
public sealed record RegionTelemetryDto(byte RegionId, string Name, int NeuronCount, int SynapseCount, float MeanFiringRate, float MeanWeight, float WeightStdDev, float MeanUsage, float StrongestWeight, int PrunedSinceStart);
public sealed record PathwayTelemetryDto(string Name, int SynapseCount, float MeanWeight, float MeanUsage, float WeightSkew, int ActiveConnections);
public sealed record BodyStateDto(
    long StepIndex,
    float HeadTilt, float HeadNod,
    float ShoulderL, float ShoulderR,
    float ElbowL, float ElbowR,
    float WristL, float WristR,
    float HipL, float HipR,
    float KneeL, float KneeR,
    float TorsoLean, float TorsoTwist,
    float Curiosity, float Fear, float Pleasure, float Surprise,
    float Drowsiness, float Arousal,
    float AnimationEnergy);
