"""Saved stationary results only: no membrane equations or new eigensolves."""
from decimal import Decimal,InvalidOperation,localcontext
import json
import math
from cleft_rest_protocol import validate
from cleft_trajectory_protocol import labels,group


def comparison(actual,expected,settings):
    """Retain an exceeded bound as evidence, rather than discard its witness."""
    absolute=Decimal(str(settings['absolute']));relative=Decimal(str(settings['relative']))
    maximum=Decimal(0);worst='';count=0
    def visit(a,b,path):
        nonlocal maximum,worst,count
        if isinstance(b,dict):
            assert isinstance(a,dict) and a.keys()==b.keys(),path
            for k in b:visit(a[k],b[k],path+'/'+k)
        elif isinstance(b,list):
            assert isinstance(a,list) and len(a)==len(b),path
            for i,(x,y) in enumerate(zip(a,b,strict=True)):visit(x,y,path+'/'+str(i))
        else:
            try:y=Decimal.from_float(b) if isinstance(b,float) else Decimal(str(b))
            except InvalidOperation:
                assert a==b,path
                return
            x=Decimal.from_float(a) if isinstance(a,float) else Decimal(str(a))
            assert x.is_finite() and y.is_finite(),path
            ratio=abs(x-y)/(absolute+relative*abs(y));count+=1
            if ratio>maximum:maximum=ratio;worst=path
    with localcontext() as ctx:
        ctx.prec=100;visit(actual,expected,'')
    return dict(passed=maximum<=1,maximumAllowedFraction=str(maximum),worstLocation=worst,numericValues=count)


def validate_state(values,count):
    assert len(values)==1+11*count and all(math.isfinite(float(v)) for v in values)
    for i,v in enumerate(values):
        if i==0:continue
        offset=(i-1)%11
        value=Decimal(str(v))
        if offset in (2,3):assert value>0
        if offset>=4:assert 0<=value<=1


def rates_from_saved_evaluation(value):
    rates=[value['coupling']['spatial']['hairMvPerMs']]
    for m,c in zip(value['membranes'],value['coupling']['spatial']['compartments'],strict=True):
        assert m['id']==c['id']
        rates.extend(c[k] for k in ('cleftMvPerMs','calyxMvPerMs','potassiumMmPerMs','sodiumMmPerMs'))
        rates.extend(m['hairGateDerivatives'][k+'PerMs'] for k in ('potassiumLow','calcium','hcn1'))
        rates.extend(m['calyxGateDerivatives'][k+'PerMs'] for k in ('innerKv74','innerHcn2','outerKv74','outerHcn2'))
    return rates


def saved_residual(values,criteria):
    # Pure saved-rate arithmetic; matches the recorded physical groups.
    tolerances=[criteria['voltageRateMvPerMs']]+[
        criteria['voltageRateMvPerMs'] if (i-1)%11<2 else criteria['ionRateMmPerMs'] if (i-1)%11<4 else criteria['gateRatePerMs']
        for i in range(1,len(values))]
    with localcontext() as ctx:
        ctx.prec=100
        fractions=[abs(Decimal(v))/Decimal.from_float(float(t)) for v,t in zip(values,tolerances,strict=True)]
    assert all(x.is_finite() for x in fractions)
    return dict(passed=max(fractions)<=1,maximumAllowedFraction=float(max(fractions)))


def distance(a,b,names,bounds):
    assert len(a)==len(b)==len(names)
    errors={k:0. for k in bounds}
    for x,y,name in zip(a,b,names,strict=True):
        g=group(name);field=dict(voltage='voltageMv',ion='ionMm',gate='gate')[g]
        errors[field]=max(errors[field],abs(float(x)-float(y)))
    ratio=max(errors[k]/bounds[k] for k in bounds)
    return dict(withinDeclaredDistance=ratio<=1,maximumAllowedFraction=ratio,maximumAbsoluteByGroup=errors)


