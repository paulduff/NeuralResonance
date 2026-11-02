using NeuralResonanceEngine.Shared.Contracts;

// Tool-only partition controls. The default path is tested against the production
// integration wrapper. No live host can select these diagnostic ceilings.
internal static class SourceNumericalStepper
{
    internal static void Advance(double dt,OlivaryMembrane membrane,OlivarySynapticContacts synapses,
        NeuralEnergyBudget? energy,double couplingStep,double membraneStep)
    {
        int count=(int)Math.Ceiling(dt/couplingStep);double h=dt/count;
        for(int i=0;i<count;i++)
        {
            synapses.Advance(h/2);
            bool fired=membrane.Advance(h,synapses.Conductances with {MetabolicPotassium=energy?.PotassiumConductance??0},membraneStep);
            synapses.Advance(h/2);
            energy?.Advance(h,(fired?.0028:0)+membrane.AdditionalIonicLoad*h*.000002,NeuralSubstrateSupply.ReferenceBath);
        }
    }
}
