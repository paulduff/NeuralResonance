// Isolated coupled-solver candidate for the unchanged Solinas reference model.
// Alexander SDIRK2 control or explicit three-stage third-order candidate.
// No live host, imposed pacemaking, gate clipping or automatic step retry.
internal enum GolgiSdirkOrder { Second = 2, Third = 3 }

internal sealed class GolgiSdirkCell
{
    public const string Version = "isolated-golgi-coupled-sdirk2-v1";
    public const string ThirdOrderVersion = "isolated-golgi-coupled-sdirk3-v1";
    public static readonly double Gamma = 1 - 1 / Math.Sqrt(2);
    // Middle root of 6*g^3 - 18*g^2 + 9*g - 1 = 0. The tableau
    // is documented by Alexander (1977) and INL MOOSE LStableDirk3.
    public const double ThirdGamma = .435866521508458999416019451193556843;
    public static readonly double ThirdA21 = (1 - ThirdGamma) / 2;
    public static readonly double ThirdB1 = -1.5 * ThirdGamma * ThirdGamma + 4 * ThirdGamma - .25;
    public static readonly double ThirdB2 = 1.5 * ThirdGamma * ThirdGamma - 5 * ThirdGamma + 1.25;
    public const double MaximumStepMs = .015625;
    private const double PumpPerMs = 1.3;
    private const double VoltageResidualMv = 1e-10, CalciumResidualUm = 1e-11;
    private const int MaximumIterations = 12;

    private sealed class State
    {
        // Voltage relative to leak reversal avoids a large common DC term in
        // the cable solve. Exported voltage remains the original mV convention.
        public readonly double[] V = new double[135], Gates = new double[18], Sk = new double[6];
        public double High = GolgiChannelKinetics.RestingCalciumMm, Low = GolgiChannelKinetics.RestingCalciumMm;
        public void CopyTo(State target)
        { V.CopyTo(target.V, 0); Gates.CopyTo(target.Gates, 0); Sk.CopyTo(target.Sk, 0); target.High = High; target.Low = Low; }
        public void Extrapolate(State other, double factor, State target)
        {
            for (int i = 0; i < V.Length; ++i) target.V[i] = V[i] + factor * (other.V[i] - V[i]);
            for (int i = 0; i < Gates.Length; ++i) target.Gates[i] = Gates[i] + factor * (other.Gates[i] - Gates[i]);
            for (int i = 0; i < Sk.Length; ++i) target.Sk[i] = Sk[i] + factor * (other.Sk[i] - Sk[i]);
            target.High = High + factor * (other.High - High); target.Low = Low + factor * (other.Low - Low);
        }
        public void ThirdStageBasis(State first, State second, State secondBasis, State target)
        {
            // h*f_j = (Y_j - basis_j)/gamma. These combinations are stage
            // right-hand sides, not accepted biological states; do not clip them.
            double a = ThirdB1 / ThirdGamma, b = ThirdB2 / ThirdGamma;
            for (int i = 0; i < V.Length; ++i) target.V[i] = V[i] + a * (first.V[i]-V[i]) + b * (second.V[i]-secondBasis.V[i]);
            for (int i = 0; i < Gates.Length; ++i) target.Gates[i] = Gates[i] + a * (first.Gates[i]-Gates[i]) + b * (second.Gates[i]-secondBasis.Gates[i]);
            for (int i = 0; i < Sk.Length; ++i) target.Sk[i] = Sk[i] + a * (first.Sk[i]-Sk[i]) + b * (second.Sk[i]-secondBasis.Sk[i]);
            target.High = High + a * (first.High-High) + b * (second.High-secondBasis.High);
            target.Low = Low + a * (first.Low-Low) + b * (second.Low-secondBasis.Low);
        }
    }

