using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

internal sealed record NeuronalMemoryDecision(
    bool Available,
    bool RecallActive,
    int RecalledEnsemble,
    double RecallStrength,
    double RecallMargin,
    double CueDrive,
    double EngramStrength,
    double Interference,
    double Extinction,
    double HippocampalDependence,
    double CorticalConsolidation,
    int LearnedSynapseCount,
    bool HippocampalEncodingAvailable,
    IReadOnlyList<SynapticMemoryEnsembleActivity> Ensembles)
{
    public const string Authority = "PersistedSynapticState";

    public static NeuronalMemoryDecision Unavailable { get; } = new(
        false,
        false,
        -1,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0,
        false,
        []);
}

internal sealed record NeuronalMemorySnapshot(
    long Tick,
    string Authority,
    NeuronalMemoryDecision Memory);

internal sealed class NeuronalMemoryRuntime
{
    private readonly object _gate = new();
    private long _tick = -1;
    private NeuronalMemoryDecision _memory = NeuronalMemoryDecision.Unavailable;

    public NeuronalMemoryDecision Update(long tick, IReadOnlyList<InstanceStructureSnapshot> snapshots)
    {
        var memory = NeuronalMemoryDecoder.Decode(snapshots);
        lock (_gate)
        {
            if (tick >= _tick)
            {
                _tick = tick;
                _memory = memory;
            }

            return _memory;
        }
    }

    public NeuronalMemorySnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return new NeuronalMemorySnapshot(_tick, NeuronalMemoryDecision.Authority, _memory);
        }
    }
}

internal static class NeuronalMemoryDecoder
{
    private const int EnsembleCount = 8;

    public static NeuronalMemoryDecision Decode(IReadOnlyList<InstanceStructureSnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        var diagnostics = snapshots
            .Select(static snapshot => snapshot.SynapticMemoryDiagnostics)
            .Where(static item => item is not null)
            .Cast<SynapticMemoryDiagnostics>()
            .ToArray();
        if (diagnostics.Length == 0)
        {
            return NeuronalMemoryDecision.Unavailable;
        }

        var activities = new SynapticMemoryEnsembleActivity[EnsembleCount];
        var recallScores = new double[EnsembleCount];
        for (var ensemble = 0; ensemble < EnsembleCount; ensemble++)
        {
            var values = diagnostics
                .SelectMany(static diagnostic => diagnostic.Ensembles)
                .Where(item => item.EnsembleIndex == ensemble)
                .ToArray();
            var cue = Max(values, static item => item.CueDrive);
            var strength = MeanPositive(values, static item => item.EngramStrength);
            var recall = MeanPositive(values, static item => item.RecallActivation);
            var eligibility = MeanPositive(values, static item => item.EligibilityTrace);
            var tag = MeanPositive(values, static item => item.SynapticTag);
            var interference = Max(values, static item => item.Interference);
            var extinction = Max(values, static item => item.Extinction);
            var consolidation = MeanPositive(values, static item => item.Consolidation);
            var supporting = values.Sum(static item => item.SupportingSynapses);
            activities[ensemble] = new SynapticMemoryEnsembleActivity(
                ensemble,
                cue,
                strength,
                recall,
                eligibility,
                tag,
                interference,
                extinction,
                consolidation,
                supporting);
            recallScores[ensemble] = Math.Max(0.0, recall - (interference * 0.25));
        }

        var ranked = Enumerable.Range(0, EnsembleCount)
            .OrderByDescending(index => recallScores[index])
            .ThenBy(static index => index)
            .ToArray();
        var recalled = ranked[0];
        var dominant = activities[recalled];
        var margin = Math.Max(0.0, recallScores[recalled] - recallScores[ranked[1]]);
        var hippocampal = MeanPositive(
            diagnostics.Where(static item => IsHippocampal(item.SourceStructure)).ToArray(),
            static item => item.HippocampalDependence);
        var cortical = MeanPositive(
            diagnostics.Where(static item => IsCortical(item.SourceStructure)).ToArray(),
            static item => item.CorticalConsolidation);
        var dependence = hippocampal <= 0f && cortical <= 0f
            ? 0f
            : hippocampal / Math.Max(0.0001f, hippocampal + cortical);
        var learnedSynapses = diagnostics.Sum(static item => item.LearnedSynapseCount);
        var hippocampalEncodingAvailable = diagnostics.Any(static item => IsHippocampal(item.SourceStructure));
        var active = learnedSynapses > 0 &&
            dominant.SupportingSynapses > 0 &&
            dominant.CueDrive > 0.005f &&
            dominant.EngramStrength > 0.005f &&
            recallScores[recalled] > 0.0001 &&
            margin > 0.00001;

        return new NeuronalMemoryDecision(
            true,
            active,
            active ? recalled : -1,
            recallScores[recalled],
            margin,
            dominant.CueDrive,
            dominant.EngramStrength,
            dominant.Interference,
            dominant.Extinction,
            dependence,
            cortical,
            learnedSynapses,
            hippocampalEncodingAvailable,
            activities);
    }

    private static float MeanPositive<T>(IReadOnlyList<T> values, Func<T, float> selector)
    {
        var positive = values.Select(selector).Where(static value => value > 0f).ToArray();
        return positive.Length == 0 ? 0f : (float)positive.Average();
    }

    private static float Max<T>(IReadOnlyList<T> values, Func<T, float> selector)
        => values.Count == 0 ? 0f : values.Max(selector);

    private static bool IsHippocampal(StructureId structure)
        => structure is StructureId.EntorhinalCortex
            or StructureId.DentateGyrus
            or StructureId.CA3
            or StructureId.CA2
            or StructureId.CA1
            or StructureId.Subiculum
            or StructureId.Presubiculum
            or StructureId.Parasubiculum;

    private static bool IsCortical(StructureId structure)
        => structure is StructureId.InferotemporalCortex
            or StructureId.PerirhinalCortex
            or StructureId.ParahippocampalCortex
            or StructureId.RetrosplenialCortex
            or StructureId.Ppc
            or StructureId.TemporalAssociation
            or StructureId.TemporalPole
            or StructureId.Insula
            or StructureId.Pfc
            or StructureId.PremotorCortex
            or StructureId.Sma;
}
