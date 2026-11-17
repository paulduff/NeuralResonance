using System.Text.Json;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class VestibularCleftMembraneTests
{
    private static readonly AvatarHairMembraneGates HairGates = new(.2,.3,.4);
    private static readonly AvatarCalyxGates CalyxGates = new(.2,.3,.4,.5);
    private static AvatarVestibularCleftMembranes Kernel(int count = 1, AvatarHairCellParameters? hair = null, AvatarCalyxParameters? calyx = null)
    {
        hair ??= AvatarHairCellParameters.TypeIReference; calyx ??= AvatarCalyxParameters.DepositedReference;
        var caps = AvatarCleftCapacitances.FromMembraneGeometry(hair,calyx);
        var nodes = Enumerable.Range(0,count).Select(i => new AvatarCleftCompartment("p"+i,8.0/count,
            caps.HairCleftPf/count,caps.CalyxInnerPf/count,caps.CalyxOuterPf/count)).ToArray();
        var links = Enumerable.Range(1,count-1).Select(i => new AvatarCleftConnection("e"+i,"p"+(i-1),"p"+i,.02,.5,3)).ToArray();
        var spatial = new AvatarCleftSpatialCoupling("hair",caps.HairApicalPf,caps.HairPerilymphPf,AvatarCleftMaterial.DepositedReference,nodes,links);
        return new(new(spatial,new("perilymph",5,140),[new("apex",nodes[^1].Id,.02,.25)]),hair,calyx);
    }
    private static AvatarCleftCompartmentState[] States(int count = 1) => Enumerable.Range(0,count).Select(i => new AvatarCleftCompartmentState("p"+i,0,-60,5,140)).ToArray();
    private static AvatarCleftLocalMembraneInput[] Inputs(int count = 1) => Enumerable.Range(0,count).Select(i => new AvatarCleftLocalMembraneInput("p"+i,HairGates,CalyxGates,0)).ToArray();
    private static void Close(double expected, double actual) => Assert.InRange(Math.Abs(expected-actual),0,2e-10+2e-12*Math.Abs(expected));

    [Fact]
    public void UniformSinglePatchUsesTheQualifiedMembraneCurrentsAndOneApicalPort()
    {
        var k = Kernel(); var r = k.Evaluate(-70,0,0,States(),Inputs()); var face = Assert.Single(r.Membranes);
        var hair = new AvatarHairCellKernel(AvatarHairCellParameters.TypeIReference,AvatarHairCellBath.Reference).Currents(-70,.2,.3,.4,0);
        var calyx = new AvatarCalyxKernel(AvatarCalyxParameters.DepositedReference).Evaluate(-60,0,5,140,CalyxGates);
        Close(hair.MetPa,r.MetCurrentPa); Assert.Equal(0,face.HairCurrents.MetPa);
        Close(hair.PotassiumLowPa,face.HairCurrents.PotassiumLowPa); Close(hair.CalciumPa,face.HairCurrents.CalciumPa);
        Close(hair.HcnPotassiumPa,face.HairCurrents.HcnPotassiumPa); Close(hair.HcnSodiumPa,face.HairCurrents.HcnSodiumPa);
        Close(hair.PumpPotassiumPa,face.HairCurrents.PumpPotassiumPa); Close(hair.PumpSodiumPa,face.HairCurrents.PumpSodiumPa);
        Assert.Equal(calyx,face.Calyx);
        Close(-hair.TotalOutwardPa,r.Coupling.Spatial.HairChargeRatePa);
        Close(hair.TotalOutwardPa-hair.MetPa+calyx.Inner.TotalOutwardPa,r.Coupling.Spatial.Compartments[0].CleftChargeRatePa);
        Close(-calyx.TotalOutwardPa,r.Coupling.Spatial.Compartments[0].CalyxChargeRatePa);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(8)]
    public void UniformSubdivisionPreservesWholeCellChannelsCapacitanceAndMetCurrent(int count)
    {
        var a = Kernel().Evaluate(-70,0,0,States(),Inputs());
        var b = Kernel(count).Evaluate(-70,0,0,States(count),Inputs(count));
        Close(a.MetCurrentPa,b.MetCurrentPa);
        Close(a.Membranes[0].HairCurrents.TotalOutwardPa,b.Membranes.Sum(m => m.HairCurrents.TotalOutwardPa));
        Close(a.Membranes[0].Calyx.Inner.TotalOutwardPa,b.Membranes.Sum(m => m.Calyx.Inner.TotalOutwardPa));
        Close(a.Membranes[0].Calyx.Outer.TotalOutwardPa,b.Membranes.Sum(m => m.Calyx.Outer.TotalOutwardPa));
        Close(a.Coupling.Spatial.HairMvPerMs,b.Coupling.Spatial.HairMvPerMs);
        foreach (var node in b.Coupling.Spatial.Compartments)
        {
            Close(a.Coupling.Spatial.Compartments[0].CleftMvPerMs,node.CleftMvPerMs);
            Close(a.Coupling.Spatial.Compartments[0].CalyxMvPerMs,node.CalyxMvPerMs);
            Close(a.Coupling.Spatial.Compartments[0].PotassiumMmPerMs,node.PotassiumMmPerMs);
        }
    }

    [Fact]
    public void LocalVoltagesAndActualGateStatesRemainSeparateBetweenPatches()
    {
        var k = Kernel(2); var states = States(2); states[1] = states[1] with { CleftMv=9 };
        var inputs = Inputs(2); var a = k.Evaluate(-70,0,0,states,inputs);
        Assert.NotEqual(a.Membranes[0].HairRates,a.Membranes[1].HairRates);
        Assert.NotEqual(a.Membranes[0].Calyx.Rates.Inner,a.Membranes[1].Calyx.Rates.Inner);
        Assert.Equal(a.Membranes[0].Calyx.Rates.Outer,a.Membranes[1].Calyx.Rates.Outer);
        inputs[0] = inputs[0] with { HairGates = HairGates with { PotassiumLow=.8 } };
        var b = k.Evaluate(-70,0,0,states,inputs);
        Assert.NotEqual(a.Membranes[0].HairCurrents,b.Membranes[0].HairCurrents);
        Assert.Equal(a.Membranes[1],b.Membranes[1]);
        Assert.NotEqual(0,b.Membranes[0].HairGateDerivatives.PotassiumLowPerMs);
    }

    [Fact]
    public void CleftIonsAffectTheirHairAndInnerCalyxFacesAtTheCurrentStage()
    {
        var k = Kernel(2); var states = States(2); var inputs = Inputs(2);
        var a = k.Evaluate(-70,0,0,states,inputs);
        states[1] = states[1] with { PotassiumMm=10,SodiumMm=100 };
        var b = k.Evaluate(-70,0,0,states,inputs);
        Assert.Equal(a.Membranes[0],b.Membranes[0]);
        Assert.NotEqual(a.Membranes[1].HairCurrents,b.Membranes[1].HairCurrents);
        Assert.NotEqual(a.Membranes[1].Calyx.Inner,b.Membranes[1].Calyx.Inner);
        Assert.Equal(a.Membranes[1].Calyx.Outer,b.Membranes[1].Calyx.Outer);
        Close(26*Math.Log(2),b.Membranes[1].Calyx.InnerPotassiumReversalMv-a.Membranes[1].Calyx.InnerPotassiumReversalMv);
        Close(a.MetCurrentPa,b.MetCurrentPa);
    }

    [Fact]
    public void BundleDisplacementChangesMetWithoutResettingLocalMembraneGates()
    {
        var k = Kernel(2); var states = States(2); var inputs = Inputs(2);
        var a = k.Evaluate(-70,0,-300,states,inputs); var b = k.Evaluate(-70,0,300,states,inputs);
        Assert.True(b.MetCurrentPa < a.MetCurrentPa);
        Assert.Equal(JsonSerializer.Serialize(a.Membranes),JsonSerializer.Serialize(b.Membranes));
        Assert.NotEqual(a.Coupling.Spatial.HairMvPerMs,b.Coupling.Spatial.HairMvPerMs);
        Assert.All(inputs,p => Assert.Equal(HairGates,p.HairGates));
    }

    [Fact]
    public void UncoveredHairAreaAddsCapacitanceWithoutAddingActiveChannels()
    {
        var first = Kernel(); var second = Kernel(hair:AvatarHairCellParameters.TypeIReference with { BasolateralAreaSquareMicrometers=400 });
        var a = first.Evaluate(-70,0,0,States(),Inputs()); var b = second.Evaluate(-70,0,0,States(),Inputs());
        Assert.Equal(a.Membranes[0],b.Membranes[0]); Assert.Equal(a.MetCurrentPa,b.MetCurrentPa);
        Assert.True(Math.Abs(b.Coupling.Spatial.HairMvPerMs) < Math.Abs(a.Coupling.Spatial.HairMvPerMs));
        Assert.NotEqual(first.Fingerprint,second.Fingerprint);
    }

    [Fact]
    public void CommonPotentialShiftAndInputPermutationPreserveMembranePhysics()
    {
        var k = Kernel(2); var states = States(2); var inputs = Inputs(2);
        var a = k.Evaluate(-70,0,0,states,inputs);
        var b = k.Evaluate(-38,32,0,states.Reverse().Select(s => s with { CleftMv=s.CleftMv+32,CalyxMv=s.CalyxMv+32 }).ToArray(),inputs.Reverse().ToArray());
        Close(a.EndolymphPotentialMv+32,b.EndolymphPotentialMv); Assert.Equal(a.MetCurrentPa,b.MetCurrentPa);
        Assert.Equal(JsonSerializer.Serialize(a.Membranes),JsonSerializer.Serialize(b.Membranes));
        Assert.Equal(JsonSerializer.Serialize(a.Coupling),JsonSerializer.Serialize(b.Coupling));
    }

    [Fact]
    public void NeutralKccTransportAndCalciumCurrentStayInTheirSeparateLedgers()
    {
        var a = Kernel().Evaluate(-70,0,0,States(),Inputs());
        var b = Kernel(calyx:AvatarCalyxParameters.DepositedReference with { InnerKccMaximumPaPerSquareMicrometer=0 }).Evaluate(-70,0,0,States(),Inputs());
        Assert.Equal(a.Membranes[0].Port.CalyxToCleftPa,b.Membranes[0].Port.CalyxToCleftPa);
        Assert.NotEqual(a.Membranes[0].Port.CalyxPotassiumPa,b.Membranes[0].Port.CalyxPotassiumPa);
        Assert.NotEqual(a.Coupling.Spatial.Compartments[0].PotassiumMmPerMs,b.Coupling.Spatial.Compartments[0].PotassiumMmPerMs);
        Close(a.Coupling.Spatial.HairMvPerMs,b.Coupling.Spatial.HairMvPerMs);
        var hair = a.Membranes[0].HairCurrents; var port = a.Membranes[0].Port;
        Close(hair.CalciumPa,port.HairToCleftPa-port.HairPotassiumPa-port.HairSodiumPa);
        Assert.NotEqual(0,hair.CalciumPa);
    }

    [Fact]
    public void ExplicitAxonPortChangesCalyxChargeWithoutInventingMembraneCurrents()
    {
        var k = Kernel(); var inputs = Inputs(); var a = k.Evaluate(-70,0,0,States(),inputs);
        inputs[0] = inputs[0] with { AxonOutgoingPa=10 }; var b = k.Evaluate(-70,0,0,States(),inputs);
        Assert.Equal(a.Membranes[0].Calyx,b.Membranes[0].Calyx); Assert.Equal(a.Membranes[0].HairCurrents,b.Membranes[0].HairCurrents);
        Close(a.Coupling.Spatial.Compartments[0].CalyxChargeRatePa-10,b.Coupling.Spatial.Compartments[0].CalyxChargeRatePa);
        Close(10,b.Coupling.EndolymphHoldingCurrentPa+b.Coupling.Perilymph.HoldingCurrentPa);
    }

    [Fact]
    public void IncompleteIdentityAndInvalidGatesAreRejectedWithoutDefaults()
    {
        var k = Kernel(2); var states = States(2); var inputs = Inputs(2);
        Assert.Throws<ArgumentException>(() => k.Evaluate(-70,0,0,states,[inputs[0]]));
        Assert.Throws<ArgumentException>(() => k.Evaluate(-70,0,0,states,[inputs[0],inputs[0]]));
        Assert.Throws<ArgumentException>(() => k.Evaluate(-70,0,0,states,[inputs[0],inputs[1] with { Id="unknown" }]));
        Assert.Throws<ArgumentException>(() => k.Evaluate(-70,0,0,states,[inputs[0],inputs[1] with { HairGates=new(-.1,.3,.4) }]));
        Assert.Throws<ArgumentException>(() => k.Evaluate(-70,0,0,states,[inputs[0],inputs[1] with { AxonOutgoingPa=double.NaN }]));
        Assert.Throws<ArgumentException>(() => k.Evaluate(-70,0,0,[states[0],states[1] with { PotassiumMm=0 }],inputs));
    }

    [Fact]
    public void InconsistentGeometryOrAnUnsupportedOuterChemicalBathFailsConstruction()
    {
        var k = Kernel(); var hair = AvatarHairCellParameters.TypeIReference; var calyx = AvatarCalyxParameters.DepositedReference;
        Assert.Throws<ArgumentException>(() => new AvatarVestibularCleftMembranes(k.Coupling,hair with { ApicalCapacitancePf=6 },calyx));
        Assert.Throws<ArgumentException>(() => new AvatarVestibularCleftMembranes(k.Coupling,hair with { CleftFacingAreaSquareMicrometers=150 },calyx));
        Assert.Throws<ArgumentException>(() => new AvatarVestibularCleftMembranes(k.Coupling,hair,calyx with { OuterAreaSquareMicrometers=500 }));
        var changed = new AvatarCleftOpenCoupling(k.Coupling.Spatial,new("perilymph",6,140),k.Coupling.Faces);
        Assert.Throws<ArgumentException>(() => new AvatarVestibularCleftMembranes(changed,hair,calyx));
    }
}
