using Microsoft.Extensions.Configuration;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

internal sealed record NeuronalMotorControlSettings(
    double BaselineRateHz,
    double SaturationRateHz,
    double SmoothingAlpha,
    int PopulationSnapshotMaxAgeTicks,
    double MinimumCircuitCoverage,
    double MinimumOutputConfidence,
    int MaxPopulationEventsPerSide)
{
    public static NeuronalMotorControlSettings FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("NeuronalMotorControl");
        return Normalize(new NeuronalMotorControlSettings(
            BaselineRateHz: section.GetValue<double?>("BaselineRateHz") ?? 1.5,
            SaturationRateHz: section.GetValue<double?>("SaturationRateHz") ?? 25.0,
            SmoothingAlpha: section.GetValue<double?>("SmoothingAlpha") ?? 0.20,
            PopulationSnapshotMaxAgeTicks: section.GetValue<int?>("PopulationSnapshotMaxAgeTicks") ?? 96,
            MinimumCircuitCoverage: section.GetValue<double?>("MinimumCircuitCoverage") ?? 0.45,
            MinimumOutputConfidence: section.GetValue<double?>("MinimumOutputConfidence") ?? 0.45,
            MaxPopulationEventsPerSide: section.GetValue<int?>("MaxPopulationEventsPerSide") ?? 12));
    }

    public static NeuronalMotorControlSettings Normalize(NeuronalMotorControlSettings value)
    {
        var baseline = Math.Clamp(value.BaselineRateHz, 0.0, 100.0);
        var saturation = Math.Clamp(value.SaturationRateHz, baseline + 0.1, 500.0);
        return value with
        {
            BaselineRateHz = baseline,
            SaturationRateHz = saturation,
            SmoothingAlpha = Math.Clamp(value.SmoothingAlpha, 0.01, 1.0),
            PopulationSnapshotMaxAgeTicks = Math.Clamp(value.PopulationSnapshotMaxAgeTicks, 1, 4096),
            MinimumCircuitCoverage = Math.Clamp(value.MinimumCircuitCoverage, 0.05, 1.0),
            MinimumOutputConfidence = Math.Clamp(value.MinimumOutputConfidence, 0.05, 1.0),
            MaxPopulationEventsPerSide = Math.Clamp(value.MaxPopulationEventsPerSide, 1, 64)
        };
    }
}

internal sealed record NeuronalMotorControlSnapshot(
    NeuronalMotorControlSettings Settings);

internal sealed class NeuronalMotorPopulationWindow
{
    private static readonly HashSet<StructureId> MotorStructures =
    [
        StructureId.PremotorCortex,
        StructureId.Sma,
        StructureId.M1,
        StructureId.MotorThalamus,
        StructureId.ReticularFormation,
        StructureId.SpinalCordMotor
    ];

    private readonly object _gate = new();
    private readonly Dictionary<string, (long Tick, InstanceStructureSnapshot Snapshot)> _latest =
        new(StringComparer.OrdinalIgnoreCase);
    private long _lastTick = -1;

    public IReadOnlyList<InstanceStructureSnapshot> UpdateAndGet(
        long tick,
        IReadOnlyList<InstanceStructureSnapshot> current,
        int maxAgeTicks)
    {
        ArgumentNullException.ThrowIfNull(current);
        var boundedAge = Math.Clamp(maxAgeTicks, 1, 4096);
        lock (_gate)
        {
            if (_lastTick >= 0 && tick < _lastTick)
            {
                _latest.Clear();
            }

            _lastTick = tick;
            for (var i = 0; i < current.Count; i++)
            {
                var snapshot = current[i];
                if (!IsRelevant(snapshot))
                {
                    continue;
                }

                _latest[snapshot.Instance.InstanceKey] = (tick, snapshot);
            }

            var cutoff = tick - boundedAge;
            foreach (var key in _latest
                         .Where(pair => pair.Value.Tick < cutoff)
                         .Select(static pair => pair.Key)
                         .ToArray())
            {
                _latest.Remove(key);
            }

            return _latest.Values
                .OrderBy(static item => item.Snapshot.StructureId)
                .ThenBy(static item => item.Snapshot.Instance.HemisphereNormalized, StringComparer.Ordinal)
                .ThenBy(static item => item.Snapshot.Instance.InstanceKey, StringComparer.OrdinalIgnoreCase)
                .Select(static item => item.Snapshot)
                .ToArray();
        }
    }

