// Coupled, isolated Solinas-reference candidate. No host selects this model.
// Free voltage and calcium; no prescribed spikes, periodic forcing or resets.
// Reference bath only: pumps/reservoirs are not an ATP/perfusion model.
internal enum GolgiDiagnosticBlock { None, Passive, WithoutPersistentSodium, WithoutHcn, WithoutSk }

internal sealed class GolgiReferenceCell
{
    public const string Version = "golgi-reference-cell-be-v1";
    public const double SpikeThresholdMv = -20; // Deposited somatic NetCon threshold.
    private const double CalciumPumpPerMs = 1.3;
    private readonly GolgiCable _cable = new();
    private readonly GolgiChannelKinetics _channels;
    private readonly double[] _voltage, _next, _conductance, _source, _injection;
    private readonly List<double> _spikes = [];
    private readonly double _initialVoltage;
    private readonly GolgiDiagnosticBlock _block;
    private bool _faulted;
    public double TimeMs { get; private set; }
    public double SomaVoltageMv => _voltage[0];
    public double ElectrodeVoltageMv => _voltage[_cable.ElectrodeIndex];
    public double HighCalciumMm { get; private set; } = GolgiChannelKinetics.RestingCalciumMm;
    public double LowCalciumMm { get; private set; } = GolgiChannelKinetics.RestingCalciumMm;
    public double HighCalciumEnteredMm { get; private set; }
    public double HighCalciumRemovedMm { get; private set; }
    public double LowCalciumEnteredMm { get; private set; }
    public double LowCalciumRemovedMm { get; private set; }
    public double NetMembraneChargeFc { get; private set; }
    public double MaximumChargeBalanceErrorFc { get; private set; }
    public double MaximumCalciumBalanceErrorMm { get; private set; }
    public double MaximumKirchhoffResidualPa { get; private set; }
    public double MinimumVoltageMv { get; private set; }
    public double MaximumVoltageMv { get; private set; }
    public GolgiIonicCurrents LastSomaCurrents { get; private set; }
    public double[] CopyVoltages() => (double[])_voltage.Clone();
    public double[] CopyGates() => _channels.CopyGates();
    public double[] CopySkOccupancy() => _channels.CopySkOccupancy();
    public double[] CopySomaticCrossings() => _spikes.ToArray();
    public GolgiCableNode[] CopyNodes() => _cable.CopyNodes();

    public GolgiReferenceCell(double temperatureCelsius = 23, double initialVoltageMv = -60,
        GolgiDiagnosticBlock block = GolgiDiagnosticBlock.None)
    {
        if (!Enum.IsDefined(block)) throw new ArgumentOutOfRangeException(nameof(block));
        _channels = new(initialVoltageMv, temperatureCelsius: temperatureCelsius);
        _initialVoltage = initialVoltageMv; _block = block;
        _voltage = Enumerable.Repeat(initialVoltageMv, _cable.Count).ToArray();
        _next = new double[_cable.Count]; _conductance = new double[_cable.Count];
        _source = new double[_cable.Count]; _injection = new double[_cable.Count];
        for (int i = 0; i < _cable.Count; ++i)
        {
            _conductance[i] = _cable.Node(i).LeakConductanceNs;
            _source[i] = _conductance[i] * GolgiChannelKinetics.LeakReversalMv;
        }
        MinimumVoltageMv = MaximumVoltageMv = initialVoltageMv;
    }

    // One declared integration interval, with constant imposed diagnostic current.
    // Kinetics are exponential at the interval's starting soma V/high-Ca;
    // membrane/cable use backward Euler at those updated gates and old Nernst Ca;
    // each Ca pool then solves its constant-current removal equation exactly.
    // This coupling is first order. A small timestep is not itself qualification.
    public void Advance(double elapsedMs, double somaCurrentPa = 0, double electrodeCurrentPa = 0)
    {
        if (_faulted) throw new InvalidOperationException("Faulted candidate cannot resume or reset itself.");
        if (!double.IsFinite(elapsedMs) || elapsedMs < 1e-6 || elapsedMs > .025 ||
            !double.IsFinite(somaCurrentPa) || !double.IsFinite(electrodeCurrentPa))
            throw new ArgumentOutOfRangeException(nameof(elapsedMs), "Use a declared 0.000001..0.025 ms step and finite pA inputs.");
        try { AdvanceCore(elapsedMs, somaCurrentPa, electrodeCurrentPa); }
        catch { _faulted = true; throw; }
    }

