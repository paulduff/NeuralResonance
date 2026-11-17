namespace NRE.SimAvatar;

public readonly record struct AvatarHairMembraneGates(double PotassiumLow, double Calcium, double Hcn1)
{
    public void Validate()
    {
        AvatarHairCellKernel.Probability(PotassiumLow); AvatarHairCellKernel.Probability(Calcium); AvatarHairCellKernel.Probability(Hcn1);
    }
}
public readonly record struct AvatarHairGateDerivatives(double PotassiumLowPerMs, double CalciumPerMs, double Hcn1PerMs);
public readonly record struct AvatarCalyxGateDerivatives(double InnerKv74PerMs, double InnerHcn2PerMs,
    double OuterKv74PerMs, double OuterHcn2PerMs);

// The external axon port is explicit. Zero declares a sealed terminal, not an
// implemented afferent cable or a default inferred from missing neural input.
public readonly record struct AvatarCleftLocalMembraneInput(string Id, AvatarHairMembraneGates HairGates,
    AvatarCalyxGates CalyxGates, double AxonOutgoingPa);
public sealed record AvatarCleftLocalMembraneEvaluation(string Id, double HairMembraneVoltageMv,
    AvatarHairCellRates HairRates, AvatarHairCellCurrents HairCurrents, AvatarHairGateDerivatives HairGateDerivatives,
    AvatarCalyxEvaluation Calyx, AvatarCalyxGateDerivatives CalyxGateDerivatives, AvatarCleftMembranePort Port);
public sealed record AvatarVestibularCleftEvaluation(double EndolymphPotentialMv, double MetCurrentPa,
    IReadOnlyList<AvatarCleftLocalMembraneEvaluation> Membranes, AvatarCleftOpenEvaluation Coupling);

// One joint derivative from the instantaneous membrane/cleft state. The source
// places active hair channels within the calyx; the uncovered part is capacitive.
// Intracellular ions and calcium reservoirs remain fixed as in the deposited
// reduction. No clock, gate resetting, time integration or afferent event exists.
public sealed class AvatarVestibularCleftMembranes
{
    public const string Version = "local-hair-calyx-open-cleft-membranes-v1";
    public AvatarCleftOpenCoupling Coupling { get; }
    public AvatarHairCellParameters HairParameters { get; }
    public AvatarCalyxKernel CalyxKernel { get; }
    public string Fingerprint { get; }
    private readonly AvatarHairCellKernel _hairReference;
    private readonly Patch[] _patches;
    private sealed record Patch(string Id, double HairFraction, double CalyxInnerFraction, double CalyxOuterFraction);

    public AvatarVestibularCleftMembranes(AvatarCleftOpenCoupling coupling,
        AvatarHairCellParameters hairParameters, AvatarCalyxParameters calyxParameters)
    {
        ArgumentNullException.ThrowIfNull(coupling); ArgumentNullException.ThrowIfNull(hairParameters);
        ArgumentNullException.ThrowIfNull(calyxParameters); hairParameters.Validate(); calyxParameters.Validate();
        Coupling = coupling; HairParameters = hairParameters; CalyxKernel = new(calyxParameters);
        _hairReference = new(hairParameters,AvatarHairCellBath.Reference);
        // The qualified calyx outer expressions use literal reversals and K=5.
        // Supporting a different outer chemical bath requires a declared model.
        if (coupling.Reservoir.PotassiumMm != 5 || coupling.Reservoir.SodiumMm != 140 || coupling.Spatial.Material.ThermalVoltageMv != 26)
            throw new ArgumentException("This membrane reduction requires its declared 5/140 mM perilymph and 26 mV thermal voltage.");
        Match(hairParameters.CleftFacingAreaSquareMicrometers,calyxParameters.InnerAreaSquareMicrometers);
        var spatial = coupling.Spatial;
        Match(hairParameters.ApicalCapacitancePf,spatial.HairApicalPf);
        Match((hairParameters.BasolateralAreaSquareMicrometers-hairParameters.CleftFacingAreaSquareMicrometers)*
            hairParameters.SpecificCapacitancePfPerSquareMicrometer,spatial.HairPerilymphPf);
        double hairTotal = 0, innerTotal = 0, outerTotal = 0;
        _patches = spatial.Compartments.Select(n =>
        {
            double hairArea = n.HairCleftPf/hairParameters.SpecificCapacitancePfPerSquareMicrometer;
            double innerArea = n.CalyxInnerPf/calyxParameters.SpecificCapacitancePfPerSquareMicrometer;
            double outerArea = n.CalyxOuterPf/calyxParameters.SpecificCapacitancePfPerSquareMicrometer;
            Match(hairArea,innerArea); hairTotal += hairArea; innerTotal += innerArea; outerTotal += outerArea;
            // Capacitance ratios are the same area fractions under the declared
            // uniform specific capacitance, avoiding an extra divide/multiply.
            return new Patch(n.Id,n.HairCleftPf/(hairParameters.SpecificCapacitancePfPerSquareMicrometer*hairParameters.CleftFacingAreaSquareMicrometers),
                n.CalyxInnerPf/CalyxKernel.InnerCapacitancePf,n.CalyxOuterPf/CalyxKernel.OuterCapacitancePf);
        }).ToArray();
        Match(hairParameters.CleftFacingAreaSquareMicrometers,hairTotal);
        Match(calyxParameters.InnerAreaSquareMicrometers,innerTotal); Match(calyxParameters.OuterAreaSquareMicrometers,outerTotal);
        foreach (var p in _patches)
        {
            CleftNumbers.Positive(p.HairFraction); CleftNumbers.Positive(p.CalyxInnerFraction); CleftNumbers.Positive(p.CalyxOuterFraction);
        }
        Fingerprint = CleftNumbers.Fingerprint(new { Version, coupling.Fingerprint, Hair = _hairReference.Fingerprint,
            Calyx = CalyxKernel.Fingerprint, Patches = _patches,
            HairLocation = "active cleft-facing membrane; uncovered membrane capacitive only",
            Chemistry = "fixed intracellular K=150/Na=12/Ca=.001; extracellular Ca=1.3 mM",
            Endolymph = "parameter offset from perilymph", Axon = "required instantaneous external current; no cable" });
    }

