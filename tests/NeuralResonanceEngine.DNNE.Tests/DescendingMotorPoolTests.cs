using System.Text.Json;
using System.Text.Json.Nodes;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

// These explicitly prescribed source packets test the receiving motor cells and
// their body transport. They do not establish descending neuronal firing,
// human spinal calibration, muscle force or live brain authority.
public sealed class DescendingMotorPoolTests
{
    private static readonly Guid Session=Guid.Parse("f769cb84-555f-4745-9dba-76a6796cb9e6");
    private const string Instance="isolated-descending-motor-pool";
    private static string Json(object value)=>JsonSerializer.Serialize(value);
    private static string Hash(string value)=>VestibularProjectionOutbox.Hash(value);

    private sealed class Fixture
    {
        internal readonly DescendingMotorCell[] Cells;
        internal readonly VestibularProjectionInput[] Inputs;
        internal readonly VestibularProjectionOutbox[] Sources;
        internal readonly DescendingMotorPool Pool;
        internal Fixture(int cells=1,int sources=1,int boutons=1,double delay=.125)
        {
            var routes=Enumerable.Range(0,sources).Select(i=>new SynapticConnection(StructureId.SpinalCordMotor,
                StableNeuralIdentity.Create("declared-motor-pool-control-projection",i),NTEnum.GLUTAMATE,"declared-motor-input-control")).ToArray();
            Sources=routes.Select((route,i)=>new VestibularProjectionOutbox(Source(i),"L",0,.125,[route],
                [new(StructureId.SpinalCordMotor,Instance,"L",new Uri("http://127.0.0.1:1"))],Structure(i))).ToArray();
            Inputs=Sources.Select((box,i)=>new VestibularProjectionInput(Source(i),box.Fingerprint,0,.125,[routes[i]],Structure(i))).ToArray();
            Cells=Enumerable.Range(0,cells).Select(m=>
            {
                var identity=new ReceptorMotorIdentity("isolated-detailed-motor-"+m,"motor-"+m,
                    StableNeuralIdentity.Create("motor-pool-control-output-projection",m),StableNeuralIdentity.Create("motor-pool-control-output-synapse",m));
                var contacts=Enumerable.Range(0,sources).SelectMany(s=>Enumerable.Range(0,boutons).Select(b=>new MotorReceptorDefinition(
                    Source(s),Structure(s),"descending-"+m,StableNeuralIdentity.Create("motor-pool-control-input-synapse",s,m),routes[s].SynapseId,
                    identity.Instance,identity.Neuron,m,"dendritic-site-"+b,1+b%4,MotorReceptorKind.AmpaReference,50,delay))).ToArray();
                return new DescendingMotorCell(identity,MotoneuronReferenceParameters.CatLowThreshold,contacts);
            }).ToArray();
            Pool=new(Session,Instance,Cells,Inputs);
        }
        internal static string Source(int i)=>"isolated-descending-source-"+i;
        internal static StructureId Structure(int i)=>i==0?StructureId.VestibularNuclei:StructureId.ReticularFormation;
        internal SpikeMessage Event(int cell=0,int source=0,double at=.125,int ordinal=0)
        {
            var contact=Cells[cell].Contacts.First(c=>c.SourceInstance==Source(source));
            return new(){MessageId=StableNeuralIdentity.Create("motor-pool-control-event",source,cell,at,ordinal),TimestampMs=at,
                SourceStructure=contact.SourceStructure,SourceInstanceKey=contact.SourceInstance,SourceNeuronId=contact.SourceNeuron,
                SynapseId=contact.SourceSynapse,ProjectionId=contact.Projection,TargetStructure=StructureId.SpinalCordMotor,
                TargetNeuronId=contact.TargetNeuron,Neurotransmitter=contact.Transmitter,VesicleQuanta=1,ReuptakeRate=8,
                SpikeType=SpikeTypeEnum.ACTION_POTENTIAL};
        }
        internal VestibularProjectionPacket Packet(int source,long end,params SpikeMessage[] events)
        {
            var box=Sources[source];var state=box.Capture();long start=state.ReleasedTick;
            Assert.All(events,e=>Assert.True(e.TimestampMs>start*.125&&e.TimestampMs<=end*.125));
            var steps=Enumerable.Range(0,checked((int)(end-start))).Select(offset=>
            {
                long tick=start+offset+1;var selected=events.Where(e=>e.TimestampMs>(tick-1)*.125&&e.TimestampMs<=tick*.125).ToArray();
                return Json(new StructureStepResult(new(Structure(source),tick,selected.Length,0,0,0,selected.Length,256,default,new()),selected,[]));
            }).ToArray();
            string original=Json(new{PrescribedTransportFixture=true,source,start,end,steps});
            return Assert.Single(box.Stage(state.ReleasedSequence+1,original,Hash(original),end,end*.125,steps).Packets);
        }
        internal VestibularProjectionPacket Send(int source,long end,params SpikeMessage[] events)
        {
            var packet=Packet(source,end,events);var receipt=Pool.Admit(packet);var box=Sources[source];
            box.Accept(receipt);box.Release(box.Capture().Pending!.Sha256);return packet;
        }
        internal ReceptorMotorSource Bare(int cell=0)
        {
            var d=Cells[cell];return new(d.Parameters,d.Contacts,Session,d.Identity,d.MaximumStepMs,d.InitialVoltageMv,
                sampleMs:d.SampleMs,crossingInterpolation:MotoneuronCrossingInterpolation.CubicHermite);
        }
        internal MotorAxonAnatomy Anatomy()=>new(Cells.Select((c,i)=>MotorAxonContact.Create(c.Identity.Instance,c.Identity.Neuron,
            c.Identity.Projection,"declared-motor-unit-"+i,2.0625)));
    }

