using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class AvatarWallSensingTests
{
    [Fact]
    public void ProximityFromRay_UsesBodyClearanceAndProbeRange()
    {
        Assert.Equal(1.0, AvatarWallSensing.ProximityFromRay(true, 0.30, 0.30, 1.70), 6);
        Assert.Equal(0.5, AvatarWallSensing.ProximityFromRay(true, 1.15, 0.30, 1.70), 6);
        Assert.Equal(0.0, AvatarWallSensing.ProximityFromRay(true, 2.00, 0.30, 1.70), 6);
        Assert.Equal(0.0, AvatarWallSensing.ProximityFromRay(false, 0.30, 0.30, 1.70), 6);
    }

    [Theory]
    [InlineData(0.90, 0.10, 0.0, "R")]
    [InlineData(0.10, 0.90, 0.0, "L")]
    [InlineData(0.50, 0.50, 12.0, "R")]
    [InlineData(0.50, 0.50, -12.0, "L")]
    public void ResolveEscapeHemisphere_TurnsAwayFromCloserWall(
        double left,
        double right,
        double turnRate,
        string expected)
    {
        Assert.Equal(expected, AvatarWallSensing.ResolveEscapeHemisphere(null, left, right, turnRate));
    }

    [Fact]
    public void ResolveEscapeHemisphere_PreservesContactEpisodeLatch()
    {
        Assert.Equal("R", AvatarWallSensing.ResolveEscapeHemisphere("R", 0.05, 0.95, -80.0));
        Assert.Equal("L", AvatarWallSensing.ResolveEscapeHemisphere("L", 0.95, 0.05, 80.0));
    }
}
