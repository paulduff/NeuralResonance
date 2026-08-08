using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

internal sealed record NeuronalLanguageGroundingDecision(
    bool CircuitObserved,
    bool Available,
    bool Grounded,
    bool IsSleeping,
    int PerceptEnsemble,
    double PerceptConfidence,
    int MemoryEnsemble,
    double MemoryConfidence,
    int AttentionChannel,
    double LanguageAttention,
    double AttentionConfidence,
    double LanguageCircuitCoverage,
    double ComprehensionDrive,
    double ExpressionDrive,
    double GroundingConfidence,
    double Uncertainty,
    bool SpeechAuthorized,
    IReadOnlyList<DyadNeuronalGroundingSource> Sources)
{
    public const string Authority = "DistributedGroundedLanguageCircuits";

    public static NeuronalLanguageGroundingDecision Unavailable { get; } = new(
        false,
        false,
        false,
        false,
        -1,
        0.0,
        -1,
        0.0,
        -1,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        1.0,
        false,
        []);
}

internal sealed record NeuronalLanguageGroundingRuntimeSnapshot(
    long Tick,
    string Authority,
    NeuronalLanguageGroundingDecision Grounding);

internal sealed class NeuronalLanguageGroundingRuntime
{
    private readonly object _gate = new();
    private long _tick = -1;
    private NeuronalLanguageGroundingDecision _decision = NeuronalLanguageGroundingDecision.Unavailable;

    public NeuronalLanguageGroundingDecision Update(
        long tick,
        NeuronalPerceptDecision percept,
        NeuronalMemoryDecision memory,
        NeuronalAttentionWorkspaceDecision attention,
        NeuronalSleepConsolidationDecision sleep,
        IReadOnlyList<InstanceStructureSnapshot> snapshots)
    {
        var decoded = NeuronalLanguageGroundingDecoder.Decode(
            tick,
            percept,
            memory,
            attention,
            sleep,
            snapshots);
        lock (_gate)
        {
            if (tick >= _tick)
            {
                _tick = tick;
                _decision = decoded;
            }

            return _decision;
        }
    }

    public NeuronalLanguageGroundingRuntimeSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return new NeuronalLanguageGroundingRuntimeSnapshot(
                _tick,
                NeuronalLanguageGroundingDecision.Authority,
                _decision);
        }
    }
}

internal static class NeuronalLanguageGroundingDecoder
{
    public const int LanguageAttentionChannel = 5;

