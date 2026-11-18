using System.Reflection;
using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

[Collection(EnvironmentVariableTestCollection.Name)]
public sealed class ObservedAfferentIngressTests
{
    private const string Target="isolated-observed-vestibular";
    private static ObservedAfferentSource Source()=>new(Guid.Parse("a893dff4-5e55-4a61-b2a6-09b6bd140b0c"),
        Guid.Parse("37bfe39f-1c99-443c-b937-2f0747d8e09a"),"isolated-sensory-owner","afferent-7","central-output",new string('A',64),0,.125);
    private static ObservedAfferentEvent Event(ObservedAfferentSource s,long ordinal=1,long step=2,double time=.1875)=>new(s.EventId(ordinal),ordinal,step,time);
    private static ObservedAfferentRoute Route(int target=17,double delay=.25)=>new(StableNeuralIdentity.Create("sensory-test-projection",target),Target,target,delay,1,8);
    private static TickSignal Tick(long n,double time,double dt)=>new(n,time,dt,new(),new Dictionary<BrainRhythm,double>(),0){NeuralSubstrates=NeuralSubstrateSupply.ReferenceBath};
    private static ModelNeuron[] Cells(StructureEngine e)=>(ModelNeuron[])typeof(StructureEngine).GetField("_neurons",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(e)!;
    private sealed class Fixture : IDisposable
    {
        private readonly string? _directory=Environment.GetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR"),_instance=Environment.GetEnvironmentVariable("SERVICE_INSTANCE");
        internal StructureEngine Engine { get; }
        internal IsolatedAfferentIngress Ingress { get; }
        internal Fixture(ObservedAfferentSource? source=null,ObservedAfferentRoute[]? routes=null,int capacity=4096)
        {
            Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR",Path.Combine(Path.GetTempPath(),"nre-observed-afferent",Guid.NewGuid().ToString("N")));
            Environment.SetEnvironmentVariable("SERVICE_INSTANCE",Target);
            try
            {
                Ingress=new(source??Source(),routes??[Route()]);
                Engine=new(new(StructureId.VestibularNuclei,"LIF","STDP","isolated observed event admission",new(1,5),capacity),null,diagnosticAfferentIngress:Ingress);
            }
            catch {Restore();throw;}
        }
        private void Restore(){Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR",_directory);Environment.SetEnvironmentVariable("SERVICE_INSTANCE",_instance);}
        public void Dispose(){try{Engine.Dispose();}finally{Restore();}}
    }

    [Fact]
    public void PacketRoundTripRetainsOriginalEventClockAndFingerprint()
    {
        var s=Source();var p=new ObservedAfferentPacket(s,0,4,[Event(s)]);
        var restored=JsonSerializer.Deserialize<ObservedAfferentPacket>(JsonSerializer.Serialize(p))!;
        Assert.Equal(p.Fingerprint,restored.Fingerprint);Assert.Equal(p.Source,restored.Source);Assert.Equal(p.Events,restored.Events);
        Assert.Equal(.5,restored.EndMs);Assert.Equal(.1875,restored.Events[0].CrossingTimeMs);
    }
    [Fact]
    public void PacketCopiesCallerArrayAndExposesReadOnlyEvents()
    {
        var s=Source();var a=new[]{Event(s)};var p=new ObservedAfferentPacket(s,0,4,a);a[0]=Event(s,time:.2);
        Assert.Equal(.1875,p.Events[0].CrossingTimeMs);
        Assert.Throws<NotSupportedException>(()=>((IList<ObservedAfferentEvent>)p.Events)[0]=a[0]);
    }
    [Fact]
    public void InvalidEpochIdentityClockAndEventIntervalsAreRejected()
    {
        var s=Source();
        foreach(var invalid in new[]{s with {SessionId=Guid.Empty},s with {StepMs=0},s with {EpochMs=double.NaN},s with {ObservationFingerprint="bad"}})
            Assert.Throws<ArgumentException>(()=>new ObservedAfferentPacket(invalid,0,4,[]));
        foreach(var invalid in new[]{Event(s) with {MessageId=Guid.Empty},Event(s) with {Ordinal=0},Event(s) with {CompletedStep=0},Event(s) with {CrossingTimeMs=.3},Event(s) with {CrossingTimeMs=double.NaN}})
            Assert.Throws<ArgumentException>(()=>new ObservedAfferentPacket(s,0,4,[invalid]));
        Assert.Throws<ArgumentException>(()=>new ObservedAfferentPacket(s,0,0,[]));
        Assert.Throws<ArgumentException>(()=>new ObservedAfferentPacket(s,0,4,[Event(s),Event(s)]));
    }
    [Fact]
    public void AnatomicalContactSurvivesNewEpochButEventDeliveryDoesNot()
    {
        var a=Source();var b=a with {SessionId=Guid.NewGuid()};var route=Route();var first=route.Message(a,Event(a));var next=route.Message(b,Event(b));
        Assert.Equal(first.SynapseId,next.SynapseId);Assert.NotEqual(first.MessageId,next.MessageId);
        Assert.NotEqual(first.SynapseId,Route(18).Message(a,Event(a)).SynapseId);
        Assert.Equal(.1875,first.TimestampMs);Assert.False(first.IsSyntheticBackground);Assert.False(first.IsRightingCircuitSpike);
    }
    [Fact]
    public void CanonicalRoutesPreserveFingerprintAndRejectDuplicateContacts()
    {
        var s=Source();var a=Route();var b=Route(18);
        Assert.Equal(new IsolatedAfferentIngress(s,[a,b]).Fingerprint,new IsolatedAfferentIngress(s,[b,a]).Fingerprint);
        Assert.NotEqual(new IsolatedAfferentIngress(s,[a]).Fingerprint,new IsolatedAfferentIngress(s,[a with {RemainingDelayMs=.5}]).Fingerprint);
        Assert.Throws<ArgumentException>(()=>new IsolatedAfferentIngress(s,[a,a]));
        Assert.Throws<ArgumentException>(()=>new IsolatedAfferentIngress(s,[]));
        Assert.Throws<ArgumentException>(()=>new IsolatedAfferentIngress(s,[a with {RemainingDelayMs=-1}]));
    }
    [Fact]
    public async Task QuietIntervalsAdvancePermissionWithoutCreatingSynapticInput()
    {
        using var f=new Fixture();var seen=new List<InboundSynapticObservation>();f.Engine.InboundIntegrationObserver=seen.Add;
        var receipt=await f.Engine.EnqueueObservedAfferentPacketAsync(new(f.Ingress.Source,0,4,[]));
        Assert.Empty(receipt.Deliveries);Assert.Equal(.75,receipt.TargetSafeThroughMs);
        await f.Engine.ProcessTickAsync(Tick(1,.75,.75));Assert.Empty(seen);Assert.Equal(0,f.Ingress.LastOrdinal);
    }
    [Fact]
    public async Task UnknownSensoryFutureStopsTargetBeforeAnyClockOrCellChange()
    {
        using var f=new Fixture();var before=Cells(f.Engine).Select(c=>c.CaptureCorticalReceptors()).ToArray();
        await Assert.ThrowsAsync<InvalidOperationException>(async()=>await f.Engine.ProcessTickAsync(Tick(1,.5,.5)));
        Assert.Equal(before,Cells(f.Engine).Select(c=>c.CaptureCorticalReceptors()).ToArray());
        await f.Engine.EnqueueObservedAfferentPacketAsync(new(f.Ingress.Source,0,4,[]));
        await f.Engine.ProcessTickAsync(Tick(1,.5,.5)); // same tick still admissible
    }
    [Fact]
    public async Task ExactPacketRetryDoesNotReleaseTwiceEvenAfterTargetAdvances()
    {
        using var f=new Fixture();var p=new ObservedAfferentPacket(f.Ingress.Source,0,4,[Event(f.Ingress.Source)]);
        var seen=new List<InboundSynapticObservation>();f.Engine.InboundIntegrationObserver=seen.Add;
        var first=await f.Engine.EnqueueObservedAfferentPacketAsync(p);Assert.Same(first,await f.Engine.EnqueueObservedAfferentPacketAsync(p));
        await f.Engine.ProcessTickAsync(Tick(1,.5,.5));Assert.Single(seen);
        Assert.Same(first,await f.Engine.EnqueueObservedAfferentPacketAsync(p));await f.Engine.ProcessTickAsync(Tick(2,.75,.25));Assert.Single(seen);
    }
    [Fact]
    public async Task ChangedRetryAndSkippedClockOrEventLeaveProgressUnchanged()
    {
        using var f=new Fixture();var s=f.Ingress.Source;var p=new ObservedAfferentPacket(s,0,4,[Event(s)]);
        var first=await f.Engine.EnqueueObservedAfferentPacketAsync(p);
        foreach(var bad in new[]{new ObservedAfferentPacket(s,0,4,[Event(s,time:.2)]),new(s,5,8,[]),new(s,4,8,[Event(s,3,6,.6875)]),new(s with {CellId=Guid.NewGuid()},4,8,[])})
            await Assert.ThrowsAsync<ArgumentException>(async()=>await f.Engine.EnqueueObservedAfferentPacketAsync(bad));
        Assert.Equal(4,f.Ingress.CompletedStep);Assert.Equal(1,f.Ingress.LastOrdinal);Assert.Same(first,f.Ingress.LastReceipt);
        await f.Engine.EnqueueObservedAfferentPacketAsync(new(s,4,8,[Event(s,2,6,.6875)]));Assert.Equal(2,f.Ingress.LastOrdinal);
    }
    [Fact]
    public async Task QueueOverloadRejectsEveryContactAndWatermarkAtomically()
    {
        using var f=new Fixture(routes:[Route(),Route(18)],capacity:1);var p=new ObservedAfferentPacket(f.Ingress.Source,0,4,[Event(f.Ingress.Source)]);
        await Assert.ThrowsAsync<StructureIngressOverloadException>(async()=>await f.Engine.EnqueueObservedAfferentPacketAsync(p));
        Assert.Equal(0,f.Ingress.CompletedStep);Assert.Equal(0,f.Ingress.LastOrdinal);Assert.Null(f.Ingress.LastReceipt);
        var seen=new List<InboundSynapticObservation>();f.Engine.InboundIntegrationObserver=seen.Add;
        await f.Engine.EnqueueObservedAfferentPacketAsync(new(f.Ingress.Source,0,4,[]));await f.Engine.ProcessTickAsync(Tick(1,.75,.75));Assert.Empty(seen);
    }
    [Fact]
    public async Task ActualReceiverUsesDeclaredCellAndFractionalConductionDelay()
    {
        using var f=new Fixture();var s=f.Ingress.Source;var seen=new List<RubralInputDelivery>();f.Engine.RubralInputObserver=seen.Add;
        var receipt=await f.Engine.EnqueueObservedAfferentPacketAsync(new(s,0,4,[Event(s)]));
        await f.Engine.ProcessTickAsync(Tick(1,.375,.375));Assert.Empty(seen);
        await f.Engine.ProcessTickAsync(Tick(2,.5,.125));var delivery=Assert.Single(seen);
        Assert.Equal(17,delivery.ActualTargetIndex);Assert.Equal(.4375,delivery.ArrivalAtMs);Assert.Equal(.4375,delivery.AppliedAtMs);
        Assert.Equal(receipt.Deliveries[0].ContactId,delivery.ReceivingContactId);
        var message=JsonSerializer.Deserialize<SpikeMessage>(delivery.OriginalJson)!;
        Assert.Equal(.1875,message.TimestampMs);Assert.Equal(s.NeuronId,message.SourceNeuronId);Assert.Equal(s.InstanceKey,message.SourceInstanceKey);
        Assert.True(Cells(f.Engine)[17].CaptureCorticalReceptors().Ampa>0);Assert.Equal(0,Cells(f.Engine)[18].CaptureCorticalReceptors().Ampa);
    }
    [Fact]
    public async Task DeclaredBranchesRetainOneOriginalEventAndSeparateContacts()
    {
        using var f=new Fixture(routes:[Route(),Route(18,.5)]);var s=f.Ingress.Source;
        var receipt=await f.Engine.EnqueueObservedAfferentPacketAsync(new(s,0,4,[Event(s)]));
        Assert.Equal(2,receipt.Deliveries.Count);Assert.All(receipt.Deliveries,d=>Assert.Equal(s.EventId(1),d.OriginalEventId));
        Assert.Equal(2,receipt.Deliveries.Select(d=>d.ContactId).Distinct().Count());Assert.Equal(2,receipt.Deliveries.Select(d=>d.DeliveryId).Distinct().Count());
        var seen=new List<InboundSynapticObservation>();f.Engine.InboundIntegrationObserver=seen.Add;
        await f.Engine.ProcessTickAsync(Tick(1,.75,.75));Assert.Equal(new[]{17,18},seen.Select(e=>e.TargetNeuronIndex).ToArray());
        Assert.Equal(new[]{.4375,.6875},seen.Select(e=>e.ProcessingTimestampMs).ToArray());
    }
    [Fact]
    public async Task LegacyStimulusEntryCannotBypassObservedOwnership()
    {
        using var f=new Fixture();var s=f.Ingress.Source;
        await Assert.ThrowsAsync<InvalidOperationException>(async()=>await f.Engine.EnqueueSpikeAsync(Route().Message(s,Event(s))));
        Assert.Equal(0,f.Ingress.CompletedStep);
    }
    [Fact]
    public async Task CancellationDoesNotConsumeAPacketOrAdvanceItsHorizon()
    {
        using var f=new Fixture();using var token=new CancellationTokenSource();token.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async()=>await f.Engine.EnqueueObservedAfferentPacketAsync(new(f.Ingress.Source,0,4,[]),token.Token));
        Assert.Equal(0,f.Ingress.CompletedStep);Assert.Null(f.Ingress.LastReceipt);
    }
    [Fact]
    public void WrongRecipientInstanceAndReusedOwnerCannotBeBound()
    {
        Assert.Throws<InvalidOperationException>(()=>new Fixture(routes:[Route() with {TargetInstanceKey="some-other-instance"}]));
        using var f=new Fixture();
        Assert.Throws<InvalidOperationException>(()=>new StructureEngine(new(StructureId.VestibularNuclei,"LIF","STDP","test",new(1,5)),null,diagnosticAfferentIngress:f.Ingress));
    }
    [Fact]
    public async Task StalePreviouslyAcknowledgedPacketCannotReplayAfterANewerPacket()
    {
        using var f=new Fixture();var a=new ObservedAfferentPacket(f.Ingress.Source,0,4,[]);var b=new ObservedAfferentPacket(f.Ingress.Source,4,8,[]);
        await f.Engine.EnqueueObservedAfferentPacketAsync(a);await f.Engine.EnqueueObservedAfferentPacketAsync(b);
        await Assert.ThrowsAsync<ArgumentException>(async()=>await f.Engine.EnqueueObservedAfferentPacketAsync(a));Assert.Equal(8,f.Ingress.CompletedStep);
    }
    [Fact]
    public async Task DifferentPacketPartitionsPreserveSameEventDeliveryAndReceivingState()
    {
        async Task<(CorticalReceptorState,Guid)> Run(bool split)
        {
            using var f=new Fixture();var s=f.Ingress.Source;
            if(split)await f.Engine.EnqueueObservedAfferentPacketAsync(new(s,0,1,[]));
            var r=await f.Engine.EnqueueObservedAfferentPacketAsync(new(s,split?1:0,4,[Event(s)]));
            await f.Engine.ProcessTickAsync(Tick(1,.75,.75));return(Cells(f.Engine)[17].CaptureCorticalReceptors(),r.Deliveries[0].DeliveryId);
        }
        Assert.Equal(await Run(false),await Run(true));
    }
    [Fact]
    public void AvatarOutboxStopsBeforeOverrunAndRequiresExactAcknowledgement()
    {
        var m=AfferentEventObserverTests.Model();var o=new AvatarAfferentEventObserver(m,new("isolated-source","a","a",-60));var source=new AvatarAfferentPacketSource(o,1);
        source.AdvanceNext(0,0);string before=JsonSerializer.Serialize(m.Capture());
        Assert.Throws<InvalidOperationException>(()=>source.AdvanceNext(1,0));Assert.Equal(before,JsonSerializer.Serialize(m.Capture()));
        var p=source.Seal();Assert.Empty(p.Events);Assert.Same(p,source.Seal());
        Assert.Throws<ArgumentException>(()=>source.Acknowledge("wrong"));Assert.Throws<InvalidOperationException>(()=>source.AdvanceNext(1,0));
        source.Acknowledge(p.Fingerprint);source.Acknowledge(p.Fingerprint);source.AdvanceNext(1,0);Assert.Equal(2,source.Seal().CompletedStep);
    }
    [Fact]
    public void AvatarOutboxRetainsActualCrossingAndExactSourceRetry()
    {
        var m=AfferentEventObserverTests.Model();var o=new AvatarAfferentEventObserver(m,new("isolated-source","a","a",-60));var source=new AvatarAfferentPacketSource(o);
        for(int i=0;i<100;i++){var receipt=source.AdvanceNext(i,0);Assert.Same(receipt,source.AdvanceNext(i,0));}
        var p=source.Seal();var e=Assert.Single(p.Events);Assert.InRange(e.CrossingTimeMs,.04,.05);
        Assert.Equal(o.Capture().EventCount,e.Ordinal);Assert.Equal(p.Source.EventId(e.Ordinal),e.MessageId);Assert.Equal(100,m.Capture().CompletedSteps);
    }
    [Fact]
    public async Task ActualPassiveObservationReachesOneDeclaredReceivingContact()
    {
        // An analytic passive threshold control checks connection mechanics;
        // it is not physiological afferent firing or Avatar balance evidence.
        var m=AfferentEventObserverTests.Model();var o=new AvatarAfferentEventObserver(m,new("isolated-source","a","a",-60));var source=new AvatarAfferentPacketSource(o);
        for(int i=0;i<100;i++)source.AdvanceNext(i,0);
        var p=source.Seal();using var f=new Fixture(p.Source);var seen=new List<RubralInputDelivery>();f.Engine.RubralInputObserver=seen.Add;
        var receipt=await f.Engine.EnqueueObservedAfferentPacketAsync(p);source.Acknowledge(receipt.PacketFingerprint);
        await f.Engine.ProcessTickAsync(Tick(1,receipt.TargetSafeThroughMs,receipt.TargetSafeThroughMs));
        var delivery=Assert.Single(seen);Assert.Equal(17,delivery.ActualTargetIndex);
        Assert.Equal(p.Events[0].CrossingTimeMs+.25,delivery.AppliedAtMs);Assert.Equal(receipt.Deliveries[0].ContactId,delivery.ReceivingContactId);
    }
}
