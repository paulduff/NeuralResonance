using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class HostSemanticExteroceptionBoundaryTests
{
    [Fact]
    public void SemanticObjectTransportTypesNoLongerExist()
    {
        Assert.Null(typeof(VisualInputRequest).Assembly.GetType(
            "NeuralResonanceEngine.Shared.Contracts.ObjectInputRequest",
            throwOnError: false));
        Assert.Null(typeof(AvatarSightFrame).Assembly.GetType(
            "NRE.SimAvatar.AvatarObjectObservation",
            throwOnError: false));
    }

    [Fact]
    public void ControlProgramHasNoHostObjectAnnotationEndpoint()
    {
        var source = ReadSource("ControlProgram", "Program.cs");

        AssertSourceOmits(
            source,
            "/api/v1/admin/input/object",
            "NormalizeObjectKey",
            "ObjectInputRequest");
    }

    [Theory]
    [InlineData("NRE.WpfMazeSim")]
    [InlineData("NRE.WpfWorldSim")]
    public void RenderedWorldsCannotPublishPreclassifiedObjects(string project)
    {
        var source = ReadSource("src", project, "MainWindow.xaml.cs");

        AssertSourceOmits(
            source,
            "AvatarObjectObservation",
            "PostObjectCandidates",
            "TryDequeueObjectObservation",
            "DispatchRecognizedObjectsAsync",
            "DispatchSurvivalCueAsync",
            "BuildRecognizedObjectCues");
    }

    [Fact]
    public void NeuronalPerceptionDiagnosticsAndRenderedVisionRemainAvailable()
    {
        Assert.NotNull(typeof(VisualObjectRecognitionDiagnostics));

        var mazeSource = ReadSource("src", "NRE.WpfMazeSim", "MainWindow.xaml.cs");
        var worldSource = ReadSource("src", "NRE.WpfWorldSim", "MainWindow.xaml.cs");

        Assert.Contains("RenderAvatarVisionFrame", mazeSource, StringComparison.Ordinal);
        Assert.Contains("SendAvatarVisualStimulusAsync", mazeSource, StringComparison.Ordinal);
        Assert.Contains("PostSightInputFrame", worldSource, StringComparison.Ordinal);
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
