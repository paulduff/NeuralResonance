using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

internal enum NeuronalSleepState
{
    Wake = 0,
    Nrem = 1,
    Rem = 2
}

internal sealed record NeuronalSleepConsolidationDecision(
    bool CircuitObserved,
    bool Available,
    bool StateActive,
    NeuronalSleepState State,
    double StateConfidence,
    IReadOnlyList<double> StateScores,
    double CircuitCoverage,
    bool ReplayActive,
    int ReplayEnsemble,
    double ReplayStrength,
    double ReplayMargin,
    double ReplayCircuitCoverage,
    double SpindleCoupling,
    double SlowWaveCoupling,
    double CorticalConsolidationGain,
    IReadOnlyList<SleepReplayEnsembleActivity> ReplayEnsembles)
{
    public const string Authority = "DistributedSleepReplayCircuits";

    public static NeuronalSleepConsolidationDecision Unavailable { get; } = new(
        false,
        false,
        false,
        NeuronalSleepState.Wake,
        0.0,
        [],
        0.0,
        false,
        -1,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        []);
}

internal sealed record NeuronalSleepConsolidationSnapshot(
    long Tick,
    string Authority,
    NeuronalSleepConsolidationDecision SleepConsolidation);

internal sealed class NeuronalSleepConsolidationRuntime
{
    private readonly object _gate = new();
    private long _tick = -1;
    private NeuronalSleepConsolidationDecision _decision = NeuronalSleepConsolidationDecision.Unavailable;

    public NeuronalSleepConsolidationDecision Update(long tick, IReadOnlyList<InstanceStructureSnapshot> snapshots)
    {
        var decoded = NeuronalSleepConsolidationDecoder.Decode(snapshots);
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

    public NeuronalSleepConsolidationSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return new NeuronalSleepConsolidationSnapshot(
                _tick,
                NeuronalSleepConsolidationDecision.Authority,
                _decision);
        }
    }
}

internal static class NeuronalSleepConsolidationDecoder
{
    private const int StateChannelCount = 3;
    private const int ReplayEnsembleCount = 8;

