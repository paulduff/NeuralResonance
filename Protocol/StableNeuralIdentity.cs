using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace NeuralResonanceEngine.Protocol;

/// <summary>Versioned, process-independent identities. Never use GetHashCode for anatomy.</summary>
public static class StableNeuralIdentity
{
    public const string TopologyVersion = "contact-events-v1";

    // Length framing prevents ambiguous concatenations. Invariant formatting is
    // required when a nucleus moves to a host with another locale.
    private static string Encode(object[] parts) => string.Concat(parts.Select(part =>
    {
        var value = Convert.ToString(part, CultureInfo.InvariantCulture) ?? string.Empty;
        return value.Length.ToString(CultureInfo.InvariantCulture) + ":" + value;
    }));

    public static int Combine(params object[] parts)
    {
        uint hash = 2166136261;
        foreach (var character in Encode(parts))
        {
            hash = unchecked((hash ^ (byte)character) * 16777619);
            hash = unchecked((hash ^ (byte)(character >> 8)) * 16777619);
        }
        return unchecked((int)hash);
    }

    public static Guid Create(params object[] parts)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(Encode(parts)));
        return new Guid(digest.AsSpan(0, 16));
    }

    public static Guid Contact(SpikeMessage message, string targetInstance, int targetCell) => Create(
        TopologyVersion, message.SourceInstanceKey, (int)message.SourceStructure,
        message.SourceNeuronId, targetInstance, (int)message.TargetStructure, targetCell,
        message.ProjectionId == Guid.Empty ? message.SynapseId : message.ProjectionId,
        (int)message.Neurotransmitter, message.IsFeedback, message.IsSyntheticBackground);
}
