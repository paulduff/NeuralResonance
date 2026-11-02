"""Independent membrane/receptor ODE reference. Diagnostic only; no metabolic K.

Same equations/initial conditions as the frozen DNNE candidate, independently
expressed here and advanced together by SciPy DOP853. No production solver calls.
Numba compiles scalar arithmetic without fast-math or persistent JIT caches.
"""
import argparse
from hashlib import sha256
import json
import math
from pathlib import Path
import time
import numpy as np
import scipy
from scipy.integrate import solve_ivp
import numba
from numba import njit

INITIAL=np.array([-60.,-60.,-60.,.3596066,.2369847,.1,.7423159,.0321349,.0112788,.0049291,.0337836,3.7152,.9,.2369847])
read=lambda p:json.loads(p.read_text(encoding="utf-8-sig"))
def save(p,v):p.write_text(json.dumps(v,indent=2,allow_nan=False)+"\n",encoding="utf-8")

@njit(cache=False)
def trap(x,scale):
    if abs(x/scale)<1e-6:return scale+x/2
    return x/(-np.expm1(-x/scale))

@njit(cache=False)
def rhs(t,y,release):
    f=np.zeros_like(y);vs,vd,va=y[0],y[1],y[2]
    ga=0.;gd=0.;gs=0.
    for j in range(release.shape[0]):
        i=14+j*4;amp,fast,slow,calcium=y[i:i+4]
        if calcium<=0:
            f[:]=np.nan
            return f  # reject an invalid trial step, never clamp/reset state
        ex,inh=release[j,0],release[j,1]
        f[i]=(ex/.18)*(1-amp)-amp/1.8
        f[i+1]=(inh/3)*(1-fast)-fast/10
        tau=37+32/(1+np.exp(-(calcium-6.06975598757552e-5)/9.823374231158e-7))
        f[i+2]=(inh/3)*(1-slow)-slow/tau
        f[i+3]=(-(calcium-50e-9)/.267272442329587+.00192493295896767*np.log(.002/calcium)*inh)/1000
        ga+=.12*amp;gd+=.1*fast;gs+=.05*slow
    cat=1.1*y[6]**3*y[7]*(vs-120)
    cah=4.5*y[8]**2*(vd-120)
    ina_s=150/(1+np.exp((-30-vs)/5.5))**3*y[3]*(vs-55)
    ina_a=240/(1+np.exp((-30-va)/5.5))**3*y[12]*(va-55)
    f[0]=-cat-.52*(vs-vd)-(.13/.85)*(vs-va)-ina_s-.016*(vs-10)-(9*y[4]**4+5*y[5]**4)*(vs+75)-gs*(vs+63)
    f[1]=-cah-(.13/.75)*(vd-vs)-.016*(vd-10)-35*y[9]*(vd+75)-.12*y[10]*(vd+43)-ga*vd-gd*(vd+75)
    f[2]=-240*y[13]**4*(va+75)-(.13/.15)*(va-vs)-.016*(va-10)-ina_a
    f[3]=(1/(1+np.exp((vs+70)/5.8))-y[3])/(3*np.exp((-40-vs)/33))
    f[4]=(1/(1+np.exp((-3-vs)/10))-y[4])/(5+47*np.exp((50+vs)/900))
    for v,index in ((vs,5),(va,13)):
        alpha=.13*trap(v+25,10);beta=1.69*np.exp(-.0125*(v+35))
        f[index]=alpha*(1-y[index])-beta*y[index]
    f[6]=1/(1+np.exp(-(vs+61)/4.2))-y[6]
    f[7]=(1/(1+np.exp((vs+85.5)/8.5))-y[7])/(20*np.exp((vs+160)/30)/(1+np.exp((vs+84)/7.3))+35)
    alpha=1.7/(1+np.exp(-(vd-5)/13.9));beta=.02*trap(-(vd+8.5),5)
    f[8]=(alpha*(1-y[8])-beta*y[8])/5
    f[9]=min(.00002*y[11],.01)*(1-y[9])-.015*y[9]
    f[10]=(1/(1+np.exp((vd+80)/4))-y[10])*(np.exp(-.086*vd-14.6)+np.exp(.070*vd-1.87))
    f[11]=-3*cah-.075*y[11]
    f[12]=(1/(1+np.exp((va+60)/5.8))-y[12])/(1.5*np.exp((-40-va)/33))
    return f

