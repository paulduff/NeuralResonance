using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.DNNE.Tests;

[Collection(EnvironmentVariableTestCollection.Name)]
public sealed class BiologicalContactLearningTests
{
    static SynapseState Contact() => new(Guid.NewGuid(), NTEnum.GLUTAMATE, 1f);

    [Fact]
    public void RoutingAndWireSerializationKeepCellIdentitySeparateFromProjection()
    {
        var original = new SpikeMessage { SynapseId=Guid.NewGuid(), SourceInstanceKey="R_Pfc", SourceNeuronId="n-004", IsSyntheticBackground=true };
        var route = new SynapticConnection(StructureId.Striatum, Guid.NewGuid(), NTEnum.GLUTAMATE, "test projection");
        var rewritten = (SpikeMessage)typeof(TickCoordinator).GetMethod("RewriteSpikeForRoute", BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[original,route])!;
        var decoded = ProtoBuf.Serializer.DeepClone(rewritten);
        Assert.Equal(original.SynapseId, decoded.SynapseId);
        Assert.Equal(route.SynapseId, decoded.ProjectionId);
        Assert.Equal("R_Pfc", decoded.SourceInstanceKey);
        Assert.Equal("n-004", decoded.SourceNeuronId);
        Assert.True(decoded.IsSyntheticBackground);
    }

