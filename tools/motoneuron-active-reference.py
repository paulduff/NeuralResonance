"""Independent coupled ODE comparison. Execute only in the visible study window.

Reuse source-derived analytic *rates* checked by the previous clamp study, not
candidate dynamics. Derive currents/geometry from pinned MOD/HOC declarations.
No native NEURON execution, parameter fitting, biological promotion or live input.
"""
from hashlib import sha256
import importlib.util
import json
from pathlib import Path
import sys
import time
import numpy as np
from scipy.integrate import solve_ivp
from motoneuron_active_protocol import protocol

read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
GATES=np.r_[np.arange(5,14),np.arange(15,27)]


def source_model(root):
    spec=importlib.util.spec_from_file_location('motor_channel_reference',Path(__file__).with_name('motoneuron-reference-check.py'))
    channels=importlib.util.module_from_spec(spec);spec.loader.exec_module(channels)
    p,s,d=channels.model(root)
    hoc=channels.numbers((root/'FivecomptcMG_0.hoc').read_text())
    sections=[s]+[d]*4
    area=np.asarray([np.pi*x['diam']*x['L'] for x in sections])
    cap=.01*area*np.asarray([x['cm'] for x in sections])
    half=np.asarray([1e9/(x['Ra']*(x['L']*.5e-4)/(np.pi*(x['diam']*.5e-4)**2)) for x in sections])
    lap=np.zeros((7,7))
    for a,b,g in ((0,5,half[0]),(0,6,half[0]),(1,5,half[1]),(2,5,half[2]),(3,6,half[3]),(4,6,half[4])):
        lap[a,a]+=g;lap[b,b]+=g;lap[a,b]-=g;lap[b,a]-=g
    axial=lap[:5,:5]-lap[:5,5:]@np.linalg.solve(lap[5:,5:],lap[5:,:5])
    # Exact elimination conserves internal charge. Reconstruct its diagonal
    # from off-diagonal edges to avoid cancellation of the large soma-endpoint
    # conductance masquerading as a tiny external current over long runs.
    np.fill_diagonal(axial,0);np.fill_diagonal(axial,-np.sum(axial,axis=1))
    leak=np.asarray([x['g_pas'] for x in sections]);rev=np.asarray([x['e_pas'] for x in sections])
    hcn=np.asarray([x['ghbar_gh'] for x in sections])
    cal=np.asarray([hoc[f'd{i}.gcabar_L_Ca_inact'] for i in range(1,5)])
    initial=np.zeros(30);initial[:5]=-65;initial[14]=p['mAHP']['cainf']
    r=channels.rates(-65,initial[14],hoc['celsius'],1,p)
    initial[5:14]=r[:9,0];initial[15:27]=np.tile(r[9:,0],4)

    def derivative(block,command):
        def rhs(t,y):
            v=y[:5];g=y[5:14];dend=y[15:27].reshape(4,3)
            rates=channels.rates(v[0],y[14],hoc['celsius'],1,p)
            dy=np.zeros(30);dy[5:14]=(rates[:9,0]-g)/rates[:9,1]
            l=p['L_Ca_inact'];h=p['gh'];vd=v[1:]
            inf=np.column_stack((1/(1+np.exp((vd-l['theta_m'])/l['kappa_m'])),
                1/(1+np.exp((vd-l['theta_h'])/l['kappa_h'])),1/(1+np.exp((vd-h['half'])/h['slp']))))
            dy[15:27]=((inf-dend)/np.asarray([l['tau_m'],l['tau_h'],h['htau']])).ravel()
            ca=s['gcamax_mAHP']*g[6]*(v[0]-12.5*np.log(40000))
            entry=max(0.,-10000*ca/(2*96485.33212*p['mAHP']['depth']))
            removal=(y[14]-p['mAHP']['cainf'])/s['taur_mAHP']
            dy[14]=entry-removal;dy[27]=entry;dy[28]=removal
            density=leak*(v-rev)
            na=(0 if block=='WithoutTransientSodium' else s['gbar_na3rp']*g[0]**3*g[1]*g[2])+s['gbar_naps']*g[3]*g[4]
            potassium=s['gMax_kdrRL']*g[5]**4+(0 if block=='WithoutPotassiumCalcium' else s['gkcamax_mAHP']*g[7])
            density[0]+=na*(v[0]-50)+potassium*(v[0]-s['ek'])+ca+hcn[0]*g[8]*(v[0]-h['eh'])
            density[1:]+=hcn[1:]*dend[:,2]*(vd-h['eh'])
            if block!='WithoutCalciumL':density[1:]+=cal*dend[:,0]*dend[:,1]*(vd-l['vca'])
            outside=-10*area*density
            outside[0]+=command['somaCurrentPa']
            outside[1:]-=command['dendriteExcitatoryNs']*vd+command['dendriteInhibitoryNs']*(vd+80)
            dy[:5]=(outside-axial@v)/cap;dy[29]=np.sum(outside)
            return dy
        return rhs
    return derivative,initial,area,cap,axial


