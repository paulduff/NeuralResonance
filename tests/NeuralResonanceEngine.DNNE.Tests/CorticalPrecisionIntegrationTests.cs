using System.Text.Json.Nodes;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.DNNE.Tests;

[Collection(EnvironmentVariableTestCollection.Name)]
public sealed class CorticalPrecisionIntegrationTests
{
    private static readonly StructureProfile Profile=new(StructureId.TemporalAssociation,"Izhikevich",
        "STDP+SynapticTaggingCapture","isolated cortical precision",new DelayWindow(0,0));
    private static int Count=>StructureCircuitProfile.For(Profile.StructureId).NeuronCount;
    private static SpikeMessage Input(int eventId=0, double time=0, NTEnum nt=NTEnum.GLUTAMATE)=>new()
    {
        MessageId=StableNeuralIdentity.Create("precision-input-event",eventId,nt),TimestampMs=time,
        SourceStructure=nt==NTEnum.ACETYLCHOLINE?StructureId.NucleusBasalis:nt==NTEnum.DOPAMINE?StructureId.Vta:StructureId.EntorhinalCortex,
        SourceInstanceKey="isolated-source",SourceNeuronId="source-cell-0",TargetStructure=Profile.StructureId,
        TargetNeuronId="declared-target",SynapseId=StableNeuralIdentity.Create("precision-input-projection",nt),
        Neurotransmitter=nt,VesicleQuanta=1.15f,ReuptakeRate=8,SpikeType=SpikeTypeEnum.ACTION_POTENTIAL
    };
    private static IsolatedSynapticTargets Map(int modulationTarget=166)=>new(
        new[]{NTEnum.GLUTAMATE,NTEnum.ACETYLCHOLINE,NTEnum.DOPAMINE}.Select(nt=>
            IsolatedSynapticTarget.From(Input(nt:nt),nt==NTEnum.GLUTAMATE?166:modulationTarget)),Count);
    private static TickSignal Tick(int tick)=>new(tick,tick,1,new(),new Dictionary<BrainRhythm,double>(),0)
        {NeuralSubstrates=NeuralSubstrateSupply.ReferenceBath};
    private static CorticalContactState Initial(Guid id)=>new(id,20,1,1,.6,.2,.4,.5,.2,2,
        PlasticityRules.InitialPlasticityBudgetQuanta,20,0);

    [Fact]
    public void SubFloatLearningSurvivesMirrorsPersistenceAndWarmInitialization()
    {
        using var scope=new Scope(); var contact=new SynapseState(Guid.NewGuid(),NTEnum.GLUTAMATE,1)
            {LastTargetNeuronIndex=166,SourceInstanceKey="source",SourceNeuronId="n-0",ProjectionId=Guid.NewGuid()};
        contact.EnableCorticalPrecision(Initial(contact.SynapseId),true);
        contact.CorticalPrecision!.Modulate(20.0001,.0001,1,1,.2);
        contact.RefreshCorticalEvidence(); var exact=contact.CorticalPrecision.State;
        Assert.True(exact.Weight>1); Assert.Equal(1f,contact.VesicleQuanta);
        contact.VesicleQuanta=4; contact.EligibilityTrace=-1; contact.Stabilize();
        Assert.Equal(exact,contact.CorticalPrecision.State); Assert.Equal(1f,contact.VesicleQuanta);
        using(var store=new SynapsePersistenceStore(Profile.StructureId,"precision-storage",Profile.PlasticityRule))
            store.SaveAndDispose(new(){{contact.SynapseId,contact}},new());
        var inbound=new Dictionary<Guid,SynapseState>();
        using(var store=new SynapsePersistenceStore(Profile.StructureId,"precision-storage",Profile.PlasticityRule))
            store.Load(inbound,new());
        var loaded=Assert.Single(inbound.Values); Assert.Equal(exact,loaded.CorticalPrecision!.State);
        loaded.PrepareWarmStart(); var warm=loaded.CorticalPrecision.State;
        Assert.Equal(exact with {TimeMs=0,PreTrace=0,PostTrace=0,Eligibility=0,Tag=0,BudgetTimeMs=-1},warm);
        loaded.InitializeCorticalClock(1_000_000);
        Assert.Equal(exact.Weight,loaded.CorticalPrecision.State.Weight);
        Assert.Equal(exact.Budget,loaded.CorticalPrecision.State.Budget);
        Assert.Throws<InvalidOperationException>(()=>ContactPlasticity.Spike(loaded,"STDP",true,1_000_000,0));
    }

