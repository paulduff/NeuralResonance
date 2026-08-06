using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class HostAuditoryAuthorityBoundaryTests
{
    [Fact]
    public void SemanticAuditoryTransportTypesDoNotExist()
    {
        var avatar = typeof(AvatarAudioFrame).Assembly;

        Assert.Null(avatar.GetType("NRE.SimAvatar.AvatarAuditoryCue", throwOnError: false));
        Assert.Null(avatar.GetType("NRE.SimAvatar.AvatarAuditoryDispatchResult", throwOnError: false));
    }

    [Fact]
    public void ControlProgramOnlyExposesRawCochlearIngress()
    {
        var source = ReadSource("ControlProgram", "Program.cs");

        Assert.Contains("/api/v1/admin/input/audio-frame", source, StringComparison.Ordinal);
        Assert.Contains("CochlearFrameTransducerRuntime", source, StringComparison.Ordinal);
        Assert.DoesNotContain("app.MapPost(\"/api/v1/admin/input/auditory\",", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildAuditoryStimulusSpikes", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AuditoryInputRequest", source, StringComparison.Ordinal);
        Assert.DoesNotContain("a1_tonotopic_", source, StringComparison.Ordinal);
    }

    [Fact]
    public void EditorMicrophoneCanOnlySendCapturedPcm()
    {
        var source = ReadSource("src", "NRE.WpfEditor", "MainWindow.Microphone.cs");

        Assert.Contains("PostCochlearFrameAsync", source, StringComparison.Ordinal);
        Assert.Contains("CaptureMicrophoneFrame", source, StringComparison.Ordinal);
        AssertSourceOmits(
            source,
            "SendLanguageStimulusAsync",
            "BuildMicrophoneUtterance",
            "ResolveMicrophonePattern",
            "TargetStructure",
            "BurstCount");
    }

    [Theory]
    [InlineData("NRE.WpfMazeSim")]
    [InlineData("NRE.WpfWorldSim")]
    public void RenderedWorldsCanOnlySendPhysicalAcousticFrames(string project)
    {
        var source = ReadSource("src", project, "MainWindow.xaml.cs");

        Assert.Contains("AvatarAcousticRenderer.RenderFrame", source, StringComparison.Ordinal);
        Assert.Contains("PostCochlearFrameAsync", source, StringComparison.Ordinal);
        AssertSourceOmits(
            source,
            "AvatarAuditoryCue",
            "PostAuditoryCueAsync",
            "BuildEnvironmentAuditoryCues",
            "BearGrowl",
            "GoalShelterHum",
            "CheckpointSafeChime");
    }

    [Fact]
    public void BurnInUsesRawSensoryFrames()
    {
        var source = ReadSource("tools", "burnin-dnne.ps1");

        Assert.Contains("/api/v1/admin/input/audio-frame", source, StringComparison.Ordinal);
        Assert.Contains("/api/v1/admin/input/visual-frame", source, StringComparison.Ordinal);
        Assert.DoesNotContain("/api/v1/admin/input/auditory", source, StringComparison.Ordinal);
        Assert.DoesNotContain("/api/v1/admin/input/visual\"", source, StringComparison.Ordinal);
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
