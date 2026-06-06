using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class AvatarWarningGateTests
{
    [Fact]
    public void ShouldLogSuppressesRepeatedKeyInsideIntervalEvenWhenMessageChanges()
    {
        var gate = new AvatarWarningGate(minimumIntervalMs: 1000);

        Assert.True(gate.ShouldLog("timeout (streak 1)", "vision:timeout", nowMs: 10));
        Assert.False(gate.ShouldLog("timeout (streak 2)", "vision:timeout", nowMs: 900));
        Assert.True(gate.ShouldLog("timeout (streak 3)", "vision:timeout", nowMs: 1200));
    }

    [Fact]
    public void ShouldLogKeepsLegacyMessageBasedDedupe()
    {
        var gate = new AvatarWarningGate(minimumIntervalMs: 1000);

        Assert.True(gate.ShouldLog("same warning", nowMs: 10));
        Assert.False(gate.ShouldLog("same warning", nowMs: 900));
        Assert.True(gate.ShouldLog("different warning", nowMs: 950));
    }
}