    [Fact]
    public void SubFloatEfficacyReachesAllExistingExcitatoryReceptorInputs()
    {
        using var scope=new Scope(); var circuit=StructureCircuitProfile.For(Profile.StructureId);
        var ordinary=new ModelNeuron(166,"Izhikevich",circuit); var precise=new ModelNeuron(166,"Izhikevich",circuit);
        var message=Input(); message.VesicleQuanta=1;
        double effective=StructureEngine.CombineCorticalPrecisionStrength(1,1+1e-10);
        Assert.Equal(1f,(float)effective); Assert.True(effective>1);
        ordinary.Integrate(message,0); precise.IntegrateCorticalPrecision(message,effective,0);
        var a=ordinary.CaptureCorticalReceptors(); var b=precise.CaptureCorticalReceptors();
        Assert.True(b.Ampa>a.Ampa);Assert.True(b.Nmda>a.Nmda);Assert.True(b.Metabotropic>a.Metabotropic);
        Assert.True(b.BasalVoltage>a.BasalVoltage);Assert.True(b.ApicalVoltage>a.ApicalVoltage);
        Assert.True(b.ProximalTrace>a.ProximalTrace);Assert.True(b.DistalTrace>a.DistalTrace);
        Assert.True(b.MicrotubuleDrive>a.MicrotubuleDrive);
        Assert.Equal(1f,message.VesicleQuanta); // Local gain never rewrites source release on the wire.
    }

    [Fact]
    public void ExactFloatEfficacyRetainsLegacyReceptorArithmetic()
    {
        using var scope=new Scope(); var circuit=StructureCircuitProfile.For(Profile.StructureId);
        foreach(bool feedback in new[]{false,true})
        {
            var a=new ModelNeuron(166,"Izhikevich",circuit);var b=new ModelNeuron(166,"Izhikevich",circuit);
            foreach(float q in new[]{.05f,.2f,1.15f,5f})
            {
                var message=Input();message.VesicleQuanta=q;message.IsFeedback=feedback;
                a.Integrate(message,0);b.IntegrateCorticalPrecision(message,(double)q,0);
                Assert.Equal(a.CaptureCorticalReceptors(),b.CaptureCorticalReceptors());
            }
        }
    }

    [Fact]
    public void InvalidPrecisionInputLeavesTheReceivingCellUnchanged()
    {
        using var scope=new Scope(); var cell=new ModelNeuron(166,"Izhikevich",StructureCircuitProfile.For(Profile.StructureId));
        var before=cell.CaptureCorticalReceptors();
        foreach(double q in new[]{double.NaN,double.PositiveInfinity,0d,5.001})
            Assert.Throws<ArgumentException>(()=>cell.IntegrateCorticalPrecision(Input(),q,0));
        var synthetic=Input();synthetic.IsSyntheticBackground=true;
        Assert.Throws<ArgumentException>(()=>cell.IntegrateCorticalPrecision(synthetic,1,0));
        Assert.Throws<ArgumentException>(()=>cell.IntegrateCorticalPrecision(Input(nt:NTEnum.GABA),1,0));
        Assert.Equal(before,cell.CaptureCorticalReceptors());
    }

