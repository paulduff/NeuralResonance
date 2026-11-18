"""Independent active roots and full dynamic Jacobians; visible study only."""
from copy import deepcopy
from decimal import Decimal,localcontext
import importlib.util
import json
from pathlib import Path
import traceback
import numpy as np
from scipy.optimize import least_squares
from cleft_membrane_reference import evaluate,hair_rates,calyx_rates
from cleft_spatial_reference import value as d
from cleft_trajectory_protocol import labels

ROOT=Path(__file__).resolve().parents[1]
s=importlib.util.spec_from_file_location('rest_constitutive_reference',ROOT/'tools/analyse-cleft-coupling.py')
base=importlib.util.module_from_spec(s);s.loader.exec_module(base)
def save(path,value):
    def clean(v):
        if isinstance(v,dict):return {k:clean(x) for k,x in v.items()}
        if isinstance(v,list):return [clean(x) for x in v]
        return str(v) if isinstance(v,float) and not np.isfinite(v) else v
    with path.open('x',encoding='utf-8') as stream:json.dump(clean(value),stream,indent=2,allow_nan=False)
HAIR=('potassiumLow','calcium','hcn1')
CALYX=('innerKv74','innerHcn2','outerKv74','outerHcn2')


def physical_rates(result):
    values=[result['coupling']['spatial']['hairMvPerMs']]
    for m,c in zip(result['membranes'],result['coupling']['spatial']['compartments'],strict=True):
        assert m['id']==c['id']
        values.extend(c[k] for k in ('cleftMvPerMs','calyxMvPerMs','potassiumMmPerMs','sodiumMmPerMs'))
        values.extend(m['hairGateDerivatives'][k+'PerMs'] for k in HAIR)
        values.extend(m['calyxGateDerivatives'][k+'PerMs'] for k in CALYX)
    assert all(x.is_finite() for x in values);return values


def rate_group(index):
    if index==0 or (index-1)%11<2:return 'voltage'
    return 'ion' if (index-1)%11<4 else 'gate'


def residual(values,criteria):
    tolerances=dict(voltage=criteria['voltageRateMvPerMs'],ion=criteria['ionRateMmPerMs'],gate=criteria['gateRatePerMs'])
    ratios=[abs(d(v))/d(tolerances[rate_group(i)]) for i,v in enumerate(values)]
    maximum=max(ratios)
    return dict(passed=bool(maximum<=1),maximumAllowedFraction=float(maximum),
        maximumAbsoluteByGroup={k:float(max(abs(d(v)) for i,v in enumerate(values) if rate_group(i)==k)) for k in tolerances})


class ActiveReference:
    def __init__(self,fixture,precision):
        self.fixture=deepcopy(fixture);self.precision=precision
        self.nodes=sorted(fixture['topology']['compartments'],key=lambda n:n['id'])
        self.reduced_indices=[0]+[1+11*i+j for i in range(len(self.nodes)) for j in range(4)]
        states={s['id']:s for s in fixture['states']};bath=fixture['perilymphMv']
        self.initial=[fixture['hairMv']-bath]
        for n in self.nodes:
            a=states[n['id']];self.initial.extend([a['cleftMv']-bath,a['calyxMv']-bath,np.log(a['potassiumMm']),np.log(a['sodiumMm'])])
        self.initial=np.array(self.initial,dtype=float)

    def stationary_row(self,unknowns):
        assert len(unknowns)==1+4*len(self.nodes) and np.isfinite(unknowns).all()
        row=deepcopy(self.fixture);bath=d(row['perilymphMv']);row['hairMv']=bath+d(float(unknowns[0]));row['states']=[];row['inputs']=[]
        for i,n in enumerate(self.nodes):
            o=1+4*i;sv=bath+d(float(unknowns[o]));cv=bath+d(float(unknowns[o+1]))
            k=d(float(unknowns[o+2])).exp();na=d(float(unknowns[o+3])).exp();assert k>0 and na>0
            hr=hair_rates(row['hairMv']-sv);inside=calyx_rates(cv-sv);outside=calyx_rates(cv-bath)
            row['states'].append(dict(id=n['id'],cleftMv=sv,calyxMv=cv,potassiumMm=k,sodiumMm=na))
            row['inputs'].append(dict(id=n['id'],hairGates={key:hr[key]['equilibrium'] for key in HAIR},
                calyxGates=dict(innerKv74=inside['kv74']['equilibrium'],innerHcn2=inside['hcn2']['equilibrium'],
                    outerKv74=outside['kv74']['equilibrium'],outerHcn2=outside['hcn2']['equilibrium']),axonOutgoingPa=0.))
        return row

    def full_state(self,row):
        states={s['id']:s for s in row['states']};inputs={s['id']:s for s in row['inputs']};values=[d(row['hairMv'])]
        for n in self.nodes:
            state=states[n['id']];g=inputs[n['id']]
            values.extend(d(state[k]) for k in ('cleftMv','calyxMv','potassiumMm','sodiumMm'))
            values.extend(d(g['hairGates'][k]) for k in HAIR);values.extend(d(g['calyxGates'][k]) for k in CALYX)
        return values

    def dynamic_row(self,values):
        # Actual independent gate coordinates. Never replace these perturbations
        # with g_inf: that would suppress the feedback whose stability we need.
        assert len(values)==1+11*len(self.nodes) and all(d(v).is_finite() for v in values)
        row=deepcopy(self.fixture);row['hairMv']=d(values[0]);row['states']=[];row['inputs']=[]
        for i,n in enumerate(self.nodes):
            o=1+11*i;v=[d(x) for x in values[o:o+11]];assert v[2]>0 and v[3]>0
            row['states'].append(dict(id=n['id'],**dict(zip(('cleftMv','calyxMv','potassiumMm','sodiumMm'),v[:4],strict=True))))
            row['inputs'].append(dict(id=n['id'],hairGates=dict(zip(HAIR,v[4:7],strict=True)),
                calyxGates=dict(zip(CALYX,v[7:],strict=True)),axonOutgoingPa=0.))
        return row

    def evaluation(self,row,precision=None):
        with localcontext() as ctx:
            ctx.prec=precision or self.precision
            return evaluate(row,base.edge,base.dense_solve)


