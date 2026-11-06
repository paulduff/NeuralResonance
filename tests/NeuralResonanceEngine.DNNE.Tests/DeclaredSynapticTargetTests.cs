using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.DNNE.Tests;

[Collection(EnvironmentVariableTestCollection.Name)]
public sealed class DeclaredSynapticTargetTests
{
    private static readonly StructureProfile Profile = new(StructureId.TemporalAssociation, "Izhikevich",
        "STDP+SynapticTaggingCapture", "isolated recipient identity check", new DelayWindow(0, 0));
    private static int Count => StructureCircuitProfile.For(Profile.StructureId).NeuronCount;
    private static SpikeMessage Input(StructureId source = StructureId.NucleusBasalis, NTEnum nt = NTEnum.ACETYLCHOLINE, int id = 1) => new()
    {
        MessageId = StableNeuralIdentity.Create("declared-target-input", id), TimestampMs = 0,
        SourceStructure = source, TargetStructure = Profile.StructureId,
        SourceInstanceKey = "fixture-source", SourceNeuronId = "n-000", TargetNeuronId = "neuromod-target-140",
        SynapseId = StableNeuralIdentity.Create("declared-target-projection", id),
        Neurotransmitter = nt, VesicleQuanta = 1.2f, ReuptakeRate = 20, SpikeType = SpikeTypeEnum.GRADED
    };
    private static IsolatedSynapticTargets Map(SpikeMessage input, int cell = 166) => new([IsolatedSynapticTarget.From(input, cell)], Count);

    [Fact]
    public void CanonicalOrderingPreservesFingerprintButChangingRecipientDoesNot()
    {
        var a = IsolatedSynapticTarget.From(Input(), 166); var b = IsolatedSynapticTarget.From(Input(id: 2), 165);
        var first = new IsolatedSynapticTargets([a,b], Count); var reordered = new IsolatedSynapticTargets([b,a], Count);
        Assert.Equal(first.Fingerprint, reordered.Fingerprint);
        Assert.NotEqual(first.Fingerprint, new IsolatedSynapticTargets([a with { TargetCell = 165 },b], Count).Fingerprint);
    }

    [Fact]
    public void DuplicateAndOutOfPopulationRecipientsAreRejected()
    {
        var a = IsolatedSynapticTarget.From(Input(), 166);
        Assert.Throws<ArgumentException>(() => new IsolatedSynapticTargets([a,a], Count));
        Assert.Throws<ArgumentException>(() => new IsolatedSynapticTargets([a,a with { TargetCell = 165 }], Count));
        Assert.Throws<ArgumentException>(() => new IsolatedSynapticTargets([a with { TargetCell = Count }], Count));
        Assert.Throws<ArgumentException>(() => new IsolatedSynapticTargets([a with { TargetCell = -1 }], Count));
        Assert.Throws<ArgumentException>(() => new IsolatedSynapticTargets([], Count));
        Assert.Throws<ArgumentException>(() => Map(Input()).ValidateFor(Count + 1));
    }

    [Fact]
    public void EverySourceAndContactFieldParticipatesInRecipientIdentity()
    {
        var input = Input(); var map = Map(input);
        Assert.True(map.TryResolve(input, out var cell)); Assert.Equal(166, cell);
        foreach (var mutation in new Action<SpikeMessage>[] {
            x => x.SourceInstanceKey="other", x => x.SourceNeuronId="n-001", x => x.SourceStructure=StructureId.Vta,
            x => x.ProjectionId=Guid.NewGuid(), x => x.Neurotransmitter=NTEnum.DOPAMINE,
            x => x.IsFeedback=true, x => x.IsSyntheticBackground=true })
        {
            var changed=Input(); mutation(changed); Assert.False(map.TryResolve(changed,out _));
        }
        input.ProjectionId=input.SynapseId;
        Assert.True(map.TryResolve(input,out _)); // Explicit projection and its legacy fallback identify the same axon.
    }

    [Fact]
    public void CallerOwnedEntriesCannotAlterTheFrozenMap()
    {
        var input=Input();var entries=new[] {IsolatedSynapticTarget.From(input,166)};
        var map=new IsolatedSynapticTargets(entries,Count);var fingerprint=map.Fingerprint;
        entries[0]=entries[0] with {TargetCell=165};
        Assert.True(map.TryResolve(input,out var actual));Assert.Equal(166,actual);Assert.Equal(fingerprint,map.Fingerprint);
        Assert.Throws<NotSupportedException>(() => ((IList<IsolatedSynapticTarget>)map.Entries)[0]=entries[0]);
    }

