using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class CalyxMembraneTests
{
    private static readonly AvatarCalyxGates Gates = new(.2, .3, .4, .5);
    private static AvatarCalyxKernel Kernel(AvatarCalyxParameters? p = null) => new(p ?? AvatarCalyxParameters.DepositedReference);
    private static void Close(double a, double b, double limit = 1e-11) => Assert.InRange(Math.Abs(a - b), 0, limit);

    [Fact]
    public void EachMembraneHasItsOwnCapacitanceAndElectricalReference()
    {
        var k = Kernel(); var e = k.Evaluate(-70, 9, 5, 140, Gates);
        Close(2.9463, k.InnerCapacitancePf); Close(2.8833, k.OuterCapacitancePf);
        Assert.Equal(-79, e.InnerVoltageMv); Assert.Equal(-70, e.OuterVoltageMv);
        Assert.NotEqual(e.Rates.Inner.Kv74, e.Rates.Outer.Kv74);
    }

    [Fact]
    public void CleftPotentialChangesOnlyTheInnerFaceAtFixedGateState()
    {
        var k = Kernel(); var a = k.Evaluate(-70, 0, 5, 140, Gates); var b = k.Evaluate(-70, 9, 5, 140, Gates);
        Assert.Equal(a.Outer, b.Outer); Assert.Equal(a.Rates.Outer, b.Rates.Outer);
        Assert.True(b.Inner.Kv74PotassiumPa < a.Inner.Kv74PotassiumPa);
        Assert.True(b.Inner.HcnSodiumPa < a.Inner.HcnSodiumPa);
        Assert.Equal(a.Inner.PumpPotassiumPa, b.Inner.PumpPotassiumPa);
    }

    [Fact]
    public void ShiftingCalyxAndCleftTogetherPreservesOnlyTheInnerTransmembraneState()
    {
        var k = Kernel(); var a = k.Evaluate(-70, 0, 5, 140, Gates); var b = k.Evaluate(-61, 9, 5, 140, Gates);
        Assert.Equal(a.Inner, b.Inner); Assert.Equal(a.Rates.Inner, b.Rates.Inner);
        Assert.NotEqual(a.Outer, b.Outer); Assert.NotEqual(a.Rates.Outer, b.Rates.Outer);
    }

    [Fact]
    public void CleftPotassiumShiftsItsReversalButDoesNotChangeTheOuterReservoir()
    {
        var k = Kernel(); var a = k.Evaluate(-70, 0, 5, 140, Gates); var b = k.Evaluate(-70, 0, 10, 140, Gates);
        Close(26 * Math.Log(2), b.InnerPotassiumReversalMv - a.InnerPotassiumReversalMv);
        Assert.Equal(a.InnerSodiumReversalMv, b.InnerSodiumReversalMv); Assert.Equal(a.Outer, b.Outer);
        Assert.True(Math.Abs(b.Inner.PumpPotassiumPa) > Math.Abs(a.Inner.PumpPotassiumPa));
        Assert.True(Math.Abs(b.Inner.KccPotassiumPa) > Math.Abs(a.Inner.KccPotassiumPa));
    }

    [Fact]
    public void ElectricalNeutralityDoesNotEraseKccPotassiumTransport()
    {
        var on = Kernel().Evaluate(-70, 0, 5, 140, Gates);
        var off = Kernel(AvatarCalyxParameters.DepositedReference with { InnerKccMaximumPaPerSquareMicrometer = 0 }).Evaluate(-70, 0, 5, 140, Gates);
        Assert.True(on.Inner.KccPotassiumPa < 0); Assert.True(on.Inner.KccChloridePa > 0);
        Assert.Equal(0, on.Inner.KccPotassiumPa + on.Inner.KccChloridePa);
        Assert.Equal(on.TotalOutwardPa, off.TotalOutwardPa);
        Assert.NotEqual(on.Inner.PotassiumOutwardPa, off.Inner.PotassiumOutwardPa);
        Close(294.63, -Kernel().Evaluate(-70, 0, 17.5, 140, Gates).Inner.KccPotassiumPa);
    }

    [Fact]
    public void BothPumpsRetainTwoInThreeOutStoichiometry()
    {
        var e = Kernel().Evaluate(-70, 9, 5, 140, Gates);
        foreach (var c in new[] { e.Inner, e.Outer })
        {
            Assert.True(c.PumpPotassiumPa < 0 && c.PumpSodiumPa > 0);
            Close(0, 3 * c.PumpPotassiumPa + 2 * c.PumpSodiumPa);
            Assert.True(c.PumpPotassiumPa + c.PumpSodiumPa > 0);
        }
        Close(2 * 288.33 / 294.63, e.Outer.PumpPotassiumPa / e.Inner.PumpPotassiumPa);
    }

    [Fact]
    public void AreaScalesCapacitanceAndTransporterCountsButNotWholeCellConductances()
    {
        var p = AvatarCalyxParameters.DepositedReference;
        var k = Kernel(); var doubled = Kernel(p with { InnerAreaSquareMicrometers = 2 * p.InnerAreaSquareMicrometers,
            OuterAreaSquareMicrometers = 2 * p.OuterAreaSquareMicrometers });
        var a = k.Evaluate(-70, 9, 5, 140, Gates); var b = doubled.Evaluate(-70, 9, 5, 140, Gates);
        Assert.Equal(2 * k.InnerCapacitancePf, doubled.InnerCapacitancePf);
        Assert.Equal(2 * k.OuterCapacitancePf, doubled.OuterCapacitancePf);
        Assert.Equal(a.Inner.Kv74PotassiumPa, b.Inner.Kv74PotassiumPa); Assert.Equal(a.Outer.HcnSodiumPa, b.Outer.HcnSodiumPa);
        Assert.Equal(a.Outer.UnassignedLeakPa, b.Outer.UnassignedLeakPa);
        Close(2 * a.Inner.PumpPotassiumPa, b.Inner.PumpPotassiumPa); Close(2 * a.Inner.KccPotassiumPa, b.Inner.KccPotassiumPa);
    }

    [Fact]
    public void WholeCalyxKv74IsSplitFourToOneAndHcnEqually()
    {
        var e = Kernel().Evaluate(-60, 0, 5, 140, new(1, 1, 1, 1));
        Close(19.2, e.Inner.Kv74PotassiumPa / (-60 - e.InnerPotassiumReversalMv));
        Close(4.8, e.Outer.Kv74PotassiumPa / (-60 - AvatarCalyxKernel.OuterPotassiumReversalMv));
        Close(1.85, e.Inner.HcnPotassiumPa / (.8 * (-60 - e.InnerPotassiumReversalMv)));
        Close(1.85, e.Outer.HcnSodiumPa / (.2 * (-60 - AvatarCalyxKernel.OuterSodiumReversalMv)));
    }

    [Fact]
    public void IonicChannelsVanishAtTheExplicitInnerAndStoredOuterReversals()
    {
        var k = Kernel(); var baseline = k.Evaluate(-70, 9, 5, 140, Gates);
        var innerK = k.Evaluate(baseline.InnerPotassiumReversalMv + 9, 9, 5, 140, Gates);
        Close(0, innerK.Inner.Kv74PotassiumPa); Close(0, innerK.Inner.HcnPotassiumPa);
        Close(0, k.Evaluate(baseline.InnerSodiumReversalMv + 9, 9, 5, 140, Gates).Inner.HcnSodiumPa);
        var outerK = k.Evaluate(AvatarCalyxKernel.OuterPotassiumReversalMv, 0, 5, 140, Gates);
        Assert.Equal(0, outerK.Outer.Kv74PotassiumPa); Assert.Equal(0, outerK.Outer.HcnPotassiumPa);
        Assert.Equal(0, k.Evaluate(AvatarCalyxKernel.OuterSodiumReversalMv, 0, 5, 140, Gates).Outer.HcnSodiumPa);
    }

    [Fact]
    public void SourceOuterLeakIsReportedWithoutInventingAnIonicSpecies()
    {
        var e = Kernel().Evaluate(-70, 9, 5, 140, Gates);
        Assert.Equal(0, e.Inner.UnassignedLeakPa); Assert.Equal(-140, e.Outer.UnassignedLeakPa);
        Close(e.Outer.TotalOutwardPa, e.Outer.PotassiumOutwardPa + e.Outer.SodiumOutwardPa + e.Outer.UnassignedLeakPa);
    }

    [Theory]
    [InlineData(-52, false)]
    [InlineData(-95, true)]
    public void ChannelHalfActivationVoltagesRemainDistinct(double voltage, bool hcn)
    {
        var r = AvatarCalyxKernel.FaceRates(voltage); Assert.Equal(.5, hcn ? r.Hcn2.Equilibrium : r.Kv74.Equilibrium);
    }

    [Fact]
    public void SourceKv74FitHasItsDeclaredOneMillisecondMinimum()
    {
        var low = AvatarCalyxKernel.FaceRates(-60); var high = AvatarCalyxKernel.FaceRates(80);
        Assert.True(low.Kv74.TauMs > 1); Assert.Equal(1, high.Kv74.TauMs);
    }

    [Fact]
    public void StoredHcnConventionIsExplicitlyDifferentFromThePrintedSignVariant()
    {
        const double voltage = -120;
        double printedTau = 209.479 + (Math.Exp(-(voltage + 80.646) / 6.916) + Math.Exp((voltage + 80.646) / 14.881)) / 2551.988;
        Assert.True(Math.Abs(AvatarCalyxKernel.FaceRates(voltage).Hcn2.TauMs - printedTau) > .1);
        Assert.Equal(3.7, AvatarCalyxParameters.DepositedReference.Hcn2MaximumNs);
        Assert.Contains("deposited-20221205", AvatarCalyxKernel.Version);
    }

    [Fact]
    public void RatesRemainFiniteAndProbabilisticAcrossTheDeclaredClampDomain()
    {
        for (int i = 0; i <= 2000; i++)
        {
            var r = AvatarCalyxKernel.FaceRates(-120 + i * .1);
            foreach (var rate in new[] { r.Kv74, r.Hcn2 })
            {
                Assert.InRange(rate.Equilibrium, 0, 1); Assert.True(double.IsFinite(rate.TauMs) && rate.TauMs > 0);
            }
        }
    }

    [Fact]
    public void VoltageClampRelaxationPreservesZeroTimeAndAnalyticPartitioning()
    {
        Assert.Equal(Gates, AvatarCalyxKernel.RelaxVoltageClamp(Gates, -60, 9, 0));
        var whole = AvatarCalyxKernel.RelaxVoltageClamp(Gates, -60, 9, 25); var split = Gates;
        for (int i = 0; i < 200; i++) split = AvatarCalyxKernel.RelaxVoltageClamp(split, -60, 9, .125);
        Close(whole.InnerKv74, split.InnerKv74, 2e-13); Close(whole.InnerHcn2, split.InnerHcn2, 2e-13);
        Close(whole.OuterKv74, split.OuterKv74, 2e-13); Close(whole.OuterHcn2, split.OuterHcn2, 2e-13);
        var rate = AvatarCalyxKernel.Rates(-60, 9).Inner.Kv74;
        var efold = AvatarCalyxKernel.RelaxVoltageClamp(Gates, -60, 9, rate.TauMs);
        Close(rate.Equilibrium + (Gates.InnerKv74 - rate.Equilibrium) / Math.E, efold.InnerKv74);
    }

    [Fact]
    public void ConditionedGatesDoNotClaimFreeElectricalEquilibrium()
    {
        var gates = AvatarCalyxKernel.VoltageConditionedGates(-70, 0);
        var result = Kernel().Evaluate(-70, 0, 5, 140, gates);
        Assert.NotEqual(0, result.TotalOutwardPa);
        Assert.Equal(gates.InnerKv74, result.Rates.Inner.Kv74.Equilibrium);
        Assert.Equal(gates.OuterHcn2, result.Rates.Outer.Hcn2.Equilibrium);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    public void InvalidConcentrationsAreRejectedWithoutSubstitutingAReferenceBath(double potassium)
    {
        Assert.Throws<ArgumentException>(() => Kernel().Evaluate(-70, 0, potassium, 140, Gates));
    }

    [Fact]
    public void InvalidGateGeometryAndClockInputsAreNotClipped()
    {
        Assert.Throws<ArgumentException>(() => Kernel().Evaluate(-70, 0, 5, 140, Gates with { InnerKv74 = 1.001 }));
        Assert.Throws<ArgumentException>(() => Kernel(AvatarCalyxParameters.DepositedReference with { Kv74InnerFraction = -1 }));
        Assert.Throws<ArgumentException>(() => Kernel(AvatarCalyxParameters.DepositedReference with { InnerAreaSquareMicrometers = 0 }));
        Assert.Throws<ArgumentException>(() => AvatarCalyxKernel.Rates(-70, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => AvatarCalyxKernel.RelaxVoltageClamp(Gates, -70, 0, -1));
    }

    [Fact]
    public void ParametersAndReferenceVersionParticipateInTheFingerprint()
    {
        var original = Kernel(); Assert.Equal(original.Fingerprint, Kernel().Fingerprint);
        Assert.NotEqual(original.Fingerprint, Kernel(AvatarCalyxParameters.DepositedReference with { Hcn2MaximumNs = 3.6 }).Fingerprint);
        Assert.Equal(64, original.Fingerprint.Length);
    }
}
