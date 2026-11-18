using System.Text.Json;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class VestibularCleftDynamicsTests
{
    private static readonly Guid Session = Guid.Parse("900e2dc8-a3e2-4b36-b0fe-6e148c9c2be2");
    private static readonly Guid Cell = Guid.Parse("033b33d2-4008-4ab0-b3d9-43eef7f9b426");
    private static AvatarVestibularCleftMembranes Kernel(int count=1, bool passive=false)
    {
        var hair=AvatarHairCellParameters.TypeIReference; var calyx=AvatarCalyxParameters.DepositedReference;
        if (passive)
        {
            hair=hair with { MetMaximumNs=0,PotassiumLowMaximumNs=0,CalciumMaximumNs=0,Hcn1MaximumNs=0,PumpDensityPerSquareMicrometer=0 };
            calyx=calyx with { Kv74MaximumNs=0,Hcn2MaximumNs=0,InnerPumpDensityPerSquareMicrometer=0,
                OuterPumpDensityPerSquareMicrometer=0,InnerKccMaximumPaPerSquareMicrometer=0,OuterLeakNs=0 };
        }
        var caps=AvatarCleftCapacitances.FromMembraneGeometry(hair,calyx);
        var nodes=Enumerable.Range(0,count).Select(i=>new AvatarCleftCompartment("p"+i,8.0/count,
            caps.HairCleftPf/count,caps.CalyxInnerPf/count,caps.CalyxOuterPf/count)).ToArray();
        var edges=Enumerable.Range(1,count-1).Select(i=>new AvatarCleftConnection("e"+i,"p"+(i-1),"p"+i,.02,.5,3)).ToArray();
        var spatial=new AvatarCleftSpatialCoupling("hair",caps.HairApicalPf,caps.HairPerilymphPf,AvatarCleftMaterial.DepositedReference,nodes,edges);
        return new(new(spatial,new("bath",5,140),[new("apex",nodes[^1].Id,.02,.25)]),hair,calyx);
    }
    private static AvatarCleftCompartmentState[] States(int count, bool passive=false, double gauge=0) =>
        Enumerable.Range(0,count).Select(i=>new AvatarCleftCompartmentState("p"+i,gauge,passive ? gauge : gauge-60,5,140)).ToArray();
    private static AvatarCleftGateState[] Gates(int count) => Enumerable.Range(0,count).Select(i=>new AvatarCleftGateState("p"+i,new(.2,.3,.4),new(.2,.3,.4,.5))).ToArray();
    private static AvatarCleftAxonCurrent[] Axon(int count, double current=0) => Enumerable.Range(0,count).Select(i=>new AvatarCleftAxonCurrent("p"+i,current/count)).ToArray();
    private static AvatarVestibularCleftDynamics CellModel(int count=1, bool passive=false, double step=.001, double epoch=0, double gauge=0) =>
        new(Session,Cell,Kernel(count,passive),new(step),passive ? gauge : gauge-70,gauge,States(count,passive,gauge),Gates(count),epoch);
    private static double Value(AvatarCleftDynamicsState s,string label) => s.Values[Array.IndexOf(s.StateLabels,label)];
    private static double Ledger(AvatarCleftDynamicsState s,string label) => s.Ledgers[Array.IndexOf(s.LedgerLabels,label)];

    [Fact]
    public void ExplicitInitializationRetainsActualGatesAndDoesNotEvaluateOrSettleTheCell()
    {
        var cell=CellModel(2); var s=cell.Capture();
        Assert.Equal(0,s.CompletedSteps); Assert.Equal(0,s.NonlinearEvaluations); Assert.Null(s.Failure); Assert.Null(s.MostRecentHeldInput);
        Assert.Equal(23,s.Values.Length); Assert.Equal(s.InitialValues,s.Values);
        Assert.Equal(.2,Value(s,"p0/hairPotassiumLow")); Assert.Equal(.5,Value(s,"p1/calyxOuterHcn2"));
        Assert.All(s.Ledgers,x=>Assert.Equal(0,x)); Assert.All(s.AdditionResiduals,x=>Assert.Equal(0,x));
        Assert.NotEqual(AvatarHairCellKernel.Rates(-70).PotassiumLow.Equilibrium,Value(s,"p0/hairPotassiumLow"));
        cell.AdvanceHeld(0,300,Axon(2)); Assert.Equal(JsonSerializer.Serialize(s),JsonSerializer.Serialize(cell.Capture()));
    }

    [Theory]
    [InlineData(.01)]
    [InlineData(.005)]
    public void PassiveFixedPotentialsGiveTheAnalyticSevenGateRelaxations(double step)
    {
        var cell=CellModel(passive:true,step:step); var before=cell.Capture();
        cell.AdvanceHeld((long)Math.Round(2/step),0,Axon(1)); var after=cell.Capture();
        Assert.Equal(2,after.TimeMs); Assert.Null(after.Failure);
        foreach (string label in new[] { "hairMv","p0/cleftMv","p0/calyxMv","p0/potassiumMm","p0/sodiumMm" })
            Assert.Equal(Value(before,label),Value(after,label));
        var h=AvatarHairCellKernel.Rates(0); var c=AvatarCalyxKernel.FaceRates(0);
        var cases=new[] { ("p0/hairPotassiumLow",h.PotassiumLow.Equilibrium,h.PotassiumLow.TauMs),
            ("p0/hairCalcium",h.Calcium.Equilibrium,h.Calcium.TauMs),("p0/hairHcn1",h.Hcn1.Equilibrium,h.Hcn1.TauMs),
            ("p0/calyxInnerKv74",c.Kv74.Equilibrium,c.Kv74.TauMs),("p0/calyxInnerHcn2",c.Hcn2.Equilibrium,c.Hcn2.TauMs),
            ("p0/calyxOuterKv74",c.Kv74.Equilibrium,c.Kv74.TauMs),("p0/calyxOuterHcn2",c.Hcn2.Equilibrium,c.Hcn2.TauMs) };
        foreach (var (label,target,tau) in cases)
        {
            double expected=target+(Value(before,label)-target)*Math.Exp(-2/tau);
            Assert.InRange(Math.Abs(Value(after,label)-expected),0,step*step*.1+2e-10);
        }
        Assert.All(after.Ledgers,x=>Assert.Equal(0,x)); Assert.Equal(0,after.MaximumChargeResidualFc); Assert.Equal(0,after.MaximumIonResidualAttomoles);
    }

    [Fact]
    public void PassiveGateTemporalRefinementReducesAnalyticErrorAtSecondOrder()
    {
        double Error(double step)
        {
            var cell=CellModel(passive:true,step:step); cell.AdvanceHeld((long)Math.Round(.6/step),0,Axon(1));
            var r=AvatarHairCellKernel.Rates(0).Calcium;
            return Math.Abs(Value(cell.Capture(),"p0/hairCalcium")-(r.Equilibrium+(.3-r.Equilibrium)*Math.Exp(-1)));
        }
        double coarse=Error(.02),fine=Error(.01);
        Assert.True(fine>1e-9); Assert.InRange(coarse/fine,3.8,4.2);
    }

    [Fact]
    public void ChunkingAndInputOrderLeaveEveryAcceptedStateAndLedgerBitwiseIdentical()
    {
        var whole=CellModel(2); var split=CellModel(2);
        whole.AdvanceHeld(7,100,Axon(2,1)); split.AdvanceHeld(2,100,Axon(2,1).Reverse().ToArray()); split.AdvanceHeld(5,100,Axon(2,1));
        var a=whole.Capture(); var b=split.Capture();
        Assert.Equal(a.Fingerprint,b.Fingerprint); Assert.Equal(a.CompletedSteps,b.CompletedSteps); Assert.Equal(a.TimeMs,b.TimeMs);
        Assert.Equal(a.Values,b.Values); Assert.Equal(a.AdditionResiduals,b.AdditionResiduals);
        Assert.Equal(a.Ledgers,b.Ledgers); Assert.Equal(a.LedgerAdditionResiduals,b.LedgerAdditionResiduals);
        Assert.Equal(a.NonlinearEvaluations,b.NonlinearEvaluations); Assert.Equal(a.MaximumChargeResidualFc,b.MaximumChargeResidualFc);
        Assert.Null(a.Failure); Assert.Null(b.Failure);
    }

    [Fact]
    public void ActiveLocalChargeAndIonLedgersRemainConsistentIncludingTheExplicitAxon()
    {
        var cell=CellModel(2); var before=cell.Capture(); cell.AdvanceHeld(20,200,Axon(2,10)); var s=cell.Capture();
        Assert.Null(s.Failure); Assert.NotEqual(before.Values,s.Values);
        Assert.InRange(s.MaximumChargeResidualFc,0,2e-9); Assert.InRange(s.MaximumIonResidualAttomoles,0,2e-10);
        double axon=Ledger(s,"membrane/p0/calyxToAxonChargeFc")+Ledger(s,"membrane/p1/calyxToAxonChargeFc");
        Assert.InRange(Math.Abs(axon-.2),0,1e-13);
        Assert.InRange(Math.Abs(Ledger(s,"endolymphHoldingChargeFc")+Ledger(s,"perilymphHoldingChargeFc")-axon),0,2e-10);
        // A gate is evolved from its prior state, not overwritten with its
        // instantaneous steady-state target after the voltage update.
        double v=Value(s,"hairMv")-Value(s,"p0/cleftMv");
        Assert.NotEqual(AvatarHairCellKernel.Rates(v).PotassiumLow.Equilibrium,Value(s,"p0/hairPotassiumLow"));
    }

    [Fact]
    public void BundleAndTerminalInputsCauseDistinctCoupledResponses()
    {
        var quiet=CellModel(); var bundle=CellModel(); var terminal=CellModel();
        quiet.AdvanceHeld(10,-300,Axon(1)); bundle.AdvanceHeld(10,300,Axon(1)); terminal.AdvanceHeld(10,-300,Axon(1,10));
        var a=quiet.Capture(); var b=bundle.Capture(); var c=terminal.Capture();
        Assert.NotEqual(Value(a,"hairMv"),Value(b,"hairMv")); Assert.NotEqual(Ledger(a,"metOutwardChargeFc"),Ledger(b,"metOutwardChargeFc"));
        Assert.NotEqual(Value(a,"p0/calyxMv"),Value(c,"p0/calyxMv")); Assert.Equal(0,Ledger(a,"membrane/p0/calyxToAxonChargeFc"));
        Assert.InRange(Math.Abs(Ledger(c,"membrane/p0/calyxToAxonChargeFc")-.1),0,1e-13);
    }

    [Fact]
    public void NumericFailurePreservesAcceptedPrefixAndCannotSilentlyRestart()
    {
        var cell=CellModel(); cell.AdvanceHeld(2,0,Axon(1)); var before=cell.Capture();
        Assert.ThrowsAny<Exception>(()=>cell.AdvanceHeld(1,0,Axon(1,double.MaxValue)));
        var failed=cell.Capture(); Assert.NotNull(failed.Failure); Assert.NotEmpty(failed.Failure.Reason);
        Assert.Equal(before.CompletedSteps,failed.CompletedSteps); Assert.Equal(before.TimeMs,failed.TimeMs);
        Assert.Equal(before.Values,failed.Values); Assert.Equal(before.AdditionResiduals,failed.AdditionResiduals);
        Assert.Equal(before.Ledgers,failed.Ledgers); Assert.Equal(before.LedgerAdditionResiduals,failed.LedgerAdditionResiduals);
        Assert.Equal(double.MaxValue,Assert.Single(failed.MostRecentHeldInput!.AxonCurrents).OutgoingPa);
        Assert.Equal(before.Values.Length,failed.Failure.Candidate.Length);
        Assert.Throws<InvalidOperationException>(()=>cell.AdvanceHeld(1,0,Axon(1)));
    }

    [Fact]
    public void BadArgumentsDoNotAlterAcceptedStateOrPoisonTheCell()
    {
        var cell=CellModel(2); var before=JsonSerializer.Serialize(cell.Capture());
        Assert.Throws<ArgumentException>(()=>cell.AdvanceHeld(1,0,[]));
        Assert.Throws<ArgumentException>(()=>cell.AdvanceHeld(1,0,[new("p0",0),new("p0",0)]));
        Assert.Throws<ArgumentException>(()=>cell.AdvanceHeld(1,0,[new("p0",0),new("wrong",0)]));
        Assert.Throws<ArgumentException>(()=>cell.AdvanceHeld(1,double.NaN,Axon(2)));
        Assert.Throws<ArgumentException>(()=>cell.AdvanceHeld(-1,0,Axon(2)));
        Assert.Equal(before,JsonSerializer.Serialize(cell.Capture()));
        cell.AdvanceHeld(1,0,Axon(2)); Assert.Null(cell.Capture().Failure);
    }

    [Fact]
    public void SnapshotsAndCallerArraysCannotMutateOwnedState()
    {
        var states=States(1); var gates=Gates(1);
        var cell=new AvatarVestibularCleftDynamics(Session,Cell,Kernel(),new(.001),-70,0,states,gates);
        states[0]=states[0] with { PotassiumMm=100 }; gates[0]=gates[0] with { Hair=new(1,1,1) };
        var baseline=JsonSerializer.Serialize(cell.Capture()); var copy=cell.Capture();
        copy.Values[0]=999; copy.InitialValues[0]=999; copy.AdditionResiduals[0]=999; copy.StateLabels[0]="wrong";
        copy.Ledgers[0]=999; copy.LedgerAdditionResiduals[0]=999; copy.LedgerLabels[0]="wrong";
        Assert.Equal(baseline,JsonSerializer.Serialize(cell.Capture()));
        var axon=Axon(1); cell.AdvanceHeld(1,0,axon); axon[0]=new("wrong",99);
        copy=cell.Capture(); copy.MostRecentHeldInput!.AxonCurrents[0]=new("wrong",99);
        Assert.Equal("p0",Assert.Single(cell.Capture().MostRecentHeldInput!.AxonCurrents).Id);
    }

    [Fact]
    public void SourceAndNumericalIdentityCoverInitialStateBathClockAndSolverSettings()
    {
        var k=Kernel(); var states=States(1); var gates=Gates(1);
        string Make(AvatarCleftIntegrationSettings settings,double epoch,double bath,double hair)=>
            new AvatarVestibularCleftDynamics(Session,Cell,k,settings,hair,bath,states,gates,epoch).Fingerprint;
        string original=Make(new(.001),0,0,-70);
        Assert.NotEqual(original,Make(new(.002),0,0,-70)); Assert.NotEqual(original,Make(new(.001,RelativeTolerance:1e-12),0,0,-70));
        Assert.NotEqual(original,Make(new(.001),1000,0,-70)); Assert.NotEqual(original,Make(new(.001),0,1,-70));
        Assert.NotEqual(original,Make(new(.001),0,0,-69));
        Assert.Throws<ArgumentException>(()=>CellModel(step:.1)); Assert.Throws<ArgumentException>(()=>CellModel(epoch:double.MaxValue));
    }

    [Fact]
    public void ClockUsesIntegerLocalStepsAndOnlyItsOwnerMayAdvanceOrCapture()
    {
        var cell=CellModel(epoch:1000); cell.AdvanceHeld(9,0,Axon(1)); Assert.Equal(1000+9*.001,cell.Capture().TimeMs);
        Exception? capture=null, advance=null;
        var thread=new Thread(()=>
        {
            try { cell.Capture(); } catch (Exception e) { capture=e; }
            try { cell.AdvanceHeld(1,0,Axon(1)); } catch (Exception e) { advance=e; }
        });
        thread.Start(); thread.Join(); Assert.IsType<InvalidOperationException>(capture); Assert.IsType<InvalidOperationException>(advance);
        Assert.Equal(9,cell.Capture().CompletedSteps);
    }
}
