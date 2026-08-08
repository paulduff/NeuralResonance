namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class NeuronalOnlyQualificationHarnessTests
{
    [Fact]
    public void Production_Runtime_Has_No_Machine_Learning_Dependency_Or_Policy_Engine()
    {
        var root = ResolveRepositoryRoot();
        var productionRoots = new[]
        {
            Path.Combine(root, "ControlProgram"),
            Path.Combine(root, "Protocol"),
            Path.Combine(root, "Shared.Contracts"),
            Path.Combine(root, "src"),
            Path.Combine(root, "Structures")
        };
        var productionSource = string.Join(
            '\n',
            productionRoots
                .Where(Directory.Exists)
                .SelectMany(static directory => Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories))
                .Where(static path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                                      path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
                .Where(static path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                                      !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .Select(File.ReadAllText));

        string[] forbiddenRuntimeSignatures =
        [
            "Microsoft.ML",
            "MLContext",
            "PredictionEngine",
            "TorchSharp",
            "TensorFlow",
            "Microsoft.ML.OnnxRuntime",
            "Accord.MachineLearning",
            "PolicyNetwork",
            "ReinforcementLearning",
            "QLearning",
            "QTable"
        ];

        Assert.All(
            forbiddenRuntimeSignatures,
            signature => Assert.DoesNotContain(signature, productionSource, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Harness_Does_Not_Claim_Embodied_Qualification_From_Offline_Evidence()
    {
        var source = ReadHarness();

        Assert.Contains("PREFLIGHT_PASS_LIVE_REQUIRED", source, StringComparison.Ordinal);
        Assert.Contains("$embodiedQualified = $preflightPassed -and $liveRequested -and $liveGatePassed", source, StringComparison.Ordinal);
        Assert.Contains("burnin-worldsim.ps1", source, StringComparison.Ordinal);
        Assert.Contains("worldMotorDispatchTotal", source, StringComparison.Ordinal);
        Assert.Contains("worldLocomotorDispatchTotal", source, StringComparison.Ordinal);
        Assert.Contains("worldManipulatorDispatchTotal", source, StringComparison.Ordinal);
        Assert.Contains("worldDistanceTravelledDelta", source, StringComparison.Ordinal);
        Assert.Contains("worldVisitedTerrainDelta", source, StringComparison.Ordinal);
        Assert.Contains("worldInteractionAttemptDelta", source, StringComparison.Ordinal);
        Assert.Contains("worldRetinalAcceptedDelta", source, StringComparison.Ordinal);
        Assert.Contains("worldCochlearAcceptedDelta", source, StringComparison.Ordinal);
        Assert.Contains("worldPhysicalBodyAcceptedDelta", source, StringComparison.Ordinal);
        Assert.Contains("worldSomaticAcceptedDelta", source, StringComparison.Ordinal);
        Assert.Contains("worldTickFailureDelta", source, StringComparison.Ordinal);
        Assert.Contains("worldDetected", source, StringComparison.Ordinal);
        Assert.DoesNotContain("mazeDetected", source, StringComparison.Ordinal);
        Assert.DoesNotContain("burnin-dnne.ps1", source, StringComparison.Ordinal);
    }

    [Fact]
    public void WorldSim_BurnIn_Observes_Physical_Evidence_Without_Issuing_Actions()
    {
        var source = ReadScript("burnin-worldsim.ps1");

        Assert.Contains("start-world-sim.ps1", source, StringComparison.Ordinal);
        Assert.Contains("dnne.worldsim.state.v1", source, StringComparison.Ordinal);
        Assert.Contains("processId", source, StringComparison.Ordinal);
        Assert.Contains("sessionId", source, StringComparison.Ordinal);
        Assert.Contains("seed", source, StringComparison.Ordinal);
        Assert.Contains("neuronalMotorDispatchTotal", source, StringComparison.Ordinal);
        Assert.Contains("neuronalLocomotorDispatchTotal", source, StringComparison.Ordinal);
        Assert.Contains("neuronalManipulatorDispatchTotal", source, StringComparison.Ordinal);
        Assert.Contains("interactionAttempts", source, StringComparison.Ordinal);
        Assert.Contains("retinalFramesAccepted", source, StringComparison.Ordinal);
        Assert.Contains("cochlearFramesAccepted", source, StringComparison.Ordinal);
        Assert.Contains("physicalBodyFramesAccepted", source, StringComparison.Ordinal);
        Assert.Contains("somaticFramesAccepted", source, StringComparison.Ordinal);
        Assert.Contains("tickFailures", source, StringComparison.Ordinal);
        Assert.Contains("NRE.WpfWorldSim.exe", source, StringComparison.Ordinal);
        Assert.DoesNotContain("[Math]::Clamp", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MotorDirective", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GoalCoordinate", source, StringComparison.Ordinal);
        Assert.DoesNotContain("start-maze-sim.ps1", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Harness_Contains_No_Symbolic_Behaviour_Authority()
    {
        var source = ReadHarness();

        Assert.DoesNotContain("MotorDirective", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GoalCoordinate", source, StringComparison.Ordinal);
        Assert.DoesNotContain("current-dnne-intent", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("control-state-intent", source, StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadHarness()
    {
        return ReadScript("run-neuronal-only-qualification.ps1");
    }

    private static string ReadScript(string fileName)
    {
        var root = ResolveRepositoryRoot();
        return File.ReadAllText(Path.Combine(root, "tools", fileName));
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

        throw new DirectoryNotFoundException("Could not resolve the DNNE repository root.");
    }
}
