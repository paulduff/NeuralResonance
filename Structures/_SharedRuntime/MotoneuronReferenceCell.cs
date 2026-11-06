// Five capacitive compartments, independently expressed from the deposited
// Powers/Heckman 2017 cat MG model. Isolated only; no ModelNeuron/host selection.
// Passive endpoint junctions are eliminated exactly, preserving both branches.
internal sealed record MotoneuronSectionParameters(double LengthUm, double DiameterUm,
    double AxialResistivityOhmCm, double CapacitanceUfPerCm2, double LeakSiemensPerCm2,
    double LeakReversalMv, double HcnSiemensPerCm2);

internal sealed record MotoneuronReferenceParameters(string SourceKey,
    MotoneuronSectionParameters Soma, MotoneuronSectionParameters Dendrite,
    double SodiumTransient, double SodiumPersistent, double PotassiumDelayed,
    double CalciumHigh, double PotassiumCalcium, double CalciumRemovalMs,
    double Dendrite1CalciumL, double Dendrite2CalciumL, double Dendrite3CalciumL, double Dendrite4CalciumL,
    MotoneuronChannelParameters Channels)
{
    // Numeric facts from FivecomptcMG_0.hoc. The study also loads pinned rows
    // 9 and 19 independently; this fixture is not an Avatar anatomical default.
    public static MotoneuronReferenceParameters CatLowThreshold { get; } = new("FivecomptcMG_0",
        new(2952, 22, .001, 1.35546, .00008109, -71, .00003),
        new(1794.13, 8.73071, 51.038, .867781, .0000793445, -71, .00003),
        .01, .000026, .015, .0000064, .00045, 60, .000085, .000095, .0001, .000115, new());

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(SourceKey) || Soma is null || Dendrite is null || Channels is null)
            throw new ArgumentException("Explicit reference identity, geometry and channels required.");
        Channels.Validate();
        foreach (var section in new[] { Soma, Dendrite })
        {
            double[] positive = [section.LengthUm, section.DiameterUm, section.AxialResistivityOhmCm, section.CapacitanceUfPerCm2];
            if (positive.Any(x => !double.IsFinite(x) || x <= 0) || !double.IsFinite(section.LeakReversalMv) ||
                !double.IsFinite(section.LeakSiemensPerCm2) || section.LeakSiemensPerCm2 < 0 ||
                !double.IsFinite(section.HcnSiemensPerCm2) || section.HcnSiemensPerCm2 < 0)
                throw new ArgumentException("Invalid motoneuron geometry or passive parameters.");
        }
        double[] conductance = [SodiumTransient, SodiumPersistent, PotassiumDelayed, CalciumHigh, PotassiumCalcium,
            Dendrite1CalciumL, Dendrite2CalciumL, Dendrite3CalciumL, Dendrite4CalciumL];
        if (conductance.Any(x => !double.IsFinite(x) || x < 0) || !double.IsFinite(CalciumRemovalMs) || CalciumRemovalMs <= 0)
            throw new ArgumentException("Invalid motoneuron active parameters.");
    }
}

internal enum MotoneuronDiagnosticBlock { None, Passive, WithoutTransientSodium, WithoutCalciumL, WithoutPotassiumCalcium }

internal sealed class MotoneuronReferenceCell
{
    public const string Version = "isolated-motoneuron-five-compartment-rk4-v1";
    public const double MaximumStepMs = .0125;
    public const double SpikeThresholdMv = -20; // Observational crossing, never a reset.
    public const int PhysicalStateCount = 27;
    // V soma/d1/d2/d3/d4 (0..4), nine soma gates (5..13), local Ca (14),
    // each dendrite mCaL/hCaL/HCN (15..26); release/removal/charge ledgers (27..29).
    private readonly double[] _state = new double[30];
    private readonly double[] _initialVoltage = new double[5];
    private readonly double[] _area = new double[5], _capacitance = new double[5];
    private readonly double[,] _coupling = new double[5, 5];
    private readonly MotoneuronReferenceParameters _parameters;
    private readonly MotoneuronDiagnosticBlock _block;
    private readonly List<double> _spikes = [];
    private bool _faulted;
    public double TimeMs { get; private set; }
    public double SomaVoltageMv => _state[0];
    public double CalciumMm => _state[14];
    public double MaximumCalciumLedgerErrorMm { get; private set; }
    public double MaximumChargeLedgerErrorFc { get; private set; }
    public double[] CopyState() => (double[])_state.Clone();
    public double[] CopyCrossings() => _spikes.ToArray();
    public double[] CopyAreasUm2() => (double[])_area.Clone();
    public double[] CopyCapacitancesPf() => (double[])_capacitance.Clone();
    public double[][] CopyCouplingNs() => Enumerable.Range(0, 5).Select(i => Enumerable.Range(0, 5).Select(j => _coupling[i, j]).ToArray()).ToArray();

