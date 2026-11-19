using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.DNNE.Tests;

[Collection(EnvironmentVariableTestCollection.Name)]
public sealed class VestibularProjectionReceiverTests
{
    private static string Json(object value)=>JsonSerializer.Serialize(value);
    private sealed class Fixture : IDisposable
    {
        internal readonly string Instance;
        internal readonly SynapticConnection Route;
        internal readonly VestibularProjectionOutbox[] Sources;
        internal readonly VestibularProjectionInput[] Inputs;
        internal readonly VestibularProjectionReceiver Receiver;
        internal Fixture(int sources=1,StructureId structure=StructureId.ReticularFormation,int capacity=4096)
        {
            Instance="isolated-L-"+structure;Route=Assert.Single(VestibularProjectionOutboxTests.Routes(),r=>r.Target==structure);
            Sources=Enumerable.Range(0,sources).Select(i=>new VestibularProjectionOutbox(SourceName(i),"L",1000,.125,[Route],
                [new(structure,Instance,"L",new Uri("http://127.0.0.1:1"))])).ToArray();
            Inputs=Sources.Select((s,i)=>new VestibularProjectionInput(SourceName(i),s.Fingerprint,1000,.125,[Route])).ToArray();
            Receiver=new(structure,Instance,DirectoryName(),.125,Inputs,NeuralSubstrateSupply.ReferenceBath,capacity);
        }
        internal static string SourceName(int i)=>"isolated-receiving-source-"+i;
        internal static string DirectoryName()=>Path.Combine(Path.GetTempPath(),"nre-projection-receiver",Guid.NewGuid().ToString("N"));
        internal static SpikeMessage Event(int input=0,int ordinal=0,double time=1000.125)=>new()
        {
            MessageId=StableNeuralIdentity.Create("recipient-transport-control",input,ordinal),TimestampMs=time,
            SourceStructure=StructureId.VestibularNuclei,SourceInstanceKey=SourceName(input),SourceNeuronId=$"n-{17+ordinal:000}",
            TargetStructure=StructureId.IntralaminarThalamus,TargetNeuronId="auto-IntralaminarThalamus-005",
            SynapseId=StableNeuralIdentity.Create("recipient-transport-contact",input,ordinal),
            ProjectionId=StableNeuralIdentity.Create("recipient-transport-contact",input,ordinal),Neurotransmitter=NTEnum.GLUTAMATE,
            VesicleQuanta=1,ReuptakeRate=8,SpikeType=SpikeTypeEnum.ACTION_POTENTIAL
        };
        internal VestibularProjectionPacket Packet(int source,long end,params SpikeMessage[] events)
        {
            var box=Sources[source];var state=box.Capture();long start=state.ReleasedTick;
            Assert.All(events,e=>Assert.True(e.TimestampMs>1000+start*.125&&e.TimestampMs<=1000+end*.125));
            string[] steps=Enumerable.Range(0,checked((int)(end-start))).Select(offset=>
            {
                long tick=start+offset+1;var selected=events.Where(e=>e.TimestampMs>1000+(tick-1)*.125&&e.TimestampMs<=1000+tick*.125).ToArray();
                return Json(new StructureStepResult(new(StructureId.VestibularNuclei,tick,selected.Length,0,0,0,selected.Length,256,default,new()),selected,[]));
            }).ToArray();
            string original=Json(new{ExplicitTransportFixture=true,steps});
            var frame=box.Stage(state.ReleasedSequence+1,original,VestibularProjectionOutbox.Hash(original),end,1000+end*.125,steps);
            return Assert.Single(frame.Packets);
        }
        internal VestibularProjectionPacket Send(int source,long end,params SpikeMessage[] events)
        {
            var packet=Packet(source,end,events);var receipt=Receiver.Admit(packet);var box=Sources[source];
            box.Accept(receipt);box.Release(box.Capture().Pending!.Sha256);return packet;
        }
        internal StructureEngine Engine=>(StructureEngine)typeof(VestibularProjectionReceiver).GetField("_engine",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(Receiver)!;
        public void Dispose()=>Receiver.Dispose();
    }
    private static JsonElement[] Deliveries(VestibularRecipientFrame frame)
    {using var doc=JsonDocument.Parse(frame.CanonicalJson);return doc.RootElement.GetProperty("ActualDeliveries").EnumerateArray().Select(e=>e.Clone()).ToArray();}
    private static VestibularProjectionPacket Change(VestibularProjectionPacket packet,Action<JsonObject> edit,int? eventCount=null)
    {
        var node=JsonNode.Parse(packet.CanonicalJson)!.AsObject();edit(node);string json=node.ToJsonString(new JsonSerializerOptions{WriteIndented=false});
        return packet with{CanonicalJson=json,Sha256=VestibularProjectionOutbox.Hash(json),EventCount=eventCount??packet.EventCount};
    }

