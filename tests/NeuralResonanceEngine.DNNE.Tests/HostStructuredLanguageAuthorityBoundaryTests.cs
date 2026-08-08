using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class HostStructuredLanguageAuthorityBoundaryTests
{
    [Fact]
    public void StructuredLanguageTransportTypesDoNotExist()
    {
        var avatar = typeof(AvatarSightFrame).Assembly;

        Assert.Null(avatar.GetType("NRE.SimAvatar.AvatarLanguageCommand", throwOnError: false));
        Assert.Null(avatar.GetType("NRE.SimAvatar.AvatarLanguageCommandResult", throwOnError: false));
    }

    [Fact]
    public void ControlProgramDoesNotExposeStructuredLanguageIngress()
    {
        var source = ReadSource("ControlProgram", "Program.cs");

        Assert.Contains("/api/v1/admin/input/visual-frame", source, StringComparison.Ordinal);
        Assert.DoesNotContain("app.MapPost(\"/api/v1/admin/input/language\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("LanguageInputRequest", source, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildLanguageStimulusSpikes(", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("NRE.WpfMazeSim")]
    [InlineData("NRE.WpfWorldSim")]
    [InlineData("NRE.WpfEditor")]
    public void DesktopClientsPresentTypedTextOnlyAsRetinalPixels(string project)
    {
        var source = ReadSource("src", project, "MainWindow.xaml.cs");

        Assert.Contains("AvatarTextSightRenderer.Render", source, StringComparison.Ordinal);
        Assert.Contains("PostRetinalFrameAsync", source, StringComparison.Ordinal);
        AssertSourceOmits(
            source,
            "PostLanguageCommandAsync",
            "AvatarLanguageCommand",
            "SendLanguageStimulusAsync",
            "LanguageModeCombo",
            "LanguageHemisphereCombo",
            "result.MotorDirective");
    }

    [Fact]
    public void TextRendererEmitsPixelsWithoutNeuralRoutingMetadata()
    {
        var source = ReadSource("src", "NRE.SimAvatar", "AvatarTextSightRenderer.cs");

        Assert.Contains("AvatarSightFrame", source, StringComparison.Ordinal);
        AssertSourceOmits(
            source,
            "TargetStructure",
            "TargetNeuron",
            "Hemisphere",
            "MotorDirective",
            "SpikeMessage");
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
