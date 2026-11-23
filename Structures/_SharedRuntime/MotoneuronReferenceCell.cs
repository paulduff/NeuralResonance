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
    // Numeric facts from FivecomptcMG_0.hoc. Other pool members have not been
    // imported; this fixture is not an Avatar anatomical default.
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
internal enum MotoneuronAccumulation { Naive, Compensated }

internal sealed class MotoneuronReferenceCell
{
    public const string Version = "isolated-motoneuron-five-compartment-rk4-v1";
    public const string CompensatedVersion = "isolated-motoneuron-five-compartment-rk4-compensated-v2";
    public const double MaximumStepMs = .0125;
    public const double SpikeThresholdMv = -20; // Observational crossing, never a reset.
    public const int PhysicalStateCount = 27;
    // V soma/d1/d2/d3/d4 (0..4), nine soma gates (5..13), local Ca (14),
    // each dendrite mCaL/hCaL/HCN (15..26); release/removal/charge ledgers (27..29).
    private readonly double[] _state = new double[30];
    private readonly double[] _additionError = new double[30];
    private readonly double[] _initialVoltage = new double[5];
    private readonly double[] _area = new double[5], _capacitance = new double[5];
    private readonly double[,] _coupling = new double[5, 5];
    private readonly MotoneuronReferenceParameters _parameters;
    private readonly MotoneuronDiagnosticBlock _block;
    private readonly List<double> _spikes = [];
    private bool _faulted, _receptorInputUsed;
    public double InitialNeuralTimeMs { get; }
    public MotoneuronAccumulation Accumulation { get; }
    public MotoneuronCrossingInterpolation CrossingInterpolation { get; }
    public string IntegrationVersion => (Accumulation == MotoneuronAccumulation.Compensated ? CompensatedVersion : Version) +
        (CrossingInterpolation == MotoneuronCrossingInterpolation.CubicHermite ? MotoneuronCrossing.CubicVersionSuffix : "") +
        (_receptorInputUsed ? "+point-receptors-stage-evaluated-v1" : "") +
        (InitialNeuralTimeMs == 0 ? "" : "+fresh-neural-epoch-v1");
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

    // Read-only diagnostic at a supplied physical state. This is not a restart:
    // saved CSV states omit the compensated integrator's addition residuals.
    internal double[] EvaluateDerivativeForDiagnosis(ReadOnlySpan<double> state, double currentPa,
        double excitationNs, double inhibitionNs)
    {
        if (state.Length != 30 || !double.IsFinite(currentPa) || !double.IsFinite(excitationNs) || excitationNs < 0 ||
            !double.IsFinite(inhibitionNs) || inhibitionNs < 0)
            throw new ArgumentException("Require thirty state values and finite physical input.");
        double[] snapshot = state.ToArray(), result = new double[30];
        Derivative(snapshot, result, currentPa, excitationNs, inhibitionNs);
        return result;
    }

