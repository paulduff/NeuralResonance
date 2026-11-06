namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class GolgiReferenceCellTests
{
    [Fact]
    public void MorphologyRetainsSharedEndpointsAndReferenceMembraneAreas()
    {
        var cable = new GolgiCable(); var nodes = cable.CopyNodes();
        Assert.Equal(135, cable.Count);
        Assert.Equal(133, nodes.Count(n => n.CapacitancePf > 0));
        Assert.Equal(30, nodes.Count(n => n.Name.StartsWith("dendrite-")));
        Assert.Equal(100, nodes.Count(n => n.Name.StartsWith("axon-")));
        Assert.All(nodes.Where(n => n.Name is "dendrite-0-0" or "dendrite-1-0" or "dendrite-2-0" or "seal"), n => Assert.Equal(1, n.Parent));
        Assert.Equal(2, nodes.Single(n => n.Name == "axon-0").Parent);
        Assert.Equal(Math.PI * 27 * 27 * .01, nodes[0].CapacitancePf);
        Assert.InRange(nodes.Where(n => n.Name.StartsWith("axon-")).Sum(n => n.CapacitancePf), 90.4, 90.6);
        Assert.InRange(cable.TotalCapacitancePf, 145, 146);
        Assert.All(nodes.Skip(1).Select((n, i) => (n, i)), x => Assert.InRange(x.n.Parent, 0, x.i));
        Assert.Equal(0, nodes[cable.ElectrodeIndex].LeakConductanceNs);
    }

    [Fact]
    public void PassiveCableConservesInjectedChargeAcrossTheWholeTree()
    {
        var cable = new GolgiCable(); var nodes = cable.CopyNodes();
        double[] old = Enumerable.Repeat(-60d, cable.Count).ToArray(), next = new double[cable.Count];
        double[] zero = new double[cable.Count], injection = new double[cable.Count];
        injection[cable.ElectrodeIndex] = 10;
        cable.Solve(old, .015625, zero, zero, injection, next);
        double charge = nodes.Select((n, i) => n.CapacitancePf * (next[i] - old[i])).Sum();
        Assert.InRange(Math.Abs(charge - 10 * .015625), 0, 1e-7);
        Assert.True(next[cable.ElectrodeIndex] > next[0]);
        Assert.True(next[0] > -60);
    }

    [Fact]
    public void PassiveEquilibriumIsMaintainedWithoutVoltageClamps()
    {
        var cable = new GolgiCable(); var nodes = cable.CopyNodes();
        double[] old = Enumerable.Repeat(-55d, cable.Count).ToArray(), next = new double[cable.Count];
        var conductance = nodes.Select(n => n.LeakConductanceNs).ToArray();
        var source = conductance.Select(g => -55 * g).ToArray();
        cable.Solve(old, .015625, conductance, source, new double[cable.Count], next);
        Assert.All(next, v => Assert.InRange(Math.Abs(v + 55), 0, 1e-8));
    }

    [Theory]
    [InlineData(-80)]
    [InlineData(-40)]
    [InlineData(30)]
    public void ImplicitConductancesMatchTheQualifiedCurrentEquations(double voltage)
    {
        var channels = new GolgiChannelKinetics(voltage, .001);
        var conductance = channels.Conductances();
        double high = GolgiChannelKinetics.CalciumReversal(.001, 23), low = GolgiChannelKinetics.CalciumReversal(.0001, 23);
        Assert.Equal(channels.Currents(voltage, .001, .0001), conductance.Currents(voltage, high, low));
        double current = conductance.Currents(voltage, high, low).Total;
        Assert.InRange(Math.Abs(current - (conductance.Total * voltage - conductance.ReversalWeighted(high, low))), 0, 1e-12);
    }

    [Fact]
    public void CoupledCalciumAndChargeRetainTheirAccounting()
    {
        var cell = new GolgiReferenceCell();
        for (int i = 0; i < 1024; ++i) cell.Advance(.00390625, somaCurrentPa: 50);
        Assert.InRange(cell.HighCalciumMm, 0, .1); Assert.InRange(cell.LowCalciumMm, 0, .1);
        Assert.True(cell.HighCalciumEnteredMm > 0 && cell.LowCalciumEnteredMm > 0);
        Assert.InRange(cell.MaximumCalciumBalanceErrorMm, 0, 1e-11);
        Assert.InRange(cell.MaximumChargeBalanceErrorFc, 0, 1e-4);
        Assert.InRange(cell.MaximumKirchhoffResidualPa, 0, 1e-5);
        Assert.InRange(Math.Abs(cell.CopySkOccupancy().Sum() - 1), 0, 1e-10);
    }

    [Fact]
    public void InvalidInputCannotAdvanceTheCellOrInventASpike()
    {
        var cell = new GolgiReferenceCell(); var initial = cell.CopyVoltages();
        Assert.Throws<ArgumentOutOfRangeException>(() => cell.Advance(.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => cell.Advance(.01, double.NaN));
        Assert.Equal(0, cell.TimeMs); Assert.Equal(initial, cell.CopyVoltages());
        Assert.Empty(cell.CopySomaticCrossings());
    }
}
