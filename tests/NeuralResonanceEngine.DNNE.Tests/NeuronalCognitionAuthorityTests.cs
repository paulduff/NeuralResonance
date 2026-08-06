using System.Reflection;
using Microsoft.AspNetCore.Http;
using NeuralResonanceEngine.ControlProgram.Services;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class NeuronalCognitionAuthorityTests
{
    [Fact]
    public void AuthorityAuditNeverPermitsSymbolicCognitionFallback()
    {
        var authority = new NeuronalCognitionAuthorityRuntime();
        var snapshot = authority.Update(
            12,
            NeuronalPerceptDecision.Unavailable,
            NeuronalMemoryDecision.Unavailable,
            NeuronalAttentionWorkspaceDecision.Unavailable,
            NeuronalVisualAttentionDecision.Unavailable,
            NeuronalSleepConsolidationDecision.Unavailable,
            NeuronalLanguageGroundingDecision.Unavailable,
            NeuronalAffectValuationDecision.Unavailable,
            NeuronalExecutiveDecision.Unavailable,
            NeuronalMotorRuntime.Default);

        Assert.Equal(NeuronalCognitionAuthorityRuntime.Authority, snapshot.Authority);
        Assert.False(snapshot.SymbolicScaffoldCanAuthorize);
        Assert.False(snapshot.SemanticMotorInjectionAllowed);
        Assert.False(snapshot.WorldGoalSteeringAllowed);
        Assert.False(snapshot.LegacyLanguageEmissionAllowed);
        Assert.Equal(10, snapshot.Domains.Count);
        Assert.All(snapshot.Domains, static domain =>
        {
            Assert.True(domain.ReadOnlyDecoder);
            Assert.False(domain.DecoderCanAuthorize);
        });
    }

    [Fact]
    public void MetabolicPhysiologyCannotAuthorizeSleepWithoutNeuronalDecision()
    {
        var state = CreateState();
        MetabolicTransitionResult transition = default!;
        for (var i = 0; i < 1024; i++)
        {
            state.AdvanceClockAndCreateTickSignal();
            transition = state.AdvanceMetabolicPhysiology(
                HighLoadTick(),
                NeuronalSleepConsolidationDecision.Unavailable);
        }

        var physiology = state.GetMetabolicPhysiologyRuntime();
        Assert.False(transition.NeuronalSleepObserved);
        Assert.False(transition.EnteredSleep);
        Assert.Equal(0, physiology.SleepEpisodes);
        Assert.Equal(0.0f, physiology.AtpBudget);
        Assert.Equal(physiology.MaxHomeostaticPressure, physiology.HomeostaticPressure);
    }

    [Fact]
    public void GoalAwareMazeNavigatorIsRetiredFromRuntimeAuthority()
    {
        var request = new HippocampalNavigationControlRequest(
            "authority-test",
            "maze-test",
            Reset: true,
            AtCellCenter: true,
            HeadingDeg: 0.0,
            Observation: new HippocampalNavigationObservation(
                Row: 0,
                Column: 0,
                HeadingQuarter: 0,
                ForwardOpen: true,
                LeftOpen: false,
                RightOpen: false,
                RearOpen: false,
                GoalRow: 4,
                GoalColumn: 4,
                GoalBearingDeg: 45.0,
                DistanceToGoal: 5.6,
                CollisionCount: 0,
                GoalReached: false));

        var result = NavigationRoutes.PostDecision(request, new HippocampalNavigationSessionManager());

        Assert.Equal(
            StatusCodes.Status410Gone,
            Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
    }

    [Fact]
    public void ScalarCognitionRecordTypesArePhysicallyAbsent()
    {
        var assembly = typeof(SimulationState).Assembly;
        string[] retiredTypes =
        [
            "PlanningWorkspaceRuntime",
            "GoalIntentRuntime",
            "MotivationArbitrationRuntime",
            "LanguageIntentRuntime",
            "CognitiveLanguageWorkspaceRuntime",
            "InnerSpeechLoopRuntime",
            "PrefrontalWorkingMemoryRuntime",
            "IntentionalActionLoopRuntime",
            "ConsciousnessRhythmRuntime",
            "GlobalWorkspaceRuntime",
            "NarrativeSelfModelRuntime",
            "IdentityBoundaryRuntime",
            "PendingPromiseRuntime",
            "ContinuityJournalRuntime",
            "RoomStateRuntime",
            "HabitablePlaceModelRuntime",
            "AttentionAffordanceRuntime",
            "PreferenceTemperamentRuntime",
            "SelfMaintenanceRuntime",
            "WorldAtmosphereRuntime",
            "WorkingMemoryShelfRuntime",
            "SleepDreamDigestRuntime",
            "BrainNarrationRuntime",
            "SpeechIntentionRuntime",
            "MemoryControlSettings",
            "BodySchemaRuntime",
            "InteroceptiveCoreRuntime",
            "PainProtectionRuntime",
            "BodyPresenceRuntime",
            "BiologicalAttentionRuntime",
            "LimbicRuntimeState",
            "EmotionRuntimeState",
            "CerebellumRuntime",
            "SleepMemoryRuntime",
            "SleepReplayStage"
        ];

        Assert.All(retiredTypes, typeName => Assert.Null(assembly.GetType(typeName)));
    }

    [Fact]
    public void RuntimeAssemblyContainsNoSemanticMotorSpikeBuilders()
    {
        var methodNames = typeof(SimulationState).Assembly
            .GetTypes()
            .SelectMany(type => type.GetMethods(
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Static |
                BindingFlags.Instance))
            .Select(static method => method.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("BuildLanguageIntentMotorSpikes", methodNames);
        Assert.DoesNotContain("BuildIntentionalActionMotorSpikes", methodNames);
        Assert.DoesNotContain("DispatchLanguageIntentMotorSpikesAsync", methodNames);
        Assert.DoesNotContain("DispatchIntentionalActionMotorSpikesAsync", methodNames);
    }

    private static MetabolicTickInput HighLoadTick()
        => new(
            DrainedSpikes: 80,
            GeneratedSpikes: 80,
            ActivePathways: 20,
            SpontaneousGenerated: 2);

    private static SimulationState CreateState()
    {
        var state = new SimulationState();
        state.Configure(
            tickDurationMs: 1.0,
            registry: new Dictionary<StructureId, string>(),
            connectivity: new Dictionary<StructureId, List<SynapticConnection>>());
        return state;
    }
}
