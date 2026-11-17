using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NRE.SimAvatar;

// Independently expressed reduced hair-cell equations from Govindaraju et al.
// PNAS 2023, doi:10.1073/pnas.2207466120, supplement S1/S2 and pp.16-18.
// Only the reconciled hair-cell terms are used. See the Nov17 equation audit.
// Fixed baths, room-temperature kinetics, no spatial cleft/calyx, no adaptation
// or quantal release. This type is isolated; no live Avatar/Unity caller exists.
public sealed record AvatarHairCellParameters(
    double BasolateralAreaSquareMicrometers, double CleftFacingAreaSquareMicrometers,
    double ApicalCapacitancePf, double SpecificCapacitancePfPerSquareMicrometer,
    double MetMaximumNs, double PotassiumLowMaximumNs, double CalciumMaximumNs,
    double Hcn1MaximumNs, double PumpDensityPerSquareMicrometer,
    double EndolymphPotentialMv, double MetReversalMv)
{
    public static AvatarHairCellParameters TypeIReference { get; } = new(
        340.21, 294.63, 3, .01, 5, 80, .5, 4.2, 1000, 5, 0);

    public void Validate()
    {
        double[] positive = [BasolateralAreaSquareMicrometers, CleftFacingAreaSquareMicrometers,
            ApicalCapacitancePf, SpecificCapacitancePfPerSquareMicrometer];
        double[] nonnegative = [MetMaximumNs, PotassiumLowMaximumNs, CalciumMaximumNs,
            Hcn1MaximumNs, PumpDensityPerSquareMicrometer];
        if (positive.Any(x => !double.IsFinite(x) || x <= 0) ||
            nonnegative.Any(x => !double.IsFinite(x) || x < 0) ||
            CleftFacingAreaSquareMicrometers > BasolateralAreaSquareMicrometers ||
            !double.IsFinite(EndolymphPotentialMv) || !double.IsFinite(MetReversalMv))
            throw new ArgumentException("Require explicit finite hair-cell geometry and channel parameters.");
    }
}

// These six concentrations and outside voltage are held reservoirs in this
// reduction. They are not maintained by an implemented metabolic/cleft model.
public sealed record AvatarHairCellBath(double PotassiumOutsideMm, double SodiumOutsideMm,
    double CalciumOutsideMm, double PotassiumInsideMm, double SodiumInsideMm,
    double CalciumInsideMm, double BasolateralOutsidePotentialMv)
{
    public static AvatarHairCellBath Reference { get; } = new(5, 140, 1.3, 150, 12, .001, 0);

    public void Validate()
    {
        double[] concentrations = [PotassiumOutsideMm, SodiumOutsideMm, CalciumOutsideMm,
            PotassiumInsideMm, SodiumInsideMm, CalciumInsideMm];
        if (concentrations.Any(x => !double.IsFinite(x) || x <= 0) ||
            !double.IsFinite(BasolateralOutsidePotentialMv))
            throw new ArgumentException("Require explicit positive finite fixed-bath concentrations.");
    }
}

public readonly record struct AvatarHairCellGateRate(double Equilibrium, double TauMs);
public readonly record struct AvatarHairCellRates(AvatarHairCellGateRate PotassiumLow,
    AvatarHairCellGateRate Calcium, AvatarHairCellGateRate Hcn1);
public readonly record struct AvatarHairCellCurrents(double MetPa, double PotassiumLowPa,
    double CalciumPa, double HcnPotassiumPa, double HcnSodiumPa,
    double PumpPotassiumPa, double PumpSodiumPa)
{
    // Positive current is outward positive charge. Currents are whole-cell pA,
    // not densities; pA*ms and pF*mV are both femtocoulombs.
    public double TotalOutwardPa => MetPa + PotassiumLowPa + CalciumPa + HcnPotassiumPa +
        HcnSodiumPa + PumpPotassiumPa + PumpSodiumPa;
    public double BasolateralPotassiumPa => PotassiumLowPa + HcnPotassiumPa + PumpPotassiumPa;
    public double BasolateralSodiumPa => HcnSodiumPa + PumpSodiumPa;
}

public sealed class AvatarHairCellKernel
{
    public const string Version = "isolated-type-I-hair-cell-fixed-bath-v1";
    public AvatarHairCellParameters Parameters { get; }
    public AvatarHairCellBath Bath { get; }
    public double CapacitancePf { get; }
    public double PotassiumReversalMv { get; }
    public double SodiumReversalMv { get; }
    public double CalciumReversalMv { get; }
    public double PumpTurnoverChargePa { get; }
    public string Fingerprint { get; }

