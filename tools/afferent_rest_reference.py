"""Independent connected roots and free-gate stability; visible study only.

No native point seeds the independent search. Algebraic gate elimination is
confined to stationary_row; dynamic_row preserves every actual free gate.
"""
from copy import deepcopy
from decimal import Decimal,localcontext
import json
import traceback
import numpy as np
from scipy.optimize import least_squares
from afferent_numerical_reference import joint,rates as nerve_rates
from afferent_numerical_protocol import GATES
from afferent_numerical_analysis import agreement,joint_conservation,strings
from afferent_rest_protocol import labels,group
from cleft_membrane_reference import hair_rates,calyx_rates
from cleft_spatial_reference import value as d
from cleft_rest_reference import HAIR,CALYX,save
from cleft_rest_analysis import comparison,rates_from_saved_evaluation


def physical_rates(value):
    rates=rates_from_saved_evaluation(value['sensory'])
    for node in value['cable']['nodes']:
        rates+=[node['potentialMvPerMs']]+[node['membrane']['gateDerivatives'][key+'PerMs'] for key in GATES]
    assert all(d(x).is_finite() for x in rates)
    return rates


def residual(values,names,criteria):
    limits=dict(voltage=criteria['voltageRateMvPerMs'],ion=criteria['ionRateMmPerMs'],gate=criteria['gateRatePerMs'])
    with localcontext() as ctx:
        ctx.prec=100
        ratios=[abs(d(v))/d(limits[group(n)]) for v,n in zip(values,names,strict=True)]
    maximum=max(ratios)
    return dict(passed=bool(maximum<=1),maximumAllowedFraction=float(maximum),
        maximumAbsoluteByGroup={k:float(max(abs(d(v)) for v,n in zip(values,names,strict=True) if group(n)==k)) for k in limits})


