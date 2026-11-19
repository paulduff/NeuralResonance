using System.Reflection;
using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

[Collection(EnvironmentVariableTestCollection.Name)]
public sealed class ConvergentAfferentIngressTests
{
    private const string Target="isolated-convergent-vestibular";
    private static readonly Guid Session=Guid.Parse("196a7555-988c-4b11-b68d-34f1946c4c01");
    private static ObservedAfferentSource Source(string name,double step=.125)=>new(Session,
        StableNeuralIdentity.Create("convergent-control-cell",name),"isolated-sensory-"+name,"fibre-"+name,"a",new string('A',64),0,step);
    private static ObservedAfferentRoute Route(int cell=17,double delay=.25)=>new(StableNeuralIdentity.Create("convergent-control-projection",cell),Target,cell,delay,1,8);
    private static ConvergentAfferentInput Input(ObservedAfferentSource s,params ObservedAfferentRoute[] routes)=>new(s,routes.Length==0?[Route()]:routes);
    private static ObservedAfferentEvent Event(ObservedAfferentSource s,long ordinal=1,long step=2,double time=.1875)=>new(s.EventId(ordinal),ordinal,step,time);
    private static TickSignal Tick(long n,double time,double dt)=>new(n,time,dt,new(),new Dictionary<BrainRhythm,double>(),0,NeuralSubstrates:NeuralSubstrateSupply.ReferenceBath);
    private static ModelNeuron[] Cells(StructureEngine e)=>(ModelNeuron[])typeof(StructureEngine).GetField("_neurons",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(e)!;
    private static string Electrical(StructureEngine e)=>JsonSerializer.Serialize(Cells(e).Select(n=>new{n.MembranePotentialMillivolts,n.NetSynapticCurrent,Receptors=n.CaptureCorticalReceptors()}));
    private sealed class Fixture : IDisposable
    {
        private readonly string? _directory=Environment.GetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR"),_instance=Environment.GetEnvironmentVariable("SERVICE_INSTANCE");
        internal StructureEngine Engine { get; }
        internal ConvergentAfferentIngress Ingress { get; }
        internal Fixture(ConvergentAfferentInput[]? inputs=null,int capacity=4096,string? directory=null,StructureId structure=StructureId.VestibularNuclei)
        {
            Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR",directory??Path.Combine(Path.GetTempPath(),"nre-convergent-afferent",Guid.NewGuid().ToString("N")));
            Environment.SetEnvironmentVariable("SERVICE_INSTANCE",Target);
            try
            {
                Ingress=new(inputs??[Input(Source("a")),Input(Source("b",.0625))]);
                Engine=new(new(structure,"LIF","STDP","isolated convergent observed input",new(1,5),capacity),null,diagnosticAfferentConvergence:Ingress);
            }
            catch{Restore();throw;}
        }
        private void Restore(){Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR",_directory);Environment.SetEnvironmentVariable("SERVICE_INSTANCE",_instance);}
        public void Dispose(){try{Engine.Dispose();}finally{Restore();}}
    }

    [Fact]
    public void CanonicalInputOrderDoesNotChangeContactOrSourceIdentity()
    {
        var a=Input(Source("a"),Route(),Route(18));var b=Input(Source("b"));
        var first=new ConvergentAfferentIngress([a,b]);var second=new ConvergentAfferentIngress([b,a with{Routes=a.Routes.Reverse().ToArray()}]);
        Assert.Equal(first.Fingerprint,second.Fingerprint);
        var changed=new ConvergentAfferentIngress([a,b with{Routes=[Route(delay:.5)]}]);Assert.NotEqual(first.Fingerprint,changed.Fingerprint);
        Assert.Equal(2,first.Inputs.Count);Assert.Equal(0,first.Capture()[0].CompletedStep);
        Assert.Throws<NotSupportedException>(()=>((IList<ConvergentAfferentInput>)first.Inputs)[0]=b);
    }

    [Fact]
    public void DuplicateFibresDifferentEpochsAndUnboundedDeclarationsAreRejected()
    {
        var a=Input(Source("a"));var b=Input(Source("b"));
        foreach(var bad in new[]{b with{Source=b.Source with{SessionId=Guid.NewGuid()}},b with{Source=b.Source with{EpochMs=1}},
            b with{Source=b.Source with{CellId=a.Source.CellId}},b with{Source=b.Source with{InstanceKey=a.Source.InstanceKey,NeuronId=a.Source.NeuronId}}})
            Assert.Throws<ArgumentException>(()=>new ConvergentAfferentIngress([a,bad]));
        Assert.Throws<ArgumentException>(()=>new ConvergentAfferentIngress([a]));
        Assert.Throws<ArgumentException>(()=>new ConvergentAfferentIngress([a,a]));
        Assert.Throws<ArgumentException>(()=>new ConvergentAfferentIngress(Enumerable.Repeat(a,257)));
        Assert.Throws<ArgumentException>(()=>new ConvergentAfferentIngress([a,b with{Routes=[]}]));
    }

    [Fact]
    public async Task FastFibreCannotAdvanceTheNucleusBeyondTheSlowFibre()
    {
        using var f=new Fixture();var a=f.Ingress.Inputs[0].Source;var b=f.Ingress.Inputs[1].Source;
        var first=await f.Engine.EnqueueConvergentAfferentPacketAsync(new(a,0,8,[]));
        Assert.Equal(.25,first.TargetSafeThroughMs);string before=Electrical(f.Engine);
        await Assert.ThrowsAsync<InvalidOperationException>(async()=>await f.Engine.ProcessTickAsync(Tick(1,.5,.5)));
        Assert.Equal(before,Electrical(f.Engine));
        await f.Engine.EnqueueConvergentAfferentPacketAsync(new(b,0,8,[]));Assert.Equal(.75,f.Ingress.SafeThroughMs);
        await f.Engine.ProcessTickAsync(Tick(1,.5,.5));
        await Assert.ThrowsAsync<InvalidOperationException>(async()=>await f.Engine.ProcessTickAsync(Tick(2,.75,.25)));
        await f.Engine.EnqueueConvergentAfferentPacketAsync(new(b,8,16,[]));await f.Engine.ProcessTickAsync(Tick(2,1,.5));
        Assert.Equal(new long[]{8,16},f.Ingress.Capture().Select(p=>p.CompletedStep));
    }

    [Fact]
    public async Task TwoFibresReachTheSameCellThroughDistinctActualContacts()
    {
        using var f=new Fixture();var a=f.Ingress.Inputs[0].Source;var b=f.Ingress.Inputs[1].Source;
        var seen=new List<RubralInputDelivery>();f.Engine.RubralInputObserver=seen.Add;
        var ra=await f.Engine.EnqueueConvergentAfferentPacketAsync(new(a,0,4,[Event(a)]));
        var rb=await f.Engine.EnqueueConvergentAfferentPacketAsync(new(b,0,8,[Event(b,step:4,time:.21875)]));
        await f.Engine.ProcessTickAsync(Tick(1,.5,.5));
        Assert.Equal(2,seen.Count);Assert.All(seen,d=>Assert.Equal(17,d.ActualTargetIndex));
        Assert.Equal(new[]{.4375,.46875},seen.Select(d=>d.AppliedAtMs));
        Assert.Equal(new[]{ra.Deliveries[0].ContactId,rb.Deliveries[0].ContactId},seen.Select(d=>d.ReceivingContactId));
        Assert.NotEqual(ra.Deliveries[0].ContactId,rb.Deliveries[0].ContactId);
        Assert.True(Cells(f.Engine)[17].CaptureCorticalReceptors().Ampa>0);Assert.Equal(0,Cells(f.Engine)[18].CaptureCorticalReceptors().Ampa);
    }

    [Fact]
    public async Task QuietCompanionCarriesTimeWithoutCreatingARelease()
    {
        using var f=new Fixture();var a=f.Ingress.Inputs[0].Source;var b=f.Ingress.Inputs[1].Source;
        var seen=new List<RubralInputDelivery>();f.Engine.RubralInputObserver=seen.Add;
        var active=await f.Engine.EnqueueConvergentAfferentPacketAsync(new(a,0,4,[Event(a)]));
        var quiet=await f.Engine.EnqueueConvergentAfferentPacketAsync(new(b,0,8,[]));
        Assert.Empty(quiet.Deliveries);await f.Engine.ProcessTickAsync(Tick(1,.5,.5));
        Assert.Equal(active.Deliveries[0].DeliveryId,Assert.Single(seen).OriginalMessageId);
        Assert.Equal(a.EventId(1),active.Deliveries[0].OriginalEventId);
        Assert.Equal(a.InstanceKey,JsonSerializer.Deserialize<SpikeMessage>(seen[0].OriginalJson)!.SourceInstanceKey);
        Assert.Equal(0,f.Ingress.Capture()[1].LastOrdinal);
    }

    [Fact]
    public async Task ExactRetryRetainsItsOriginalReceiptAfterAnotherFibreAdvances()
    {
        using var f=new Fixture();var a=f.Ingress.Inputs[0].Source;var b=f.Ingress.Inputs[1].Source;
        var packet=new ObservedAfferentPacket(a,0,4,[Event(a)]);var receipt=await f.Engine.EnqueueConvergentAfferentPacketAsync(packet);
        await f.Engine.EnqueueConvergentAfferentPacketAsync(new(b,0,8,[]));Assert.Equal(.75,f.Ingress.SafeThroughMs);
        Assert.Same(receipt,await f.Engine.EnqueueConvergentAfferentPacketAsync(packet));Assert.Equal(.25,receipt.TargetSafeThroughMs);
        await f.Engine.ProcessTickAsync(Tick(1,.5,.5));Assert.Same(receipt,await f.Engine.EnqueueConvergentAfferentPacketAsync(packet));
        await f.Engine.EnqueueConvergentAfferentPacketAsync(new(a,4,8,[]));
        await Assert.ThrowsAsync<ArgumentException>(async()=>await f.Engine.EnqueueConvergentAfferentPacketAsync(packet));
    }

    [Fact]
    public async Task UnknownChangedSkippedAndMissingOrdinalPacketsDoNotChangeEitherFibre()
    {
        using var f=new Fixture();var a=f.Ingress.Inputs[0].Source;
        await f.Engine.EnqueueConvergentAfferentPacketAsync(new(a,0,4,[Event(a)]));string before=JsonSerializer.Serialize(f.Ingress.Capture());
        foreach(var packet in new[]{new ObservedAfferentPacket(Source("unknown"),0,4,[]),new(a with{StepMs=.25},4,8,[]),
            new(a,5,8,[]),new(a,4,8,[Event(a,ordinal:3,step:6,time:.6875)]),new(a,0,4,[Event(a,time:.2)])})
            await Assert.ThrowsAsync<ArgumentException>(async()=>await f.Engine.EnqueueConvergentAfferentPacketAsync(packet));
        Assert.Equal(before,JsonSerializer.Serialize(f.Ingress.Capture()));
    }

    [Fact]
    public async Task SharedQueueOverloadPreservesBothSourceWatermarksAndTheAcceptedPacket()
    {
        using var f=new Fixture(capacity:1);var a=f.Ingress.Inputs[0].Source;var b=f.Ingress.Inputs[1].Source;
        var packet=new ObservedAfferentPacket(a,0,4,[Event(a)]);var receipt=await f.Engine.EnqueueConvergentAfferentPacketAsync(packet);
        string before=JsonSerializer.Serialize(f.Ingress.Capture());
        await Assert.ThrowsAsync<StructureIngressOverloadException>(async()=>await f.Engine.EnqueueConvergentAfferentPacketAsync(new(b,0,8,[Event(b,step:4,time:.21875)])));
        Assert.Equal(before,JsonSerializer.Serialize(f.Ingress.Capture()));Assert.Same(receipt,await f.Engine.EnqueueConvergentAfferentPacketAsync(packet));
        Assert.Equal(0,f.Ingress.Capture()[1].CompletedStep);Assert.Null(f.Ingress.Capture()[1].LastReceipt);
    }

    [Fact]
    public async Task CancellationAndLegacyIngressCannotBypassJointOwnership()
    {
        using var f=new Fixture();var a=f.Ingress.Inputs[0].Source;using var stop=new CancellationTokenSource();stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async()=>await f.Engine.EnqueueConvergentAfferentPacketAsync(new(a,0,4,[]),stop.Token));
        await Assert.ThrowsAsync<InvalidOperationException>(async()=>await f.Engine.EnqueueSpikeAsync(Route().Message(a,Event(a))));
        await Assert.ThrowsAsync<InvalidOperationException>(async()=>await f.Engine.EnqueueObservedAfferentPacketAsync(new(a,0,4,[])));
        Assert.All(f.Ingress.Capture(),p=>Assert.Equal(0,p.CompletedStep));
    }

    [Fact]
    public void BindingRejectsExistingSavesOtherRegionsAndReusedOwners()
    {
        string folder=Path.Combine(Path.GetTempPath(),"nre-convergent-preserved",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder,"original.txt"),"unchanged");
        Assert.Throws<InvalidOperationException>(()=>new Fixture(directory:folder));Assert.Equal("unchanged",File.ReadAllText(Path.Combine(folder,"original.txt")));
        Assert.Throws<InvalidOperationException>(()=>new Fixture(structure:StructureId.RedNucleus));
        using var f=new Fixture();
        Assert.Throws<InvalidOperationException>(()=>new StructureEngine(new(StructureId.VestibularNuclei,"LIF","STDP","test",new(1,5)),null,diagnosticAfferentConvergence:f.Ingress));
    }

