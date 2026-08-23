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
    int MaxPopulationEventsPerSide,
    int ActionPersistenceMilliseconds = 350,
    double ActionPersistenceBias = 0.06,
    double ReciprocalReleaseAlpha = 0.65)
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
            MaxPopulationEventsPerSide: section.GetValue<int?>("MaxPopulationEventsPerSide") ?? 12,
            ActionPersistenceMilliseconds: section.GetValue<int?>("ActionPersistenceMilliseconds") ?? 350,
            ActionPersistenceBias: section.GetValue<double?>("ActionPersistenceBias") ?? 0.06,
            ReciprocalReleaseAlpha: section.GetValue<double?>("ReciprocalReleaseAlpha") ?? 0.65));
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
            MaxPopulationEventsPerSide = Math.Clamp(value.MaxPopulationEventsPerSide, 1, 64),
            ActionPersistenceMilliseconds = Math.Clamp(value.ActionPersistenceMilliseconds, 0, 5000),
            ActionPersistenceBias = Math.Clamp(value.ActionPersistenceBias, 0.0, 0.25),
            ReciprocalReleaseAlpha = Math.Clamp(value.ReciprocalReleaseAlpha, 0.01, 1.0)
        };
    }
}

internal sealed record NeuronalMotorControlSnapshot(
    NeuronalMotorControlSettings Settings);

internal static class VestibuloReticularPopulationDecoder
{
    public static VestibuloReticularDiagnostics? Decode(
        IReadOnlyList<InstanceStructureSnapshot> snapshots)
    {
        var vestibularTotal = 0f;
        var vestibularCount = 0;
        var reticularTotal = 0f;
        var reticularCount = 0;
        var vermisTotal = 0f;
        var vermisCount = 0;
        var spinalTotal = 0f;
        var spinalCount = 0;
        var norepinephrineTotal = 0f;

        foreach (var snapshot in snapshots)
        {
            switch (snapshot.StructureId)
            {
                case StructureId.VestibularNuclei:
                    vestibularTotal += snapshot.MeanFiringRateHz;
                    vestibularCount++;
                    break;
                case StructureId.ReticularFormation:
                    reticularTotal += snapshot.MeanFiringRateHz;
                    norepinephrineTotal += snapshot.NeuromodLocal.NorepinephrineLevel;
                    reticularCount++;
                    break;
                case StructureId.CerebellarVermis:
                    vermisTotal += snapshot.MeanFiringRateHz;
                    vermisCount++;
                    break;
                case StructureId.SpinalCordMotor:
                    spinalTotal += snapshot.MeanFiringRateHz;
                    spinalCount++;
                    break;
            }
        }

        if (vestibularCount + reticularCount + vermisCount + spinalCount == 0)
        {
            return null;
        }

        return Compose(
            Mean(vestibularTotal, vestibularCount),
            Mean(reticularTotal, reticularCount),
            Mean(vermisTotal, vermisCount),
            Mean(spinalTotal, spinalCount),
            Mean(norepinephrineTotal, reticularCount));
    }

    public static VestibuloReticularDiagnostics Compose(
        float vestibular,
        float reticular,
        float vermis,
        float spinalTone,
        float norepinephrine)
    {
        var arousal = reticular * (0.80f + Math.Clamp(norepinephrine, 0f, 1f) * 0.55f);
        var balanceError = Math.Max(0f, vestibular - ((vermis * 0.55f) + (spinalTone * 0.25f)));
        var postureStability = Math.Clamp(
            (vermis * 0.35f) +
            (spinalTone * 0.30f) +
            (arousal * 0.20f) -
            (balanceError * 0.25f),
            0f,
            120f);

        return new VestibuloReticularDiagnostics(
            SelectMode(vestibular, arousal, vermis, spinalTone, balanceError),
            vestibular,
            arousal,
            vermis,
            spinalTone,
            postureStability,
            balanceError);
    }

    public static string SelectMode(
        float vestibular,
        float reticular,
        float vermis,
        float spinalTone,
        float balanceError)
    {
        if (balanceError > Math.Max(0.20f, vermis * 0.75f))
        {
            return "Rebalancing";
        }

        if (reticular > Math.Max(0.18f, spinalTone * 1.20f))
        {
            return "Aroused";
        }

        return "Steady";
    }

    private static float Mean(float total, int count)
        => count > 0 ? total / count : 0f;
}