    private static SpikeMessage[] Messages(VestibularProjectionPacket packet)
    {
        using var doc=JsonDocument.Parse(packet.CanonicalJson);
        return doc.RootElement.GetProperty("Events").EnumerateArray().Select(e=>JsonSerializer.Deserialize<SpikeMessage>(e.GetProperty("RoutedEventJson").GetString()!)!).ToArray();
    }
    private static void AdmitBare(Fixture f,ReceptorMotorSource bare,int cell,VestibularProjectionPacket packet,double through)
    {
        var events=Messages(packet);
        foreach(var d in f.Cells[cell].Contacts)
            bare.Admit(Session,d.ContactId,packet.SourceSequence,through,events.Where(e=>e.SourceInstanceKey==d.SourceInstance&&
                e.SourceNeuronId==d.SourceNeuron&&e.TargetNeuronId==d.TargetNeuron).OrderBy(e=>e.TimestampMs).ThenBy(e=>e.MessageId).ToArray());
    }
    private static DescendingMotorSample[] Samples(DescendingMotorFrame frame)
    {
        using var doc=JsonDocument.Parse(frame.CanonicalJson);
        return JsonSerializer.Deserialize<DescendingMotorSample[]>(doc.RootElement.GetProperty("MotorSamples").GetRawText())!;
    }

    [Fact]
    public void MissingCompanionInputCannotBecomeSilenceOrAdvanceAMotorCell()
    {
        var f=new Fixture(sources:2);string initial=Json(f.Pool.Capture().Cells.Select(c=>c.Cell.MembraneState));
        Assert.Throws<InvalidOperationException>(()=>f.Pool.PublishCompleted());
        f.Send(0,16,f.Event());var first=f.Pool.PublishCompleted();Assert.Empty(Samples(first));
        Assert.Equal(initial,Json(f.Pool.Capture().Cells.Select(c=>c.Cell.MembraneState)));
        Assert.All(f.Pool.Capture().Cells,c=>{Assert.Equal(0,c.CompletedSamples);Assert.Equal(0,c.Cell.TimeMs);});
        f.Pool.Acknowledge(first.Sha256);f.Send(1,16);
        var second=f.Pool.PublishCompleted();Assert.Equal(20,Samples(second).Length);
        Assert.Equal(2,Assert.Single(f.Pool.Capture().Cells).Cell.TimeMs);
        Assert.Equal(1,Assert.Single(f.Pool.Capture().Cells).Cell.DeliveredEvents);
    }

