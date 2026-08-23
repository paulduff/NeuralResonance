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
    double FunctionalCoverage,
    double OutputInhibition,
    IReadOnlyList<double> ChannelScores,
    IReadOnlyList<ActionAuthorityChannelTrace> ChannelTraces,
    string AuthorityReason)
{
    public static NeuronalActionDecision Unavailable { get; } = new(
        false,
        false,
        -1,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        1.0,
        new double[NeuronalActionSelectionDecoder.ChannelCount],
        [],
        "No action-selection diagnostics were observed.");
}

internal static class NeuronalActionSelectionDecoder
{
    internal const int ChannelCount = 38;
    internal const int ForwardChannel = 0;
    internal const int LeftTurnChannel = 1;
    internal const int RightTurnChannel = 2;
    internal const int ReverseChannel = 3;
    internal const int LeftShoulderFlexionChannel = 4;
    internal const int LeftShoulderExtensionChannel = 5;
    internal const int RightShoulderFlexionChannel = 6;
    internal const int RightShoulderExtensionChannel = 7;
    internal const int LeftShoulderAbductionChannel = 8;
    internal const int LeftShoulderAdductionChannel = 9;
    internal const int RightShoulderAbductionChannel = 10;
    internal const int RightShoulderAdductionChannel = 11;
    internal const int LeftElbowFlexionChannel = 12;
    internal const int LeftElbowExtensionChannel = 13;
    internal const int RightElbowFlexionChannel = 14;
    internal const int RightElbowExtensionChannel = 15;
    internal const int StandChannel = 16;
    internal const int CrouchChannel = 17;
    internal const int SitChannel = 18;
    internal const int LieChannel = 19;
    internal const int LeftHipAbductionChannel = 20;
    internal const int LeftHipAdductionChannel = 21;
    internal const int RightHipAbductionChannel = 22;
    internal const int RightHipAdductionChannel = 23;
    internal const int LeftAnkleDorsiflexionChannel = 24;
    internal const int LeftAnklePlantarflexionChannel = 25;
    internal const int RightAnkleDorsiflexionChannel = 26;
    internal const int RightAnklePlantarflexionChannel = 27;
    internal const int LeftAnkleInversionChannel = 28;
    internal const int LeftAnkleEversionChannel = 29;
    internal const int RightAnkleInversionChannel = 30;
    internal const int RightAnkleEversionChannel = 31;
    internal const int TrunkRotateLeftChannel = 32;
    internal const int TrunkRotateRightChannel = 33;
    internal const int LeftHandCloseChannel = 34;
    internal const int LeftHandOpenChannel = 35;
    internal const int RightHandCloseChannel = 36;
    internal const int RightHandOpenChannel = 37;

    public static NeuronalActionDecision Decode(
        IReadOnlyList<InstanceStructureSnapshot> snapshots,
        int preferredChannel = -1,
        double persistenceBias = 0.0,
        int inhibitedChannel = -1,
        double inhibition = 0.0)
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
        var proposals = new double[ChannelCount];
        var direct = new double[ChannelCount];
        var indirect = new double[ChannelCount];
        var hyperdirect = new double[ChannelCount];
        var eligibility = new double[ChannelCount];
        var learnedStrength = new double[ChannelCount];
        var persistenceByChannel = new double[ChannelCount];
        var inhibitionByChannel = new double[ChannelCount];
        var functional = new bool[ChannelCount];
        var functionalCoverageByChannel = new double[ChannelCount];
        var striatalChannels = new ActionChannelActivity[ChannelCount];
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
            striatalChannels[channel] = striatum;

            var proposal = WeightedProposal(
                (pfc, 0.30),
                (acc, 0.10),
                (premotor, 0.35),
                (sma, 0.25));
            var output = Math.Max(gpi.OutputNucleusInhibition, snr.OutputNucleusInhibition);
            var learned = Math.Clamp(striatum.LearnedSynapticStrength / 5.0, 0.0, 1.0);
            var trace = Math.Clamp(striatum.EligibilityTrace, -1.0f, 1.0f);
            scores[channel] =
                (proposal * 0.30) +
                (striatum.DirectPathwayActivation * 0.32) +
                (relay.ThalamicRelayActivation * 0.18) +
                (learned * 0.08) +
                (striatum.IndirectPathwayActivation * 0.20) -
                (stn.HyperdirectSuppression * 0.28) -
                (output * 0.42);
            outputs[channel] = output;
            thalamic[channel] = relay.ThalamicRelayActivation;
            proposals[channel] = proposal;
            direct[channel] = striatum.DirectPathwayActivation;
            indirect[channel] = striatum.IndirectPathwayActivation;
            hyperdirect[channel] = stn.HyperdirectSuppression;
            eligibility[channel] = trace;
            learnedStrength[channel] = striatum.LearnedSynapticStrength;

