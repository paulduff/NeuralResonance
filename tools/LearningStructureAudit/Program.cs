using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

internal static class LearningAudit
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    static readonly BindingFlags HiddenInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly BindingFlags HiddenStatic = BindingFlags.Static | BindingFlags.NonPublic;
    static readonly MethodInfo Rewrite = typeof(TickCoordinator).GetMethod("RewriteSpikeForRoute", HiddenStatic)!;
    static readonly MethodInfo Project = typeof(StructureEngine).GetMethod("ResolveInboundNeuronIndex", HiddenInstance)!;
    static readonly MethodInfo OutboundDelta = typeof(StructureEngine).GetMethod("ComputeOutboundPlasticityDelta", HiddenInstance)!;
    static readonly FieldInfo Inbound = typeof(StructureEngine).GetField("_inboundSynapses", HiddenInstance)!;
    static readonly FieldInfo Neurons = typeof(StructureEngine).GetField("_neurons", HiddenInstance)!;

    static void Save(string output, string name, object data) => File.WriteAllText(Path.Combine(output, name + ".json"), JsonSerializer.Serialize(data, Json));
    static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    static Dictionary<Guid, SynapseState> States(StructureEngine engine) => (Dictionary<Guid, SynapseState>)Inbound.GetValue(engine)!;
    static StructureEngine Engine(string output, string name, StructureId id = StructureId.VestibularNuclei, string model = "LIF", string rule = "STDP")
    {
        Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR", Path.Combine(output, "isolated-state", name));
        Environment.SetEnvironmentVariable("SERVICE_INSTANCE", "audit-" + name);
        return new(new StructureProfile(id, model, rule, "Isolated mechanism audit; not learned behavior", new DelayWindow(1, 5)));
    }
    static TickSignal Tick(int tick) => new(tick, tick, 1, new NeuromodState(), new Dictionary<BrainRhythm, double>(), 0);
    static SpikeMessage Spike(int source, int time = 1) => new()
    {
        MessageId = Guid.NewGuid(), TimestampMs = time, SourceStructure = StructureId.VestibularAfferents,
        TargetStructure = StructureId.VestibularNuclei, SourceNeuronId = $"n-{source:000}", TargetNeuronId = "auto-VestibularNuclei-000",
        SynapseId = Guid.Parse("afbc1111-1111-4111-8111-111111111111"), Neurotransmitter = NTEnum.GLUTAMATE,
        VesicleQuanta = 1, ReuptakeRate = 8, SpikeType = SpikeTypeEnum.ACTION_POTENTIAL
    };

    public static async Task<int> Main(string[] args)
    {
        if (args.Length != 2) throw new ArgumentException("Arguments: repository new-output-directory");
        var repo = Path.GetFullPath(args[0]); var output = Path.GetFullPath(args[1]);
        if (Directory.Exists(output) || !Directory.Exists(Path.Combine(repo, "Structures"))) throw new InvalidOperationException("Use an existing repository and a new output directory.");
        var liveState = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NeuralResonanceEngine");
        if (output.StartsWith(liveState, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Output cannot be in live neural state.");
        Directory.CreateDirectory(output);
        var oldDirectory = Environment.GetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR");
        var oldInstance = Environment.GetEnvironmentVariable("SERVICE_INSTANCE");
        try
        {
            var sourcePaths = new[] { "ControlProgram/Program.cs", "ControlProgram/NeuronalMotorControl.cs", "Protocol/SpikeMessage.cs", "Structures/_SharedRuntime/StructureEngine.cs", "Structures/_SharedRuntime/ModelNeuron.cs", "Structures/_SharedRuntime/PlasticityRules.cs", "Structures/_SharedRuntime/SynapseState.cs", "Structures/_SharedRuntime/SynapsePersistenceStore.cs", "Structures/_SharedRuntime/CircuitKernels.cs", "Structures/_SharedRuntime/StructureCircuitProfile.cs", "Structures/_SharedRuntime/IntrinsicCircuitDriveTopology.cs" };
            var hashes = sourcePaths.ToDictionary(path => path, path => Hash(Path.Combine(repo, path)));
            Save(output, "inventory", Inventory(repo));
            Save(output, "connection-identity", await Identity(output));
            Save(output, "postsynaptic-event", await PostEvent(output));
            Save(output, "delayed-modulation", await DelayedModulation(output));
            Save(output, "outbound-learning", Outbound(output));
            Save(output, "cadence", Cadence());
            Save(output, "clock-budget", ClockBudget());
            foreach (var source in hashes) if (Hash(Path.Combine(repo, source.Key)) != source.Value) throw new InvalidOperationException("Reviewed source changed during audit: " + source.Key);
            Save(output, "completion", new { Completed = true, SourceHashes = hashes, LiveBrainAccessed = false,
                PhysicalRecoveryAssessed = false, BiologicalEquivalenceQualified = false,
                Scope = "Read-only source inventory and isolated mechanism characterization. Forced cell stimulation is explicitly diagnostic; no brain services or saved brain weights are accessed." });
            Console.WriteLine("AUDIT CAPTURES COMPLETE. Completion does not mean biological requirements passed.");
            return 0;
        }
        catch (Exception error) { Save(output, "AUDIT_FAILED", new { Error = error.ToString() }); Console.Error.WriteLine(error); return 1; }
        finally
        {
            Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR", oldDirectory);
            Environment.SetEnvironmentVariable("SERVICE_INSTANCE", oldInstance);
        }
    }

    static object Inventory(string repo)
    {
        var profiles = new List<(StructureId Id, string Model, string Rule, string Path)>();
        foreach (var directory in Directory.GetDirectories(Path.Combine(repo, "Structures")))
        {
            var file = Path.Combine(directory, "Program.cs"); if (!File.Exists(file)) continue;
            var match = Regex.Match(File.ReadAllText(file), "new StructureProfile\\(\\s*StructureId\\.(\\w+)\\s*,\\s*\"([^\"]+)\"\\s*,\\s*\"([^\"]+)\"");
            if (match.Success) profiles.Add((Enum.Parse<StructureId>(match.Groups[1].Value), match.Groups[2].Value, match.Groups[3].Value, Path.GetRelativePath(repo, file)));
        }
        using var config = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo, "ControlProgram", "appsettings.json")));
        var registry = config.RootElement.GetProperty("ServiceRegistry").EnumerateObject().Select(item => Enum.Parse<StructureId>(item.Name)).ToHashSet();
        var rows = profiles.OrderBy(item => (int)item.Id).Select(item => new
        {
            item.Id, item.Model, PlasticityRule = item.Rule, HostSource = item.Path, HostSourceSha256 = Hash(Path.Combine(repo, item.Path)),
            NeuronsPerInstance = StructureCircuitProfile.For(item.Id).NeuronCount,
            CircuitKernel = CircuitKernelFactory.For(item.Id).GetType().Name,
            CorticalLaminar = CorticalLaminarTopology.IsCorticalStructure(item.Id), Registered = registry.Contains(item.Id),
            InboundLtdCanReceiveClimbingCoincidence = item.Rule != "CerebellarLTD" || item.Id == StructureId.PurkinjeCellLayer
        }).ToArray();
        return new { EnumStructures = Enum.GetValues<StructureId>().Length, RegistryStructures = registry.Count,
            HostProfiles = rows.Length, MissingHosts = registry.Except(profiles.Select(item => item.Id)),
            ModelCounts = profiles.GroupBy(item => item.Model).ToDictionary(group => group.Key, group => group.Count()),
            RuleCounts = profiles.GroupBy(item => item.Rule).ToDictionary(group => group.Key, group => group.Count()),
            KernelCount = rows.Select(row => row.CircuitKernel).Distinct().Count(), TotalNeuronsOneInstanceEach = rows.Sum(row => row.NeuronsPerInstance),
            MinimumNeuronsPerInstance = rows.Min(row => row.NeuronsPerInstance), MaximumNeuronsPerInstance = rows.Max(row => row.NeuronsPerInstance), Profiles = rows };
    }

    static async Task<object> Identity(string output)
    {
        using var engine = Engine(output, "identity");
        var route = new SynapticConnection(StructureId.VestibularNuclei, Guid.Parse("afbc2222-2222-4222-8222-222222222222"), NTEnum.GLUTAMATE, "audit");
        var candidates = Enumerable.Range(0, 128).Select(index => (SpikeMessage)Rewrite.Invoke(null, [Spike(index), route])!).ToArray();
        int Target(SpikeMessage message) => (int)Project.Invoke(engine, [message])!;
        var first = candidates[0]; var second = candidates.First(message => Target(message) != Target(first));
        var observations = new List<InboundSynapticObservation>(); engine.InboundIntegrationObserver = observations.Add;
        await engine.EnqueueSpikeBatchAsync([first, second]);
        for (var tick = 1; tick <= 40; tick++) await engine.ProcessStepAsync(Tick(tick), 0);
        var cells = observations.Select(item => item.TargetNeuronIndex).Distinct().ToArray();
        return new { DistinctSourceCells = new[] { first.SourceNeuronId, second.SourceNeuronId }, DistinctTargetCells = cells,
            SameRewrittenSynapseId = first.SynapseId == second.SynapseId, LearnedIncomingStates = States(engine).Count,
            SeparateConnectionLearningState = cells.Length == 2 && States(engine).Count == 2,
            WirePreservesSourceInstance = typeof(SpikeMessage).GetProperty("SourceInstanceKey") != null,
            ProjectionFingerprint = candidates.Select(Target).ToArray(),
            Scope = "Compares actual routed IDs, selected cells and receiving records; does not change routing or learning." };
    }

    static async Task<object> PostEvent(string output)
    {
        using var engine = Engine(output, "post-event");
        var message = Spike(16); message.IsRightingCircuitSpike = true;
        var target = (int)Project.Invoke(engine, [message])!;
        await engine.EnqueueSpikeAsync(message);
        for (var tick = 1; tick <= 20; tick++) await engine.ProcessStepAsync(Tick(tick), 0);
        var state = States(engine)[message.SynapseId]; var before = Snapshot(state);
        var cell = ((ModelNeuron[])Neurons.GetValue(engine)!)[target];
        // Explicit diagnostic drive onto this cell, after the presynaptic input.
        // No second inbound-synapse event is allowed to obscure the post-event path.
        var drive = Spike(16); drive.VesicleQuanta = 5;
        for (var input = 0; input < 40; input++) cell.Integrate(drive, 1);
        var fired = 0;
        for (var tick = 21; tick <= 40; tick++)
        {
            await engine.ProcessStepAsync(Tick(tick), 0); if (cell.IsActive) fired++;
        }
        var after = Snapshot(state);
        if (fired == 0) throw new InvalidOperationException("Diagnostic postsynaptic cell did not fire.");
        return new { TargetCell = target, ForcedDiagnosticPostsynapticSpikes = fired, InboundEvents = 1, Before = before, After = after,
            PostsynapticEventUpdatesThisSynapse = after.UpdateCount > before.UpdateCount || after.PostTrace != before.PostTrace,
            Scope = "Characterizes absence/presence of a post-spike update; not a physiological parameter calibration." };
    }
    static SynapseView Snapshot(SynapseState value) => new(value.VesicleQuanta, value.PreTrace, value.PostTrace, value.UpdateCount, value.LastUpdateTimestampMs);
    sealed record SynapseView(float Weight, float PreTrace, float PostTrace, int UpdateCount, double LastUpdateTimestampMs);

    static async Task<object> DelayedModulation(string output)
    {
        using var engine = Engine(output, "delayed-modulation", StructureId.Striatum, "LIF", "DopamineModulatedSTDP");
        var candidates = Enumerable.Range(0, 128).Select(index =>
        {
            var message = Spike(index); message.SourceStructure = StructureId.Snc; message.TargetStructure = StructureId.Striatum;
            message.SynapseId = Guid.Parse("afbc3333-3333-4333-8333-333333333333"); message.Neurotransmitter = NTEnum.DOPAMINE; message.VesicleQuanta = 5;
            return message;
        });
        var dopamine = candidates.First(message => (int)Project.Invoke(engine, [message])! % 2 == 0);
        var target = (int)Project.Invoke(engine, [dopamine])!;
        // Seed a diagnostic eligibility trace as the controlled residue of an
        // earlier pre/post pairing, then deliver only the later modulator.
        var eligible = new SynapseState(Guid.Parse("afbc4444-4444-4444-8444-444444444444"), NTEnum.GLUTAMATE, 1, 1)
        { EligibilityTrace = .8f, SynapticTagTrace = .8f, LastTargetNeuronIndex = target, UpdateCount = 1 };
        States(engine)[eligible.SynapseId] = eligible;
        var before = Snapshot(eligible);
        for (var pulse = 0; pulse < 10; pulse++)
        {
            var message = Spike(0); message.SourceStructure = dopamine.SourceStructure; message.TargetStructure = dopamine.TargetStructure;
            message.SourceNeuronId = dopamine.SourceNeuronId; message.SynapseId = dopamine.SynapseId;
            message.Neurotransmitter = dopamine.Neurotransmitter; message.VesicleQuanta = 5;
            await engine.EnqueueSpikeAsync(message);
        }
        var cell = ((ModelNeuron[])Neurons.GetValue(engine)!)[target];
        var peakDopamine = 0f; var peakTeaching = 0f;
        for (var tick = 1; tick <= 40; tick++)
        {
            await engine.ProcessStepAsync(Tick(tick), 0);
            peakDopamine = Math.Max(peakDopamine, cell.LocalNeuromodState.DopamineLevel); peakTeaching = Math.Max(peakTeaching, cell.LocalRewardSignal);
        }
        if (peakTeaching <= .05f) throw new InvalidOperationException("Diagnostic modulator did not reach a positive local teaching level.");
        var after = Snapshot(eligible);
        return new { DiagnosticEligibilitySeed = .8f, TargetCell = target, PeakLocalDopamine = peakDopamine, PeakLocalTeaching = peakTeaching,
            AdditionalGlutamatergicPresynapticEvents = 0, Before = before, After = after,
            DelayedModulatorUpdatesPreviouslyEligibleConnection = after.Weight != before.Weight || after.UpdateCount != before.UpdateCount,
            Scope = "Controlled eligibility-state characterization, not experimental calibration of a biological dopamine window." };
    }

    static object Outbound(string output)
    {
        using var engine = Engine(output, "outbound");
        var rows = new List<object>();
        foreach (var interval in new[] { 5, 10, 20, 50, 100, 250 })
        {
            var synapse = new SynapseState(Guid.NewGuid(), NTEnum.GLUTAMATE, 1.08f, 1.08f);
            for (var time = interval; time <= 120_000; time += interval)
            {
                var raw = (float)OutboundDelta.Invoke(engine, [synapse, .16f, new NeuromodState(), 0f, (double)time])!;
                var change = PlasticityRules.ApplyCadenceInvariantBudget(synapse, raw, time);
                synapse.VesicleQuanta = PlasticityRules.ClampQuanta(synapse.VesicleQuanta + change);
            }
            rows.Add(new { IntervalMilliseconds = interval, ConstantSenderActivity = .16f, ActualReceiverActivitySupplied = false,
                InitialWeight = 1.08f, FinalWeight = synapse.VesicleQuanta, synapse.TotalAbsolutePlasticityChange });
        }
        return new { DurationBiologicalMilliseconds = 120_000, Cases = rows,
            Observation = "The actual sender rule updates despite receiving no target-cell activity argument. Its post activity proxy is sender activity times 0.35." };
    }

    static object Cadence()
    {
        var rows = new List<object>();
        var voltage = typeof(ModelNeuron).GetField("_v", HiddenInstance)!;
        var ais = typeof(ModelNeuron).GetField("_aisVoltage", HiddenInstance)!;
        foreach (var dt in new[] { 1, 2, 4 })
        {
            var neuron = new ModelNeuron(0, "LIF", StructureCircuitProfile.For(StructureId.VestibularNuclei));
            float rateAt8 = 0, activityAt8 = 0; var spikes = 0; double activitySum = 0, rateSum = 0;
            for (var time = dt; time <= 2000; time += dt)
            {
                var forceSpike = time % 100 == 0;
                voltage.SetValue(neuron, forceSpike ? 40.0 : -90.0); ais.SetValue(neuron, forceSpike ? 35.0 : -90.0);
                var fired = neuron.Step(dt);
                if (time > 1000)
                {
                    if (fired) spikes++;
                    activitySum += neuron.ActivityTrace * dt; rateSum += neuron.FiringRateHz * dt;
                }
                if (time == 1908) { rateAt8 = neuron.FiringRateHz; activityAt8 = neuron.ActivityTrace; }
            }
            if (spikes != 10) throw new InvalidOperationException("Cadence probe failed to impose ten diagnostic spikes.");
            rows.Add(new { StepMilliseconds = dt, ActualSpikesPerSecond = spikes, RateEightMillisecondsAfterSpike = rateAt8, ActivityEightMillisecondsAfterSpike = activityAt8,
                MeanActivityTraceInSecond = activitySum / 1000, MeanFiringRateHzInSecond = rateSum / 1000 });
        }
        return new { DiagnosticVoltageImposition = true, DurationBiologicalMilliseconds = 2000, MeasuredAfterWarmupMilliseconds = 1000, Cases = rows,
            Observation = "Identical imposed spike times; existing fixed per-step smoothing produces different physical-time traces." };
    }

    static object ClockBudget()
    {
        var resumed = new SynapseState(Guid.NewGuid(), NTEnum.GLUTAMATE, 1) { PlasticityBudgetQuanta = 0, LastPlasticityBudgetTimestampMs = 7_000_000 };
        var restarted = PlasticityRules.ApplyCadenceInvariantBudget(resumed, .05f, 1000);
        var continuous = PlasticityRules.ApplyCadenceInvariantBudget(resumed, .05f, 7_001_000);
        return new { SavedBudgetTimestampMilliseconds = 7_000_000, RestartedClockTimestamp = 1000, AppliedWithRestartedClock = restarted,
            AppliedWithContinuingClock = continuous, RequiresClockContinuationOrDefinedRebase = true,
            Scope = "Conditional budget behavior; does not establish that a live resume actually used a reset coordinator clock." };
    }
}
