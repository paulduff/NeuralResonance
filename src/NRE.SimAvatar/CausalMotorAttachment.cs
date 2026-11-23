using System.Security.Cryptography;
using System.Text.Json;
using NeuralResonanceEngine.Shared.Contracts;

namespace NRE.SimAvatar;

// Explicit fresh initialization AFTER the first physical sampling interval.
// This receipt does not adopt an evolved muscle, reset a neuron, or certify
// silence before the new motor epoch. Preserve the original physical clock.
internal sealed class CausalMotorAttachment
{
    public const string Version = "passive-physical-sampling-motor-attachment-v1";
    public string PhysicalClockFingerprint { get; }
    public MotorMechanicalFrame Frame { get; }
    public string InitializationEvidenceSha256 { get; }
    public double NeuralEpochMs { get; }
    public string Fingerprint { get; }

    public CausalMotorAttachment(PhysicalSampleClock clock, MotorMechanicalFrame frame, string initializationEvidenceSha256)
    {
        ArgumentNullException.ThrowIfNull(clock); clock.Validate(); ArgumentNullException.ThrowIfNull(frame);
        if (frame.SessionId != clock.SessionId || frame.PhysicsStep != checked(clock.InitialPhysicsStep + 1) ||
            frame.PhysicalTimeMs != clock.AtStep(frame.PhysicsStep).PhysicalMs || frame.LastAppliedEffortId != Guid.Empty ||
            string.IsNullOrWhiteSpace(frame.BodyFingerprint) || frame.Paths is null || frame.Paths.Count == 0 ||
            frame.Paths.Any(p => p is null || string.IsNullOrWhiteSpace(p.MuscleId) || !double.IsFinite(p.LengthMeters) ||
                p.LengthMeters <= 0 || !double.IsFinite(p.VelocityMetersPerSecond)) ||
            frame.Paths.Select(p => p.MuscleId).Distinct(StringComparer.Ordinal).Count() != frame.Paths.Count ||
            initializationEvidenceSha256 is null || initializationEvidenceSha256.Length != 64 ||
            initializationEvidenceSha256.Any(c => !(c is >= '0' and <= '9' or >= 'A' and <= 'F')))
            throw new ArgumentException("Require the actual first passive endpoint, unchanged clock and preserved initialization evidence.");
        PhysicalClockFingerprint = clock.Fingerprint;
        Frame = frame with { Paths = Array.AsReadOnly(frame.Paths.OrderBy(p => p.MuscleId, StringComparer.Ordinal).ToArray()) };
        InitializationEvidenceSha256 = initializationEvidenceSha256;
        NeuralEpochMs = clock.AtStep(frame.PhysicsStep).NeuralMs;
        Fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        { Version, PhysicalClockFingerprint, Frame, InitializationEvidenceSha256, NeuralEpochMs })));
    }
}
