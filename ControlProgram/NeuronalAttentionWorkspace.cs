using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

internal sealed record NeuronalAttentionWorkspaceDecision(
    bool Available,
    bool Active,
    int SelectedChannel,
    double SelectionMargin,
    IReadOnlyList<double> ChannelScores,
    IReadOnlyList<int> MaintainedChannels,
    int CapacityUsed,
    bool BroadcastActive,
    int BroadcastChannel,
    double DistractorSuppression,
    double CircuitCoverage,
    IReadOnlyList<AttentionWorkspaceChannelActivity> Channels)
{
    public const string Authority = "DistributedNeuronalCompetition";

    public static NeuronalAttentionWorkspaceDecision Unavailable { get; } = new(
        false,
        false,
        -1,
        0.0,
        [],
        [],
        0,
        false,
        -1,
        0.0,
        0.0,
        []);
}

internal sealed record NeuronalAttentionWorkspaceSnapshot(
    long Tick,
    string Authority,
    NeuronalAttentionWorkspaceDecision AttentionWorkspace);

internal sealed class NeuronalAttentionWorkspaceRuntime
{
    private readonly object _gate = new();
    private long _tick = -1;
    private NeuronalAttentionWorkspaceDecision _decision = NeuronalAttentionWorkspaceDecision.Unavailable;

    public NeuronalAttentionWorkspaceDecision Update(long tick, IReadOnlyList<InstanceStructureSnapshot> snapshots)
    {
        var decoded = NeuronalAttentionWorkspaceDecoder.Decode(snapshots);
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

    public NeuronalAttentionWorkspaceSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return new NeuronalAttentionWorkspaceSnapshot(
                _tick,
                NeuronalAttentionWorkspaceDecision.Authority,
                _decision);
        }
    }
}

internal static class NeuronalAttentionWorkspaceDecoder
{
    private const int ChannelCount = 7;
    private static readonly string[] ChannelLabels =
    [
        "visual",
        "auditory",
        "somatosensory",
        "interoceptive",
        "memory",
        "language",
        "motor"
    ];

    public static NeuronalAttentionWorkspaceDecision Decode(IReadOnlyList<InstanceStructureSnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        var diagnostics = snapshots
            .Select(static snapshot => snapshot.NeuronalAttentionWorkspaceDiagnostics)
            .Where(static item => item is not null)
            .Cast<NeuronalAttentionWorkspaceDiagnostics>()
            .ToArray();
        if (diagnostics.Length == 0)
        {
            return NeuronalAttentionWorkspaceDecision.Unavailable;
        }

        var activities = new AttentionWorkspaceChannelActivity[ChannelCount];
        var scores = new double[ChannelCount];
        for (var channel = 0; channel < ChannelCount; channel++)
        {
            var values = diagnostics
                .SelectMany(static diagnostic => diagnostic.Channels)
                .Where(item => item.ChannelIndex == channel)
                .ToArray();
            var sensory = Max(values, static item => item.SensoryDrive);
            var pulvinar = MeanPositive(values, static item => item.PulvinarPriority);
            var trn = MeanPositive(values, static item => item.TrnSuppression);
            var relay = MeanPositive(values, static item => item.ThalamicRelay);
            var mediodorsal = MeanPositive(values, static item => item.MediodorsalSupport);
            var pfc = MeanPositive(values, static item => item.PfcMaintenance);
            var broadcast = MeanPositive(values, static item => item.IntralaminarBroadcast);
            var score = Math.Clamp(
                (sensory * 0.28) +
                (pulvinar * 0.24) +
                (relay * 0.20) +
                (mediodorsal * 0.12) +
                (pfc * 0.18) +
                (broadcast * 0.10) -
                (trn * 0.62),
                0.0,
                1.0);
            scores[channel] = score;
            activities[channel] = new AttentionWorkspaceChannelActivity(
                channel,
                sensory,
                pulvinar,
                trn,
                relay,
                mediodorsal,
                pfc,
                broadcast,
                (float)score);
        }

        var ranked = Enumerable.Range(0, ChannelCount)
            .OrderByDescending(channel => scores[channel])
            .ThenBy(static channel => channel)
            .ToArray();
        var selected = ranked[0];
        var margin = Math.Max(0.0, scores[selected] - scores[ranked[1]]);
        var required = new[]
        {
            StructureId.Pulvinar,
            StructureId.Thalamus,
            StructureId.Trn,
            StructureId.Pfc
        };
        var present = diagnostics.Select(static item => item.SourceStructure).ToHashSet();
        var coverage = required.Count(present.Contains) / (double)required.Length;
        var active = coverage >= 0.75 && scores[selected] > 0.005 && margin > 0.00001;
        var maintained = present.Contains(StructureId.Pfc)
            ? ranked.Where(channel => activities[channel].PfcMaintenance > 0.005f).Take(4).ToArray()
            : [];
        var broadcastActive = active &&
            present.Contains(StructureId.IntralaminarThalamus) &&
            activities[selected].IntralaminarBroadcast > 0.002f;
        var suppression = activities
            .Where(item => item.ChannelIndex != selected)
            .Select(static item => item.TrnSuppression)
            .DefaultIfEmpty(0f)
            .Average();

        return new NeuronalAttentionWorkspaceDecision(
            true,
            active,
            active ? selected : -1,
            margin,
            scores,
            maintained,
            maintained.Length,
            broadcastActive,
            broadcastActive ? selected : -1,
            suppression,
            coverage,
            activities);
    }

