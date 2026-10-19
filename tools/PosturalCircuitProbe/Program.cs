using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

namespace Nre.PosturalCircuitProbe;

internal static class Probe
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    static readonly JsonSerializerOptions Compact = new(JsonSerializerDefaults.Web);
    static readonly (StructureId Id, string Folder, string Model, string Plasticity, int Min, int Max)[] Profiles =
    [
        (StructureId.ProprioceptiveAfferents, "ProprioceptiveAfferents", "Izhikevich", "STDP", 1, 6),
        (StructureId.VestibularAfferents, "VestibularAfferents", "Izhikevich", "STDP", 1, 5),
        (StructureId.VestibularNuclei, "VestibularNuclei", "LIF", "STDP", 1, 5),
        (StructureId.CerebellarVermis, "Cerebellum.Vermis", "HH", "CerebellarLTD", 8, 16),
        (StructureId.ReticularFormation, "ReticularFormation", "Izhikevich", "HomeostaticGain", 2, 10),
        (StructureId.SpinalCordMotor, "SpinalCordMotor", "Izhikevich", "STDP", 1, 4),
        (StructureId.FastigialNucleus, "FastigialNucleus", "Izhikevich", "STDP", 3, 9)
    ];
    static readonly MethodInfo Rewriter = Private("RewriteSpikeForRoute");
    static readonly MethodInfo Efference = Private("BuildCerebellarEfferenceCopySpike");
    static readonly MethodInfo TargetResolver = Private("ResolveTargetInstances");
    static readonly MethodInfo ReflexDecoder = typeof(NeuronalMotorPopulationDecoder).GetMethod("DecodeBilateralRightingReflex", BindingFlags.NonPublic | BindingFlags.Static)!;

    static Probe()
    {
        Json.Converters.Add(new JsonStringEnumConverter());
        Compact.Converters.Add(new JsonStringEnumConverter());
    }

    static MethodInfo Private(string name) => typeof(TickCoordinator).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Coordinator helper unavailable: " + name);
    static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    static bool Under(string path, string root) => path.Equals(root, StringComparison.OrdinalIgnoreCase) || path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    static void Save(string path, object value) => File.WriteAllText(path, JsonSerializer.Serialize(value, Json));

    public static async Task<int> Main(string[] args)
    {
        if (args.Length < 4) throw new ArgumentException("Arguments: new-output-directory completed-run shutdown-backup repository [--smoke]");
        var output = Path.GetFullPath(args[0]);
        var run = Path.GetFullPath(args[1]);
        var backup = Path.GetFullPath(args[2]);
        var repo = Path.GetFullPath(args[3]);
        var smoke = args.Contains("--smoke");
        var localState = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NeuralResonanceEngine");
        if (Directory.Exists(output) || Under(output, run) || Under(output, backup) || Under(output, localState))
            throw new InvalidOperationException("Use a new isolated output directory outside all input and live-state folders.");
        Directory.CreateDirectory(output);
        var protectedFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var oldDirectory = Environment.GetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR");
        var oldInstance = Environment.GetEnvironmentVariable("SERVICE_INSTANCE");
        try
        {
            foreach (var p in Profiles)
            {
                var source = File.ReadAllText(Path.Combine(repo, "Structures", p.Folder, "Program.cs"));
                var pattern = $"StructureId\\.{p.Id}\\s*,\\s*\"{p.Model}\"\\s*,\\s*\"{p.Plasticity}\"[\\s\\S]*new DelayWindow\\({p.Min},\\s*{p.Max}\\)";
                if (!Regex.IsMatch(source, pattern)) throw new InvalidOperationException("Profile differs from original structure host: " + p.Id);
            }
            var tracePath = Path.Combine(run, "standing-trace.jsonl");
            var coordinatorPath = Path.Combine(run, "coordinator-before.json");
            protectedFiles[tracePath] = Hash(tracePath);
            protectedFiles[coordinatorPath] = Hash(coordinatorPath);
            using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(backup, "backup-manifest.json")));
            var hashes = manifest.RootElement.GetProperty("Files").EnumerateArray().ToDictionary(
                row => row.GetProperty("Path").GetString()!, row => row.GetProperty("Sha256").GetString()!, StringComparer.OrdinalIgnoreCase);
            foreach (var p in Profiles)
            foreach (var side in new[] { "L", "R" })
            {
                var relative = Path.Combine("synapses-action38-hand-v1", $"{side}_{p.Id}.synapses.json");
                var path = Path.Combine(backup, relative);
                var digest = Hash(path);
                if (!hashes.TryGetValue(relative, out var expected) || !digest.Equals(expected, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Saved synapse backup failed verification: " + relative);
                protectedFiles[path] = digest;
            }
            var frames = File.ReadLines(tracePath).Select(line =>
            {
                using var doc = JsonDocument.Parse(line);
                return doc.RootElement.GetProperty("body").Deserialize<PhysicalBodyFrameRequest>(Json)!;
            }).ToArray();
            var stable = frames.First(frame => frame.Articulation?.Musculoskeletal?.Balance?.Phase == "stable");
            var fallen = frames.First(frame => frame.Articulation?.Musculoskeletal?.Balance?.Phase == "fallen");
            using var coordinator = JsonDocument.Parse(File.ReadAllText(coordinatorPath));
            var connectivity = coordinator.RootElement.GetProperty("connectivityMap").EnumerateObject().ToDictionary(
                row => Enum.Parse<StructureId>(row.Name, ignoreCase: true), row => row.Value.Deserialize<List<SynapticConnection>>(Json)!);
            var selected = Profiles.Select(p => p.Id).ToHashSet();
            var routes = connectivity.Where(pair => selected.Contains(pair.Key)).ToDictionary(pair => pair.Key,
                pair => (IReadOnlyList<SynapticConnection>)pair.Value.Where(route => selected.Contains(route.Target)).ToArray());
            var settings = NeuronalMotorControlSettings.FromConfiguration(new ConfigurationBuilder().AddJsonFile(Path.Combine(repo, "ControlProgram", "appsettings.json")).Build());
            var plans = smoke ? new[] { "stable-fallen-stable", "fallen-silent", "fallen-left-only" } :
                new[] { "stable-hold", "fallen-hold", "stable-fallen-stable", "fallen-silent", "fallen-left-only" };
            var limits = smoke ? new[] { 24 } : new[] { 24, int.MaxValue };
            Save(Path.Combine(output, "protocol.json"), new
            {
                Smoke = smoke, NeuralTickMilliseconds = 1, SensoryIntervalMilliseconds = smoke ? 20 : 140,
                CaseMilliseconds = smoke ? 200 : 6000, Cases = plans,
                SourceSpikeLimits = limits.Select(limit => limit == int.MaxValue ? "unrestricted-in-subcircuit" : limit.ToString()),
                Regions = Profiles.Select(p => new { p.Id, p.Folder, p.Model, p.Plasticity, p.Min, p.Max }),
                Inputs = new { StableSequence = stable.Sequence, FallenSequence = fallen.Sequence, SourceRun = run },
                ProtectedInputs = protectedFiles, SourceSynapseBackup = backup,
                Scope = "Fourteen in-process engines: seven regions in both hemispheres; synchronous 1 ms biological clock and next-tick delivery using actual coordinator routing helpers. Held captured frames and deliberate phase steps; not a replica of the whole-brain wall-clock scheduler or a physical recovery experiment.",
                FullPhysicalTrajectoryReplayed = false, ExternalBrainRegionsIncluded = false,
                BackgroundNoiseInjectionIncluded = false, WholeBrainSchedulerReplicated = false,
                GlobalSourceBudgetApplied = false, MembraneStatesReinitialized = true, NormalPlasticityWithinIsolatedCopies = true,
                LiveBrainAccessed = false, PhysicalRecoveryAssessed = false
            });
            var summaries = new List<object>();
            foreach (var weights in new[] { "saved", "fresh" })
            foreach (var limit in limits)
            foreach (var plan in plans)
            {
                var label = $"{weights}-{(limit == int.MaxValue ? "all" : limit)}-{plan}";
                Console.WriteLine("CASE " + label);
                summaries.Add(await RunCase(output, backup, label, weights, limit, plan, smoke, stable, fallen, routes, settings));
                foreach (var file in protectedFiles)
                    if (Hash(file.Key) != file.Value) throw new InvalidOperationException("Protected input changed: " + file.Key);
                Save(Path.Combine(output, "progress.json"), new { CompletedCases = summaries.Count, TotalCases = 2 * limits.Length * plans.Length, LastCase = label });
            }
            Save(Path.Combine(output, "results.json"), new { Completed = true, AllCasesFinite = true, Smoke = smoke,
                CaseCount = summaries.Count, ProtectedInputsUnchanged = true, LiveBrainAccessed = false,
                FullMotorAuthorityAssessed = false, PhysicalRecoveryAssessed = false, Results = summaries });
            Console.WriteLine("ALL CASES COMPLETE. Completion is separate from discrimination or recovery quality.");
            return 0;
        }
        catch (Exception error)
        {
            Save(Path.Combine(output, "STUDY_FAILED.json"), new { Error = error.ToString(), LiveBrainAccessed = false });
            Console.Error.WriteLine(error);
            return 1;
        }
        finally
        {
            Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR", oldDirectory);
            Environment.SetEnvironmentVariable("SERVICE_INSTANCE", oldInstance);
        }
    }

    static async Task<object> RunCase(string output, string backup, string label, string weights, int limit, string plan, bool smoke,
        PhysicalBodyFrameRequest stable, PhysicalBodyFrameRequest fallen,
        IReadOnlyDictionary<StructureId, IReadOnlyList<SynapticConnection>> routes, NeuronalMotorControlSettings settings)
    {
        var directory = Path.Combine(output, label);
        var stateDirectory = Path.Combine(directory, "isolated-synapses");
        Directory.CreateDirectory(stateDirectory);
        Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR", stateDirectory);
        var nodes = new List<(ServiceInstance Instance, StructureEngine Engine)>();
        var initial = new List<object>();
        try
        {
            foreach (var p in Profiles)
            foreach (var side in new[] { "L", "R" })
            {
                var key = $"{side}_{p.Id}";
                var target = Path.Combine(stateDirectory, key + ".synapses.json");
                if (weights == "saved") File.Copy(Path.Combine(backup, "synapses-action38-hand-v1", key + ".synapses.json"), target);
                var initialHash = File.Exists(target) ? Hash(target) : null;
                int expectedInbound = 0, expectedOutbound = 0;
                if (weights == "saved")
                {
                    using var saved = JsonDocument.Parse(File.ReadAllText(target));
                    if (saved.RootElement.GetProperty("SchemaVersion").GetInt32() != SynapsePersistenceStore.CurrentSchemaVersion ||
                        saved.RootElement.GetProperty("InstanceKey").GetString() != key)
                        throw new InvalidOperationException("Saved state is incompatible with the isolated instance: " + key);
                    expectedInbound = saved.RootElement.GetProperty("Inbound").GetArrayLength();
                    expectedOutbound = saved.RootElement.GetProperty("Outbound").GetArrayLength();
                }
                Environment.SetEnvironmentVariable("SERVICE_INSTANCE", key);
                var profile = new StructureProfile(p.Id, p.Model, p.Plasticity, "Isolated replay using original host profile", new DelayWindow(p.Min, p.Max));
                var engine = new StructureEngine(profile);
                nodes.Add((new ServiceInstance(p.Id, key, side, new Uri("http://127.0.0.1:1")), engine));
                int Count(string field) => ((System.Collections.ICollection)typeof(StructureEngine).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(engine)!).Count;
                var loadedInbound = Count("_inboundSynapses"); var loadedOutbound = Count("_outboundSynapses");
                if (loadedInbound != expectedInbound || loadedOutbound != expectedOutbound)
                    throw new InvalidOperationException($"Saved state was not loaded exactly for {key}: expected {expectedInbound}/{expectedOutbound}, loaded {loadedInbound}/{loadedOutbound}.");
                initial.Add(new { InstanceKey = key, CopiedSavedState = weights == "saved", InitialSha256 = initialHash, ExpectedInbound = expectedInbound, ExpectedOutbound = expectedOutbound, LoadedInbound = loadedInbound, LoadedOutbound = loadedOutbound });
            }
            Save(Path.Combine(directory, "initial-state.json"), initial);
            var instances = nodes.GroupBy(node => node.Instance.StructureId).ToDictionary(group => group.Key, group => group.Select(node => node.Instance).ToList());
            var engines = nodes.ToDictionary(node => node.Instance.InstanceKey, node => node.Engine);
            var transducer = new PhysicalBodyTransducerRuntime();
            var duration = smoke ? 200 : 6000;
            var interval = smoke ? 20 : 140;
            long sequence = 0, primaryQueued = 0, rightPrimaryQueued = 0, primaryRighting = 0, sourceDrops = 0, routed = 0, queueRejected = 0, taggedRelays = 0;
            var phaseStats = new Dictionary<string, PhaseStats>();
            var previous = NeuronalMotorRuntime.Default;
            using var trace = new StreamWriter(Path.Combine(directory, "neural-trace.jsonl"));
            for (var tick = 1; tick <= duration; tick++)
            {
                bool stablePhase = plan == "stable-hold" || plan == "stable-fallen-stable" && (tick <= duration / 3 || tick > duration * 2 / 3);
                var phase = stablePhase ? "captured-stable" : "captured-fallen";
                var template = stablePhase ? stable : fallen;
                if ((tick - 1) % interval == 0)
                {
                    var request = template with { Sequence = ++sequence, TimestampMs = tick, MotorTrainingMode = false };
                    if (!PhysicalBodyFrameDescriptor.TryCreate(request, out var descriptor, out var error)) throw new InvalidOperationException(error);
                    var input = transducer.Transduce(descriptor!, tick, tick);
                    foreach (var node in nodes.Where(node => node.Instance.StructureId is StructureId.ProprioceptiveAfferents or StructureId.VestibularAfferents))
                    {
                        if (plan == "fallen-silent" || plan == "fallen-left-only" && node.Instance.Hemisphere == "R") continue;
                        var messages = input.For(node.Instance.StructureId, node.Instance.Hemisphere);
                        var accepted = await node.Engine.EnqueueSpikeBatchAsync(messages);
                        primaryQueued += accepted;
                        if (node.Instance.Hemisphere == "R") rightPrimaryQueued += accepted;
                        primaryRighting += messages.Count(RightingReflexTopology.IsPrimaryRightingAfferent);
                        queueRejected += messages.Count - accepted;
                    }
                }
                var signal = new TickSignal(tick, tick, 1.0, new NeuromodState(), new Dictionary<BrainRhythm, double>(), 0f);
                var processed = new List<(ServiceInstance Instance, StructureStepResult Step)>();
                foreach (var node in nodes) processed.Add((node.Instance, await node.Engine.ProcessStepAsync(signal, 0)));
                var snapshots = processed.Select(item => Snapshot(item.Instance, item.Step.Ack)).ToArray();
                var batches = nodes.ToDictionary(node => node.Instance.InstanceKey, _ => new List<SpikeMessage>());
                foreach (var item in processed)
                {
                    var selected = item.Step.OutboundSpikes.Take(limit);
                    sourceDrops += Math.Max(0, item.Step.OutboundSpikes.Count - limit);
                    if (!routes.TryGetValue(item.Instance.StructureId, out var candidates)) continue;
                    foreach (var spike in selected)
                    foreach (var route in TickCoordinator.ResolveRoutes(candidates, spike))
                    {
                        bool efference = item.Instance.StructureId == StructureId.SpinalCordMotor && route.Target == StructureId.CerebellarVermis && spike.TargetStructure != StructureId.CerebellarVermis;
                        var message = (SpikeMessage)(efference ? Efference : Rewriter).Invoke(null, [spike, route])!;
                        var targets = (IReadOnlyList<ServiceInstance>)TargetResolver.Invoke(null, [route.Target, item.Instance.Hemisphere, instances, item.Instance.StructureId, route.ProjectionType])!;
                        foreach (var target in targets)
                        {
                            batches[target.InstanceKey].Add(message);
                            routed++;
                            if (message.IsRightingCircuitSpike) taggedRelays++;
                        }
                    }
                }
                foreach (var batch in batches.Where(pair => pair.Value.Count > 0))
                    queueRejected += batch.Value.Count - await engines[batch.Key].EnqueueSpikeBatchAsync(batch.Value);
                var posture = VestibuloReticularPopulationDecoder.Decode(snapshots)!;
                previous = NeuronalMotorPopulationDecoder.Decode(tick, snapshots, new NeuronalMotorControlSnapshot(settings), previous, tick);
                if (snapshots.Any(snapshot => !float.IsFinite(snapshot.MeanFiringRateHz)) || !float.IsFinite(posture.BalanceError))
                    throw new InvalidOperationException("Non-finite circuit response in " + label);
                if (tick % 10 == 0)
                {
                    var reflex = ReflexDecoder.Invoke(null, [snapshots])!;
                    double Read(string name) => Convert.ToDouble(reflex.GetType().GetProperty(name)!.GetValue(reflex));
                    var afferent = Read("AfferentDrive"); var descending = Read("DescendingDrive"); var drive = Read("Drive");
                    if (!phaseStats.TryGetValue(phase, out var stats)) phaseStats[phase] = stats = new PhaseStats();
                    stats.Add(posture.BalanceError, afferent, descending, drive, previous.RightingLatchActive);
                    trace.WriteLine(JsonSerializer.Serialize(new { Tick = tick, Phase = phase, PhysicalBalanceError = template.Articulation!.Musculoskeletal!.BalanceError,
                        PopulationError = posture.BalanceError, Afferent = afferent, Descending = descending, BilateralDrive = drive,
                        Latch = previous.RightingLatchActive, PartialCircuitMotorCoverage = previous.MotorCircuitCoverage,
                        Regions = snapshots.Select(snapshot => new { Instance = snapshot.Instance.InstanceKey, snapshot.MeanFiringRateHz, snapshot.SpikeInCount, snapshot.SpikeOutCount, snapshot.FeedbackQueueDepth }) }, Compact));
                }
                if (tick % 1000 == 0) Console.WriteLine($"  {label}: {tick}/{duration} neural ms, input={primaryQueued}, routed={routed}, rejected={queueRejected}");
            }
            if (plan == "fallen-silent" && primaryQueued != 0 || plan == "fallen-left-only" && rightPrimaryQueued != 0)
                throw new InvalidOperationException("Input control did not hold in " + label);
            var result = new { Case = label, Weights = weights, Plan = plan, SourceLimit = limit == int.MaxValue ? "unrestricted-in-subcircuit" : limit.ToString(),
                PrimaryQueued = primaryQueued, RightPrimaryQueued = rightPrimaryQueued, PrimaryFallSensitiveMessages = primaryRighting,
                SourceBudgetDrops = sourceDrops, RoutedMessages = routed, TaggedRelays = taggedRelays, QueueRejected = queueRejected,
                Phases = phaseStats, Finite = true, FullMotorAuthorityAssessed = false, PhysicalRecoveryAssessed = false };
            Save(Path.Combine(directory, "summary.json"), result);
            return result;
        }
        finally { foreach (var node in nodes) node.Engine.Dispose(); }
    }

    static InstanceStructureSnapshot Snapshot(ServiceInstance instance, TickAck ack) => new(instance, ack.StructureId, ack.ActiveNeuronCount,
        ack.MeanFiringRateHz, ack.DominantRhythm, [], ack.LocalNeuromod, ack.SpikeInCount, ack.SpikeOutCount, ack.FeedbackQueueDepth,
        VestibuloReticularDiagnostics: ack.VestibuloReticularDiagnostics, ActionSelectionDiagnostics: ack.ActionSelectionDiagnostics,
        CerebellarDiagnostics: ack.CerebellarDiagnostics, SourceTick: ack.Tick);

    sealed class PhaseStats
    {
        public int Samples { get; private set; }
        public double MeanPopulationError => Samples == 0 ? 0 : errorSum / Samples;
        public double PeakPopulationError { get; private set; }
        public double PeakAfferent { get; private set; }
        public double PeakDescending { get; private set; }
        public int LatchSamples { get; private set; }
        public int DrivePresentButErrorGateClosed { get; private set; }
        double errorSum;
        public void Add(double error, double afferent, double descending, double drive, bool latch)
        {
            Samples++; errorSum += error; PeakPopulationError = Math.Max(PeakPopulationError, error);
            PeakAfferent = Math.Max(PeakAfferent, afferent); PeakDescending = Math.Max(PeakDescending, descending);
            if (latch) LatchSamples++;
            if (drive >= .04 && error < .4) DrivePresentButErrorGateClosed++;
        }
    }
}