def analyse(package):
    read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
    spec=validate(read(package/'rest-input.json'));native=read(package/'study/results.json');independent=read(package/'independent-rest/results.json')
    assert native['completed'] and native['attempted']==18 and not native['executionFailures']
    assert independent['completed'] and independent['expectedCases']==18
    keys=[c['key'] for c in spec['cases']]
    assert [r['key'] for r in native['cases']]==[r['key'] for r in independent['cases']]==keys
    witnesses=read(package/'native-witnesses/results.json');assert [r['key'] for r in witnesses['cases']]==keys
    expected_input={k:spec[k] for k in ('version','kernelVersion','expectedCases','cases','nativeSettings')}
    assert read(package/'study/input.json')==expected_input
    control=read(package/'independent-rest/controls/controls.json')
    assert control['completed'] and control['passed'] and len(control['cases'])==4 and all(c['passed'] for c in control['cases'])
    rows=[];states={};fingerprints={}
    for case in spec['cases']:
        key=case['key'];r=read(package/'independent-rest'/key/'result.json')
        n=read(package/'study'/key/'result.json');w=read(package/'native-witnesses'/(key+'.json'))
        initial=read(package/'independent-rest'/key/'input.json')
        assert initial['case']==case and initial['settings']==spec['reference'] and not initial['candidateStateUsedAsSeed']
        assert n['version']==spec['kernelVersion'] and n['settings']==spec['nativeSettings']
        assert not n['localStabilityAssessed'] and not n['physiologicalRestQualified'] and not n['liveCellModified']
        assert n['perilymphMv']==case['fixture']['perilymphMv'] and n['bundleDisplacementNanometers']==0
        ids=sorted(s['id'] for s in case['fixture']['states'])
        assert n['axonCurrents']==[dict(id=i,outgoingPa=0.) for i in ids]
        assert len(n['kernelFingerprint'])==len(n['fingerprint'])==64
        assert fingerprints.setdefault(case['geometryKey'],n['kernelFingerprint'])==n['kernelFingerprint']
        assert [a['iteration'] for a in n['acceptedIterations']]==list(range(len(n['acceptedIterations'])))
        assert all(b['point']['scaledResidual']<a['point']['scaledResidual'] for a,b in zip(n['acceptedIterations'],n['acceptedIterations'][1:]))
        native_reported=n['stationaryCriteriaPassed'];native_verified=False;reference_verified=False;stability_checked=False;branch=None
        if w['completed'] and w['pointAvailable']:
            validate_state(w['fullState'],len(ids));lo,hi=w['precisionWitnesses']
            precision=comparison(lo,hi,spec['precisionComparison']);candidate=comparison(n['acceptedIterations'][-1]['point']['evaluation'],hi,spec['derivativeComparison'])
            assert precision==w['precision'] and candidate==w['nativeEvaluationComparison']
            assert rates_from_saved_evaluation(hi)==w['physicalRates']
            root=saved_residual(w['physicalRates'],spec['rootCriteria'])
            assert root['passed']==w['physicalResidual']['passed']
            native_verified=native_reported and precision['passed'] and candidate['passed'] and root['passed']
        if r['completed']:
            assert not r['candidateStateUsedAsSeed'] and not r['timeIntegrated'] and not r['physiologicalRestQualified']
            validate_state(r['fullStateDecimal'],len(ids));assert [float(x) for x in r['fullStateDecimal']]==r['fullState']
            precision=comparison(*r['precisionWitnesses'],spec['precisionComparison'])
            assert precision==r['precision']
            assert rates_from_saved_evaluation(r['precisionWitnesses'][0])==r['physicalRates']
            root=saved_residual(r['physicalRates'],spec['rootCriteria']);assert root['passed']==r['physicalResidual']['passed']
            reference_verified=precision['passed'] and root['passed']
            stability=r['localStability']
            if reference_verified and stability and stability['completed']:
                assert stability['dimension']==1+11*len(ids) and stability['labels']==labels(case['fixture'])[0]
                assert stability['centralDifferencePowers']==spec['stability']['centralDifferencePowers']
                assert not stability['gateEliminationDuringJacobian'] and not stability['timeIntegrated']
                assert (package/'independent-rest'/key/'full-dynamic-jacobians.npz').is_file()
                differences=stability['successiveJacobianDifferences']
                assert len(differences)==2 and all(math.isfinite(x) and x>=0 for x in differences)
                refined=differences[1]<=spec['stability']['jacobianRefinementMaximumRemainingFraction']*differences[0]+spec['stability']['jacobianRefinementAbsoluteFloorPerMs']
                assert refined==stability['sampledJacobianRefinementPassed']
                gate_pass=stability['exactFreeGateBlockMaximumError']<=stability['exactFreeGateBlockAllowedError']
                assert gate_pass==stability['exactFreeGateBlockPassed']
                stability_checked=stability['spectrum']['eigenResidualPassed'] and refined and gate_pass
            if native_verified and reference_verified:
                branch=distance(w['fullState'],r['fullStateDecimal'],labels(case['fixture'])[0],spec['sameRootDistance'])
                states[key]=r['fullStateDecimal']
        row=dict(key=key,nativeSearchOutcome=n['outcome'],nativeSearchReportedRoot=native_reported,
            nativeRootIndependentlyVerified=native_verified,independentRootVerified=reference_verified,
            independentCompleted=r['completed'],nativeWitnessCompleted=w['completed'],sameBranchComparison=branch,
            fullGateLocalStabilityChecked=stability_checked,
            stabilityClassification=r.get('localStability',{}).get('spectrum',{}).get('classification') if r.get('localStability') else None,
            passed=native_verified and reference_verified and stability_checked)
        rows.append(row);print(key,json.dumps(row),flush=True)
    assert native['rootsFound']==sum(r['nativeSearchReportedRoot'] for r in rows)
    seed_comparisons=[]
    for case in spec['cases']:
        key=case['key'];first=case['geometryKey']+'-original'
        if key!=first and key in states and first in states:
            seed_comparisons.append(dict(first=first,second=key,**distance(states[first],states[key],labels(case['fixture'])[0],spec['sameRootDistance'])))
    result=dict(completed=True,numericalStationaryCriteriaPassed=all(r['passed'] for r in rows),cases=rows,
        independentlyVerifiedNativeRoots=sum(r['nativeRootIndependentlyVerified'] for r in rows),
        independentRoots=sum(r['independentRootVerified'] for r in rows),seedComparisons=seed_comparisons,
        allMethodsReachedSameBranch=all(r['sameBranchComparison'] and r['sameBranchComparison']['withinDeclaredDistance'] for r in rows),
        branchUniquenessProven=False,nonlinearStabilityProven=False,timeIntegrated=False,originalBiologyChanged=False,
        physiologicalRestQualified=False,afferentFiringQualified=False,avatarRecoveryAssessed=False)
    with (package/'rest-analysis.json').open('x',encoding='utf-8') as stream:json.dump(result,stream,indent=2,allow_nan=False)
    if not result['numericalStationaryCriteriaPassed']:raise RuntimeError('Some stationary roots or local stability checks remain unresolved; all results retained.')
    return result
