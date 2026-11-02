using NeuralResonanceEngine.Protocol;
using NeuralResonanceEngine.Shared.Contracts;

namespace NeuralResonanceEngine.DNNE.Tests;

public sealed class OlivaryCalibrationTests
{
    [Fact]
    public void RemovableRateSingularitiesHaveFiniteContinuousLimits()
    {
        foreach(double v in new[]{-25.0,-8.5})
        {
            var k=OlivaryMembrane.PotassiumGate(v);var ca=OlivaryMembrane.HighCalciumGate(v);
            Assert.InRange(k.Activation,0,1);Assert.True(k.Tau>0&&double.IsFinite(k.Tau));
            Assert.InRange(ca.Activation,0,1);Assert.True(ca.Tau>0&&double.IsFinite(ca.Tau));
            Assert.InRange(Math.Abs(k.Activation-OlivaryMembrane.PotassiumGate(v+1e-7).Activation),0,1e-7);
            Assert.InRange(Math.Abs(ca.Activation-OlivaryMembrane.HighCalciumGate(v+1e-7).Activation),0,1e-7);
        }
    }
    [Fact]
    public void RestingOscillationIsSubthresholdAndNotImposedByPacemaker()
    {
        var cell=new ModelNeuron(0,"HH",StructureCircuitProfile.For(StructureId.InferiorOlive));
        var control=new ModelNeuron(0,"HH",StructureCircuitProfile.For(StructureId.InferiorOlive));
        double low=0,high=-100;
        for(int i=0;i<12000;i++)
        {
            IntrinsicCircuitDriveTopology.Resolve(StructureId.InferiorOlive,0,i,out var e,out var h);
            Assert.Equal(0,e);Assert.Equal(0,h);
            cell.IntegratePacemakerDrive(1,1);cell.IntegrateIntrinsicDrive(1,1);
            cell.Step(.25);control.Step(.25);
            if(i>8000){low=Math.Min(low,cell.Olive!.SomaVoltage);high=Math.Max(high,cell.Olive!.SomaVoltage);}
        }
        Assert.Equal(control.Olive!.Snapshot(),cell.Olive!.Snapshot());
        Assert.InRange(cell.Olive.AxonalSpikes,0,6);
        Assert.InRange(high-low,1,30);
        Assert.InRange(cell.Energy.Reserve,.95,1);Assert.InRange(Math.Abs(cell.Energy.BalanceError),0,1e-8);
    }
    [Fact]
    public void CurrentClampCanEvokeUnresetActionPotentialsAndConverges()
    {
        var coarse=new OlivaryMembrane();var fine=new OlivaryMembrane();
        double maxError=0,maxVoltage=-100;
        for(int i=0;i<16000;i++)
        {
            var input=new OlivaryInput(DendriticCurrent:i>=8000&&i<8400?8:0);
            coarse.Advance(.125,input);
            fine.Advance(.0625,input,maximumInternalStepMs:.0125);fine.Advance(.0625,input,maximumInternalStepMs:.0125);
            maxError=Math.Max(maxError,Math.Abs(coarse.SomaVoltage-fine.SomaVoltage));maxVoltage=Math.Max(maxVoltage,coarse.SomaVoltage);
        }
        Assert.True(coarse.AxonalSpikes>0,$"No axonal spikes; soma max {maxVoltage}, soma spikes {coarse.SomaticSpikes}");
        Assert.Equal(fine.AxonalSpikes,coarse.AxonalSpikes);Assert.Equal(fine.SomaticSpikes,coarse.SomaticSpikes);
        Assert.InRange(maxError,0,.2);
    }
    [Fact]
    public void ContactsSumIndependentlyAndReleaseDurationUsesPhysicalTime()
    {
        var single=new OlivarySynapticContacts();var pair=new OlivarySynapticContacts();
        var a=new SpikeMessage{SourceInstanceKey="source",SourceNeuronId="a",Neurotransmitter=NTEnum.GLUTAMATE};
        var b=new SpikeMessage{SourceInstanceKey="source",SourceNeuronId="b",Neurotransmitter=NTEnum.GLUTAMATE};
        single.Release(a,1);pair.Release(a,1);pair.Release(b,1);
        single.Advance(.5);pair.Advance(.25);pair.Advance(.25);
        Assert.Equal(1,single.Count);Assert.Equal(2,pair.Count);
        Assert.Equal(single.Conductances.DendriticAmpa*2,pair.Conductances.DendriticAmpa,10);
        var fine=new OlivarySynapses();var coarse=new OlivarySynapses();
        fine.Release(false,1);coarse.Release(false,1);
        for(int i=0;i<40;i++){coarse.Advance(.5);for(int j=0;j<4;j++)fine.Advance(.125);}
        Assert.Equal(coarse.FastGaba,fine.FastGaba,10);Assert.Equal(coarse.SlowGaba,fine.SlowGaba,8);
        Assert.True(coarse.SlowGaba>coarse.FastGaba);
        // Legacy projection fallback must agree with the contact-learning key.
        a.SynapseId=Guid.NewGuid();pair.Release(a,1);
        a.SynapseId=Guid.NewGuid();pair.Release(a,1);
        Assert.Equal(4,pair.Count);
    }
    [Fact]
    public void MalformedInputsFailRatherThanSilentlyResettingTheCell()
    {
        var membrane=new OlivaryMembrane();
        Assert.Throws<ArgumentOutOfRangeException>(()=>membrane.Advance(2));
        Assert.Throws<ArgumentOutOfRangeException>(()=>membrane.Advance(.25,new(DendriticAmpa:-1)));
        Assert.Throws<ArgumentOutOfRangeException>(()=>new OlivaryMembrane(new(Hcn:double.NaN)));
    }
    [Theory]
    [InlineData(1)]
    [InlineData(.25)]
    [InlineData(.125)]
    public void RealReceptorInputProducesCountedAxonalBurstsWithConservedEnergy(double dt)
    {
        var cell=new ModelNeuron(0,"HH",StructureCircuitProfile.For(StructureId.InferiorOlive));
        int outgoing=0;
        for(int step=0;step<3000/dt;step++)
        {
            double time=step*dt;
            if(time>=1000&&time<2000&&time%50==0)
                for(int contact=0;contact<4;contact++)cell.Integrate(new SpikeMessage{
                    SourceInstanceKey="test",SourceNeuronId="source-"+contact,Neurotransmitter=NTEnum.GLUTAMATE,VesicleQuanta=1},dt);
            if(cell.Step(dt))outgoing++;
        }
        Assert.True(cell.Olive!.SomaticSpikes>0);
        Assert.True(cell.Olive.AxonalSpikes>cell.Olive.SomaticSpikes);
        Assert.Equal(cell.Olive.AxonalSpikes,outgoing);
        Assert.Equal(0,cell.Energy.Unmet);Assert.InRange(Math.Abs(cell.Energy.BalanceError),0,1e-8);
    }
    [Fact]
    public async Task HostPacketIsSubdividedToRespectTheMembraneEventInterval()
    {
        string? oldDir=Environment.GetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR"),oldInstance=Environment.GetEnvironmentVariable("SERVICE_INSTANCE");
        try
        {
            Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR",Path.Combine(Path.GetTempPath(),"nre-olive-test-"+Guid.NewGuid().ToString("N")));
            var profile=new StructureProfile(StructureId.InferiorOlive,"HH","STDP","isolated packet test",new DelayWindow(0,0));
            Environment.SetEnvironmentVariable("SERVICE_INSTANCE","packet");using var packet=new StructureEngine(profile);
            Environment.SetEnvironmentVariable("SERVICE_INSTANCE","individual");using var individual=new StructureEngine(profile);
            var signal=new TickSignal(16,16,16,new NeuromodState(),new Dictionary<BrainRhythm,double>(),0,NeuralSubstrates:NeuralSubstrateSupply.ReferenceBath);
            await packet.ProcessStepAsync(signal,0);
            for(int i=1;i<=16;i++)await individual.ProcessStepAsync(signal with{Tick=i,TimestampMs=i,TickDurationMs=1},0);
            var first=BiologicalContactLearningTests.Cells(packet);var second=BiologicalContactLearningTests.Cells(individual);
            Assert.Equal(first.Select(c=>c.Olive!.SomaVoltage),second.Select(c=>c.Olive!.SomaVoltage));
            Assert.Equal(first.Select(c=>c.Energy.Reserve),second.Select(c=>c.Energy.Reserve));
        }
        finally
        {
            Environment.SetEnvironmentVariable("NRE_SYNAPSE_STATE_DIR",oldDir);
            Environment.SetEnvironmentVariable("SERVICE_INSTANCE",oldInstance);
        }
    }
    [Fact]
    public void FastReceptorDriveDoesNotAliasAcrossHostStepSizes()
    {
        var a=new ModelNeuron(0,"HH",StructureCircuitProfile.For(StructureId.InferiorOlive));
        var b=new ModelNeuron(0,"HH",StructureCircuitProfile.For(StructureId.InferiorOlive));
        for(int time=0;time<3000;time++)
        {
            if(time>=1000&&time<2000&&time%50==0)
                for(int contact=0;contact<4;contact++)
                {
                    var spike=new SpikeMessage{SourceInstanceKey="test",SourceNeuronId="source-"+contact,Neurotransmitter=NTEnum.GLUTAMATE,VesicleQuanta=1};
                    a.Integrate(spike,1);b.Integrate(spike,.125);
                }
            a.Step(1);for(int sub=0;sub<8;sub++)b.Step(.125);
            Assert.Equal(a.Olive!.SomaVoltage,b.Olive!.SomaVoltage,10);
        }
        Assert.True(a.Olive!.AxonalSpikes>0);
        Assert.Equal(a.Olive.SomaticSpikes,b.Olive!.SomaticSpikes);Assert.Equal(a.Olive.AxonalSpikes,b.Olive.AxonalSpikes);
        Assert.Equal(a.Energy.Reserve,b.Energy.Reserve,10);
    }
    [Fact]
    public void CoupledReceptorAndMembraneSolutionConvergesWithInnerStepRefinement()
    {
        var a=new OlivaryMembrane();var b=new OlivaryMembrane();
        var sa=new OlivarySynapticContacts();var sb=new OlivarySynapticContacts();
        double error=0;
        for(int step=0;step<24000;step++)
        {
            double time=step*.125;
            if(time>=1000&&time<2000&&time%50==0)
                for(int contact=0;contact<4;contact++)
                {
                    var spike=new SpikeMessage{SourceInstanceKey="test",SourceNeuronId="source-"+contact,Neurotransmitter=NTEnum.GLUTAMATE};
                    sa.Release(spike,1);sb.Release(spike,1);
                }
            OlivaryIntegration.Advance(.125,a,sa,null,NeuralSubstrateSupply.ReferenceBath);
            OlivaryIntegration.Advance(.125,b,sb,null,NeuralSubstrateSupply.ReferenceBath,.0125);
            error=Math.Max(error,Math.Abs(a.SomaVoltage-b.SomaVoltage));
        }
        Assert.True(a.AxonalSpikes>0);Assert.Equal(a.AxonalSpikes,b.AxonalSpikes);Assert.Equal(a.SomaticSpikes,b.SomaticSpikes);
        Assert.InRange(error,0,.5);
    }
}
