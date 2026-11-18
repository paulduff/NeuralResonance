using System.Text.Json;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class CleftStationarySearchTests
{
    private static readonly double HairRoot=26*(Math.Log(5)-Math.Log(150));
    // Analytic numerical control: hair KL only and outer calyx leak only.
    // At a sealed terminal, H=E_K, S=C=0, K=5, Na=140 is a known root.
    // Channel removals are fixtures, never proposed live biological defaults.
    private static AvatarVestibularCleftMembranes Kernel(int count=1,bool passive=false)
    {
        var h=AvatarHairCellParameters.TypeIReference with { MetMaximumNs=0,CalciumMaximumNs=0,Hcn1MaximumNs=0,
            PumpDensityPerSquareMicrometer=0,PotassiumLowMaximumNs=passive ? 0 : 80 };
        var c=AvatarCalyxParameters.DepositedReference with { Kv74MaximumNs=0,Hcn2MaximumNs=0,
            InnerPumpDensityPerSquareMicrometer=0,OuterPumpDensityPerSquareMicrometer=0,
            InnerKccMaximumPaPerSquareMicrometer=0,OuterLeakNs=passive ? 0 : 2 };
        var caps=AvatarCleftCapacitances.FromMembraneGeometry(h,c);
        var nodes=Enumerable.Range(0,count).Select(i=>new AvatarCleftCompartment("p"+i,8.0/count,
            caps.HairCleftPf/count,caps.CalyxInnerPf/count,caps.CalyxOuterPf/count)).ToArray();
        var edges=Enumerable.Range(1,count-1).Select(i=>new AvatarCleftConnection("e"+i,"p"+(i-1),"p"+i,.02,.5,3)).ToArray();
        var spatial=new AvatarCleftSpatialCoupling("hair",caps.HairApicalPf,caps.HairPerilymphPf,AvatarCleftMaterial.DepositedReference,nodes,edges);
        return new(new(spatial,new("bath",5,140),[new("apex",nodes[^1].Id,.02,.25)]),h,c);
    }
    private static AvatarCleftCompartmentState[] States(int count=1,double calyx=0,double gauge=0)=>
        Enumerable.Range(0,count).Select(i=>new AvatarCleftCompartmentState("p"+i,gauge,calyx+gauge,5,140)).ToArray();
    private static AvatarCleftAxonCurrent[] Axon(int count=1,double total=0)=>
        Enumerable.Range(0,count).Select(i=>new AvatarCleftAxonCurrent("p"+i,total/count)).ToArray();
    private static AvatarCleftStationaryPoint Final(AvatarCleftStationaryResult result)
    {
        Assert.True(result.StationaryCriteriaPassed,result.Outcome+" "+result.LastTrialError);
        Assert.Equal("StationaryResidualCriteriaMet",result.Outcome);
        Assert.False(result.LocalStabilityAssessed);Assert.False(result.PhysiologicalRestQualified);Assert.False(result.LiveCellModified);
        var point=result.AcceptedIterations[^1].Point;Assert.InRange(point.ScaledResidual,0,1);return point;
    }

    [Fact]
    public void KnownAnalyticRootIsRecognizedWithoutTakingANewtonStep()
    {
        var result=new AvatarCleftStationarySearch(Kernel(),new()).Find(HairRoot,0,States(),0,Axon());
        var p=Final(result);Assert.Single(result.AcceptedIterations);Assert.Equal(1,result.Evaluations);
        Assert.InRange(Math.Abs(p.HairMv-HairRoot),0,1e-12);Assert.InRange(p.MaximumGateRatePerMs,0,1e-12);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void PerturbedKnownControlConvergesToVoltageAndChemicalBalance(int direction)
    {
        var initial=new AvatarCleftCompartmentState[] { new("p0",direction*.01,direction*.05,5*(1+direction*.001),140*(1-direction*.001)) };
        var result=new AvatarCleftStationarySearch(Kernel(),new()).Find(HairRoot+direction,0,initial,0,Axon());var p=Final(result);
        Assert.True(result.AcceptedIterations.Length>1);
        Assert.InRange(Math.Abs(p.HairMv-HairRoot),0,1e-6);var s=Assert.Single(p.Compartments);
        Assert.InRange(Math.Abs(s.CleftMv),0,1e-7);Assert.InRange(Math.Abs(s.CalyxMv),0,1e-7);
        Assert.InRange(Math.Abs(s.PotassiumMm-5),0,1e-7);Assert.InRange(Math.Abs(s.SodiumMm-140),0,1e-7);
        for (int i=1;i<result.AcceptedIterations.Length;i++)
            Assert.True(result.AcceptedIterations[i].Point.ScaledResidual<result.AcceptedIterations[i-1].Point.ScaledResidual);
    }

    [Theory]
    [InlineData(-10)]
    [InlineData(10)]
    public void StationaryTerminalFlowIsBalancedByOuterCurrentWithoutZeroingEveryCurrent(double current)
    {
        var k=Kernel(2);double calyx=-current/k.CalyxKernel.Parameters.OuterLeakNs;
        var result=new AvatarCleftStationarySearch(k,new()).Find(HairRoot,0,States(2,calyx),0,Axon(2,current));var p=Final(result);
        Assert.All(p.Compartments,s=>Assert.InRange(Math.Abs(s.CalyxMv-calyx),0,1e-12));
        Assert.InRange(Math.Abs(p.Evaluation.Coupling.Perilymph.HoldingCurrentPa-current),0,1e-10);
        Assert.All(p.Evaluation.Membranes,m=>Assert.NotEqual(0,m.Port.CalyxToPerilymphPa));
        Assert.All(p.VoltageIonRates,r=>Assert.InRange(Math.Abs(r),0,1e-9));
    }

    [Fact]
    public void IdentityPermutationPreservesTheCanonicalSearchAndFingerprint()
    {
        var search=new AvatarCleftStationarySearch(Kernel(2),new());var states=States(2);var currents=Axon(2);
        var a=search.Find(HairRoot,0,states,0,currents);
        var b=search.Find(HairRoot,0,states.Reverse().ToArray(),0,currents.Reverse().ToArray());
        Final(a);Final(b);Assert.Equal(JsonSerializer.Serialize(a),JsonSerializer.Serialize(b));
        Assert.Equal(new[] { "p0","p1" },a.AcceptedIterations[^1].Point.Compartments.Select(s=>s.Id));
    }

    [Fact]
    public void ReturnedStationaryGatesInitializeANewCellWithoutChangingTheDynamicRule()
    {
        var k=Kernel();var point=Final(new AvatarCleftStationarySearch(k,new()).Find(HairRoot,0,States(),0,Axon()));
        var cell=new AvatarVestibularCleftDynamics(Guid.NewGuid(),Guid.NewGuid(),k,new(.001),point.HairMv,0,point.Compartments,point.Gates);
        var before=cell.Capture();cell.AdvanceHeld(10,0,Axon());var after=cell.Capture();
        Assert.Null(after.Failure);
        for (int i=0;i<before.Values.Length;i++) Assert.InRange(Math.Abs(after.Values[i]-before.Values[i]),0,2e-10);
        Assert.Equal(10,after.CompletedSteps);
    }

    [Fact]
    public void InvalidInputsAreRejectedInsteadOfInventingMissingPortsOrConcentrations()
    {
        var search=new AvatarCleftStationarySearch(Kernel(2),new());
        Assert.Throws<ArgumentException>(()=>search.Find(HairRoot,0,States(1),0,Axon(2)));
        Assert.Throws<ArgumentException>(()=>search.Find(HairRoot,0,States(2),0,[new("p0",0),new("p0",0)]));
        Assert.Throws<ArgumentException>(()=>search.Find(HairRoot,0,States(2),0,[new("p0",0),new("wrong",0)]));
        Assert.Throws<ArgumentException>(()=>search.Find(HairRoot,0,[new("p0",0,0,0,140),new("p1",0,0,5,140)],0,Axon(2)));
        Assert.Throws<ArgumentException>(()=>new AvatarCleftStationarySearch(Kernel(),new(VoltageRateToleranceMvPerMs:0)));
    }

    [Fact]
    public void NumericalFailureIsAnExplicitUnqualifiedResultWithItsTrialPreserved()
    {
        var result=new AvatarCleftStationarySearch(Kernel(),new()).Find(HairRoot,0,States(),0,Axon(total:double.MaxValue));
        Assert.False(result.StationaryCriteriaPassed);Assert.Equal("NumericalFailure",result.Outcome);Assert.NotNull(result.LastTrialError);
        Assert.False(result.PhysiologicalRestQualified);Assert.False(result.LiveCellModified);Assert.False(result.LocalStabilityAssessed);
        Assert.Equal(result.InitialTransformedState.Length,result.LastTrialTransformedState.Length);
        Assert.Equal(double.MaxValue,Assert.Single(result.AxonCurrents).OutgoingPa);
    }

    [Fact]
    public void ExhaustingTheDeclaredSearchBudgetDoesNotTurnAnApproximatePointIntoSuccess()
    {
        var result=new AvatarCleftStationarySearch(Kernel(),new(MaximumNewtonIterations:1)).Find(HairRoot+1,0,States(),0,Axon());
        Assert.False(result.StationaryCriteriaPassed);Assert.Equal("IterationLimit",result.Outcome);
        Assert.Equal(2,result.AcceptedIterations.Length);Assert.True(result.AcceptedIterations[^1].Point.ScaledResidual>1);
        Assert.True(result.AcceptedIterations[^1].Point.ScaledResidual<result.AcceptedIterations[0].Point.ScaledResidual);
    }

    [Fact]
    public void AFamilyOfPassiveStationaryPointsDoesNotImplyUniqueStableRest()
    {
        var search=new AvatarCleftStationarySearch(Kernel(passive:true),new());
        var a=search.Find(-70,0,States(),0,Axon());var b=search.Find(-50,0,States(),0,Axon());
        Assert.NotEqual(Final(a).HairMv,Final(b).HairMv);Assert.False(a.LocalStabilityAssessed);Assert.False(b.LocalStabilityAssessed);
        Assert.False(a.PhysiologicalRestQualified);Assert.False(b.PhysiologicalRestQualified);
    }

    [Fact]
    public void SearchingDoesNotModifyCallerGuessesOrAnExistingCellSharingTheKernel()
    {
        var k=Kernel();var states=States();var gates=new[] { new AvatarCleftGateState("p0",new(.2,.3,.4),new(.2,.3,.4,.5)) };
        var cell=new AvatarVestibularCleftDynamics(Guid.NewGuid(),Guid.NewGuid(),k,new(.001),HairRoot,0,states,gates);
        string before=JsonSerializer.Serialize(cell.Capture()),original=JsonSerializer.Serialize(states);
        Final(new AvatarCleftStationarySearch(k,new()).Find(HairRoot,0,states,0,Axon()));
        Assert.Equal(before,JsonSerializer.Serialize(cell.Capture()));Assert.Equal(original,JsonSerializer.Serialize(states));
    }
}
