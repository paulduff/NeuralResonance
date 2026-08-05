using System.Security.Cryptography;
using System.Text;

namespace NeuralResonanceEngine.Shared.Contracts;

public enum DyadLanguageCandidateDecision
{
    Deferred = 0,
    AcceptedForReview = 1,
    AcceptedForEmission = 2
}

public sealed record DyadLanguageCandidateRequest(
    string? ProtocolVersion,
    string? SessionId,
    string? TurnId,
    string? EntityVersion,
    string? EntityConfiguration,
    string? PromptFingerprint,
    string? PromptText,
    string? CandidateKind,
    string? CandidateText,
    IReadOnlyList<string>? SourceReferences);

public sealed record DyadLanguageCandidateProposal(
    string ProtocolVersion,
    string SessionId,
    string TurnId,
    string EntityVersion,
    string EntityConfiguration,
    string PromptFingerprint,
    string PromptText,
    string CandidateKind,
    string CandidateText,
    IReadOnlyList<string> SourceReferences);

public sealed record DyadEntityGenerationRequest(
    string? ProtocolVersion,
    string? SessionId,
    string? TurnId,
    string? CandidateKind,
    string? Purpose);

public sealed record DyadEntityGenerationParameters(
    string ProtocolVersion,
    string SessionId,
    string TurnId,
    string CandidateKind,
    string Purpose);

public sealed record DyadVerifiedMemoryExcerpt(
    string MemorySystem,
    string Summary,
    float Confidence,
    long LastUpdatedTick,
    string Evidence);

public sealed record DyadCommunicationIntentSnapshot(
    bool Active,
    string Intent,
    string Mood,
    string Subject,
    float Strength,
    string Evidence);

public sealed record DyadLanguageGroundingSnapshot(
    long Tick,
    bool IsSleeping,
    bool WorkspaceActive,
    float WorkspaceConfidence,
    float WorkingMemoryStability,
    string BoundGoalKey,
    string SemanticFocus,
    string NeedState,
    string AffectiveState,
    float LanguageAttention,
    float AttentionConfidence,
    string SpeechMode,
    bool SpeechEligible,
    float SpeechConfidence,
    float SpeechReleaseGate,
    float SpeechSuppression,
    string Evidence,
    IReadOnlyList<DyadVerifiedMemoryExcerpt> MemoryExcerpts,
    DyadCommunicationIntentSnapshot CommunicationIntent);

public sealed record DyadLanguageCandidateResponse(
    string ProtocolVersion,
    string SessionId,
    string TurnId,
    DyadLanguageCandidateDecision Decision,
    string DecisionReason,
    DyadLanguageGroundingSnapshot Grounding,
    long ReviewSequence,
    DateTimeOffset ReviewedAtUtc);

public sealed record DyadLanguageCandidateAuditRecord(
    long ReviewSequence,
    DateTimeOffset ReviewedAtUtc,
    DyadLanguageCandidateProposal Proposal,
    DyadLanguageCandidateDecision Decision,
    string DecisionReason,
    DyadLanguageGroundingSnapshot Grounding);

public sealed record DyadEntityPromptSnapshot(
    string PromptText,
    string PromptFingerprint,
    string FallbackText,
    DyadLanguageGroundingSnapshot Grounding);

public sealed record DyadEntityGenerationResponse(
    string ProtocolVersion,
    string SessionId,
    string TurnId,
    bool EntityAvailable,
    bool UsedFallback,
    string Origin,
    string Text,
    string Detail,
    DyadLanguageCandidateResponse? Review)
{
    public bool Emitted { get; init; }
    public string CandidateText { get; init; } = string.Empty;
}

public static class DyadLanguageContract
{
    public const string ProtocolVersion = "dyad.language-candidate.v1";
    public const int MaxCandidateLength = 1600;
    public const int MaxPromptLength = 2400;
    public const int MaxSourceReferences = 12;

