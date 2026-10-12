using System.Numerics;
using Nre.UnityWorld;
using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class UnityBodySensorTests
{
    [Theory]
    [InlineData(.3,0)]
    [InlineData(0,.3)]
    public void RigidBodyTiltIsNotReportedAsAnAdditionalTrunkBend(double pitch,double roll)
    {
        var root=Quaternion.CreateFromYawPitchRoll(.4f,(float)pitch,(float)roll);
        var measured=BodySensorMath.RelativeTrunkTilt(root,root);
        Assert.Equal(0,measured.pitch,6);Assert.Equal(0,measured.roll,6);
        var request=new PhysicalBodyFrameRequest(1,1000,0,0,0,0,0,0,6000000,1,37,.98f,.75f,"unity_tilt",
            PhysicalArticulationFrame.Neutral with {TrunkPitchRadians=measured.pitch,TrunkRollRadians=measured.roll,
                Musculoskeletal=MusculoskeletalStateFrame.Neutral with {Balance=PhysicalBalanceStateFrame.Neutral with {
                    FallPitchRadians=(float)pitch,FallRollRadians=(float)roll}}});
        Assert.True(PhysicalBodyFrameDescriptor.TryCreate(request,out var descriptor,out var error),error);
        var output=new VestibularLabyrinthTransducer().Transduce(descriptor!,null,0);
        string receptor=pitch>0?"otolith_pitch_forward":"otolith_roll_right";
        Assert.Equal(Math.Sin(.3)/.85,Assert.Single(output.Left,p=>p.Receptor==receptor).Activation,6);
    }

    [Fact]
    public void RelativeSpinalMotionIsRetainedWhenThePelvisAlsoMoves()
    {
        var root=Quaternion.CreateFromYawPitchRoll(.6f,.25f,-.1f);
        var chest=root*Quaternion.CreateFromAxisAngle(Vector3.UnitX,.12f);
        var measured=BodySensorMath.RelativeTrunkTilt(root,chest);
        Assert.Equal(.12,measured.pitch,6);Assert.Equal(0,measured.roll,6);
    }

    [Fact]
    public void PlantarLoadsConserveForceAndMirrorMedialFieldsAcrossFeet()
    {
        var left=PlantarPressure.Capture(true,360,new[]{new PlantarContact(.04f,-.1f),new PlantarContact(-.04f,.1f),new PlantarContact(.04f,.1f)});
        var right=PlantarPressure.Capture(false,360,new[]{new PlantarContact(-.04f,-.1f),new PlantarContact(.04f,.1f),new PlantarContact(-.04f,.1f)});
        Assert.Equal(120,left.heelMedialLoadNewtons);Assert.Equal(0,left.heelLateralLoadNewtons);
        Assert.Equal(120,left.forefootMedialLoadNewtons);Assert.Equal(120,left.forefootLateralLoadNewtons);
        Assert.Equal(left.heelMedialLoadNewtons,right.heelMedialLoadNewtons);
        Assert.Equal(left.forefootLateralLoadNewtons,right.forefootLateralLoadNewtons);
        Assert.Equal(360,left.heelMedialLoadNewtons+left.heelLateralLoadNewtons+left.forefootMedialLoadNewtons+left.forefootLateralLoadNewtons);
    }

    [Fact]
    public void AbsentFloorContactDoesNotInventPressureAndInvalidLoadIsRejected()
    {
        var empty=PlantarPressure.Capture(true,360,Array.Empty<PlantarContact>());
        Assert.Equal(0,empty.heelMedialLoadNewtons+empty.heelLateralLoadNewtons+empty.forefootMedialLoadNewtons+empty.forefootLateralLoadNewtons);
        Assert.Throws<ArgumentException>(()=>PlantarPressure.Capture(true,float.NaN,Array.Empty<PlantarContact>()));
    }

    [Fact]
    public void SupportLossUsesCanonicalFractionAndHandSaturationLeavesRawLoadIntact()
    {
        Assert.Equal(0,BodySensorMath.NormalizedSupportError(.1f));
        Assert.Equal(.5f,BodySensorMath.NormalizedSupportError(-.11f));
        Assert.Equal(1,BodySensorMath.NormalizedSupportError(-1.08f));
        float impact=5995.6714f;
        Assert.Equal(5000,BodySensorMath.HandReceptorLoad(impact));
        Assert.Equal(5995.6714f,impact);
    }
}