class ActiveReference:
    def __init__(self,fixture,source,precision):
        self.fixture=deepcopy(fixture);self.source=deepcopy(source);self.precision=precision
        self.nodes=sorted(fixture['sensory']['topology']['compartments'],key=lambda n:n['id'])
        self.cable_nodes=sorted(fixture['cableTopology']['nodes'],key=lambda n:n['id'])
        self.names=labels(fixture)[0];self.cable_start=1+11*len(self.nodes)
        self.reduced_indices=[0]+[1+11*i+j for i in range(len(self.nodes)) for j in range(4)]
        self.reduced_indices += [self.cable_start+7*i for i in range(len(self.cable_nodes))]
        f=fixture['sensory'];states={s['id']:s for s in f['states']};bath=f['perilymphMv']
        initial=[f['hairMv']-bath]
        for n in self.nodes:
            s=states[n['id']];initial.extend([s['cleftMv']-bath,s['calyxMv']-bath,np.log(s['potassiumMm']),np.log(s['sodiumMm'])])
        cable={s['id']:s for s in fixture['cableStates']}
        initial += [cable[n['id']]['potentialMv']-bath for n in self.cable_nodes]
        self.initial=np.array(initial,dtype=float)
        assert len(self.names)==self.cable_start+7*len(self.cable_nodes)

    def stationary_row(self,unknowns):
        assert len(unknowns)==len(self.reduced_indices) and np.isfinite(unknowns).all()
        row=deepcopy(self.fixture);f=row['sensory'];bath=d(f['perilymphMv'])
        f['hairMv']=bath+d(float(unknowns[0]));f['states']=[];f['inputs']=[]
        for i,n in enumerate(self.nodes):
            o=1+4*i;sv=bath+d(float(unknowns[o]));cv=bath+d(float(unknowns[o+1]))
            k=d(float(unknowns[o+2])).exp();na=d(float(unknowns[o+3])).exp();assert k>0 and na>0
            hr=hair_rates(f['hairMv']-sv);inside=calyx_rates(cv-sv);outside=calyx_rates(cv-bath)
            f['states'].append(dict(id=n['id'],cleftMv=sv,calyxMv=cv,potassiumMm=k,sodiumMm=na))
            f['inputs'].append(dict(id=n['id'],hairGates={key:hr[key]['equilibrium'] for key in HAIR},
                calyxGates=dict(innerKv74=inside['kv74']['equilibrium'],innerHcn2=inside['hcn2']['equilibrium'],
                    outerKv74=outside['kv74']['equilibrium'],outerHcn2=outside['hcn2']['equilibrium']),axonOutgoingPa=0.))
        row['cableStates']=[]
        for i,n in enumerate(self.cable_nodes):
            relative=d(float(unknowns[1+4*len(self.nodes)+i]));rates=nerve_rates(relative,self.source)
            row['cableStates'].append(dict(id=n['id'],potentialMv=bath+relative,gates={k:rates[k]['equilibrium'] for k in GATES}))
        return row

    def full_state(self,row):
        f=row['sensory'];states={s['id']:s for s in f['states']};inputs={s['id']:s for s in f['inputs']};values=[d(f['hairMv'])]
        for n in self.nodes:
            s=states[n['id']];g=inputs[n['id']]
            values.extend(d(s[k]) for k in ('cleftMv','calyxMv','potassiumMm','sodiumMm'))
            values.extend(d(g['hairGates'][k]) for k in HAIR);values.extend(d(g['calyxGates'][k]) for k in CALYX)
        cable={s['id']:s for s in row['cableStates']}
        for n in self.cable_nodes:
            s=cable[n['id']];values.append(d(s['potentialMv']));values.extend(d(s['gates'][k]) for k in GATES)
        assert len(values)==len(self.names)
        return values

    def validate_state(self,values):
        assert len(values)==len(self.names) and all(d(v).is_finite() for v in values)
        for v,n in zip(values,self.names,strict=True):
            if group(n)=='ion':assert d(v)>0
            if group(n)=='gate':assert 0<=d(v)<=1

    def dynamic_row(self,values):
        # Decimal coordinates remain Decimal through the derivative stencil.
        # Gate values may leave [0,1] ONLY in this local smooth extension.
        assert len(values)==len(self.names) and all(d(v).is_finite() for v in values)
        row=deepcopy(self.fixture);f=row['sensory'];f['hairMv']=d(values[0]);f['states']=[];f['inputs']=[]
        for i,n in enumerate(self.nodes):
            o=1+11*i;v=[d(x) for x in values[o:o+11]];assert v[2]>0 and v[3]>0
            f['states'].append(dict(id=n['id'],**dict(zip(('cleftMv','calyxMv','potassiumMm','sodiumMm'),v[:4],strict=True))))
            f['inputs'].append(dict(id=n['id'],hairGates=dict(zip(HAIR,v[4:7],strict=True)),
                calyxGates=dict(zip(CALYX,v[7:],strict=True)),axonOutgoingPa=0.))
        row['cableStates']=[]
        for i,n in enumerate(self.cable_nodes):
            o=self.cable_start+7*i
            row['cableStates'].append(dict(id=n['id'],potentialMv=d(values[o]),gates=dict(zip(GATES,map(d,values[o+1:o+7]),strict=True))))
        return row

    def evaluation(self,row,precision=None):
        with localcontext() as ctx:
            ctx.prec=precision or self.precision
            return joint(row,self.source,allow_trial_gates=True)


