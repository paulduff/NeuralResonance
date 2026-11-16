"""Saved-data refinement audit using qualified reference traces; no ODE solves."""
import argparse
import importlib.util
import json
import os
from pathlib import Path
for name in ('OMP_NUM_THREADS','OPENBLAS_NUM_THREADS','MKL_NUM_THREADS','NUMEXPR_NUM_THREADS'):os.environ[name]='1'
import numpy as np
from native_hinge_protocol import read,make_protocol,CASES
from native_hinge_refinement_protocol import CONDITIONS,CRITERIA


def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path);value=importlib.util.module_from_spec(spec);spec.loader.exec_module(value);return value


def validate(package):
    config=read(package/'configuration.json')
    assert config['IsolatedNativeRefinement'] and config['RefinementConditions']==CONDITIONS
    assert config['RefinementCriteria']==CRITERIA and config['ExpectedNativePhysicsSteps']==26624
    review=read(package/'reference/hinge-comparison-review.json')
    assert review['operatorReportedCompletion'] and review['savedAnalysisExactlyReproduced'] and review['capturedTrajectoryAgreementPassed']
    assert review['independentReferences']==12 and review['geometryControls']==49 and review['mechanicalMethodChecks']==6
    base=module('comparison_math',package/'source/tools/analyse-native-hinge-comparison.py')
    current=base.environment(package)
    assert current==read(package/'preparation-environment.json')==read(package/'qualified/reference-preflight.json')['environment']
    assert current==read(package/'qualified/reference-postflight.json')['environment']
    return base,read(package/'qualified/hinge-input.json')


