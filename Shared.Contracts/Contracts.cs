using NeuralResonanceEngine.Protocol;
using ProtoBuf;
using ProtoBuf.Grpc;
using ProtoBuf.Grpc.Configuration;

namespace NeuralResonanceEngine.Shared.Contracts;

public enum BrainRhythm
{
    DELTA,
    THETA,
    ALPHA,
    BETA,
    GAMMA
}

public sealed record AttentionVector(float Visual, float Auditory, float Somatosensory, float Interoceptive);

public sealed record TickSignal(
    long Tick,
    double TimestampMs,
    double TickDurationMs,
    NeuromodState GlobalNeuromodState,
    IReadOnlyDictionary<BrainRhythm, double> PhaseContext,
    float RewardPredictionError);

public sealed record TickAck(
    StructureId StructureId,
    long Tick,
    int SpikeCount,
    float MeanFiringRateHz,
    int FeedbackQueueDepth,
    int SpikeInCount,
    int SpikeOutCount,
    int ActiveNeuronCount,
    BrainRhythm DominantRhythm,
    NeuromodState LocalNeuromod,
    MicrotubuleDiagnostics? MicrotubuleDiagnostics = null,
    BodySchemaDiagnostics? BodySchemaDiagnostics = null,
    BasalGangliaDiagnostics? BasalGangliaDiagnostics = null,
    CerebellarDiagnostics? CerebellarDiagnostics = null,
    VestibuloReticularDiagnostics? VestibuloReticularDiagnostics = null,
    SuperiorColliculusDiagnostics? SuperiorColliculusDiagnostics = null,
    HippocampalSpatialDiagnostics? HippocampalSpatialDiagnostics = null,
    SalienceAffectDiagnostics? SalienceAffectDiagnostics = null,
    PrefrontalWorkingMemoryDiagnostics? PrefrontalWorkingMemoryDiagnostics = null,
    ThalamicAttentionGateDiagnostics? ThalamicAttentionGateDiagnostics = null,
    HypothalamicHomeostasisDiagnostics? HypothalamicHomeostasisDiagnostics = null,
    SleepWakeArousalDiagnostics? SleepWakeArousalDiagnostics = null,
    DescendingDefenseDiagnostics? DescendingDefenseDiagnostics = null,
    DopamineRewardDiagnostics? DopamineRewardDiagnostics = null,
    SeptohippocampalThetaDiagnostics? SeptohippocampalThetaDiagnostics = null,
    SpinalProprioceptiveDiagnostics? SpinalProprioceptiveDiagnostics = null,
    OlfactoryLimbicMemoryDiagnostics? OlfactoryLimbicMemoryDiagnostics = null);

public sealed record MicrotubuleDiagnostics(
    string Mode,
    bool Enabled,
    bool Experimental,
    float MeanStability,
    float MeanSpineInvasionEligibility,
    float MeanTransportSupport,
    float MeanOpticalCollectiveBias,
    float MeanRadicalPairSensitivity,
    float MeanPlasticitySupport,
    float MeanTracePersistenceSupport,
    float MeanIntegrationGain,
    float MeanConsolidationSupport);

public sealed record BodySchemaDiagnostics(
    string DominantBodyZone,
    string DominantSpatialZone,
    float FaceHeadActivation,
    float HandArmActivation,
    float TrunkActivation,
    float LegFootActivation,
    float NearBodyActivation,
    float LeftPeripersonalActivation,
    float RightPeripersonalActivation,
    float FarSpaceActivation);

public sealed record BasalGangliaDiagnostics(
    string DominantMode,
    float DirectPathwayActivation,
    float IndirectPathwayActivation,
    float HyperdirectPathwayActivation,
    float OutputNucleusInhibition,
    float ThalamicDisinhibition,
    float DopamineModulation,
    float ActionSelectionBias);

public sealed record CerebellarDiagnostics(
    string CorrectionMode,
    float MossyFiberDrive,
    float ClimbingFiberError,
    float PurkinjeInhibition,
    float DeepNucleusOutput,
    float VermisStabilization,
    float CorrectionGain,
    float PredictionError);

public sealed record VestibuloReticularDiagnostics(
    string PostureMode,
    float VestibularDrive,
    float ReticularArousal,
    float VermisBalanceCorrection,
    float SpinalMotorTone,
    float PostureStability,
    float BalanceError);

public sealed record SuperiorColliculusDiagnostics(
    string OrientingMode,
    float VisualOrientingDrive,
    float AuditoryOrientingDrive,
    float NigrotectalInhibition,
    float PulvinarAttention,
    float HeadEyeCommand,
    float SaccadeReadiness,
    float SalienceBias);