    private readonly GolgiCable _cable = new();
    private readonly GolgiDiagnosticBlock _block;
    private readonly double _temperature, _initialOffset, _toleranceScale;
    private readonly State _state = new(), _first = new(), _secondBase = new(), _second = new(), _scratch = new(), _trial = new();
    private readonly State? _thirdBase, _third;
    private readonly double[] _conductance = new double[135], _source = new double[135], _injection = new double[135];
    private readonly double[] _ledgers = new double[5], _ledgerErrors = new double[5];
    private readonly List<double> _spikes = [];
    private bool _faulted;
    public double TimeMs { get; private set; }
    public double SomaVoltageMv => _state.V[0] + GolgiChannelKinetics.LeakReversalMv;
    public double HighCalciumMm => _state.High;
    public double LowCalciumMm => _state.Low;
    public double NetMembraneChargeFc => _ledgers[0];
    public double HighCalciumEnteredMm => _ledgers[1];
    public double HighCalciumRemovedMm => _ledgers[2];
    public double LowCalciumEnteredMm => _ledgers[3];
    public double LowCalciumRemovedMm => _ledgers[4];
    public double MaximumChargeBalanceErrorFc { get; private set; }
    public double MaximumCalciumBalanceErrorMm { get; private set; }
    public double MaximumKirchhoffResidualPa { get; private set; }
    public double MaximumGateStageResidualPerMs { get; private set; }
    public double MaximumCalciumStageResidualMmPerMs { get; private set; }
    public double MaximumSkMassError { get; private set; }
    public double MaximumScaledNonlinearResidual { get; private set; }
    public long NonlinearEvaluations { get; private set; }
    public int MaximumNewtonIterations { get; private set; }
    public bool Faulted => _faulted;
    public GolgiSdirkOrder Order { get; }
    public string SolverVersion => Order == GolgiSdirkOrder.Second ? Version : ThirdOrderVersion;
    public double[] CopyVoltages() => _state.V.Select(v => v + GolgiChannelKinetics.LeakReversalMv).ToArray();
    public double[] CopyGates() => (double[])_state.Gates.Clone();
    public double[] CopySkOccupancy() => (double[])_state.Sk.Clone();
    public double[] CopySomaticCrossings() => _spikes.ToArray();

    // Physical trace, not a restart checkpoint: 135 shifted cable voltages,
    // 18 gates, six SK occupancies, then two calcium pools in micromolar.
    internal void CopyPhysicalTrace(Span<double> destination)
    {
        if (destination.Length != 161) throw new ArgumentException("Require all 161 physical values.");
        _state.V.AsSpan().CopyTo(destination); _state.Gates.AsSpan().CopyTo(destination[135..]);
        _state.Sk.AsSpan().CopyTo(destination[153..]);
        destination[159] = _state.High * 1000; destination[160] = _state.Low * 1000;
    }

    public GolgiSdirkCell(double temperatureCelsius = 23, double initialVoltageMv = -60,
        GolgiDiagnosticBlock block = GolgiDiagnosticBlock.None, double toleranceScale = 1,
        GolgiSdirkOrder order = GolgiSdirkOrder.Second)
    {
        if (!Enum.IsDefined(block) || !Enum.IsDefined(order) || !double.IsFinite(toleranceScale) || toleranceScale is < .125 or > 1)
            throw new ArgumentException("Declare a valid block, SDIRK order and a 0.125..1 nonlinear tolerance scale.");
        Order = order;
        if (order == GolgiSdirkOrder.Third) { _thirdBase = new(); _third = new(); }
        var channels = new GolgiChannelKinetics(initialVoltageMv, temperatureCelsius: temperatureCelsius);
        _temperature = temperatureCelsius; _block = block; _toleranceScale = toleranceScale;
        _initialOffset = initialVoltageMv - GolgiChannelKinetics.LeakReversalMv;
        Array.Fill(_state.V, _initialOffset);
        channels.CopyGates().CopyTo(_state.Gates, 0); channels.CopySkOccupancy().CopyTo(_state.Sk, 0);
        Validate(_state);
    }

