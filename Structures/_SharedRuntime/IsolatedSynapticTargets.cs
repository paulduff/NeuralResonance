using System.Security.Cryptography;
using System.Text.Json;
using NeuralResonanceEngine.Protocol;

// Explicit recipient anatomy for a fresh isolated cortical comparison. This is
// not a fitted cortical arbor or a service configuration option. Unlisted input
// retains existing routing; an exact declared contact must not be remapped.
internal sealed record IsolatedSynapticTarget(string SourceInstance, StructureId Source,
    string SourceNeuron, Guid Projection, NTEnum Neurotransmitter, bool Feedback,
    bool Synthetic, int TargetCell)
{
    internal static IsolatedSynapticTarget From(SpikeMessage message, int targetCell) => new(
        message.SourceInstanceKey, message.SourceStructure, message.SourceNeuronId,
        message.ProjectionId == Guid.Empty ? message.SynapseId : message.ProjectionId,
        message.Neurotransmitter, message.IsFeedback, message.IsSyntheticBackground, targetCell);
}

internal sealed class IsolatedSynapticTargets
{
    public const string Version = "isolated-declared-synaptic-targets-v1";
    private readonly Dictionary<IsolatedSynapticTarget, int> _lookup = new();
    private readonly IsolatedSynapticTarget[] _entries;
    private readonly int _neuronCount;
    internal IReadOnlyList<IsolatedSynapticTarget> Entries => Array.AsReadOnly(_entries);
    internal string Fingerprint { get; }

    internal IsolatedSynapticTargets(IEnumerable<IsolatedSynapticTarget> entries, int neuronCount)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (neuronCount <= 0) throw new ArgumentOutOfRangeException(nameof(neuronCount));
        _neuronCount = neuronCount;
        var copied = entries.ToArray();
        if (copied.Length == 0 || copied.Any(e => e is null || e.SourceInstance is null ||
            string.IsNullOrWhiteSpace(e.SourceNeuron) || !Enum.IsDefined(e.Source) || !Enum.IsDefined(e.Neurotransmitter) ||
            e.Projection == Guid.Empty || e.TargetCell < 0 || e.TargetCell >= neuronCount))
            throw new ArgumentException("Declare valid source/contact/recipient identities.");
        _entries = copied.OrderBy(e => e.SourceInstance, StringComparer.Ordinal).ThenBy(e => e.Source)
            .ThenBy(e => e.SourceNeuron, StringComparer.Ordinal).ThenBy(e => e.Projection)
            .ThenBy(e => e.Neurotransmitter).ThenBy(e => e.Feedback).ThenBy(e => e.Synthetic).ToArray();
        foreach (var entry in _entries)
            if (!_lookup.TryAdd(entry with { TargetCell = -1 }, entry.TargetCell))
                throw new ArgumentException("A declared input contact cannot have duplicate or competing recipient cells.");
        Fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { Version, NeuronCount = neuronCount, Entries = _entries })));
    }

    internal bool TryResolve(SpikeMessage message, out int target) =>
        _lookup.TryGetValue(IsolatedSynapticTarget.From(message, -1), out target);

    internal void ValidateFor(int neuronCount)
    {
        if (neuronCount != _neuronCount) throw new ArgumentException("The target map belongs to a different population size.");
    }
}

public sealed partial class StructureEngine
{
    private readonly IsolatedSynapticTargets? _diagnosticSynapticTargets;
    internal string? DiagnosticSynapticTargetFingerprint => _diagnosticSynapticTargets?.Fingerprint;
}