    [Fact]
    public void AttachmentUsesFreshActualCellsAndRestoresProcessEnvironment()
    {
        string? directory=Environment.GetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR"),instance=Environment.GetEnvironmentVariable("SERVICE_INSTANCE");
        using var f=new Fixture();var state=f.Receiver.Capture();Assert.Equal(0,state.CompletedTicks);Assert.Equal(1000,state.CompletedThroughMs);
        Assert.Null(state.Actual.StartedThroughMs);Assert.Equal(-1,state.Actual.CompletedTick);Assert.Equal(0,state.Actual.ReceivingContacts);
        using var cells=JsonDocument.Parse(state.Actual.CellStateJson);Assert.Equal(256,cells.RootElement.GetArrayLength());
        Assert.Equal(directory,Environment.GetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR"));Assert.Equal(instance,Environment.GetEnvironmentVariable("SERVICE_INSTANCE"));
        Assert.Throws<InvalidOperationException>(()=>f.Receiver.PublishCompleted());
    }

    [Fact]
    public void ActualReticularContactsReceiveOriginalEventsAtTheirExistingConductionTimes()
    {
        using var f=new Fixture();var packet=f.Send(0,320,Fixture.Event(),Fixture.Event(ordinal:1));
        var admitted=f.Receiver.Capture();Assert.Equal(0,admitted.CompletedTicks);Assert.Equal(2,admitted.WaitingEvents);
        Assert.Equal(0,admitted.Actual.QueuedEvents);var frame=f.Receiver.PublishCompleted();var deliveries=Deliveries(frame);
        Assert.Equal(2,deliveries.Length);Assert.Equal(2,deliveries.Select(d=>d.GetProperty("ReceivingContactId").GetGuid()).Distinct().Count());
        using var original=JsonDocument.Parse(packet.CanonicalJson);
        var routed=original.RootElement.GetProperty("Events").EnumerateArray().Select(e=>e.GetProperty("RoutedEventJson").GetString()).ToHashSet();
        foreach(var d in deliveries)
        {
            Assert.Contains(d.GetProperty("OriginalJson").GetString(),routed);
            Assert.Equal(d.GetProperty("ArrivalAtMs").GetDouble(),d.GetProperty("AppliedAtMs").GetDouble());
            Assert.True(d.GetProperty("AppliedAtMs").GetDouble()>1000.125);Assert.InRange(d.GetProperty("ActualTargetIndex").GetInt32(),0,255);
        }
        var state=f.Receiver.Capture();Assert.Equal(320,state.CompletedTicks);Assert.Equal(1040,state.CompletedThroughMs);
        Assert.Equal(0,state.WaitingEvents);Assert.Equal(0,state.Actual.QueuedEvents);Assert.Equal(2,state.Actual.ReceivingContacts);
    }

    [Fact]
    public void TwoOriginalNucleiReachDistinctContactsInTheActualSpinalPopulation()
    {
        using var f=new Fixture(2,StructureId.SpinalCordMotor);f.Send(0,320,Fixture.Event(0));f.Send(1,320,Fixture.Event(1));
        var frame=f.Receiver.PublishCompleted();var deliveries=Deliveries(frame);Assert.Equal(2,deliveries.Length);
        Assert.Equal(2,deliveries.Select(d=>d.GetProperty("ReceivingContactId").GetGuid()).Distinct().Count());
        Assert.Equal(new[]{Fixture.SourceName(0),Fixture.SourceName(1)},deliveries.Select(d=>JsonSerializer.Deserialize<SpikeMessage>(d.GetProperty("OriginalJson").GetString()!)!.SourceInstanceKey).Order(StringComparer.Ordinal));
        Assert.Equal(320,f.Receiver.Capture().Actual.CompletedTick);
    }

    [Fact]
    public void FasterInputCannotTurnUnknownCompanionTimeIntoSilence()
    {
        using var f=new Fixture(2);f.Send(0,320,Fixture.Event(0));var first=f.Receiver.PublishCompleted();
        Assert.Equal(0,first.CompletedTicks);Assert.Empty(Deliveries(first));Assert.Null(f.Receiver.Capture().Actual.StartedThroughMs);
        f.Receiver.Acknowledge(first.Sha256);Assert.Equal(1,f.Receiver.Capture().WaitingEvents);
        f.Send(1,320);var second=f.Receiver.PublishCompleted();Assert.Equal(320,second.CompletedTicks);Assert.Single(Deliveries(second));
        Assert.Equal(1040,f.Receiver.Capture().KnownSourceThroughMs);
    }

    [Fact]
    public void FutureConductionRemainsOwnedAfterEarlierOutputIsAcknowledged()
    {
        using var f=new Fixture();var packet=f.Send(0,1,Fixture.Event());var first=f.Receiver.PublishCompleted();
        Assert.Empty(Deliveries(first));f.Receiver.Acknowledge(first.Sha256);
        var waiting=f.Receiver.Capture();Assert.Equal(1,waiting.WaitingEvents);Assert.Contains(packet.Sha256,waiting.RetainedEvidenceJson);
        f.Send(0,320);var second=f.Receiver.PublishCompleted();Assert.Single(Deliveries(second));f.Receiver.Acknowledge(second.Sha256);
        Assert.Equal(0,f.Receiver.Capture().WaitingEvents);
        using var doc=JsonDocument.Parse(f.Receiver.Capture().RetainedEvidenceJson);Assert.Empty(doc.RootElement.GetProperty("OwnedPackets").EnumerateArray());
    }

    [Fact]
    public void ExactRetriesAndOutputBackpressureDoNotAdvanceOrDeliverAgain()
    {
        using var f=new Fixture();var packet=f.Send(0,1,Fixture.Event());var before=Json(f.Receiver.Capture());
        var receipt=f.Receiver.Admit(packet);Assert.Equal(before,Json(f.Receiver.Capture()));Assert.Equal(packet.Sha256,receipt.PacketSha256);
        var frame=f.Receiver.PublishCompleted();before=Json(f.Receiver.Capture());Assert.Same(frame,f.Receiver.PublishCompleted());
        Assert.Equal(receipt,f.Receiver.Admit(packet));Assert.Throws<ArgumentException>(()=>f.Receiver.Acknowledge("wrong"));
        var next=f.Packet(0,2);Assert.Throws<InvalidOperationException>(()=>f.Receiver.Admit(next));Assert.Equal(before,Json(f.Receiver.Capture()));
        f.Receiver.Acknowledge(frame.Sha256);f.Receiver.Acknowledge(frame.Sha256);f.Receiver.Admit(next);
        var second=f.Receiver.PublishCompleted();Assert.Throws<ArgumentException>(()=>f.Receiver.Acknowledge(frame.Sha256));Assert.Same(second,f.Receiver.Capture().Pending);
    }

    [Fact]
    public void TamperedIdentityTimeChemistryAndDuplicateEventsLeaveTheRecipientUnchanged()
    {
        using var f=new Fixture();var packet=f.Packet(0,1,Fixture.Event());string before=Json(f.Receiver.Capture());
        foreach(var bad in new[]{packet with{Sha256="wrong"},packet with{TargetInstance="unknown"},
            Change(packet,n=>n["OwnerFingerprint"]=new string('0',64)),Change(packet,n=>n["CompletedThroughMs"]=1000.25),
            Change(packet,n=>n["StartingTick"]=1),Change(packet,n=>n["SourceSequence"]=2),
            Change(packet,n=>n["Events"]!.AsArray().Add(n["Events"]![0]!.DeepClone()),2),
            Change(packet,n=>{var e=JsonNode.Parse(n["Events"]![0]!["RoutedEventJson"]!.GetValue<string>())!;e["VesicleQuanta"]=2;n["Events"]![0]!["RoutedEventJson"]=e.ToJsonString();})})
        {Assert.Throws<ArgumentException>(()=>f.Receiver.Admit(bad));Assert.Equal(before,Json(f.Receiver.Capture()));}
    }

    [Fact]
    public void OwnershipCapacityRejectsWholePacketsBeforeAnyAdmission()
    {
        using var f=new Fixture(capacity:1);var packet=f.Packet(0,1,Fixture.Event(),Fixture.Event(ordinal:1));string before=Json(f.Receiver.Capture());
        Assert.Throws<InvalidOperationException>(()=>f.Receiver.Admit(packet));Assert.Equal(before,Json(f.Receiver.Capture()));
        Assert.Null(f.Receiver.Capture().Sources[0].LastReceipt);
    }

    [Fact]
    public void PacketPartitionAndSourceArrivalOrderPreserveActualNeuronalAndContactState()
    {
        (string Cells,string Deliveries) Run(bool partitioned)
        {
            using var f=new Fixture(2);var deliveries=new List<string>();
            var events=new[]{Fixture.Event(0),Fixture.Event(1)};
            double Arrival(SpikeMessage e)
            {
                var routed=TickCoordinator.RewriteSpikeForRoute(e,f.Route);
                routed.MessageId=StableNeuralIdentity.Create(VestibularProjectionOutbox.Version,e.MessageId,f.Route.SynapseId,f.Instance);
                return f.Engine.DescribeIsolatedProjectionArrival(routed);
            }
            var arrivals=events.Select(Arrival).ToArray();double common=arrivals.Max();
            for(int i=0;i<events.Length;i++)events[i].TimestampMs+=common-arrivals[i];
            Assert.All(events,e=>Assert.Equal(common,Arrival(e))); // Deliberate equal-time contact ordering control.
            void Publish(){var frame=f.Receiver.PublishCompleted();deliveries.AddRange(Deliveries(frame).Select(d=>d.GetRawText()));f.Receiver.Acknowledge(frame.Sha256);}
            if(partitioned)
            {
                for(int block=1;block<=10;block++)
                {
                    foreach(int source in block%2==0?new[]{0,1}:new[]{1,0})
                        f.Send(source,block*32,events[source].TimestampMs>1000+(block-1)*4&&events[source].TimestampMs<=1000+block*4?[events[source]]:[]);
                    Publish();
                }
            }
            else{f.Send(0,320,events[0]);f.Send(1,320,events[1]);Publish();}
            Assert.Equal(2,deliveries.Count);
            return(f.Receiver.Capture().Actual.CellStateJson,Json(deliveries));
        }
        Assert.Equal(Run(false),Run(true));
    }

    [Fact]
    public void FailureAfterARealContactRetainsActualPrefixAndCannotReplay()
    {
        using var f=new Fixture();f.Send(0,320,Fixture.Event());var observer=f.Engine.RubralInputObserver!;
        f.Engine.RubralInputObserver=d=>{observer(d);throw new InvalidOperationException("declared downstream observer fault control");};
        Assert.Throws<InvalidOperationException>(()=>f.Receiver.PublishCompleted());var state=f.Receiver.Capture();
        Assert.NotNull(state.Failure);Assert.Equal(1,state.Actual.ReceivingContacts);Assert.Equal(1,state.WaitingEvents);
        using var doc=JsonDocument.Parse(state.RetainedEvidenceJson);Assert.Single(doc.RootElement.GetProperty("ActualDeliveries").EnumerateArray());
        Assert.Single(doc.RootElement.GetProperty("DispatchingEvents").EnumerateArray());
        string before=Json(state);Assert.Throws<InvalidOperationException>(()=>f.Receiver.PublishCompleted());
        Assert.Throws<InvalidOperationException>(()=>f.Receiver.Acknowledge("anything"));Assert.Equal(before,Json(f.Receiver.Capture()));
    }

    [Fact]
    public void SavedStateWrongEpochAndDuplicateSourceContractsCannotBeAttached()
    {
        using var f=new Fixture();string directory=Fixture.DirectoryName();Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"original.txt"),"original");
        Assert.Throws<InvalidOperationException>(()=>new VestibularProjectionReceiver(StructureId.ReticularFormation,f.Instance,directory,.125,f.Inputs,NeuralSubstrateSupply.ReferenceBath));
        Assert.Equal("original",File.ReadAllText(Path.Combine(directory,"original.txt")));
        Assert.Throws<ArgumentException>(()=>new VestibularProjectionReceiver(StructureId.ReticularFormation,f.Instance,Fixture.DirectoryName(),.125,[f.Inputs[0],f.Inputs[0]],NeuralSubstrateSupply.ReferenceBath));
        using var pair=new Fixture(2);var inputs=pair.Inputs.ToArray();inputs[1]=inputs[1] with{EpochMs=1001};
        Assert.Throws<ArgumentException>(()=>new VestibularProjectionReceiver(StructureId.ReticularFormation,pair.Instance,Fixture.DirectoryName(),.125,inputs,NeuralSubstrateSupply.ReferenceBath));
    }
}