def spectrum(matrix,uncertainty,settings,path):
    values,vectors=np.linalg.eig(matrix)
    assert np.isfinite(values).all() and np.isfinite(vectors).all()
    # Retain the actual witnesses, unlike the earlier sealed reference.
    np.savez_compressed(path,eigenvalues=values,eigenvectors=vectors)
    condition=float(np.linalg.cond(vectors));norm=float(np.linalg.norm(matrix,2))
    residuals=np.linalg.norm(matrix@vectors-vectors*values,axis=0)/(max(1.,norm)*np.linalg.norm(vectors,axis=0))
    assert np.isfinite(residuals).all()
    roundoff=settings['roundoffMultiplier']*np.finfo(float).eps*len(matrix)*max(1.,norm)
    radius=condition*(uncertainty+roundoff) if np.isfinite(condition) else float('inf')
    right=float(values.real.max());margin=settings['classificationMarginPerMs'];passed=float(residuals.max())<=settings['eigenResidualLimit']
    classification='inconclusive'
    if passed and np.isfinite(radius):
        if right+radius < -margin:classification='locally-decaying estimate'
        elif right-radius > margin:classification='locally-growing estimate'
    return dict(eigenvalues=[dict(real=float(v.real),imaginary=float(v.imag)) for v in values],
        maximumRealPartPerMs=right,eigenvectorCondition=condition if np.isfinite(condition) else None,
        normalizedEigenResiduals=residuals.tolist(),maximumNormalizedEigenResidual=float(residuals.max()),eigenResidualPassed=passed,
        sampledUncertaintyRadiusPerMs=radius if np.isfinite(radius) else None,classification=classification,
        eigenvectorsRetained=True,uncertaintyIsEmpirical=True,nonlinearStabilityProven=False,physiologicalRestQualified=False)


def stability(reference,row,settings,folder):
    y=reference.full_state(row);reference.validate_state(y);n=len(y);names=reference.names
    scale=[d(settings[dict(voltage='voltageScaleMv',ion='ionScaleMm',gate='gateScale')[group(k)]]) for k in names]
    for i,name in enumerate(names):
        if group(name)=='ion':scale[i]=min(scale[i],abs(y[i]))
    matrices=[];assert settings['centralDifferencePowers']==[12,14,16]
    for power in settings['centralDifferencePowers']:
        matrix=np.empty((n,n))
        with localcontext() as ctx:
            ctx.prec=reference.precision;step=Decimal(2)**(-power)
            for col in range(n):
                a=y.copy();b=y.copy();delta=step*scale[col];a[col]+=delta;b[col]-=delta
                av=physical_rates(reference.evaluation(reference.dynamic_row(a)))
                bv=physical_rates(reference.evaluation(reference.dynamic_row(b)))
                matrix[:,col]=[float((x-z)/(2*step)/scale[i]) for i,(x,z) in enumerate(zip(av,bv,strict=True))]
        assert np.isfinite(matrix).all();matrices.append(matrix)
    r1=(16*matrices[1]-matrices[0])/15;r2=(16*matrices[2]-matrices[1])/15
    uncertainty=float(np.linalg.norm(r2-r1,2));differences=[float(np.linalg.norm(b-a,2)) for a,b in zip(matrices,matrices[1:])]
    refined=differences[1]<=settings['jacobianRefinementMaximumRemainingFraction']*differences[0]+settings['jacobianRefinementAbsoluteFloorPerMs']
    evaluation=reference.evaluation(row);taus=[]
    for m in evaluation['sensory']['membranes']:
        taus.extend(m['hairRates'][k]['tauMs'] for k in HAIR)
        rates=m['calyx']['rates'];taus.extend(rates[side][k]['tauMs'] for side in ('inner','outer') for k in ('kv74','hcn2'))
    for node in evaluation['cable']['nodes']:taus.extend(node['membrane']['rates'][k]['tauMs'] for k in GATES)
    gate_indices=[i for i,k in enumerate(names) if group(k)=='gate'];assert len(taus)==len(gate_indices)
    expected=np.diag([-1/float(t) for t in taus]);actual=r2[np.ix_(gate_indices,gate_indices)]
    gate_error=float(np.max(np.abs(actual-expected)))
    gate_bound=settings['freeGateBlockAbsoluteTolerance']+settings['freeGateBlockRelativeTolerance']*float(np.max(np.abs(expected)))
    gate_pass=gate_error<=gate_bound
    np.savez_compressed(folder/'full-dynamic-jacobians.npz',coarse=matrices[0],middle=matrices[1],fine=matrices[2],
        richardsonCoarse=r1,richardsonFine=r2,scale=np.array([float(x) for x in scale]),state=np.array([float(x) for x in y]),
        exactFreeGateBlock=expected,gateIndices=np.array(gate_indices,dtype=np.int64))
    eigen=spectrum(r2,uncertainty,settings,folder/'eigen-witnesses.npz')
    if not refined or not gate_pass:eigen['classification']='inconclusive'
    return dict(dimension=n,labels=names,centralDifferencePowers=settings['centralDifferencePowers'],
        successiveJacobianDifferences=differences,sampledJacobianRefinementPassed=refined,successiveRichardsonDifferenceNorm=uncertainty,
        gateEliminationDuringJacobian=False,exactFreeGateBlockMaximumError=gate_error,exactFreeGateBlockAllowedError=gate_bound,
        exactFreeGateBlockPassed=gate_pass,spectrum=eigen,timeIntegrated=False)


