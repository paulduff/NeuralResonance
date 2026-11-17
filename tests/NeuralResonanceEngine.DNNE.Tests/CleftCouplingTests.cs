using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class CleftCouplingTests
{
    // Deliberately synthetic geometry; these fixtures are not an anatomy fit.
    private static readonly AvatarCleftEdgeGeometry Geometry = new(2, 4, 3, 7);
    private static AvatarCleftEdgeKernel Edge(AvatarCleftMaterial? material = null, AvatarCleftEdgeGeometry? geometry = null) =>
        new(material ?? AvatarCleftMaterial.DepositedReference, geometry ?? Geometry);
    private static AvatarCleftChargeBalance Block(AvatarCleftCapacitances? p = null) =>
        new(p ?? AvatarCleftCapacitances.FromMembraneGeometry(AvatarHairCellParameters.TypeIReference, AvatarCalyxParameters.DepositedReference));
    private static void Close(double expected, double actual, double absolute = 1e-11, double relative = 1e-12) =>
        Assert.InRange(Math.Abs(expected - actual), 0, absolute + relative * Math.Abs(expected));

    [Fact]
    public void UniformSitesHaveNoChemicalOrElectricalTransfer()
    {
        var result = Edge().Evaluate(new(12, 5, 140), new(12, 5, 140));
        Assert.Equal(0, result.PotassiumAttomolesPerMs); Assert.Equal(0, result.SodiumAttomolesPerMs);
        Assert.Equal(0, result.TotalCurrentPa); Assert.Equal(0, result.DissipationFw);
        Assert.Equal(default, result.ConcentrationRates);
    }

    [Fact]
    public void ZeroFieldGivesFickFluxWithExplicitAmountsAndVolumes()
    {
        var result = Edge().Evaluate(new(0, 8, 130), new(0, 3, 140));
        Close(.81 * 2 / 4 * 5, result.PotassiumAttomolesPerMs);
        Close(.56 * 2 / 4 * -10, result.SodiumAttomolesPerMs);
        Close(96.485 * .81 * 2 / 4 * 5, result.PotassiumCurrentPa);
        Assert.True(result.ConcentrationRates.LeftPotassiumMmPerMs < 0);
        Assert.True(result.ConcentrationRates.RightPotassiumMmPerMs > 0);
        Close(result.PotassiumAttomolesPerMs, result.ConcentrationRates.RightPotassiumMmPerMs * 7);
        Close(-result.PotassiumAttomolesPerMs, result.ConcentrationRates.LeftPotassiumMmPerMs * 3);
    }

    [Theory]
    [InlineData(-1e-10)]
    [InlineData(1e-10)]
    [InlineData(-52)]
    [InlineData(52)]
    public void EqualConcentrationsGiveExactDriftAndNoDoubleCountedConductivity(double rightMv)
    {
        var result = Edge().Evaluate(new(0, 5, 140), new(rightMv, 5, 140));
        double k = -.81 * .5 * 5 * rightMv / 26;
        double na = -.56 * .5 * 140 * rightMv / 26;
        Close(k, result.PotassiumAttomolesPerMs, 1e-24);
        Close(na, result.SodiumAttomolesPerMs, 1e-24);
        Close(-600 * .5 * rightMv, result.OtherIonCurrentPa, 1e-24);
        Close(-(600 + 96.485 * (.81 * 5 + .56 * 140) / 26) * .5 * rightMv, result.TotalCurrentPa, 1e-20);
    }

    [Fact]
    public void ElectrochemicalEquilibriumCancelsDiffusionAndDrift()
    {
        var edge = Edge(AvatarCleftMaterial.DepositedReference with { OtherIonConductivityNsPerMicrometer = 0 });
        var result = edge.Evaluate(new(0, 5, 140), new(-26 * Math.Log(2), 10, 280));
        Close(0, result.PotassiumAttomolesPerMs); Close(0, result.SodiumAttomolesPerMs);
        Close(0, result.PotassiumElectrochemicalDropMv); Close(0, result.SodiumElectrochemicalDropMv);
        Close(0, result.TotalCurrentPa); Close(0, result.DissipationFw);
    }

    [Fact]
    public void ReversingAnEdgeReversesFluxAndConservesBothSpecies()
    {
        var left = new AvatarCleftSite(-9, 12, 110); var right = new AvatarCleftSite(4, 5, 140);
        var a = Edge().Evaluate(left, right);
        var b = Edge(geometry: Geometry with { LeftVolumeCubicMicrometers = 7, RightVolumeCubicMicrometers = 3 }).Evaluate(right, left);
        Close(-a.PotassiumAttomolesPerMs, b.PotassiumAttomolesPerMs);
        Close(-a.SodiumAttomolesPerMs, b.SodiumAttomolesPerMs); Close(-a.TotalCurrentPa, b.TotalCurrentPa);
        Close(a.ConcentrationRates.LeftPotassiumMmPerMs, b.ConcentrationRates.RightPotassiumMmPerMs);
        Close(a.DissipationFw, b.DissipationFw);
        Close(0, 3 * a.ConcentrationRates.LeftPotassiumMmPerMs + 7 * a.ConcentrationRates.RightPotassiumMmPerMs);
        Close(0, 3 * a.ConcentrationRates.LeftSodiumMmPerMs + 7 * a.ConcentrationRates.RightSodiumMmPerMs);
    }

    [Fact]
    public void VoltageGaugeShiftDoesNotChangeAnEdge()
    {
        Assert.Equal(Edge().Evaluate(new(-9, 12, 110), new(4, 5, 140)),
            Edge().Evaluate(new(23, 12, 110), new(36, 5, 140)));
    }

    [Fact]
    public void GeometricScalingChangesTransferButKeepsVolumeAccountingExplicit()
    {
        var left = new AvatarCleftSite(-9, 12, 110); var right = new AvatarCleftSite(4, 5, 140);
        var a = Edge().Evaluate(left, right);
        var b = Edge(geometry: Geometry with { FaceAreaSquareMicrometers = 4 }).Evaluate(left, right);
        var c = Edge(geometry: Geometry with { CenterDistanceMicrometers = 8 }).Evaluate(left, right);
        Close(2 * a.TotalCurrentPa, b.TotalCurrentPa); Close(a.TotalCurrentPa / 2, c.TotalCurrentPa);
        var volume = Edge(geometry: Geometry with { LeftVolumeCubicMicrometers = 6 }).Evaluate(left, right);
        Assert.Equal(a.TotalCurrentPa, volume.TotalCurrentPa);
        Close(a.ConcentrationRates.LeftPotassiumMmPerMs / 2, volume.ConcentrationRates.LeftPotassiumMmPerMs);
        Assert.Equal(a.ConcentrationRates.RightPotassiumMmPerMs, volume.ConcentrationRates.RightPotassiumMmPerMs);
    }

    [Fact]
    public void ExactConstantFieldProfileCarriesTheSameFluxThroughTwoHalfEdges()
    {
        const double psi = 2;
        static double Middle(double left, double right)
        {
            double steady = (right - left * Math.Exp(-psi)) / (1 - Math.Exp(-psi));
            return (left - steady) * Math.Exp(-psi / 2) + steady;
        }
        var a = new AvatarCleftSite(0, 5, 140); var b = new AvatarCleftSite(52, 20, 100);
        var middle = new AvatarCleftSite(26, Middle(5, 20), Middle(140, 100));
        var whole = Edge().Evaluate(a, b);
        var half = Edge(geometry: Geometry with { CenterDistanceMicrometers = 2 });
        foreach (var value in new[] { half.Evaluate(a, middle), half.Evaluate(middle, b) })
        {
            Close(whole.PotassiumAttomolesPerMs, value.PotassiumAttomolesPerMs);
            Close(whole.SodiumAttomolesPerMs, value.SodiumAttomolesPerMs);
            Close(whole.TotalCurrentPa, value.TotalCurrentPa);
        }
    }

    [Fact]
    public void ConstitutiveFluxDissipatesElectrochemicalPotentialAcrossRepresentativeStates()
    {
        foreach (double v in new[] { -520d, -52, -.026, 0, .026, 52, 520 })
        foreach (double k in new[] { .1, 5, 50d })
        foreach (double na in new[] { .1, 140, 300d })
        {
            var e = Edge().Evaluate(new(0, 5, 140), new(v, k, na));
            Assert.True(e.PotassiumCurrentPa * e.PotassiumElectrochemicalDropMv >= -1e-15);
            Assert.True(e.SodiumCurrentPa * e.SodiumElectrochemicalDropMv >= -1e-15);
            Assert.True(e.DissipationFw >= -1e-15);
        }
    }

    [Theory]
    [InlineData(-26000)]
    [InlineData(26000)]
    public void ExtremeConstantFieldsRemainFiniteWithoutExponentialOverflow(double rightMv)
    {
        var e = Edge().Evaluate(new(0, 5, 140), new(rightMv, 5, 140));
        Assert.True(double.IsFinite(e.TotalCurrentPa)); Assert.True(double.IsFinite(e.DissipationFw));
        Close(-.81 * .5 * 5 * rightMv / 26, e.PotassiumAttomolesPerMs);
    }

    [Fact]
    public void ZeroSpeciesMobilityRemovesItsTransportWithoutRemovingOtherIons()
    {
        var e = Edge(AvatarCleftMaterial.DepositedReference with { PotassiumDiffusionSquareMicrometersPerMs = 0 }).Evaluate(new(0, 5, 140), new(26, 10, 100));
        Assert.Equal(0, e.PotassiumAttomolesPerMs); Assert.Equal(0, e.ConcentrationRates.LeftPotassiumMmPerMs);
        Assert.NotEqual(0, e.SodiumAttomolesPerMs); Assert.NotEqual(0, e.OtherIonCurrentPa);
    }

    [Fact]
    public void InvalidStatesAndUnrepresentableGeometryAreRejectedWithoutClipping()
    {
        Assert.Throws<ArgumentException>(() => Edge().Evaluate(new(0, 0, 140), new(0, 5, 140)));
        Assert.Throws<ArgumentException>(() => Edge().Evaluate(new(double.NaN, 5, 140), new(0, 5, 140)));
        Assert.Throws<ArgumentException>(() => Edge().Evaluate(new(double.MaxValue, 5, 140), new(-double.MaxValue, 5, 140)));
        Assert.Throws<ArgumentException>(() => Edge(geometry: Geometry with { CenterDistanceMicrometers = 0 }));
        Assert.Throws<ArgumentException>(() => Edge(geometry: Geometry with { LeftVolumeCubicMicrometers = -1 }));
        Assert.Throws<ArgumentException>(() => Edge(geometry: Geometry with { FaceAreaSquareMicrometers = double.Epsilon, CenterDistanceMicrometers = double.MaxValue }));
        Assert.Throws<ArgumentException>(() => Edge(AvatarCleftMaterial.DepositedReference with { ThermalVoltageMv = 0 }));
    }

    [Fact]
    public void PhysicalParametersParticipateInTransportAndCapacitanceFingerprints()
    {
        Assert.Equal(Edge().Fingerprint, Edge().Fingerprint);
        Assert.NotEqual(Edge().Fingerprint, Edge(geometry: Geometry with { LeftVolumeCubicMicrometers = 4 }).Fingerprint);
        Assert.NotEqual(Edge().Fingerprint, Edge(AvatarCleftMaterial.DepositedReference with { ThermalVoltageMv = 27 }).Fingerprint);
        Assert.NotEqual(Block().Fingerprint, Block(Block().Capacitances with { HairApicalPf = 4 }).Fingerprint);
    }

    [Fact]
    public void WholeMembraneGeometrySeparatesEndolymphExposedAndCleftCapacitance()
    {
        var p = Block().Capacitances;
        Close(3, p.HairApicalPf); Close(.4558, p.HairPerilymphPf); Close(2.9463, p.HairCleftPf);
        Close(2.9463, p.CalyxInnerPf); Close(2.8833, p.CalyxOuterPf);
        Close(6.4021, p.HairApicalPf + p.HairPerilymphPf + p.HairCleftPf);
        Assert.Throws<ArgumentException>(() => AvatarCleftCapacitances.FromMembraneGeometry(
            AvatarHairCellParameters.TypeIReference, AvatarCalyxParameters.DepositedReference with { InnerAreaSquareMicrometers = 300 }));
    }

    [Fact]
    public void SharedCapacitanceSolveSatisfiesAllThreeKirchhoffEquations()
    {
        var block = Block(); var p = block.Capacitances;
        foreach (var ports in new[] { new AvatarCleftPortCurrents(7, 3, 11, -13, 17, 19, -23),
            new AvatarCleftPortCurrents(0, 0, 1, 0, 0, 0, 0), new AvatarCleftPortCurrents(0, 0, 0, 1, 0, 0, 0),
            new AvatarCleftPortCurrents(0, 0, 0, 0, 0, 1, 0) })
        {
            var d = block.Solve(ports);
            Close(ports.HairChargeRatePa, (p.HairApicalPf + p.HairPerilymphPf + p.HairCleftPf) * d.HairMvPerMs - p.HairCleftPf * d.CleftMvPerMs);
            Close(ports.CleftChargeRatePa, -p.HairCleftPf * d.HairMvPerMs + (p.HairCleftPf + p.CalyxInnerPf) * d.CleftMvPerMs - p.CalyxInnerPf * d.CalyxMvPerMs);
            Close(ports.CalyxChargeRatePa, -p.CalyxInnerPf * d.CleftMvPerMs + (p.CalyxInnerPf + p.CalyxOuterPf) * d.CalyxMvPerMs);
        }
    }

    [Fact]
    public void UnitCapacitanceCircuitHasKnownCoupledResponse()
    {
        var b = Block(new(1, 0, 1, 1, 1));
        var d = b.Solve(new(0, 0, 0, 0, 0, -1, 0));
        Close(.5, d.HairMvPerMs); Close(1, d.CleftMvPerMs); Close(.5, d.CalyxMvPerMs);
        Assert.Equal(default, b.Solve(default));
    }

    [Fact]
    public void MembraneTransferIsInternalAndOnlyNamedExternalPortsChangeTotalLocalCharge()
    {
        var b = Block();
        var ports = new AvatarCleftPortCurrents(7, 3, 11, -13, 17, 19, -23);
        var q = b.ChargeRates(b.Solve(ports));
        Close(-(7 + 3 + 17 + 19 - 23), q.HairPa + q.CleftPa + q.CalyxPa);
        Close(0, q.HairPa + q.CleftPa + q.CalyxPa + q.EndolymphPa + q.PerilymphPa);
        var internalOnly = b.ChargeRates(b.Solve(new(0, 0, 11, -13, 0, 0, 0)));
        Close(0, internalOnly.HairPa + internalOnly.CleftPa + internalOnly.CalyxPa);
    }

    [Fact]
    public void HeldBathsRetainTheirCapacitorChargeAndGaugeInvariance()
    {
        var b = Block(); var a = new AvatarCleftPotentials(-70, 3, -60, 5, 0);
        var shifted = new AvatarCleftPotentials(-54, 19, -44, 21, 16);
        var q = b.Charges(a);
        Assert.Equal(q, b.Charges(shifted)); Assert.Equal(b.StoredEnergyAttojoules(a), b.StoredEnergyAttojoules(shifted));
        Close(0, q.HairFc + q.CleftFc + q.CalyxFc + q.EndolymphFc + q.PerilymphFc);
        Close(225, q.EndolymphFc);
        Assert.NotEqual(q.HairFc, b.Charges(a with { EndolymphMv = 0 }).HairFc);
    }

    [Fact]
    public void CapacitiveEnergyRateMatchesTerminalPowerIncludingHeldBathCharge()
    {
        var b = Block(); var v = new AvatarCleftPotentials(-70, 3, -60, 5, 2);
        var ports = new AvatarCleftPortCurrents(7, 3, 11, -13, 17, 19, -23);
        var d = b.Solve(ports); var q = b.ChargeRates(d);
        double power = v.HairMv * ports.HairChargeRatePa + v.CleftMv * ports.CleftChargeRatePa +
            v.CalyxMv * ports.CalyxChargeRatePa + v.EndolymphMv * q.EndolymphPa + v.PerilymphMv * q.PerilymphPa;
        Close(power, b.StoredEnergyRateFw(v, d));
        Assert.True(b.StoredEnergyAttojoules(v) > 0);
        Assert.Equal(0, b.StoredEnergyAttojoules(new(5, 5, 5, 5, 5)));
    }

    [Fact]
    public void PositiveCapacitancesWithLargeRatiosDoNotNeedAnArtificialGroundLeak()
    {
        var b = Block(new(1, 0, 1e12, 1e12, 1));
        var d = b.Solve(new(0, 0, 0, 0, 0, -1, 0));
        Close(.5, d.HairMvPerMs); Close(.5 + .5e-12, d.CleftMvPerMs); Close(.5, d.CalyxMvPerMs);
    }

    [Fact]
    public void InvalidCapacitiveParametersAndNonfiniteCurrentsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => Block(new(0, 0, 1, 1, 1)));
        Assert.Throws<ArgumentException>(() => Block(new(1, -1, 1, 1, 1)));
        Assert.Throws<ArgumentException>(() => Block(new(1, 0, 1, 1, double.PositiveInfinity)));
        Assert.Throws<ArgumentException>(() => Block().Solve(new(0, 0, double.NaN, 0, 0, 0, 0)));
        Assert.Throws<ArgumentException>(() => Block().StoredEnergyAttojoules(new(double.MaxValue, 0, 0, 0, 0)));
    }
}
