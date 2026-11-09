using System.Diagnostics;
using System.Globalization;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

if (args.Length != 1 || !Path.IsPathFullyQualified(args[0]) || Directory.Exists(args[0]))
    throw new ArgumentException("Supply a new absolute output directory; old brain state is never loaded.");
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
string root = args[0]; Directory.CreateDirectory(root);
var json = new JsonSerializerOptions { WriteIndented = true };
void Save(string path, object value) => File.WriteAllText(path, JsonSerializer.Serialize(value, json));
var cases = new[]
{
    new NeuralCase("temporal-live-rule", StructureId.TemporalAssociation, "Izhikevich", "STDP", new(0,0)),
    new NeuralCase("temporal-tag-candidate", StructureId.TemporalAssociation, "Izhikevich", "STDP+SynapticTaggingCapture", new(0,0)),
    new NeuralCase("granule-live-rule", StructureId.CerebellarGranule, "LIF", "MossyFiberLTP", new(0,0)),
    new NeuralCase("olive-live-rule", StructureId.InferiorOlive, "HH", "STDP", new(10,15)),
    new NeuralCase("purkinje-live-rule", StructureId.PurkinjeCellLayer, "HH", "PurkinjeCalciumLTD", new(10,15)),
    new NeuralCase("spinal-live-rule", StructureId.SpinalCordMotor, "Izhikevich", "STDP", new(1,4))
};
const int frameMs = 20, frames = 50, pairs = 3;
Save(Path.Combine(root, "protocol.json"), new { version = "isolated-cpu-costs-v2", cases,
    durationMs = frameMs * frames, frameMs, pairs, warmupRunsPerCase = 1,
    supply = NeuralSubstrateSupply.ReferenceBath, fullConfiguredPopulations = true,
    fixedInputsAreDiagnosticFixtures = true, liveServices = false, unityPhysicsMeasured = false,
    networkTransportMeasured = false, gpuMeasured = false, physiologicalQualification = false,
    generatedMessageIdsExcludedFromSemanticComparison = true,
    runtime = RuntimeInformation.FrameworkDescription, os = RuntimeInformation.OSDescription,
    cpuThreads = Environment.ProcessorCount, serverGc = GCSettings.IsServerGC, stopwatchFrequency = Stopwatch.Frequency });
string? oldDirectory = Environment.GetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR");
string? oldInstance = Environment.GetEnvironmentVariable("SERVICE_INSTANCE");
string? oldMicrotubules = Environment.GetEnvironmentVariable("NRE_MICROTUBULE_MODE");
var results = new List<NeuralRun>();
try
{
    // Fix the optional mode so separate invocations use the same workload.
    Environment.SetEnvironmentVariable("NRE_MICROTUBULE_MODE", "classical");
    foreach (var specification in cases)
    {
        var inputs = Enumerable.Range(0, frames).Select(frame => Inputs(specification, frame)).ToArray();
        string caseRoot = Path.Combine(root, specification.Key); Directory.CreateDirectory(caseRoot);
        Save(Path.Combine(caseRoot, "inputs.json"), inputs);
        for (int pair = -1; pair < pairs; ++pair)
        foreach (bool measured in pair < 0 ? new[] { true } : pair % 2 == 0 ? new[] { false, true } : new[] { true, false })
        {
            string name = pair < 0 ? "warmup" : $"pair-{pair}-{(measured ? "timed" : "plain")}";
            Console.WriteLine(specification.Key + " / " + name);
            string directory = Path.Combine(caseRoot, name); Directory.CreateDirectory(directory);
            Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR", Path.Combine(directory, "fresh-state"));
            Environment.SetEnvironmentVariable("SERVICE_INSTANCE", "isolated-compute-" + specification.Key);
            var profile = new StructureProfile(specification.Structure, specification.Model, specification.Rule,
                "isolated performance fixture; no physiological qualification", specification.Delay);
            var declared = specification.Key == "temporal-tag-candidate"
                ? new IsolatedSynapticTargets(inputs.SelectMany(x => x)
                    .Where(s => s.Neurotransmitter is NTEnum.ACETYLCHOLINE or NTEnum.DOPAMINE)
                    .DistinctBy(s => s.SynapseId).Select(s => IsolatedSynapticTarget.From(s, 166)),
                    StructureCircuitProfile.For(specification.Structure).NeuronCount) : null;
            double enqueueMs = 0, stepMs = 0, drainMs = 0;
            long allocated = 0; var gc = new int[3]; var outputs = new List<object>();
            object state; StructurePerformanceSample[] phases; int cells, contacts; long emitted = 0;
            var process = Process.GetCurrentProcess(); var cpuBefore = process.TotalProcessorTime;
            long completeStarted = Stopwatch.GetTimestamp();
            using (var engine = new StructureEngine(profile, null, diagnosticSynapticTargets: declared, diagnosticPerformance: measured))
            {
                for (int frame = 0; frame < frames; ++frame)
                {
                    var tick = new TickSignal(frame + 1, (frame + 1) * frameMs, frameMs, new(), new Dictionary<BrainRhythm,double>(), 0,
                        NeuralSubstrates: NeuralSubstrateSupply.ReferenceBath);
                    int[] collectionsBefore = Enumerable.Range(0, 3).Select(GC.CollectionCount).ToArray();
                    long bytesBefore = GC.GetAllocatedBytesForCurrentThread();
                    long start = Stopwatch.GetTimestamp();
                    await engine.EnqueueSpikeBatchAsync(inputs[frame]); enqueueMs += Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                    start = Stopwatch.GetTimestamp();
                    var ack = await engine.ProcessTickAsync(tick); stepMs += Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                    start = Stopwatch.GetTimestamp();
                    var outgoing = await engine.DrainOutboundSpikesAsync(); drainMs += Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                    allocated += GC.GetAllocatedBytesForCurrentThread() - bytesBefore;
                    for (int generation = 0; generation < 3; ++generation) gc[generation] += GC.CollectionCount(generation) - collectionsBefore[generation];
                    emitted += outgoing.Count;
                    outputs.Add(new { tick, ack, outgoing }); // No serialization in the timed calls.
                }
                phases = engine.DiagnosticPerformance?.Capture() ?? [];
                cells = ((Array)StateEvidence.Field(engine, "_neurons")!).Length;
                contacts = engine.CaptureMemoryContactEvidence().Length;
                state = StateEvidence.Engine(engine);
            }
            double completeMs = Stopwatch.GetElapsedTime(completeStarted).TotalMilliseconds;
            double cpuMs = (process.TotalProcessorTime - cpuBefore).TotalMilliseconds;
            Save(Path.Combine(directory, "raw-outputs.json"), outputs);
            File.WriteAllBytes(Path.Combine(directory, "state-evidence.json"), JsonSerializer.SerializeToUtf8Bytes(state));
            var semantic = StateEvidence.NormalizeGeneratedEventIds(JsonSerializer.SerializeToNode(outputs))!;
            File.WriteAllBytes(Path.Combine(directory, "semantic-outputs.json"), JsonSerializer.SerializeToUtf8Bytes(semantic));
            var result = new NeuralRun(specification.Key, name, pair, measured, cells, contacts, emitted,
                enqueueMs, stepMs, drainMs, completeMs, cpuMs, allocated, gc, phases,
                StateEvidence.Hash(semantic), StateEvidence.Hash(state));
            Save(Path.Combine(directory, "timing.json"), result); results.Add(result);
            if (pair >= 0 && results.Count(r => r.Key == specification.Key && r.Pair == pair) == 2)
            {
                var contrast = results.Where(r => r.Key == specification.Key && r.Pair == pair).ToArray();
                if (contrast[0].OutputsSha256 != contrast[1].OutputsSha256 || contrast[0].StateSha256 != contrast[1].StateSha256)
                    throw new InvalidOperationException("Timing observation changed saved neural evidence: " + specification.Key);
            }
        }
    }
    var body = MotorPlantStudy.Run(Path.Combine(root, "motor-units"));
    Save(Path.Combine(root, "results.json"), new { completed = true, neuralRuns = results,
        exactNeuralObservationPairs = cases.Length * pairs, body, gpuMeasured = false,
        networkTransportMeasured = false, unityPhysicsMeasured = false, physiologyQualified = false, avatarRecoveryAssessed = false });
}
catch (Exception error) { Save(Path.Combine(root, "FAILED.json"), new { error = error.ToString(), completedNeuralRuns = results }); throw; }
finally
{
    Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR", oldDirectory);
    Environment.SetEnvironmentVariable("SERVICE_INSTANCE", oldInstance);
    Environment.SetEnvironmentVariable("NRE_MICROTUBULE_MODE", oldMicrotubules);
}