def spectrum(matrix,uncertainty,settings):
    values,vectors=np.linalg.eig(matrix);condition=float(np.linalg.cond(vectors))
    norm=float(np.linalg.norm(matrix,2));residuals=np.linalg.norm(matrix@vectors-vectors*values,axis=0)/(max(1.,norm)*np.linalg.norm(vectors,axis=0))
    assert np.isfinite(values).all() and np.isfinite(residuals).all()
    roundoff=settings['roundoffMultiplier']*np.finfo(float).eps*len(matrix)*max(1.,norm)
    radius=condition*(uncertainty+roundoff) if np.isfinite(condition) else float('inf')
    right=float(values.real.max());margin=settings['classificationMarginPerMs'];residual_pass=float(residuals.max())<=settings['eigenResidualLimit']
    classification='inconclusive'
    if residual_pass and np.isfinite(radius):
        if right+radius < -margin:classification='locally-decaying estimate'
        elif right-radius > margin:classification='locally-growing estimate'
    return dict(eigenvalues=[dict(real=float(x.real),imaginary=float(x.imag)) for x in values],
        maximumRealPartPerMs=right,eigenvectorCondition=condition if np.isfinite(condition) else None,
        maximumNormalizedEigenResidual=float(residuals.max()),eigenResidualPassed=residual_pass,
        sampledUncertaintyRadiusPerMs=radius if np.isfinite(radius) else None,classification=classification,
        uncertaintyIsEmpirical=True,nonlinearStabilityProven=False,physiologicalRestQualified=False)


