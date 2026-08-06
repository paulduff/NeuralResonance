using Microsoft.Extensions.Configuration;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

internal static class NeuronalMotorModes
{
    public const string Shadow = "Shadow";
    public const string Assist = "Assist";
    public const string Primary = "Primary";

    public static bool TryNormalize(string? value, out string mode)
    {
        if (string.Equals(value, Shadow, StringComparison.OrdinalIgnoreCase))
        {
            mode = Shadow;
            return true;
        }

        if (string.Equals(value, Assist, StringComparison.OrdinalIgnoreCase))
        {
            mode = Assist;
            return true;
        }

        if (string.Equals(value, Primary, StringComparison.OrdinalIgnoreCase))
        {
            mode = Primary;
            return true;
        }

        mode = Shadow;
        return false;
    }
}

internal sealed record NeuronalMotorControlSettings(
    string Mode,
    double BaselineRateHz,
    double SaturationRateHz,
    double SmoothingAlpha,
    int PopulationSnapshotMaxAgeTicks,
    double MinimumCircuitCoverage,
    double MinimumOutputConfidence,
    int MaxPopulationEventsPerSide,
    int PromotionMinimumSamples,
    double PromotionMinimumAgreement,
    double PromotionMinimumConfidence,
    double PromotionMinimumCoverage,
    int PromotionConsecutiveTicks)
{
    public static NeuronalMotorControlSettings FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("NeuronalMotorControl");
        NeuronalMotorModes.TryNormalize(section["Mode"], out var mode);
        if (mode == NeuronalMotorModes.Primary)
        {
            // Primary authority is earned by the live evidence gate, never granted
            // merely by an appsettings value at process startup.
            mode = NeuronalMotorModes.Shadow;
        }

        return Normalize(new NeuronalMotorControlSettings(
            Mode: mode,
            BaselineRateHz: section.GetValue<double?>("BaselineRateHz") ?? 1.5,
            SaturationRateHz: section.GetValue<double?>("SaturationRateHz") ?? 25.0,
            SmoothingAlpha: section.GetValue<double?>("SmoothingAlpha") ?? 0.20,
            PopulationSnapshotMaxAgeTicks: section.GetValue<int?>("PopulationSnapshotMaxAgeTicks") ?? 96,
            MinimumCircuitCoverage: section.GetValue<double?>("MinimumCircuitCoverage") ?? 0.45,
            MinimumOutputConfidence: section.GetValue<double?>("MinimumOutputConfidence") ?? 0.45,
            MaxPopulationEventsPerSide: section.GetValue<int?>("MaxPopulationEventsPerSide") ?? 12,
            PromotionMinimumSamples: section.GetValue<int?>("PromotionMinimumSamples") ?? 1200,
            PromotionMinimumAgreement: section.GetValue<double?>("PromotionMinimumAgreement") ?? 0.72,
            PromotionMinimumConfidence: section.GetValue<double?>("PromotionMinimumConfidence") ?? 0.62,
            PromotionMinimumCoverage: section.GetValue<double?>("PromotionMinimumCoverage") ?? 0.80,
            PromotionConsecutiveTicks: section.GetValue<int?>("PromotionConsecutiveTicks") ?? 600));
    }

    public static NeuronalMotorControlSettings Normalize(NeuronalMotorControlSettings value)
    {
        NeuronalMotorModes.TryNormalize(value.Mode, out var mode);
        var baseline = Math.Clamp(value.BaselineRateHz, 0.0, 100.0);
        var saturation = Math.Clamp(value.SaturationRateHz, baseline + 0.1, 500.0);
        return value with
        {
            Mode = mode,
            BaselineRateHz = baseline,
            SaturationRateHz = saturation,
            SmoothingAlpha = Math.Clamp(value.SmoothingAlpha, 0.01, 1.0),
            PopulationSnapshotMaxAgeTicks = Math.Clamp(value.PopulationSnapshotMaxAgeTicks, 1, 4096),
            MinimumCircuitCoverage = Math.Clamp(value.MinimumCircuitCoverage, 0.05, 1.0),
            MinimumOutputConfidence = Math.Clamp(value.MinimumOutputConfidence, 0.05, 1.0),
            MaxPopulationEventsPerSide = Math.Clamp(value.MaxPopulationEventsPerSide, 1, 64),
            PromotionMinimumSamples = Math.Clamp(value.PromotionMinimumSamples, 50, 10_000_000),
            PromotionMinimumAgreement = Math.Clamp(value.PromotionMinimumAgreement, 0.0, 1.0),
            PromotionMinimumConfidence = Math.Clamp(value.PromotionMinimumConfidence, 0.0, 1.0),
            PromotionMinimumCoverage = Math.Clamp(value.PromotionMinimumCoverage, 0.0, 1.0),
            PromotionConsecutiveTicks = Math.Clamp(value.PromotionConsecutiveTicks, 10, 10_000_000)
        };
    }
}

