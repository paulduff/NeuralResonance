internal sealed record NeuronalCognitionDomainAuthority(
    string Domain,
    bool CircuitObserved,
    bool NeuronalAvailable,
    bool NeuronalActive,
    bool LegacyTelemetryOnly,
    bool LegacyCanAuthorize,
    string AuthoritativeEndpoint,
    string Evidence);

internal sealed record NeuronalCognitionAuthoritySnapshot(
    long Tick,
    string Authority,
    bool FullyObserved,
    bool SymbolicScaffoldCanAuthorize,
    bool SemanticMotorInjectionAllowed,
    bool WorldGoalSteeringAllowed,
    bool LegacyLanguageEmissionAllowed,
    IReadOnlyList<NeuronalCognitionDomainAuthority> Domains);

internal sealed class NeuronalCognitionAuthorityRuntime
{
    public const string Authority = "NeuronalOnlyCognitionAuthority";
    private readonly object _gate = new();
    private NeuronalCognitionAuthoritySnapshot _snapshot = Empty(-1);

    public NeuronalCognitionAuthoritySnapshot Update(
        long tick,
        NeuronalPerceptDecision percept,
        NeuronalMemoryDecision memory,
        NeuronalAttentionWorkspaceDecision attention,
        NeuronalSleepConsolidationDecision sleep,
        NeuronalLanguageGroundingDecision language,
        NeuronalMotorRuntime motor)
    {
        ArgumentNullException.ThrowIfNull(percept);
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(attention);
        ArgumentNullException.ThrowIfNull(sleep);
        ArgumentNullException.ThrowIfNull(language);
        ArgumentNullException.ThrowIfNull(motor);

        var domains = new[]
        {
            Domain("perception", percept.Available, percept.Available, percept.Active,
                "/api/v1/neuronal-perception",
                $"ensemble={percept.DominantEnsemble};coverage={percept.CircuitCoverage:0.000}"),
            Domain("memory", memory.Available, memory.Available, memory.RecallActive,
                "/api/v1/neuronal-memory",
                $"ensemble={memory.RecalledEnsemble};synapses={memory.LearnedSynapseCount}"),
            Domain("attention-workspace", attention.Available, attention.Available, attention.Active,
                "/api/v1/neuronal-attention-workspace",
                $"channel={attention.SelectedChannel};coverage={attention.CircuitCoverage:0.000}"),
            Domain("sleep-consolidation", sleep.CircuitObserved, sleep.Available, sleep.StateActive,
                "/api/v1/neuronal-sleep-consolidation",
                $"state={(int)sleep.State};replay={sleep.ReplayEnsemble};coverage={sleep.CircuitCoverage:0.000}"),
            Domain("language-grounding", language.CircuitObserved, language.Available, language.Grounded,
                "/api/v1/neuronal-language-grounding",
                $"percept={language.PerceptEnsemble};recall={language.MemoryEnsemble};uncertainty={language.Uncertainty:0.000}"),
            Domain("action-selection", motor.ActionCircuitObserved, motor.ActionCircuitObserved, motor.SelectedActionChannel >= 0,
                "/api/v1/neuronal-motor",
                $"channel={motor.SelectedActionChannel};coverage={motor.ActionCircuitCoverage:0.000}"),
            Domain("motor-output", motor.MotorCircuitCoverage > 0.0, motor.MotorCircuitCoverage >= 0.45, motor.Active,
                "/api/v1/neuronal-motor",
                $"mode={motor.Mode};coverage={motor.MotorCircuitCoverage:0.000};confidence={motor.Confidence:0.000}")
        };
        var next = new NeuronalCognitionAuthoritySnapshot(
            tick,
            Authority,
            domains.All(static domain => domain.CircuitObserved),
            SymbolicScaffoldCanAuthorize: false,
            SemanticMotorInjectionAllowed: false,
            WorldGoalSteeringAllowed: false,
            LegacyLanguageEmissionAllowed: false,
            domains);
        lock (_gate)
        {
            if (tick >= _snapshot.Tick)
            {
                _snapshot = next;
            }

            return _snapshot;
        }
    }

    public NeuronalCognitionAuthoritySnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return _snapshot;
        }
    }

    private static NeuronalCognitionDomainAuthority Domain(
        string domain,
        bool observed,
        bool available,
        bool active,
        string endpoint,
        string evidence)
        => new(
            domain,
            observed,
            available,
            active,
            LegacyTelemetryOnly: true,
            LegacyCanAuthorize: false,
            endpoint,
            evidence);

    private static NeuronalCognitionAuthoritySnapshot Empty(long tick)
        => new(
            tick,
            Authority,
            FullyObserved: false,
            SymbolicScaffoldCanAuthorize: false,
            SemanticMotorInjectionAllowed: false,
            WorldGoalSteeringAllowed: false,
            LegacyLanguageEmissionAllowed: false,
            []);
}
