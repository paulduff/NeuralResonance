namespace NRE.SimAvatar;

public sealed record AvatarCleftCapacitances(double HairApicalPf, double HairPerilymphPf,
    double HairCleftPf, double CalyxInnerPf, double CalyxOuterPf)
{
    public void Validate()
    {
        CleftNumbers.Positive(HairApicalPf); CleftNumbers.Nonnegative(HairPerilymphPf);
        CleftNumbers.Positive(HairCleftPf); CleftNumbers.Positive(CalyxInnerPf); CleftNumbers.Positive(CalyxOuterPf);
    }

    public static AvatarCleftCapacitances FromMembraneGeometry(AvatarHairCellParameters hair, AvatarCalyxParameters calyx)
    {
        ArgumentNullException.ThrowIfNull(hair); ArgumentNullException.ThrowIfNull(calyx);
        hair.Validate(); calyx.Validate();
        if (hair.CleftFacingAreaSquareMicrometers != calyx.InnerAreaSquareMicrometers)
            throw new ArgumentException("The two facing membranes must describe the same declared patch area.");
        var result = new AvatarCleftCapacitances(hair.ApicalCapacitancePf,
            (hair.BasolateralAreaSquareMicrometers - hair.CleftFacingAreaSquareMicrometers) * hair.SpecificCapacitancePfPerSquareMicrometer,
            hair.CleftFacingAreaSquareMicrometers * hair.SpecificCapacitancePfPerSquareMicrometer,
            calyx.InnerAreaSquareMicrometers * calyx.SpecificCapacitancePfPerSquareMicrometer,
            calyx.OuterAreaSquareMicrometers * calyx.SpecificCapacitancePfPerSquareMicrometer);
        result.Validate(); return result;
    }
}

public readonly record struct AvatarCleftPotentials(double HairMv, double CleftMv, double CalyxMv,
    double EndolymphMv, double PerilymphMv)
{
    public void Validate()
    {
        ReadOnlySpan<double> values = [HairMv, CleftMv, CalyxMv, EndolymphMv, PerilymphMv];
        foreach (double value in values) CleftNumbers.Finite(value);
    }
}

// Currents explicitly name their direction. The cleft term can be a sum of
// outgoing spatial edges. An axonal boundary remains an external port here;
// there is no hidden current injection, stimulus or sensory spike generator.
public readonly record struct AvatarCleftPortCurrents(double HairToEndolymphPa,
    double HairToPerilymphPa, double HairToCleftPa, double CalyxToCleftPa,
    double CalyxToPerilymphPa, double CleftOutgoingPa, double CalyxToAxonPa)
{
    public void Validate()
    {
        ReadOnlySpan<double> values = [HairToEndolymphPa, HairToPerilymphPa, HairToCleftPa,
            CalyxToCleftPa, CalyxToPerilymphPa, CleftOutgoingPa, CalyxToAxonPa];
        foreach (double value in values) CleftNumbers.Finite(value);
    }
    public double HairChargeRatePa => -(HairToEndolymphPa + HairToPerilymphPa + HairToCleftPa);
    public double CleftChargeRatePa => HairToCleftPa + CalyxToCleftPa - CleftOutgoingPa;
    public double CalyxChargeRatePa => -(CalyxToCleftPa + CalyxToPerilymphPa + CalyxToAxonPa);
}

public readonly record struct AvatarCleftVoltageRates(double HairMvPerMs, double CleftMvPerMs, double CalyxMvPerMs);
public readonly record struct AvatarCleftCharges(double HairFc, double CleftFc, double CalyxFc,
    double EndolymphFc, double PerilymphFc);
public readonly record struct AvatarCleftChargeRates(double HairPa, double CleftPa, double CalyxPa,
    double EndolymphPa, double PerilymphPa);

// Simultaneous local membrane capacitance equations; not three independent
// I/C updates. All capacitances are explicit and fixed; bath potentials are
// held during a derivative evaluation. There is no time integration here.
public sealed class AvatarCleftChargeBalance
{
    public const string Version = "hair-cleft-calyx-capacitance-block-v1";
    public AvatarCleftCapacitances Capacitances { get; }
    public string Fingerprint { get; }
    private readonly double _hairTotal, _calyxTotal, _hairRatio, _calyxRatio, _cleftSchur;