    [Fact]
    public void EditorLearningAggregateRetainsContactOwnersAndRejectsMissingMemberTelemetry()
    {
        var contact = new SynapticContactSample(Guid.NewGuid(), Guid.NewGuid(), "L_Pfc", "n-004",
            "L_Striatum", 18, 1.04f, .1f, 3, false);
        var left = new SynapticLearningDiagnostics(StableNeuralIdentity.TopologyVersion,
            "Learned weights; warm initialization", "Reduced model; physiological calibration pending",
            256, 1, 2, 1, [contact], 250);
        var right = left with { Contacts = [contact with { ContactId=Guid.NewGuid(), TargetInstance="R_Striatum" }], ContactSampleTimestampMs=300 };
        InstanceStructureSnapshot Member(string side, SynapticLearningDiagnostics? learning) => new(
            new ServiceInstance(StructureId.Striatum, side+"_Striatum", side, new Uri("http://127.0.0.1:1")),
            StructureId.Striatum, 0, 0, default, [], new NeuromodState(), 0, 0, 0,
            SynapticLearningDiagnostics:learning);
        var combine = typeof(TickCoordinator).GetMethod("CombineSynapticLearningDiagnostics", BindingFlags.Static|BindingFlags.NonPublic)!;
        var combined = Assert.IsType<SynapticLearningDiagnostics>(combine.Invoke(null,
            [new[] {Member("L",left), Member("R",right)}]));
        Assert.Equal(512, combined.NeuronCount);
        Assert.Equal(2, combined.ContactCount);
        Assert.Equal(4, combined.PresynapticEvents);
        Assert.Equal(2, combined.PostsynapticEvents);
        Assert.Equal(250, combined.ContactSampleTimestampMs);
        var json = JsonSerializer.Serialize(combined, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var wire = JsonDocument.Parse(json);
        Assert.Equal("L_Striatum", wire.RootElement.GetProperty("contacts")[0].GetProperty("targetInstance").GetString());
        Assert.Equal("R_Striatum", wire.RootElement.GetProperty("contacts")[1].GetProperty("targetInstance").GetString());
        Assert.Null(combine.Invoke(null, [new[] {Member("L",left), Member("R",null)}]));
    }

    [Fact]
    public void SameEligibleContactRespondsToReversedLocalTeaching()
    {
        var positive = Contact(); var negative = Contact();
        foreach (var contact in new[] {positive,negative})
        {
            ContactPlasticity.Spike(contact,"DopamineModulatedSTDP",true,10,0);
            ContactPlasticity.Spike(contact,"DopamineModulatedSTDP",false,20,0);
        }
        for (var time=21; time<=1000; time++)
        {
            ContactPlasticity.Modulate(positive,"DopamineModulatedSTDP",time,1,new NeuromodState {DopamineLevel=1},1,0);
            ContactPlasticity.Modulate(negative,"DopamineModulatedSTDP",time,1,new NeuromodState {DopamineLevel=1},-1,0);
        }
        Assert.True(positive.VesicleQuanta > 1);
        Assert.True(negative.VesicleQuanta < 1);
    }

    [Fact]
    public void ActualPrePostOrderControlsSignAndUnpairedInputDoesNotPotentiate()
    {
        var causal = Contact(); var reversed = Contact(); var unpaired = Contact();
        ContactPlasticity.Spike(causal, "STDP", true, 10, 0);
        ContactPlasticity.Spike(causal, "STDP", false, 20, 0);
        ContactPlasticity.Spike(reversed, "STDP", false, 10, 0);
        ContactPlasticity.Spike(reversed, "STDP", true, 20, 0);
        for (var time = 10; time <= 100; time += 10) ContactPlasticity.Spike(unpaired, "STDP", true, time, 1);
        Assert.True(causal.VesicleQuanta > 1);
        Assert.True(reversed.VesicleQuanta < 1);
        Assert.Equal(1f, unpaired.VesicleQuanta);
    }

    [Fact]
    public void PairingOneContactLeavesOtherContactsUnchanged()
    {
        var paired = Contact(); var untouched = Contact();
        ContactPlasticity.Spike(paired, "STDP", true, 10, 0);
        ContactPlasticity.Spike(paired, "STDP", false, 20, 0);
        Assert.True(paired.VesicleQuanta > untouched.VesicleQuanta);
        Assert.Equal(0, untouched.UpdateCount);
    }

    [Fact]
    public void DelayedDopamineRequiresEligibilityAndDecaysWithDelay()
    {
        float Run(int delay, bool paired, float dopamine)
        {
            var contact = Contact();
            if (paired)
            {
                ContactPlasticity.Spike(contact, "DopamineModulatedSTDP", true, 10, 0);
                ContactPlasticity.Spike(contact, "DopamineModulatedSTDP", false, 20, 0);
            }
            for (var time = 20 + delay; time < 120 + delay; time++)
                ContactPlasticity.Modulate(contact, "DopamineModulatedSTDP", time, 1, new NeuromodState { DopamineLevel = dopamine }, dopamine, 0);
            return contact.VesicleQuanta;
        }
        var early = Run(300, true, 1);
        Assert.True(early > 1);
        Assert.Equal(1f, Run(300, false, 1));
        Assert.Equal(1f, Run(300, true, 0));
        Assert.True(early > Run(6000, true, 1));
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(4)]
    public void ContinuousModulationUsesBiologicalTime(int dt)
    {
        float Run(int step)
        {
            var contact = Contact(); contact.EligibilityTrace = .8f;
            for (var time = step; time <= 1000; time += step)
                ContactPlasticity.Modulate(contact, "DopamineModulatedSTDP", time, step, new NeuromodState { DopamineLevel = 1 }, 1, 0);
            return contact.VesicleQuanta;
        }
        Assert.InRange(Math.Abs(Run(dt) - Run(1)), 0, .00003f);
    }

    [Fact]
    public void ClimbingInputChangesOnlyCoactiveParallelFibreContacts()
    {
        var parallel = Contact(); parallel.SourceStructure = StructureId.CerebellarGranule;
        var dormant = Contact(); dormant.SourceStructure = StructureId.CerebellarGranule;
        var climbing = Contact(); climbing.SourceStructure = StructureId.InferiorOlive;
        ContactPlasticity.Spike(parallel, "CerebellarLTD", true, 10, 0);
        ContactPlasticity.Spike(climbing, "CerebellarLTD", true, 10, 0);
        foreach (var contact in new[] { parallel, dormant, climbing }) ContactPlasticity.ClimbingInput(contact, 20);
        Assert.True(parallel.VesicleQuanta < 1);
        Assert.Equal(1, dormant.VesicleQuanta);
        Assert.Equal(1, climbing.VesicleQuanta);
    }

    [Fact]
    public void SyntheticBackgroundCannotCreateHebbianCredit()
    {
        var contact = Contact(); contact.IsSyntheticBackground = true;
        ContactPlasticity.Spike(contact, "STDP", true, 10, 0);
        ContactPlasticity.Spike(contact, "STDP", false, 20, 1);
        Assert.Equal(1f, contact.VesicleQuanta);
        Assert.Equal(0, contact.UpdateCount);
    }

    [Fact]
    public async Task RoutedCellsAndHemispheresOwnDifferentActualReceivingContacts()
    {
        using var fixture = new IsolatedEngine(); var engine = fixture.Engine;
        var a = fixture.Spike(0); var b = fixture.Spike(1); var c = fixture.Spike(0); c.SourceInstanceKey = "R_VestibularAfferents";
        await engine.EnqueueSpikeBatchAsync([a,b,c]);
        for (var tick=1; tick<=40; tick++) await engine.ProcessTickAsync(Tick(tick));
        var ids = new[] { engine.ResolveContactId(a), engine.ResolveContactId(b), engine.ResolveContactId(c) };
        Assert.Equal(3, ids.Distinct().Count());
        Assert.All(ids, id => Assert.InRange(engine.GetInboundSynapseStrength(id), .05f, 5f));
        Assert.Throws<InvalidOperationException>(() => engine.GetInboundSynapseStrength(a.SynapseId));
    }

    [Fact]
    public async Task ActualTargetSpikesUpdateIncomingContactWithoutAnotherInput()
    {
        using var fixture = new IsolatedEngine(); var engine = fixture.Engine;
        var spike = fixture.Spike(16); spike.IsRightingCircuitSpike = true;
        await engine.EnqueueSpikeAsync(spike);
        for (var tick=1; tick<=20; tick++) await engine.ProcessTickAsync(Tick(tick));
        var contact = States(engine)[engine.ResolveContactId(spike)];
        var before = contact.UpdateCount;
        var cell = Cells(engine)[contact.LastTargetNeuronIndex];
        var drive = fixture.Spike(16); drive.VesicleQuanta = 5;
        for (var i=0; i<40; i++) cell.Integrate(drive, 1);
        var fired = 0;
        for (var tick=21; tick<=40; tick++) { await engine.ProcessTickAsync(Tick(tick)); if(cell.IsActive) fired++; }
        Assert.True(fired > 0);
        Assert.Equal(before + fired, contact.UpdateCount);
        Assert.True(contact.PostTrace > 0);
        Assert.True(contact.VesicleQuanta > 1);
        var ack = await engine.ProcessTickAsync(Tick(41));
        if (cell.IsActive) fired++;
        var diagnostics = Assert.IsType<SynapticLearningDiagnostics>(ack.SynapticLearningDiagnostics);
        Assert.Equal(1, diagnostics.ContactCount);
        Assert.Equal(1, diagnostics.PresynapticEvents);
        Assert.Equal(fired, diagnostics.PostsynapticEvents);
        Assert.Equal(contact.SynapseId, Assert.Single(diagnostics.Contacts).ContactId);
        Assert.Contains("calibration pending", diagnostics.ModelStatus);
        var learnedWeight = contact.VesicleQuanta;
        var remainingBudget = contact.PlasticityBudgetQuanta;
        fixture.Reload();
        var restored = States(fixture.Engine)[contact.SynapseId];
        Assert.Equal(learnedWeight, restored.VesicleQuanta);
        Assert.Equal(remainingBudget, restored.PlasticityBudgetQuanta);
        Assert.Equal(0, restored.PreTrace);
        Assert.Equal(0, restored.PostTrace);
        await fixture.Engine.ProcessTickAsync(Tick(1));
        Assert.InRange(restored.LastPlasticityBudgetTimestampMs, 0, 1);
    }

    [Fact]
    public void WarmStartPreservesWeightAndRemainingBudgetWithoutFutureTimestamps()
    {
        var contact = Contact(); contact.VesicleQuanta = 1.7f; contact.PlasticityBudgetQuanta = 0;
        contact.LastPlasticityBudgetTimestampMs = 7_000_000; contact.LastUpdateTimestampMs = 7_000_000;
        contact.PreTrace = 4; contact.EligibilityTrace = .8f;
        contact.PrepareWarmStart();
        Assert.Equal(1.7f, contact.VesicleQuanta);
        Assert.Equal(0, contact.PreTrace); Assert.Equal(0, contact.EligibilityTrace);
        Assert.Equal(0, PlasticityRules.ApplyCadenceInvariantBudget(contact, 1, 1));
        Assert.True(PlasticityRules.ApplyCadenceInvariantBudget(contact, 1, 1001) > 0);
    }

    [Fact]
    public void IncompatibleSavedStateIsRejectedWithoutOverwrite()
    {
        using var fixture = new IsolatedEngine(create:false);
        var path = Path.Combine(fixture.Directory, "mechanism.synapses.json");
        System.IO.Directory.CreateDirectory(fixture.Directory);
        var original = JsonSerializer.Serialize(new { SchemaVersion=3, StructureId=StructureId.VestibularNuclei, InstanceKey="mechanism" });
        File.WriteAllText(path, original);
        Assert.Throws<InvalidOperationException>(() => fixture.Create());
        Assert.Equal(original, File.ReadAllText(path));
    }

    [Fact]
    public async Task BackwardsBiologicalClockIsRejectedEvenWithANewerTick()
    {
        using var fixture = new IsolatedEngine();
        await fixture.Engine.ProcessTickAsync(Tick(10));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Engine.ProcessTickAsync(Tick(11) with {TimestampMs=5}).AsTask());
    }

