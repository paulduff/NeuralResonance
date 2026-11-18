using System.Text.Json;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class VestibularAfferentDynamicsTests
{
    private static readonly Guid Session=Guid.Parse("e4e955ab-784d-4dbc-bd51-567e6f8f55d5");
    private static readonly Guid Cell=Guid.Parse("7211cd58-ffaf-4ef9-b1c7-9173a2bddf8f");
    private static readonly AvatarAfferentGates AxonGates=new(.2,.3,.4,.5,.6,.7);
    private static AvatarVestibularCleftAfferent Kernel(bool passive=false,double terminal=.2,int patches=1)
    {
        var hp=AvatarHairCellParameters.TypeIReference;var cp=AvatarCalyxParameters.DepositedReference;
        if(passive)
        {
            hp=hp with { MetMaximumNs=0,PotassiumLowMaximumNs=0,CalciumMaximumNs=0,Hcn1MaximumNs=0,PumpDensityPerSquareMicrometer=0 };
            cp=cp with { Kv74MaximumNs=0,Hcn2MaximumNs=0,InnerPumpDensityPerSquareMicrometer=0,OuterPumpDensityPerSquareMicrometer=0,InnerKccMaximumPaPerSquareMicrometer=0,OuterLeakNs=0 };
        }
        var caps=AvatarCleftCapacitances.FromMembraneGeometry(hp,cp);
        var nodes=Enumerable.Range(0,patches).Select(i=>new AvatarCleftCompartment("p"+i,8.0/patches,caps.HairCleftPf/patches,caps.CalyxInnerPf/patches,caps.CalyxOuterPf/patches)).ToArray();
        var edges=Enumerable.Range(1,patches-1).Select(i=>new AvatarCleftConnection("e"+i,"p"+(i-1),"p"+i,.02,.5,3)).ToArray();
        var spatial=new AvatarCleftSpatialCoupling("hair",caps.HairApicalPf,caps.HairPerilymphPf,AvatarCleftMaterial.DepositedReference,nodes,edges);
        var sensory=new AvatarVestibularCleftMembranes(new(spatial,new("bath",5,140),[new("apex",nodes[^1].Id,.02,.25)]),hp,cp);
        var ap=AvatarAfferentMembraneParameters.NodeReference;
        if(passive)ap=ap with { SodiumMaximumNsPerSquareMicrometer=0,Kv1MaximumNsPerSquareMicrometer=0,Kv7MaximumNsPerSquareMicrometer=0,Kv34MaximumNsPerSquareMicrometer=0 };
        var terminals=Enumerable.Range(0,patches).Select(i=>new AvatarAfferentCableTerminal("calyx"+i,"a",terminal/patches)).Append(new("sealed","b",0)).ToArray();
        var cable=new AvatarAfferentCable([new("a",1,1,ap),new("b",1,1,ap)],[new("ab","a","b",1,2,1)],terminals);
        return new(sensory,cable,Enumerable.Range(0,patches).Select(i=>new AvatarCleftAfferentAttachment("calyx"+i,"p"+i)));
    }
    private static AvatarCleftCompartmentState[] States(int patches,bool passive,double gauge=0)=>Enumerable.Range(0,patches)
        .Select(i=>new AvatarCleftCompartmentState("p"+i,gauge,gauge+(passive?0:-60),5,140)).ToArray();
    private static AvatarCleftGateState[] Gates(int patches)=>Enumerable.Range(0,patches).Select(i=>new AvatarCleftGateState("p"+i,new(.2,.3,.4),new(.2,.3,.4,.5))).ToArray();
    private static AvatarAfferentCableState[] Axon(double first,double second,double gauge=0)=>[new("b",gauge+second,AxonGates),new("a",gauge+first,AxonGates)];
    private static AvatarVestibularAfferentDynamics Model(bool passive=false,double step=.00025,double terminal=.2,int patches=1,
        double? first=null,double? second=null,double gauge=0,double epoch=0,bool reverse=false)=>new(Session,Cell,Kernel(passive,terminal,patches),new(step),
            gauge+(passive?0:-70),gauge,reverse?States(patches,passive,gauge).Reverse().ToArray():States(patches,passive,gauge),
            reverse?Gates(patches).Reverse().ToArray():Gates(patches),reverse?Axon(first??(passive?0:-70),second??(passive?0:-70),gauge).Reverse().ToArray():
            Axon(first??(passive?0:-70),second??(passive?0:-70),gauge),epoch);
    private static double Value(AvatarAfferentDynamicsState state,string label)=>state.Values[Array.IndexOf(state.StateLabels,label)];
    private static double Ledger(AvatarAfferentDynamicsState state,string label)=>state.Ledgers[Array.IndexOf(state.LedgerLabels,label)];
    private static string Bytes(AvatarAfferentDynamicsState state)=>JsonSerializer.Serialize(state);

    [Fact]
    public void ConstructionPreservesEveryActualStateWithoutEquilibrationOrEvaluation()
    {
        var model=Model(patches:2);var s=model.Capture();
        Assert.Equal(37,s.Values.Length);Assert.Equal(s.InitialValues,s.Values);Assert.Equal(0,s.NonlinearEvaluations);
        Assert.Equal(.2,Value(s,"axon/a/sodiumActivation"));Assert.Equal(.7,Value(s,"axon/b/kv34Inactivation"));
        Assert.Equal(-60,Value(s,"p1/calyxMv"));Assert.Null(s.MostRecentHeldInput);Assert.Null(s.Failure);
        Assert.All(s.Ledgers,x=>Assert.Equal(0,x));model.AdvanceHeld(0,300);Assert.Equal(Bytes(s),Bytes(model.Capture()));
    }

    [Theory]
    [InlineData(.005)] [InlineData(.0025)]
    public void PassiveFixedVoltageKeepsChargeStillWhileAllActualGatesRelaxAnalytically(double step)
    {
        var model=Model(passive:true,step:step);var before=model.Capture();model.AdvanceHeld((long)Math.Round(.25/step),0);var s=model.Capture();
        Assert.Null(s.Failure);Assert.Equal(.25,s.TimeMs);Assert.All(s.Ledgers,x=>Assert.Equal(0,x));
        foreach(var label in new[] { "hairMv","p0/cleftMv","p0/calyxMv","p0/potassiumMm","p0/sodiumMm","axon/a/potentialMv","axon/b/potentialMv" })
            Assert.Equal(Value(before,label),Value(s,label));
        var r=AvatarAfferentMembraneKernel.Rates(0);
        var cases=new[] { ("sodiumActivation",r.SodiumActivation),("sodiumInactivation",r.SodiumInactivation),("kv1Activation",r.Kv1Activation),
            ("kv7Activation",r.Kv7Activation),("kv34Activation",r.Kv34Activation),("kv34Inactivation",r.Kv34Inactivation) };
        foreach(string id in new[] { "a","b" })foreach(var (gate,rate) in cases)
        {
            string label="axon/"+id+"/"+gate;double expected=rate.Equilibrium+(Value(before,label)-rate.Equilibrium)*Math.Exp(-.25/rate.TauMs);
            Assert.InRange(Math.Abs(Value(s,label)-expected),0,.5*step*step+2e-10);
        }
        var h=AvatarHairCellKernel.Rates(0);var c=AvatarCalyxKernel.FaceRates(0);
        var sensory=new[] { ("hairPotassiumLow",h.PotassiumLow.Equilibrium,h.PotassiumLow.TauMs),("hairCalcium",h.Calcium.Equilibrium,h.Calcium.TauMs),
            ("hairHcn1",h.Hcn1.Equilibrium,h.Hcn1.TauMs),("calyxInnerKv74",c.Kv74.Equilibrium,c.Kv74.TauMs),("calyxInnerHcn2",c.Hcn2.Equilibrium,c.Hcn2.TauMs),
            ("calyxOuterKv74",c.Kv74.Equilibrium,c.Kv74.TauMs),("calyxOuterHcn2",c.Hcn2.Equilibrium,c.Hcn2.TauMs) };
        foreach(var (gate,equilibrium,tau) in sensory)
        {
            string label="p0/"+gate;double expected=equilibrium+(Value(before,label)-equilibrium)*Math.Exp(-.25/tau);
            Assert.InRange(Math.Abs(Value(s,label)-expected),0,.5*step*step+2e-10);
        }
    }

    [Fact]
    public void ASeveredPassiveTwoCompartmentCableMatchesItsAnalyticDischargeAndRefines()
    {
        double Run(double step)
        {
            var model=Model(passive:true,terminal:0,step:step,first:-65,second:-75);model.AdvanceHeld((long)Math.Round(.01/step),0);var s=model.Capture();
            double expected=5*Math.Exp(-.01/(2*Math.PI*.01)); // C/(2g), C=2*pi*.01 pF and g=.5 nS.
            double a=Value(s,"axon/a/potentialMv"),b=Value(s,"axon/b/potentialMv");
            Assert.InRange(Math.Abs((a+b)/2+70),0,2e-12);Assert.InRange(s.MaximumCableChargeResidualFc,0,2e-12);
            Assert.Equal(0,Ledger(s,"membrane/p0/calyxToAxonChargeFc"));Assert.Equal(0,Value(s,"p0/calyxMv"));
            Assert.True(Ledger(s,"axon-edge/ab/leftToRightChargeFc")>0);
            return Math.Abs(a-(-70+expected));
        }
        double coarse=Run(.0005),fine=Run(.00025);Assert.InRange(fine,1e-9,1e-5);Assert.InRange(coarse/fine,3.8,4.2);
    }

    [Fact]
    public void PacketGroupingAndInputOrderingDoNotChangeTheNumericalTrajectory()
    {
        var whole=Model(patches:2);var split=Model(patches:2,reverse:true);whole.AdvanceHeld(8,150);split.AdvanceHeld(3,150);split.AdvanceHeld(5,150);
        var a=whole.Capture();var b=split.Capture();Assert.Equal(a.Fingerprint,b.Fingerprint);Assert.Equal(a.TimeMs,b.TimeMs);
        Assert.Equal(a.Values,b.Values);Assert.Equal(a.AdditionResiduals,b.AdditionResiduals);Assert.Equal(a.Ledgers,b.Ledgers);
        Assert.Equal(a.LedgerAdditionResiduals,b.LedgerAdditionResiduals);Assert.Equal(a.NonlinearEvaluations,b.NonlinearEvaluations);
    }

    [Fact]
    public void ActiveCableAndSensoryCellShareConservedContactChargeAtEveryPatch()
    {
        var model=Model(patches:2);model.AdvanceHeld(12,100);var s=model.Capture();Assert.Null(s.Failure);
        Assert.InRange(s.MaximumChargeResidualFc,0,2e-8);Assert.InRange(s.MaximumIonResidualAttomoles,0,2e-10);
        Assert.InRange(s.MaximumCableChargeResidualFc,0,2e-10);Assert.InRange(s.MaximumInterfaceChargeResidualFc,0,2e-12);
        for(int i=0;i<2;i++)Assert.InRange(Math.Abs(Ledger(s,"membrane/p"+i+"/calyxToAxonChargeFc")+
            Ledger(s,"axon-terminal/calyx"+i+"/nodeToCalyxChargeFc")),0,2e-12);
        Assert.NotEqual(0,Ledger(s,"axon-membrane/a/outwardChargeFc"));
        Assert.Equal(0,Ledger(s,"axon-terminal/sealed/nodeToCalyxChargeFc"));
        Assert.NotEqual(.2,Value(s,"axon/a/sodiumActivation"));
    }

    [Theory]
    [InlineData(-80)] [InlineData(-40)]
    public void ChangingTheNerveStateFeedsBackIntoTheCalyxUnlessTheContactIsCut(double nerveMv)
    {
        var baseline=Model();var changed=Model(first:nerveMv,second:nerveMv);
        var cut=Model(terminal:0);var cutChanged=Model(terminal:0,first:nerveMv,second:nerveMv);
        foreach(var m in new[] { baseline,changed,cut,cutChanged })m.AdvanceHeld(2,0);
        Assert.NotEqual(Value(baseline.Capture(),"p0/calyxMv"),Value(changed.Capture(),"p0/calyxMv"));
        Assert.InRange(Math.Abs(Value(cut.Capture(),"p0/calyxMv")-Value(cutChanged.Capture(),"p0/calyxMv")),0,2e-9);
        Assert.Equal(0,Ledger(cutChanged.Capture(),"membrane/p0/calyxToAxonChargeFc"));
    }

    [Fact]
    public void ElectricalGaugeShiftDoesNotChangePhysicalEvolution()
    {
        var a=Model();var b=Model(gauge:32);a.AdvanceHeld(4,50);b.AdvanceHeld(4,50);var x=a.Capture();var y=b.Capture();
        for(int i=0;i<x.Values.Length;i++)
        {
            bool voltage=x.StateLabels[i].EndsWith("Mv",StringComparison.Ordinal);
            Assert.InRange(Math.Abs(x.Values[i]-(y.Values[i]-(voltage?32:0))),0,2e-8);
        }
        for(int i=0;i<x.Ledgers.Length;i++)Assert.InRange(Math.Abs(x.Ledgers[i]-y.Ledgers[i]),0,2e-8);
    }

    [Fact]
    public void NumericalDomainFailurePreservesStateAndCannotBeRestartedOrClipped()
    {
        // Finite explicit state, but the declared sodium table's extrapolation
        // is nonphysical here. Construction does not secretly run or repair it.
        var model=Model(first:1000);var initial=model.Capture();
        Assert.ThrowsAny<Exception>(()=>model.AdvanceHeld(1,0));var failed=model.Capture();Assert.NotNull(failed.Failure);
        Assert.Equal(initial.Values,failed.Values);Assert.Equal(initial.Ledgers,failed.Ledgers);Assert.Equal(initial.CompletedSteps,failed.CompletedSteps);
        Assert.Equal(initial.AdditionResiduals,failed.AdditionResiduals);Assert.Equal(initial.LedgerAdditionResiduals,failed.LedgerAdditionResiduals);
        Assert.Equal(initial.Values.Length,failed.Failure.Candidate.Length);Assert.NotEmpty(failed.Failure.Reason);
        Assert.Throws<InvalidOperationException>(()=>model.AdvanceHeld(1,0));
        failed.Failure.Candidate[0]=999;Assert.NotEqual(999,model.Capture().Failure!.Candidate[0]);
    }

    [Fact]
    public void InvalidRequestsCannotChangeStateOrPoisonTheOwnedEpoch()
    {
        var model=Model();string before=Bytes(model.Capture());
        Assert.Throws<ArgumentException>(()=>model.AdvanceHeld(-1,0));Assert.Throws<ArgumentException>(()=>model.AdvanceHeld(1,double.NaN));
        Assert.Throws<ArgumentException>(()=>model.AdvanceHeld(9_007_199_254_740_992,0));Assert.Equal(before,Bytes(model.Capture()));
        model.AdvanceHeld(1,0);Assert.Null(model.Capture().Failure);
    }

    [Fact]
    public void InitialCableIdentityAndActualGateDomainAreRequired()
    {
        AvatarVestibularAfferentDynamics Make(AvatarAfferentCableState[] states)=>new(Session,Cell,Kernel(),new(.00025),-70,0,States(1,false),Gates(1),states);
        var valid=Axon(-70,-70);
        Assert.ThrowsAny<ArgumentException>(()=>Make([]));Assert.ThrowsAny<ArgumentException>(()=>Make([valid[0],valid[0]]));
        Assert.ThrowsAny<ArgumentException>(()=>Make([valid[0],valid[1] with { Id="other" }]));
        Assert.ThrowsAny<ArgumentException>(()=>Make([valid[0],valid[1] with { Gates=AxonGates with { SodiumActivation=1.01 } }]));
        Assert.ThrowsAny<ArgumentException>(()=>Make([valid[0],valid[1] with { PotentialMv=double.NaN }]));
    }

    [Fact]
    public void CallerArraysAndSnapshotsCannotChangeTheOwnedTrajectory()
    {
        var states=States(1,false);var gates=Gates(1);var axon=Axon(-70,-70);
        var model=new AvatarVestibularAfferentDynamics(Session,Cell,Kernel(),new(.00025),-70,0,states,gates,axon);
        string original=Bytes(model.Capture());states[0]=states[0] with { CalyxMv=99 };axon[0]=axon[0] with { PotentialMv=99 };gates[0]=gates[0] with { Hair=new(1,1,1) };
        var copy=model.Capture();copy.Values[0]=999;copy.InitialValues[0]=999;copy.StateLabels[0]="wrong";copy.AdditionResiduals[0]=999;
        copy.Ledgers[0]=999;copy.LedgerAdditionResiduals[0]=999;copy.LedgerLabels[0]="wrong";Assert.Equal(original,Bytes(model.Capture()));
    }

    [Fact]
    public void StateAnatomyAndSolverChangesHaveDifferentNumericalFingerprints()
    {
        string original=Model().Fingerprint;
        Assert.NotEqual(original,Model(first:-69).Fingerprint);Assert.NotEqual(original,Model(terminal:0).Fingerprint);
        Assert.NotEqual(original,Model(step:.000125).Fingerprint);Assert.NotEqual(original,Model(gauge:32).Fingerprint);
        Assert.NotEqual(original,Model(epoch:1000).Fingerprint);Assert.Throws<ArgumentException>(()=>Model(epoch:double.MaxValue));
    }

    [Fact]
    public void TheSingleOwnerRetainsTheIntegerClockAcrossCalls()
    {
        var model=Model(epoch:1000);model.AdvanceHeld(3,0);Assert.Equal(1000+3*.00025,model.Capture().TimeMs);
        Exception? capture=null,advance=null;var thread=new Thread(()=>
        {
            try { model.Capture(); } catch(Exception e) { capture=e; }
            try { model.AdvanceHeld(1,0); } catch(Exception e) { advance=e; }
        });
        thread.Start();Assert.True(thread.Join(TimeSpan.FromSeconds(5)));Assert.IsType<InvalidOperationException>(capture);Assert.IsType<InvalidOperationException>(advance);
        Assert.Equal(3,model.Capture().CompletedSteps);
    }
}
