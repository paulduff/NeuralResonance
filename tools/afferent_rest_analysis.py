"""Saved connected roots and matrix witnesses; no model or eigensolve."""
from decimal import localcontext
import json
import math
import numpy as np
from afferent_rest_protocol import validate,labels,group
from afferent_numerical_protocol import GATES
from afferent_numerical_analysis import agreement,joint_conservation
from cleft_rest_analysis import comparison,distance,rates_from_saved_evaluation


def read(path):return json.loads(path.read_text(encoding='utf-8-sig'))


def rates(value):
    result=rates_from_saved_evaluation(value['sensory'])
    for n in value['cable']['nodes']:result += [n['potentialMvPerMs']]+[n['membrane']['gateDerivatives'][k+'PerMs'] for k in GATES]
    return result


def state_at(point,names):
    from cleft_rest_reference import HAIR,CALYX
    values=[point['hairMv']];gates={g['id']:g for g in point['gates']}
    for s in sorted(point['compartments'],key=lambda s:s['id']):
        values += [s[k] for k in ('cleftMv','calyxMv','potassiumMm','sodiumMm')]
        values += [gates[s['id']]['hair'][k] for k in HAIR]+[gates[s['id']]['calyx'][k] for k in CALYX]
    for n in sorted(point['cableStates'],key=lambda n:n['id']):values += [n['potentialMv']]+[n['gates'][k] for k in GATES]
    validate_state(values,names);return values


def validate_state(values,names):
    assert len(values)==len(names) and all(math.isfinite(float(v)) for v in values)
    for v,n in zip(values,names,strict=True):
        if group(n)=='ion':assert float(v)>0
        if group(n)=='gate':assert 0<=float(v)<=1


def saved_residual(values,names,criteria):
    # Imports define functions only; this call performs saved-rate arithmetic.
    from afferent_rest_reference import residual
    return residual(values,names,criteria)


def audit_spectrum(matrix,uncertainty,settings,record,path):
    with np.load(path,allow_pickle=False) as data:
        assert set(data.files)=={'eigenvalues','eigenvectors'};values=data['eigenvalues'];vectors=data['eigenvectors']
    n=len(matrix);assert matrix.shape==(n,n) and values.shape==(n,) and vectors.shape==(n,n)
    assert np.isfinite(matrix).all() and np.isfinite(values).all() and np.isfinite(vectors).all()
    assert record['eigenvalues']==[dict(real=float(v.real),imaginary=float(v.imag)) for v in values]
    norm=float(np.linalg.norm(matrix,2));condition=float(np.linalg.cond(vectors))
    residuals=np.linalg.norm(matrix@vectors-vectors*values,axis=0)/(max(1.,norm)*np.linalg.norm(vectors,axis=0))
    assert np.isfinite(residuals).all() and record['normalizedEigenResiduals']==residuals.tolist()
    assert record['maximumNormalizedEigenResidual']==float(residuals.max())
    passed=float(residuals.max())<=settings['eigenResidualLimit'];assert record['eigenResidualPassed']==passed
    assert record['eigenvectorCondition']==(condition if np.isfinite(condition) else None)
    radius=condition*(uncertainty+settings['roundoffMultiplier']*np.finfo(float).eps*n*max(1.,norm)) if np.isfinite(condition) else float('inf')
    assert record['sampledUncertaintyRadiusPerMs']==(radius if np.isfinite(radius) else None)
    right=float(values.real.max());assert right==record['maximumRealPartPerMs']
    classification='inconclusive';margin=settings['classificationMarginPerMs']
    if passed and np.isfinite(radius):
        if right+radius < -margin:classification='locally-decaying estimate'
        elif right-radius > margin:classification='locally-growing estimate'
    assert record['eigenvectorsRetained'] and record['uncertaintyIsEmpirical']
    assert not record['nonlinearStabilityProven'] and not record['physiologicalRestQualified']
    return dict(eigenResidualPassed=passed,classification=classification)


