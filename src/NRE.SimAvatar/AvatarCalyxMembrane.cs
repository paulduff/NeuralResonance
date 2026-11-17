using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NRE.SimAvatar;

// Reduced inner/outer calyx membrane terms, independently expressed from the
// deposited Govindaraju et al. model. This explicitly uses the stored HCN2
// convention, which differs from the printed supplement; see the source audit.
// No voltage integrator, cleft dynamics, axial cable or spike generator is hidden
// here. The caller must supply both actual local voltages and gate states.
public sealed record AvatarCalyxParameters(
    double InnerAreaSquareMicrometers, double OuterAreaSquareMicrometers,
    double SpecificCapacitancePfPerSquareMicrometer, double Kv74MaximumNs,
    double Kv74InnerFraction, double Hcn2MaximumNs, double Hcn2InnerFraction,
    double InnerPumpDensityPerSquareMicrometer, double OuterPumpDensityPerSquareMicrometer,
    double InnerKccMaximumPaPerSquareMicrometer, double OuterLeakNs)
{
    public static AvatarCalyxParameters DepositedReference { get; } = new(
        294.63, 288.33, .01, 24, .8, 3.7, .5, 1000, 2000, 2, 2);

    public void Validate()
    {
        double[] positive = [InnerAreaSquareMicrometers, OuterAreaSquareMicrometers,
            SpecificCapacitancePfPerSquareMicrometer];
        double[] nonnegative = [Kv74MaximumNs, Hcn2MaximumNs, InnerPumpDensityPerSquareMicrometer,
            OuterPumpDensityPerSquareMicrometer, InnerKccMaximumPaPerSquareMicrometer, OuterLeakNs];
        if (positive.Any(x => !double.IsFinite(x) || x <= 0) ||
            nonnegative.Any(x => !double.IsFinite(x) || x < 0))
            throw new ArgumentException("Finite positive geometry and nonnegative membrane parameters required.");
        AvatarCalyxKernel.Probability(Kv74InnerFraction); AvatarCalyxKernel.Probability(Hcn2InnerFraction);
    }
}

public readonly record struct AvatarCalyxGates(double InnerKv74, double InnerHcn2,
    double OuterKv74, double OuterHcn2)
{
    public void Validate()
    {
        AvatarCalyxKernel.Probability(InnerKv74); AvatarCalyxKernel.Probability(InnerHcn2);
        AvatarCalyxKernel.Probability(OuterKv74); AvatarCalyxKernel.Probability(OuterHcn2);
    }
}

public readonly record struct AvatarCalyxGateRate(double Equilibrium, double TauMs);
public readonly record struct AvatarCalyxFaceRates(AvatarCalyxGateRate Kv74, AvatarCalyxGateRate Hcn2);
public readonly record struct AvatarCalyxRates(AvatarCalyxFaceRates Inner, AvatarCalyxFaceRates Outer);

// Currents are whole-face pA, positive OUT OF the calyx intracellular space.
// Inner-face currents therefore enter the cleft; outer-face currents enter the
// perilymph. KCC transports ions without net charge; do not erase its K flux
// merely because the paired electrical currents sum to zero. Leak has no
// assigned ionic species in this reduced source and is recorded separately.
public readonly record struct AvatarCalyxFaceCurrents(double Kv74PotassiumPa,
    double HcnPotassiumPa, double HcnSodiumPa, double PumpPotassiumPa,
    double PumpSodiumPa, double KccPotassiumPa, double KccChloridePa,
    double UnassignedLeakPa)
{
    public double PotassiumOutwardPa => Kv74PotassiumPa + HcnPotassiumPa + PumpPotassiumPa + KccPotassiumPa;
    public double SodiumOutwardPa => HcnSodiumPa + PumpSodiumPa;
    public double TotalOutwardPa => Kv74PotassiumPa + HcnPotassiumPa + HcnSodiumPa +
        PumpPotassiumPa + PumpSodiumPa + (KccPotassiumPa + KccChloridePa) + UnassignedLeakPa;
}

public readonly record struct AvatarCalyxEvaluation(double InnerVoltageMv, double OuterVoltageMv,
    double InnerPotassiumReversalMv, double InnerSodiumReversalMv,
    AvatarCalyxRates Rates, AvatarCalyxFaceCurrents Inner, AvatarCalyxFaceCurrents Outer)
{
    public double TotalOutwardPa => Inner.TotalOutwardPa + Outer.TotalOutwardPa;
}