    [Fact]
    public void DeclaredContactsRequireFreshIsolatedStateAndPreserveExistingFiles()
    {
        using var environment=new EnvironmentScope();
        Environment.SetEnvironmentVariable("SERVICE_INSTANCE","live-cortex");
        Assert.Throws<InvalidOperationException>(() => new StructureEngine(Profile,null,diagnosticSynapticTargets:Map(Input())));
        Environment.SetEnvironmentVariable("SERVICE_INSTANCE","isolated-recipient-check");
        Directory.CreateDirectory(environment.DirectoryPath);var marker=Path.Combine(environment.DirectoryPath,"preserved.txt");
        File.WriteAllText(marker,"existing state must remain");
        Assert.Throws<InvalidOperationException>(() => new StructureEngine(Profile,null,diagnosticSynapticTargets:Map(Input())));
        Assert.Equal("existing state must remain",File.ReadAllText(marker));
    }

    [Fact]
    public async Task ActualDeclaredInputReachesTheCueCellAndItsLocalModulation()
    {
        var observed=await Run(166);
        Assert.All(observed.Where(e=>e.Before.Neurotransmitter is NTEnum.ACETYLCHOLINE or NTEnum.DOPAMINE),e=>Assert.Equal(166,e.Before.Target));
        var cue=observed.Where(e=>e.Before.SourceNeuron=="memory-feature-140").ToArray();
        Assert.NotEmpty(cue);Assert.All(cue,e=>Assert.Equal(166,e.Before.Target));
        Assert.Contains(cue,e=>e.Dopamine>0 && e.Acetylcholine>0);
        Assert.Contains(cue,e=>e.Kind=="modulation");
    }

    [Fact]
    public async Task NeighboringRecipientDoesNotBroadcastLocalModulatorsToTheCue()
    {
        var observed=await Run(165);
        Assert.All(observed.Where(e=>e.Before.Neurotransmitter is NTEnum.ACETYLCHOLINE or NTEnum.DOPAMINE),e=>Assert.Equal(165,e.Before.Target));
        var cue=observed.Where(e=>e.Before.SourceNeuron=="memory-feature-140").ToArray();
        Assert.NotEmpty(cue);Assert.All(cue,e=>{Assert.Equal(166,e.Before.Target);Assert.Equal(0,e.Dopamine);Assert.Equal(0,e.Acetylcholine);});
        Assert.DoesNotContain(cue,e=>e.Kind=="modulation");
    }

    [Fact]
    public async Task UndeclaredInputsRetainTheirExistingPopulationMapping()
    {
        var observed=await Run(null);
        var cue=observed.Where(e=>e.Before.SourceNeuron=="memory-feature-140").ToArray();
        Assert.NotEmpty(cue);Assert.All(cue,e=>Assert.Equal(166,e.Before.Target));
        var modulators=observed.Where(e=>e.Before.Neurotransmitter is NTEnum.ACETYLCHOLINE or NTEnum.DOPAMINE).ToArray();
        Assert.NotEmpty(modulators);Assert.All(modulators,e=>Assert.Equal(CorticalPopulation.Layer1Modulatory,CorticalLaminarTopology.PopulationForNeuron(e.Before.Target)));
    }

    private static async Task<MemoryLearningEvidence[]> Run(int? target)
    {
        using var environment=new EnvironmentScope();
        var ach=Input();var da=Input(StructureId.Vta,NTEnum.DOPAMINE,2);
        var map=target is { } cell ? new IsolatedSynapticTargets(new[] {ach,da}.Select(m=>IsolatedSynapticTarget.From(m,cell)),Count) : null;
        using var engine=new StructureEngine(Profile,null,diagnosticSynapticTargets:map);
        var events=new List<MemoryLearningEvidence>();engine.MemoryLearningObserver=events.Add;
        await engine.EnqueueSpikeBatchAsync([ach,da]);
        await engine.ProcessTickAsync(new TickSignal(1,25,25,new(),new Dictionary<BrainRhythm,double>(),0));
        var cue=Input(StructureId.EntorhinalCortex,NTEnum.GLUTAMATE,3);
        cue.SourceNeuronId="memory-feature-140";cue.TargetNeuronId="memory-target-140";
        cue.TimestampMs=30;cue.SpikeType=SpikeTypeEnum.ACTION_POTENTIAL;
        await engine.EnqueueSpikeBatchAsync([cue]);
        await engine.ProcessTickAsync(new TickSignal(2,55,30,new(),new Dictionary<BrainRhythm,double>(),0));
        return events.ToArray();
    }

    private sealed class EnvironmentScope : IDisposable
    {
        private readonly string? _directory=Environment.GetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR");
        private readonly string? _instance=Environment.GetEnvironmentVariable("SERVICE_INSTANCE");
        internal string DirectoryPath { get; }=Path.Combine(Path.GetTempPath(),"nre-declared-target-tests",Guid.NewGuid().ToString("N"));
        internal EnvironmentScope()
        {
            Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR",DirectoryPath);
            Environment.SetEnvironmentVariable("SERVICE_INSTANCE","isolated-recipient-check");
        }
        public void Dispose()
        {
            Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR",_directory);
            Environment.SetEnvironmentVariable("SERVICE_INSTANCE",_instance);
        }
    }
}
