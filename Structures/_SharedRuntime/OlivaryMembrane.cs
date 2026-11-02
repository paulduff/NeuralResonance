// Independent implementation of the three-compartment equations of de Gruijl
// et al. 2012 (doi:10.1371/journal.pcbi.1002814), with the parameter set used by
// Loyola et al. 2023 Figure 7 / OliveTree's vanilla cell. See the calibration doc.
// Units: mV, ms, uA/cm^2, mS/cm^2, uF/cm^2; model calcium in uM.
// This is an isolated generalized olivary cell, not a fitted human IO network.
internal sealed class OlivaryMembrane
{
    public const string Version = "olivary-three-compartment-v1";
    public const double MaximumInternalStepMs = .025;
    const int Vs=0,Vd=1,Va=2,H=3,N=4,Xs=5,K=6,L=7,R=8,S=9,Q=10,Ca=11,Ha=12,Xa=13,Count=14;
    readonly double[] _state = [-60,-60,-60,.3596066,.2369847,.1,.7423159,.0321349,.0112788,.0049291,.0337836,3.7152,.9,.2369847];
    public OlivaryParameters Parameters { get; }
    public double SomaVoltage => _state[Vs];
    public double DendriteVoltage => _state[Vd];
    public double AxonVoltage => _state[Va];
    public double CalciumMicromolar => _state[Ca];
    public double HcnActivation => _state[Q];
    public double LowCalciumActivation => _state[K];
    public double LowCalciumAvailability => _state[L];
    public double CalciumPotassiumActivation => _state[S];
    public long SomaticSpikes { get; private set; }
    public long AxonalSpikes { get; private set; }
    public double AdditionalIonicLoad { get; private set; }
    public double SynapticCurrent { get; private set; }
    public double[] Snapshot() => (double[])_state.Clone();
    public OlivaryMembrane(OlivaryParameters? parameters = null)
    {
        Parameters=parameters ?? new();
        Parameters.Validate();
    }

    // Diagnostic voltage-clamp calculation; does not change a live cell.
    internal static (double Activation,double Tau) PotassiumGate(double v)
    {
        double alpha=.13*Trap(v+25,10),beta=1.69*Math.Exp(-.0125*(v+35));
        return (alpha/(alpha+beta),1/(alpha+beta));
    }
    internal static (double Activation,double Tau) HighCalciumGate(double v)
    {
        double alpha=1.7/(1+Math.Exp(-(v-5)/13.9)),beta=.02*Trap(-(v+8.5),5);
        return (alpha/(alpha+beta),5/(alpha+beta));
    }
    static double Trap(double x,double scale) => Math.Abs(x/scale)<1e-6
        ? scale*(1+x/scale/2) : x/(1-Math.Exp(-x/scale));
    static double Cube(double x)=>x*x*x;
    static double Fourth(double x)=>x*x*x*x;
    static double SodiumActivation(double v)=>1/(1+Math.Exp((-30-v)/5.5));
    static double Relax(double x,double target,double tau)=>(target-x)/tau;

    public bool Advance(double dt, OlivaryInput input = default, double maximumInternalStepMs = MaximumInternalStepMs)
    {
        if(!double.IsFinite(dt)||dt<=0||dt>1||!double.IsFinite(maximumInternalStepMs)||maximumInternalStepMs<=0||maximumInternalStepMs>MaximumInternalStepMs)
            throw new ArgumentOutOfRangeException(nameof(dt));
        input.Validate();
        Span<double> a=stackalloc double[Count],b=stackalloc double[Count],c=stackalloc double[Count],d=stackalloc double[Count],temp=stackalloc double[Count];
        double remaining=dt,load=0,synaptic=0;
        int axonal=0;
        while(remaining>1e-12)
        {
            Slope(_state,a,input,out var ionicLoad,out var synapticCurrent);
            double stiffness=Math.Max(1/PotassiumGate(_state[Va]).Tau,1/PotassiumGate(_state[Vs]).Tau);
            double ga=240*Cube(SodiumActivation(_state[Va]))*_state[Ha]+Parameters.AxonalPotassium*Fourth(_state[Xa])+1;
            double gs=150*Cube(SodiumActivation(_state[Vs]))*_state[H]+9*Fourth(_state[N])+5*Fourth(_state[Xs])+input.MetabolicPotassium+1;
            stiffness=Math.Max(stiffness,Math.Max(ga,gs));
            double voltageSlope=Math.Max(Math.Abs(a[Vs]),Math.Max(Math.Abs(a[Vd]),Math.Abs(a[Va])));
            double step=Math.Min(remaining,Math.Min(maximumInternalStepMs,Math.Min(.25/stiffness,2/Math.Max(1,voltageSlope))));
            if(!double.IsFinite(step)||step<1e-8&&remaining>=1e-8)throw new InvalidOperationException("Olivary input exceeds numerical operating range.");
            for(int i=0;i<Count;i++)temp[i]=_state[i]+a[i]*step/2;
            Slope(temp,b,input,out _,out _);
            for(int i=0;i<Count;i++)temp[i]=_state[i]+b[i]*step/2;
            Slope(temp,c,input,out _,out _);
            for(int i=0;i<Count;i++)temp[i]=_state[i]+c[i]*step;
            Slope(temp,d,input,out _,out _);
            double oldSoma=_state[Vs],oldAxon=_state[Va];
            for(int i=0;i<Count;i++)
            {
                _state[i]+=step*(a[i]+2*b[i]+2*c[i]+d[i])/6;
                if(!double.IsFinite(_state[i]))throw new InvalidOperationException("Non-finite olivary state; no silent reset.");
                if(i>=3 && i!=Ca && (_state[i]<-1e-8 || _state[i]>1+1e-8))throw new InvalidOperationException("Olivary gate outside physical bounds.");
            }
            if(_state[Ca]<0)throw new InvalidOperationException("Negative olivary calcium.");
            if(oldSoma<0 && _state[Vs]>=0)SomaticSpikes++;
            if(oldAxon<0 && _state[Va]>=0){AxonalSpikes++;axonal++;}
            load+=ionicLoad*step;synaptic+=synapticCurrent*step;remaining-=step;
        }
        if(axonal>1)throw new InvalidOperationException("Outer step lost resolution of individual axonal spikes; reduce the integration interval.");
        AdditionalIonicLoad=load/dt;SynapticCurrent=synaptic/dt;
        return axonal==1;
    }