    [Fact]
    public void EveryActiveMotorSampleMatchesTheExistingUnwrappedCellExactly()
    {
        var f=new Fixture();var bare=f.Bare();var packet=f.Send(0,32,f.Event(),f.Event(at:2.125));
        AdmitBare(f,bare,0,packet,4);var frame=f.Pool.PublishCompleted();var samples=Samples(frame);
        Assert.Equal(40,samples.Length);
        foreach(var sample in samples)Assert.Equal(Json(bare.AdvanceOneSample()),Json(sample.State));
        Assert.Equal(Json(bare.Capture()),Json(Assert.Single(f.Pool.Capture().Cells)));
        Assert.Equal(2,bare.Capture().Cell.DeliveredEvents);Assert.True(Assert.Single(bare.Capture().Cell.Contacts).Value.OpenFraction>0);
    }

    [Fact]
    public void OriginalTargetSelectsOneMotorCellWithoutExcitingItsNeighbour()
    {
        var f=new Fixture(cells:2);var quiet=f.Bare(1);var packet=f.Send(0,16,f.Event());
        AdmitBare(f,quiet,1,packet,2);for(int i=0;i<20;i++)quiet.AdvanceOneSample();
        f.Pool.PublishCompleted();var actual=f.Pool.Capture().Cells;
        Assert.Equal(1,actual[0].Cell.DeliveredEvents);Assert.Equal(0,actual[1].Cell.DeliveredEvents);
        Assert.Equal(Json(quiet.Capture()),Json(actual[1]));
        Assert.NotEqual(Json(actual[0].Cell.MembraneState),Json(actual[1].Cell.MembraneState));
    }

    [Fact]
    public void TwoNucleiConvergeOnDistinctReceptorsOfOneActualMotorCell()
    {
        var f=new Fixture(sources:2);f.Send(0,16,f.Event());f.Send(1,16,f.Event(source:1));f.Pool.PublishCompleted();
        var state=Assert.Single(f.Pool.Capture().Cells);Assert.Equal(2,state.Cell.DeliveredEvents);
        Assert.Equal(2,state.Cell.Contacts.Select(c=>c.ContactId).Distinct().Count());
        Assert.All(state.Cell.Contacts,c=>{Assert.Equal(1,c.AcceptedEvents);Assert.True(c.Value.OpenFraction>0);});
    }

    [Fact]
    public void OneOriginalAxonEventCanReachSeveralDeclaredBoutonsWithoutChangingIdentity()
    {
        var f=new Fixture(boutons:2);var packet=f.Send(0,16,f.Event());Assert.Equal(1,packet.EventCount);
        var routed=Assert.Single(Messages(packet));var frame=f.Pool.PublishCompleted();var state=Assert.Single(f.Pool.Capture().Cells);
        Assert.Equal(2,state.Cell.AdmittedEvents);Assert.Equal(2,state.Cell.DeliveredEvents);
        using var doc=JsonDocument.Parse(frame.CanonicalJson);
        var admissions=doc.RootElement.GetProperty("Admissions")[0].GetProperty("Contacts").EnumerateArray().ToArray();Assert.Equal(2,admissions.Length);
        foreach(var contact in admissions)
        {
            Guid id=contact.GetProperty("ContactId").GetGuid();string expected=Hash(Json(new{SessionId=Session,ContactId=id,Sequence=1L,
                CompleteThroughSourceMs=2d,OriginalEventHashes=new[]{Hash(Json(routed))}}));
            Assert.Equal(expected,contact.GetProperty("Sha256").GetString());
        }
    }

    [Fact]
    public void UndeclaredTargetRejectsTheEntirePacketBeforeAnyReceptorAdmission()
    {
        var f=new Fixture();var unknown=f.Event(at:.25,ordinal:1);unknown.TargetNeuronId="unmapped-cell";
        var packet=f.Packet(0,16,f.Event(),unknown);string before=Json(f.Pool.Capture());
        Assert.Throws<ArgumentException>(()=>f.Pool.Admit(packet));Assert.Equal(before,Json(f.Pool.Capture()));
        Assert.NotNull(f.Sources[0].Capture().Pending);
    }

