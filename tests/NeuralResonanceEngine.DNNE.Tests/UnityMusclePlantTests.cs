using U=Nre.UnityWorld;
using N=NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class UnityMusclePlantTests
{
    [Fact]
    public void PortMatchesCanonicalActivationForceLengthVelocityAndFatigue()
    {
        var canonical=new N.AvatarMuscle("Test","L",1500);
        var port=new U.AvatarMuscle("Test","L",1500);
        for(int step=0;step<6000;step++) {
            double excitation=step<2000?.7:step<4000?.25:0;
            double length=1+Math.Sin(step*.01)*.2;
            canonical.Advance(excitation,length,.01);port.Advance(excitation,length,.01);
            Assert.Equal(canonical.Activation,port.Activation,12);
            Assert.Equal(canonical.ForceNewtons,port.ForceNewtons,9);
            Assert.Equal(canonical.VelocityPerSecond,port.VelocityPerSecond,10);
            Assert.Equal(canonical.FatigueFraction,port.FatigueFraction,12);
        }
    }

    [Fact]
    public void MeasuredJointPortKeepsCanonicalMuscleForcesWithoutIntegratingAngles()
    {
        var flexor=new N.AvatarMuscle("F","L",720);var extensor=new N.AvatarMuscle("E","L",2900);
        var original=new N.AvatarMuscleJoint(flexor,extensor,-.78,.52,0,.55,.038,22,3.8);
        var port=new U.AvatarMuscleJoint(new U.AvatarMuscle("F","L",720),new U.AvatarMuscle("E","L",2900),-.78,.52,0,.55,.038,22,3.8);
        for(int step=0;step<3000;step++) {
            double target=Math.Sin(step*.01)*.4,angle=original.Angle,speed=original.AngularVelocity;
            original.Advance(target,.8,.04,.01);
            double active=port.AdvanceMeasured(target,.8,.04,angle,speed,.01);
            Assert.Equal((flexor.ForceNewtons-extensor.ForceNewtons)*.038,active,10);
            Assert.Equal(flexor.Activation,port.Flexor.Activation,12);
            Assert.Equal(extensor.FatigueFraction,port.Extensor.FatigueFraction,12);
            Assert.Equal(angle,port.Angle);Assert.Equal(speed,port.AngularVelocity);
        }
    }

    [Fact]
    public void NineteenMeasuredAxesExposeThirtyEightNamedMuscles()
    {
        var plant=new U.MusclePlant();
        Assert.Equal(19,U.MuscleProfiles.All.Length);
        Assert.Equal(19,U.MuscleProfiles.All.Select(p=>(p.Joint,p.Axis)).Distinct().Count());
        Assert.Equal(38,plant.Capture().Length);
        Assert.All(plant.Capture(),m=>{Assert.False(string.IsNullOrWhiteSpace(m.Name));Assert.InRange(m.LengthFraction,.55,1.45);});
    }

    [Fact]
    public void RestAndExhaustedCapacityCannotInventAnActiveCommand()
    {
        var plant=new U.MusclePlant();var spinal=new U.AvatarSpinalLocomotorState(.8,.1,.1,.8,0,0,0);
        double Angle(string joint,int axis)=>U.MuscleProfiles.All.Single(p=>p.Joint==joint&&p.Axis==axis).Rest;
        var rest=plant.Advance(new U.MotorFrame(),spinal,Angle,(_,_)=>0,1,.01);
        Assert.All(rest,e=>{Assert.Equal(0,e.First);Assert.Equal(0,e.Second);});
        var exhausted=plant.Advance(new U.MotorFrame{leftDrive=1,rightDrive=-1,standDrive=1},spinal,Angle,(_,_)=>0,0,.01);
        Assert.All(exhausted,e=>{Assert.Equal(0,e.First);Assert.Equal(0,e.Second);});
        Assert.All(plant.Capture(),m=>Assert.Equal(0,m.ForceNewtons));
    }

    [Fact]
    public void IndependentSignedLegRecruitmentAndMeasurementBoundsAreRetained()
    {
        var plant=new U.MusclePlant();var spinal=new U.AvatarSpinalLocomotorState(.8,.1,.8,.1,0,0,0);
        double Angle(string joint,int axis)=>U.MuscleProfiles.All.Single(p=>p.Joint==joint&&p.Axis==axis).Rest;
        var efforts=plant.Advance(new U.MotorFrame{leftDrive=.6f,rightDrive=-.3f},spinal,Angle,(_,_)=>0,1,.01).ToDictionary(e=>e.Joint);
        Assert.True(efforts["left_thigh"].First>0);Assert.True(efforts["right_thigh"].First<0);
        Assert.Throws<ArgumentException>(()=>plant.Advance(new U.MotorFrame(),spinal,(_,_)=>double.NaN,(_,_)=>0,1,.01));
    }

    [Fact]
    public void FullActiveCapacityRetainsBalancedRestingToneAndRelaxesToZero()
    {
        var joint=Ankle();
        for(int step=0;step<200;step++) {
            double torque=joint.AdvanceMeasured(0,1,.08,0,0,.01,preserveActiveCapacity:true);
            Assert.Equal(0,torque,10);
            Assert.Equal(joint.Flexor.ForceNewtons,joint.Extensor.ForceNewtons,9);
        }
        for(int step=0;step<200;step++)joint.AdvanceMeasured(0,0,0,0,0,.01,preserveActiveCapacity:true);
        Assert.Equal(0,joint.Flexor.ForceNewtons);Assert.Equal(0,joint.Extensor.ForceNewtons);
    }

    [Fact]
    public void FullActiveCapacityUsesTheStrongerCalfWithoutInflatingTheWeakerFlexor()
    {
        var canonical=Ankle();var candidate=Ankle();
        double original=canonical.AdvanceMeasured(-.52,1,0,0,0,.01);
        double revised=candidate.AdvanceMeasured(-.52,1,0,0,0,.01,preserveActiveCapacity:true);
        double response=1-Math.Exp(-.01/.045);
        Assert.Equal(response*720/2900,canonical.Extensor.Activation,12);
        Assert.Equal(response,candidate.Extensor.Activation,12);
        Assert.True(revised<original*3.9);
        Assert.Equal(0,candidate.Flexor.Activation);

        canonical=Ankle();candidate=Ankle();
        original=canonical.AdvanceMeasured(.52,1,0,0,0,.01);
        revised=candidate.AdvanceMeasured(.52,1,0,0,0,.01,preserveActiveCapacity:true);
        Assert.Equal(original,revised,10);
        Assert.Equal(0,candidate.Extensor.Activation);
    }

    [Fact]
    public void FullPositionPolicyKeepsCanonicalVelocityOppositionWhenPositionRecruitmentIsZero()
    {
        var original=Ankle();var candidate=Ankle();
        for(int step=0;step<3000;step++) {
            double angle=Math.Sin(step*.03)*.25,speed=Math.Cos(step*.03)*.75;
            double control=original.AdvanceMeasured(-.52,0,.04,angle,speed,.01);
            double revised=candidate.AdvanceMeasured(-.52,0,.04,angle,speed,.01,
                preserveActiveCapacity:true,preserveVelocityCapacity:false);
            Assert.Equal(control,revised,10);
            Assert.Equal(original.Extensor.Activation,candidate.Extensor.Activation,12);
        }
    }

    [Fact]
    public void DiagnosticFatigueAblationPreservesActivationLengthAndVelocity()
    {
        var normal=new U.AvatarMuscle("Test","L",1500);
        var diagnostic=new U.AvatarMuscle("Test","L",1500);
        for(int step=0;step<2000;step++){
            normal.Advance(1,1,.01);diagnostic.Advance(1,1,.01,suppressDiagnosticFatigue:true);
            Assert.Equal(normal.Activation,diagnostic.Activation,12);
            Assert.Equal(normal.LengthFraction,diagnostic.LengthFraction);
            Assert.Equal(normal.VelocityPerSecond,diagnostic.VelocityPerSecond);
            Assert.Equal(0,diagnostic.FatigueFraction);
        }
        Assert.True(normal.FatigueFraction>.3);
        Assert.Equal(1500*diagnostic.Activation,diagnostic.ForceNewtons,10);
        Assert.True(diagnostic.ForceNewtons>normal.ForceNewtons*2);
        for(int step=0;step<200;step++)diagnostic.Advance(0,1,.01,suppressDiagnosticFatigue:true);
        Assert.Equal(0,diagnostic.ForceNewtons);
    }

    [Fact]
    public void AnkleOnlyAblationRetainsFatigueAndForcesInOtherMuscles()
    {
        var normal=new U.MusclePlant{PreserveActiveCapacity=true};
        var diagnostic=new U.MusclePlant{PreserveActiveCapacity=true,
            DiagnosticFatiguePolicy=U.MuscleFatiguePolicy.DiagnosticAnklesSuppressed};
        var command=new U.MotorFrame{standDrive=1,leftDrive=.8f,leftAnkleCoronalDrive=1,rightAnkleCoronalDrive=-1};
        var spinal=new U.AvatarSpinalLocomotorState(.8,.1,.8,.1,0,0,0);
        double Angle(string joint,int axis)=>U.MuscleProfiles.All.Single(p=>p.Joint==joint&&p.Axis==axis).Rest;
        for(int step=0;step<300;step++){
            normal.Advance(command,spinal,Angle,(_,_)=>0,1,.01);
            diagnostic.Advance(command,spinal,Angle,(_,_)=>0,1,.01);
        }
        var original=normal.Capture();var changed=diagnostic.Capture();
        var ankleNames=new[]{"TibialisAnterior","GastrocnemiusSoleus","TibialisPosterior","FibularisLongusBrevis"};
        Assert.All(changed.Where(m=>ankleNames.Contains(m.Name)),m=>Assert.Equal(0,m.FatigueFraction));
        Assert.Contains(original,m=>ankleNames.Contains(m.Name)&&m.FatigueFraction>.01);
        foreach(var muscle in changed.Where(m=>!ankleNames.Contains(m.Name))){
            var control=original.Single(m=>m.Name==muscle.Name&&m.Side==muscle.Side);
            Assert.Equal(control.FatigueFraction,muscle.FatigueFraction);Assert.Equal(control.ForceNewtons,muscle.ForceNewtons);
        }
        Assert.Contains(changed,m=>m.Name=="Iliopsoas"&&m.FatigueFraction>.01);
    }

    static U.AvatarMuscleJoint Ankle()=>new(new U.AvatarMuscle("TibialisAnterior","L",720),
        new U.AvatarMuscle("GastrocnemiusSoleus","L",2900),-.78,.52,0,.55,.038,22,3.8);
}
