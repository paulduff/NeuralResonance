using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.DNNE.Tests;

// Source fixtures exercise ownership. They are not calibrated vestibular firing.
[Collection(EnvironmentVariableTestCollection.Name)]
public sealed class ReticulospinalCircuitTests
{
    private const string Source="isolated-recurrent-control";
    private static string Json(object value)=>JsonSerializer.Serialize(value);
    private static string Hash(string value)=>VestibularProjectionOutbox.Hash(value);
    private static string DirectoryName()=>Path.Combine(Path.GetTempPath(),"nre-reticulospinal",Guid.NewGuid().ToString("N"));
    private static SynapticConnection[] Catalog(StructureId structure)
    {
        var directory=new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null&&!File.Exists(Path.Combine(directory.FullName,"connectivity","dnne-connectivity.json")))directory=directory.Parent;
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory!.FullName,"connectivity","dnne-connectivity.json")));
        var source=Assert.Single(doc.RootElement.EnumerateArray(),r=>r.GetProperty("source").GetString()==structure.ToString());
        return source.GetProperty("connections").EnumerateArray().Select(r=>new SynapticConnection(
            Enum.Parse<StructureId>(r.GetProperty("target").GetString()!),r.GetProperty("synapseId").GetGuid(),
            Enum.Parse<NTEnum>(r.GetProperty("neurotransmitter").GetString()!),r.GetProperty("projectionType").GetString()!)).ToArray();
    }
    private static T Field<T>(object value,string name)=>(T)value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(value)!;
    private static SpikeMessage Event(int ordinal=0,double time=1000.125,StructureId structure=StructureId.VestibularNuclei,string instance=Source)=>new()
    {
        MessageId=StableNeuralIdentity.Create("recurrent-transport-control",structure,ordinal),TimestampMs=time,
        SourceStructure=structure,SourceInstanceKey=instance,SourceNeuronId=$"n-{17+ordinal:000}",
        TargetStructure=StructureId.IntralaminarThalamus,TargetNeuronId="auto-IntralaminarThalamus-005",
        SynapseId=StableNeuralIdentity.Create("recurrent-contact",structure,ordinal),ProjectionId=StableNeuralIdentity.Create("recurrent-contact",structure,ordinal),
        Neurotransmitter=NTEnum.GLUTAMATE,VesicleQuanta=1,ReuptakeRate=8,SpikeType=SpikeTypeEnum.ACTION_POTENTIAL
    };
    private static VestibularProjectionFrame Stage(VestibularProjectionOutbox box,StructureId source,long end,params SpikeMessage[] events)
    {
        var state=box.Capture();long start=state.ReleasedTick;
        Assert.All(events,e=>Assert.True(e.TimestampMs>1000+start*.125&&e.TimestampMs<=1000+end*.125));
        var steps=Enumerable.Range(0,checked((int)(end-start))).Select(offset=>
        {
            long tick=start+offset+1;var selected=events.Where(e=>e.TimestampMs>1000+(tick-1)*.125&&e.TimestampMs<=1000+tick*.125).ToArray();
            return Json(new StructureStepResult(new(source,tick,selected.Length,0,0,0,selected.Length,256,default,new()),selected,[]));
        }).ToArray();
        string original=Json(new{ExplicitTransportFixture=true,steps});
        return box.Stage(state.ReleasedSequence+1,original,Hash(original),end,1000+end*.125,steps);
    }
    private sealed class Fixture : IDisposable
    {
        internal readonly ReticulospinalCircuit Circuit;
        internal readonly VestibularProjectionOutbox Input;
        internal Fixture()
        {
            Circuit=Create();Input=new(Source,"L",1000,.125,Circuit.InputRoutes,Circuit.InputRecipients);
            Assert.Equal(Circuit.InputFingerprint,Input.Fingerprint);
        }
        internal static ReticulospinalCircuit Create(string? directory=null,double rfStep=.125,double spinalStep=.125)=>
            new(Source,"L",1000,.125,rfStep,spinalStep,directory??DirectoryName(),Catalog(StructureId.VestibularNuclei),
                Catalog(StructureId.ReticularFormation),Catalog(StructureId.SpinalCordMotor),NeuralSubstrateSupply.ReferenceBath);
        internal VestibularProjectionFrame Send(long end,params SpikeMessage[] events)
        {
            var frame=Stage(Input,StructureId.VestibularNuclei,end,events);
            foreach(var receipt in Circuit.AdmitSource(frame))Input.Accept(receipt);
            Input.Release(frame.Sha256);return frame;
        }
        internal VestibularProjectionReceiver Reticular=>Field<VestibularProjectionReceiver>(Circuit,"_reticular");
        internal VestibularProjectionReceiver Spinal=>Field<VestibularProjectionReceiver>(Circuit,"_spinal");
        public void Dispose()=>Circuit.Dispose();
    }
    private static JsonElement[] Transfers(ReticulospinalFrame frame)
    {using var doc=JsonDocument.Parse(frame.CanonicalJson);return doc.RootElement.GetProperty("Transfers").EnumerateArray().Select(r=>r.Clone()).ToArray();}
    private static string[] Deliveries(ReticulospinalFrame frame)=>Transfers(frame).SelectMany(t=>
    {
        using var doc=JsonDocument.Parse(t.GetProperty("Original").GetProperty("CanonicalJson").GetString()!);
        return doc.RootElement.GetProperty("ActualDeliveries").EnumerateArray().Select(d=>d.GetRawText()).ToArray();
    }).ToArray();
    private static VestibularProjectionFrame ChangePacket(Fixture f,VestibularProjectionFrame frame,int index,Action<JsonObject> change)
    {
        var packets=frame.Packets.ToArray();var packet=packets[index];var node=JsonNode.Parse(packet.CanonicalJson)!.AsObject();change(node);
        string json=node.ToJsonString();packets[index]=packet with{CanonicalJson=json,Sha256=Hash(json)};
        string digest=Hash(Json(new{Version=VestibularProjectionOutbox.Version,Fingerprint=f.Circuit.InputFingerprint,
            sequence=frame.Sequence,originalFrameSha256=frame.SourceFrameSha256,completedTick=frame.CompletedTick,
            completedThroughMs=frame.CompletedThroughMs,Packets=packets}));
        return frame with{Packets=Array.AsReadOnly(packets),Sha256=digest};
    }

    [Fact]
    public void TypedReticularAndSpinalOutputsPreserveTheirActualSourceIdentity()
    {
        foreach(var source in new[]{StructureId.ReticularFormation,StructureId.SpinalCordMotor})
        {
            var target=source==StructureId.ReticularFormation?StructureId.SpinalCordMotor:StructureId.ReticularFormation;
            var route=Assert.Single(Catalog(source),r=>r.Target==target);string instance="isolated-typed-"+source;
            var recipient=new ServiceInstance(target,"isolated-typed-target","L",new Uri("http://127.0.0.1:1"));
            var box=new VestibularProjectionOutbox(instance,"L",1000,.125,[route],[recipient],source);
            var spike=Event(structure:source,instance:instance);var frame=Stage(box,source,1,spike);
            using var doc=JsonDocument.Parse(Assert.Single(frame.Packets).CanonicalJson);var root=doc.RootElement;
            Assert.Equal((int)source,root.GetProperty("SourceStructure").GetInt32());
            var row=Assert.Single(root.GetProperty("Events").EnumerateArray());Assert.Equal(Json(spike),row.GetProperty("OriginalEventJson").GetString());
            var routed=JsonSerializer.Deserialize<SpikeMessage>(row.GetProperty("RoutedEventJson").GetString()!)!;
            Assert.Equal(source,routed.SourceStructure);Assert.Equal(instance,routed.SourceInstanceKey);Assert.Equal(spike.TimestampMs,routed.TimestampMs);
            Assert.Equal(target,routed.TargetStructure);Assert.Equal(route.SynapseId,routed.ProjectionId);
        }
    }

    [Fact]
    public void ProtocolValidationCannotSilentlyRewriteOriginalModulation()
    {
        using var f=new Fixture();var spike=Event();spike.ModulationContext=new(){DopamineLevel=2};string original=Json(spike);
        Assert.Throws<ArgumentException>(()=>Stage(f.Input,StructureId.VestibularNuclei,1,spike));
        Assert.Equal(original,Json(spike));Assert.Null(f.Input.Capture().Pending);Assert.Equal(0,f.Input.Capture().ReleasedSequence);
    }

    [Fact]
    public void SpinalCollateralCannotBypassTheExistingCerebellarEfferencePolicy()
    {
        var route=Assert.Single(Catalog(StructureId.SpinalCordMotor),r=>r.Target==StructureId.CerebellarGranule);
        Assert.Throws<ArgumentException>(()=>new VestibularProjectionOutbox(Source,"L",1000,.125,[route],
            [new(route.Target,"isolated-unsupported-target","L",new Uri("http://127.0.0.1:1"))],StructureId.SpinalCordMotor));
    }

    [Fact]
    public void ExistingConductionAlwaysExceedsTheDeclaredLookaheadBound()
    {
        using var f=new Fixture();
        foreach(var receiver in new[]{f.Reticular,f.Spinal})
        {
            var engine=Field<StructureEngine>(receiver,"_engine");
            foreach(var source in new[]{StructureId.VestibularNuclei,StructureId.ReticularFormation,StructureId.SpinalCordMotor})
            foreach(var nt in Enum.GetValues<NTEnum>())foreach(var type in Enum.GetValues<SpikeTypeEnum>())foreach(bool feedback in new[]{false,true})
            {
                var spike=Event(structure:source);spike.TargetStructure=receiver.Structure;spike.Neurotransmitter=nt;spike.SpikeType=type;spike.IsFeedback=feedback;
                int minimum=engine.MinimumIsolatedProjectionDelayMs(source);Assert.True(minimum>0);
                Assert.True(engine.DescribeIsolatedProjectionArrival(spike)>=spike.TimestampMs+minimum);
            }
        }
    }

    [Fact]
    public void FreshCyclicOwnersCanAdvanceOnlyWithinTheirExistingConductionWindow()
    {
        using var f=new Fixture();Assert.Throws<InvalidOperationException>(()=>f.Circuit.PublishCompleted());
        f.Send(0);var frame=f.Circuit.PublishCompleted();var state=f.Circuit.Capture();
        Assert.Equal(1002,state.Reticular.CompletedThroughMs);Assert.Equal(1001,state.Spinal.CompletedThroughMs);
        Assert.Equal(1000,state.Reticular.KnownSourceThroughMs);Assert.Equal(1000,state.Spinal.KnownSourceThroughMs);
        Assert.False(frame.MoreKnownWork);Assert.Empty(Deliveries(frame));Assert.NotEmpty(Transfers(frame));
        f.Circuit.Acknowledge(frame.Sha256);Assert.Throws<InvalidOperationException>(()=>f.Circuit.PublishCompleted());
    }

    [Fact]
    public void RoundedUnknownArrivalCannotBecomeAnAlreadyIntegratedBoundary()
    {
        double epoch=Math.BitDecrement(1024d);var route=Assert.Single(Catalog(StructureId.VestibularNuclei),r=>r.Target==StructureId.ReticularFormation);
        var target=new ServiceInstance(StructureId.ReticularFormation,"isolated-rounding-recipient","L",new Uri("http://127.0.0.1:1"));
        var box=new VestibularProjectionOutbox(Source,"L",epoch,.125,[route],[target]);
        using var receiver=new VestibularProjectionReceiver(target.StructureId,target.InstanceKey,DirectoryName(),.125,
            [new(Source,box.Fingerprint,epoch,.125,[route])],NeuralSubstrateSupply.ReferenceBath,conductionLookahead:true);
        Assert.Equal(epoch+2,Math.BitIncrement(epoch)+2); // Strict source ordering is lost by this floating-point addition.
        var frame=receiver.PublishCompleted();Assert.True(frame.CompletedThroughMs<Math.BitIncrement(epoch)+2);
        Assert.True(receiver.Capture().PermittedThroughMs<Math.BitIncrement(epoch)+2);
    }

    [Fact]
    public void UnknownCompanionTimeBoundsEvenAFasterSourceInLookaheadMode()
    {
        using var f=new Fixture();var frame=Stage(f.Input,StructureId.VestibularNuclei,320,Event());
        f.Reticular.Admit(frame.Packets.Single(p=>p.TargetInstance==f.Reticular.Instance));var output=f.Reticular.PublishCompleted();
        Assert.Equal(1002,output.CompletedThroughMs);Assert.Equal(1,f.Reticular.Capture().WaitingEvents);
        Assert.Equal(0,f.Reticular.Capture().Actual.ReceivingContacts);
    }

    [Fact]
    public void ARealRecipientOutputRemainsHeldUntilItsActualDownstreamOwnerAcceptsIt()
    {
        using var f=new Fixture();var adapter=Field<RecipientNucleusProjection>(f.Circuit,"_reticularOut");
        var output=adapter.PublishCompleted();var original=f.Reticular.Capture().Pending!;
        Assert.Equal(original.Sha256,output.SourceFrameSha256);Assert.Same(output,adapter.PublishCompleted());
        Assert.Throws<InvalidOperationException>(()=>adapter.ReleaseSource(output.Sha256));
        var receipt=f.Spinal.Admit(Assert.Single(output.Packets));adapter.Accept(receipt);adapter.ReleaseSource(output.Sha256);adapter.ReleaseSource(output.Sha256);
        Assert.Null(f.Reticular.Capture().Pending);Assert.Equal(original.CompletedTicks,adapter.Capture().Outgoing.ReleasedTick);
    }

    [Fact]
    public void ExternalReleaseOfOriginalNeuronalEvidenceCannotBeHiddenByTheAdapter()
    {
        using var f=new Fixture();var adapter=Field<RecipientNucleusProjection>(f.Circuit,"_reticularOut");var output=adapter.PublishCompleted();
        var original=f.Reticular.Capture().Pending!;f.Reticular.Acknowledge(original.Sha256);
        Assert.Throws<InvalidOperationException>(()=>adapter.PublishCompleted());Assert.Throws<InvalidOperationException>(()=>adapter.ReleaseSource(output.Sha256));
        Assert.Same(original,adapter.Capture().Original);Assert.Same(output,adapter.Capture().Outgoing.Pending);
    }

    [Fact]
    public void QuietProgressTerminatesAndBothActualPopulationsReceiveOriginalContacts()
    {
        using var f=new Fixture();f.Send(320,Event(),Event(1));var frame=f.Circuit.PublishCompleted();var state=f.Circuit.Capture();
        Assert.False(frame.MoreKnownWork);Assert.Equal(1042,state.Reticular.CompletedThroughMs);Assert.Equal(1041,state.Spinal.CompletedThroughMs);
        Assert.True(state.Reticular.Actual.ReceivingContacts>=2);Assert.True(state.Spinal.Actual.ReceivingContacts>=2);
        var deliveries=Deliveries(frame).Select(s=>JsonSerializer.Deserialize<RubralInputDelivery>(s)!).ToArray();
        Assert.Equal(4,deliveries.Count(d=>JsonSerializer.Deserialize<SpikeMessage>(d.OriginalJson)!.SourceStructure==StructureId.VestibularNuclei));
        Assert.All(deliveries,d=>Assert.Equal(d.ArrivalAtMs,d.AppliedAtMs));
        foreach(var transfer in Transfers(frame))
        {
            var original=JsonSerializer.Deserialize<VestibularRecipientFrame>(transfer.GetProperty("Original").GetRawText())!;
            var projection=JsonSerializer.Deserialize<VestibularProjectionFrame>(transfer.GetProperty("Projection").GetRawText())!;
            Assert.Equal(Hash(original.CanonicalJson),original.Sha256);Assert.Equal(original.Sha256,projection.SourceFrameSha256);
            Assert.Equal(projection.Packets.Count,transfer.GetProperty("Receipts").GetArrayLength());
        }
    }

    [Fact]
    public void FeedbackSchedulingAndInputPartitionPreserveActualCellsAndContacts()
    {
        (string Reticular,string Spinal,string Deliveries) Run(bool partitioned)
        {
            using var f=new Fixture();var deliveries=new List<string>();
            foreach(int end in partitioned?Enumerable.Range(1,10).Select(i=>i*32):new[]{320})
            {
                f.Send(end,end==(partitioned?32:320)?[Event(),Event(1)]:[]);
                var frame=f.Circuit.PublishCompleted();Assert.False(frame.MoreKnownWork);deliveries.AddRange(Deliveries(frame));f.Circuit.Acknowledge(frame.Sha256);
            }
            var state=f.Circuit.Capture();Assert.Equal(4,deliveries.Count(d=>
                JsonSerializer.Deserialize<SpikeMessage>(JsonSerializer.Deserialize<RubralInputDelivery>(d)!.OriginalJson)!.SourceStructure==StructureId.VestibularNuclei));
            return(state.Reticular.Actual.CellStateJson,state.Spinal.Actual.CellStateJson,Json(deliveries.Order(StringComparer.Ordinal)));
        }
        Assert.Equal(Run(false),Run(true));
    }

    [Fact]
    public void OriginalInputAndOutputRetriesAreInertAndOutputAppliesBackpressure()
    {
        using var f=new Fixture();var input=f.Send(32);var before=Json(f.Circuit.Capture());var receipts=f.Circuit.AdmitSource(input);
        Assert.Equal(before,Json(f.Circuit.Capture()));Assert.Equal(2,receipts.Count);
        var frame=f.Circuit.PublishCompleted();before=Json(f.Circuit.Capture());Assert.Same(frame,f.Circuit.PublishCompleted());
        var next=Stage(f.Input,StructureId.VestibularNuclei,64);Assert.Throws<InvalidOperationException>(()=>f.Circuit.AdmitSource(next));
        Assert.Throws<ArgumentException>(()=>f.Circuit.Acknowledge("wrong"));Assert.Equal(before,Json(f.Circuit.Capture()));
        f.Circuit.Acknowledge(frame.Sha256);f.Circuit.Acknowledge(frame.Sha256);f.Circuit.AdmitSource(next);
        var second=f.Circuit.PublishCompleted();Assert.Throws<ArgumentException>(()=>f.Circuit.Acknowledge(frame.Sha256));Assert.Same(second,f.Circuit.Capture().Pending);
    }

    [Fact]
    public void MissingRecipientOrChangedOuterDigestCannotPartiallyEnterTheCircuit()
    {
        using var f=new Fixture();var frame=Stage(f.Input,StructureId.VestibularNuclei,32);string before=Json(f.Circuit.Capture());
        Assert.Throws<ArgumentException>(()=>f.Circuit.AdmitSource(frame with{Packets=Array.AsReadOnly(frame.Packets.Take(1).ToArray())}));
        Assert.Throws<ArgumentException>(()=>f.Circuit.AdmitSource(frame with{Sha256=new string('0',64)}));Assert.Equal(before,Json(f.Circuit.Capture()));
    }

    [Fact]
    public void ARejectedSecondRecipientRetainsTheFirstActualAdmissionAndFaultsTheCircuit()
    {
        using var f=new Fixture();var frame=Stage(f.Input,StructureId.VestibularNuclei,32,Event());
        int index=Array.FindIndex(frame.Packets.ToArray(),p=>p.TargetInstance==f.Spinal.Instance);
        var changed=ChangePacket(f,frame,index,n=>n["SourceStructure"]=(int)StructureId.SpinalCordMotor);
        Assert.Throws<ArgumentException>(()=>f.Circuit.AdmitSource(changed));var state=f.Circuit.Capture();
        Assert.NotNull(state.Failure);Assert.Equal(1,state.Reticular.WaitingEvents);Assert.Equal(0,state.Spinal.WaitingEvents);
        using var doc=JsonDocument.Parse(state.RetainedEvidenceJson);Assert.Single(doc.RootElement.GetProperty("InputReceipts").EnumerateArray());
        string before=Json(state);Assert.Throws<InvalidOperationException>(()=>f.Circuit.AdmitSource(frame));Assert.Throws<InvalidOperationException>(()=>f.Circuit.PublishCompleted());
        Assert.Equal(before,Json(f.Circuit.Capture()));Assert.NotNull(f.Input.Capture().Pending);
    }

    [Fact]
    public void ActualContactFailureRetainsThePartialNeuronalStateWithoutReplayingIt()
    {
        using var f=new Fixture();f.Send(320,Event());var engine=Field<StructureEngine>(f.Reticular,"_engine");var original=engine.RubralInputObserver!;
        engine.RubralInputObserver=d=>{original(d);throw new InvalidOperationException("declared observer failure after actual receptor delivery");};
        Assert.Throws<InvalidOperationException>(()=>f.Circuit.PublishCompleted());var state=f.Circuit.Capture();
        Assert.NotNull(state.Failure);Assert.NotNull(state.Reticular.Failure);Assert.Equal(1,state.Reticular.Actual.ReceivingContacts);
        Assert.Contains("DispatchingEvents",state.RetainedEvidenceJson);string before=Json(state);
        Assert.Throws<InvalidOperationException>(()=>f.Circuit.PublishCompleted());Assert.Throws<InvalidOperationException>(()=>f.Circuit.Acknowledge("any"));
        Assert.Equal(before,Json(f.Circuit.Capture()));
    }

    [Fact]
    public void OldSavesAndGridsTooCoarseForSafeCyclicProgressAreRejected()
    {
        string directory=DirectoryName();Directory.CreateDirectory(directory);string saved=Path.Combine(directory,"original.txt");File.WriteAllText(saved,"original");
        Assert.Throws<ArgumentException>(()=>Fixture.Create(directory));Assert.Equal("original",File.ReadAllText(saved));
        Assert.Throws<ArgumentException>(()=>Fixture.Create(rfStep:4,spinalStep:4));
    }
}