    public MotoneuronReferenceCell(MotoneuronReferenceParameters parameters, double initialVoltageMv = -65,
        MotoneuronDiagnosticBlock block = MotoneuronDiagnosticBlock.None)
    {
        ArgumentNullException.ThrowIfNull(parameters); parameters.Validate();
        if (!double.IsFinite(initialVoltageMv) || !Enum.IsDefined(block)) throw new ArgumentException("Invalid isolated cell initialization.");
        _parameters = parameters; _block = block;
        for (int i = 0; i < 5; ++i)
        {
            var section = i == 0 ? parameters.Soma : parameters.Dendrite;
            _area[i] = Math.PI * section.DiameterUm * section.LengthUm;
            _capacitance[i] = .01 * _area[i] * section.CapacitanceUfPerCm2;
            if (!double.IsFinite(_area[i]) || !double.IsFinite(_capacitance[i]) || _area[i] <= 0 || _capacitance[i] <= 0)
                throw new ArgumentException("Geometry exceeds the numerical operating range.");
            _state[i] = _initialVoltage[i] = initialVoltageMv;
        }
        double halfSomaNs = HalfSectionConductanceNs(parameters.Soma), halfDendriteNs = HalfSectionConductanceNs(parameters.Dendrite);
        // Each soma endpoint joins two dendrites. Schur-complement elimination
        // adds pair conductances gi*gj/sum(g), including the small dendrite pair.
        foreach (int start in new[] { 1, 3 })
        {
            double sum = halfSomaNs + 2 * halfDendriteNs;
            AddEdge(0, start, halfSomaNs * halfDendriteNs / sum);
            AddEdge(0, start + 1, halfSomaNs * halfDendriteNs / sum);
            AddEdge(start, start + 1, halfDendriteNs * halfDendriteNs / sum);
        }
        _state[14] = MotoneuronChannelKinetics.RestingCalciumMm;
        Span<MotoneuronGateRate> rates = stackalloc MotoneuronGateRate[9];
        MotoneuronChannelKinetics.Soma(initialVoltageMv, _state[14], parameters.Channels, rates);
        for (int j = 0; j < 9; ++j) _state[5 + j] = rates[j].Equilibrium;
        MotoneuronChannelKinetics.Dendrite(initialVoltageMv, parameters.Channels, rates);
        for (int i = 0; i < 4; ++i)
            for (int j = 0; j < 3; ++j) _state[15 + 3 * i + j] = rates[j].Equilibrium;
        RequireState(_state);
    }

    private static double HalfSectionConductanceNs(MotoneuronSectionParameters s)
        => 1e5 * Math.PI * s.DiameterUm * s.DiameterUm / (2 * s.AxialResistivityOhmCm * s.LengthUm);
    private void AddEdge(int a, int b, double conductance)
    {
        if (!double.IsFinite(conductance) || conductance <= 0) throw new ArgumentException("Invalid reduced axial conductance.");
        _coupling[a, b] = _coupling[b, a] = conductance;
    }

    // Constant diagnostic currents/conductances for this interval. The caller
    // must split at real input changes. Inputs are physical transduction, not
    // prescribed spike times, a firing-rate model or a balance controller.
    public void Advance(double dtMs, double somaCurrentPa = 0, double dendriteExcitatoryNs = 0, double dendriteInhibitoryNs = 0)
    {
        if (_faulted) throw new InvalidOperationException("Faulted motor-cell candidate cannot resume.");
        if (!double.IsFinite(dtMs) || dtMs <= 0 || dtMs > MaximumStepMs || !double.IsFinite(TimeMs + dtMs) || TimeMs + dtMs == TimeMs ||
            !double.IsFinite(somaCurrentPa) || !double.IsFinite(dendriteExcitatoryNs) || dendriteExcitatoryNs < 0 ||
            !double.IsFinite(dendriteInhibitoryNs) || dendriteInhibitoryNs < 0)
            throw new ArgumentException("Finite bounded step/current and nonnegative conductance required.");
        try
        {
            Span<double> a = stackalloc double[30], b = stackalloc double[30], c = stackalloc double[30], d = stackalloc double[30];
            Span<double> temporary = stackalloc double[30], next = stackalloc double[30];
            Derivative(_state, a, somaCurrentPa, dendriteExcitatoryNs, dendriteInhibitoryNs);
            Offset(_state, a, dtMs / 2, temporary); Derivative(temporary, b, somaCurrentPa, dendriteExcitatoryNs, dendriteInhibitoryNs);
            Offset(_state, b, dtMs / 2, temporary); Derivative(temporary, c, somaCurrentPa, dendriteExcitatoryNs, dendriteInhibitoryNs);
            Offset(_state, c, dtMs, temporary); Derivative(temporary, d, somaCurrentPa, dendriteExcitatoryNs, dendriteInhibitoryNs);
            for (int i = 0; i < 30; ++i) next[i] = _state[i] + dtMs / 6 * (a[i] + 2 * b[i] + 2 * c[i] + d[i]);
            RequireState(next);
            double charge = 0;
            for (int i = 0; i < 5; ++i) charge += _capacitance[i] * (next[i] - _initialVoltage[i]);
            MaximumChargeLedgerErrorFc = Math.Max(MaximumChargeLedgerErrorFc, Math.Abs(charge - next[29]));
            MaximumCalciumLedgerErrorMm = Math.Max(MaximumCalciumLedgerErrorMm,
                Math.Abs(next[14] - MotoneuronChannelKinetics.RestingCalciumMm - next[27] + next[28]));
            if (_state[0] < SpikeThresholdMv && next[0] >= SpikeThresholdMv)
                _spikes.Add(TimeMs + dtMs * (SpikeThresholdMv - _state[0]) / (next[0] - _state[0]));
            next.CopyTo(_state); TimeMs += dtMs;
        }
        catch { _faulted = true; throw; }
    }

