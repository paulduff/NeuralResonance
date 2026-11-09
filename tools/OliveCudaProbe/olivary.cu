// Direct float64 transcription of the qualified OlivaryBatchKernel.
// NVRTC: no fast math, FMA contraction disabled, precise divide/sqrt, no FTZ.
// State uses SoA with eight guard doubles at each end. One thread owns one cell.
enum { Vs=0,Vd=1,Va=2,H=3,N=4,Xs=5,K=6,L=7,R=8,S=9,Q=10,Ca=11,Ha=12,Xa=13 };
struct Gate { double activation, tau; };
struct Parameters { double low, high, hcn, kca, ka; };
struct Input { double ampa, gabaD, gabaS, soma, dendrite, metabolic; };
__device__ double mn(double a,double b) { return isnan(a)||isnan(b) ? __longlong_as_double(0x7ff8000000000000ULL) : (a<b?a:b); }
__device__ double mx(double a,double b) { return isnan(a)||isnan(b) ? __longlong_as_double(0x7ff8000000000000ULL) : (a>b?a:b); }
__device__ double cube(double x) { return x*x*x; }
__device__ double fourth(double x) { return x*x*x*x; }
__device__ double trap(double x,double scale) { return fabs(x/scale)<1e-6 ? scale*(1+x/scale/2) : x/(1-exp(-x/scale)); }
__device__ double sodium(double v) { return 1/(1+exp((-30-v)/5.5)); }
__device__ double relax(double x,double target,double tau) { return (target-x)/tau; }
__device__ Gate potassium(double v) {
    double alpha=.13*trap(v+25,10), beta=1.69*exp(-.0125*(v+35));
    return {alpha/(alpha+beta),1/(alpha+beta)};
}
__device__ Gate high_calcium(double v) {
    double alpha=1.7/(1+exp(-(v-5)/13.9)), beta=.02*trap(-(v+8.5),5);
    return {alpha/(alpha+beta),5/(alpha+beta)};
}
__device__ void slope(const double* y,double* f,Input input,Parameters p,double& load,double& synaptic) {
    double vs=y[Vs],vd=y[Vd],va=y[Va];
    Gate xs=potassium(vs),xa=potassium(va),r=high_calcium(vd);
    double cat=p.low*cube(y[K])*y[L]*(vs-120);
    double cah=p.high*y[R]*y[R]*(vd-120);
    double ih=p.hcn*y[Q]*(vd+43);
    double exc=input.ampa*vd;
    double inhD=input.gabaD*(vd+75),inhS=input.gabaS*(vs+63);
    f[Vs]=-(cat+.13/.25*(vs-vd)+.13/.85*(vs-va)+150*cube(sodium(vs))*y[H]*(vs-55)
        +.016*(vs-10)+(9*fourth(y[N])+5*fourth(y[Xs])+input.metabolic)*(vs+75)+inhS)+input.soma;
    f[Vd]=-(cah+.13/.75*(vd-vs)+.016*(vd-10)+p.kca*y[S]*(vd+75)+ih+exc+inhD)+input.dendrite;
    f[Va]=-(p.ka*fourth(y[Xa])*(va+75)+.13/.15*(va-vs)+.016*(va-10)
        +240*cube(sodium(va))*y[Ha]*(va-55));
    f[H]=relax(y[H],1/(1+exp((vs+70)/5.8)),3*exp((-40-vs)/33));
    f[N]=relax(y[N],1/(1+exp((-3-vs)/10)),5+47*exp((50+vs)/900));
    f[Xs]=relax(y[Xs],xs.activation,xs.tau);
    f[K]=relax(y[K],1/(1+exp(-(vs+61)/4.2)),1);
    f[L]=relax(y[L],1/(1+exp((vs+85.5)/8.5)),20*exp((vs+160)/30)/(1+exp((vs+84)/7.3))+35);
    f[R]=relax(y[R],r.activation,r.tau);
    double calciumRate=mn(.00002*y[Ca],.01);
    f[S]=calciumRate*(1-y[S])-.015*y[S];
    f[Q]=relax(y[Q],1/(1+exp((vd+80)/4)),1/(exp(-.086*vd-14.6)+exp(.070*vd-1.87)));
    f[Ca]=-3*cah-.075*y[Ca];
    f[Ha]=relax(y[Ha],1/(1+exp((va+60)/5.8)),1.5*exp((-40-va)/33));
    f[Xa]=relax(y[Xa],xa.activation,xa.tau);
    const double area=1+3+.15/.85;
    load=(fabs(cat)+fabs(inhS)+3*(fabs(cah)+fabs(ih)+fabs(exc)+fabs(inhD)))/area;
    synaptic=-(inhS+3*(exc+inhD))/area;
}