    public AvatarHairCellKernel(AvatarHairCellParameters parameters, AvatarHairCellBath bath)
    {
        ArgumentNullException.ThrowIfNull(parameters); ArgumentNullException.ThrowIfNull(bath);
        parameters.Validate(); bath.Validate(); Parameters = parameters; Bath = bath;
        CapacitancePf = parameters.ApicalCapacitancePf +
            parameters.SpecificCapacitancePfPerSquareMicrometer * parameters.BasolateralAreaSquareMicrometers;
        PotassiumReversalMv = 26 * (Math.Log(bath.PotassiumOutsideMm) - Math.Log(bath.PotassiumInsideMm));
        SodiumReversalMv = 26 * (Math.Log(bath.SodiumOutsideMm) - Math.Log(bath.SodiumInsideMm));
        CalciumReversalMv = 13 * (Math.Log(bath.CalciumOutsideMm) - Math.Log(bath.CalciumInsideMm));
        // Source convention: F=96.485 C/mmol, Avogadro scaling=602000,
        // turnover=.1/ms. Pump density is on the cleft-facing membrane only.
        double occupancy = bath.PotassiumOutsideMm / (bath.PotassiumOutsideMm + 1.5);
        PumpTurnoverChargePa = 96.485 * parameters.PumpDensityPerSquareMicrometer * .1 / 602000 *
            occupancy * occupancy * parameters.CleftFacingAreaSquareMicrometers;
        double[] derived = [CapacitancePf, PotassiumReversalMv, SodiumReversalMv,
            CalciumReversalMv, PumpTurnoverChargePa];
        if (derived.Any(x => !double.IsFinite(x)) || CapacitancePf <= 0)
            throw new ArgumentException("Hair-cell parameters exceed the numerical operating range.");
        Fingerprint = Hash(JsonSerializer.Serialize(new { Version, parameters, bath,
            Temperature = "unscaled room-temperature source kinetics", IntracellularConcentrations = "fixed reservoirs" }));
    }

    public static double MetOpenProbability(double displacementNanometers)
    {
        Finite(displacementNanometers);
        double scaled = displacementNanometers / 1000 + .2;
        return Sigmoid(4.05 * (scaled - .39)) * Sigmoid(14.5 * (scaled - .25));
    }

    public static AvatarHairCellRates Rates(double membraneVoltageMv)
    {
        Finite(membraneVoltageMv);
        double v = membraneVoltageMv;
        var rates = new AvatarHairCellRates(
            new(Sigmoid((v + 80) / 2.84), 429.7 * Math.Exp(-.2826 * (v + 80) / 2.84) + 10),
            new(Sigmoid((v + 44) / 5.8), .6),
            new(Sigmoid(-(v + 90) / 6.8),
                209.479 + (Math.Exp((v + 80.646) / 6.916) + Math.Exp((v + 80.646) / 14.881)) / 2551.988));
        Validate(rates.PotassiumLow); Validate(rates.Calcium); Validate(rates.Hcn1);
        return rates;
    }

    public AvatarHairCellCurrents Currents(double intracellularVoltageMv,
        double potassiumLowGate, double calciumGate, double hcn1Gate, double displacementNanometers)
    {
        Finite(intracellularVoltageMv); Probability(potassiumLowGate); Probability(calciumGate); Probability(hcn1Gate);
        double v = intracellularVoltageMv - Bath.BasolateralOutsidePotentialMv;
        Finite(v);
        var p = Parameters;
        var currents = new AvatarHairCellCurrents(
            p.MetMaximumNs * MetOpenProbability(displacementNanometers) * (intracellularVoltageMv - p.MetReversalMv - p.EndolymphPotentialMv),
            p.PotassiumLowMaximumNs * potassiumLowGate * (v - PotassiumReversalMv),
            p.CalciumMaximumNs * calciumGate * (v - CalciumReversalMv),
            .8 * p.Hcn1MaximumNs * hcn1Gate * (v - PotassiumReversalMv),
            .2 * p.Hcn1MaximumNs * hcn1Gate * (v - SodiumReversalMv),
            -2 * PumpTurnoverChargePa, 3 * PumpTurnoverChargePa);
        if (!double.IsFinite(currents.TotalOutwardPa))
            throw new ArithmeticException("Nonfinite hair-cell current; do not clip or replace it.");
        return currents;
    }

    // Explicit voltage-clamp analytic control, not a free-cell integrator.
    public static double RelaxClampedGate(double gate, AvatarHairCellGateRate rate, double elapsedMs)
    {
        Probability(gate); Validate(rate); Finite(elapsedMs);
        if (elapsedMs < 0) throw new ArgumentOutOfRangeException(nameof(elapsedMs));
        return rate.Equilibrium + (gate - rate.Equilibrium) * Math.Exp(-elapsedMs / rate.TauMs);
    }

