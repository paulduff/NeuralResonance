using NRE.SimAvatar;
using NeuralResonanceEngine.Shared.Contracts;

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

    [Fact]
    public void ControlProgramHasNoHostLanguageConditioningSubsystem()
    {
        var source = ReadSource("ControlProgram", "Program.cs");
        var telemetryRoutes = ReadSource("ControlProgram", "Routes", "AdminTelemetryRoutes.cs");
        var settings = ReadSource("ControlProgram", "appsettings.json");

        AssertSourceOmits(
            source,
            "InjectPerceptionLanguageConditioningAsync",
            "BuildPerceptionLanguageTokens",
            "BuildLanguageStimulusSpikesForTarget",
            "PerceptionLanguageBridge",
            "PhoneticLanguageEngine",
            "EnglishLanguageLexicon",
            "LanguageBackoffPolicy",
            "DialogueTurnManager");
        AssertSourceOmits(
            telemetryRoutes,
            "prosody-telemetry",
            "PerceptionLanguage",
            "LanguageBackoff");
        Assert.DoesNotContain("PerceptionLanguageBridge", settings, StringComparison.Ordinal);
    }

    [Fact]
    public void TransportTelemetryHasNoHostLanguageConditioningFields()
    {
        var propertyNames = typeof(TransportRuntimeStats)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain(propertyNames, name => name.Contains("PerceptionLanguage", StringComparison.Ordinal));
        Assert.DoesNotContain(propertyNames, name => name.Contains("LanguageBackoff", StringComparison.Ordinal));
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

    [Fact]
    public void DyadGroundingContractContainsOnlyNeuronalEvidence()
    {
        Assert.Equal("dyad.language-candidate.v2", DyadLanguageContract.ProtocolVersion);
        var propertyNames = typeof(DyadLanguageGroundingSnapshot)
            .GetProperties()
            .Select(static property => property.Name)
            .ToArray();
        string[] forbidden =
        [
            "BoundGoalKey",
            "SemanticFocus",
            "NeedState",
            "AffectiveState",
            "CommunicationIntent",
            "MemoryExcerpts",
            "GroundedLabel"
        ];
        Assert.All(forbidden, property => Assert.DoesNotContain(property, propertyNames));
        Assert.Null(typeof(DyadEntityPromptSnapshot).GetProperty("FallbackText"));
        Assert.Null(typeof(DyadEntityGenerationResponse).GetProperty("UsedFallback"));
    }

    [Fact]
    public void SyntheticNarrationAndSemanticPerceptAnnotationsDoNotExist()
    {
        var avatar = typeof(AvatarSightFrame).Assembly;
        Assert.Null(avatar.GetType("NRE.SimAvatar.AvatarBrainNarration", throwOnError: false));
        Assert.Null(typeof(AvatarControlApi).GetMethod("TryReadBrainNarration"));

        var perception = ReadSource("ControlProgram", "NeuronalPerception.cs");
        var grounding = ReadSource("ControlProgram", "NeuronalLanguageGrounding.cs");
        var program = ReadSource("ControlProgram", "Program.cs");
        AssertSourceOmits(
            perception,
            "PerceptLanguageAnnotation",
            "TryAttachLanguageAnnotation",
            "ObjectId",
            "LanguageAnnotationAttached");
        AssertSourceOmits(
            grounding,
            "GroundedLabel",
            "post-percept-language-annotation");
        AssertSourceOmits(
            program,
            "Post-percept annotation:",
            "FallbackText",
            "BrainNarration");

        foreach (var project in new[] { "NRE.WpfMazeSim", "NRE.WpfWorldSim" })
        {
            AssertSourceOmits(
                ReadSource("src", project, "MainWindow.xaml.cs"),
                "TryReadBrainNarration",
                "AvatarBrainNarration",
                "BrainNarrationText");
            Assert.DoesNotContain(
                "BrainNarrationText",
                ReadSource("src", project, "MainWindow.xaml"),
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void EditorDoesNotGenerateOrSpeakHostAuthoredPhrases()
    {
        var root = ResolveRepositoryRoot();
        var editorDirectory = Path.Combine(root, "src", "NRE.WpfEditor");
        var editor = File.ReadAllText(Path.Combine(editorDirectory, "MainWindow.xaml.cs"));
        var xaml = File.ReadAllText(Path.Combine(editorDirectory, "MainWindow.xaml"));

        Assert.False(File.Exists(Path.Combine(editorDirectory, "MainWindow.Speech.cs")));
        AssertSourceOmits(
            editor,
            "BuildSpeechPhrase",
            "RememberLanguageUtterance",
            "TryQueueSpeechFromLanguageDispatch",
            "SAPI.SpVoice",
            "SpeechTriggerMode",
            "_speechQueue");
        AssertSourceOmits(
            xaml,
            "Speech Output",
            "SpeechTriggerModeCombo",
            "ToggleSpeechOutputButton");
    }

    [Fact]
    public void DesktopObserversDoNotReadRetiredSemanticState()
    {
        var maze = ReadSource("src", "NRE.WpfMazeSim", "MainWindow.xaml.cs") +
                   ReadSource("src", "NRE.WpfMazeSim", "MainWindow.xaml");
        var world = ReadSource("src", "NRE.WpfWorldSim", "MainWindow.xaml.cs") +
                    ReadSource("src", "NRE.WpfWorldSim", "MainWindow.xaml");
        var editor = ReadSource("src", "NRE.WpfEditor", "MainWindow.TelemetryFormatters.cs");

        AssertSourceOmits(
            maze,
            "objectMemory",
            "ObjectMemory",
            "limbicState",
            "globalNeuromodState");
        AssertSourceOmits(
            world,
            "/api/v1/admin/object-memory",
            "planningWorkspace",
            "goalIntent",
            "intentionalActionLoop",
            "limbicState",
            "ReadBrainIntentCarrier");
        AssertSourceOmits(
            editor,
            "limbicState",
            "globalNeuromodState",
            "groundedLabel",
            "Narration:",
            "motorDirective",
            "commandKey");
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