    public static NeuronalLanguageGroundingDecision Decode(
        long tick,
        NeuronalPerceptDecision percept,
        NeuronalMemoryDecision memory,
        NeuronalAttentionWorkspaceDecision attention,
        NeuronalSleepConsolidationDecision sleep,
        IReadOnlyList<InstanceStructureSnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(percept);
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(attention);
        ArgumentNullException.ThrowIfNull(sleep);
        ArgumentNullException.ThrowIfNull(snapshots);

        var languageSnapshots = snapshots
            .Where(static snapshot => snapshot.AuditoryLanguageMotorDiagnostics is not null)
            .ToArray();
        if (languageSnapshots.Length == 0)
        {
            return NeuronalLanguageGroundingDecision.Unavailable;
        }

        var a1 = Role(languageSnapshots, StructureId.A1, static item => item.A1AuditoryDrive);
        var wernicke = Role(languageSnapshots, StructureId.WernickePstgPsts, static item => item.WernickeComprehension);
        var arcuate = Role(languageSnapshots, StructureId.ArcuateFasciculus, static item => item.ArcuatePhonologicalRelay);
        var broca = Role(languageSnapshots, StructureId.BrocaBa44Ba45, static item => item.BrocaSpeechSequence);
        var premotor = Role(languageSnapshots, StructureId.PremotorCortex, static item => item.PremotorArticulationPlan);
        var m1 = Role(languageSnapshots, StructureId.M1, static item => item.M1SpeechMotorCommand);
        var basalGanglia = Role(languageSnapshots,
            [StructureId.Striatum, StructureId.GPi, StructureId.Snr],
            static item => item.BasalGangliaSpeechGate);
        var motorThalamus = Role(languageSnapshots,
            [StructureId.MotorThalamus, StructureId.Thalamus],
            static item => item.MotorThalamicRelay);

        var roles = new[] { a1, wernicke, arcuate, broca, premotor, m1, basalGanglia, motorThalamus };
        var comprehensionRoles = new[] { a1, wernicke, arcuate };
        var expressionRoles = new[] { broca, premotor, m1, basalGanglia, motorThalamus };
        var circuitCoverage = roles.Count(static role => role.Observed) / (double)roles.Length;
        var comprehensionCoverage = comprehensionRoles.Count(static role => role.Observed) /
                                    (double)comprehensionRoles.Length;
        var expressionCoverage = expressionRoles.Count(static role => role.Observed) /
                                 (double)expressionRoles.Length;
        var comprehensionDrive = ChainSupport(comprehensionRoles);
        var expressionDrive = ChainSupport(expressionRoles);

        var perceptActive = percept.Available && percept.Active && percept.DominantEnsemble >= 0;
        var memoryActive = memory.Available && memory.RecallActive && memory.RecalledEnsemble >= 0;
        var perceptConfidence = perceptActive ? Math.Clamp(percept.Confidence, 0.0, 1.0) : 0.0;
        var memoryConfidence = memoryActive ? Math.Clamp(memory.RecallStrength, 0.0, 1.0) : 0.0;
        var referenceAvailable = percept.Available || memory.Available;
        var referencesAgree = perceptActive && memoryActive &&
                              percept.DominantEnsemble == memory.RecalledEnsemble;
        var referencesConflict = perceptActive && memoryActive && !referencesAgree;
        var referenceConfidence = referencesAgree
            ? Math.Clamp((perceptConfidence * 0.55) + (memoryConfidence * 0.45) + 0.08, 0.0, 1.0)
            : referencesConflict
                ? Math.Clamp((perceptConfidence * 0.55) + (memoryConfidence * 0.25) - 0.20, 0.0, 1.0)
                : Math.Max(perceptConfidence, memoryConfidence);
        var groundingConfidence = Math.Clamp(
            (referenceConfidence * 0.72) +
            (comprehensionDrive * 0.18) +
            (comprehensionCoverage * 0.10),
            0.0,
            1.0);
        var uncertainty = Math.Clamp(
            1.0 - groundingConfidence + (referencesConflict ? 0.25 : 0.0),
            0.0,
            1.0);

        var languageAttention = attention.ChannelScores.Count > LanguageAttentionChannel
            ? Math.Clamp(attention.ChannelScores[LanguageAttentionChannel], 0.0, 1.0)
            : 0.0;
        var attentionConfidence = attention.Active
            ? Math.Clamp((attention.SelectionMargin * 4.0) + (attention.CircuitCoverage * 0.25), 0.0, 1.0)
            : 0.0;
        var languageSelected = attention.Available &&
                               attention.Active &&
                               attention.SelectedChannel == LanguageAttentionChannel &&
                               attention.BroadcastActive &&
                               attention.BroadcastChannel == LanguageAttentionChannel;
        var isSleeping = sleep.StateActive && sleep.State != NeuronalSleepState.Wake;
        var available = referenceAvailable && attention.Available && comprehensionCoverage >= 1.0;
        var grounded = available &&
                       (perceptActive || memoryActive) &&
                       comprehensionDrive > 0.005 &&
                       groundingConfidence >= 0.20 &&
                       uncertainty <= 0.85;
        var speechAuthorized = grounded &&
                               !isSleeping &&
                               languageSelected &&
                               expressionCoverage >= 1.0 &&
                               expressionDrive > 0.005 &&
                               uncertainty <= 0.70;

        var sources = BuildSources(
            tick,
            perceptActive,
            percept,
            memoryActive,
            memory,
            attention,
            sleep,
            comprehensionDrive,
            expressionDrive,
            circuitCoverage);

        return new NeuronalLanguageGroundingDecision(
            true,
            available,
            grounded,
            isSleeping,
            perceptActive ? percept.DominantEnsemble : -1,
            perceptConfidence,
            memoryActive ? memory.RecalledEnsemble : -1,
            memoryConfidence,
            attention.Active ? attention.SelectedChannel : -1,
            languageAttention,
            attentionConfidence,
            circuitCoverage,
            comprehensionDrive,
            expressionDrive,
            groundingConfidence,
            uncertainty,
            speechAuthorized,
            sources);
    }

