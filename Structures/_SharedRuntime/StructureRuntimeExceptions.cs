using NeuralResonanceEngine.Protocol;

public sealed class StructureIngressOverloadException(StructureId structureId, int capacity) : InvalidOperationException(
	$"{structureId} inbound spike queue reached its capacity of {capacity} messages.")
{
	public StructureId StructureId { get; } = structureId;
	public int Capacity { get; } = capacity;
}

public sealed class StructureTickSequenceException(StructureId structureId, long receivedTick, long lastProcessedTick) : InvalidOperationException(
	$"{structureId} rejected tick {receivedTick}; last processed tick is {lastProcessedTick}.")
{
	public StructureId StructureId { get; } = structureId;
	public long ReceivedTick { get; } = receivedTick;
	public long LastProcessedTick { get; } = lastProcessedTick;
}