    [Theory]
    [InlineData("empty-id")] [InlineData("duplicate")] [InlineData("wrong-source")] [InlineData("null-contacts")]
    public async Task MalformedContactStateIsRejectedWithoutDiscardingOrRewritingIt(string corruption)
    {
        using var fixture = new IsolatedEngine();
        await fixture.Engine.EnqueueSpikeAsync(fixture.Spike(0));
        for (var tick=1; tick<=40; tick++) await fixture.Engine.ProcessTickAsync(Tick(tick));
        fixture.Engine.Dispose();
        var path = Path.Combine(fixture.Directory, "mechanism.synapses.json");
        var document = JsonNode.Parse(File.ReadAllText(path))!;
        var contacts = document["Inbound"]!.AsArray();
        Assert.Single(contacts);
        if (corruption == "empty-id") contacts[0]!["SynapseId"] = Guid.Empty.ToString();
        if (corruption == "duplicate") contacts.Add(contacts[0]!.DeepClone());
        if (corruption == "wrong-source") contacts[0]!["SourceNeuronId"] = "n-999";
        if (corruption == "null-contacts") document["Inbound"] = null;
        var corrupted = document.ToJsonString();
        File.WriteAllText(path, corrupted);
        Assert.Throws<InvalidOperationException>(() => fixture.Create());
        Assert.Equal(corrupted, File.ReadAllText(path));
    }

