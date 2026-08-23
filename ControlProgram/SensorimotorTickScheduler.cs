using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

internal sealed record TickParticipantSelection(
    List<ServiceInstance> Participants,
    bool Throttled,
    int FastLaneAvailable,
    int FastLaneSelected,
    int GeneralLaneSelected,
    IReadOnlyList<ServiceInstance> FastLaneInstances);

internal sealed record SensorimotorInstanceCadence(
    string InstanceKey,
    StructureId StructureId,
    string Hemisphere,
    long LastSelectedTick,
    long AgeMilliseconds,
    double CadenceEmaMilliseconds,
    int SelectionCount);

internal sealed record SensorimotorTimingRuntime(
    long Tick,
    long PhysicalBodyInputAgeMilliseconds,
    int FastLaneAvailable,
    int FastLaneSelected,
    int GeneralLaneSelected,
    double FastLaneCadenceMeanMilliseconds,
    double FastLaneCadenceMaxMilliseconds,
    long FastLaneOldestAgeMilliseconds,
    IReadOnlyList<SensorimotorInstanceCadence> Instances)
{
    public static SensorimotorTimingRuntime Default { get; } = new(
        Tick: 0,
        PhysicalBodyInputAgeMilliseconds: -1,
        FastLaneAvailable: 0,
        FastLaneSelected: 0,
        GeneralLaneSelected: 0,
        FastLaneCadenceMeanMilliseconds: 0.0,
        FastLaneCadenceMaxMilliseconds: 0.0,
        FastLaneOldestAgeMilliseconds: -1,
        Instances: []);
}

internal sealed class SensorimotorCadenceTracker
{
    private sealed class InstanceState(ServiceInstance instance)
    {
        public ServiceInstance Instance { get; } = instance;
        public long LastSelectedTick { get; set; }
        public long LastSelectedMonotonicMs { get; set; } = -1;
        public double CadenceEmaMilliseconds { get; set; }
        public int SelectionCount { get; set; }
    }

    private readonly Dictionary<string, InstanceState> _instances = new(StringComparer.OrdinalIgnoreCase);

    public SensorimotorTimingRuntime Observe(
        long tick,
        long monotonicMilliseconds,
        long physicalBodyInputAgeMilliseconds,
        TickParticipantSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        var liveKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var instance in selection.FastLaneInstances)
        {
            liveKeys.Add(instance.InstanceKey);
            if (!_instances.ContainsKey(instance.InstanceKey))
            {
                _instances[instance.InstanceKey] = new InstanceState(instance);
            }
        }

        foreach (var staleKey in _instances.Keys.Where(key => !liveKeys.Contains(key)).ToArray())
        {
            _instances.Remove(staleKey);
        }

        foreach (var instance in selection.Participants.Where(static item => SensorimotorTickScheduler.IsFastLane(item.StructureId)))
        {
            var state = _instances[instance.InstanceKey];
            if (state.LastSelectedMonotonicMs >= 0)
            {
                var interval = Math.Max(0, monotonicMilliseconds - state.LastSelectedMonotonicMs);
                state.CadenceEmaMilliseconds = state.SelectionCount <= 1
                    ? interval
                    : (state.CadenceEmaMilliseconds * 0.80) + (interval * 0.20);
            }

            state.LastSelectedTick = tick;
            state.LastSelectedMonotonicMs = monotonicMilliseconds;
            state.SelectionCount++;
        }

