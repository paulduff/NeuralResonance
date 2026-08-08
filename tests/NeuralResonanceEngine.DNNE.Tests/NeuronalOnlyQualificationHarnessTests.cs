namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class NeuronalOnlyQualificationHarnessTests
{
    [Fact]
    public void Harness_Does_Not_Claim_Embodied_Qualification_From_Offline_Evidence()
    {
        var source = ReadHarness();

        Assert.Contains("PREFLIGHT_PASS_LIVE_REQUIRED", source, StringComparison.Ordinal);
        Assert.Contains("$embodiedQualified = $preflightPassed -and $liveRequested -and $liveGatePassed", source, StringComparison.Ordinal);
        Assert.Contains("mazeMotorDispatchTotal", source, StringComparison.Ordinal);
        Assert.Contains("mazeProgressTotal", source, StringComparison.Ordinal);
        Assert.Contains("mazeDetected", source, StringComparison.Ordinal);
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
        var root = ResolveRepositoryRoot();
        return File.ReadAllText(Path.Combine(root, "tools", "run-neuronal-only-qualification.ps1"));
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