    [Theory]
    [InlineData("target")]
    [InlineData("transmitter")]
    [InlineData("source")]
    public void RehashedRoutedPayloadCannotChangeOriginalIdentityOrChemistry(string field)
    {
        var f=new Fixture();var packet=f.Packet(0,16,f.Event());var root=JsonNode.Parse(packet.CanonicalJson)!.AsObject();
        var row=root["Events"]![0]!;var routed=JsonSerializer.Deserialize<SpikeMessage>(row["RoutedEventJson"]!.GetValue<string>())!;
        if(field=="target")routed.TargetNeuronId="another-cell";
        else if(field=="transmitter")routed.Neurotransmitter=NTEnum.GABA;
        else routed.SourceStructure=StructureId.ReticularFormation;
        row["RoutedEventJson"]=Json(routed);string changed=root.ToJsonString();var bad=packet with{CanonicalJson=changed,Sha256=Hash(changed)};
        string before=Json(f.Pool.Capture());Assert.Throws<ArgumentException>(()=>f.Pool.Admit(bad));Assert.Equal(before,Json(f.Pool.Capture()));
    }

    [Fact]
    public void RetryAndPendingOutputCannotAdvanceOrReleaseASecondTime()
    {
        var f=new Fixture();var packet=f.Send(0,8,f.Event());string before=Json(f.Pool.Capture());
        var receipt=f.Pool.Admit(packet);Assert.Equal(before,Json(f.Pool.Capture()));
        var frame=f.Pool.PublishCompleted();before=Json(f.Pool.Capture());var next=f.Packet(0,16);
        Assert.Same(frame,f.Pool.PublishCompleted());Assert.Equal(receipt,f.Pool.Admit(packet));
        Assert.Throws<InvalidOperationException>(()=>f.Pool.Admit(next));Assert.Throws<ArgumentException>(()=>f.Pool.Acknowledge("wrong"));
        Assert.Equal(before,Json(f.Pool.Capture()));f.Pool.Acknowledge(frame.Sha256);f.Pool.Acknowledge(frame.Sha256);
        f.Pool.Admit(next);var second=f.Pool.PublishCompleted();Assert.Throws<ArgumentException>(()=>f.Pool.Acknowledge(frame.Sha256));
        Assert.Same(second,f.Pool.Capture().Pending);
    }

    [Fact]
    public void FutureArrivalRetainsItsOriginalPacketAcrossEarlierAcknowledgement()
    {
        var f=new Fixture(delay:1);var packet=f.Send(0,4,f.Event());var first=f.Pool.PublishCompleted();
        Assert.Equal(0,Assert.Single(f.Pool.Capture().Cells).Cell.DeliveredEvents);f.Pool.Acknowledge(first.Sha256);
        Assert.Contains(packet.Sha256,f.Pool.Capture().RetainedEvidenceJson);
        f.Send(0,16);var second=f.Pool.PublishCompleted();Assert.Equal(1,Assert.Single(f.Pool.Capture().Cells).Cell.DeliveredEvents);
        f.Pool.Acknowledge(second.Sha256);using var doc=JsonDocument.Parse(f.Pool.Capture().RetainedEvidenceJson);
        Assert.Empty(doc.RootElement.GetProperty("RetainedPackets").EnumerateArray());
    }

    [Fact]
    public void InputPacketPartitionPreservesAllBiologicalStatesAndObservedEmissionsExactly()
    {
        ReceptorMotorSourceState Run(bool partitioned)
        {
            var f=new Fixture();foreach(int end in partitioned?new[]{8,16,24,32}:new[]{32})
            {
                var events=new[]{f.Event(),f.Event(at:2.125)}.Where(e=>e.TimestampMs>f.Sources[0].Capture().ReleasedThroughMs&&e.TimestampMs<=end*.125).ToArray();
                f.Send(0,end,events);var frame=f.Pool.PublishCompleted();Assert.False(frame.MoreKnownWork);f.Pool.Acknowledge(frame.Sha256);
            }
            return Assert.Single(f.Pool.Capture().Cells);
        }
        var whole=Run(false);var split=Run(true);
        Assert.All(whole.Cell.InputLanes,l=>Assert.Equal(1,l.Batches));Assert.All(split.Cell.InputLanes,l=>Assert.Equal(4,l.Batches));
        // Packet count is transport bookkeeping, not neuronal state. Preserve
        // every other field, including delivered counts, currents and ledgers.
        static ReceptorMotorSourceState Biological(ReceptorMotorSourceState s)=>s with{Cell=s.Cell with
            {InputLanes=s.Cell.InputLanes.Select(l=>l with{Batches=0}).ToArray()}};
        Assert.Equal(Json(Biological(whole)),Json(Biological(split)));
    }

