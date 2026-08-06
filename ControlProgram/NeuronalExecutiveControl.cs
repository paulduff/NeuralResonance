using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

internal sealed record NeuronalExecutiveDecision(
    bool Available,
    bool Active,
    bool Committed,
    int SelectedActionChannel,
    int MaintainedContextChannel,
    long SustainedSelectionTicks,
    double PersistentActivity,
    double MediodorsalSupport,
    double FrontoparietalContext,
    double SemanticContext,
    double StriatalGate,
    double ConflictDemand,
    double TopDownBias,
    double TaskSetStability,
    double ActionSelectionMargin,
    double CircuitCoverage,
    double Confidence)
{
    public const string Authority = "RecurrentPrefrontalThalamicStriatalCircuit";

    public static NeuronalExecutiveDecision Unavailable { get; } = new(
        false,
        false,
        false,
        -1,
        -1,
        0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0);
}

internal sealed record NeuronalExecutiveSnapshot(
    long Tick,
    string Authority,
    bool ReadOnlyMonitor,
    bool CanInjectGoals,
    bool CanOverrideActionSelection,
    bool LegacyPlanningEnabled,
    NeuronalExecutiveDecision Executive);

internal sealed class NeuronalExecutiveRuntime
{
    private readonly object _gate = new();
    private long _tick = -1;
    private NeuronalExecutiveDecision _decision = NeuronalExecutiveDecision.Unavailable;

    public NeuronalExecutiveDecision Update(
        long tick,
        IReadOnlyList<InstanceStructureSnapshot> snapshots,
        NeuronalAttentionWorkspaceDecision attention,
        NeuronalMotorRuntime motor)
    {
        var decoded = NeuronalExecutiveDecoder.Decode(snapshots, attention, motor);
        lock (_gate)
        {
            if (tick < _tick)
            {
                return _decision;
            }

            var sustainedTicks = decoded.Active
                ? _decision.Active &&
                  _decision.SelectedActionChannel == decoded.SelectedActionChannel
                    ? _decision.SustainedSelectionTicks + 1
                    : 1
                : 0;
            _tick = tick;
            _decision = decoded with { SustainedSelectionTicks = sustainedTicks };
            return _decision;
        }
    }

    public NeuronalExecutiveSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return new NeuronalExecutiveSnapshot(
                _tick,
                NeuronalExecutiveDecision.Authority,
                ReadOnlyMonitor: true,
                CanInjectGoals: false,
                CanOverrideActionSelection: false,
                LegacyPlanningEnabled: false,
                _decision);
        }
    }
}

internal static class NeuronalExecutiveDecoder
{
    private const double FiringSaturationHz = 25.0;

    private static readonly StructureId[] RequiredStructures =
    [
        StructureId.Pfc,
        StructureId.MediodorsalThalamus,
        StructureId.Ppc,
        StructureId.Striatum,
        StructureId.Acc
    ];

    // This is an observer, not a planner. It decodes recurrent executive state
    // and mirrors the action winner already selected by neuronal basal-ganglia
    // circuitry. No value produced here is fed back into the brain or motor path.
    public static NeuronalExecutiveDecision Decode(
        IReadOnlyList<InstanceStructureSnapshot> snapshots,
        NeuronalAttentionWorkspaceDecision attention,
        NeuronalMotorRuntime motor)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        ArgumentNullException.ThrowIfNull(attention);
        ArgumentNullException.ThrowIfNull(motor);

        var executiveSnapshots = snapshots
            .Where(static snapshot => snapshot.PrefrontalWorkingMemoryDiagnostics is not null)
            .ToArray();
        if (executiveSnapshots.Length == 0)
        {
            return NeuronalExecutiveDecision.Unavailable;
        }

        var observed = executiveSnapshots
            .Select(static snapshot => snapshot.StructureId)
            .ToHashSet();
        var coverage = RequiredStructures.Count(observed.Contains) / (double)RequiredStructures.Length;
        var pfc = Maximum(executiveSnapshots, StructureId.Pfc,
            static value => value.PfcPersistentActivity);
        var mediodorsal = Maximum(executiveSnapshots, StructureId.MediodorsalThalamus,
            static value => value.MediodorsalThalamicSupport);
        var frontoparietal = Maximum(executiveSnapshots, StructureId.Ppc,
            static value => value.FrontoparietalContext);
        var semantic = Maximum(executiveSnapshots, StructureId.TemporalAssociation,
            static value => value.SemanticContext);
        var striatalGate = Maximum(executiveSnapshots, StructureId.Striatum,
            static value => value.StriatalGate);
        var conflict = Maximum(executiveSnapshots, StructureId.Acc,
            static value => value.AccControlDemand);
        var topDown = Math.Clamp(
            (pfc * 0.42) +
            (mediodorsal * 0.24) +
            (frontoparietal * 0.20) +
            (semantic * 0.14) -
            (conflict * 0.12),
            0.0,
            1.0);
        var stability = Math.Clamp(
            (pfc * 0.34) +
            (mediodorsal * 0.24) +
            (frontoparietal * 0.14) +
            (striatalGate * 0.20) -
            (conflict * 0.10),
            0.0,
            1.0);

        var circuitComplete = coverage >= 1.0 && motor.ActionCircuitObserved;
        var active = circuitComplete &&
            motor.SelectedActionChannel >= 0 &&
            pfc > 0.002 &&
            mediodorsal > 0.002 &&
            striatalGate > 0.002;
        var confidence = Math.Clamp(
            (coverage * 0.34) +
            (stability * 0.24) +
            (motor.ActionSelectionConfidence * 0.24) +
            (Math.Clamp(motor.ActionSelectionMargin * 4.0, 0.0, 1.0) * 0.10) +
            ((1.0 - conflict) * 0.08),
            0.0,
            1.0);
        var committed = active &&
            stability >= 0.08 &&
            motor.ActionSelectionMargin > 0.0025 &&
            confidence >= 0.45;

        return new NeuronalExecutiveDecision(
            Available: true,
            Active: active,
            Committed: committed,
            SelectedActionChannel: active ? motor.SelectedActionChannel : -1,
            MaintainedContextChannel: attention.Active ? attention.SelectedChannel : -1,
            SustainedSelectionTicks: 0,
            PersistentActivity: pfc,
            MediodorsalSupport: mediodorsal,
            FrontoparietalContext: frontoparietal,
            SemanticContext: semantic,
            StriatalGate: striatalGate,
            ConflictDemand: conflict,
            TopDownBias: topDown,
            TaskSetStability: stability,
            ActionSelectionMargin: motor.ActionSelectionMargin,
            CircuitCoverage: coverage,
            Confidence: confidence);
    }

    private static double Maximum(
        IReadOnlyList<InstanceStructureSnapshot> snapshots,
        StructureId structure,
        Func<PrefrontalWorkingMemoryDiagnostics, float> selector)
    {
        var maximum = 0.0;
        for (var index = 0; index < snapshots.Count; index++)
        {
            var snapshot = snapshots[index];
            if (snapshot.StructureId != structure ||
                snapshot.PrefrontalWorkingMemoryDiagnostics is not { } diagnostics)
            {
                continue;
            }

            var value = selector(diagnostics);
            if (float.IsFinite(value))
            {
                maximum = Math.Max(maximum, value / FiringSaturationHz);
            }
        }

        return Math.Clamp(maximum, 0.0, 1.0);
    }
}
