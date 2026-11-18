using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class CanalSkullInputTests
{
    private static readonly AvatarSkullVector Zero=new(0,0,0),X=new(1,0,0),Y=new(0,1,0),Z=new(0,0,1);
    private static AvatarCanalPrimeBasis Cartesian()=>new("synthetic source coordinates",new string('A',64),X,Y,Z);
    private static PhysicalSampleClock Clock(double ms=250)=>new(Guid.NewGuid(),"avatar","head",500,ms,1234,9876);
    private static AvatarCanalSkullInput Owner(PhysicalSampleClock clock,AvatarCanalPrimeBasis? basis=null)=>new(clock,"head","centre",Zero,basis??Cartesian());
    private static AvatarSkullObservation Observation(PhysicalSampleClock c,long step,AvatarSkullVector? omega=null)=>
        new(step,PhysicalSampleProtocol.Identity(c,step),"head","centre",Zero,Zero,X,Y,Z,Zero,Zero,omega??Zero,new(0,-9.80665,0));
    private static void Near(double expected,double actual)=>Assert.InRange(Math.Abs(expected-actual),0,1e-11*Math.Max(1,Math.Abs(expected)));
    private static void Near(AvatarSkullVector a,AvatarSkullVector b) { Near(a.X,b.X);Near(a.Y,b.Y);Near(a.Z,b.Z); }
    private static AvatarSkullVector Cross(AvatarSkullVector a,AvatarSkullVector b)=>new(a.Y*b.Z-a.Z*b.Y,a.Z*b.X-a.X*b.Z,a.X*b.Y-a.Y*b.X);

    [Fact]
    public void PublishedDirectionsKeepEveryPrintedLiteralIncludingNonunitPosterior()
    {
        var b=AvatarCanalPrimeBasis.PublishedPrintedRightLabyrinth1();
        Assert.Equal(new(-.064,.042,-.997),b.HC);Assert.Equal(new(.702,.699,-.136),b.AC);Assert.Equal(new(.857,-.557,-.103),b.PC);
        Near(1.055307,b.PC.X*b.PC.X+b.PC.Y*b.PC.Y+b.PC.Z*b.PC.Z);
        Assert.Equal(AvatarCoupledCanalParameters.PublishedRightLabyrinth1().SourceSha256,b.SourceSha256);
        Assert.Equal(b.Fingerprint,AvatarCanalPrimeBasis.PublishedPrintedRightLabyrinth1().Fingerprint);
    }

    [Fact]
    public void NonorthogonalCoordinatesUseTheInverseBasisRatherThanDotProducts()
    {
        var b=new AvatarCanalPrimeBasis("oblique analytic control",new string('A',64),X,new(1,1,0),new(0,1,1));
        var r=b.Resolve(new(3,5,3));Near(1,r.Components.HC);Near(2,r.Components.AC);Near(3,r.Components.PC);
        Near(new(3,5,3),r.ReconstructedSourceAcceleration);Assert.InRange(r.MaximumBackwardError,0,1e-12);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void EachPrintedBasisDirectionReconstructsOnlyItsOwnCoordinate(int axis)
    {
        var b=AvatarCanalPrimeBasis.PublishedPrintedRightLabyrinth1();var vectors=new[]{b.HC,b.AC,b.PC};var r=b.Resolve(vectors[axis]);
        var values=new[]{r.Components.HC,r.Components.AC,r.Components.PC};for(int i=0;i<3;i++)Near(i==axis?1:0,values[i]);
    }

    [Fact]
    public void BasisScalingIsRetainedInCoordinatesAndIdentity()
    {
        var a=Cartesian();var b=new AvatarCanalPrimeBasis(a.Source,a.SourceSha256,new(2,0,0),Y,Z);
        Near(1,a.Resolve(X).Components.HC);Near(.5,b.Resolve(X).Components.HC);Assert.NotEqual(a.Fingerprint,b.Fingerprint);
    }

    [Fact]
    public void SingularAndNonfiniteBasisOrInputAreRejected()
    {
        Assert.Throws<ArgumentException>(()=>new AvatarCanalPrimeBasis("zero row",new string('A',64),X,X,Z));
        Assert.Throws<ArithmeticException>(()=>new AvatarCanalPrimeBasis("singular",new string('A',64),new(1,1,1),new(2,2,2),new(4,0,4)));
        Assert.Throws<ArgumentException>(()=>new AvatarCanalPrimeBasis("invalid",new string('A',64),new(double.NaN,0,0),Y,Z));
        Assert.Throws<ArgumentException>(()=>new AvatarCanalPrimeBasis(" ",new string('A',64),X,Y,Z));
        Assert.Throws<ArgumentException>(()=>new AvatarCanalPrimeBasis("invalid","bad",X,Y,Z));
        Assert.Throws<ArgumentException>(()=>Cartesian().Resolve(new(0,double.PositiveInfinity,0)));
        Assert.Throws<ArgumentException>(()=>AvatarCanalPrimeBasis.AxialSkullToSource(new(double.NaN,0,0)));
    }

    [Fact]
    public void NonfiniteReconstructionIsNotClipped()
    {
        var b=new AvatarCanalPrimeBasis("scale control",new string('A',64),new(2,0,0),Y,Z);
        Assert.Throws<ArithmeticException>(()=>b.Reconstruct(new(double.MaxValue,0,0)));
    }

    [Theory]
    [InlineData(1,0,0,0,-1,0)]
    [InlineData(0,1,0,0,0,-1)]
    [InlineData(0,0,1,1,0,0)]
    public void PositivePitchYawRollHaveDeclaredSourceAxialSigns(double x,double y,double z,double p,double r,double s)
    { Near(new(p,r,s),AvatarCanalPrimeBasis.AxialSkullToSource(new(x,y,z))); }

    [Theory]
    [InlineData(1,0,0,0,1,0)]
    [InlineData(0,1,0,0,0,1)]
    [InlineData(0,0,1,1,0,0)]
    [InlineData(-1,2,-3,4,-5,6)]
    public void ReflectionPreservesRigidPointVelocityOnlyWithTheAxialDeterminant(double x,double y,double z,double a,double b,double c)
    {
        var omega=new AvatarSkullVector(x,y,z);var point=new AvatarSkullVector(a,b,c);
        Near(AvatarCanalPrimeBasis.PolarSkullToSource(Cross(omega,point)),
            Cross(AvatarCanalPrimeBasis.AxialSkullToSource(omega),AvatarCanalPrimeBasis.PolarSkullToSource(point)));
    }

    [Fact]
    public void FirstObservationDoesNotInventAngularAcceleration()
    {
        var clock=Clock();var r=Owner(clock).Consume(Observation(clock,500,new(1,2,3)));
        Assert.Null(r.Projection);Assert.Null(r.DifferenceFromStep);Assert.Null(r.AverageAngularAccelerationWorldRadiansPerSecondSquared);
        Assert.Equal(clock.AtStep(500).NeuralMs,r.Skull.NeuralTimeMs);
    }

    [Fact]
    public void DifferenceIsTakenInWorldCoordinatesThenProjectedAtTheCurrentEndpoint()
    {
        var clock=Clock();var owner=Owner(clock);owner.Consume(Observation(clock,500,new(1,2,3)));
        var r=owner.Consume(Observation(clock,501,new(2,4,6)) with{RightWorld=new(0,0,-1),ForwardWorld=X});
        Near(new(4,8,12),r.AverageAngularAccelerationWorldRadiansPerSecondSquared!);
        Near(new(-12,8,4),r.AverageAngularAccelerationSkullRadiansPerSecondSquared!);
        Near(new(4,12,-8),r.Projection!.SourceAccelerationRadiansPerSecondSquared);
        Assert.Equal(500,r.DifferenceFromStep);Assert.Equal(clock.AtStep(501).NeuralMs,r.Skull.NeuralTimeMs);
        Assert.NotEqual(clock.AtStep(500).NeuralMs,r.Skull.NeuralTimeMs);
    }

    [Fact]
    public void ConstantWorldAngularVelocityDoesNotCreateAccelerationFromFrameChange()
    {
        var clock=Clock();var owner=Owner(clock);var omega=new AvatarSkullVector(1,2,3);
        owner.Consume(Observation(clock,500,omega));
        var r=owner.Consume(Observation(clock,501,omega) with{RightWorld=new(0,0,-1),ForwardWorld=X});
        Near(Zero,r.AverageAngularAccelerationWorldRadiansPerSecondSquared!);Near(Zero,r.Projection!.ReconstructedSourceAcceleration);
    }

    [Fact]
    public void ReplayReturnsTheOriginalReceiptWithoutAnotherDifference()
    {
        var clock=Clock();var owner=Owner(clock);var a=Observation(clock,500);var first=owner.Consume(a);
        Assert.Same(first,owner.Consume(a));var b=Observation(clock,501,X);var second=owner.Consume(b);Assert.Same(second,owner.Consume(b));
        Assert.Equal(first.Skull.RawObservationSha256,second.Skull.PreviousObservationSha256);
    }

    [Fact]
    public void GapsConflictingReplaysAndWrongIdentityLeaveAcceptedStateUnchanged()
    {
        var clock=Clock();var owner=Owner(clock);owner.Consume(Observation(clock,500));var before=owner.Capture();
        Assert.Throws<ArgumentException>(()=>owner.Consume(Observation(clock,502)));
        Assert.Throws<ArgumentException>(()=>owner.Consume(Observation(clock,500,X)));
        Assert.Throws<ArgumentException>(()=>owner.Consume(Observation(clock,501) with{SampleId=Guid.NewGuid()}));
        Assert.Equal(before,owner.Capture());Assert.False(owner.Faulted);
    }

    [Fact]
    public void GeometryValidationIsInheritedWithoutBasisNormalization()
    {
        var clock=Clock();var owner=Owner(clock);owner.Consume(Observation(clock,500));var before=owner.Capture();
        Assert.Throws<ArgumentException>(()=>owner.Consume(Observation(clock,501) with{RightWorld=new(2,0,0)}));
        Assert.Throws<ArgumentException>(()=>owner.Consume(Observation(clock,501) with{ForwardWorld=new(0,0,-1)}));
        Assert.Throws<ArgumentException>(()=>owner.Consume(Observation(clock,501) with{PointWorldMeters=X}));
        Assert.Equal(before,owner.Capture());
    }

    [Fact]
    public void FailedDifferenceRetainsTheAcceptedSkullAndLastValidAngularReceipt()
    {
        var clock=Clock();var owner=Owner(clock);var before=owner.Consume(Observation(clock,500,new(-double.MaxValue,0,0)));
        Assert.Throws<ArithmeticException>(()=>owner.Consume(Observation(clock,501,new(double.MaxValue,0,0))));
        var state=owner.Capture();Assert.True(state.Faulted);Assert.Same(before,state.LastReceipt);
        Assert.Equal(501,state.LastSkull!.PhysicsStep);Assert.NotNull(state.Failure);
        Assert.Throws<InvalidOperationException>(()=>owner.Consume(Observation(clock,502)));
    }

    [Fact]
    public void ClockAndBasisChoicesHaveDistinctOwnerIdentities()
    {
        var clock=Clock();var original=Owner(clock);
        Assert.NotEqual(original.Fingerprint,Owner(clock with{PhysicsStepMs=125}).Fingerprint);
        Assert.NotEqual(original.Fingerprint,Owner(clock,AvatarCanalPrimeBasis.PublishedPrintedRightLabyrinth1()).Fingerprint);
    }

    [Fact]
    public async Task OneThreadOwnsTheObservationHistory()
    {
        var clock=Clock();var owner=Owner(clock);
        await Task.Run(()=>Assert.Throws<InvalidOperationException>(()=>owner.Consume(Observation(clock,500))));
    }
}
