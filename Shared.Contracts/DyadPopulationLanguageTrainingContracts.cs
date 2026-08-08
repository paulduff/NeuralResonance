using System.Security.Cryptography;
using System.Text;

namespace NeuralResonanceEngine.Shared.Contracts;

public sealed record DyadAdapterTrainingSource(
    int PopulationIndex,
    float Confidence,
    long Tick);

public sealed record DyadAdapterTrainingGrounding(
    long Tick,
    bool IsSleeping,
    bool NeuronalCircuitObserved,
    bool NeuronalGroundingAvailable,
    bool NeuronalGrounded,
    int PerceptPopulation,
    float PerceptConfidence,
    int MemoryPopulation,
    float MemoryConfidence,
    int AttentionPopulation,
    float LanguageAttention,
    float AttentionConfidence,
    float LanguageCircuitCoverage,
    float ComprehensionDrive,
    float ExpressionDrive,
    float GroundingConfidence,
    float Uncertainty,
    bool NeuronalSpeechAuthorized,
    IReadOnlyList<DyadAdapterTrainingSource> Sources);

public sealed record DyadAdapterTrainingRecord(
    string ProtocolVersion,
    long ReviewSequence,
    DateTimeOffset ReviewedAtUtc,
    string SessionFingerprint,
    string TurnFingerprint,
    string CandidateFingerprint,
    DyadAdapterTrainingGrounding Grounding,
    string TargetText);

public sealed record DyadAdapterTrainingDataset(
    string ProtocolVersion,
    int Count,
    IReadOnlyList<DyadAdapterTrainingRecord> Records);

public static class DyadPopulationLanguageTrainingContract
{
    public const string ProtocolVersion = "dyad.population-language-training.v1";

    public static DyadAdapterTrainingDataset CreateDataset(
        IEnumerable<DyadLanguageCandidateAuditRecord> reviews)
    {
        ArgumentNullException.ThrowIfNull(reviews);
        var records = reviews
            .Where(IsEligible)
            .Select(CreateRecord)
            .ToArray();
        return new DyadAdapterTrainingDataset(ProtocolVersion, records.Length, records);
    }

    private static bool IsEligible(DyadLanguageCandidateAuditRecord review)
        => review.Decision == DyadLanguageCandidateDecision.AcceptedForEmission &&
           !string.IsNullOrWhiteSpace(review.Proposal.CandidateText) &&
           !review.Grounding.IsSleeping &&
           review.Grounding.NeuronalCircuitObserved &&
           review.Grounding.NeuronalGroundingAvailable &&
           review.Grounding.NeuronalGrounded &&
           review.Grounding.NeuronalSpeechAuthorized;

    private static DyadAdapterTrainingRecord CreateRecord(DyadLanguageCandidateAuditRecord review)
    {
        var grounding = review.Grounding;
        return new DyadAdapterTrainingRecord(
            ProtocolVersion,
            review.ReviewSequence,
            review.ReviewedAtUtc,
            Fingerprint("session", review.Proposal.SessionId),
            Fingerprint("turn", review.Proposal.SessionId + "\u001f" + review.Proposal.TurnId),
            Fingerprint("candidate", review.Proposal.CandidateText),
            new DyadAdapterTrainingGrounding(
                grounding.Tick,
                grounding.IsSleeping,
                grounding.NeuronalCircuitObserved,
                grounding.NeuronalGroundingAvailable,
                grounding.NeuronalGrounded,
                grounding.PerceptEnsemble,
                grounding.PerceptConfidence,
                grounding.MemoryEnsemble,
                grounding.MemoryConfidence,
                grounding.AttentionChannel,
                grounding.LanguageAttention,
                grounding.AttentionConfidence,
                grounding.LanguageCircuitCoverage,
                grounding.ComprehensionDrive,
                grounding.ExpressionDrive,
                grounding.GroundingConfidence,
                grounding.Uncertainty,
                grounding.NeuronalSpeechAuthorized,
                grounding.Sources
                    .Where(static source =>
                        source.PopulationIndex >= 0 &&
                        source.Tick >= 0 &&
                        float.IsFinite(source.Confidence) &&
                        source.Confidence is >= 0f and <= 1f)
                    .Take(64)
                    .Select(static source => new DyadAdapterTrainingSource(
                        source.PopulationIndex,
                        source.Confidence,
                        source.Tick))
                    .ToArray()),
            review.Proposal.CandidateText);
    }

    private static string Fingerprint(string domain, string value)
    {
        var bytes = Encoding.UTF8.GetBytes($"dyad-adapter:{domain}:v1\u001f{value}");
        return "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
