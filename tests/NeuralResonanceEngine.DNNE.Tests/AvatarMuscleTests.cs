using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class AvatarMuscleTests
{
    [Fact]
    public void SustainedModerateIsometricLoadAccumulatesFatigueButRestDoesNot()
    {
        var loaded = new AvatarMuscle("PosturalStabilizer", "L", 1_000.0);
        var ordinaryTone = new AvatarMuscle("OrdinaryPosturalTone", "M", 1_000.0);
        var resting = new AvatarMuscle("RestingMuscle", "R", 1_000.0);
        for (var step = 0; step < 4_800; step++)
        {
            loaded.Advance(excitation: 0.25, lengthFraction: 1.0, dt: 0.025);
            ordinaryTone.Advance(excitation: 0.08, lengthFraction: 1.0, dt: 0.025);
            resting.Advance(excitation: 0.0, lengthFraction: 1.0, dt: 0.025);
        }

        Assert.True(loaded.FatigueFraction > 0.35);
        Assert.True(loaded.ForceNewtons > 0.0);
        Assert.Equal(0.0, ordinaryTone.FatigueFraction);
        Assert.Equal(0.0, resting.Activation);
        Assert.Equal(0.0, resting.ForceNewtons);
        Assert.Equal(0.0, resting.FatigueFraction);
    }

    [Fact]
    public void SustainedExcitationExhaustsForceAndRestRecoversToTrueZero()
    {
        var muscle = new AvatarMuscle("TestFlexor", "L", 1_000.0);
        var peakForce = 0.0;
        for (var step = 0; step < 2_600; step++)
        {
            muscle.Advance(excitation: 1.0, lengthFraction: 1.0, dt: 0.025);
            peakForce = Math.Max(peakForce, muscle.ForceNewtons);
        }

        Assert.True(muscle.FatigueFraction > 0.98);
        Assert.True(muscle.ForceNewtons < peakForce * 0.01);

        for (var step = 0; step < 2_000; step++)
        {
            muscle.Advance(excitation: 0.0, lengthFraction: 1.0, dt: 0.025);
        }

        Assert.Equal(0.0, muscle.Activation);
        Assert.Equal(0.0, muscle.ForceNewtons);
        Assert.Equal(0.0, muscle.FatigueFraction);
    }
}
