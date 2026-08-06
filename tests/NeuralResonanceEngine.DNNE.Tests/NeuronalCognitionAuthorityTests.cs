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
            Assert.True(domain.LegacyTelemetryOnly);
            Assert.False(domain.LegacyCanAuthorize);
        });
    }

    [Fact]
    public void ProductionSleepPathCannotUseThresholdFallback()
    {
        var state = CreateState();
        SleepTransitionResult transition = default!;
        for (var i = 0; i < 1024; i++)
        {
            state.AdvanceClockAndCreateTickSignal();
            transition = state.AdvanceSleepHomeostasis(
                HighLoadTick(),
                NeuronalSleepConsolidationDecision.Unavailable);
        }

        var sleep = state.GetSleepMemoryRuntime();
        Assert.False(transition.IsSleeping);
        Assert.False(transition.EnteredSleep);
        Assert.Equal(0, sleep.SleepEpisodes);
        Assert.True(sleep.AtpBudget <= sleep.SleepEnterThreshold ||
                    sleep.SleepPressure >= sleep.SleepPressureEnterThreshold);
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

    private static SleepTickInput HighLoadTick()
        => new(
            DrainedSpikes: 80,
            DispatchedSpikes: 80,
            ActivePathways: 20,
            SpontaneousGenerated: 2,
            NeuronalReplaySpikes: 0);

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