static SpikeMessage[] Inputs(NeuralCase specification, int frame)
{
    var result = new List<SpikeMessage>();
    for (int i = 0; i < 16; ++i)
    {
        NTEnum nt = i < 8 ? NTEnum.GLUTAMATE : i < 12 ? NTEnum.GABA : i < 14 ? NTEnum.ACETYLCHOLINE : NTEnum.DOPAMINE;
        var source = nt switch { NTEnum.GABA => StructureId.ReticularFormation, NTEnum.ACETYLCHOLINE => StructureId.NucleusBasalis,
            NTEnum.DOPAMINE => StructureId.Vta, _ => specification.Structure == StructureId.PurkinjeCellLayer
                ? (i == 0 ? StructureId.InferiorOlive : StructureId.CerebellarGranule) : StructureId.EntorhinalCortex };
        result.Add(new SpikeMessage { MessageId = StableNeuralIdentity.Create("compute-input", specification.Key, frame, i),
            TimestampMs = frame * 20 + .25 + i * .03125, SourceStructure = source, TargetStructure = specification.Structure,
            SourceInstanceKey = "isolated-compute-fixture", SourceNeuronId = i < 8 ? $"memory-feature-{140+i}" : $"fixture-{i}",
            TargetNeuronId = i < 8 ? $"memory-target-{140+i}" : $"fixture-target-{i}",
            SynapseId = StableNeuralIdentity.Create("compute-input-projection", specification.Key, i),
            Neurotransmitter = nt, VesicleQuanta = 1.15f, ReuptakeRate = 8,
            SpikeType = nt is NTEnum.GLUTAMATE or NTEnum.GABA ? SpikeTypeEnum.ACTION_POTENTIAL : SpikeTypeEnum.GRADED });
    }
    return result.ToArray();
}

internal sealed record NeuralCase(string Key, StructureId Structure, string Model, string Rule, DelayWindow Delay);
internal sealed record NeuralRun(string Key, string Run, int Pair, bool Instrumented, int Cells, int Contacts, long Emitted,
    double EnqueueMs, double StepMs, double DrainMs, double CompleteWithEvidenceAndShutdownMs, double ProcessCpuMs,
    long TimedCallsAllocatedBytes, int[] TimedCallsGcCollections, StructurePerformanceSample[] Phases,
    string OutputsSha256, string StateSha256);
