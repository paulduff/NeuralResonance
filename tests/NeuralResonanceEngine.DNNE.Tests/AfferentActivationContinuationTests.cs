using NRE.SimAvatar;
using Xunit;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class AfferentActivationContinuationTests
{
    private const AvatarSodiumActivationContinuation Hold=AvatarSodiumActivationContinuation.EndpointHold;
    private const AvatarSodiumActivationContinuation Original=AvatarSodiumActivationContinuation.DepositedFixedValue;
    private static readonly AvatarAfferentGates Gates=new(.2,.4,.3,.6,.5,.7);

    [Fact]
    public void DefaultRemainsOriginalAndCandidateHasDistinctIdentity()
    {
        var p=AvatarAfferentMembraneParameters.NodeReference;
        var a=new AvatarAfferentMembraneKernel(p);var b=new AvatarAfferentMembraneKernel(p,Original);
        var c=new AvatarAfferentMembraneKernel(p,Hold);
        Assert.Equal(Original,a.ActivationContinuation);Assert.Equal(a.Fingerprint,b.Fingerprint);
        Assert.Equal(a.Evaluate(-70,Gates),b.Evaluate(-70,Gates));Assert.NotEqual(a.Fingerprint,c.Fingerprint);
    }
    [Fact]
    public void EverySourceKnotAndInteriorIntervalRemainsExactlyUnchanged()
    {
        foreach(var p in AvatarAfferentMembraneKernel.SodiumActivationTable)
        {
            Assert.Equal(AvatarAfferentMembraneKernel.Rates(p.VoltageMv),AvatarAfferentMembraneKernel.Rates(p.VoltageMv,Hold));
            if(p.VoltageMv<60)Assert.Equal(AvatarAfferentMembraneKernel.Rates(p.VoltageMv+2.5),AvatarAfferentMembraneKernel.Rates(p.VoltageMv+2.5,Hold));
        }
    }
    [Theory]
    [InlineData(-55)] [InlineData(60)]
    public void OneSidedBoundaryLimitsAreContinuous(double boundary)
    {
        var at=AvatarAfferentMembraneKernel.Rates(boundary,Hold).SodiumActivation.TauMs;
        foreach(double v in new[] { Math.BitDecrement(boundary),Math.BitIncrement(boundary) })
            Assert.InRange(Math.Abs(AvatarAfferentMembraneKernel.Rates(v,Hold).SodiumActivation.TauMs-at),0,1e-13);
        var outside=boundary<0?Math.BitDecrement(boundary):Math.BitIncrement(boundary);
        Assert.Equal(.2,AvatarAfferentMembraneKernel.Rates(outside).SodiumActivation.TauMs);
        Assert.NotEqual(.2,at);
    }
    [Theory]
    [InlineData(-140)] [InlineData(-80)] [InlineData(-55.001)]
    [InlineData(60.001)] [InlineData(70)] [InlineData(80)]
    public void OutsideContinuationChangesOnlyActivationTime(double v)
    {
        var a=AvatarAfferentMembraneKernel.Rates(v);var b=AvatarAfferentMembraneKernel.Rates(v,Hold);
        Assert.Equal(a,b with { SodiumActivation=a.SodiumActivation });
        Assert.Equal(a.SodiumActivation.Equilibrium,b.SodiumActivation.Equilibrium);
        Assert.Equal(v< -55?.26131:.21091,b.SodiumActivation.TauMs);
    }
    [Fact]
    public void CurrentAndOtherGateDerivativesUseIdenticalActualState()
    {
        var p=AvatarAfferentMembraneParameters.NodeReference;
        var a=new AvatarAfferentMembraneKernel(p).Evaluate(-80,Gates);
        var b=new AvatarAfferentMembraneKernel(p,Hold).Evaluate(-80,Gates);
        Assert.Equal(a.Currents,b.Currents);Assert.NotEqual(a.GateDerivatives.SodiumActivationPerMs,b.GateDerivatives.SodiumActivationPerMs);
        Assert.Equal(a.GateDerivatives,b.GateDerivatives with { SodiumActivationPerMs=a.GateDerivatives.SodiumActivationPerMs });
    }
    [Fact]
    public void VoltageClampComposesUnderExplicitCandidateKinetics()
    {
        var a=AvatarAfferentMembraneKernel.RelaxVoltageClamp(Gates,-80,.03,Hold);
        var b=AvatarAfferentMembraneKernel.RelaxVoltageClamp(AvatarAfferentMembraneKernel.RelaxVoltageClamp(Gates,-80,.01,Hold),-80,.02,Hold);
        Assert.InRange(Math.Abs(a.SodiumActivation-b.SodiumActivation),0,1e-15);
        Assert.NotEqual(a.SodiumActivation,AvatarAfferentMembraneKernel.RelaxVoltageClamp(Gates,-80,.03).SodiumActivation);
        Assert.Equal(Gates,AvatarAfferentMembraneKernel.RelaxVoltageClamp(Gates,-80,0,Hold));
    }
    [Fact]
    public void CablePropagatesPolicyAndChangesFingerprintWithoutChangingGeometry()
    {
        var a=AvatarAfferentCable.ReferenceChain(1);var b=AvatarAfferentCable.ReferenceChain(1,Original);
        var c=AvatarAfferentCable.ReferenceChain(1,Hold);
        Assert.Equal(a.Fingerprint,b.Fingerprint);Assert.NotEqual(a.Fingerprint,c.Fingerprint);
        Assert.Equal(Hold,c.ActivationContinuation);Assert.Equal(a.Nodes,c.Nodes);Assert.Equal(a.Edges,c.Edges);Assert.Equal(a.Terminals,c.Terminals);
    }
    [Fact]
    public void UnknownPolicyFailsAtEachPublicBoundary()
    {
        var invalid=(AvatarSodiumActivationContinuation)100;
        Assert.Throws<ArgumentOutOfRangeException>(()=>AvatarAfferentMembraneKernel.Rates(-70,invalid));
        Assert.Throws<ArgumentOutOfRangeException>(()=>new AvatarAfferentMembraneKernel(AvatarAfferentMembraneParameters.NodeReference,invalid));
        Assert.Throws<ArgumentOutOfRangeException>(()=>AvatarAfferentMembraneKernel.RelaxVoltageClamp(Gates,-70,0,invalid));
        Assert.Throws<ArgumentOutOfRangeException>(()=>AvatarAfferentCable.ReferenceChain(1,invalid));
    }
    [Fact]
    public void EquilibriumGatesRemainStationaryForBothPolicies()
    {
        foreach(double v in new[] { -80d,-55,0,60,80 })
        {
            var gates=AvatarAfferentMembraneKernel.VoltageConditionedGates(v);
            foreach(var policy in new[] { Original,Hold })
                Assert.Equal(default,new AvatarAfferentMembraneKernel(AvatarAfferentMembraneParameters.NodeReference,policy).Evaluate(v,gates).GateDerivatives);
        }
    }
}