    [Fact]
    public async Task NeuronGeneratedPostsynapticEventsLearnAndWarmStartWithoutRounding()
    {
        using var scope=new Scope();var learned=new List<CorticalPrecisionLearning>();var delivered=new List<CorticalPrecisionInput>();
        CorticalContactState state;Guid contactId;int fired=0;
        using(var engine=new StructureEngine(Profile,null,diagnosticSynapticTargets:Map(),diagnosticCorticalPrecision:true))
        {
            engine.CorticalPrecisionLearningObserver=learned.Add;engine.CorticalPrecisionInputObserver=delivered.Add;
            engine.CorticalNeuronFiringObserver=e=>{if(e.Cell==166)++fired;};
            for(int tick=1;tick<=220;++tick)
            {
                if(tick is 1 or 56 or 111 or 166)
                {
                    await engine.EnqueueSpikeBatchAsync(Enumerable.Range(0,12).Select(i=>Input(tick*100+i,tick-1)).ToArray());
                    await engine.EnqueueSpikeBatchAsync(new[]{Input(tick,tick-1,NTEnum.ACETYLCHOLINE),Input(tick,tick-1,NTEnum.DOPAMINE)});
                }
                await engine.ProcessTickAsync(Tick(tick));
            }
            var contact=Assert.Single(engine.CaptureCorticalPrecisionContacts(),c=>c.SourceInstance=="isolated-source");state=contact.State;contactId=state.ContactId;
            var cueEvents=learned.Where(e=>e.After.State.ContactId==contactId).ToArray();
            var cueInputs=delivered.Where(e=>e.ContactId==contactId).ToArray();
            Assert.True(fired>0);Assert.Equal(fired,cueEvents.Count(e=>e.Kind=="post"));
            Assert.Contains(cueEvents,e=>e.Kind=="post" && e.After.State.Weight>e.Before.State.Weight);
            Assert.Contains(cueEvents,e=>e.Kind=="modulation" && e.Acetylcholine>0 && e.Dopamine>0);
            Assert.NotEqual((double)(float)state.Weight,state.Weight);
            Assert.Equal(48,cueInputs.Length);
            Assert.All(cueInputs,e=>{Assert.Equal(contactId,e.ContactId);Assert.Equal(166,e.Target);
                Assert.Equal(StructureEngine.CombineCorticalPrecisionStrength((float)e.Release,e.Weight),e.EffectiveQuanta);
                Assert.True(e.After.Ampa>e.Before.Ampa);});
            Assert.Equal(48,cueEvents.Count(e=>e.Kind=="pre"));
        }
        byte[] preserved=File.ReadAllBytes(scope.SavePath);
        Assert.Throws<InvalidOperationException>(()=>new StructureEngine(Profile));
        Assert.Throws<InvalidOperationException>(()=>new StructureEngine(Profile,null,diagnosticCorticalPrecision:true));
        Assert.Throws<InvalidOperationException>(()=>new StructureEngine(Profile,null,diagnosticSynapticTargets:Map(165),
            diagnosticCorticalPrecision:true,diagnosticCorticalWarmStart:true));
        Assert.Equal(preserved,File.ReadAllBytes(scope.SavePath));
        using var restored=new StructureEngine(Profile,null,diagnosticSynapticTargets:Map(),diagnosticCorticalPrecision:true,diagnosticCorticalWarmStart:true);
        var warm=Assert.Single(restored.CaptureCorticalPrecisionContacts(),c=>c.State.ContactId==contactId).State;
        Assert.Equal(contactId,warm.ContactId);
        Assert.Equal(state with {TimeMs=0,PreTrace=0,PostTrace=0,Eligibility=0,Tag=0,BudgetTimeMs=-1},warm);
        await restored.ProcessTickAsync(Tick(1));
        Assert.Equal(warm.Weight,Assert.Single(restored.CaptureCorticalPrecisionContacts(),c=>c.State.ContactId==contactId).State.Weight);
    }

    [Fact]
    public async Task NeighboringModulationStaysLocalToItsReceivingCell()
    {
        using var scope=new Scope();using var engine=new StructureEngine(Profile,null,
            diagnosticSynapticTargets:Map(165),diagnosticCorticalPrecision:true);
        var events=new List<CorticalPrecisionLearning>();engine.CorticalPrecisionLearningObserver=events.Add;
        await engine.EnqueueSpikeBatchAsync(new[]{Input(nt:NTEnum.ACETYLCHOLINE),Input(nt:NTEnum.DOPAMINE),Input()});
        for(int tick=1;tick<=55;++tick)await engine.ProcessTickAsync(Tick(tick));
        var cues=events.Where(e=>e.After.SourceInstance=="isolated-source").ToArray();
        Assert.NotEmpty(cues);Assert.All(cues,e=>{Assert.Equal(166,e.After.Target);Assert.Equal(0,e.Acetylcholine);Assert.Equal(0,e.Dopamine);});
        Assert.DoesNotContain(cues,e=>e.Kind=="modulation");
        Assert.True(BiologicalContactLearningTests.Cells(engine)[165].LocalNeuromodState.AcetylcholineLevel>0);
    }

    [Fact]
    public void PrecisionScopeAndExplicitWarmStartRejectAccidentalLiveOrMissingState()
    {
        using var scope=new Scope();
        Assert.Throws<InvalidOperationException>(()=>new StructureEngine(Profile,null,diagnosticCorticalPrecision:true,diagnosticCorticalWarmStart:true));
        Assert.False(File.Exists(scope.SavePath));
        Environment.SetEnvironmentVariable("SERVICE_INSTANCE","live-cortex");
        Assert.Throws<InvalidOperationException>(()=>new StructureEngine(Profile,null,diagnosticCorticalPrecision:true));
        Environment.SetEnvironmentVariable("SERVICE_INSTANCE",Scope.Instance);
        var unsupported=new StructureProfile(Profile.StructureId,"Izhikevich","BCM","unsupported",new DelayWindow(0,0));
        Assert.Throws<InvalidOperationException>(()=>new StructureEngine(unsupported,null,diagnosticCorticalPrecision:true));
        Assert.Throws<ArgumentException>(()=>new StructureEngine(Profile,null,diagnosticCorticalWarmStart:true));
        Directory.CreateDirectory(scope.DirectoryPath);var marker=Path.Combine(scope.DirectoryPath,"keep.txt");File.WriteAllText(marker,"preserved");
        Assert.Throws<InvalidOperationException>(()=>new StructureEngine(Profile,null,diagnosticCorticalPrecision:true));
        Assert.Equal("preserved",File.ReadAllText(marker));
    }

