using System.Text.Json;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class CleftOpenBoundaryTests
{
    private static AvatarCleftCompartment Node(string id, double volume = 3) => new(id,volume,1,1,1);
    private static AvatarCleftCompartmentState State(string id = "a", double v = 0, double k = 5, double na = 140) => new(id,v,-60,k,na);
    private static AvatarCleftMembranePort Quiet(string id = "a") => new(id,0,0,0,0,0,0,0,0);
    private static AvatarCleftApexFace Face(string id = "apex", string node = "a", double area = 2) => new(id,node,area,4);
    private static AvatarCleftSpatialCoupling Closed() => new("hair",1,0,AvatarCleftMaterial.DepositedReference,[Node("a")],[]);
    private static AvatarCleftOpenCoupling Open() => new(Closed(),new("perilymph",5,140),[Face()]);
    private static void Close(double expected, double actual) => Assert.InRange(Math.Abs(expected-actual),0,1e-9+1e-11*Math.Abs(expected));

    [Fact]
    public void BoundaryAtEqualVoltageAndConcentrationsAddsNoFluxOrActivity()
    {
        var open = Open(); var states = new[] { State() }; var ports = new[] { Quiet() };
        var a = open.Evaluate(-70,5,0,0,0,states,ports);
        var b = open.Spatial.Evaluate(-70,5,0,0,0,states,ports);
        Assert.Equal(JsonSerializer.Serialize(b),JsonSerializer.Serialize(a.Spatial));
        var f = Assert.Single(a.ApexTransfers).Outgoing;
        Assert.Equal(0,f.PotassiumAttomolesPerMs); Assert.Equal(0,f.SodiumAttomolesPerMs);
        Assert.Equal(0,f.TotalCurrentPa); Assert.Equal(0,a.Perilymph.HoldingCurrentPa);
    }

    [Fact]
    public void FickTransferChangesTheFiniteCleftAndRetainsEveryReservoirAmount()
    {
        var r = Open().Evaluate(-70,5,0,0,0,[State(k:8,na:130)],[Quiet()]);
        var f = Assert.Single(r.ApexTransfers).Outgoing; var n = Assert.Single(r.Spatial.Compartments);
        Close(1.215,f.PotassiumAttomolesPerMs); Close(-2.8,f.SodiumAttomolesPerMs); Assert.Equal(0,f.OtherIonCurrentPa);
        Close(-1.215/3,n.PotassiumMmPerMs); Close(2.8/3,n.SodiumMmPerMs);
        Close(0,3*n.PotassiumMmPerMs+r.Perilymph.CleftPotassiumAttomolesPerMs);
        Close(0,3*n.SodiumMmPerMs+r.Perilymph.CleftSodiumAttomolesPerMs);
        Close(96.485*(1.215-2.8),f.TotalCurrentPa);
        Assert.True(f.DissipationFw > 0);
    }

    [Fact]
    public void EqualConcentrationsStillCarryElectricDriftAndSeparateOtherIonCurrent()
    {
        var r = Open().Evaluate(-70,5,0,0,0,[State(v:26)],[Quiet()]);
        var f = Assert.Single(r.ApexTransfers).Outgoing;
        Close(2.025,f.PotassiumAttomolesPerMs); Close(39.2,f.SodiumAttomolesPerMs);
        Close(7800,f.OtherIonCurrentPa); Close(96.485*(2.025+39.2)+7800,f.TotalCurrentPa);
        Assert.True(r.Spatial.Compartments[0].CleftMvPerMs < 0);
    }

    [Fact]
    public void BathCanSupplyIonsWithoutClampingTheInteriorToItsConcentration()
    {
        var state = State(k:3,na:130);
        var r = Open().Evaluate(-70,5,0,0,0,[state],[Quiet()]);
        Assert.True(r.Perilymph.CleftPotassiumAttomolesPerMs < 0);
        Assert.True(r.Perilymph.CleftSodiumAttomolesPerMs < 0);
        Assert.True(r.Spatial.Compartments[0].PotassiumMmPerMs > 0);
        Assert.True(r.Spatial.Compartments[0].SodiumMmPerMs > 0);
        Assert.Equal(3,state.PotassiumMm); Assert.Equal(130,state.SodiumMm);
    }

    [Fact]
    public void BoundaryCurrentSolvesTheCoupledCapacitancesAndBothHoldingCurrents()
    {
        var r = Open().Evaluate(-70,5,0,0,0,[State(v:26)],[Quiet()]);
        double j = r.Perilymph.CleftCurrentPa; var n = Assert.Single(r.Spatial.Compartments);
        // Analytic unit-capacitor graph: H-E, H-S, C-S, C-P.
        Close(-j/2,r.Spatial.HairMvPerMs); Close(-j,n.CleftMvPerMs); Close(-j/2,n.CalyxMvPerMs);
        Close(0,r.Spatial.HairChargeRatePa); Close(-j,n.CleftChargeRatePa); Close(0,n.CalyxChargeRatePa);
        Close(j/2,r.EndolymphHoldingCurrentPa); Close(-j/2,r.Perilymph.HoldingCurrentPa);
        Close(j/2,r.Perilymph.CapacitiveChargeRatePa);
        Close(0,r.Perilymph.HoldingCurrentPa+r.EndolymphHoldingCurrentPa);
    }

    [Fact]
    public void SplitOpeningPreservesTotalTransferAndWholeCellResponse()
    {
        var single = Open();
        var split = new AvatarCleftOpenCoupling(Closed(),new("perilymph",5,140),[Face("left",area:1),Face("right",area:1)]);
        var a = single.Evaluate(-70,5,0,0,0,[State(v:26,k:8,na:130)],[Quiet()]);
        var b = split.Evaluate(-70,5,0,0,0,[State(v:26,k:8,na:130)],[Quiet()]);
        Assert.Equal(JsonSerializer.Serialize(a.Spatial),JsonSerializer.Serialize(b.Spatial));
        Assert.Equal(a.Perilymph,b.Perilymph); Assert.Equal(2,b.ApexTransfers.Count);
        Assert.NotEqual(single.Fingerprint,split.Fingerprint);
    }

    [Fact]
    public void WholeNetworkBalancesMembraneSourcesApexFluxAndAxonCurrent()
    {
        var spatial = new AvatarCleftSpatialCoupling("hair",1,0,AvatarCleftMaterial.DepositedReference,
            [Node("a"),Node("b",7)],[new("ab","a","b",.02,.5,3)]);
        var open = new AvatarCleftOpenCoupling(spatial,new("perilymph",5,140),[Face(node:"b")]);
        var states = new[] { State(v:-9,k:12,na:110),State("b",4,5,140) };
        var ports = new[] { new AvatarCleftMembranePort("a",11,-13,17,-23,96.485,0,0,-192.97),new("b",-2,3,-5,7,0,0,0,0) };
        var r = open.Evaluate(-70,5,2,7,3,states,ports);
        var a = r.Spatial.Compartments[0]; var b = r.Spatial.Compartments[1];
        Close(1,3*a.PotassiumMmPerMs+7*b.PotassiumMmPerMs+r.Perilymph.CleftPotassiumAttomolesPerMs);
        Close(-2,3*a.SodiumMmPerMs+7*b.SodiumMmPerMs+r.Perilymph.CleftSodiumAttomolesPerMs);
        Close(15,r.Perilymph.MembraneCurrentPa); // hair 3 + calyx 17 - 5
        Close(-16,r.Perilymph.HoldingCurrentPa+r.EndolymphHoldingCurrentPa); // external axon -23 + 7
        Close(0,r.Spatial.HairChargeRatePa+a.CleftChargeRatePa+a.CalyxChargeRatePa+b.CleftChargeRatePa+b.CalyxChargeRatePa+
            r.Spatial.PerilymphChargeRatePa+r.Spatial.EndolymphChargeRatePa);
    }

    [Fact]
    public void GaugeShiftAndInputOrderPreserveTheOpenPhysicalResult()
    {
        var spatial = new AvatarCleftSpatialCoupling("hair",1,0,AvatarCleftMaterial.DepositedReference,[Node("a"),Node("b",7)],[]);
        var first = new AvatarCleftOpenCoupling(spatial,new("perilymph",5,140),[Face("b","b"),Face("a")]);
        var second = new AvatarCleftOpenCoupling(spatial,new("perilymph",5,140),[Face("a"),Face("b","b")]);
        var states = new[] { State(v:-9,k:12,na:110),State("b",4,5,140) }; var ports = new[] { Quiet(),Quiet("b") };
        var a = first.Evaluate(-70,5,2,0,0,states,ports);
        var b = second.Evaluate(-38,37,34,0,0,states.Reverse().Select(s => s with { CleftMv=s.CleftMv+32,CalyxMv=s.CalyxMv+32 }).ToArray(),ports.Reverse().ToArray());
        Assert.Equal(first.Fingerprint,second.Fingerprint);
        Assert.Equal(JsonSerializer.Serialize(a),JsonSerializer.Serialize(b));
    }

    [Fact]
    public void ElectricalEnergyRetainsTheHeldBathCapacitorTerms()
    {
        var r = Open().Evaluate(-70,5,2,0,0,[State(v:4,k:8,na:130)],[Quiet()]);
        double expected = -4*r.Perilymph.CleftCurrentPa+5*r.Spatial.EndolymphChargeRatePa+2*r.Spatial.PerilymphChargeRatePa;
        Close(expected,r.Spatial.StoredEnergyRateFw);
        Close(r.Spatial.PerilymphChargeRatePa-r.Perilymph.CleftCurrentPa,r.Perilymph.HoldingCurrentPa);
        Assert.NotEqual(r.Perilymph.HoldingCurrentPa,r.Perilymph.CleftCurrentPa);
    }

    [Fact]
    public void BoundaryTopologyIsImmutableNamedAndFingerprintedWithItsReservoir()
    {
        var faces = new[] { Face() }; var original = new AvatarCleftOpenCoupling(Closed(),new("perilymph",5,140),faces);
        faces[0] = Face(area:99); Assert.Equal(2,original.Faces[0].AreaSquareMicrometers);
        Assert.NotEqual(original.Fingerprint,new AvatarCleftOpenCoupling(Closed(),new("perilymph",6,140),[Face()]).Fingerprint);
        Assert.NotEqual(original.Fingerprint,new AvatarCleftOpenCoupling(Closed(),new("other",5,140),[Face()]).Fingerprint);
        Assert.NotEqual(original.Fingerprint,new AvatarCleftOpenCoupling(Closed(),new("perilymph",5,140),[Face() with { CenterToBathDistanceMicrometers=8 }]).Fingerprint);
    }

    [Fact]
    public void MissingAmbiguousOrNonfiniteBoundaryInputFailsExplicitly()
    {
        Assert.Throws<ArgumentException>(() => new AvatarCleftOpenCoupling(Closed(),new("perilymph",5,140),[]));
        Assert.Throws<ArgumentException>(() => new AvatarCleftOpenCoupling(Closed(),new("perilymph",5,140),[Face(),Face()]));
        Assert.Throws<ArgumentException>(() => new AvatarCleftOpenCoupling(Closed(),new("perilymph",5,140),[Face(node:"missing")]));
        Assert.Throws<ArgumentException>(() => new AvatarCleftOpenCoupling(Closed(),new("hair",5,140),[Face()]));
        Assert.Throws<ArgumentException>(() => new AvatarCleftOpenCoupling(Closed(),new("perilymph",0,140),[Face()]));
        Assert.Throws<ArgumentException>(() => new AvatarCleftOpenCoupling(Closed(),new("perilymph",5,140),[Face() with { CenterToBathDistanceMicrometers=double.PositiveInfinity }]));
        Assert.Throws<ArgumentException>(() => Open().Evaluate(-70,5,double.NaN,0,0,[State()],[Quiet()]));
        Assert.Throws<ArgumentException>(() => Open().Evaluate(-70,5,0,0,0,[],[Quiet()]));
        Assert.Throws<ArgumentException>(() => Open().Evaluate(-70,5,0,0,0,[State(k:-1)],[Quiet()]));
    }
}
