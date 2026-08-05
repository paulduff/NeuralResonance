using System.Text.Json;
using Microsoft.AspNetCore.Http;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class DeterministicSurvivalBenchmarkTests
{
    [Fact]
    public void Same_Seed_And_Brain_Snapshot_Produce_The_Same_Episode_Record()
    {
        var initial = CreateState().ExportNetworkState();
        initial.ExportedAtUnixMs = 1;
        initial.ExportedTickWallClockUnixMs = 1;
        initial.ExportFingerprint = "test-initial-state";
        var request = new SurvivalBenchmarkRequest(
            Seed: 317,
            Steps: 48,
            Policies: new[] { "current-dnne-intent", "rule-safety", "deterministic-random", "no-learning-stationary" },
            InitialBrainState: initial);

        var first = DeterministicSurvivalBenchmark.Run(request, initial, "test-snapshot");
        var second = DeterministicSurvivalBenchmark.Run(request, initial, "test-snapshot");

        Assert.Equal(first.ProtocolVersion, second.ProtocolVersion);
        Assert.Equal(first.InitialWorld, second.InitialWorld);
        Assert.Equal(
            JsonSerializer.Serialize(first.Episodes),
            JsonSerializer.Serialize(second.Episodes));
    }

    [Fact]
    public void Benchmark_Records_Embodied_Observations_Actions_And_Outcomes()
    {
        var initial = CreateState().ExportNetworkState();
        var request = new SurvivalBenchmarkRequest(
            Seed: 91,
            Steps: 32,
            Policies: new[] { "current-dnne-intent" },
            InitialBrainState: initial);

        var result = DeterministicSurvivalBenchmark.Run(request, initial, "test-snapshot");
        var episode = Assert.Single(result.Episodes);

        Assert.InRange(episode.StepsExecuted, 1, 32);
        Assert.Equal(episode.StepsExecuted, episode.Steps.Count);
        Assert.All(episode.Steps, step =>
        {
            Assert.True(step.BrainTick > 0);
            Assert.NotEmpty(step.PolicyAction);
            Assert.NotEmpty(step.DnneMotorDirective);
            Assert.InRange(step.WorldAfterAction.Health, 0f, 1f);
            Assert.InRange(step.WorldAfterAction.Hunger, 0f, 1f);
            Assert.InRange(step.PainLevel, 0f, 1f);
            Assert.InRange(step.DamageLevel, 0f, 1f);
        });
        Assert.NotEqual("running", episode.TerminalCondition);
        Assert.True(episode.FinalBrainSnapshot.Tick >= episode.StepsExecuted);
        Assert.True(episode.Metrics.IntentDrivenActions > 0);
    }

    [Fact]
    public async Task Route_Uses_A_Private_Benchmark_State_And_Rejects_Unknown_Policies()
    {
        var liveState = CreateState();
        liveState.AdvanceClockAndCreateTickSignal();
        var tickBefore = liveState.Tick;

        var success = await SurvivalBenchmarkRoutes.PostRun(
            new SurvivalBenchmarkRequest(Seed: 17, Steps: 12, Policies: new[] { "no-learning-stationary" }),
            liveState);
        var benchmark = Assert.IsType<SurvivalBenchmarkResult>(
            Assert.IsAssignableFrom<IValueHttpResult>(success).Value);

        Assert.Equal(tickBefore, liveState.Tick);
        Assert.Single(benchmark.Episodes);
        Assert.Equal("control-state-snapshot", benchmark.InitialBrainSnapshot.Source);

        var invalid = await SurvivalBenchmarkRoutes.PostRun(
            new SurvivalBenchmarkRequest(Policies: new[] { "not-a-policy" }),
            liveState);
        var status = Assert.IsAssignableFrom<IStatusCodeHttpResult>(invalid).StatusCode;
        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Equal(tickBefore, liveState.Tick);
    }

    private static SimulationState CreateState()
    {
        var state = new SimulationState();
        state.Configure(
            tickDurationMs: 1.0,
            registry: new Dictionary<StructureId, string>(),
            connectivity: new Dictionary<StructureId, List<SynapticConnection>>());
        state.UpdateNeuromod(
            new NeuromodState
            {
                DopamineLevel = 0.50f,
                SerotoninLevel = 0.50f,
                AcetylcholineLevel = 0.50f,
                NorepinephrineLevel = 0.50f
            },
            rewardPredictionError: 0f,
            attention: new AttentionVector(0.25f, 0.25f, 0.25f, 0.25f));
        return state;
    }
}
