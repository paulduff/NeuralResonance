// Isolated candidate, not selected by ModelNeuron or a live structure host.
// Solinas et al. (2007), doi:10.3389/neuro.03.002.2007; ModelDB 112685,
// revision 2ffaeca558235d61222240fc74247cab1cd02fe7. Equations independently
// expressed from the deposited Golgi_*.mod mechanisms by Solinas, D'Angelo,
// Forti, Nieus, Fontana and Destexhe. See docs/DNNE_Golgi_Channels_2026-11-06.md
// for voltage convention, numerical corrections and limits of this candidate.
// Units: mV, ms, mM, deg C, S/cm^2 and mA/cm^2. Positive current is outward.
internal enum GolgiGate
{
    NaM, NaH, NaP, NaRS, NaRF, Kv, Km, KaA, KaB, HvaS, HvaU, LvaM, LvaH,
    Hcn1Fast, Hcn1Slow, Hcn2Fast, Hcn2Slow, Bk
}

internal readonly record struct GolgiGateRate(double Equilibrium, double TauMs);

internal readonly record struct GolgiIonicCurrents(
    double SodiumTransient, double SodiumPersistent, double SodiumResurgent,
    double PotassiumDelayed, double PotassiumSlow, double PotassiumA,
    double CalciumHigh, double CalciumLow, double Hcn1, double Hcn2,
    double Bk, double Sk, double Leak)
{
    public double Total => SodiumTransient + SodiumPersistent + SodiumResurgent +
        PotassiumDelayed + PotassiumSlow + PotassiumA + CalciumHigh + CalciumLow +
        Hcn1 + Hcn2 + Bk + Sk + Leak;
}

internal sealed class GolgiChannelKinetics
{
    public const string CandidateVersion = "golgi-solinas-channels-v1";
    public const int GateCount = 18;
    public const double RestingCalciumMm = .00005;
    public const double SodiumReversalMv = 87.39;
    public const double PotassiumReversalMv = -84.69;
    public const double LeakReversalMv = -55;
    public const double HcnReversalMv = -20;
    private readonly double[] _gates = new double[GateCount];
    // SK2: c1,c2,c3,c4,o1,o2. Preserve occupancy; never repair it by renormalizing.
    private readonly double[] _sk = new double[6];
    public double TemperatureCelsius { get; }
    public double Gate(GolgiGate gate) => _gates[(int)gate];
    public double[] CopyGates() => (double[])_gates.Clone();
    public double[] CopySkOccupancy() => (double[])_sk.Clone();
    public double SkOpenProbability => _sk[4] + _sk[5];

    public GolgiChannelKinetics(double voltageMv = -60, double calciumMm = RestingCalciumMm,
        double temperatureCelsius = 23)
    {
        ValidateBath(voltageMv, calciumMm, temperatureCelsius);
        TemperatureCelsius = temperatureCelsius;
        Span<GolgiGateRate> rates = stackalloc GolgiGateRate[GateCount];
        FillRates(voltageMv, calciumMm, temperatureCelsius, rates);
        for (int i = 0; i < GateCount; ++i) _gates[i] = rates[i].Equilibrium;
        // Detailed balance on the five edges of the published SK2 graph.
        double ca = calciumMm / 3;
        _sk[0] = 1;
        _sk[1] = _sk[0] * 200 * ca / .08;
        _sk[2] = _sk[1] * 160 * ca / .08;
        _sk[3] = _sk[2] * 80 * ca / .2;
        _sk[4] = _sk[2] * .16;
        _sk[5] = _sk[3] * 12;
        double total = _sk.Sum();
        for (int i = 0; i < _sk.Length; ++i) _sk[i] /= total;
    }

    // Voltage and calcium are explicitly imposed for this channel test only.
    // A free cell needs coupled cable voltage, calcium flux/pumps and substrates.
    public void AdvanceVoltageClamp(double voltageMv, double calciumMm, double elapsedMs)
    {
        ValidateBath(voltageMv, calciumMm, TemperatureCelsius);
        if (!double.IsFinite(elapsedMs) || elapsedMs < 0 || elapsedMs > 10_000)
            throw new ArgumentOutOfRangeException(nameof(elapsedMs));
        Span<GolgiGateRate> rates = stackalloc GolgiGateRate[GateCount];
        FillRates(voltageMv, calciumMm, TemperatureCelsius, rates);
        for (int i = 0; i < GateCount; ++i)
        {
            var r = rates[i];
            _gates[i] = r.Equilibrium + (_gates[i] - r.Equilibrium) * Math.Exp(-elapsedMs / r.TauMs);
        }
        AdvanceSk(calciumMm, elapsedMs);
    }