def audit_stability(folder,record,names,state,settings,root_evaluation):
    assert record['completed'] and record['dimension']==len(names) and record['labels']==names
    assert record['centralDifferencePowers']==settings['centralDifferencePowers']==[12,14,16]
    assert not record['gateEliminationDuringJacobian'] and not record['timeIntegrated']
    with np.load(folder/'full-dynamic-jacobians.npz',allow_pickle=False) as f:
        matrices=[f[k].copy() for k in ('coarse','middle','fine')];r1=f['richardsonCoarse'];r2=f['richardsonFine']
        assert all(a.shape==(len(names),len(names)) and np.isfinite(a).all() for a in matrices+[r1,r2])
        assert np.array_equal((16*matrices[1]-matrices[0])/15,r1) and np.array_equal((16*matrices[2]-matrices[1])/15,r2)
        assert np.array_equal(f['state'],np.array(list(map(float,state))))
        scale=[settings[dict(voltage='voltageScaleMv',ion='ionScaleMm',gate='gateScale')[group(k)]] for k in names]
        for i,k in enumerate(names):
            if group(k)=='ion':scale[i]=min(scale[i],abs(float(state[i])))
        assert np.array_equal(f['scale'],np.array(scale))
        indices=[i for i,k in enumerate(names) if group(k)=='gate'];assert np.array_equal(f['gateIndices'],np.array(indices))
        taus=[]
        for m in root_evaluation['sensory']['membranes']:
            taus.extend(m['hairRates'][k]['tauMs'] for k in ('potassiumLow','calcium','hcn1'))
            taus.extend(m['calyx']['rates'][side][k]['tauMs'] for side in ('inner','outer') for k in ('kv74','hcn2'))
        for node in root_evaluation['cable']['nodes']:taus.extend(node['membrane']['rates'][k]['tauMs'] for k in GATES)
        assert len(taus)==len(indices);expected=np.diag([-1/float(t) for t in taus])
        assert np.array_equal(f['exactFreeGateBlock'],expected)
        gate_error=float(np.max(np.abs(r2[np.ix_(indices,indices)]-expected)))
        bound=settings['freeGateBlockAbsoluteTolerance']+settings['freeGateBlockRelativeTolerance']*float(np.max(np.abs(expected)))
    differences=[float(np.linalg.norm(b-a,2)) for a,b in zip(matrices,matrices[1:])]
    uncertainty=float(np.linalg.norm(r2-r1,2))
    refined=differences[1]<=settings['jacobianRefinementMaximumRemainingFraction']*differences[0]+settings['jacobianRefinementAbsoluteFloorPerMs']
    assert record['successiveJacobianDifferences']==differences and record['successiveRichardsonDifferenceNorm']==uncertainty
    assert record['sampledJacobianRefinementPassed']==refined and record['exactFreeGateBlockMaximumError']==gate_error
    assert record['exactFreeGateBlockAllowedError']==bound and record['exactFreeGateBlockPassed']==(gate_error<=bound)
    eigen=audit_spectrum(r2,uncertainty,settings,record['spectrum'],folder/'eigen-witnesses.npz')
    classification=eigen['classification'] if refined and gate_error<=bound else 'inconclusive'
    assert record['spectrum']['classification']==classification
    return dict(passed=refined and gate_error<=bound and eigen['eigenResidualPassed'],classification=classification,
        retainedEigenvectorsVerified=True,newEigenSolveExecuted=False)


