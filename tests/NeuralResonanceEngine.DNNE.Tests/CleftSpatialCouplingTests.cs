using System.Text.Json;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class CleftSpatialCouplingTests
{
    private static AvatarCleftCompartment Node(string id, double volume = 3) => new(id,volume,1,1,1);
    private static AvatarCleftConnection Link(string id = "ab", string left = "a", string right = "b") => new(id,left,right,.02,.5,3);
    private static AvatarCleftCompartmentState State(string id, double cleft = 0, double calyx = -60, double k = 5, double na = 140) => new(id,cleft,calyx,k,na);
    private static AvatarCleftMembranePort Quiet(string id) => new(id,0,0,0,0,0,0,0,0);
    private static AvatarCleftSpatialCoupling Pair(AvatarCleftMaterial? material = null, bool connected = true) =>
        new("hair",1,0,material ?? AvatarCleftMaterial.DepositedReference,[Node("a"),Node("b",7)],connected ? [Link()] : []);
    private static void Close(double expected, double actual, double absolute = 1e-10, double relative = 1e-11) =>
        Assert.InRange(Math.Abs(expected-actual),0,absolute+relative*Math.Abs(expected));

    [Fact]
    public void SingleCompartmentReducesToTheLocalCoupledCapacitanceEquations()
    {
        var caps = AvatarCleftCapacitances.FromMembraneGeometry(AvatarHairCellParameters.TypeIReference,AvatarCalyxParameters.DepositedReference);
        var block = new AvatarCleftChargeBalance(caps);
        var network = new AvatarCleftSpatialCoupling("hair",caps.HairApicalPf,caps.HairPerilymphPf,AvatarCleftMaterial.DepositedReference,
            [new("a",3,caps.HairCleftPf,caps.CalyxInnerPf,caps.CalyxOuterPf)],[]);
        var ports = new AvatarCleftPortCurrents(7,3,11,-13,17,0,-23);
        var v = new AvatarCleftPotentials(-70,3,-60,5,2); var d = block.Solve(ports); var q = block.Charges(v); var dq = block.ChargeRates(d);
        var r = network.Evaluate(-70,5,2,7,3,[State("a",3)],[new("a",11,-13,17,-23,0,0,0,0)]);
        var s = Assert.Single(r.Compartments); Assert.Empty(r.Transfers);
        Close(d.HairMvPerMs,r.HairMvPerMs); Close(d.CleftMvPerMs,s.CleftMvPerMs); Close(d.CalyxMvPerMs,s.CalyxMvPerMs);
        Close(q.HairFc,r.HairChargeFc); Close(q.CleftFc,s.CleftChargeFc); Close(q.CalyxFc,s.CalyxChargeFc);
        Close(q.EndolymphFc,r.EndolymphChargeFc); Close(q.PerilymphFc,r.PerilymphChargeFc);
        Close(dq.HairPa,r.HairChargeRatePa); Close(dq.CleftPa,s.CleftChargeRatePa); Close(dq.CalyxPa,s.CalyxChargeRatePa);
        Close(block.StoredEnergyAttojoules(v),r.StoredEnergyAttojoules); Close(block.StoredEnergyRateFw(v,d),r.StoredEnergyRateFw);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(8)]
    [InlineData(32)]
    public void UniformSubdivisionPreservesResponseWithoutDuplicatingTheHairCell(int count)
    {
        var local = new AvatarCleftChargeBalance(new(3,.5,4,4,2));
        var expected = local.Solve(new(7,3,12,-8,4,0,0));
        var nodes = Enumerable.Range(0,count).Select(i => new AvatarCleftCompartment("p"+i,8.0/count,4.0/count,4.0/count,2.0/count)).ToArray();
        var links = Enumerable.Range(1,count-1).Select(i => new AvatarCleftConnection("e"+i,"p"+(i-1),"p"+i,.02,.5,3)).ToArray();
        var states = nodes.Select(n => State(n.Id,2,-60)).ToArray();
        var ports = nodes.Select(n => new AvatarCleftMembranePort(n.Id,12.0/count,-8.0/count,4.0/count,0,96.485/count,0,0,0)).ToArray();
        var result = new AvatarCleftSpatialCoupling("hair",3,.5,AvatarCleftMaterial.DepositedReference,nodes,links).Evaluate(-70,5,0,7,3,states,ports);
        Close(expected.HairMvPerMs,result.HairMvPerMs);
        foreach (var n in result.Compartments)
        {
            Close(expected.CleftMvPerMs,n.CleftMvPerMs); Close(expected.CalyxMvPerMs,n.CalyxMvPerMs);
            Close(.125,n.PotassiumMmPerMs);
        }
        Assert.All(result.Transfers,t => { Assert.Equal(0,t.Cleft.TotalCurrentPa); Assert.Equal(0,t.CalyxCurrentLeftToRightPa); });
        Close(local.StoredEnergyAttojoules(new(-70,2,-60,5,0)),result.StoredEnergyAttojoules);
    }

    [Fact]
    public void InternalSpatialTransferConservesAmountsAndTotalElectricalCharge()
    {
        var network = Pair();
        var result = network.Evaluate(-70,5,0,0,0,[State("a",-9,-60,12,110),State("b",4,-50,5,140)],[Quiet("a"),Quiet("b")]);
        var a = result.Compartments[0]; var b = result.Compartments[1];
        Close(0,3*a.PotassiumMmPerMs+7*b.PotassiumMmPerMs);
        Close(0,3*a.SodiumMmPerMs+7*b.SodiumMmPerMs);
        Close(0,result.HairChargeRatePa+a.CleftChargeRatePa+a.CalyxChargeRatePa+b.CleftChargeRatePa+b.CalyxChargeRatePa);
        Close(0,result.HairChargeFc+result.EndolymphChargeFc+result.PerilymphChargeFc+a.CleftChargeFc+a.CalyxChargeFc+b.CleftChargeFc+b.CalyxChargeFc);
        var transfer = Assert.Single(result.Transfers); Close(-30,transfer.CalyxCurrentLeftToRightPa);
        Assert.NotEqual(0,transfer.Cleft.TotalCurrentPa);
    }

    [Fact]
    public void CalyxAxialCurrentFlowsFromHigherToLowerPotential()
    {
        var material = new AvatarCleftMaterial(0,0,26,0);
        var r = Pair(material).Evaluate(0,0,0,0,0,[State("a",0,10),State("b",0,0)],[Quiet("a"),Quiet("b")]);
        Close(30,Assert.Single(r.Transfers).CalyxCurrentLeftToRightPa);
        Assert.Equal(0,r.Transfers[0].Cleft.TotalCurrentPa);
        Assert.True(r.Compartments[0].CalyxMvPerMs < 0); Assert.True(r.Compartments[1].CalyxMvPerMs > 0);
        Assert.All(r.Compartments,n => { Assert.Equal(0,n.PotassiumMmPerMs); Assert.Equal(0,n.SodiumMmPerMs); });
    }

    [Fact]
    public void NeutralMembraneTransportChangesNamedIonAmountsWithoutInventingCurrent()
    {
        var r = Pair(connected:false).Evaluate(-70,5,0,0,0,[State("a"),State("b")],
            [new("a",0,0,0,0,0,0,-96.485,0),new("b",0,0,0,0,0,192.97,0,0)]);
        Assert.Equal(0,r.HairMvPerMs);
        Assert.All(r.Compartments,n => { Assert.Equal(0,n.CleftMvPerMs); Assert.Equal(0,n.CalyxMvPerMs); });
        Close(-1,3*r.Compartments[0].PotassiumMmPerMs); Close(2,7*r.Compartments[1].SodiumMmPerMs);
    }

    [Fact]
    public void CompartmentsRemainElectricallyConnectedThroughTheirOneSharedHairCell()
    {
        var r = Pair(connected:false).Evaluate(0,0,0,0,0,[State("a",0,0),State("b",0,0)],
            [new("a",1,0,0,0,0,0,0,0),Quiet("b")]);
        Close(-.2,r.HairMvPerMs); Close(8.0/15,r.Compartments[0].CleftMvPerMs);
        Close(-2.0/15,r.Compartments[1].CleftMvPerMs); Close(-1.0/15,r.Compartments[1].CalyxMvPerMs);
    }

    [Fact]
    public void InputPermutationAndEdgeOrientationCannotChangePhysicalResultsOrIdentity()
    {
        var first = Pair();
        var second = new AvatarCleftSpatialCoupling("hair",1,0,AvatarCleftMaterial.DepositedReference,
            [Node("b",7),Node("a")],[Link(left:"b",right:"a")]);
        var states = new[] { State("a",-9,-60,12,110),State("b",4,-50,5,140) };
        var ports = new[] { Quiet("a"),Quiet("b") };
        Assert.Equal(first.Fingerprint,second.Fingerprint);
        var a = first.Evaluate(-70,5,0,0,0,states,ports);
        var b = second.Evaluate(-70,5,0,0,0,states.Reverse().ToArray(),ports.Reverse().ToArray());
        Assert.Equal(JsonSerializer.Serialize(a),JsonSerializer.Serialize(b));
    }

    [Fact]
    public void CommonVoltageShiftPreservesSpatialTransferChargeAndEnergy()
    {
        var network = Pair(); var states = new[] { State("a",-9,-60,12,110),State("b",4,-50,5,140) };
        var ports = new[] { Quiet("a"),Quiet("b") };
        var a = network.Evaluate(-70,5,0,0,0,states,ports);
        var b = network.Evaluate(-38,37,32,0,0,states.Select(s => s with { CleftMv=s.CleftMv+32,CalyxMv=s.CalyxMv+32 }).ToArray(),ports);
        Assert.Equal(JsonSerializer.Serialize(a),JsonSerializer.Serialize(b));
    }

    [Fact]
    public void UniformUnforcedStateHasNoManufacturedActivity()
    {
        var r = Pair().Evaluate(-70,5,0,0,0,[State("a"),State("b")],[Quiet("a"),Quiet("b")]);
        Assert.Equal(0,r.HairMvPerMs); Assert.Equal(0,r.StoredEnergyRateFw);
        Assert.All(r.Compartments,n => { Assert.Equal(0,n.CleftMvPerMs); Assert.Equal(0,n.CalyxMvPerMs); Assert.Equal(0,n.PotassiumMmPerMs); Assert.Equal(0,n.SodiumMmPerMs); });
    }

    [Fact]
    public void StoredElectricalEnergyRateMatchesSpatialAndExternalTerminalPower()
    {
        var r = Pair().Evaluate(-70,5,2,7,3,[State("a",-9,-60,12,110),State("b",4,-50,5,140)],
            [new("a",11,-13,17,-23,0,0,0,0),new("b",-2,3,-5,7,0,0,0,0)]);
        var edge = Assert.Single(r.Transfers);
        double rh = -(7+3+11-2);
        double sa = 11-13-edge.Cleft.TotalCurrentPa, sb = -2+3+edge.Cleft.TotalCurrentPa;
        double ca = -(-13+17-23)-edge.CalyxCurrentLeftToRightPa, cb = -(3-5+7)+edge.CalyxCurrentLeftToRightPa;
        double expected = -70*rh-9*sa+4*sb-60*ca-50*cb+5*r.EndolymphChargeRatePa+2*r.PerilymphChargeRatePa;
        Close(expected,r.StoredEnergyRateFw);
        Close(rh,r.HairChargeRatePa); Close(sa,r.Compartments[0].CleftChargeRatePa); Close(cb,r.Compartments[1].CalyxChargeRatePa);
    }

    [Fact]
    public void ConstructorCopiesItsTopologyAndFingerprintsPhysicalIdentity()
    {
        var nodes = new[] { Node("a"),Node("b",7) }; var links = new[] { Link() };
        var n = new AvatarCleftSpatialCoupling("hair",1,0,AvatarCleftMaterial.DepositedReference,nodes,links);
        nodes[0] = Node("changed"); links[0] = Link("changed");
        Assert.Equal("a",n.Compartments[0].Id); Assert.Equal("ab",n.Connections[0].Id);
        Assert.Equal(Pair().Fingerprint,n.Fingerprint);
        Assert.NotEqual(n.Fingerprint,new AvatarCleftSpatialCoupling("other-hair",1,0,AvatarCleftMaterial.DepositedReference,[Node("a"),Node("b",7)],[Link()]).Fingerprint);
        Assert.NotEqual(n.Fingerprint,new AvatarCleftSpatialCoupling("hair",1,0,AvatarCleftMaterial.DepositedReference,[Node("a"),Node("b",8)],[Link()]).Fingerprint);
    }

    [Fact]
    public void IncompleteOrAmbiguousIdentityAndNonfiniteStateFailExplicitly()
    {
        Assert.Throws<ArgumentException>(() => new AvatarCleftSpatialCoupling("hair",1,0,AvatarCleftMaterial.DepositedReference,[Node("a"),Node("a")],[]));
        Assert.Throws<ArgumentException>(() => new AvatarCleftSpatialCoupling("hair",1,0,AvatarCleftMaterial.DepositedReference,[Node("a"),Node("b")],[Link(),Link("ba","b","a")]));
        Assert.Throws<ArgumentException>(() => new AvatarCleftSpatialCoupling("hair",1,0,AvatarCleftMaterial.DepositedReference,[Node("a"),Node("b")],[Link(right:"unknown")]));
        Assert.Throws<ArgumentException>(() => Pair().Evaluate(0,0,0,0,0,[State("a")],[Quiet("a"),Quiet("b")]));
        Assert.Throws<ArgumentException>(() => Pair().Evaluate(0,0,0,0,0,[State("a"),State("a")],[Quiet("a"),Quiet("b")]));
        Assert.Throws<ArgumentException>(() => Pair().Evaluate(0,0,0,0,0,[State("a"),State("b")],[Quiet("a"),Quiet("a")]));
        Assert.Throws<ArgumentException>(() => Pair().Evaluate(0,0,0,0,0,[State("a",k:0),State("b")],[Quiet("a"),Quiet("b")]));
        Assert.Throws<ArgumentException>(() => Pair().Evaluate(0,0,0,0,0,[State("a",calyx:double.NaN),State("b")],[Quiet("a"),Quiet("b")]));
    }
}
