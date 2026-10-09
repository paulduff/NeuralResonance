using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using NeuralResonanceEngine.Shared.Contracts;

namespace NRE.SimAvatar;

public sealed record AvatarInquiryReceipt(
    Uri Endpoint,
    string Question,
    string SessionId,
    string TurnId,
    long BrainTickAtPresentation,
    AvatarRetinalFrameDispatchResult RetinalDispatch);

public sealed record AvatarInquiryPresentation(
    AvatarInquiryReceipt? Receipt,
    AvatarRetinalFrameDispatchResult RetinalDispatch,
    string Status);

public sealed record AvatarInquiryReply(
    string AcceptedText,
    string Status,
    DyadEntityGenerationResponse? Generation);

/// <summary>
/// Presents an operator question as retinal pixels, then requests a separate
/// DNNE-reviewed Entity reply. A queued frame and a later tick demonstrate a
/// processing opportunity, not delivery acknowledgement or comprehension.
/// </summary>
public static class AvatarInquiryApi
{
    public const string GenerationPath = "/api/v1/dyad/language/generate";
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static string NormalizeQuestion(string question)
    {
        ArgumentNullException.ThrowIfNull(question);
        var normalized = string.Join(" ", question.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (normalized.Length == 0 || normalized.Length > AvatarTextSightRenderer.MaximumVisibleCharacters)
        {
            throw new ArgumentException(
                $"Enter a question of 1 to {AvatarTextSightRenderer.MaximumVisibleCharacters} visible characters.", nameof(question));
        }

        if (normalized.Any(character => !AvatarTextSightRenderer.CanRenderCharacter(character)))
        {
            throw new ArgumentException("The text display cannot render one or more characters in this question.", nameof(question));
        }

        return normalized;
    }

    public static async Task<AvatarInquiryPresentation> PresentQuestionAsync(
        HttpClient client,
        Uri endpoint,
        string question,
        string sessionId,
        int generation,
        long captureTimestampMs,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeQuestion(question);
        if (string.IsNullOrWhiteSpace(sessionId) || sessionId.Length > 128)
        {
            throw new ArgumentException("A session identifier of at most 128 characters is required.", nameof(sessionId));
        }

        var frame = AvatarTextSightRenderer.Render(normalized, generation, captureTimestampMs);
        var retinal = await AvatarControlApi.PostRetinalFrameAsync(
            client, endpoint, frame, AvatarRuntimeDefaults.TypedTextVisualInputSource, cancellationToken);
        if (!retinal.Accepted || retinal.BlockedByInputGate || retinal.TargetInstances <= 0 || retinal.GeneratedSpikes <= 0)
        {
            return new AvatarInquiryPresentation(null, retinal,
                "Question input was blocked or produced no activity for a live Retina service. Present it again when input is available.");
        }

        // The visual route dispatches asynchronously. Do not treat its accepted
        // response (which usually has deliveredSpikes=0) as neural comprehension.
        var tick = await ReadBrainTickAsync(client, endpoint, cancellationToken);
        var receipt = new AvatarInquiryReceipt(endpoint, normalized, sessionId, Guid.NewGuid().ToString("N"), tick, retinal);
        return new AvatarInquiryPresentation(receipt, retinal,
            $"Question pixels {(retinal.DispatchDeferred ? "queued" : "submitted")}; Retina activity {retinal.GeneratedSpikes}. Request a reply after DNNE advances beyond tick {tick}.");
    }

    public static async Task<AvatarInquiryReply> RequestReplyAsync(
        HttpClient client,
        Uri endpoint,
        string currentQuestion,
        AvatarInquiryReceipt? receipt,
        CancellationToken cancellationToken = default)
    {
        if (receipt is null)
        {
            return NoReply("Present the question first.");
        }

        if (!string.Equals(endpoint.AbsoluteUri, receipt.Endpoint.AbsoluteUri, StringComparison.Ordinal) ||
            !string.Equals(NormalizeQuestion(currentQuestion), receipt.Question, StringComparison.Ordinal))
        {
            return NoReply("The question or DNNE endpoint changed. Present the question again.");
        }

        var tick = await ReadBrainTickAsync(client, endpoint, cancellationToken);
        if (tick <= receipt.BrainTickAtPresentation)
        {
            return NoReply($"DNNE has not advanced beyond tick {receipt.BrainTickAtPresentation}. Wait, or present again if the brain restarted.");
        }

        // This purpose identifies the operator's question for language generation.
        // It supplies no interpretation, neuronal label, action, or answer to DNNE.
        var request = new DyadEntityGenerationRequest(
            DyadLanguageContract.ProtocolVersion, receipt.SessionId, receipt.TurnId, "dialogue",
            $"Answer the operator's question: {receipt.Question} Use only supported neuronal evidence; say when it is insufficient to describe a feeling, thought or activity.");
        using var response = await client.PostAsJsonAsync(
            AvatarControlApi.BuildUri(endpoint, GenerationPath), request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var candidate = await response.Content.ReadFromJsonAsync<DyadEntityGenerationResponse>(JsonOptions, cancellationToken);
        if (!TryGetAcceptedText(candidate, receipt, out var text, out var reason, minimumBrainTick: tick))
        {
            return new AvatarInquiryReply(string.Empty, reason, candidate);
        }

        return new AvatarInquiryReply(text,
            $"DNNE accepted Entity's reply at tick {candidate!.Review!.Grounding.Tick} (review {candidate.Review.ReviewSequence}).", candidate);
    }

    public static bool TryGetAcceptedText(
        DyadEntityGenerationResponse? response,
        AvatarInquiryReceipt receipt,
        out string text,
        out string reason,
        long minimumBrainTick = 0)
    {
        text = string.Empty;
        var review = response?.Review;
        if (response is null || review is null ||
            !string.Equals(response.ProtocolVersion, DyadLanguageContract.ProtocolVersion, StringComparison.Ordinal) ||
            !string.Equals(review.ProtocolVersion, DyadLanguageContract.ProtocolVersion, StringComparison.Ordinal) ||
            !string.Equals(response.SessionId, receipt.SessionId, StringComparison.Ordinal) ||
            !string.Equals(response.TurnId, receipt.TurnId, StringComparison.Ordinal) ||
            !string.Equals(review.SessionId, receipt.SessionId, StringComparison.Ordinal) ||
            !string.Equals(review.TurnId, receipt.TurnId, StringComparison.Ordinal))
        {
            reason = "No matching DNNE review was returned for this question.";
            return false;
        }

        if (!response.EntityAvailable || !response.Emitted ||
            review.Decision != DyadLanguageCandidateDecision.AcceptedForEmission || review.ReviewSequence <= 0)
        {
            reason = "DNNE deferred the reply: " + response.Detail;
            return false;
        }

        var grounding = review.Grounding;
        if (grounding is null || grounding.Tick <= receipt.BrainTickAtPresentation || grounding.Tick < minimumBrainTick ||
            grounding.IsSleeping || !grounding.NeuronalCircuitObserved || !grounding.NeuronalGroundingAvailable ||
            !grounding.NeuronalGrounded || !grounding.NeuronalSpeechAuthorized)
        {
            reason = "The review has no later, available neuronal speech authorization.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(response.Text) || response.Text.Length > DyadLanguageContract.MaxCandidateLength ||
            !string.Equals(response.Text, response.CandidateText, StringComparison.Ordinal))
        {
            reason = "Reply text does not exactly match the accepted Entity candidate.";
            return false;
        }

        text = response.Text;
        reason = "Exact DNNE-accepted text is available for presentation.";
        return true;
    }

    private static AvatarInquiryReply NoReply(string status) => new(string.Empty, status, null);

    private static async Task<long> ReadBrainTickAsync(HttpClient client, Uri endpoint, CancellationToken cancellationToken)
    {
        var response = await AvatarControlApi.GetJsonAsync(client, endpoint, "/api/v1/state", cancellationToken);
        using var document = response.Document;
        if (!response.IsSuccessStatusCode || document is null ||
            !AvatarJson.TryGetProperty(document.RootElement, "tick", out var value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt64(out var tick) || tick < 0)
        {
            throw new InvalidOperationException("DNNE did not return a valid brain tick.");
        }

        return tick;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
