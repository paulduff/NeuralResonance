using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class AvatarInputPressureGateTests
{
    [Fact]
    public void FailurePausesUntilBackoffExpires()
    {
        var gate = new AvatarInputPressureGate(baseDelayMs: 100, severeMinimumDelayMs: 400);

        var delay = gate.RegisterFailure(nowMs: 1000, "timeout posting vision", severe: false);

        Assert.Equal(100, delay);
        Assert.True(gate.ShouldPause(nowMs: 1050, out var reason));
        Assert.Contains("timeout posting vision", reason);
        Assert.False(gate.ShouldPause(nowMs: 1100, out _));
    }

    [Fact]
    public void SevereFailureUsesMinimumDelay()
    {
        var gate = new AvatarInputPressureGate(baseDelayMs: 100, severeMinimumDelayMs: 700);

        var delay = gate.RegisterFailure(nowMs: 2000, "HTTP 500", severe: true);

        Assert.Equal(700, delay);
        Assert.True(gate.ShouldPause(nowMs: 2600, out _));
        Assert.False(gate.ShouldPause(nowMs: 2700, out _));
    }

    [Fact]
    public void SuccessClearsExpiredPause()
    {
        var gate = new AvatarInputPressureGate(baseDelayMs: 100);
        gate.RegisterFailure(nowMs: 0, "timeout", severe: false);

        gate.RegisterSuccess(nowMs: 100);

        Assert.Equal(0, gate.FailureStreak);
        Assert.False(gate.ShouldPause(nowMs: 101, out _));
    }

    [Fact]
    public void CriticalAvatarInputThrottlesButDoesNotPauseAtModeratePressure()
    {
        var decision = AvatarInputPressurePolicy.Evaluate(
            engineInputPressure: 0.36,
            telemetryFailureStreak: 0,
            channelGatePaused: false,
            channelGateReason: string.Empty,
            priority: AvatarInputPriority.Critical,
            normalIntervalMs: 200);

        Assert.False(decision.ShouldPause);
        Assert.True(decision.ShouldThrottle);
        Assert.True(decision.MinimumIntervalMs >= 750);
        Assert.Contains("critical sensory throttle", decision.Reason);
    }

    [Fact]
    public void OptionalAvatarInputPausesAtModeratePressure()
    {
        var decision = AvatarInputPressurePolicy.Evaluate(
            engineInputPressure: 0.36,
            telemetryFailureStreak: 0,
            channelGatePaused: false,
            channelGateReason: string.Empty,
            priority: AvatarInputPriority.Optional,
            normalIntervalMs: 9000);

        Assert.True(decision.ShouldPause);
        Assert.False(decision.ShouldThrottle);
        Assert.Contains("engine input pressure 0.36", decision.Reason);
    }

    [Fact]
    public void CriticalAvatarInputKeepsLifelineAtSeverePressure()
    {
        var decision = AvatarInputPressurePolicy.Evaluate(
            engineInputPressure: 0.72,
            telemetryFailureStreak: 0,
            channelGatePaused: false,
            channelGateReason: string.Empty,
            priority: AvatarInputPriority.Critical,
            normalIntervalMs: 350);

        Assert.False(decision.ShouldPause);
        Assert.True(decision.ShouldThrottle);
        Assert.True(decision.MinimumIntervalMs >= 2100);
        Assert.Contains("critical sensory lifeline", decision.Reason);
    }

    [Fact]
    public void ChannelBackoffPausesOnlyTheChannelThatFailed()
    {
        var visionGate = new AvatarInputPressureGate(baseDelayMs: 100);
        var audioGate = new AvatarInputPressureGate(baseDelayMs: 100);

        visionGate.RegisterFailure(nowMs: 1000, "vision timeout", severe: false);

        Assert.True(visionGate.ShouldPause(nowMs: 1050, out _));
        Assert.False(audioGate.ShouldPause(nowMs: 1050, out _));
    }
}
