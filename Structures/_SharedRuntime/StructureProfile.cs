using NeuralResonanceEngine.Protocol;

public sealed record StructureProfile(
	StructureId StructureId,
	string NeuronModel,
	string PlasticityRule,
	string BiologicalJustification,
	DelayWindow FeedbackDelay,
	int MaxInboundQueueDepth = 4096);