internal sealed class NeuronalMotorPopulationWindow
{
    private static readonly HashSet<StructureId> MotorStructures =
    [
        StructureId.PremotorCortex,
        StructureId.Sma,
        StructureId.M1,
        StructureId.MotorThalamus,
		StructureId.RedNucleus,
		StructureId.DentateNucleus,
		StructureId.InterposedNuclei,
		StructureId.FastigialNucleus,
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
           snapshot.StructureId == StructureId.SuperiorColliculus ||
           snapshot.ActionSelectionDiagnostics is not null ||
           snapshot.BasalGangliaDiagnostics is not null ||
           snapshot.CerebellarDiagnostics is not null ||
           snapshot.VestibuloReticularDiagnostics is not null ||
           snapshot.StructureId == StructureId.Habenula;
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
    long Tick,
    long Sequence,
    double LeftDrive,
    double RightDrive,
    double ForwardDrive,
    double TurnDrive,
    double ManipulatorDrive,
    double LeftShoulderSagittalDrive,
    double RightShoulderSagittalDrive,
    double LeftShoulderCoronalDrive,
    double RightShoulderCoronalDrive,
    double LeftElbowDrive,
    double RightElbowDrive,
    double HeadYawDrive,
    double HeadPitchDrive,
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
    bool ActionCircuitObserved = false,
    double StandDrive = 0.0,
    double CrouchDrive = 0.0,
    double SitDrive = 0.0,
    double LieDrive = 0.0,
    double LeftHipCoronalDrive = 0.0,
    double RightHipCoronalDrive = 0.0,
    double LeftAnkleSagittalDrive = 0.0,
    double RightAnkleSagittalDrive = 0.0,
    double LeftAnkleCoronalDrive = 0.0,
    double RightAnkleCoronalDrive = 0.0,
    long ActionProgramStartedTick = 0,
    long ActionProgramStartedMonotonicMs = 0,
    bool ActionPersistenceApplied = false,
    double TrunkYawDrive = 0.0,
    double SpinalWithdrawalDrive = 0.0,
    IReadOnlyList<SpinalWithdrawalSourceActivity>? SpinalWithdrawalSources = null,
    double ActionFunctionalCoverage = 0.0,
    string ActionAuthorityReason = "Action-selection circuit not observed.",
    IReadOnlyList<ActionAuthorityChannelTrace>? ActionChannelTraces = null,
    double LeftHandGraspDrive = 0.0,
    double RightHandGraspDrive = 0.0,
    bool RightingLatchActive = false,
    int RightingStableTicks = 0,
    long RightingEnteredTick = 0,
    long RightingRecoveredTick = 0,
    bool FreshActionRequired = false)
{
    public static NeuronalMotorRuntime Default { get; } = new(
        Active: false,
        Tick: 0,
        Sequence: 0,
        LeftDrive: 0.0,
        RightDrive: 0.0,
        ForwardDrive: 0.0,
        TurnDrive: 0.0,
        ManipulatorDrive: 0.0,
        LeftShoulderSagittalDrive: 0.0,
        RightShoulderSagittalDrive: 0.0,
        LeftShoulderCoronalDrive: 0.0,
        RightShoulderCoronalDrive: 0.0,
        LeftElbowDrive: 0.0,
        RightElbowDrive: 0.0,
        HeadYawDrive: 0.0,
        HeadPitchDrive: 0.0,
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
    private static readonly HashSet<StructureId> RequiredMotorStructures =
    [
        StructureId.PremotorCortex,
        StructureId.Sma,
        StructureId.M1,
        StructureId.MotorThalamus,
        StructureId.ReticularFormation,
        StructureId.SpinalCordMotor
    ];

    private static readonly IReadOnlyDictionary<StructureId, double> MotorWeights =
        new Dictionary<StructureId, double>
        {
            [StructureId.PremotorCortex] = 0.15,
            [StructureId.Sma] = 0.12,
            [StructureId.M1] = 0.30,
            [StructureId.MotorThalamus] = 0.08,
			[StructureId.RedNucleus] = 0.08,
			[StructureId.DentateNucleus] = 0.04,
			[StructureId.InterposedNuclei] = 0.05,
			[StructureId.FastigialNucleus] = 0.04,
            [StructureId.ReticularFormation] = 0.10,
            [StructureId.SpinalCordMotor] = 0.25
        };

    private const double MetricsAlpha = 0.02;

    public static NeuronalMotorRuntime Decode(
        long tick,
        IReadOnlyList<InstanceStructureSnapshot> snapshots,
        NeuronalMotorControlSnapshot control,
        NeuronalMotorRuntime previous,
        long monotonicMilliseconds = -1)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(previous);

        var settings = control.Settings;
        var requiredWeightPerSide = MotorWeights
            .Where(pair => RequiredMotorStructures.Contains(pair.Key))
            .Sum(static pair => pair.Value);
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
        var leftRequiredObservedWeight = 0.0;
        var rightRequiredObservedWeight = 0.0;
        foreach (var pair in MotorWeights)
        {
            if (ratesByPopulation.TryGetValue((pair.Key, "L"), out var left))
            {
                leftWeightedRate += (left.Sum / left.Count) * pair.Value;
                leftObservedWeight += pair.Value;
                if (RequiredMotorStructures.Contains(pair.Key))
                {
                    leftRequiredObservedWeight += pair.Value;
                }
            }

            if (ratesByPopulation.TryGetValue((pair.Key, "R"), out var right))
            {
                rightWeightedRate += (right.Sum / right.Count) * pair.Value;
                rightObservedWeight += pair.Value;
                if (RequiredMotorStructures.Contains(pair.Key))
                {
                    rightRequiredObservedWeight += pair.Value;
                }
            }

            if (ratesByPopulation.TryGetValue((pair.Key, "M"), out var midline))
            {
                var midlineRate = midline.Sum / midline.Count;
                leftWeightedRate += midlineRate * pair.Value * 0.5;
                rightWeightedRate += midlineRate * pair.Value * 0.5;
                leftObservedWeight += pair.Value * 0.5;
                rightObservedWeight += pair.Value * 0.5;
                if (RequiredMotorStructures.Contains(pair.Key))
                {
                    leftRequiredObservedWeight += pair.Value * 0.5;
                    rightRequiredObservedWeight += pair.Value * 0.5;
                }
            }
        }

        var leftPopulation = leftObservedWeight > 0.0 ? leftWeightedRate / leftObservedWeight : 0.0;
        var rightPopulation = rightObservedWeight > 0.0 ? rightWeightedRate / rightObservedWeight : 0.0;
        // Authority requires a bilateral descending path. An arithmetic total can
        // hide a complete hemisphere loss, so coverage is set by the weaker side.
        var leftCoverage = Math.Clamp(leftRequiredObservedWeight / Math.Max(0.001, requiredWeightPerSide), 0.0, 1.0);
        var rightCoverage = Math.Clamp(rightRequiredObservedWeight / Math.Max(0.001, requiredWeightPerSide), 0.0, 1.0);
        var motorCoverage = Math.Min(leftCoverage, rightCoverage);

        var physicalNowMs = monotonicMilliseconds >= 0 ? monotonicMilliseconds : tick;
        var previousSelectionAgeMs = previous.SelectedActionChannel >= 0 &&
            previous.ActionProgramStartedMonotonicMs > 0 &&
            physicalNowMs >= previous.ActionProgramStartedMonotonicMs
                ? physicalNowMs - previous.ActionProgramStartedMonotonicMs
                : long.MaxValue;
        var unassistedDecision = NeuronalActionSelectionDecoder.Decode(snapshots, -1, 0.0);
        var posturalPopulation = VestibuloReticularPopulationDecoder.Decode(snapshots);
        var balancePredictionError = posturalPopulation?.BalanceError ?? 0f;
        var aversivePressure = snapshots
            .Where(static snapshot => snapshot.StructureId == StructureId.Habenula)
            .Select(snapshot => NormalizeRate(snapshot.MeanFiringRateHz, settings))
            .DefaultIfEmpty(0.0)
            .Max();
        var withdrawalReflex = DecodeSpinalWithdrawalReflex(snapshots);
        var rightingReflex = DecodeBilateralRightingReflex(snapshots);
        var rightingTrigger = rightingReflex.Bilateral &&
            balancePredictionError >= 0.40f &&
            rightingReflex.Drive >= 0.04;
        var rightingLatchActive = previous.RightingLatchActive;
        var rightingStableTicks = previous.RightingStableTicks;
        var rightingEnteredTick = previous.RightingEnteredTick;
        var rightingRecoveredTick = previous.RightingRecoveredTick;
        var rightingJustRecovered = false;
        if (rightingTrigger)
        {
            if (!rightingLatchActive)
            {
                rightingEnteredTick = tick;
            }
            rightingLatchActive = true;
            rightingStableTicks = 0;
        }
        else if (rightingLatchActive)
        {
            // Proprioceptive and vestibular stand lanes carry tonic postural
            // tone even after balance has recovered. They must not hold the
            // emergency righting latch indefinitely; the physical balance
            // error is the recovery signal, while renewed error retriggers it.
            var physicallySettled = balancePredictionError <= 0.18f;
            rightingStableTicks = physicallySettled ? rightingStableTicks + 1 : 0;
            if (rightingStableTicks >= 4)
            {
                rightingLatchActive = false;
                rightingStableTicks = 0;
                rightingRecoveredTick = tick;
                rightingJustRecovered = true;
            }
        }
        var aversiveReleaseAgeMs = Math.Max(250L, settings.ActionPersistenceMilliseconds);
        var aversiveActionRelease = previous.SelectedActionChannel >= 0 &&
            previousSelectionAgeMs >= aversiveReleaseAgeMs &&
            aversivePressure >= 0.55;
        var spinalActionRelease = previous.SelectedActionChannel >= 0 &&
            withdrawalReflex.PeakDrive >= 0.04;
        var protectiveActionRelease = aversiveActionRelease || spinalActionRelease ||
            rightingLatchActive || rightingJustRecovered;
        var reciprocalRelease = unassistedDecision.Active &&
            IsReciprocalAction(previous.SelectedActionChannel, unassistedDecision.SelectedChannel) &&
            unassistedDecision.SelectionMargin >= 0.0025;
        var persistenceSuppressed = reciprocalRelease ||
            balancePredictionError >= 0.65f ||
            aversivePressure >= 0.55 ||
            withdrawalReflex.PeakDrive >= 0.04 ||
            rightingLatchActive || rightingJustRecovered;
        // The preference itself is capped at 0.06, so it can only affect a
        // decision whose unassisted margin is no greater than that weak bias.
        var nearTie = !unassistedDecision.Available ||
            unassistedDecision.SelectionMargin <= settings.ActionPersistenceBias;
        var persistenceFraction = previousSelectionAgeMs < settings.ActionPersistenceMilliseconds &&
            settings.ActionPersistenceMilliseconds > 0 &&
            nearTie &&
            !persistenceSuppressed
                ? 1.0 - (previousSelectionAgeMs / (double)settings.ActionPersistenceMilliseconds)
                : 0.0;
        var preferredChannel = persistenceFraction > 0.0
            ? previous.SelectedActionChannel
            : -1;
        var actionDecision = NeuronalActionSelectionDecoder.Decode(
            snapshots,
            preferredChannel,
            settings.ActionPersistenceBias * persistenceFraction,
            protectiveActionRelease ? previous.SelectedActionChannel : -1,
            protectiveActionRelease
                ? Math.Max(
                    Math.Max(aversivePressure, withdrawalReflex.PeakDrive),
                    rightingLatchActive || rightingJustRecovered ? 1.0 : 0.0)
                : 0.0);
        var voluntaryAuthorityBlocked = rightingLatchActive || rightingJustRecovered;
        var motorAuthorityReason = voluntaryAuthorityBlocked
            ? rightingLatchActive
                ? $"Motor output blocked by emergency righting latch since tick {rightingEnteredTick}; stable recovery {rightingStableTicks}/4."
                : "Motor output awaits a fresh neuronal selection after righting recovery."
            : actionDecision.AuthorityReason;
        var actionProgramStartedTick = actionDecision.Active && !voluntaryAuthorityBlocked
            ? actionDecision.SelectedChannel == previous.SelectedActionChannel &&
              previous.ActionProgramStartedTick > 0
                ? previous.ActionProgramStartedTick
                : tick
            : 0;
        var actionProgramStartedMonotonicMs = actionDecision.Active && !voluntaryAuthorityBlocked
            ? actionDecision.SelectedChannel == previous.SelectedActionChannel &&
              previous.ActionProgramStartedMonotonicMs > 0
                ? previous.ActionProgramStartedMonotonicMs
                : physicalNowMs
            : 0;
        var actionPersistenceApplied = actionDecision.Active && !voluntaryAuthorityBlocked &&
            preferredChannel >= 0 &&
            actionDecision.SelectedChannel == preferredChannel;
        var basalGanglia = snapshots
            .Select(static snapshot => snapshot.BasalGangliaDiagnostics)
            .Where(static diagnostics => diagnostics is not null)
            .Cast<BasalGangliaDiagnostics>()
            .ToArray();
        var coarseSelectionGate = basalGanglia.Length == 0
            ? 0.50
            : Math.Clamp(basalGanglia.Average(static item =>
                (item.ThalamicDisinhibition * 0.55) +
                (item.DirectPathwayActivation * 0.25) +
                (Math.Max(0.0f, item.ActionSelectionBias) * 0.20)), 0.0, 1.0);
        var coarseOutputInhibition = basalGanglia.Length == 0
            ? 0.50
            : Math.Clamp(basalGanglia.Average(static item => item.OutputNucleusInhibition), 0.0, 1.0);
        // Once the action circuit has selected a lane, its own GPi/SNr output is
        // the relevant motor gate. A global average includes inhibited competing
        // lanes and tonic output-nucleus activity, so it must not veto the winner.
        var outputInhibition = actionDecision.Available
            ? Math.Clamp(actionDecision.OutputInhibition, 0.0, 1.0)
            : coarseOutputInhibition;
        var selectionGate = actionDecision.Available
            ? 1.0 - outputInhibition
            : coarseSelectionGate;
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

        var posturalSupport = posturalPopulation is null
            ? 0.50
            : Math.Clamp(
                (posturalPopulation.PostureStability * 0.50) +
                (posturalPopulation.SpinalMotorTone * 0.30) +
                ((1.0f - posturalPopulation.BalanceError) * 0.20),
                0.0,
                1.0);

        var supportGain = 0.75 + (cerebellarSupport * 0.15) + (posturalSupport * 0.10);
        var unshapedLeft = Math.Clamp(leftPopulation * effectiveGate * supportGain, 0.0, 1.0);
        var unshapedRight = Math.Clamp(rightPopulation * effectiveGate * supportGain, 0.0, 1.0);
        var shaped = NeuronalActionSelectionDecoder.ShapeMotorPopulation(
            actionDecision,
            unshapedLeft,
            unshapedRight);
        var voluntaryNonStandPostureSelected = !voluntaryAuthorityBlocked && actionDecision.Active &&
            actionDecision.SelectedChannel is NeuronalActionSelectionDecoder.CrouchChannel or
                NeuronalActionSelectionDecoder.SitChannel or
                NeuronalActionSelectionDecoder.LieChannel;
        var rightingReleaseActive = rightingLatchActive || rightingJustRecovered;
        var axialForward = withdrawalReflex.DriveFor(NeuronalActionSelectionDecoder.ForwardChannel);
        var axialReverse = withdrawalReflex.DriveFor(NeuronalActionSelectionDecoder.ReverseChannel);
        var axialLeftTurn = withdrawalReflex.DriveFor(NeuronalActionSelectionDecoder.LeftTurnChannel);
        var axialRightTurn = withdrawalReflex.DriveFor(NeuronalActionSelectionDecoder.RightTurnChannel);
        var axialTranslation = Math.Max(axialForward, axialReverse);
        var axialTurning = Math.Max(axialLeftTurn, axialRightTurn);
        var axialWithdrawalActive = Math.Max(axialTranslation, axialTurning) >= 0.04;
        var rawLeft = rightingReleaseActive
            ? 0.0
            : axialWithdrawalActive
                ? axialTranslation >= axialTurning
                    ? axialForward >= axialReverse ? axialForward : -axialReverse
                    : axialLeftTurn >= axialRightTurn ? axialLeftTurn * 0.18 : axialRightTurn
                : shaped.Left;
        var rawRight = rightingReleaseActive
            ? 0.0
            : axialWithdrawalActive
                ? axialTranslation >= axialTurning
                    ? axialForward >= axialReverse ? axialForward : -axialReverse
                    : axialLeftTurn >= axialRightTurn ? axialLeftTurn : axialRightTurn * 0.18
                : shaped.Right;
        var alpha = settings.SmoothingAlpha;
        double ReciprocalSlew(double prior, double target)
        {
            if (rightingReleaseActive && Math.Abs(target) < 0.0001)
            {
                return 0.0;
            }

            var releasing = Math.Abs(target) < 0.0001 ||
                (Math.Abs(prior) >= 0.0001 && Math.Sign(prior) != Math.Sign(target));
            var result = Lerp(
                prior,
                target,
                releasing ? settings.ReciprocalReleaseAlpha : alpha);
            return Math.Abs(result) < 0.0001 ? 0.0 : result;
        }

        var leftDrive = ReciprocalSlew(previous.LeftDrive, rawLeft);
        var rightDrive = ReciprocalSlew(previous.RightDrive, rawRight);
        var armPopulationMagnitude = Math.Clamp(effectiveGate * supportGain, 0.0, 1.0);
        double SignedActionLaneDrive(int positiveChannel, int negativeChannel) =>
            !rightingReleaseActive && actionDecision.Active && actionDecision.SelectedChannel == positiveChannel
                ? armPopulationMagnitude
                : !rightingReleaseActive && actionDecision.Active && actionDecision.SelectedChannel == negativeChannel
                    ? -armPopulationMagnitude
                    : 0.0;
        double WithdrawalAdjustedLaneDrive(int positiveChannel, int negativeChannel, double voluntaryDrive)
        {
            var positive = withdrawalReflex.DriveFor(positiveChannel);
            var negative = withdrawalReflex.DriveFor(negativeChannel);
            var reflexMagnitude = Math.Max(positive, negative);
            if (reflexMagnitude < 0.04)
            {
                return voluntaryDrive;
            }

            // A spinal withdrawal lane excites its agonist and releases the
            // reciprocal antagonist represented by the opposite signed lane.
            return positive >= negative ? positive : -negative;
        }
        var leftShoulderSagittalDrive = ReciprocalSlew(
            previous.LeftShoulderSagittalDrive,
            WithdrawalAdjustedLaneDrive(
                NeuronalActionSelectionDecoder.LeftShoulderFlexionChannel,
                NeuronalActionSelectionDecoder.LeftShoulderExtensionChannel,
                SignedActionLaneDrive(
                    NeuronalActionSelectionDecoder.LeftShoulderFlexionChannel,
                    NeuronalActionSelectionDecoder.LeftShoulderExtensionChannel)));
        var rightShoulderSagittalDrive = ReciprocalSlew(
            previous.RightShoulderSagittalDrive,
            WithdrawalAdjustedLaneDrive(
                NeuronalActionSelectionDecoder.RightShoulderFlexionChannel,
                NeuronalActionSelectionDecoder.RightShoulderExtensionChannel,
                SignedActionLaneDrive(
                    NeuronalActionSelectionDecoder.RightShoulderFlexionChannel,
                    NeuronalActionSelectionDecoder.RightShoulderExtensionChannel)));
        var leftShoulderCoronalDrive = ReciprocalSlew(
            previous.LeftShoulderCoronalDrive,
            WithdrawalAdjustedLaneDrive(
                NeuronalActionSelectionDecoder.LeftShoulderAbductionChannel,
                NeuronalActionSelectionDecoder.LeftShoulderAdductionChannel,
                SignedActionLaneDrive(
                    NeuronalActionSelectionDecoder.LeftShoulderAbductionChannel,
                    NeuronalActionSelectionDecoder.LeftShoulderAdductionChannel)));
        var rightShoulderCoronalDrive = ReciprocalSlew(
            previous.RightShoulderCoronalDrive,
            WithdrawalAdjustedLaneDrive(
                NeuronalActionSelectionDecoder.RightShoulderAbductionChannel,
                NeuronalActionSelectionDecoder.RightShoulderAdductionChannel,
                SignedActionLaneDrive(
                    NeuronalActionSelectionDecoder.RightShoulderAbductionChannel,
                    NeuronalActionSelectionDecoder.RightShoulderAdductionChannel)));
        var leftElbowDrive = ReciprocalSlew(
            previous.LeftElbowDrive,
            WithdrawalAdjustedLaneDrive(
                NeuronalActionSelectionDecoder.LeftElbowFlexionChannel,
                NeuronalActionSelectionDecoder.LeftElbowExtensionChannel,
                SignedActionLaneDrive(
                    NeuronalActionSelectionDecoder.LeftElbowFlexionChannel,
                    NeuronalActionSelectionDecoder.LeftElbowExtensionChannel)));
        var rightElbowDrive = ReciprocalSlew(
            previous.RightElbowDrive,
            WithdrawalAdjustedLaneDrive(
                NeuronalActionSelectionDecoder.RightElbowFlexionChannel,
                NeuronalActionSelectionDecoder.RightElbowExtensionChannel,
                SignedActionLaneDrive(
                    NeuronalActionSelectionDecoder.RightElbowFlexionChannel,
                    NeuronalActionSelectionDecoder.RightElbowExtensionChannel)));
        var leftHipCoronalDrive = ReciprocalSlew(
            previous.LeftHipCoronalDrive,
            WithdrawalAdjustedLaneDrive(
                NeuronalActionSelectionDecoder.LeftHipAbductionChannel,
                NeuronalActionSelectionDecoder.LeftHipAdductionChannel,
                SignedActionLaneDrive(
                    NeuronalActionSelectionDecoder.LeftHipAbductionChannel,
                    NeuronalActionSelectionDecoder.LeftHipAdductionChannel)));
        var rightHipCoronalDrive = ReciprocalSlew(
            previous.RightHipCoronalDrive,
            WithdrawalAdjustedLaneDrive(
                NeuronalActionSelectionDecoder.RightHipAbductionChannel,
                NeuronalActionSelectionDecoder.RightHipAdductionChannel,
                SignedActionLaneDrive(
                    NeuronalActionSelectionDecoder.RightHipAbductionChannel,
                    NeuronalActionSelectionDecoder.RightHipAdductionChannel)));
        var leftAnkleSagittalDrive = ReciprocalSlew(
            previous.LeftAnkleSagittalDrive,
            WithdrawalAdjustedLaneDrive(
                NeuronalActionSelectionDecoder.LeftAnkleDorsiflexionChannel,
                NeuronalActionSelectionDecoder.LeftAnklePlantarflexionChannel,
                SignedActionLaneDrive(
                    NeuronalActionSelectionDecoder.LeftAnkleDorsiflexionChannel,
                    NeuronalActionSelectionDecoder.LeftAnklePlantarflexionChannel)));
        var rightAnkleSagittalDrive = ReciprocalSlew(
            previous.RightAnkleSagittalDrive,
            WithdrawalAdjustedLaneDrive(
                NeuronalActionSelectionDecoder.RightAnkleDorsiflexionChannel,
                NeuronalActionSelectionDecoder.RightAnklePlantarflexionChannel,
                SignedActionLaneDrive(
                    NeuronalActionSelectionDecoder.RightAnkleDorsiflexionChannel,
                    NeuronalActionSelectionDecoder.RightAnklePlantarflexionChannel)));
        var leftAnkleCoronalDrive = ReciprocalSlew(
            previous.LeftAnkleCoronalDrive,
            SignedActionLaneDrive(
                NeuronalActionSelectionDecoder.LeftAnkleInversionChannel,
                NeuronalActionSelectionDecoder.LeftAnkleEversionChannel));
        var rightAnkleCoronalDrive = ReciprocalSlew(
            previous.RightAnkleCoronalDrive,
            SignedActionLaneDrive(
                NeuronalActionSelectionDecoder.RightAnkleInversionChannel,
                NeuronalActionSelectionDecoder.RightAnkleEversionChannel));
        var trunkYawDrive = ReciprocalSlew(
            previous.TrunkYawDrive,
            WithdrawalAdjustedLaneDrive(
                NeuronalActionSelectionDecoder.TrunkRotateRightChannel,
                NeuronalActionSelectionDecoder.TrunkRotateLeftChannel,
                SignedActionLaneDrive(
                    NeuronalActionSelectionDecoder.TrunkRotateRightChannel,
                    NeuronalActionSelectionDecoder.TrunkRotateLeftChannel)));
        var leftHandGraspDrive = ReciprocalSlew(
            previous.LeftHandGraspDrive,
            WithdrawalAdjustedLaneDrive(
                NeuronalActionSelectionDecoder.LeftHandCloseChannel,
                NeuronalActionSelectionDecoder.LeftHandOpenChannel,
                SignedActionLaneDrive(
                    NeuronalActionSelectionDecoder.LeftHandCloseChannel,
                    NeuronalActionSelectionDecoder.LeftHandOpenChannel)));
        var rightHandGraspDrive = ReciprocalSlew(
            previous.RightHandGraspDrive,
            WithdrawalAdjustedLaneDrive(
                NeuronalActionSelectionDecoder.RightHandCloseChannel,
                NeuronalActionSelectionDecoder.RightHandOpenChannel,
                SignedActionLaneDrive(
                    NeuronalActionSelectionDecoder.RightHandCloseChannel,
                    NeuronalActionSelectionDecoder.RightHandOpenChannel)));
        // Retained only as a compatibility/display summary. Physical interaction
        // authority comes exclusively from the sided antagonistic hand lanes.
        var manipulatorDrive = Math.Max(
            Math.Abs(leftHandGraspDrive),
            Math.Abs(rightHandGraspDrive));
        var peripersonalReach = DecodePeripersonalReachGate(snapshots);
        var orienting = DecodeOrientingPopulation(snapshots, settings);
        var headYawDrive = Lerp(previous.HeadYawDrive, orienting.YawDrive, alpha);
        var headPitchDrive = Lerp(previous.HeadPitchDrive, orienting.PitchDrive, alpha);
        double PostureLaneDrive(int channel) => !voluntaryAuthorityBlocked && actionDecision.Active &&
            actionDecision.SelectedChannel == channel
                ? Math.Clamp(effectiveGate * supportGain, 0.0, 1.0)
                : 0.0;
        var descendingStandDrive = PostureLaneDrive(NeuronalActionSelectionDecoder.StandChannel);
        var reflexStandDrive = voluntaryNonStandPostureSelected ? 0.0 : rightingReflex.Drive;
        var standTarget = Math.Max(descendingStandDrive, reflexStandDrive);
        var crouchTarget = PostureLaneDrive(NeuronalActionSelectionDecoder.CrouchChannel);
        var sitTarget = PostureLaneDrive(NeuronalActionSelectionDecoder.SitChannel);
        var lieTarget = PostureLaneDrive(NeuronalActionSelectionDecoder.LieChannel);
        // Basal-ganglia selection names one posture population. Its striatal
        // winner inhibits the losing postural pools immediately; otherwise the
        // smoothing trace lets stand, crouch, sit, and lie remain co-active for
        // many body frames after every neuronal switch.
        var standDrive = standTarget > 0.0
            ? ReciprocalSlew(previous.StandDrive, standTarget)
            : 0.0;
        var crouchDrive = crouchTarget > 0.0
            ? ReciprocalSlew(previous.CrouchDrive, crouchTarget)
            : 0.0;
        var sitDrive = sitTarget > 0.0
            ? ReciprocalSlew(previous.SitDrive, sitTarget)
            : 0.0;
        var lieDrive = lieTarget > 0.0
            ? ReciprocalSlew(previous.LieDrive, lieTarget)
            : 0.0;
        var decodedSignalStrength = new[]
        {
            Math.Abs(leftDrive), Math.Abs(rightDrive), Math.Abs(manipulatorDrive),
            Math.Abs(leftShoulderSagittalDrive), Math.Abs(rightShoulderSagittalDrive),
            Math.Abs(leftShoulderCoronalDrive), Math.Abs(rightShoulderCoronalDrive),
            Math.Abs(leftElbowDrive), Math.Abs(rightElbowDrive),
            Math.Abs(leftHipCoronalDrive), Math.Abs(rightHipCoronalDrive),
            Math.Abs(leftAnkleSagittalDrive), Math.Abs(rightAnkleSagittalDrive),
            Math.Abs(leftAnkleCoronalDrive), Math.Abs(rightAnkleCoronalDrive),
            Math.Abs(leftHandGraspDrive), Math.Abs(rightHandGraspDrive),
            Math.Abs(trunkYawDrive),
            Math.Abs(headYawDrive), Math.Abs(headPitchDrive),
            Math.Abs(standDrive), Math.Abs(crouchDrive), Math.Abs(sitDrive), Math.Abs(lieDrive)
        }.Max();

        var supportCoverage = ((cerebellar.Length > 0 ? 1.0 : 0.0) + (posturalPopulation is not null ? 1.0 : 0.0)) * 0.5;
        var motorConfidence = Math.Clamp(
            (motorCoverage * 0.48) +
            (decodedSignalStrength * 0.24) +
            (basalGangliaCoverage * 0.18) +
            (supportCoverage * 0.10),
            0.0,
            1.0);
        var confidence = actionDecision.Available
            ? Math.Clamp((motorConfidence * 0.72) + (actionDecision.Confidence * 0.28), 0.0, 1.0)
            : motorConfidence;
        confidence = Math.Max(confidence, rightingReflex.Confidence);
        confidence = Math.Max(confidence, withdrawalReflex.Confidence);
        confidence = Math.Max(confidence, orienting.Confidence);
        var confidenceEma = previous.Tick <= 0
            ? confidence
            : Lerp(previous.ConfidenceEma, confidence, MetricsAlpha);
        var actionAuthorityReady = !voluntaryAuthorityBlocked && actionDecision.Available &&
            actionDecision.Active &&
            actionDecision.Confidence >= settings.MinimumOutputConfidence;
        var rightingAuthorityReady = rightingLatchActive && rightingReflex.Bilateral &&
            !voluntaryNonStandPostureSelected &&
            rightingReflex.Drive >= 0.04;
        var withdrawalAuthorityReady = withdrawalReflex.Available &&
            withdrawalReflex.PeakDrive >= 0.04;
        var descendingMotorReady = motorCoverage >= settings.MinimumCircuitCoverage &&
            ((actionAuthorityReady && confidence >= settings.MinimumOutputConfidence) ||
             rightingAuthorityReady ||
             withdrawalAuthorityReady);
        var orientingAuthorityReady = orienting.Available &&
            orienting.Confidence >= settings.MinimumOutputConfidence;

        if (!descendingMotorReady)
        {
            leftDrive = 0.0;
            rightDrive = 0.0;
            manipulatorDrive = 0.0;
            leftHandGraspDrive = 0.0;
            rightHandGraspDrive = 0.0;
            leftShoulderSagittalDrive = 0.0;
            rightShoulderSagittalDrive = 0.0;
            leftShoulderCoronalDrive = 0.0;
            rightShoulderCoronalDrive = 0.0;
            leftElbowDrive = 0.0;
            rightElbowDrive = 0.0;
            leftHipCoronalDrive = 0.0;
            rightHipCoronalDrive = 0.0;
            leftAnkleSagittalDrive = 0.0;
            rightAnkleSagittalDrive = 0.0;
            leftAnkleCoronalDrive = 0.0;
            rightAnkleCoronalDrive = 0.0;
            trunkYawDrive = 0.0;
            standDrive = 0.0;
            crouchDrive = 0.0;
            sitDrive = 0.0;
            lieDrive = 0.0;
        }

        if (!orientingAuthorityReady)
        {
            headYawDrive = 0.0;
            headPitchDrive = 0.0;
        }

        var authorizedSignalStrength = new[]
        {
            Math.Abs(leftDrive), Math.Abs(rightDrive), Math.Abs(manipulatorDrive),
            Math.Abs(leftShoulderSagittalDrive), Math.Abs(rightShoulderSagittalDrive),
            Math.Abs(leftShoulderCoronalDrive), Math.Abs(rightShoulderCoronalDrive),
            Math.Abs(leftElbowDrive), Math.Abs(rightElbowDrive),
            Math.Abs(leftHipCoronalDrive), Math.Abs(rightHipCoronalDrive),
            Math.Abs(leftAnkleSagittalDrive), Math.Abs(rightAnkleSagittalDrive),
            Math.Abs(leftAnkleCoronalDrive), Math.Abs(rightAnkleCoronalDrive),
            Math.Abs(leftHandGraspDrive), Math.Abs(rightHandGraspDrive),
            Math.Abs(trunkYawDrive),
            Math.Abs(headYawDrive), Math.Abs(headPitchDrive),
            Math.Abs(standDrive), Math.Abs(crouchDrive), Math.Abs(sitDrive), Math.Abs(lieDrive)
        }.Max();
        var active = authorizedSignalStrength >= 0.01;

        var forwardDrive = Math.Clamp((leftDrive + rightDrive) * 0.5, 0.0, 1.0);
        var turnDrive = Math.Clamp(rightDrive - leftDrive, -1.0, 1.0);
        return new NeuronalMotorRuntime(
            Active: active,
            Tick: tick,
            Sequence: previous.Sequence + 1,
            LeftDrive: leftDrive,
            RightDrive: rightDrive,
            ForwardDrive: forwardDrive,
            TurnDrive: turnDrive,
            ManipulatorDrive: manipulatorDrive,
            LeftShoulderSagittalDrive: leftShoulderSagittalDrive,
            RightShoulderSagittalDrive: rightShoulderSagittalDrive,
            LeftShoulderCoronalDrive: leftShoulderCoronalDrive,
            RightShoulderCoronalDrive: rightShoulderCoronalDrive,
            LeftElbowDrive: leftElbowDrive,
            RightElbowDrive: rightElbowDrive,
            HeadYawDrive: headYawDrive,
            HeadPitchDrive: headPitchDrive,
            MotorCircuitCoverage: motorCoverage,
            SelectionGate: selectionGate,
            OutputInhibition: outputInhibition,
            Confidence: confidence,
            ConfidenceEma: confidenceEma,
            MinimumOutputConfidence: settings.MinimumOutputConfidence,
            MaxPopulationEventsPerSide: settings.MaxPopulationEventsPerSide,
            Evidence: $"motor-populations={observedStructures.Count}/{MotorWeights.Count}; bilateral-coverage={motorCoverage:0.000}; orienting={(orienting.Available ? $"topographic:{orienting.Coverage:0.000}" : "missing")}; basal-ganglia={(basalGanglia.Length > 0 ? "observed" : "missing")}; action-channels={(actionDecision.Available ? (actionDecision.Active ? "selected" : "suppressed") : "missing")}; persistence={(actionPersistenceApplied ? $"retained:{actionDecision.SelectedChannel},age={previousSelectionAgeMs}ms" : persistenceSuppressed ? "neuronally-released" : "released")}; protective-release={(rightingReleaseActive ? "righting" : spinalActionRelease ? "spinal" : aversiveActionRelease ? "aversive" : "quiet")}; righting={(rightingReflex.Bilateral ? $"bilateral:{rightingReflex.Drive:0.000}(afferent={rightingReflex.AfferentDrive:0.000}[L={rightingReflex.AfferentLeft:0.000},R={rightingReflex.AfferentRight:0.000}],descending={rightingReflex.DescendingDrive:0.000}[L={rightingReflex.DescendingLeft:0.000},R={rightingReflex.DescendingRight:0.000}])" : "incomplete")}; balance-error={(posturalPopulation is null ? "missing" : $"population:{balancePredictionError:0.000}")}; withdrawal={(withdrawalReflex.Available ? $"spinal:{withdrawalReflex.PeakDrive:0.000}" : "quiet")}; reach-gate={(peripersonalReach.Available ? $"ppc:{peripersonalReach.Gate:0.000}(near={peripersonalReach.NearBodyDrive:0.000},peri={peripersonalReach.PeripersonalDrive:0.000},far={peripersonalReach.FarSpaceDrive:0.000})" : "missing")}; cerebellar={(cerebellar.Length > 0 ? "observed" : "missing")}; posture={(posturalPopulation is not null ? "observed" : "missing")}",
            SelectedActionChannel: voluntaryAuthorityBlocked ? -1 : actionDecision.SelectedChannel,
            ActionSelectionConfidence: actionDecision.Confidence,
            ActionCircuitCoverage: actionDecision.CircuitCoverage,
            ActionSelectionMargin: actionDecision.SelectionMargin,
            ActionCircuitObserved: actionDecision.Available,
            StandDrive: standDrive,
            CrouchDrive: crouchDrive,
            SitDrive: sitDrive,
            LieDrive: lieDrive,
            LeftHipCoronalDrive: leftHipCoronalDrive,
            RightHipCoronalDrive: rightHipCoronalDrive,
            LeftAnkleSagittalDrive: leftAnkleSagittalDrive,
            RightAnkleSagittalDrive: rightAnkleSagittalDrive,
            LeftAnkleCoronalDrive: leftAnkleCoronalDrive,
            RightAnkleCoronalDrive: rightAnkleCoronalDrive,
            ActionProgramStartedTick: actionProgramStartedTick,
            ActionProgramStartedMonotonicMs: actionProgramStartedMonotonicMs,
            ActionPersistenceApplied: actionPersistenceApplied,
            TrunkYawDrive: trunkYawDrive,
            SpinalWithdrawalDrive: withdrawalReflex.PeakDrive,
            SpinalWithdrawalSources: withdrawalReflex.Sources,
            ActionFunctionalCoverage: actionDecision.FunctionalCoverage,
            ActionAuthorityReason: motorAuthorityReason,
            ActionChannelTraces: actionDecision.ChannelTraces,
            LeftHandGraspDrive: leftHandGraspDrive,
            RightHandGraspDrive: rightHandGraspDrive,
            RightingLatchActive: rightingLatchActive,
            RightingStableTicks: rightingStableTicks,
            RightingEnteredTick: rightingEnteredTick,
            RightingRecoveredTick: rightingRecoveredTick,
            FreshActionRequired: rightingJustRecovered);
    }

    private static bool IsReciprocalAction(int previousChannel, int selectedChannel)
    {
        if (previousChannel < 4 || selectedChannel < 4)
        {
            return previousChannel switch
            {
                NeuronalActionSelectionDecoder.ForwardChannel =>
                    selectedChannel == NeuronalActionSelectionDecoder.ReverseChannel,
                NeuronalActionSelectionDecoder.ReverseChannel =>
                    selectedChannel == NeuronalActionSelectionDecoder.ForwardChannel,
                NeuronalActionSelectionDecoder.LeftTurnChannel =>
                    selectedChannel == NeuronalActionSelectionDecoder.RightTurnChannel,
                NeuronalActionSelectionDecoder.RightTurnChannel =>
                    selectedChannel == NeuronalActionSelectionDecoder.LeftTurnChannel,
                _ => false
            };
        }

        return (previousChannel ^ 1) == selectedChannel;
    }

    private static OrientingPopulation DecodeOrientingPopulation(
        IReadOnlyList<InstanceStructureSnapshot> snapshots,
        NeuronalMotorControlSettings settings)
    {
        const int columns = 16;
        const int rows = 14;
        var weightedX = 0.0;
        var weightedY = 0.0;
        var totalWeight = 0.0;
        var observed = 0;
        var expected = 0;
        var populationGain = 0.0;

        foreach (var snapshot in snapshots.Where(static item =>
                     item.StructureId == StructureId.SuperiorColliculus))
        {
            expected++;
            populationGain = Math.Max(populationGain, NormalizeRate(snapshot.MeanFiringRateHz, settings));
            var usedSnapshot = false;
            foreach (var neuron in snapshot.TopActiveNeurons)
            {
                if (!TryParseRetinotopicIndex(neuron.NeuronId, columns * rows, out var index))
                {
                    continue;
                }

                var weight = Math.Max(0.0, neuron.FiringRateHz - settings.BaselineRateHz);
                if (weight <= 0.0)
                {
                    continue;
                }

                var column = index % columns;
                var row = index / columns;
                weightedX += ((column - ((columns - 1) * 0.5)) / ((columns - 1) * 0.5)) * weight;
                weightedY += ((((rows - 1) * 0.5) - row) / ((rows - 1) * 0.5)) * weight;
                totalWeight += weight;
                usedSnapshot = true;
            }

            if (usedSnapshot)
            {
                observed++;
            }
        }

        if (totalWeight <= 0.0 || observed == 0)
        {
            return OrientingPopulation.None;
        }

        var coverage = Math.Clamp(observed / (double)Math.Max(1, expected), 0.0, 1.0);
        var yaw = Math.Clamp((weightedX / totalWeight) * populationGain, -1.0, 1.0);
        var pitch = Math.Clamp((weightedY / totalWeight) * populationGain, -1.0, 1.0);
        var directionalStrength = Math.Clamp(Math.Sqrt((yaw * yaw) + (pitch * pitch)), 0.0, 1.0);
        var confidence = Math.Clamp((coverage * 0.55) + (populationGain * 0.25) + (directionalStrength * 0.20), 0.0, 1.0);
        return new OrientingPopulation(true, yaw, pitch, coverage, confidence);
    }

    private static bool TryParseRetinotopicIndex(string neuronId, int count, out int index)
    {
        index = -1;
        if (string.IsNullOrWhiteSpace(neuronId))
        {
            return false;
        }

        var separator = neuronId.LastIndexOf('-');
        var token = separator >= 0 ? neuronId[(separator + 1)..] : neuronId;
        return int.TryParse(token, out index) && index >= 0 && index < count;
    }

    private static double NormalizeRate(float rateHz, NeuronalMotorControlSettings settings)
        => Math.Clamp(
            (rateHz - settings.BaselineRateHz) /
            Math.Max(0.1, settings.SaturationRateHz - settings.BaselineRateHz),
            0.0,
            1.0);

    private static BilateralRightingReflex DecodeBilateralRightingReflex(
        IReadOnlyList<InstanceStructureSnapshot> snapshots)
    {
        var descendingLeft = new List<double>();
        var descendingRight = new List<double>();
        var afferentLeft = new List<double>();
        var afferentRight = new List<double>();
        foreach (var snapshot in snapshots)
        {
            if (snapshot.StructureId is not (StructureId.ProprioceptiveAfferents or
                    StructureId.VestibularAfferents or
                    StructureId.SpinalCordMotor) ||
                snapshot.ActionSelectionDiagnostics is not { } diagnostics)
            {
                continue;
            }

            var standLane = diagnostics.Channels.FirstOrDefault(static channel =>
                channel.ChannelIndex == NeuronalActionSelectionDecoder.StandChannel);
            if (standLane is null)
            {
                continue;
            }

			var drive = Math.Clamp(standLane.ReflexDrive, 0.0f, 1.0f);
            var left = snapshot.StructureId == StructureId.SpinalCordMotor
                ? descendingLeft
                : afferentLeft;
            var right = snapshot.StructureId == StructureId.SpinalCordMotor
                ? descendingRight
                : afferentRight;
            switch (snapshot.Instance.HemisphereNormalized)
            {
                case "L":
                    left.Add(drive);
                    break;
                case "R":
                    right.Add(drive);
                    break;
                default:
                    left.Add(drive);
                    right.Add(drive);
                    break;
            }
        }

        var descendingLeftDrive = descendingLeft.Count > 0 ? descendingLeft.Average() : 0.0;
        var descendingRightDrive = descendingRight.Count > 0 ? descendingRight.Average() : 0.0;
        var afferentLeftDrive = afferentLeft.Count > 0 ? afferentLeft.Max() : 0.0;
        var afferentRightDrive = afferentRight.Count > 0 ? afferentRight.Max() : 0.0;
        var bilateral = descendingLeft.Count > 0 && descendingRight.Count > 0 &&
            afferentLeft.Count > 0 && afferentRight.Count > 0;
        var descendingDrive = bilateral
            ? Math.Min(descendingLeftDrive, descendingRightDrive)
            : 0.0;
        var afferentDrive = bilateral
            ? Math.Min(afferentLeftDrive, afferentRightDrive)
            : 0.0;
        var driveValue = bilateral
            ? Math.Min(descendingDrive, afferentDrive)
            : 0.0;
        var confidence = bilateral
            ? Math.Clamp(0.65 + (driveValue * 0.35), 0.0, 1.0)
            : 0.0;
        return new BilateralRightingReflex(
            bilateral,
            driveValue,
            confidence,
            afferentDrive,
            descendingDrive,
            afferentLeftDrive,
            afferentRightDrive,
            descendingLeftDrive,
            descendingRightDrive);
    }

    private static SpinalWithdrawalReflex DecodeSpinalWithdrawalReflex(
        IReadOnlyList<InstanceStructureSnapshot> snapshots)
    {
        var drives = new double[NeuronalActionSelectionDecoder.ChannelCount];
        var sources = new Dictionary<string, SpinalWithdrawalSourceActivity>(StringComparer.Ordinal);
        var observed = false;
        foreach (var snapshot in snapshots)
        {
            if (snapshot.StructureId != StructureId.SpinalCordMotor ||
                snapshot.ActionSelectionDiagnostics is not { } diagnostics)
            {
                continue;
            }

            foreach (var channel in diagnostics.Channels)
            {
                if (!NeuronalWithdrawalReflex.IsWithdrawalChannel(channel.ChannelIndex))
                {
                    continue;
                }

                var drive = Math.Clamp(channel.ReflexDrive, 0.0f, 1.0f);
                drives[channel.ChannelIndex] = Math.Max(drives[channel.ChannelIndex], drive);
                observed |= drive > 0.0;
            }

            foreach (var source in diagnostics.WithdrawalSources ?? [])
            {
                if (!sources.TryGetValue(source.SourceKey, out var current))
                {
                    sources.Add(source.SourceKey, source);
                    continue;
                }

                sources[source.SourceKey] = current with
                {
                    AfferentDrive = Math.Max(current.AfferentDrive, source.AfferentDrive),
                    ReflexDrive = Math.Max(current.ReflexDrive, source.ReflexDrive),
                    RecurrentInhibition = Math.Max(current.RecurrentInhibition, source.RecurrentInhibition),
                    AfferentAgeMilliseconds = Math.Min(
                        current.AfferentAgeMilliseconds,
                        source.AfferentAgeMilliseconds)
                };
            }
        }

        var peak = drives.DefaultIfEmpty(0.0).Max();
        return new SpinalWithdrawalReflex(
            Available: observed,
            PeakDrive: peak,
            Confidence: observed ? Math.Clamp(0.62 + (peak * 0.38), 0.0, 1.0) : 0.0,
            ChannelDrives: drives,
            Sources: sources.Values
                .OrderByDescending(static source => Math.Max(source.ReflexDrive, source.AfferentDrive))
                .ThenBy(static source => source.SourceKey, StringComparer.Ordinal)
                .ToArray());
    }

    private static double Lerp(double current, double target, double alpha)
        => current + ((target - current) * alpha);

    private static PeripersonalReachGate DecodePeripersonalReachGate(
        IReadOnlyList<InstanceStructureSnapshot> snapshots)
    {
        var diagnostics = snapshots
            .Where(static snapshot => snapshot.StructureId == StructureId.Ppc)
            .Select(static snapshot => snapshot.BodySchemaDiagnostics)
            .Where(static bodySchema => bodySchema is not null)
            .Cast<BodySchemaDiagnostics>()
            .ToArray();
        if (diagnostics.Length == 0)
        {
            return PeripersonalReachGate.Unavailable;
        }

        var nearBody = diagnostics.Average(static value => Math.Max(0f, value.NearBodyActivation));
        var peripersonal = diagnostics.Average(static value =>
            Math.Max(0f, value.LeftPeripersonalActivation) +
            Math.Max(0f, value.RightPeripersonalActivation));
        var farSpace = diagnostics.Average(static value => Math.Max(0f, value.FarSpaceActivation));
        var representedSpace = nearBody + peripersonal + farSpace;
        if (representedSpace <= 0.0001)
        {
            return PeripersonalReachGate.Unavailable;
        }

        var reachableFraction = (nearBody + peripersonal) / representedSpace;
        return new PeripersonalReachGate(
            Available: true,
            Gate: Math.Clamp(reachableFraction, 0.0, 1.0),
            NearBodyDrive: nearBody,
            PeripersonalDrive: peripersonal,
            FarSpaceDrive: farSpace);
    }

    private readonly record struct BilateralRightingReflex(
        bool Bilateral,
        double Drive,
        double Confidence,
        double AfferentDrive,
        double DescendingDrive,
        double AfferentLeft,
        double AfferentRight,
        double DescendingLeft,
        double DescendingRight);

    private readonly record struct SpinalWithdrawalReflex(
        bool Available,
        double PeakDrive,
        double Confidence,
        IReadOnlyList<double> ChannelDrives,
        IReadOnlyList<SpinalWithdrawalSourceActivity> Sources)
    {
        public double DriveFor(int channel)
            => channel >= 0 && channel < ChannelDrives.Count ? ChannelDrives[channel] : 0.0;
    }

    private readonly record struct PeripersonalReachGate(
        bool Available,
        double Gate,
        double NearBodyDrive,
        double PeripersonalDrive,
        double FarSpaceDrive)
    {
        public static PeripersonalReachGate Unavailable { get; } = new(false, 1.0, 0.0, 0.0, 0.0);
    }

    private readonly record struct OrientingPopulation(
        bool Available,
        double YawDrive,
        double PitchDrive,
        double Coverage,
        double Confidence)
    {
        public static OrientingPopulation None { get; } = new(false, 0.0, 0.0, 0.0, 0.0);
    }
}