def analyse(package):
    spec=validate(read(package/'rest-input.json'));native=read(package/'study/results.json');independent=read(package/'independent-rest/results.json')
    keys=[c['key'] for c in spec['cases']];witnesses=read(package/'native-witnesses/results.json')
    assert native['completed'] and native['attempted']==18 and not native['executionFailures']
    assert independent['completed'] and independent['expectedCases']==18 and witnesses['completed']
    assert [r['key'] for r in native['cases']]==[r['key'] for r in independent['cases']]==[r['key'] for r in witnesses['cases']]==keys
    assert read(package/'study/input.json')=={k:spec[k] for k in ('version','kernelVersion','expectedCases','cases','nativeSettings')}
    controls=read(package/'independent-rest/controls/controls.json')
    assert controls['completed'] and controls['passed'] and len(controls['cases'])==5 and not controls['modelIntegrated']
    assert [{k:c[k] for k in ('name','matrix','expected')} for c in controls['cases']]==spec['stabilityControls']
    for c in controls['cases']:
        e=audit_spectrum(np.array(c['matrix']),0.,spec['stability'],c['result'],package/'independent-rest/controls'/(c['name']+'-eigen-witnesses.npz'))
        assert c['passed'] and e['classification']==c['expected']==c['result']['classification']
    rows=[];states={};fingerprints={}
    for case in spec['cases']:
        key=case['key'];names=labels(case['fixture'])[0];folder=package/'independent-rest'/key
        n=read(package/'study'/key/'result.json');r=read(folder/'result.json');w=read(package/'native-witnesses'/(key+'.json'));initial=read(folder/'input.json')
        assert initial['case']==case and initial['settings']==spec['reference'] and not initial['candidateStateUsedAsSeed']
        assert n['version']==spec['kernelVersion'] and n['settings']==spec['nativeSettings']
        assert not n['localStabilityAssessed'] and not n['physiologicalRestQualified'] and not n['liveCellModified']
        assert n['perilymphMv']==case['fixture']['sensory']['perilymphMv'] and n['bundleDisplacementNanometers']==0
        assert len(n['kernelFingerprint'])==len(n['fingerprint'])==64
        assert fingerprints.setdefault(case['geometryKey'],n['kernelFingerprint'])==n['kernelFingerprint']
        assert [a['iteration'] for a in n['acceptedIterations']]==list(range(len(n['acceptedIterations'])))
        assert all(b['point']['scaledResidual']<a['point']['scaledResidual'] for a,b in zip(n['acceptedIterations'],n['acceptedIterations'][1:]))
        native_verified=False;reference_verified=False;stability=None;branch=None
        if w['completed'] and w['pointAvailable']:
            point=n['acceptedIterations'][-1]['point'];assert w['fullState']==state_at(point,names)
            low,high=w['precisionWitnesses'];precision=comparison(low,high,spec['precisionComparison'])
            candidate=agreement('joints',point['evaluation'],high,spec['derivativeCriteria'])
            assert precision==w['precision'] and candidate==w['nativeEvaluationComparison'] and rates(high)==w['physicalRates']
            root=saved_residual(w['physicalRates'],names,spec['rootCriteria']);assert root==w['physicalResidual']
            row=deepcopy_fixture(case['fixture'],point)
            with localcontext() as ctx:
                ctx.prec=100;accounting=[joint_conservation(row,v,spec['derivativeCriteria']['conservation']).result() for v in (point['evaluation'],low,high)]
            assert accounting==w['accounting']
            native_verified=n['stationaryCriteriaPassed'] and precision['passed'] and candidate['passed'] and root['passed'] and all(a['passed'] for a in accounting)
        if r['completed']:
            assert not r['candidateStateUsedAsSeed'] and not r['timeIntegrated'] and not r['physiologicalRestQualified']
            validate_state(r['fullStateDecimal'],names);assert list(map(float,r['fullStateDecimal']))==r['fullState']
            low,high=r['precisionWitnesses'];precision=comparison(low,high,spec['precisionComparison']);assert precision==r['precision']
            assert rates(low)==r['physicalRates'];root=saved_residual(r['physicalRates'],names,spec['rootCriteria']);assert root==r['physicalResidual']
            # Reconstruct the saved state without evaluating any equations.
            from afferent_rest_reference import ActiveReference
            shape=ActiveReference(case['fixture'],{},60);row=shape.dynamic_row(r['fullStateDecimal'])
            with localcontext() as ctx:
                ctx.prec=100;accounting=[joint_conservation(row,v,spec['derivativeCriteria']['conservation']).result() for v in (low,high)]
            assert accounting==r['accounting'];reference_verified=precision['passed'] and root['passed'] and all(a['passed'] for a in accounting)
            if reference_verified and r['localStability'] and r['localStability']['completed']:
                stability=audit_stability(folder,r['localStability'],names,r['fullStateDecimal'],spec['stability'],low)
            if native_verified and reference_verified:
                branch=distance(w['fullState'],r['fullStateDecimal'],names,spec['sameRootDistance']);states[key]=r['fullStateDecimal']
        rows.append(dict(key=key,nativeSearchOutcome=n['outcome'],nativeSearchReportedRoot=n['stationaryCriteriaPassed'],
            nativeRootIndependentlyVerified=native_verified,independentRootVerified=reference_verified,
            independentCompleted=r['completed'],nativeWitnessCompleted=w['completed'],sameBranchComparison=branch,
            freeGateStability=stability,passed=native_verified and reference_verified and bool(stability and stability['passed'])))
        print(key,json.dumps(rows[-1]),flush=True)
    assert native['rootsFound']==sum(r['nativeSearchReportedRoot'] for r in rows)
    seeds=[]
    for c in spec['cases']:
        first=c['geometryKey']+'-original';key=c['key']
        if key!=first and first in states and key in states:seeds.append(dict(first=first,second=key,**distance(states[first],states[key],labels(c['fixture'])[0],spec['sameRootDistance'])))
    result=dict(completed=True,numericalStationaryCriteriaPassed=all(r['passed'] for r in rows),cases=rows,seedComparisons=seeds,
        independentlyVerifiedNativeRoots=sum(r['nativeRootIndependentlyVerified'] for r in rows),independentRoots=sum(r['independentRootVerified'] for r in rows),
        allMethodsReachedSameBranch=all(r['sameBranchComparison'] and r['sameBranchComparison']['withinDeclaredDistance'] for r in rows),
        branchUniquenessProven=False,nonlinearStabilityProven=False,timeIntegrated=False,originalBiologyChanged=False,
        physiologicalRestQualified=False,afferentFiringQualified=False,avatarRecoveryAssessed=False)
    with (package/'rest-analysis.json').open('x',encoding='utf-8') as stream:json.dump(result,stream,indent=2,allow_nan=False)
    if not result['numericalStationaryCriteriaPassed']:raise RuntimeError('Connected stationary criteria unresolved; all roots, failures and original bounds retained.')
    return result


def deepcopy_fixture(fixture,point):
    from copy import deepcopy
    row=deepcopy(fixture);f=row['sensory'];f['hairMv']=point['hairMv'];f['states']=point['compartments'];row['cableStates']=point['cableStates']
    f['inputs']=[dict(id=g['id'],hairGates=g['hair'],calyxGates=g['calyx'],axonOutgoingPa=0.) for g in point['gates']]
    return row