    [Fact]
    public void SecondCellAdmissionFailureRetainsTheRealFirstCellPrefixAndStopsThePool()
    {
        var f=new Fixture(cells:2);var packet=f.Packet(0,16,f.Event(),f.Event(cell:1),f.Event(cell:1,at:.25,ordinal:1));
        // The existing receptor model rejects overlapping transmitter pulses.
        // The first cell has already accepted its intact original input.
        Assert.Throws<ArgumentException>(()=>f.Pool.Admit(packet));var state=f.Pool.Capture();Assert.NotNull(state.Failure);
        Assert.Equal(1,Assert.Single(state.Cells[0].Cell.InputLanes).Batches);Assert.Equal(1,state.Cells[0].Cell.AdmittedEvents);
        Assert.Equal(0,Assert.Single(state.Cells[1].Cell.InputLanes).Batches);Assert.Equal(0,state.Cells[1].Cell.AdmittedEvents);
        Assert.All(state.Cells,c=>Assert.Equal(0,c.Cell.TimeMs));Assert.Contains(packet.Sha256,state.RetainedEvidenceJson);
        string before=Json(state);Assert.Throws<InvalidOperationException>(()=>f.Pool.Admit(packet));
        Assert.Throws<InvalidOperationException>(()=>f.Pool.PublishCompleted());Assert.Throws<InvalidOperationException>(()=>f.Pool.Acknowledge("any"));
        Assert.Equal(before,Json(f.Pool.Capture()));Assert.NotNull(f.Sources[0].Capture().Pending);
    }

    [Fact]
    public void EveryActualBodyReceiptIsRequiredBeforeReleasingMultipleMotorCells()
    {
        var f=new Fixture(cells:2);var anatomy=f.Anatomy();var clock=new MotorAxonClock(Session,0,0);
        f.Pool.AttachMotorOutput(anatomy,clock);var receiver=new AvatarMotorAxonReceiver(anatomy,clock);f.Send(0,16);
        var frame=f.Pool.PublishCompleted();Assert.Equal(2,frame.MotorBatches.Count);Assert.All(frame.MotorBatches,b=>Assert.Empty(b.Spikes));
        var first=receiver.AcceptWithAcknowledgement(frame.MotorBatches[0]);f.Pool.AcceptBodyReceipt(first);f.Pool.AcceptBodyReceipt(first);
        Assert.Equal(0,receiver.CompleteThroughBodyMs);Assert.Throws<InvalidOperationException>(()=>f.Pool.Acknowledge(frame.Sha256));
        string before=Json(f.Pool.Capture());Assert.Throws<ArgumentException>(()=>f.Pool.AcceptBodyReceipt(first with{BatchSha256=new string('0',64)}));
        Assert.Equal(before,Json(f.Pool.Capture()));f.Pool.AcceptBodyReceipt(receiver.AcceptWithAcknowledgement(frame.MotorBatches[1]));
        Assert.Equal(2,receiver.CompleteThroughBodyMs);Assert.Empty(receiver.AdvanceTo(2));f.Pool.Acknowledge(frame.Sha256);
        Assert.Null(f.Pool.Capture().Pending);Assert.All(f.Pool.Capture().Cells,c=>Assert.Equal(2,c.Cell.TimeMs));
    }

