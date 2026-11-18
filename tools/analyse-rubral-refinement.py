"""All native records plus common-grid comparison against completed references."""
import argparse
import importlib.util
from pathlib import Path
import numpy as np
from rubral_refinement_protocol import VERSION,RESOLUTIONS,CRITERIA
from native_hinge_protocol import read

def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m


def analyse(package):
    import rubral_refinement_gate as gate
    gate.preflight(package);gate.check_tests(package)
    previous_audit=read(package/'baseline-audit.json')
    assert previous_audit['completed'] and previous_audit['originalFullCaseAccountingExactlyReproduced']
    assert previous_audit['sourceBodyAccounting']==read(package/'baseline/native-source-analysis.json')
    old=module('refinement_native_audit',package/'source/tools/analyse-rubral-native-body.py')
    base=module('refinement_hinge_arrays',package/'source/tools/analyse-native-hinge-comparison.py')
    bridge=module('refinement_native_wire',package/'source/tools/analyse-native-motor-bridge.py')
    baseline=read(package/'baseline/rubral-hinge-input.json');old_case=next(c for c in baseline['cases'] if c['key']=='strong-left')
    capture=package/'refinement';protocol=read(capture/'protocol.json');outcome=read(capture/'results.json')
    assert protocol==dict(version=VERSION,resolutions=RESOLUTIONS,caseKey='strong-left',durationMs=old_case['durationMs'],
        commonSamples=8000,actualPhysicsSteps=95988,newNeuronalEpochs=5,originalBoundsChanged=False,fullCaseMatrixAssessed=False,
        referenceOnlyMotorReplay=False,neuronalModelExecuted=True,nativePhysicsExecuted=True,physiologyQualified=False,avatarRecoveryAssessed=False)
    assert outcome==dict(completed=True,conditions=[dict(key=r['key'],completed=True,physicsSteps=r['steps']) for r in RESOLUTIONS],
        actualPhysicsSteps=95988,fullCaseMatrixAssessed=False,coupledTrajectoryAccuracyQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    references={};grid=None
    for setting in baseline['referenceSettings']:
        name='strong-left-'+setting['key'];stats=read(package/'baseline/rubral-reference'/(name+'-statistics.json'))
        assert stats['completed'] and stats['acceptedInternalDomainPassed'] and stats['sampleDomainPassed']
        with np.load(package/'baseline/rubral-reference'/(name+'.npz'),allow_pickle=False) as f:
            y=f['state'].copy();forces=f['forces'].copy();times=f['timeSeconds'].copy()
        assert y.shape==(8000,28) and forces.shape==(8000,2,7) and np.isfinite(y).all() and np.isfinite(forces).all()
        if grid is None:grid=times
        else:assert np.array_equal(grid,times)
        for a,b in references.values():assert base.within(base.errors(y,a,forces,b),baseline['referenceBounds'])
        references[setting['key']]=(y,forces)
    reports=[];arrays={}
    event_fields=('muscleIndex','arrivalAtMs','originalSourceNeuronId','originalContactId')
    for r in RESOLUTIONS:
        print('REVIEW NATIVE PARTITION',r['key'],flush=True)
        for name in ('failed-request-000033.json','last-reply-000032.json'):
            assert (capture/r['key']/'reference'/name).read_bytes()==(package/'reference'/name).read_bytes()
        _,spec,data,evidence=old.audit_at(package,capture/r['key'],base,bridge,r['physicsResolution'],r['muscleSubsteps'],True)
        case=spec['cases'][0];assert len(spec['cases'])==1 and case['key']=='strong-left' and case['durationMs']==old_case['durationMs']
        assert [{k:e[k] for k in event_fields} for e in case['events']]==[{k:e[k] for k in event_fields} for e in old_case['events']]
        for field in ('candidateVersion','muscleVersion','geometry','parameterRows','unitRows','initialFiberLengthMeters',
                      'referenceSettings','candidateBounds','referenceBounds'):assert spec[field]==baseline[field],field
        assert {k:v for k,v in spec['nativeGeometry'].items() if k!='stepSeconds'}=={k:v for k,v in baseline['nativeGeometry'].items() if k!='stepSeconds'}
        candidate=data['strong-left'];stride=2**(r['physicsResolution']-1)
        assert np.array_equal(candidate['timeMs'][::stride]/1000,grid)
        y=candidate['state'][::stride];forces=candidate['forces'][::stride];assert y.shape==(8000,28)
        assert base.accounting(spec,case,candidate['state'])['passed']
        assert all(candidate['recordedLedgers'][k]<=spec[v] for k,v in [('calciumLedger','calciumLedgerAbsolute'),
            ('fiberMomentumLedger','fiberMomentumLedgerNewtonSeconds'),('fiberKineticLedger','fiberKineticLedgerJoules')])
        comparisons=[]
        for key,(ry,rf) in references.items():
            errors=base.errors(y,ry,forces,rf)
            comparisons.append(dict(reference=key,errors=errors,passed=base.within(errors,baseline['candidateBounds'])))
        arrays[r['key']]=(y,forces)
        normalized={k:max(c['errors'][k] for c in comparisons)/bound for k,bound in baseline['candidateBounds'].items()}
        reports.append(dict(key=r['key'],allNativeEndpointsReconciled=True,commonSamples=8000,
            source=evidence,comparisons=comparisons,maximumNormalizedByMetric=normalized,absoluteAccuracyPassed=all(c['passed'] for c in comparisons)))
    # Repeat discrepancy is bounded against preserved old candidate errors. This
    # does not pretend to be a bitwise original-trace comparison across sessions.
    original=next(r for r in read(package/'baseline/analysis.json')['cases'] if r['case']=='strong-left')
    repeat=max(abs(a['errors'][k]-b['errors'][k])/bound for a,b in zip(reports[0]['comparisons'],original['comparisons'],strict=True)
               for k,bound in baseline['candidateBounds'].items())
    physical=[next(r for r in reports if r['key']==key) for key in ('muscle-fine','body-fine','body-finer')]
    refinement=[]
    for a,b in zip(physical,physical[1:]):
        checks={k: b['maximumNormalizedByMetric'][k]<=CRITERIA['refinementMaximumRatio']*v+CRITERIA['refinementFloorFraction']
                for k,v in a['maximumNormalizedByMetric'].items()}
        refinement.append(dict(coarse=a['key'],fine=b['key'],metrics=checks,passed=all(checks.values())))
    muscle=[]
    for a,b in (('repeat','muscle-fine'),('body-finer','body-finer-muscle-fine')):
        ay,af=arrays[a];by,bf=arrays[b];differences=base.errors(ay,by,af,bf)
        normalized={k:v/baseline['candidateBounds'][k] for k,v in differences.items()}
        muscle.append(dict(coarse=a,fine=b,normalizedDifferences=normalized,
            smallRelativeToOriginalBounds=all(v<=CRITERIA['muscleRefinementBoundFraction'] for v in normalized.values())))
    absolute=all(r['absoluteAccuracyPassed'] for r in reports if r['key'] in CRITERIA['absoluteAccuracyRequired'])
    result=dict(completed=True,criteria=CRITERIA,cases=reports,repeatMaximumBoundFraction=repeat,
        repeatCriteriaPassed=repeat<=CRITERIA['baselineRepeatBoundFraction'],physicalRefinement=refinement,muscleRefinement=muscle,
        finestAccuracyPassed=absolute,numericalCriteriaPassed=absolute and repeat<=CRITERIA['baselineRepeatBoundFraction'] and all(r['passed'] for r in refinement),
        originalBoundsPreserved=True,independentReferencesReused=3,newReferenceSolves=0,actualPhysicsSteps=95988,
        allNativeEndpointsReconciled=True,commonGridAccuracyOnly=True,fullCaseMatrixAssessed=False,
        upstreamNumericalAccuracyQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    gate.save(package/'analysis.json',result)
    for r in reports:print(r['key'],r['absoluteAccuracyPassed'],r['maximumNormalizedByMetric'],flush=True)
    if not result['numericalCriteriaPassed']:raise RuntimeError('Motor partition criteria failed; preserve the unchanged bounds and full evidence.')


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    p=argparse.ArgumentParser();p.add_argument('package',type=Path);analyse(p.parse_args().package.resolve())