    private static bool IsRelevant(InstanceStructureSnapshot snapshot)
        => MotorStructures.Contains(snapshot.StructureId) ||
           snapshot.ActionSelectionDiagnostics is not null ||
           snapshot.BasalGangliaDiagnostics is not null ||
           snapshot.CerebellarDiagnostics is not null ||
           snapshot.VestibuloReticularDiagnostics is not null;
}

internal sealed class NeuronalMotorControlState
{
    private readonly NeuronalMotorControlSettings _settings;

    public NeuronalMotorControlState(NeuronalMotorControlSettings settings)
    {
        _settings = NeuronalMotorControlSettings.Normalize(settings);
    }

    public static NeuronalMotorControlState FromConfiguration(IConfiguration configuration)
        => new(NeuronalMotorControlSettings.FromConfiguration(configuration));

    public NeuronalMotorControlSnapshot GetSnapshot()
        => new(_settings);
}

internal sealed record NeuronalMotorRuntime(
    bool Active,
    bool Sleeping,
    long Tick,
    long Sequence,
    double LeftDrive,
    double RightDrive,
    double ForwardDrive,
    double TurnDrive,
    double MotorCircuitCoverage,
    double SelectionGate,
    double OutputInhibition,
    double Confidence,
    double ConfidenceEma,
    double MinimumOutputConfidence,
    int MaxPopulationEventsPerSide,
    string Evidence,
    int SelectedActionChannel = -1,
    double ActionSelectionConfidence = 0.0,
    double ActionCircuitCoverage = 0.0,
    double ActionSelectionMargin = 0.0,
    bool ActionCircuitObserved = false)
{
    public static NeuronalMotorRuntime Default { get; } = new(
        Active: false,
        Sleeping: false,
        Tick: 0,
        Sequence: 0,
        LeftDrive: 0.0,
        RightDrive: 0.0,
        ForwardDrive: 0.0,
        TurnDrive: 0.0,
        MotorCircuitCoverage: 0.0,
        SelectionGate: 0.0,
        OutputInhibition: 1.0,
        Confidence: 0.0,
        ConfidenceEma: 0.0,
        MinimumOutputConfidence: 0.45,
        MaxPopulationEventsPerSide: 12,
        Evidence: "waiting for bilateral neuronal motor populations",
        SelectedActionChannel: -1,
        ActionSelectionConfidence: 0.0,
        ActionCircuitCoverage: 0.0,
        ActionSelectionMargin: 0.0);
}

internal static class NeuronalMotorPopulationDecoder
{
    private static readonly IReadOnlyDictionary<StructureId, double> MotorWeights =
        new Dictionary<StructureId, double>
        {
            [StructureId.PremotorCortex] = 0.15,
            [StructureId.Sma] = 0.12,
            [StructureId.M1] = 0.30,
            [StructureId.MotorThalamus] = 0.08,
            [StructureId.ReticularFormation] = 0.10,
            [StructureId.SpinalCordMotor] = 0.25
        };

    private const double MetricsAlpha = 0.02;

    public static NeuronalMotorRuntime Decode(
        long tick,
        IReadOnlyList<InstanceStructureSnapshot> snapshots,
        bool sleeping,
        NeuronalMotorControlSnapshot control,
        NeuronalMotorRuntime previous)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(previous);

        var settings = control.Settings;
        var expectedWeightPerSide = MotorWeights.Values.Sum();
        var ratesByPopulation = new Dictionary<(StructureId Structure, string Hemisphere), (double Sum, int Count)>();
        var observedStructures = new HashSet<StructureId>();

        for (var i = 0; i < snapshots.Count; i++)
        {
            var snapshot = snapshots[i];
            if (!MotorWeights.TryGetValue(snapshot.StructureId, out var weight))
            {
                continue;
            }

            observedStructures.Add(snapshot.StructureId);
            var rate = NormalizeRate(snapshot.MeanFiringRateHz, settings);
            var hemisphere = snapshot.Instance.HemisphereNormalized;
            var key = (snapshot.StructureId, hemisphere is "L" or "R" ? hemisphere : "M");
            ratesByPopulation.TryGetValue(key, out var accumulator);
            ratesByPopulation[key] = (accumulator.Sum + rate, accumulator.Count + 1);
        }