    public AvatarVestibularCleftEvaluation Evaluate(double hairPotentialMv, double perilymphPotentialMv,
        double bundleDisplacementNanometers, IReadOnlyList<AvatarCleftCompartmentState> states,
        IReadOnlyList<AvatarCleftLocalMembraneInput> inputs)
    {
        CleftNumbers.Finite(hairPotentialMv); CleftNumbers.Finite(perilymphPotentialMv);
        CleftNumbers.Finite(bundleDisplacementNanometers); ArgumentNullException.ThrowIfNull(states); ArgumentNullException.ThrowIfNull(inputs);
        if (states.Count != _patches.Length || inputs.Count != _patches.Length)
            throw new ArgumentException("Complete named cleft state and local membrane gates/axon ports are required.");
        var namedStates = states.ToDictionary(s => s.Id,StringComparer.Ordinal);
        var namedInputs = inputs.ToDictionary(s => s.Id,StringComparer.Ordinal);
        double endolymph = perilymphPotentialMv+HairParameters.EndolymphPotentialMv; CleftNumbers.Finite(endolymph);
        double met = HairParameters.MetMaximumNs*AvatarHairCellKernel.MetOpenProbability(bundleDisplacementNanometers)*
            (hairPotentialMv-HairParameters.MetReversalMv-endolymph);
        CleftNumbers.Finite(met);
        var evaluations = new AvatarCleftLocalMembraneEvaluation[_patches.Length];
        var ports = new AvatarCleftMembranePort[_patches.Length];
        for (int i = 0; i < _patches.Length; i++)
        {
            var patch = _patches[i];
            if (!namedStates.TryGetValue(patch.Id,out var state) || !namedInputs.TryGetValue(patch.Id,out var input))
                throw new ArgumentException("Unknown or missing local membrane identity.");
            input.HairGates.Validate(); input.CalyxGates.Validate(); CleftNumbers.Finite(input.AxonOutgoingPa);
            new AvatarCleftSite(state.CleftMv,state.PotassiumMm,state.SodiumMm).Validate();
            double hairVoltage = hairPotentialMv-state.CleftMv; CleftNumbers.Finite(hairVoltage);
            var hairRates = AvatarHairCellKernel.Rates(hairVoltage);
            var hair = HairCurrents(hairVoltage,state.PotassiumMm,state.SodiumMm,input.HairGates,patch.HairFraction);
            var hg = input.HairGates;
            var dh = new AvatarHairGateDerivatives((hairRates.PotassiumLow.Equilibrium-hg.PotassiumLow)/hairRates.PotassiumLow.TauMs,
                (hairRates.Calcium.Equilibrium-hg.Calcium)/hairRates.Calcium.TauMs,(hairRates.Hcn1.Equilibrium-hg.Hcn1)/hairRates.Hcn1.TauMs);
            var calyx = CalyxKernel.Evaluate(state.CalyxMv-perilymphPotentialMv,state.CleftMv-perilymphPotentialMv,
                state.PotassiumMm,state.SodiumMm,input.CalyxGates);
            calyx = calyx with { Inner = Scale(calyx.Inner,patch.CalyxInnerFraction), Outer = Scale(calyx.Outer,patch.CalyxOuterFraction) };
            var cg = input.CalyxGates; var cr = calyx.Rates;
            var dc = new AvatarCalyxGateDerivatives((cr.Inner.Kv74.Equilibrium-cg.InnerKv74)/cr.Inner.Kv74.TauMs,
                (cr.Inner.Hcn2.Equilibrium-cg.InnerHcn2)/cr.Inner.Hcn2.TauMs,(cr.Outer.Kv74.Equilibrium-cg.OuterKv74)/cr.Outer.Kv74.TauMs,
                (cr.Outer.Hcn2.Equilibrium-cg.OuterHcn2)/cr.Outer.Hcn2.TauMs);
            ports[i] = new(patch.Id,hair.TotalOutwardPa,calyx.Inner.TotalOutwardPa,calyx.Outer.TotalOutwardPa,input.AxonOutgoingPa,
                hair.BasolateralPotassiumPa,hair.BasolateralSodiumPa,calyx.Inner.PotassiumOutwardPa,calyx.Inner.SodiumOutwardPa);
            ReadOnlySpan<double> rates = [dh.PotassiumLowPerMs,dh.CalciumPerMs,dh.Hcn1PerMs,dc.InnerKv74PerMs,dc.InnerHcn2PerMs,dc.OuterKv74PerMs,dc.OuterHcn2PerMs];
            foreach (double value in rates) CleftNumbers.Finite(value);
            evaluations[i] = new(patch.Id,hairVoltage,hairRates,hair,dh,calyx,dc,ports[i]);
        }
        var result = Coupling.Evaluate(hairPotentialMv,endolymph,perilymphPotentialMv,met,0,states,ports);
        return new(endolymph,met,Array.AsReadOnly(evaluations),result);
    }

