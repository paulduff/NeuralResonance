using System.Security.Cryptography;
using System.Text.Json;
using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

internal sealed record DescendingPrimaryTarget(int SourceIndex, string TargetNeuron);
internal sealed record DescendingPrimaryOutput(string SourceInstance, StructureId SourceStructure,
    string SourceNeuron, int SourceIndex, StructureId TargetStructure, string TargetNeuron,
    Guid SourceSynapse, Guid Projection, NTEnum Transmitter);

// Declares the existing one-primary-output-per-cell reduction before a fresh
// population exists. It neither invents axon events nor claims calibrated anatomy
// or a complete biological axonal arbor. Receiving boutons remain separate contacts.
internal sealed class IsolatedDescendingProjection
{
    internal const string Version = "isolated-declared-descending-primary-v1";
    internal StructureId SourceStructure { get; }
    internal IReadOnlyList<DescendingPrimaryTarget> Targets { get; }
    internal string Fingerprint { get; }
    private readonly DescendingPrimaryTarget[] _targets;

    internal IsolatedDescendingProjection(StructureId source, IEnumerable<DescendingPrimaryTarget> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        var copied = targets.ToArray();
        if (source is not (StructureId.VestibularNuclei or StructureId.ReticularFormation) ||
            copied.Length is < 1 or > 65536 || copied.Any(t => t is null || t.SourceIndex < 0 ||
                string.IsNullOrWhiteSpace(t.TargetNeuron) || t.TargetNeuron.Length > 128 ||
                t.TargetNeuron.Any(c => char.IsWhiteSpace(c) || char.IsControl(c) || c == '|')))
            throw new ArgumentException("Declare explicit vestibular/reticular cells and canonical motor-cell names.");
        _targets = copied.OrderBy(t => t.SourceIndex).ToArray();
        if (_targets.Where((t, i) => t.SourceIndex != i).Any())
            throw new ArgumentException("Every original source cell needs exactly one primary output, with no gaps or duplicates.");
        SourceStructure = source;
        Targets = Array.AsReadOnly(_targets);
        Fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
            { Version, SourceStructure, TargetStructure = StructureId.SpinalCordMotor, Targets = _targets })));
    }

    internal void ValidateFor(StructureId source, int cells)
    {
        if (source != SourceStructure || cells != _targets.Length)
            throw new ArgumentException("The declaration must cover this entire original source population.");
    }
    internal string Target(int sourceIndex) => _targets[sourceIndex].TargetNeuron;
}

public sealed partial class StructureEngine
{
    private readonly IsolatedDescendingProjection? _diagnosticDescendingProjection;

    // Apply anatomy before choosing/creating the actual output synapse and before
    // the original event exists. Chemistry and firing decisions stay untouched.
    private void ResolveDeclaredDescendingOutput(int sourceIndex, bool feedback,
        ref StructureId target, ref string targetNeuron)
    {
        if (_diagnosticDescendingProjection is null || feedback) return;
        target = StructureId.SpinalCordMotor;
        targetNeuron = _diagnosticDescendingProjection.Target(sourceIndex);
    }

    // Read-only identity prediction uses exactly the existing persistence key.
    // Calling this does not create a synapse or perform any learning/integration.
    internal IReadOnlyList<DescendingPrimaryOutput> DescribeDeclaredDescendingOutputs()
    {
        lock (_stateGate)
        {
            var declared = _diagnosticDescendingProjection ??
                throw new InvalidOperationException("No descending anatomy was declared for this population.");
            return Array.AsReadOnly(_neurons.Select(n =>
            {
                const StructureId target = StructureId.SpinalCordMotor;
                string name = declared.Target(n.Index);
                string key = n.Id + "|" + target + "|" + name + "|" + n.PreferredNt + "|N";
                Guid synapse = _outboundSynapses.TryGetValue(key, out var existing) ? existing.SynapseId :
                    StableNeuralIdentity.Create(StableNeuralIdentity.TopologyVersion, _instanceKey, key);
                return new DescendingPrimaryOutput(_instanceKey, _profile.StructureId, n.Id, n.Index,
                    target, name, synapse, synapse, n.PreferredNt);
            }).ToArray());
        }
    }
}