def metrics(time_ms,state,crossings,cap):
    assert state.shape==(60001,30) and np.isfinite(state).all()
    assert np.isfinite(crossings).all() and np.all(np.diff(crossings)>0) and np.all((crossings>0)&(crossings<=6000))
    ledger_ca=float(np.max(np.abs(state[:,14]-.0001-state[:,27]+state[:,28])))
    ledger_q=float(np.max(np.abs((state[:,:5]+65)@cap-state[:,29])))
    windows=[(0,1000),(1000,1800),(1800,2200),(2200,3000),(3000,6000)]
    return dict(crossingCount=len(crossings),windowCrossings=[int(np.sum((crossings>=a)&(crossings<b))) for a,b in windows],
        minimumSomaVoltageMv=float(np.min(state[:,0])),maximumSomaVoltageMv=float(np.max(state[:,0])),
        maximumCalciumMm=float(np.max(state[:,14])),finalSomaVoltageMv=float(state[-1,0]),finalCalciumMm=float(state[-1,14]),
        maximumCalciumLedgerErrorMm=ledger_ca,maximumChargeLedgerErrorFc=ledger_q,
        minimumGate=float(np.min(state[:,GATES])),maximumGate=float(np.max(state[:,GATES])),
        minimumCalciumMm=float(np.min(state[:,14])),
        baselineMeanSomaVoltageMv=float(np.mean(state[(time_ms>=900)&(time_ms<1000),0])),
        postBriefMinimumSomaVoltageMv=float(np.min(state[(time_ms>=1002)&(time_ms<=1202),0])))


def compare(a,b,sa,sb):
    same=len(sa)==len(sb)
    return dict(voltageMv=float(np.max(np.abs(a[:,:5]-b[:,:5]))),
        gate=float(np.max(np.abs(a[:,GATES]-b[:,GATES]))),calciumMm=float(np.max(np.abs(a[:,14]-b[:,14]))),
        equalCrossingCounts=same,crossingMs=float(np.max(np.abs(sa-sb),initial=0)) if same else None)


def meets(error,criteria,prefix):
    return error['equalCrossingCounts'] and all(error[k]<=criteria[prefix+k[0].upper()+k[1:]] for k in ('voltageMv','gate','calciumMm','crossingMs'))


def physical(m,c):
    return (m['minimumGate']>=-1e-10 and m['maximumGate']<=1+1e-10 and m['minimumCalciumMm']>=-1e-13
        and m['maximumCalciumLedgerErrorMm']<=c['calciumLedgerMm'] and m['maximumChargeLedgerErrorFc']<=c['chargeLedgerFc'])


