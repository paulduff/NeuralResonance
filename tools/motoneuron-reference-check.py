"""Source-derived channel and passive-cable checks, for the visible test window.

No active-cell qualification. Read the pinned MOD/HOC numeric declarations;
compare clamp relaxation and a seven-node cable reduced independently by Schur
complement, then use matrix exponentials for the passive voltage reference.
"""
import json
from pathlib import Path
import re
import sys
import numpy as np
from scipy.linalg import expm

NUMBER=r'([+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?)'
ASSIGN=r'(?m)^\s*([\w.]+)\s*=\s*'+NUMBER
read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))


def numbers(text):return {k:float(v) for k,v in re.findall(ASSIGN,text)}


def model(reference):
    hoc=(reference/'FivecomptcMG_0.hoc').read_text()
    h=numbers(hoc)
    p={}
    for suffix in ('na3rp','naps','kdrRL','mAHP','gh','L_Ca_inact'):
        text=(reference/(suffix+'.mod')).read_text()
        p[suffix]=numbers(re.search(r'\bPARAMETER\s*\{(.*?)\}',text,re.S).group(1))
        for name in list(p[suffix]):
            if name+'_'+suffix in h:p[suffix][name]=h[name+'_'+suffix]
            if 'soma.'+name+'_'+suffix in h:p[suffix][name]=h['soma.'+name+'_'+suffix]
    dend=numbers(re.search(r'forsec dend\s*\{(.*?)\}',hoc,re.S).group(1))
    soma={key.removeprefix('soma.'):value for key,value in h.items() if key.startswith('soma.')}
    return p,soma,dend


def rates(v,ca,temp,availability,p):
    def trap(x,threshold,a,q):
        z=(x-threshold)/q
        return a*q if z==0 else a*(x-threshold)/(-np.expm1(-z))
    def slow(v,d):
        a=d['a0s']*np.exp((d['asvh']-v)/d['avs'])
        b=d['b0s']/(np.exp((d['bsvh']-v)/d['bvs'])+1)
        steady=a/(a+b)
        return steady+availability*(1-steady),1/(a+b)
    d=p['na3rp'];qt=d['q10']**((temp-24)/10)
    a=trap(v,d['tha']+d['sh'],d['Ra'],d['qa']);b=trap(-v,-d['tha']-d['sh'],d['Rb'],d['qa'])
    result=[(a/(a+b),max(d['mmin'],1/(a+b)/qt))]
    a=trap(v,d['thi1']+d['sh'],d['Rd'],d['qd']);b=trap(-v,-d['thi2']-d['sh'],d['Rg'],d['qg'])
    result.append((1/(1+np.exp((v-d['thinf']-d['sh'])/d['qinf'])),max(d['hmin'],1/(a+b)/qt)))
    result.append(slow(v,d))
    d=p['naps'];result.append((1/(1+np.exp(-(v+52.3-d['sh'])/d['vslope'])),d['mtau']))
    result.append(slow(v,d))
    d=p['kdrRL'];b=np.exp((v-d['tVh'])/d['tslp'])
    result.append((1/(1+np.exp(-(v-d['mVh'])/d['mslp'])),d['tmin']+d['taumax']*b/(1+b)**2))
    d=p['mAHP'];result.append((1/(1+np.exp(-(v-d['mvhalfca'])/d['mslpca'])),d['mtauca']))
    a=d['fKCa']*(1000*(ca-d['cainf']))**d['caix'];b=d['bKCa']
    result.append((a/(a+b),1/(a+b)))
    d=p['gh'];hcn=(1/(1+np.exp((v-d['half'])/d['slp'])),d['htau']);result.append(hcn)
    d=p['L_Ca_inact']
    result.extend([(1/(1+np.exp((v-d['theta_m'])/d['kappa_m'])),d['tau_m']),
                   (1/(1+np.exp((v-d['theta_h'])/d['kappa_h'])),d['tau_h']),hcn])
    return np.asarray(result)