    [Fact]
    public async Task SimultaneousReleasesHaveTheSameOrderAndStateForReversedPacketArrival()
    {
        async Task<(string Deliveries,string Cells)> Run(bool reverse,bool partition)
        {
            var a=Source("a");var b=Source("b");using var f=new Fixture([Input(a),Input(b)]);
            var deliveries=new List<RubralInputDelivery>();f.Engine.RubralInputObserver=deliveries.Add;
            foreach(var s in reverse?new[]{b,a}:new[]{a,b})
            {
                if(partition)await f.Engine.EnqueueConvergentAfferentPacketAsync(new(s,0,1,[]));
                await f.Engine.EnqueueConvergentAfferentPacketAsync(new(s,partition?1:0,4,[Event(s)]));
            }
            await f.Engine.ProcessTickAsync(Tick(1,.5,.5));
            Assert.Equal(new[]{a.InstanceKey,b.InstanceKey},deliveries.Select(d=>JsonSerializer.Deserialize<SpikeMessage>(d.OriginalJson)!.SourceInstanceKey));
            return(JsonSerializer.Serialize(deliveries),Electrical(f.Engine));
        }
        var original=await Run(false,false);Assert.Equal(original,await Run(true,false));Assert.Equal(original,await Run(true,true));
    }

    [Fact]
    public async Task UnknownExactBoundaryWaitsForTheNextPacketBeforeRecipientIntegration()
    {
        var a=Source("a");var b=Source("b");using var f=new Fixture([Input(a,Route(delay:0)),Input(b)]);
        await f.Engine.EnqueueConvergentAfferentPacketAsync(new(a,0,2,[]));await f.Engine.EnqueueConvergentAfferentPacketAsync(new(b,0,8,[]));
        string before=Electrical(f.Engine);await Assert.ThrowsAsync<InvalidOperationException>(async()=>await f.Engine.ProcessTickAsync(Tick(1,.25,.25)));
        Assert.Equal(before,Electrical(f.Engine));var seen=new List<RubralInputDelivery>();f.Engine.RubralInputObserver=seen.Add;
        await f.Engine.EnqueueConvergentAfferentPacketAsync(new(a,2,4,[Event(a,step:3,time:.25)]));
        await f.Engine.ProcessTickAsync(Tick(1,.375,.375));Assert.Equal(.25,Assert.Single(seen).AppliedAtMs);
    }

