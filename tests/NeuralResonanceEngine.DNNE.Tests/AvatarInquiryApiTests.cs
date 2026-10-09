using System.Net;
using System.Text;
using System.Text.Json;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class AvatarInquiryApiTests
{
    private static readonly Uri Endpoint = new("http://localhost:5080/");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string QueuedRetina = """{"accepted":true,"dispatchDeferred":true,"blockedByInputGate":false,"generatedSpikes":18,"deliveredSpikes":0,"targetInstances":2}""";

    [Fact]
    public async Task InquirySendsOnlyPixelsToRetinaAndKeepsTheQuestionClientSide()
    {
        var handler = new ScriptedHandler(_ => JsonResponse(QueuedRetina), _ => JsonResponse("""{"tick":10}"""));
        using var client = new HttpClient(handler);

        var presentation = await AvatarInquiryApi.PresentQuestionAsync(client, Endpoint, "How do you feel?", "operator", 7, 12);

        Assert.NotNull(presentation.Receipt);
        Assert.Equal("How do you feel?", presentation.Receipt.Question);
        Assert.Equal(10, presentation.Receipt.BrainTickAtPresentation);
        Assert.Contains("queued", presentation.Status);
        Assert.Equal(2, handler.Requests.Count);
        var retinal = handler.Requests[0];
        Assert.Equal(HttpMethod.Post, retinal.Method);
        Assert.StartsWith(AvatarControlApi.RetinalFrameInputPath + "?", retinal.Uri.PathAndQuery);
        Assert.Contains("inputSource=avatar_text_display", retinal.Uri.Query);
        Assert.DoesNotContain("question", retinal.Uri.Query, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("application/octet-stream", retinal.ContentType);
        Assert.Equal(AvatarTextSightRenderer.Render("How do you feel?", 7, 12).Pixels, retinal.Body);
        Assert.Equal("/api/v1/state", handler.Requests[1].Uri.AbsolutePath);
    }

    [Theory]
    [InlineData(false, false, 18, 2)]
    [InlineData(true, true, 18, 2)]
    [InlineData(true, false, 0, 2)]
    [InlineData(true, false, 18, 0)]
    public async Task BlockedOrEmptyRetinalInputCannotMakeAReplyEligible(bool accepted, bool blocked, int spikes, int targets)
    {
        var retinal = new AvatarRetinalFrameDispatchResult(accepted, true, blocked, spikes, 0, targets, 0, 0, 0, 0, 0, 0);
        var handler = new ScriptedHandler(_ => JsonResponse(JsonSerializer.Serialize(retinal, JsonOptions)));
        using var client = new HttpClient(handler);

        var result = await AvatarInquiryApi.PresentQuestionAsync(client, Endpoint, "How do you feel?", "operator", 1, 1);

        Assert.Null(result.Receipt);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task AReplyNeedsALaterBrainTickAndDoesNotStepTheBrain()
    {
        var handler = new ScriptedHandler(_ => JsonResponse("""{"tick":10}"""));
        using var client = new HttpClient(handler);

        var reply = await AvatarInquiryApi.RequestReplyAsync(client, Endpoint, Receipt().Question, Receipt());

        Assert.Empty(reply.AcceptedText);
        Assert.Null(reply.Generation);
        Assert.Contains("has not advanced", reply.Status);
        Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, handler.Requests[0].Method);
    }

    [Fact]
    public async Task QuestionOrEndpointEditsNeedANewPresentation()
    {
        var handler = new ScriptedHandler();
        using var client = new HttpClient(handler);
        var receipt = Receipt();

        var absent = await AvatarInquiryApi.RequestReplyAsync(client, Endpoint, receipt.Question, null);
        var edited = await AvatarInquiryApi.RequestReplyAsync(client, Endpoint, "What are you doing?", receipt);
        var moved = await AvatarInquiryApi.RequestReplyAsync(client, new Uri("http://localhost:5081/"), receipt.Question, receipt);

        Assert.All(new[] { absent, edited, moved }, reply => Assert.Empty(reply.AcceptedText));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task LaterInquiryUsesDyadDialogueAndPresentsTheExactReviewedCandidate()
    {
        var receipt = Receipt();
        const string exact = "I cannot yet describe that state.\nMore evidence is needed.";
        var handler = new ScriptedHandler(
            _ => JsonResponse("""{"tick":11}"""),
            _ => JsonResponse(JsonSerializer.Serialize(Accepted(receipt, exact), JsonOptions)));
        using var client = new HttpClient(handler);

        var reply = await AvatarInquiryApi.RequestReplyAsync(client, Endpoint, receipt.Question, receipt);

        Assert.Equal(exact, reply.AcceptedText);
        Assert.NotNull(reply.Generation);
        var generation = handler.Requests[1];
        Assert.Equal(AvatarInquiryApi.GenerationPath, generation.Uri.AbsolutePath);
        var request = JsonSerializer.Deserialize<DyadEntityGenerationRequest>(generation.Body, JsonOptions)!;
        Assert.Equal("dialogue", request.CandidateKind);
        Assert.Equal(receipt.SessionId, request.SessionId);
        Assert.Equal(receipt.TurnId, request.TurnId);
        Assert.Contains(receipt.Question, request.Purpose);
        Assert.True(DyadLanguageContract.TryNormalizeGeneration(request, out _, out var error), error);
    }

    [Fact]
    public async Task AnOlderAcceptedReplyCannotBorrowANewerBrainState()
    {
        var receipt = Receipt();
        var handler = new ScriptedHandler(
            _ => JsonResponse("""{"tick":12}"""),
            _ => JsonResponse(JsonSerializer.Serialize(Accepted(receipt, "Earlier words."), JsonOptions)));
        using var client = new HttpClient(handler);

        var reply = await AvatarInquiryApi.RequestReplyAsync(client, Endpoint, receipt.Question, receipt);

        Assert.Empty(reply.AcceptedText);
        Assert.NotNull(reply.Generation);
        Assert.Contains("later", reply.Status);
    }

    [Theory]
    [InlineData("missing-review")]
    [InlineData("response-protocol")]
    [InlineData("review-protocol")]
    [InlineData("response-session")]
    [InlineData("response-turn")]
    [InlineData("review-session")]
    [InlineData("review-turn")]
    [InlineData("deferred")]
    [InlineData("not-emitted")]
    [InlineData("unavailable")]
    [InlineData("no-sequence")]
    [InlineData("stale-tick")]
    [InlineData("sleeping")]
    [InlineData("unobserved")]
    [InlineData("ungrounded")]
    [InlineData("grounding-unavailable")]
    [InlineData("unauthorized")]
    [InlineData("altered-text")]
    public void InvalidOrDeferredReviewNeverBecomesAvatarSpeech(string defect)
    {
        var receipt = Receipt();
        var response = Accepted(receipt, "Only these exact words.");
        var review = response.Review!;
        var grounding = review.Grounding;
        response = defect switch
        {
            "missing-review" => response with { Review = null },
            "response-protocol" => response with { ProtocolVersion = "v1" },
            "review-protocol" => response with { Review = review with { ProtocolVersion = "v1" } },
            "response-session" => response with { SessionId = "other" },
            "response-turn" => response with { TurnId = "other" },
            "review-session" => response with { Review = review with { SessionId = "other" } },
            "review-turn" => response with { Review = review with { TurnId = "other" } },
            "deferred" => response with { Review = review with { Decision = DyadLanguageCandidateDecision.Deferred } },
            "not-emitted" => response with { Emitted = false },
            "unavailable" => response with { EntityAvailable = false },
            "no-sequence" => response with { Review = review with { ReviewSequence = 0 } },
            "stale-tick" => response with { Review = review with { Grounding = grounding with { Tick = 10 } } },
            "sleeping" => response with { Review = review with { Grounding = grounding with { IsSleeping = true } } },
            "unobserved" => response with { Review = review with { Grounding = grounding with { NeuronalCircuitObserved = false } } },
            "ungrounded" => response with { Review = review with { Grounding = grounding with { NeuronalGrounded = false } } },
            "grounding-unavailable" => response with { Review = review with { Grounding = grounding with { NeuronalGroundingAvailable = false } } },
            "unauthorized" => response with { Review = review with { Grounding = grounding with { NeuronalSpeechAuthorized = false } } },
            "altered-text" => response with { Text = "A paraphrase of the candidate." },
            _ => throw new ArgumentException(defect)
        };

        Assert.False(AvatarInquiryApi.TryGetAcceptedText(response, receipt, out var text, out _));
        Assert.Empty(text);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("What is \u03b1?")]
    public void InquiryDoesNotSilentlySubstituteUnsupportedWriting(string question)
    {
        Assert.Throws<ArgumentException>(() => AvatarInquiryApi.NormalizeQuestion(question));
    }

    [Fact]
    public void TheWholeQuestionMustFitOnTheRetinalDisplay()
    {
        Assert.Equal("How do you feel?", AvatarInquiryApi.NormalizeQuestion("  How\n do\t you feel?  "));
        Assert.Equal(120, AvatarInquiryApi.NormalizeQuestion(new string('A', 120)).Length);
        Assert.Throws<ArgumentException>(() => AvatarInquiryApi.NormalizeQuestion(new string('A', 121)));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"tick\":null}")]
    [InlineData("{\"tick\":\"eleven\"}")]
    [InlineData("{\"tick\":-1}")]
    public async Task MissingBrainTimeCannotAuthorizeAReply(string state)
    {
        var handler = new ScriptedHandler(_ => JsonResponse(state));
        using var client = new HttpClient(handler);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AvatarInquiryApi.RequestReplyAsync(client, Endpoint, Receipt().Question, Receipt()));
        Assert.Single(handler.Requests);
    }

    private static AvatarInquiryReceipt Receipt() => new(
        Endpoint, "How do you feel?", "operator", "turn-1", 10,
        new AvatarRetinalFrameDispatchResult(true, true, false, 18, 0, 2, 24, 12, 10, 8, .8f, .2f));

    private static DyadEntityGenerationResponse Accepted(AvatarInquiryReceipt receipt, string text)
    {
        var grounding = new DyadLanguageGroundingSnapshot(
            11, "DistributedGroundedLanguageCircuits", false, true, true, true,
            1, .9f, 2, .8f, 1, .8f, .9f, .8f, .8f, .8f, .9f, .1f, true, []);
        var review = new DyadLanguageCandidateResponse(
            DyadLanguageContract.ProtocolVersion, receipt.SessionId, receipt.TurnId,
            DyadLanguageCandidateDecision.AcceptedForEmission, "grounded", grounding, 12, DateTimeOffset.UtcNow);
        return new DyadEntityGenerationResponse(
            DyadLanguageContract.ProtocolVersion, receipt.SessionId, receipt.TurnId,
            true, "entity", text, "exact candidate", review, true, text);
    }

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed record RecordedRequest(Uri Uri, HttpMethod Method, string? ContentType, byte[] Body);

    private sealed class ScriptedHandler(params Func<RecordedRequest, HttpResponseMessage>[] responses) : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var recorded = new RecordedRequest(request.RequestUri!, request.Method, request.Content?.Headers.ContentType?.MediaType,
                request.Content is null ? [] : await request.Content.ReadAsByteArrayAsync(cancellationToken));
            Requests.Add(recorded);
            if (Requests.Count > responses.Length)
            {
                throw new InvalidOperationException("Unexpected HTTP request: " + request.RequestUri);
            }

            return responses[Requests.Count - 1](recorded);
        }
    }
}
