using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class HostSomaticAuthorityBoundaryTests
{
    [Fact]
    public void ContactContractContainsOnlyPhysicalMeasurementsAndTransportIdentity()
    {
        var properties = typeof(SomaticContactFrameRequest)
            .GetProperties()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("Pattern", properties);
        Assert.DoesNotContain("Intensity", properties);
        Assert.DoesNotContain("BurstCount", properties);
        Assert.DoesNotContain("TargetStructure", properties);
        Assert.DoesNotContain("SourceStructure", properties);
        Assert.DoesNotContain("Hemisphere", properties);
        Assert.DoesNotContain("IsFeedback", properties);
        Assert.Contains("ForceNewtons", properties);
        Assert.Contains("SurfaceNormalX", properties);
        Assert.Contains("TangentialSpeedMetersPerSecond", properties);
    }

    [Fact]
    public void ControlProgramOnlyExposesRawSomaticContactIngress()
    {
        var source = ReadSource("ControlProgram", "Program.cs");

        Assert.Contains("/api/v1/admin/input/contact-frame", source, StringComparison.Ordinal);
        Assert.Contains("SomaticContactTransducerRuntime", source, StringComparison.Ordinal);
        Assert.DoesNotContain("app.MapPost(\"/api/v1/admin/input/collision\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildCollisionStimulusSpikes", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CollisionInputRequest", source, StringComparison.Ordinal);
        Assert.DoesNotContain("collision_", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("NRE.WpfMazeSim")]
    [InlineData("NRE.WpfWorldSim")]
    public void RenderedWorldsCanOnlySendPhysicalContactFrames(string project)
    {
        var source = ReadSource("src", project, "MainWindow.xaml.cs");

        Assert.Contains("PostSomaticContactFrameAsync", source, StringComparison.Ordinal);
        Assert.Contains("SomaticContactFrameRequest", source, StringComparison.Ordinal);
        Assert.DoesNotContain("/api/v1/admin/input/collision", source, StringComparison.Ordinal);
        Assert.DoesNotContain("wall_contact", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DispatchCollisionInputAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("QueueCollisionInput", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SomaticAfferentsAreARealNeuronalService()
    {
        var root = ResolveRepositoryRoot();
        Assert.True(File.Exists(Path.Combine(
            root,
            "Structures",
            "SomaticAfferents",
            "NeuralResonanceEngine.Structures.SomaticAfferents.csproj")));
        Assert.Contains(
            "StructureId.SomaticAfferents",
            ReadSource("Structures", "SomaticAfferents", "Program.cs"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void HeadlessWorldSomaticTransportIsBoundedParallelAndSequenceIdempotent()
    {
        var source = ReadSource("src", "NRE.WorldSim", "HeadlessWorldRuntime.cs")
            .Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains("somaticClient", source, StringComparison.Ordinal);
        Assert.Contains("MaxDegreeOfParallelism = 4", source, StringComparison.Ordinal);
        Assert.Contains("somaticFrameRetries", source, StringComparison.Ordinal);
        Assert.Contains("_replayCache", ReadSource("ControlProgram", "SomaticContactTransducerRuntime.cs"), StringComparison.Ordinal);
        Assert.Contains("PostSomaticContactFrameAsync(\n                            somaticClient", source, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "PostSomaticContactFrameAsync(\n                            sensoryClient",
            source,
            StringComparison.Ordinal);
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