    [Fact]
    public void NewlyArrivedContactCannotReceiveRetroactiveConsolidation()
    {
        var contact = Contact();
        contact.LastUpdateTimestampMs = 100;
        contact.EligibilityTrace = .8f;
        ContactPlasticity.Modulate(contact, "DopamineModulatedSTDP", 100, 4,
            new NeuromodState { DopamineLevel = 1 }, 1, 0);
        Assert.Equal(1f, contact.VesicleQuanta);
        ContactPlasticity.Modulate(contact, "DopamineModulatedSTDP", 104, 4,
            new NeuromodState { DopamineLevel = 1 }, 1, 0);
        Assert.True(contact.VesicleQuanta > 1);
    }

    [Fact]
    public void InhibitoryHomeostasisWeakensIdleTargetsAndStrengthensCoactiveTargets()
    {
        var idle = new SynapseState(Guid.NewGuid(), NTEnum.GABA, 1);
        var active = new SynapseState(Guid.NewGuid(), NTEnum.GABA, 1);
        ContactPlasticity.Spike(idle, "STDP", true, 20, 0);
        ContactPlasticity.Spike(active, "STDP", false, 10, 0);
        ContactPlasticity.Spike(active, "STDP", true, 20, 0);
        Assert.True(idle.VesicleQuanta < 1);
        Assert.True(active.VesicleQuanta > 1);
    }

    internal static TickSignal Tick(int tick) => new(tick,tick,1,new NeuromodState(),new Dictionary<BrainRhythm,double>(),0);
    internal static ModelNeuron[] Cells(StructureEngine engine) => (ModelNeuron[])typeof(StructureEngine).GetField("_neurons",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(engine)!;
    internal static Dictionary<Guid,SynapseState> States(StructureEngine engine) => (Dictionary<Guid,SynapseState>)typeof(StructureEngine).GetField("_inboundSynapses",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(engine)!;

    sealed class IsolatedEngine : IDisposable
    {
        readonly string? oldDirectory=Environment.GetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR");
        readonly string? oldInstance=Environment.GetEnvironmentVariable("SERVICE_INSTANCE");
        public string Directory {get;}=Path.Combine(Path.GetTempPath(),"nre-contact-tests",Guid.NewGuid().ToString("N"));
        public StructureEngine Engine {get;private set;}=null!;
        public IsolatedEngine(bool create=true)
        {
            Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR",Directory);
            Environment.SetEnvironmentVariable("SERVICE_INSTANCE","mechanism");
            if(create) Create();
        }
        public void Create() => Engine=new(new StructureProfile(StructureId.VestibularNuclei,"LIF","STDP","mechanism test",new DelayWindow(1,5)));
        public void Reload() { Engine.Dispose(); Create(); }
        public SpikeMessage Spike(int index) => new() {MessageId=Guid.NewGuid(), TimestampMs=1,
            SourceStructure=StructureId.VestibularAfferents, TargetStructure=StructureId.VestibularNuclei,
            SourceInstanceKey="L_VestibularAfferents",SourceNeuronId=$"n-{index:000}",TargetNeuronId="auto-VestibularNuclei-000",
            SynapseId=Guid.Parse("afbc1111-1111-4111-8111-111111111111"), Neurotransmitter=NTEnum.GLUTAMATE,
            VesicleQuanta=1,ReuptakeRate=8,SpikeType=SpikeTypeEnum.ACTION_POTENTIAL};
        public void Dispose()
        {
            Engine?.Dispose();
            Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR",oldDirectory);
            Environment.SetEnvironmentVariable("SERVICE_INSTANCE",oldInstance);
        }
    }
}
