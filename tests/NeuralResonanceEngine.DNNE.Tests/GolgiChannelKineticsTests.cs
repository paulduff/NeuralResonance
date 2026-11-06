namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class GolgiChannelKineticsTests
{
    [Fact]
    public void AllGatesStayProbabilisticAcrossClampAndTemperatureDomain()
    {
        var rates = new GolgiGateRate[GolgiChannelKinetics.GateCount];
        foreach (double temperature in new[] { 15.0, 23.0, 37.0, 40.0 })
        foreach (double calcium in new[] { 0.0, .00005, .001, .1 })
        for (int i = 0; i <= 2200; ++i)
        {
            GolgiChannelKinetics.FillRates(-120 + i * .1, calcium, temperature, rates);
            foreach (var rate in rates)
            {
                Assert.InRange(rate.Equilibrium, 0, 1);
                Assert.True(double.IsFinite(rate.TauMs) && rate.TauMs > 0);
            }
        }
    }

    [Theory]
    [InlineData(-25)] // Na transient removable singularity (source fallback sign error).
    [InlineData(-26)] // Kv
    [InlineData(-40)] // Na persistent
    [InlineData(4.48754)] // Na resurgent slow activation
    [InlineData(-43.97494)] // Na resurgent slow deactivation
    public void RemovableRateSingularitiesAreContinuous(double voltage)
    {
        var center = new GolgiGateRate[18]; var left = new GolgiGateRate[18]; var right = new GolgiGateRate[18];
        GolgiChannelKinetics.FillRates(voltage, .00005, 23, center);
        GolgiChannelKinetics.FillRates(voltage - 1e-8, .00005, 23, left);
        GolgiChannelKinetics.FillRates(voltage + 1e-8, .00005, 23, right);
        for (int i = 0; i < 18; ++i)
        {
            Assert.InRange(Math.Abs(center[i].Equilibrium - (left[i].Equilibrium + right[i].Equilibrium) / 2), 0, 1e-10);
            Assert.InRange(Math.Abs(center[i].TauMs - (left[i].TauMs + right[i].TauMs) / 2), 0, center[i].TauMs * 1e-10);
        }
    }

    [Fact]
    public void HcnFractionsRemainNonnegativeAtRoundedBranchBoundaries()
    {
        foreach (double voltage in new[] { -108.700001, -108.7, -64.71, -64.700001, -64.7, 11.47, 30.0, 100.0 })
        {
            var cell = new GolgiChannelKinetics(voltage);
            foreach (var gate in cell.CopyGates()) Assert.InRange(gate, 0, 1);
        }
    }

    [Theory]
    [InlineData(-100, 0)]
    [InlineData(-60, .001)]
    [InlineData(30, .01)]
    public void ClampEvolutionDoesNotDependOnHostPartition(double voltage, double calcium)
    {
        var whole = new GolgiChannelKinetics(); var partitioned = new GolgiChannelKinetics();
        whole.AdvanceVoltageClamp(voltage, calcium, 25);
        for (int i = 0; i < 200; ++i) partitioned.AdvanceVoltageClamp(voltage, calcium, .125);
        AssertClose(whole.CopyGates(), partitioned.CopyGates(), 1e-12);
        AssertClose(whole.CopySkOccupancy(), partitioned.CopySkOccupancy(), 1e-12);
        Assert.InRange(Math.Abs(partitioned.CopySkOccupancy().Sum() - 1), 0, 1e-12);
    }

    [Fact]
    public void CalciumRecruitsSkAndBkAndRemovalClosesThem()
    {
        var cell = new GolgiChannelKinetics(-60, 0);
        Assert.Equal(0, cell.SkOpenProbability);
        Assert.Equal(0, cell.Gate(GolgiGate.Bk));
        cell.AdvanceVoltageClamp(-60, .01, 200);
        Assert.True(cell.SkOpenProbability > .5);
        Assert.True(cell.Gate(GolgiGate.Bk) > 0);
        foreach (var p in cell.CopySkOccupancy()) Assert.InRange(p, 0, 1);
        Assert.InRange(Math.Abs(cell.CopySkOccupancy().Sum() - 1), 0, 1e-12);
        cell.AdvanceVoltageClamp(-60, 0, 2_000);
        Assert.InRange(cell.SkOpenProbability, 0, 1e-10);
        Assert.InRange(cell.Gate(GolgiGate.Bk), 0, 1e-10);
    }

    [Fact]
    public void DepositedSkEquilibriumIsStationaryWithoutRenormalization()
    {
        foreach (double calcium in new[] { 0.0, .00005, .001, .01 })
        {
            var cell = new GolgiChannelKinetics(-60, calcium);
            var before = cell.CopySkOccupancy();
            cell.AdvanceVoltageClamp(-60, calcium, 500);
            AssertClose(before, cell.CopySkOccupancy(), 3e-12);
        }
    }

    [Fact]
    public void CurrentsRespectPhysicalDirectionsAndReversals()
    {
        var cell = new GolgiChannelKinetics(-40, .001);
        var rest = cell.Currents(-60, .001, .001);
        Assert.True(rest.SodiumTransient < 0 && rest.SodiumPersistent < 0 && rest.SodiumResurgent < 0);
        Assert.True(rest.CalciumHigh < 0 && rest.CalciumLow < 0 && rest.Hcn1 < 0 && rest.Hcn2 < 0);
        Assert.True(rest.PotassiumDelayed > 0 && rest.PotassiumSlow > 0 && rest.PotassiumA > 0 && rest.Bk > 0 && rest.Sk > 0);
        var sodium = cell.Currents(GolgiChannelKinetics.SodiumReversalMv, .001, .001);
        Assert.Equal(0, sodium.SodiumTransient); Assert.Equal(0, sodium.SodiumPersistent); Assert.Equal(0, sodium.SodiumResurgent);
        var potassium = cell.Currents(GolgiChannelKinetics.PotassiumReversalMv, .001, .001);
        Assert.Equal(0, potassium.PotassiumDelayed); Assert.Equal(0, potassium.PotassiumSlow); Assert.Equal(0, potassium.PotassiumA);
        Assert.Equal(0, potassium.Bk); Assert.Equal(0, potassium.Sk);
        var hcn = cell.Currents(GolgiChannelKinetics.HcnReversalMv, .001, .001);
        Assert.Equal(0, hcn.Hcn1); Assert.Equal(0, hcn.Hcn2);
        Assert.Equal(0, cell.Currents(GolgiChannelKinetics.LeakReversalMv, .001, .001).Leak);
        double eCa = GolgiChannelKinetics.CalciumReversal(.001, 23);
        Assert.True(eCa > GolgiChannelKinetics.CalciumReversal(.01, 23));
        Assert.Equal(0, cell.Currents(eCa, .001, .001).CalciumHigh);
        Assert.Equal(0, cell.Currents(eCa, .001, .001).CalciumLow);
    }

    [Fact]
    public void CalciumShellHasCorrectInfluxAndRemovalSigns()
    {
        double rest = GolgiChannelKinetics.RestingCalciumMm;
        Assert.Equal(0, GolgiChannelKinetics.CalciumDerivative(rest, 0));
        Assert.True(GolgiChannelKinetics.CalciumDerivative(rest, -.001) > 0);
        Assert.True(GolgiChannelKinetics.CalciumDerivative(.001, 0) < 0);
        Assert.True(GolgiChannelKinetics.CalciumDerivative(rest, .001) < 0);
    }

    [Fact]
    public void TemperatureScalesSupportedKineticsWithoutInventingHcnCorrection()
    {
        var cold = new GolgiGateRate[18]; var warm = new GolgiGateRate[18];
        GolgiChannelKinetics.FillRates(-60, .00005, 23, cold);
        GolgiChannelKinetics.FillRates(-60, .00005, 33, warm);
        Assert.InRange(Math.Abs(cold[(int)GolgiGate.NaM].TauMs / warm[(int)GolgiGate.NaM].TauMs - 3), 0, 1e-12);
        Assert.InRange(Math.Abs(cold[(int)GolgiGate.LvaM].TauMs / warm[(int)GolgiGate.LvaM].TauMs - 5), 0, 1e-12);
        Assert.Equal(cold[(int)GolgiGate.Hcn1Fast], warm[(int)GolgiGate.Hcn1Fast]);
        Assert.Equal(cold[(int)GolgiGate.Hcn2Slow], warm[(int)GolgiGate.Hcn2Slow]);
    }

    [Fact]
    public void InvalidInputsCannotResetOrCorruptState()
    {
        var cell = new GolgiChannelKinetics();
        var before = cell.CopyGates(); var sk = cell.CopySkOccupancy();
        Assert.Throws<ArgumentOutOfRangeException>(() => cell.AdvanceVoltageClamp(double.NaN, .00005, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => cell.AdvanceVoltageClamp(-60, -.001, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => cell.AdvanceVoltageClamp(-60, .00005, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => GolgiChannelKinetics.CalciumReversal(0, 23));
        Assert.Equal(before, cell.CopyGates()); Assert.Equal(sk, cell.CopySkOccupancy());
    }

    private static void AssertClose(double[] expected, double[] actual, double tolerance)
    {
        Assert.Equal(expected.Length, actual.Length);
        for (int i = 0; i < expected.Length; ++i) Assert.InRange(Math.Abs(expected[i] - actual[i]), 0, tolerance);
    }
}