public sealed record HippocampalSpatialDiagnostics(
    string MemoryMode,
    float EntorhinalGridDrive,
    float DentatePatternSeparation,
    float Ca3PatternCompletion,
    float Ca1PlaceIndex,
    float SubicularOutput,
    float HeadDirectionAlignment,
    float SpatialCoherence,
    float NoveltyMismatch);

public sealed record SalienceAffectDiagnostics(
    string SalienceMode,
    float ThreatSalience,
    float InteroceptiveDrive,
    float ConflictMonitoring,
    float AutonomicArousal,
    float AttentionGain,
    float DefensiveReadiness,
    float ControlBias,
    float AffectIntensity);

public sealed record PrefrontalWorkingMemoryDiagnostics(
    string ControlMode,
    float PfcPersistentActivity,
    float MediodorsalThalamicSupport,
    float FrontoparietalContext,
    float SemanticContext,
    float StriatalGate,
    float AccControlDemand,
    float TopDownBias,
    float TaskSetStability);

public sealed record ThalamicAttentionGateDiagnostics(
    string GateMode,
    float ThalamocorticalRelay,
    float TrnInhibitoryGate,
    float PulvinarSpotlight,
    float MediodorsalAccess,
    float IntralaminarBroadcast,
    float SensoryGain,
    float CorticalAccess,
    float RelaySelectionBias);

public sealed record HypothalamicHomeostasisDiagnostics(
    string HomeostasisMode,
    float VisceralAfferentDrive,
    float HypothalamicSetpointError,
    float InsulaBodyFeeling,
    float LimbicHomeostaticPressure,
    float AutonomicBrainstemDrive,
    float ArousalPressure,
    float ComfortDeficit,
    float DefensiveBodyCommand);

public sealed record SleepWakeArousalDiagnostics(
    string ArousalMode,
    float HypothalamicSleepPressure,
    float ReticularActivatingDrive,
    float PontomedullaryStateTone,
    float LocusCoeruleusWakeTone,
    float RapheStabilizationTone,
    float BasalForebrainWakeDrive,
    float IntralaminarArousalBroadcast,
    float CorticalReadiness);

public sealed record DescendingDefenseDiagnostics(
    string DefenseMode,
    float AmygdalaThreatDrive,
    float HypothalamicDefenseDrive,
    float PagDefensiveCommand,
    float RaphePainModulation,
    float MedullaryAutonomicSupport,
    float ReticularPatternRelease,
    float SpinalWithdrawalDrive,
    float ProtectionReadiness);

public sealed record DopamineRewardDiagnostics(
    string RewardMode,
    float VtaPhasicDopamine,
    float SncActionTeaching,
    float NucleusAccumbensIncentive,
    float StriatalActionValue,
    float HabenulaNegativePrediction,
    float OrbitofrontalExpectedValue,
    float PfcGoalBias,
    float RewardPredictionError,
    float LearningReadiness);

public sealed record SeptohippocampalThetaDiagnostics(
    string ThetaMode,
    float SeptalThetaDrive,
    float EntorhinalGridPhase,
    float DentateEncodingGate,
    float Ca3SequenceReplay,
    float Ca1PlaceTiming,
    float SubicularNavigationOutput,
    float HeadDirectionAlignment,
    float RetrosplenialSceneAnchor,
    float VestibularPathIntegration,
    float ThetaCoherence);

public sealed record SpinalProprioceptiveDiagnostics(
    string ReflexMode,
    float SpinalReflexDrive,
    float S1ProprioceptiveMap,
    float M1DescendingCommand,
    float CerebellarMossyFeedback,
    float VestibularBalanceInput,
    float ReticularPosturalSet,
    float ThalamicRelayTone,
    float ReflexReadiness,
    float ProprioceptiveCoherence);

public sealed record OlfactoryLimbicMemoryDiagnostics(
    string MemoryMode,
    float OlfactoryCueDrive,
    float TemporalPiriformAssociation,
    float AmygdalaAffectiveTag,
    float EntorhinalMemoryGate,
    float HippocampalEpisodeIndex,
    float OrbitofrontalValenceContext,
    float PfcAutobiographicalControl,
    float FamiliaritySignal,
    float AutobiographicalCoherence);

public sealed record StructureStepRequest(
    TickSignal TickSignal,
    int TopK,
    bool IncludeTop);

public sealed record StructureStepResult(
    TickAck Ack,
    IReadOnlyList<SpikeMessage> OutboundSpikes,
    IReadOnlyList<NeuronActivity> TopActiveNeurons);