def audit(package):
    base,spec=validate(package);geometry=base.HingeGeometry(spec['nativeGeometry']);rows=[];data={};total_steps=0
    references={}
    for case in spec['cases']:
        for settings in spec['referenceSettings']:
            key=case['key']+'-'+settings['key'];stats=read(package/'qualified/hinge-reference'/(key+'-statistics.json'))
            assert stats['completed'] and stats['acceptedInternalDomainPassed'] and stats['sampleDomainPassed']
            with np.load(package/'qualified/hinge-reference'/(key+'.npz')) as raw:
                y,f,t=raw['state'],raw['forces'],raw['timeSeconds']
            assert y.shape==(1025,28) and f.shape==(1025,2,7)
            assert np.array_equal(t,np.arange(1025)*case['physicalStepMs']/1000)
            assert base.accounting(spec,case,y,geometry.inertia)['passed'] and base.reference_energy(spec,geometry,case,y)['passed']
            references[(case['key'],settings['key'])]=(y,f)
    for condition in CONDITIONS:
        folder=package/'conditions'/condition['key'];bridge=read(folder/'bridge-analysis.json')
        assert bridge['completed'] and bridge['originalCriteriaPassed'] and bridge['actualPhysicsSteps']==4*condition['steps']
        assert bridge['completedReplyLockControlPassed'] and bridge['fixtureEventsExactlyDelivered']==8
        total_steps+=bridge['actualPhysicsSteps']
        local=make_protocol(folder/'native-bridge',folder/'reference/muscle-input.json',condition['physicsResolution'],condition['muscleSubsteps'])
        physical=dict(local['nativeGeometry']);physical.pop('stepSeconds')
        previous=dict(spec['nativeGeometry']);previous.pop('stepSeconds');assert physical==previous
        for case,original in zip(local['cases'],spec['cases']):
            assert case['key']==original['key'] and case['events']==original['events']
            assert case['durationMs']==original['durationMs'] and case['initialAngleRadians']==original['initialAngleRadians']==0
            d=base.candidate_data(folder/'native-bridge'/case['key'],case);data[(condition['key'],case['key'])]=d
            # All native endpoints are domain/ledger/event/release checked. Only
            # actual common endpoints, never interpolated data, enter contrasts.
            model=base.HingeCoupledReference(spec,case,geometry);y,f=d['state'],d['forces']
            cosine=np.cos(spec['geometry']['pennationRadians'])
            domain=all(m.domain(y[:,sl]) and np.min(d['path'][:,i]-cosine*y[:,sl.start])>0 for i,(m,sl) in enumerate(zip(model.muscles,model.slices)))
            release=max(float(np.max(abs(np.asarray([m.release(t/1000,0) for t in d['timeMs']])-d['release'][:,i]))) for i,m in enumerate(model.muscles))
            path_error=float(np.max(abs(geometry.measure(y[:,0])['length']-d['path'])))
            ledgers=base.accounting(spec,case,y)
            recorded_ok=all(d['recordedLedgers'][k]<=spec[v] for k,v in [('calciumLedger','calciumLedgerAbsolute'),('fiberMomentumLedger','fiberMomentumLedgerNewtonSeconds'),('fiberKineticLedger','fiberKineticLedgerJoules')])
            candidate_indices=np.arange(0,len(y),2 if condition['physicsResolution']==1 else 1)
            reference_indices=np.arange(0,1025,2 if condition['physicsResolution']==-1 else 1)
            assert np.array_equal(d['timeMs'][candidate_indices],np.arange(1025)[reference_indices]*original['physicalStepMs'])
            contrasts=[]
            for settings in spec['referenceSettings']:
                ry,rf=references[(case['key'],settings['key'])]
                error=base.errors(y[candidate_indices],ry[reference_indices],f[candidate_indices],rf[reference_indices])
                contrasts.append(dict(reference=settings['key'],errors=error,passed=base.within(error,spec['candidateBounds'])))
            internal_ok=bool(domain and ledgers['passed'] and recorded_ok and release<=spec['releaseWaveformAbsolute'] and path_error<=spec['pathGeometryMeters'])
            required=condition['key'] in CRITERIA['absoluteAccuracyRequired']
            rows.append(dict(condition=condition['key'],case=case['key'],nativeSamples=len(y),comparedSamples=len(candidate_indices),
                ledgers=ledgers,maximumReleaseDriveError=release,maximumPathErrorMeters=path_error,internalChecksPassed=internal_ok,
                contrasts=contrasts,absoluteAccuracyRequired=required,passed=internal_ok and (not required or all(c['passed'] for c in contrasts))))
    refinement=[];muscle=[];repeat=[]
    for case in spec['cases']:
        key=case['key'];ry,rf=references[(key,'dop853-tight')];errors={}
        for condition in CONDITIONS[:3]:
            d=data[(condition['key'],key)];stride=condition['steps']//512
            assert np.array_equal(d['timeMs'][::stride],np.arange(513)*2*case['physicalStepMs'])
            errors[condition['key']]=base.errors(d['state'][::stride],ry[::2],d['forces'][::stride],rf[::2])
        for left,right in (('coarse','baseline'),('baseline','fine')):
            metrics={name:dict(coarser=errors[left][name],finer=errors[right][name],
                permitted=max(bound*CRITERIA['refinementFloorFraction'],CRITERIA['refinementMaximumRatio']*errors[left][name])) for name,bound in spec['candidateBounds'].items()}
            refinement.append(dict(case=key,left=left,right=right,metrics=metrics,passed=all(m['finer']<=m['permitted'] for m in metrics.values())))
        for left,right in (('baseline','baseline-muscle-fine'),('fine','fine-muscle-fine')):
            a,b=data[(left,key)],data[(right,key)];assert np.array_equal(a['timeMs'],b['timeMs'])
            error=base.errors(a['state'],b['state'],a['forces'],b['forces'])
            bounds={k:v*CRITERIA['muscleRefinementBoundFraction'] for k,v in spec['candidateBounds'].items()}
            muscle.append(dict(case=key,left=left,right=right,errors=error,passed=base.within(error,bounds)))
        old=base.candidate_data(package/'qualified/native-bridge'/key,case);new=data[('baseline',key)]
        error=base.errors(old['state'],new['state'],old['forces'],new['forces'])
        bounds={k:v*CRITERIA['baselineRepeatBoundFraction'] for k,v in spec['candidateBounds'].items()}
        repeat.append(dict(case=key,errors=error,observedStateBitwiseEqual=bool(np.array_equal(old['state'][:,base.OBSERVED],new['state'][:,base.OBSERVED])),passed=base.within(error,bounds)))
    assert total_steps==26624 and len(rows)==20 and len(refinement)==len(muscle)==8 and len(repeat)==4
    return dict(completed=True,nativeSteps=total_steps,nativePhysicalConditions=20,nativeRejectionControls=40,
        conditions=rows,physicalRefinement=refinement,muscleRefinement=muscle,baselineRepeat=repeat,
        originalBoundsChanged=False,referenceSolvesExecuted=False,qualifiedReferencesReused=12,
        numericalCriteriaPassed=all(r['passed'] for r in rows+refinement+muscle+repeat),
        physicalStepRefinementAssessed=True,muscleStepRefinementAssessed=True,commonRefinementSamples=513,
        finestHalfStepReferenceSamplesAssessed=False,fullCoupledTrajectoryQualified=False,
        neuronalModelExecuted=False,physiologyQualified=False,avatarRecoveryAssessed=False)


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('package',type=Path);parser.add_argument('--preflight',action='store_true')
    args=parser.parse_args();package=args.package.resolve()
    if args.preflight:
        validate(package);print('Qualified reference environment and prospective matrix verified; no integration.',flush=True)
    else:
        result=audit(package)
        with (package/'refinement-analysis.json').open('x') as stream:json.dump(result,stream,indent=2,allow_nan=False)
        print(json.dumps({k:v for k,v in result.items() if k not in ('conditions','physicalRefinement','muscleRefinement','baselineRepeat')},indent=2),flush=True)
        if not result['numericalCriteriaPassed']:raise RuntimeError('Native refinement criteria failed; preserve evidence and original bounds.')