def stability(reference,row,settings,folder):
    y=reference.full_state(row);n=len(y)
    scale=[d(settings['voltageScaleMv'] if rate_group(i)=='voltage' else settings['ionScaleMm'] if rate_group(i)=='ion' else settings['gateScale']) for i in range(n)]
    for i in range(n):
        if rate_group(i)=='ion':scale[i]=min(scale[i],abs(y[i]))
    matrices=[]
    for power in settings['centralDifferencePowers']:
        matrix=np.empty((n,n))
        with localcontext() as ctx:
            ctx.prec=reference.precision;step=Decimal(2)**(-power)
            for col in range(n):
                a=y.copy();b=y.copy();delta=step*scale[col];a[col]+=delta;b[col]-=delta
                # This is a derivative stencil, not an accepted dynamic state.
                # Smooth gate extension outside [0,1] is explicit; ions stay positive.
                av=physical_rates(reference.evaluation(reference.dynamic_row(a)))
                bv=physical_rates(reference.evaluation(reference.dynamic_row(b)))
                matrix[:,col]=[float((x-z)/(2*step)/scale[i]) for i,(x,z) in enumerate(zip(av,bv,strict=True))]
        assert np.isfinite(matrix).all();matrices.append(matrix)
    # Fourfold stencil reduction: central differences have an h^2 leading term.
    r1=(16*matrices[1]-matrices[0])/15;r2=(16*matrices[2]-matrices[1])/15
    uncertainty=float(np.linalg.norm(r2-r1,2))
    differences=[float(np.linalg.norm(b-a,2)) for a,b in zip(matrices,matrices[1:])]
    refinement=differences[1]<=settings['jacobianRefinementMaximumRemainingFraction']*differences[0]+settings['jacobianRefinementAbsoluteFloorPerMs']
    # For every free gate, the exact gate-to-gate block is diagonal -1/tau.
    # Eliminating/resetting gates during the stencil would fail this control.
    gate_indices=[i for i in range(n) if rate_group(i)=='gate'];taus=[]
    with localcontext() as ctx:
        ctx.prec=reference.precision
        for i in range(len(reference.nodes)):
            o=1+11*i;hr=hair_rates(y[0]-y[o]);inside=calyx_rates(y[o+1]-y[o]);outside=calyx_rates(y[o+1]-d(row['perilymphMv']))
            taus.extend(hr[k]['tauMs'] for k in HAIR)
            taus.extend((inside['kv74']['tauMs'],inside['hcn2']['tauMs'],outside['kv74']['tauMs'],outside['hcn2']['tauMs']))
    expected=np.diag([-1/float(t) for t in taus]);actual=r2[np.ix_(gate_indices,gate_indices)]
    gate_error=float(np.max(np.abs(actual-expected)))
    gate_bound=settings['freeGateBlockAbsoluteTolerance']+settings['freeGateBlockRelativeTolerance']*float(np.max(np.abs(expected)))
    gate_pass=gate_error<=gate_bound
    np.savez_compressed(folder/'full-dynamic-jacobians.npz',coarse=matrices[0],middle=matrices[1],fine=matrices[2],
        richardsonCoarse=r1,richardsonFine=r2,scale=np.array([float(x) for x in scale]),state=np.array([float(x) for x in y]))
    eigen=spectrum(r2,uncertainty,settings)
    if not refinement or not gate_pass:eigen['classification']='inconclusive'
    return dict(dimension=n,labels=labels(reference.fixture)[0],centralDifferencePowers=settings['centralDifferencePowers'],
        successiveJacobianDifferences=differences,sampledJacobianRefinementPassed=refinement,
        successiveRichardsonDifferenceNorm=uncertainty,gateEliminationDuringJacobian=False,
        exactFreeGateBlockMaximumError=gate_error,exactFreeGateBlockAllowedError=gate_bound,exactFreeGateBlockPassed=gate_pass,
        spectrum=eigen,timeIntegrated=False)


def run_case(case,protocol,folder):
    folder.mkdir();settings=protocol['reference'];reference=ActiveReference(case['fixture'],settings['decimalPrecision'])
    initial=reference.initial;history=[];calls=0;last_trial=None;last_error=None
    def evaluate_unknowns(x):
        nonlocal calls,last_trial,last_error
        calls+=1;last_trial=[float(v) for v in x];last_error=None
        try:
            with localcontext() as ctx:
                ctx.prec=reference.precision;row=reference.stationary_row(x);result=reference.evaluation(row);rates=physical_rates(result)
            return row,result,rates
        except Exception:
            last_error=traceback.format_exc();raise
    def fun(x):
        _,_,rates=evaluate_unknowns(x)
        out=np.array([float(rates[i])/(settings['ionResidualScale'] if rate_group(i)=='ion' else settings['voltageResidualScale']) for i in reference.reduced_indices])
        assert np.isfinite(out).all();return out
    def accepted(x):
        _,_,rates=evaluate_unknowns(x)
        history.append(dict(unknowns=[float(v) for v in x],physicalResidual=residual(rates,protocol['rootCriteria'])))
    save(folder/'input.json',dict(case=case,settings=settings,initialUnknowns=initial.tolist(),candidateStateUsedAsSeed=False))
    try:
        accepted(initial)
        x_scale=np.array([settings['logIonUnknownScale'] if i>0 and (i-1)%4>=2 else settings['voltageUnknownScaleMv'] for i in range(len(initial))])
        solved=least_squares(fun,initial,method=settings['method'],jac=settings['jacobian'],x_scale=x_scale,
            ftol=settings['ftol'],xtol=settings['xtol'],gtol=settings['gtol'],max_nfev=settings['maximumEvaluations'],callback=accepted)
        row,low,rates=evaluate_unknowns(solved.x)
        high=reference.evaluation(row,settings['witnessPrecision'])
        from cleft_rest_analysis import comparison
        precision=comparison(low,high,protocol['precisionComparison'])
        criterion=residual(rates,protocol['rootCriteria'])
        result=dict(completed=True,optimizerSuccess=bool(solved.success),optimizerStatus=int(solved.status),message=str(solved.message),
            nfev=int(solved.nfev),njev=int(solved.njev),evaluationsIncludingRecordedIterates=calls,unknowns=solved.x.tolist(),
            fullState=[float(v) for v in reference.full_state(row)],fullStateDecimal=[str(v) for v in reference.full_state(row)],
            physicalRates=[str(v) for v in rates],physicalResidual=criterion,acceptedIterates=history,
            precisionWitnesses=[base.decimal_strings(low),base.decimal_strings(high)],precision=precision,
            candidateStateUsedAsSeed=False,timeIntegrated=False,physiologicalRestQualified=False)
        # Roots failing their physical residuals do not receive a stability label.
        result['localStability']=None
        if criterion['passed']:
            try:result['localStability']=dict(completed=True,**stability(reference,row,protocol['stability'],folder))
            except Exception:result['localStability']=dict(completed=False,error=traceback.format_exc(),physiologicalRestQualified=False)
    except Exception:
        result=dict(completed=False,error=traceback.format_exc(),evaluationsIncludingRecordedIterates=calls,
            acceptedIterates=history,lastTrialUnknowns=last_trial,lastTrialError=last_error,
            candidateStateUsedAsSeed=False,timeIntegrated=False,physiologicalRestQualified=False)
    save(folder/'result.json',result)
    return dict(key=case['key'],completed=result['completed'],rootCriteriaPassed=result.get('physicalResidual',{}).get('passed',False))