        var leftWeightedRate = 0.0;
        var rightWeightedRate = 0.0;
        var leftObservedWeight = 0.0;
        var rightObservedWeight = 0.0;
        foreach (var pair in MotorWeights)
        {
            if (ratesByPopulation.TryGetValue((pair.Key, "L"), out var left))
            {
                leftWeightedRate += (left.Sum / left.Count) * pair.Value;
                leftObservedWeight += pair.Value;
            }

            if (ratesByPopulation.TryGetValue((pair.Key, "R"), out var right))
            {
                rightWeightedRate += (right.Sum / right.Count) * pair.Value;
                rightObservedWeight += pair.Value;
            }

            if (ratesByPopulation.TryGetValue((pair.Key, "M"), out var midline))
            {
                var midlineRate = midline.Sum / midline.Count;
                leftWeightedRate += midlineRate * pair.Value * 0.5;
                rightWeightedRate += midlineRate * pair.Value * 0.5;
                leftObservedWeight += pair.Value * 0.5;
                rightObservedWeight += pair.Value * 0.5;
            }
        }

        var leftPopulation = leftObservedWeight > 0.0 ? leftWeightedRate / leftObservedWeight : 0.0;
        var rightPopulation = rightObservedWeight > 0.0 ? rightWeightedRate / rightObservedWeight : 0.0;
        // Authority requires a bilateral descending path. An arithmetic total can
        // hide a complete hemisphere loss, so coverage is set by the weaker side.
        var leftCoverage = Math.Clamp(leftObservedWeight / Math.Max(0.001, expectedWeightPerSide), 0.0, 1.0);
        var rightCoverage = Math.Clamp(rightObservedWeight / Math.Max(0.001, expectedWeightPerSide), 0.0, 1.0);
        var motorCoverage = Math.Min(leftCoverage, rightCoverage);

        var basalGanglia = snapshots
            .Select(static snapshot => snapshot.BasalGangliaDiagnostics)
            .Where(static diagnostics => diagnostics is not null)
            .Cast<BasalGangliaDiagnostics>()
            .ToArray();
        var selectionGate = basalGanglia.Length == 0
            ? 0.50
            : Math.Clamp(basalGanglia.Average(static item =>
                (item.ThalamicDisinhibition * 0.55) +
                (item.DirectPathwayActivation * 0.25) +
                (Math.Max(0.0f, item.ActionSelectionBias) * 0.20)), 0.0, 1.0);
        var outputInhibition = basalGanglia.Length == 0
            ? 0.50
            : Math.Clamp(basalGanglia.Average(static item => item.OutputNucleusInhibition), 0.0, 1.0);
        var basalGangliaCoverage = basalGanglia.Length > 0 ? 1.0 : 0.0;
        var effectiveGate = Math.Clamp((selectionGate * 0.75) + ((1.0 - outputInhibition) * 0.25), 0.0, 1.0);

        var cerebellar = snapshots
            .Select(static snapshot => snapshot.CerebellarDiagnostics)
            .Where(static diagnostics => diagnostics is not null)
            .Cast<CerebellarDiagnostics>()
            .ToArray();
        var cerebellarSupport = cerebellar.Length == 0
            ? 0.50
            : Math.Clamp(cerebellar.Average(static item =>
                (item.DeepNucleusOutput * 0.55) + (item.CorrectionGain * 0.45)), 0.0, 1.0);

        var postural = snapshots
            .Select(static snapshot => snapshot.VestibuloReticularDiagnostics)
            .Where(static diagnostics => diagnostics is not null)
            .Cast<VestibuloReticularDiagnostics>()
            .ToArray();
        var posturalSupport = postural.Length == 0
            ? 0.50
            : Math.Clamp(postural.Average(static item =>
                (item.PostureStability * 0.50) +
                (item.SpinalMotorTone * 0.30) +
                ((1.0f - item.BalanceError) * 0.20)), 0.0, 1.0);

