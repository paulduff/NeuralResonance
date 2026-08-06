using NeuralResonanceEngine.Shared.Contracts;

internal sealed record NeuronalAffectValuationDecision(
    bool Available,
    bool Active,
    int DominantChannel,
    double SelectionMargin,
    double AppetitiveDrive,
    double DefensiveDrive,
    double HomeostaticDrive,
    double ExploratoryDrive,
    double PositiveValence,
    double NegativeValence,
    double Arousal,
    double Conflict,
    double LearningReadiness,
    double CircuitCoverage,
    double Confidence,
    IReadOnlyList<double> ChannelScores)
{
    public const string Authority = "DistributedNeuronalAffectValuation";

    public static NeuronalAffectValuationDecision Unavailable { get; } = new(
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
        0.0,
        0.0,
        0.0,
        0.0,
        [0.0, 0.0, 0.0, 0.0]);
}

internal sealed record NeuronalAffectValuationSnapshot(
    long Tick,
    string Authority,
    bool CanBiasAction,
    string CausalPath,
    NeuronalAffectValuationDecision Valuation);

internal sealed class NeuronalAffectValuationRuntime
{
    private readonly object _gate = new();
    private long _tick = -1;
    private NeuronalAffectValuationDecision _decision = NeuronalAffectValuationDecision.Unavailable;

    public NeuronalAffectValuationDecision Update(
        long tick,
        IReadOnlyList<InstanceStructureSnapshot> snapshots)
    {
        var decoded = NeuronalAffectValuationDecoder.Decode(snapshots);
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

    public NeuronalAffectValuationSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return new NeuronalAffectValuationSnapshot(
                _tick,
                NeuronalAffectValuationDecision.Authority,
                false,
                "connectome-spikes-and-local-receptor-plasticity",
                _decision);
        }
    }
}

internal static class NeuronalAffectValuationDecoder
{
    private const double FiringSaturationHz = 25.0;

