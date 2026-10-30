using NeuralResonanceEngine.Shared.Contracts;

// Reduced ATP/ADP feedback, not ATP concentration or a calibrated metabolic flux.
// Supply represents extracellular availability; body joules never become cell ATP.
internal sealed class NeuralEnergyBudget
{
    public const double OxidativeCapacityPerSecond = .45;
    public const double GlycolyticCapacityPerSecond = .05;
    public const double BasalDemandPerSecond = .01;
    public double Reserve { get; private set; } = 1;
    public double ProductionActivation { get; private set; } = .02;
    public double DemandRate { get; private set; } = BasalDemandPerSecond;
    public double Produced { get; private set; }
    public double Requested { get; private set; }
    public double Consumed { get; private set; }
    public double Unmet { get; private set; }
    public double Overflow { get; private set; }
    public double BalanceError => Reserve - (1 + Produced - Consumed - Overflow);
    // ATP-sensitive outward potassium feedback, provisional density/affinity.
    // Fixed ion gradients mean this does NOT model terminal pump failure/injury.
    public double PotassiumConductance => 1.5 * Math.Pow(1 - Reserve, 4) /
        (Math.Pow(.3, 4) + Math.Pow(1 - Reserve, 4));

    public void Advance(double dtMs, double activityCost, NeuralSubstrateSupply supply)
    {
        if (!double.IsFinite(dtMs) || dtMs <= 0 || !double.IsFinite(activityCost) || activityCost < 0 || !supply.IsValid)
            throw new ArgumentOutOfRangeException(nameof(dtMs));
        double seconds = dtMs / 1000;
        double demand = activityCost + BasalDemandPerSecond * seconds;
        DemandRate += (demand / seconds - DemandRate) * (1 - Math.Exp(-dtMs / 200));
        double deficit = 1 - Reserve;
        double target = Math.Clamp(DemandRate / .5 + .5 * deficit / (.1 + deficit), 0, 1);
        ProductionActivation += (target - ProductionActivation) * (1 - Math.Exp(-dtMs / 250));
        double production = seconds * ProductionActivation * supply.GlucoseAvailability * supply.PerfusionAvailability *
            (OxidativeCapacityPerSecond * supply.OxygenAvailability + GlycolyticCapacityPerSecond);
        Produced += production;
        Requested += demand;
        double consumed = Math.Min(Reserve + production, demand);
        Consumed += consumed;
        Unmet += demand - consumed;
        double next = Reserve + production - consumed;
        Overflow += Math.Max(0, next - 1);
        Reserve = Math.Min(1, next);
    }
}