    public AvatarCleftChargeBalance(AvatarCleftCapacitances capacitances)
    {
        ArgumentNullException.ThrowIfNull(capacitances); capacitances.Validate(); Capacitances = capacitances;
        double outside = capacitances.HairApicalPf + capacitances.HairPerilymphPf;
        _hairTotal = outside + capacitances.HairCleftPf;
        _calyxTotal = capacitances.CalyxInnerPf + capacitances.CalyxOuterPf;
        CleftNumbers.Positive(outside); CleftNumbers.Positive(_hairTotal); CleftNumbers.Positive(_calyxTotal);
        _hairRatio = capacitances.HairCleftPf / _hairTotal;
        _calyxRatio = capacitances.CalyxInnerPf / _calyxTotal;
        CleftNumbers.Positive(_hairRatio); CleftNumbers.Positive(_calyxRatio);
        // Algebraically identical to Ch+Ci-Ch^2/A-Ci^2/C, without subtractive
        // cancellation. No artificial grounding or diagonal regularization.
        _cleftSchur = _hairRatio * outside + _calyxRatio * capacitances.CalyxOuterPf;
        CleftNumbers.Positive(_cleftSchur);
        Fingerprint = CleftNumbers.Fingerprint(new { Version, capacitances, Baths = "held endolymph/perilymph potentials" });
    }

    public AvatarCleftVoltageRates Solve(AvatarCleftPortCurrents currents)
    {
        currents.Validate();
        double h = currents.HairChargeRatePa, s = currents.CleftChargeRatePa, c = currents.CalyxChargeRatePa;
        CleftNumbers.Finite(h); CleftNumbers.Finite(s); CleftNumbers.Finite(c);
        double ds = (s + _hairRatio * h + _calyxRatio * c) / _cleftSchur;
        double dh = h / _hairTotal + _hairRatio * ds;
        double dc = c / _calyxTotal + _calyxRatio * ds;
        CleftNumbers.Finite(dh); CleftNumbers.Finite(ds); CleftNumbers.Finite(dc);
        return new(dh, ds, dc);
    }

    public AvatarCleftCharges Charges(AvatarCleftPotentials v)
    {
        v.Validate(); var p = Capacitances;
        double apical = p.HairApicalPf * (v.HairMv - v.EndolymphMv);
        double exposed = p.HairPerilymphPf * (v.HairMv - v.PerilymphMv);
        double hair = p.HairCleftPf * (v.HairMv - v.CleftMv);
        double inner = p.CalyxInnerPf * (v.CalyxMv - v.CleftMv);
        double outer = p.CalyxOuterPf * (v.CalyxMv - v.PerilymphMv);
        var result = new AvatarCleftCharges(apical + exposed + hair, -hair - inner, inner + outer, -apical, -exposed - outer);
        ReadOnlySpan<double> values = [apical, exposed, hair, inner, outer,
            result.HairFc, result.CleftFc, result.CalyxFc, result.EndolymphFc, result.PerilymphFc];
        foreach (double value in values) CleftNumbers.Finite(value);
        return result;
    }

    // A linear capacitance matrix has the same coefficients for charge rates;
    // the two held baths have zero voltage rate, not zero capacitive current.
    public AvatarCleftChargeRates ChargeRates(AvatarCleftVoltageRates v)
    {
        var q = Charges(new(v.HairMvPerMs, v.CleftMvPerMs, v.CalyxMvPerMs, 0, 0));
        return new(q.HairFc, q.CleftFc, q.CalyxFc, q.EndolymphFc, q.PerilymphFc);
    }

    public double StoredEnergyAttojoules(AvatarCleftPotentials v)
    {
        v.Validate(); var p = Capacitances;
        static double Squared(double x) => x * x;
        double value = .5 * (p.HairApicalPf * Squared(v.HairMv - v.EndolymphMv) +
            p.HairPerilymphPf * Squared(v.HairMv - v.PerilymphMv) +
            p.HairCleftPf * Squared(v.HairMv - v.CleftMv) +
            p.CalyxInnerPf * Squared(v.CalyxMv - v.CleftMv) +
            p.CalyxOuterPf * Squared(v.CalyxMv - v.PerilymphMv));
        CleftNumbers.Finite(value); return value;
    }

    public double StoredEnergyRateFw(AvatarCleftPotentials v, AvatarCleftVoltageRates rates)
    {
        var q = Charges(v);
        CleftNumbers.Finite(rates.HairMvPerMs); CleftNumbers.Finite(rates.CleftMvPerMs); CleftNumbers.Finite(rates.CalyxMvPerMs);
        double value = q.HairFc * rates.HairMvPerMs + q.CleftFc * rates.CleftMvPerMs + q.CalyxFc * rates.CalyxMvPerMs;
        CleftNumbers.Finite(value); return value;
    }
}
