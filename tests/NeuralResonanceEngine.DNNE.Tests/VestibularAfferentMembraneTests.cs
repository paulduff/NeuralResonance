using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class VestibularAfferentMembraneTests
{
    private static readonly AvatarAfferentGates Mixed=new(.2,.3,.4,.5,.6,.7);
    private static void Close(double a,double b,double absolute=1e-11)=>Assert.InRange(Math.Abs(a-b),0,absolute+Math.Abs(a)*2e-12);
    [Fact]
    public void NamedSourceTablesRetainKnotsAndExplicitOutsideBehavior()
    {
        foreach(var x in AvatarAfferentMembraneKernel.SodiumActivationTable)Close(x.TauMs,AvatarAfferentMembraneKernel.Rates(x.VoltageMv).SodiumActivation.TauMs,1e-15);
        foreach(var x in AvatarAfferentMembraneKernel.SodiumInactivationTable)Close(x.TauMs,AvatarAfferentMembraneKernel.Rates(x.VoltageMv).SodiumInactivation.TauMs,1e-15);
        Assert.Equal(.2,AvatarAfferentMembraneKernel.Rates(-55.001).SodiumActivation.TauMs);
        Assert.Equal(.2,AvatarAfferentMembraneKernel.Rates(60.001).SodiumActivation.TauMs);
        Close((.26131+.4427675)/2,AvatarAfferentMembraneKernel.Rates(-52.5).SodiumActivation.TauMs);
        Close(3.288872222+2*(3.288872222-1.198086111),AvatarAfferentMembraneKernel.Rates(-55).SodiumInactivation.TauMs);
    }
    [Theory]
    [InlineData(-140)] [InlineData(-100)] [InlineData(-80)] [InlineData(-55)]
    [InlineData(-45)] [InlineData(0)] [InlineData(60)] [InlineData(80)]
    public void AllSixGatesHaveFiniteProbabilityAndPositiveTimeConstant(double v)
    {
        var r=AvatarAfferentMembraneKernel.Rates(v);
        foreach(var x in new[] { r.SodiumActivation,r.SodiumInactivation,r.Kv1Activation,r.Kv7Activation,r.Kv34Activation,r.Kv34Inactivation })
        { Assert.True(double.IsFinite(x.TauMs)&&x.TauMs>0);Assert.InRange(x.Equilibrium,0,1); }
    }
    [Fact]
    public void SodiumUsesActualCubedActivationAndIndependentInactivation()
    {
        var k=new AvatarAfferentMembraneKernel(AvatarAfferentMembraneParameters.NodeReference);var a=k.Evaluate(-50,Mixed);
        var b=k.Evaluate(-50,Mixed with { SodiumActivation=.4 });Close(8*a.Currents.SodiumPaPerSquareMicrometer,b.Currents.SodiumPaPerSquareMicrometer);
        Assert.Equal(a.Rates,b.Rates);Assert.NotEqual(a.GateDerivatives,b.GateDerivatives);
        Assert.Equal(0,k.Evaluate(-50,Mixed with { SodiumInactivation=0 }).Currents.SodiumPaPerSquareMicrometer);
        Assert.Equal(a.Currents.PotassiumPaPerSquareMicrometer,b.Currents.PotassiumPaPerSquareMicrometer);
    }
    [Fact]
    public void EachIonVanishesAtItsOwnReversalAndRespondsToItsReservoir()
    {
        var p=AvatarAfferentMembraneParameters.NodeReference;var k=new AvatarAfferentMembraneKernel(p);var e=k.Evaluate(-60,Mixed);
        Assert.Equal(0,k.Evaluate(e.SodiumReversalMv,Mixed).Currents.SodiumPaPerSquareMicrometer);
        Assert.Equal(0,k.Evaluate(e.PotassiumReversalMv,Mixed).Currents.PotassiumPaPerSquareMicrometer);
        var raised=new AvatarAfferentMembraneKernel(p with { OutsidePotassiumMm=10 }).Evaluate(-60,Mixed);
        Close(26*Math.Log(2),raised.PotassiumReversalMv-e.PotassiumReversalMv);Assert.Equal(e.SodiumReversalMv,raised.SodiumReversalMv);
    }
    [Fact]
    public void RegionsRetainDistinctConductanceDensityAndMyelinCapacitance()
    {
        var n=new AvatarAfferentMembraneKernel(AvatarAfferentMembraneParameters.NodeReference).Evaluate(-50,Mixed);
        var u=new AvatarAfferentMembraneKernel(AvatarAfferentMembraneParameters.UnmyelinatedReference).Evaluate(-50,Mixed);
        var m=new AvatarAfferentMembraneKernel(AvatarAfferentMembraneParameters.MyelinatedReference).Evaluate(-50,Mixed);
        Close(n.Currents.SodiumPaPerSquareMicrometer/4,u.Currents.SodiumPaPerSquareMicrometer);Assert.Equal(0,u.Currents.PotassiumPaPerSquareMicrometer);
        Assert.Equal(0,m.Currents.TotalOutwardPaPerSquareMicrometer);Assert.Equal(.00002,AvatarAfferentMembraneParameters.MyelinatedReference.SpecificCapacitancePfPerSquareMicrometer);
        Assert.Equal(54,n.Rates.Kv7Activation.TauMs);Assert.Equal(3.7,n.Rates.Kv1Activation.TauMs);Assert.Equal(25.4,n.Rates.Kv34Inactivation.TauMs);
    }
    [Fact]
    public void VoltageConditionedStateHasZeroGateRatesWithoutForcingZeroCurrent()
    {
        var k=new AvatarAfferentMembraneKernel(AvatarAfferentMembraneParameters.NodeReference);
        var e=k.Evaluate(-65,AvatarAfferentMembraneKernel.VoltageConditionedGates(-65));Assert.Equal(default,e.GateDerivatives);
        Assert.NotEqual(0,e.Currents.TotalOutwardPaPerSquareMicrometer);
    }
    [Fact]
    public void AnalyticHeldVoltageRelaxationComposesWithoutResettingActualGates()
    {
        var a=AvatarAfferentMembraneKernel.RelaxVoltageClamp(Mixed,-65,3);
        var b=AvatarAfferentMembraneKernel.RelaxVoltageClamp(AvatarAfferentMembraneKernel.RelaxVoltageClamp(Mixed,-65,1),-65,2);
        Close(a.SodiumActivation,b.SodiumActivation);Close(a.SodiumInactivation,b.SodiumInactivation);Close(a.Kv1Activation,b.Kv1Activation);
        Close(a.Kv7Activation,b.Kv7Activation);Close(a.Kv34Activation,b.Kv34Activation);Close(a.Kv34Inactivation,b.Kv34Inactivation);
        Assert.Equal(Mixed,AvatarAfferentMembraneKernel.RelaxVoltageClamp(Mixed,-65,0));Assert.NotEqual(Mixed,a);
    }
    [Fact]
    public void InvalidStateParametersAndNonpositiveExtrapolatedTauAreRejected()
    {
        var p=AvatarAfferentMembraneParameters.NodeReference;var k=new AvatarAfferentMembraneKernel(p);
        Assert.ThrowsAny<ArgumentException>(()=>k.Evaluate(double.NaN,Mixed));
        Assert.ThrowsAny<ArgumentException>(()=>k.Evaluate(-60,Mixed with { SodiumActivation=1.01 }));
        Assert.ThrowsAny<ArgumentException>(()=>new AvatarAfferentMembraneKernel(p with { InsideSodiumMm=0 }));
        Assert.ThrowsAny<ArgumentException>(()=>AvatarAfferentMembraneKernel.Rates(1000));
        Assert.ThrowsAny<ArgumentException>(()=>AvatarAfferentMembraneKernel.RelaxVoltageClamp(Mixed,-65,-1));
    }
    [Fact]
    public void ChannelAblationChangesOnlyItsCurrentAndModelIdentity()
    {
        var p=AvatarAfferentMembraneParameters.NodeReference;var a=new AvatarAfferentMembraneKernel(p);var b=new AvatarAfferentMembraneKernel(p with { SodiumMaximumNsPerSquareMicrometer=0 });
        var x=a.Evaluate(-60,Mixed);var y=b.Evaluate(-60,Mixed);Assert.NotEqual(a.Fingerprint,b.Fingerprint);
        Assert.Equal(x.GateDerivatives,y.GateDerivatives);Assert.Equal(x.Currents.PotassiumPaPerSquareMicrometer,y.Currents.PotassiumPaPerSquareMicrometer);
        Assert.Equal(0,y.Currents.SodiumPaPerSquareMicrometer);
    }
}
