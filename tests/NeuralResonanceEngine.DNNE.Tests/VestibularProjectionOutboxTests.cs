using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.DNNE.Tests;

// Explicit transport fixtures below are not evidence of neuronal firing.
[Collection(EnvironmentVariableTestCollection.Name)]
public sealed class VestibularProjectionOutboxTests
{
    internal const string Instance="isolated-projection-source";
    internal static string Json(object value)=>JsonSerializer.Serialize(value);
    internal static SynapticConnection[] Routes()
    {
        var directory=new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null&&!File.Exists(Path.Combine(directory.FullName,"connectivity","dnne-connectivity.json")))directory=directory.Parent;
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory!.FullName,"connectivity","dnne-connectivity.json")));
        var source=Assert.Single(doc.RootElement.EnumerateArray(),r=>r.GetProperty("source").GetString()=="VestibularNuclei");
        return source.GetProperty("connections").EnumerateArray().Select(r=>new SynapticConnection(
            Enum.Parse<StructureId>(r.GetProperty("target").GetString()!),r.GetProperty("synapseId").GetGuid(),
            Enum.Parse<NTEnum>(r.GetProperty("neurotransmitter").GetString()!),r.GetProperty("projectionType").GetString()!)).ToArray();
    }
    internal static ServiceInstance[] Recipients(IEnumerable<SynapticConnection> routes)=>routes.Select(r=>r.Target).Distinct()
        .SelectMany(t=>new[]{"L","R"}.Select(side=>new ServiceInstance(t,$"isolated-{side}-{t}",side,new Uri("http://127.0.0.1:1")))).ToArray();
    private static VestibularProjectionOutbox Create(SynapticConnection[]? routes=null)
    {routes??=Routes();return new(Instance,"L",1000,.125,routes,Recipients(routes));}
    private static SpikeMessage Event(double time=1000.125)=>new()
    {
        MessageId=Guid.NewGuid(),TimestampMs=time,SourceStructure=StructureId.VestibularNuclei,SourceInstanceKey=Instance,
        SourceNeuronId="n-017",TargetStructure=StructureId.IntralaminarThalamus,TargetNeuronId="auto-IntralaminarThalamus-005",
        SynapseId=StableNeuralIdentity.Create("projection-transport-control","original-contact"),
        ProjectionId=StableNeuralIdentity.Create("projection-transport-control","original-contact"),
        Neurotransmitter=NTEnum.GLUTAMATE,VesicleQuanta=1.25f,ReuptakeRate=8,SpikeType=SpikeTypeEnum.ACTION_POTENTIAL
    };
    private static string Step(long tick,params SpikeMessage[] events)=>Json(new StructureStepResult(
        new(StructureId.VestibularNuclei,tick,events.Length,0,0,0,events.Length,256,default,new()),events,[]));
    private static VestibularProjectionFrame Stage(VestibularProjectionOutbox box,long sequence,long tick,params string[] steps)
    {
        string original=Json(new{TransportFixture=true,sequence,tick,steps});
        return box.Stage(sequence,original,VestibularProjectionOutbox.Hash(original),tick,1000+tick*.125,steps);
    }
    internal static VestibularProjectionReceipt Receipt(VestibularProjectionPacket packet)=>
        new(packet.TargetInstance,packet.SourceSequence,packet.SourceFrameSha256,packet.Sha256,packet.EventCount);
    private static void Release(VestibularProjectionOutbox box,VestibularProjectionFrame frame)
    {foreach(var packet in frame.Packets)box.Accept(Receipt(packet));box.Release(frame.Sha256);}
    private static SpikeMessage[] Messages(VestibularProjectionPacket packet)
    {
        using var doc=JsonDocument.Parse(packet.CanonicalJson);
        return doc.RootElement.GetProperty("Events").EnumerateArray().Select(e=>JsonSerializer.Deserialize<SpikeMessage>(e.GetProperty("RoutedEventJson").GetString()!)!).ToArray();
    }

    [Fact]
    public void AllSevenDeclaredPathwaysRetainOriginalTimingChemistryAndEventIdentity()
    {
        var routes=Routes();Assert.Equal(7,routes.Length);var box=Create(routes);var first=Event(1000.0625);var second=Event();
        string[] originals=[Json(first),Json(second)];var frame=Stage(box,1,1,Step(1,first,second));Assert.Equal(7,frame.Packets.Count);
        Assert.All(frame.Packets,p=>
        {
            Assert.StartsWith("isolated-L-",p.TargetInstance);Assert.Equal(2,p.EventCount);
            using var doc=JsonDocument.Parse(p.CanonicalJson);var records=doc.RootElement.GetProperty("Events").EnumerateArray().ToArray();
            Assert.Equal(originals,records.Select(r=>r.GetProperty("OriginalEventJson").GetString()));
            foreach(var pair in new[]{first,second}.Zip(Messages(p)))
            {
                Assert.Equal(pair.First.TimestampMs,pair.Second.TimestampMs);Assert.Equal(pair.First.SourceInstanceKey,pair.Second.SourceInstanceKey);
                Assert.Equal(pair.First.SourceNeuronId,pair.Second.SourceNeuronId);Assert.Equal(pair.First.SynapseId,pair.Second.SynapseId);
                Assert.Equal(pair.First.Neurotransmitter,pair.Second.Neurotransmitter);Assert.Equal(pair.First.VesicleQuanta,pair.Second.VesicleQuanta);
                Assert.Equal(pair.First.ReuptakeRate,pair.Second.ReuptakeRate);Assert.Equal(pair.First.SpikeType,pair.Second.SpikeType);
                Assert.Equal(pair.First.TargetNeuronId,pair.Second.TargetNeuronId);Assert.NotEqual(pair.First.MessageId,pair.Second.MessageId);
                Assert.Contains(routes,r=>r.Target==pair.Second.TargetStructure&&r.SynapseId==pair.Second.ProjectionId);
            }
        });
        Assert.Equal(14,frame.Packets.SelectMany(Messages).Select(m=>m.MessageId).Distinct().Count());
        Assert.Equal(originals,new[]{Json(first),Json(second)});
    }

    [Fact]
    public async Task TwoCollateralsToOneActualRecipientRemainDistinctAtAdmission()
    {
        var route=Assert.Single(Routes(),r=>r.Target==StructureId.SpinalCordMotor);
        var routes=new[]{route,route with{SynapseId=StableNeuralIdentity.Create("projection-control-second-collateral")}};
        var box=Create(routes);var frame=Stage(box,1,1,Step(1,Event()));var packet=Assert.Single(frame.Packets);
        var messages=Messages(packet);Assert.Equal(2,messages.Select(m=>m.MessageId).Distinct().Count());
        string? directory=Environment.GetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR"),instance=Environment.GetEnvironmentVariable("SERVICE_INSTANCE");
        try
        {
            Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR",Path.Combine(Path.GetTempPath(),"nre-projection-recipient",Guid.NewGuid().ToString("N")));
            Environment.SetEnvironmentVariable("SERVICE_INSTANCE",packet.TargetInstance);
            using var engine=new StructureEngine(new(StructureId.SpinalCordMotor,"Izhikevich","STDP","isolated admission control",new(1,4)));
            Assert.Equal(2,await engine.EnqueueSpikeBatchAsync(messages));
            Assert.Equal(2,await engine.EnqueueSpikeBatchAsync(messages));
            var step=await engine.ProcessStepAsync(new(1,1000.125,.125,new(),new Dictionary<BrainRhythm,double>(),0,NeuralSubstrates:NeuralSubstrateSupply.ReferenceBath),0);
            Assert.Equal(2,step.Ack.SpikeInCount); // Retry did not enqueue twice. No claim about later contact integration.
        }
        finally{Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR",directory);Environment.SetEnvironmentVariable("SERVICE_INSTANCE",instance);}
    }

    [Fact]
    public void QuietAndZeroTickFramesStillRequireEveryRecipientsProgressReceipt()
    {
        var box=Create();var empty=Stage(box,1,0);Assert.Equal(7,empty.Packets.Count);Assert.All(empty.Packets,p=>Assert.Equal(0,p.EventCount));
        Assert.Throws<InvalidOperationException>(()=>box.Release(empty.Sha256));Release(box,empty);
        var quiet=Stage(box,2,2,Step(1),Step(2));Assert.All(quiet.Packets,p=>Assert.Empty(Messages(p)));Assert.Equal(1000.25,quiet.CompletedThroughMs);
        Release(box,quiet);Assert.Equal(2,box.Capture().ReleasedTick);
    }

    [Fact]
    public void ExactRetryReturnsTheSameImmutableFrameAndCallerMutationCannotChangeIt()
    {
        var box=Create();var message=Event();string[] steps=[Step(1,message)];var frame=Stage(box,1,1,steps);
        Assert.Same(frame,Stage(box,1,1,steps));string before=Json(frame);message.VesicleQuanta=9;steps[0]=Step(1,message);
        Assert.Throws<InvalidOperationException>(()=>Stage(box,1,1,steps));Assert.Equal(before,Json(frame));
        Assert.Throws<NotSupportedException>(()=>((IList<VestibularProjectionPacket>)frame.Packets)[0]=frame.Packets[1]);
    }

    [Fact]
    public void PartialWrongOrForeignAcknowledgementsCannotReleaseTheSource()
    {
        var box=Create();var frame=Stage(box,1,1,Step(1,Event()));var first=Receipt(frame.Packets[0]);
        foreach(var wrong in new[]{first with{PacketSha256="wrong"},first with{AcceptedEvents=0},first with{TargetInstance="unknown"},first with{SourceFrameSha256="wrong"},first with{SourceSequence=2}})
            Assert.Throws<ArgumentException>(()=>box.Accept(wrong));
        box.Accept(first);box.Accept(first);Assert.Single(box.Capture().Receipts);
        Assert.Throws<InvalidOperationException>(()=>box.Release(frame.Sha256));Assert.Same(frame,box.Capture().Pending);
        foreach(var p in frame.Packets.Skip(1))box.Accept(Receipt(p));box.Release(frame.Sha256);box.Release(frame.Sha256);
        Assert.Null(box.Capture().Pending);
    }

    [Fact]
    public void StaleAcknowledgementsCannotReleaseANewerFrame()
    {
        var box=Create();var a=Stage(box,1,1,Step(1));Release(box,a);var b=Stage(box,2,2,Step(2));
        Assert.Throws<ArgumentException>(()=>box.Accept(Receipt(a.Packets[0])));Assert.Throws<InvalidOperationException>(()=>box.Release(a.Sha256));
        Assert.Same(b,box.Capture().Pending);Release(box,b);
        Assert.Throws<ArgumentException>(()=>Stage(box,2,2,Step(2)));Assert.Equal(2,box.Capture().ReleasedSequence);
    }

    [Fact]
    public void ForeignDuplicateOutOfIntervalOrMissingOriginalEventsDoNotAdvanceTheOutbox()
    {
        var box=Create();string before=Json(box.Capture());
        foreach(var time in new[]{1000,999,1000.126})
        {
            Assert.Throws<ArgumentException>(()=>Stage(box,1,1,Step(1,Event(time))));
        }
        var wrong=Event();wrong.SourceInstanceKey="isolated-foreign";Assert.Throws<ArgumentException>(()=>Stage(box,1,1,Step(1,wrong)));
        wrong=Event();wrong.IsSyntheticBackground=true;Assert.Throws<ArgumentException>(()=>Stage(box,1,1,Step(1,wrong)));
        var duplicate=Event();Assert.Throws<ArgumentException>(()=>Stage(box,1,1,Step(1,duplicate,duplicate)));
        Assert.Throws<ArgumentException>(()=>Stage(box,1,2,Step(2)));
        Assert.Throws<ArgumentException>(()=>Stage(box,1,1,Step(2)));
        Assert.Equal(before,Json(box.Capture()));
    }

    [Fact]
    public void ChemistryCannotBeChangedByRoutingToAnIncompatibleProjection()
    {
        var box=Create();var spike=Event();spike.Neurotransmitter=NTEnum.GABA;
        Assert.Throws<InvalidOperationException>(()=>Stage(box,1,1,Step(1,spike)));Assert.Null(box.Capture().Pending);
        spike=Event();spike.VesicleQuanta=.01f;
        Assert.Throws<InvalidOperationException>(()=>Stage(box,1,1,Step(1,spike)));Assert.Equal(0,box.Capture().ReleasedSequence);
    }

    [Fact]
    public void MissingRecipientsDuplicateProjectionsAndInvalidClocksAreRejected()
    {
        var routes=Routes();var recipients=Recipients(routes);
        Assert.Throws<ArgumentException>(()=>new VestibularProjectionOutbox(Instance,"L",1000,.125,routes,recipients.Where(r=>r.StructureId!=StructureId.SpinalCordMotor).ToArray()));
        Assert.Throws<ArgumentException>(()=>Create([routes[0],routes[0]]));
        foreach(double dt in new[]{0,-1,double.NaN,double.PositiveInfinity,double.Epsilon})
            Assert.Throws<ArgumentException>(()=>new VestibularProjectionOutbox(Instance,"L",1000,dt,routes,recipients));
        Assert.Throws<ArgumentException>(()=>new VestibularProjectionOutbox(Instance,"unknown",1000,.125,routes,recipients));
        Assert.Throws<InvalidOperationException>(()=>Create().Release(null!));
    }

    [Fact]
    public void RouteOrderingAndPacketPartitionDoNotChangeTheDeliveredOriginalEvents()
    {
        var routes=Routes();var recipients=Recipients(routes);var a=Create(routes);
        var b=new VestibularProjectionOutbox(Instance,"L",1000,.125,routes.Reverse().ToArray(),recipients.Reverse().ToArray());
        Assert.Equal(a.Fingerprint,b.Fingerprint);var e1=Event();var e2=Event(1000.25);
        var all=Stage(a,1,2,Step(1,e1),Step(2,e2));var first=Stage(b,1,1,Step(1,e1));Release(b,first);var second=Stage(b,2,2,Step(2,e2));
        foreach(var packet in all.Packets)
        {
            var parts=Messages(first.Packets.Single(p=>p.TargetInstance==packet.TargetInstance)).Concat(Messages(second.Packets.Single(p=>p.TargetInstance==packet.TargetInstance)));
            Assert.Equal(Messages(packet).Select(Json),parts.Select(Json));
        }
    }

    [Fact]
    public void OriginalDigestSequenceAndFrameBoundsAreRequiredBeforeAnyCommit()
    {
        var box=Create();string[] steps=[Step(1)];string original="explicit transport control";
        Assert.Throws<ArgumentException>(()=>box.Stage(1,original,"wrong",1,1000.125,steps));
        Assert.Throws<ArgumentException>(()=>box.Stage(1,original,VestibularProjectionOutbox.Hash(original),1,1000.25,steps));
        Assert.Throws<ArgumentException>(()=>Stage(box,2,1,steps));
        Assert.Throws<ArgumentException>(()=>Stage(box,1,4097,Enumerable.Range(1,4097).Select(i=>Step(i)).ToArray()));
        Assert.Equal(0,box.Capture().ReleasedSequence);Assert.Null(box.Capture().Pending);
    }
}
