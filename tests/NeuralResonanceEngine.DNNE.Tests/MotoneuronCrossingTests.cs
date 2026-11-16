namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class MotoneuronCrossingTests
{
    [Theory]
    [InlineData(.2)]
    [InlineData(.5)]
    [InlineData(.8)]
    public void Known_Monotone_Cubic_Has_Analytic_Crossing(double root)
    {
        double V(double s) => -20 + (s - root) + .2 * Math.Pow(s - root, 3);
        double D(double s) => 1 + .6 * Math.Pow(s - root, 2);
        var estimate = MotoneuronCrossing.Cubic(4, 6, V(0), V(1), D(0) / 2, D(1) / 2);
        Assert.InRange(Math.Abs(estimate.TimeMs - (4 + 2 * root)), 0, 1e-12);
        Assert.InRange(Math.Abs(estimate.ResidualMv), 0, 1e-12);
        Assert.True(estimate.MinimumScaledSlope > 0);
    }

    [Fact]
    public void Linear_Limit_And_Exact_Right_Endpoint_Are_Retained()
    {
        Assert.Equal(4.5, MotoneuronCrossing.Cubic(4, 6, -21, -17, 2, 2).TimeMs);
        Assert.Equal(6, MotoneuronCrossing.Cubic(4, 6, -24, -20, 2, 2).TimeMs);
    }

    [Fact]
    public void Multiple_Crossings_And_Nonmonotone_Brackets_Are_Not_Silently_Linearized()
    {
        // (s-.2)(s-.5)(s-.8): endpoints bracket three roots.
        Assert.Throws<InvalidOperationException>(() => MotoneuronCrossing.Cubic(0, 1, -20.08, -19.92, .66, .66));
        Assert.Throws<InvalidOperationException>(() => MotoneuronCrossing.Cubic(0, 1, -21, -19, -1, 2));
    }

    [Fact]
    public void Invalid_Brackets_And_Unknown_Mode_Are_Rejected()
    {
        Assert.Throws<ArgumentException>(() => MotoneuronCrossing.Cubic(0, 0, -21, -19, 1, 1));
        Assert.Throws<ArgumentException>(() => MotoneuronCrossing.Cubic(0, 1, -20, -19, 1, 1));
        Assert.Throws<ArgumentException>(() => MotoneuronCrossing.Cubic(0, 1, -21, -20.1, 1, 1));
        Assert.Throws<ArgumentException>(() => MotoneuronCrossing.Cubic(0, 1, -21, -19, double.NaN, 1));
        Assert.Throws<ArgumentException>(() => new MotoneuronReferenceCell(MotoneuronReferenceParameters.CatLowThreshold,
            crossingInterpolation: (MotoneuronCrossingInterpolation)99));
    }

    [Theory]
    [InlineData(.0125)]
    [InlineData(.003125)]
    public void Active_Membrane_Trajectory_Is_Bitwise_Preserved_At_Every_Accepted_Step(double step)
    {
        var linear = new MotoneuronReferenceCell(MotoneuronReferenceParameters.CatLowThreshold,
            accumulation: MotoneuronAccumulation.Compensated);
        var cubic = new MotoneuronReferenceCell(MotoneuronReferenceParameters.CatLowThreshold,
            accumulation: MotoneuronAccumulation.Compensated, crossingInterpolation: MotoneuronCrossingInterpolation.CubicHermite);
        int observed = 0;
        for (int i = 0; i < (int)(256 / step); ++i)
        {
            double start = cubic.TimeMs, voltage = cubic.SomaVoltageMv;
            // Explicit injected-current numerical control; not a live brain command.
            linear.Advance(step, 6000); cubic.Advance(step, 6000);
            Assert.Equal(linear.CopyState(), cubic.CopyState());
            Assert.Equal(linear.TimeMs, cubic.TimeMs);
            Assert.Equal(linear.MaximumChargeLedgerErrorFc, cubic.MaximumChargeLedgerErrorFc);
            Assert.Equal(linear.MaximumCalciumLedgerErrorMm, cubic.MaximumCalciumLedgerErrorMm);
            var crossings = cubic.CopyCrossings();
            Assert.Equal(linear.CopyCrossings().Length, crossings.Length);
            if (crossings.Length != observed)
            {
                Assert.Equal(observed + 1, crossings.Length);
                Assert.True(voltage < -20 && cubic.SomaVoltageMv >= -20);
                Assert.InRange(crossings[^1], start, cubic.TimeMs);
                ++observed;
            }
        }
        Assert.True(observed > 0, "The active trajectory must actually cross threshold.");
        Assert.Contains(linear.CopyCrossings().Zip(cubic.CopyCrossings()), p => p.First != p.Second);
    }
}