        var snapshots = _instances.Values
            .OrderBy(static state => state.Instance.StructureId)
            .ThenBy(static state => state.Instance.HemisphereNormalized, StringComparer.Ordinal)
            .ThenBy(static state => state.Instance.InstanceKey, StringComparer.Ordinal)
            .Select(state => new SensorimotorInstanceCadence(
                state.Instance.InstanceKey,
                state.Instance.StructureId,
                state.Instance.HemisphereNormalized,
                state.LastSelectedTick,
                state.LastSelectedMonotonicMs < 0
                    ? -1
                    : Math.Max(0, monotonicMilliseconds - state.LastSelectedMonotonicMs),
                state.CadenceEmaMilliseconds,
                state.SelectionCount))
            .ToArray();
        var measuredCadences = snapshots
            .Where(static snapshot => snapshot.SelectionCount > 1)
            .Select(static snapshot => snapshot.CadenceEmaMilliseconds)
            .ToArray();
        var measuredAges = snapshots
            .Where(static snapshot => snapshot.AgeMilliseconds >= 0)
            .Select(static snapshot => snapshot.AgeMilliseconds)
            .ToArray();

        return new SensorimotorTimingRuntime(
            Tick: tick,
            PhysicalBodyInputAgeMilliseconds: physicalBodyInputAgeMilliseconds,
            FastLaneAvailable: selection.FastLaneAvailable,
            FastLaneSelected: selection.FastLaneSelected,
            GeneralLaneSelected: selection.GeneralLaneSelected,
            FastLaneCadenceMeanMilliseconds: measuredCadences.Length == 0 ? 0.0 : measuredCadences.Average(),
            FastLaneCadenceMaxMilliseconds: measuredCadences.Length == 0 ? 0.0 : measuredCadences.Max(),
            FastLaneOldestAgeMilliseconds: measuredAges.Length == 0 ? -1 : measuredAges.Max(),
            Instances: snapshots);
    }
}

/// <summary>
/// Preserves biological elapsed time when load shedding skips a structure.
/// Selection and successful integration are deliberately separate: a failed
/// request must not consume the interval that the structure still needs to
/// integrate when it recovers.
/// </summary>
internal sealed class StructureIntegrationCadence
{
    internal const double MaximumCatchUpMilliseconds = 100.0;

    private readonly object _gate = new();
    private readonly Dictionary<string, double> _lastSuccessfulTimestampMs =
        new(StringComparer.OrdinalIgnoreCase);

    public TickSignal CreateSignal(ServiceInstance instance, TickSignal globalSignal)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(globalSignal);

        double elapsedMilliseconds;
        lock (_gate)
        {
            elapsedMilliseconds = _lastSuccessfulTimestampMs.TryGetValue(instance.InstanceKey, out var lastTimestamp)
                ? globalSignal.TimestampMs - lastTimestamp
                : globalSignal.TickDurationMs;
        }

        if (!double.IsFinite(elapsedMilliseconds) || elapsedMilliseconds <= 0.0)
        {
            elapsedMilliseconds = globalSignal.TickDurationMs;
        }

        elapsedMilliseconds = Math.Clamp(
            elapsedMilliseconds,
            Math.Max(0.001, globalSignal.TickDurationMs),
            MaximumCatchUpMilliseconds);
        return globalSignal with { TickDurationMs = elapsedMilliseconds };
    }

    public void MarkSuccessful(ServiceInstance instance, TickSignal processedSignal)
    {
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(processedSignal);
        lock (_gate)
        {
            _lastSuccessfulTimestampMs[instance.InstanceKey] = processedSignal.TimestampMs;
        }
    }
}

/// <summary>
/// Gives time-critical afferent, balance, action-selection, and descending motor
/// populations a bounded update cadence while retaining round-robin service for
/// every other structure. This changes scheduling only; all signals still pass
/// through the normal neuronal tick and synaptic routing machinery.
/// </summary>
internal static class SensorimotorTickScheduler
{
    private static readonly HashSet<StructureId> FastLaneStructures =
    [
        StructureId.Retina,
        StructureId.V1,
        StructureId.Mt,
        StructureId.SuperiorColliculus,
        StructureId.Ppc,
        StructureId.SomaticAfferents,
        StructureId.ProprioceptiveAfferents,
        StructureId.VestibularAfferents,
        StructureId.S1,
        StructureId.VestibularNuclei,
        StructureId.CerebellarGranule,
        StructureId.CerebellarVermis,
        StructureId.PurkinjeCellLayer,
        StructureId.InferiorOlive,
        StructureId.PontineNuclei,
        StructureId.DentateNucleus,
        StructureId.InterposedNuclei,
        StructureId.FastigialNucleus,
        StructureId.Pfc,
        StructureId.Acc,
        StructureId.PremotorCortex,
        StructureId.Sma,
        StructureId.M1,
        StructureId.Striatum,
        StructureId.GPe,
        StructureId.GPi,
        StructureId.Stn,
        StructureId.Snr,
        StructureId.Snc,
        StructureId.Habenula,
        StructureId.MotorThalamus,
        StructureId.ReticularFormation,
        StructureId.RedNucleus,
        StructureId.SpinalCordMotor
    ];

