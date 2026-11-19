using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

// Known component fixtures, not calibrated vestibular motion or afferent firing.
public sealed class CanalAfferentMidpointTests
{
    private static readonly Guid Session=Guid.Parse("113f4096-5399-4c20-9ef4-8a4367c2a8c8");
    private static readonly AvatarSkullVector Zero=new(0,0,0);
    private static string Json(object value)=>JsonSerializer.Serialize(value);
    private static AvatarSkullObservation Head(PhysicalSampleClock c,long step)=>new(step,PhysicalSampleProtocol.Identity(c,step),
        "head","centre",Zero,Zero,new(1,0,0),new(0,1,0),new(0,0,1),Zero,Zero,Zero,new(0,-9.80665,0));

    private static AvatarVestibularAfferentDynamics Model(AvatarCanalAfferentClock clock,bool passive)
    {
        // Same declared fixture as CanalAfferentInputTests, retaining every gate,
        // ion and cable state at the attached epoch without evaluating a step.
        var template=AfferentEventObserverTests.Model(clock.SourceStepMs,passive,session:Session);
        var s=template.Capture();var kernel=template.Kernel;
        if(!passive)kernel=new(kernel.Sensory,new AvatarAfferentCable(kernel.Cable.Nodes,kernel.Cable.Edges,
            kernel.Cable.Terminals.Select(t=>t with{ConductanceNs=.2})),kernel.Attachments);
        double V(string name)=>s.Values[Array.IndexOf(s.StateLabels,name)];
        var states=kernel.Sensory.Coupling.Spatial.Compartments.Select(n=>new AvatarCleftCompartmentState(n.Id,
            V(n.Id+"/cleftMv"),V(n.Id+"/calyxMv"),V(n.Id+"/potassiumMm"),V(n.Id+"/sodiumMm"))).ToArray();
        var gates=kernel.Sensory.Coupling.Spatial.Compartments.Select(n=>new AvatarCleftGateState(n.Id,
            new(V(n.Id+"/hairPotassiumLow"),V(n.Id+"/hairCalcium"),V(n.Id+"/hairHcn1")),
            new(V(n.Id+"/calyxInnerKv74"),V(n.Id+"/calyxInnerHcn2"),V(n.Id+"/calyxOuterKv74"),V(n.Id+"/calyxOuterHcn2")))).ToArray();
        var cable=kernel.Cable.Nodes.Select(n=>new AvatarAfferentCableState(n.Id,V("axon/"+n.Id+"/potentialMv"),
            new(V("axon/"+n.Id+"/sodiumActivation"),V("axon/"+n.Id+"/sodiumInactivation"),V("axon/"+n.Id+"/kv1Activation"),
                V("axon/"+n.Id+"/kv7Activation"),V("axon/"+n.Id+"/kv34Activation"),V("axon/"+n.Id+"/kv34Inactivation")))).ToArray();
        return new(Session,s.CellId,kernel,s.Settings,V("hairMv"),s.PerilymphMv,states,gates,cable,clock.SourceEpochMs);
    }
    private sealed record Rig(AvatarCanalAfferentInput Owner,AvatarCanalBundleInput Mechanics,
        AvatarVestibularAfferentDynamics Model,AvatarCanalAfferentClock Clock);
    private static Rig Create(int packet=16,int steps=16,double bodyStep=.125,bool passive=true,
        AvatarCanalForcingPolicy policy=AvatarCanalForcingPolicy.MidpointHeld)
    {
        var physical=new PhysicalSampleClock(Session,"fixture-body","fixture-source",50,bodyStep,0,1000);
        var clock=new AvatarCanalAfferentClock(physical,32,steps);var model=Model(clock,passive);
        var p=AvatarCoupledCanalParameters.PublishedRightLabyrinth1();
        var field=AvatarCupulaMechanicalBinding.EquivalentCircularField(p,"declared-control-organ",AvatarCupulaCanal.Horizontal,
            "explicit stiffness-derived test geometry",p.SourceSha256,.37,.5);
        var attachment=new AvatarCupulaBundleAttachment(model.Capture().CellId,field.OrganId,field.Canal,"central control attachment",0,40e-6,1);
        var mechanics=new AvatarCanalBundleInput(physical,"head","centre",Zero,AvatarCanalPrimeBasis.PublishedPrintedRightLabyrinth1(),
            p,new(new(0,0,0,1e-14,0,0),default),new AvatarCanalStepSchedule("unequal sensory control",32,new[]{4,4,8,16}),field,attachment);
        mechanics.Consume(Head(physical,50));mechanics.Consume(Head(physical,51));
        return new(new(mechanics,model,new("declared-canal-source","one-actual-afferent","a",passive?-60:0),clock,packet,policy),mechanics,model,clock);
    }
    private static void Close(Rig r,long step=52)=>r.Owner.ConsumeHead(Head(r.Clock.PhysicalClock,step));

