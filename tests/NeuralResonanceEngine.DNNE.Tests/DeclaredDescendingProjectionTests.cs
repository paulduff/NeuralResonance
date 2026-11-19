using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.DNNE.Tests;

// A declared reduction, not a calibrated reticulospinal arbor. Upstream inputs
// are explicit fixtures; the connected motor test must use actual RF emissions.
[Collection(EnvironmentVariableTestCollection.Name)]
public sealed class DeclaredDescendingProjectionTests
{
    private const string Instance="isolated-declared-reticular-source",Input="isolated-declared-vestibular-fixture",Pool="isolated-declared-motor-pool";
    private static readonly Guid Session=Guid.Parse("0f751c39-aa60-4ab2-a3b9-11150df193b2");
    private static string Json(object value)=>JsonSerializer.Serialize(value);
    private static string DirectoryName()=>Path.Combine(Path.GetTempPath(),"nre-declared-descending",Guid.NewGuid().ToString("N"));
    private static T Field<T>(object value,string name)=>(T)value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(value)!;
    private static IsolatedDescendingProjection Map(StructureId source=StructureId.ReticularFormation,int count=256)=>
        new(source,Enumerable.Range(0,count).Select(i=>new DescendingPrimaryTarget(i,"motor-"+(i%2))));
    private static StructureEngine Engine(IsolatedDescendingProjection? map,string? directory=null,string instance=Instance,int? count=null,
        StructureId structure=StructureId.ReticularFormation)
    {
        string? previousDirectory=Environment.GetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR"),previousInstance=Environment.GetEnvironmentVariable("SERVICE_INSTANCE");
        try
        {
            Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR",directory??DirectoryName());Environment.SetEnvironmentVariable("SERVICE_INSTANCE",instance);
            return new(new(structure,"Izhikevich",structure==StructureId.ReticularFormation?"HomeostaticGain":"STDP",
                "Explicit anatomy control; physiology unqualified.",new(2,10)),count,diagnosticDescendingProjection:map);
        }
        finally{Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR",previousDirectory);Environment.SetEnvironmentVariable("SERVICE_INSTANCE",previousInstance);}
    }
    private static void Evidence(string name,object value)
    {
        string? root=Environment.GetEnvironmentVariable("NRE_DESCENDING_ANATOMY_EVIDENCE_DIR");if(string.IsNullOrWhiteSpace(root))return;
        if(!Path.IsPathFullyQualified(root))throw new InvalidOperationException("Absolute isolated evidence directory required.");
        Directory.CreateDirectory(root);using var f=new FileStream(Path.Combine(root,name+".json"),FileMode.CreateNew,FileAccess.Write);
        JsonSerializer.Serialize(f,value);
    }
    private static SpikeMessage Build(StructureEngine engine,int index,bool feedback=false)
    {
        var cell=Field<ModelNeuron[]>(engine,"_neurons")[index];
        var tick=new TickSignal(1,.125,.125,new(),new Dictionary<BrainRhythm,double>(),0,NeuralSubstrates:NeuralSubstrateSupply.ReferenceBath);
        return (SpikeMessage)typeof(StructureEngine).GetMethod("BuildOutboundSpike",BindingFlags.Instance|BindingFlags.NonPublic)!
            .Invoke(engine,[cell,tick,feedback?StructureId.CA3:cell.PreferredTarget,cell.PreferredNt,feedback])!;
    }

    [Fact]
    public void DeclarationRejectsMissingDuplicateForeignAndAmbiguousCells()
    {
        Assert.Throws<ArgumentException>(()=>new IsolatedDescendingProjection(StructureId.Pfc,[new(0,"motor-0")]));
        foreach(var targets in new DescendingPrimaryTarget[][]{[],[new(1,"motor-0")],[new(0,"motor-0"),new(0,"motor-1")],
            [new(0,"motor|0")],[new(0,"motor 0")],[new(0,"")],[new(-1,"motor-0")]})
            Assert.Throws<ArgumentException>(()=>new IsolatedDescendingProjection(StructureId.ReticularFormation,targets));
        Assert.Throws<ArgumentException>(()=>Map(count:255).ValidateFor(StructureId.ReticularFormation,256));
        Assert.Throws<ArgumentException>(()=>Map().ValidateFor(StructureId.VestibularNuclei,256));
    }

    [Fact]
    public void DeclarationIsCanonicalDetachedAndIncludesSourceAndCellAnatomy()
    {
        var targets=Enumerable.Range(0,256).Select(i=>new DescendingPrimaryTarget(i,"motor-"+(i%2))).ToArray();
        var a=new IsolatedDescendingProjection(StructureId.ReticularFormation,targets);
        var b=new IsolatedDescendingProjection(StructureId.ReticularFormation,targets.Reverse());
        Assert.Equal(a.Fingerprint,b.Fingerprint);targets[0]=new(0,"different-motor");Assert.Equal("motor-0",a.Targets[0].TargetNeuron);
        Assert.NotEqual(a.Fingerprint,new IsolatedDescendingProjection(StructureId.ReticularFormation,targets).Fingerprint);
        Assert.NotEqual(a.Fingerprint,Map(StructureId.VestibularNuclei).Fingerprint);
        Assert.Throws<NotSupportedException>(()=>((IList<DescendingPrimaryTarget>)a.Targets)[0]=new(0,"replacement"));
    }

    [Fact]
    public void EngineRequiresFullFreshIsolatedStateAndPreservesOldFiles()
    {
        Assert.Throws<InvalidOperationException>(()=>Engine(Map(),instance:"live-reticular"));
        Assert.Throws<InvalidOperationException>(()=>Engine(Map(),count:1));
        Assert.Throws<ArgumentException>(()=>Engine(Map(count:255)));
        Assert.Throws<ArgumentException>(()=>Engine(Map(StructureId.VestibularNuclei)));
        string directory=DirectoryName();Directory.CreateDirectory(directory);string marker=Path.Combine(directory,"old-save.txt");File.WriteAllText(marker,"preserve");
        Assert.Throws<InvalidOperationException>(()=>Engine(Map(),directory));Assert.Equal("preserve",File.ReadAllText(marker));
        using var ordinary=Engine(null);Assert.Throws<InvalidOperationException>(()=>ordinary.DescribeDeclaredDescendingOutputs());
    }

    [Fact]
    public void DescriptorIsReadOnlyAndOriginalEmitterUsesItsExactPrimaryContactIdentity()
    {
        using var engine=Engine(Map());string before=Json(engine.ObserveIsolatedProjectionRecipient());
        var outputs=engine.DescribeDeclaredDescendingOutputs();Assert.Equal(256,outputs.Count);
        Assert.Equal(before,Json(engine.ObserveIsolatedProjectionRecipient()));Assert.Equal(256,outputs.Select(o=>o.SourceSynapse).Distinct().Count());
        foreach(int index in new[]{0,17,255})
        {
            var expected=outputs[index];var spike=Build(engine,index); // Serialization control; this call is not neuronal firing evidence.
            Assert.Equal(Instance,spike.SourceInstanceKey);Assert.Equal(expected.SourceNeuron,spike.SourceNeuronId);
            Assert.Equal(expected.TargetStructure,spike.TargetStructure);Assert.Equal(expected.TargetNeuron,spike.TargetNeuronId);
            Assert.Equal(expected.SourceSynapse,spike.SynapseId);Assert.Equal(expected.Projection,spike.ProjectionId);
            Assert.Equal(expected.Transmitter,spike.Neurotransmitter);Assert.False(spike.IsFeedback);Assert.False(spike.IsSyntheticBackground);
        }
        Assert.Equal(outputs,engine.DescribeDeclaredDescendingOutputs());
    }

    [Fact]
    public void UndeclaredDefaultAndSeparateFeedbackKeepTheirExistingDestinations()
    {
        using var ordinary=Engine(null);using var declared=Engine(Map());
        Assert.Equal(StructureId.IntralaminarThalamus,Build(ordinary,17).TargetStructure);
        var feedback=Build(declared,17,true);Assert.Equal(StructureId.CA3,feedback.TargetStructure);
        Assert.StartsWith("auto-CA3-",feedback.TargetNeuronId);Assert.True(feedback.IsFeedback);
        Assert.Equal(Build(ordinary,17,true).SynapseId,feedback.SynapseId);
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly VestibularProjectionReceiver Receiver;
        internal readonly VestibularProjectionOutbox Source;
        internal string? InputEvidence;
        internal Fixture(bool mapped=true)
        {
            var route=new SynapticConnection(StructureId.ReticularFormation,StableNeuralIdentity.Create("declared-descending-fixture-input"),
                NTEnum.GLUTAMATE,"explicit-neuronal-input-control");
            Source=new(Input,"L",0,.125,[route],[new(StructureId.ReticularFormation,Instance,"L",new Uri("http://127.0.0.1:1"))]);
            Receiver=new(StructureId.ReticularFormation,Instance,DirectoryName(),.125,[new(Input,Source.Fingerprint,0,.125,[route])],
                NeuralSubstrateSupply.ReferenceBath,declaredOutput:mapped?Map():null);
        }
        internal void Send(long ticks=320)
        {
            var events=Enumerable.Range(0,32).Select(i=>new SpikeMessage{MessageId=StableNeuralIdentity.Create("declared-source-fixture-event",i),
                TimestampMs=(i+1)*.125,SourceStructure=StructureId.VestibularNuclei,SourceInstanceKey=Input,SourceNeuronId=$"n-{i:000}",
                TargetStructure=StructureId.IntralaminarThalamus,TargetNeuronId=$"auto-IntralaminarThalamus-{i:000}",
                SynapseId=StableNeuralIdentity.Create("declared-source-fixture-contact",i),ProjectionId=StableNeuralIdentity.Create("declared-source-fixture-contact",i),
                Neurotransmitter=NTEnum.GLUTAMATE,VesicleQuanta=1,ReuptakeRate=8,SpikeType=SpikeTypeEnum.ACTION_POTENTIAL}).ToArray();
            var steps=Enumerable.Range(1,checked((int)ticks)).Select(i=>
            {
                var selected=events.Where(e=>e.TimestampMs>(i-1)*.125&&e.TimestampMs<=i*.125).ToArray();
                return Json(new StructureStepResult(new(StructureId.VestibularNuclei,i,selected.Length,0,0,0,selected.Length,256,default,new()),selected,[]));
            }).ToArray();
            InputEvidence=Json(new{ExplicitUpstreamFixture=true,steps});
            var staged=Source.Stage(1,InputEvidence,VestibularProjectionOutbox.Hash(InputEvidence),ticks,ticks*.125,steps);
            Source.Accept(Receiver.Admit(Assert.Single(staged.Packets)));Source.Release(staged.Sha256);
        }
        public void Dispose()=>Receiver.Dispose();
    }
    private static SpikeMessage[] Emissions(VestibularRecipientFrame frame)
    {
        using var doc=JsonDocument.Parse(frame.CanonicalJson);
        return doc.RootElement.GetProperty("ReceiverStepJson").EnumerateArray().SelectMany(r=>
            JsonSerializer.Deserialize<StructureStepResult>(r.GetString()!)!.OutboundSpikes).ToArray();
    }
    private static string SameNeuronalEmission(SpikeMessage value)
    {
        var copy=JsonNode.Parse(Json(value))!.AsObject();
        foreach(string key in new[]{"MessageId","SynapseId","ProjectionId","TargetStructure","TargetNeuronId"})copy.Remove(key);
        return copy.ToJsonString();
    }

    [Fact]
    public void ReceivingOwnerDeclaresActualPrimaryOutputsBeforeInputAndRestoresEnvironment()
    {
        string? directory=Environment.GetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR"),instance=Environment.GetEnvironmentVariable("SERVICE_INSTANCE");
        using var mapped=new Fixture();using var ordinary=new Fixture(false);
        Assert.Equal(directory,Environment.GetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR"));Assert.Equal(instance,Environment.GetEnvironmentVariable("SERVICE_INSTANCE"));
        string before=Json(mapped.Receiver.Capture());Assert.Equal(256,mapped.Receiver.DescribePrimaryMotorOutputs().Count);
        Assert.Equal(before,Json(mapped.Receiver.Capture()));Assert.NotEqual(mapped.Receiver.Fingerprint,ordinary.Receiver.Fingerprint);
        Assert.Throws<InvalidOperationException>(()=>ordinary.Receiver.DescribePrimaryMotorOutputs());
        Assert.Equal(ordinary.Receiver.Capture().Actual.CellStateJson,mapped.Receiver.Capture().Actual.CellStateJson);
    }

    [Fact]
    public void ActualNeuronalEvolutionAndEmissionContentRemainExactAcrossTheAnatomyChange()
    {
        using var ordinary=new Fixture(false);using var mapped=new Fixture();ordinary.Send();mapped.Send();
        var original=ordinary.Receiver.PublishCompleted();var candidate=mapped.Receiver.PublishCompleted();
        var before=Emissions(original);var after=Emissions(candidate);var routes=mapped.Receiver.DescribePrimaryMotorOutputs();
        Evidence("actual-source-comparison",new{Input=ordinary.InputEvidence,Original=original,Candidate=candidate,
            OriginalState=ordinary.Receiver.Capture(),CandidateState=mapped.Receiver.Capture(),Routes=routes,
            Normalization="Only independent event IDs and explicitly declared primary target/contact identities differ; all neuronal fields compare exactly."});
        Assert.NotEmpty(before);Assert.Equal(ordinary.InputEvidence,mapped.InputEvidence);
        Assert.Equal(ordinary.Receiver.Capture().Actual.CellStateJson,mapped.Receiver.Capture().Actual.CellStateJson);
        Assert.Equal(before.Select(SameNeuronalEmission),after.Select(SameNeuronalEmission));
        foreach(var spike in after)
        {
            var route=Assert.Single(routes,r=>r.SourceNeuron==spike.SourceNeuronId);
            Assert.Equal(route.SourceSynapse,spike.SynapseId);Assert.Equal(route.Projection,spike.ProjectionId);
            Assert.Equal(route.TargetNeuron,spike.TargetNeuronId);Assert.Equal(StructureId.SpinalCordMotor,spike.TargetStructure);
            Assert.Equal(route.Transmitter,spike.Neurotransmitter);
        }
    }

    [Fact]
    public void OriginalReticularEmissionsReachDeclaredDetailedMotorContactsWithoutRenaming()
    {
        using var f=new Fixture();var outputs=f.Receiver.DescribePrimaryMotorOutputs();
        var route=new SynapticConnection(StructureId.SpinalCordMotor,StableNeuralIdentity.Create("declared-reticular-motor-path"),
            NTEnum.GLUTAMATE,"declared-primary-motor-output");
        var outgoing=new RecipientNucleusProjection(f.Receiver,"L",[route],[new(StructureId.SpinalCordMotor,Pool,"L",new Uri("http://127.0.0.1:1"))]);
        var definitions=Enumerable.Range(0,2).Select(m=>
        {
            var identity=new ReceptorMotorIdentity("isolated-declared-motor-"+m,"motor-"+m,
                StableNeuralIdentity.Create("declared-motor-output",m),StableNeuralIdentity.Create("declared-motor-synapse",m));
            var contacts=outputs.Where(o=>o.TargetNeuron==identity.Neuron).Select(o=>new MotorReceptorDefinition(o.SourceInstance,o.SourceStructure,
                o.SourceNeuron,o.SourceSynapse,route.SynapseId,identity.Instance,identity.Neuron,m,"declared-dendrite-"+o.SourceIndex,
                1,MotorReceptorKind.AmpaReference,50,.125)).ToArray();
            return new DescendingMotorCell(identity,MotoneuronReferenceParameters.CatLowThreshold,contacts,SampleMs:.125);
        }).ToArray();
        var pool=new DescendingMotorPool(Session,Pool,definitions,[new(Instance,outgoing.Fingerprint,0,.125,[route],StructureId.ReticularFormation)]);
        f.Send();var frame=outgoing.PublishCompleted();var packet=Assert.Single(frame.Packets);
        var originalNeuronalFrame=outgoing.Capture().Original!;
        var receipt=pool.Admit(packet);outgoing.Accept(receipt);outgoing.ReleaseSource(frame.Sha256);
        var motorFrames=new List<DescendingMotorFrame>();bool more;
        do{var next=pool.PublishCompleted();motorFrames.Add(next);more=next.MoreKnownWork;pool.Acknowledge(next.Sha256);}while(more);
        var state=pool.Capture();
        Evidence("actual-motor-admission",new{Input=f.InputEvidence,OriginalSourceFrame=frame,OriginalNeuronalFrame=originalNeuronalFrame,Packet=packet,Receipt=receipt,
            OriginalNeuronalOwner=outgoing.Capture(),Definitions=definitions,
            ContactManifest=definitions.SelectMany(d=>d.Contacts.Select(c=>new{c.ContactId,c.Transmitter,Definition=c})),
            Routes=outputs,MotorFrames=motorFrames,State=state,
            PhysiologicalFiringQualified=false,LiveBrainAuthorityQualified=false,AvatarBalanceAssessed=false});
        Assert.True(packet.EventCount>0);Assert.Equal(packet.EventCount,receipt.AcceptedEvents);
        Assert.Equal(packet.EventCount,state.Cells.Sum(c=>c.Cell.AdmittedEvents));Assert.True(state.Cells.Sum(c=>c.Cell.DeliveredEvents)>0);
        Assert.All(state.Cells,c=>{Assert.Equal(40,c.Cell.TimeMs);Assert.Equal(320,c.CompletedSamples);Assert.False(c.Faulted);});
        using var doc=JsonDocument.Parse(packet.CanonicalJson);
        foreach(var entry in doc.RootElement.GetProperty("Events").EnumerateArray())
        {
            var original=JsonSerializer.Deserialize<SpikeMessage>(entry.GetProperty("OriginalEventJson").GetString()!)!;
            var delivered=JsonSerializer.Deserialize<SpikeMessage>(entry.GetProperty("RoutedEventJson").GetString()!)!;
            Assert.Equal(original.SourceNeuronId,delivered.SourceNeuronId);Assert.Equal(original.TargetNeuronId,delivered.TargetNeuronId);
            Assert.Equal(original.Neurotransmitter,delivered.Neurotransmitter);Assert.Equal(original.SynapseId,delivered.SynapseId);
        }
    }
}