        var supportGain = 0.75 + (cerebellarSupport * 0.15) + (posturalSupport * 0.10);
        var actionDecision = NeuronalActionSelectionDecoder.Decode(snapshots);
        var unshapedLeft = sleeping ? 0.0 : Math.Clamp(leftPopulation * effectiveGate * supportGain, 0.0, 1.0);
        var unshapedRight = sleeping ? 0.0 : Math.Clamp(rightPopulation * effectiveGate * supportGain, 0.0, 1.0);
        var shaped = NeuronalActionSelectionDecoder.ShapeMotorPopulation(
            actionDecision,
            unshapedLeft,
            unshapedRight);
        var rawLeft = sleeping ? 0.0 : shaped.Left;
        var rawRight = sleeping ? 0.0 : shaped.Right;
        var alpha = settings.SmoothingAlpha;
        var leftDrive = sleeping ? 0.0 : Lerp(previous.LeftDrive, rawLeft, alpha);
        var rightDrive = sleeping ? 0.0 : Lerp(previous.RightDrive, rawRight, alpha);
        var signalStrength = Math.Max(Math.Abs(leftDrive), Math.Abs(rightDrive));

        var supportCoverage = ((cerebellar.Length > 0 ? 1.0 : 0.0) + (postural.Length > 0 ? 1.0 : 0.0)) * 0.5;
        var motorConfidence = Math.Clamp(
            (motorCoverage * 0.48) +
            (signalStrength * 0.24) +
            (basalGangliaCoverage * 0.18) +
            (supportCoverage * 0.10),
            0.0,
            1.0);
        var confidence = actionDecision.Available
            ? Math.Clamp((motorConfidence * 0.72) + (actionDecision.Confidence * 0.28), 0.0, 1.0)
            : motorConfidence;
        var confidenceEma = previous.Tick <= 0
            ? confidence
            : Lerp(previous.ConfidenceEma, confidence, MetricsAlpha);
        var actionAuthorityReady = !actionDecision.Available ||
            (actionDecision.Active && actionDecision.Confidence >= settings.MinimumOutputConfidence);
        var active = !sleeping &&
            actionAuthorityReady &&
            motorCoverage >= settings.MinimumCircuitCoverage &&
            confidence >= settings.MinimumOutputConfidence &&
            signalStrength >= 0.01;

        var forwardDrive = Math.Clamp((leftDrive + rightDrive) * 0.5, 0.0, 1.0);
        var turnDrive = Math.Clamp(rightDrive - leftDrive, -1.0, 1.0);
        return new NeuronalMotorRuntime(
            Active: active,
            Sleeping: sleeping,
            Tick: tick,
            Sequence: previous.Sequence + 1,
            LeftDrive: leftDrive,
            RightDrive: rightDrive,
            ForwardDrive: forwardDrive,
            TurnDrive: turnDrive,
            MotorCircuitCoverage: motorCoverage,
            SelectionGate: selectionGate,
            OutputInhibition: outputInhibition,
            Confidence: confidence,
            ConfidenceEma: confidenceEma,
            MinimumOutputConfidence: settings.MinimumOutputConfidence,
            MaxPopulationEventsPerSide: settings.MaxPopulationEventsPerSide,
            Evidence: $"motor-populations={observedStructures.Count}/{MotorWeights.Count}; bilateral-coverage={motorCoverage:0.000}; basal-ganglia={(basalGanglia.Length > 0 ? "observed" : "missing")}; action-channels={(actionDecision.Available ? (actionDecision.Active ? "selected" : "suppressed") : "missing")}; cerebellar={(cerebellar.Length > 0 ? "observed" : "missing")}; posture={(postural.Length > 0 ? "observed" : "missing")}",
            SelectedActionChannel: actionDecision.SelectedChannel,
            ActionSelectionConfidence: actionDecision.Confidence,
            ActionCircuitCoverage: actionDecision.CircuitCoverage,
            ActionSelectionMargin: actionDecision.SelectionMargin,
            ActionCircuitObserved: actionDecision.Available);
    }

    private static double NormalizeRate(float rateHz, NeuronalMotorControlSettings settings)
        => Math.Clamp(
            (rateHz - settings.BaselineRateHz) /
            Math.Max(0.1, settings.SaturationRateHz - settings.BaselineRateHz),
            0.0,
            1.0);

    private static double Lerp(double current, double target, double alpha)
        => current + ((target - current) * alpha);
}