    public GolgiIonicCurrents Currents(double voltageMv, double highCalciumMm, double lowCalciumMm)
    {
        ValidateBath(voltageMv, highCalciumMm, TemperatureCelsius);
        ValidateBath(voltageMv, lowCalciumMm, TemperatureCelsius);
        double hi = CalciumReversal(highCalciumMm, TemperatureCelsius);
        double lo = CalciumReversal(lowCalciumMm, TemperatureCelsius);
        double G(GolgiGate gate) => Gate(gate);
        double na = voltageMv - SodiumReversalMv, k = voltageMv - PotassiumReversalMv;
        return new(
            .048 * Math.Pow(G(GolgiGate.NaM), 3) * G(GolgiGate.NaH) * na,
            .00019 * G(GolgiGate.NaP) * na,
            .0017 * G(GolgiGate.NaRS) * G(GolgiGate.NaRF) * na,
            .032 * Math.Pow(G(GolgiGate.Kv), 4) * k,
            .001 * G(GolgiGate.Km) * k,
            .008 * Math.Pow(G(GolgiGate.KaA), 3) * G(GolgiGate.KaB) * k,
            .000460 * Math.Pow(G(GolgiGate.HvaS), 2) * G(GolgiGate.HvaU) * (voltageMv - hi),
            .00025 * Math.Pow(G(GolgiGate.LvaM), 2) * G(GolgiGate.LvaH) * (voltageMv - lo),
            .00005 * (G(GolgiGate.Hcn1Fast) + G(GolgiGate.Hcn1Slow)) * (voltageMv - HcnReversalMv),
            .00008 * (G(GolgiGate.Hcn2Fast) + G(GolgiGate.Hcn2Slow)) * (voltageMv - HcnReversalMv),
            .003 * G(GolgiGate.Bk) * k,
            .038 * SkOpenProbability * k,
            .000021 * (voltageMv - LeakReversalMv));
    }

    public static double CalciumReversal(double calciumMm, double temperatureCelsius, double externalMm = 2)
    {
        ValidateBath(-60, calciumMm, temperatureCelsius);
        if (calciumMm <= 0 || !double.IsFinite(externalMm) || externalMm <= 0)
            throw new ArgumentOutOfRangeException(nameof(calciumMm), "Nernst concentrations must be positive.");
        return 1000 * 8.31446261815324 * (temperatureCelsius + 273.15) /
            (2 * 96485.33212) * Math.Log(externalMm / calciumMm);
    }

    // Published 0.2 um shell and first-order removal. No reservoir/ATP claim.
    public static double CalciumDerivative(double calciumMm, double outwardCurrentDensity)
    {
        if (!double.IsFinite(calciumMm) || calciumMm < 0 || !double.IsFinite(outwardCurrentDensity))
            throw new ArgumentOutOfRangeException(nameof(calciumMm));
        return -outwardCurrentDensity * 1e4 / (2 * 96485.33212 * .2) -
            1.3 * (calciumMm - RestingCalciumMm);
    }

