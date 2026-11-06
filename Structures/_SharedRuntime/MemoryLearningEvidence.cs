using NeuralResonanceEngine.Protocol;

// Opt-in evidence for the cortical fixture investigation. These records expose
// existing state; they are not a new learning/consolidation mechanism.
internal sealed record MemoryContactEvidence(Guid ContactId, Guid ProjectionId,
    string SourceInstance, StructureId Source, string SourceNeuron, int Target,
    NTEnum Neurotransmitter, bool Synthetic, float Baseline, float Weight,
    float PreTrace, float PostTrace, float Eligibility, float Tag, float Theta,
    int Updates, double TimestampMs, float Budget, double BudgetTimestampMs, double AbsoluteChange)
{
    internal static MemoryContactEvidence From(SynapseState c) => new(c.SynapseId, c.ProjectionId,
        c.SourceInstanceKey, c.SourceStructure, c.SourceNeuronId, c.LastTargetNeuronIndex,
        c.Neurotransmitter, c.IsSyntheticBackground, c.BaselineVesicleQuanta, c.VesicleQuanta,
        c.PreTrace, c.PostTrace, c.EligibilityTrace, c.SynapticTagTrace, c.ThetaM, c.UpdateCount,
        c.LastUpdateTimestampMs, c.PlasticityBudgetQuanta, c.LastPlasticityBudgetTimestampMs, c.TotalAbsolutePlasticityChange);
}

internal sealed record MemoryLearningEvidence(string Kind, Guid? MessageId, double TimestampMs, double ElapsedMs,
    float Activity, float Dopamine, float Acetylcholine, float Teaching,
    MemoryContactEvidence Before, MemoryContactEvidence After);

public sealed partial class StructureEngine
{
    internal Action<MemoryLearningEvidence>? MemoryLearningObserver { get; set; }

    internal MemoryContactEvidence[] CaptureMemoryContactEvidence()
    {
        lock (_stateGate)
            return _inboundSynapses.Values.OrderBy(c => c.SynapseId).Select(MemoryContactEvidence.From).ToArray();
    }

    private void ObserveMemoryLearning(string kind, Guid? messageId, double timestampMs, double elapsedMs,
        ModelNeuron cell, MemoryContactEvidence? before, SynapseState contact)
    {
        if (before is null || MemoryLearningObserver is not { } observer) return;
        var modulation = cell.LocalNeuromodState;
        observer(new(kind, messageId, timestampMs, elapsedMs, cell.ActivityTrace,
            modulation.DopamineLevel, modulation.AcetylcholineLevel, cell.LocalRewardSignal,
            before, MemoryContactEvidence.From(contact)));
    }
}
