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
}
