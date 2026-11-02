using NeuralResonanceEngine.Protocol;

// Loyola et al. 2023 synaptic occupancy equations, independently implemented.
// Events release transmitter for 1 ms; quanta scales release, not voltage.
internal sealed class OlivarySynapses
{
    readonly List<(double End,double Ampa,double Gaba)> _pulses = new();
    double _time;
    public double Ampa { get; private set; }
    public double FastGaba { get; private set; }
    public double SlowGaba { get; private set; }
    public double SynapticCalciumMillimolar { get; private set; } = 50e-9;
    public OlivaryInput Conductances => new(.12*Ampa,.1*FastGaba,.05*SlowGaba);

    public void Release(bool glutamate,double quanta)
    {
        if(!double.IsFinite(quanta)||quanta<0)throw new ArgumentOutOfRangeException(nameof(quanta));
        _pulses.Add((_time+1,glutamate?quanta:0,glutamate?0:quanta));
    }
    static double Gate(double value,double opening,double closing,double dt)
    {
        double rate=opening+closing,target=opening/rate;
        return target+(value-target)*Math.Exp(-rate*dt);
    }
    static double CalciumSlope(double calcium,double stimulus) =>
        (-(calcium-50e-9)/.267272442329587+.00192493295896767*Math.Log(.002/calcium)*stimulus)/1000;

    public void Advance(double dt)
    {
        if(!double.IsFinite(dt)||dt<=0||dt>1)throw new ArgumentOutOfRangeException(nameof(dt));
        double end=_time+dt;
        while(end-_time>1e-10)
        {
            _pulses.RemoveAll(p=>p.End<=_time+1e-10);
            double ampa=0,gaba=0,step=Math.Min(.0125,end-_time);
            foreach(var pulse in _pulses){ampa+=pulse.Ampa;gaba+=pulse.Gaba;step=Math.Min(step,pulse.End-_time);}
            // Calcium is positive; adaptive relative change protects its logarithm
            // near the tiny published baseline without clamping the solution.
            double ca=SynapticCalciumMillimolar,a=CalciumSlope(ca,gaba);
            step=Math.Min(step,.1*ca/Math.Max(1e-30,Math.Abs(a)));
            double b=CalciumSlope(ca+a*step/2,gaba),c=CalciumSlope(ca+b*step/2,gaba),d=CalciumSlope(ca+c*step,gaba);
            double next=ca+step*(a+2*b+2*c+d)/6;
            if(!double.IsFinite(next)||next<=0)throw new InvalidOperationException("Invalid olivary synaptic calcium.");
            double closing=1/(37+32/(1+Math.Exp(-((ca+next)/2-6.06975598757552e-5)/9.823374231158e-7)));
            Ampa=Gate(Ampa,ampa/.18,1/1.8,step);
            FastGaba=Gate(FastGaba,gaba/3,1.0/10,step);
            SlowGaba=Gate(SlowGaba,gaba/3,closing,step);
            SynapticCalciumMillimolar=next;_time+=step;
        }
        _time=end;
    }
}

// Occupancy saturates at each contact, not across the whole cell. Distinct
// connected sources contribute conductance independently, retaining identity.
internal sealed class OlivarySynapticContacts
{
    readonly record struct Contact(Guid Projection,string Instance,StructureId Structure,string Cell,NTEnum Nt,bool Feedback,bool Synthetic);
    readonly Dictionary<Contact,OlivarySynapses> _contacts = new();
    public int Count => _contacts.Count;
    public OlivaryInput Conductances { get; private set; }
    public void Release(SpikeMessage spike,double quanta)
    {
        var key=new Contact(spike.ProjectionId==Guid.Empty?spike.SynapseId:spike.ProjectionId,spike.SourceInstanceKey,spike.SourceStructure,spike.SourceNeuronId,
            spike.Neurotransmitter,spike.IsFeedback,spike.IsSyntheticBackground);
        if(!_contacts.TryGetValue(key,out var synapse))_contacts[key]=synapse=new();
        synapse.Release(spike.Neurotransmitter==NTEnum.GLUTAMATE,quanta);
    }
    public void Advance(double dt)
    {
        double ampa=0,dendGaba=0,somaGaba=0;
        foreach(var contact in _contacts.Values)
        {
            contact.Advance(dt);var g=contact.Conductances;
            ampa+=g.DendriticAmpa;dendGaba+=g.DendriticGaba;somaGaba+=g.SomaticGaba;
        }
        Conductances=new(ampa,dendGaba,somaGaba);
    }
}