    internal static void Validate(AvatarHairCellGateRate rate)
    {
        Probability(rate.Equilibrium);
        if (!double.IsFinite(rate.TauMs) || rate.TauMs <= 0)
            throw new ArithmeticException("Nonfinite/nonpositive hair-cell gate time constant.");
    }
    internal static void Probability(double value)
    {
        if (!double.IsFinite(value) || value is < 0 or > 1)
            throw new ArgumentException("A gate must be a finite probability; no clipping is applied.");
    }
    internal static void Finite(double value)
    {
        if (!double.IsFinite(value)) throw new ArgumentException("Finite physical input required.");
    }
    private static double Sigmoid(double x)
    {
        if (x >= 0) return 1 / (1 + Math.Exp(-x));
        double e = Math.Exp(x); return e / (1 + e);
    }
    internal static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}

public sealed record AvatarHairCellState(Guid SessionId, Guid CellId, string Version, string Fingerprint,
    long CompletedSteps, double TimeMs, double StepMs, double[] Values, double[] AdditionResiduals,
    double MaximumChargeResidualFc, bool Faulted, string? FaultReason, double[]? FailedCandidate);

// A reduced nonspiking receptor cell. It has no outgoing spike/event shortcut.
// A future calyx/afferent stage must turn receptor currents into actual neuronal
// activity. The caller supplies a complete held-displacement interval explicitly;
// this class does not infer it from a missing/late body observation.
public sealed class AvatarVestibularHairCell
{
    public const string Version = AvatarHairCellKernel.Version + "+local-clock-rk4-compensated-v1";
    public const int StateCount = 11;
    public const double MaximumStepMs = .05;
    // 0 voltage; 1 K,L gate; 2 Ca gate; 3 HCN1 gate; 4..10 outward charge
    // integrals in the same order as AvatarHairCellCurrents (all fC).
    private readonly double[] _state = new double[StateCount], _error = new double[StateCount];
    private readonly int _owner = Environment.CurrentManagedThreadId;
    private readonly Guid _sessionId, _cellId;
    private readonly double _epochMs, _initialVoltageMv, _stepMs;
    private long _steps;
    private bool _faulted;
    private string? _faultReason;
    private double[]? _failedCandidate;
    private double _maximumChargeResidualFc;
    public AvatarHairCellKernel Kernel { get; }
    public string Fingerprint { get; }

    public AvatarVestibularHairCell(Guid sessionId, Guid cellId, AvatarHairCellKernel kernel,
        double initialVoltageMv, double stepMs, double epochMs = 0)
    {
        ArgumentNullException.ThrowIfNull(kernel);
        AvatarHairCellKernel.Finite(initialVoltageMv); AvatarHairCellKernel.Finite(stepMs); AvatarHairCellKernel.Finite(epochMs);
        if (sessionId == Guid.Empty || cellId == Guid.Empty || stepMs <= 0 || stepMs > MaximumStepMs || epochMs < 0 || epochMs + stepMs <= epochMs)
            throw new ArgumentException("Require stable identity, an explicit representable epoch and bounded integration step.");
        _sessionId = sessionId; _cellId = cellId; Kernel = kernel;
        _epochMs = epochMs; _stepMs = stepMs; _initialVoltageMv = initialVoltageMv;
        var rates = AvatarHairCellKernel.Rates(initialVoltageMv - kernel.Bath.BasolateralOutsidePotentialMv);
        _state[0] = initialVoltageMv; _state[1] = rates.PotassiumLow.Equilibrium;
        _state[2] = rates.Calcium.Equilibrium; _state[3] = rates.Hcn1.Equilibrium;
        // Gates are voltage-conditioned; this is NOT an assertion that the
        // free membrane is at electrical equilibrium or has already settled.
        Fingerprint = AvatarHairCellKernel.Hash(JsonSerializer.Serialize(new { Version, kernel.Fingerprint,
            initialVoltageMv, stepMs, epochMs, Initialization = "voltage-conditioned gates; zero charge integrals" }));
    }

    public AvatarHairCellState Capture()
    {
        RequireOwner();
        return new(_sessionId, _cellId, Version, Fingerprint, _steps, TimeAt(_steps), _stepMs,
            (double[])_state.Clone(), (double[])_error.Clone(), _maximumChargeResidualFc,
            _faulted, _faultReason, _failedCandidate is null ? null : (double[])_failedCandidate.Clone());
    }