    public static bool TryNormalize(
        DyadLanguageCandidateRequest? request,
        out DyadLanguageCandidateProposal? proposal,
        out string? error)
    {
        proposal = null;
        error = null;

        if (request is null)
        {
            error = "Request payload is required.";
            return false;
        }

        if (!string.Equals(request.ProtocolVersion?.Trim(), ProtocolVersion, StringComparison.Ordinal))
        {
            error = $"Unsupported protocolVersion. Expected '{ProtocolVersion}'.";
            return false;
        }

        if (!TryNormalizeRequired(request.SessionId, "sessionId", 128, out var sessionId, out error) ||
            !TryNormalizeRequired(request.TurnId, "turnId", 128, out var turnId, out error) ||
            !TryNormalizeRequired(request.EntityVersion, "entityVersion", 128, out var entityVersion, out error) ||
            !TryNormalizeRequired(request.PromptFingerprint, "promptFingerprint", 128, out var promptFingerprint, out error) ||
            !TryNormalizeRequired(request.PromptText, "promptText", MaxPromptLength, out var promptText, out error) ||
            !TryNormalizeRequired(request.CandidateText, "candidateText", MaxCandidateLength, out var candidateText, out error))
        {
            return false;
        }

        if (!TryNormalizeCandidateKind(request.CandidateKind, out var candidateKind, out error))
        {
            return false;
        }

        if (!TryNormalizeOptional(request.EntityConfiguration, "entityConfiguration", 256, out var entityConfiguration, out error) ||
            !TryNormalizeSourceReferences(request.SourceReferences, out var sourceReferences, out error))
        {
            return false;
        }

        var expectedFingerprint = CreatePromptFingerprint(promptText);
        if (!FixedTimeEquals(promptFingerprint, expectedFingerprint))
        {
            error = "promptFingerprint does not match the normalized promptText.";
            return false;
        }

        proposal = new DyadLanguageCandidateProposal(
            ProtocolVersion,
            sessionId,
            turnId,
            entityVersion,
            entityConfiguration,
            promptFingerprint,
            promptText,
            candidateKind,
            candidateText,
            sourceReferences);
        return true;
    }

    public static bool TryNormalizeGeneration(
        DyadEntityGenerationRequest? request,
        out DyadEntityGenerationParameters? parameters,
        out string? error)
    {
        parameters = null;
        error = null;

        if (request is null)
        {
            error = "Request payload is required.";
            return false;
        }

        if (!string.Equals(request.ProtocolVersion?.Trim(), ProtocolVersion, StringComparison.Ordinal))
        {
            error = $"Unsupported protocolVersion. Expected '{ProtocolVersion}'.";
            return false;
        }

        if (!TryNormalizeRequired(request.SessionId, "sessionId", 128, out var sessionId, out error) ||
            !TryNormalizeRequired(request.TurnId, "turnId", 128, out var turnId, out error) ||
            !TryNormalizeCandidateKind(request.CandidateKind, out var candidateKind, out error) ||
            !TryNormalizeOptional(request.Purpose, "purpose", 320, out var purpose, out error))
        {
            return false;
        }

        parameters = new DyadEntityGenerationParameters(
            ProtocolVersion,
            sessionId,
            turnId,
            candidateKind,
            purpose);
        return true;
    }

    private static bool TryNormalizeCandidateKind(string? value, out string candidateKind, out string? error)
    {
        candidateKind = NormalizeWhitespace(value);
        if (string.IsNullOrEmpty(candidateKind))
        {
            candidateKind = "utterance";
        }

        candidateKind = candidateKind.ToLowerInvariant();
        if (candidateKind is "utterance" or "interpretation" or "question" or "dialogue")
        {
            error = null;
            return true;
        }

        error = "candidateKind must be utterance, interpretation, question, or dialogue.";
        return false;
    }

    private static bool TryNormalizeRequired(string? value, string field, int maximumLength, out string normalized, out string? error)
    {
        normalized = NormalizeWhitespace(value);
        if (string.IsNullOrEmpty(normalized))
        {
            error = $"{field} is required.";
            return false;
        }

        if (normalized.Length > maximumLength)
        {
            error = $"{field} must not exceed {maximumLength} characters.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryNormalizeOptional(string? value, string field, int maximumLength, out string normalized, out string? error)
    {
        normalized = NormalizeWhitespace(value);
        if (normalized.Length > maximumLength)
        {
            error = $"{field} must not exceed {maximumLength} characters.";
            return false;
        }

        if (string.IsNullOrEmpty(normalized))
        {
            normalized = "unspecified";
        }

        error = null;
        return true;
    }

    private static bool TryNormalizeSourceReferences(
        IReadOnlyList<string>? values,
        out IReadOnlyList<string> normalized,
        out string? error)
    {
        normalized = Array.Empty<string>();
        error = null;
        if (values is null || values.Count == 0)
        {
            return true;
        }

        if (values.Count > MaxSourceReferences)
        {
            error = $"sourceReferences must contain at most {MaxSourceReferences} entries.";
            return false;
        }

        var entries = new List<string>(values.Count);
        foreach (var value in values)
        {
            var entry = NormalizeWhitespace(value);
            if (string.IsNullOrEmpty(entry))
            {
                error = "sourceReferences cannot contain empty values.";
                return false;
            }

            if (entry.Length > 256)
            {
                error = "Each sourceReferences entry must not exceed 256 characters.";
                return false;
            }

            entries.Add(entry);
        }

        normalized = entries;
        return true;
    }

    private static string NormalizeWhitespace(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    public static string CreatePromptFingerprint(string promptText)
    {
        var normalized = NormalizeWhitespace(promptText);
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return "sha256:" + Convert.ToHexString(digest).ToLowerInvariant();
    }

    private static bool FixedTimeEquals(string supplied, string expected)
    {
        var suppliedBytes = Encoding.UTF8.GetBytes(supplied);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        return suppliedBytes.Length == expectedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(suppliedBytes, expectedBytes);
    }
}
