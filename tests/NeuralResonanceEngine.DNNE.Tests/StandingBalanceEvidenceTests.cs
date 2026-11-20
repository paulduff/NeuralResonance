using Nre.UnityWorld;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class StandingBalanceEvidenceTests
{
    static StandingBalanceObservation Supported() => new() {
        supportMarginMeters = .04, leftFootLoadNewtons = 350, rightFootLoadNewtons = 350 };

    [Fact]
    public void OneStepExcursionIsRetainedBetweenCoarseObservationTimes()
    {
        var evidence = new StandingBalanceEvidence();
        evidence.Begin(80, .01, Supported());
        for (int i = 1; i <= 20; i++)
        {
            var sample = Supported();
            if (i == 7) { sample.supportMarginMeters = -.002; sample.balanceError = .68; }
            evidence.Observe(80 + i, sample);
        }
        Assert.Equal(.07, evidence.firstMarginExitSeconds, 12);
        Assert.Equal(.07, evidence.firstNegativeMarginSeconds, 12);
        Assert.Equal(1, evidence.marginExitEvents);
        Assert.Equal(.68, evidence.maximumBalanceError);
        Assert.Equal(20, evidence.completedSteps);
        Assert.Equal(100, evidence.lastPhysicsStep);
        Assert.Equal(-1, evidence.firstFallenSeconds);
    }

    [Fact]
    public void UnsupportedInitialPoseIsKeptSeparateFromLaterSupportLoss()
    {
        var evidence = new StandingBalanceEvidence();
        var unsupported = new StandingBalanceObservation { supportMarginMeters = -.1, balanceError = .25 };
        evidence.Begin(0, .01, unsupported);
        evidence.Observe(1, unsupported);
        Assert.False(evidence.initialFeetLoaded);
        Assert.True(evidence.initialMarginNegative);
        Assert.Equal(0, evidence.firstNegativeMarginSeconds);
        Assert.Equal(-1, evidence.firstFootSupportLossSeconds);
        Assert.Equal(0, evidence.marginExitEvents);
        evidence.Observe(2, Supported());
        evidence.Observe(3, unsupported);
        Assert.Equal(.03, evidence.firstFootSupportLossSeconds, 12);
        Assert.Equal(1, evidence.footSupportLossEvents);
        Assert.Equal(1, evidence.marginExitEvents);
    }

    [Fact]
    public void WeightTransferAndBoundaryTouchDoNotInventSupportLoss()
    {
        var evidence = new StandingBalanceEvidence();
        evidence.Begin(0, .01, Supported());
        var leftOnly = Supported(); leftOnly.rightFootLoadNewtons = 0; leftOnly.supportMarginMeters = 0;
        evidence.Observe(1, leftOnly);
        var rightOnly = Supported(); rightOnly.leftFootLoadNewtons = 0;
        evidence.Observe(2, rightOnly);
        Assert.Equal(0, evidence.footSupportLossEvents);
        Assert.Equal(0, evidence.marginExitEvents);
        Assert.Equal(-1, evidence.firstNegativeMarginSeconds);
    }

    [Fact]
    public void TiltErrorIsVisibleEvenWhileCenterOfMassRemainsSupported()
    {
        var evidence = new StandingBalanceEvidence();
        evidence.Begin(0, .01, Supported());
        var tilted = Supported(); tilted.pitchRadians = -.4; tilted.rollRadians = .15;
        tilted.balanceError = BodySensorMath.PhysicalBalanceError(.04f, -.4f, .15f, "stable");
        evidence.Observe(1, tilted);
        Assert.True(evidence.maximumBalanceError > 0);
        Assert.Equal(.4, evidence.maximumAbsPitchRadians);
        Assert.Equal(.15, evidence.maximumAbsRollRadians);
        Assert.Equal(-1, evidence.firstMarginExitSeconds);
        Assert.Equal(-1, evidence.firstFallenSeconds);
    }

    [Fact]
    public void RepeatedFallsKeepFirstTimeAndCountEachDistinctSupportExit()
    {
        var evidence = new StandingBalanceEvidence();
        evidence.Begin(0, .01, Supported());
        var fallen = new StandingBalanceObservation { fallen = true, supportMarginMeters = -.1, balanceError = 1 };
        evidence.Observe(1, fallen); evidence.Observe(2, fallen);
        evidence.Observe(3, Supported()); evidence.Observe(4, fallen);
        Assert.Equal(.01, evidence.firstFallenSeconds);
        Assert.Equal(.01, evidence.firstFootSupportLossSeconds);
        Assert.Equal(2, evidence.marginExitEvents);
        Assert.Equal(2, evidence.footSupportLossEvents);
    }

    [Fact]
    public void MissingDuplicatedOrReorderedEndpointsCannotClaimCompleteEvidence()
    {
        var evidence = new StandingBalanceEvidence();
        Assert.Throws<InvalidOperationException>(() => evidence.Observe(1, Supported()));
        evidence.Begin(7, .01, Supported());
        foreach (long invalid in new long[] { 6, 7, 9 })
            Assert.Throws<ArgumentException>(() => evidence.Observe(invalid, Supported()));
        Assert.Equal(0, evidence.completedSteps);
        Assert.Equal(7, evidence.lastPhysicsStep);
        evidence.Observe(8, Supported());
        Assert.Throws<ArgumentException>(() => evidence.Observe(8, Supported()));
        Assert.Throws<InvalidOperationException>(() => evidence.Begin(8, .01, Supported()));
    }

    [Fact]
    public void FailedMeasurementDoesNotConsumeThePhysicalEndpoint()
    {
        var evidence = new StandingBalanceEvidence(); evidence.Begin(0, .01, Supported());
        var invalid = Supported(); invalid.pitchRadians = double.NaN;
        Assert.Throws<ArgumentException>(() => evidence.Observe(1, invalid));
        Assert.Equal(0, evidence.completedSteps);
        Assert.Equal(0, evidence.lastPhysicsStep);
        Assert.Equal(0, evidence.maximumBalanceError);
        evidence.Observe(1, Supported());
        Assert.Equal(1, evidence.completedSteps);
    }

    [Fact]
    public void AlreadyFallenBaselineIsReportedAtZeroWithoutInventingASupportTransition()
    {
        var evidence = new StandingBalanceEvidence();
        evidence.Begin(30, .01, new StandingBalanceObservation { fallen = true, balanceError = 1, supportMarginMeters = -.1 });
        evidence.Observe(31, Supported());
        Assert.True(evidence.initialFallen);
        Assert.Equal(0, evidence.firstFallenSeconds);
        Assert.Equal(0, evidence.footSupportLossEvents);
        Assert.Equal(0, evidence.marginExitEvents);
    }

    [Theory]
    [InlineData(double.NaN, 350, 0)]
    [InlineData(.01, double.PositiveInfinity, 0)]
    [InlineData(.01, -1, 0)]
    [InlineData(.01, 350, 1.01)]
    public void InvalidPhysicalInputsAreRejectedWithoutInventingDefaults(double seconds, double load, double error)
    {
        var evidence = new StandingBalanceEvidence(); var sample = Supported();
        sample.leftFootLoadNewtons = load; sample.balanceError = error;
        Assert.Throws<ArgumentException>(() => evidence.Begin(0, seconds, sample));
        Assert.Equal(-1, evidence.initialPhysicsStep);
        Assert.Equal(0, evidence.completedSteps);
    }
}