    [Fact]
    public async Task NonzeroEpochAndDifferentFibreGridsRetainTheirOriginalTimes()
    {
        var a=Source("a") with{EpochMs=1000};var b=Source("b",.0625) with{EpochMs=1000};using var f=new Fixture([Input(a),Input(b)]);
        var seen=new List<RubralInputDelivery>();f.Engine.RubralInputObserver=seen.Add;
        await f.Engine.EnqueueConvergentAfferentPacketAsync(new(a,0,4,[Event(a,time:1000.1875)]));
        await f.Engine.EnqueueConvergentAfferentPacketAsync(new(b,0,8,[Event(b,step:4,time:1000.21875)]));
        await f.Engine.ProcessTickAsync(Tick(1,1000.5,.5));Assert.Equal(new[]{1000.4375,1000.46875},seen.Select(d=>d.AppliedAtMs));
        Assert.Equal(new[]{4L,8L},f.Ingress.Capture().Select(p=>p.CompletedStep));
    }

    [Fact]
    public async Task TwoActualPassiveObservationsConvergeWithoutChangingTheirEvents()
    {
        AvatarAfferentPacketSource Create(string name)
        {
            var template=AfferentEventObserverTests.Model(session:Session);
            var model=new AvatarVestibularAfferentDynamics(Session,Source(name).CellId,template.Kernel,template.Settings,0,0,
                [new("p",0,0,5,140)],[new("p",new(.2,.3,.4),new(.2,.3,.4,.5))],
                [new("b",-45,new(.2,.3,.4,.5,.6,.7)),new("a",-65,new(.2,.3,.4,.5,.6,.7))]);
            return new(new AvatarAfferentEventObserver(model,new("isolated-sensory-"+name,"fibre-"+name,"a",-60)));
        }
        // Analytic passive crossings establish original-source delivery only.
        var left=Create("a");var right=Create("b");
        for(int i=0;i<100;i++){left.AdvanceNext(i,0);right.AdvanceNext(i,0);}
        var a=left.Seal();var b=right.Seal();Assert.Single(a.Events);Assert.Single(b.Events);
        using var f=new Fixture([Input(a.Source,Route(delay:.125)),Input(b.Source,Route(delay:.125))]);
        var seen=new List<RubralInputDelivery>();f.Engine.RubralInputObserver=seen.Add;
        var ra=await f.Engine.EnqueueConvergentAfferentPacketAsync(a);var rb=await f.Engine.EnqueueConvergentAfferentPacketAsync(b);
        await f.Engine.ProcessTickAsync(Tick(1,.2,.2));Assert.Equal(2,seen.Count);
        Assert.Equal(new[]{a.Events[0].CrossingTimeMs+.125,b.Events[0].CrossingTimeMs+.125},seen.Select(d=>d.AppliedAtMs));
        Assert.Equal(new[]{ra.Deliveries[0].DeliveryId,rb.Deliveries[0].DeliveryId},seen.Select(d=>d.OriginalMessageId));
        Assert.All(seen,d=>Assert.Equal(17,d.ActualTargetIndex));
        left.Acknowledge(ra.PacketFingerprint);right.Acknowledge(rb.PacketFingerprint);
    }
}
