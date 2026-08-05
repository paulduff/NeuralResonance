using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.CorticalBenchmarks;

internal static class Program
{
    private const string ProtocolVersion = "dnne.cortical-functional-benchmark.v1";

    public static async Task<int> Main(string[] args)
    {
        string outputDirectory = ResolveArgument(args, "--output")
            ?? Path.Combine(ResolveRepositoryRoot(), "artifacts", "cortical-functional-benchmark");
        int epochs = int.TryParse(ResolveArgument(args, "--epochs"), out int parsedEpochs)
            ? Math.Clamp(parsedEpochs, 8, 200)
            : 24;

        Directory.CreateDirectory(outputDirectory);
        CorticalBenchmarkResult result = await RunAsync(epochs);
        string stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
        string jsonPath = Path.Combine(outputDirectory, $"cortical-functional-benchmark-{stamp}.json");
        string markdownPath = Path.Combine(outputDirectory, $"cortical-functional-benchmark-{stamp}.md");

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(result, jsonOptions), new UTF8Encoding(false));
        await File.WriteAllTextAsync(markdownPath, BuildMarkdown(result), new UTF8Encoding(false));

        Console.WriteLine("DNNE cortical functional benchmark complete.");
        Console.WriteLine($"Status: {(result.Passed ? "PASS" : "FAIL")}");
        Console.WriteLine($"Overall score: {result.OverallScore:P1}");
        Console.WriteLine($"Stream separation: {result.StreamSeparationScore:P1}");
        Console.WriteLine($"Learning: {result.LearningScore:P1}");
        Console.WriteLine($"Persistence: {result.PersistenceScore:P1}");
        Console.WriteLine($"Adaptive output gating: {result.AdaptiveOutputScore:P1}");
        Console.WriteLine($"JSON: {jsonPath}");
        Console.WriteLine($"Report: {markdownPath}");
        return result.Passed ? 0 : 1;
    }

    private static async Task<CorticalBenchmarkResult> RunAsync(int epochs)
    {
        IReadOnlyList<SeparationResult> separation = RunSeparationScenarios();
        IReadOnlyList<AdaptiveOutputResult> adaptiveOutput = RunAdaptiveOutputScenarios();
        IReadOnlyList<LearningResult> learning = await RunLearningScenariosAsync(epochs);

        float separationScore = separation.Average(item => item.Score);
        float learningScore = learning.Count(item => item.Learned) / (float)learning.Count;
        float persistenceScore = learning.Count(item => item.Persisted) / (float)learning.Count;
        float adaptiveOutputScore = adaptiveOutput.Count(item => item.Passed) / (float)adaptiveOutput.Count;
        float overall = (separationScore + learningScore + persistenceScore + adaptiveOutputScore) / 4f;
        bool passed = separationScore >= 0.90f
            && learningScore >= 0.80f
            && persistenceScore >= 0.99f
            && adaptiveOutputScore >= 0.99f
            && overall >= 0.90f;

        return new CorticalBenchmarkResult(
            ProtocolVersion,
            DateTimeOffset.UtcNow,
            epochs,
            passed,
            overall,
            separationScore,
            learningScore,
            persistenceScore,
            adaptiveOutputScore,
            separation,
            learning,
            adaptiveOutput);
    }

    private static IReadOnlyList<SeparationResult> RunSeparationScenarios()
    {
        return
        [
            MeasureSeparation(
                "ventral visual streams",
                StructureId.InferotemporalCortex,
                [(StructureId.V4, "visual-173"), (StructureId.Mt, "visual-173"), (StructureId.TemporalAssociation, "visual-173")]),
            MeasureSeparation(
                "auditory object streams",
                StructureId.AuditoryAssociationCortex,
                [(StructureId.A1, "tone-205"), (StructureId.TemporalAssociation, "tone-205"), (StructureId.WernickePstgPsts, "tone-205")]),
            MeasureSeparation(
                "somatic body map",
                StructureId.SecondarySomatosensoryCortex,
                [(StructureId.S1, "touch-face-12"), (StructureId.S1, "touch-hand-12"), (StructureId.S1, "touch-trunk-12"), (StructureId.S1, "touch-foot-12")]),
            MeasureSeparation(
                "self-context evidence",
                StructureId.TemporoparietalJunction,
                [(StructureId.SecondarySomatosensoryCortex, "context-91"), (StructureId.PosteriorCingulate, "context-91"), (StructureId.Amygdala, "context-91"), (StructureId.TemporalAssociation, "context-91")]),
            MeasureSeparation(
                "executive control streams",
                StructureId.DorsomedialPrefrontalCortex,
                [(StructureId.Pfc, "control-247"), (StructureId.Acc, "control-247"), (StructureId.OrbitofrontalCortex, "control-247"), (StructureId.Striatum, "control-247"), (StructureId.Ppc, "control-247")])
        ];
    }

    private static SeparationResult MeasureSeparation(
        string scenario,
        StructureId target,
        IReadOnlyList<(StructureId Source, string NeuronId)> patterns)
    {
        StructureCircuitProfile circuit = StructureCircuitProfile.For(target);
        ICircuitKernel kernel = CircuitKernelFactory.For(target);
        var indices = new List<int>(patterns.Count);
        for (int index = 0; index < patterns.Count; index++)
        {
            (StructureId source, string neuronId) = patterns[index];
            var spike = CreateSpike(
                source,
                target,
                neuronId,
                DeterministicGuid($"separation:{scenario}:{index}"),
                0,
                1f,
                index);
            indices.Add(kernel.ResolveInboundNeuronIndex(spike, circuit.NeuronCount, circuit));
        }

        int distinct = indices.Distinct().Count();
        return new SeparationResult(
            scenario,
            target,
            patterns.Count,
            distinct,
            distinct / (float)patterns.Count,
            indices);
    }

    private static IReadOnlyList<AdaptiveOutputResult> RunAdaptiveOutputScenarios()
    {
        ICircuitKernel executive = CircuitKernelFactory.For(StructureId.FrontalEyeFields);
        return
        [
            MeasureOutput(
                "FEF attention burst",
                SpikeTypeEnum.BURST,
                executive.SelectSpikeType(StructureId.FrontalEyeFields, false, MakeTick(acetylcholine: 0.64f, norepinephrine: 0.46f))),
            MeasureOutput(
                "midcingulate prediction-error burst",
                SpikeTypeEnum.BURST,
                executive.SelectSpikeType(StructureId.MidcingulateCortex, false, MakeTick(rewardPredictionError: -0.34f))),
            MeasureOutput(
                "vmPFC value-gated burst",
                SpikeTypeEnum.BURST,
                executive.SelectSpikeType(StructureId.VentromedialPrefrontalCortex, false, MakeTick(dopamine: 0.61f))),
            MeasureOutput(
                "quiet FEF tonic output",
                SpikeTypeEnum.ACTION_POTENTIAL,
                executive.SelectSpikeType(StructureId.FrontalEyeFields, false, MakeTick()))
        ];
    }

    private static AdaptiveOutputResult MeasureOutput(string scenario, SpikeTypeEnum expected, SpikeTypeEnum observed)
        => new(scenario, expected, observed, expected == observed);

    private static async Task<IReadOnlyList<LearningResult>> RunLearningScenariosAsync(int epochs)
    {
        var specifications = new[]
        {
            new LearningSpecification(StructureId.InferotemporalCortex, StructureId.V4, "Izhikevich", "STDP+SynapticTaggingCapture", new DelayWindow(5, 12)),
            new LearningSpecification(StructureId.AuditoryAssociationCortex, StructureId.A1, "Izhikevich", "STDP+SynapticTaggingCapture", new DelayWindow(4, 10)),
            new LearningSpecification(StructureId.SecondarySomatosensoryCortex, StructureId.S1, "LIF", "STDP", new DelayWindow(4, 10)),
            new LearningSpecification(StructureId.TemporoparietalJunction, StructureId.PosteriorCingulate, "Izhikevich", "STDP+SynapticTaggingCapture", new DelayWindow(5, 14)),
            new LearningSpecification(StructureId.FrontalEyeFields, StructureId.Ppc, "Izhikevich", "STDP", new DelayWindow(3, 10))
        };

        var results = new List<LearningResult>(specifications.Length);
        string stateDirectory = Path.Combine(Path.GetTempPath(), "nre-cortical-benchmark", Guid.NewGuid().ToString("N"));
        string? previousDirectory = Environment.GetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR");
        string? previousInstance = Environment.GetEnvironmentVariable("SERVICE_INSTANCE");

        try
        {
            Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR", stateDirectory);
            foreach (LearningSpecification specification in specifications)
            {
                string instance = $"benchmark-{specification.Target}";
                Environment.SetEnvironmentVariable("SERVICE_INSTANCE", instance);
                Guid synapseId = DeterministicGuid($"learning:{specification.Target}");
                float firstStrength;
                float finalStrength;
                float peakFiringRate = 0f;
                int outboundSpikes = 0;

                using (var engine = CreateEngine(specification))
                {
                    firstStrength = 0f;
                    for (int epoch = 1; epoch <= epochs; epoch++)
                    {
                        double timestamp = epoch * 100;
                        var batch = new SpikeMessage[8];
                        for (int pulse = 0; pulse < batch.Length; pulse++)
                        {
                            batch[pulse] = CreateSpike(
                                specification.Source,
                                specification.Target,
                                $"benchmark-{specification.Target}-173",
                                synapseId,
                                timestamp + pulse * 0.2,
                                1.4f,
                                epoch * 100 + pulse);
                        }

                        await engine.EnqueueSpikeBatchAsync(batch);
                        StructureStepResult step = await engine.ProcessStepAsync(MakeTick(
                            epoch,
                            timestamp + 50,
                            dopamine: 0.58f,
                            acetylcholine: 0.54f,
                            norepinephrine: 0.34f,
                            rewardPredictionError: 0.36f), 5);
                        if (epoch == 1)
                        {
                            firstStrength = engine.GetInboundSynapseStrength(synapseId);
                        }
                        peakFiringRate = Math.Max(peakFiringRate, step.TopActiveNeurons.Count == 0 ? 0f : step.TopActiveNeurons.Max(item => item.FiringRateHz));
                        outboundSpikes += step.OutboundSpikes.Count;
                    }
                    finalStrength = engine.GetInboundSynapseStrength(synapseId);
                }

                float reloadedStrength;
                using (var reloaded = CreateEngine(specification))
                {
                    reloadedStrength = reloaded.GetInboundSynapseStrength(synapseId);
                }

                bool learned = MathF.Abs(finalStrength - firstStrength) > 0.0001f;
                bool persisted = MathF.Abs(reloadedStrength - finalStrength) < 0.0001f;
                results.Add(new LearningResult(
                    specification.Target,
                    specification.Source,
                    firstStrength,
                    finalStrength,
                    reloadedStrength,
                    StructureEngine.CombineInboundSynapticStrength(1.4f, finalStrength),
                    finalStrength - firstStrength,
                    peakFiringRate,
                    outboundSpikes,
                    learned,
                    persisted));
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR", previousDirectory);
            Environment.SetEnvironmentVariable("SERVICE_INSTANCE", previousInstance);
            if (Directory.Exists(stateDirectory))
            {
                Directory.Delete(stateDirectory, recursive: true);
            }
        }

        return results;
    }

    private static StructureEngine CreateEngine(LearningSpecification specification)
        => new(new StructureProfile(
            specification.Target,
            specification.NeuronModel,
            specification.PlasticityRule,
            "isolated cortical functional benchmark",
            specification.FeedbackDelay));

    private static SpikeMessage CreateSpike(
        StructureId source,
        StructureId target,
        string sourceNeuronId,
        Guid synapseId,
        double timestamp,
        float vesicleQuanta,
        int sequence)
        => new()
        {
            MessageId = DeterministicGuid($"message:{source}:{target}:{sequence}"),
            TimestampMs = timestamp,
            SourceStructure = source,
            TargetStructure = target,
            SourceNeuronId = sourceNeuronId,
            TargetNeuronId = $"benchmark-{target}-target",
            SynapseId = synapseId,
            Neurotransmitter = NTEnum.GLUTAMATE,
            VesicleQuanta = vesicleQuanta,
            ReuptakeRate = 1f,
            SpikeType = SpikeTypeEnum.ACTION_POTENTIAL
        };

    private static TickSignal MakeTick(
        long tick = 1,
        double timestamp = 10,
        float dopamine = 0f,
        float acetylcholine = 0f,
        float norepinephrine = 0f,
        float rewardPredictionError = 0f)
        => new(
            tick,
            timestamp,
            10,
            new NeuromodState
            {
                DopamineLevel = dopamine,
                SerotoninLevel = 0.30f,
                AcetylcholineLevel = acetylcholine,
                NorepinephrineLevel = norepinephrine
            },
            new Dictionary<BrainRhythm, double>(),
            rewardPredictionError);

    private static Guid DeterministicGuid(string value)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(hash.AsSpan(0, 16));
    }

    private static string BuildMarkdown(CorticalBenchmarkResult result)
    {
        var lines = new List<string>
        {
            "# DNNE Cortical Functional Benchmark",
            string.Empty,
            $"- Protocol: `{result.ProtocolVersion}`",
            $"- Status: **{(result.Passed ? "PASS" : "FAIL")}**",
            $"- Epochs per learning circuit: `{result.Epochs}`",
            $"- Overall score: `{result.OverallScore:P1}`",
            $"- Stream separation: `{result.StreamSeparationScore:P1}`",
            $"- Learning: `{result.LearningScore:P1}`",
            $"- Persistence: `{result.PersistenceScore:P1}`",
            $"- Adaptive output gating: `{result.AdaptiveOutputScore:P1}`",
            string.Empty,
            "## Stream Separation",
            string.Empty,
            "| Scenario | Target | Patterns | Distinct populations | Score |",
            "| --- | --- | ---: | ---: | ---: |"
        };
        lines.AddRange(result.Separation.Select(item => $"| {item.Scenario} | {item.Target} | {item.PatternCount} | {item.DistinctPopulationCount} | {item.Score:P1} |"));
        lines.AddRange(
        [
            string.Empty,
            "## Learning And Persistence",
            string.Empty,
            "| Target | Source | First strength | Final strength | Reloaded | Effective input | Peak Hz | Outbound | Learned | Persisted |",
            "| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | --- | --- |"
        ]);
        lines.AddRange(result.Learning.Select(item =>
            $"| {item.Target} | {item.Source} | {item.FirstStrength:F4} | {item.FinalStrength:F4} | {item.ReloadedStrength:F4} | {item.EffectiveInputStrength:F4} | {item.PeakFiringRateHz:F2} | {item.OutboundSpikeCount} | {item.Learned} | {item.Persisted} |"));
        lines.AddRange(
        [
            string.Empty,
            "## Adaptive Output",
            string.Empty,
            "| Scenario | Expected | Observed | Passed |",
            "| --- | --- | --- | --- |"
        ]);
        lines.AddRange(result.AdaptiveOutput.Select(item => $"| {item.Scenario} | {item.Expected} | {item.Observed} | {item.Passed} |"));
        lines.AddRange(
        [
            string.Empty,
            "This is a deterministic isolated-circuit benchmark. It establishes functional differentiation, synaptic learning application, persistence, and adaptive output gating. It does not by itself demonstrate human-level recognition, perspective taking, or executive reasoning."
        ]);
        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private static string? ResolveArgument(string[] args, string name)
    {
        for (int index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }
        return null;
    }

    private static string ResolveRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "NeuralResonanceEngine.DNNE.slnx")))
            {
                return directory.FullName;
            }
        }
        return Directory.GetCurrentDirectory();
    }
}

