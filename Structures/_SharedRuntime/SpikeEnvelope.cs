using NeuralResonanceEngine.Protocol;

internal sealed record SpikeEnvelope(SpikeMessage Message, double DeliverAtTimestampMs);