def solve(rhs,initial,scenario,method,grid):
    result=np.empty((len(grid),30));result[0]=initial;state=initial.copy();begin=0.;crossings=[];steps=0;nfev=0
    atol=np.r_[np.full(5,1e-9),np.full(9,1e-11),1e-13,np.full(12,1e-11),1e-13,1e-13,1e-7]*method['atolScale']
    def crossing(t,y):return y[0]+20
    crossing.direction=1;crossing.terminal=False
    for command in scenario['segments']:
        end=command['untilMs'];mask=(grid>begin)&(grid<=end)
        solution=solve_ivp(rhs(scenario['block'],command),(begin,end),state,method=method['method'],
            rtol=method['rtol'],atol=atol,max_step=method['maximumStepMs'],events=crossing,dense_output=True)
        if not solution.success:raise RuntimeError(solution.message)
        assert solution.t[-1]==end and np.isfinite(solution.y).all()
        result[mask]=solution.sol(grid[mask]).T
        events=solution.t_events[0]
        # A root exactly at an input boundary belongs to the preceding segment.
        crossings.extend(float(t) for t in events if t>begin)
        state=solution.y[:,-1];begin=end;steps+=len(solution.t)-1;nfev+=solution.nfev
    return result,np.asarray(crossings),dict(acceptedSteps=steps,functionEvaluations=nfev)


