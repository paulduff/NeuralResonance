using NRE.WorldSim;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class AvatarHandPlantTests
{
    [Fact]
    public void ArmMotionOrContactWithoutCloseDriveCannotAcquireAGrasp()
    {
        var hand = new AvatarHandPlant();

        AvatarHandPlantOutput output = default;
        for (var tick = 0; tick < 120; tick++)
        {
            output = hand.Advance(1.0 / 60.0, new AvatarHandPlantInput(
                SignedGraspDrive: 0.0,
                TargetContact: true,
                HoldingObject: false,
                RequiredGripForceNewtons: 6.0));
        }

        Assert.False(output.GraspAcquired);
        Assert.Equal(AvatarHandPhase.Open, output.Phase);
        Assert.Equal(1.0, output.ApertureFraction, precision: 6);
        Assert.Equal(0.0, output.GripForceNewtons, precision: 6);
    }

    [Fact]
    public void ExplicitCloseDriveAndPhysicalContactAcquireAGrasp()
    {
        var hand = new AvatarHandPlant();
        var acquired = false;

        for (var tick = 0; tick < 120 && !acquired; tick++)
        {
            acquired = hand.Advance(1.0 / 60.0, new AvatarHandPlantInput(
                SignedGraspDrive: 1.0,
                TargetContact: true,
                HoldingObject: false,
                RequiredGripForceNewtons: 6.0)).GraspAcquired;
        }

        Assert.True(acquired);
        Assert.True(hand.State.GripForceNewtons >= 6.0);
        Assert.True(hand.State.ApertureFraction <= 0.42);
    }

    [Fact]
    public void ZeroDriveRelaxesTheHandAndReleasesAHeldObject()
    {
        var hand = CreateClosedHand();
        var closedAperture = hand.State.ApertureFraction;
        var released = false;

        for (var tick = 0; tick < 300 && !released; tick++)
        {
            released = hand.Advance(1.0 / 60.0, new AvatarHandPlantInput(
                SignedGraspDrive: 0.0,
                TargetContact: true,
                HoldingObject: true,
                RequiredGripForceNewtons: 6.0)).Released;
        }

        Assert.True(released);
        Assert.True(hand.State.ApertureFraction > closedAperture);
    }

    [Fact]
    public void SustainedLoadedContractionEventuallyForcesFatigueRelease()
    {
        var hand = CreateClosedHand();
        var output = hand.State;

        for (var tick = 0; tick < 2_000 && !output.FatigueRelease; tick++)
        {
            output = hand.Advance(1.0 / 60.0, new AvatarHandPlantInput(
                SignedGraspDrive: 1.0,
                TargetContact: true,
                HoldingObject: true,
                RequiredGripForceNewtons: 80.0));
        }

        Assert.True(output.Released);
        Assert.True(output.FatigueRelease);
        Assert.True(output.FatigueFraction >= 0.94);
    }

    private static AvatarHandPlant CreateClosedHand()
    {
        var hand = new AvatarHandPlant();
        for (var tick = 0; tick < 120; tick++)
        {
            var output = hand.Advance(1.0 / 60.0, new AvatarHandPlantInput(
                SignedGraspDrive: 1.0,
                TargetContact: true,
                HoldingObject: false,
                RequiredGripForceNewtons: 6.0));
            if (output.GraspAcquired)
            {
                break;
            }
        }
        return hand;
    }
}
