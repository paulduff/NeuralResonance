using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class HostVisualAuthorityBoundaryTests
{
    [Fact]
    public void StructuredVisualTransportTypesDoNotExist()
    {
        var contracts = typeof(InputGateControlRequest).Assembly;
        var avatar = typeof(AvatarSightFrame).Assembly;

        Assert.Null(contracts.GetType(
            "NeuralResonanceEngine.Shared.Contracts.VisualInputRequest",
            throwOnError: false));
        Assert.Null(contracts.GetType(
            "NeuralResonanceEngine.Shared.Contracts.VisualInputDispatchClient",
            throwOnError: false));
        Assert.Null(avatar.GetType("NRE.SimAvatar.AvatarVisualSignal", throwOnError: false));
        Assert.Null(avatar.GetType("NRE.SimAvatar.AvatarVisualSignalFactory", throwOnError: false));
    }

    [Fact]
    public void ControlProgramOnlyExposesRawRetinalVisionIngress()
    {
        var source = ReadSource("ControlProgram", "Program.cs");

        Assert.Contains("/api/v1/admin/input/visual-frame", source, StringComparison.Ordinal);
        Assert.Contains("RetinalFrameTransducerRuntime", source, StringComparison.Ordinal);
        Assert.DoesNotContain("app.MapPost(\"/api/v1/admin/input/visual\",", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildVisualStimulusSpikes", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ResolveVisualAttentionTargetHemisphere", source, StringComparison.Ordinal);
        Assert.DoesNotContain("LeftFieldSaliency", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RightFieldSaliency", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("NRE.WpfMazeSim")]
    [InlineData("NRE.WpfWorldSim")]
    public void RenderedWorldsCanOnlySendRawRetinalFrames(string project)
    {
        var source = ReadSource("src", project, "MainWindow.xaml.cs");

        Assert.Contains("PostRetinalFrameAsync", source, StringComparison.Ordinal);
        AssertSourceOmits(
            source,
            "VisualInputRequest",
            "VisualInputDispatchClient",
            "AvatarVisualSignal",
            "LeftFieldSaliency",
            "RightFieldSaliency",
            "BuildWorldBasedAvatarVisionSignal",
            "ComputeWorldVisionSaliency",
            "/api/engine/visual-frame",
            "localhost:5005");
    }

    [Fact]
    public void EditorWebcamCanOnlySendRawRetinalFrames()
    {
        var source = ReadSource("src", "NRE.WpfEditor", "MainWindow.Webcam.cs");

        Assert.Contains("PostRetinalFrameAsync", source, StringComparison.Ordinal);
        AssertSourceOmits(
            source,
            "VisualInputRequest",
            "VisualInputDispatchClient",
            "AvatarVisualSignal",
            "ComputeHemifieldSaliency",
            "LeftFieldSaliency",
            "RightFieldSaliency");
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
