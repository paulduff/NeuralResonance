namespace NeuralResonanceEngine.Shared.Contracts;

// Dimensionless boundary availability, not intracellular ATP or blood concentration.
public sealed record NeuralSubstrateSupply(double GlucoseAvailability, double OxygenAvailability,
    double PerfusionAvailability, string Source)
{
    public bool IsValid => Unit(GlucoseAvailability) && Unit(OxygenAvailability) &&
        Unit(PerfusionAvailability) && !string.IsNullOrWhiteSpace(Source) && Source.Length <= 128;
    static bool Unit(double x) => double.IsFinite(x) && x >= 0 && x <= 1;
    public static NeuralSubstrateSupply ReferenceBath { get; } = new(1, 1, 1, "isolated-reference-bath");
    public static NeuralSubstrateSupply Missing { get; } = new(0, 0, 0, "missing-supply");
}

public sealed record NeuralEnergyDiagnostics(string SupplySource, int CellCount,
    double MeanReserve, double MinimumReserve, double Produced, double Consumed,
    double Unmet, double Overflow, double MaximumBalanceError);
