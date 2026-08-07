using System.Reflection;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class HostValuationIngressBoundaryTests
{
    [Fact]
    public void PublicContractsExposePhysicalBodyFactsOnly()
    {
        var properties = typeof(PhysicalBodyFrameRequest)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(static property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains(nameof(PhysicalBodyFrameRequest.StoredEnergyJoules), properties);
        Assert.Contains(nameof(PhysicalBodyFrameRequest.TissueIntegrityFraction), properties);
        Assert.Contains(nameof(PhysicalBodyFrameRequest.CoreTemperatureCelsius), properties);
        Assert.Contains(nameof(PhysicalBodyFrameRequest.BloodOxygenSaturationFraction), properties);
        Assert.Contains(nameof(PhysicalBodyFrameRequest.HydrationFraction), properties);
        Assert.DoesNotContain("Hunger", properties);
        Assert.DoesNotContain("Health", properties);
        Assert.DoesNotContain("PainLevel", properties);
        Assert.DoesNotContain("TargetStructure", properties);
        Assert.DoesNotContain("SourceStructure", properties);
        Assert.DoesNotContain("Hemisphere", properties);
        Assert.DoesNotContain("Intensity", properties);
        Assert.DoesNotContain("BurstCount", properties);
        Assert.DoesNotContain("Pattern", properties);
        Assert.DoesNotContain("EnvironmentalDarkness", properties);
        Assert.DoesNotContain("ShelterNeed", properties);
        Assert.DoesNotContain("Anxiety", properties);
        Assert.DoesNotContain("PredatorThreat", properties);
        Assert.DoesNotContain("InShelter", properties);
        Assert.DoesNotContain("ShelterSafety", properties);
        Assert.DoesNotContain("Urgency", properties);
    }

    [Fact]
    public void SemanticOutcomeTransportTypesNoLongerExist()
    {
        Assert.Null(typeof(PhysicalBodyFrameRequest).Assembly.GetType(
            "NeuralResonanceEngine.Shared.Contracts.OutcomeInputRequest",
            throwOnError: false));
        Assert.Null(typeof(AvatarService).Assembly.GetType(
            "NRE.SimAvatar.AvatarOutcomeTelemetry",
            throwOnError: false));
        Assert.Null(typeof(AvatarService).Assembly.GetType(
            "NRE.SimAvatar.AvatarOutcomeInputFactory",
            throwOnError: false));
        Assert.Null(typeof(PhysicalBodyFrameRequest).Assembly.GetType(
            "NeuralResonanceEngine.Shared.Contracts.BodyStateInputRequest",
            throwOnError: false));
        Assert.Null(typeof(AvatarService).Assembly.GetType(
            "NRE.SimAvatar.AvatarBodyStateProfile",
            throwOnError: false));
    }

    [Fact]
    public void ControlProgramHasNoHostValuationEndpointOrScalarState()
    {
        var source = ReadSource("ControlProgram", "Program.cs");

        AssertSourceOmits(
            source,
            "/api/v1/admin/input/outcome",
            "/api/v1/admin/input/body-state",
            "BuildBodyStateStimulusSpikes",
            "ResolveBodyStateInteroceptiveTargets",
            "BodyStateRuntime",
            "UpdateOutcomeState",
            "OutcomeStateRuntime",
            "UpdateEnvironmentalState",
            "EnvironmentalStateRuntime");
        Assert.Contains("/api/v1/admin/input/body-frame", source, StringComparison.Ordinal);
        Assert.Contains("StructureId.ProprioceptiveAfferents", source, StringComparison.Ordinal);
        Assert.Contains("StructureId.VestibularAfferents", source, StringComparison.Ordinal);
        Assert.Contains("StructureId.VisceralAfferents", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("NRE.WpfMazeSim")]
    [InlineData("NRE.WpfWorldSim")]
    public void RenderedWorldsCannotPublishSemanticOutcomes(string project)
    {
        var source = ReadSource("src", project, "MainWindow.xaml.cs");

        AssertSourceOmits(
            source,
            "AvatarOutcomeTelemetry",
            "QueueOutcomeInput",
            "PostOutcomeAsync",
            "TryDequeueOutcome",
            "AvatarBodyStateProfile",
            "PostBodyStateAsync",
            "BodyStateInputRequest");
    }

    private static void AssertSourceOmits(string source, params string[] forbiddenSymbols)
    {
        foreach (var symbol in forbiddenSymbols)
        {
            Assert.DoesNotContain(symbol, source, StringComparison.Ordinal);
        }
    }

    private static string ReadSource(params string[] pathParts)
        => File.ReadAllText(Path.Combine([ResolveRepositoryRoot(), .. pathParts]));

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
