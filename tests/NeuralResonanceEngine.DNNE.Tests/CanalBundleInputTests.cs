using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class CanalBundleInputTests
{
    private static readonly AvatarSkullVector Zero=new(0,0,0),X=new(1,0,0),Y=new(0,1,0),Z=new(0,0,1);
    private static readonly Guid Cell=Guid.Parse("ddc1d6ab-dc76-413d-b879-dc70d0d34e56");
    private static PhysicalSampleClock Clock()=>new(Guid.NewGuid(),"body","head",500,1,32,1000);
    private static AvatarCoupledCanalParameters Parameters()
    {
        var m=new double[36];var c=new double[36];var k=new double[36];var f=new double[18];
        for(int i=0;i<6;i++){m[6*i+i]=1;c[6*i+i]=2;if(i>=3)k[6*i+i]=i-2;}
        f[0]=1;f[9]=1;return new("synthetic bridge mechanics",new string('A',64),m,c,k,f);
    }
    private static AvatarCanalPrimeBasis Basis()=>new("synthetic Cartesian control",new string('A',64),X,Y,Z);
    private static AvatarCupulaShearField Field(AvatarCoupledCanalParameters p,AvatarCupulaCanal c=AvatarCupulaCanal.Horizontal)=>
        AvatarCupulaMechanicalBinding.EquivalentCircularField(p,"fixture-right",c,"stiffness-derived synthetic geometry",new string('B',64),1,.5);
    private static AvatarCupulaBundleAttachment Attachment(AvatarCupulaShearField f)=>new(Cell,f.OrganId,f.Canal,"synthetic attachment",0,.1*f.RadiusMeters,1);
    private static AvatarCanalStepSchedule Schedule()=>new("unequal bridge control",16,new[]{1,1,2,4,8});
    private static AvatarCanalBundleInput Owner(PhysicalSampleClock c,AvatarCanalStepSchedule? schedule=null)
    {
        var p=Parameters();var f=Field(p);return new(c,"head","centre",Zero,Basis(),p,new(default,default),schedule??Schedule(),f,Attachment(f));
    }
    private static AvatarSkullObservation Input(PhysicalSampleClock c,long step,double roll=0)=>new(step,PhysicalSampleProtocol.Identity(c,step),
        "head","centre",Zero,Zero,X,Y,Z,Zero,Zero,new(0,0,roll),new(0,-9.80665,0));
    private static void Initialize(AvatarCanalBundleInput owner,PhysicalSampleClock c)
    {owner.Consume(Input(c,500));owner.Consume(Input(c,501,1));}
    private static void Close(double expected,double actual)=>Assert.InRange(Math.Abs(actual-expected),0,1e-13*(1+Math.Abs(expected)));

    [Theory]
    [InlineData(AvatarCupulaCanal.Horizontal)]
    [InlineData(AvatarCupulaCanal.Anterior)]
    [InlineData(AvatarCupulaCanal.Posterior)]
    public void DeclaredInverseGeometryMatchesEachOriginalStiffness(AvatarCupulaCanal canal)
    {
        var p=AvatarCoupledCanalParameters.PublishedRightLabyrinth1();string fingerprint=p.Fingerprint;
        var f=Field(p,canal);var match=AvatarCupulaMechanicalBinding.Match(p,f);
        Assert.InRange(match.RelativeDifference,0,1e-12);Assert.Equal(p.Fingerprint,match.MechanicalParametersFingerprint);
        Assert.Equal(f.Fingerprint,match.FieldFingerprint);Assert.Equal(fingerprint,p.Fingerprint);
        Close(.5,f.ThicknessMeters/f.RadiusMeters);
    }

    [Fact]
    public void GeometryMismatchIsRejectedWithoutChangingEitherModel()
    {
        var p=Parameters();var original=Field(p);var wrong=new AvatarCupulaShearField(original.OrganId,original.Canal,original.GeometrySource,
            original.SourceSha256,original.RadiusMeters,original.ThicknessMeters,2*original.ShearModulusPascal);
        Assert.Throws<ArgumentException>(()=>AvatarCupulaMechanicalBinding.Match(p,wrong));
        Assert.Throws<ArgumentException>(()=>new AvatarCanalBundleInput(Clock(),"head","centre",Zero,Basis(),p,new(default,default),Schedule(),wrong,Attachment(wrong)));
        Assert.Equal(original.StiffnessPascalPerCubicMeter,Field(p).StiffnessPascalPerCubicMeter);
    }

    [Fact]
    public void DifferentDeclaredMaterialDoesNotMasqueradeAsTheSameGeometry()
    {
        var p=Parameters();var a=Field(p);var b=AvatarCupulaMechanicalBinding.EquivalentCircularField(p,a.OrganId,a.Canal,a.GeometrySource,a.SourceSha256,2,.5);
        Assert.NotEqual(a.RadiusMeters,b.RadiusMeters);Assert.NotEqual(a.Fingerprint,b.Fingerprint);
        Assert.InRange(AvatarCupulaMechanicalBinding.Match(p,b).RelativeDifference,0,1e-12);
    }

    [Fact]
    public void FirstAngularAvailabilityInitializesWithoutInventingASensoryInterval()
    {
        var c=Clock();var owner=Owner(c);var raw=owner.Consume(Input(c,500));Assert.Null(raw.AvailableSample);Assert.Null(raw.CompletedTrace);
        var angular=owner.Consume(Input(c,501,1));Assert.NotNull(angular.AvailableSample);Assert.Null(angular.CompletedTrace);
        Assert.Equal(16,angular.AvailableSample.ClockTick);Assert.Equal(c.AtStep(501).NeuralMs,angular.AvailableSample.NeuralTimeMs);
        Assert.Equal(0,angular.AvailableSample.Bundle.BundleDisplacementNanometers);Assert.Equal(0,owner.Capture().Mechanics.Mechanics.IntegratedIntervals);
    }

    [Fact]
    public void EveryAcceptedMechanicalKnotAndOriginalTimestampIsRetained()
    {
        var c=Clock();var owner=Owner(c);Initialize(owner,c);var receipt=owner.Consume(Input(c,502,-7));var trace=receipt.CompletedTrace!;
        Assert.Equal(new long[]{16,17,18,20,24,32},trace.Knots.Select(k=>k.ClockTick));
        Assert.Equal(501,trace.HeldInput.Skull.PhysicsStep);Assert.Equal(502,trace.ClosingInput.Skull.PhysicsStep);
        foreach(var (saved,knot) in receipt.Mechanics.CompletedInterval!.Substeps.Zip(trace.Knots.Skip(1)))
        {
            Assert.Equal(saved.Transition.After,knot.Mechanics);Assert.Equal(saved.PhysicalTimeMs,knot.PhysicalTimeMs);
            Assert.Equal(saved.NeuralTimeMs,knot.NeuralTimeMs);Assert.Equal(Cell,knot.Bundle.HairCellId);
            Assert.Same(knot,trace.AtTick(knot.ClockTick));
        }
        Assert.Equal(receipt.AvailableSample,trace.Knots[^1]);Assert.Equal(receipt.Mechanics.CompletedInterval.Fingerprint,trace.MechanicalIntervalFingerprint);
    }

    [Fact]
    public void IntermediateClockQueriesUseTheContainingMechanicalStage()
    {
        var c=Clock();var owner=Owner(c);Initialize(owner,c);var receipt=owner.Consume(Input(c,502));var trace=receipt.CompletedTrace!;
        var sample=trace.AtTick(21);var transition=receipt.Mechanics.CompletedInterval!.Substeps[3].Transition;
        Assert.Equal(AvatarCanalDensePoint.At(transition,.25),sample.Mechanics);
        Assert.Equal(c.PhysicalEpochMs+21d/16,sample.PhysicalTimeMs);Assert.Equal(c.NeuralEpochMs+21d/16,sample.NeuralTimeMs);
        var p=Parameters();var f=Field(p);Assert.Equal(f.Project(sample.Mechanics,Attachment(f)),sample.Bundle);
        Assert.Throws<ArgumentOutOfRangeException>(()=>trace.AtTick(15));Assert.Throws<ArgumentOutOfRangeException>(()=>trace.AtTick(33));
    }

    [Fact]
    public void ClosingInputChangesOnlyTheFollowingMechanicalAndBundleInterval()
    {
        var c=Clock();var a=Owner(c);var b=Owner(c);Initialize(a,c);Initialize(b,c);
        var first=a.Consume(Input(c,502,20));var other=b.Consume(Input(c,502,-10));
        Assert.Equal(first.CompletedTrace!.Knots,other.CompletedTrace!.Knots);
        Assert.NotEqual(first.CompletedTrace.Fingerprint,other.CompletedTrace.Fingerprint);
        a.AcknowledgeTrace(first.CompletedTrace.Fingerprint);b.AcknowledgeTrace(other.CompletedTrace.Fingerprint);
        var nextA=a.Consume(Input(c,503));var nextB=b.Consume(Input(c,503));
        Assert.NotEqual(nextA.AvailableSample!.Bundle.BundleDisplacementNanometers,nextB.AvailableSample!.Bundle.BundleDisplacementNanometers);
    }

    [Fact]
    public void PendingTraceBlocksBothOwnersAndExactLatestRetryIsInert()
    {
        var c=Clock();var owner=Owner(c);Initialize(owner,c);var receipt=owner.Consume(Input(c,502));var before=owner.Capture();
        Assert.Throws<InvalidOperationException>(()=>owner.Consume(Input(c,503)));Assert.Equal(before,owner.Capture());
        Assert.Same(receipt,owner.Consume(Input(c,502)));Assert.Equal(before,owner.Capture());
        Assert.Throws<ArgumentException>(()=>owner.AcknowledgeTrace("foreign"));Assert.Equal(before,owner.Capture());
        owner.AcknowledgeTrace(receipt.CompletedTrace!.Fingerprint);owner.AcknowledgeTrace(receipt.CompletedTrace.Fingerprint);
        Assert.Null(owner.Capture().PendingTrace);Assert.Null(owner.Capture().Mechanics.PendingInterval);
        Assert.Same(receipt,owner.Consume(Input(c,502)));Assert.Null(owner.Capture().PendingTrace);
    }

    [Fact]
    public void InvalidObservationDoesNotFaultOrAdvanceAnOtherwiseValidBridge()
    {
        var c=Clock();var owner=Owner(c);Initialize(owner,c);var before=owner.Capture();
        Assert.Throws<ArgumentException>(()=>owner.Consume(Input(c,503)));
        Assert.Throws<ArgumentException>(()=>owner.Consume(Input(c,501,20)));
        Assert.Throws<ArgumentException>(()=>owner.Consume(Input(c,502) with{SampleId=Guid.NewGuid()}));
        Assert.Equal(before,owner.Capture());Assert.Null(owner.Capture().Failure);
    }

    [Fact]
    public void ReleasedTraceRemainsImmutableAfterAcknowledgementAndFurtherMotion()
    {
        var c=Clock();var owner=Owner(c);Initialize(owner,c);var first=owner.Consume(Input(c,502));var trace=first.CompletedTrace!;
        var sample=trace.AtTick(21);var list=Assert.IsAssignableFrom<IList<AvatarTimedCupulaBundle>>(trace.Knots);
        Assert.Throws<NotSupportedException>(()=>list[0]=sample);owner.AcknowledgeTrace(trace.Fingerprint);owner.Consume(Input(c,503,5));
        Assert.Equal(sample,trace.AtTick(21));Assert.Same(first.CompletedTrace,trace);
    }

    [Fact]
    public void MechanicalClockFaultRetainsItsAttemptedPrefixWithoutABundleReset()
    {
        var c=Clock() with{PhysicsStepMs=4,PhysicalEpochMs=0,NeuralEpochMs=9007199254740986d};
        var owner=Owner(c,new AvatarCanalStepSchedule("uniform failure control",4,new[]{1,1,1,1}));
        owner.Consume(Input(c,500));owner.Consume(Input(c,501));var before=owner.Capture();
        Assert.Throws<ArithmeticException>(()=>owner.Consume(Input(c,502)));var after=owner.Capture();
        Assert.NotNull(after.Failure);Assert.True(after.Mechanics.Faulted);Assert.Equal(before.Mechanics.Mechanics,after.Mechanics.Mechanics);
        Assert.Equal(502,after.Mechanics.Input.LastSkull!.PhysicsStep);Assert.Equal(2,after.Mechanics.Failure!.AttemptedIntervalPrefix.Count);
        Assert.Null(after.PendingTrace);Assert.Throws<InvalidOperationException>(()=>owner.Consume(Input(c,502)));
    }

    private static AvatarCanalMechanicalTransition Polynomial()
    {
        const double h=2;
        AvatarCanalVector V(double x)=>new(x,2*x,3*x,4*x,5*x,6*x);
        AvatarCanalMechanicalPoint P(double t)=>new(V(3+4*t+2.5*t*t),V(4+5*t));
        return new(AvatarCoupledCanalDynamics.Version,"analytic constant-acceleration control",h,default,P(0),P(h),
            new(P(h/3),V(5)),new(P(h),V(5)),0,0,0,0,0,0,0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(.125)]
    [InlineData(.3333333333333333)]
    [InlineData(.5)]
    [InlineData(.875)]
    [InlineData(1)]
    public void DensePointRecoversConstantAccelerationInAllSixCoordinates(double fraction)
    {
        var state=AvatarCanalDensePoint.At(Polynomial(),fraction);double t=2*fraction;
        for(int i=0;i<6;i++){Close((i+1)*(3+4*t+2.5*t*t),state.VolumeMeters3.ToArray()[i]);Close((i+1)*(4+5*t),state.FlowMeters3PerSecond.ToArray()[i]);}
    }

    [Fact]
    public void DensePointReturnsOriginalAcceptedAndStageEndpointsExactly()
    {
        var t=Polynomial();Assert.Same(t.Before,AvatarCanalDensePoint.At(t,0));Assert.Same(t.After,AvatarCanalDensePoint.At(t,1));
        Assert.Same(t.FirstStage.Point,AvatarCanalDensePoint.At(t,1d/3));
    }

    [Theory]
    [InlineData(-.001)]
    [InlineData(1.001)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void DensePointRejectsMissingHistoryInsteadOfExtrapolating(double fraction)
    { Assert.Throws<ArgumentOutOfRangeException>(()=>AvatarCanalDensePoint.At(Polynomial(),fraction)); }

    [Fact]
    public void DensePointRejectsUnknownStageConventionAndNonfiniteAcceptedPoint()
    {
        var t=Polynomial();Assert.Throws<ArgumentException>(()=>AvatarCanalDensePoint.At(t with{Version="unknown"},.5));
        Assert.Throws<ArgumentException>(()=>AvatarCanalDensePoint.At(t with{After=new(new(double.NaN,0,0,0,0,0),default)},1));
    }
}