    public static TickParticipantSelection Select(
        IReadOnlyList<ServiceInstance> availableServices,
        ref int fastLaneCursor,
        ref int generalLaneCursor,
        int maxTickRequestConcurrency,
        double adaptivePressure,
        bool startupWarmup)
    {
        ArgumentNullException.ThrowIfNull(availableServices);
        if (availableServices.Count == 0)
        {
            fastLaneCursor = 0;
            generalLaneCursor = 0;
            return new TickParticipantSelection([], false, 0, 0, 0, []);
        }

        var concurrency = Math.Max(1, maxTickRequestConcurrency);
        var baselineMultiplier = startupWarmup ? 2.0 : 3.0;
        var pressure = Math.Clamp(adaptivePressure, 0.0, 1.0);
        var totalBudget = Math.Clamp(
            (int)Math.Round(concurrency * (baselineMultiplier - ((baselineMultiplier - 1.0) * pressure))),
            concurrency,
            availableServices.Count);

        var fastLane = availableServices
            .Where(static instance => FastLaneStructures.Contains(instance.StructureId))
            .ToArray();
        var generalLane = availableServices
            .Where(static instance => !FastLaneStructures.Contains(instance.StructureId))
            .ToArray();

        if (totalBudget >= availableServices.Count)
        {
            fastLaneCursor = 0;
            generalLaneCursor = 0;
            return new TickParticipantSelection(
                availableServices.ToList(),
                false,
                fastLane.Length,
                fastLane.Length,
                generalLane.Length,
                fastLane);
        }

        // Reserve most of each constrained pass for the closed sensorimotor loop,
        // but always advance at least one associative service so no circuit starves.
        var minimumGeneral = generalLane.Length > 0 ? 1 : 0;
        var desiredFast = Math.Max(1, (int)Math.Round(totalBudget * 0.82));
        var fastBudget = Math.Min(fastLane.Length, Math.Max(0, totalBudget - minimumGeneral));
        fastBudget = Math.Min(fastBudget, desiredFast);
        var generalBudget = Math.Min(generalLane.Length, totalBudget - fastBudget);
        if (generalBudget == 0 && generalLane.Length > 0 && fastBudget > 0)
        {
            fastBudget--;
            generalBudget = 1;
        }

        var selected = new List<ServiceInstance>(fastBudget + generalBudget);
        AddRoundRobin(fastLane, fastBudget, ref fastLaneCursor, selected);
        AddRoundRobin(generalLane, generalBudget, ref generalLaneCursor, selected);
        return new TickParticipantSelection(
            selected,
            true,
            fastLane.Length,
            fastBudget,
            generalBudget,
            fastLane);
    }

    public static bool IsFastLane(StructureId structureId) => FastLaneStructures.Contains(structureId);

    private static void AddRoundRobin(
        IReadOnlyList<ServiceInstance> source,
        int count,
        ref int cursor,
        ICollection<ServiceInstance> destination)
    {
        if (source.Count == 0 || count <= 0)
        {
            cursor = 0;
            return;
        }

        cursor = Math.Clamp(cursor, 0, source.Count - 1);
        for (var index = 0; index < count; index++)
        {
            destination.Add(source[(cursor + index) % source.Count]);
        }

        cursor = (cursor + count) % source.Count;
    }
}
