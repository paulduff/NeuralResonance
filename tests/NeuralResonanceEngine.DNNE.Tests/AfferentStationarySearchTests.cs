using System.Text.Json;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class AfferentStationarySearchTests
{
    private static readonly double Ek=26*(Math.Log(5)-Math.Log(150));
    // Numerical controls only: sensory hair KL, calyx leak and a passive cable,
    // or a Kv1-only cable anchoring the calyx at E_K. Not live channel defaults.
    private static AvatarVestibularCleftAfferent Kernel(int patches=1,double contact=.2,bool passive=false,bool nervePotassium=false)
    {
        var h=AvatarHairCellParameters.TypeIReference with { MetMaximumNs=0,CalciumMaximumNs=0,Hcn1MaximumNs=0,
            PumpDensityPerSquareMicrometer=0,PotassiumLowMaximumNs=passive?0:80 };
        var c=AvatarCalyxParameters.DepositedReference with { Kv74MaximumNs=0,Hcn2MaximumNs=0,
            InnerPumpDensityPerSquareMicrometer=0,OuterPumpDensityPerSquareMicrometer=0,InnerKccMaximumPaPerSquareMicrometer=0,
            OuterLeakNs=passive||nervePotassium?0:2 };
        var cap=AvatarCleftCapacitances.FromMembraneGeometry(h,c);
        var nodes=Enumerable.Range(0,patches).Select(i=>new AvatarCleftCompartment("p"+i,8.0/patches,
            cap.HairCleftPf/patches,cap.CalyxInnerPf/patches,cap.CalyxOuterPf/patches)).ToArray();
        var edges=Enumerable.Range(1,patches-1).Select(i=>new AvatarCleftConnection("e"+i,"p"+(i-1),"p"+i,.02,.5,3)).ToArray();
        var spatial=new AvatarCleftSpatialCoupling("hair",cap.HairApicalPf,cap.HairPerilymphPf,AvatarCleftMaterial.DepositedReference,nodes,edges);
        var sensory=new AvatarVestibularCleftMembranes(new(spatial,new("bath",5,140),[new("apex",nodes[^1].Id,.02,.25)]),h,c);
        var axon=AvatarAfferentMembraneParameters.NodeReference with { SodiumMaximumNsPerSquareMicrometer=0,
            Kv1MaximumNsPerSquareMicrometer=nervePotassium?.13:0,Kv7MaximumNsPerSquareMicrometer=0,Kv34MaximumNsPerSquareMicrometer=0 };
        var terminals=Enumerable.Range(0,patches).Select(i=>new AvatarAfferentCableTerminal("calyx"+i,"a",contact/patches)).Append(new("sealed","b",0));
        var cable=new AvatarAfferentCable([new("a",1,1,axon),new("b",1,1,axon)],[new("ab","a","b",1,2,1)],terminals);
        return new(sensory,cable,Enumerable.Range(0,patches).Select(i=>new AvatarCleftAfferentAttachment("calyx"+i,"p"+i)));
    }
    private static AvatarCleftCompartmentState[] States(int count=1,double calyx=0,double gauge=0)=>
        Enumerable.Range(0,count).Select(i=>new AvatarCleftCompartmentState("p"+i,gauge,gauge+calyx,5,140)).ToArray();
    private static AvatarAfferentStationaryGuess[] Cable(double first=0,double second=0,double gauge=0)=>[new("b",gauge+second),new("a",gauge+first)];
    private static AvatarAfferentStationaryPoint Final(AvatarAfferentStationaryResult result)
    {
        Assert.True(result.StationaryCriteriaPassed,result.Outcome+" "+result.LastTrialError);
        Assert.Equal("StationaryResidualCriteriaMet",result.Outcome);
        Assert.False(result.LocalStabilityAssessed);Assert.False(result.PhysiologicalRestQualified);Assert.False(result.LiveCellModified);
        var p=result.AcceptedIterations[^1].Point;Assert.InRange(p.ScaledResidual,0,1);return p;
    }

    [Fact]
    public void KnownConnectedRootIsRecognizedWithoutAdvancingOrTakingANewtonStep()
    {
        var r=new AvatarAfferentStationarySearch(Kernel(),new()).Find(Ek,0,States(),Cable(),0);var p=Final(r);
        Assert.Single(r.AcceptedIterations);Assert.Equal(1,r.Evaluations);Assert.Equal(7,r.UnknownLabels.Length);
        Assert.All(p.CableStates,s=>Assert.Equal(0,s.PotentialMv));Assert.All(p.VoltageIonRates,v=>Assert.InRange(Math.Abs(v),0,1e-9));
        Assert.InRange(p.MaximumGateRatePerMs,0,1e-12);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void PerturbedConnectedControlFindsTheAnalyticSensoryAndCableBalance(int sign)
    {
        var states=new AvatarCleftCompartmentState[] { new("p0",sign*.01,sign*.05,5*(1+sign*.001),140*(1-sign*.001)) };
        var r=new AvatarAfferentStationarySearch(Kernel(),new()).Find(Ek+sign,0,states,Cable(sign*.1,-sign*.05),0);var p=Final(r);
        Assert.True(r.AcceptedIterations.Length>1);Assert.InRange(Math.Abs(p.HairMv-Ek),0,1e-6);
        Assert.All(p.CableStates,s=>Assert.InRange(Math.Abs(s.PotentialMv),0,1e-7));
        Assert.InRange(Math.Abs(p.Compartments[0].CalyxMv),0,1e-7);Assert.InRange(Math.Abs(p.Compartments[0].CleftMv),0,1e-7);
        Assert.InRange(Math.Abs(p.Compartments[0].PotassiumMm-5),0,1e-7);Assert.InRange(Math.Abs(p.Compartments[0].SodiumMm-140),0,1e-7);
        for(int i=1;i<r.AcceptedIterations.Length;i++)Assert.True(r.AcceptedIterations[i].Point.ScaledResidual<r.AcceptedIterations[i-1].Point.ScaledResidual);
    }

    [Fact]
    public void AnActivePotassiumCableHasTheKnownNernstStationaryPoint()
    {
        var r=new AvatarAfferentStationarySearch(Kernel(nervePotassium:true),new()).Find(Ek,0,States(calyx:Ek),Cable(Ek,Ek),0);
        var p=Final(r);Assert.Single(r.AcceptedIterations);
        Assert.All(p.CableStates,s=>Assert.Equal(Ek,s.PotentialMv));
        Assert.All(p.Evaluation.Cable.Nodes,n=>Assert.Equal(0,n.Membrane.Currents.Kv1PotassiumPaPerSquareMicrometer));
        Assert.All(p.CableStates,s=>Assert.True(s.Gates.Kv1Activation>0));
    }

    [Fact]
    public void EveryRootTrialUsesReciprocalCurrentFromItsActualCableVoltage()
    {
        var r=new AvatarAfferentStationarySearch(Kernel(),new()).Find(Ek,0,States(),Cable(-10,-20),0);
        var first=r.AcceptedIterations[0].Point;var t=Assert.Single(first.Evaluation.Transfers);
        Assert.Equal(2,t.CalyxToCablePa);Assert.Equal(-2,t.CableToCalyxPa);
        Assert.Equal(t.CalyxToCablePa,first.Evaluation.Sensory.Membranes[0].Port.CalyxToAxonPa);
        Assert.NotEqual(0,first.Evaluation.Cable.Nodes[0].PotentialMvPerMs);
        Assert.InRange(Math.Abs(Final(r).CableStates[0].PotentialMv),0,1e-7);
    }

    [Fact]
    public void ASeveredAttachmentRemovesBothDirectionsOfTheElectricalLoad()
    {
        var r=new AvatarAfferentStationarySearch(Kernel(contact:0),new()).Find(Ek,0,States(),Cable(-10,-20),0);
        // A disconnected passive cable has an unanchored mean. Do not demand a
        // unique root or hide a singular Jacobian with a fictitious conductance.
        var p=r.AcceptedIterations[0].Point;
        Assert.All(p.Evaluation.Transfers,t=>{Assert.Equal(0,t.CalyxToCablePa);Assert.Equal(0,t.CableToCalyxPa);});
        Assert.Equal(0,p.Evaluation.Sensory.Membranes[0].Port.CalyxToAxonPa);
        Assert.False(r.LocalStabilityAssessed);Assert.False(r.PhysiologicalRestQualified);
    }

    [Fact]
    public void IdentityPermutationPreservesCanonicalUnknownsAndTheWholeSearch()
    {
        var search=new AvatarAfferentStationarySearch(Kernel(patches:2),new());var s=States(2);var a=Cable();
        var first=search.Find(Ek,0,s,a,0);var second=search.Find(Ek,0,s.Reverse().ToArray(),a.Reverse().ToArray(),0);
        Final(first);Final(second);Assert.Equal(JsonSerializer.Serialize(first),JsonSerializer.Serialize(second));
        Assert.Equal(new[] { "a","b" },first.AcceptedIterations[0].Point.CableStates.Select(n=>n.Id));
    }

    [Fact]
    public void UniformReferencePotentialShiftPreservesPhysicalRootCurrents()
    {
        var search=new AvatarAfferentStationarySearch(Kernel(),new());var a=Final(search.Find(Ek,0,States(),Cable(),0));
        var b=Final(search.Find(Ek+32,32,States(gauge:32),Cable(gauge:32),0));
        Assert.InRange(Math.Abs(b.HairMv-a.HairMv-32),0,1e-12);Assert.All(b.CableStates,s=>Assert.Equal(32,s.PotentialMv));
        Assert.InRange(Math.Abs(a.Evaluation.Cable.TotalMembraneOutwardPa-b.Evaluation.Cable.TotalMembraneOutwardPa),0,1e-12);
    }

    [Fact]
    public void AReturnedControlRootCanInitializeANewJointCellWithFreeGates()
    {
        var kernel=Kernel();var p=Final(new AvatarAfferentStationarySearch(kernel,new()).Find(Ek,0,States(),Cable(),0));
        var cell=new AvatarVestibularAfferentDynamics(Guid.NewGuid(),Guid.NewGuid(),kernel,new(.00025),p.HairMv,0,p.Compartments,p.Gates,p.CableStates);
        var before=cell.Capture();cell.AdvanceHeld(8,0);var after=cell.Capture();Assert.Null(after.Failure);
        Assert.Equal(8,after.CompletedSteps);Assert.True(after.NonlinearEvaluations>0);
        for(int i=0;i<before.Values.Length;i++)Assert.InRange(Math.Abs(after.Values[i]-before.Values[i]),0,2e-9);
    }

    [Fact]
    public void SearchingCannotResetAnExistingCellOrMutateItsCallerGuesses()
    {
        var k=Kernel();var s=States();var a=Cable();
        var cell=new AvatarVestibularAfferentDynamics(Guid.NewGuid(),Guid.NewGuid(),k,new(.00025),Ek,0,s,
            [new("p0",new(.2,.3,.4),new(.2,.3,.4,.5))],[new("a",0,new(.2,.3,.4,.5,.6,.7)),new("b",0,new(.3,.4,.5,.6,.7,.8))]);
        string before=JsonSerializer.Serialize(cell.Capture()),guesses=JsonSerializer.Serialize(new { s,a });
        Final(new AvatarAfferentStationarySearch(k,new()).Find(Ek,0,s,a,0));
        Assert.Equal(before,JsonSerializer.Serialize(cell.Capture()));Assert.Equal(guesses,JsonSerializer.Serialize(new { s,a }));
    }

    [Fact]
    public void InvalidIdentityConcentrationOrWorkLimitsAreRejected()
    {
        var search=new AvatarAfferentStationarySearch(Kernel(),new());
        Assert.Throws<ArgumentException>(()=>search.Find(Ek,0,States(),[new("a",0)],0));
        Assert.Throws<ArgumentException>(()=>search.Find(Ek,0,States(),[new("a",0),new("a",0)],0));
        Assert.Throws<ArgumentException>(()=>search.Find(Ek,0,States(),[new("a",0),new("unknown",0)],0));
        Assert.Throws<ArgumentException>(()=>search.Find(Ek,0,[new("p0",0,0,0,140)],Cable(),0));
        Assert.Throws<ArgumentException>(()=>search.Find(Ek,0,States(),Cable(double.NaN,0),0));
        Assert.Throws<ArgumentException>(()=>new AvatarAfferentStationarySearch(Kernel(),new(VoltageRateToleranceMvPerMs:0)));
    }

    [Fact]
    public void AnInvalidChannelDomainRetainsTheFailedCableTrialWithoutClipping()
    {
        var r=new AvatarAfferentStationarySearch(Kernel(),new()).Find(Ek,0,States(),Cable(1000,0),0);
        Assert.False(r.StationaryCriteriaPassed);Assert.Equal("NumericalFailure",r.Outcome);Assert.NotNull(r.LastTrialError);
        Assert.Empty(r.AcceptedIterations);Assert.Equal(1000,r.LastTrialTransformedState[5]);
        Assert.False(r.LocalStabilityAssessed);Assert.False(r.PhysiologicalRestQualified);Assert.False(r.LiveCellModified);
    }

    [Fact]
    public void ASearchBudgetDoesNotTurnAnApproximateRootIntoSuccess()
    {
        var r=new AvatarAfferentStationarySearch(Kernel(),new(MaximumNewtonIterations:1)).Find(Ek+1,0,States(),Cable(),0);
        Assert.False(r.StationaryCriteriaPassed);Assert.Equal("IterationLimit",r.Outcome);
        Assert.Equal(2,r.AcceptedIterations.Length);Assert.True(r.AcceptedIterations[^1].Point.ScaledResidual>1);
    }

    [Fact]
    public void APassiveFamilyOfRootsDoesNotClaimUniquenessOrStablePhysiology()
    {
        var search=new AvatarAfferentStationarySearch(Kernel(passive:true),new());
        var a=search.Find(-70,0,States(),Cable(),0);var b=search.Find(-50,0,States(),Cable(),0);
        Assert.NotEqual(Final(a).HairMv,Final(b).HairMv);Assert.False(a.LocalStabilityAssessed);Assert.False(b.PhysiologicalRestQualified);
    }

    [Fact]
    public void ReturnedArrayMutationCannotChangeTheKernelOrASubsequentSearch()
    {
        var search=new AvatarAfferentStationarySearch(Kernel(),new());var first=search.Find(Ek,0,States(),Cable(),0);
        string expected=JsonSerializer.Serialize(first);Final(first);
        first.InitialTransformedState[0]=900;first.AcceptedIterations[0].Point.CableStates[0]=new("wrong",900,new());
        var next=search.Find(Ek,0,States(),Cable(),0);Final(next);Assert.Equal(expected,JsonSerializer.Serialize(next));
    }
}