            var functionalProposal = proposal >= 0.02;
            var functionalStriatum = striatum.DirectPathwayActivation >= 0.01 &&
                striatum.IndirectPathwayActivation >= 0.01 &&
                striatum.DirectPathwayActivation > striatum.IndirectPathwayActivation + 0.005;
            var functionalOutput = output >= 0.005 && output < 0.90;
            var functionalRelay = relay.ThalamicRelayActivation >= 0.01;
            functionalCoverageByChannel[channel] =
                (Convert.ToInt32(functionalProposal) +
                 Convert.ToInt32(functionalStriatum) +
                 Convert.ToInt32(functionalOutput) +
                 Convert.ToInt32(functionalRelay)) / 4.0;
            functional[channel] = functionalProposal &&
                functionalStriatum &&
                functionalOutput &&
                functionalRelay;
        }

        if (preferredChannel >= 0 && preferredChannel < ChannelCount && persistenceBias > 0.0)
        {
            // A recently selected cortico-basal-ganglia loop remains slightly
            // facilitated. This resolves near-ties without overriding a newly
            // dominant channel or creating host-authored behavioural policy.
            var boundedPersistence = Math.Clamp(persistenceBias, 0.0, 0.25);
            if (functional[preferredChannel])
            {
                persistenceByChannel[preferredChannel] = boundedPersistence;
                scores[preferredChannel] += boundedPersistence;
            }
        }

        if (inhibitedChannel >= 0 && inhibitedChannel < ChannelCount && inhibition > 0.0)
        {
            // Aversive neural evidence can inhibit the action population that was
            // temporally responsible for a costly outcome. It supplies no escape
            // direction; another neuronal population must still win on its own.
            var boundedInhibition = Math.Clamp(inhibition, 0.0, 1.0);
            inhibitionByChannel[inhibitedChannel] = boundedInhibition;
            scores[inhibitedChannel] -= boundedInhibition;
        }

        var rankedAll = Enumerable.Range(0, ChannelCount)
            .OrderByDescending(channel => scores[channel])
            .ThenBy(static channel => channel)
            .ToArray();
        var rankedFunctional = rankedAll.Where(channel => functional[channel]).ToArray();
        var ranked = rankedFunctional.Length > 0 ? rankedFunctional : rankedAll;
        var selected = ranked[0];
        var runnerUp = rankedAll.FirstOrDefault(channel => channel != selected, selected);
        var margin = Math.Max(0.0, scores[selected] - scores[runnerUp]);
        var scoreStrength = Math.Clamp((scores[selected] + 0.25) / 0.75, 0.0, 1.0);
        var confidence = Math.Clamp(
            (coverage * 0.42) +
            (Math.Clamp(margin * 4.0, 0.0, 1.0) * 0.28) +
            (scoreStrength * 0.18) +
            (thalamic[selected] * 0.12),
            0.0,
            1.0);
        var active = coverage >= 0.60 &&
            functional[selected] &&
            scores[selected] > 0.01 &&
            margin > 0.0025;
        var authorityReason = ExplainAuthority(
            active,
            coverage,
            proposals[selected],
            direct[selected],
            indirect[selected],
            outputs[selected],
            thalamic[selected],
            scores[selected],
            margin);
        var traces = Enumerable.Range(0, ChannelCount)
            .Select(channel => new ActionAuthorityChannelTrace(
                channel,
                (float)proposals[channel],
                (float)direct[channel],
                (float)indirect[channel],
                (float)hyperdirect[channel],
                (float)outputs[channel],
                (float)thalamic[channel],
                (float)eligibility[channel],
                (float)learnedStrength[channel],
                (float)scores[channel],
                (float)persistenceByChannel[channel],
                (float)inhibitionByChannel[channel],
                proposals[channel] >= 0.02,
                direct[channel] >= 0.01 && indirect[channel] >= 0.01 &&
                    direct[channel] > indirect[channel] + 0.005,
                outputs[channel] >= 0.005 && outputs[channel] < 0.90,
                thalamic[channel] >= 0.01,
                channel == selected,
                active && channel == selected,
                channel == selected
                    ? authorityReason
                    : functional[channel]
                        ? "Functional competitor was not the winning channel."
                        : ExplainFunctionalDeficit(
                            proposals[channel],
                            direct[channel],
                            indirect[channel],
                            outputs[channel],
                            thalamic[channel]),
                striatalChannels[channel].DirectMeanMembraneMillivolts,
                striatalChannels[channel].IndirectMeanMembraneMillivolts,
                striatalChannels[channel].DirectMeanSynapticCurrent,
                striatalChannels[channel].IndirectMeanSynapticCurrent,
                striatalChannels[channel].DirectActiveNeurons,
                striatalChannels[channel].IndirectActiveNeurons,
                striatalChannels[channel].DirectMeanUpState,
                striatalChannels[channel].IndirectMeanUpState))
            .ToArray();

        return new NeuronalActionDecision(
            Available: true,
            Active: active,
            SelectedChannel: active ? selected : -1,
            SelectionScore: scores[selected],
            SelectionMargin: margin,
            Confidence: confidence,
            CircuitCoverage: coverage,
            FunctionalCoverage: functionalCoverageByChannel[selected],
            OutputInhibition: outputs[selected],
            ChannelScores: scores,
            ChannelTraces: traces,
            AuthorityReason: authorityReason);
    }

    public static ActionAuthorityTrace ToTrace(NeuronalActionDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);
        return new ActionAuthorityTrace(
            decision.Available,
            decision.Active,
            decision.SelectedChannel,
            (float)decision.SelectionScore,
            (float)decision.SelectionMargin,
            (float)decision.CircuitCoverage,
            (float)decision.FunctionalCoverage,
            decision.AuthorityReason,
            decision.ChannelTraces);
    }

    public static (double Left, double Right) ShapeMotorPopulation(
        NeuronalActionDecision decision,
        double left,
        double right)
    {
        ArgumentNullException.ThrowIfNull(decision);
        if (!decision.Available)
        {
            return (0.0, 0.0);
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
            (float)values.Average(static item => item.SelectionScore),
            (float)values.Average(static item => item.ReflexDrive),
            (float)values.Average(static item => item.DirectMeanMembraneMillivolts),
            (float)values.Average(static item => item.IndirectMeanMembraneMillivolts),
            (float)values.Average(static item => item.DirectMeanSynapticCurrent),
            (float)values.Average(static item => item.IndirectMeanSynapticCurrent),
            (int)Math.Round(values.Average(static item => item.DirectActiveNeurons)),
            (int)Math.Round(values.Average(static item => item.IndirectActiveNeurons)),
            (float)values.Average(static item => item.DirectMeanUpState),
            (float)values.Average(static item => item.IndirectMeanUpState));
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

    private static string ExplainAuthority(
        bool active,
        double coverage,
        double proposal,
        double direct,
        double indirect,
        double output,
        double relay,
        double score,
        double margin)
    {
        if (active)
        {
            return "Authority granted: cortical proposal, D1/D2 competition, output-nucleus disinhibition, and motor-thalamic relay were all functional.";
        }

        if (coverage < 0.60)
        {
            return $"Authority denied: structural circuit coverage was {coverage:0.000}.";
        }

        var deficit = ExplainFunctionalDeficit(proposal, direct, indirect, output, relay);
        if (!string.IsNullOrEmpty(deficit))
        {
            return $"Authority denied: {deficit}";
        }

        if (score <= 0.01)
        {
            return $"Authority denied: winning score {score:0.0000} did not exceed 0.0100.";
        }

        return $"Authority denied: selection margin {margin:0.0000} did not exceed 0.0025.";
    }

    private static string ExplainFunctionalDeficit(
        double proposal,
        double direct,
        double indirect,
        double output,
        double relay)
    {
        var reasons = new List<string>(4);
        if (proposal < 0.02)
        {
            reasons.Add("no channel-specific cortical proposal");
        }
        if (direct < 0.01 || indirect < 0.01)
        {
            reasons.Add("silent D1/D2 striatal competition");
        }
        else if (direct <= indirect + 0.005)
        {
            reasons.Add("indirect pathway prevented D1 selection");
        }
        if (output < 0.005 || output >= 0.90)
        {
            reasons.Add(output < 0.005
                ? "output-nucleus activity was absent"
                : "output nucleus remained inhibitory");
        }
        if (relay < 0.01)
        {
            reasons.Add("motor-thalamic relay was silent");
        }

        return string.Join(", ", reasons);
    }
}