def main(package):
    output=package/'motoneuron-active-reference';output.mkdir()
    report=dict(completed=False,numericalCriteriaPassed=False,nativeNeuronExecuted=False,
        physiologicalCalibrationComplete=False,activeMotorSourceQualified=False,avatarRecoveryAssessed=False,cases=[])
    def save(): (output/'comparison.json').write_text(json.dumps(report,indent=2,allow_nan=False)+'\n')
    try:
        spec=read(package/'motoneuron-active-input.json');assert spec==protocol()
        report['criteria']=spec['criteria'];c=spec['criteria']
        study=package/'motoneuron-active-study';meta=read(study/'protocol.json');outcomes=read(study/'results.json')
        assert meta['inputSha256']==sha256((package/'motoneuron-active-input.json').read_bytes()).hexdigest().upper()
        assert meta['specification']=={k:spec[k] for k in meta['specification']} and not meta['liveBrainAccessed']
        assert outcomes['completed'] and outcomes['expectedCases']==30 and len(outcomes['cases'])==30
        expected={(s['key'],h) for s in spec['cases'] for h in spec['stepsMs']}
        for row in outcomes['cases']:
            key=(row['scenario'],row['stepMs']);assert key in expected;expected.remove(key)
            assert row['key']==row['scenario']+'-h'+str(row['stepMs'])
        rhs,initial,area,cap,axial=source_model(package/'motoneuron-input')
        coupling=-axial.copy();np.fill_diagonal(coupling,0)
        assert np.max(np.abs(area-meta['areasUm2']))<=1e-8 and np.max(np.abs(cap-meta['capacitancesPf']))<=1e-9
        assert np.max(np.abs(coupling-np.asarray(meta['couplingNs'])))<=1e-9
        grid=np.arange(60001)*.1
        for scenario in spec['cases']:
            row=dict(scenario=scenario['key'],references=[],referenceContrasts=[],candidates=[],passed=False)
            report['cases'].append(row);references={}
            for method in spec['referenceMethods']:
                print(scenario['key']+' / '+method['key'],flush=True);start=time.monotonic()
                try:
                    state,crossings,solver=solve(rhs,initial,scenario,method,grid)
                    m=metrics(grid,state,crossings,cap)
                    np.savez_compressed(output/(scenario['key']+'-'+method['key']+'.npz'),timeMs=grid,state=state,crossingsMs=crossings)
                    references[method['key']]=(state,crossings)
                    row['references'].append(dict(method=method['key'],completed=True,elapsedSeconds=time.monotonic()-start,
                        physicalAndAccountingPassed=physical(m,c),metrics=m,**solver))
                except Exception as error:
                    row['references'].append(dict(method=method['key'],completed=False,error=repr(error)))
                save()
            for a,b in (('dop853','dop853-tight'),('dop853-tight','radau')):
                if a in references and b in references:
                    x,sx=references[a];y,sy=references[b];error=compare(x,y,sx,sy)
                    row['referenceContrasts'].append(dict(left=a,right=b,maximumAbsolute=error,passed=meets(error,c,'reference')))
            ref_ok=(len(row['referenceContrasts'])==2 and all(x['passed'] for x in row['referenceContrasts'])
                and len(references)==3 and all(x['physicalAndAccountingPassed'] for x in row['references']))
            if 'dop853-tight' in references:
                reference,rs=references['dop853-tight']
                for candidate in (x for x in outcomes['cases'] if x['scenario']==scenario['key']):
                    item=dict(key=candidate['key'],stepMs=candidate['stepMs'],completed=candidate['completed'],passed=False)
                    row['candidates'].append(item)
                    if not candidate['completed']:
                        item['error']=candidate['error'];continue
                    try:
                        trace=np.loadtxt(study/(candidate['key']+'.csv'),delimiter=',',skiprows=1)
                        assert trace.shape==(60001,31) and np.array_equal(trace[:,0],grid)
                        xs=np.asarray(candidate['crossingsMs']);state=trace[:,1:]
                        assert np.max(np.abs(state[0]-initial))<=1e-12
                        error=compare(state,reference,xs,rs);m=metrics(grid,state,xs,cap)
                        accounting=(physical(m,c) and candidate['maximumCalciumLedgerErrorMm']<=c['calciumLedgerMm']
                            and candidate['maximumChargeLedgerErrorFc']<=c['chargeLedgerFc'])
                        item.update(maximumAbsolute=error,metrics=m,physicalAndAccountingPassed=accounting,
                            passed=bool(meets(error,c,'candidate') and accounting))
                    except Exception as error:item['error']=repr(error)
                # Compare refinement against independently measured uncertainty;
                # no selecting whichever reference happens to favor a candidate.
                ordered=sorted(row['candidates'],key=lambda x:x['stepMs'],reverse=True)
                refinement=[]
                for coarse,fine in zip(ordered,ordered[1:]):
                    passed=False
                    if ref_ok and 'maximumAbsolute' in coarse and 'maximumAbsolute' in fine:
                        ec=coarse['maximumAbsolute'];ef=fine['maximumAbsolute']
                        passed=ec['equalCrossingCounts'] and ef['equalCrossingCounts']
                        for field in ('voltageMv','gate','calciumMm','crossingMs'):
                            uncertainty=max(x['maximumAbsolute'][field] for x in row['referenceContrasts'])
                            if ec[field] is None or ef[field] is None or ef[field]>max(2*uncertainty,c['refinementRatio']*ec[field]):passed=False
                    refinement.append(dict(coarseStepMs=coarse['stepMs'],fineStepMs=fine['stepMs'],passed=bool(passed)))
                row['refinement']=refinement
                row['passed']=bool(ref_ok and len(ordered)==3 and ordered[-1]['passed'] and len(refinement)==2
                    and all(x['passed'] for x in refinement) and all(x.get('physicalAndAccountingPassed',False) for x in ordered))
            save()
        # Agreement on quiet traces alone must not qualify an active experiment.
        strong=next(x for x in report['cases'] if x['scenario']=='soma-6000')
        exercise=(len(strong['references'])==3 and all(x.get('completed',False)
            and sum(x['metrics']['windowCrossings'][1:4])>0 and x['metrics']['maximumSomaVoltageMv']>0
            and x['metrics']['maximumCalciumMm']>.0001 for x in strong['references']))
        report.update(completed=True,actionPotentialAndCalciumExercisePassed=bool(exercise),
            numericalCriteriaPassed=bool(exercise and all(x['passed'] for x in report['cases'])))
        save()
        if not report['numericalCriteriaPassed']:raise RuntimeError('Active numerical criteria not all met; preserve evidence and review without changing bounds.')
        print('Active numerical comparisons passed. Physiological calibration, motor-source integration and Avatar recovery remain unqualified.',flush=True)
    except Exception as error:
        report['error']=repr(error);save();raise


if __name__=='__main__':main(Path(sys.argv[1]).resolve())
