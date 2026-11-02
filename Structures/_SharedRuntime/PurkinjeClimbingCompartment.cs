using NeuralResonanceEngine.Protocol;

// Reduced receiving dendritic compartment for the dense climbing-fibre contact
// field. Its 0.5/1.2 ms AMPA kinetics follow the De Schutter/Bower reference;
// conductance density and soma coupling are provisional effective parameters.
// It produces voltage/current, never a forced spike or a calcium/weight pulse.
internal sealed class PurkinjeClimbingCompartment(double initialVoltageMv)
{
    internal const string Version="purkinje-climbing-conductance-v1";
    sealed class Receptor { internal double Bound,Open; }
    readonly Dictionary<Guid,Receptor> contacts=[];
    internal double VoltageMv { get; private set; }=initialVoltageMv;
    internal double MeanAxialCurrent { get; private set; }
    internal double MeanAbsoluteCurrent { get; private set; }
    internal void Release(SpikeMessage message)
    {
        var id=StableNeuralIdentity.Contact(message,"local-purkinje-compartment",0);
        if(!contacts.TryGetValue(id,out var r))contacts.Add(id,r=new());
        r.Bound+=(1-r.Bound)*(1-Math.Exp(-message.VesicleQuanta));
    }
    internal void Advance(double dtMs,double receivingDendriteMv)
    {
        int n=(int)Math.Ceiling(dtMs/.125);double h=dtMs/n,axial=0,absolute=0;
        for(int i=0;i<n;i++)
        {
            double g=0;
            foreach(var r in contacts.Values)
            {
                double decay=Math.Exp(-h/1.2),rise=Math.Exp(-h/.5);
                r.Open=r.Open*rise+r.Bound*1.2/.7*(decay-rise);r.Bound*=decay;
                g+=4*r.Open;
            }
            double total=.2+g,target=.2*receivingDendriteMv/total;
            double next=target+(VoltageMv-target)*Math.Exp(-total*h);
            double middle=(VoltageMv+next)*.5;
            axial+=.2*(middle-receivingDendriteMv)*h;
            absolute+=(Math.Abs(g*middle)+Math.Abs(.2*(middle-receivingDendriteMv)))*h;
            VoltageMv=next;
        }
        MeanAxialCurrent=axial/dtMs;MeanAbsoluteCurrent=absolute/dtMs;
        if(!double.IsFinite(VoltageMv+MeanAxialCurrent))throw new InvalidOperationException("Invalid climbing compartment voltage.");
    }
}