def controls(protocol,folder):
    folder.mkdir();settings=protocol['stability'];rows=[]
    for name,matrix,expected in [('stable',np.array([[-2.,3.],[0.,-1.]]),'locally-decaying estimate'),
        ('unstable',np.array([[-2.,0.],[0.,1.]]),'locally-growing estimate'),
        ('neutral',np.array([[-2.,0.],[0.,0.]]),'inconclusive'),
        ('oscillatory',np.array([[-1.,-3.],[3.,-1.]]),'locally-decaying estimate')]:
        value=spectrum(matrix,0.,settings);rows.append(dict(name=name,matrix=matrix.tolist(),expected=expected,result=value,passed=value['classification']==expected))
    result=dict(completed=True,passed=all(r['passed'] for r in rows),cases=rows,modelIntegrated=False)
    save(folder/'controls.json',result);assert result['passed']


def run(package,protocol):
    output=package/'independent-rest';output.mkdir();controls(protocol,output/'controls');rows=[]
    for case in protocol['cases']:
        print('INDEPENDENT ACTIVE REST AND FULL-GATE JACOBIAN',case['key'],flush=True)
        rows.append(run_case(case,protocol,output/case['key']))
    save(output/'results.json',dict(completed=True,cases=rows,expectedCases=18,timeIntegrated=False,physiologicalRestQualified=False))


def native_witnesses(package,protocol):
    # Evaluate the captured native point, including its ACTUAL captured gates.
    # These points never seed the separate SciPy search above.
    from cleft_rest_analysis import comparison,validate_state
    output=package/'native-witnesses';output.mkdir();rows=[]
    for case in protocol['cases']:
        key=case['key'];folder=package/'study'/key
        try:
            native=json.loads((folder/'result.json').read_text(encoding='utf-8-sig'))
            history=native['acceptedIterations']
            if not history:
                result=dict(completed=True,pointAvailable=False,nativeOutcome=native['outcome'])
            else:
                point=history[-1]['point'];row=deepcopy(case['fixture'])
                row['hairMv']=point['hairMv'];row['states']=point['compartments']
                row['inputs']=[dict(id=g['id'],hairGates=g['hair'],calyxGates=g['calyx'],axonOutgoingPa=0.) for g in point['gates']]
                reference=ActiveReference(row,protocol['reference']['decimalPrecision'])
                state=reference.full_state(row);validate_state(state,len(reference.nodes))
                low=reference.evaluation(row);high=reference.evaluation(row,protocol['reference']['witnessPrecision'])
                with localcontext() as ctx:
                    ctx.prec=protocol['reference']['witnessPrecision']
                    precision=comparison(low,high,protocol['precisionComparison'])
                    candidate=comparison(point['evaluation'],high,protocol['derivativeComparison'])
                    root=residual(physical_rates(high),protocol['rootCriteria'])
                result=dict(completed=True,pointAvailable=True,nativeOutcome=native['outcome'],
                    fullState=[float(x) for x in state],physicalRates=[str(x) for x in physical_rates(high)],
                    precisionWitnesses=[base.decimal_strings(low),base.decimal_strings(high)],precision=precision,
                    nativeEvaluationComparison=candidate,physicalResidual=root,timeIntegrated=False)
        except Exception:result=dict(completed=False,error=traceback.format_exc())
        save(output/(key+'.json'),result);rows.append(dict(key=key,completed=result['completed']))
    save(output/'results.json',dict(completed=True,cases=rows))
