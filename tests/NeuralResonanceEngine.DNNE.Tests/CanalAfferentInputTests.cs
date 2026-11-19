using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class CanalAfferentInputTests
{
    private static readonly Guid Session=Guid.Parse("113f4096-5399-4c20-9ef4-8a4367c2a8c8");
    private static readonly AvatarSkullVector Zero=new(0,0,0),X=new(1,0,0),Y=new(0,1,0),Z=new(0,0,1);
    private static PhysicalSampleClock Physical(double step=.125,double epoch=1000)=>new(Session,"fixture-body","fixture-source",50,step,0,epoch);
    private static AvatarAfferentObservationSite Site(double threshold=-60)=>new("declared-canal-source","one-actual-afferent","a",threshold);
    private static string Bytes(object state)=>JsonSerializer.Serialize(state);
    private static AvatarSkullObservation Head(PhysicalSampleClock c,long step)=>new(step,PhysicalSampleProtocol.Identity(c,step),
        "head","centre",Zero,Zero,X,Y,Z,Zero,Zero,Zero,new(0,-9.80665,0));

    // Reuse the already declared passive control/active membrane fixture, but
    // explicitly attach its full initial state at the mechanical availability
    // epoch. No settling/evaluation is performed by this construction helper.
    private static AvatarVestibularAfferentDynamics Model(AvatarCanalAfferentClock clock,bool passive=true,double capacitance=.01,
        Guid? cell=null,double? epoch=null,double? step=null)
    {
        var template=AfferentEventObserverTests.Model(clock.SourceStepMs,passive,capacitance:capacitance,session:clock.PhysicalClock.SessionId);
        var s=template.Capture();var kernel=template.Kernel;
        if(!passive)kernel=new(kernel.Sensory,new AvatarAfferentCable(kernel.Cable.Nodes,kernel.Cable.Edges,
            kernel.Cable.Terminals.Select(t=>t with{ConductanceNs=.2})),kernel.Attachments);
        double V(string label)=>s.Values[Array.IndexOf(s.StateLabels,label)];
        var compartments=kernel.Sensory.Coupling.Spatial.Compartments.Select(n=>new AvatarCleftCompartmentState(n.Id,V(n.Id+"/cleftMv"),
            V(n.Id+"/calyxMv"),V(n.Id+"/potassiumMm"),V(n.Id+"/sodiumMm"))).ToArray();
        var gates=kernel.Sensory.Coupling.Spatial.Compartments.Select(n=>new AvatarCleftGateState(n.Id,
            new(V(n.Id+"/hairPotassiumLow"),V(n.Id+"/hairCalcium"),V(n.Id+"/hairHcn1")),
            new(V(n.Id+"/calyxInnerKv74"),V(n.Id+"/calyxInnerHcn2"),V(n.Id+"/calyxOuterKv74"),V(n.Id+"/calyxOuterHcn2")))).ToArray();
        var cable=kernel.Cable.Nodes.Select(n=>new AvatarAfferentCableState(n.Id,V("axon/"+n.Id+"/potentialMv"),
            new(V("axon/"+n.Id+"/sodiumActivation"),V("axon/"+n.Id+"/sodiumInactivation"),V("axon/"+n.Id+"/kv1Activation"),
                V("axon/"+n.Id+"/kv7Activation"),V("axon/"+n.Id+"/kv34Activation"),V("axon/"+n.Id+"/kv34Inactivation")))).ToArray();
        return new(s.SessionId,cell??s.CellId,kernel,s.Settings with{StepMs=step??clock.SourceStepMs},V("hairMv"),s.PerilymphMv,
            compartments,gates,cable,epoch??clock.SourceEpochMs);
    }
    private static AvatarCanalBundleInput Mechanics(PhysicalSampleClock clock,Guid cell)
    {
        var p=AvatarCoupledCanalParameters.PublishedRightLabyrinth1();var field=AvatarCupulaMechanicalBinding.EquivalentCircularField(p,
            "declared-control-organ",AvatarCupulaCanal.Horizontal,"explicit stiffness-derived test geometry",p.SourceSha256,.37,.5);
        var attachment=new AvatarCupulaBundleAttachment(cell,field.OrganId,field.Canal,"central control attachment",0,40e-6,1);
        var initial=new AvatarCanalMechanicalPoint(new(0,0,0,1e-14,0,0),default);
        var owner=new AvatarCanalBundleInput(clock,"head","centre",Zero,AvatarCanalPrimeBasis.PublishedPrintedRightLabyrinth1(),p,initial,
            new AvatarCanalStepSchedule("unequal sensory control",32,new[]{4,4,8,16}),field,attachment);
        owner.Consume(Head(clock,50));owner.Consume(Head(clock,51));return owner;
    }
    private sealed record Rig(AvatarCanalAfferentInput Owner,AvatarCanalBundleInput Mechanics,AvatarVestibularAfferentDynamics Model,AvatarCanalAfferentClock Clock);
    private static Rig Create(int packet=8,int sourceSteps=16,double bodyStep=.125,bool passive=true,double capacitance=.01,double threshold=-60)
    {
        var clock=new AvatarCanalAfferentClock(Physical(bodyStep),32,sourceSteps);var model=Model(clock,passive,capacitance);
        var mechanics=Mechanics(clock.PhysicalClock,model.Capture().CellId);
        return new(new(mechanics,model,Site(threshold),clock,packet),mechanics,model,clock);
    }
    private static void Close(Rig r,long step=52)=>r.Owner.ConsumeHead(Head(r.Clock.PhysicalClock,step));

    [Fact]
    public void IntegerClockMapsEpochAndDifferentSubdivisionsWithoutTimestampSearch()
    {
        var c=new AvatarCanalAfferentClock(Physical(),32,16);var first=c.At(0);var middle=c.At(3);var final=c.At(16);
        Assert.Equal(32,first.MechanicalTick);Assert.Equal(38,middle.MechanicalTick);Assert.Equal(64,final.MechanicalTick);
        Assert.Equal(1000.125,first.SourceNeuralMs);Assert.Equal(1000.1484375,middle.SourceNeuralMs);Assert.Equal(1000.25,final.SourceNeuralMs);
        Assert.Equal(.25,final.PhysicalMs);Assert.Equal(0,final.RepresentationDifferenceMs);
    }

    [Theory]
    [InlineData(0,1)]
    [InlineData(32,0)]
    [InlineData(32,3)]
    [InlineData(30,2)]
    [InlineData(32,64)]
    public void NonaddressableSensoryGridCannotBeRoundedIntoAnAllowedRate(int ticks,int steps)
        =>Assert.Throws<ArgumentException>(()=>new AvatarCanalAfferentClock(Physical(),ticks,steps));

    [Fact]
    public void EquivalentIntegerCoordinatesRetainBothRoundedClockRepresentations()
    {
        var c=new AvatarCanalAfferentClock(Physical(.1),32,16);bool different=false;
        for(int i=0;i<4096;i++)
        {
            var t=c.At(i);Assert.Equal(32+2L*i,t.MechanicalTick);Assert.Equal(c.SourceEpochMs+i*c.SourceStepMs,t.SourceNeuralMs);
            Assert.Equal(1000+(32+2L*i)*(.1/32),t.MechanicalNeuralMs);
            Assert.Equal(t.SourceNeuralMs-t.MechanicalNeuralMs,t.RepresentationDifferenceMs);
            different|=t.SourceNeuralMs!=t.MechanicalNeuralMs;
        }
        Assert.True(different); // They must not be rewritten to make a false bitwise identity.
    }

    [Fact]
    public void ExhaustedTimestampResolutionCannotSilentlyMergeAdjacentTicks()
    {
        Assert.Throws<ArithmeticException>(()=>new AvatarCanalAfferentClock(Physical(4,9007199254740986d),32,16));
        var c=new AvatarCanalAfferentClock(Physical(),32,16);
        Assert.Throws<ArgumentOutOfRangeException>(()=>c.At(-1));
        Assert.Throws<ArgumentOutOfRangeException>(()=>c.At(ObservedAfferentSource.MaximumExactStep));
    }

    [Fact]
    public void AttachmentPreservesEveryInitialGateIonVoltageAndLedgerWithoutEvaluation()
    {
        var c=new AvatarCanalAfferentClock(Physical(),32,16);var model=Model(c);var mechanics=Mechanics(c.PhysicalClock,model.Capture().CellId);
        string before=Bytes(model.Capture()),physical=Bytes(mechanics.Capture());var owner=new AvatarCanalAfferentInput(mechanics,model,Site(),c,8);
        Assert.Equal(before,Bytes(model.Capture()));Assert.Equal(physical,Bytes(mechanics.Capture()));
        Assert.Equal(0,owner.Capture().Observer.EndpointEvaluations);Assert.Equal(0,owner.Capture().CompletedSteps);
    }

    [Theory]
    [InlineData("cell")]
    [InlineData("epoch")]
    [InlineData("step")]
    [InlineData("session")]
    public void MismatchedSourceIdentityOrClockIsRejectedBeforeAdvancement(string kind)
    {
        var c=new AvatarCanalAfferentClock(Physical(),32,16);var correct=Model(c);var mechanics=Mechanics(c.PhysicalClock,correct.Capture().CellId);
        var alternate=kind=="session"?new AvatarCanalAfferentClock(Physical() with{SessionId=Guid.NewGuid()},32,16):c;
        var model=Model(alternate,cell:kind=="cell"?Guid.NewGuid():null,epoch:kind=="epoch"?0:null,step:kind=="step"?c.SourceStepMs*2:null);
        string before=Bytes(model.Capture());Assert.Throws<ArgumentException>(()=>new AvatarCanalAfferentInput(mechanics,model,Site(),c,8));
        Assert.Equal(before,Bytes(model.Capture()));Assert.Null(mechanics.Capture().PendingTrace);
    }

    [Fact]
    public void NoCompletedHeadIntervalMeansNoInventedSensoryInput()
    {
        var r=Create();string before=Bytes(r.Owner.Capture());Assert.Throws<InvalidOperationException>(()=>r.Owner.AdvanceNext(0));
        Assert.Equal(before,Bytes(r.Owner.Capture()));Assert.Throws<InvalidOperationException>(()=>r.Owner.Seal());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WarmAttachmentCannotDiscardAnEarlierNeuronalOrMechanicalInterval(bool neuronal)
    {
        var c=new AvatarCanalAfferentClock(Physical(),32,16);var model=Model(c);var mechanics=Mechanics(c.PhysicalClock,model.Capture().CellId);
        if(neuronal)model.AdvanceHeld(1,0);else mechanics.Consume(Head(c.PhysicalClock,52));
        string before=Bytes(model.Capture()),physical=Bytes(mechanics.Capture());
        Assert.Throws<ArgumentException>(()=>new AvatarCanalAfferentInput(mechanics,model,Site(),c,8));
        Assert.Equal(before,Bytes(model.Capture()));Assert.Equal(physical,Bytes(mechanics.Capture()));
    }

    [Fact]
    public void AnotherThreadCannotTakeOwnershipOfTheSensoryClock()
    {
        var r=Create();Exception? error=null;string before=Bytes(r.Owner.Capture());
        var thread=new Thread(()=>{try{Close(r);}catch(Exception e){error=e;}});thread.Start();thread.Join();
        Assert.IsType<InvalidOperationException>(error);Assert.Equal(before,Bytes(r.Owner.Capture()));
    }

    [Fact]
    public void EverySourceStepUsesItsActualDenseStartAndPreservesOriginalObservation()
    {
        var r=Create(packet:32);Close(r);var reference=Model(r.Clock);var observer=new AvatarAfferentEventObserver(reference,Site());
        var trace=r.Mechanics.Capture().PendingTrace!;
        for(int i=0;i<16;i++)
        {
            var sample=trace.AtTick(32+2*i);var expected=observer.AdvanceNext(i,sample.Bundle.BundleDisplacementNanometers);
            var actual=r.Owner.AdvanceNext(i);Assert.Equal(expected,actual.Observation);Assert.Equal(sample,actual.HeldBundle);
            Assert.Equal(Bytes(reference.Capture()),Bytes(r.Model.Capture()));Assert.Same(actual,r.Owner.AdvanceNext(i));
        }
        Assert.Equal(16,r.Owner.Capture().CompletedSteps);Assert.NotNull(r.Mechanics.Capture().PendingTrace);
        Assert.Throws<InvalidOperationException>(()=>r.Owner.AdvanceNext(16));Assert.Throws<InvalidOperationException>(()=>Close(r,53));
        var packet=r.Owner.Seal();Assert.Single(packet.Events);Assert.Equal(16,packet.CompletedStep);
        Assert.InRange(packet.Events[0].CrossingTimeMs,r.Clock.SourceEpochMs,r.Clock.At(16).SourceNeuralMs);
        r.Owner.AcknowledgePacket(packet.Fingerprint);Assert.Null(r.Mechanics.Capture().PendingTrace);
    }

    [Fact]
    public void SmallOutboxStopsBeforeAdvancingAndQuietAcknowledgementResumesSameTrace()
    {
        var r=Create(packet:1,threshold:100);Close(r);r.Owner.AdvanceNext(0);string before=Bytes(r.Model.Capture());
        Assert.Throws<InvalidOperationException>(()=>r.Owner.AdvanceNext(1));Assert.Equal(before,Bytes(r.Model.Capture()));
        var p=r.Owner.Seal();Assert.Empty(p.Events);Assert.Same(p,r.Owner.Seal());Assert.Equal(1,p.CompletedStep);
        Assert.Throws<ArgumentException>(()=>r.Owner.AcknowledgePacket("foreign"));Assert.Throws<InvalidOperationException>(()=>r.Owner.AdvanceNext(1));
        r.Owner.AcknowledgePacket(p.Fingerprint);r.Owner.AcknowledgePacket(p.Fingerprint);Assert.NotNull(r.Mechanics.Capture().PendingTrace);
        Assert.Throws<ArgumentException>(()=>r.Owner.AdvanceNext(0));r.Owner.AdvanceNext(1);Assert.Equal(2,r.Model.Capture().CompletedSteps);
    }

    [Fact]
    public void PacketPartitionsPreserveContinuousStateAndOriginalEventIdentityAcrossBodyIntervals()
    {
        var a=Create(packet:1);var b=Create(packet:16);var ea=new List<ObservedAfferentEvent>();var eb=new List<ObservedAfferentEvent>();
        for(int interval=0;interval<2;interval++)
        {
            Close(a,52+interval);Close(b,52+interval);
            for(int j=0;j<16;j++)
            {
                long step=16*interval+j;a.Owner.AdvanceNext(step);b.Owner.AdvanceNext(step);
                var pa=a.Owner.Seal();ea.AddRange(pa.Events);a.Owner.AcknowledgePacket(pa.Fingerprint);
                Assert.Equal(Bytes(a.Model.Capture()),Bytes(b.Model.Capture()));
            }
            var pb=b.Owner.Seal();eb.AddRange(pb.Events);b.Owner.AcknowledgePacket(pb.Fingerprint);
        }
        Assert.Equal(ea,eb);Assert.Single(ea);Assert.Equal(32,a.Model.Capture().CompletedSteps);
        Assert.Equal(32,a.Owner.Capture().AcknowledgedSteps);Assert.Null(a.Mechanics.Capture().PendingTrace);
    }

    [Fact]
    public void InvalidHeadReplayOrGapDoesNotChangeAnyAcceptedNeuronalState()
    {
        var r=Create();string before=Bytes(r.Owner.Capture());
        Assert.Throws<ArgumentException>(()=>Close(r,53));Assert.Equal(before,Bytes(r.Owner.Capture()));
        Close(r);var exact=Head(r.Clock.PhysicalClock,52);var receipt=r.Owner.ConsumeHead(exact);before=Bytes(r.Owner.Capture());
        Assert.Same(receipt,r.Owner.ConsumeHead(exact));
        Assert.Throws<ArgumentException>(()=>r.Owner.ConsumeHead(exact with{SampleId=Guid.NewGuid()}));
        Assert.Equal(before,Bytes(r.Owner.Capture()));Assert.Null(r.Owner.Capture().Failure);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ExternalAdvancementCannotBypassTheComposedOwner(bool advanceNeuron)
    {
        var r=Create();if(advanceNeuron)r.Model.AdvanceHeld(1,0);else r.Mechanics.Consume(Head(r.Clock.PhysicalClock,52));
        string before=Bytes(r.Model.Capture());Assert.Throws<InvalidOperationException>(()=>Close(r));
        Assert.Equal(before,Bytes(r.Model.Capture()));Assert.Throws<InvalidOperationException>(()=>r.Owner.AdvanceNext(0));
    }

    [Fact]
    public void AmbiguousCrossingPreservesAcceptedNeuronAndMechanicalHistoryWithoutReset()
    {
        var r=Create(sourceSteps:2,bodyStep:.1,capacitance:.0001);Close(r);var trace=r.Mechanics.Capture().PendingTrace;
        Assert.Throws<InvalidOperationException>(()=>r.Owner.AdvanceNext(0));var state=r.Owner.Capture();
        Assert.Equal(1,state.Neuronal.CompletedSteps);Assert.Equal(0,state.CompletedSteps);Assert.NotNull(state.Failure);
        Assert.NotNull(state.Observer.Failure);Assert.Same(trace,r.Mechanics.Capture().PendingTrace);
        Assert.Throws<InvalidOperationException>(()=>r.Owner.AdvanceNext(0));Assert.Throws<InvalidOperationException>(()=>r.Owner.Seal());
    }

    [Fact]
    public void ActualActiveCalyxCableUsesThePhysicalInputWithoutChangingItsEquations()
    {
        var r=Create(packet:8,sourceSteps:8,bodyStep:.000008,passive:false,threshold:0);Close(r);
        var original=Model(r.Clock,passive:false);var quiet=Model(r.Clock,passive:false);
        var initial=Bytes(r.Model.Capture());var trace=r.Mechanics.Capture().PendingTrace!;
        Assert.All(r.Model.Kernel.Cable.Terminals,t=>Assert.Equal(.2,t.ConductanceNs));
        for(int i=0;i<8;i++)
        {
            var input=trace.AtTick(32+4*i).Bundle.BundleDisplacementNanometers;
            original.AdvanceHeld(1,input);quiet.AdvanceHeld(1,0);r.Owner.AdvanceNext(i);Assert.Equal(Bytes(original.Capture()),Bytes(r.Model.Capture()));
        }
        Assert.NotEqual(initial,Bytes(r.Model.Capture()));Assert.Equal(8,r.Model.Capture().CompletedSteps);
        var drivenState=r.Model.Capture();var quietState=quiet.Capture();
        int met=Array.IndexOf(drivenState.LedgerLabels,"metOutwardChargeFc");Assert.True(met>=0);
        Assert.NotEqual(quietState.Ledgers[met],drivenState.Ledgers[met]);
        Assert.InRange(r.Model.Capture().MaximumInterfaceChargeResidualFc,0,1e-10);
        var packet=r.Owner.Seal();r.Owner.AcknowledgePacket(packet.Fingerprint);Assert.Null(r.Mechanics.Capture().PendingTrace);
    }
}
