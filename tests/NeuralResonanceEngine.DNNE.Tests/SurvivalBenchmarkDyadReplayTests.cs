using System.Text.Json;
using Microsoft.AspNetCore.Http;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class SurvivalBenchmarkDyadReplayTests
{
    [Fact]
    public async Task Replay_Records_A_Bounded_Entity_Candidate_And_Dnne_Review()
    {
        var artifact = CreateArtifact();
        var artifactBefore = JsonSerializer.Serialize(artifact.Episodes);
        var request = new SurvivalBenchmarkDyadReplayRequest(
            artifact,
            Policy: "current-dnne-intent",
            SampleEverySteps: 8,
            MaxSamples: 3,
            SessionId: "survival-test",
            CandidateKind: "interpretation");
        Assert.True(SurvivalBenchmarkDyadReplay.TryNormalize(request, out var parameters, out var error), error);
        var entity = new StubEntityLanguageClient(new EntityLanguageCandidateResult(
            true,
            "test candidate",
            "DNNE reports a bounded survival state.",
            "entity-test; tokenizer=Bpe",
            "tokens=80;temperature=0.20;topK=8;seed=1337",
            new[] { "test://survival-source" }));

        var replay = await SurvivalBenchmarkDyadReplay.EvaluateAsync(parameters!, entity, CancellationToken.None);

        Assert.True(replay.ReplayVerified, replay.ReplayEvidence);
        Assert.InRange(replay.Turns.Count, 1, 3);
        Assert.All(replay.Turns, turn =>
        {
            Assert.True(turn.EntityAvailable);
            Assert.False(turn.UsedFallback);
            Assert.NotNull(turn.Review);
            if (turn.Review.Decision == DyadLanguageCandidateDecision.AcceptedForEmission)
            {
                Assert.Equal("entity", turn.Origin);
                Assert.Equal("DNNE reports a bounded survival state.", turn.Text);
            }
            else
            {
                Assert.Equal("entity-deferred", turn.Origin);
                Assert.Empty(turn.Text);
            }
            Assert.Equal(turn.BrainTick, turn.Prompt.Grounding.Tick);
            Assert.InRange(turn.Prompt.PromptText.Length, 1, DyadLanguageContract.MaxPromptLength);
            Assert.Equal(new[] { "test://survival-source" }, turn.SourceReferences);
        });
        Assert.Equal(artifactBefore, JsonSerializer.Serialize(artifact.Episodes));
    }

    [Fact]
    public async Task Replay_Uses_Dnne_Fallback_For_Unavailable_Or_Malformed_Entity_Output()
    {
        var artifact = CreateArtifact();
        var request = new SurvivalBenchmarkDyadReplayRequest(
            artifact,
            SampleEverySteps: 12,
            MaxSamples: 2);
        Assert.True(SurvivalBenchmarkDyadReplay.TryNormalize(request, out var parameters, out var error), error);

        var unavailable = await SurvivalBenchmarkDyadReplay.EvaluateAsync(
            parameters!,
            new StubEntityLanguageClient(EntityLanguageCandidateResult.Unavailable("simulated outage")),
            CancellationToken.None);
        var malformed = await SurvivalBenchmarkDyadReplay.EvaluateAsync(
            parameters!,
            new StubEntityLanguageClient(new EntityLanguageCandidateResult(
                true,
                "malformed candidate",
                new string('x', DyadLanguageContract.MaxCandidateLength + 1),
                "entity-test",
                "tokens=80",
                Array.Empty<string>())),
            CancellationToken.None);

        Assert.All(unavailable.Turns, turn =>
        {
            Assert.False(turn.EntityAvailable);
            Assert.True(turn.UsedFallback);
            if (turn.Prompt.Grounding.NeuronalCircuitObserved &&
                turn.Prompt.Grounding.NeuronalGroundingAvailable &&
                turn.Prompt.Grounding.NeuronalGrounded &&
                turn.Prompt.Grounding.NeuronalSpeechAuthorized &&
                !turn.Prompt.Grounding.IsSleeping)
            {
                Assert.Equal("dnne-fallback", turn.Origin);
                Assert.Equal(turn.Prompt.FallbackText, turn.Text);
            }
            else
            {
                Assert.Equal("dnne-deferred", turn.Origin);
                Assert.Empty(turn.Text);
            }
            Assert.Null(turn.Review);
        });
        Assert.All(malformed.Turns, turn =>
        {
            Assert.False(turn.EntityAvailable);
            Assert.True(turn.UsedFallback);
            Assert.Contains("contract validation", turn.Detail, StringComparison.OrdinalIgnoreCase);
            Assert.Null(turn.Review);
        });
    }

    [Fact]
    public async Task Replay_Route_Returns_A_Review_Only_Artifact()
    {
        var artifact = CreateArtifact();
        var result = await SurvivalBenchmarkRoutes.PostDyadReplay(
            new SurvivalBenchmarkDyadReplayRequest(artifact, MaxSamples: 1),
            new StubEntityLanguageClient(EntityLanguageCandidateResult.Unavailable("test bridge disabled")),
            CancellationToken.None);
        var replay = Assert.IsType<SurvivalBenchmarkDyadReplayResult>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);

        Assert.True(replay.ReplayVerified);
        var turn = Assert.Single(replay.Turns);
        Assert.True(turn.UsedFallback);
        Assert.Null(turn.Review);
        Assert.DoesNotContain(
            typeof(SurvivalBenchmarkDyadReplayTurn).GetProperties().Select(property => property.Name),
            name => name.Contains("MotorOutput", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("RewardUpdate", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("MemoryWrite", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("PolicyChange", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Replay_Rejects_A_Tampered_Artifact()
    {
        var artifact = CreateArtifact();
        var tampered = artifact with { RequestedSteps = artifact.RequestedSteps + 1 };

        Assert.False(SurvivalBenchmarkDyadReplay.TryNormalize(
            new SurvivalBenchmarkDyadReplayRequest(tampered),
            out _,
            out var error));
        Assert.Contains("fingerprint", error, StringComparison.OrdinalIgnoreCase);
    }

    private static SurvivalBenchmarkResult CreateArtifact()
    {
        var initial = CreateState().ExportNetworkState();
        initial.ExportedAtUnixMs = 1;
        initial.ExportedTickWallClockUnixMs = 1;
        initial.ExportFingerprint = "survival-replay-test";
        var request = new SurvivalBenchmarkRequest(
            Seed: 317,
            Steps: 30,
            Policies: new[] { "current-dnne-intent" },
            InitialBrainState: initial);
        return DeterministicSurvivalBenchmark.Run(request, initial, "test-snapshot");
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

    private sealed class StubEntityLanguageClient(EntityLanguageCandidateResult result) : IEntityLanguageClient
    {
        public Task<EntityLanguageCandidateResult> GenerateAsync(DyadEntityPromptSnapshot prompt, CancellationToken cancellationToken)
            => Task.FromResult(result);
    }
}
