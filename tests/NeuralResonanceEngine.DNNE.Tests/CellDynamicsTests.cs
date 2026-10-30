using System.Reflection;
using NeuralResonanceEngine.Protocol;

namespace NeuralResonanceEngine.DNNE.Tests;

[Collection(EnvironmentVariableTestCollection.Name)]
public sealed class CellDynamicsTests
{
    [Fact]
    public void ReferenceHhRestAndRateSingularitiesAreFinite()
    {
        var cell = new HodgkinHuxleyMembrane();
        Assert.InRange(cell.M, .0529, .0530);
        Assert.InRange(cell.H, .5960, .5962);
        Assert.InRange(cell.N, .3176, .3178);
        Assert.Equal(1, HodgkinHuxleyMembrane.Rates(-40).Am, 10);
        Assert.Equal(.1, HodgkinHuxleyMembrane.Rates(-55).An, 10);
        for (int i = 0; i < 100; i++) Assert.False(cell.Advance(1, 0));
        Assert.InRange(cell.Voltage, -65.1, -64.9);
    }

    [Fact]
    public void VoltageClampGatesRetainHistoryAndComposeAcrossTimeSteps()
    {
        var coarse = new HodgkinHuxleyMembrane();
        var fine = new HodgkinHuxleyMembrane();
        var equilibrated = new HodgkinHuxleyMembrane(-20);
        coarse.AdvanceGates(-20, .5);
        for (int i = 0; i < 20; i++) fine.AdvanceGates(-20, .025);
        Assert.Equal(coarse.M, fine.M, 12);
        Assert.Equal(coarse.H, fine.H, 12);
        Assert.Equal(coarse.N, fine.N, 12);
        Assert.True(coarse.H > equilibrated.H + .1, "Inactivation must evolve, not jump to equilibrium.");
        Assert.True(coarse.N < equilibrated.N - .1, "Delayed potassium activation must retain its history.");
    }

    [Fact]
    public void ReferenceHhActionPotentialRepolarizesWithoutResetAndMatchesFineReference()
    {
        var cell = new HodgkinHuxleyMembrane();
        double[] reference = [-65, cell.M, cell.H, cell.N];
        double peak = -65, troughAfterSpike = 0, error = 0;
        int spikes = 0;
        for (int i = 0; i < 4000; i++)
        {
            if (cell.Advance(.025, 10)) spikes++;
            for (int j = 0; j < 25; j++) reference = Rk4(reference, .001, 10);
            peak = Math.Max(peak, cell.Voltage);
            if (spikes > 0) troughAfterSpike = Math.Min(troughAfterSpike, cell.Voltage);
            error = Math.Max(error, Math.Abs(reference[0] - cell.Voltage));
        }
        Assert.InRange(spikes, 6, 8);
        Assert.InRange(peak, 30, 50);
        Assert.True(troughAfterSpike < -70);
        Assert.True(error < 1.5, $"Membrane differs from small-step reference by {error} mV.");
    }

    // Fine-step coupled ODE reference, with a separate array implementation.
    // This checks numerical resolution; the voltage-clamp tests check gate history.
    static double[] Rk4(double[] s, double dt, double current)
    {
        double[] F(double[] x)
        {
            var r = HodgkinHuxleyMembrane.Rates(x[0]);
            return [current-120*x[1]*x[1]*x[1]*x[2]*(x[0]-50)-36*Math.Pow(x[3],4)*(x[0]+77)-.3*(x[0]+54.387),
                r.Am*(1-x[1])-r.Bm*x[1], r.Ah*(1-x[2])-r.Bh*x[2], r.An*(1-x[3])-r.Bn*x[3]];
        }
        double[] At(double[] k, double fraction) => s.Select((v,i)=>v+dt*fraction*k[i]).ToArray();
        var a=F(s);var b=F(At(a,.5));var c=F(At(b,.5));var d=F(At(c,1));
        return s.Select((v,i)=>v+dt*(a[i]+2*b[i]+2*c[i]+d[i])/6).ToArray();
    }

    [Fact]
    public void CerebellarHhSpikesAreChangedByBothExcitationAndInhibition()
    {
        int Count(NTEnum? transmitter)
        {
            var cell = new ModelNeuron(17,"HH",StructureCircuitProfile.For(StructureId.CerebellarVermis));
            int count = 0;
            for (int tick=1;tick<=1000;tick++)
            {
                if (tick>200 && tick<=800 && tick%10==1 && transmitter is { } nt)
                    cell.Integrate(new SpikeMessage{Neurotransmitter=nt,VesicleQuanta=1},1);
                IntrinsicCircuitDriveTopology.Resolve(StructureId.CerebellarVermis,17,tick,out var ex,out var inh);
                cell.IntegratePacemakerDrive(ex,inh);
                if(cell.Step(1) && tick>200 && tick<=800)count++;
            }
            return count;
        }
        int baseline=Count(null), excited=Count(NTEnum.GLUTAMATE), inhibited=Count(NTEnum.GABA);
        Assert.True(baseline>0, "The provisional pacemaker must be observable.");
        Assert.True(excited>baseline, $"Excitation {excited} must change the baseline {baseline}.");
        Assert.True(inhibited<baseline, $"Inhibition {inhibited} must change the baseline {baseline}.");
    }

    [Fact]
    public void ContinuousGlialCleanupDependsOnElapsedTimeRatherThanCallCount()
    {
        var a=new ModelNeuron(0,"LIF",StructureCircuitProfile.For(StructureId.VestibularNuclei));
        var b=new ModelNeuron(0,"LIF",StructureCircuitProfile.For(StructureId.VestibularNuclei));
        var flags=BindingFlags.NonPublic|BindingFlags.Instance;
        var load=typeof(ModelNeuron).GetField("_astrocyteGlutamateLoad",flags)!;
        var current=typeof(ModelNeuron).GetField("_ampaCurrent",flags)!;
        var advance=typeof(ModelNeuron).GetMethod("AdvanceGlialSupport",flags)!;
        foreach(var cell in new[]{a,b}) {load.SetValue(cell,.7);current.SetValue(cell,10d);}
        advance.Invoke(a,[1d]);
        for(int i=0;i<4;i++)advance.Invoke(b,[.25]);
        Assert.InRange(Math.Abs((double)current.GetValue(a)!-(double)current.GetValue(b)!),0,.0001);
    }

    [Fact]
    public void MicrotubuleInputDoesNotAdvanceTimeAndSubMillisecondStepsConverge()
    {
        var old=Environment.GetEnvironmentVariable("NRE_MICROTUBULE_MODE");
        try
        {
            Environment.SetEnvironmentVariable("NRE_MICROTUBULE_MODE","classical");
            IntracellularMicrotubuleState Run(double dt)
            {
                var state=IntracellularMicrotubuleState.Create(0);
                state.ObserveSynapticInput(NTEnum.GLUTAMATE,1,null,dt);
                for(int step=0;step<100/dt;step++)state.Advance(dt,new NeuromodState(),5,5,0,false);
                return state;
            }
            var a=Run(1);var b=Run(.25);
            Assert.InRange(Math.Abs(a.SpineInvasionEligibility-b.SpineInvasionEligibility),0,.0001);
            Assert.InRange(Math.Abs(a.TransportSupport-b.TransportSupport),0,.0001);
        }
        finally {Environment.SetEnvironmentVariable("NRE_MICROTUBULE_MODE",old);}
    }
}