    public static void FillRates(double v, double calciumMm, double temperatureCelsius, Span<GolgiGateRate> output)
    {
        ValidateBath(v, calciumMm, temperatureCelsius);
        if (output.Length != GateCount) throw new ArgumentException("Require all 18 gates.", nameof(output));
        double q20 = Q(temperatureCelsius, 20), q30 = Q(temperatureCelsius, 30);
        output[(int)GolgiGate.NaM] = FromRates(q20 * .3 * -Linoid(v + 25, -10), q20 * 12 * Math.Exp((v + 50) / -18.182));
        output[(int)GolgiGate.NaH] = FromRates(q20 * .21 * Math.Exp((v + 50) / -3.333), q20 * 3 * Sigmoid(v + 17, -5));
        output[(int)GolgiGate.NaP] = new(Sigmoid(v + 43, -5),
            5 / (q30 * (-.91 * Linoid(v + 40, -5) + .62 * Linoid(v + 40, 5))));
        output[(int)GolgiGate.NaRS] = FromRates(q20 * (.00008 - .00493 * Linoid(v - 4.48754, -6.81881)),
            q20 * (.04752 + .01558 * Linoid(v + 43.97494, .10818, capExponent: true)));
        output[(int)GolgiGate.NaRF] = FromRates(q20 * .31836 * Math.Exp((v + 80) / -62.52621),
            q20 * .01014 * Math.Exp((v + 83.3332) / 16.05379));
        double qKv = Q(temperatureCelsius, 6.3);
        output[(int)GolgiGate.Kv] = FromRates(qKv * -.01 * Linoid(v + 26, -10), qKv * .125 * Math.Exp((v + 36) / -80));
        output[(int)GolgiGate.Km] = new(Sigmoid(v + 35, -6),
            1 / (Q(temperatureCelsius, 22) * .0033 * (Math.Exp((v + 30) / 40) + Math.Exp((v + 30) / -20))));
        double qKa = Q(temperatureCelsius, 25.5);
        output[(int)GolgiGate.KaA] = new(Sigmoid(v + 38, -17),
            1 / (qKa * (.8147 * Sigmoid(v + 9.17203, -23.32708) + .1655 * Math.Exp(-(v + 18.27914) / 19.47175))));
        output[(int)GolgiGate.KaB] = new(Sigmoid(v + 78.8, 8.4),
            1 / (qKa * (.0368 * Sigmoid(v + 111.33209, 12.8433) + .0345 * Sigmoid(v + 49.9537, -8.90123))));
        output[(int)GolgiGate.HvaS] = FromRates(q20 * .04944 * Math.Exp((v + 29.06) / 15.87301587302),
            q20 * .08298 * Math.Exp((v + 18.66) / -25.641));
        output[(int)GolgiGate.HvaU] = FromRates(q20 * .0013 * Math.Exp((v + 48) / -18.183),
            q20 * .0013 * Math.Exp((v + 48) / 83.33));
        output[(int)GolgiGate.LvaM] = new(Sigmoid(v + 2 + 50, -7.4),
            (3 + 1 / (Math.Exp((v + 2 + 25) / 10) + Math.Exp((v + 2 + 100) / -15))) / Math.Pow(5, (temperatureCelsius - 24) / 10));
        output[(int)GolgiGate.LvaH] = new(Sigmoid(v + 2 + 78, 5),
            (85 + 1 / (Math.Exp((v + 2 + 46) / 4) + Math.Exp((v + 2 + 405) / -50))) / Q(temperatureCelsius, 24));
        // The original HCN mechanisms calculate q but do not use it in their taus.
        // Preserve those deposited kinetics. HCN1 fraction is bounded explicitly,
        // avoiding negative occupancies where its linear fit exceeds [0,1].
        double r1 = Math.Clamp(.002096 * v + .97596, 0, 1), open1 = Sigmoid(v + 72.49, 1 / .11305);
        output[(int)GolgiGate.Hcn1Fast] = new(r1 * open1, Math.Exp((.01371 * v + 3.368) * 2.302585092));
        output[(int)GolgiGate.Hcn1Slow] = new((1 - r1) * open1, Math.Exp((.01451 * v + 4.056) * 2.302585092));
        // The rounded HCN2 branch boundary also permits a tiny negative fraction.
        double r2 = v >= -64.70 ? 0 : v <= -108.70 ? 1 : Math.Clamp(-.0227 * v - 1.4694, 0, 1);
        double open2 = Sigmoid(v + 81.95, 1 / .1661);
        output[(int)GolgiGate.Hcn2Fast] = new(r2 * open2, Math.Exp((.0269 * v + 5.6111) * 2.3026));
        output[(int)GolgiGate.Hcn2Slow] = new((1 - r2) * open2, Math.Exp((.0152 * v + 5.2944) * 2.3026));
        double voltageFactor = Math.Exp(v / -11.765);
        output[(int)GolgiGate.Bk] = FromRates(q30 * 7 * calciumMm / (calciumMm + .0015 * voltageFactor),
            q30 * .00015 * voltageFactor / (calciumMm + .00015 * voltageFactor));
        foreach (var r in output)
            if (!double.IsFinite(r.Equilibrium) || r.Equilibrium < 0 || r.Equilibrium > 1 || !double.IsFinite(r.TauMs) || r.TauMs <= 0)
                throw new InvalidOperationException("Golgi rate outside candidate operating range; no silent repair.");
    }