    [Fact]
    public void ExplicitMidpointVersionsTheOwnerWithoutChangingInitialNeuronalOrMechanicalState()
    {
        var start=Create(policy:AvatarCanalForcingPolicy.StartHeld);var middle=Create();
        Assert.Equal(Json(start.Model.Capture()),Json(middle.Model.Capture()));
        Assert.Equal(Json(start.Mechanics.Capture()),Json(middle.Mechanics.Capture()));
        Assert.NotEqual(start.Owner.Fingerprint,middle.Owner.Fingerprint);Assert.Equal(start.Owner.Source,middle.Owner.Source);
        Assert.Equal(0,middle.Owner.Capture().Observer.EndpointEvaluations);
        Assert.Equal(AvatarCanalForcingPolicy.StartHeld,start.Owner.ForcingPolicy);
        Assert.Equal(AvatarCanalForcingPolicy.MidpointHeld,middle.Owner.ForcingPolicy);
    }

    [Fact]
    public void MidpointRequiresAnExactlyAddressableTickAndAValidPolicy()
    {
        Assert.Throws<ArgumentException>(()=>Create(steps:32));
        Assert.Throws<ArgumentException>(()=>Create(policy:(AvatarCanalForcingPolicy)99));
        var original=Create(steps:32,policy:AvatarCanalForcingPolicy.StartHeld);
        Assert.Equal(0,original.Model.Capture().CompletedSteps);
    }

    [Fact]
    public void NoCompletedMechanicalHistoryMeansNoMidpointPredictionOrNeuronalAdvance()
    {
        var r=Create();string before=Json(r.Owner.Capture());
        Assert.Throws<InvalidOperationException>(()=>r.Owner.AdvanceNext(0));Assert.Equal(before,Json(r.Owner.Capture()));
        Assert.Throws<InvalidOperationException>(()=>r.Owner.Seal());
    }

    [Fact]
    public void ActualDenseMidpointsPreserveOriginalStepBoundariesAndObservedEvents()
    {
        var r=Create();Close(r);var bare=Model(r.Clock,true);
        var observer=new AvatarAfferentEventObserver(bare,new("declared-canal-source","one-actual-afferent","a",-60));
        var trace=r.Mechanics.Capture().PendingTrace!;
        for(int i=0;i<16;i++)
        {
            var sample=trace.AtTick(33+2*i);var expected=observer.AdvanceNext(i,sample.Bundle.BundleDisplacementNanometers);
            var actual=r.Owner.AdvanceNext(i);Assert.Equal(sample,actual.HeldBundle);Assert.Equal(expected,actual.Observation);
            Assert.Equal(r.Clock.At(i),actual.Start);Assert.Equal(r.Clock.At(i+1),actual.End);
            Assert.True(sample.ClockTick>actual.Start.MechanicalTick&&sample.ClockTick<actual.End.MechanicalTick);
            Assert.Equal(Json(bare.Capture()),Json(r.Model.Capture()));
        }
        Assert.Throws<InvalidOperationException>(()=>r.Owner.AdvanceNext(16));
        Assert.Throws<InvalidOperationException>(()=>Close(r,53));
        var packet=r.Owner.Seal();Assert.Single(packet.Events);r.Owner.AcknowledgePacket(packet.Fingerprint);
        Assert.Null(r.Mechanics.Capture().PendingTrace);
    }