    private AvatarHairCellCurrents HairCurrents(double voltage, double potassium, double sodium, AvatarHairMembraneGates gates, double fraction)
    {
        var p = HairParameters;
        double ek = 26*(Math.Log(potassium)-Math.Log(150));
        double ena = 26*(Math.Log(sodium)-Math.Log(12));
        double occupancy = potassium/(potassium+1.5);
        double pump = 96.485*p.PumpDensityPerSquareMicrometer*.1/602000*occupancy*occupancy*p.CleftFacingAreaSquareMicrometers;
        var c = new AvatarHairCellCurrents(0,
            fraction*(p.PotassiumLowMaximumNs*gates.PotassiumLow*(voltage-ek)),
            fraction*(p.CalciumMaximumNs*gates.Calcium*(voltage-_hairReference.CalciumReversalMv)),
            fraction*(.8*p.Hcn1MaximumNs*gates.Hcn1*(voltage-ek)),
            fraction*(.2*p.Hcn1MaximumNs*gates.Hcn1*(voltage-ena)),fraction*(-2*pump),fraction*(3*pump));
        ReadOnlySpan<double> values = [c.PotassiumLowPa,c.CalciumPa,c.HcnPotassiumPa,c.HcnSodiumPa,c.PumpPotassiumPa,c.PumpSodiumPa,
            c.TotalOutwardPa,c.BasolateralPotassiumPa,c.BasolateralSodiumPa];
        foreach (double value in values) CleftNumbers.Finite(value);
        return c;
    }

    private static AvatarCalyxFaceCurrents Scale(AvatarCalyxFaceCurrents c, double f) =>
        new(f*c.Kv74PotassiumPa,f*c.HcnPotassiumPa,f*c.HcnSodiumPa,f*c.PumpPotassiumPa,f*c.PumpSodiumPa,
            f*c.KccPotassiumPa,f*c.KccChloridePa,f*c.UnassignedLeakPa);

    private static void Match(double expected, double actual)
    {
        CleftNumbers.Finite(expected); CleftNumbers.Finite(actual);
        // Construction-only rounding allowance in area/capacitance mapping;
        // not a numerical comparison tolerance or a geometry-fit parameter.
        if (Math.Abs(expected-actual) > 1e-10+1e-12*Math.Abs(expected))
            throw new ArgumentException("Membrane areas/capacitances must match the declared spatial topology.");
    }
}