    private static double Q(double temperature, double reference) => Math.Pow(3, (temperature - reference) / 10);
    private static double Sigmoid(double x, double scale) => 1 / (1 + Math.Exp(x / scale));
    private static GolgiGateRate FromRates(double alpha, double beta) => new(alpha / (alpha + beta), 1 / (alpha + beta));

    // Continuous x/(exp(x/scale)-1). The sodium transient source's separate
    // x/(1-exp(...)) branch has an incorrect sign in its near-zero fallback;
    // using -Linoid above applies the correct removable limit, including v=-25.
    private static double Linoid(double x, double scale, bool capExponent = false)
    {
        double z = x / scale;
        if (Math.Abs(z) < 1e-5) return scale * (1 - z / 2 + z * z / 12);
        return x / (Math.Exp(capExponent ? Math.Min(z, 200) : z) - 1);
    }

    private void AdvanceSk(double calciumMm, double dt)
    {
        double q = Q(TemperatureCelsius, 23), ca = calciumMm / 3;
        Span<double> forward = stackalloc double[] { 200 * ca * q, 160 * ca * q, 80 * ca * q, .16 * q, 1.2 * q };
        Span<double> reverse = stackalloc double[] { .08 * q, .08 * q, .2 * q, 1 * q, .1 * q };
        ReadOnlySpan<int> from = stackalloc int[] { 0, 1, 2, 2, 3 };
        ReadOnlySpan<int> to = stackalloc int[] { 1, 2, 3, 4, 5 };
        Span<double> outgoing = stackalloc double[6];
        outgoing.Clear();
        for (int edge = 0; edge < 5; ++edge) { outgoing[from[edge]] += forward[edge]; outgoing[to[edge]] += reverse[edge]; }
        double lambda = 0;
        foreach (double rate in outgoing) lambda = Math.Max(lambda, rate);
        // Uniformization of a continuous-time Markov chain. Bounded Poisson mean
        // avoids underflow and gives non-negative occupancy without clipping.
        int steps = Math.Max(1, checked((int)Math.Ceiling(dt * lambda / 8)));
        double mean = dt / steps * lambda;
        Span<double> term = stackalloc double[6];
        Span<double> next = stackalloc double[6];
        Span<double> result = stackalloc double[6];
        for (int step = 0; step < steps; ++step)
        {
            _sk.AsSpan().CopyTo(term);
            double poisson = Math.Exp(-mean);
            for (int i = 0; i < 6; ++i) result[i] = poisson * term[i];
            for (int order = 1; order <= 160; ++order)
            {
                for (int i = 0; i < 6; ++i) next[i] = term[i] * (1 - outgoing[i] / lambda);
                for (int edge = 0; edge < 5; ++edge)
                {
                    next[to[edge]] += term[from[edge]] * forward[edge] / lambda;
                    next[from[edge]] += term[to[edge]] * reverse[edge] / lambda;
                }
                next.CopyTo(term);
                poisson *= mean / order;
                for (int i = 0; i < 6; ++i) result[i] += poisson * term[i];
                if (order > mean && poisson < 1e-17) break;
                if (order == 160) throw new InvalidOperationException("SK2 series failed to converge.");
            }
            result.CopyTo(_sk);
        }
    }

    private static void ValidateBath(double voltage, double calcium, double temperature)
    {
        if (!double.IsFinite(voltage) || voltage < -120 || voltage > 100 ||
            !double.IsFinite(calcium) || calcium < 0 || calcium > .1 ||
            !double.IsFinite(temperature) || temperature < 15 || temperature > 40)
            throw new ArgumentOutOfRangeException(nameof(voltage), "Candidate requires -120..100 mV, 0..0.1 mM Ca and 15..40 C; these are numerical bounds, not physiological qualification.");
    }
}