    [Fact]
    public void ActiveSensoryKernelReceivesExactMidpointInputWithNoEquationOrClockChange()
    {
        var r=Create(packet:8,steps:8,bodyStep:.000008,passive:false);Close(r);
        var midpoint=Model(r.Clock,false);var start=Model(r.Clock,false);var trace=r.Mechanics.Capture().PendingTrace!;
        for(int i=0;i<8;i++)
        {
            midpoint.AdvanceHeld(1,trace.AtTick(34+4*i).Bundle.BundleDisplacementNanometers);
            start.AdvanceHeld(1,trace.AtTick(32+4*i).Bundle.BundleDisplacementNanometers);
            var receipt=r.Owner.AdvanceNext(i);Assert.Equal(34+4*i,receipt.HeldBundle.ClockTick);
            Assert.Equal(Json(midpoint.Capture()),Json(r.Model.Capture()));
        }
        int met=Array.IndexOf(r.Model.Capture().LedgerLabels,"metOutwardChargeFc");Assert.True(met>=0);
        Assert.NotEqual(start.Capture().Ledgers[met],r.Model.Capture().Ledgers[met]);
        Assert.Equal(start.Capture().TimeMs,r.Model.Capture().TimeMs);
    }

    [Fact]
    public void RetryAndPacketBackpressureCannotResampleOrAdvanceTheAcceptedPrefix()
    {
        var r=Create(packet:1);Close(r);var first=r.Owner.AdvanceNext(0);string before=Json(r.Owner.Capture());
        Assert.Same(first,r.Owner.AdvanceNext(0));Assert.Throws<InvalidOperationException>(()=>r.Owner.AdvanceNext(1));
        Assert.Equal(before,Json(r.Owner.Capture()));var packet=r.Owner.Seal();
        Assert.Throws<ArgumentException>(()=>r.Owner.AcknowledgePacket("wrong"));Assert.Same(packet,r.Owner.Seal());
        r.Owner.AcknowledgePacket(packet.Fingerprint);r.Owner.AcknowledgePacket(packet.Fingerprint);
        Assert.NotNull(r.Mechanics.Capture().PendingTrace);Assert.Throws<ArgumentException>(()=>r.Owner.AdvanceNext(0));
        Assert.Equal(35,r.Owner.AdvanceNext(1).HeldBundle.ClockTick);
    }

    [Fact]
    public void PacketPartitionPreservesMidpointStatesAndOriginalEventsAcrossPhysicalIntervals()
    {
        var a=Create(packet:1);var b=Create();var left=new List<ObservedAfferentEvent>();var right=new List<ObservedAfferentEvent>();
        for(int interval=0;interval<2;interval++)
        {
            Close(a,52+interval);Close(b,52+interval);
            for(int i=0;i<16;i++)
            {
                int step=interval*16+i;a.Owner.AdvanceNext(step);b.Owner.AdvanceNext(step);
                var packet=a.Owner.Seal();left.AddRange(packet.Events);a.Owner.AcknowledgePacket(packet.Fingerprint);
                Assert.Equal(Json(a.Model.Capture()),Json(b.Model.Capture()));
            }
            var slow=b.Owner.Seal();right.AddRange(slow.Events);b.Owner.AcknowledgePacket(slow.Fingerprint);
        }
        Assert.Single(left);Assert.Equal(left,right);Assert.Equal(32,a.Model.Capture().CompletedSteps);
    }

    [Fact]
    public void ClosingHeadObservationCannotSupplyAccelerationRetroactivelyToTheMidpoints()
    {
        var a=Create();var b=Create();Close(a);
        b.Owner.ConsumeHead(Head(b.Clock.PhysicalClock,52) with{AngularVelocityWorldRadiansPerSecond=new(0,1,0)});
        for(int i=0;i<16;i++)
        {
            var left=a.Owner.AdvanceNext(i);var right=b.Owner.AdvanceNext(i);
            Assert.Equal(left.HeldBundle,right.HeldBundle);Assert.Equal(Json(a.Model.Capture()),Json(b.Model.Capture()));
        }
    }
}