internal sealed record CorticalBenchmarkResult(
    string ProtocolVersion,
    DateTimeOffset GeneratedAtUtc,
    int Epochs,
    bool Passed,
    float OverallScore,
    float StreamSeparationScore,
    float LearningScore,
    float PersistenceScore,
    float AdaptiveOutputScore,
    IReadOnlyList<SeparationResult> Separation,
    IReadOnlyList<LearningResult> Learning,
    IReadOnlyList<AdaptiveOutputResult> AdaptiveOutput);

internal sealed record SeparationResult(
    string Scenario,
    StructureId Target,
    int PatternCount,
    int DistinctPopulationCount,
    float Score,
    IReadOnlyList<int> PopulationIndices);

internal sealed record LearningResult(
    StructureId Target,
    StructureId Source,
    float FirstStrength,
    float FinalStrength,
    float ReloadedStrength,
    float EffectiveInputStrength,
    float StrengthDelta,
    float PeakFiringRateHz,
    int OutboundSpikeCount,
    bool Learned,
    bool Persisted);

internal sealed record AdaptiveOutputResult(
    string Scenario,
    SpikeTypeEnum Expected,
    SpikeTypeEnum Observed,
    bool Passed);

internal sealed record LearningSpecification(
    StructureId Target,
    StructureId Source,
    string NeuronModel,
    string PlasticityRule,
    DelayWindow FeedbackDelay);