    public static BiologicalAttentionRuntime ApplyAuthority(
        long tick,
        BiologicalAttentionRuntime legacy,
        BiologicalAttentionRuntime previous,
        NeuronalAttentionWorkspaceDecision decision)
    {
        if (!decision.Available)
        {
            return legacy;
        }

        var scores = decision.ChannelScores.Count == ChannelCount
            ? decision.ChannelScores.Select(static value => Math.Max(0.0, value)).ToArray()
            : new double[ChannelCount];
        var total = scores.Sum();
        var weights = total > 0.000001
            ? scores.Select(value => (float)(value / total)).ToArray()
            : new float[ChannelCount];
        var selectedLabel = decision.Active
            ? LabelFor(decision.SelectedChannel)
            : "none";
        var switched = !string.Equals(previous.DominantChannel, selectedLabel, StringComparison.OrdinalIgnoreCase);
        var relay = decision.Channels.Count == ChannelCount
            ? decision.Channels.Max(static channel => channel.ThalamicRelay)
            : 0f;
        var trn = decision.Channels.Count == ChannelCount
            ? decision.Channels.Max(static channel => channel.TrnSuppression)
            : 0f;
        var sensoryTotal = weights[0] + weights[1] + weights[2] + weights[3];
        var sensoryBias = sensoryTotal > 0.000001f
            ? new AttentionVector(
                weights[0] / sensoryTotal,
                weights[1] / sensoryTotal,
                weights[2] / sensoryTotal,
                weights[3] / sensoryTotal)
            : previous.SensoryBias;

        return BiologicalAttentionRuntime.Normalize(legacy with
        {
            Visual = weights[0],
            Auditory = weights[1],
            Somatosensory = weights[2],
            Interoceptive = weights[3],
            Memory = weights[4],
            Language = weights[5],
            Motor = weights[6],
            DominantChannel = selectedLabel,
            FocusConfidence = decision.Active ? (float)Math.Clamp(decision.SelectionMargin * 4.0, 0.0, 1.0) : 0f,
            ThalamicRelayGain = relay,
            TrnInhibition = trn,
            SensoryBias = sensoryBias,
            LastSwitchTick = switched ? tick : previous.LastSwitchTick,
            HoldTicksRemaining = decision.Active && decision.MaintainedChannels.Contains(decision.SelectedChannel) ? 12 : 0
        });
    }

    private static string LabelFor(int channel)
        => channel >= 0 && channel < ChannelLabels.Length ? ChannelLabels[channel] : "none";

    private static float MeanPositive<T>(IReadOnlyList<T> values, Func<T, float> selector)
    {
        var positive = values.Select(selector).Where(static value => value > 0f).ToArray();
        return positive.Length == 0 ? 0f : (float)positive.Average();
    }

    private static float Max<T>(IReadOnlyList<T> values, Func<T, float> selector)
        => values.Count == 0 ? 0f : values.Max(selector);
}