    public void Advance(double elapsedMs, double somaCurrentPa = 0, double electrodeCurrentPa = 0)
    {
        if (_faulted) throw new InvalidOperationException("Faulted SDIRK candidate cannot resume.");
        if (!double.IsFinite(elapsedMs) || elapsedMs is < 1e-6 or > MaximumStepMs ||
            !double.IsFinite(TimeMs + elapsedMs) || TimeMs + elapsedMs == TimeMs ||
            !double.IsFinite(somaCurrentPa) || !double.IsFinite(electrodeCurrentPa))
            throw new ArgumentOutOfRangeException(nameof(elapsedMs));
        try
        {
            _injection[0] = somaCurrentPa; _injection[_cable.ElectrodeIndex] = electrodeCurrentPa;
            bool third = Order == GolgiSdirkOrder.Third;
            double alpha = (third ? ThirdGamma : Gamma) * elapsedMs;
            SolveStage(_state, alpha, _state, _first);
            _state.Extrapolate(_first, third ? ThirdA21 / ThirdGamma : (1 - Gamma) / Gamma, _secondBase);
            SolveStage(_secondBase, alpha, _first, _second);
            // Accounting uses actual stage RHSs and the RK weights. It is not
            // reconstructed from the accepted calcium or voltage endpoint.
            Span<double> f1 = stackalloc double[5], f2 = stackalloc double[5], f3 = stackalloc double[5];
            AuditStage(_state, _first, alpha, f1); AuditStage(_secondBase, _second, alpha, f2);
            State accepted = _second;
            if (third)
            {
                _state.ThirdStageBasis(_first, _second, _secondBase, _thirdBase!);
                SolveStage(_thirdBase!, alpha, _second, _third!);
                AuditStage(_thirdBase!, _third!, alpha, f3);
                accepted = _third!; // Stiff accuracy: accepted state is the last solved stage.
            }
            Span<double> nextLedger = stackalloc double[5], nextErrors = stackalloc double[5];
            for (int i = 0; i < 5; ++i)
            {
                double increment = (third
                    ? elapsedMs * (ThirdB1 * f1[i] + ThirdB2 * f2[i] + ThirdGamma * f3[i])
                    : elapsedMs * ((1 - Gamma) * f1[i] + Gamma * f2[i])) - _ledgerErrors[i];
                nextLedger[i] = _ledgers[i] + increment;
                nextErrors[i] = (nextLedger[i] - _ledgers[i]) - increment;
                if (!double.IsFinite(nextLedger[i]) || !double.IsFinite(nextErrors[i])) throw new InvalidOperationException("Non-finite accounting.");
            }
            double charge = 0;
            for (int i = 0; i < 135; ++i) charge += _cable.Node(i).CapacitancePf * (accepted.V[i] - _initialOffset);
            double calciumError = Math.Max(
                Math.Abs(accepted.High - GolgiChannelKinetics.RestingCalciumMm - nextLedger[1] + nextLedger[2]),
                Math.Abs(accepted.Low - GolgiChannelKinetics.RestingCalciumMm - nextLedger[3] + nextLedger[4]));
            double previousSoma = SomaVoltageMv, nextSoma = accepted.V[0] + GolgiChannelKinetics.LeakReversalMv;
            if (previousSoma < GolgiReferenceCell.SpikeThresholdMv && nextSoma >= GolgiReferenceCell.SpikeThresholdMv)
                _spikes.Add(TimeMs + elapsedMs * (GolgiReferenceCell.SpikeThresholdMv - previousSoma) / (nextSoma - previousSoma));
            MaximumChargeBalanceErrorFc = Math.Max(MaximumChargeBalanceErrorFc, Math.Abs(charge - nextLedger[0]));
            MaximumCalciumBalanceErrorMm = Math.Max(MaximumCalciumBalanceErrorMm, calciumError);
            nextLedger.CopyTo(_ledgers); nextErrors.CopyTo(_ledgerErrors); accepted.CopyTo(_state); TimeMs += elapsedMs;
        }
        catch { _faulted = true; throw; }
    }

