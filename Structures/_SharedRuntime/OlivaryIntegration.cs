using NeuralResonanceEngine.Shared.Contracts;

internal static class OlivaryIntegration
{
    // AMPA rises in 0.18 ms. Holding its conductance for a 1 ms host interval
    // aliases the input even when the membrane solver itself takes tiny steps.
    internal const double MaximumSynapticStepMs=.025;
    public static (bool Fired,double MeanSynapticCurrent) Advance(double dt,OlivaryMembrane membrane,
        OlivarySynapticContacts synapses,NeuralEnergyBudget? energy,NeuralSubstrateSupply supply,
        double maximumSynapticStepMs=MaximumSynapticStepMs)
    {
        if(!double.IsFinite(dt)||dt<=0||dt>1||!double.IsFinite(maximumSynapticStepMs)||maximumSynapticStepMs<=0||maximumSynapticStepMs>MaximumSynapticStepMs)
            throw new ArgumentOutOfRangeException(nameof(dt));
        int count=(int)Math.Ceiling(dt/maximumSynapticStepMs);
        double step=dt/count,current=0;
        long before=membrane.AxonalSpikes;
        for(int i=0;i<count;i++)
        {
            synapses.Advance(step/2);
            bool fired=membrane.Advance(step,synapses.Conductances with {MetabolicPotassium=energy?.PotassiumConductance??0});
            synapses.Advance(step/2);
            current+=membrane.SynapticCurrent*step;
            energy?.Advance(step,(fired?.0028:0)+membrane.AdditionalIonicLoad*step*.000002,supply);
        }
        long spikes=membrane.AxonalSpikes-before;
        if(spikes>1)throw new InvalidOperationException("Host interval contains multiple olivary axonal spikes; reduce the outer integration step.");
        return (spikes==1,current/dt);
    }
}
