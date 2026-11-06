using System.Security.Cryptography;
using System.Text.Json;

namespace NeuralResonanceEngine.Protocol;

// Isolated candidate protocol. A populated packet is not proof of a qualified
// motor neuron, neuromuscular junction, muscle force, or live motor authority.
public sealed record MotorAxonContact(
    string SourceInstanceKey, string SourceNeuronId, Guid ProjectionId,
    Guid ContactId, string MotorUnitId, double ConductionDelayMs)
{
    // This contact addresses a whole motor unit's endplate group. It does not
    // model individual muscle fibres or their separate neuromuscular junctions.
    public static MotorAxonContact Create(string instance, string neuron, Guid projection,
        string motorUnit, double delayMs) => new(instance, neuron, projection,
            StableNeuralIdentity.Create("motor-endplate-group-v1", instance, neuron, projection, motorUnit),
            motorUnit, delayMs);
}

public sealed record MotorAxonSpike(Guid MessageId, long Ordinal, string SourceNeuronId,
    Guid ProjectionId, Guid SourceSynapseId, double FiredAtNeuralMs, NTEnum Neurotransmitter,
    bool IsSyntheticBackground, bool IsFeedback);

// CompleteThroughNeuralMs is an inclusive source watermark: after accepting it,
// no later batch may contain an event at or before that time. Empty batches
// establish silence. Absence of a batch does not establish silence.
public sealed record MotorAxonBatch(int Version, Guid SessionId, string SourceInstanceKey,
    string AnatomyFingerprint, long Sequence, double CompleteThroughNeuralMs,
    IReadOnlyList<MotorAxonSpike> Spikes);

public sealed class MotorAxonAnatomy
{
    public const int ProtocolVersion = 1;
    private readonly MotorAxonContact[] _contacts;
    private readonly Dictionary<(string Instance, string Neuron, Guid Projection), MotorAxonContact> _lookup;
    public string Fingerprint { get; }
    public IReadOnlyList<MotorAxonContact> Contacts => Array.AsReadOnly(_contacts);

    public MotorAxonAnatomy(IEnumerable<MotorAxonContact> contacts)
    {
        ArgumentNullException.ThrowIfNull(contacts);
        _contacts = contacts.OrderBy(c => c?.ContactId).ToArray()!;
        if (_contacts.Length == 0) throw new ArgumentException("Declare at least one motor-unit contact.");
        _lookup = new();
        var units = new HashSet<string>(StringComparer.Ordinal);
        var axons = new HashSet<(string, string)>();
        foreach (var c in _contacts)
        {
            if (c is null || !ValidKey(c.SourceInstanceKey) || !ValidKey(c.SourceNeuronId) ||
                !ValidKey(c.MotorUnitId) || c.ProjectionId == Guid.Empty ||
                !double.IsFinite(c.ConductionDelayMs) || c.ConductionDelayMs < 0 ||
                c.ContactId != MotorAxonContact.Create(c.SourceInstanceKey, c.SourceNeuronId,
                    c.ProjectionId, c.MotorUnitId, c.ConductionDelayMs).ContactId)
                throw new ArgumentException("Invalid or unstable motor contact identity/delay.");
            if (!units.Add(c.MotorUnitId) || !axons.Add((c.SourceInstanceKey, c.SourceNeuronId)) ||
                !_lookup.TryAdd((c.SourceInstanceKey, c.SourceNeuronId, c.ProjectionId), c))
                throw new ArgumentException("Each declared motor axon innervates one distinct motor unit.");
        }
        Fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
            new { Version = ProtocolVersion, Contacts = _contacts })));
    }

    public MotorAxonContact Resolve(string instance, string neuron, Guid projection) =>
        _lookup.TryGetValue((instance, neuron, projection), out var contact) ? contact :
            throw new ArgumentException("Event has no declared motor-unit contact.");

    internal static bool ValidKey(string? key) => !string.IsNullOrWhiteSpace(key) && key == key.Trim();

    public static void ValidateSpike(MotorAxonSpike spike)
    {
        if (spike is null || spike.MessageId == Guid.Empty || spike.Ordinal <= 0 ||
            !ValidKey(spike.SourceNeuronId) || spike.ProjectionId == Guid.Empty || spike.SourceSynapseId == Guid.Empty ||
            !double.IsFinite(spike.FiredAtNeuralMs) || spike.FiredAtNeuralMs < 0 ||
            spike.Neurotransmitter != NTEnum.ACETYLCHOLINE || spike.IsSyntheticBackground || spike.IsFeedback)
            throw new ArgumentException("Require an identified cholinergic motor discharge; no background or feedback event.");
    }
}

// One shared simulation clock, with an explicitly established epoch. Wall time
// is not biological time. Pausing means not advancing the body clock; neither a
// delayed packet nor a service restart may re-anchor this mapping automatically.
public sealed record MotorAxonClock(Guid SessionId, double NeuralEpochMs, double BodyEpochMs)
{
    public void Validate()
    {
        if (SessionId == Guid.Empty || !double.IsFinite(NeuralEpochMs) || NeuralEpochMs < 0 ||
            !double.IsFinite(BodyEpochMs) || BodyEpochMs < 0)
            throw new ArgumentException("Declare a fresh session and finite nonnegative clock epochs.");
    }

    public double ToBodyMs(double neuralMs)
    {
        Validate();
        double result = BodyEpochMs + (neuralMs - NeuralEpochMs);
        if (!double.IsFinite(neuralMs) || neuralMs < NeuralEpochMs || !double.IsFinite(result))
            throw new ArgumentOutOfRangeException(nameof(neuralMs));
        return result;
    }
}
