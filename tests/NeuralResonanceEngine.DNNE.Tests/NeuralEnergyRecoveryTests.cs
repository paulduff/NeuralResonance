using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class NeuralEnergyRecoveryTests
{
    [Theory]
    [InlineData(.25)]
    [InlineData(.125)]
    public void SupportedPurkinjeCandidateCanResumeAfterInhibitionWithoutRestartPulse(double dt)
    {
        var id=StructureId.PurkinjeCellLayer;
        var cell=new ModelNeuron(0,"HH",StructureCircuitProfile.For(id));
        cell.SetSubstrateSupply(NeuralSubstrateSupply.ReferenceBath);
        var inhibition=new SpikeMessage { SourceStructure=id,TargetStructure=id,SourceInstanceKey="test",SourceNeuronId="test",
            Neurotransmitter=NTEnum.GABA,VesicleQuanta=1 };
        int initial=0,during=0,returned=0;
        for(int step=0;step<7000/dt;step++)
        {
            double time=step*dt;
            if(time>=1000 && time<2000 && time%10==0){inhibition.TimestampMs=time;cell.Integrate(inhibition,dt);}
            IntrinsicCircuitDriveTopology.Resolve(id,0,(long)Math.Ceiling(time+dt),out var excitation,out var inhibitory);
            cell.IntegratePacemakerDrive(excitation,inhibitory);
            if(cell.Step(dt)) { if(time<1000)initial++;if(time>=1000 && time<2000)during++;if(time>=6000)returned++; }
        }
        Assert.True(initial>0);Assert.True(during<initial);Assert.True(returned>0);
        Assert.InRange(Math.Abs(cell.Energy.BalanceError),0,1e-8);
    }
    [Fact]
    public void SupplySurvivesTickSerializationWithoutBecomingNeuromodulation()
    {
        var original=new TickSignal(2,20,1,new NeuromodState(),new Dictionary<BrainRhythm,double>(),0,
            NeuralSubstrates:new(.4,.8,.9,"body-test"));
        var restored=System.Text.Json.JsonSerializer.Deserialize<TickSignal>(System.Text.Json.JsonSerializer.Serialize(original))!;
        Assert.Equal(original.NeuralSubstrates,restored.NeuralSubstrates);
        Assert.Equal(0,restored.GlobalNeuromodState.DopamineLevel);
        Assert.Equal(0,restored.RewardPredictionError);
    }
    [Theory]
    [InlineData(.25)]
    [InlineData(.125)]
    public void SustainedActivityHasFiniteFuelCostAndSupplyCanBeWithdrawnAndRestored(double dt)
    {
        var cell = new NeuralEnergyBudget();
        void Run(double duration, NeuralSubstrateSupply supply)
        {
            for(int i=0;i<duration/dt;i++) cell.Advance(dt, i % (20/dt)==0 ? .0028 : 0, supply);
            Assert.InRange(Math.Abs(cell.BalanceError),0,1e-9);
            Assert.Equal(cell.Requested,cell.Consumed+cell.Unmet,8);
        }
        Run(10000,NeuralSubstrateSupply.ReferenceBath);
        Assert.InRange(cell.Reserve,.9,1);
        Assert.Equal(0,cell.Unmet);
        double before=cell.Produced;
        Run(10000,new(0,1,1,"test-no-glucose"));
        Assert.Equal(before,cell.Produced);
        Assert.Equal(0,cell.Reserve);
        Assert.True(cell.Unmet>0);
        Run(10000,NeuralSubstrateSupply.ReferenceBath);
        Assert.InRange(cell.Reserve,.9,1);
    }

    [Fact]
    public void OxygenWithdrawalRetainsOnlyLimitedGlycolysisAndNoPerfusionStopsProduction()
    {
        var oxygen = new NeuralEnergyBudget();var normal = new NeuralEnergyBudget();var stopped = new NeuralEnergyBudget();
        for(int i=0;i<10000;i++)
        {
            double cost=i%20==0?.0028:0;
            oxygen.Advance(1,cost,new(1,0,1,"anoxia"));
            normal.Advance(1,cost,NeuralSubstrateSupply.ReferenceBath);
            stopped.Advance(1,cost,new(1,1,0,"no-perfusion"));
        }
        Assert.True(oxygen.Produced>0 && oxygen.Produced<normal.Produced);
        Assert.Equal(0,stopped.Produced);
        Assert.True(oxygen.Reserve<normal.Reserve);
    }

    [Theory]
    [InlineData(StructureId.PurkinjeCellLayer)]
    [InlineData(StructureId.InferiorOlive)]
    public void HcnGatesRememberHyperpolarizationAndRelaxInPhysicalTime(StructureId id)
    {
        var profile=HcnChannelProfile.For(id);
        var coarse=new HodgkinHuxleyMembrane(-65,profile);var fine=new HodgkinHuxleyMembrane(-65,profile);
        double before=coarse.Q;
        coarse.AdvanceGates(-90,500);
        for(int i=0;i<5000;i++) fine.AdvanceGates(-90,.1);
        Assert.Equal(coarse.Q,fine.Q,10);
        Assert.True(coarse.Q>before && coarse.Q<profile.Equilibrium(-90));
        double activated=coarse.Q;
        coarse.AdvanceGates(-60,50);
        Assert.True(coarse.Q<activated && coarse.Q>profile.Equilibrium(-60));
        Assert.True(-profile.Conductance*activated*(-90-profile.Reversal)>0);
    }

    [Fact]
    public void BoundaryRejectsBadValuesAndDoesNotTreatStaleOrMissingFuelAsHealthy()
    {
        var boundary = new NeuralSupplyBoundary();
        Assert.Equal("missing-supply",boundary.Resolve(0,0).Source);
        boundary.Observe(NeuralSubstrateSupply.ReferenceBath,10,100);
        Assert.Equal(1,boundary.Resolve(1010,5100).OxygenAvailability);
        Assert.Equal("stale-body-supply",boundary.Resolve(1011,5100).Source);
        Assert.Equal(0,boundary.Resolve(10,5101).PerfusionAvailability);
        boundary.Observe(null,20,200);
        Assert.Equal("missing-supply",boundary.Resolve(20,200).Source);
        Assert.Throws<ArgumentException>(()=>boundary.Observe(new(double.NaN,1,1,"bad"),20,200));
    }

    [Theory]
    [InlineData(StructureId.PurkinjeCellLayer)]
    [InlineData(StructureId.InferiorOlive)]
    public void SlowInwardGatesFollowVoltageAndRetainHistory(StructureId id)
    {
        var profile=SlowInwardChannelProfile.For(id);
        var coarse=new HodgkinHuxleyMembrane(-60,slow:profile);
        var fine=new HodgkinHuxleyMembrane(-60,slow:profile);
        double initialP=coarse.P,initialL=coarse.L;
        coarse.AdvanceGates(-85,500);
        for(int i=0;i<5000;i++) fine.AdvanceGates(-85,.1);
        Assert.Equal(coarse.P,fine.P,10);Assert.Equal(coarse.L,fine.L,10);
        Assert.True(coarse.P<initialP);
        if(profile.Calcium) Assert.True(coarse.L>initialL);
        else Assert.Equal(1,coarse.L);
        var gate=profile.Activation(-55);
        double expected=gate.Equilibrium+(coarse.P-gate.Equilibrium)*Math.Exp(-.5/gate.Tau);
        coarse.AdvanceGates(-55,.5);
        Assert.Equal(expected,coarse.P,12);
    }

    [Fact]
    public void AvatarAndGeneratedUnityUseSameExplicitReducedSubstrates()
    {
        var avatar=AvatarWorldDynamics.EstimateNeuralSubstrates(new(200000,.25,.8),.49);
        var unity=Nre.UnityWorld.AvatarWorldDynamics.EstimateNeuralSubstrates(new(200000,.25,.8),.49);
        Assert.Equal(.5,avatar.GlucoseAvailability);
        Assert.Equal(.5,avatar.OxygenAvailability);
        Assert.Equal(.4,avatar.PerfusionAvailability);
        Assert.Equal(avatar.GlucoseAvailability,unity.GlucoseAvailability);
        Assert.Equal(avatar.OxygenAvailability,unity.OxygenAvailability);
        Assert.Equal(avatar.PerfusionAvailability,unity.PerfusionAvailability);
    }
}