    // Monitoring only. This decoder summarizes population activity for inspection;
    // its result is never fed into a neuron, synapse, action lane, or motor decoder.
    public static NeuronalAffectValuationDecision Decode(
        IReadOnlyList<InstanceStructureSnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        var affect = snapshots
            .Select(static snapshot => snapshot.SalienceAffectDiagnostics)
            .Where(static item => item is not null)
            .Cast<SalienceAffectDiagnostics>()
            .ToArray();
        var homeostasis = snapshots
            .Select(static snapshot => snapshot.HypothalamicHomeostasisDiagnostics)
            .Where(static item => item is not null)
            .Cast<HypothalamicHomeostasisDiagnostics>()
            .ToArray();
        var defense = snapshots
            .Select(static snapshot => snapshot.DescendingDefenseDiagnostics)
            .Where(static item => item is not null)
            .Cast<DescendingDefenseDiagnostics>()
            .ToArray();
        var reward = snapshots
            .Select(static snapshot => snapshot.DopamineRewardDiagnostics)
            .Where(static item => item is not null)
            .Cast<DopamineRewardDiagnostics>()
            .ToArray();
        var observedGroups =
            (affect.Length > 0 ? 1 : 0) +
            (homeostasis.Length > 0 ? 1 : 0) +
            (defense.Length > 0 ? 1 : 0) +
            (reward.Length > 0 ? 1 : 0);
        if (observedGroups == 0)
        {
            return NeuronalAffectValuationDecision.Unavailable;
        }

        var threat = MaxNormalized(affect, static item => item.ThreatSalience);
        var interoception = MaxNormalized(affect, static item => item.InteroceptiveDrive);
        var conflict = MaxNormalized(affect, static item => item.ConflictMonitoring);
        var arousal = Math.Max(
            MaxNormalized(affect, static item => item.AutonomicArousal),
            MaxNormalized(homeostasis, static item => item.ArousalPressure));
        var attention = MaxNormalized(affect, static item => item.AttentionGain);
        var control = MaxNormalized(affect, static item => item.ControlBias);
        var setpoint = MaxNormalized(homeostasis, static item => item.HypothalamicSetpointError);
        var limbicPressure = MaxNormalized(homeostasis, static item => item.LimbicHomeostaticPressure);
        var comfortDeficit = MaxNormalized(homeostasis, static item => item.ComfortDeficit);
        var homeostatic = Math.Clamp(
            (setpoint * 0.45) +
            (limbicPressure * 0.30) +
            (interoception * 0.25),
            0.0,
            1.0);
        var defensiveReadiness = Math.Max(
            MaxNormalized(affect, static item => item.DefensiveReadiness),
            Math.Max(
                MaxNormalized(homeostasis, static item => item.DefensiveBodyCommand),
                MaxNormalized(defense, static item => item.ProtectionReadiness)));

        // Human-readable diagnostic mode strings are deliberately ignored. Reward
        // and aversion are decoded only from measured population activity.
        var rewardActivity = Math.Clamp(
            (MaxNormalized(reward, static item => item.VtaPhasicDopamine) * 0.30) +
            (MaxNormalized(reward, static item => item.NucleusAccumbensIncentive) * 0.30) +
            (MaxNormalized(reward, static item => item.StriatalActionValue) * 0.20) +
            (MaxNormalized(reward, static item => item.OrbitofrontalExpectedValue) * 0.20),
            0.0,
            1.0);
        var aversionActivity = Math.Clamp(
            (MaxNormalized(reward, static item => item.HabenulaNegativePrediction) * 0.35) +
            (threat * 0.30) +
            (defensiveReadiness * 0.25) +
            (comfortDeficit * 0.10),
            0.0,
            1.0);
        var learning = MaxNormalized(reward, static item => item.LearningReadiness);
        var positiveValence = Math.Clamp(
            (rewardActivity * 0.72) + (control * 0.16) + (attention * 0.12),
            0.0,
            1.0);
        var negativeValence = Math.Clamp(
            (aversionActivity * 0.62) + (threat * 0.18) +
            (defensiveReadiness * 0.12) + (conflict * 0.08),
            0.0,
            1.0);
        var appetitiveDrive = Math.Clamp(
            (positiveValence * 0.45) + (homeostatic * 0.35) + (interoception * 0.20),
            0.0,
            1.0);
        var defensiveDrive = Math.Clamp(
            (negativeValence * 0.45) + (threat * 0.25) + (defensiveReadiness * 0.30),
            0.0,
            1.0);
        var recoveryDrive = Math.Clamp(
            (homeostatic * 0.48) + (comfortDeficit * 0.32) +
            (interoception * 0.20),
            0.0,
            1.0);
        var exploratoryDrive = Math.Clamp(
            (control * 0.32) + (attention * 0.20) + (learning * 0.28) +
            ((1.0 - conflict) * 0.20),
            0.0,
            1.0);
        var scores = new[]
        {
            appetitiveDrive,
            exploratoryDrive,
            recoveryDrive,
            defensiveDrive
        };
        var ranked = Enumerable.Range(0, scores.Length)
            .OrderByDescending(index => scores[index])
            .ThenBy(static index => index)
            .ToArray();
        var dominant = ranked[0];
        var margin = Math.Max(0.0, scores[dominant] - scores[ranked[1]]);
        var coverage = observedGroups / 4.0;
        var signal = scores[dominant];
        var confidence = Math.Clamp(
            (coverage * 0.48) +
            (Math.Clamp(margin * 3.0, 0.0, 1.0) * 0.24) +
            (signal * 0.20) +
            ((1.0 - conflict) * 0.08),
            0.0,
            1.0);
        var active = coverage >= 0.50 && signal >= 0.08 && margin >= 0.005;

        return new NeuronalAffectValuationDecision(
            Available: true,
            Active: active,
            DominantChannel: active ? dominant : -1,
            SelectionMargin: margin,
            AppetitiveDrive: appetitiveDrive,
            DefensiveDrive: defensiveDrive,
            HomeostaticDrive: recoveryDrive,
            ExploratoryDrive: exploratoryDrive,
            PositiveValence: positiveValence,
            NegativeValence: negativeValence,
            Arousal: arousal,
            Conflict: conflict,
            LearningReadiness: learning,
            CircuitCoverage: coverage,
            Confidence: confidence,
            ChannelScores: scores);
    }

    private static double MaxNormalized<T>(IReadOnlyList<T> values, Func<T, float> selector)
    {
        var maximum = 0.0;
        for (var index = 0; index < values.Count; index++)
        {
            var firingRate = selector(values[index]);
            if (float.IsFinite(firingRate))
            {
                maximum = Math.Max(maximum, firingRate / FiringSaturationHz);
            }
        }

        return Math.Clamp(maximum, 0.0, 1.0);
    }
}
