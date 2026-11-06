using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

if (args.Length == 3 && args[0] == "--contact-replay")
{
    ContactReplayStudy.Run(args[1], args[2]);
    return;
}
if (args.Length == 3 && args[0] == "--contact-numerics")
{
    ContactNumericsStudy.Run(args[1], args[2]);
    return;
}

if (args.Length != 1 || !Path.IsPathFullyQualified(args[0]) || Directory.Exists(args[0]))
    throw new ArgumentException("Supply a new absolute output directory. Old brain saves are never loaded.");
string root = args[0]; Directory.CreateDirectory(root);
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
void Save(string path, object value) => File.WriteAllText(path, JsonSerializer.Serialize(value, new JsonSerializerOptions(json) { WriteIndented = true }));
var scenarios = new[]
{
    new Scenario("legacy-observed", false, 0, true, true),
    new Scenario("legacy-unobserved", false, 0, true, false),
    new Scenario("continuous-simultaneous", true, 0, true, true),
    new Scenario("continuous-spaced", true, 1, true, true),
    new Scenario("continuous-unmodulated", true, 0, false, true),
    new Scenario("continuous-colocated", true, 0, true, true, 166),
    new Scenario("continuous-neighbor", true, 0, true, true, 165)
};
Save(Path.Combine(root, "protocol.json"), new
{
    version = "cortical-memory-diagnostic-v2", scenarios, repetitions = 9, featureIndex = 140, cueEvents = 12,
    durationMs = 495, structure = "TemporalAssociation", neuronModel = "Izhikevich", rule = "STDP+SynapticTaggingCapture",
    diagnosticOnly = true, learningParametersChanged = false, originalTestChanged = false, physiologicalQualification = false
});
var completed = new List<object>();
string? oldDirectory = Environment.GetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR");
string? oldInstance = Environment.GetEnvironmentVariable("SERVICE_INSTANCE");
try
{
    foreach (var scenario in scenarios)
    {
        Console.WriteLine(scenario.Key);
        string output = Path.Combine(root, scenario.Key); Directory.CreateDirectory(output);
        string stateDirectory = Path.Combine(output, "fresh-state");
        if (Directory.Exists(stateDirectory)) throw new InvalidOperationException("Fresh state path already exists.");
        Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR", stateDirectory);
        // Identical instance and event/contact IDs across independent fresh runs
        // permit a direct observation-on/off comparison. Nothing is restored.
        Environment.SetEnvironmentVariable("SERVICE_INSTANCE", "isolated-cortical-memory-evidence");
        using var learning = new StreamWriter(Path.Combine(output, "learning.jsonl"));
        using var inbound = new StreamWriter(Path.Combine(output, "inbound.jsonl"));
        using var inputs = new StreamWriter(Path.Combine(output, "inputs.jsonl"));
        using var ticks = new StreamWriter(Path.Combine(output, "ticks.jsonl"));
        var declaredTargets = scenario.ModulatorTarget is { } recipient
            ? new IsolatedSynapticTargets(Enumerable.Range(0, 9).SelectMany(r => Modulation(55 * r, r))
                .Select(message => IsolatedSynapticTarget.From(message, recipient)),
                StructureCircuitProfile.For(StructureId.TemporalAssociation).NeuronCount) : null;
        using var engine = new StructureEngine(new StructureProfile(StructureId.TemporalAssociation, "Izhikevich",
            "STDP+SynapticTaggingCapture", "isolated cortical fixture diagnosis", new DelayWindow(0, 0)),
            isolatedNeuronCount: null, diagnosticSynapticTargets: declaredTargets);
        Save(Path.Combine(output, "declared-targets.json"), new { fingerprint = engine.DiagnosticSynapticTargetFingerprint,
            entries = declaredTargets?.Entries, receptorCalibrationQualified = false });
        long learningEvents = 0, inboundEvents = 0;
        if (scenario.Observe)
        {
            engine.MemoryLearningObserver = value => { learning.WriteLine(JsonSerializer.Serialize(value, json)); ++learningEvents; };
            engine.InboundIntegrationObserver = value => { inbound.WriteLine(JsonSerializer.Serialize(value, json)); ++inboundEvents; };
        }
        long tick = 0; double previous = 0;
        for (int repetition = 0; repetition < 9; ++repetition)
        {
            var modulation = scenario.Modulation ? Modulation(previous, repetition) : [];
            if (modulation.Length > 0) await engine.EnqueueSpikeBatchAsync(modulation);
            inputs.WriteLine(JsonSerializer.Serialize(new { repetition, phase = "modulation", spikes = modulation }, json));
            double end = previous + 25;
            var signal = new TickSignal(++tick, end, scenario.Continuous ? 25 : 10,
                new NeuromodState(), new Dictionary<BrainRhythm, double>(), 0f);
            var ack = await engine.ProcessTickAsync(signal);
            var emitted = await engine.DrainOutboundSpikesAsync();
            ticks.WriteLine(JsonSerializer.Serialize(new { repetition, phase = "modulation", signal, ack,
                contacts = engine.CaptureMemoryContactEvidence(), emitted }, json));
            var cues = Enumerable.Range(0, 12).Select(index => new SpikeMessage
            {
                MessageId = StableNeuralIdentity.Create("memory-evidence-cue", repetition, index),
                TimestampMs = previous + 30 + scenario.SpacingMs * index,
                SourceStructure = StructureId.EntorhinalCortex, TargetStructure = StructureId.TemporalAssociation,
                SourceNeuronId = "memory-feature-140", TargetNeuronId = "memory-target-140",
                SynapseId = StableNeuralIdentity.Create("memory-evidence-cue-projection"),
                Neurotransmitter = NTEnum.GLUTAMATE, VesicleQuanta = 1.15f, ReuptakeRate = 8f,
                SpikeType = index % 4 == 0 ? SpikeTypeEnum.BURST : SpikeTypeEnum.ACTION_POTENTIAL
            }).ToArray();
            await engine.EnqueueSpikeBatchAsync(cues);
            inputs.WriteLine(JsonSerializer.Serialize(new { repetition, phase = "cue", spikes = cues }, json));
            end = previous + 55;
            signal = new TickSignal(++tick, end, scenario.Continuous ? 30 : 10,
                new NeuromodState(), new Dictionary<BrainRhythm, double>(), 0f);
            ack = await engine.ProcessTickAsync(signal); emitted = await engine.DrainOutboundSpikesAsync();
            ticks.WriteLine(JsonSerializer.Serialize(new { repetition, phase = "cue", signal, ack,
                contacts = engine.CaptureMemoryContactEvidence(), emitted }, json));
            previous = end;
        }
        completed.Add(new { scenario.Key, completed = true, ticks = tick, finalTimeMs = previous,
            learningEvents, inboundEvents, observed = scenario.Observe, freshState = stateDirectory });
    }
    Save(Path.Combine(root, "results.json"), new { completed = true, cases = completed,
        originalTestPassed = (bool?)null, biologicalConsolidationQualified = false, avatarRecoveryAssessed = false });
}
catch (Exception error) { Save(Path.Combine(root, "FAILED.json"), new { error = error.ToString(), completed }); throw; }
finally
{
    Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR", oldDirectory);
    Environment.SetEnvironmentVariable("SERVICE_INSTANCE", oldInstance);
}

