using Microsoft.Extensions.Configuration;
using NRE.BlazorEditor.Services;
using NRE.WorldSim;
using NeuralResonanceEngine.Protocol;
using System.Text.Json;

namespace NeuralResonanceEngine.DNNE.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class BlazorEditorEnvironmentCollection
{
    public const string Name = "Blazor editor environment";
}

[Collection(BlazorEditorEnvironmentCollection.Name)]
public sealed class BlazorEditorHostTests
{
    private static readonly string[] EditorEnvironmentVariables =
    [
        "NRE_EDITOR_CONTROL_BASE_URL",
        "NRE_EDITOR_LISTEN_ANY_IP",
        "NRE_EDITOR_ACCESS_KEY",
        "NRE_EDITOR_PORT",
        "NRE_EDITOR_TRUST_FORWARDED_HEADERS"
    ];

    [Fact]
    public void ControlProgramEndpointMustRemainOnLoopback()
    {
        WithCleanEditorEnvironment(() =>
        {
            var configuration = BuildConfiguration(new Dictionary<string, string?>
            {
                ["Editor:ControlProgramBaseUrl"] = "http://192.168.1.20:5080"
            });

            var error = Assert.Throws<InvalidOperationException>(
                () => EditorHostOptions.FromConfiguration(configuration));

            Assert.Contains("loopback", error.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void LanListenerRequiresAnAccessKey()
    {
        WithCleanEditorEnvironment(() =>
        {
            var configuration = BuildConfiguration(new Dictionary<string, string?>
            {
                ["Editor:ListenAnyIp"] = "true"
            });

            var error = Assert.Throws<InvalidOperationException>(
                () => EditorHostOptions.FromConfiguration(configuration));

            Assert.Contains("NRE_EDITOR_ACCESS_KEY", error.Message, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void AuthenticatedLanListenerKeepsControlLocal()
    {
        WithCleanEditorEnvironment(() =>
        {
            var configuration = BuildConfiguration(new Dictionary<string, string?>
            {
                ["Editor:ControlProgramBaseUrl"] = "http://127.0.0.1:5080",
                ["Editor:ListenAnyIp"] = "true",
                ["Editor:AccessKey"] = "correct horse battery staple",
                ["Editor:Port"] = "5190"
            });

            var options = EditorHostOptions.FromConfiguration(configuration);

            Assert.True(options.ControlProgramBaseUri.IsLoopback);
            Assert.True(options.ListenAnyIp);
            Assert.True(options.RequiresAuthentication);
            Assert.Equal(5190, options.Port);
            Assert.True(options.IsValidAccessKey("correct horse battery staple"));
            Assert.False(options.IsValidAccessKey("incorrect"));
        });
    }

    [Fact]
    public void LauncherMasksSecretsInItsEnvironmentPreview()
    {
        var root = ResolveRepositoryRoot();
        var launcherHelper = File.ReadAllText(Path.Combine(root, "tools", "_start-dnne-project.ps1"));

        Assert.Contains("Format-EnvironmentPreviewValue", launcherHelper, StringComparison.Ordinal);
        Assert.Contains("secret|password|token|key", launcherHelper, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<set>", launcherHelper, StringComparison.Ordinal);
    }

    [Fact]
    public void BrowserAtlasCarriesCanonicalCorticalTerritories()
    {
        var root = ResolveRepositoryRoot();
        var atlasPath = Path.Combine(root, "src", "NRE.BlazorEditor", "wwwroot", "data", "brain-atlas.json");
        using var atlas = JsonDocument.Parse(File.ReadAllText(atlasPath));
        var document = atlas.RootElement;
        var cortical = document.GetProperty("structures")
            .EnumerateArray()
            .Where(item => item.GetProperty("layout").GetString() == "CorticalSheet")
            .ToArray();

        Assert.Equal(2, document.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(70, cortical.Length);
        Assert.Equal(Enum.GetValues<StructureId>().Length, document.GetProperty("structures")
            .EnumerateArray()
            .Select(item => item.GetProperty("protocolStructureId").GetInt32())
            .Distinct()
            .Count());
        Assert.All(cortical, item =>
        {
            Assert.True(item.TryGetProperty("corticalTerritory", out var territory));
            Assert.False(string.IsNullOrWhiteSpace(territory.GetProperty("shape").GetString()));
        });

        var rightOrbitofrontal = Assert.Single(cortical, item =>
            item.GetProperty("instanceId").GetString() == "R_OrbitofrontalCortex");
        Assert.Equal([38d, -12d, 44d],
            rightOrbitofrontal.GetProperty("centerMm").EnumerateArray().Select(value => value.GetDouble()).ToArray());
        Assert.Equal("VentralBand",
            rightOrbitofrontal.GetProperty("corticalTerritory").GetProperty("shape").GetString());
    }

    [Fact]
    public void BrowserRendererUsesHalfMantlesAndSurfaceParcels()
    {
        var root = ResolveRepositoryRoot();
        var renderer = File.ReadAllText(Path.Combine(
            root, "src", "NRE.BlazorEditor", "wwwroot", "js", "brain-editor.js"));

        Assert.Contains("createCorticalMantleGeometry", renderer, StringComparison.Ordinal);
        Assert.Contains("createCorticalTerritoryGeometry", renderer, StringComparison.Ordinal);
        Assert.Contains("isInsideCorticalTerritory", renderer, StringComparison.Ordinal);
        Assert.DoesNotContain("new THREE.SphereGeometry(1, 64, 42)", renderer, StringComparison.Ordinal);
    }

    [Fact]
    public void BrowserRendererMapsLiveProtocolActivityAndToleratesBriefDelays()
    {
        var root = ResolveRepositoryRoot();
        var renderer = File.ReadAllText(Path.Combine(
            root, "src", "NRE.BlazorEditor", "wwwroot", "js", "brain-editor.js"));
        var gateway = File.ReadAllText(Path.Combine(
            root, "src", "NRE.BlazorEditor", "Services", "ControlTelemetryClient.cs"));

        Assert.Contains("protocolStructureId", renderer, StringComparison.Ordinal);
        Assert.Contains("meanFiringRateHz", renderer, StringComparison.Ordinal);
        Assert.Contains("spikeOutCount", renderer, StringComparison.Ordinal);
        Assert.Contains("resolveStructureId", renderer, StringComparison.Ordinal);
        Assert.Contains("setRuntimeDelayed", renderer, StringComparison.Ordinal);
        Assert.Contains("TimeSpan.FromSeconds(12)", gateway, StringComparison.Ordinal);
    }

    [Fact]
    public void BrowserRendererPreservesHemispheresAndUsesAnatomicalViewAxes()
    {
        var root = ResolveRepositoryRoot();
        var renderer = File.ReadAllText(Path.Combine(
            root, "src", "NRE.BlazorEditor", "wwwroot", "js", "brain-editor.js"));
        var page = File.ReadAllText(Path.Combine(
            root, "src", "NRE.BlazorEditor", "Components", "Pages", "Editor.razor"));

        Assert.Contains("value(spike, 'sourceHemisphere')", renderer, StringComparison.Ordinal);
        Assert.Contains("value(spike, 'targetHemisphere')", renderer, StringComparison.Ordinal);
        Assert.Contains("meshesForStructureHemisphere", renderer, StringComparison.Ordinal);
        Assert.Contains("sourceHemisphere ?? '*'", renderer, StringComparison.Ordinal);
        Assert.Contains("targetHemisphere ?? '*'", renderer, StringComparison.Ordinal);
        Assert.Contains("camera.up.fromArray(preset.up)", renderer, StringComparison.Ordinal);
        Assert.Contains("superior: { position: [0, 245, -4]", renderer, StringComparison.Ordinal);
        Assert.Contains("left: { position: [245, -3, -4]", renderer, StringComparison.Ordinal);
        Assert.Contains("right: { position: [-245, -3, -4]", renderer, StringComparison.Ordinal);
        Assert.Contains("Math.pow(lateralShoulder, 2.15)", renderer, StringComparison.Ordinal);
        Assert.Contains("side: THREE.FrontSide", renderer, StringComparison.Ordinal);

        foreach (var view in new[] { "anterior", "posterior", "left", "right", "superior", "inferior" })
        {
            Assert.Contains($"data-editor-view=\"{view}\"", page, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task WorldStateReaderReturnsAuthoritativeInProcessState()
    {
        await using var runtime = new HeadlessWorldRuntime(new HeadlessWorldOptions(
            new Uri("http://127.0.0.1:5080"),
            Seed: 911));
        var result = await new WorldStateReader(runtime).ReadAsync(CancellationToken.None);

        Assert.True(result.Available);
        Assert.Equal("live", result.Status);
        Assert.Equal(911, result.State!.Value.GetProperty("seed").GetInt32());
        Assert.Equal("dnne.worldsim.state.v3", result.State.Value.GetProperty("protocolVersion").GetString());
    }

    [Fact]
    public void BrowserWorldRendererKeepsFineVoxelsAndExposesNeuralAvatarAnatomy()
    {
        var root = ResolveRepositoryRoot();
        var renderer = File.ReadAllText(Path.Combine(
            root, "src", "NRE.BlazorEditor", "wwwroot", "js", "world-editor.js"));
        var page = File.ReadAllText(Path.Combine(
            root, "src", "NRE.BlazorEditor", "Components", "Pages", "Editor.razor"));

        Assert.Contains("const VISUAL_SUBDIVISIONS = 4", renderer, StringComparison.Ordinal);
        Assert.Contains("const HEIGHT_UNITS_PER_METER = 4", renderer, StringComparison.Ordinal);
        Assert.Contains("const TERRAIN_HEIGHT_UNIT = 0.25", renderer, StringComparison.Ordinal);
        Assert.Contains("const SEA_LEVEL_METERS = 3", renderer, StringComparison.Ordinal);
        Assert.Contains("const CLIFF_THRESHOLD_HEIGHT_UNITS = 4", renderer, StringComparison.Ordinal);
        Assert.Contains("heightUnitsAtWorld(state.heights, worldX, worldZ)", renderer, StringComparison.Ordinal);
        Assert.Contains("new THREE.InstancedMesh", renderer, StringComparison.Ordinal);
        Assert.Contains("function createAvatar()", renderer, StringComparison.Ordinal);
        Assert.Contains("new THREE.Bone()", renderer, StringComparison.Ordinal);
        Assert.Contains("new THREE.Skeleton(rig.bones)", renderer, StringComparison.Ordinal);
        Assert.Contains("createRigVisuals(rig)", renderer, StringComparison.Ordinal);
        Assert.Contains("state.articulation.leftKnee", renderer, StringComparison.Ordinal);
        Assert.Contains("leftHipAngleRadians", renderer, StringComparison.Ordinal);
        Assert.Contains("leftHipAbductionRadians", renderer, StringComparison.Ordinal);
        Assert.Contains("trunkPitchRadians", renderer, StringComparison.Ordinal);
        Assert.Contains("trunkYawRadians", renderer, StringComparison.Ordinal);
        Assert.Contains("setSignedChannel('leftShoulderChannel'", renderer, StringComparison.Ordinal);
        Assert.Contains("setUnsignedChannel('leftFootLoadChannel'", renderer, StringComparison.Ordinal);
        Assert.Contains("new THREE.LatheGeometry(torsoProfile", renderer, StringComparison.Ordinal);
        Assert.Contains("Flattened pinnae", renderer, StringComparison.Ordinal);
        Assert.Contains("hairLocks", renderer, StringComparison.Ordinal);
        Assert.Contains("heavy forequarters", renderer, StringComparison.Ordinal);
        Assert.Contains("function addNerve", renderer, StringComparison.Ordinal);
        Assert.Contains("state.avatar.body.position.y = gaitBob", renderer, StringComparison.Ordinal);
        Assert.Contains("terrainTopAt(state.heights, state.avatar.root.position.x", renderer, StringComparison.Ordinal);
        Assert.Contains("prepareShelterGround(state.heights, state.shelterSites)", renderer, StringComparison.Ordinal);
        Assert.Contains("isInsideShelterClearance(worldX, worldZ, shelterSites)", renderer, StringComparison.Ordinal);
        Assert.Contains("slab(wall, -2.5, 1.2, 3.8, 2.8, 2.4, 0.32)", renderer, StringComparison.Ordinal);
        Assert.Contains("slab(wall, 2.5, 1.2, 3.8, 2.8, 2.4, 0.32)", renderer, StringComparison.Ordinal);
        Assert.DoesNotContain("slab(wall, 0, 2.20, 3.8, 2.2, 0.40, 0.32)", renderer, StringComparison.Ordinal);
        Assert.DoesNotContain("slab(glass, 0, 1.55, 3.82", renderer, StringComparison.Ordinal);
        Assert.DoesNotContain("state.avatar.root.position.y +=", renderer, StringComparison.Ordinal);
        Assert.Contains("data-avatar-mode=\"neural\"", page, StringComparison.Ordinal);
        Assert.Contains("data-workspace-tab=\"world\"", page, StringComparison.Ordinal);
        Assert.Contains("id=\"leftShoulderChannel\"", page, StringComparison.Ordinal);
        Assert.Contains("id=\"rightAnkleChannel\"", page, StringComparison.Ordinal);
        Assert.Contains("id=\"leftHipAbductionChannel\"", page, StringComparison.Ordinal);
        Assert.Contains("id=\"manipulatorExtensionChannel\"", page, StringComparison.Ordinal);
        Assert.Contains("id=\"leftHandApertureChannel\"", page, StringComparison.Ordinal);
        Assert.Contains("id=\"rightHandApertureChannel\"", page, StringComparison.Ordinal);
        Assert.Contains("id=\"avatarDevelopmentStage\"", page, StringComparison.Ordinal);
        Assert.Contains("id=\"worldHandSequence\"", page, StringComparison.Ordinal);
        Assert.Contains("numberValue(snapshot, 'leftGripForceNewtons')", renderer, StringComparison.Ordinal);
        Assert.Contains("integerValue(snapshot, 'graspMisses')", renderer, StringComparison.Ordinal);
        Assert.Contains("id=\"trunkYawChannel\"", page, StringComparison.Ordinal);
        Assert.Contains("const JOINT_LIMITS = Object.freeze", renderer, StringComparison.Ordinal);
        Assert.Contains("clampJoint(state.articulation.leftElbow, 'elbow')", renderer, StringComparison.Ordinal);
        Assert.Contains("clampJoint(state.articulation.leftKnee, 'knee')", renderer, StringComparison.Ordinal);
        Assert.Contains("-clampJoint(state.articulation.leftHip, 'hip')", renderer, StringComparison.Ordinal);
        Assert.Contains("./js/world-editor.js?v=137", page, StringComparison.Ordinal);
    }

    [Fact]
    public void WorldWorkspaceDoesNotOverrideGlobalBrainRuntimeSummary()
    {
        var root = ResolveRepositoryRoot();
        var worldRenderer = File.ReadAllText(Path.Combine(
            root, "src", "NRE.BlazorEditor", "wwwroot", "js", "world-editor.js"));
        var brainRenderer = File.ReadAllText(Path.Combine(
            root, "src", "NRE.BlazorEditor", "wwwroot", "js", "brain-editor.js"));

        Assert.DoesNotContain("updateWorldHeader", worldRenderer, StringComparison.Ordinal);
        Assert.DoesNotContain("setText('runtimeTick'", worldRenderer, StringComparison.Ordinal);
        Assert.DoesNotContain("setText('runtimeServices'", worldRenderer, StringComparison.Ordinal);
        Assert.Contains("applyFrame(state, frame);\n        setRuntimeState(frame, 'online');", brainRenderer, StringComparison.Ordinal);
    }

    [Fact]
    public void WorldSimulatorSnapshotPublishesLiveEntityCoordinates()
    {
        var root = ResolveRepositoryRoot();
        var worldSource = File.ReadAllText(Path.Combine(
            root, "src", "NRE.WorldSim", "HeadlessWorldRuntime.cs"));
        var hostSource = File.ReadAllText(Path.Combine(
            root, "src", "NRE.BlazorEditor", "Program.cs"));

        Assert.Contains("FoodPickups: SnapshotEntities(foods)", worldSource, StringComparison.Ordinal);
        Assert.Contains("WeaponPickups: SnapshotEntities(devices)", worldSource, StringComparison.Ordinal);
        Assert.Contains("Predators: SnapshotEntities(predators)", worldSource, StringComparison.Ordinal);
        Assert.Contains("Shelters: SnapshotEntities(shelters)", worldSource, StringComparison.Ordinal);
        Assert.Contains("AvatarNeuronalMotorBridge.Compose", worldSource, StringComparison.Ordinal);
        Assert.Contains("PostRetinalFrameAsync", worldSource, StringComparison.Ordinal);
        Assert.Contains("PostPhysicalBodyFrameAsync", worldSource, StringComparison.Ordinal);
        Assert.Contains("options.MotorTrainingMode ? 0.0 : 1.0", worldSource, StringComparison.Ordinal);
        Assert.Contains("TerrainAscentCompleted: terrainAscent.CompletedCount", worldSource, StringComparison.Ordinal);
        Assert.Contains("AddHostedService<WorldRuntimeHostedService>", hostSource, StringComparison.Ordinal);
        Assert.Contains("MotorTrainingMode: false", hostSource, StringComparison.Ordinal);
        Assert.Contains("builder.Configuration[\"World:DevelopmentStage\"]", hostSource, StringComparison.Ordinal);
        Assert.Contains("DevelopmentStage: developmentStage", hostSource, StringComparison.Ordinal);
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static void WithCleanEditorEnvironment(Action action)
    {
        var previous = EditorEnvironmentVariables.ToDictionary(
            name => name,
            name => Environment.GetEnvironmentVariable(name));
        try
        {
            foreach (var name in EditorEnvironmentVariables)
            {
                Environment.SetEnvironmentVariable(name, null);
            }

            action();
        }
        finally
        {
            foreach (var pair in previous)
            {
                Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            }
        }
    }

    private static string ResolveRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null &&
               !File.Exists(Path.Combine(current.FullName, "NeuralResonanceEngine.DNNE.slnx")))
        {
            current = current.Parent;
        }

        return current?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
