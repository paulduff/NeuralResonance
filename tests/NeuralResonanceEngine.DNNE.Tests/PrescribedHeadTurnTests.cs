using System.Text.Json;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class PrescribedHeadTurnTests
{
    private static readonly AvatarSkullVector Zero=new(0,0,0),X=new(1,0,0),Y=new(0,1,0),Z=new(0,0,1);
    private static PhysicalSampleClock Clock()=>new(Guid.Parse("dbbe9fc3-e4a2-4883-9f5b-9124b36eccdf"),"prescribed-head","diagnostic-turn",42,1,1234.5,1000);
    private static AvatarSkullObservation Rest(PhysicalSampleClock clock,bool rotated=false,bool centre=false)
    {
        var local=centre?Zero:new AvatarSkullVector(.03,.02,.01);var origin=new AvatarSkullVector(.3,.4,.5);
        var right=rotated?new AvatarSkullVector(0,0,-1):X;var forward=rotated?X:Z;
        var point=Add(origin,Add(Scale(right,local.X),Add(Scale(Y,local.Y),Scale(forward,local.Z))));
        return new(clock.InitialPhysicsStep,PhysicalSampleProtocol.Identity(clock,clock.InitialPhysicsStep),"head","labyrinth",local,
            origin,right,Y,forward,point,Zero,Zero,new(0,-9.80665,0));
    }
    private static PrescribedHeadTurn Create(PrescribedHeadAxis axis=PrescribedHeadAxis.Yaw,double angle=.4,bool rotated=false,bool centre=false)
    {var c=Clock();return new(c,Rest(c,rotated,centre),axis,angle,10,1000);}
    private static void Near(double actual,double expected,double tolerance=1e-12)=>Assert.InRange(Math.Abs(actual-expected),0,tolerance);
    private static void Near(AvatarSkullVector actual,AvatarSkullVector expected,double tolerance=1e-12)
    {Near(actual.X,expected.X,tolerance);Near(actual.Y,expected.Y,tolerance);Near(actual.Z,expected.Z,tolerance);}
    private static AvatarSkullVector Scale(AvatarSkullVector a,double b)=>new(a.X*b,a.Y*b,a.Z*b);
    private static AvatarSkullVector Add(AvatarSkullVector a,AvatarSkullVector b)=>new(a.X+b.X,a.Y+b.Y,a.Z+b.Z);
    private static AvatarSkullVector Subtract(AvatarSkullVector a,AvatarSkullVector b)=>Add(a,Scale(b,-1));
    private static AvatarSkullVector Derivative(PrescribedHeadTurn p,long step,Func<PrescribedHeadTurnSample,AvatarSkullVector> value)
        =>Scale(Add(Subtract(Scale(value(p.AtStep(step+1)),8),Scale(value(p.AtStep(step-1)),8)),
            Subtract(value(p.AtStep(step-2)),value(p.AtStep(step+2)))),1/(12*p.Clock.PhysicsStepMs/1000));

    [Fact]
    public void InitialQuietSamplesPreserveTheExactStationaryHead()
    {
        var c=Clock();var rest=Rest(c,rotated:true);var p=new PrescribedHeadTurn(c,rest,PrescribedHeadAxis.Pitch,.4,10,1000);
        for(long step=c.InitialPhysicsStep;step<=p.StartStep;step++)
        {
            var sample=p.AtStep(step);var expected=rest with{PhysicsStep=step,SampleId=PhysicalSampleProtocol.Identity(c,step)};
            Assert.Equal(JsonSerializer.Serialize(expected),JsonSerializer.Serialize(sample.Observation));
            Assert.Equal(0,sample.AngleRadians);Assert.Equal(0,sample.AngularSpeedRadiansPerSecond);
            Assert.Equal(0,sample.AngularAccelerationRadiansPerSecondSquared);Assert.Equal(Zero,sample.PointAccelerationWorldMetersPerSecondSquared);
        }
    }

    [Fact]
    public void ZeroExcursionRemainsStationaryBeforeDuringAndAfterTheTurn()
    {
        var p=Create(angle:0,rotated:true);var rest=Rest(p.Clock,rotated:true);
        foreach(long step in new[]{p.Clock.InitialPhysicsStep,p.StartStep+200,p.StartStep+500,p.EndStep,p.EndStep+100})
        {
            var s=p.AtStep(step);Assert.Equal(0,s.AngleRadians);Assert.Equal(Zero,s.Observation.PointVelocityWorldMetersPerSecond);
            Assert.Equal(Zero,s.Observation.AngularVelocityWorldRadiansPerSecond);Assert.Equal(rest.PointWorldMeters,s.Observation.PointWorldMeters);
            Assert.Equal(rest.RightWorld,s.Observation.RightWorld);Assert.Equal(Zero,s.PointAccelerationWorldMetersPerSecondSquared);
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void MidpointMatchesTheAnalyticQuinticTurn(int axis)
    {
        var p=Create((PrescribedHeadAxis)axis);var s=p.AtStep(p.StartStep+500);
        Near(s.AngleRadians,.2);Near(s.AngularSpeedRadiansPerSecond,.4*15/8);Near(s.AngularAccelerationRadiansPerSecondSquared,0);
        Near(s.Observation.AngularVelocityWorldRadiansPerSecond,Scale(new[]{X,Y,Z}[axis],.75));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void PositiveQuarterTurnsUseTheDeclaredRightHandedSkullAxes(int axis)
    {
        var p=Create((PrescribedHeadAxis)axis,Math.PI/2);var s=p.AtStep(p.EndStep).Observation;
        var right=new[]{X,Scale(Z,-1),Y};var up=new[]{Z,Y,Scale(X,-1)};var forward=new[]{Scale(Y,-1),X,Z};
        Near(s.RightWorld,right[axis]);Near(s.UpWorld,up[axis]);Near(s.ForwardWorld,forward[axis]);
    }

    [Fact]
    public void OppositeExcursionsReverseAngularMotionWithoutChangingGravityOrThePivot()
    {
        var plus=Create(angle:.4);var minus=Create(angle:-.4);
        foreach(long step in new[]{plus.StartStep,plus.StartStep+200,plus.StartStep+500,plus.StartStep+800,plus.EndStep,plus.EndStep+20})
        {
            var a=plus.AtStep(step);var b=minus.AtStep(step);
            Near(a.AngleRadians,-b.AngleRadians);Near(a.AngularSpeedRadiansPerSecond,-b.AngularSpeedRadiansPerSecond);
            Near(a.AngularAccelerationRadiansPerSecondSquared,-b.AngularAccelerationRadiansPerSecondSquared);
            Near(a.Observation.AngularVelocityWorldRadiansPerSecond,Scale(b.Observation.AngularVelocityWorldRadiansPerSecond,-1));
            Assert.Equal(a.Observation.OriginWorldMeters,b.Observation.OriginWorldMeters);
            Assert.Equal(a.Observation.GravityWorldMetersPerSecondSquared,b.Observation.GravityWorldMetersPerSecondSquared);
            Near(a.Observation.RightWorld.X,b.Observation.RightWorld.X);Near(a.Observation.RightWorld.Z,-b.Observation.RightWorld.Z);
        }
    }

    [Theory]
    [InlineData(200)] [InlineData(500)] [InlineData(800)]
    public void AnalyticPointVelocityAndAccelerationMatchIndependentPositionDifferences(int offset)
    {
        var p=Create(PrescribedHeadAxis.Pitch,rotated:true);long step=p.StartStep+offset;var s=p.AtStep(step);
        Near(Derivative(p,step,x=>x.Observation.PointWorldMeters),s.Observation.PointVelocityWorldMetersPerSecond,1e-8);
        Near(Derivative(p,step,x=>x.Observation.PointVelocityWorldMetersPerSecond),s.PointAccelerationWorldMetersPerSecondSquared,1e-8);
        Near(Derivative(p,step,x=>x.Observation.AngularVelocityWorldRadiansPerSecond),
            Scale(Rest(p.Clock,rotated:true).RightWorld,s.AngularAccelerationRadiansPerSecondSquared),1e-8);
    }

    [Fact]
    public void EndingTheTurnStopsMotionWithoutResettingOrientation()
    {
        var p=Create();var end=p.AtStep(p.EndStep);var tail=p.AtStep(p.EndStep+500);
        Near(end.AngleRadians,.4);Assert.Equal(0,end.AngularSpeedRadiansPerSecond);Assert.Equal(0,end.AngularAccelerationRadiansPerSecondSquared);
        Assert.Equal(end.Observation.RightWorld,tail.Observation.RightWorld);Assert.NotEqual(X,end.Observation.RightWorld);
        Assert.Equal(end.Observation.PointWorldMeters,tail.Observation.PointWorldMeters);Assert.Equal(Zero,tail.PointAccelerationWorldMetersPerSecondSquared);
    }

    [Fact]
    public void OffCentreMaterialPointHasCentripetalAccelerationAtZeroAngularAcceleration()
    {
        var p=Create();var s=p.AtStep(p.StartStep+500);var offset=Subtract(s.Observation.PointWorldMeters,s.Observation.OriginWorldMeters);
        Near(s.AngularAccelerationRadiansPerSecondSquared,0);
        Near(s.PointAccelerationWorldMetersPerSecondSquared,new(-.75*.75*offset.X,0,-.75*.75*offset.Z));
        Assert.NotEqual(Zero,s.PointAccelerationWorldMetersPerSecondSquared);
    }

    [Fact]
    public void CentreRotationFeedsTheActualSkullAdapterWithoutInventingLinearMotion()
    {
        var p=Create(centre:true);var rest=Rest(p.Clock,centre:true);var owner=new AvatarSkullKinematics(p.Clock,"head","labyrinth",Zero);
        for(long step=p.Clock.InitialPhysicsStep;step<=p.EndStep+2;step++)
        {
            var sample=p.AtStep(step);var receipt=owner.Consume(sample.Observation);
            if(step==p.Clock.InitialPhysicsStep){Assert.Null(receipt.VestibularInput);continue;}
            Assert.Equal(Zero,receipt.AverageAccelerationWorldMetersPerSecondSquared);
            Near(receipt.VestibularInput!.YawVelocityRadiansPerSecond,sample.AngularSpeedRadiansPerSecond);
            Near(receipt.VestibularInput.GravitoInertialDownMetersPerSecondSquared,9.80665);
            Assert.Equal(p.Clock.AtStep(step).NeuralMs,receipt.NeuralTimeMs);
        }
    }

    [Fact]
    public void RandomAccessAndRetriesDoNotAccumulatePhaseOrChangeIdentity()
    {
        var p=Create();long step=p.StartStep+701;var first=p.AtStep(step);p.AtStep(p.EndStep+900);p.AtStep(p.StartStep);
        Assert.Equal(first,p.AtStep(step));Assert.Equal(PhysicalSampleProtocol.Identity(p.Clock,step),first.Observation.SampleId);
        Assert.Equal(step,first.Observation.PhysicsStep);
    }

    [Fact]
    public void ConflictingMotionsCannotMasqueradeAsTheSamePhysicalSample()
    {
        var p=Create();var opposite=Create(angle:-.4);var owner=new AvatarSkullKinematics(p.Clock,"head","labyrinth",Rest(p.Clock).LocalPointMeters);
        for(long step=p.Clock.InitialPhysicsStep;step<=p.StartStep+1;step++)owner.Consume(p.AtStep(step).Observation);
        var before=owner.Capture();Assert.Throws<ArgumentException>(()=>owner.Consume(opposite.AtStep(p.StartStep+1).Observation));
        Assert.Same(before,owner.Capture());Assert.False(owner.Faulted);
    }

    [Fact]
    public void RotationAxisFollowsTheInitialHeadFrameInsteadOfTheWorldFrame()
    {
        var p=Create(PrescribedHeadAxis.Pitch,Math.PI/2,rotated:true);var middle=p.AtStep(p.StartStep+500);
        Near(middle.Observation.AngularVelocityWorldRadiansPerSecond,Scale(Z,-Math.PI/2*15/8));
        var end=p.AtStep(p.EndStep).Observation;Near(end.RightWorld,Scale(Z,-1));Near(end.UpWorld,X);Near(end.ForwardWorld,Scale(Y,-1));
    }

    [Fact]
    public void InvalidMotionDeclarationsAndForeignInitialGeometryAreRejected()
    {
        var c=Clock();var rest=Rest(c);
        foreach(double angle in new[]{double.NaN,double.PositiveInfinity,4*Math.PI})
            Assert.Throws<ArgumentException>(()=>new PrescribedHeadTurn(c,rest,PrescribedHeadAxis.Yaw,angle,10,1000));
        Assert.Throws<ArgumentException>(()=>new PrescribedHeadTurn(c,rest,PrescribedHeadAxis.Yaw,.4,1,1000));
        Assert.Throws<ArgumentException>(()=>new PrescribedHeadTurn(c,rest,PrescribedHeadAxis.Yaw,.4,10,1));
        Assert.Throws<ArgumentException>(()=>new PrescribedHeadTurn(c,rest,(PrescribedHeadAxis)99,.4,10,1000));
        foreach(var bad in new[]{rest with{SampleId=Guid.NewGuid()},rest with{RightWorld=Scale(X,-1)},
            rest with{AngularVelocityWorldRadiansPerSecond=Y},rest with{PointVelocityWorldMetersPerSecond=X}})
            Assert.Throws<ArgumentException>(()=>new PrescribedHeadTurn(c,bad,PrescribedHeadAxis.Yaw,.4,10,1000));
        foreach(var measured in new[]{rest with{RightWorld=Scale(X,1+1e-7)},
            rest with{UpWorld=new(1e-7,1,0)},rest with{PointWorldMeters=Add(rest.PointWorldMeters,new(1e-7,0,0))}})
        {
            // Valid native float measurements need not be valid analytic fixtures.
            new AvatarSkullKinematics(c,"head","labyrinth",rest.LocalPointMeters).Consume(measured);
            Assert.Throws<ArgumentException>(()=>new PrescribedHeadTurn(c,measured,PrescribedHeadAxis.Yaw,.4,10,1000));
        }
    }

    [Fact]
    public void FingerprintRetainsSignedDirectionAxisAndTimingChoices()
    {
        var p=Create();Assert.Equal(p.Fingerprint,Create().Fingerprint);Assert.NotEqual(p.Fingerprint,Create(angle:-.4).Fingerprint);
        Assert.NotEqual(p.Fingerprint,Create(PrescribedHeadAxis.Roll).Fingerprint);
        Assert.NotEqual(p.Fingerprint,new PrescribedHeadTurn(p.Clock,Rest(p.Clock),PrescribedHeadAxis.Yaw,.4,11,1000).Fingerprint);
        Assert.NotEqual(p.Fingerprint,new PrescribedHeadTurn(p.Clock,Rest(p.Clock),PrescribedHeadAxis.Yaw,.4,10,500).Fingerprint);
    }
}