def main(package):
    output=package/'motoneuron-reference';output.mkdir()
    report=dict(completed=False,channelCriteriaPassed=False,passiveCriteriaPassed=False,
        activeFiringQualified=False,physiologicalCalibrationComplete=False,avatarRecoveryAssessed=False,
        criteria=dict(gateAbsolute=1e-12,tauAbsoluteMs=1e-10,voltageAbsoluteMv=1e-7,chargeLedgerFc=1e-5),cases=[])
    try:
        p,soma,dend=model(package/'motoneuron-input')
        root=package/'motoneuron-study';clamps=read(root/'clamps.json');protocol=read(root/'protocol.json')
        assert protocol['sourceRevision']=='0d11354490fad5fcd940d016a8d69b000693fd81' and protocol['block']=='Passive'
        assert protocol['initialVoltageMv']==-71 and protocol['durationMs']==100 and protocol['sampleMs']==.25
        assert protocol['stepsMs']==[.0125,.00625,.003125] and protocol['currentsPa']==[-100,100,200]
        assert abs(protocol['somaticCalciumReversalMv']-12.5*np.log(40000))<=1e-12 and protocol['sodiumReversalMv']==50
        expected={(t,v,ca,a) for t in (24,37) for v in (-100,-80,-65,-44,-29,-20,0,40) for ca in (.0001,.0002,.001) for a in (0,1)}
        assert clamps['caseCount']==len(clamps['cases'])==len(expected)==96
        maximum_gate=maximum_tau=maximum_equilibrium=0.
        for case in clamps['cases']:
            key=tuple(case[n] for n in ('temperature','voltage','calcium','availability'));assert key in expected;expected.remove(key)
            actual=rates(case['voltage'],case['calcium'],case['temperature'],case['availability'],p)
            saved=np.asarray([[r['equilibrium'],r['tauMs']] for r in case['rates']])
            maximum_equilibrium=max(maximum_equilibrium,float(np.max(np.abs(actual[:,0]-saved[:,0]))))
            maximum_tau=max(maximum_tau,float(np.max(np.abs(actual[:,1]-saved[:,1]))))
            initial=rates(-65,.0001,case['temperature'],case['availability'],p)[:,0]
            assert [s['timeMs'] for s in case['samples']]==[0,.0125,.125,1,10,100,1000]
            for sample in case['samples']:
                reference=actual[:,0]+(initial-actual[:,0])*np.exp(-sample['timeMs']/actual[:,1])
                observed=np.asarray(sample['gates']);assert observed.shape==(12,) and np.isfinite(observed).all()
                assert np.min(observed)>=0 and np.max(observed)<=1
                maximum_gate=max(maximum_gate,float(np.max(np.abs(reference-observed))))
        report.update(clampCases=96,maximumGateError=maximum_gate,maximumEquilibriumError=maximum_equilibrium,maximumTauErrorMs=maximum_tau,
            channelCriteriaPassed=maximum_gate<=1e-12 and maximum_equilibrium<=1e-12 and maximum_tau<=1e-10)
        sections=[soma]+[dend]*4
        area=np.asarray([np.pi*s['diam']*s['L'] for s in sections]);cap=.01*area*np.asarray([s['cm'] for s in sections])
        leak=10*area*np.asarray([s['g_pas'] for s in sections]);rev=np.asarray([s['e_pas'] for s in sections])
        # Native half-section resistances, in ohms. Nodes 5/6 have no membrane;
        # derive their elimination by dense linear algebra rather than pair rules.
        conductance=np.asarray([1e9/(s['Ra']*(s['L']*.5*1e-4)/(np.pi*(s['diam']*.5*1e-4)**2)) for s in sections])
        laplacian=np.zeros((7,7))
        for a,b,g in ((0,5,conductance[0]),(0,6,conductance[0]),(1,5,conductance[1]),(2,5,conductance[2]),(3,6,conductance[3]),(4,6,conductance[4])):
            laplacian[a,a]+=g;laplacian[b,b]+=g;laplacian[a,b]-=g;laplacian[b,a]-=g
        reduced=laplacian[:5,:5]-laplacian[:5,5:]@np.linalg.solve(laplacian[5:,5:],laplacian[5:,:5])
        coupling=-reduced.copy();np.fill_diagonal(coupling,0)
        assert np.max(np.abs(area-protocol['areasUm2']))<=1e-8 and np.max(np.abs(cap-protocol['capacitancesPf']))<=1e-9
        coupling_error=float(np.max(np.abs(coupling-np.asarray(protocol['couplingNs']))))
        assert coupling_error<=1e-9
        study=read(root/'results.json');assert study['completed'] and len(study['cases'])==9
        expected={(i,h) for i in (-100,100,200) for h in (.0125,.00625,.003125)}
        passive_passed=True;maximum_voltage=0.
        for case in study['cases']:
            key=(case['currentPa'],case['stepMs']);assert key in expected;expected.remove(key)
            trace=np.loadtxt(root/(case['key']+'.csv'),delimiter=',',skiprows=1)
            assert trace.shape==(401,31) and np.isfinite(trace).all() and np.array_equal(trace[:,0],np.arange(401)*.25)
            operator=np.zeros((6,6));operator[:5,:5]=-(reduced+np.diag(leak))/cap[:,None]
            operator[:5,5]=(leak*rev+np.r_[case['currentPa'],np.zeros(4)])/cap
            state=np.r_[np.full(5,-71.),1.];step=expm(operator*.25)
            reference=[state[:5].copy()]
            for _ in range(400):state=step@state;reference.append(state[:5].copy())
            reference=np.asarray(reference)
            endpoint=(expm(operator*100)@np.r_[np.full(5,-71.),1.])[:5]
            assert np.max(np.abs(reference[-1]-endpoint))<=1e-9
            error=float(np.max(np.abs(trace[:,1:6]-reference)));maximum_voltage=max(maximum_voltage,error)
            ledger=float(np.max(np.abs((trace[:,1:6]+71)@cap-trace[:,30])))
            assert np.all(trace[:,15]==.0001) and np.all(trace[:,28:30]==0) and not case['crossings']
            passed=error<=1e-7 and ledger<=1e-5 and case['maximumChargeLedgerErrorFc']<=1e-5
            passive_passed &= passed
            np.savez_compressed(output/(case['key']+'.npz'),timeMs=trace[:,0],voltageMv=reference)
            report['cases'].append(dict(key=case['key'],maximumVoltageErrorMv=error,chargeLedgerErrorFc=ledger,passed=passed))
        report.update(completed=True,passiveCriteriaPassed=bool(passive_passed),maximumPassiveVoltageErrorMv=maximum_voltage,
            couplingErrorNs=coupling_error,nativeNeuronExecuted=False)
        (output/'comparison.json').write_text(json.dumps(report,indent=2,allow_nan=False)+'\n')
        if not report['channelCriteriaPassed'] or not passive_passed:raise RuntimeError('Channel/passive reference criteria not met; review without relaxing bounds.')
        print('Channel and passive checks passed. Active firing, physiological calibration and Avatar recovery remain unqualified.',flush=True)
    except Exception as error:
        report['error']=repr(error)
        (output/'comparison.json').write_text(json.dumps(report,indent=2,allow_nan=False)+'\n')
        raise


if __name__=='__main__':main(Path(sys.argv[1]).resolve())