static SpikeMessage[] Modulation(double previous, int repetition)
{
    var target = StructureId.TemporalAssociation; var circuit = StructureCircuitProfile.For(target);
    var kernel = CircuitKernelFactory.For(target);
    int cell = kernel.ResolveInboundNeuronIndex(new SpikeMessage
    {
        SourceStructure = StructureId.EntorhinalCortex, TargetStructure = target,
        SourceNeuronId = "memory-feature-140", TargetNeuronId = "memory-target-140",
        SynapseId = Guid.Empty, Neurotransmitter = NTEnum.GLUTAMATE
    }, circuit.NeuronCount, circuit);
    var result = new List<SpikeMessage>();
    foreach (var (source, nt, count, quanta, reuptake, offset) in new[]
    {
        (StructureId.NucleusBasalis, NTEnum.ACETYLCHOLINE, 4, 1.2f, 20f, 0d),
        (StructureId.LocusCoeruleus, NTEnum.NOREPINEPHRINE, 4, 1.1f, 30f, 0d),
        (StructureId.Vta, NTEnum.DOPAMINE, 6, 1.4f * .55f, 40f, 1d)
    })
    {
        int sourceIndex = Enumerable.Range(0, circuit.NeuronCount * 32).First(index =>
            kernel.ResolveInboundNeuronIndex(new SpikeMessage
            {
                SourceStructure = source, TargetStructure = target, SourceNeuronId = $"n-{index:000}",
                TargetNeuronId = "neuromod-target-140", SynapseId = Guid.Empty, Neurotransmitter = nt
            }, circuit.NeuronCount, circuit) == cell);
        for (int i = 0; i < count; ++i)
            result.Add(new SpikeMessage
            {
                MessageId = StableNeuralIdentity.Create("memory-evidence-modulation", repetition, source, i),
                TimestampMs = previous + offset + i * .01, SourceStructure = source, TargetStructure = target,
                SourceNeuronId = $"n-{sourceIndex:000}", TargetNeuronId = "neuromod-target-140",
                SynapseId = StableNeuralIdentity.Create("memory-evidence-modulation-projection", repetition, source, i),
                Neurotransmitter = nt, VesicleQuanta = quanta, ReuptakeRate = reuptake, SpikeType = SpikeTypeEnum.GRADED
            });
    }
    return result.ToArray();
}

internal sealed record Scenario(string Key, bool Continuous, double SpacingMs, bool Modulation, bool Observe, int? ModulatorTarget = null);
