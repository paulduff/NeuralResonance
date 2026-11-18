namespace NRE.SimAvatar;

// Explicit candidate: deposited channel expressions + May14 sodium timing data
// with declared linear interpolation. Not a claim about COMSOL switch defaults
// or exact reproduction of the printed supplement. No fitted firing-rate map.
public sealed record AvatarAfferentMembraneParameters(double SpecificCapacitancePfPerSquareMicrometer,
    double SodiumMaximumNsPerSquareMicrometer,double Kv1MaximumNsPerSquareMicrometer,
    double Kv7MaximumNsPerSquareMicrometer,double Kv34MaximumNsPerSquareMicrometer,
    double InsidePotassiumMm,double OutsidePotassiumMm,double InsideSodiumMm,double OutsideSodiumMm,double ThermalVoltageMv)
{
    public static AvatarAfferentMembraneParameters NodeReference { get; }=new(.01,120,.13,.1,.2,150,5,12,140,26);
    public static AvatarAfferentMembraneParameters UnmyelinatedReference { get; }=NodeReference with
        { SodiumMaximumNsPerSquareMicrometer=30,Kv1MaximumNsPerSquareMicrometer=0,Kv7MaximumNsPerSquareMicrometer=0,Kv34MaximumNsPerSquareMicrometer=0 };
    public static AvatarAfferentMembraneParameters MyelinatedReference { get; }=UnmyelinatedReference with
        { SpecificCapacitancePfPerSquareMicrometer=.00002,SodiumMaximumNsPerSquareMicrometer=0 };
    public void Validate()
    {
        foreach(double v in new[] { SpecificCapacitancePfPerSquareMicrometer,InsidePotassiumMm,OutsidePotassiumMm,InsideSodiumMm,OutsideSodiumMm,ThermalVoltageMv })CleftNumbers.Positive(v);
        foreach(double v in new[] { SodiumMaximumNsPerSquareMicrometer,Kv1MaximumNsPerSquareMicrometer,Kv7MaximumNsPerSquareMicrometer,Kv34MaximumNsPerSquareMicrometer })CleftNumbers.Nonnegative(v);
    }
}

public readonly record struct AvatarAfferentGates(double SodiumActivation,double SodiumInactivation,
    double Kv1Activation,double Kv7Activation,double Kv34Activation,double Kv34Inactivation)
{
    public void Validate()
    {
        foreach(double x in new[] { SodiumActivation,SodiumInactivation,Kv1Activation,Kv7Activation,Kv34Activation,Kv34Inactivation })AvatarCalyxKernel.Probability(x);
    }
}
public readonly record struct AvatarAfferentGateRate(double Equilibrium,double TauMs);
public readonly record struct AvatarAfferentRates(AvatarAfferentGateRate SodiumActivation,AvatarAfferentGateRate SodiumInactivation,
    AvatarAfferentGateRate Kv1Activation,AvatarAfferentGateRate Kv7Activation,AvatarAfferentGateRate Kv34Activation,AvatarAfferentGateRate Kv34Inactivation);
public readonly record struct AvatarAfferentGateDerivatives(double SodiumActivationPerMs,double SodiumInactivationPerMs,
    double Kv1ActivationPerMs,double Kv7ActivationPerMs,double Kv34ActivationPerMs,double Kv34InactivationPerMs);
public readonly record struct AvatarAfferentCurrentDensities(double SodiumPaPerSquareMicrometer,double Kv1PotassiumPaPerSquareMicrometer,
    double Kv7PotassiumPaPerSquareMicrometer,double Kv34PotassiumPaPerSquareMicrometer)
{
    public double PotassiumPaPerSquareMicrometer=>Kv1PotassiumPaPerSquareMicrometer+Kv7PotassiumPaPerSquareMicrometer+Kv34PotassiumPaPerSquareMicrometer;
    public double TotalOutwardPaPerSquareMicrometer=>SodiumPaPerSquareMicrometer+PotassiumPaPerSquareMicrometer;
}
public readonly record struct AvatarAfferentMembraneEvaluation(double MembraneVoltageMv,double PotassiumReversalMv,double SodiumReversalMv,
    AvatarAfferentRates Rates,AvatarAfferentGateDerivatives GateDerivatives,AvatarAfferentCurrentDensities Currents);
public readonly record struct AvatarAfferentTimePoint(double VoltageMv,double TauMs);

// EndpointHold is an explicit alternative to the deposited fixed-value
// continuation. It preserves every tabulated value but changes kinetics outside
// the table. It is a new modelling hypothesis, not a numerical repair of v1.
public enum AvatarSodiumActivationContinuation { DepositedFixedValue, EndpointHold }

