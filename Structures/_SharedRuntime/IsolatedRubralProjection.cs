using System.Security.Cryptography;
using System.Text.Json;
using NeuralResonanceEngine.Protocol;

// Explicit outgoing anatomy fixed before a fresh isolated nucleus is created.
// One original event per firing neuron; no post-hoc relabelling or fan-out.
internal sealed class IsolatedRubralProjection
{
    internal const string Version = "isolated-declared-rubral-projection-v1";
    private readonly HashSet<int> _sources;
    internal IReadOnlyList<int> Sources { get; }
    internal int TargetIndex { get; }
    internal string Fingerprint { get; }

    internal IsolatedRubralProjection(IEnumerable<int> sourceIndices, int targetIndex)
    {
        ArgumentNullException.ThrowIfNull(sourceIndices);
        var copied = sourceIndices.Order().ToArray();
        if (copied.Length == 0 || copied.Any(i => i < 0 || i >= 256) ||
            copied.Distinct().Count() != copied.Length || targetIndex < 0)
            throw new ArgumentException("Declare distinct rubral cells and a nonnegative spinal target.");
        _sources = copied.ToHashSet(); Sources = Array.AsReadOnly(copied); TargetIndex = targetIndex;
        Fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
            { Version, Sources = copied, TargetIndex })));
    }

    internal void ValidateFor(int cells, int targetCount)
    {
        if (cells != 256 || TargetIndex >= targetCount)
            throw new ArgumentException("Declared rubral projection exceeds the original source/target population.");
    }

    internal int Resolve(int source, int original) => _sources.Contains(source) ? TargetIndex : original;
}

public sealed partial class StructureEngine
{
    private readonly IsolatedRubralProjection? _diagnosticRubralProjection;
    private int ResolveDeclaredRubralOutput(int source, StructureId target, bool feedback, int original) =>
        _diagnosticRubralProjection is not null && target == StructureId.SpinalCordMotor && !feedback
            ? _diagnosticRubralProjection.Resolve(source, original) : original;
}