public sealed record PublishedStepMessage(
    string InstanceKey,
    string Hemisphere,
    StructureId StructureId,
    StructureStepResult Step);

public sealed record NeuronActivity(string NeuronId, float FiringRateHz);

public sealed record StructureSnapshot(
    StructureId StructureId,
    int ActiveNeuronCount,
    float MeanFiringRateHz,
    BrainRhythm DominantRhythm,
    IReadOnlyList<NeuronActivity> TopActiveNeurons,
    NeuromodState NeuromodLocal,
    int SpikeInCount,
    int SpikeOutCount,
    int FeedbackQueueDepth,
    MicrotubuleDiagnostics? MicrotubuleDiagnostics = null,
    BodySchemaDiagnostics? BodySchemaDiagnostics = null,
    BasalGangliaDiagnostics? BasalGangliaDiagnostics = null,
    CerebellarDiagnostics? CerebellarDiagnostics = null,
    VestibuloReticularDiagnostics? VestibuloReticularDiagnostics = null,
    SuperiorColliculusDiagnostics? SuperiorColliculusDiagnostics = null,
    HippocampalSpatialDiagnostics? HippocampalSpatialDiagnostics = null,
    SalienceAffectDiagnostics? SalienceAffectDiagnostics = null,
    PrefrontalWorkingMemoryDiagnostics? PrefrontalWorkingMemoryDiagnostics = null,
    ThalamicAttentionGateDiagnostics? ThalamicAttentionGateDiagnostics = null,
    HypothalamicHomeostasisDiagnostics? HypothalamicHomeostasisDiagnostics = null,
    SleepWakeArousalDiagnostics? SleepWakeArousalDiagnostics = null,
    DescendingDefenseDiagnostics? DescendingDefenseDiagnostics = null,
    DopamineRewardDiagnostics? DopamineRewardDiagnostics = null,
    SeptohippocampalThetaDiagnostics? SeptohippocampalThetaDiagnostics = null,
    SpinalProprioceptiveDiagnostics? SpinalProprioceptiveDiagnostics = null,
    OlfactoryLimbicMemoryDiagnostics? OlfactoryLimbicMemoryDiagnostics = null);

public sealed record ActivePathway(
    StructureId Source,
    StructureId Target,
    int SpikeVolume,
    NTEnum DominantNt);

public sealed record BrainSnapshot(
    long Tick,
    double TimestampMs,
    NeuromodState NeuromodState,
    IReadOnlyDictionary<BrainRhythm, double> OscillationPhases,
    float RewardPredictionError,
    IReadOnlyList<StructureSnapshot> StructureStates,
    IReadOnlyList<ActivePathway> ActivePathways);

public sealed record SynapticConnection(StructureId Target, Guid SynapseId, NTEnum Neurotransmitter, string ProjectionType);
public sealed record ConnectivityRule(StructureId Source, IReadOnlyList<SynapticConnection> Connections);


[ProtoContract]
public sealed class SpikeBatchEnvelope
{
    [ProtoMember(1)]
    public List<SpikeMessage> Spikes { get; set; } = [];
}

[ProtoContract]
public sealed class SpikeBatchAck
{
    [ProtoMember(1)]
    public int Accepted { get; set; }

    [ProtoMember(2)]
    public string Error { get; set; } = string.Empty;
}

public interface IStructureSpikeTransport
{
    ValueTask<SpikeBatchAck> PushSpikeBatchAsync(SpikeBatchEnvelope request, CallContext context = default);

    // Bidirectional stream of spike batches with per-batch ACKs. A single long-lived
    // stream per (control, structure) pair replaces per-call HEADERS frame overhead
    // and lets HTTP/2 stream windows do flow control. Opt-in via the
    // NRE_USE_GRPC_BIDI_STREAM environment variable on the ControlProgram side.
    IAsyncEnumerable<SpikeBatchAck> StreamSpikeBatchesAsync(IAsyncEnumerable<SpikeBatchEnvelope> requests, CallContext context = default);
}
public interface IStructureHost
{
    ValueTask EnqueueSpikeAsync(SpikeMessage message, CancellationToken cancellationToken = default);
    ValueTask<TickAck> ProcessTickAsync(TickSignal tickSignal, CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<SpikeMessage>> DrainOutboundSpikesAsync(CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<NeuronActivity>> GetTopActiveNeuronsAsync(int topK, CancellationToken cancellationToken = default);
}

public interface ISnapshotSink
{
    ValueTask AppendAsync(BrainSnapshot snapshot, CancellationToken cancellationToken = default);
}
