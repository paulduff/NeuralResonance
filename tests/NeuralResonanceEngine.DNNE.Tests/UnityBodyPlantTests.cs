using U = Nre.UnityWorld;
using N = NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class UnityBodyPlantTests
{
    [Fact]
    public void OpposingLegCommandsRetainIndependentDirectionAndRecruitment()
    {
        var population = new U.AvatarSpinalLocomotorState(.8, .1, .8, .1, 0, 0, 0);
        var command = new U.MotorFrame { leftDrive = .6f, rightDrive = -.3f };
        var efforts = U.MotorActuation.Build(command, population, 1).ToDictionary(e => e.Joint);
        Assert.True(efforts["left_thigh"].First > 0);
        Assert.True(efforts["right_thigh"].First < 0);
        Assert.Equal(efforts["left_thigh"].First * -.5f, efforts["right_thigh"].First, 5);
        command.rightDrive = 0;
        efforts = U.MotorActuation.Build(command, population, 1).ToDictionary(e => e.Joint);
        Assert.Equal(0, efforts["right_thigh"].First);
        Assert.Equal(0, efforts["right_shin"].First);
        Assert.Equal(0, efforts["right_foot"].First);
    }

    [Fact]
    public void ExhaustionCannotSupplyAnIndependentMotorPolicyOrHoldingReserve()
    {
        var command = new U.MotorFrame { leftDrive = 1, rightDrive = 1, standDrive = 1,
            leftElbowDrive = 1, headYawDrive = 1 };
        var population = new U.AvatarSpinalLocomotorState(.8, .1, .1, .8, 0, 0, 0);
        Assert.All(U.MotorActuation.Build(command, population, 0), effort =>
        {
            Assert.Equal(0, effort.First); Assert.Equal(0, effort.Second); Assert.Equal(0, effort.Stiffness);
        });
        Assert.All(U.MotorActuation.Build(new U.MotorFrame(), population, 1), effort =>
        {
            Assert.Equal(0, effort.First); Assert.Equal(0, effort.Second); Assert.Equal(0, effort.Stiffness);
        });
    }

    [Fact]
    public void AuthorityAcceptsConfiguredZeroThresholdAndRejectsInvalidMeasurements()
    {
        Assert.True(new U.MotorFrame { active = true, confidence = .2f, minimumOutputConfidence = 0 }.IsAuthorized());
        Assert.False(new U.MotorFrame { active = false, confidence = 1, minimumOutputConfidence = 0 }.IsAuthorized());
        Assert.False(new U.MotorFrame { active = true, confidence = .4f, minimumOutputConfidence = .45f }.IsAuthorized());
        Assert.False(new U.MotorFrame { active = true, confidence = 1, minimumOutputConfidence = -.1f }.IsAuthorized());
        Assert.False(new U.MotorFrame { active = true, confidence = 1, minimumOutputConfidence = .45f,
            rightElbowDrive = float.NaN }.IsAuthorized());
    }

    [Fact]
    public void PortablePhysiologyMatchesCanonicalMetabolismDamageAndCapacity()
    {
        var uo = U.AvatarWorldDefaults.Options;
        var no = new N.AvatarPhysiologyOptions(uo.NominalStoredEnergyJoules, uo.MetabolicBurnJoulesPerSecond,
            uo.HydrationLossPerSecond, uo.EnergyDepletionStressEnter, uo.EnergyDepletionStressFull,
            uo.EnergyDamageRateMinimum, uo.EnergyDamageRateScale, uo.DehydrationDamageThreshold,
            uo.DehydrationDamageRateMinimum, uo.DehydrationDamageRateScale, uo.ShelteredSleepRecoveryRate);
        var us = U.AvatarWorldDynamics.CreateRespawnState(uo);
        var ns = N.AvatarWorldDynamics.CreateRespawnState(no);
        for (int i = 0; i < 4_000; i++)
        {
            bool asleep = i > 3000, sheltered = i > 3500;
            us = U.AvatarWorldDynamics.AdvancePhysiology(us, uo, 1, 1, asleep, sheltered);
            ns = N.AvatarWorldDynamics.AdvancePhysiology(ns, no, 1, 1, asleep, sheltered);
            var ua = U.AvatarWorldDynamics.ApplyPhysicalContact(us,
                new U.AvatarPhysicalContactExposure("right_hand", 500, 50, 1200, i + 1, 1, i == 0 ? 50 : 0));
            var na = N.AvatarWorldDynamics.ApplyPhysicalContact(ns,
                new N.AvatarPhysicalContactExposure("right_hand", 500, 50, 1200, i + 1, 1, i == 0 ? 50 : 0));
            us = ua.State; ns = na.State;
            Assert.Equal(ns.StoredEnergyJoules, us.StoredEnergyJoules, 12);
            Assert.Equal(ns.HydrationFraction, us.HydrationFraction, 12);
            Assert.Equal(ns.TissueIntegrityFraction, us.TissueIntegrityFraction, 12);
            Assert.Equal(na.DamageFraction, ua.DamageFraction, 12);
            var uv = U.AvatarWorldDynamics.AssessVitalState(us, uo);
            var nv = N.AvatarWorldDynamics.AssessVitalState(ns, no);
            Assert.Equal((int)nv.State, (int)uv.State);
            Assert.Equal(nv.MotorCapacity, uv.MotorCapacity, 12);
        }
    }

    [Fact]
    public void PlantingSupportDoesNotBecomeAnImpactButASevereOnsetDoes()
    {
        Assert.Equal(0, U.BodyPhysiology.EstimateImpact(true, .05, 80, 73.4, .01));
        Assert.Equal(0, U.BodyPhysiology.EstimateImpact(false, 3, 80, 73.4, .01));
        var support = new U.BodyPhysiology();
        double tissue = support.State.TissueIntegrityFraction;
        for (int i = 0; i < 10_000; i++)
            support.Contact("left_foot", 720, 7.2, 8000, (i + 1) * .01, .01, 0);
        Assert.Equal(tissue, support.State.TissueIntegrityFraction);
        Assert.Equal(0, support.ContactDamage);
        var impulse = U.BodyPhysiology.EstimateImpact(true, 3, 80, 73.4, .01);
        support.Contact("right_hand", 8000, 80, 2000, .01, .01, impulse);
        Assert.True(support.ContactDamage > 0);
        double afterImpact = support.State.TissueIntegrityFraction;
        support.Contact("right_hand", 8000, 80, 2000, .02, .01, impulse);
        Assert.Equal(afterImpact, support.State.TissueIntegrityFraction);
    }
}
