using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class AvatarPhysicalInteractionTests
{
    [Theory]
    [InlineData(0.0, 0.8, true)]
    [InlineData(0.8, 0.8, true)]
    [InlineData(0.0, -0.8, false)]
    [InlineData(2.0, 0.0, false)]
    public void EffectorReachUsesOnlyBodyGeometry(double targetX, double targetZ, bool expected)
    {
        var actual = AvatarPhysicalInteraction.IsWithinEffectorCone(
            bodyX: 0.0,
            bodyZ: 0.0,
            headingDeg: 0.0,
            targetX,
            targetZ,
            maximumDistance: 1.2,
            halfAngleDeg: 72.0);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EffectorGeometryExposesNoObjectOrActionSemantics()
    {
        var parameters = typeof(AvatarPhysicalInteraction)
            .GetMethod(nameof(AvatarPhysicalInteraction.IsWithinEffectorCone))!
            .GetParameters()
            .Select(static parameter => parameter.Name)
            .ToArray();

        Assert.DoesNotContain(parameters, name => name!.Contains("object", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(parameters, name => name!.Contains("action", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(parameters, name => name!.Contains("weapon", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(0.0, 0.8, true, true)]
    [InlineData(0.0, 1.8, false, true)]
    [InlineData(0.8, -0.8, true, false)]
    public void Effector_Assessment_Reports_Physical_Miss_Reason(
        double targetX,
        double targetZ,
        bool withinReach,
        bool withinCone)
    {
        var assessment = AvatarPhysicalInteraction.AssessEffectorCone(
            bodyX: 0.0,
            bodyZ: 0.0,
            headingDeg: 0.0,
            targetX,
            targetZ,
            maximumDistance: 1.2,
            halfAngleDeg: 72.0);

        Assert.Equal(withinReach, assessment.WithinReach);
        Assert.Equal(withinCone, assessment.WithinCone);
        Assert.Equal(withinReach && withinCone, assessment.CanContact);
    }
}
