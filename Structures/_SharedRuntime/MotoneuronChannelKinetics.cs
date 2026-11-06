// Isolated Powers/Heckman 2017 reference candidate; no live host selects it.
// Equations independently expressed from ModelDB 239582, revision
// 0d11354490fad5fcd940d016a8d69b000693fd81. See the accompanying reference audit.
// mV, ms, mM, degrees C. Analytic rates, not NEURON's 1 mV lookup tables.
internal readonly record struct MotoneuronGateRate(double Equilibrium, double TauMs);

internal sealed record MotoneuronChannelParameters(
    double TemperatureCelsius = 37, double TransientSodiumShiftMv = 1,
    double PersistentSodiumShiftMv = 5, double TransientSodiumAvailability = 1,
    double PersistentSodiumAvailability = 1, double CalciumLActivationHalfMv = -40,
    double CalciumLInactivationHalfMv = 8)
{
    public void Validate()
    {
        double[] finite = [TemperatureCelsius, TransientSodiumShiftMv, PersistentSodiumShiftMv,
            TransientSodiumAvailability, PersistentSodiumAvailability, CalciumLActivationHalfMv, CalciumLInactivationHalfMv];
        if (finite.Any(x => !double.IsFinite(x)) || TemperatureCelsius is < 0 or > 50 ||
            TransientSodiumAvailability is < 0 or > 1 || PersistentSodiumAvailability is < 0 or > 1)
            throw new ArgumentException("Invalid declared motoneuron channel parameters.");
    }
}

internal static class MotoneuronChannelKinetics
{
    public const double RestingCalciumMm = .0001;
    public const double SodiumReversalMv = 50;
    public const double PotassiumReversalMv = -80;
    public const double HcnReversalMv = -41;
    public const double CalciumLReversalMv = 80;
    // ca concentration is LOCAL to mAHP.mod, not USEION ca WRITE cai.
    // Therefore eca remains the simulator's fixed parameter, not a Nernst
    // function of that local pool. NEURON 8.2.4 membdef.h: 12.5*log(2/5e-5).
    public static readonly double SomaticCalciumReversalMv = 12.5 * Math.Log(40000);
    public const double FaradayCoulombsPerMole = 96485.33212;
    public const double CalciumShellDepthUm = .1;

    public static double Sigmoid(double x)
    {
        if (x >= 0) return 1 / (1 + Math.Exp(-x));
        double e = Math.Exp(x); return e / (1 + e);
    }

    // Continuous removable singularity, with a local series rather than the
    // deposited mechanism's tiny constant band around v == threshold.
    public static double SodiumRate(double voltage, double threshold, double scale, double slope)
    {
        double z = (voltage - threshold) / slope;
        return scale * slope * (Math.Abs(z) < 1e-5 ? 1 + z / 2 + z * z / 12 - Math.Pow(z, 4) / 720 :
            z / (1 - Math.Exp(-z)));
    }

    private static MotoneuronGateRate Availability(double voltage, double halfA, double halfB, double residual)
    {
        double a = .001 * Math.Exp((halfA - voltage) / 30), b = .0034 * Sigmoid((voltage - halfB) / 10);
        double fraction = a / (a + b);
        return new(fraction + residual * (1 - fraction), 1 / (a + b));
    }

    // Order: Na m,h,s; persistent Na m,s; delayed K; high-threshold Ca; KCa; HCN.
    public static void Soma(double voltage, double calciumMm, MotoneuronChannelParameters p, Span<MotoneuronGateRate> rates)
    {
        if (rates.Length < 9 || !double.IsFinite(voltage) || !double.IsFinite(calciumMm) || calciumMm < 0)
            throw new ArgumentException("Finite voltage/calcium and nine rate slots required.");
        double qt = Math.Pow(2, (p.TemperatureCelsius - 24) / 10), shift = p.TransientSodiumShiftMv;
        double a = SodiumRate(voltage, -30 + shift, .4, 7.2);
        double b = SodiumRate(-voltage, 30 - shift, .124, 7.2);
        rates[0] = new(a / (a + b), Math.Max(.02, 1 / (a + b) / qt));
        a = SodiumRate(voltage, -45 + shift, .03, 1.5);
        b = SodiumRate(-voltage, 45 - shift, .01, 1.5);
        rates[1] = new(Sigmoid(-(voltage + 50 - shift) / 8), Math.Max(.5, 1 / (a + b) / qt));
        rates[2] = Availability(voltage, -85, -17, p.TransientSodiumAvailability);
        rates[3] = new(Sigmoid((voltage + 52.3 - p.PersistentSodiumShiftMv) / 5), 1);
        rates[4] = Availability(voltage, -90, -22, p.PersistentSodiumAvailability);
        double activation = Sigmoid((voltage + 39) / 5.5);
        rates[5] = new(Sigmoid((voltage + 21) / 20), .8 + 20 * activation * (1 - activation));
        rates[6] = new(Sigmoid((voltage + 20) / 4), 2);
        double alpha = .1 * Math.Pow(1000 * (calciumMm - RestingCalciumMm), 2);
        rates[7] = new(alpha / (alpha + .1), 1 / (alpha + .1));
        rates[8] = Hcn(voltage);
    }

    public static MotoneuronGateRate Hcn(double voltage) => new(Sigmoid(-(voltage + 77) / 8), 30);
    // Order: L-type calcium m,h; HCN. The deposited tadj variable is unused;
    // do not add an undeclared temperature factor to these time constants.
    public static void Dendrite(double voltage, MotoneuronChannelParameters p, Span<MotoneuronGateRate> rates)
    {
        if (rates.Length < 3 || !double.IsFinite(voltage)) throw new ArgumentException("Finite voltage and three rate slots required.");
        rates[0] = new(Sigmoid((voltage - p.CalciumLActivationHalfMv) / 6), 40);
        rates[1] = new(Sigmoid(-(voltage - p.CalciumLInactivationHalfMv) / 5), 2500);
        rates[2] = Hcn(voltage);
    }

    public static double Relax(double gate, MotoneuronGateRate rate, double dtMs)
    {
        if (!double.IsFinite(gate) || gate is < 0 or > 1 || !double.IsFinite(dtMs) || dtMs < 0 ||
            !double.IsFinite(rate.Equilibrium) || rate.Equilibrium is < 0 or > 1 || !double.IsFinite(rate.TauMs) || rate.TauMs <= 0)
            throw new ArgumentException("Invalid clamped gate state or rate.");
        return rate.Equilibrium + (gate - rate.Equilibrium) * Math.Exp(-dtMs / rate.TauMs);
    }
}