internal sealed record NeuronalMotorControlSnapshot(
    long Generation,
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
    private readonly object _gate = new();
    private NeuronalMotorControlSettings _settings;
    private long _generation;

    public NeuronalMotorControlState(NeuronalMotorControlSettings settings)
    {
        _settings = NeuronalMotorControlSettings.Normalize(settings);
    }

    public static NeuronalMotorControlState FromConfiguration(IConfiguration configuration)
        => new(NeuronalMotorControlSettings.FromConfiguration(configuration));

    public NeuronalMotorControlSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return new NeuronalMotorControlSnapshot(_generation, _settings);
        }
    }

    public bool TryApplyMode(
        string? requestedMode,
        NeuronalMotorRuntime runtime,
        out NeuronalMotorControlSnapshot snapshot,
        out string error)
    {
        if (!NeuronalMotorModes.TryNormalize(requestedMode, out var mode))
        {
            snapshot = GetSnapshot();
            error = "Mode must be Shadow, Assist, or Primary.";
            return false;
        }

        lock (_gate)
        {
            if (mode == NeuronalMotorModes.Primary && !runtime.PromotionReady)
            {
                snapshot = new NeuronalMotorControlSnapshot(_generation, _settings);
                error = "Primary mode is locked until the neuronal motor evidence gate passes.";
                return false;
            }

            if (!string.Equals(_settings.Mode, mode, StringComparison.Ordinal))
            {
                _settings = _settings with { Mode = mode };
                _generation++;
            }

            snapshot = new NeuronalMotorControlSnapshot(_generation, _settings);
            error = string.Empty;
            return true;
        }
    }
}

