using NeuralResonanceEngine.Protocol;

// Opt-in immutable values around actual presynaptic learning. No service installs
// this observer. Capture all contacts so cross-cell credit can be checked too.
internal sealed record ContactLearningSnapshot(Guid ContactId, string SourceInstance,
    StructureId Source, string SourceNeuron, int Target, float Weight, float PreTrace,
    float PostTrace, double TimestampMs, float Budget, double BudgetTimestampMs,
    double AbsoluteChange, int Updates, bool Synthetic)
{
    internal static ContactLearningSnapshot From(SynapseState c) => new(c.SynapseId,
        c.SourceInstanceKey,c.SourceStructure,c.SourceNeuronId,c.LastTargetNeuronIndex,
        c.VesicleQuanta,c.PreTrace,c.PostTrace,c.LastUpdateTimestampMs,c.PlasticityBudgetQuanta,
        c.LastPlasticityBudgetTimestampMs,c.TotalAbsolutePlasticityChange,c.UpdateCount,c.IsSyntheticBackground);
}

internal sealed record ContactLearningObservation(Guid MessageId, Guid ContactId,
    double ScheduledMs, double ProcessedMs, int Target, StructureId Source,
    ContactLearningSnapshot[] Before, ContactLearningSnapshot[] After);