    private static void Offset(ReadOnlySpan<double> state, ReadOnlySpan<double> slope, double dt, Span<double> output)
    {
        for (int i = 0; i < state.Length; ++i) output[i] = state[i] + dt * slope[i];
    }

    private static void RequireState(ReadOnlySpan<double> state)
    {
        for (int i = 0; i < state.Length; ++i)
            if (!double.IsFinite(state[i]) || i >= 5 && i < 27 && i != 14 && state[i] is < 0 or > 1)
                throw new InvalidOperationException("Motor-cell state left its domain; no clamp or reset.");
        if (state[14] < 0) throw new InvalidOperationException("Negative local calcium; no floor.");
    }

    private void Derivative(ReadOnlySpan<double> y, Span<double> dy, double currentPa, double excitationNs, double inhibitionNs)
    {
        RequireState(y); dy.Clear();
        var p = _parameters; var cp = p.Channels;
        Span<MotoneuronGateRate> rates = stackalloc MotoneuronGateRate[9];
        MotoneuronChannelKinetics.Soma(y[0], y[14], cp, rates);
        for (int j = 0; j < 9; ++j) dy[5 + j] = (rates[j].Equilibrium - y[5 + j]) / rates[j].TauMs;
        double caHigh = _block == MotoneuronDiagnosticBlock.Passive ? 0 : p.CalciumHigh * y[11] * (y[0] - MotoneuronChannelKinetics.SomaticCalciumReversalMv);
        double influx = Math.Max(0, -10000 * caHigh / (2 * MotoneuronChannelKinetics.FaradayCoulombsPerMole * MotoneuronChannelKinetics.CalciumShellDepthUm));
        double removal = (y[14] - MotoneuronChannelKinetics.RestingCalciumMm) / p.CalciumRemovalMs;
        dy[14] = influx - removal; dy[27] = influx; dy[28] = removal;
        for (int i = 0; i < 5; ++i)
        {
            var section = i == 0 ? p.Soma : p.Dendrite;
            double voltage = y[i], density = section.LeakSiemensPerCm2 * (voltage - section.LeakReversalMv);
            if (i == 0 && _block != MotoneuronDiagnosticBlock.Passive)
            {
                double sodium = (_block == MotoneuronDiagnosticBlock.WithoutTransientSodium ? 0 : p.SodiumTransient * y[5] * y[5] * y[5] * y[6] * y[7]) + p.SodiumPersistent * y[8] * y[9];
                double potassium = p.PotassiumDelayed * Math.Pow(y[10], 4) +
                    (_block == MotoneuronDiagnosticBlock.WithoutPotassiumCalcium ? 0 : p.PotassiumCalcium * y[12]);
                density += sodium * (voltage - MotoneuronChannelKinetics.SodiumReversalMv) +
                    potassium * (voltage - MotoneuronChannelKinetics.PotassiumReversalMv) + caHigh +
                    section.HcnSiemensPerCm2 * y[13] * (voltage - MotoneuronChannelKinetics.HcnReversalMv);
            }
            if (i > 0)
            {
                int start = 15 + 3 * (i - 1);
                MotoneuronChannelKinetics.Dendrite(voltage, cp, rates);
                for (int j = 0; j < 3; ++j) dy[start + j] = (rates[j].Equilibrium - y[start + j]) / rates[j].TauMs;
                double calciumL = i switch { 1 => p.Dendrite1CalciumL, 2 => p.Dendrite2CalciumL, 3 => p.Dendrite3CalciumL, _ => p.Dendrite4CalciumL };
                if (_block != MotoneuronDiagnosticBlock.Passive)
                    density += section.HcnSiemensPerCm2 * y[start + 2] * (voltage - MotoneuronChannelKinetics.HcnReversalMv) +
                        (_block == MotoneuronDiagnosticBlock.WithoutCalciumL ? 0 : calciumL * y[start] * y[start + 1] * (voltage - MotoneuronChannelKinetics.CalciumLReversalMv));
            }
            double outsidePa = (i == 0 ? currentPa : -excitationNs * voltage - inhibitionNs * (voltage + 80)) - density * _area[i] * 10;
            dy[29] += outsidePa;
            double netPa = outsidePa;
            for (int j = 0; j < 5; ++j) netPa -= _coupling[i, j] * (voltage - y[j]);
            dy[i] = netPa / _capacitance[i];
        }
    }
}
