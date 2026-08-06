using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

internal sealed record NeuronalActionDecision(
    bool Available,
    bool Active,
    int SelectedChannel,
    double SelectionScore,
    double SelectionMargin,
    double Confidence,
    double CircuitCoverage,
    double OutputInhibition,
    IReadOnlyList<double> ChannelScores)
{
    public static NeuronalActionDecision Unavailable { get; } = new(
        false,
        false,
        -1,
        0.0,
        0.0,
        0.0,
        0.0,
        1.0,
        [0.0, 0.0, 0.0, 0.0]);
}

internal static class NeuronalActionSelectionDecoder
{
    private const int ChannelCount = 4;

    public static NeuronalActionDecision Decode(IReadOnlyList<InstanceStructureSnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        var actionSnapshots = snapshots
            .Where(static snapshot => snapshot.ActionSelectionDiagnostics is not null)
            .ToArray();
        if (actionSnapshots.Length == 0)
        {
            return NeuronalActionDecision.Unavailable;
        }

        var observed = actionSnapshots
            .Select(static snapshot => snapshot.StructureId)
            .ToHashSet();
        var proposalCoverage = new[]
        {
            StructureId.Pfc,
            StructureId.Acc,
            StructureId.PremotorCortex,
            StructureId.Sma
        }.Count(observed.Contains) / 4.0;
        var striatalCoverage = observed.Contains(StructureId.Striatum) ? 1.0 : 0.0;
        var hyperdirectCoverage = observed.Contains(StructureId.Stn) ? 1.0 : 0.0;
        var outputCoverage = observed.Contains(StructureId.GPi) || observed.Contains(StructureId.Snr) ? 1.0 : 0.0;
        var thalamicCoverage = observed.Contains(StructureId.MotorThalamus) ? 1.0 : 0.0;
        var coverage = (proposalCoverage + striatalCoverage + hyperdirectCoverage + outputCoverage + thalamicCoverage) / 5.0;

        var scores = new double[ChannelCount];
        var outputs = new double[ChannelCount];
        var thalamic = new double[ChannelCount];
        for (var channel = 0; channel < ChannelCount; channel++)
        {
            var pfc = AverageChannel(actionSnapshots, StructureId.Pfc, channel);
            var acc = AverageChannel(actionSnapshots, StructureId.Acc, channel);
            var premotor = AverageChannel(actionSnapshots, StructureId.PremotorCortex, channel);
            var sma = AverageChannel(actionSnapshots, StructureId.Sma, channel);
            var striatum = AverageChannel(actionSnapshots, StructureId.Striatum, channel);
            var stn = AverageChannel(actionSnapshots, StructureId.Stn, channel);
            var gpi = AverageChannel(actionSnapshots, StructureId.GPi, channel);
            var snr = AverageChannel(actionSnapshots, StructureId.Snr, channel);
            var relay = AverageChannel(actionSnapshots, StructureId.MotorThalamus, channel);

            var proposal = WeightedProposal(
                (pfc, 0.30),
                (acc, 0.10),
                (premotor, 0.35),
                (sma, 0.25));
            var output = Math.Max(gpi.OutputNucleusInhibition, snr.OutputNucleusInhibition);
            var learned = Math.Clamp(striatum.LearnedSynapticStrength / 5.0, 0.0, 1.0);
            var eligibility = Math.Clamp(striatum.EligibilityTrace, -1.0f, 1.0f);
            scores[channel] =
                (proposal * 0.30) +
                (striatum.DirectPathwayActivation * 0.32) +
                (relay.ThalamicRelayActivation * 0.18) +
                (learned * 0.08) +
                (Math.Max(0.0, eligibility) * 0.04) -
                (striatum.IndirectPathwayActivation * 0.20) -
                (stn.HyperdirectSuppression * 0.28) -
                (output * 0.42);
            outputs[channel] = output;
            thalamic[channel] = relay.ThalamicRelayActivation;
        }

        var ranked = Enumerable.Range(0, ChannelCount)
            .OrderByDescending(channel => scores[channel])
            .ThenBy(static channel => channel)
            .ToArray();
        var selected = ranked[0];
        var margin = Math.Max(0.0, scores[selected] - scores[ranked[1]]);
        var scoreStrength = Math.Clamp((scores[selected] + 0.25) / 0.75, 0.0, 1.0);
        var confidence = Math.Clamp(
            (coverage * 0.42) +
            (Math.Clamp(margin * 4.0, 0.0, 1.0) * 0.28) +
            (scoreStrength * 0.18) +
            (thalamic[selected] * 0.12),
            0.0,
            1.0);
        var active = coverage >= 0.60 &&
            scores[selected] > 0.01 &&
            margin > 0.0025 &&
            outputs[selected] < 0.90;

        return new NeuronalActionDecision(
            Available: true,
            Active: active,
            SelectedChannel: active ? selected : -1,
            SelectionScore: scores[selected],
            SelectionMargin: margin,
            Confidence: confidence,
            CircuitCoverage: coverage,
            OutputInhibition: outputs[selected],
            ChannelScores: scores);
    }

    public static (double Left, double Right) ShapeMotorPopulation(
        NeuronalActionDecision decision,
        double left,
        double right)
    {
        ArgumentNullException.ThrowIfNull(decision);
        if (!decision.Available)
        {
            return (left, right);
        }

        if (!decision.Active)
        {
            return (0.0, 0.0);
        }

        var magnitude = Math.Max(Math.Abs(left), Math.Abs(right));
        return decision.SelectedChannel switch
        {
            0 => (left, right),
            1 => (left * 0.18, right),
            2 => (left, right * 0.18),
            3 => (-magnitude, -magnitude),
            _ => (0.0, 0.0)
        };
    }

    private static ActionChannelActivity AverageChannel(
        IReadOnlyList<InstanceStructureSnapshot> snapshots,
        StructureId structure,
        int channel)
    {
        var values = snapshots
            .Where(snapshot => snapshot.StructureId == structure)
            .SelectMany(static snapshot => snapshot.ActionSelectionDiagnostics!.Channels)
            .Where(item => item.ChannelIndex == channel)
            .ToArray();
        if (values.Length == 0)
        {
            return EmptyChannel(channel);
        }

        return new ActionChannelActivity(
            channel,
            (float)values.Average(static item => item.ProposalDrive),
            (float)values.Average(static item => item.DirectPathwayActivation),
            (float)values.Average(static item => item.IndirectPathwayActivation),
            (float)values.Average(static item => item.HyperdirectSuppression),
            (float)values.Average(static item => item.OutputNucleusInhibition),
            (float)values.Average(static item => item.ThalamicRelayActivation),
            (float)values.Average(static item => item.EligibilityTrace),
            (float)values.Average(static item => item.LearnedSynapticStrength),
            (float)values.Average(static item => item.SelectionScore));
    }

    private static double WeightedProposal(
        params (ActionChannelActivity Channel, double Weight)[] values)
    {
        var total = 0.0;
        foreach (var value in values)
        {
            total += value.Channel.ProposalDrive * value.Weight;
        }

        return total;
    }

    private static ActionChannelActivity EmptyChannel(int channel)
        => new(channel, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f);
}
