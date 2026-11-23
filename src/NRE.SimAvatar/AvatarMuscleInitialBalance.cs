using NeuralResonanceEngine.Protocol;

namespace NRE.SimAvatar;

internal sealed record MuscleInitialBalanceLimits(double FiberLengthToleranceMeters,
    double ForceAbsoluteToleranceNewtons, double ForceRelativeTolerance, int MaximumIterations)
{
    public static MuscleInitialBalanceLimits Diagnostic { get; } = new(1e-13, 1e-9, 1e-12, 128);
    public void Validate()
    {
        if (!double.IsFinite(FiberLengthToleranceMeters) || FiberLengthToleranceMeters <= 0 ||
            !double.IsFinite(ForceAbsoluteToleranceNewtons) || ForceAbsoluteToleranceNewtons <= 0 ||
            !double.IsFinite(ForceRelativeTolerance) || ForceRelativeTolerance < 0 ||
            MaximumIterations < 1 || MaximumIterations > 512)
            throw new ArgumentException("Declare finite positive length/force accuracy and a bounded iteration count.");
    }
}

internal sealed record MuscleInitialBalanceResult(string Version, string MuscleId, bool Bracketed, bool Balanced,
    string StopReason, double MeasuredPathLengthMeters, double RequestedLowerFiberMeters,
    double RequestedUpperFiberMeters, double FinalLowerFiberMeters, double FinalUpperFiberMeters,
    double LowerForceImbalanceNewtons, double UpperForceImbalanceNewtons, double ForceImbalanceNewtons,
    double ForceAllowanceNewtons, int Evaluations, MuscleInitialBalanceLimits Limits,
    DynamicMuscleSnapshot Candidate, CausalMuscleDefinition? Definition);

// Initial condition selection only. No evolved cell/body is accepted, reset,
// equilibrated after a step, or supplied with invented activation. Each force
// evaluation uses a fresh instance of the ORIGINAL continuous muscle equation.
// Balance at zero fiber velocity is not a stationary moving-body solution.
internal static class AvatarMuscleInitialBalance
{
    public const string Version = "fresh-passive-fiber-force-balance-v1";

    public static MuscleInitialBalanceResult Solve(string muscleId, DynamicMuscleGeometry geometry,
        IEnumerable<SlowMotorUnitDefinition> units, MotorAxonClock clock, double measuredPathLengthMeters,
        double lowerFiberMeters, double upperFiberMeters, MuscleInitialBalanceLimits limits)
    {
        ArgumentNullException.ThrowIfNull(geometry); geometry.Validate();
        ArgumentNullException.ThrowIfNull(units); ArgumentNullException.ThrowIfNull(clock); clock.Validate();
        ArgumentNullException.ThrowIfNull(limits); limits.Validate();
        if (string.IsNullOrWhiteSpace(muscleId) || !double.IsFinite(measuredPathLengthMeters) || measuredPathLengthMeters <= 0 ||
            !double.IsFinite(lowerFiberMeters) || !double.IsFinite(upperFiberMeters) || lowerFiberMeters <= 0 || upperFiberMeters <= lowerFiberMeters)
            throw new ArgumentException("Require a named actual path and an explicit ordered positive fiber bracket.");
        // Snapshot the caller's unit collection; the immutable contact and
        // parameter records retain their original identities and force limits.
        var declared = Array.AsReadOnly(units.ToArray());
        int evaluations = 0;
        DynamicMuscleSnapshot Evaluate(double fiber)
        {
            var cell = new AvatarDynamicMuscle(geometry, declared, clock, fiber, measuredPathLengthMeters);
            var state = cell.Capture(); evaluations++;
            if (state.TimeMs != clock.BodyEpochMs || state.FiberVelocityMetersPerSecond != 0 || state.Yield != 1 ||
                state.Boundary != FiberBoundary.Free || state.Forces.ActiveFiberForceNewtons != 0 || state.Forces.GeometricForceNewtons != 0 ||
                state.Units.Any(u => u.Activation != 0 || u.FreeCalcium != 0 || u.BoundCalcium != 0 || u.Arrivals != 0))
                throw new InvalidOperationException("Fresh passive initialization assumptions changed; do not repair an evolved state.");
            if (state.Forces.PassiveFiberForceNewtons <= 0 || state.Forces.TendonAxialForceNewtons <= 0)
                throw new ArithmeticException("The force evaluation lost positive passive/tendon resolution; a zero from underflow is not equilibrium.");
            return state;
        }
        static double Residual(DynamicMuscleSnapshot s) => s.Forces.ProjectedTendonForceNewtons-s.Forces.ProjectedFiberForceNewtons;
        double allowance = limits.ForceAbsoluteToleranceNewtons+limits.ForceRelativeTolerance*geometry.ReferenceForceNewtons;
        if (!double.IsFinite(allowance)) throw new ArgumentException("Force allowance is not representable.");
        double lo = lowerFiberMeters, hi = upperFiberMeters;
        var left = Evaluate(lo); var right = Evaluate(hi);
        double flo = Residual(left), fhi = Residual(right);
        if (flo < fhi) throw new ArithmeticException("Passive constitutive balance is not numerically monotone on the declared bracket.");
        bool bracketed = flo >= 0 && fhi <= 0;
        DynamicMuscleSnapshot Closest() => Math.Abs(flo) <= Math.Abs(fhi) ? left : right;
        MuscleInitialBalanceResult Finish(bool balanced, string reason, DynamicMuscleSnapshot candidate) => new(
            Version, muscleId, bracketed, balanced, reason, measuredPathLengthMeters, lowerFiberMeters, upperFiberMeters,
            lo, hi, flo, fhi, Residual(candidate), allowance, evaluations, limits, candidate,
            balanced ? new(muscleId, geometry, declared, candidate.FiberLengthMeters, 0) : null);
        if (!bracketed) return Finish(false, "NoRootInDeclaredBracket", Closest());
        if (flo == 0) { hi = lo; fhi = flo; return Finish(true, "FloatingPointForceBalance", left); }
        if (fhi == 0) { lo = hi; flo = fhi; return Finish(true, "FloatingPointForceBalance", right); }
        for (int iteration = 0; iteration < limits.MaximumIterations; ++iteration)
        {
            double mid = lo+(hi-lo)/2;
            if (mid == lo || mid == hi)
                return Finish(hi-lo <= limits.FiberLengthToleranceMeters && Math.Abs(Residual(Closest())) <= allowance,
                    "RepresentableBracketLimit", Closest());
            var candidate = Evaluate(mid); double value = Residual(candidate);
            if (value > flo || value < fhi) throw new ArithmeticException("A force evaluation left its monotone bracket.");
            if (value == 0) { lo = hi = mid; flo = fhi = 0; return Finish(true, "FloatingPointForceBalance", candidate); }
            if (value > 0) { lo = mid; flo = value; left = candidate; } else { hi = mid; fhi = value; right = candidate; }
            // Both state position and force must converge. A tiny force alone
            // cannot admit an arbitrary fiber position on a shallow curve.
            if (hi-lo <= limits.FiberLengthToleranceMeters && Math.Abs(value) <= allowance)
                return Finish(true, "LengthAndForceConverged", candidate);
        }
        return Finish(false, "IterationLimit", Closest());
    }
}