def run_case(case,protocol,source,folder):
    folder.mkdir();settings=protocol['reference'];reference=ActiveReference(case['fixture'],source,settings['decimalPrecision'])
    initial=reference.initial;history=[];calls=0;last_trial=None;last_error=None
    def evaluate_unknowns(x):
        nonlocal calls,last_trial,last_error
        calls+=1;last_trial=[float(v) for v in x];last_error=None
        try:
            with localcontext() as ctx:
                ctx.prec=reference.precision;row=reference.stationary_row(x);result=reference.evaluation(row);rates=physical_rates(result)
            return row,result,rates
        except Exception:last_error=traceback.format_exc();raise
    def fun(x):
        _,_,rates=evaluate_unknowns(x)
        out=np.array([float(rates[i])/(settings['ionResidualScale'] if group(reference.names[i])=='ion' else settings['voltageResidualScale']) for i in reference.reduced_indices])
        assert np.isfinite(out).all();return out
    def accepted(x):
        _,_,rates=evaluate_unknowns(x)
        history.append(dict(unknowns=[float(v) for v in x],physicalResidual=residual(rates,reference.names,protocol['rootCriteria'])))
    save(folder/'input.json',dict(case=case,settings=settings,initialUnknowns=initial.tolist(),candidateStateUsedAsSeed=False))
    try:
        accepted(initial)
        x_scale=np.array([settings['logIonUnknownScale'] if group(reference.names[i])=='ion' else settings['voltageUnknownScaleMv'] for i in reference.reduced_indices])
        solved=least_squares(fun,initial,method=settings['method'],jac=settings['jacobian'],x_scale=x_scale,
            ftol=settings['ftol'],xtol=settings['xtol'],gtol=settings['gtol'],max_nfev=settings['maximumEvaluations'],callback=accepted)
        row,low,rates=evaluate_unknowns(solved.x);state=reference.full_state(row);reference.validate_state(state)
        high=reference.evaluation(row,settings['witnessPrecision']);precision=comparison(low,high,protocol['precisionComparison'])
        criterion=residual(rates,reference.names,protocol['rootCriteria'])
        with localcontext() as ctx:
            ctx.prec=100
            accounting=[joint_conservation(row,v,protocol['derivativeCriteria']['conservation']).result() for v in (low,high)]
        result=dict(completed=True,optimizerSuccess=bool(solved.success),optimizerStatus=int(solved.status),message=str(solved.message),
            nfev=int(solved.nfev),njev=int(solved.njev),evaluationsIncludingRecordedIterates=calls,unknowns=solved.x.tolist(),
            fullState=[float(v) for v in state],fullStateDecimal=[str(v) for v in state],physicalRates=[str(v) for v in rates],
            physicalResidual=criterion,acceptedIterates=history,precisionWitnesses=[strings(low),strings(high)],precision=precision,
            accounting=accounting,candidateStateUsedAsSeed=False,timeIntegrated=False,physiologicalRestQualified=False,localStability=None)
        if criterion['passed'] and precision['passed'] and all(a['passed'] for a in accounting):
            try:result['localStability']=dict(completed=True,**stability(reference,row,protocol['stability'],folder))
            except Exception:result['localStability']=dict(completed=False,error=traceback.format_exc(),physiologicalRestQualified=False)
    except Exception:
        result=dict(completed=False,error=traceback.format_exc(),evaluationsIncludingRecordedIterates=calls,
            acceptedIterates=history,lastTrialUnknowns=last_trial,lastTrialError=last_error,
            candidateStateUsedAsSeed=False,timeIntegrated=False,physiologicalRestQualified=False)
    save(folder/'result.json',result)
    return dict(key=case['key'],completed=result['completed'],rootCriteriaPassed=result.get('physicalResidual',{}).get('passed',False))


