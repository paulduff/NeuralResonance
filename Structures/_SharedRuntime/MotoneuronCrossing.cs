internal enum MotoneuronCrossingInterpolation { Linear, CubicHermite }

internal readonly record struct MotoneuronCrossingEstimate(double TimeMs, double Fraction,
    double ResidualMv, double MinimumScaledSlope);

// Observational event localization only. Never resets voltage, advances a cell,
// predicts future endpoints, or substitutes a firing rate for a neuronal event.
internal static class MotoneuronCrossing
{
    internal const string CubicVersionSuffix = "+cubic-hermite-crossings-v1";
    internal const double ResidualBoundMv = 1e-8;

    internal static MotoneuronCrossingEstimate Cubic(double startMs, double endMs,
        double startMv, double endMv, double startSlopeMvPerMs, double endSlopeMvPerMs,
        double thresholdMv = MotoneuronReferenceCell.SpikeThresholdMv)
    {
        if (!double.IsFinite(startMs) || !double.IsFinite(endMs) || startMs < 0 || endMs <= startMs ||
            !double.IsFinite(startMv) || !double.IsFinite(endMv) || !double.IsFinite(thresholdMv) ||
            !double.IsFinite(startSlopeMvPerMs) || !double.IsFinite(endSlopeMvPerMs) ||
            startMv >= thresholdMv || endMv < thresholdMv)
            throw new ArgumentException("Require two completed finite endpoints bracketing an upward crossing.");
        double h = endMs - startMs;
        double a = 2 * startMv - 2 * endMv + h * (startSlopeMvPerMs + endSlopeMvPerMs);
        double b = -3 * startMv + 3 * endMv - h * (2 * startSlopeMvPerMs + endSlopeMvPerMs);
        double c = h * startSlopeMvPerMs, d = startMv - thresholdMv;
        if (!double.IsFinite(a) || !double.IsFinite(b) || !double.IsFinite(c) || !double.IsFinite(d))
            throw new InvalidOperationException("Non-finite crossing polynomial; no event may be selected.");
        double Slope(double s) => 3 * a * s * s + 2 * b * s + c;
        double minimum = Math.Min(Slope(0), Slope(1));
        if (a != 0)
        {
            double vertex = -b / (3 * a);
            if (vertex > 0 && vertex < 1) minimum = Math.Min(minimum, Slope(vertex));
        }
        if (!double.IsFinite(minimum) || minimum <= 0)
            throw new InvalidOperationException("Crossing polynomial is not strictly increasing; refine the trajectory before selecting an event.");
        double Value(double s) => ((a * s + b) * s + c) * s + d;
        double lower = 0, upper = 1;
        for (int iteration = 0; iteration < 64; ++iteration)
        {
            double middle = (lower + upper) * .5;
            if (middle == lower || middle == upper) break;
            if (Value(middle) < 0) lower = middle;
            else upper = middle;
        }
        double fraction = (lower + upper) * .5, residual = Value(fraction);
        double at = startMs + h * fraction;
        if (!double.IsFinite(residual) || Math.Abs(residual) > ResidualBoundMv ||
            !double.IsFinite(at) || at < startMs || at > endMs)
            throw new InvalidOperationException("Crossing localization exceeded its residual or completed-time bracket.");
        return new(at, fraction, residual, minimum);
    }
}
