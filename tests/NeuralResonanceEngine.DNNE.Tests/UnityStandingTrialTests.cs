using Nre.UnityWorld;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class UnityStandingTrialTests
{
    static StandingObservation Supported(long tick, bool actuation = true) => new() {
        finite = true, authorizedMotor = true, legActuationApplied = actuation, motorTick = tick,
        uprightFraction = .95, pelvisHeight = 1, initialPelvisHeight = 1.1,
        leftFootLoad = 360, rightFootLoad = 360, bodyWeight = 720,
        otherSupportLoad = 0, supportMargin = .02, limitViolation = .001, standDrive = actuation ? .2 : 0 };

    [Fact]
    public void QuietSupportAloneDoesNotEstablishStandingUnderNeuralControl()
    {
        var metrics = new StandingTrialMetrics();
        for (int i = 0; i < 12; i++) metrics.Observe(1, Supported(i, actuation: false));
        Assert.Equal(12, metrics.standingSeconds);
        Assert.False(metrics.StandingCriterionMet);
        Assert.False(metrics.WeightTransferObserved);
    }

    [Fact]
    public void StandingRequiresTenContinuousFinalSecondsAndFreshNeuralInfluence()
    {
        var metrics = new StandingTrialMetrics();
        for (int i = 0; i < 12; i++) metrics.Observe(1, Supported(i));
        Assert.True(metrics.StandingCriterionMet);
        var fallen = Supported(13); fallen.uprightFraction = .1; fallen.pelvisHeight = .2;
        metrics.Observe(.1, fallen);
        Assert.Equal(12, metrics.longestStandingSeconds);
        Assert.Equal(0, metrics.currentStandingSeconds);
        Assert.False(metrics.StandingCriterionMet);
        Assert.Equal(1, metrics.fallingSamples);
    }

    [Fact]
    public void HandSupportOrJointLimitStrikesCannotQualifyBipedalStanding()
    {
        var metrics = new StandingTrialMetrics();
        for (int i = 0; i < 12; i++) {
            var sample = Supported(i); sample.otherSupportLoad = 100;
            metrics.Observe(1, sample);
        }
        Assert.Equal(0, metrics.standingSeconds);
        var strike = Supported(20); strike.limitViolation = .06;
        metrics.Observe(.1, strike);
        for (int i = 0; i < 12; i++) metrics.Observe(1, Supported(21 + i));
        Assert.False(metrics.StandingCriterionMet);
    }

    [Fact]
    public void WeightTransferRequiresBothDominantFeetDuringSupportedStanding()
    {
        var metrics = new StandingTrialMetrics();
        var left = Supported(1); left.leftFootLoad = 480; left.rightFootLoad = 240;
        metrics.Observe(.6, left);
        Assert.False(metrics.WeightTransferObserved);
        var right = Supported(2); right.leftFootLoad = 240; right.rightFootLoad = 480;
        right.pelvisHeight = .1;
        metrics.Observe(.6, right);
        Assert.False(metrics.WeightTransferObserved);
        right.pelvisHeight = 1;
        metrics.Observe(.6, right);
        Assert.True(metrics.WeightTransferObserved);
        Assert.InRange(metrics.minimumLeftLoadShare, .33, .34);
        Assert.InRange(metrics.maximumLeftLoadShare, .66, .67);
    }

    [Fact]
    public void NonfiniteOrStaleCommandEvidenceDoesNotQualifyStanding()
    {
        var metrics = new StandingTrialMetrics();
        for (int i = 0; i < 12; i++) metrics.Observe(1, Supported(1));
        Assert.False(metrics.StandingCriterionMet);
        var invalid = Supported(2); invalid.supportMargin = double.NaN;
        metrics.Observe(.1, invalid);
        Assert.False(metrics.finite);
        Assert.False(metrics.StandingCriterionMet);
    }

    static StandingObservation Fallen(long tick) {
        var sample = Supported(tick); sample.uprightFraction = .1; sample.pelvisHeight = .2;
        return sample;
    }

    [Fact]
    public void RecoveryRequiresAFallThenTenContinuousSupportedSecondsWithFreshNeuralActuation()
    {
        var metrics = new StandingTrialMetrics();
        metrics.Observe(1, Fallen(0));
        metrics.Observe(1, Fallen(1));
        Assert.Equal(1, metrics.fallEvents);
        for (int i = 0; i < 9; i++) metrics.Observe(1, Supported(2 + i));
        Assert.False(metrics.RecoveryObserved);
        metrics.Observe(1, Supported(11));
        Assert.True(metrics.RecoveryObserved);
        Assert.True(metrics.RecoveryCriterionMet);
        Assert.Equal(1, metrics.recoveries);
        Assert.Equal(1, metrics.firstFallSeconds);
        Assert.Equal(12, metrics.firstRecoverySeconds);
        Assert.Equal(12, metrics.stepsWithLegActuationAfterFall);
        Assert.Equal(12, metrics.authorizedMotorTicksAfterFall);
        metrics.Observe(1, Fallen(12));
        Assert.True(metrics.RecoveryObserved);
        Assert.False(metrics.RecoveryCriterionMet);
        for (int i = 0; i < 10; i++) metrics.Observe(1, Supported(13 + i));
        Assert.Equal(2, metrics.recoveries);
        Assert.Equal(2, metrics.fallEvents);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void PassiveSupportOrStaleMotorAfterAFallCannotClaimRecovery(bool actuation, bool stale)
    {
        var metrics = new StandingTrialMetrics();
        for (int i = 0; i < 12; i++) metrics.Observe(1, Supported(i));
        Assert.False(metrics.RecoveryObserved); // Standing without a preceding fall is not recovery.
        metrics.Observe(1, Fallen(12));
        for (int i = 0; i < 12; i++) metrics.Observe(1, Supported(stale ? 13 : 13 + i, actuation));
        Assert.False(metrics.RecoveryObserved);
        Assert.Equal(0, metrics.recoveries);
    }

    [Fact]
    public void InterruptedSupportResetsRecoveryWindowButEarlierLimitStrikesDoNotHideLaterRecovery()
    {
        var metrics = new StandingTrialMetrics();
        var fall = Fallen(0); fall.limitViolation = .1;
        metrics.Observe(1, fall);
        for (int i = 0; i < 9; i++) metrics.Observe(1, Supported(1 + i));
        var handSupport = Supported(10); handSupport.otherSupportLoad = 100;
        metrics.Observe(1, handSupport);
        for (int i = 0; i < 9; i++) metrics.Observe(1, Supported(11 + i));
        Assert.False(metrics.RecoveryObserved);
        metrics.Observe(1, Supported(20));
        Assert.True(metrics.RecoveryCriterionMet);
        Assert.False(metrics.StandingCriterionMet); // Separate criterion retains whole-run limit bound.
        var invalid = Supported(21); invalid.pelvisHeight = double.NaN;
        metrics.Observe(.1, invalid);
        Assert.False(metrics.RecoveryObserved);
        Assert.False(metrics.RecoveryCriterionMet);
    }
}
