using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class VestibularAfferentCableTests
{
    private static readonly AvatarAfferentGates Gates=new(.2,.3,.4,.5,.6,.7);
    private static AvatarAfferentCableNode Node(string id)=>new(id,1,1,AvatarAfferentMembraneParameters.MyelinatedReference);
    private static AvatarAfferentCable Pair(bool reverse=false)=>new([Node("a"),Node("b")],
        [new("edge",reverse?"b":"a",reverse?"a":"b",2,4,6)],[new("sealed","b",0)]);
    private static void Close(double a,double b)=>Assert.InRange(Math.Abs(a-b),0,1e-9+2e-12*Math.Abs(a));
    [Fact]
    public void PassiveAxialExchangeConservesChargeAndDissipatesEnergy()
    {
        var e=Pair().Evaluate(0,[new("a",0,Gates),new("b",10,Gates)],[new("sealed",0)]);
        Assert.Equal(-30,e.Edges[0].LeftToRightPa);Assert.Equal(300,e.AxialDissipationFw);
        Assert.Equal(30,e.Nodes[0].ChargeRatePa);Assert.Equal(-30,e.Nodes[1].ChargeRatePa);
        Assert.Equal(0,e.ChargeResidualPa);Close(0,e.PowerResidualFw);Close(-300,e.StoredEnergyRateFw);
        Close(30/(2*Math.PI*.00002),e.Nodes[0].PotentialMvPerMs);
    }
    [Fact]
    public void TerminalExchangeUsesThePotentialDifferenceAndWholeCurrent()
    {
        var k=new AvatarAfferentCable([Node("a")],[],[new("calyx","a",2)]);
        var e=k.Evaluate(0,[new("a",-70,Gates)],[new("calyx",-50)]);
        Assert.Equal(-40,e.Terminals[0].NodeToTerminalPa);Assert.Equal(40,e.Nodes[0].ChargeRatePa);
        Assert.Equal(800,e.AxialDissipationFw);Close(0,e.ChargeResidualPa);Close(0,e.PowerResidualFw);
    }
    [Fact]
    public void ReversingAnEdgeChangesItsSignButNotThePhysicalNodeRates()
    {
        AvatarAfferentCableState[] s=[new("a",-60,Gates),new("b",-70,Gates)];AvatarAfferentTerminalState[] t=[new("sealed",0)];
        var a=Pair().Evaluate(0,s,t);var b=Pair(true).Evaluate(0,s,t);
        Assert.Equal(a.Nodes,b.Nodes);Assert.Equal(a.AxialDissipationFw,b.AxialDissipationFw);Assert.Equal(a.Edges[0].LeftToRightPa,-b.Edges[0].LeftToRightPa);
    }
    [Fact]
    public void OutwardPotassiumCurrentRemovesIntracellularCharge()
    {
        var k=new AvatarAfferentCable([Node("a") with { Membrane=AvatarAfferentMembraneParameters.NodeReference }],[],[new("sealed","a",0)]);
        var r=k.Evaluate(0,[new("a",0,new(0,1,1,1,1,1))],[new("sealed",0)]).Nodes[0];
        Assert.True(r.MembraneOutwardPa>0&&r.PotentialMvPerMs<0);Assert.Equal(-r.MembraneOutwardPa,r.ChargeRatePa);
    }
    [Fact]
    public void SourceRegionSubdivisionPreservesLengthAreaAndCapacitance()
    {
        var original=AvatarAfferentCable.ReferenceChain(1);
        foreach(int subdivisions in new[] { 1,2,4 })
        {
            var k=AvatarAfferentCable.ReferenceChain(subdivisions);Assert.Equal(7*subdivisions,k.Nodes.Count);
            Close(425,k.Nodes.Sum(n=>n.LengthMicrometers));Close(2*Math.PI*1.5*425,k.Nodes.Sum(n=>2*Math.PI*n.RadiusMicrometers*n.LengthMicrometers));
            Close(original.Nodes.Sum(n=>n.LengthMicrometers*n.Membrane.SpecificCapacitancePfPerSquareMicrometer),k.Nodes.Sum(n=>n.LengthMicrometers*n.Membrane.SpecificCapacitancePfPerSquareMicrometer));
            Assert.Equal(0,k.Terminals.Single(t=>t.Id=="distal-sealed").ConductanceNs);
            Close(1000*Math.PI*1.5*1.5/(4.0/subdivisions),k.Terminals.Single(t=>t.Id=="calyx").ConductanceNs);
        }
    }
    [Fact]
    public void CommonElectricalReferenceShiftPreservesThePhysicalEvaluation()
    {
        var k=Pair();var a=k.Evaluate(0,[new("a",-60,Gates),new("b",-70,Gates)],[new("sealed",0)]);
        var b=k.Evaluate(32,[new("a",-28,Gates),new("b",-38,Gates)],[new("sealed",32)]);
        Assert.Equal(a.Nodes,b.Nodes);Assert.Equal(a.Edges,b.Edges);Assert.Equal(a.StoredEnergyAttojoules,b.StoredEnergyAttojoules);Assert.Equal(a.PowerResidualFw,b.PowerResidualFw);
    }
    [Fact]
    public void InputOrderDoesNotChangeIdentityOrLocalStateOwnership()
    {
        var a=Pair();var b=new AvatarAfferentCable(a.Nodes.Reverse(),a.Edges.Reverse(),a.Terminals.Reverse());Assert.Equal(a.Fingerprint,b.Fingerprint);
        AvatarAfferentCableState[] s=[new("b",-70,Gates),new("a",-60,Gates with { Kv1Activation=.9 })];AvatarAfferentTerminalState[] t=[new("sealed",0)];
        Assert.Equal(a.Evaluate(0,s,t).Nodes,b.Evaluate(0,s.Reverse().ToArray(),t).Nodes);
    }
    [Fact]
    public void MissingDuplicateUnknownAndNonfiniteInputsCannotBecomeSilence()
    {
        var k=Pair();AvatarAfferentCableState[] s=[new("a",-60,Gates),new("b",-70,Gates)];AvatarAfferentTerminalState[] t=[new("sealed",0)];
        Assert.ThrowsAny<ArgumentException>(()=>k.Evaluate(0,s,[]));Assert.ThrowsAny<ArgumentException>(()=>k.Evaluate(0,[s[0],s[0]],t));
        Assert.ThrowsAny<ArgumentException>(()=>k.Evaluate(0,[s[0],s[1] with { Id="unknown" }],t));
        Assert.ThrowsAny<ArgumentException>(()=>k.Evaluate(0,s,[new("sealed",double.NaN)]));
    }
    [Fact]
    public void DisconnectedOrInvalidGeometryCannotCreateAnUnownedNode()
    {
        Assert.ThrowsAny<ArgumentException>(()=>new AvatarAfferentCable([Node("a"),Node("b")],[],[new("sealed","a",0)]));
        Assert.ThrowsAny<ArgumentException>(()=>new AvatarAfferentCable([Node("a")],[],[new("sealed","unknown",0)]));
        Assert.ThrowsAny<ArgumentException>(()=>new AvatarAfferentCable([Node("a") with { RadiusMicrometers=0 }],[],[new("sealed","a",0)]));
        Assert.ThrowsAny<ArgumentException>(()=>new AvatarAfferentCable([Node("a")],[new("self","a","a",1,1,1)],[new("sealed","a",0)]));
    }
    [Fact]
    public void CallerArrayReplacementDoesNotMutateTheFrozenCable()
    {
        AvatarAfferentCableNode[] nodes=[Node("a")];AvatarAfferentCableTerminal[] terminals=[new("sealed","a",0)];
        var k=new AvatarAfferentCable(nodes,[],terminals);string fingerprint=k.Fingerprint;nodes[0]=Node("other");terminals[0]=new("other","other",10);
        Assert.Equal("a",k.Nodes[0].Id);Assert.Equal("sealed",k.Terminals[0].Id);Assert.Equal(fingerprint,k.Fingerprint);
    }
}
