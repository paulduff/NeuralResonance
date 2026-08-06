using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class AdminRouteHandlerUnitTests
{
    [Fact]
    public async Task InputGate_Handler_Applies_Requested_Changes()
    {
        var state = CreateState();

        var result = AdminInputControlRoutes.SetInputGates(
            new InputGateControlRequest(AvatarVisionEnabled: false, SpontaneousSpikingEnabled: false),
            state);
        var (status, body) = await ExecuteJsonResultAsync(result);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.NotNull(body);
        Assert.True(GetBool(body.RootElement, "applied"));
        Assert.False(state.IsAvatarVisionEnabled());
        Assert.False(state.IsSpontaneousSpikingEnabled());
    }

    [Fact]
    public async Task InputGate_Handler_Rejects_Empty_Request()
    {
        var state = CreateState();

        var result = AdminInputControlRoutes.SetInputGates(
            new InputGateControlRequest(AvatarVisionEnabled: null, SpontaneousSpikingEnabled: null),
            state);
        var (status, body) = await ExecuteJsonResultAsync(result);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.NotNull(body);
        Assert.Contains("at least one setting", GetString(body.RootElement, "error"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Telemetry_Handlers_Return_Ok()
    {
        var state = CreateState();

        var startup = await ExecuteJsonResultAsync(AdminTelemetryRoutes.GetStartupHealth(state, 4));
        Assert.Equal(StatusCodes.Status200OK, startup.StatusCode);

        var ingress = new InputIngressRuntime(new ConfigurationBuilder().Build());
        var transport = await ExecuteJsonResultAsync(AdminTelemetryRoutes.GetTransportStats(state, ingress));
        Assert.Equal(StatusCodes.Status200OK, transport.StatusCode);
        Assert.NotNull(transport.Body);
        Assert.True(TryGetProperty(transport.Body.RootElement, "inputIngress", out var inputIngress));
        Assert.True(TryGetProperty(inputIngress, "sensory", out _));
        Assert.True(TryGetProperty(inputIngress, "video", out _));
        Assert.False(TryGetProperty(inputIngress, "object", out _));
    }

    [Fact]
    public void Conventional_Reasoning_Handlers_Are_Physically_Absent()
    {
        var methodNames = typeof(AdminReasoningRoutes)
            .GetMethods()
            .Select(static method => method.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.DoesNotContain("GetSchemas", methodNames);
        Assert.DoesNotContain("GetWorldModel", methodNames);
        Assert.DoesNotContain("PostCounterfactual", methodNames);
        Assert.DoesNotContain("GetConsolidation", methodNames);
    }

    [Fact]
    public async Task DyadCandidate_Handler_Rejects_An_Unsupported_Contract_Version()
    {
        var state = CreateState();

        var result = DyadLanguageRoutes.PostCandidate(
            CreateDyadRequest(protocolVersion: "dyad.language-candidate.v0"),
            state);
        var (status, body) = await ExecuteJsonResultAsync(result);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.NotNull(body);
        Assert.Contains("protocolVersion", GetString(body.RootElement, "error"), StringComparison.OrdinalIgnoreCase);
        Assert.Empty(state.GetDyadLanguageCandidateReviews(8));
    }

    [Fact]
    public void DyadCandidate_Handler_Records_A_Neuronal_Only_Review_Without_Symbolic_Fallback()
    {
        var state = CreateState();

        var result = DyadLanguageRoutes.PostCandidate(CreateDyadRequest(), state);
        var value = Assert.IsAssignableFrom<IValueHttpResult>(result).Value;
        var response = Assert.IsType<DyadLanguageCandidateResponse>(value);

        Assert.Equal(DyadLanguageCandidateDecision.Deferred, response.Decision);
        Assert.False(response.Grounding.IsSleeping);
        Assert.Equal(NeuronalLanguageGroundingDecision.Authority, response.Grounding.Authority);
        Assert.False(response.Grounding.NeuronalCircuitObserved);
        Assert.False(response.Grounding.NeuronalGrounded);
        Assert.Contains("did not issue", response.DecisionReason, StringComparison.OrdinalIgnoreCase);

        var audit = Assert.Single(state.GetDyadLanguageCandidateReviews(8));
        Assert.Equal("entity-25m-bpe-v1", audit.Proposal.EntityVersion);
        Assert.Equal("hello from Entity", audit.Proposal.CandidateText);
        Assert.Equal(response.Decision, audit.Decision);
        Assert.Empty(response.Grounding.MemoryExcerpts);
    }

    [Fact]
    public async Task DyadCandidate_Handler_Rejects_A_Prompt_Fingerprint_Mismatch()
    {
        var state = CreateState();
        var invalid = CreateDyadRequest() with { PromptFingerprint = "sha256:wrong" };

        var (status, body) = await ExecuteJsonResultAsync(DyadLanguageRoutes.PostCandidate(invalid, state));

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Contains("fingerprint", GetString(body!.RootElement, "error"), StringComparison.OrdinalIgnoreCase);
        Assert.Empty(state.GetDyadLanguageCandidateReviews(8));
    }

    [Fact]
    public void DyadCandidate_Handler_Is_Idempotent_Per_Session_Turn()
    {
        var state = CreateState();
        var request = CreateDyadRequest();

        var first = Assert.IsType<DyadLanguageCandidateResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(DyadLanguageRoutes.PostCandidate(request, state)).Value);
        var second = Assert.IsType<DyadLanguageCandidateResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(DyadLanguageRoutes.PostCandidate(request, state)).Value);

        Assert.Equal(first.ReviewSequence, second.ReviewSequence);
        Assert.Single(state.GetDyadLanguageCandidateReviews(8));
    }

    [Fact]
    public async Task DyadEntityGeneration_Handler_Defers_Without_Symbolic_Narration_When_Entity_Is_Unavailable()
    {
        var state = CreateState();
        var entityClient = new StubEntityLanguageClient(EntityLanguageCandidateResult.Unavailable("test outage"));

        var result = await DyadLanguageGenerationRoutes.PostGenerate(
            CreateDyadGenerationRequest(),
            state,
            entityClient,
            CancellationToken.None);
        var response = Assert.IsType<DyadEntityGenerationResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);

        Assert.True(response.UsedFallback);
        Assert.False(response.EntityAvailable);
        Assert.False(response.Emitted);
        Assert.Equal("dnne-deferred", response.Origin);
        Assert.Empty(response.Text);
        Assert.Empty(response.CandidateText);
        Assert.Null(response.Review);
        Assert.Empty(state.GetDyadLanguageCandidateReviews(8));
    }

    [Fact]
    public async Task DyadEntityGeneration_Handler_Reviews_An_Entity_Candidate_Without_Symbolic_Fallback()
    {
        var state = CreateState();
        var entityClient = new StubEntityLanguageClient(new EntityLanguageCandidateResult(
            true,
            "test candidate",
            "I am observing the current state.",
            "entity-test; architecture=Mlp; tokenizer=Bpe",
            "tokens=80;temperature=0.20;topK=8;seed=1337",
            new[] { "test://knowledge" }));

        var result = await DyadLanguageGenerationRoutes.PostGenerate(
            CreateDyadGenerationRequest(),
            state,
            entityClient,
            CancellationToken.None);
        var response = Assert.IsType<DyadEntityGenerationResponse>(
            Assert.IsAssignableFrom<IValueHttpResult>(result).Value);

        Assert.False(response.UsedFallback);
        Assert.True(response.EntityAvailable);
        Assert.NotNull(response.Review);
        Assert.Equal(DyadLanguageCandidateDecision.Deferred, response.Review.Decision);
        Assert.False(response.Review.Grounding.NeuronalCircuitObserved);
        Assert.Empty(response.Review.Grounding.MemoryExcerpts);

        var audit = Assert.Single(state.GetDyadLanguageCandidateReviews(8));
        Assert.Contains("You are Entity, the language component of Dyad.", audit.Proposal.PromptText);
        Assert.Contains("has not observed a neuronal language-grounding circuit", audit.Proposal.PromptText);
        Assert.DoesNotContain("Verified DNNE communication intent", audit.Proposal.PromptText);
        Assert.DoesNotContain("prefrontal-working-memory", audit.Proposal.PromptText);
        Assert.Equal("I am observing the current state.", audit.Proposal.CandidateText);
    }

    [Fact]
    public void DyadCandidate_Contract_Cannot_Express_Motor_Reward_Or_Memory_Writes()
    {
        var propertyNames = typeof(DyadLanguageCandidateRequest)
            .GetProperties()
            .Select(property => property.Name);

        Assert.DoesNotContain(propertyNames, name => name.Contains("motor", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(propertyNames, name => name.Contains("reward", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(propertyNames, name => name.Contains("memory", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(propertyNames, name => name.Contains("action", StringComparison.OrdinalIgnoreCase));
    }

    private static SimulationState CreateState()
    {
        var state = new SimulationState();
        state.Configure(
            tickDurationMs: 1.0,
            registry: new Dictionary<StructureId, string>(),
            connectivity: new Dictionary<StructureId, List<SynapticConnection>>());
        return state;
    }

    private static DyadLanguageCandidateRequest CreateDyadRequest(string protocolVersion = DyadLanguageContract.ProtocolVersion)
        => new(
            ProtocolVersion: protocolVersion,
            SessionId: "test-session",
            TurnId: "turn-001",
            EntityVersion: "entity-25m-bpe-v1",
            EntityConfiguration: "temperature=0.2;topK=8",
            PromptFingerprint: DyadLanguageContract.CreatePromptFingerprint("Produce a short grounded test response."),
            PromptText: "Produce a short grounded test response.",
            CandidateKind: "utterance",
            CandidateText: "hello from Entity",
            SourceReferences: new[] { "test://source" });

    private static DyadEntityGenerationRequest CreateDyadGenerationRequest()
        => new(
            ProtocolVersion: DyadLanguageContract.ProtocolVersion,
            SessionId: "test-session",
            TurnId: "turn-entity-001",
            CandidateKind: "utterance",
            Purpose: "state reflection");

    private sealed class StubEntityLanguageClient(EntityLanguageCandidateResult result) : IEntityLanguageClient
    {
        public Task<EntityLanguageCandidateResult> GenerateAsync(DyadEntityPromptSnapshot prompt, CancellationToken cancellationToken)
            => Task.FromResult(result);
    }

    private static Task<(int StatusCode, JsonDocument? Body)> ExecuteJsonResultAsync(IResult result)
    {
        var status = result is IStatusCodeHttpResult statusResult
            ? statusResult.StatusCode.GetValueOrDefault(StatusCodes.Status200OK)
            : StatusCodes.Status200OK;

        var value = result is IValueHttpResult valueResult
            ? valueResult.Value
            : null;

        if (value is null)
        {
            return Task.FromResult((status, (JsonDocument?)null));
        }

        var payload = JsonSerializer.Serialize(value, value.GetType());
        return Task.FromResult<(int StatusCode, JsonDocument? Body)>((status, JsonDocument.Parse(payload)));
    }

    private static bool GetBool(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value))
        {
            return false;
        }

        if (value.ValueKind == JsonValueKind.True)
        {
            return true;
        }

        if (value.ValueKind == JsonValueKind.False)
        {
            return false;
        }

        if (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var parsed))
        {
            return parsed;
        }

        return false;
    }

    private static string GetString(JsonElement element, string name)
    {
        if (!TryGetProperty(element, name, out var value))
        {
            return string.Empty;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.TryGetProperty(name, out value))
        {
            return true;
        }

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
