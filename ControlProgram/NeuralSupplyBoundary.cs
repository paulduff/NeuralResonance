using NeuralResonanceEngine.Shared.Contracts;

// Receiver clocks own freshness. A sender's wall clock cannot keep old fuel alive.
internal sealed class NeuralSupplyBoundary
{
    NeuralSubstrateSupply? _supply;
    double _simulationMs;
    long _monotonicMs;
    public void Observe(NeuralSubstrateSupply? supply, double simulationMs, long monotonicMs)
    {
        if (supply is { IsValid: false }) throw new ArgumentException("Invalid substrate supply.");
        _supply = supply;
        _simulationMs = simulationMs;
        _monotonicMs = monotonicMs;
    }
    public NeuralSubstrateSupply Resolve(double simulationMs, long monotonicMs)
    {
        if (_supply is null) return NeuralSubstrateSupply.Missing;
        if (simulationMs < _simulationMs || monotonicMs < _monotonicMs ||
            simulationMs - _simulationMs > 1000 || monotonicMs - _monotonicMs > 5000)
            return new(0, 0, 0, "stale-body-supply");
        return _supply;
    }
}
