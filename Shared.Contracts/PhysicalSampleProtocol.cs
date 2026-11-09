using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NeuralResonanceEngine.Protocol;

namespace NeuralResonanceEngine.Shared.Contracts;

// Isolated physical-sample lane. Completeness means all declared physics-grid
// samples are present, NOT that all afferent events between them are known.
// No live Avatar, Unity, coordinator or neuronal host selects this lane yet.
public sealed record PhysicalSampleClock(Guid SessionId, string BodyId, string SourceId,
    long InitialPhysicsStep, double PhysicsStepMs, double PhysicalEpochMs, double NeuralEpochMs)
{
    public const long MaximumExactStep = 9_007_199_254_740_991;

    public void Validate()
    {
        if (SessionId == Guid.Empty || !ValidKey(BodyId) || !ValidKey(SourceId) ||
            InitialPhysicsStep < 0 || InitialPhysicsStep >= MaximumExactStep ||
            !double.IsFinite(PhysicsStepMs) || PhysicsStepMs <= 0 ||
            !double.IsFinite(PhysicalEpochMs) || PhysicalEpochMs < 0 ||
            !double.IsFinite(NeuralEpochMs) || NeuralEpochMs < 0 ||
            !double.IsFinite(PhysicalEpochMs + PhysicsStepMs) || !double.IsFinite(NeuralEpochMs + PhysicsStepMs) ||
            PhysicalEpochMs + PhysicsStepMs <= PhysicalEpochMs || NeuralEpochMs + PhysicsStepMs <= NeuralEpochMs)
            throw new ArgumentException("Declare an explicit body/source/session and representable physical-to-neural clock.");
    }

    public (double PhysicalMs, double NeuralMs) AtStep(long step)
    {
        Validate();
        if (step < InitialPhysicsStep || step > MaximumExactStep) throw new ArgumentOutOfRangeException(nameof(step));
        long elapsedSteps = step - InitialPhysicsStep;
        double elapsed = elapsedSteps * PhysicsStepMs;
        double physical = PhysicalEpochMs + elapsed, neural = NeuralEpochMs + elapsed;
        if (!double.IsFinite(physical) || !double.IsFinite(neural) || (elapsedSteps > 0 &&
            (physical <= PhysicalEpochMs + (elapsedSteps - 1) * PhysicsStepMs ||
             neural <= NeuralEpochMs + (elapsedSteps - 1) * PhysicsStepMs)))
            throw new ArgumentOutOfRangeException(nameof(step), "Sample clock resolution exhausted; never merge adjacent steps.");
        return (physical, neural);
    }

    // Integer steps are authoritative. Mapped double timestamps are their
    // representations, not a claim that long floating-point sums are exact.
    public string Fingerprint
    {
        get { Validate(); return PhysicalSampleProtocol.Digest(JsonSerializer.Serialize(new
        { PhysicalSampleProtocol.Version, SessionId, BodyId, SourceId, InitialPhysicsStep, PhysicsStepMs, PhysicalEpochMs, NeuralEpochMs })); }
    }

    private static bool ValidKey(string? value) => !string.IsNullOrWhiteSpace(value) && value == value.Trim();
}

// Payload text is an immutable snapshot, including nested muscle arrays. The
// old request timestamp is retained as capture metadata, never used for mapping.
public sealed record PhysicalBodySample(Guid SampleId, long PhysicsStep, double PhysicalTimeMs,
    double NeuralTimeMs, string ObservationJson, string ObservationSha256);

public sealed record PhysicalSampleBatch(int Version, Guid SessionId, string SourceId,
    string ClockFingerprint, long Sequence, long CompleteThroughStep, IReadOnlyList<PhysicalBodySample> Samples);

// Receipt proves in-memory acceptance, not receptor consumption or durable storage.
public sealed record PhysicalSampleReceipt(int Version, Guid SessionId, string SourceId,
    string ClockFingerprint, long Sequence, long CompleteThroughStep, string BatchSha256);

public static class PhysicalSampleProtocol
{
    public const int Version = 1;
    public const int MaximumObservationBytes = 262_144;
    public const int MaximumBatchSamples = 512;
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static string Digest(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    public static string BatchDigest(PhysicalSampleBatch batch) => Digest(JsonSerializer.Serialize(batch));
    public static int ObservationBytes(PhysicalBodySample sample) => Encoding.UTF8.GetByteCount(sample.ObservationJson);
    public static Guid Identity(PhysicalSampleClock clock, long step) =>
        StableNeuralIdentity.Create("physical-body-sample-v1", clock.Fingerprint, step);

    public static PhysicalBodySample Capture(PhysicalSampleClock clock, long step, PhysicalBodyFrameRequest observation)
    {
        ArgumentNullException.ThrowIfNull(clock); ArgumentNullException.ThrowIfNull(observation);
        var time = clock.AtStep(step);
        ValidateOrigin(clock, observation);
        // Default number handling rejects non-finite values, including nested
        // physical measurements. No saturation, neutral filling or reinterpretation.
        string payload = JsonSerializer.Serialize(observation, Json);
        if (Encoding.UTF8.GetByteCount(payload) > MaximumObservationBytes)
            throw new ArgumentException("Physical observation exceeds its declared byte limit.");
        return new(Identity(clock, step), step, time.PhysicalMs, time.NeuralMs, payload, Digest(payload));
    }

    public static PhysicalBodyFrameRequest Decode(PhysicalSampleClock clock, PhysicalBodySample sample)
    {
        ArgumentNullException.ThrowIfNull(clock); ArgumentNullException.ThrowIfNull(sample);
        var time = clock.AtStep(sample.PhysicsStep);
        if (sample.SampleId != Identity(clock, sample.PhysicsStep) || sample.PhysicalTimeMs != time.PhysicalMs ||
            sample.NeuralTimeMs != time.NeuralMs || string.IsNullOrEmpty(sample.ObservationJson) ||
            ObservationBytes(sample) > MaximumObservationBytes || sample.ObservationSha256 != Digest(sample.ObservationJson))
            throw new ArgumentException("Physical sample identity, clock or original payload differs.");
        var observation = JsonSerializer.Deserialize<PhysicalBodyFrameRequest>(sample.ObservationJson, Json)
            ?? throw new ArgumentException("Missing physical observation.");
        ValidateOrigin(clock, observation);
        // Re-serialization validates finite nested numbers. The stored original
        // bytes remain authoritative; decode returns a fresh, caller-owned graph.
        _ = JsonSerializer.Serialize(observation, Json);
        return observation;
    }

    private static void ValidateOrigin(PhysicalSampleClock clock, PhysicalBodyFrameRequest observation)
    {
        if (observation.InputSource != clock.SourceId || observation.Sequence < 0 || observation.TimestampMs < 0)
            throw new ArgumentException("Physical observation belongs to another source or has invalid capture metadata.");
        // This layer checks provenance/finite serialization, not physiological
        // plausibility. Receptor-specific domain checks remain necessary.
    }

    public static PhysicalSampleReceipt Receipt(PhysicalSampleBatch batch) => new(batch.Version, batch.SessionId,
        batch.SourceId, batch.ClockFingerprint, batch.Sequence, batch.CompleteThroughStep, BatchDigest(batch));
}