    private static void Validate(State state)
    {
        foreach (double v in state.V) if (!double.IsFinite(v)) throw new InvalidOperationException("Non-finite cable state.");
        foreach (double gate in state.Gates) if (!double.IsFinite(gate) || gate is < 0 or > 1) throw new InvalidOperationException("Gate left domain; no clipping.");
        foreach (double occupancy in state.Sk) if (!double.IsFinite(occupancy) || occupancy is < 0 or > 1) throw new InvalidOperationException("SK occupancy left domain; no normalization.");
        if (Math.Abs(state.Sk.Sum() - 1) > 1e-8 || !double.IsFinite(state.High) || !double.IsFinite(state.Low) ||
            state.High is <= 0 or > .1 || state.Low is <= 0 or > .1)
            throw new InvalidOperationException("Calcium/SK state left domain.");
    }

    // Solve only V_soma and two Ca pools nonlinearly. At each trial those three
    // values determine the linear gate, Markov and Hines systems exactly.
    // Calcium unknowns/residuals are in micromolar to improve Newton scaling.
    private void SolveStage(State basis, double alpha, State guess, State output)
    {
        Span<double> x = stackalloc double[] { guess.V[0] + GolgiChannelKinetics.LeakReversalMv, guess.High * 1000, guess.Low * 1000 };
        Span<double> residual = stackalloc double[3], plus = stackalloc double[3], minus = stackalloc double[3];
        Span<double> trialX = stackalloc double[3], trialResidual = stackalloc double[3], matrix = stackalloc double[9], step = stackalloc double[3];
        for (int iteration = 0; iteration <= MaximumIterations; ++iteration)
        {
            Evaluate(basis, alpha, x, output, residual);
            double norm = Norm(residual);
            if (norm <= 1)
            {
                Validate(output);
                MaximumScaledNonlinearResidual = Math.Max(MaximumScaledNonlinearResidual, norm);
                MaximumNewtonIterations = Math.Max(MaximumNewtonIterations, iteration);
                return;
            }
            if (iteration == MaximumIterations) break;
            for (int column = 0; column < 3; ++column)
            {
                double delta = 6e-6 * Math.Max(Math.Abs(x[column]), column == 0 ? 1 : .05);
                x.CopyTo(trialX); trialX[column] += delta; Evaluate(basis, alpha, trialX, _scratch, plus);
                x.CopyTo(trialX); trialX[column] -= delta; Evaluate(basis, alpha, trialX, _scratch, minus);
                for (int row = 0; row < 3; ++row) matrix[3 * row + column] = (plus[row] - minus[row]) / (2 * delta);
            }
            for (int i = 0; i < 3; ++i) step[i] = -residual[i];
            SolveDense(matrix, step, 3);
            bool accepted = false;
            for (int reduction = 0; reduction <= 12; ++reduction)
            {
                double fraction = Math.ScaleB(1, -reduction);
                for (int i = 0; i < 3; ++i) trialX[i] = x[i] + fraction * step[i];
                if (trialX[0] is < -120 or > 100 || trialX[1] is <= 0 or > 100 || trialX[2] is <= 0 or > 100) continue;
                try { Evaluate(basis, alpha, trialX, _trial, trialResidual); }
                catch (ArgumentException) { continue; }
                catch (InvalidOperationException) { continue; }
                if (Norm(trialResidual) >= norm) continue;
                trialX.CopyTo(x); accepted = true; break;
            }
            if (!accepted) throw new InvalidOperationException("SDIRK stage Newton line search failed; no reset or step substitution.");
        }
        throw new InvalidOperationException("SDIRK nonlinear stage did not converge.");
    }

    private double Norm(ReadOnlySpan<double> residual) => Math.Max(Math.Abs(residual[0]) / VoltageResidualMv,
        Math.Max(Math.Abs(residual[1]), Math.Abs(residual[2])) / CalciumResidualUm) / _toleranceScale;

