using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class ActionAuthorityTelemetryAccumulatorTests
{
    [Fact]
    public void CapturesTransientAuthorityAndPerChannelRecruitmentPeaks()
    {
        var telemetry = new ActionAuthorityTelemetryAccumulator();
        telemetry.Observe(10, true, false, 2, [Trace(2, selected: true, granted: false, direct: 0.12f)]);
        telemetry.Observe(11, true, true, 2, [Trace(2, selected: true, granted: true, direct: 0.44f)]);
        telemetry.Observe(12, true, true, 2, [Trace(2, selected: true, granted: true, direct: 0.31f)]);
        telemetry.Observe(13, true, false, -1, [Trace(2, selected: false, granted: false, direct: 0.05f)]);
        telemetry.Observe(14, true, true, 2, [Trace(2, selected: true, granted: true, direct: 0.27f)]);

        var history = telemetry.Capture();
        Assert.Equal(5, history.Samples);
        Assert.Equal(5, history.CircuitObservedTicks);
        Assert.Equal(3, history.AuthorityGrantedTicks);
        Assert.Equal(2, history.AuthorityGrantEpisodes);
        Assert.Equal(11, history.FirstAuthorityGrantTick);
        Assert.Equal(14, history.LastAuthorityGrantTick);
        var channel = Assert.Single(history.Channels);
        Assert.Equal(4, channel.SelectedTicks);
        Assert.Equal(3, channel.AuthorityGrantedTicks);
        Assert.Equal(0.44f, channel.PeakDirectPathwayActivation);
        Assert.Equal(7, channel.PeakDirectActiveNeurons);
        Assert.Equal(0.72f, channel.PeakDirectMeanUpState);
        Assert.Equal(0.18f, channel.MinimumOutputNucleusInhibition);
    }

    [Fact]
    public void DuplicateTickDoesNotInflateHistoryAndResetStartsANewRun()
    {
        var telemetry = new ActionAuthorityTelemetryAccumulator();
        var trace = Trace(1, selected: true, granted: true, direct: 0.2f);
        telemetry.Observe(4, true, true, 1, [trace]);
        telemetry.Observe(4, true, true, 1, [trace]);

        Assert.Equal(1, telemetry.Capture().Samples);
        telemetry.Reset();
        var reset = telemetry.Capture();
        Assert.Equal(0, reset.Samples);
        Assert.Empty(reset.Channels);
    }

    [Fact]
    public void NegativeSelectionScoreIsPreservedRatherThanReportedAsFalseZero()
    {
        var telemetry = new ActionAuthorityTelemetryAccumulator();
        var trace = Trace(5, selected: true, granted: false, direct: 0.02f) with
        {
            SelectionScore = -0.12f
        };

        telemetry.Observe(1, true, false, -1, [trace]);

        Assert.Equal(-0.12f, Assert.Single(telemetry.Capture().Channels).PeakSelectionScore);
    }

    [Fact]
    public void RightingBlockedCandidateIsNotReportedAsBodyOutputAuthority()
    {
        var telemetry = new ActionAuthorityTelemetryAccumulator();
        var runtime = NeuronalMotorRuntime.Default with
        {
            Tick = 35,
            ActionCircuitObserved = true,
            SelectedActionChannel = -1,
            RightingLatchActive = true,
            ActionChannelTraces = [Trace(2, selected: true, granted: true, direct: 0.44f)]
        };

        telemetry.Observe(runtime);

        var history = telemetry.Capture();
        Assert.Equal(0, history.AuthorityGrantedTicks);
        var channel = Assert.Single(history.Channels);
        Assert.Equal(0, channel.SelectedTicks);
        Assert.Equal(0, channel.AuthorityGrantedTicks);
    }

    private static ActionAuthorityChannelTrace Trace(
        int channel,
        bool selected,
        bool granted,
        float direct)
        => new(
            channel,
            ProposalDrive: 0.35f,
            DirectPathwayActivation: direct,
            IndirectPathwayActivation: 0.16f,
            HyperdirectSuppression: 0.11f,
            OutputNucleusInhibition: 0.18f,
            ThalamicRelayActivation: 0.29f,
            EligibilityTrace: 0.08f,
            LearnedSynapticStrength: 1.1f,
            SelectionScore: 0.24f,
            PersistenceBias: 0f,
            AversiveInhibition: 0f,
            FunctionalProposal: true,
            FunctionalStriatalCompetition: true,
            FunctionalOutputNucleus: true,
            FunctionalThalamicRelay: true,
            Selected: selected,
            AuthorityGranted: granted,
            AuthorityReason: "test",
            DirectMeanMembraneMillivolts: -58f,
            IndirectMeanMembraneMillivolts: -61f,
            DirectMeanSynapticCurrent: 2.3f,
            IndirectMeanSynapticCurrent: 1.7f,
            DirectActiveNeurons: 7,
            IndirectActiveNeurons: 4,
            DirectMeanUpState: 0.72f,
            IndirectMeanUpState: 0.39f);
}