    private void AdvanceCore(double dt, double somaCurrentPa, double electrodeCurrentPa)
    {
        var previousSoma = _voltage[0];
        _channels.AdvanceVoltageClamp(previousSoma, HighCalciumMm, dt);
        var conductances = _channels.Conductances();
        conductances = _block switch
        {
            GolgiDiagnosticBlock.Passive => new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, .000021),
            GolgiDiagnosticBlock.WithoutPersistentSodium => conductances with { SodiumPersistent = 0 },
            GolgiDiagnosticBlock.WithoutHcn => conductances with { Hcn1 = 0, Hcn2 = 0 },
            GolgiDiagnosticBlock.WithoutSk => conductances with { Sk = 0 },
            _ => conductances
        };
        double hiReversal = GolgiChannelKinetics.CalciumReversal(HighCalciumMm, _channels.TemperatureCelsius);
        double loReversal = GolgiChannelKinetics.CalciumReversal(LowCalciumMm, _channels.TemperatureCelsius);
        double areaToNs = _cable.Node(0).AreaUm2 * 10;
        _conductance[0] = conductances.Total * areaToNs;
        _source[0] = conductances.ReversalWeighted(hiReversal, loReversal) * areaToNs;
        _injection[0] = somaCurrentPa; _injection[_cable.ElectrodeIndex] = electrodeCurrentPa;
        _cable.Solve(_voltage, dt, _conductance, _source, _injection, _next);
        LastSomaCurrents = conductances.Currents(_next[0], hiReversal, loReversal);
        double netCurrent = 0, actualCharge = 0;
        Span<double> residual = stackalloc double[_cable.Count];
        for (int i = 0; i < _cable.Count; ++i)
        {
            var node = _cable.Node(i);
            double ionic = i == 0 ? LastSomaCurrents.Total * areaToNs : _conductance[i] * _next[i] - _source[i];
            residual[i] = node.CapacitancePf * (_next[i] - _voltage[i]) / dt + ionic - _injection[i];
            netCurrent += _injection[i] - ionic;
            actualCharge += node.CapacitancePf * (_next[i] - _initialVoltage);
            MinimumVoltageMv = Math.Min(MinimumVoltageMv, _next[i]);
            MaximumVoltageMv = Math.Max(MaximumVoltageMv, _next[i]);
        }
        for (int i = 1; i < _cable.Count; ++i)
        {
            var node = _cable.Node(i);
            double outward = node.AxialConductanceNs * (_next[i] - _next[node.Parent]);
            residual[i] += outward; residual[node.Parent] -= outward;
        }
        foreach (double value in residual) MaximumKirchhoffResidualPa = Math.Max(MaximumKirchhoffResidualPa, Math.Abs(value));
        NetMembraneChargeFc += netCurrent * dt;
        MaximumChargeBalanceErrorFc = Math.Max(MaximumChargeBalanceErrorFc, Math.Abs(actualCharge - NetMembraneChargeFc));
        var high = AdvanceCalcium(HighCalciumMm, LastSomaCurrents.CalciumHigh, dt);
        var low = AdvanceCalcium(LowCalciumMm, LastSomaCurrents.CalciumLow, dt);
        HighCalciumMm = high.Final; HighCalciumEnteredMm += high.Entered; HighCalciumRemovedMm += high.Removed;
        LowCalciumMm = low.Final; LowCalciumEnteredMm += low.Entered; LowCalciumRemovedMm += low.Removed;
        MaximumCalciumBalanceErrorMm = Math.Max(MaximumCalciumBalanceErrorMm,
            Math.Max(Math.Abs(HighCalciumMm - GolgiChannelKinetics.RestingCalciumMm - HighCalciumEnteredMm + HighCalciumRemovedMm),
                     Math.Abs(LowCalciumMm - GolgiChannelKinetics.RestingCalciumMm - LowCalciumEnteredMm + LowCalciumRemovedMm)));
        if (previousSoma < SpikeThresholdMv && _next[0] >= SpikeThresholdMv)
            _spikes.Add(TimeMs + dt * (SpikeThresholdMv - previousSoma) / (_next[0] - previousSoma));
        _next.CopyTo(_voltage, 0); TimeMs += dt;
    }

    private static (double Final, double Entered, double Removed) AdvanceCalcium(double initial, double outwardDensity, double dt)
    {
        double influx = -outwardDensity * 1e4 / (2 * 96485.33212 * .2);
        double offset = initial - GolgiChannelKinetics.RestingCalciumMm;
        double removedFraction = 1 - Math.Exp(-CalciumPumpPerMs * dt);
        double final = initial + (influx / CalciumPumpPerMs - offset) * removedFraction;
        double removed = (offset - influx / CalciumPumpPerMs) * removedFraction + influx * dt;
        if (!double.IsFinite(final) || final <= 0 || final > .1)
            throw new InvalidOperationException("Calcium outside the candidate domain; no floor or concentration reset.");
        return (final, influx * dt, removed);
    }
}