    private void Evaluate(State basis, double alpha, ReadOnlySpan<double> x, State output, Span<double> residual)
    {
        ++NonlinearEvaluations;
        double v = x[0], high = x[1] / 1000, low = x[2] / 1000;
        Span<GolgiGateRate> rates = stackalloc GolgiGateRate[18];
        GolgiChannelKinetics.FillRates(v, high, _temperature, rates);
        for (int i = 0; i < 18; ++i)
        { double q = alpha / rates[i].TauMs; output.Gates[i] = (basis.Gates[i] + q * rates[i].Equilibrium) / (1 + q); }
        Span<double> generator = stackalloc double[36], matrix = stackalloc double[36], sk = stackalloc double[6];
        FillSkGenerator(high, generator);
        for (int i = 0; i < 36; ++i) matrix[i] = -alpha * generator[i];
        for (int i = 0; i < 6; ++i) matrix[6 * i + i] += 1;
        basis.Sk.AsSpan().CopyTo(sk); SolveDense(matrix, sk, 6); sk.CopyTo(output.Sk);
        var g = Conductances(output.Gates, output.Sk);
        double hi = GolgiChannelKinetics.CalciumReversal(high, _temperature), lo = GolgiChannelKinetics.CalciumReversal(low, _temperature);
        for (int i = 0; i < 135; ++i) { _conductance[i] = _cable.Node(i).LeakConductanceNs; _source[i] = 0; }
        double area = _cable.Node(0).AreaUm2 * 10;
        _conductance[0] = g.Total * area;
        _source[0] = (g.ReversalWeighted(hi, lo) - g.Total * GolgiChannelKinetics.LeakReversalMv) * area;
        _cable.Solve(basis.V, alpha, _conductance, _source, _injection, output.V);
        var currents = g.Currents(v, hi, lo);
        output.High = (basis.High + alpha * (Influx(currents.CalciumHigh) + PumpPerMs * GolgiChannelKinetics.RestingCalciumMm)) / (1 + alpha * PumpPerMs);
        output.Low = (basis.Low + alpha * (Influx(currents.CalciumLow) + PumpPerMs * GolgiChannelKinetics.RestingCalciumMm)) / (1 + alpha * PumpPerMs);
        residual[0] = v - (output.V[0] + GolgiChannelKinetics.LeakReversalMv);
        residual[1] = x[1] - 1000 * output.High; residual[2] = x[2] - 1000 * output.Low;
        foreach (double value in residual) if (!double.IsFinite(value)) throw new InvalidOperationException("Non-finite stage residual.");
    }

    private static double Influx(double currentDensity) => -currentDensity * 1e4 / (2 * 96485.33212 * .2);