    public void AdvanceHeldDisplacement(long stepCount, double displacementNanometers)
    {
        RequireOwner();
        if (_faulted) throw new InvalidOperationException("Faulted hair cell cannot silently restart.");
        AvatarHairCellKernel.Finite(displacementNanometers);
        if (stepCount < 0) throw new ArgumentOutOfRangeException(nameof(stepCount));
        long end = checked(_steps + stepCount);
        if (stepCount > 0 && (!double.IsFinite(TimeAt(end)) || TimeAt(end) <= TimeAt(end - 1)))
            throw new ArgumentOutOfRangeException(nameof(stepCount), "Cell clock cannot represent the requested endpoint.");
        while (_steps < end)
        {
            try
            {
                if (TimeAt(_steps + 1) <= TimeAt(_steps))
                    throw new ArithmeticException("Cell clock lost resolution; preserve the accepted prefix.");
                AdvanceOne(displacementNanometers); _steps++;
            }
            catch (Exception error) { _faulted = true; _faultReason = error.ToString(); throw; }
        }
    }

    private void AdvanceOne(double displacement)
    {
        Span<double> k1 = stackalloc double[StateCount], k2 = stackalloc double[StateCount],
            k3 = stackalloc double[StateCount], k4 = stackalloc double[StateCount],
            stage = stackalloc double[StateCount], next = stackalloc double[StateCount], residual = stackalloc double[StateCount];
        int diagnosticStage = 0;
        try
        {
            Derivative(_state, k1, displacement);
            for (int i = 0; i < StateCount; i++) stage[i] = _state[i] + _stepMs * .5 * k1[i];
            diagnosticStage = 1;
            Derivative(stage, k2, displacement);
            for (int i = 0; i < StateCount; i++) stage[i] = _state[i] + _stepMs * .5 * k2[i];
            Derivative(stage, k3, displacement);
            for (int i = 0; i < StateCount; i++) stage[i] = _state[i] + _stepMs * k3[i];
            Derivative(stage, k4, displacement);
            for (int i = 0; i < StateCount; i++)
            {
                double delta = _stepMs / 6 * (k1[i] + 2 * k2[i] + 2 * k3[i] + k4[i]) - _error[i];
                next[i] = _state[i] + delta; residual[i] = (next[i] - _state[i]) - delta;
            }
            diagnosticStage = 2;
            ValidateState(next);
            double outwardCharge = 0;
            for (int i = 4; i < StateCount; i++) outwardCharge += next[i];
            double chargeResidual = Math.Abs(Kernel.CapacitancePf * (next[0] - _initialVoltageMv) + outwardCharge);
            if (!double.IsFinite(chargeResidual)) throw new ArithmeticException("Nonfinite hair-cell charge ledger.");
            next.CopyTo(_state); residual.CopyTo(_error); _failedCandidate = null;
            _maximumChargeResidualFc = Math.Max(_maximumChargeResidualFc, chargeResidual);
        }
        catch
        {
            _failedCandidate = diagnosticStage switch
            {
                1 => stage.ToArray(), 2 => next.ToArray(), _ => (double[])_state.Clone()
            };
            throw;
        }
    }

    private void Derivative(ReadOnlySpan<double> state, Span<double> derivative, double displacement)
    {
        ValidateState(state);
        var rates = AvatarHairCellKernel.Rates(state[0] - Kernel.Bath.BasolateralOutsidePotentialMv);
        var currents = Kernel.Currents(state[0], state[1], state[2], state[3], displacement);
        derivative[0] = -currents.TotalOutwardPa / Kernel.CapacitancePf;
        derivative[1] = (rates.PotassiumLow.Equilibrium - state[1]) / rates.PotassiumLow.TauMs;
        derivative[2] = (rates.Calcium.Equilibrium - state[2]) / rates.Calcium.TauMs;
        derivative[3] = (rates.Hcn1.Equilibrium - state[3]) / rates.Hcn1.TauMs;
        derivative[4] = currents.MetPa; derivative[5] = currents.PotassiumLowPa; derivative[6] = currents.CalciumPa;
        derivative[7] = currents.HcnPotassiumPa; derivative[8] = currents.HcnSodiumPa;
        derivative[9] = currents.PumpPotassiumPa; derivative[10] = currents.PumpSodiumPa;
    }

    private static void ValidateState(ReadOnlySpan<double> state)
    {
        if (state.Length != StateCount) throw new ArgumentException("Complete hair-cell state required.");
        foreach (double value in state)
            if (!double.IsFinite(value)) throw new ArithmeticException("Nonfinite hair-cell state; preserve the failure.");
        for (int i = 1; i <= 3; i++) AvatarHairCellKernel.Probability(state[i]);
    }
    private double TimeAt(long step) => _epochMs + step * _stepMs;
    private void RequireOwner()
    {
        if (Environment.CurrentManagedThreadId != _owner)
            throw new InvalidOperationException("One owner advances the receptor cell and its clock.");
    }
}
