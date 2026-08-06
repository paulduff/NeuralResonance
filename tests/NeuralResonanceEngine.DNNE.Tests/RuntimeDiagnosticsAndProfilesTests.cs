using System.Text.Json;
using Microsoft.Extensions.Configuration;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class RuntimeDiagnosticsAndProfilesTests
{
    [Theory]
    [InlineData("working", 0.8f, 2, "motor_seek_shelter")]
    [InlineData("stalled", 0.2f, 2, "motor_turn_left")]
    [InlineData("stalled", 0.2f, 3, "motor_turn_right")]
    [InlineData("stalled", 0.8f, 2, "motor_about_face_left")]
    [InlineData("stalled", 0.8f, 3, "motor_about_face_right")]
    public void MotorRecoveryTurnsAStalledLocomotionIntent(
        string monitorState,
        float blocked,
        long sequence,
        string expected)
    {
        var directive = SimulationState.ApplyMotorRecoveryDirective(
            "motor_seek_shelter",
            monitorState,
            blocked,
            sequence);

        Assert.Equal(expected, directive);
    }

    [Theory]
    [InlineData("stable", 12, true)]
    [InlineData("diagnostic", 2, true)]
    [InlineData("normal", 6, true)]
    [InlineData("fast", 12, true)]
    [InlineData("headless", 30, true)]
    [InlineData("ultra", 12, true)]
    public void Performance_Profile_Presets_Map_To_Runtime_Settings(string profile, int snapshotEvery, bool directStep)
    {
        var settings = RuntimePerformanceProfileSettings.ForProfile(profile);

        Assert.True(RuntimePerformanceProfileSettings.IsSupported(profile));
        Assert.Equal(snapshotEvery, settings.SnapshotEveryNTicks);
        Assert.Equal(directStep, settings.UseDirectStepFastPath);
    }

    [Fact]
    public void Configured_Ultra_Profile_Is_Accepted_As_Fast_Compatibility_Alias()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PerformanceProfile"] = "ultra"
            })
            .Build();

        var settings = RuntimePerformanceProfileSettings.FromConfiguration(configuration);

        Assert.Equal("fast", settings.ProfileName);
    }

    [Fact]
    public void Runtime_Defaults_Start_In_Healthy_Awake_Baseline()
    {
        var state = new SimulationState();

        Assert.False(state.SleepMemory.IsSleeping);
        Assert.InRange(state.SleepMemory.SleepPressure, 0.0f, 0.20f);
        Assert.InRange(state.LimbicState.NeuromodState.DopamineLevel, 0.40f, 0.55f);
        Assert.InRange(state.LimbicState.NeuromodState.AcetylcholineLevel, 0.45f, 0.60f);
        Assert.InRange(state.LimbicState.Threat, 0.0f, 0.10f);
    }

    [Fact]
    public void Circuit_Audit_Warns_When_Registered_Structure_Has_No_Route_Or_Spikes()
    {
        var state = new SimulationState();
        state.Configure(
            tickDurationMs: 1.0,
            registry: new Dictionary<StructureId, string>
            {
                [StructureId.V1] = "http://localhost:5001"
            },
            connectivity: new Dictionary<StructureId, List<SynapticConnection>>());

        AdvanceTicks(state, 1201);

        using var document = SerializeDiagnostics(state);
        var audit = GetObject(document.RootElement, "circuitAudit");
        var summary = GetObject(audit, "summary");
        var firstWarning = GetFirstArrayObject(audit, "warnings");

        Assert.True(GetInt(summary, "neverSpikedCount") >= 1);
        Assert.True(GetInt(summary, "registeredDisconnectedCount") >= 1);
        Assert.Contains("registered/visible but disconnected", ReadStringArray(firstWarning, "issues"));
        Assert.Contains("never spiked in retained trace", ReadStringArray(firstWarning, "issues"));
        Assert.Contains("visual", GetString(firstWarning, "purpose"), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("disconnected route", GetString(firstWarning, "silenceCause"));
        Assert.Equal("no recent activation evidence", GetString(firstWarning, "lastActivationReason"));
    }

    [Fact]
    public void Circuit_Audit_Warns_When_Connectome_Target_Has_No_Registered_Service()
    {
        var state = new SimulationState();
        state.Configure(
            tickDurationMs: 1.0,
            registry: new Dictionary<StructureId, string>
            {
                [StructureId.V1] = "http://localhost:5001"
            },
            connectivity: new Dictionary<StructureId, List<SynapticConnection>>
            {
                [StructureId.V1] =
                [
                    new SynapticConnection(
                        StructureId.Pfc,
                        Guid.NewGuid(),
                        NTEnum.GLUTAMATE,
                        "feedforward")
                ]
            });

        AdvanceTicks(state, 1201);

        using var document = SerializeDiagnostics(state);
        var audit = GetObject(document.RootElement, "circuitAudit");
        var pfcWarning = EnumerateObjects(audit, "warnings")
            .First(w => GetString(w, "structure").Equals("Pfc", StringComparison.OrdinalIgnoreCase));

        Assert.Contains("connectome route has no registered service", ReadStringArray(pfcWarning, "issues"));
        Assert.Equal("missing service for known connectome route", GetString(pfcWarning, "silenceCause"));
        Assert.Contains("executive", GetString(pfcWarning, "purpose"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Circuit_Audit_Classifies_Routed_Registered_Service_Without_Telemetry_As_Offline()
    {
        var state = new SimulationState();
        state.Configure(
            tickDurationMs: 1.0,
            registry: new Dictionary<StructureId, string>
            {
                [StructureId.Pfc] = "http://localhost:5001",
                [StructureId.M1] = "http://localhost:5002"
            },
            connectivity: new Dictionary<StructureId, List<SynapticConnection>>
            {
                [StructureId.Pfc] =
                [
                    new SynapticConnection(
                        StructureId.M1,
                        Guid.NewGuid(),
                        NTEnum.GLUTAMATE,
                        "corticospinal planning")
                ],
                [StructureId.M1] =
                [
                    new SynapticConnection(
                        StructureId.Pfc,
                        Guid.NewGuid(),
                        NTEnum.GLUTAMATE,
                        "efference copy")
                ]
            });

        AdvanceTicks(state, 121);

        using var document = SerializeDiagnostics(state);
        var audit = GetObject(document.RootElement, "circuitAudit");
        var summary = GetObject(audit, "summary");
        var pfcWarning = EnumerateObjects(audit, "warnings")
            .First(w => GetString(w, "structure").Equals("Pfc", StringComparison.OrdinalIgnoreCase));

        Assert.True(GetInt(summary, "serviceOfflineCount") >= 1);
        Assert.Contains("registered service has no telemetry", ReadStringArray(pfcWarning, "issues"));
        Assert.Equal("offline", GetString(pfcWarning, "serviceState"));
        Assert.Equal("service unavailable: INIT", GetString(pfcWarning, "silenceCause"));
    }

    [Fact]
    public void Input_Gates_AutoRestore_Spontaneous_Spiking_After_Neural_Starvation()
    {
        var state = new SimulationState();
        Assert.True(state.TrySetInputGates(
            new InputGateControlRequest(AvatarVisionEnabled: true, SpontaneousSpikingEnabled: false),
            out var disabled,
            out var error));
        Assert.Null(error);
        Assert.False(disabled.SpontaneousSpikingEnabled);

        var changed = state.EnsureSpontaneousSpikingEnabled("test starvation");
        var restored = state.GetInputGatesSnapshot();

        Assert.True(changed);
        Assert.True(restored.SpontaneousSpikingEnabled);
    }

    [Fact]
    public void Input_Gates_AutoRestore_Is_Idempotent_When_Spontaneous_Spiking_Is_Already_On()
    {
        var state = new SimulationState();

        var changed = state.EnsureSpontaneousSpikingEnabled("already active");

        Assert.False(changed);
        Assert.True(state.GetInputGatesSnapshot().SpontaneousSpikingEnabled);
    }

    [Fact]
    public void Runtime_Instance_Catalog_Does_Not_Treat_Known_Instances_As_Live()
    {
        var catalog = new RuntimeInstanceCatalog();
        var v1 = new ServiceInstance(
            StructureId.V1,
            "V1_L",
            "L",
            new Uri("http://localhost:5001"));

        catalog.SetKnownInstances([v1]);

        Assert.Empty(catalog.GetByStructure(StructureId.V1, "left"));
        Assert.Single(catalog.GetByStructureWithKnownFallback(StructureId.V1, "left"));

        catalog.SetLiveInstances([v1]);

        Assert.Single(catalog.GetByStructure(StructureId.V1, "left"));

        catalog.SetLiveInstances([]);

        Assert.Empty(catalog.GetByStructure(StructureId.V1, "left"));
        Assert.Single(catalog.GetByStructureWithKnownFallback(StructureId.V1, "left"));
    }

    [Fact]
    public void Circuit_Audit_Keeps_Quiet_Functions_Distinct_From_Unsupported_Active_Functions()
    {
        var state = new SimulationState();

        using var document = SerializeDiagnostics(state);
        var audit = GetObject(document.RootElement, "circuitAudit");
        var functions = EnumerateObjects(audit, "functionSupport").ToArray();

        Assert.NotEmpty(functions);
        Assert.DoesNotContain(
            functions,
            entry => GetBool(entry, "active") && GetString(entry, "status") == "unsupported");
        Assert.All(
            functions,
            entry => Assert.NotEmpty(ReadStringArray(entry, "requiredStructures")));
    }

    [Fact]
    public void Legacy_Perception_Writers_Diagnostics_And_Checkpoint_State_Are_Absent()
    {
        Assert.Null(typeof(SimulationState).GetMethod("ObservePredictivePerception"));
        Assert.Null(typeof(SimulationState).GetMethod("GetPredictivePerceptionSnapshot"));
        Assert.Null(typeof(SimulationState).GetMethod("GetPersistentPerceptsSnapshot"));
        Assert.Null(typeof(NetworkStateDocument).GetProperty("PersistentPercepts"));

        using var document = SerializeDiagnostics(new SimulationState());
        Assert.False(TryGetProperty(document.RootElement, "predictivePerception", out _));
        Assert.False(TryGetProperty(document.RootElement, "persistentPercepts", out _));
    }

    [Fact]
    public void Legacy_Attention_And_Workspace_Authority_Is_Absent()
    {
        const System.Reflection.BindingFlags allMethods =
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic;

        Assert.Null(typeof(SimulationState).GetMethod("UpdateAttentionState", allMethods));
        Assert.Null(typeof(SimulationState).GetMethod("GetAttentionSnapshot", allMethods));
        Assert.Null(typeof(SimulationState).GetMethod("GetPrefrontalWorkingMemorySnapshot", allMethods));
        Assert.Null(typeof(SimulationState).GetMethod("GetConsciousnessRhythmSnapshot", allMethods));
        Assert.Null(typeof(SimulationState).GetMethod("GetGlobalWorkspaceSnapshot", allMethods));
        Assert.Null(typeof(SimulationState).GetMethod("UpdatePrefrontalWorkingMemoryLocked", allMethods));
        Assert.Null(typeof(SimulationState).GetMethod("UpdateConsciousnessRhythmLocked", allMethods));
        Assert.Null(typeof(SimulationState).GetMethod("UpdateGlobalWorkspaceLocked", allMethods));

        Assert.Null(typeof(NetworkStateDocument).GetProperty("GlobalAttentionBias"));
        Assert.Null(typeof(NetworkStateDocument).GetProperty("AttentionState"));
        Assert.Null(typeof(NetworkStateDocument).GetProperty("PrefrontalWorkingMemory"));
        Assert.Null(typeof(NetworkStateDocument).GetProperty("ConsciousnessRhythm"));
        Assert.Null(typeof(NetworkStateDocument).GetProperty("GlobalWorkspace"));

        var updateNeuromod = Assert.Single(
            typeof(SimulationState).GetMethods(),
            static method => method.Name == "UpdateNeuromod");
        Assert.Equal(2, updateNeuromod.GetParameters().Length);

        using var document = SerializeDiagnostics(new SimulationState());
        Assert.False(TryGetProperty(document.RootElement, "attentionState", out _));
        Assert.False(TryGetProperty(document.RootElement, "prefrontalWorkingMemory", out _));
        Assert.False(TryGetProperty(document.RootElement, "consciousnessRhythm", out _));
        Assert.False(TryGetProperty(document.RootElement, "globalWorkspace", out _));
    }


    [Fact]
    public void Legacy_Symbolic_Cognition_Harness_Is_Absent()
    {
        const System.Reflection.BindingFlags allMethods =
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic;

        Assert.Null(typeof(SimulationState).GetMethod("ObserveCognitiveRuntime", allMethods));
    }

    private static void AdvanceTicks(SimulationState state, int count)
    {
        for (var i = 0; i < count; i++)
        {
            state.AdvanceClockAndCreateTickSignal();
        }
    }

    private static JsonDocument SerializeDiagnostics(SimulationState state)
        => JsonDocument.Parse(JsonSerializer.Serialize(state.ToDiagnostics()));

    private static JsonElement GetObject(JsonElement element, string name)
    {
        Assert.True(TryGetProperty(element, name, out var value));
        Assert.Equal(JsonValueKind.Object, value.ValueKind);
        return value;
    }

    private static JsonElement GetFirstArrayObject(JsonElement element, string name)
        => EnumerateObjects(element, name).First();

    private static IEnumerable<JsonElement> EnumerateObjects(JsonElement element, string name)
    {
        Assert.True(TryGetProperty(element, name, out var value));
        Assert.Equal(JsonValueKind.Array, value.ValueKind);
        return value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.Object).ToArray();
    }

    private static int GetInt(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value) || value.ValueKind != JsonValueKind.Number)
        {
            return 0;
        }

        return value.GetInt32();
    }

    private static float GetSingle(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value) || value.ValueKind != JsonValueKind.Number)
        {
            return 0f;
        }

        return value.GetSingle();
    }

    private static bool GetBool(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value))
        {
            return false;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => false
        };
    }

    private static string GetString(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return string.Empty;
        }

        return value.GetString() ?? string.Empty;
    }

    private static string[] ReadStringArray(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return value
            .EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString() ?? string.Empty)
            .Where(item => item.Length > 0)
            .ToArray();
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