def controls(protocol,folder):
    folder.mkdir();rows=[]
    for control in protocol['stabilityControls']:
        name=control['name'];matrix=np.array(control['matrix']);expected=control['expected']
        value=spectrum(matrix,0.,protocol['stability'],folder/(name+'-eigen-witnesses.npz'))
        rows.append(dict(name=name,matrix=matrix.tolist(),expected=expected,result=value,passed=value['classification']==expected))
    result=dict(completed=True,passed=all(r['passed'] for r in rows),cases=rows,modelIntegrated=False)
    save(folder/'controls.json',result);assert result['passed']


def run(package,protocol,source):
    output=package/'independent-rest';output.mkdir();controls(protocol,output/'controls');rows=[]
    for case in protocol['cases']:
        print('INDEPENDENT CONNECTED REST AND FREE-GATE JACOBIAN',case['key'],flush=True)
        rows.append(run_case(case,protocol,source,output/case['key']))
    save(output/'results.json',dict(completed=True,cases=rows,expectedCases=18,timeIntegrated=False,physiologicalRestQualified=False))


def native_witnesses(package,protocol,source):
    output=package/'native-witnesses';output.mkdir();rows=[]
    for case in protocol['cases']:
        key=case['key']
        try:
            native=json.loads((package/'study'/key/'result.json').read_text(encoding='utf-8-sig'))
            history=native['acceptedIterations']
            if not history:result=dict(completed=True,pointAvailable=False,nativeOutcome=native['outcome'])
            else:
                point=history[-1]['point'];row=deepcopy(case['fixture']);f=row['sensory']
                f['hairMv']=point['hairMv'];f['states']=point['compartments'];row['cableStates']=point['cableStates']
                f['inputs']=[dict(id=g['id'],hairGates=g['hair'],calyxGates=g['calyx'],axonOutgoingPa=0.) for g in point['gates']]
                reference=ActiveReference(row,source,protocol['reference']['decimalPrecision']);state=reference.full_state(row);reference.validate_state(state)
                low=reference.evaluation(row);high=reference.evaluation(row,protocol['reference']['witnessPrecision'])
                with localcontext() as ctx:
                    ctx.prec=100
                    precision=comparison(low,high,protocol['precisionComparison']);candidate=agreement('joints',point['evaluation'],high,protocol['derivativeCriteria'])
                    accounting=[joint_conservation(row,v,protocol['derivativeCriteria']['conservation']).result() for v in (point['evaluation'],low,high)]
                result=dict(completed=True,pointAvailable=True,nativeOutcome=native['outcome'],
                    fullState=[float(x) for x in state],physicalRates=[str(x) for x in physical_rates(high)],
                    precisionWitnesses=[strings(low),strings(high)],precision=precision,nativeEvaluationComparison=candidate,
                    accounting=accounting,physicalResidual=residual(physical_rates(high),reference.names,protocol['rootCriteria']),timeIntegrated=False)
        except Exception:result=dict(completed=False,error=traceback.format_exc(),pointAvailable=False,timeIntegrated=False)
        save(output/(key+'.json'),result);rows.append(dict(key=key,completed=result['completed'],pointAvailable=result['pointAvailable']))
    save(output/'results.json',dict(completed=True,cases=rows,timeIntegrated=False))
