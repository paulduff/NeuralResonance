using System.Reflection;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class HostValuationIngressBoundaryTests
{
    [Fact]
    public void PublicContractsExposePhysicalBodyFactsOnly()
    {
        var properties = typeof(BodyStateInputRequest)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(static property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains(nameof(BodyStateInputRequest.Hunger), properties);
        Assert.Contains(nameof(BodyStateInputRequest.Health), properties);
        Assert.Contains(nameof(BodyStateInputRequest.PainLevel), properties);
        Assert.Contains(nameof(BodyStateInputRequest.TactileFront), properties);
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
        Assert.Null(typeof(BodyStateInputRequest).Assembly.GetType(
            "NeuralResonanceEngine.Shared.Contracts.OutcomeInputRequest",
            throwOnError: false));
        Assert.Null(typeof(AvatarBodyTelemetry).Assembly.GetType(
            "NRE.SimAvatar.AvatarOutcomeTelemetry",
            throwOnError: false));
        Assert.Null(typeof(AvatarBodyTelemetry).Assembly.GetType(
            "NRE.SimAvatar.AvatarOutcomeInputFactory",
            throwOnError: false));
    }

    [Fact]
    public void ControlProgramHasNoHostValuationEndpointOrScalarState()
    {
        var source = ReadSource("ControlProgram", "Program.cs");

        AssertSourceOmits(
            source,
            "/api/v1/admin/input/outcome",
            "UpdateOutcomeState",
            "OutcomeStateRuntime",
            "UpdateEnvironmentalState",
            "EnvironmentalStateRuntime");
        Assert.Contains("InteroceptiveState", source, StringComparison.Ordinal);
        Assert.Contains("ComputeStableStimulusHash", source, StringComparison.Ordinal);
        Assert.Contains("StructureId.NucleusTractusSolitarius", source, StringComparison.Ordinal);
        Assert.Contains("StructureId.Hypothalamus", source, StringComparison.Ordinal);
        Assert.Contains("StructureId.Insula", source, StringComparison.Ordinal);
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
            "TryDequeueOutcome");
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
