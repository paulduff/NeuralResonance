// Reference HH membrane, not a calibrated mammalian cerebellar cell.
// Hodgkin & Huxley (1952), doi:10.1113/jphysiol.1952.sp004764;
// modern voltage convention also documented in NEURON src/nrnoc/hh.mod.
// mV, ms, microamp/cm^2, millisiemens/cm^2, microfarad/cm^2; 6.3 degrees C.
internal sealed class HodgkinHuxleyMembrane
{
    public double Voltage { get; private set; }
    public double M { get; private set; }
    public double H { get; private set; }
    public double N { get; private set; }
    public const double MaximumStepMilliseconds = 0.025;

    public HodgkinHuxleyMembrane(double voltage = -65)
    {
        if (!double.IsFinite(voltage)) throw new ArgumentOutOfRangeException(nameof(voltage));
        Voltage = voltage;
        var rates = Rates(voltage);
        M = rates.Am / (rates.Am + rates.Bm);
        H = rates.Ah / (rates.Ah + rates.Bh);
        N = rates.An / (rates.An + rates.Bn);
    }

    public static (double Am, double Bm, double Ah, double Bh, double An, double Bn) Rates(double v) =>
        (.1 * Trap(-(v + 40), 10), 4 * Math.Exp(-(v + 65) / 18),
         .07 * Math.Exp(-(v + 65) / 20), 1 / (1 + Math.Exp(-(v + 35) / 10)),
         .01 * Trap(-(v + 55), 10), .125 * Math.Exp(-(v + 65) / 80));

    static double Trap(double x, double scale) => Math.Abs(x / scale) < 1e-6
        ? scale * (1 - x / scale / 2) : x / (Math.Exp(x / scale) - 1);
    static double Gate(double value, double alpha, double beta, double dt) =>
        alpha / (alpha + beta) + (value - alpha / (alpha + beta)) * Math.Exp(-(alpha + beta) * dt);

    // Exact gate relaxation under an imposed voltage, also used to validate
    // voltage-clamp kinetics independently of the free membrane integrator.
    public void AdvanceGates(double voltage, double dt)
    {
        var r = Rates(voltage);
        M = Gate(M, r.Am, r.Bm, dt);
        H = Gate(H, r.Ah, r.Bh, dt);
        N = Gate(N, r.An, r.Bn, dt);
    }

    public bool Advance(double dt, double currentDensity, double excitatoryConductance = 0,
        double nmdaConductance = 0, double gabaAConductance = 0, double gabaBConductance = 0)
    {
        if (!double.IsFinite(dt) || dt <= 0 || !double.IsFinite(currentDensity))
            throw new ArgumentOutOfRangeException(nameof(dt), "Finite positive time and finite current required.");
        var input = new Input(currentDensity, excitatoryConductance, nmdaConductance, gabaAConductance, gabaBConductance);
        if (!ValidConductance(excitatoryConductance) || !ValidConductance(nmdaConductance) ||
            !ValidConductance(gabaAConductance) || !ValidConductance(gabaBConductance))
            throw new ArgumentOutOfRangeException(nameof(excitatoryConductance));
        double remaining = dt;
        bool crossed = false;
        while (remaining > 1e-12)
        {
            double before = Voltage;
            var state = new MembraneState(Voltage, M, H, N);
            var a = Slope(state, input);
            var rates = Rates(Voltage);
            double fastest = Math.Max(Math.Max(rates.Am+rates.Bm, rates.Ah+rates.Bh), rates.An+rates.Bn);
            fastest = Math.Max(fastest, 120*M*M*M*H + 36*N*N*N*N + .3 + excitatoryConductance + nmdaConductance + gabaAConductance + gabaBConductance);
            // Bound stiffness and voltage change without clipping a spike or
            // resetting an invalid state. Strong conductance input needs smaller steps.
            double step = Math.Min(remaining, Math.Min(MaximumStepMilliseconds,
                Math.Min(.25/fastest, 5/Math.Max(1,Math.Abs(a.V)))));
            if (!double.IsFinite(step) || step < 1e-8 && remaining >= 1e-8)
                throw new InvalidOperationException("HH input exceeds the numerical operating range.");
            var b = Slope(state.Offset(a, step / 2), input);
            var c = Slope(state.Offset(b, step / 2), input);
            var d = Slope(state.Offset(c, step), input);
            Voltage += step / 6 * (a.V + 2 * b.V + 2 * c.V + d.V);
            M += step / 6 * (a.M + 2 * b.M + 2 * c.M + d.M);
            H += step / 6 * (a.H + 2 * b.H + 2 * c.H + d.H);
            N += step / 6 * (a.N + 2 * b.N + 2 * c.N + d.N);
            if (!double.IsFinite(Voltage) || !double.IsFinite(M) || !double.IsFinite(H) || !double.IsFinite(N))
                throw new InvalidOperationException("Non-finite HH membrane; no silent voltage reset.");
            crossed |= before < 0 && Voltage >= 0;
            remaining -= step;
        }
        // Voltage and gates keep evolving through the spike. No imposed reset,
        // instantaneous gate replacement, AIS trigger or forced refractory timer.
        return crossed;
    }

    readonly record struct MembraneState(double V, double M, double H, double N)
    {
        public MembraneState Offset(MembraneState slope, double time) =>
            new(V + time * slope.V, M + time * slope.M, H + time * slope.H, N + time * slope.N);
    }
    readonly record struct Input(double Current, double Excitatory, double Nmda, double GabaA, double GabaB);
    static bool ValidConductance(double g) => double.IsFinite(g) && g >= 0;
    static MembraneState Slope(MembraneState s, Input input)
    {
        var r = Rates(s.V);
        double nmdaRelief = 1 / (1 + .28 * Math.Exp(-.062 * s.V));
        double synaptic = -(input.Excitatory + input.Nmda * nmdaRelief) * s.V
            - input.GabaA * (s.V + 80) - input.GabaB * (s.V + 95);
        return new(input.Current + synaptic - 120 * s.M * s.M * s.M * s.H * (s.V - 50)
                           - 36 * s.N * s.N * s.N * s.N * (s.V + 77) - .3 * (s.V + 54.387),
            r.Am * (1 - s.M) - r.Bm * s.M, r.Ah * (1 - s.H) - r.Bh * s.H, r.An * (1 - s.N) - r.Bn * s.N);
    }
}