internal sealed record NeuronalMotorRuntime(
    string Mode,
    long ControlGeneration,
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
    bool SymbolicReferenceAvailable,
    double SymbolicReferenceLeft,
    double SymbolicReferenceRight,
    double Agreement,
    double AgreementEma,
    long EvaluationSamples,
    long ActiveEvaluationSamples,
    int QualifiedConsecutiveTicks,
    bool PromotionReady,
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
        Mode: NeuronalMotorModes.Shadow,
        ControlGeneration: 0,
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
        SymbolicReferenceAvailable: false,
        SymbolicReferenceLeft: 0.0,
        SymbolicReferenceRight: 0.0,
        Agreement: 0.0,
        AgreementEma: 0.0,
        EvaluationSamples: 0,
        ActiveEvaluationSamples: 0,
        QualifiedConsecutiveTicks: 0,
        PromotionReady: false,
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
        IntentionalActionLoopRuntime symbolicReference,
        bool sleeping,
        NeuronalMotorControlSnapshot control,
        NeuronalMotorRuntime previous)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        ArgumentNullException.ThrowIfNull(symbolicReference);
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

        var reference = ResolveSymbolicReference(symbolicReference);
        var comparable = reference.Available && active;
        var agreement = comparable
            ? CalculateAgreement(leftDrive, rightDrive, reference.Left, reference.Right)
            : previous.Agreement;
        var agreementEma = comparable
            ? (previous.ActiveEvaluationSamples == 0 ? agreement : Lerp(previous.AgreementEma, agreement, MetricsAlpha))
            : previous.AgreementEma;
        var evaluationSamples = reference.Available ? previous.EvaluationSamples + 1 : previous.EvaluationSamples;
        var activeEvaluationSamples = comparable ? previous.ActiveEvaluationSamples + 1 : previous.ActiveEvaluationSamples;
        var actionPromotionReady = !actionDecision.Available ||
            (actionDecision.Confidence >= settings.PromotionMinimumConfidence &&
             actionDecision.CircuitCoverage >= settings.PromotionMinimumCoverage);
        var qualified = comparable &&
            actionPromotionReady &&
            agreementEma >= settings.PromotionMinimumAgreement &&
            confidenceEma >= settings.PromotionMinimumConfidence &&
            motorCoverage >= settings.PromotionMinimumCoverage;
        var qualifiedTicks = qualified ? previous.QualifiedConsecutiveTicks + 1 : 0;
        var promotionReady = activeEvaluationSamples >= settings.PromotionMinimumSamples &&
            qualifiedTicks >= settings.PromotionConsecutiveTicks;

        var forwardDrive = Math.Clamp((leftDrive + rightDrive) * 0.5, 0.0, 1.0);
        var turnDrive = Math.Clamp(rightDrive - leftDrive, -1.0, 1.0);
        return new NeuronalMotorRuntime(
            Mode: settings.Mode,
            ControlGeneration: control.Generation,
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
            SymbolicReferenceAvailable: reference.Available,
            SymbolicReferenceLeft: reference.Left,
            SymbolicReferenceRight: reference.Right,
            Agreement: agreement,
            AgreementEma: agreementEma,
            EvaluationSamples: evaluationSamples,
            ActiveEvaluationSamples: activeEvaluationSamples,
            QualifiedConsecutiveTicks: qualifiedTicks,
            PromotionReady: promotionReady,
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

    private static (bool Available, double Left, double Right) ResolveSymbolicReference(IntentionalActionLoopRuntime intent)
    {
        if (!intent.Active || string.IsNullOrWhiteSpace(intent.MotorDirective))
        {
            return (false, 0.0, 0.0);
        }

        var directive = intent.MotorDirective.Trim().ToLowerInvariant();
        if (directive.Contains("idle", StringComparison.Ordinal) ||
            directive.Contains("stop", StringComparison.Ordinal) ||
            directive.Contains("rest", StringComparison.Ordinal) ||
            directive.Contains("guard", StringComparison.Ordinal) ||
            directive.Contains("immobilize", StringComparison.Ordinal))
        {
            return (true, 0.0, 0.0);
        }

        if (directive.Contains("about_face_left", StringComparison.Ordinal) ||
            directive.Contains("turn_around_left", StringComparison.Ordinal) ||
            directive.Contains("turn_left", StringComparison.Ordinal) ||
            directive.Contains("pivot_left", StringComparison.Ordinal))
        {
            return (true, -1.0, 1.0);
        }

        if (directive.Contains("about_face", StringComparison.Ordinal) ||
            directive.Contains("turn_around", StringComparison.Ordinal) ||
            directive.Contains("turn_right", StringComparison.Ordinal) ||
            directive.Contains("pivot_right", StringComparison.Ordinal))
        {
            return (true, 1.0, -1.0);
        }

        if (directive.Contains("bear_left", StringComparison.Ordinal) || directive.Contains("arc_left", StringComparison.Ordinal))
        {
            return (true, 0.20, 1.0);
        }

        if (directive.Contains("bear_right", StringComparison.Ordinal) || directive.Contains("arc_right", StringComparison.Ordinal))
        {
            return (true, 1.0, 0.20);
        }

        if (directive.Contains("avoid", StringComparison.Ordinal) ||
            directive.Contains("escape", StringComparison.Ordinal) ||
            directive.Contains("back", StringComparison.Ordinal) ||
            directive.Contains("retreat", StringComparison.Ordinal))
        {
            return (true, -1.0, -1.0);
        }

        if (directive.Contains("slow_protect", StringComparison.Ordinal))
        {
            return (true, 0.25, 0.25);
        }

        if (directive.Contains("forward", StringComparison.Ordinal) ||
            directive.Contains("approach", StringComparison.Ordinal) ||
            directive.Contains("seek", StringComparison.Ordinal) ||
            directive.Contains("explore", StringComparison.Ordinal))
        {
            return (true, 1.0, 1.0);
        }

        return (false, 0.0, 0.0);
    }

    private static double CalculateAgreement(double left, double right, double referenceLeft, double referenceRight)
    {
        var distance = Math.Sqrt(
            Math.Pow(left - referenceLeft, 2.0) +
            Math.Pow(right - referenceRight, 2.0));
        return Math.Clamp(1.0 - (distance / Math.Sqrt(8.0)), 0.0, 1.0);
    }

    private static double Lerp(double current, double target, double alpha)
        => current + ((target - current) * alpha);
}

internal sealed record NeuronalMotorModeRequest(string? Mode);