extern "C" __global__ void advance_olive(const double* state,double* work,const double* parameter,
    const double* request,double* report,int count) {
    unsigned int lane=blockIdx.x*blockDim.x+threadIdx.x;
    if(lane >= (unsigned int)count) return;
    state+=8+lane; work+=8+lane; parameter+=8+lane; request+=8+lane; report+=8+lane;
    for(int i=0;i<5;++i) report[i*count]=0;
    if(request[0]==0) { for(int i=0;i<14;++i) work[i*count]=state[i*count]; return; }
    double dt=request[count], maximum=request[2*count];
    Input input={request[3*count],request[4*count],request[5*count],request[6*count],request[7*count],request[8*count]};
    Parameters p={parameter[0],parameter[count],parameter[2*count],parameter[3*count],parameter[4*count]};
    double y[14],a[14],b[14],c[14],d[14],temp[14];
    for(int i=0;i<14;++i) y[i]=state[i*count];
    double remaining=dt,load=0,synaptic=0;
    int axon=0,soma=0,iterations=0;
    while(remaining>1e-12) {
        // Explicit operating bound, never silently truncates the interval.
        if(++iterations>100000) {report[0]=7;return;}
        double ionicLoad,synapticCurrent,unusedLoad,unusedSynaptic;
        slope(y,a,input,p,ionicLoad,synapticCurrent);
        double stiffness=mx(1/potassium(y[Va]).tau,1/potassium(y[Vs]).tau);
        double ga=240*cube(sodium(y[Va]))*y[Ha]+p.ka*fourth(y[Xa])+1;
        double gs=150*cube(sodium(y[Vs]))*y[H]+9*fourth(y[N])+5*fourth(y[Xs])+input.metabolic+1;
        stiffness=mx(stiffness,mx(ga,gs));
        double voltageSlope=mx(fabs(a[Vs]),mx(fabs(a[Vd]),fabs(a[Va])));
        double step=mn(remaining,mn(maximum,mn(.25/stiffness,2/mx(1,voltageSlope))));
        if(!isfinite(step)||(step<1e-8&&remaining>=1e-8)) {report[0]=1;return;}
        for(int i=0;i<14;++i) temp[i]=y[i]+a[i]*step/2;
        slope(temp,b,input,p,unusedLoad,unusedSynaptic);
        for(int i=0;i<14;++i) temp[i]=y[i]+b[i]*step/2;
        slope(temp,c,input,p,unusedLoad,unusedSynaptic);
        for(int i=0;i<14;++i) temp[i]=y[i]+c[i]*step;
        slope(temp,d,input,p,unusedLoad,unusedSynaptic);
        double oldSoma=y[Vs],oldAxon=y[Va];
        for(int i=0;i<14;++i) {
            y[i]+=step*(a[i]+2*b[i]+2*c[i]+d[i])/6;
            if(!isfinite(y[i])) {report[0]=2;return;}
            if(i>=3&&i!=Ca&&(y[i]<-1e-8||y[i]>1+1e-8)) {report[0]=3;return;}
        }
        if(y[Ca]<0) {report[0]=4;return;}
        if(oldSoma<0&&y[Vs]>=0) ++soma;
        if(oldAxon<0&&y[Va]>=0) ++axon;
        load+=ionicLoad*step; synaptic+=synapticCurrent*step; remaining-=step;
    }
    if(axon>1) {report[0]=5;return;}
    if(!isfinite(load/dt)||load<0||!isfinite(synaptic/dt)) {report[0]=6;return;}
    for(int i=0;i<14;++i) work[i*count]=y[i];
    report[count]=soma; report[2*count]=axon; report[3*count]=load/dt; report[4*count]=synaptic/dt;
}