    public MotoneuronReferenceCell(MotoneuronReferenceParameters parameters, double initialVoltageMv = -65,
        MotoneuronDiagnosticBlock block = MotoneuronDiagnosticBlock.None,
        MotoneuronAccumulation accumulation = MotoneuronAccumulation.Naive,
        MotoneuronCrossingInterpolation crossingInterpolation = MotoneuronCrossingInterpolation.Linear,
        double initialNeuralTimeMs = 0)
    {
        ArgumentNullException.ThrowIfNull(parameters); parameters.Validate();
        if (!double.IsFinite(initialVoltageMv) || !Enum.IsDefined(block) || !Enum.IsDefined(accumulation) || !Enum.IsDefined(crossingInterpolation) ||
            !double.IsFinite(initialNeuralTimeMs) || initialNeuralTimeMs < 0 ||
            !double.IsFinite(initialNeuralTimeMs + MaximumStepMs) || initialNeuralTimeMs + MaximumStepMs / 2 <= initialNeuralTimeMs)
            throw new ArgumentException("Invalid isolated cell initialization.");
        // Fresh initial state at a declared absolute time. There is deliberately
        // no setter/reset/retiming operation for an already existing cell.
        InitialNeuralTimeMs = TimeMs = initialNeuralTimeMs;
        _parameters = parameters; _block = block; Accumulation = accumulation; CrossingInterpolation = crossingInterpolation;
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
        => AdvanceCore(dtMs, somaCurrentPa, dendriteExcitatoryNs, dendriteInhibitoryNs, null);

    // Only the isolated synaptic owner uses this entry point. It must establish
    // complete input and split at every release and pulse-end boundary before
    // calling. The membrane receives actual compartment-local conductances at
    // all four RK stages, with each stage's voltage determining current.
    internal void AdvanceWithReceptors(double dtMs, IReadOnlyList<MotorReceptorContact> contacts)
    {
        ArgumentNullException.ThrowIfNull(contacts);
        if (contacts.Any(c => c is null || c.Capture().Value.TimeMs != TimeMs) ||
            contacts.Distinct().Count() != contacts.Count)
            throw new ArgumentException("Require distinct receptor contacts on this membrane's clock.");
        AdvanceCore(dtMs, 0, 0, 0, contacts);
    }

    private void AdvanceCore(double dtMs, double somaCurrentPa, double dendriteExcitatoryNs,
        double dendriteInhibitoryNs, IReadOnlyList<MotorReceptorContact>? contacts)
    {
        if (_faulted) throw new InvalidOperationException("Faulted motor-cell candidate cannot resume.");
        if (!double.IsFinite(dtMs) || dtMs <= 0 || dtMs > MaximumStepMs || !double.IsFinite(TimeMs + dtMs) || TimeMs + dtMs == TimeMs ||
            !double.IsFinite(somaCurrentPa) || !double.IsFinite(dendriteExcitatoryNs) || dendriteExcitatoryNs < 0 ||
            !double.IsFinite(dendriteInhibitoryNs) || dendriteInhibitoryNs < 0)
            throw new ArgumentException("Finite bounded step/current and nonnegative conductance required.");
        double middle = TimeMs + dtMs / 2, end = TimeMs + dtMs;
        if (contacts is not null && (middle <= TimeMs || middle >= end))
            throw new ArgumentException("The source clock cannot resolve receptor solver stages.");
        try
        {
            Span<double> a = stackalloc double[30], b = stackalloc double[30], c = stackalloc double[30], d = stackalloc double[30];
            Span<double> temporary = stackalloc double[30], next = stackalloc double[30], nextError = stackalloc double[30];
            Derivative(_state, a, somaCurrentPa, dendriteExcitatoryNs, dendriteInhibitoryNs, TimeMs, contacts);
            Offset(_state, a, dtMs / 2, temporary); Derivative(temporary, b, somaCurrentPa, dendriteExcitatoryNs, dendriteInhibitoryNs, middle, contacts);
            Offset(_state, b, dtMs / 2, temporary); Derivative(temporary, c, somaCurrentPa, dendriteExcitatoryNs, dendriteInhibitoryNs, middle, contacts);
            Offset(_state, c, dtMs, temporary); Derivative(temporary, d, somaCurrentPa, dendriteExcitatoryNs, dendriteInhibitoryNs, end, contacts);
            for (int i = 0; i < 30; ++i)
            {
                double increment = dtMs / 6 * (a[i] + 2 * b[i] + 2 * c[i] + d[i]);
                // Kahan addition retains the part lost when a small RK4 increment
                // is added to a larger state. The RHS, stages and physical bounds
                // are unchanged. Apply to physical states AND accounting integrals.
                if (Accumulation == MotoneuronAccumulation.Compensated)
                {
                    increment -= _additionError[i];
                    next[i] = _state[i] + increment;
                    nextError[i] = (next[i] - _state[i]) - increment;
                    if (!double.IsFinite(nextError[i])) throw new InvalidOperationException("Non-finite compensated increment.");
                }
                else { next[i] = _state[i] + increment; nextError[i] = 0; }
            }
            RequireState(next);
            double charge = 0;
            for (int i = 0; i < 5; ++i) charge += _capacitance[i] * (next[i] - _initialVoltage[i]);
            MaximumChargeLedgerErrorFc = Math.Max(MaximumChargeLedgerErrorFc, Math.Abs(charge - next[29]));
            MaximumCalciumLedgerErrorMm = Math.Max(MaximumCalciumLedgerErrorMm,
                Math.Abs(next[14] - MotoneuronChannelKinetics.RestingCalciumMm - next[27] + next[28]));
            if (_state[0] < SpikeThresholdMv && next[0] >= SpikeThresholdMv)
            {
                double crossing;
                if (CrossingInterpolation == MotoneuronCrossingInterpolation.CubicHermite)
                {
                    // RK4's d stage is not the derivative at its accepted endpoint.
                    // Evaluate that endpoint without advancing any receptor or cell.
                    Span<double> endpoint = stackalloc double[30];
                    Derivative(next, endpoint, somaCurrentPa, dendriteExcitatoryNs, dendriteInhibitoryNs, end, contacts);
                    crossing = MotoneuronCrossing.Cubic(TimeMs, end, _state[0], next[0], a[0], endpoint[0]).TimeMs;
                }
                else crossing = TimeMs + dtMs * (SpikeThresholdMv - _state[0]) / (next[0] - _state[0]);
                _spikes.Add(crossing);
            }
            next.CopyTo(_state); nextError.CopyTo(_additionError); TimeMs += dtMs;
            _receptorInputUsed |= contacts is not null;
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

    private void Derivative(ReadOnlySpan<double> y, Span<double> dy, double currentPa, double excitationNs, double inhibitionNs,
        double stageTimeMs = 0, IReadOnlyList<MotorReceptorContact>? contacts = null)
    {
        RequireState(y); dy.Clear();
        Span<double> synapticPa = stackalloc double[5];
        synapticPa.Clear();
        if (contacts is not null)
            foreach (var contact in contacts)
            {
                var value = contact.EvaluateKnownReleaseAt(stageTimeMs);
                int compartment = value.ElectricalCompartment;
                synapticPa[compartment] += value.InwardCurrentPa(y[compartment]);
                if (!double.IsFinite(synapticPa[compartment]))
                    throw new InvalidOperationException("Nonfinite summed synaptic current.");
            }
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
            if (synapticPa[i] != 0) outsidePa += synapticPa[i];
            dy[29] += outsidePa;
            double netPa = outsidePa;
            for (int j = 0; j < 5; ++j) netPa -= _coupling[i, j] * (voltage - y[j]);
            dy[i] = netPa / _capacitance[i];
        }
    }
}