    public static NeuronalSleepConsolidationDecision Decode(IReadOnlyList<InstanceStructureSnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        var diagnostics = snapshots
            .Select(static snapshot => snapshot.NeuronalSleepConsolidationDiagnostics)
            .Where(static item => item is not null)
            .Cast<NeuronalSleepConsolidationDiagnostics>()
            .ToArray();
        if (diagnostics.Length == 0)
        {
            return NeuronalSleepConsolidationDecision.Unavailable;
        }

        var present = diagnostics.Select(static item => item.SourceStructure).ToHashSet();
        var stateRoles = new[]
        {
            present.Contains(StructureId.DorsomedialHypothalamicNucleus),
            present.Contains(StructureId.ReticularFormation),
            present.Contains(StructureId.PontineNuclei),
            present.Contains(StructureId.LocusCoeruleus),
            present.Contains(StructureId.NucleusBasalis),
            present.Contains(StructureId.IntralaminarThalamus)
        };
        var coverage = stateRoles.Count(static value => value) / (double)stateRoles.Length;
        var stateValues = diagnostics.SelectMany(static item => item.StateChannels).ToArray();
        var homeostatic = Max(stateValues, static item => item.HomeostaticDrive);
        var wake = MeanPositive(stateValues, static item => item.WakeDrive);
        var nrem = MeanPositive(stateValues, static item => item.NremDrive);
        var rem = MeanPositive(stateValues, static item => item.RemDrive);
        var trnSpindle = MeanPositive(
            diagnostics.Where(static item => item.SourceStructure == StructureId.Trn)
                .SelectMany(static item => item.StateChannels)
                .ToArray(),
            static item => item.SpindleSynchrony);
        var thalamicSpindle = MeanPositive(
            diagnostics.Where(static item => item.SourceStructure == StructureId.IntralaminarThalamus)
                .SelectMany(static item => item.StateChannels)
                .ToArray(),
            static item => item.SpindleSynchrony);
        var spindle = Math.Min(trnSpindle, thalamicSpindle);
        var slowWave = MeanPositive(stateValues, static item => item.SlowWaveSynchrony);
        var replayGate = MeanPositive(stateValues, static item => item.ReplayGate);
        var stateScores = new double[StateChannelCount];
        stateScores[(int)NeuronalSleepState.Wake] = Math.Clamp(
            (wake * 0.72) + ((1.0 - homeostatic) * 0.28),
            0.0,
            1.0);
        stateScores[(int)NeuronalSleepState.Nrem] = Math.Clamp(
            (homeostatic * 0.34) +
            (nrem * 0.22) +
            (spindle * 0.18) +
            (slowWave * 0.14) +
            (replayGate * 0.12) -
            (wake * 0.38),
            0.0,
            1.0);
        stateScores[(int)NeuronalSleepState.Rem] = Math.Clamp(
            (rem * 0.52) +
            (homeostatic * 0.20) +
            (replayGate * 0.12) -
            (wake * 0.36) -
            (spindle * 0.10),
            0.0,
            1.0);

        var rankedStates = Enumerable.Range(0, StateChannelCount)
            .OrderByDescending(index => stateScores[index])
            .ThenBy(static index => index)
            .ToArray();
        var stateChannel = rankedStates[0];
        var stateMargin = Math.Max(0.0, stateScores[stateChannel] - stateScores[rankedStates[1]]);
        var available = coverage >= (4.0 / 6.0);
        var stateActive = available && stateScores[stateChannel] > 0.01 && stateMargin > 0.00001;
        var state = stateActive ? (NeuronalSleepState)stateChannel : NeuronalSleepState.Wake;

        var replayActivities = new SleepReplayEnsembleActivity[ReplayEnsembleCount];
        var replayScores = new double[ReplayEnsembleCount];
        for (var ensemble = 0; ensemble < ReplayEnsembleCount; ensemble++)
        {
            var values = diagnostics
                .SelectMany(static item => item.ReplayEnsembles)
                .Where(item => item.EnsembleIndex == ensemble)
                .ToArray();
            var hippocampalBurst = Max(values, static item => item.HippocampalBurst);
            var trnCoupling = MeanPositive(
                diagnostics.Where(static item => item.SourceStructure == StructureId.Trn)
                    .SelectMany(static item => item.ReplayEnsembles)
                    .Where(item => item.EnsembleIndex == ensemble)
                    .ToArray(),
                static item => item.SpindleCoupling);
            var thalamicCoupling = MeanPositive(
                diagnostics.Where(static item => item.SourceStructure == StructureId.IntralaminarThalamus)
                    .SelectMany(static item => item.ReplayEnsembles)
                    .Where(item => item.EnsembleIndex == ensemble)
                    .ToArray(),
                static item => item.SpindleCoupling);
            var spindleCoupling = Math.Min(trnCoupling, thalamicCoupling);
            var slowWaveCoupling = MeanPositive(values, static item => item.SlowWaveCoupling);
            var corticalEcho = MeanPositive(values, static item => item.CorticalEcho);
            var engramStrength = MeanPositive(values, static item => item.EngramStrength);
            var interference = Max(values, static item => item.Interference);
            var consolidation = MeanPositive(values, static item => item.ConsolidationGain);
            replayActivities[ensemble] = new SleepReplayEnsembleActivity(
                ensemble,
                hippocampalBurst,
                spindleCoupling,
                slowWaveCoupling,
                corticalEcho,
                engramStrength,
                interference,
                consolidation);
            replayScores[ensemble] = Math.Clamp(
                (hippocampalBurst * 0.30) +
                (spindleCoupling * 0.22) +
                (slowWaveCoupling * 0.15) +
                (corticalEcho * 0.14) +
                (engramStrength * 0.13) +
                (consolidation * 0.08) -
                (interference * 0.22),
                0.0,
                1.0);
        }

        var replayRoles = new[]
        {
            present.Contains(StructureId.CA3),
            present.Contains(StructureId.CA1),
            present.Contains(StructureId.Trn),
            present.Contains(StructureId.IntralaminarThalamus),
            present.Any(IsCorticalConsolidationStructure)
        };
        var replayCoverage = replayRoles.Count(static value => value) / (double)replayRoles.Length;
        var rankedReplay = Enumerable.Range(0, ReplayEnsembleCount)
            .OrderByDescending(index => replayScores[index])
            .ThenBy(static index => index)
            .ToArray();
        var replayEnsemble = rankedReplay[0];
        var replayMargin = Math.Max(0.0, replayScores[replayEnsemble] - replayScores[rankedReplay[1]]);
        var replayActive = stateActive &&
            state == NeuronalSleepState.Nrem &&
            present.Contains(StructureId.CA3) &&
            present.Contains(StructureId.CA1) &&
            present.Contains(StructureId.Trn) &&
            present.Contains(StructureId.IntralaminarThalamus) &&
            replayCoverage >= 0.60 &&
            replayScores[replayEnsemble] > 0.002 &&
            replayMargin > 0.00001;
        var dominantReplay = replayActivities[replayEnsemble];

        return new NeuronalSleepConsolidationDecision(
            true,
            available,
            stateActive,
            state,
            stateMargin,
            stateScores,
            coverage,
            replayActive,
            replayActive ? replayEnsemble : -1,
            replayScores[replayEnsemble],
            replayMargin,
            replayCoverage,
            dominantReplay.SpindleCoupling,
            dominantReplay.SlowWaveCoupling,
            dominantReplay.ConsolidationGain,
            replayActivities);
    }

    private static float MeanPositive<T>(IReadOnlyList<T> values, Func<T, float> selector)
    {
        var positive = values.Select(selector).Where(static value => value > 0f).ToArray();
        return positive.Length == 0 ? 0f : (float)positive.Average();
    }

    private static float Max<T>(IReadOnlyList<T> values, Func<T, float> selector)
        => values.Count == 0 ? 0f : values.Max(selector);

    private static bool IsCorticalConsolidationStructure(StructureId structure)
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
