using NeuralResonanceEngine.Protocol;

internal static class CorticalPrecisionMode
{
    internal const string Version = "isolated-cortical-receptor-persistence-double-v1";
    internal static bool Supports(string rule) => rule is "STDP" or "STDP+SynapticTaggingCapture" or "SynapticTaggingCapture";
    internal static bool PairChangesWeight(string rule) => rule is "STDP" or "STDP+SynapticTaggingCapture";
    internal static bool Captures(string rule) => rule is "STDP+SynapticTaggingCapture" or "SynapticTaggingCapture";
}

internal sealed record CorticalPrecisionContact(Guid ProjectionId, string SourceInstance, StructureId Source,
    string SourceNeuron, string TargetInstance, int Target, CorticalContactState State)
{
    internal static CorticalPrecisionContact From(SynapseState c, string targetInstance) => new(c.ProjectionId,
        c.SourceInstanceKey,c.SourceStructure,c.SourceNeuronId,targetInstance,c.LastTargetNeuronIndex,
        c.CorticalPrecision?.State ?? throw new InvalidOperationException("No precise contact state."));
}
internal sealed record CorticalPrecisionLearning(string Kind, Guid? MessageId, double TimestampMs, double ElapsedMs,
    double Activity, double Dopamine, double Acetylcholine, CorticalPrecisionContact Before, CorticalPrecisionContact After);
internal sealed record CorticalReceptorState(double Ampa, double Nmda, double Metabotropic,
    double BasalVoltage, double ApicalVoltage, double ProximalTrace, double DistalTrace, double MicrotubuleDrive);
internal sealed record CorticalPrecisionInput(Guid MessageId, Guid ContactId, double ArrivalMs, double AppliedMs,
    int Target, double Release, double Weight, double EffectiveQuanta, CorticalReceptorState Before, CorticalReceptorState After);
internal sealed record CorticalNeuronFiring(string Instance, int Cell, double TimestampMs);

public sealed partial class StructureEngine
{
    private readonly bool _diagnosticCorticalPrecision;
    private readonly double[]? _preciseCellPostTrace;
    internal Action<CorticalPrecisionLearning>? CorticalPrecisionLearningObserver { get; set; }
    internal Action<CorticalPrecisionInput>? CorticalPrecisionInputObserver { get; set; }
    internal Action<CorticalNeuronFiring>? CorticalNeuronFiringObserver { get; set; }

    internal CorticalPrecisionContact[] CaptureCorticalPrecisionContacts()
    {
        lock (_stateGate)
            return _inboundSynapses.Values.Where(c=>c.CorticalPrecision is not null).OrderBy(c=>c.SynapseId)
                .Select(c=>CorticalPrecisionContact.From(c,_instanceKey)).ToArray();
    }

    private void ValidateCorticalPrecisionMode(int? isolatedNeuronCount, bool warmStart)
    {
        string? directory=Environment.GetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR");
        if (isolatedNeuronCount is not null || !CorticalLaminarTopology.IsCorticalStructure(_profile.StructureId) ||
            !CorticalPrecisionMode.Supports(_profile.PlasticityRule) ||
            string.IsNullOrWhiteSpace(directory) || !Path.IsPathFullyQualified(directory) ||
            !_instanceKey.StartsWith("isolated-",StringComparison.Ordinal) ||
            (!warmStart && Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any()))
            throw new InvalidOperationException("Cortical precision requires a full isolated cortex, supported rule and an empty absolute state directory, or an explicit compatible warm start.");
    }

    private void EnableNewCorticalContact(SynapseState contact, double timeMs)
    {
        if (!_diagnosticCorticalPrecision || contact.IsSyntheticBackground || contact.Neurotransmitter!=NTEnum.GLUTAMATE) return;
        int target=contact.LastTargetNeuronIndex;
        double post=_preciseCellPostTrace![target]*Math.Exp(-(timeMs-_cellPostTimestamp[target])/ContactPlasticity.PostTauMs);
        var initial=CorticalContactNumerics.FromLegacy(MemoryContactEvidence.From(contact)) with {PostTrace=post};
        contact.EnableCorticalPrecision(initial,CorticalPrecisionMode.PairChangesWeight(_profile.PlasticityRule));
    }

    private bool LearnCorticalPrecision(SynapseState contact, ModelNeuron cell, string kind, Guid? messageId,
        double timeMs, double elapsedMs=0)
    {
        if (contact.CorticalPrecision is not { } numerical) return false;
        var before=CorticalPrecisionLearningObserver is null?null:CorticalPrecisionContact.From(contact,_instanceKey);
        var modulation=cell.LocalNeuromodState;
        if (kind=="modulation") numerical.Modulate(timeMs,elapsedMs,modulation.DopamineLevel,
            modulation.AcetylcholineLevel,cell.ActivityTrace,CorticalPrecisionMode.Captures(_profile.PlasticityRule));
        else if (kind is "pre" or "post") numerical.Spike(kind=="pre",timeMs);
        else throw new ArgumentException("Unknown contact event.",nameof(kind));
        contact.RefreshCorticalEvidence();
        if (before is not null) CorticalPrecisionLearningObserver?.Invoke(new(kind,messageId,timeMs,elapsedMs,
            cell.ActivityTrace,modulation.DopamineLevel,modulation.AcetylcholineLevel,before,
            CorticalPrecisionContact.From(contact,_instanceKey)));
        return true;
    }

    internal static double CombineCorticalPrecisionStrength(float release, double weight)
    {
        if (!double.IsFinite(weight) || weight<(double).05f || weight>5)
            throw new ArgumentOutOfRangeException(nameof(weight));
        return Math.Clamp((double)PlasticityRules.ClampQuanta(release)*weight,(double).05f,5);
    }

    private void IntegrateCorticalPrecision(SpikeMessage message, SynapseState contact, ModelNeuron cell,
        double arrivalMs, double timeMs, double dtMs)
    {
        var state=contact.CorticalPrecision!.State;
        double effective=CombineCorticalPrecisionStrength(message.VesicleQuanta,state.Weight);
        var before=CorticalPrecisionInputObserver is null?null:cell.CaptureCorticalReceptors();
        cell.IntegrateCorticalPrecision(message,effective,dtMs);
        if (before is not null) CorticalPrecisionInputObserver?.Invoke(new(message.MessageId,contact.SynapseId,
            arrivalMs,timeMs,cell.Index,message.VesicleQuanta,state.Weight,effective,before,cell.CaptureCorticalReceptors()));
    }
}
