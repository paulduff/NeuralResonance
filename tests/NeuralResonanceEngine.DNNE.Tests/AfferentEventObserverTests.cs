using System.Text.Json;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class AfferentEventObserverTests
{
    private static readonly Guid Session=Guid.Parse("113f4096-5399-4c20-9ef4-8a4367c2a8c8");
    private static readonly Guid Cell=Guid.Parse("b0fc3e62-ebc2-4a8d-bbf9-3ed25f22a5d6");
    private static AvatarAfferentObservationSite Site(double threshold=-60)=>new("isolated-vestibular","afferent-001","a",threshold);
    private static string Bytes(AvatarAfferentDynamicsState value)=>JsonSerializer.Serialize(value);
    internal static AvatarVestibularAfferentDynamics Model(double step=.001,bool passive=true,double bath=0,double capacitance=.01,Guid? session=null)
    {
        var hp=AvatarHairCellParameters.TypeIReference;var cp=AvatarCalyxParameters.DepositedReference;
        var ap=AvatarAfferentMembraneParameters.NodeReference;
        if(passive)
        {
            hp=hp with { MetMaximumNs=0,PotassiumLowMaximumNs=0,CalciumMaximumNs=0,Hcn1MaximumNs=0,PumpDensityPerSquareMicrometer=0 };
            cp=cp with { Kv74MaximumNs=0,Hcn2MaximumNs=0,InnerPumpDensityPerSquareMicrometer=0,OuterPumpDensityPerSquareMicrometer=0,InnerKccMaximumPaPerSquareMicrometer=0,OuterLeakNs=0 };
            ap=ap with { SodiumMaximumNsPerSquareMicrometer=0,Kv1MaximumNsPerSquareMicrometer=0,Kv7MaximumNsPerSquareMicrometer=0,Kv34MaximumNsPerSquareMicrometer=0 };
        }
        ap=ap with { SpecificCapacitancePfPerSquareMicrometer=capacitance };
        var caps=AvatarCleftCapacitances.FromMembraneGeometry(hp,cp);
        var spatial=new AvatarCleftSpatialCoupling("hair",caps.HairApicalPf,caps.HairPerilymphPf,AvatarCleftMaterial.DepositedReference,
            [new("p",8,caps.HairCleftPf,caps.CalyxInnerPf,caps.CalyxOuterPf)],[]);
        var sensory=new AvatarVestibularCleftMembranes(new(spatial,new("bath",5,140),[new("apex","p",.02,.25)]),hp,cp);
        // Two equal capacitors and a 0.5 nS edge. The severed terminal makes
        // their passive decay analytic; this is not an afferent firing model.
        var cable=new AvatarAfferentCable([new("b",1,1,ap),new("a",1,1,ap)],[new("ab","a","b",1,2,1)],[new("calyx","a",0)]);
        var joint=new AvatarVestibularCleftAfferent(sensory,cable,[new("calyx","p")]);
        return new(session??Session,Cell,joint,new(step),bath+(passive?0:-70),bath,
            [new("p",bath,bath+(passive?0:-60),5,140)],[new("p",new(.2,.3,.4),new(.2,.3,.4,.5))],
            [new("b",bath+(passive?-45:-70),new(.2,.3,.4,.5,.6,.7)),new("a",bath+(passive?-65:-70),new(.2,.3,.4,.5,.6,.7))]);
    }
    private static AvatarAfferentObservedEvent Run(double step=.001,double bath=0,Guid? session=null)
    {
        var model=Model(step,bath:bath,session:session);var observer=new AvatarAfferentEventObserver(model,Site());
        var events=new List<AvatarAfferentObservedEvent>();
        for(int i=0;i<(int)Math.Round(.1/step);i++)if(observer.AdvanceNext(i,0).Event is { } e)events.Add(e);
        Assert.Single(events);return events[0];
    }

    [Theory]
    [InlineData(.2)] [InlineData(.5)] [InlineData(.8)]
    public void AnalyticMonotoneCubicCrossingsPreserveTheCompletedBracket(double root)
    {
        double V(double s)=>(s-root)+.2*Math.Pow(s-root,3);
        double D(double s)=>1+.6*Math.Pow(s-root,2);
        var e=AvatarAfferentCrossing.Observe(new(4,6,V(0),V(1),D(0)/2,D(1)/2),0);
        Assert.NotNull(e);Assert.InRange(Math.Abs(e.Value.TimeMs-(4+2*root)),0,1e-12);
        Assert.InRange(Math.Abs(e.Value.ResidualMv),0,1e-12);Assert.True(e.Value.MinimumScaledSlope>0);
    }
    [Fact]
    public void LinearLimitAndExactEndpointDoNotBecomeFrameTimes()
    {
        Assert.Equal(4.5,AvatarAfferentCrossing.Observe(new(4,6,-1,3,2,2),0)!.Value.TimeMs);
        Assert.Equal(6,AvatarAfferentCrossing.Observe(new(4,6,-4,0,2,2),0)!.Value.TimeMs);
    }
    [Fact]
    public void AHiddenPulseBetweenSameSideEndpointsIsExplicitlyUnresolved()
        =>Assert.Throws<InvalidOperationException>(()=>AvatarAfferentCrossing.Observe(new(0,1,-1,-1,8,-8),0));
    [Fact]
    public void ADownwardPairWithAnInteriorUpwardCrossingIsNotDiscarded()
        =>Assert.Throws<InvalidOperationException>(()=>AvatarAfferentCrossing.Observe(new(0,1,1,-1,-10,-10),0));
    [Fact]
    public void MultipleRootBracketIsNotSilentlyLinearized()
        =>Assert.Throws<InvalidOperationException>(()=>AvatarAfferentCrossing.Observe(new(0,1,-.08,.08,.66,.66),0));
    [Fact]
    public void DownwardStillAndAlreadyAboveIntervalsDoNotInventEvents()
    {
        Assert.Null(AvatarAfferentCrossing.Observe(new(0,1,1,-1,-2,-2),0));
        Assert.Null(AvatarAfferentCrossing.Observe(new(0,1,0,0,0,0),0));
        Assert.Null(AvatarAfferentCrossing.Observe(new(0,1,1,2,1,1),0));
        Assert.Null(AvatarAfferentCrossing.Observe(new(0,1,0,1,1,1),0));
    }
    [Fact]
    public void NonfiniteAndUncompletedIntervalsAreRejected()
    {
        Assert.Throws<ArgumentException>(()=>AvatarAfferentCrossing.Observe(new(0,0,-1,1,2,2),0));
        Assert.Throws<ArgumentException>(()=>AvatarAfferentCrossing.Observe(new(-1,0,-1,1,2,2),0));
        Assert.Throws<ArgumentException>(()=>AvatarAfferentCrossing.Observe(new(0,1,-1,1,double.NaN,2),0));
        Assert.Throws<ArgumentException>(()=>AvatarAfferentCrossing.Observe(new(0,1,-1,1,2,2),double.PositiveInfinity));
    }
    [Fact]
    public void AttachmentNeitherEvaluatesNorSettlesTheSource()
    {
        var model=Model();string before=Bytes(model.Capture());var observer=new AvatarAfferentEventObserver(model,Site());
        Assert.Equal(before,Bytes(model.Capture()));Assert.Equal(0,observer.Capture().EndpointEvaluations);Assert.Equal(0,observer.Capture().EventCount);
    }
    [Fact]
    public void ActualPassiveTrajectoryCrossingRefinesAgainstTheAnalyticTime()
    {
        double expected=2*Math.PI*.01*Math.Log(2);var coarse=Run();var fine=Run(.0005);
        double a=Math.Abs(coarse.Crossing.TimeMs-expected),b=Math.Abs(fine.Crossing.TimeMs-expected);
        Assert.InRange(a,0,2e-5);Assert.True(b<=.4*a+1e-10);Assert.Equal(1,coarse.Ordinal);
        Assert.Equal("a",coarse.CableNodeId);Assert.Equal("afferent-001",coarse.SourceNeuronId);
    }
    [Fact]
    public void ObservationPreservesEveryPassiveAcceptedStateGateAndLedger()
    {
        var observed=Model();var original=Model();var observer=new AvatarAfferentEventObserver(observed,Site());int count=0;
        for(int i=0;i<100;i++)
        {
            original.AdvanceHeld(1,0);var receipt=observer.AdvanceNext(i,0);
            Assert.Equal(Bytes(original.Capture()),Bytes(observed.Capture()));
            Assert.Equal(i+1,receipt.CompletedStep);Assert.Equal(original.Capture().TimeMs,receipt.Interval.EndMs);
            if(receipt.Event is not null)count++;
        }
        Assert.Equal(1,count);Assert.Equal(200,observer.Capture().EndpointEvaluations);
    }
    [Fact]
    public void ActiveFreeGatesAndHeldInputChangesRemainBitwiseUnchanged()
    {
        var observed=Model(1e-6,false);var original=Model(1e-6,false);var observer=new AvatarAfferentEventObserver(observed,Site(0));
        for(int i=0;i<8;i++)
        {
            double input=i%2==0?0:200;original.AdvanceHeld(1,input);observer.AdvanceNext(i,input);
            Assert.Equal(Bytes(original.Capture()),Bytes(observed.Capture()));
        }
        Assert.Equal(0,observer.Capture().EventCount);
    }
    [Fact]
    public void ExactRetryReturnsTheOriginalReceiptWithoutAdvancingOrEvaluating()
    {
        var model=Model();var observer=new AvatarAfferentEventObserver(model,Site());var first=observer.AdvanceNext(0,0);
        string before=Bytes(model.Capture());var state=observer.Capture();Assert.Same(first,observer.AdvanceNext(0,0));
        Assert.Equal(state,observer.Capture());Assert.Equal(before,Bytes(model.Capture()));
    }
    [Fact]
    public void ChangedRetryAndSkippedOrStaleStepsDoNotAdvanceTheSource()
    {
        var model=Model();var observer=new AvatarAfferentEventObserver(model,Site());observer.AdvanceNext(0,0);observer.AdvanceNext(1,0);
        string before=Bytes(model.Capture());
        Assert.Throws<ArgumentException>(()=>observer.AdvanceNext(1,1));
        Assert.Throws<ArgumentException>(()=>observer.AdvanceNext(0,0));
        Assert.Throws<ArgumentException>(()=>observer.AdvanceNext(3,0));
        Assert.Equal(before,Bytes(model.Capture()));Assert.Null(observer.Capture().Failure);
    }
    [Fact]
    public void ExternalAdvancementCannotBeMistakenForObservedHistory()
    {
        var model=Model();var observer=new AvatarAfferentEventObserver(model,Site());model.AdvanceHeld(1,0);
        Assert.Throws<InvalidOperationException>(()=>observer.AdvanceNext(0,0));Assert.Equal(1,model.Capture().CompletedSteps);
    }
    [Fact]
    public void UnknownSiteOrWarmAttachmentIsRejected()
    {
        var model=Model();Assert.Throws<ArgumentException>(()=>new AvatarAfferentEventObserver(model,Site() with { CableNodeId="missing" }));
        model.AdvanceHeld(1,0);Assert.Throws<ArgumentException>(()=>new AvatarAfferentEventObserver(model,Site()));
    }
    [Fact]
    public void ObservedIdentityIsRepeatableAndDistinctAcrossFreshEpochs()
    {
        var a=Run();var b=Run();var c=Run(session:Guid.Parse("d22af277-63a1-45dc-bd81-4d7a08b3940b"));
        Assert.Equal(a,b);Assert.NotEqual(a.MessageId,c.MessageId);Assert.Equal(a.Crossing,c.Crossing);
    }
    [Fact]
    public void ThresholdUsesMembraneVoltageRelativeToTheDeclaredBath()
    {
        var a=Run();var b=Run(bath:32);Assert.InRange(Math.Abs(a.Crossing.TimeMs-b.Crossing.TimeMs),0,1e-10);
    }
    [Fact]
    public void AmbiguousObservationRetainsTheAcceptedPhysicalPrefixAndFaultsExplicitly()
    {
        // Deliberately coarse passive decay overshoots its equilibrium under
        // this SDIRK step. Endpoint slopes make the crossing nonmonotone.
        var model=Model(.05,capacitance:.0001);var original=Model(.05,capacitance:.0001);
        var observer=new AvatarAfferentEventObserver(model,Site());original.AdvanceHeld(1,0);
        Assert.Throws<InvalidOperationException>(()=>observer.AdvanceNext(0,0));
        Assert.Equal(Bytes(original.Capture()),Bytes(model.Capture()));Assert.Equal(1,model.Capture().CompletedSteps);
        var state=observer.Capture();Assert.NotNull(state.Failure);Assert.Equal(1,state.Failure.PhysicalCompletedStep);
        Assert.NotNull(state.Failure.AttemptedInterval);Assert.Equal(0,state.EventCount);Assert.Equal(0,state.ObservedThroughStep);
        Assert.Throws<InvalidOperationException>(()=>observer.AdvanceNext(1,0));Assert.Equal(1,model.Capture().CompletedSteps);
    }
}