    private GolgiIonicConductances Conductances(double[] gates, double[] sk)
    {
        var g = GolgiChannelKinetics.ConductancesForState(gates, sk);
        return _block switch
        {
            GolgiDiagnosticBlock.Passive => new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, .000021),
            GolgiDiagnosticBlock.WithoutPersistentSodium => g with { SodiumPersistent = 0 },
            GolgiDiagnosticBlock.WithoutHcn => g with { Hcn1 = 0, Hcn2 = 0 },
            GolgiDiagnosticBlock.WithoutSk => g with { Sk = 0 }, _ => g
        };
    }

    private void FillSkGenerator(double calciumMm, Span<double> matrix)
    {
        matrix.Clear(); double q = Math.Pow(3, (_temperature - 23) / 10), ca = calciumMm / 3;
        ReadOnlySpan<int> from = [0, 1, 2, 2, 3], to = [1, 2, 3, 4, 5];
        ReadOnlySpan<double> forward = [200 * ca * q, 160 * ca * q, 80 * ca * q, .16 * q, 1.2 * q];
        ReadOnlySpan<double> reverse = [.08 * q, .08 * q, .2 * q, q, .1 * q];
        for (int i = 0; i < 5; ++i)
        {
            int a = from[i], b = to[i];
            matrix[6 * a + a] -= forward[i]; matrix[6 * b + a] += forward[i];
            matrix[6 * b + b] -= reverse[i]; matrix[6 * a + b] += reverse[i];
        }
    }

    private void AuditStage(State basis, State stage, double alpha, Span<double> ledgerRates)
    {
        var g = Conductances(stage.Gates, stage.Sk);
        double v = stage.V[0] + GolgiChannelKinetics.LeakReversalMv;
        var currents = g.Currents(v, GolgiChannelKinetics.CalciumReversal(stage.High, _temperature), GolgiChannelKinetics.CalciumReversal(stage.Low, _temperature));
        Span<double> residual = stackalloc double[135]; ledgerRates.Clear();
        for (int i = 0; i < 135; ++i)
        {
            var node = _cable.Node(i);
            double ionic = i == 0 ? currents.Total * node.AreaUm2 * 10 : node.LeakConductanceNs * stage.V[i];
            residual[i] = node.CapacitancePf * (stage.V[i] - basis.V[i]) / alpha + ionic - _injection[i];
            ledgerRates[0] += _injection[i] - ionic;
        }
        for (int i = 1; i < 135; ++i)
        { var node = _cable.Node(i); double outward = node.AxialConductanceNs * (stage.V[i] - stage.V[node.Parent]); residual[i] += outward; residual[node.Parent] -= outward; }
        foreach (double value in residual) MaximumKirchhoffResidualPa = Math.Max(MaximumKirchhoffResidualPa, Math.Abs(value));
        ledgerRates[1] = Influx(currents.CalciumHigh); ledgerRates[2] = PumpPerMs * (stage.High - GolgiChannelKinetics.RestingCalciumMm);
        ledgerRates[3] = Influx(currents.CalciumLow); ledgerRates[4] = PumpPerMs * (stage.Low - GolgiChannelKinetics.RestingCalciumMm);
        MaximumCalciumStageResidualMmPerMs = Math.Max(MaximumCalciumStageResidualMmPerMs, Math.Max(
            Math.Abs((stage.High - basis.High) / alpha - ledgerRates[1] + ledgerRates[2]),
            Math.Abs((stage.Low - basis.Low) / alpha - ledgerRates[3] + ledgerRates[4])));
        Span<GolgiGateRate> rates = stackalloc GolgiGateRate[18]; GolgiChannelKinetics.FillRates(v, stage.High, _temperature, rates);
        for (int i = 0; i < 18; ++i) MaximumGateStageResidualPerMs = Math.Max(MaximumGateStageResidualPerMs,
            Math.Abs((stage.Gates[i] - basis.Gates[i]) / alpha - (rates[i].Equilibrium - stage.Gates[i]) / rates[i].TauMs));
        Span<double> q = stackalloc double[36]; FillSkGenerator(stage.High, q);
        for (int i = 0; i < 6; ++i)
        {
            double rhs = 0; for (int j = 0; j < 6; ++j) rhs += q[6 * i + j] * stage.Sk[j];
            MaximumGateStageResidualPerMs = Math.Max(MaximumGateStageResidualPerMs, Math.Abs((stage.Sk[i] - basis.Sk[i]) / alpha - rhs));
        }
        MaximumSkMassError = Math.Max(MaximumSkMassError, Math.Abs(stage.Sk.Sum() - 1));
    }

    private static void SolveDense(Span<double> matrix, Span<double> rhs, int n)
    {
        for (int column = 0; column < n; ++column)
        {
            int pivot = column;
            for (int row = column + 1; row < n; ++row) if (Math.Abs(matrix[n * row + column]) > Math.Abs(matrix[n * pivot + column])) pivot = row;
            if (!double.IsFinite(matrix[n * pivot + column]) || Math.Abs(matrix[n * pivot + column]) < 1e-18)
                throw new InvalidOperationException("Singular stage system.");
            if (pivot != column)
            {
                for (int j = column; j < n; ++j) (matrix[n * column + j], matrix[n * pivot + j]) = (matrix[n * pivot + j], matrix[n * column + j]);
                (rhs[column], rhs[pivot]) = (rhs[pivot], rhs[column]);
            }
            for (int row = column + 1; row < n; ++row)
            {
                double factor = matrix[n * row + column] / matrix[n * column + column];
                for (int j = column + 1; j < n; ++j) matrix[n * row + j] -= factor * matrix[n * column + j];
                rhs[row] -= factor * rhs[column];
            }
        }
        for (int row = n - 1; row >= 0; --row)
        { for (int j = row + 1; j < n; ++j) rhs[row] -= matrix[n * row + j] * rhs[j]; rhs[row] /= matrix[n * row + row]; if (!double.IsFinite(rhs[row])) throw new InvalidOperationException("Non-finite stage solution."); }
    }
}
