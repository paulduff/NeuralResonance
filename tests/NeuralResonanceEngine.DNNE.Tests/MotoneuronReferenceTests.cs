namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class MotoneuronReferenceTests
{
    [Fact]
    public void CompensatedPassiveChargeRetainsSubUlpIncrements()
    {
        // Analytic diagnostic: no membrane currents, so sum(C*dV) = I*T.
        // Current is deliberately below one voltage ULP per step; not physiology.
        var original = MotoneuronReferenceParameters.CatLowThreshold;
        var parameters = original with { Soma = original.Soma with { LeakSiemensPerCm2 = 0 },
            Dendrite = original.Dendrite with { LeakSiemensPerCm2 = 0 } };
        var naive = new MotoneuronReferenceCell(parameters, -71, MotoneuronDiagnosticBlock.Passive);
        var compensated = new MotoneuronReferenceCell(parameters, -71, MotoneuronDiagnosticBlock.Passive,
            MotoneuronAccumulation.Compensated);
        const double current = 1e-10, step = .003125;
        const int count = 32000;
        for (int i = 0; i < count; ++i) { naive.Advance(step, current); compensated.Advance(step, current); }
        var cap = compensated.CopyCapacitancesPf(); var state = compensated.CopyState();
        double expectedCharge = current * (step * count);
        // Two voltage ULPs across all capacitances, fixed before the experiment.
        double bound = 2 * (Math.BitIncrement(-71d) + 71) * cap.Sum();
        double charge = Enumerable.Range(0, 5).Sum(i => cap[i] * (state[i] + 71));
        Assert.All(naive.CopyState().Take(5), v => Assert.Equal(-71, v));
        Assert.True(expectedCharge > 10 * bound);
        Assert.InRange(Math.Abs(charge - expectedCharge), 0, bound);
        Assert.InRange(Math.Abs(state[29] - expectedCharge), 0, 1e-20);
        Assert.InRange(compensated.MaximumChargeLedgerErrorFc, 0, bound);
        Assert.True(naive.MaximumChargeLedgerErrorFc > 10 * bound);
        Assert.Empty(compensated.CopyCrossings());
    }

    [Fact]
    public void AccumulationIsExplicitAndRejectedInputsPreserveCompensatedContinuation()
    {
        var parameters = MotoneuronReferenceParameters.CatLowThreshold;
        Assert.Throws<ArgumentException>(() => new MotoneuronReferenceCell(parameters, accumulation: (MotoneuronAccumulation)99));
        var naive = new MotoneuronReferenceCell(parameters);
        var first = new MotoneuronReferenceCell(parameters, -71, MotoneuronDiagnosticBlock.Passive, MotoneuronAccumulation.Compensated);
        var control = new MotoneuronReferenceCell(parameters, -71, MotoneuronDiagnosticBlock.Passive, MotoneuronAccumulation.Compensated);
        Assert.Equal(MotoneuronReferenceCell.Version, naive.IntegrationVersion);
        Assert.Equal(MotoneuronReferenceCell.CompensatedVersion, first.IntegrationVersion);
        for (int i = 0; i < 100; ++i) { first.Advance(.003125, 100); control.Advance(.003125, 100); }
        var accepted = first.CopyState();
        Assert.Throws<ArgumentException>(() => first.Advance(.025));
        Assert.Equal(accepted, first.CopyState());
        for (int i = 0; i < 100; ++i) { first.Advance(.003125, 100); control.Advance(.003125, 100); }
        Assert.Equal(control.CopyState(), first.CopyState());
        Assert.Equal(control.TimeMs, first.TimeMs);
    }

    [Theory]
    [InlineData(24)]
    [InlineData(37)]
    public void DeclaredClampDomainRetainsPhysicalGateProbabilities(double temperature)
    {
        var parameters = new MotoneuronChannelParameters(TemperatureCelsius: temperature);
        var rates = new MotoneuronGateRate[9];
        foreach (double voltage in new[] { -100d, -80, -65, -44, -29, -20, 0, 40, 80 })
        foreach (double calcium in new[] { .0001, .0002, .001 })
        {
            MotoneuronChannelKinetics.Soma(voltage, calcium, parameters, rates);
            foreach (var rate in rates)
            {
                Assert.InRange(rate.Equilibrium, 0, 1); Assert.True(double.IsFinite(rate.TauMs) && rate.TauMs > 0);
                Assert.InRange(MotoneuronChannelKinetics.Relax(.5, rate, 100), 0, 1);
            }
            MotoneuronChannelKinetics.Dendrite(voltage, parameters, rates);
            Assert.All(rates.Take(3), rate => Assert.InRange(rate.Equilibrium, 0, 1));
        }
    }

    [Fact]
    public void SodiumSingularitiesHaveContinuousDeclaredLimits()
    {
        foreach (var (threshold, scale, slope) in new[] { (-29d, .4, 7.2), (-44d, .03, 1.5) })
        {
            Assert.Equal(scale * slope, MotoneuronChannelKinetics.SodiumRate(threshold, threshold, scale, slope));
            double below = MotoneuronChannelKinetics.SodiumRate(threshold - 1e-8, threshold, scale, slope);
            double above = MotoneuronChannelKinetics.SodiumRate(threshold + 1e-8, threshold, scale, slope);
            Assert.InRange(Math.Abs(above - below), 0, 1e-8);
        }
    }

    [Fact]
    public void CalciumAffectsAfterhyperpolarizationGateWithoutChangingVoltageGates()
    {
        var low = new MotoneuronGateRate[9]; var high = new MotoneuronGateRate[9];
        MotoneuronChannelKinetics.Soma(-60, .0001, new(), low);
        MotoneuronChannelKinetics.Soma(-60, .001, new(), high);
        Assert.Equal(0, low[7].Equilibrium); Assert.True(high[7].Equilibrium > .4);
        for (int i = 0; i < 9; ++i) if (i != 7) Assert.Equal(low[i], high[i]);
    }

    [Fact]
    public void PassiveJunctionReductionPreservesBranchSymmetryAndIsolation()
    {
        var cell = new MotoneuronReferenceCell(MotoneuronReferenceParameters.CatLowThreshold);
        var coupling = cell.CopyCouplingNs();
        for (int i = 0; i < 5; ++i)
        for (int j = 0; j < 5; ++j)
        {
            Assert.Equal(coupling[i][j], coupling[j][i]); Assert.True(coupling[i][j] >= 0);
            if (i == j) Assert.Equal(0, coupling[i][j]);
        }
        Assert.True(coupling[1][2] > 0); Assert.Equal(coupling[1][2], coupling[3][4]);
        Assert.Equal(0, coupling[1][3]); Assert.Equal(coupling[0][1], coupling[0][4]);
        Assert.All(cell.CopyCapacitancesPf(), value => Assert.True(value > 0));
        Assert.Empty(cell.CopyCrossings());
    }

    [Fact]
    public void PassiveCellAtLeakEquilibriumHasNoInventedVoltageChangeOrCalcium()
    {
        var cell = new MotoneuronReferenceCell(MotoneuronReferenceParameters.CatLowThreshold, -71, MotoneuronDiagnosticBlock.Passive);
        for (int i = 0; i < 1000; ++i) cell.Advance(.0125);
        Assert.All(cell.CopyState().Take(5), value => Assert.Equal(-71, value));
        Assert.Equal(.0001, cell.CalciumMm); Assert.Empty(cell.CopyCrossings());
        Assert.Equal(0, cell.MaximumChargeLedgerErrorFc); Assert.Equal(0, cell.MaximumCalciumLedgerErrorMm);
    }

    [Fact]
    public void PassiveCurrentResponseObeysSuperpositionAndChargeAccounting()
    {
        var first = new MotoneuronReferenceCell(MotoneuronReferenceParameters.CatLowThreshold, -71, MotoneuronDiagnosticBlock.Passive);
        var twice = new MotoneuronReferenceCell(MotoneuronReferenceParameters.CatLowThreshold, -71, MotoneuronDiagnosticBlock.Passive);
        for (int i = 0; i < 8000; ++i) { first.Advance(.0125, 100); twice.Advance(.0125, 200); }
        var a = first.CopyState(); var b = twice.CopyState();
        Assert.True(a[0] > -71 && a[1] > -71);
        for (int i = 0; i < 5; ++i) Assert.InRange(Math.Abs((b[i] + 71) - 2 * (a[i] + 71)), 0, 1e-9);
        Assert.InRange(first.MaximumChargeLedgerErrorFc, 0, 1e-5);
        Assert.InRange(twice.MaximumChargeLedgerErrorFc, 0, 1e-5);
    }

    [Fact]
    public void DiagnosticCopiesCannotMutateTheCell()
    {
        var cell = new MotoneuronReferenceCell(MotoneuronReferenceParameters.CatLowThreshold);
        cell.CopyState()[0] = 100; cell.CopyAreasUm2()[0] = 0; cell.CopyCouplingNs()[0][1] = 0;
        Assert.Equal(-65, cell.SomaVoltageMv); Assert.True(cell.CopyAreasUm2()[0] > 0);
        Assert.True(cell.CopyCouplingNs()[0][1] > 0);
    }

    [Fact]
    public void InvalidInputsFailWithoutChangingTheAcceptedState()
    {
        var parameters = MotoneuronReferenceParameters.CatLowThreshold;
        var cell = new MotoneuronReferenceCell(parameters); var initial = cell.CopyState();
        Assert.Throws<ArgumentException>(() => cell.Advance(.025));
        Assert.Throws<ArgumentException>(() => cell.Advance(.01, dendriteExcitatoryNs: -1));
        Assert.Throws<ArgumentException>(() => cell.Advance(.01, double.NaN));
        Assert.Equal(initial, cell.CopyState()); Assert.Equal(0, cell.TimeMs);
        Assert.Throws<ArgumentException>(() => new MotoneuronReferenceCell(parameters with { Soma = parameters.Soma with { AxialResistivityOhmCm = 0 } }));
    }
}
