using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NRE.SimAvatar;

// Material constants from the pinned deposited vestibular model. Geometry is
// deliberately separate: these constants do not qualify a chosen cleft shape.
public sealed record AvatarCleftMaterial(double PotassiumDiffusionSquareMicrometersPerMs,
    double SodiumDiffusionSquareMicrometersPerMs, double ThermalVoltageMv,
    double OtherIonConductivityNsPerMicrometer)
{
    public static AvatarCleftMaterial DepositedReference { get; } = new(.81, .56, 26, 600);

    public void Validate()
    {
        CleftNumbers.Positive(ThermalVoltageMv);
        CleftNumbers.Nonnegative(PotassiumDiffusionSquareMicrometersPerMs);
        CleftNumbers.Nonnegative(SodiumDiffusionSquareMicrometersPerMs);
        CleftNumbers.Nonnegative(OtherIonConductivityNsPerMicrometer);
    }
}

// Face area is perpendicular to transport, not the membrane surface area.
// Two positive control volumes are explicit; no implicit bath or fitted gap.
public sealed record AvatarCleftEdgeGeometry(double FaceAreaSquareMicrometers,
    double CenterDistanceMicrometers, double LeftVolumeCubicMicrometers,
    double RightVolumeCubicMicrometers)
{
    public void Validate()
    {
        CleftNumbers.Positive(FaceAreaSquareMicrometers); CleftNumbers.Positive(CenterDistanceMicrometers);
        CleftNumbers.Positive(LeftVolumeCubicMicrometers); CleftNumbers.Positive(RightVolumeCubicMicrometers);
    }
}

public readonly record struct AvatarCleftSite(double PotentialMv, double PotassiumMm, double SodiumMm)
{
    public void Validate()
    {
        CleftNumbers.Finite(PotentialMv); CleftNumbers.Positive(PotassiumMm); CleftNumbers.Positive(SodiumMm);
    }
}

public readonly record struct AvatarCleftConcentrationRates(double LeftPotassiumMmPerMs,
    double RightPotassiumMmPerMs, double LeftSodiumMmPerMs, double RightSodiumMmPerMs);

// A surface transfer has no reservoir volume. A finite receiving compartment
// converts this amount/current into state; a held bath records the exchange.
public readonly record struct AvatarCleftFaceFlux(double PotassiumAttomolesPerMs,
    double SodiumAttomolesPerMs, double OtherIonCurrentPa,
    double PotassiumElectrochemicalDropMv, double SodiumElectrochemicalDropMv, double PotentialDropMv)
{
    public double PotassiumCurrentPa => AvatarCleftEdgeKernel.ChargePaPerAttomolePerMs * PotassiumAttomolesPerMs;
    public double SodiumCurrentPa => AvatarCleftEdgeKernel.ChargePaPerAttomolePerMs * SodiumAttomolesPerMs;
    public double TotalCurrentPa => PotassiumCurrentPa + SodiumCurrentPa + OtherIonCurrentPa;
    public double DissipationFw => PotassiumCurrentPa * PotassiumElectrochemicalDropMv +
        SodiumCurrentPa * SodiumElectrochemicalDropMv + OtherIonCurrentPa * PotentialDropMv;
}

// Positive flux/current goes left -> right. mM*um^3 is attomoles; conversion
// to pA is 96.485 for monovalent ions. Other ions carry current but their
// concentrations are not explicit state in the deposited reduction.
public readonly record struct AvatarCleftEdgeFlux(double PotassiumAttomolesPerMs,
    double SodiumAttomolesPerMs, double OtherIonCurrentPa,
    AvatarCleftConcentrationRates ConcentrationRates,
    double PotassiumElectrochemicalDropMv, double SodiumElectrochemicalDropMv,
    double PotentialDropMv)
{
    public double PotassiumCurrentPa => AvatarCleftEdgeKernel.ChargePaPerAttomolePerMs * PotassiumAttomolesPerMs;
    public double SodiumCurrentPa => AvatarCleftEdgeKernel.ChargePaPerAttomolePerMs * SodiumAttomolesPerMs;
    public double TotalCurrentPa => PotassiumCurrentPa + SodiumCurrentPa + OtherIonCurrentPa;
    // pA*mV = fW. This is the constitutive dissipation of one edge, not a
    // positivity/energy guarantee for a future time integrator.
    public double DissipationFw => PotassiumCurrentPa * PotassiumElectrochemicalDropMv +
        SodiumCurrentPa * SodiumElectrochemicalDropMv + OtherIonCurrentPa * PotentialDropMv;
}

public sealed class AvatarCleftEdgeKernel
{
    public const string Version = "cleft-monovalent-scharfetter-gummel-edge-v1";
    public const double ChargePaPerAttomolePerMs = 96.485;
    public AvatarCleftMaterial Material { get; }
    public AvatarCleftEdgeGeometry Geometry { get; }
    public string Fingerprint { get; }
    private readonly double _potassiumMobility, _sodiumMobility, _otherConductance;

