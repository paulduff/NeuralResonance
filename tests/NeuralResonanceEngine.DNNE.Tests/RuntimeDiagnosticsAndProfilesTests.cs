using System.Text.Json;
using Microsoft.Extensions.Configuration;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class RuntimeDiagnosticsAndProfilesTests
{
    [Theory]
    [InlineData("stable", 12, false)]
    [InlineData("diagnostic", 2, false)]
    [InlineData("normal", 6, false)]
    [InlineData("fast", 12, false)]
    [InlineData("headless", 30, false)]
    [InlineData("ultra", 12, false)]
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
        Assert.False(state.GoalIntent.Active);
        Assert.Equal("motor_idle", state.GoalIntent.MotorDirective);
        Assert.False(state.MotivationArbitration.Active);
        Assert.False(state.IntentionalActionLoop.Active);
        Assert.Equal("motor_idle", state.IntentionalActionLoop.MotorDirective);
        Assert.InRange(state.IntentionalActionLoop.M1Readiness, 0.18f, 0.35f);
        Assert.InRange(state.GoalIntent.InhibitoryTone, 0.0f, 0.35f);
        Assert.InRange(state.GoalIntent.BasalGangliaGate, 0.30f, 0.50f);
        Assert.InRange(state.AttentionState.FocusConfidence, 0.25f, 0.45f);
        Assert.InRange(state.AttentionState.ThalamicRelayGain, 0.55f, 0.75f);
        Assert.InRange(state.AttentionState.TrnInhibition, 0.0f, 0.25f);
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
    public void Circuit_Audit_Reports_Function_Level_Biological_Support()
    {
        var state = new SimulationState();
        var tokens = new[] { "find", "food" };
        var grammar = EnglishLanguageLexicon.AnalyzeGrammar(tokens);

        state.AdvanceClockAndCreateTickSignal();
        state.ObserveLanguageIntent(grammar, tokens, state.Tick);
        DriveCognition(state, 4);

        using var document = SerializeDiagnostics(state);
        var audit = GetObject(document.RootElement, "circuitAudit");
        var summary = GetObject(audit, "summary");
        var functions = EnumerateObjects(audit, "functionSupport").ToArray();
        var language = functions.First(entry => GetString(entry, "functionKey") == "language_loop");

        Assert.True(GetInt(summary, "functionCount") >= 12);
        Assert.True(functions.Length >= 12);
        Assert.True(GetBool(language, "active"));
        Assert.True(GetSingle(language, "support") > 0.10f);
        Assert.NotEqual("unsupported", GetString(language, "status"));
        Assert.Contains(ReadStringArray(language, "requiredStructures"), structure => structure.Contains("Broca", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(ReadStringArray(language, "requiredStructures"), structure => structure.Contains("Wernicke", StringComparison.OrdinalIgnoreCase));
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
    public void Biological_Attention_State_Is_Exposed_In_Diagnostics()
    {
        var state = new SimulationState();
        state.UpdateAttentionState(new BiologicalAttentionRuntime(
            Visual: 0.08f,
            Auditory: 0.06f,
            Somatosensory: 0.08f,
            Interoceptive: 0.48f,
            Language: 0.04f,
            Memory: 0.15f,
            Motor: 0.11f,
            DominantChannel: "interoceptive",
            FocusConfidence: 0.72f,
            Salience: 0.84f,
            ThalamicRelayGain: 0.62f,
            TrnInhibition: 0.58f,
            BasalForebrainGain: 0.66f,
            SensoryBias: new AttentionVector(0.12f, 0.08f, 0.16f, 0.64f),
            LastSwitchTick: 42,
            HoldTicksRemaining: 7));

        using var document = SerializeDiagnostics(state);
        var attention = GetObject(document.RootElement, "attentionState");
        var channels = GetObject(attention, "channels");
        var sensoryBias = GetObject(attention, "sensoryBias");

        Assert.Equal("interoceptive", GetString(attention, "dominantChannel"));
        Assert.True(GetSingle(channels, "interoceptive") > GetSingle(channels, "visual"));
        Assert.True(GetSingle(sensoryBias, "interoceptive") > GetSingle(sensoryBias, "visual"));
        Assert.True(GetSingle(attention, "trnInhibition") > 0.50f);
    }

    [Fact]
    public void Biological_Attention_Normalization_Preserves_A_Single_Biased_Sensory_Vector()
    {
        var normalized = BiologicalAttentionRuntime.Normalize(new BiologicalAttentionRuntime(
            Visual: 0.2f,
            Auditory: 0.2f,
            Somatosensory: 0.2f,
            Interoceptive: 0.2f,
            Language: 0.1f,
            Memory: 0.05f,
            Motor: 0.05f,
            DominantChannel: "",
            FocusConfidence: 2.0f,
            Salience: 2.0f,
            ThalamicRelayGain: 2.0f,
            TrnInhibition: 2.0f,
            BasalForebrainGain: 2.0f,
            SensoryBias: new AttentionVector(2.0f, 1.0f, 1.0f, 0.0f),
            LastSwitchTick: -10,
            HoldTicksRemaining: -1));

        var sensorySum = normalized.SensoryBias.Visual +
                         normalized.SensoryBias.Auditory +
                         normalized.SensoryBias.Somatosensory +
                         normalized.SensoryBias.Interoceptive;

        Assert.Equal(1.0f, sensorySum, precision: 5);
        Assert.InRange(normalized.FocusConfidence, 0.0f, 1.0f);
        Assert.InRange(normalized.TrnInhibition, 0.0f, 1.0f);
        Assert.True(normalized.LastSwitchTick >= 0);
        Assert.True(normalized.HoldTicksRemaining >= 0);
    }

    [Fact]
    public void Predictive_Perception_Biases_Attention_Toward_Unexpected_Channel()
    {
        var snapshots = new[]
        {
            MakeSnapshot(StructureId.A1, meanFiringRateHz: 40f)
        };
        var predictive = PredictivePerceptionRuntime.Normalize(PredictivePerceptionRuntime.Default with
        {
            Active = true,
            LastChannel = "auditory",
            LastCue = "bear:growl",
            InputSource = "world",
            AuditoryPrediction = 0.12f,
            AuditoryObservation = 0.92f,
            PredictionError = 0.80f,
            Surprise = 0.86f,
            Novelty = 0.74f,
            Confidence = 0.88f,
            LocusCoeruleusAlert = 0.82f,
            HippocampalEncodingGate = 0.76f,
            AccConflictSignal = 0.68f,
            SensoryBias = new AttentionVector(0.05f, 0.85f, 0.05f, 0.05f),
            LastUpdatedTick = 12
        });

        var baseline = ComputeAttentionForTest(
            tick: 20,
            snapshots,
            new Dictionary<(StructureId Source, StructureId Target, NTEnum Nt), int>(),
            BiologicalAttentionRuntime.Default,
            VisualAttentionRuntime.Default,
            PredictivePerceptionRuntime.Default,
            LimbicRuntimeState.Default,
            SleepMemoryRuntime.Default,
            EnvironmentalStateRuntime.Default,
            BodyStateRuntime.Default,
            LanguageIntentRuntime.Default,
            new AttentionVector(0.25f, 0.25f, 0.25f, 0.25f));
        var withPredictionError = ComputeAttentionForTest(
            tick: 20,
            snapshots,
            new Dictionary<(StructureId Source, StructureId Target, NTEnum Nt), int>(),
            BiologicalAttentionRuntime.Default,
            VisualAttentionRuntime.Default,
            predictive,
            LimbicRuntimeState.Default,
            SleepMemoryRuntime.Default,
            EnvironmentalStateRuntime.Default,
            BodyStateRuntime.Default,
            LanguageIntentRuntime.Default,
            new AttentionVector(0.25f, 0.25f, 0.25f, 0.25f));

        Assert.True(withPredictionError.Auditory > baseline.Auditory);
        Assert.True(withPredictionError.Memory > baseline.Memory);
        Assert.True(withPredictionError.Salience > baseline.Salience);
        Assert.True(withPredictionError.SensoryBias.Auditory > baseline.SensoryBias.Auditory);
    }

    [Fact]
    public void Predictive_Perception_Is_Exposed_In_Diagnostics()
    {
        var state = new SimulationState();
        AdvanceTicks(state, 12);
        var predictive = state.ObservePredictivePerception(
            "visual",
            "object:food",
            observation: 0.94f,
            confidence: 0.82f,
            inputSource: "world");

        using var document = SerializeDiagnostics(state);
        var diagnostic = GetObject(document.RootElement, "predictivePerception");

        Assert.True(predictive.Active);
        Assert.Equal("visual", GetString(diagnostic, "lastChannel"));
        Assert.Equal("object:food", GetString(diagnostic, "lastCue"));
        Assert.True(GetSingle(diagnostic, "surprise") > 0.20f);
        Assert.True(GetSingle(diagnostic, "hippocampalEncodingGate") > 0.0f);
        Assert.True(GetSingle(diagnostic, "sensitizationGate") > 0.0f);
        Assert.True(GetSingle(diagnostic, "noveltyEncodingDrive") > 0.0f);
        Assert.NotEqual("stable", GetString(diagnostic, "adaptationMode"));
        Assert.Contains("hippocampus", GetString(diagnostic, "adaptationEvidence"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Predictive_Perception_Tracks_Stimulus_Adaptation()
    {
        var state = new SimulationState();
        AdvanceTicks(state, 12);

        var first = state.ObservePredictivePerception(
            "visual",
            "object:lamp",
            observation: 0.72f,
            confidence: 0.84f,
            inputSource: "test");
        PredictivePerceptionRuntime repeated = first;
        for (var i = 0; i < 6; i++)
        {
            repeated = state.ObservePredictivePerception(
                "visual",
                "object:lamp",
                observation: 0.72f,
                confidence: 0.84f,
                inputSource: "test");
        }

        var novel = state.ObservePredictivePerception(
            "visual",
            "object:door",
            observation: 0.96f,
            confidence: 0.86f,
            inputSource: "test");

        Assert.True(repeated.HabituationGate > first.HabituationGate);
        Assert.True(repeated.RepetitionSuppression > first.RepetitionSuppression);
        Assert.Contains(repeated.AdaptationMode, new[] { "habituating", "sensitizing", "novelty_encoding" });
        Assert.True(novel.NoveltyEncodingDrive >= repeated.NoveltyEncodingDrive || novel.SensitizationGate >= repeated.SensitizationGate);

        using var document = SerializeDiagnostics(state);
        var audit = GetObject(document.RootElement, "circuitAudit");
        var functions = EnumerateObjects(audit, "functionSupport").ToArray();
        var adaptation = functions.First(entry => GetString(entry, "functionKey") == "stimulus_adaptation");

        Assert.True(GetSingle(adaptation, "support") > 0.10f);
        Assert.Contains(ReadStringArray(adaptation, "requiredStructures"), structure => structure.Contains("BasalForebrain", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(ReadStringArray(adaptation, "requiredStructures"), structure => structure.Contains("LocusCoeruleus", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Predictive_Sensory_Simulation_Uses_Memory_And_Goal_To_Expect_Food()
    {
        var state = new SimulationState();
        state.RegisterObjectObservation(
            "food.apple",
            "apple food",
            "M",
            salience: 0.88f,
            confidence: 0.82f,
            intensity: 1.0f,
            deliveredSpikes: 24);
        state.UpdateAttentionState(BiologicalAttentionRuntime.Default with
        {
            Visual = 0.48f,
            Memory = 0.24f,
            FocusConfidence = 0.72f,
            SensoryBias = new AttentionVector(0.58f, 0.12f, 0.12f, 0.18f)
        });
        state.UpdateEnvironmentalState(
            darkness: 0.05f,
            shelterNeed: 0.04f,
            anxiety: 0.08f,
            hunger: 0.82f,
            predatorThreat: 0.0f,
            inShelter: 0.0f,
            health: 0.94f,
            shelterSafety: 0.0f);
        state.UpdateLimbicState(MakeLimbic(tired: 0.08f, interoceptive: 0.78f, threat: 0.0f, aversive: 0.04f));

        DriveCognition(state, 8);

        using var document = SerializeDiagnostics(state);
        var predictive = GetObject(document.RootElement, "predictivePerception");

        Assert.True(GetBool(predictive, "active"));
        Assert.Equal("visual", GetString(predictive, "expectedChannel"));
        Assert.Contains("food", GetString(predictive, "expectedCue"), StringComparison.OrdinalIgnoreCase);
        Assert.True(GetSingle(predictive, "visualPrediction") > 0.22f);
        Assert.True(GetSingle(predictive, "hippocampalPatternCompletion") > 0.10f);
        Assert.True(GetSingle(predictive, "pfcTopDownExpectation") > 0.10f);
    }

    [Fact]
    public void Predictive_Sensory_Simulation_Expects_Threat_Sound_When_Predator_Is_Salient()
    {
        var state = new SimulationState();
        state.RegisterObjectObservation(
            "threat.bear",
            "bear predator",
            "M",
            salience: 0.96f,
            confidence: 0.90f,
            intensity: 1.0f,
            deliveredSpikes: 24);
        state.UpdateAttentionState(BiologicalAttentionRuntime.Default with
        {
            Auditory = 0.42f,
            Visual = 0.24f,
            FocusConfidence = 0.66f,
            SensoryBias = new AttentionVector(0.24f, 0.46f, 0.14f, 0.16f)
        });
        state.UpdateEnvironmentalState(
            darkness: 0.22f,
            shelterNeed: 0.18f,
            anxiety: 0.76f,
            hunger: 0.18f,
            predatorThreat: 0.88f,
            inShelter: 0.0f,
            health: 0.84f,
            shelterSafety: 0.0f);
        state.UpdateLimbicState(MakeLimbic(tired: 0.12f, interoceptive: 0.14f, threat: 0.86f, aversive: 0.72f));

        DriveCognition(state, 8);

        using var document = SerializeDiagnostics(state);
        var predictive = GetObject(document.RootElement, "predictivePerception");

        Assert.True(GetBool(predictive, "active"));
        Assert.Equal("auditory", GetString(predictive, "expectedChannel"));
        Assert.Contains("threat", GetString(predictive, "expectedCue"), StringComparison.OrdinalIgnoreCase);
        Assert.True(GetSingle(predictive, "auditoryPrediction") > 0.20f);
        Assert.True(GetSingle(predictive, "forwardModelConfidence") > 0.10f);
    }

    [Fact]
    public void Consciousness_Rhythm_Selects_Survival_Spotlight_And_Gates_Workspace()
    {
        var state = new SimulationState();
        state.UpdateAttentionState(new BiologicalAttentionRuntime(
            Visual: 0.42f,
            Auditory: 0.16f,
            Somatosensory: 0.12f,
            Interoceptive: 0.10f,
            Language: 0.05f,
            Memory: 0.05f,
            Motor: 0.10f,
            DominantChannel: "visual",
            FocusConfidence: 0.78f,
            Salience: 0.88f,
            ThalamicRelayGain: 0.72f,
            TrnInhibition: 0.18f,
            BasalForebrainGain: 0.76f,
            SensoryBias: new AttentionVector(0.58f, 0.16f, 0.12f, 0.14f),
            LastSwitchTick: 0,
            HoldTicksRemaining: 8));
        state.UpdateLimbicState(MakeLimbic(tired: 0.10f, interoceptive: 0.12f, threat: 0.86f, aversive: 0.76f));

        DriveCognition(state, 8);

        using var document = SerializeDiagnostics(state);
        var rhythm = GetObject(document.RootElement, "consciousnessRhythm");
        var workspace = GetObject(document.RootElement, "globalWorkspace");

        Assert.True(GetBool(rhythm, "active"));
        Assert.Equal("survival spotlight", GetString(rhythm, "currentMoment"));
        Assert.Equal("limbic", GetString(rhythm, "selectedCircuit"));
        Assert.True(GetSingle(rhythm, "globalMomentGate") > 0.18f);
        Assert.True(GetSingle(rhythm, "pulvinarSpotlight") > 0.35f);
        Assert.True(GetBool(workspace, "active"));
        Assert.True(GetSingle(workspace, "broadcastStrength") > 0.20f);
        Assert.Contains("won shared awareness", GetString(workspace, "whyThisWon"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Holding focus", GetString(workspace, "holdingState"), StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(GetString(workspace, "nextActionPreview")));
    }

    [Fact]
    public void Goal_Intent_Chooses_Shelter_When_Dark_Tired_And_Exposed()
    {
        var state = new SimulationState();
        AdvanceTicks(state, 4);
        state.UpdateEnvironmentalState(
            darkness: 0.92f,
            shelterNeed: 0.86f,
            anxiety: 0.28f,
            hunger: 0.12f,
            predatorThreat: 0.18f,
            inShelter: 0.0f,
            health: 0.82f,
            shelterSafety: 0.0f);
        state.UpdateLimbicState(MakeLimbic(tired: 0.82f, interoceptive: 0.18f, threat: 0.22f, aversive: 0.28f));
        state.ObserveCognitiveRuntime(state.Tick, dispatchedSpikes: 0, activePathwayCount: 0, rewardPredictionError: 0f, dominantPathway: null);

        using var document = SerializeDiagnostics(state);
        var goal = GetObject(document.RootElement, "goalIntent");
        var planning = GetObject(document.RootElement, "planningWorkspace");

        Assert.Equal("FindShelter", GetString(goal, "goalKey"));
        Assert.True(GetSingle(goal, "drive") > 0.55f);
        Assert.Equal("goal.FindShelter", GetString(planning, "selectedActionKey"));
    }

    [Fact]
    public void Intentional_Action_Loop_Commits_Goal_To_Premotor_Readiness()
    {
        var state = new SimulationState();
        AdvanceTicks(state, 4);
        state.UpdateEnvironmentalState(
            darkness: 0.92f,
            shelterNeed: 0.86f,
            anxiety: 0.28f,
            hunger: 0.12f,
            predatorThreat: 0.18f,
            inShelter: 0.0f,
            health: 0.82f,
            shelterSafety: 0.0f);
        state.UpdateLimbicState(MakeLimbic(tired: 0.82f, interoceptive: 0.18f, threat: 0.22f, aversive: 0.28f));
        state.ObserveCognitiveRuntime(state.Tick, dispatchedSpikes: 0, activePathwayCount: 0, rewardPredictionError: 0f, dominantPathway: null);

        using var document = SerializeDiagnostics(state);
        var intent = GetObject(document.RootElement, "intentionalActionLoop");
        var brainBehavior = GetObject(document.RootElement, "brainBehavior");
        var behaviorIntent = GetObject(brainBehavior, "intentionalActionLoop");

        Assert.True(GetBool(intent, "active"));
        Assert.Equal("FindShelter", GetString(intent, "goalKey"));
        Assert.Equal("goal.FindShelter", GetString(intent, "actionKey"));
        Assert.Equal("motor_seek_shelter", GetString(intent, "motorDirective"));
        Assert.True(GetSingle(intent, "commitment") > 0.25f);
        Assert.True(GetSingle(intent, "readiness") > 0.20f);
        Assert.True(GetSingle(intent, "basalGangliaCommit") > 0.20f);
        Assert.Contains("shelter", GetString(intent, "predictedOutcome"), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(GetString(intent, "intentionKey"), GetString(behaviorIntent, "intentionKey"));
    }

    [Fact]
    public void Pain_Protection_Loop_Guards_Body_And_Biases_Action()
    {
        var state = new SimulationState();
        AdvanceTicks(state, 4);
        state.UpdateEnvironmentalState(
            darkness: 0.12f,
            shelterNeed: 0.10f,
            anxiety: 0.18f,
            hunger: 0.06f,
            predatorThreat: 0.0f,
            inShelter: 0.30f,
            health: 0.46f,
            shelterSafety: 0.40f);
        state.UpdateBodyState(
            forwardVelocity: 0.35f,
            turnRateDeg: 18.0f,
            contactLevel: 0.96f,
            tactileFront: 0.96f,
            tactileLeft: 0.10f,
            tactileRight: 0.18f,
            tactileGround: 0.20f,
            painLevel: 0.92f,
            urgency: 0.72f,
            leftMotorDrive: 0.82f,
            rightMotorDrive: 0.76f);
        state.UpdateOutcomeState(
            satietyRelief: 0.0f,
            safetyRelief: 0.0f,
            painLevel: 0.82f,
            damageLevel: 0.56f,
            shelterComfort: 0.0f,
            progress: 0.0f,
            effortCost: 0.36f,
            novelty: 0.0f,
            socialApproval: 0.0f);
        state.UpdateLimbicState(MakeLimbic(tired: 0.16f, interoceptive: 0.18f, threat: 0.08f, aversive: 0.34f));
        state.ObserveCognitiveRuntime(state.Tick, dispatchedSpikes: 0, activePathwayCount: 0, rewardPredictionError: 0f, dominantPathway: null);

        using var document = SerializeDiagnostics(state);
        var protection = GetObject(document.RootElement, "painProtection");
        var goal = GetObject(document.RootElement, "goalIntent");
        var intent = GetObject(document.RootElement, "intentionalActionLoop");
        var workspace = GetObject(document.RootElement, "globalWorkspace");

        Assert.True(GetBool(protection, "active"));
        Assert.Contains(GetString(protection, "reflexState"), new[] { "withdraw", "guard", "immobilize" });
        Assert.True(GetSingle(protection, "nociception") > 0.70f);
        Assert.True(GetSingle(protection, "protectionDrive") > 0.55f);
        Assert.Equal("ProtectBody", GetString(goal, "goalKey"));
        Assert.Equal("ProtectBody", GetString(intent, "goalKey"));
        Assert.Contains(GetString(intent, "motorDirective"), new[] { "motor_withdraw_from_pain", "motor_guard_body", "motor_immobilize_protect", "motor_slow_protect" });
        Assert.Equal("ProtectBody", GetString(workspace, "boundGoalKey"));
    }

    [Fact]
    public void Goal_Intent_Chooses_Food_When_Hunger_Dominates()
    {
        var state = new SimulationState();
        AdvanceTicks(state, 4);
        state.UpdateEnvironmentalState(
            darkness: 0.12f,
            shelterNeed: 0.10f,
            anxiety: 0.08f,
            hunger: 0.94f,
            predatorThreat: 0.02f,
            inShelter: 0.20f,
            health: 0.92f,
            shelterSafety: 0.40f);
        state.UpdateLimbicState(MakeLimbic(tired: 0.16f, interoceptive: 0.90f, threat: 0.05f, aversive: 0.05f));
        state.ObserveCognitiveRuntime(state.Tick, dispatchedSpikes: 0, activePathwayCount: 0, rewardPredictionError: 0f, dominantPathway: null);

        using var document = SerializeDiagnostics(state);
        var goal = GetObject(document.RootElement, "goalIntent");

        Assert.Equal("FindFood", GetString(goal, "goalKey"));
        Assert.True(GetSingle(goal, "urgency") > 0.60f);
    }

    [Fact]
    public void Goal_Intent_Chooses_Threat_Avoidance_When_Danger_Dominates()
    {
        var state = new SimulationState();
        AdvanceTicks(state, 4);
        state.UpdateEnvironmentalState(
            darkness: 0.18f,
            shelterNeed: 0.20f,
            anxiety: 0.84f,
            hunger: 0.18f,
            predatorThreat: 0.96f,
            inShelter: 0.0f,
            health: 0.64f,
            shelterSafety: 0.0f);
        state.UpdateLimbicState(MakeLimbic(tired: 0.22f, interoceptive: 0.16f, threat: 0.92f, aversive: 0.80f));
        state.ObserveCognitiveRuntime(state.Tick, dispatchedSpikes: 0, activePathwayCount: 0, rewardPredictionError: 0f, dominantPathway: null);

        using var document = SerializeDiagnostics(state);
        var goal = GetObject(document.RootElement, "goalIntent");
        var brainBehavior = GetObject(document.RootElement, "brainBehavior");
        var behaviorGoal = GetObject(brainBehavior, "goalIntent");

        Assert.Equal("AvoidThreat", GetString(goal, "goalKey"));
        Assert.Equal("AvoidThreat", GetString(behaviorGoal, "goalKey"));
        Assert.True(GetSingle(goal, "basalGangliaGate") > 0.20f);
    }

    [Fact]
    public void Motivation_Arbitration_Suppresses_Hunger_And_Command_When_Threat_Is_Urgent()
    {
        var state = new SimulationState();
        var tokens = new[] { "turn", "left" };
        var grammar = EnglishLanguageLexicon.AnalyzeGrammar(tokens);

        state.AdvanceClockAndCreateTickSignal();
        state.ObserveLanguageIntent(grammar, tokens, state.Tick);
        state.UpdateEnvironmentalState(
            darkness: 0.18f,
            shelterNeed: 0.28f,
            anxiety: 0.90f,
            hunger: 0.92f,
            predatorThreat: 0.97f,
            inShelter: 0.0f,
            health: 0.58f,
            shelterSafety: 0.0f);
        state.UpdateLimbicState(MakeLimbic(tired: 0.20f, interoceptive: 0.88f, threat: 0.96f, aversive: 0.88f));
        DriveCognition(state, 6);

        using var document = SerializeDiagnostics(state);
        var goal = GetObject(document.RootElement, "goalIntent");
        var arbitration = GetObject(document.RootElement, "motivationArbitration");
        var brainBehavior = GetObject(document.RootElement, "brainBehavior");
        var behaviorArbitration = GetObject(brainBehavior, "motivationArbitration");

        Assert.Equal("AvoidThreat", GetString(goal, "goalKey"));
        Assert.Equal("AvoidThreat", GetString(arbitration, "winningGoalKey"));
        Assert.True(GetSingle(arbitration, "amygdalaThreatDrive") > GetSingle(arbitration, "hypothalamicHungerDrive") * 0.90f);
        Assert.True(GetSingle(arbitration, "pfcCommandDrive") > 0.20f);
        Assert.True(GetSingle(arbitration, "accConflict") > 0.15f);
        Assert.True(GetSingle(arbitration, "circuitEvidence") > 0.10f);
        Assert.Contains("winner=AvoidThreat", GetString(arbitration, "evidence"), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(GetString(arbitration, "winningGoalKey"), GetString(behaviorArbitration, "winningGoalKey"));
    }

    [Fact]
    public void Motivation_Arbitration_Allows_User_Command_When_Body_Is_Safe()
    {
        var state = new SimulationState();
        var tokens = new[] { "turn", "left" };
        var grammar = EnglishLanguageLexicon.AnalyzeGrammar(tokens);

        state.UpdateEnvironmentalState(
            darkness: 0.06f,
            shelterNeed: 0.04f,
            anxiety: 0.04f,
            hunger: 0.18f,
            predatorThreat: 0.0f,
            inShelter: 0.60f,
            health: 0.96f,
            shelterSafety: 0.72f);
        state.UpdateLimbicState(MakeLimbic(tired: 0.08f, interoceptive: 0.18f, threat: 0.01f, aversive: 0.02f));
        state.AdvanceClockAndCreateTickSignal();
        state.ObserveLanguageIntent(grammar, tokens, state.Tick);
        DriveCognition(state, 4);

        using var document = SerializeDiagnostics(state);
        var goal = GetObject(document.RootElement, "goalIntent");
        var arbitration = GetObject(document.RootElement, "motivationArbitration");
        var support = EnumerateObjects(GetObject(document.RootElement, "circuitAudit"), "functionSupport")
            .First(entry => GetString(entry, "functionKey") == "motivation_arbitration");

        Assert.Equal("FollowCommand", GetString(goal, "goalKey"));
        Assert.Equal("FollowCommand", GetString(arbitration, "winningGoalKey"));
        Assert.True(GetSingle(arbitration, "pfcCommandDrive") > GetSingle(arbitration, "amygdalaThreatDrive"));
        Assert.True(GetSingle(arbitration, "winningMargin") > 0.02f);
        Assert.True(GetSingle(support, "support") > 0.10f);
        Assert.Contains("basal-ganglia", GetString(support, "evidence"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Emotion_State_Tracks_Threat_As_Anxiety_And_Urgency()
    {
        var state = new SimulationState();
        state.UpdateEnvironmentalState(
            darkness: 0.40f,
            shelterNeed: 0.55f,
            anxiety: 0.88f,
            hunger: 0.12f,
            predatorThreat: 0.96f,
            inShelter: 0.0f,
            health: 0.58f,
            shelterSafety: 0.0f);
        state.UpdateLimbicState(MakeLimbic(tired: 0.18f, interoceptive: 0.16f, threat: 0.92f, aversive: 0.86f));
        DriveCognition(state, 18);

        using var document = SerializeDiagnostics(state);
        var emotion = GetObject(document.RootElement, "emotionState");
        var brainBehavior = GetObject(document.RootElement, "brainBehavior");
        var behaviorEmotion = GetObject(brainBehavior, "emotion");

        Assert.True(GetSingle(emotion, "anxiety") > 0.45f);
        Assert.True(GetSingle(emotion, "urgency") > 0.32f);
        Assert.True(GetSingle(emotion, "safety") < 0.45f);
        Assert.Equal(GetString(emotion, "dominantEmotion"), GetString(behaviorEmotion, "dominantEmotion"));
    }

    [Fact]
    public void Emotion_State_Tracks_Safety_As_Comfort_Confidence_And_Curiosity()
    {
        var state = new SimulationState();
        state.UpdateEnvironmentalState(
            darkness: 0.05f,
            shelterNeed: 0.04f,
            anxiety: 0.02f,
            hunger: 0.06f,
            predatorThreat: 0.0f,
            inShelter: 0.92f,
            health: 0.96f,
            shelterSafety: 0.94f);
        state.UpdateAttentionState(new BiologicalAttentionRuntime(
            Visual: 0.28f,
            Auditory: 0.07f,
            Somatosensory: 0.08f,
            Interoceptive: 0.12f,
            Language: 0.04f,
            Memory: 0.34f,
            Motor: 0.07f,
            DominantChannel: "memory",
            FocusConfidence: 0.62f,
            Salience: 0.32f,
            ThalamicRelayGain: 0.50f,
            TrnInhibition: 0.28f,
            BasalForebrainGain: 0.64f,
            SensoryBias: new AttentionVector(0.34f, 0.10f, 0.16f, 0.40f),
            LastSwitchTick: 0,
            HoldTicksRemaining: 0));
        state.UpdateLimbicState(MakeLimbic(tired: 0.08f, interoceptive: 0.10f, threat: 0.04f, aversive: 0.04f, expectedReward: 0.72f, observedReward: 0.76f));
        DriveCognition(state, 18);

        using var document = SerializeDiagnostics(state);
        var emotion = GetObject(document.RootElement, "emotionState");

        Assert.True(GetSingle(emotion, "safety") > 0.50f);
        Assert.True(GetSingle(emotion, "comfort") > 0.42f);
        Assert.True(GetSingle(emotion, "confidence") > 0.36f);
        Assert.True(GetSingle(emotion, "curiosity") > 0.30f);
    }

    [Fact]
    public void Emotion_State_Tracks_Body_Blockage_As_Frustration()
    {
        var state = new SimulationState();
        state.UpdateEnvironmentalState(
            darkness: 0.10f,
            shelterNeed: 0.10f,
            anxiety: 0.12f,
            hunger: 0.10f,
            predatorThreat: 0.0f,
            inShelter: 0.30f,
            health: 0.88f,
            shelterSafety: 0.35f);
        state.UpdateBodyState(
            forwardVelocity: 0.0f,
            turnRateDeg: 12.0f,
            contactLevel: 0.92f,
            leftMotorDrive: 0.95f,
            rightMotorDrive: 0.10f);
        state.UpdateLimbicState(MakeLimbic(tired: 0.16f, interoceptive: 0.14f, threat: 0.10f, aversive: 0.44f, expectedReward: 0.18f, observedReward: 0.06f));
        DriveCognition(state, 18);

        using var document = SerializeDiagnostics(state);
        var emotion = GetObject(document.RootElement, "emotionState");

        Assert.True(GetSingle(emotion, "frustration") > 0.42f);
        Assert.True(GetSingle(emotion, "valence") < 0.0f);
    }

    [Fact]
    public void Action_Memory_Learns_Biological_Goal_Outcomes()
    {
        var state = new SimulationState();
        state.UpdateEnvironmentalState(
            darkness: 0.88f,
            shelterNeed: 0.86f,
            anxiety: 0.35f,
            hunger: 0.12f,
            predatorThreat: 0.18f,
            inShelter: 0.0f,
            health: 0.86f,
            shelterSafety: 0.0f);
        state.UpdateLimbicState(MakeLimbic(tired: 0.84f, interoceptive: 0.12f, threat: 0.20f, aversive: 0.30f));
        DriveCognition(state, 6);

        using var document = SerializeDiagnostics(state);
        var actionMemory = GetObject(document.RootElement, "actionMemory");
        var first = GetFirstArrayObject(actionMemory, "top");

        Assert.True(GetInt(actionMemory, "count") > 0);
        Assert.Equal("FindShelter", GetString(first, "goalKey"));
        Assert.True(GetSingle(first, "confidence") > 0.08f);
    }

    [Fact]
    public void World_Learning_Map_Classifies_Object_Memory_Biologically()
    {
        var state = new SimulationState();
        state.RegisterObjectObservation(
            objectId: "berry_patch_1",
            label: "food berry patch",
            hemisphere: "L",
            salience: 0.82f,
            confidence: 0.78f,
            intensity: 1.0f,
            deliveredSpikes: 24);

        using var document = SerializeDiagnostics(state);
        var worldMap = GetObject(document.RootElement, "worldLearningMap");
        var first = GetFirstArrayObject(worldMap, "top");

        Assert.True(GetInt(worldMap, "count") > 0);
        Assert.Equal("food", GetString(first, "category"));
        Assert.True(GetSingle(first, "food") > 0.45f);
    }

    [Fact]
    public void Persistent_Percepts_Preserve_Recently_Seen_Objects()
    {
        var state = new SimulationState();
        state.RegisterObjectObservation(
            objectId: "berry_patch_permanent",
            label: "food berry patch",
            hemisphere: "L",
            salience: 0.84f,
            confidence: 0.80f,
            intensity: 1.0f,
            deliveredSpikes: 24);
        AdvanceTicks(state, 30);

        using var document = SerializeDiagnostics(state);
        var percepts = GetObject(document.RootElement, "persistentPercepts");

        Assert.True(GetInt(percepts, "count") > 0);
        Assert.Equal("food", GetString(percepts, "activeCategory"));
        Assert.Contains("berry", GetString(percepts, "activeLabel"), StringComparison.OrdinalIgnoreCase);
        Assert.True(GetSingle(percepts, "objectPermanence") > 0.20f);
    }

    [Fact]
    public void Persistent_Percepts_Bias_Food_Intention_Target()
    {
        var state = new SimulationState();
        state.RegisterObjectObservation(
            objectId: "berry_patch_target",
            label: "food berry patch",
            hemisphere: "R",
            salience: 0.88f,
            confidence: 0.84f,
            intensity: 1.0f,
            deliveredSpikes: 28);
        state.UpdateEnvironmentalState(
            darkness: 0.10f,
            shelterNeed: 0.08f,
            anxiety: 0.06f,
            hunger: 0.96f,
            predatorThreat: 0.02f,
            inShelter: 0.20f,
            health: 0.92f,
            shelterSafety: 0.38f);
        state.UpdateLimbicState(MakeLimbic(tired: 0.12f, interoceptive: 0.92f, threat: 0.04f, aversive: 0.05f));
        DriveCognition(state, 4);

        using var document = SerializeDiagnostics(state);
        var intent = GetObject(document.RootElement, "intentionalActionLoop");

        Assert.Equal("FindFood", GetString(intent, "goalKey"));
        Assert.Contains("remembered food", GetString(intent, "target"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("berry_patch_target", GetString(intent, "target"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Planning_Prefers_Executable_Goal_Action_Over_Raw_Connectome_Pathway()
    {
        var state = new SimulationState();
        var exported = state.ExportNetworkState();
        exported.WorldModel = WorldModelRuntime.Default with
        {
            Enabled = true,
            LearnedTransitions = 1
        };
        exported.WorldModelTransitions.Add(new WorldModelTransition(
            ActionKey: "ArcuateFasciculus->BrocaBa44Ba45|GLUTAMATE|fb=0",
            SourceStructure: StructureId.ArcuateFasciculus,
            TargetStructure: StructureId.BrocaBa44Ba45,
            Neurotransmitter: NTEnum.GLUTAMATE,
            IsFeedback: false,
            ExpectedDispatchDelta: 4.0f,
            ExpectedPathwayDelta: 4.0f,
            ExpectedRewardDelta: 4.0f,
            ExpectedSleepPressureDelta: -0.60f,
            PredictionError: 0.01f,
            Samples: 2000,
            LastTick: 1));
        Assert.True(state.TryImportNetworkState(exported, out var error), error);

        state.RegisterObjectObservation(
            objectId: "shelter_exec_target",
            label: "shelter",
            hemisphere: "M",
            salience: 0.90f,
            confidence: 0.88f,
            intensity: 1.0f,
            deliveredSpikes: 32);
        state.UpdateEnvironmentalState(
            darkness: 0.82f,
            shelterNeed: 0.90f,
            anxiety: 0.38f,
            hunger: 0.18f,
            predatorThreat: 0.05f,
            inShelter: 0.0f,
            health: 0.88f,
            shelterSafety: 0.0f);
        state.UpdateLimbicState(MakeLimbic(tired: 0.22f, interoceptive: 0.18f, threat: 0.18f, aversive: 0.36f));
        DriveCognition(state, 6);

        using var document = SerializeDiagnostics(state);
        var planning = GetObject(document.RootElement, "planningWorkspace");
        var intent = GetObject(document.RootElement, "intentionalActionLoop");
        var candidates = EnumerateObjects(planning, "candidateActions").ToArray();

        Assert.Contains(candidates, candidate => GetString(candidate, "actionKey").Contains("->", StringComparison.Ordinal));
        Assert.Equal("goal.FindShelter", GetString(planning, "selectedActionKey"));
        Assert.Equal("goal.FindShelter", GetString(candidates[0], "actionKey"));
        Assert.Equal("goal.FindShelter", GetString(intent, "actionKey"));
        Assert.Equal("motor_seek_shelter", GetString(intent, "motorDirective"));
    }

    [Fact]
    public void Prefrontal_Working_Memory_Holds_Intent_Across_Brief_Sensory_Gap()
    {
        var state = new SimulationState();
        state.RegisterObjectObservation(
            objectId: "berry_patch_hold",
            label: "food berry patch",
            hemisphere: "L",
            salience: 0.90f,
            confidence: 0.86f,
            intensity: 1.0f,
            deliveredSpikes: 34);
        state.UpdateEnvironmentalState(
            darkness: 0.08f,
            shelterNeed: 0.06f,
            anxiety: 0.04f,
            hunger: 0.96f,
            predatorThreat: 0.01f,
            inShelter: 0.20f,
            health: 0.92f,
            shelterSafety: 0.38f);
        state.UpdateAttentionState(BiologicalAttentionRuntime.Default with
        {
            Memory = 0.74f,
            Interoceptive = 0.68f,
            FocusConfidence = 0.84f,
            Salience = 0.76f,
            DominantChannel = "memory"
        });
        state.UpdateLimbicState(MakeLimbic(tired: 0.10f, interoceptive: 0.92f, threat: 0.02f, aversive: 0.04f));
        DriveCognition(state, 6);

        state.UpdateEnvironmentalState(
            darkness: 0.08f,
            shelterNeed: 0.05f,
            anxiety: 0.04f,
            hunger: 0.05f,
            predatorThreat: 0.0f,
            inShelter: 0.20f,
            health: 0.92f,
            shelterSafety: 0.38f);
        state.UpdateLimbicState(MakeLimbic(tired: 0.08f, interoceptive: 0.05f, threat: 0.0f, aversive: 0.02f));
        DriveCognition(state, 2);

        using var document = SerializeDiagnostics(state);
        var prefrontal = GetObject(document.RootElement, "prefrontalWorkingMemory");
        var intent = GetObject(document.RootElement, "intentionalActionLoop");

        Assert.True(GetBool(prefrontal, "intentHoldActive"));
        Assert.Equal("FindFood", GetString(prefrontal, "heldGoalKey"));
        Assert.Equal("goal.FindFood", GetString(prefrontal, "heldActionKey"));
        Assert.True(GetSingle(prefrontal, "intentHoldStrength") > 0.20f);
        Assert.Equal("goal.FindFood", GetString(intent, "actionKey"));
        Assert.Contains("hold=", GetString(prefrontal, "evidence"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Prefrontal_Working_Memory_Releases_Held_Intent_For_Urgent_Threat()
    {
        var state = new SimulationState();
        state.RegisterObjectObservation(
            objectId: "berry_patch_release",
            label: "food berry patch",
            hemisphere: "L",
            salience: 0.88f,
            confidence: 0.84f,
            intensity: 1.0f,
            deliveredSpikes: 30);
        state.UpdateEnvironmentalState(
            darkness: 0.08f,
            shelterNeed: 0.06f,
            anxiety: 0.04f,
            hunger: 0.96f,
            predatorThreat: 0.01f,
            inShelter: 0.20f,
            health: 0.92f,
            shelterSafety: 0.38f);
        state.UpdateAttentionState(BiologicalAttentionRuntime.Default with
        {
            Memory = 0.70f,
            Interoceptive = 0.66f,
            Visual = 0.62f,
            FocusConfidence = 0.82f,
            Salience = 0.78f,
            DominantChannel = "memory"
        });
        state.UpdateLimbicState(MakeLimbic(tired: 0.10f, interoceptive: 0.92f, threat: 0.02f, aversive: 0.04f));
        DriveCognition(state, 6);

        state.UpdateEnvironmentalState(
            darkness: 0.18f,
            shelterNeed: 0.20f,
            anxiety: 0.82f,
            hunger: 0.08f,
            predatorThreat: 0.96f,
            inShelter: 0.0f,
            health: 0.72f,
            shelterSafety: 0.0f);
        state.UpdateLimbicState(MakeLimbic(tired: 0.10f, interoceptive: 0.08f, threat: 0.96f, aversive: 0.86f));
        DriveCognition(state, 3);

        using var document = SerializeDiagnostics(state);
        var prefrontal = GetObject(document.RootElement, "prefrontalWorkingMemory");
        var intent = GetObject(document.RootElement, "intentionalActionLoop");

        Assert.Equal("AvoidThreat", GetString(prefrontal, "selectedGoal"));
        Assert.Equal("goal.AvoidThreat", GetString(intent, "actionKey"));
        Assert.True(GetSingle(prefrontal, "interferenceGate") > 0.20f);
    }

    [Fact]
    public void Embodied_Attention_Spotlight_Binds_Need_Body_And_Remembered_Object()
    {
        var state = new SimulationState();
        state.RegisterObjectObservation(
            objectId: "berry_patch_spotlight",
            label: "food berry patch",
            hemisphere: "L",
            salience: 0.90f,
            confidence: 0.86f,
            intensity: 1.0f,
            deliveredSpikes: 32);
        state.UpdateEnvironmentalState(
            darkness: 0.10f,
            shelterNeed: 0.08f,
            anxiety: 0.06f,
            hunger: 0.96f,
            predatorThreat: 0.02f,
            inShelter: 0.20f,
            health: 0.92f,
            shelterSafety: 0.38f);
        state.UpdateBodyState(
            forwardVelocity: 0.25f,
            turnRateDeg: 4f,
            contactLevel: 0.05f,
            leftMotorDrive: 0.30f,
            rightMotorDrive: 0.30f);
        state.UpdateLimbicState(MakeLimbic(tired: 0.12f, interoceptive: 0.92f, threat: 0.04f, aversive: 0.05f));
        DriveCognition(state, 4);

        using var document = SerializeDiagnostics(state);
        var spotlight = GetObject(document.RootElement, "embodiedAttentionSpotlight");
        var brainBehavior = GetObject(document.RootElement, "brainBehavior");
        var behaviorSpotlight = GetObject(brainBehavior, "embodiedAttentionSpotlight");

        Assert.True(GetBool(spotlight, "active"));
        Assert.Equal("food", GetString(spotlight, "dominantNeed"));
        Assert.Equal("food", GetString(spotlight, "focusCategory"));
        Assert.Equal("berry_patch_spotlight", GetString(spotlight, "targetObjectId"));
        Assert.True(GetSingle(spotlight, "objectBinding") > 0.20f);
        Assert.True(GetSingle(spotlight, "needBinding") > 0.25f);
        Assert.True(GetSingle(spotlight, "circuitSupport") > 0.16f);
        Assert.True(GetSingle(spotlight, "objectCircuitEvidence") > 0.15f);
        Assert.True(GetSingle(spotlight, "needCircuitEvidence") > 0.15f);
        Assert.Contains("circuits=", GetString(spotlight, "evidence"), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(GetString(spotlight, "focusKey"), GetString(behaviorSpotlight, "focusKey"));
    }

    [Fact]
    public void Action_Completion_Feedback_Reinforces_Completed_Intention()
    {
        var state = new SimulationState();
        state.RegisterObjectObservation(
            objectId: "berry_patch_completion",
            label: "food berry patch",
            hemisphere: "R",
            salience: 0.88f,
            confidence: 0.84f,
            intensity: 1.0f,
            deliveredSpikes: 30);
        state.UpdateEnvironmentalState(
            darkness: 0.08f,
            shelterNeed: 0.06f,
            anxiety: 0.04f,
            hunger: 0.94f,
            predatorThreat: 0.01f,
            inShelter: 0.20f,
            health: 0.94f,
            shelterSafety: 0.34f);
        state.UpdateBodyState(
            forwardVelocity: 1.25f,
            turnRateDeg: 12.0f,
            contactLevel: 0.02f,
            leftMotorDrive: 0.82f,
            rightMotorDrive: 0.80f);
        state.UpdateOutcomeState(
            satietyRelief: 0.72f,
            safetyRelief: 0.10f,
            painLevel: 0.0f,
            damageLevel: 0.0f,
            shelterComfort: 0.08f,
            progress: 0.78f,
            effortCost: 0.10f,
            novelty: 0.18f,
            socialApproval: 0.0f);
        state.UpdateLimbicState(MakeLimbic(tired: 0.10f, interoceptive: 0.92f, threat: 0.02f, aversive: 0.04f));
        DriveCognition(state, 8);

        using var document = SerializeDiagnostics(state);
        var feedback = GetObject(document.RootElement, "actionCompletionFeedback");
        var actionMemory = GetObject(document.RootElement, "actionMemory");
        var topAction = GetFirstArrayObject(actionMemory, "top");

        Assert.True(GetBool(feedback, "active"));
        Assert.Equal("FindFood", GetString(feedback, "goalKey"));
        Assert.Contains(GetString(feedback, "status"), new[] { "completed", "progressing" });
        Assert.True(GetSingle(feedback, "completion") > GetSingle(feedback, "stall"));
        Assert.True(GetSingle(feedback, "dopamineTeachingBias") > 0.0f);
        Assert.True(GetSingle(topAction, "completionEma") > 0.30f);
        Assert.Contains("PFC expected", GetString(feedback, "evidence"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Action_Completion_Feedback_Flags_Stalled_Intention()
    {
        var state = new SimulationState();
        state.RegisterObjectObservation(
            objectId: "berry_patch_stall",
            label: "food berry patch",
            hemisphere: "L",
            salience: 0.88f,
            confidence: 0.82f,
            intensity: 1.0f,
            deliveredSpikes: 30);
        state.UpdateEnvironmentalState(
            darkness: 0.08f,
            shelterNeed: 0.06f,
            anxiety: 0.08f,
            hunger: 0.96f,
            predatorThreat: 0.02f,
            inShelter: 0.12f,
            health: 0.92f,
            shelterSafety: 0.20f);
        state.UpdateBodyState(
            forwardVelocity: 0.0f,
            turnRateDeg: 0.0f,
            contactLevel: 0.04f,
            leftMotorDrive: 0.76f,
            rightMotorDrive: 0.76f);
        state.UpdateOutcomeState(
            satietyRelief: 0.0f,
            safetyRelief: 0.0f,
            painLevel: 0.0f,
            damageLevel: 0.0f,
            shelterComfort: 0.0f,
            progress: 0.0f,
            effortCost: 0.16f,
            novelty: 0.0f,
            socialApproval: 0.0f);
        state.UpdateLimbicState(MakeLimbic(tired: 0.10f, interoceptive: 0.94f, threat: 0.03f, aversive: 0.06f));
        DriveCognition(state, 8);

        using var document = SerializeDiagnostics(state);
        var feedback = GetObject(document.RootElement, "actionCompletionFeedback");
        var actionMemory = GetObject(document.RootElement, "actionMemory");
        var topAction = GetFirstArrayObject(actionMemory, "top");

        Assert.True(GetBool(feedback, "active"));
        Assert.Equal("stalled", GetString(feedback, "status"));
        Assert.True(GetSingle(feedback, "stall") > 0.40f);
        Assert.True(GetSingle(feedback, "accError") > 0.20f);
        Assert.True(GetSingle(feedback, "dopamineTeachingBias") < 0.0f);
        Assert.Equal("stalled", GetString(topAction, "lastCompletionStatus"));
    }

    [Fact]
    public void Self_Monitoring_Loop_Flags_Stalled_Intention()
    {
        var state = new SimulationState();
        state.RegisterObjectObservation(
            objectId: "berry_patch_self_monitor_stall",
            label: "food berry patch",
            hemisphere: "L",
            salience: 0.90f,
            confidence: 0.84f,
            intensity: 1.0f,
            deliveredSpikes: 34);
        state.UpdateEnvironmentalState(
            darkness: 0.08f,
            shelterNeed: 0.05f,
            anxiety: 0.08f,
            hunger: 0.96f,
            predatorThreat: 0.01f,
            inShelter: 0.14f,
            health: 0.94f,
            shelterSafety: 0.22f);
        state.UpdateBodyState(
            forwardVelocity: 0.0f,
            turnRateDeg: 0.0f,
            contactLevel: 0.02f,
            leftMotorDrive: 0.80f,
            rightMotorDrive: 0.80f);
        state.UpdateOutcomeState(
            satietyRelief: 0.0f,
            safetyRelief: 0.0f,
            painLevel: 0.0f,
            damageLevel: 0.0f,
            shelterComfort: 0.0f,
            progress: 0.0f,
            effortCost: 0.18f,
            novelty: 0.0f,
            socialApproval: 0.0f);
        state.UpdateLimbicState(MakeLimbic(tired: 0.10f, interoceptive: 0.94f, threat: 0.02f, aversive: 0.05f));
        DriveCognition(state, 8);

        using var document = SerializeDiagnostics(state);
        var monitor = GetObject(document.RootElement, "selfMonitoringLoop");
        var brainBehavior = GetObject(document.RootElement, "brainBehavior");
        var behaviorMonitor = GetObject(brainBehavior, "selfMonitoringLoop");

        Assert.True(GetBool(monitor, "active"));
        Assert.Equal("stalled", GetString(monitor, "monitorState"));
        Assert.Equal("reorient_or_switch_strategy", GetString(monitor, "recommendedAdjustment"));
        Assert.True(GetSingle(monitor, "stallScore") > GetSingle(monitor, "workingScore"));
        Assert.True(GetSingle(monitor, "accErrorSignal") > 0.08f);
        Assert.True(GetSingle(monitor, "circuitEvidence") > 0.10f);
        Assert.Contains("ACC=", GetString(monitor, "evidence"), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(GetString(monitor, "monitorState"), GetString(behaviorMonitor, "monitorState"));
    }

    [Fact]
    public void Self_Monitoring_Loop_Flags_Unsafe_Body_State()
    {
        var state = new SimulationState();
        state.UpdateEnvironmentalState(
            darkness: 0.30f,
            shelterNeed: 0.44f,
            anxiety: 0.82f,
            hunger: 0.18f,
            predatorThreat: 0.82f,
            inShelter: 0.0f,
            health: 0.62f,
            shelterSafety: 0.0f);
        state.UpdateBodyState(
            forwardVelocity: 0.12f,
            turnRateDeg: 18.0f,
            contactLevel: 0.86f,
            leftMotorDrive: 0.90f,
            rightMotorDrive: 0.32f);
        state.UpdateOutcomeState(
            satietyRelief: 0.0f,
            safetyRelief: 0.0f,
            painLevel: 0.78f,
            damageLevel: 0.42f,
            shelterComfort: 0.0f,
            progress: 0.0f,
            effortCost: 0.30f,
            novelty: 0.05f,
            socialApproval: 0.0f);
        state.UpdateLimbicState(MakeLimbic(tired: 0.18f, interoceptive: 0.20f, threat: 0.86f, aversive: 0.82f));
        DriveCognition(state, 8);

        using var document = SerializeDiagnostics(state);
        var monitor = GetObject(document.RootElement, "selfMonitoringLoop");
        var pain = GetObject(document.RootElement, "painProtection");
        var functions = EnumerateObjects(GetObject(document.RootElement, "circuitAudit"), "functionSupport").ToArray();
        var support = functions.First(entry => GetString(entry, "functionKey") == "self_monitoring_loop");

        Assert.True(GetBool(monitor, "active"));
        Assert.Equal("unsafe", GetString(monitor, "monitorState"));
        Assert.True(GetString(monitor, "recommendedAdjustment").Length > 0);
        Assert.True(GetSingle(monitor, "unsafeScore") > 0.15f);
        Assert.True(GetSingle(monitor, "insulaBodyAlarm") > 0.15f);
        Assert.True(GetSingle(pain, "protectionDrive") > 0.15f);
        Assert.True(GetSingle(support, "support") > 0.10f);
        Assert.Contains("insula", GetString(support, "evidence"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sleep_Dream_Consolidation_Replays_Action_And_World_Map_Memory()
    {
        var state = new SimulationState();
        state.RegisterObjectObservation(
            objectId: "safe_den_1",
            label: "shelter den safe",
            hemisphere: "M",
            salience: 0.86f,
            confidence: 0.82f,
            intensity: 1.0f,
            deliveredSpikes: 32);
        state.UpdateEnvironmentalState(
            darkness: 0.92f,
            shelterNeed: 0.88f,
            anxiety: 0.26f,
            hunger: 0.10f,
            predatorThreat: 0.10f,
            inShelter: 0.0f,
            health: 0.90f,
            shelterSafety: 0.0f);
        state.UpdateLimbicState(MakeLimbic(tired: 0.86f, interoceptive: 0.12f, threat: 0.16f, aversive: 0.20f));
        DriveCognition(state, 6);

        var exported = state.ExportNetworkState();
        exported.SleepMemory = exported.SleepMemory with
        {
            IsSleeping = true,
            SleepTicks = 32,
            WakeTicks = 0
        };
        Assert.True(state.TryImportNetworkState(exported, out var error), error);
        state.AdvanceClockAndCreateTickSignal();
        state.ObserveCognitiveRuntime(state.Tick, dispatchedSpikes: 0, activePathwayCount: 0, rewardPredictionError: 0f, dominantPathway: null);

        using var document = SerializeDiagnostics(state);
        var dream = GetObject(document.RootElement, "dreamConsolidation");

        Assert.True(GetInt(dream, "protectedMapCount") > 0);
        Assert.True(GetInt(dream, "actionMemoryReplays") > 0);
        Assert.True(GetInt(dream, "worldMapReplays") > 0);
        Assert.True(GetSingle(dream, "meanActionSuccess") > 0.0f);
        Assert.True(GetInt(dream, "offlineSimulationCount") > 0);
        Assert.Contains("hippocampus", GetString(dream, "simulatedScene"), StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(GetString(dream, "simulatedActionKey")));
        Assert.True(GetInt(dream, "autobiographicalReplays") > 0);
        Assert.True(GetInt(dream, "semanticReplays") > 0);
        Assert.Contains("Sleep replay protected", GetString(dream, "consolidationSummary"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("action=", GetString(dream, "consolidatedActionValue"), StringComparison.OrdinalIgnoreCase);
        Assert.True(GetSingle(dream, "hippocampalReplay") > 0.10f);
        Assert.True(GetSingle(dream, "pfcCounterfactual") > 0.10f);
        Assert.True(GetSingle(dream, "cerebellarMotorReplay") >= 0.0f);
        Assert.True(GetSingle(dream, "autobiographicalContinuityGain") > 0.0f);
        Assert.True(GetSingle(dream, "semanticStabilization") > 0.0f);
        Assert.True(GetSingle(dream, "actionValueStabilization") > 0.0f);
    }

    [Fact]
    public void Brain_Narration_Reports_Biological_Intent_Evidence_And_Confidence()
    {
        var state = new SimulationState();
        state.RegisterObjectObservation(
            objectId: "safe_den_2",
            label: "shelter den safe",
            hemisphere: "M",
            salience: 0.82f,
            confidence: 0.80f,
            intensity: 1.0f,
            deliveredSpikes: 20);
        state.UpdateEnvironmentalState(
            darkness: 0.88f,
            shelterNeed: 0.86f,
            anxiety: 0.32f,
            hunger: 0.10f,
            predatorThreat: 0.12f,
            inShelter: 0.0f,
            health: 0.86f,
            shelterSafety: 0.0f);
        state.UpdateLimbicState(MakeLimbic(tired: 0.82f, interoceptive: 0.12f, threat: 0.14f, aversive: 0.24f));
        DriveCognition(state, 8);

        using var document = SerializeDiagnostics(state);
        var narration = GetObject(document.RootElement, "brainNarration");

        Assert.Contains("shelter", GetString(narration, "utterance"), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("FindShelter", GetString(narration, "intent"));
        Assert.Contains(GetString(narration, "need"), new[] { "energy", "shelter" });
        Assert.Contains("goal=FindShelter", GetString(narration, "evidence"), StringComparison.OrdinalIgnoreCase);
        Assert.True(GetSingle(narration, "confidence") > 0.25f);
        Assert.True(GetSingle(narration, "narrativePriority") > 0.45f);
    }

    [Fact]
    public void Brain_Narration_Keeps_Quiet_Monitoring_Internal()
    {
        var state = new SimulationState();

        DriveCognition(state, 8);

        using var document = SerializeDiagnostics(state);
        var narration = GetObject(document.RootElement, "brainNarration");

        Assert.False(GetBool(narration, "spokenEligible"));
        Assert.True(GetSingle(narration, "speechSuppression") >= GetSingle(narration, "speechReleaseGate"));
        Assert.True(GetSingle(narration, "narrativePriority") < 0.45f);
    }

    [Fact]
    public void Speech_Intention_Keeps_Quiet_Monitoring_Internal()
    {
        var state = new SimulationState();

        DriveCognition(state, 8);

        using var document = SerializeDiagnostics(state);
        var speech = GetObject(document.RootElement, "speechIntention");

        Assert.False(GetBool(speech, "spokenEligible"));
        Assert.Contains(GetString(speech, "mode"), new[] { "internal", "suppressed" });
        Assert.True(GetSingle(speech, "suppression") >= GetSingle(speech, "releaseGate"));
        Assert.Contains("internal", GetString(speech, "reason"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Brain_Narration_Releases_Speech_For_Urgent_Threat()
    {
        var state = new SimulationState();
        state.UpdateEnvironmentalState(
            darkness: 0.30f,
            shelterNeed: 0.22f,
            anxiety: 0.88f,
            hunger: 0.08f,
            predatorThreat: 0.94f,
            inShelter: 0.0f,
            health: 0.82f,
            shelterSafety: 0.0f);
        state.UpdateLimbicState(MakeLimbic(tired: 0.08f, interoceptive: 0.10f, threat: 0.92f, aversive: 0.86f));

        DriveCognition(state, 8);

        using var document = SerializeDiagnostics(state);
        var narration = GetObject(document.RootElement, "brainNarration");

        Assert.True(GetBool(narration, "spokenEligible"));
        Assert.Contains("danger", GetString(narration, "utterance"), StringComparison.OrdinalIgnoreCase);
        Assert.True(GetSingle(narration, "speechReleaseGate") > 0.32f);
        Assert.True(GetSingle(narration, "narrativePriority") > 0.60f);
    }

    [Fact]
    public void Speech_Intention_Opens_For_Urgent_Threat()
    {
        var state = new SimulationState();
        state.UpdateEnvironmentalState(
            darkness: 0.30f,
            shelterNeed: 0.22f,
            anxiety: 0.88f,
            hunger: 0.08f,
            predatorThreat: 0.94f,
            inShelter: 0.0f,
            health: 0.82f,
            shelterSafety: 0.0f);
        state.UpdateLimbicState(MakeLimbic(tired: 0.08f, interoceptive: 0.10f, threat: 0.92f, aversive: 0.86f));

        DriveCognition(state, 8);

        using var document = SerializeDiagnostics(state);
        var speech = GetObject(document.RootElement, "speechIntention");

        Assert.True(GetBool(speech, "spokenEligible"));
        Assert.Equal("speakable", GetString(speech, "mode"));
        Assert.Contains("Broca", GetString(speech, "reason"), StringComparison.OrdinalIgnoreCase);
        Assert.True(GetSingle(speech, "priority") > 0.60f);
        Assert.True(GetSingle(speech, "confidence") > 0.35f);
    }

    [Fact]
    public void Body_Schema_Tracks_Proprioception_Fatigue_And_Balance()
    {
        var state = new SimulationState();
        state.UpdateEnvironmentalState(
            darkness: 0.78f,
            shelterNeed: 0.72f,
            anxiety: 0.20f,
            hunger: 0.42f,
            predatorThreat: 0.08f,
            inShelter: 0.0f,
            health: 0.70f,
            shelterSafety: 0.0f);
        state.UpdateBodyState(
            forwardVelocity: 0.25f,
            turnRateDeg: 180.0f,
            contactLevel: 0.82f,
            leftMotorDrive: 1.0f,
            rightMotorDrive: 0.12f);
        state.UpdateLimbicState(MakeLimbic(tired: 0.74f, interoceptive: 0.36f, threat: 0.12f, aversive: 0.30f));
        DriveCognition(state, 8);

        using var document = SerializeDiagnostics(state);
        var schema = GetObject(document.RootElement, "bodySchema");
        var presence = GetObject(document.RootElement, "bodyPresence");

        Assert.True(GetSingle(schema, "motorConflict") > 0.55f);
        Assert.True(GetSingle(schema, "fatigue") > 0.30f);
        Assert.True(GetSingle(schema, "damage") > 0.25f);
        Assert.True(GetSingle(schema, "postureStability") < 0.70f);
        Assert.True(GetSingle(schema, "proprioceptiveConfidence") > 0.20f);
        Assert.Contains("Embodied presence", GetString(presence, "feltSummary"), StringComparison.OrdinalIgnoreCase);
        Assert.True(GetSingle(presence, "bodyMap") > 0.10f);
        Assert.True(GetSingle(presence, "interoceptiveAnchor") > 0.10f);
        Assert.True(GetSingle(presence, "vestibularConfidence") > 0.10f);
    }

    [Fact]
    public void Interoceptive_Core_Binds_Hunger_Fatigue_And_Body_Feeling()
    {
        var state = new SimulationState();
        state.UpdateEnvironmentalState(
            darkness: 0.18f,
            shelterNeed: 0.16f,
            anxiety: 0.12f,
            hunger: 0.88f,
            predatorThreat: 0.02f,
            inShelter: 0.20f,
            health: 0.76f,
            shelterSafety: 0.25f);
        state.UpdateBodyState(
            forwardVelocity: 0.18f,
            turnRateDeg: 16.0f,
            contactLevel: 0.18f,
            leftMotorDrive: 0.36f,
            rightMotorDrive: 0.32f);
        state.UpdateAttentionState(BiologicalAttentionRuntime.Default with
        {
            Interoceptive = 0.62f,
            DominantChannel = "interoceptive",
            FocusConfidence = 0.70f,
            Salience = 0.76f
        });
        state.UpdateLimbicState(MakeLimbic(tired: 0.42f, interoceptive: 0.74f, threat: 0.04f, aversive: 0.10f));

        DriveCognition(state, 8);

        using var document = SerializeDiagnostics(state);
        var core = GetObject(document.RootElement, "interoceptiveCore");
        var self = GetObject(document.RootElement, "narrativeSelfModel");
        var goal = GetObject(document.RootElement, "goalIntent");

        Assert.True(GetBool(core, "active"));
        Assert.Equal("food", GetString(core, "dominantNeed"));
        Assert.Equal("hungry", GetString(core, "feltState"));
        Assert.True(GetSingle(core, "insulaBodyFeeling") > 0.35f);
        Assert.True(GetSingle(core, "hypothalamicDrive") > 0.30f);
        Assert.Equal("hungry", GetString(self, "bodyFeeling"));
        Assert.Equal("FindFood", GetString(goal, "goalKey"));
    }

    [Fact]
    public void Autobiographical_Self_Binds_Episodes_Into_Identity_Thread()
    {
        var state = new SimulationState();
        state.UpdateEnvironmentalState(
            darkness: 0.12f,
            shelterNeed: 0.10f,
            anxiety: 0.08f,
            hunger: 0.82f,
            predatorThreat: 0.02f,
            inShelter: 0.10f,
            health: 0.84f,
            shelterSafety: 0.20f);
        state.UpdateAttentionState(BiologicalAttentionRuntime.Default with
        {
            Memory = 0.62f,
            Visual = 0.54f,
            DominantChannel = "memory",
            FocusConfidence = 0.74f,
            Salience = 0.78f
        });
        state.RegisterObjectObservation(
            objectId: "berry_patch_autobio",
            label: "food berry patch",
            hemisphere: "L",
            salience: 0.88f,
            confidence: 0.82f,
            intensity: 1.0f,
            deliveredSpikes: 32);

        DriveCognition(state, 10);

        using var document = SerializeDiagnostics(state);
        var autobiographical = GetObject(document.RootElement, "autobiographicalSelf");
        var continuity = GetObject(document.RootElement, "autobiographicalContinuity");
        var self = GetObject(document.RootElement, "narrativeSelfModel");

        Assert.True(GetBool(autobiographical, "active"));
        Assert.Equal("seeking food", GetString(autobiographical, "currentChapter"));
        Assert.Contains("I am in the chapter", GetString(autobiographical, "identityThread"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            EnumerateObjects(autobiographical, "topSelfEpisodes"),
            episode => GetString(episode, "summary").Contains("food berry patch", StringComparison.OrdinalIgnoreCase));
        Assert.NotEqual("none", GetString(autobiographical, "recentSelfEpisodeKey"));
        Assert.True(GetSingle(autobiographical, "hippocampalIndex") > 0.10f);
        Assert.True(GetSingle(autobiographical, "pfcSelfContinuity") > 0.15f);
        Assert.True(GetBool(continuity, "active"));
        Assert.Contains("Continuity:", GetString(continuity, "continuityThread"), StringComparison.OrdinalIgnoreCase);
        Assert.True(GetSingle(continuity, "identityCoherence") > 0.10f);
        Assert.True(GetSingle(continuity, "semanticBridge") > 0.05f);
        Assert.True(GetSingle(self, "hippocampalAutobiographicalBinding") > 0.10f);
    }

    [Fact]
    public void Identity_Boundary_Keeps_Self_Description_Grounded_And_Bounded()
    {
        var state = new SimulationState();
        state.RegisterObjectObservation(
            objectId: "red_door_place",
            label: "red door shelter",
            hemisphere: "M",
            salience: 0.82f,
            confidence: 0.80f,
            intensity: 1.0f,
            deliveredSpikes: 30);
        state.UpdateEnvironmentalState(
            darkness: 0.24f,
            shelterNeed: 0.34f,
            anxiety: 0.18f,
            hunger: 0.18f,
            predatorThreat: 0.06f,
            inShelter: 0.0f,
            health: 0.92f,
            shelterSafety: 0.12f);

        DriveCognition(state, 8);

        using var document = SerializeDiagnostics(state);
        var boundary = GetObject(document.RootElement, "identityBoundary");
        var inhabitance = GetObject(document.RootElement, "inhabitance");
        var inhabitanceBoundary = GetObject(inhabitance, "identityBoundary");

        Assert.True(GetBool(boundary, "active"));
        Assert.Contains("runtime", GetString(boundary, "selfDescription"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not a biological person", GetString(boundary, "boundary"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("runtime state", GetString(boundary, "allowedClaim"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("biological personhood", GetString(boundary, "disallowedClaim"), StringComparison.OrdinalIgnoreCase);
        Assert.True(GetSingle(boundary, "boundaryConfidence") > 0.55f);
        Assert.Equal(GetString(boundary, "boundary"), GetString(inhabitanceBoundary, "boundary"));
    }

    [Fact]
    public void Room_State_Remembers_Biological_Rule_Promises_And_Journal()
    {
        var state = new SimulationState();
        state.UpdateAttentionState(BiologicalAttentionRuntime.Default with
        {
            Memory = 0.68f,
            Language = 0.62f,
            DominantChannel = "memory",
            FocusConfidence = 0.76f,
            Salience = 0.80f
        });
        state.RegisterObjectObservation(
            objectId: "archive_desk_place",
            label: "safe archive desk",
            hemisphere: "M",
            salience: 0.82f,
            confidence: 0.82f,
            intensity: 1.0f,
            deliveredSpikes: 30);

        var tokens = new[] { "remember", "this" };
        var grammar = EnglishLanguageLexicon.AnalyzeGrammar(tokens);
        state.AdvanceClockAndCreateTickSignal();
        state.ObserveLanguageIntent(grammar, tokens, state.Tick);
        DriveCognition(state, 10);

        using var document = SerializeDiagnostics(state);
        var room = GetObject(document.RootElement, "roomState");
        var promises = GetObject(document.RootElement, "pendingPromises");
        var journal = GetObject(document.RootElement, "continuityJournal");
        var placeModel = GetObject(document.RootElement, "habitablePlaceModel");
        var affordance = GetObject(document.RootElement, "attentionAffordance");
        var preference = GetObject(document.RootElement, "preferenceTemperament");
        var maintenance = GetObject(document.RootElement, "selfMaintenance");
        var atmosphere = GetObject(document.RootElement, "worldAtmosphere");
        var shelf = GetObject(document.RootElement, "workingMemoryShelf");
        var digest = GetObject(document.RootElement, "sleepDreamDigest");
        var inhabitance = GetObject(document.RootElement, "inhabitance");
        var inhabitanceRoom = GetObject(inhabitance, "room");
        var inhabitancePlaceModel = GetObject(inhabitance, "habitablePlaceModel");
        var inhabitanceAffordance = GetObject(inhabitance, "attentionAffordance");
        var inhabitancePreference = GetObject(inhabitance, "preferenceTemperament");
        var inhabitanceMaintenance = GetObject(inhabitance, "selfMaintenance");
        var inhabitanceAtmosphere = GetObject(inhabitance, "worldAtmosphere");
        var inhabitanceShelf = GetObject(inhabitance, "workingMemoryShelf");
        var inhabitanceDigest = GetObject(inhabitance, "sleepDreamDigest");

        Assert.True(GetBool(room, "active"));
        Assert.Contains("spiking evidence", GetString(room, "biologicalRule"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hippocampus", GetString(room, "biologicalSource"), StringComparison.OrdinalIgnoreCase);
        Assert.True(GetSingle(room, "confidence") > 0.10f);
        Assert.True(GetInt(promises, "openCount") >= 3);
        Assert.Contains("biological circuit", GetString(promises, "nextPromise"), StringComparison.OrdinalIgnoreCase);
        Assert.True(GetInt(journal, "count") > 0);
        Assert.Contains("Room=", GetString(journal, "lastEntrySummary"), StringComparison.OrdinalIgnoreCase);
        Assert.True(GetBool(placeModel, "active"));
        Assert.Contains("retrosplenial", GetString(placeModel, "biologicalSource"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains(EnumerateObjects(placeModel, "places"), place => GetString(place, "key") == "workbench");
        Assert.Contains(EnumerateObjects(placeModel, "places"), place => GetString(place, "key") == "listening-space");
        Assert.True(GetBool(affordance, "active"));
        Assert.Contains("PFC", GetString(affordance, "biologicalSource"), StringComparison.OrdinalIgnoreCase);
        Assert.True(GetSingle(affordance, "confidence") > 0.10f);
        Assert.True(GetBool(preference, "active"));
        Assert.Contains("orbitofrontal", GetString(preference, "biologicalSource"), StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(GetString(preference, "workingStyle")));
        Assert.True(GetBool(maintenance, "active"));
        Assert.Contains("insula", GetString(maintenance, "biologicalSource"), StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(GetString(maintenance, "recommendedCare")));
        Assert.True(GetBool(atmosphere, "active"));
        Assert.Contains("retina", GetString(atmosphere, "biologicalSource"), StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(GetString(atmosphere, "atmosphereSummary")));
        Assert.True(GetBool(shelf, "active"));
        Assert.Contains("dlPFC", GetString(shelf, "biologicalSource"), StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(GetString(shelf, "hypothesis")));
        Assert.Contains("hippocampal replay", GetString(digest, "biologicalSource"), StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(GetString(digest, "changed")));
        Assert.Equal(GetString(room, "activeRoom"), GetString(inhabitanceRoom, "activeRoom"));
        Assert.Equal(GetString(placeModel, "activePlaceKey"), GetString(inhabitancePlaceModel, "activePlaceKey"));
        Assert.Equal(GetString(affordance, "target"), GetString(inhabitanceAffordance, "target"));
        Assert.Equal(GetString(preference, "workingStyle"), GetString(inhabitancePreference, "workingStyle"));
        Assert.Equal(GetString(maintenance, "maintenanceState"), GetString(inhabitanceMaintenance, "maintenanceState"));
        Assert.Equal(GetString(atmosphere, "atmosphereSummary"), GetString(inhabitanceAtmosphere, "atmosphereSummary"));
        Assert.Equal(GetString(shelf, "hypothesis"), GetString(inhabitanceShelf, "hypothesis"));
        Assert.Equal(GetString(digest, "changed"), GetString(inhabitanceDigest, "changed"));
    }

    [Fact]
    public void Network_State_Export_Import_Preserves_Room_State()
    {
        var state = new SimulationState();
        state.UpdateAttentionState(BiologicalAttentionRuntime.Default with
        {
            Memory = 0.64f,
            Language = 0.58f,
            DominantChannel = "memory",
            FocusConfidence = 0.72f,
            Salience = 0.76f
        });
        DriveCognition(state, 10);
        _ = state.GetRoomStateSnapshot();

        var exported = state.ExportNetworkState();
        Assert.NotNull(exported.RoomState);
        Assert.NotNull(exported.HabitablePlaceModel);
        Assert.NotNull(exported.AttentionAffordance);
        Assert.NotNull(exported.PreferenceTemperament);
        Assert.NotNull(exported.SelfMaintenance);
        Assert.NotNull(exported.WorldAtmosphere);
        Assert.NotNull(exported.WorkingMemoryShelf);
        Assert.NotNull(exported.SleepDreamDigest);
        Assert.NotEmpty(exported.PendingPromiseItems);
        Assert.NotEmpty(exported.ContinuityJournalEntries);

        var restored = new SimulationState();
        Assert.True(restored.TryImportNetworkState(exported, out var error), error);
        using var document = SerializeDiagnostics(restored);
        var room = GetObject(document.RootElement, "roomState");
        var promises = GetObject(document.RootElement, "pendingPromises");
        var journal = GetObject(document.RootElement, "continuityJournal");
        var placeModel = GetObject(document.RootElement, "habitablePlaceModel");
        var affordance = GetObject(document.RootElement, "attentionAffordance");
        var preference = GetObject(document.RootElement, "preferenceTemperament");
        var maintenance = GetObject(document.RootElement, "selfMaintenance");
        var atmosphere = GetObject(document.RootElement, "worldAtmosphere");
        var shelf = GetObject(document.RootElement, "workingMemoryShelf");
        var digest = GetObject(document.RootElement, "sleepDreamDigest");

        Assert.Contains("spiking evidence", GetString(room, "biologicalRule"), StringComparison.OrdinalIgnoreCase);
        Assert.True(GetInt(promises, "openCount") >= 3);
        Assert.True(GetInt(journal, "count") > 0);
        Assert.Contains("retrosplenial", GetString(placeModel, "biologicalSource"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("global workspace", GetString(affordance, "biologicalSource"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hippocampus", GetString(preference, "biologicalSource"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sleep-memory", GetString(maintenance, "biologicalSource"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("hypothalamus", GetString(atmosphere, "biologicalSource"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("mediodorsal thalamus", GetString(shelf, "biologicalSource"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sleep-memory", GetString(digest, "biologicalSource"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Diagnostics_Reuses_Heavy_Composite_Snapshots_Within_Same_Tick()
    {
        var state = new SimulationState();
        state.UpdateAttentionState(BiologicalAttentionRuntime.Default with
        {
            Memory = 0.64f,
            Language = 0.58f,
            DominantChannel = "memory",
            FocusConfidence = 0.72f,
            Salience = 0.76f
        });
        DriveCognition(state, 8);

        var first = state.ToDiagnostics();
        var second = state.ToDiagnostics();

        Assert.Same(GetAnonymousProperty(first, "Inhabitance"), GetAnonymousProperty(second, "Inhabitance"));
        Assert.Same(GetAnonymousProperty(first, "CircuitAudit"), GetAnonymousProperty(second, "CircuitAudit"));
        Assert.Same(GetAnonymousProperty(first, "BrainBehavior"), GetAnonymousProperty(second, "BrainBehavior"));
        Assert.Same(GetAnonymousProperty(first, "ConsolidationTelemetry"), GetAnonymousProperty(second, "ConsolidationTelemetry"));

        state.AdvanceClockAndCreateTickSignal();
        var nextTick = state.ToDiagnostics();
        Assert.NotSame(GetAnonymousProperty(second, "Inhabitance"), GetAnonymousProperty(nextTick, "Inhabitance"));
    }

    [Fact]
    public void Unified_Event_Memory_Binds_World_Body_Language_Action_And_Reward_Events()
    {
        var state = new SimulationState();
        state.UpdateAttentionState(BiologicalAttentionRuntime.Default with
        {
            Memory = 0.72f,
            Visual = 0.54f,
            Interoceptive = 0.48f,
            Language = 0.46f,
            DominantChannel = "memory",
            FocusConfidence = 0.78f,
            Salience = 0.82f
        });

        var tokens = new[] { "find", "food" };
        var grammar = EnglishLanguageLexicon.AnalyzeGrammar(tokens);
        state.AdvanceClockAndCreateTickSignal();
        state.ObserveLanguageIntent(grammar, tokens, state.Tick);
        state.RegisterObjectObservation(
            objectId: "berry_patch_unified",
            label: "food berry patch",
            hemisphere: "L",
            salience: 0.88f,
            confidence: 0.84f,
            intensity: 1.0f,
            deliveredSpikes: 36);
        state.UpdateEnvironmentalState(
            darkness: 0.64f,
            shelterNeed: 0.56f,
            anxiety: 0.58f,
            hunger: 0.78f,
            predatorThreat: 0.42f,
            inShelter: 0.0f,
            health: 0.80f,
            shelterSafety: 0.10f);
        state.UpdateBodyState(
            forwardVelocity: 0.72f,
            turnRateDeg: 48.0f,
            contactLevel: 0.68f,
            leftMotorDrive: 0.78f,
            rightMotorDrive: 0.20f);
        state.UpdateOutcomeState(
            satietyRelief: 0.72f,
            safetyRelief: 0.18f,
            painLevel: 0.16f,
            damageLevel: 0.04f,
            shelterComfort: 0.08f,
            progress: 0.54f,
            effortCost: 0.18f,
            novelty: 0.32f,
            socialApproval: 0.0f);

        for (var i = 0; i < 8; i++)
        {
            state.AdvanceClockAndCreateTickSignal();
            state.ObserveCognitiveRuntime(
                state.Tick,
                dispatchedSpikes: 18,
                activePathwayCount: 6,
                rewardPredictionError: 0.46f,
                dominantPathway: (StructureId.Vta, StructureId.NucleusAccumbens, NTEnum.DOPAMINE));
        }

        using var document = SerializeDiagnostics(state);
        var unified = GetObject(document.RootElement, "unifiedEventMemory");

        Assert.True(GetInt(unified, "count") >= 5);
        Assert.True(GetInt(unified, "worldObjectEvents") >= 1);
        Assert.True(GetInt(unified, "environmentEvents") >= 1);
        Assert.True(GetInt(unified, "bodyEvents") >= 1);
        Assert.True(GetInt(unified, "languageEvents") >= 1);
        Assert.True(GetInt(unified, "actionEvents") >= 1);
        Assert.True(GetInt(unified, "rewardEvents") >= 1);
        Assert.True(GetSingle(unified, "coverage") >= 0.80f);
        Assert.True(GetSingle(unified, "hippocampalIndex") > 0.10f);
        Assert.Contains("EC-DG-CA3-CA1-subiculum", GetString(unified, "evidence"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Inner_Speech_Loop_Rehearses_Quietly_Without_Spoken_Narration()
    {
        var state = new SimulationState();
        var tokens = new[] { "think", "quietly" };
        var grammar = EnglishLanguageLexicon.AnalyzeGrammar(tokens);

        state.AdvanceClockAndCreateTickSignal();
        state.ObserveLanguageIntent(grammar, tokens, state.Tick);

        using var document = SerializeDiagnostics(state);
        var innerSpeech = GetObject(document.RootElement, "innerSpeechLoop");
        var narration = GetObject(document.RootElement, "brainNarration");
        var prefrontal = GetObject(document.RootElement, "prefrontalWorkingMemory");

        Assert.True(GetBool(innerSpeech, "active"));
        Assert.Equal("explicit quiet rehearsal", GetString(innerSpeech, "mode"));
        Assert.Contains("Quietly rehearse", GetString(innerSpeech, "rehearsedPhrase"), StringComparison.OrdinalIgnoreCase);
        Assert.True(GetSingle(innerSpeech, "motorSuppression") > GetSingle(innerSpeech, "speechReleaseGate"));
        Assert.True(GetSingle(innerSpeech, "workingMemoryBoost") > 0.10f);
        Assert.True(GetSingle(prefrontal, "dorsolateralMaintenance") > 0.10f);
        Assert.Equal(0, GetInt(narration, "sequence"));
    }

    [Fact]
    public void Cerebellum_Runtime_Tracks_Balance_Error_And_Learned_Correction()
    {
        var state = new SimulationState();
        state.UpdateBodyState(
            forwardVelocity: 1.1f,
            turnRateDeg: 220.0f,
            contactLevel: 0.74f,
            leftMotorDrive: 0.95f,
            rightMotorDrive: 0.18f);

        for (var i = 0; i < 10; i++)
        {
            state.AdvanceClockAndCreateTickSignal();
            state.ObserveCognitiveRuntime(state.Tick, dispatchedSpikes: 18, activePathwayCount: 9, rewardPredictionError: 0.35f, dominantPathway: null);
        }

        using var document = SerializeDiagnostics(state);
        var cerebellum = GetObject(document.RootElement, "cerebellum");

        Assert.True(GetSingle(cerebellum, "balanceError") > 0.45f);
        Assert.True(GetSingle(cerebellum, "predictionError") > 0.20f);
        Assert.True(GetSingle(cerebellum, "inferiorOliveTeaching") > 0.25f);
        Assert.True(GetSingle(cerebellum, "learnedCorrection") > 0.05f);
    }

    [Fact]
    public void World_Learning_Map_Uses_Body_Safety_Context()
    {
        var state = new SimulationState();
        state.UpdateEnvironmentalState(
            darkness: 0.16f,
            shelterNeed: 0.12f,
            anxiety: 0.10f,
            hunger: 0.10f,
            predatorThreat: 0.0f,
            inShelter: 0.0f,
            health: 0.58f,
            shelterSafety: 0.0f);
        state.UpdateBodyState(
            forwardVelocity: 0.05f,
            turnRateDeg: 160.0f,
            contactLevel: 0.88f,
            leftMotorDrive: 0.96f,
            rightMotorDrive: 0.08f);
        DriveCognition(state, 8);

        using var document = SerializeDiagnostics(state);
        var worldMap = GetObject(document.RootElement, "worldLearningMap");

        Assert.True(GetInt(worldMap, "threatSources") > 0);
        Assert.NotEqual("none", GetString(worldMap, "mostDangerousKey"));
    }

    private static void AdvanceTicks(SimulationState state, int count)
    {
        for (var i = 0; i < count; i++)
        {
            state.AdvanceClockAndCreateTickSignal();
        }
    }

    private static void DriveCognition(SimulationState state, int count)
    {
        for (var i = 0; i < count; i++)
        {
            state.AdvanceClockAndCreateTickSignal();
            state.ObserveCognitiveRuntime(state.Tick, dispatchedSpikes: 0, activePathwayCount: 0, rewardPredictionError: 0f, dominantPathway: null);
        }
    }

    private static LimbicRuntimeState MakeLimbic(
        float tired,
        float interoceptive,
        float threat,
        float aversive,
        float expectedReward = 0.30f,
        float observedReward = 0.25f)
        => LimbicRuntimeState.Default with
        {
            Salience = Math.Clamp(Math.Max(Math.Max(tired, interoceptive), Math.Max(threat, aversive)), 0f, 1f),
            Threat = threat,
            TiredDrive = tired,
            InteroceptiveDrive = interoceptive,
            AversiveDrive = aversive,
            ExpectedReward = expectedReward,
            ObservedReward = observedReward,
            NeuromodState = new NeuromodState
            {
                DopamineLevel = expectedReward,
                SerotoninLevel = 0.35f,
                AcetylcholineLevel = 0.34f,
                NorepinephrineLevel = Math.Clamp(threat + aversive, 0f, 1f)
            }
        };

    private static InstanceStructureSnapshot MakeSnapshot(StructureId structure, float meanFiringRateHz)
        => new(
            new ServiceInstance(
                structure,
                $"{structure}_test",
                "M",
                new Uri("http://localhost:5001")),
            structure,
            ActiveNeuronCount: 8,
            MeanFiringRateHz: meanFiringRateHz,
            DominantRhythm: BrainRhythm.BETA,
            TopActiveNeurons: Array.Empty<NeuronActivity>(),
            NeuromodLocal: new NeuromodState
            {
                DopamineLevel = 0.30f,
                SerotoninLevel = 0.35f,
                AcetylcholineLevel = 0.30f,
                NorepinephrineLevel = 0.25f
            },
            SpikeInCount: 0,
            SpikeOutCount: 0,
            FeedbackQueueDepth: 0);

    private static BiologicalAttentionRuntime ComputeAttentionForTest(
        long tick,
        IReadOnlyList<InstanceStructureSnapshot> snapshots,
        IReadOnlyDictionary<(StructureId Source, StructureId Target, NTEnum Nt), int> activePathways,
        BiologicalAttentionRuntime previous,
        VisualAttentionRuntime visualAttention,
        PredictivePerceptionRuntime predictivePerception,
        LimbicRuntimeState limbic,
        SleepMemoryRuntime sleepRuntime,
        EnvironmentalStateRuntime environmental,
        BodyStateRuntime bodyState,
        LanguageIntentRuntime languageIntent,
        AttentionVector sensoryRelayBias)
    {
        var method = typeof(SimulationState).Assembly
            .GetTypes()
            .Select(type => type.GetMethod(
                "ComputeBiologicalAttentionRuntime",
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public))
            .FirstOrDefault(candidate => candidate is not null);

        Assert.NotNull(method);
        var result = method!.Invoke(
            null,
            [
                tick,
                snapshots,
                activePathways,
                previous,
                visualAttention,
                predictivePerception,
                limbic,
                sleepRuntime,
                environmental,
                bodyState,
                languageIntent,
                sensoryRelayBias
            ]);

        return Assert.IsType<BiologicalAttentionRuntime>(result);
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

    private static object GetAnonymousProperty(object value, string propertyName)
    {
        var property = value.GetType().GetProperty(propertyName);
        Assert.NotNull(property);
        var result = property!.GetValue(value);
        Assert.NotNull(result);
        return result!;
    }
}