public sealed class AvatarAfferentMembraneKernel
{
    public const string Version="vestibular-afferent-deposited-may14-linear-v1";
    public const string EndpointHoldVersion="vestibular-afferent-may14-endpoint-hold-candidate-v1";
    // Data from sw1/int3 and sw2/int4 at pinned source revision
    // 47d499449df66c6676ee43cbb08ea1995469a817. The table is data, not
    // a channel reset; every gate derivative uses the actual supplied state.
    private static readonly double[] ActivationTimes=[.26131,.4427675,.497174,.66131,.841186,.941278,.89749,.88667,.784782,.768956,.598892,.556232,.440786,.42547,.4204,.387932,.327924,.2942,.295342,.272392,.24667,.249938,.22067,.21091];
    private static readonly double[] InactivationTimes=[3.288872222,1.198086111,.549882889,.448644778,.400258111,.357850222,.325629333,.298526444,.275503667,.256451333,.245151889,.230663778,.220700444,.217291222,.203848667,.201088889,.196587556,.184394222,.177319,.175587111,.168863,.162246963];
    public static IReadOnlyList<AvatarAfferentTimePoint> SodiumActivationTable { get; }=Array.AsReadOnly(ActivationTimes.Select((v,i)=>new AvatarAfferentTimePoint(-55+5*i,v)).ToArray());
    public static IReadOnlyList<AvatarAfferentTimePoint> SodiumInactivationTable { get; }=Array.AsReadOnly(InactivationTimes.Select((v,i)=>new AvatarAfferentTimePoint(-45+5*i,v)).ToArray());
    public AvatarAfferentMembraneParameters Parameters { get; }
    public AvatarSodiumActivationContinuation ActivationContinuation { get; }
    public string Fingerprint { get; }
    public AvatarAfferentMembraneKernel(AvatarAfferentMembraneParameters parameters)
        : this(parameters,AvatarSodiumActivationContinuation.DepositedFixedValue) { }
    public AvatarAfferentMembraneKernel(AvatarAfferentMembraneParameters parameters,AvatarSodiumActivationContinuation continuation)
    {
        ArgumentNullException.ThrowIfNull(parameters);parameters.Validate();ValidateContinuation(continuation);
        Parameters=parameters;ActivationContinuation=continuation;
        Fingerprint=CleftNumbers.Fingerprint(new { Version,parameters,SodiumActivationTable,SodiumInactivationTable,
            Interpolation="piecewise linear; activation outside [-55,60] = 0.2 ms; inactivation extends endpoint secants",
            SodiumOpenProbability="m^3*h",Temperature="room-temperature q10=1",Reservoirs="explicit held intracellular/extracellular ions; no pump or ATP model" });
        if(continuation!=AvatarSodiumActivationContinuation.DepositedFixedValue)
            Fingerprint=CleftNumbers.Fingerprint(new { Version=EndpointHoldVersion,OriginalFingerprint=Fingerprint,
                Continuation="activation below -55 holds 0.26131 ms; above 60 holds 0.21091 ms; table and other rates unchanged",
                PhysiologicalCalibrationQualified=false });
    }
    public static AvatarAfferentRates Rates(double v)=>Rates(v,AvatarSodiumActivationContinuation.DepositedFixedValue);
    public static AvatarAfferentRates Rates(double v,AvatarSodiumActivationContinuation continuation)
    {
        CleftNumbers.Finite(v);ValidateContinuation(continuation);
        double activationTau=continuation==AvatarSodiumActivationContinuation.EndpointHold && v< -55 ? ActivationTimes[0] :
            continuation==AvatarSodiumActivationContinuation.EndpointHold && v>60 ? ActivationTimes[^1] : Table(v,-55,ActivationTimes,true);
        var r=new AvatarAfferentRates(new(Sigmoid((v+40.1)/7.5),activationTau),
            new(Sigmoid(-(v+78.8)/6),Table(v,-45,InactivationTimes,false)),
            new(Sigmoid((v+44)/7.1),3.7),new(Sigmoid((v+41)/7.4),54),
            new(Sigmoid((v+31.3)/8.5),5*1.41*Math.Exp(-.05589*v)*Sigmoid((v+31.3)/8.5)),
            new(Sigmoid(-(v+65.84)/5.51),25.4));
        foreach(var gate in new[] { r.SodiumActivation,r.SodiumInactivation,r.Kv1Activation,r.Kv7Activation,r.Kv34Activation,r.Kv34Inactivation })
        { AvatarCalyxKernel.Probability(gate.Equilibrium);CleftNumbers.Positive(gate.TauMs); }
        return r;
    }
    public static AvatarAfferentGates VoltageConditionedGates(double v)
    {
        var r=Rates(v);return new(r.SodiumActivation.Equilibrium,r.SodiumInactivation.Equilibrium,r.Kv1Activation.Equilibrium,
            r.Kv7Activation.Equilibrium,r.Kv34Activation.Equilibrium,r.Kv34Inactivation.Equilibrium);
    }
    public AvatarAfferentMembraneEvaluation Evaluate(double membraneVoltageMv,AvatarAfferentGates gates)
    {
        gates.Validate();var r=Rates(membraneVoltageMv,ActivationContinuation);var p=Parameters;
        double ek=p.ThermalVoltageMv*(Math.Log(p.OutsidePotassiumMm)-Math.Log(p.InsidePotassiumMm));
        double ena=p.ThermalVoltageMv*(Math.Log(p.OutsideSodiumMm)-Math.Log(p.InsideSodiumMm));
        CleftNumbers.Finite(ek);CleftNumbers.Finite(ena);double v=membraneVoltageMv,m=gates.SodiumActivation;
        var current=new AvatarAfferentCurrentDensities(p.SodiumMaximumNsPerSquareMicrometer*m*m*m*gates.SodiumInactivation*(v-ena),
            p.Kv1MaximumNsPerSquareMicrometer*gates.Kv1Activation*(v-ek),p.Kv7MaximumNsPerSquareMicrometer*gates.Kv7Activation*(v-ek),
            p.Kv34MaximumNsPerSquareMicrometer*gates.Kv34Activation*gates.Kv34Inactivation*(v-ek));
        foreach(double x in new[] { current.SodiumPaPerSquareMicrometer,current.Kv1PotassiumPaPerSquareMicrometer,
            current.Kv7PotassiumPaPerSquareMicrometer,current.Kv34PotassiumPaPerSquareMicrometer,current.TotalOutwardPaPerSquareMicrometer })CleftNumbers.Finite(x);
        static double D(double g,AvatarAfferentGateRate q) { double x=(q.Equilibrium-g)/q.TauMs;CleftNumbers.Finite(x);return x; }
        var rates=new AvatarAfferentGateDerivatives(D(gates.SodiumActivation,r.SodiumActivation),D(gates.SodiumInactivation,r.SodiumInactivation),
            D(gates.Kv1Activation,r.Kv1Activation),D(gates.Kv7Activation,r.Kv7Activation),D(gates.Kv34Activation,r.Kv34Activation),D(gates.Kv34Inactivation,r.Kv34Inactivation));
        return new(v,ek,ena,r,rates,current);
    }
    // Only an analytic held-voltage control. A free cable must integrate its
    // voltage and gates together, without substituting final-voltage g_inf.
    public static AvatarAfferentGates RelaxVoltageClamp(AvatarAfferentGates gates,double voltageMv,double elapsedMs)
        =>RelaxVoltageClamp(gates,voltageMv,elapsedMs,AvatarSodiumActivationContinuation.DepositedFixedValue);
    public static AvatarAfferentGates RelaxVoltageClamp(AvatarAfferentGates gates,double voltageMv,double elapsedMs,
        AvatarSodiumActivationContinuation continuation)
    {
        gates.Validate();CleftNumbers.Nonnegative(elapsedMs);var r=Rates(voltageMv,continuation);if(elapsedMs==0)return gates;
        static double E(double x,AvatarAfferentGateRate q,double t)=>q.Equilibrium+(x-q.Equilibrium)*Math.Exp(-t/q.TauMs);
        var result=new AvatarAfferentGates(E(gates.SodiumActivation,r.SodiumActivation,elapsedMs),E(gates.SodiumInactivation,r.SodiumInactivation,elapsedMs),
            E(gates.Kv1Activation,r.Kv1Activation,elapsedMs),E(gates.Kv7Activation,r.Kv7Activation,elapsedMs),
            E(gates.Kv34Activation,r.Kv34Activation,elapsedMs),E(gates.Kv34Inactivation,r.Kv34Inactivation,elapsedMs));
        result.Validate();return result;
    }
    private static void ValidateContinuation(AvatarSodiumActivationContinuation continuation)
    {
        if(!Enum.IsDefined(continuation))throw new ArgumentOutOfRangeException(nameof(continuation));
    }
    private static double Sigmoid(double x)
    {
        if(x>=0)return 1/(1+Math.Exp(-x));double e=Math.Exp(x);return e/(1+e);
    }
    private static double Table(double v,double start,double[] values,bool fixedOutside)
    {
        double end=start+5*(values.Length-1);
        if(fixedOutside&&(v<start||v>end))return .2;
        // Explicit endpoint secants, not an inferred COMSOL interpolation default.
        // Subtraction can round a voltage one ULP below end to the final knot.
        // Select the last valid interval; do not clamp voltage or change the curve.
        int i=v<=start?0:v>=end?values.Length-2:Math.Min(values.Length-2,(int)Math.Floor((v-start)/5));
        double fraction=(v-(start+5*i))/5;
        return values[i]+fraction*(values[i+1]-values[i]);
    }
}
