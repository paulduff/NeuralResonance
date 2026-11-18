using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class VestibularCleftAfferentTests
{
    private static readonly AvatarAfferentGates AxonGates=new(.2,.3,.4,.5,.6,.7);
    private static AvatarVestibularCleftMembranes Sensory(int count=1)
    {
        var hp=AvatarHairCellParameters.TypeIReference;var cp=AvatarCalyxParameters.DepositedReference;
        var caps=AvatarCleftCapacitances.FromMembraneGeometry(hp,cp);
        var nodes=Enumerable.Range(0,count).Select(i=>new AvatarCleftCompartment("p"+i,8.0/count,caps.HairCleftPf/count,caps.CalyxInnerPf/count,caps.CalyxOuterPf/count)).ToArray();
        var edges=Enumerable.Range(1,count-1).Select(i=>new AvatarCleftConnection("e"+i,"p"+(i-1),"p"+i,.02,.5,3)).ToArray();
        var spatial=new AvatarCleftSpatialCoupling("hair",caps.HairApicalPf,caps.HairPerilymphPf,AvatarCleftMaterial.DepositedReference,nodes,edges);
        return new(new(spatial,new("bath",5,140),[new("apex",nodes[^1].Id,.02,.25)]),hp,cp);
    }
    private static AvatarAfferentCable Cable(double conductance=2)=>new([new("n",1,1,AvatarAfferentMembraneParameters.NodeReference)],[],[new("calyx","n",conductance),new("sealed","n",0)]);
    private static AvatarCleftCompartmentState[] States(int count=1)=>Enumerable.Range(0,count).Select(i=>new AvatarCleftCompartmentState("p"+i,0,-60,5,140)).ToArray();
    private static AvatarCleftGateState[] Gates(int count=1)=>Enumerable.Range(0,count).Select(i=>new AvatarCleftGateState("p"+i,new(.2,.3,.4),new(.2,.3,.4,.5))).ToArray();
    private static void Close(double a,double b)=>Assert.InRange(Math.Abs(a-b),0,2e-9+2e-12*Math.Abs(a));
    [Fact]
    public void TheActualSameStateAxonCurrentLoadsItsOwningCalyx()
    {
        var sensory=Sensory();var k=new AvatarVestibularCleftAfferent(sensory,Cable(),[new("calyx","p0")]);
        var e=k.Evaluate(-70,0,0,States(),Gates(),[new("n",-70,AxonGates)]);var transfer=Assert.Single(e.Transfers);
        Assert.Equal(20,transfer.CalyxToCablePa);Assert.Equal(-20,transfer.CableToCalyxPa);Assert.Equal(20,e.Sensory.Membranes[0].Port.CalyxToAxonPa);
        var expected=sensory.Evaluate(-70,0,0,States(),[new("p0",Gates()[0].Hair,Gates()[0].Calyx,20)]);
        Assert.Equal(expected.Coupling.Spatial.HairMvPerMs,e.Sensory.Coupling.Spatial.HairMvPerMs);
        Assert.Equal(expected.Coupling.Spatial.Compartments,e.Sensory.Coupling.Spatial.Compartments);
        Close(0,e.InterfaceChargeResidualPa);Close(0,e.InterfacePowerResidualFw);
    }
    [Theory]
    [InlineData(-70,20)] [InlineData(-50,-20)]
    public void TheInterfaceCarriesElectricalFeedbackInBothDirections(double axonVoltage,double calyxOutgoing)
    {
        var k=new AvatarVestibularCleftAfferent(Sensory(),Cable(),[new("calyx","p0")]);
        var e=k.Evaluate(-70,0,0,States(),Gates(),[new("n",axonVoltage,AxonGates)]);
        Assert.Equal(calyxOutgoing,e.Transfers[0].CalyxToCablePa);Assert.Equal(-calyxOutgoing,e.Cable.Terminals.Single(t=>t.Id=="calyx").NodeToTerminalPa);
    }
    [Fact]
    public void ExplicitTerminalCutRestoresTheOriginalSealedSensoryDerivative()
    {
        var s=Sensory();var k=new AvatarVestibularCleftAfferent(s,Cable(0),[new("calyx","p0")]);
        var e=k.Evaluate(-70,0,0,States(),Gates(),[new("n",-50,AxonGates)]);
        var original=s.Evaluate(-70,0,0,States(),[new("p0",Gates()[0].Hair,Gates()[0].Calyx,0)]);
        Assert.Equal(0,e.Transfers[0].CalyxToCablePa);Assert.Equal(original.Membranes,e.Sensory.Membranes);
        Assert.Equal(original.Coupling.Spatial.Compartments,e.Sensory.Coupling.Spatial.Compartments);
        Assert.Equal(original.Coupling.Spatial.HairMvPerMs,e.Sensory.Coupling.Spatial.HairMvPerMs);
    }
    [Fact]
    public void ElectricalReferenceShiftDoesNotCreateAnInterfaceCurrent()
    {
        var k=new AvatarVestibularCleftAfferent(Sensory(),Cable(),[new("calyx","p0")]);
        var a=k.Evaluate(-70,0,0,States(),Gates(),[new("n",-70,AxonGates)]);
        var shifted=States().Select(s=>s with { CleftMv=s.CleftMv+32,CalyxMv=s.CalyxMv+32 }).ToArray();
        var b=k.Evaluate(-38,32,0,shifted,Gates(),[new("n",-38,AxonGates)]);
        Assert.Equal(a.Transfers,b.Transfers);Assert.Equal(a.Cable.Nodes,b.Cable.Nodes);Assert.Equal(a.Sensory.Membranes,b.Sensory.Membranes);
        Close(a.Sensory.Coupling.Spatial.HairMvPerMs,b.Sensory.Coupling.Spatial.HairMvPerMs);
    }
    [Fact]
    public void MultipleContactsKeepIndividualCalyxOwnership()
    {
        var cable=new AvatarAfferentCable(Cable().Nodes,[],[new("left","n",2),new("right","n",3)]);
        var k=new AvatarVestibularCleftAfferent(Sensory(2),cable,[new("right","p1"),new("left","p0")]);var states=States(2);states[1]=states[1] with { CalyxMv=-50 };
        var e=k.Evaluate(-70,0,0,states,Gates(2),[new("n",-70,AxonGates)]);
        Assert.Equal(20,e.Sensory.Membranes.Single(m=>m.Id=="p0").Port.CalyxToAxonPa);
        Assert.Equal(60,e.Sensory.Membranes.Single(m=>m.Id=="p1").Port.CalyxToAxonPa);
        Close(0,e.InterfaceChargeResidualPa);Close(0,e.InterfacePowerResidualFw);
    }
    [Fact]
    public void InvalidIdentityAndUnownedOpenTerminalAreRejected()
    {
        var sensory=Sensory();var cable=Cable();
        Assert.ThrowsAny<ArgumentException>(()=>new AvatarVestibularCleftAfferent(sensory,cable,[new("calyx","unknown")]));
        Assert.ThrowsAny<ArgumentException>(()=>new AvatarVestibularCleftAfferent(sensory,cable,[new("sealed","p0")]));
        Assert.ThrowsAny<ArgumentException>(()=>new AvatarVestibularCleftAfferent(sensory,cable,[new("calyx","p0"),new("calyx","p0")]));
        var k=new AvatarVestibularCleftAfferent(sensory,cable,[new("calyx","p0")]);
        Assert.ThrowsAny<ArgumentException>(()=>k.Evaluate(-70,0,0,States(),[],[new("n",-70,AxonGates)]));
    }
    [Fact]
    public void MismatchedChemicalReservoirIsNotSilentlyJoined()
    {
        var original=Cable();var p=original.Nodes[0].Membrane with { OutsidePotassiumMm=10 };
        var changed=new AvatarAfferentCable([original.Nodes[0] with { Membrane=p }],[],original.Terminals);
        Assert.ThrowsAny<ArgumentException>(()=>new AvatarVestibularCleftAfferent(Sensory(),changed,[new("calyx","p0")]));
    }
}