def identity(m):
    return tuple(m[k] for k in ("projectionId","sourceInstanceKey","sourceStructure","sourceNeuronId","neurotransmitter","isFeedback","isSyntheticBackground"))

def source_system(source,duration,perturb):
    inputs=[x for x in source["inputs"] if x["arrivalMs"]<duration]
    keys=sorted({identity(x["message"]) for x in inputs});index={k:i for i,k in enumerate(keys)}
    y=np.concatenate((INITIAL.copy(),np.tile([0.,0.,0.,50e-9],len(keys))));y[0]+=perturb
    changes={0.:[],float(duration):[]}
    for x in inputs:
        j=index[identity(x["message"])];column=0 if x["message"]["neurotransmitter"]=="GLUTAMATE" else 1
        assert x["message"]["neurotransmitter"] in ("GLUTAMATE","GABA")
        at=x["arrivalMs"];q=x["message"]["vesicleQuanta"]
        changes.setdefault(at,[]).append((j,column,q))
        if at+1<duration:changes.setdefault(at+1,[]).append((j,column,-q))
    return inputs,keys,y,changes

def integrate(source,duration,rtol,perturb=0.):
    inputs,keys,y,changes=source_system(source,duration,perturb)
    release=np.zeros((len(keys),2));edges=sorted(changes);samples=[];spikes=[[],[]];nfev=0
    atol=np.full(len(y),rtol*1e-4);atol[:3]=rtol*.01;atol[11]=rtol*.001;atol[17::4]=rtol*1e-5
    def soma(t,y,r):return y[0]
    def axon(t,y,r):return y[2]
    soma.direction=axon.direction=1
    for start,end in zip(edges,edges[1:]):
        for j,column,q in changes[start]:release[j,column]+=q
        assert np.all(release>=0)
        ts=np.arange((math.floor(start/2)+1)*2,end+1e-10,2.)
        sol=solve_ivp(rhs,(start,end),y,args=(release,),method="DOP853",rtol=rtol,atol=atol,max_step=.125,
            t_eval=np.unique(np.append(ts,end)),events=(soma,axon))
        assert sol.success,sol.message
        assert np.isfinite(sol.y).all()
        assert np.all(sol.y[11]>=0) and np.all(sol.y[17::4]>0)
        gate_indices=[i for i in range(3,len(y)) if i!=11 and (i<14 or (i-14)%4!=3)]
        assert np.all(sol.y[gate_indices]>=-1e-8) and np.all(sol.y[gate_indices]<=1+1e-8)
        for channel,event_index in enumerate((0,2)):
            for t,state in zip(sol.t_events[channel],sol.y_events[channel]):
                if rhs(t,state,release)[event_index]>0 and (not spikes[channel] or abs(spikes[channel][-1]-t)>1e-8):spikes[channel].append(float(t))
        for t,state in zip(sol.t,sol.y.T):
            if abs(t/2-round(t/2))<1e-9:
                g=[sum(.12*state[14+4*j] for j in range(len(keys))),sum(.1*state[15+4*j] for j in range(len(keys))),sum(.05*state[16+4*j] for j in range(len(keys)))]
                samples.append([float(t),*state[:14].tolist(),*g])
        y=sol.y[:,-1];nfev+=sol.nfev
    assert len(samples)==duration//2
    return inputs,samples,spikes,nfev,y.tolist()

