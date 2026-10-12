using Nre.UnityWorld;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class UnityMotorUnitPoolTests
{
    [Fact]
    public void CanonicalTimingPoolControlMatchesOriginalActivationAndForceExactly()
    {
        var original=new AvatarMuscle("Test","L",1500);var adapted=new AvatarMuscle("Test","L",1500);
        for(int step=0;step<10000;step++){
            double excitation=step%1000<200?.2:step%1000<400?.8:step%1000<600?1:0;
            double length=1+Math.Sin(step*.01)*.2;
            original.Advance(excitation,length,.01,suppressDiagnosticFatigue:true);
            adapted.Advance(excitation,length,.01,diagnosticPoolParameters:new MotorUnitFatigueParameters(0,0),useDiagnosticCanonicalTiming:true);
            Assert.Equal(original.Activation,adapted.Activation);
            Assert.Equal(original.ForceNewtons,adapted.ForceNewtons);
            Assert.Equal(original.Activation,adapted.DiagnosticRequestedActivation);
            Assert.Equal(excitation,adapted.DiagnosticExcitation);
            Assert.Equal(0,adapted.FatigueFraction);
        }
    }

    [Fact]
    public void CanonicalTimingPoolControlMatchesTheThirtyEightMusclePlantTorques()
    {
        var original=new MusclePlant{PreserveActiveCapacity=true,PreserveVelocityCapacity=false,DiagnosticFatiguePolicy=MuscleFatiguePolicy.DiagnosticAllSuppressed};
        var adapted=new MusclePlant{PreserveActiveCapacity=true,PreserveVelocityCapacity=false,DiagnosticFatiguePolicy=MuscleFatiguePolicy.DiagnosticCanonicalTimingPoolsNoFatigue};
        var spinal=new AvatarSpinalLocomotorState(.8,.1,.1,.8,0,0,0);
        for(int step=0;step<5000;step++){
            var command=new MotorFrame{standDrive=step%2000<1000?1:0,leftDrive=.3f,rightDrive=-.2f,
                leftAnkleCoronalDrive=(float)Math.Sin(step*.01),rightAnkleCoronalDrive=-(float)Math.Sin(step*.01)};
            double Angle(string joint,int axis)=>MuscleProfiles.All.Single(p=>p.Joint==joint&&p.Axis==axis).Rest+.03*Math.Sin(step*.01);
            var expected=original.Advance(command,spinal,Angle,(_,_)=>.1*Math.Cos(step*.01),1,.01).ToArray();
            var actual=adapted.Advance(command,spinal,Angle,(_,_)=>.1*Math.Cos(step*.01),1,.01).ToArray();
            for(int i=0;i<expected.Length;i++){
                Assert.Equal(expected[i].First,actual[i].First);Assert.Equal(expected[i].Second,actual[i].Second);
            }
            var muscles=original.Capture();var pools=adapted.Capture();
            for(int i=0;i<muscles.Length;i++)Assert.Equal(muscles[i].ForceNewtons,pools[i].ForceNewtons);
        }
    }

    [Fact]
    public void CanonicalTimingFatigueConservesUnitsAndZeroRecoveryCanExhaustForce()
    {
        var pool=new MotorUnitPool();var rates=MotorUnitFatigueParameters.PublishedForJoint("left_foot");
        for(int step=0;step<30000;step++){
            double demand=step%4000<1000?1:step%4000<2000?.05:step%4000<3000?.3:0;
            pool.AdvanceWithActivationTiming(demand,.01,rates,AvatarMuscle.DiagnosticCanonicalTiming);
            Assert.InRange(pool.RestingFraction,0,1);Assert.InRange(pool.ActiveFraction,0,1);Assert.InRange(pool.FatiguedFraction,0,1);
            Assert.Equal(1,pool.RestingFraction+pool.ActiveFraction+pool.FatiguedFraction,10);
        }
        var exhausted=new AvatarMuscle("Test","L",1500);
        for(int step=0;step<1500;step++)exhausted.Advance(1,1,1,
            diagnosticPoolParameters:new MotorUnitFatigueParameters(.02,0),useDiagnosticCanonicalTiming:true);
        Assert.True(exhausted.FatigueFraction>.999999);Assert.InRange(exhausted.ForceNewtons,0,.00001);
    }

    [Fact]
    public void CanonicalTimingFatiguedMuscleRelaxesToTrueZeroAndRecovers()
    {
        var muscle=new AvatarMuscle("Test","L",1500);var rates=MotorUnitFatigueParameters.PublishedForJoint("left_shin");
        for(int step=0;step<3500;step++)muscle.Advance(.8,1,.01,diagnosticPoolParameters:rates,useDiagnosticCanonicalTiming:true);
        double fatigue=muscle.FatigueFraction;Assert.True(fatigue>.3);
        Assert.Equal(1500*muscle.Activation,muscle.ForceNewtons,10);
        for(int step=0;step<10000;step++)muscle.Advance(0,1,.01,diagnosticPoolParameters:rates,useDiagnosticCanonicalTiming:true);
        Assert.Equal(0,muscle.Activation);Assert.Equal(0,muscle.ForceNewtons);
        Assert.True(muscle.FatigueFraction<fatigue*.15);
    }

    [Fact]
    public void CanonicalTimingSubstepsConvergeToTheFineStepReference()
    {
        var outer=new MotorUnitPool();var fine=new MotorUnitPool();var rates=MotorUnitFatigueParameters.PublishedForJoint("left_foot");
        for(int step=0;step<2000;step++){
            outer.AdvanceWithActivationTiming(.5,.01,rates,AvatarMuscle.DiagnosticCanonicalTiming);
            for(int sub=0;sub<100;sub++)fine.AdvanceWithActivationTiming(.5,.0001,rates,AvatarMuscle.DiagnosticCanonicalTiming);
        }
        Assert.InRange(Math.Abs(outer.ActiveFraction-fine.ActiveFraction),0,3e-6);
        Assert.InRange(Math.Abs(outer.FatiguedFraction-fine.FatiguedFraction),0,1e-5);
    }

    [Fact]
    public void DefaultOrInvalidActivationTimingCannotMutateThePools()
    {
        var pool=new MotorUnitPool();var rates=new MotorUnitFatigueParameters(.02,.032);
        Assert.Throws<ArgumentException>(()=>pool.AdvanceWithActivationTiming(.5,.01,rates,default));
        foreach(double time in new[]{0,-1,double.NaN,double.PositiveInfinity})
            Assert.Throws<ArgumentException>(()=>new MotorUnitActivationTiming(time,.09,.0001));
        Assert.Equal(1,pool.RestingFraction);Assert.Equal(0,pool.ActiveFraction);Assert.Equal(0,pool.FatiguedFraction);
    }

    [Fact]
    public void NoFatiguePoolControlRetainsRecruitmentTimingAndCannotFatigue()
    {
        var pool=new MotorUnitPool();var rates=new MotorUnitFatigueParameters(0,0);
        pool.Advance(.5,.01,rates);Assert.InRange(pool.ActiveFraction,.04,.06);
        for(int step=0;step<1000;step++)pool.Advance(.5,.01,rates);
        Assert.Equal(.5,pool.ActiveFraction,12);Assert.Equal(0,pool.FatiguedFraction);
        for(int step=0;step<1000;step++)pool.Advance(0,.01,rates);
        Assert.InRange(pool.ActiveFraction,0,1e-40);Assert.Equal(1,pool.RestingFraction,12);
        var plant=new MusclePlant{DiagnosticFatiguePolicy=MuscleFatiguePolicy.DiagnosticMotorUnitPoolsNoFatigue};
        Assert.Equal(0,plant.DiagnosticPoolRates("left_foot")!.Value.FatigueRate);
        Assert.Equal(38,plant.CaptureDiagnosticPools().Length);
    }

    [Fact]
    public void MotorUnitsStayBoundedAndConservedAcrossDemandAndRest()
    {
        var pool=new MotorUnitPool();var rates=MotorUnitFatigueParameters.PublishedForJoint("left_foot");
        for(int step=0;step<30000;step++){
            double demand=step%4000<1000?1:step%4000<2000?.05:step%4000<3000?.3:0;
            pool.Advance(demand,.01,rates);
            Assert.InRange(pool.RestingFraction,0,1);Assert.InRange(pool.ActiveFraction,0,1);
            Assert.InRange(pool.FatiguedFraction,0,1);
            Assert.Equal(1,pool.RestingFraction+pool.ActiveFraction+pool.FatiguedFraction,10);
        }
    }

    [Fact]
    public void SustainableLowDemandReachesAnEquilibriumWithUnusedRestingUnits()
    {
        var pool=new MotorUnitPool();var rates=MotorUnitFatigueParameters.PublishedForJoint("left_foot");
        for(int step=0;step<30000;step++)pool.Advance(.05,1,rates);
        double expectedActive=.05/(1+rates.FatigueRate/MotorUnitPool.RecruitmentRate);
        Assert.Equal(expectedActive,pool.ActiveFraction,10);
        Assert.InRange(Math.Abs(pool.FatiguedFraction-expectedActive*rates.FatigueRate/rates.RecoveryRate),0,1e-7);
        Assert.True(pool.RestingFraction>.4);
    }

    [Fact]
    public void SustainedMaximumDemandLosesCapacityWithoutAHiddenForceFloor()
    {
        var muscle=new AvatarMuscle("Test","L",1500);
        var rates=new MotorUnitFatigueParameters(.020,0);
        for(int step=0;step<1500;step++)muscle.Advance(1,1,1,diagnosticPoolParameters:rates);
        Assert.True(muscle.FatigueFraction>.999999);
        Assert.InRange(muscle.ForceNewtons,0,.00001);
        Assert.True(muscle.DiagnosticRequestedActivation>.999999);
    }

    [Fact]
    public void RestMultiplierAffectsRecoveryOnlyWhenTargetLoadIsZero()
    {
        var normal=new MotorUnitPool();var fast=new MotorUnitPool();
        var baseRates=new MotorUnitFatigueParameters(.00589,.00058);
        var restRates=new MotorUnitFatigueParameters(.00589,.00058,15);
        for(int step=0;step<10000;step++){
            normal.Advance(.5,.01,baseRates);fast.Advance(.5,.01,restRates);
            Assert.Equal(normal.FatiguedFraction,fast.FatiguedFraction);
        }
        for(int step=0;step<10000;step++){
            normal.Advance(0,.01,baseRates);fast.Advance(0,.01,restRates);
        }
        Assert.True(fast.FatiguedFraction<normal.FatiguedFraction*.5);
        Assert.InRange(fast.ActiveFraction,0,1e-100);
    }

    [Fact]
    public void PoolForceUsesActualActiveUnitsOnceAndResetRestoresTheInitialState()
    {
        var muscle=new AvatarMuscle("Test","L",1500);
        var rates=MotorUnitFatigueParameters.PublishedForJoint("left_shin");
        for(int step=0;step<3000;step++)muscle.Advance(.8,1,.01,diagnosticPoolParameters:rates);
        Assert.True(muscle.FatigueFraction>.3);
        Assert.Equal(muscle.DiagnosticMotorUnits.ActiveFraction,muscle.Activation);
        Assert.Equal(1500*muscle.Activation,muscle.ForceNewtons,10);
        Assert.Equal(muscle.DiagnosticMotorUnits.FatiguedFraction,muscle.FatigueFraction);
        muscle.Reset();
        Assert.Equal(1,muscle.DiagnosticMotorUnits.RestingFraction);Assert.Equal(0,muscle.DiagnosticRequestedActivation);
        Assert.Equal(0,muscle.Activation);Assert.Equal(0,muscle.FatigueFraction);Assert.Equal(0,muscle.ForceNewtons);
    }

    [Fact]
    public void SubsteppingConvergesToTheFineStepReference()
    {
        var outer=new MotorUnitPool();var fine=new MotorUnitPool();
        var rates=MotorUnitFatigueParameters.PublishedForJoint("left_foot");
        for(int step=0;step<2000;step++){
            outer.Advance(.5,.01,rates);
            for(int sub=0;sub<100;sub++)fine.Advance(.5,.0001,rates);
        }
        Assert.InRange(Math.Abs(outer.ActiveFraction-fine.ActiveFraction),0,1e-8);
        Assert.InRange(Math.Abs(outer.FatiguedFraction-fine.FatiguedFraction),0,3e-6);
    }

    [Fact]
    public void InvalidInputsCannotPartiallyChangeThePools()
    {
        var pool=new MotorUnitPool();var rates=new MotorUnitFatigueParameters(.02,.032);
        foreach(double demand in new[]{-1,2,double.NaN,double.PositiveInfinity})
            Assert.Throws<ArgumentException>(()=>pool.Advance(demand,.01,rates));
        foreach(double dt in new[]{0,-1,2,double.NaN,double.PositiveInfinity})
            Assert.Throws<ArgumentException>(()=>pool.Advance(.5,dt,rates));
        Assert.Throws<ArgumentException>(()=>pool.Advance(.5,.01,new MotorUnitFatigueParameters(1e308,0)));
        Assert.Equal(1,pool.RestingFraction);Assert.Equal(0,pool.ActiveFraction);Assert.Equal(0,pool.FatiguedFraction);
    }

    [Fact]
    public void PlantExposesMatchingPoolAndBodyStatesForAllThirtyEightMuscles()
    {
        var plant=new MusclePlant{DiagnosticFatiguePolicy=MuscleFatiguePolicy.DiagnosticMotorUnitPoolsPublishedRates};
        Assert.Empty(new MusclePlant().CaptureDiagnosticPools());
        var spinal=new AvatarSpinalLocomotorState(0,0,0,0,0,0,0);
        double Angle(string joint,int axis)=>MuscleProfiles.All.Single(p=>p.Joint==joint&&p.Axis==axis).Rest;
        for(int step=0;step<2000;step++)plant.Advance(new MotorFrame{standDrive=1,leftAnkleCoronalDrive=1},spinal,Angle,(_,_)=>0,1,.01);
        var capture=plant.Capture();var pools=plant.CaptureDiagnosticPools();Assert.Equal(38,pools.Length);
        foreach(var pool in pools){
            var fiber=capture.Single(m=>m.Name==pool.name&&m.Side==pool.side);
            Assert.Equal(fiber.Activation,pool.activeFraction);Assert.Equal(fiber.FatigueFraction,pool.fatiguedFraction);
            Assert.InRange(Math.Abs(pool.activeFraction+pool.restingFraction+pool.fatiguedFraction-1),0,1e-6);
        }
        Assert.Contains(pools,p=>p.fatiguedFraction>.08);
    }
}
