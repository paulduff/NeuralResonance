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
}
