// Isolated flat-state transcription of OlivaryMembrane v1. The original is
// unchanged and remains the comparison reference. Arithmetic order, adaptive
// steps, endpoint crossing counts and operating bounds intentionally match.
internal readonly record struct OlivaryStepMetrics(long SomaticSpikes,long AxonalSpikes,
    double AdditionalIonicLoad,double SynapticCurrent,bool Fired);

internal static class OlivaryBatchKernel
{
    internal const int Count=14;
    const double MaximumInternalStepMs=OlivaryMembrane.MaximumInternalStepMs;
    const int Vs=0,Vd=1,Va=2,H=3,N=4,Xs=5,K=6,L=7,R=8,S=9,Q=10,Ca=11,Ha=12,Xa=13;
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

    internal static OlivaryStepMetrics Advance(Span<double> state, double dt, OlivaryParameters parameters,
        OlivaryInput input, double maximumInternalStepMs)
    {
        if(!double.IsFinite(dt)||dt<=0||dt>1||!double.IsFinite(maximumInternalStepMs)||maximumInternalStepMs<=0||maximumInternalStepMs>MaximumInternalStepMs)
            throw new ArgumentOutOfRangeException(nameof(dt));
        input.Validate();
        if(state.Length!=Count)throw new ArgumentException("Require fourteen state components.");
        Span<double> a=stackalloc double[Count],b=stackalloc double[Count],c=stackalloc double[Count],d=stackalloc double[Count],temp=stackalloc double[Count];
        double remaining=dt,load=0,synaptic=0;
        int axonal=0;long somatic=0;
        while(remaining>1e-12)
        {
            Slope(state,a,input,parameters,out var ionicLoad,out var synapticCurrent);
            double stiffness=Math.Max(1/PotassiumGate(state[Va]).Tau,1/PotassiumGate(state[Vs]).Tau);
            double ga=240*Cube(SodiumActivation(state[Va]))*state[Ha]+parameters.AxonalPotassium*Fourth(state[Xa])+1;
            double gs=150*Cube(SodiumActivation(state[Vs]))*state[H]+9*Fourth(state[N])+5*Fourth(state[Xs])+input.MetabolicPotassium+1;
            stiffness=Math.Max(stiffness,Math.Max(ga,gs));
            double voltageSlope=Math.Max(Math.Abs(a[Vs]),Math.Max(Math.Abs(a[Vd]),Math.Abs(a[Va])));
            double step=Math.Min(remaining,Math.Min(maximumInternalStepMs,Math.Min(.25/stiffness,2/Math.Max(1,voltageSlope))));
            if(!double.IsFinite(step)||step<1e-8&&remaining>=1e-8)throw new InvalidOperationException("Olivary input exceeds numerical operating range.");
            for(int i=0;i<Count;i++)temp[i]=state[i]+a[i]*step/2;
            Slope(temp,b,input,parameters,out _,out _);
            for(int i=0;i<Count;i++)temp[i]=state[i]+b[i]*step/2;
            Slope(temp,c,input,parameters,out _,out _);
            for(int i=0;i<Count;i++)temp[i]=state[i]+c[i]*step;
            Slope(temp,d,input,parameters,out _,out _);
            double oldSoma=state[Vs],oldAxon=state[Va];
            for(int i=0;i<Count;i++)
            {
                state[i]+=step*(a[i]+2*b[i]+2*c[i]+d[i])/6;
                if(!double.IsFinite(state[i]))throw new InvalidOperationException("Non-finite olivary state; no silent reset.");
                if(i>=3 && i!=Ca && (state[i]<-1e-8 || state[i]>1+1e-8))throw new InvalidOperationException("Olivary gate outside physical bounds.");
            }
            if(state[Ca]<0)throw new InvalidOperationException("Negative olivary calcium.");
            if(oldSoma<0 && state[Vs]>=0)somatic++;
            if(oldAxon<0 && state[Va]>=0){axonal++;}
            load+=ionicLoad*step;synaptic+=synapticCurrent*step;remaining-=step;
        }
        if(axonal>1)throw new InvalidOperationException("Outer step lost resolution of individual axonal spikes; reduce the integration interval.");
        return new(somatic,axonal,load/dt,synaptic/dt,axonal==1);
    }

    static void Slope(ReadOnlySpan<double> y,Span<double> f,OlivaryInput input,OlivaryParameters parameters,out double load,out double synaptic)
    {
        double vs=y[Vs],vd=y[Vd],va=y[Va];var p=parameters;
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