    private static IReadOnlyList<DyadNeuronalGroundingSource> BuildSources(
        long tick,
        bool perceptActive,
        NeuronalPerceptDecision percept,
        bool memoryActive,
        NeuronalMemoryDecision memory,
        NeuronalAttentionWorkspaceDecision attention,
        NeuronalSleepConsolidationDecision sleep,
        double comprehensionDrive,
        double expressionDrive,
        double circuitCoverage)
    {
        var sources = new List<DyadNeuronalGroundingSource>(6);
        if (perceptActive)
        {
            sources.Add(new(
                "neuronal-percept-ensemble",
                percept.DominantEnsemble,
                (float)Math.Clamp(percept.Confidence, 0.0, 1.0),
                tick,
                $"coverage={percept.CircuitCoverage:0.000};margin={percept.DominanceMargin:0.000}"));
        }

        if (memoryActive)
        {
            sources.Add(new(
                "persisted-synaptic-recall",
                memory.RecalledEnsemble,
                (float)Math.Clamp(memory.RecallStrength, 0.0, 1.0),
                tick,
                $"margin={memory.RecallMargin:0.000};synapses={memory.LearnedSynapseCount}"));
        }

        sources.Add(new(
            "neuronal-attention-workspace",
            attention.Active ? attention.SelectedChannel : -1,
            (float)Math.Clamp(attention.SelectionMargin * 4.0, 0.0, 1.0),
            tick,
            $"broadcast={attention.BroadcastActive};coverage={attention.CircuitCoverage:0.000}"));
        sources.Add(new(
            "auditory-comprehension-chain",
            -1,
            (float)comprehensionDrive,
            tick,
            $"language-circuit-coverage={circuitCoverage:0.000}"));
        sources.Add(new(
            "speech-expression-chain",
            -1,
            (float)expressionDrive,
            tick,
            $"language-circuit-coverage={circuitCoverage:0.000}"));
        sources.Add(new(
            "neuronal-sleep-state",
            sleep.StateActive ? (int)sleep.State : -1,
            (float)Math.Clamp(sleep.StateConfidence, 0.0, 1.0),
            tick,
            $"observed={sleep.CircuitObserved};available={sleep.Available}"));
        return sources;
    }

    private static CircuitRole Role(
        IReadOnlyList<InstanceStructureSnapshot> snapshots,
        StructureId structure,
        Func<AuditoryLanguageMotorDiagnostics, float> selector)
        => Role(snapshots, [structure], selector);

    private static CircuitRole Role(
        IReadOnlyList<InstanceStructureSnapshot> snapshots,
        IReadOnlyList<StructureId> structures,
        Func<AuditoryLanguageMotorDiagnostics, float> selector)
    {
        var values = snapshots
            .Where(snapshot => structures.Contains(snapshot.StructureId))
            .Select(static snapshot => snapshot.AuditoryLanguageMotorDiagnostics!)
            .Select(selector)
            .ToArray();
        return values.Length == 0
            ? new CircuitRole(false, 0.0)
            : new CircuitRole(true, values.Max());
    }

    private static double ChainSupport(IReadOnlyList<CircuitRole> roles)
    {
        if (roles.Count == 0 || roles.Any(static role => !role.Observed))
        {
            return 0.0;
        }

        return roles.Min(static role => NormalizeDrive(role.Drive));
    }

    private static double NormalizeDrive(double drive)
        => 1.0 - Math.Exp(-Math.Max(0.0, drive) / 10.0);

    private readonly record struct CircuitRole(bool Observed, double Drive);
}
