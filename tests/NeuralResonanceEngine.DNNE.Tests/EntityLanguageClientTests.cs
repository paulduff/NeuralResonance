using System.Net;
using System.Text;
using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class EntityLanguageClientTests
{
    [Fact]
    public async Task GenerateAsync_Uses_Entity_Chat_Contract_And_Maps_Source_References()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse("""
            {
              "response": "I am observing the current state.",
              "architecture": "Mlp",
              "tokenizer": "Bpe",
              "historicalSources": [
                { "sourceId": "history-001", "title": "History", "stableUrl": "https://example.test/history" }
              ],
              "knowledgeSources": [
                { "sourceId": "knowledge-001", "title": "Knowledge", "stableUrl": "https://example.test/knowledge" }
              ]
            }
            """));
        var options = CreateOptions(enabled: true);
        using var httpClient = new HttpClient(handler) { BaseAddress = options.ApiBaseUri };
        var client = new EntityLanguageClient(httpClient, options);

        var result = await client.GenerateAsync(CreatePrompt(), CancellationToken.None);

        Assert.True(result.IsAvailable);
        Assert.Equal("I am observing the current state.", result.CandidateText);
        Assert.Contains("architecture=Mlp", result.EntityVersion, StringComparison.Ordinal);
        Assert.Equal(
            new[] { "https://example.test/history", "https://example.test/knowledge" },
            result.SourceReferences);
        Assert.Equal("/api/chat", handler.RequestUri?.AbsolutePath);
        Assert.Contains("\"checkpointPath\"", handler.RequestBody, StringComparison.Ordinal);
        Assert.Contains("\"message\":\"verified DNNE context\"", handler.RequestBody, StringComparison.Ordinal);
        Assert.Contains("\"tokens\":80", handler.RequestBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateAsync_Does_Not_Call_Entity_When_The_Bridge_Is_Disabled()
    {
        var handler = new StubHttpMessageHandler(_ => throw new InvalidOperationException("HTTP should not be called."));
        var options = CreateOptions(enabled: false);
        using var httpClient = new HttpClient(handler) { BaseAddress = options.ApiBaseUri };
        var client = new EntityLanguageClient(httpClient, options);

        var result = await client.GenerateAsync(CreatePrompt(), CancellationToken.None);

        Assert.False(result.IsAvailable);
        Assert.Contains("disabled", result.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, handler.RequestCount);
    }

    private static EntityLanguageBridgeOptions CreateOptions(bool enabled)
        => new(
            Enabled: enabled,
            ApiBaseUri: new Uri("http://entity.test"),
            CheckpointPath: "C:\\models\\entity.json",
            ChatExamplesPath: null,
            IdentityProfilePath: null,
            HistoryPath: null,
            KnowledgePath: null,
            Tokens: 80,
            Temperature: 0.20f,
            TopK: 8,
            Seed: 1337,
            Timeout: TimeSpan.FromSeconds(30));

    private static DyadEntityPromptSnapshot CreatePrompt()
        => new(
            PromptText: "verified DNNE context",
            PromptFingerprint: "sha256:test",
            FallbackText: "I am watching and waiting.",
            Grounding: new DyadLanguageGroundingSnapshot(
                Tick: 0,
                IsSleeping: false,
                WorkspaceActive: false,
                WorkspaceConfidence: 0.25f,
                WorkingMemoryStability: 0.25f,
                BoundGoalKey: "Observe",
                SemanticFocus: "environment",
                NeedState: "observation",
                AffectiveState: "stable",
                LanguageAttention: 0.12f,
                AttentionConfidence: 0.34f,
                SpeechMode: "internal",
                SpeechEligible: false,
                SpeechConfidence: 0.25f,
                SpeechReleaseGate: 0f,
                SpeechSuppression: 1f,
                Evidence: "quiet monitoring",
                MemoryExcerpts:
                [
                    new DyadVerifiedMemoryExcerpt(
                        "prefrontal-working-memory",
                        "Task=observe; question=What is happening now?; plan=idle.",
                        0.25f,
                        0,
                        "quiet monitoring")
                ],
                CommunicationIntent: new DyadCommunicationIntentSnapshot(
                    Active: false,
                    Intent: "none",
                    Mood: "none",
                    Subject: "none",
                    Strength: 0.00f,
                    Evidence: "quiet monitoring")));

    private static HttpResponseMessage JsonResponse(string json)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder = responder;

        public Uri? RequestUri { get; private set; }
        public string RequestBody { get; private set; } = string.Empty;
        public int RequestCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            RequestUri = request.RequestUri;
            RequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return _responder(request);
        }
    }
}