public sealed class AvatarCalyxKernel
{
    public const string Version = "isolated-calyx-membranes-deposited-20221205-v1";
    // The deposited outer-face expressions contain these rounded constants.
    // Preserve them literally; do not quietly replace them with inner Nernst
    // calculations and describe the result as the same numerical source.
    public const double OuterPotassiumReversalMv = -88.4311;
    public const double OuterSodiumReversalMv = 63.8751;
    public const double OuterPotassiumMm = 5;
    public const double IntracellularPotassiumMm = 150;
    public const double IntracellularSodiumMm = 12;
    public AvatarCalyxParameters Parameters { get; }
    public double InnerCapacitancePf { get; }
    public double OuterCapacitancePf { get; }
    public string Fingerprint { get; }

    public AvatarCalyxKernel(AvatarCalyxParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters); parameters.Validate(); Parameters = parameters;
        InnerCapacitancePf = parameters.SpecificCapacitancePfPerSquareMicrometer * parameters.InnerAreaSquareMicrometers;
        OuterCapacitancePf = parameters.SpecificCapacitancePfPerSquareMicrometer * parameters.OuterAreaSquareMicrometers;
        if (!double.IsFinite(InnerCapacitancePf) || !double.IsFinite(OuterCapacitancePf))
            throw new ArgumentException("Membrane areas exceed the finite numerical range.");
        Fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new {
            Version, parameters, OuterPotassiumReversalMv, OuterSodiumReversalMv, OuterPotassiumMm,
            IntracellularPotassiumMm, IntracellularSodiumMm,
            Temperature = "unscaled room-temperature source kinetics", ErgAndSodiumChannels = "source net currents zero",
            Metabolism = "pump uses held intracellular reservoirs; no ATP model", OuterPotential = "perilymph ground"
        }))));
    }

    public static AvatarCalyxFaceRates FaceRates(double membraneVoltageMv)
    {
        Finite(membraneVoltageMv); double v = membraneVoltageMv;
        // The one-ms minimum is part of the source's empirical Kv7.4 fit,
        // not a solver stabilization or post-hoc clipping of channel state.
        double kvSum = .0002488 * Math.Exp(-.04401 * v) + .4506 * Math.Exp(.05437 * v);
        if (!double.IsFinite(kvSum) || kvSum <= 0) throw new ArithmeticException("Invalid Kv7.4 rate sum.");
        var result = new AvatarCalyxFaceRates(new(Sigmoid((v + 52) / 16), Math.Max(1 / kvSum, 1)),
            new(Sigmoid(-(v + 95) / 11.7),
                209.479 + (Math.Exp((v + 80.646) / 6.916) + Math.Exp((v + 80.646) / 14.881)) / 2551.988));
        Validate(result.Kv74); Validate(result.Hcn2); return result;
    }

    public static AvatarCalyxRates Rates(double calyxPotentialMv, double cleftPotentialMv)
    {
        Finite(calyxPotentialMv); Finite(cleftPotentialMv);
        return new(FaceRates(calyxPotentialMv - cleftPotentialMv), FaceRates(calyxPotentialMv));
    }

    public static AvatarCalyxGates VoltageConditionedGates(double calyxPotentialMv, double cleftPotentialMv)
    {
        var r = Rates(calyxPotentialMv, cleftPotentialMv);
        return new(r.Inner.Kv74.Equilibrium, r.Inner.Hcn2.Equilibrium,
            r.Outer.Kv74.Equilibrium, r.Outer.Hcn2.Equilibrium);
    }

    public AvatarCalyxEvaluation Evaluate(double calyxPotentialMv, double cleftPotentialMv,
        double cleftPotassiumMm, double cleftSodiumMm, AvatarCalyxGates gates)
    {
        gates.Validate(); var rates = Rates(calyxPotentialMv, cleftPotentialMv);
        Positive(cleftPotassiumMm); Positive(cleftSodiumMm);
        var p = Parameters;
        double innerV = calyxPotentialMv - cleftPotentialMv;
        double ek = 26 * (Math.Log(cleftPotassiumMm) - Math.Log(IntracellularPotassiumMm));
        double ena = 26 * (Math.Log(cleftSodiumMm) - Math.Log(IntracellularSodiumMm));
        double innerPump = Pump(cleftPotassiumMm, p.InnerPumpDensityPerSquareMicrometer, p.InnerAreaSquareMicrometers);
        double outerPump = Pump(OuterPotassiumMm, p.OuterPumpDensityPerSquareMicrometer, p.OuterAreaSquareMicrometers);
        // Equivalent to 1/(1+(17.5/K)^2), without squaring a huge reciprocal.
        double kccOccupancy = cleftPotassiumMm >= 17.5
            ? 1 / (1 + Math.Pow(17.5 / cleftPotassiumMm, 2))
            : Math.Pow(cleftPotassiumMm / 17.5, 2) / (1 + Math.Pow(cleftPotassiumMm / 17.5, 2));
        double kcc = p.InnerKccMaximumPaPerSquareMicrometer * p.InnerAreaSquareMicrometers * kccOccupancy;
        double innerHcn = p.Hcn2MaximumNs * p.Hcn2InnerFraction * gates.InnerHcn2;
        double outerHcn = p.Hcn2MaximumNs * (1 - p.Hcn2InnerFraction) * gates.OuterHcn2;
        var inner = new AvatarCalyxFaceCurrents(
            p.Kv74MaximumNs * p.Kv74InnerFraction * gates.InnerKv74 * (innerV - ek),
            .8 * innerHcn * (innerV - ek), .2 * innerHcn * (innerV - ena),
            -2 * innerPump, 3 * innerPump, -kcc, kcc, 0);
        var outer = new AvatarCalyxFaceCurrents(
            p.Kv74MaximumNs * (1 - p.Kv74InnerFraction) * gates.OuterKv74 * (calyxPotentialMv - OuterPotassiumReversalMv),
            .8 * outerHcn * (calyxPotentialMv - OuterPotassiumReversalMv),
            .2 * outerHcn * (calyxPotentialMv - OuterSodiumReversalMv),
            -2 * outerPump, 3 * outerPump, 0, 0, p.OuterLeakNs * calyxPotentialMv);
        Validate(inner); Validate(outer);
        var result = new AvatarCalyxEvaluation(innerV, calyxPotentialMv, ek, ena, rates, inner, outer);
        if (!double.IsFinite(result.TotalOutwardPa)) throw new ArithmeticException("Nonfinite summed calyx current.");
        return result;
    }

    // Analytic voltage-clamp transition only. A freely moving membrane/cleft
    // must integrate gates jointly with its voltage; it cannot use future
    // endpoint voltage to fill an elapsed physical interval.
    public static AvatarCalyxGates RelaxVoltageClamp(AvatarCalyxGates gates,
        double calyxPotentialMv, double cleftPotentialMv, double elapsedMs)
    {
        gates.Validate(); Finite(elapsedMs);
        if (elapsedMs < 0) throw new ArgumentOutOfRangeException(nameof(elapsedMs));
        var r = Rates(calyxPotentialMv, cleftPotentialMv);
        if (elapsedMs == 0) return gates;
        static double Relax(double x, AvatarCalyxGateRate rate, double dt) =>
            rate.Equilibrium + (x - rate.Equilibrium) * Math.Exp(-dt / rate.TauMs);
        var result = new AvatarCalyxGates(Relax(gates.InnerKv74,r.Inner.Kv74,elapsedMs),
            Relax(gates.InnerHcn2,r.Inner.Hcn2,elapsedMs), Relax(gates.OuterKv74,r.Outer.Kv74,elapsedMs),
            Relax(gates.OuterHcn2,r.Outer.Hcn2,elapsedMs));
        result.Validate(); return result;
    }

    private static double Pump(double potassiumMm, double density, double area)
    {
        double occupancy = potassiumMm / (potassiumMm + 1.5);
        return (96.485 / 602000) * .1 * density * area * occupancy * occupancy;
    }
    private static double Sigmoid(double x)
    {
        if (x >= 0) return 1 / (1 + Math.Exp(-x));
        double e = Math.Exp(x); return e / (1 + e);
    }
    private static void Validate(AvatarCalyxFaceCurrents c)
    {
        ReadOnlySpan<double> values = [c.Kv74PotassiumPa, c.HcnPotassiumPa, c.HcnSodiumPa, c.PumpPotassiumPa,
            c.PumpSodiumPa, c.KccPotassiumPa, c.KccChloridePa, c.UnassignedLeakPa,
            c.PotassiumOutwardPa, c.SodiumOutwardPa, c.TotalOutwardPa];
        foreach (double value in values)
            if (!double.IsFinite(value)) throw new ArithmeticException("Nonfinite calyx current; do not clip.");
    }
    private static void Validate(AvatarCalyxGateRate rate)
    {
        Probability(rate.Equilibrium);
        if (!double.IsFinite(rate.TauMs) || rate.TauMs <= 0) throw new ArithmeticException("Invalid calyx gate time constant.");
    }
    internal static void Probability(double x)
    {
        if (!double.IsFinite(x) || x is < 0 or > 1) throw new ArgumentException("Finite gate probability required; no clipping.");
    }
    private static void Positive(double x)
    {
        if (!double.IsFinite(x) || x <= 0) throw new ArgumentException("Explicit positive finite concentration required.");
    }
    private static void Finite(double x)
    {
        if (!double.IsFinite(x)) throw new ArgumentException("Explicit finite electrical state required.");
    }
}
