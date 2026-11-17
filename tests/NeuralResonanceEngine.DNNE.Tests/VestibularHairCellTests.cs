using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class VestibularHairCellTests
{
    private static readonly Guid Session = Guid.Parse("9610686c-291d-4109-ac1c-3f70ce5318da");
    private static readonly Guid CellId = Guid.Parse("91e9111e-3536-4e9f-af1c-2b1b0a1cd54a");
    private static AvatarHairCellKernel Kernel(AvatarHairCellParameters? parameters = null, AvatarHairCellBath? bath = null) =>
        new(parameters ?? AvatarHairCellParameters.TypeIReference, bath ?? AvatarHairCellBath.Reference);
    private static AvatarVestibularHairCell Cell(double step = .015625, AvatarHairCellParameters? p = null) =>
        new(Session, CellId, Kernel(p), -75, step);
    private static void Close(double expected, double actual, double bound = 1e-12) =>
        Assert.InRange(Math.Abs(expected - actual), 0, bound);

    [Fact]
    public void CapacitanceUsesWholeBasolateralAreaButPumpUsesCleftFacingArea()
    {
        var p = AvatarHairCellParameters.TypeIReference;
        var baseline = Kernel(); var smallerCleft = Kernel(p with { CleftFacingAreaSquareMicrometers = p.CleftFacingAreaSquareMicrometers / 2 });
        Close(6.4021, baseline.CapacitancePf);
        Assert.Equal(baseline.CapacitancePf, smallerCleft.CapacitancePf);
        Close(baseline.PumpTurnoverChargePa / 2, smallerCleft.PumpTurnoverChargePa);
        // Specified whole-cell channel conductances do not acquire a second area factor.
        Assert.Equal(baseline.Currents(-60, .5, .5, .5, 0).PotassiumLowPa,
            smallerCleft.Currents(-60, .5, .5, .5, 0).PotassiumLowPa);
    }

    [Theory]
    [InlineData(-80, 0)]
    [InlineData(-44, 1)]
    [InlineData(-90, 2)]
    public void GatesHaveTheDeclaredHalfActivationVoltages(double voltage, int gate)
    {
        var r = AvatarHairCellKernel.Rates(voltage);
        double[] equilibria = [r.PotassiumLow.Equilibrium, r.Calcium.Equilibrium, r.Hcn1.Equilibrium];
        Assert.Equal(.5, equilibria[gate]);
    }

    [Fact]
    public void ChannelsAreProbabilisticWithFiniteRatesAcrossTheClampStudyDomain()
    {
        for (int i = 0; i <= 1800; i++)
        {
            var r = AvatarHairCellKernel.Rates(-120 + i * .1);
            foreach (var gate in new[] { r.PotassiumLow, r.Calcium, r.Hcn1 })
            {
                Assert.InRange(gate.Equilibrium, 0, 1);
                Assert.True(double.IsFinite(gate.TauMs) && gate.TauMs > 0);
            }
        }
    }

    [Fact]
    public void EachIonicCurrentVanishesAtItsReversalWithAnExplicitOutsidePotential()
    {
        var k = Kernel(bath: AvatarHairCellBath.Reference with { BasolateralOutsidePotentialMv = 9 });
        var potassium = k.Currents(k.PotassiumReversalMv + 9, .3, .4, .7, 0);
        Close(0, potassium.PotassiumLowPa); Close(0, potassium.HcnPotassiumPa);
        Close(0, k.Currents(k.SodiumReversalMv + 9, .3, .4, .7, 0).HcnSodiumPa);
        Close(0, k.Currents(k.CalciumReversalMv + 9, .3, .4, .7, 0).CalciumPa);
        Assert.Equal(0, k.Currents(k.Parameters.MetReversalMv + k.Parameters.EndolymphPotentialMv, .3, .4, .7, 400).MetPa);
    }

    [Fact]
    public void PumpPreservesTwoInThreeOutStoichiometry()
    {
        var k = Kernel(); var c = k.Currents(-70, 0, 0, 0, 0);
        Assert.True(c.PumpPotassiumPa < 0 && c.PumpSodiumPa > 0);
        Close(0, 3 * c.PumpPotassiumPa + 2 * c.PumpSodiumPa);
        Close(k.PumpTurnoverChargePa, c.PumpPotassiumPa + c.PumpSodiumPa);
        Assert.True(Kernel(bath: AvatarHairCellBath.Reference with { PotassiumOutsideMm = 10 }).PumpTurnoverChargePa > k.PumpTurnoverChargePa);
    }

    [Fact]
    public void DoublingExternalPotassiumHasTheNernstShift()
    {
        var k = Kernel(); var doubled = Kernel(bath: AvatarHairCellBath.Reference with { PotassiumOutsideMm = 10 });
        Close(26 * Math.Log(2), doubled.PotassiumReversalMv - k.PotassiumReversalMv);
        Assert.Equal(k.SodiumReversalMv, doubled.SodiumReversalMv);
    }

    [Fact]
    public void MetResponseIsOrderedAndSaturatesWithoutOverflow()
    {
        double prior = -1;
        foreach (double x in new[] { -1e6, -1000, -300, 0, 300, 1000, 1e6 })
        {
            double p = AvatarHairCellKernel.MetOpenProbability(x);
            Assert.InRange(p, 0, 1); Assert.True(p >= prior); prior = p;
        }
        Assert.Equal(0, AvatarHairCellKernel.MetOpenProbability(-1e6));
        Assert.Equal(1, AvatarHairCellKernel.MetOpenProbability(1e6));
    }

    [Fact]
    public void ClampedGatesFollowAnExponentialAndDoNotDependOnCallPartition()
    {
        var r = AvatarHairCellKernel.Rates(-60);
        foreach (var rate in new[] { r.PotassiumLow, r.Calcium, r.Hcn1 })
        {
            double partitioned = .2;
            for (int i = 0; i < 200; i++) partitioned = AvatarHairCellKernel.RelaxClampedGate(partitioned, rate, .125);
            Close(AvatarHairCellKernel.RelaxClampedGate(.2, rate, 25), partitioned, 2e-13);
            Close(rate.Equilibrium + (.2 - rate.Equilibrium) / Math.E,
                AvatarHairCellKernel.RelaxClampedGate(.2, rate, rate.TauMs));
        }
    }

    [Fact]
    public void SameHeldInputIsBitwiseIndependentOfHostGrouping()
    {
        var whole = Cell(); var grouped = Cell(); whole.AdvanceHeldDisplacement(3200, 300);
        for (int remaining = 3200; remaining > 0;)
        {
            int count = Math.Min(17, remaining); grouped.AdvanceHeldDisplacement(count, 300); remaining -= count;
        }
        var a = whole.Capture(); var b = grouped.Capture();
        Assert.Equal(a.Values, b.Values); Assert.Equal(a.AdditionResiduals, b.AdditionResiduals);
        Assert.Equal(a.TimeMs, b.TimeMs); Assert.Equal(a.CompletedSteps, b.CompletedSteps);
        Assert.Equal(a.MaximumChargeResidualFc, b.MaximumChargeResidualFc);
    }

    [Fact]
    public void MetOnlyMembraneMatchesAnIndependentPassiveRcSolution()
    {
        var p = AvatarHairCellParameters.TypeIReference with
        { PotassiumLowMaximumNs = 0, CalciumMaximumNs = 0, Hcn1MaximumNs = 0, PumpDensityPerSquareMicrometer = 0 };
        var cell = Cell(.0078125, p); const double displacement = 300;
        cell.AdvanceHeldDisplacement(256, displacement);
        double reversal = p.EndolymphPotentialMv + p.MetReversalMv;
        double conductance = p.MetMaximumNs * AvatarHairCellKernel.MetOpenProbability(displacement);
        double expected = reversal + (-75 - reversal) * Math.Exp(-2 * conductance / cell.Kernel.CapacitancePf);
        Close(expected, cell.Capture().Values[0], 1e-7);
        Assert.InRange(cell.Capture().MaximumChargeResidualFc, 0, 1e-9);
    }

    [Fact]
    public void ZeroConductancesHoldVoltageAndCharge()
    {
        var p = AvatarHairCellParameters.TypeIReference with
        { MetMaximumNs = 0, PotassiumLowMaximumNs = 0, CalciumMaximumNs = 0, Hcn1MaximumNs = 0, PumpDensityPerSquareMicrometer = 0 };
        var cell = Cell(.03125, p); var before = cell.Capture(); cell.AdvanceHeldDisplacement(200, 1000);
        Assert.Equal(before.Values, cell.Capture().Values); Assert.Equal(0, cell.Capture().MaximumChargeResidualFc);
    }

    [Fact]
    public void OutwardChargeAccountsForCapacitiveChargeChange()
    {
        var cell = Cell(.0078125); cell.AdvanceHeldDisplacement(4096, 300); var s = cell.Capture();
        Close(0, cell.Kernel.CapacitancePf * (s.Values[0] + 75) + s.Values.Skip(4).Sum(), 1e-7);
        Assert.InRange(s.MaximumChargeResidualFc, 0, 1e-7);
        Close(0, 3 * s.Values[9] + 2 * s.Values[10], 1e-8);
    }

    [Fact]
    public void OppositeDeflectionsHaveOppositeInitialElectricalEffects()
    {
        var negative = Cell(); var quiet = Cell(); var positive = Cell();
        negative.AdvanceHeldDisplacement(1, -300); quiet.AdvanceHeldDisplacement(1, 0); positive.AdvanceHeldDisplacement(1, 300);
        Assert.True(positive.Capture().Values[0] > quiet.Capture().Values[0]);
        Assert.True(negative.Capture().Values[0] < quiet.Capture().Values[0]);
    }

    [Fact]
    public void InitialEpochHasNoInventedElapsedTimeOrClaimOfFreeEquilibrium()
    {
        var cell = new AvatarVestibularHairCell(Session, CellId, Kernel(), -75, .015625, 1234.5);
        var before = cell.Capture(); Assert.Equal(0, before.CompletedSteps); Assert.Equal(1234.5, before.TimeMs);
        Assert.Equal(-75, before.Values[0]); Assert.All(before.Values.Skip(4), value => Assert.Equal(0, value));
        var c = cell.Kernel.Currents(before.Values[0], before.Values[1], before.Values[2], before.Values[3], 0);
        Assert.NotEqual(0, c.TotalOutwardPa);
        cell.AdvanceHeldDisplacement(8, 0); Assert.Equal(1234.625, cell.Capture().TimeMs);
    }

    [Fact]
    public void InvalidInputIsRejectedBeforeAnyCellAdvancement()
    {
        var cell = Cell(); var before = cell.Capture();
        Assert.Throws<ArgumentOutOfRangeException>(() => cell.AdvanceHeldDisplacement(-1, 0));
        Assert.Throws<ArgumentException>(() => cell.AdvanceHeldDisplacement(1, double.NaN));
        Assert.Equal(before.Values, cell.Capture().Values); Assert.Equal(before.TimeMs, cell.Capture().TimeMs);
        Assert.False(cell.Capture().Faulted);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    public void InvalidConcentrationsCannotConstructABath(double potassium)
    {
        Assert.Throws<ArgumentException>(() => Kernel(bath: AvatarHairCellBath.Reference with { PotassiumOutsideMm = potassium }));
    }

    [Fact]
    public void FailurePreservesAcceptedStateAndTheRejectedNumericalCandidate()
    {
        var cell = Cell(p: AvatarHairCellParameters.TypeIReference with { MetMaximumNs = 1e300 });
        var before = cell.Capture(); Assert.Throws<ArithmeticException>(() => cell.AdvanceHeldDisplacement(1, 1000));
        var failed = cell.Capture(); Assert.True(failed.Faulted); Assert.NotNull(failed.FailedCandidate);
        Assert.Equal(0, failed.CompletedSteps); Assert.Equal(before.Values, failed.Values);
        Assert.Equal(before.AdditionResiduals, failed.AdditionResiduals);
        Assert.Throws<InvalidOperationException>(() => cell.AdvanceHeldDisplacement(1, 0));
    }

    [Fact]
    public void ACallerCannotMutateTheCellThroughItsSnapshot()
    {
        var cell = Cell(); var captured = cell.Capture(); captured.Values[0] = double.NaN; captured.AdditionResiduals[0] = 1e100;
        Assert.Equal(-75, cell.Capture().Values[0]); Assert.Equal(0, cell.Capture().AdditionResiduals[0]);
        Assert.Equal(Session, cell.Capture().SessionId); Assert.Equal(CellId, cell.Capture().CellId);
    }

    [Fact]
    public void ASecondThreadCannotAdvanceTheCell()
    {
        var cell = Cell(); Exception? error = null;
        var thread = new Thread(() => { try { cell.AdvanceHeldDisplacement(1, 0); } catch (Exception e) { error = e; } });
        thread.Start(); thread.Join(); Assert.IsType<InvalidOperationException>(error); Assert.Equal(0, cell.Capture().CompletedSteps);
    }
}