def self_test(fixtures,output):
    f=read(fixtures);assert f["completed"] and f["defaultStepperMatchesProduction"]
    np.testing.assert_array_equal(INITIAL,f["initialState"])
    for x in f["vectors"]:
        y=np.r_[x["state"],[.3,.2,.4,50e-9]];release=np.zeros((1,2))
        result=rhs(0.,y,release)
        np.testing.assert_allclose(result[:14],x["derivative"],rtol=2e-13,atol=1e-10)
        np.testing.assert_allclose(result,rhs.py_func(0.,y,release),rtol=2e-13,atol=1e-10)
    # Independent closed-form occupancy check, including both pulse and decay.
    y=np.r_[INITIAL,[0.,0.,0.,50e-9]]
    r=np.array([[1.,1.]])
    sol=solve_ivp(rhs,(0,1),y,args=(r,),method="DOP853",rtol=1e-10,atol=1e-13,max_step=.01)
    assert sol.success
    expected=(1/.18)/(1/.18+1/1.8)*(1-np.exp(-(1/.18+1/1.8)))
    assert abs(sol.y[14,-1]-expected)<1e-9
    assert abs(sol.y[15,-1]-(1/3)/(1/3+1/10)*(1-np.exp(-(1/3+1/10))))<1e-9
    assert sol.y[17,-1]>50e-9
    end=solve_ivp(rhs,(1,20),sol.y[:,-1],args=(np.zeros((1,2)),),method="DOP853",rtol=1e-10,atol=1e-13,max_step=.01)
    assert end.success and abs(end.y[14,-1]-expected*np.exp(-19/1.8))<1e-10
    save(output,dict(completed=True,derivativeVectors=4,compiledMatchesPython=True,analyticReceptorChecks=True,scipy=scipy.__version__,numpy=np.__version__,numba=numba.__version__))
    print("Reference derivatives agree with four production states; compiled/Python and analytic receptor checks pass.")

def main():
    p=argparse.ArgumentParser();p.add_argument("output",type=Path);p.add_argument("bundle",type=Path);p.add_argument("--smoke",action="store_true");p.add_argument("--self-test",type=Path);a=p.parse_args()
    if a.output.exists():raise ValueError("Use a new output path")
    if a.self_test:self_test(a.self_test,a.output);return
    a.output.mkdir(parents=True);bundle=read(a.bundle);duration=4000 if a.smoke else bundle["durationMs"]
    cases=[];save(a.output/"protocol.json",dict(study="independent-olive-reference",durationMs=duration,smoke=a.smoke,method="DOP853",maxStepMs=.125,
        relativeTolerances=[1e-7,1e-9,1e-11],perturbationsMv=[-.001,0,.001],energyEnabled=False,bundleSha256=sha256(a.bundle.read_bytes()).hexdigest(),
        referenceScope="Independent membrane and per-contact receptor ODEs only. No metabolic K or energy ledger; paired production energy-off cases only.",versions=dict(scipy=scipy.__version__,numpy=np.__version__,numba=numba.__version__)))
    try:
        for source in bundle["sourceCases"]:
            for rtol,perturb in ((1e-7,0.),(1e-9,0.),(1e-11,0.),(1e-11,-.001),(1e-11,.001)):
                key=f"reference-{source['id']}-r{rtol}-p{perturb}";directory=a.output/key;directory.mkdir()
                print(f"REFERENCE {len(cases)+1}/20: {key}",flush=True);started=time.monotonic()
                inputs,trace,spikes,nfev,state=integrate(source,duration,rtol,perturb)
                np.savetxt(directory/"trace.csv",np.asarray(trace),delimiter=',',header="timeMs,vs,vd,va,h,n,xs,k,l,r,s,q,ca,ha,xa,ampa,gabaD,gabaS",comments='',fmt='%.17g')
                summary=dict(key=key,kind="reference",sourceId=source["id"],duration=duration,rtol=rtol,perturbationMv=perturb,energyEnabled=False,inputs=len(inputs),
                    soma=len(spikes[0]),axon=len(spikes[1]),somaTimes=spikes[0],spikeTimes=spikes[1],nfev=nfev,finalState=state,elapsedSeconds=time.monotonic()-started)
                save(directory/"design.json",dict(inputs=inputs,rtol=rtol,perturbationMv=perturb));save(directory/"summary.json",summary);cases.append(summary)
        save(a.output/"results.json",dict(completed=True,physiologicallyQualified=False,cases=cases))
    except Exception as e:save(a.output/"FAILED.json",dict(error=repr(e),completedCases=len(cases)));raise

if __name__=="__main__":main()