    [Fact]
    public void BodyEpochBindingRetainsRealQuietPrehistoryAndRejectsAReset()
    {
        var f=new Fixture();f.Send(0,4);var frame=f.Pool.PublishCompleted();f.Pool.Acknowledge(frame.Sha256);
        var anatomy=f.Anatomy();string before=Json(f.Pool.Capture());
        Assert.Throws<ArgumentException>(()=>f.Pool.AttachMotorOutput(anatomy,new(Session,0,100)));
        Assert.Throws<ArgumentException>(()=>f.Pool.AttachMotorOutput(anatomy,new(Guid.NewGuid(),.5,100)));
        Assert.Equal(before,Json(f.Pool.Capture()));string cells=Json(f.Pool.Capture().Cells);
        var clock=new MotorAxonClock(Session,.5,100);f.Pool.AttachMotorOutput(anatomy,clock);Assert.Equal(cells,Json(f.Pool.Capture().Cells));
        Assert.Throws<ArgumentException>(()=>f.Pool.AttachMotorOutput(anatomy,clock));
        f.Send(0,8);var next=f.Pool.PublishCompleted();var receiver=new AvatarMotorAxonReceiver(anatomy,clock);
        f.Pool.AcceptBodyReceipt(receiver.AcceptWithAcknowledgement(Assert.Single(next.MotorBatches)));
        Assert.Equal(100.5,receiver.CompleteThroughBodyMs);Assert.Equal(1,Assert.Single(f.Pool.Capture().Cells).Cell.TimeMs);
    }

    [Fact]
    public void BoundedOutputFramesContinueWithoutLosingOrRepeatingMotorSamples()
    {
        var f=new Fixture(cells:2);var originals=new[]{f.Bare(0),f.Bare(1)};var packet=f.Send(0,512);
        for(int c=0;c<2;c++)AdmitBare(f,originals[c],c,packet,64);
        var samples=new List<DescendingMotorSample>();int frames=0;bool more;
        do{var frame=f.Pool.PublishCompleted();more=frame.MoreKnownWork;var part=Samples(frame);Assert.InRange(part.Length,1,512);
            samples.AddRange(part);frames++;f.Pool.Acknowledge(frame.Sha256);}while(more);
        Assert.Equal(3,frames);Assert.Equal(1280,samples.Count);
        foreach(var sample in samples)
        {int c=Array.FindIndex(f.Cells,d=>d.Identity.Instance==sample.Instance);Assert.Equal(Json(originals[c].AdvanceOneSample()),Json(sample.State));}
        for(int c=0;c<2;c++)Assert.Equal(Json(originals[c].Capture()),Json(f.Pool.Capture().Cells[c]));
        Assert.Throws<InvalidOperationException>(()=>f.Pool.PublishCompleted());
    }

    [Fact]
    public void DetachedSnapshotsAndCallerCollectionsCannotChangePrivateNeurons()
    {
        var f=new Fixture();var actual=f.Pool.Capture();string before=Json(actual);actual.Cells[0].Cell.MembraneState[0]=999;
        Assert.Equal(before,Json(f.Pool.Capture()));
        var definition=f.Cells[0];var contacts=(MotorReceptorDefinition[])definition.Contacts;contacts[0]=contacts[0] with{TargetSite="mutated-caller-site"};
        f.Cells[0]=definition with{InitialVoltageMv=-5};Assert.Equal(before,Json(f.Pool.Capture()));
        Assert.Throws<NotSupportedException>(()=>((IList<ReceptorMotorSourceState>)actual.Cells)[0]=actual.Cells[0]);
    }

    [Fact]
    public void ASecondThreadCannotReadAdmitAdvanceOrAcknowledgeTheOwnedCells()
    {
        var f=new Fixture();var packet=f.Packet(0,8);string before=Json(f.Pool.Capture());var failures=new List<Exception>();
        var worker=new Thread(()=>
        {
            Action[] actions=[()=>f.Pool.Capture(),()=>f.Pool.Admit(packet),()=>f.Pool.PublishCompleted(),()=>f.Pool.Acknowledge("any")];
            foreach(var action in actions)try{action();}catch(Exception error){failures.Add(error);}
        }){IsBackground=true};worker.Start();Assert.True(worker.Join(TimeSpan.FromSeconds(5)));
        Assert.Equal(4,failures.Count);Assert.All(failures,e=>Assert.IsType<InvalidOperationException>(e));Assert.Equal(before,Json(f.Pool.Capture()));
    }
}
