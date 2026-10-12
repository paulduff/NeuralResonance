using Nre.UnityWorld;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class UnityBalanceDiagnosticTests
{
    [Fact]
    public void ForwardSwayRecruitsCalvesAndSidewaysSwayRespectsMirroredAnchors()
    {
        var result=BalanceDiagnosticController.Command(8,1.6,.01,.02,0,0,0,0,0,0,true);
        Assert.True(result.leftAnkleSagittalDrive<0);
        Assert.Equal(result.leftAnkleSagittalDrive,result.rightAnkleSagittalDrive);
        Assert.True(result.leftAnkleCoronalDrive>0);Assert.True(result.rightAnkleCoronalDrive<0);
        Assert.Equal(0,result.leftDrive);Assert.Equal(0,result.rightDrive);
        Assert.Equal(1,result.standDrive);Assert.False(result.IsAuthorized());
    }

    [Fact]
    public void MeasuredPoseAndVelocityAffectBoundedTargetsWithoutSelectingARealNeuronalAction()
    {
        var rest=BalanceDiagnosticController.Command(8,1.6,0,0,0,0,.12,.06,0,0,true);
        Assert.Equal(.12/.52,rest.leftAnkleSagittalDrive,6);
        Assert.Equal(.06/.52,rest.rightAnkleSagittalDrive,6);
        var moving=BalanceDiagnosticController.Command(8,1.6,1,1,10,10,.12,.06,0,0,true);
        Assert.Equal(-1,moving.leftAnkleSagittalDrive);Assert.Equal(-1,moving.rightAnkleSagittalDrive);
        Assert.Equal(1,moving.leftAnkleCoronalDrive);Assert.Equal(-1,moving.rightAnkleCoronalDrive);
        Assert.False(moving.IsAuthorized());
    }

    [Fact]
    public void UnsupportedOrFallenFixtureUsesOnlyTheFixedStandControl()
    {
        var result=BalanceDiagnosticController.Command(8,1.6,1,1,10,10,.12,.06,0,0,false);
        Assert.Equal(1,result.standDrive);
        Assert.Equal(0,result.leftAnkleSagittalDrive);Assert.Equal(0,result.rightAnkleCoronalDrive);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidMeasurementsAreRejected(double value)
    {
        Assert.Throws<ArgumentException>(()=>BalanceDiagnosticController.Command(8,1.6,value,0,0,0,0,0,0,0,true));
    }

    [Theory]
    [InlineData(.5)]
    [InlineData(1)]
    public void LateralCorrectionCanRecruitTheExistingMirroredHipPairs(double hipShare)
    {
        var result=BalanceDiagnosticController.Command(12,2.4,.01,0,0,0,0,0,0,0,true,hipShare);
        Assert.True(result.leftHipCoronalDrive>0);Assert.True(result.rightHipCoronalDrive<0);
        Assert.Equal(.12*hipShare/.78,result.leftHipCoronalDrive,6);
        if(hipShare==1){Assert.Equal(0,result.leftAnkleCoronalDrive);Assert.Equal(0,result.rightAnkleCoronalDrive);}
        Assert.False(result.IsAuthorized());
    }

    [Theory]
    [InlineData(-.1)]
    [InlineData(1.1)]
    public void InvalidHipDistributionIsRejected(double share)
    {
        Assert.Throws<ArgumentException>(()=>BalanceDiagnosticController.Command(12,2.4,0,0,0,0,0,0,0,0,true,share));
    }

    [Fact]
    public void PulseOffsetsUseRadiansAcrossAsymmetricRangesAndPreserveSagittalControl()
    {
        var command=BalanceDiagnosticController.Command(12,2.4,0,0,0,0,.1,.1,0,0,true);
        float sagittal=command.leftAnkleSagittalDrive;
        BalanceDiagnosticController.AddCoronalOffsets(command,.03,-.03,.03,-.03);
        Assert.Equal(.03/.78,command.leftHipCoronalDrive,6);
        Assert.Equal(-.03/.45,command.rightHipCoronalDrive,6);
        Assert.Equal(.03/.52,command.leftAnkleCoronalDrive,6);
        Assert.Equal(-.03/.26,command.rightAnkleCoronalDrive,6);
        BalanceDiagnosticController.AddCoronalOffsets(command,-.03,.03,-.03,.03);
        Assert.InRange(Math.Abs(command.leftHipCoronalDrive)+Math.Abs(command.rightHipCoronalDrive)+
            Math.Abs(command.leftAnkleCoronalDrive)+Math.Abs(command.rightAnkleCoronalDrive),0,1e-6);
        Assert.Equal(sagittal,command.leftAnkleSagittalDrive);Assert.False(command.IsAuthorized());
    }

    [Fact]
    public void DiagnosticPulseRejectsAnActiveFrameBeforeMutation()
    {
        var command=new MotorFrame{active=true};
        Assert.Throws<ArgumentException>(()=>BalanceDiagnosticController.AddCoronalOffsets(command,.03,0,0,0));
        Assert.Equal(0,command.leftHipCoronalDrive);
    }
}