    void Slope(ReadOnlySpan<double> y,Span<double> f,OlivaryInput input,out double load,out double synaptic)
    {
        double vs=y[Vs],vd=y[Vd],va=y[Va];var p=Parameters;
        var xs=PotassiumGate(vs);var xa=PotassiumGate(va);var r=HighCalciumGate(vd);
        double cat=p.LowCalcium*Cube(y[K])*y[L]*(vs-120);
        double cah=p.HighCalcium*y[R]*y[R]*(vd-120);
        double ih=p.Hcn*y[Q]*(vd+43);
        double exc=input.DendriticAmpa*vd;
        double inhD=input.DendriticGaba*(vd+75),inhS=input.SomaticGaba*(vs+63);
        f[Vs]=-(cat+.13/.25*(vs-vd)+.13/.85*(vs-va)+150*Cube(SodiumActivation(vs))*y[H]*(vs-55)
            +.016*(vs-10)+(9*Fourth(y[N])+5*Fourth(y[Xs])+input.MetabolicPotassium)*(vs+75)+inhS)+input.SomaticCurrent;
        f[Vd]=-(cah+.13/.75*(vd-vs)+.016*(vd-10)+p.CalciumPotassium*y[S]*(vd+75)+ih+exc+inhD)+input.DendriticCurrent;
        f[Va]=-(p.AxonalPotassium*Fourth(y[Xa])*(va+75)+.13/.15*(va-vs)+.016*(va-10)
            +240*Cube(SodiumActivation(va))*y[Ha]*(va-55));
        f[H]=Relax(y[H],1/(1+Math.Exp((vs+70)/5.8)),3*Math.Exp((-40-vs)/33));
        f[N]=Relax(y[N],1/(1+Math.Exp((-3-vs)/10)),5+47*Math.Exp((50+vs)/900));
        f[Xs]=Relax(y[Xs],xs.Activation,xs.Tau);
        f[K]=Relax(y[K],1/(1+Math.Exp(-(vs+61)/4.2)),1);
        f[L]=Relax(y[L],1/(1+Math.Exp((vs+85.5)/8.5)),20*Math.Exp((vs+160)/30)/(1+Math.Exp((vs+84)/7.3))+35);
        f[R]=Relax(y[R],r.Activation,r.Tau);
        double calciumRate=Math.Min(.00002*y[Ca],.01);
        f[S]=calciumRate*(1-y[S])-.015*y[S];
        f[Q]=Relax(y[Q],1/(1+Math.Exp((vd+80)/4)),1/(Math.Exp(-.086*vd-14.6)+Math.Exp(.070*vd-1.87)));
        f[Ca]=-3*cah-.075*y[Ca];
        f[Ha]=Relax(y[Ha],1/(1+Math.Exp((va+60)/5.8)),1.5*Math.Exp((-40-va)/33));
        f[Xa]=Relax(y[Xa],xa.Activation,xa.Tau);
        // Area ratios: dendrite/soma=3, axon/soma=.15/.85. Energy remains a
        // normalized proxy; Na/K spike costs are accounted separately.
        const double area=1+3+.15/.85;
        load=(Math.Abs(cat)+Math.Abs(inhS)+3*(Math.Abs(cah)+Math.Abs(ih)+Math.Abs(exc)+Math.Abs(inhD)))/area;
        synaptic=-(inhS+3*(exc+inhD))/area;
    }
}

internal sealed record OlivaryParameters(double LowCalcium=1.1,double HighCalcium=4.5,double Hcn=.12,double CalciumPotassium=35,double AxonalPotassium=240)
{
    public void Validate()
    {
        foreach(double value in new[]{LowCalcium,HighCalcium,Hcn,CalciumPotassium,AxonalPotassium})
            if(!double.IsFinite(value)||value<0)throw new ArgumentOutOfRangeException(nameof(value));
    }
}

internal readonly record struct OlivaryInput(double DendriticAmpa=0,double DendriticGaba=0,double SomaticGaba=0,
    double SomaticCurrent=0,double DendriticCurrent=0,double MetabolicPotassium=0)
{
    public void Validate()
    {
        if(!double.IsFinite(SomaticCurrent)||!double.IsFinite(DendriticCurrent))throw new ArgumentOutOfRangeException(nameof(SomaticCurrent));
        if(!double.IsFinite(DendriticAmpa)||DendriticAmpa<0||!double.IsFinite(DendriticGaba)||DendriticGaba<0||
            !double.IsFinite(SomaticGaba)||SomaticGaba<0||!double.IsFinite(MetabolicPotassium)||MetabolicPotassium<0)
            throw new ArgumentOutOfRangeException(nameof(DendriticAmpa));
    }
}