    public AvatarCleftEdgeKernel(AvatarCleftMaterial material, AvatarCleftEdgeGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(material); ArgumentNullException.ThrowIfNull(geometry);
        material.Validate(); geometry.Validate(); Material = material; Geometry = geometry;
        double ratio = geometry.FaceAreaSquareMicrometers / geometry.CenterDistanceMicrometers;
        CleftNumbers.Positive(ratio);
        _potassiumMobility = Scale(material.PotassiumDiffusionSquareMicrometersPerMs, ratio);
        _sodiumMobility = Scale(material.SodiumDiffusionSquareMicrometersPerMs, ratio);
        _otherConductance = Scale(material.OtherIonConductivityNsPerMicrometer, ratio);
        Fingerprint = CleftNumbers.Fingerprint(new { Version, material, geometry, ChargePaPerAttomolePerMs,
            Field = "constant within edge", OtherIons = "conductivity only; no explicit concentration state" });
    }

    public AvatarCleftEdgeFlux Evaluate(AvatarCleftSite left, AvatarCleftSite right)
    {
        var f = EvaluateFace(Material,_potassiumMobility,_sodiumMobility,_otherConductance,left,right);
        var rates = new AvatarCleftConcentrationRates(-f.PotassiumAttomolesPerMs / Geometry.LeftVolumeCubicMicrometers,
            f.PotassiumAttomolesPerMs / Geometry.RightVolumeCubicMicrometers, -f.SodiumAttomolesPerMs / Geometry.LeftVolumeCubicMicrometers,
            f.SodiumAttomolesPerMs / Geometry.RightVolumeCubicMicrometers);
        ReadOnlySpan<double> values = [rates.LeftPotassiumMmPerMs,rates.RightPotassiumMmPerMs,rates.LeftSodiumMmPerMs,rates.RightSodiumMmPerMs];
        foreach (double value in values) CleftNumbers.Finite(value);
        return new(f.PotassiumAttomolesPerMs,f.SodiumAttomolesPerMs,f.OtherIonCurrentPa,rates,
            f.PotassiumElectrochemicalDropMv,f.SodiumElectrochemicalDropMv,f.PotentialDropMv);
    }

    internal static AvatarCleftFaceFlux EvaluateFace(AvatarCleftMaterial material,
        double potassiumMobility, double sodiumMobility, double otherConductance,
        AvatarCleftSite left, AvatarCleftSite right)
    {
        left.Validate(); right.Validate();
        double drop = left.PotentialMv - right.PotentialMv;
        CleftNumbers.Finite(drop);
        double psi = -drop / material.ThermalVoltageMv;
        CleftNumbers.Finite(psi);
        double k = Flow(left.PotassiumMm, right.PotassiumMm, psi, potassiumMobility);
        double na = Flow(left.SodiumMm, right.SodiumMm, psi, sodiumMobility);
        var result = new AvatarCleftFaceFlux(k, na, otherConductance * drop,
            material.ThermalVoltageMv * (Math.Log(left.PotassiumMm) - Math.Log(right.PotassiumMm)) + drop,
            material.ThermalVoltageMv * (Math.Log(left.SodiumMm) - Math.Log(right.SodiumMm)) + drop, drop);
        ReadOnlySpan<double> values = [k, na, result.OtherIonCurrentPa, result.TotalCurrentPa,
            result.PotassiumCurrentPa, result.SodiumCurrentPa, result.DissipationFw,
            result.PotassiumElectrochemicalDropMv, result.SodiumElectrochemicalDropMv];
        foreach (double value in values) CleftNumbers.Finite(value);
        return result;
    }

    private static double Flow(double left, double right, double psi, double mobility)
    {
        if (mobility == 0) return 0;
        // B(-x)=B(x)+x avoids large exponentials. The difference form retains
        // tiny electric drift at equal concentrations without subtracting two
        // almost equal Bernoulli values. No clipping of concentrations or flux.
        double b = BernoulliPositive(Math.Abs(psi));
        double difference = left - right;
        return mobility * (psi >= 0
            ? Math.FusedMultiplyAdd(-psi, right, b * difference)
            : Math.FusedMultiplyAdd(-psi, left, b * difference));
    }

    private static double BernoulliPositive(double x)
    {
        if (x < .001)
        {
            double x2 = x * x;
            return 1 - x / 2 + x2 * (1.0 / 12 + x2 * (-1.0 / 720 +
                x2 * (1.0 / 30240 - x2 / 1209600)));
        }
        double e = Math.Exp(-x);
        return x * e / (1 - e);
    }

    internal static double Scale(double value, double ratio)
    {
        if (value == 0) return 0;
        double result = value * ratio; CleftNumbers.Positive(result); return result;
    }
}

internal static class CleftNumbers
{
    internal static void Finite(double value)
    {
        if (!double.IsFinite(value)) throw new ArgumentException("Cleft state or derived quantity exceeds the finite range.");
    }
    internal static void Positive(double value)
    {
        if (!double.IsFinite(value) || value <= 0) throw new ArgumentException("Explicit positive finite cleft parameter required.");
    }
    internal static void Nonnegative(double value)
    {
        if (!double.IsFinite(value) || value < 0) throw new ArgumentException("Explicit nonnegative finite cleft parameter required.");
    }
    internal static string Fingerprint<T>(T value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
}