    [Fact]
    public void LegacySchemaFourIsPreservedWithoutAnImplicitPrecisionUpgrade()
    {
        using var scope=new Scope();var contact=new SynapseState(Guid.NewGuid(),NTEnum.GLUTAMATE,1);
        using(var store=new SynapsePersistenceStore(Profile.StructureId,"same-fingerprint"))
            store.SaveAndDispose(new(){{contact.SynapseId,contact}},new());
        byte[] preserved=File.ReadAllBytes(scope.SavePath);
        using(var store=new SynapsePersistenceStore(Profile.StructureId,"same-fingerprint",Profile.PlasticityRule))
            Assert.Throws<InvalidOperationException>(()=>store.Load(new(),new()));
        Assert.Equal(preserved,File.ReadAllBytes(scope.SavePath));
    }

    [Fact]
    public async Task InhibitoryAndSyntheticContactsRetainTheirOwnRulesAndStorage()
    {
        using var scope=new Scope();var explicitInput=Input();var inhibitory=Input(nt:NTEnum.GABA);
        var synthetic=Input(1);synthetic.IsSyntheticBackground=true;
        var map=new IsolatedSynapticTargets(new[]{IsolatedSynapticTarget.From(explicitInput,166),
            IsolatedSynapticTarget.From(inhibitory,166),IsolatedSynapticTarget.From(synthetic,165)},Count);
        using(var engine=new StructureEngine(Profile,null,diagnosticSynapticTargets:map,diagnosticCorticalPrecision:true))
        {
            await engine.EnqueueSpikeBatchAsync(new[]{explicitInput,inhibitory,synthetic});
            await engine.ProcessTickAsync(Tick(1));
            Assert.Single(engine.CaptureCorticalPrecisionContacts(),c=>c.SourceInstance=="isolated-source");
            Assert.Equal(3,engine.CaptureMemoryContactEvidence().Count(c=>c.SourceInstance=="isolated-source"));
        }
        var saved=JsonNode.Parse(File.ReadAllText(scope.SavePath))!;
        Assert.Equal(5,saved["SchemaVersion"]!.GetValue<int>());
        Assert.Equal(1,saved["Inbound"]!.AsArray().Count(e=>e!["SourceInstanceKey"]!.GetValue<string>()=="isolated-source" && e["CorticalPrecisionState"] is not null));
    }

    [Theory]
    [InlineData("missing")][InlineData("mirror")][InlineData("identity")][InlineData("schema")]
    public void IncompatibleOrIncompletePreciseSavesNeverFallBackToFloat(string mutation)
    {
        using var scope=new Scope();var contact=new SynapseState(Guid.NewGuid(),NTEnum.GLUTAMATE,1);
        contact.EnableCorticalPrecision(Initial(contact.SynapseId),true);
        using(var store=new SynapsePersistenceStore(Profile.StructureId,"precision-storage",Profile.PlasticityRule))
            store.SaveAndDispose(new(){{contact.SynapseId,contact}},new());
        var document=JsonNode.Parse(File.ReadAllText(scope.SavePath))!;var entry=document["Inbound"]![0]!;
        switch(mutation)
        {
            case "missing": entry["CorticalPrecisionState"]=null;break;
            case "mirror": entry["VesicleQuanta"]=2;break;
            case "identity":entry["CorticalPrecisionState"]!["ContactId"]=Guid.NewGuid().ToString();break;
            case "schema":document["SchemaVersion"]=4;break;
        }
        File.WriteAllText(scope.SavePath,document.ToJsonString());byte[] preserved=File.ReadAllBytes(scope.SavePath);
        using(var store=new SynapsePersistenceStore(Profile.StructureId,"precision-storage",Profile.PlasticityRule))
            Assert.Throws<InvalidOperationException>(()=>store.Load(new(),new()));
        Assert.Equal(preserved,File.ReadAllBytes(scope.SavePath));
    }

    private sealed class Scope:IDisposable
    {
        internal const string Instance="isolated-cortical-precision-test";
        private readonly Dictionary<string,string?> _prior=new[]{"NRE_SYNAPSE_STATE_DIR","SERVICE_INSTANCE","NRE_MICROTUBULE_MODE"}
            .ToDictionary(k=>k,Environment.GetEnvironmentVariable);
        internal string DirectoryPath {get;}=Path.Combine(Path.GetTempPath(),"nre-cortical-precision",Guid.NewGuid().ToString("N"));
        internal string SavePath=>Path.Combine(DirectoryPath,Instance+".synapses.json");
        internal Scope()
        {Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR",DirectoryPath);Environment.SetEnvironmentVariable("SERVICE_INSTANCE",Instance);Environment.SetEnvironmentVariable("NRE_MICROTUBULE_MODE","classical");}
        public void Dispose(){foreach(var pair in _prior)Environment.SetEnvironmentVariable(pair.Key,pair.Value);}
    }
}
