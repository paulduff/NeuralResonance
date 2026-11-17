"""Verify reported cubic receptor/motor evidence; no model or root rerun."""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
import os
from pathlib import Path
import sys

for key in ('OMP_NUM_THREADS','OPENBLAS_NUM_THREADS','MKL_NUM_THREADS','NUMEXPR_NUM_THREADS'):
    os.environ[key]='1'
import numpy as np

ROOT=Path(__file__).resolve().parents[1]


def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path)
    value=importlib.util.module_from_spec(spec); spec.loader.exec_module(value); return value


def main():
    capture=ROOT/'artifacts/receptor-cubic-20261117-94d05d'
    output=ROOT/'artifacts/receptor-cubic-review-20261117'; assert not output.exists()
    common=module('cubic_integrity',ROOT/'tools/review-olive-and-motor-20261109.py')
    print('Verifying completed C:/D: capture, binaries and source...',flush=True)
    review=common.verify(capture,['test-binaries.json','probe-binaries.json'],'b0e011be405c85eee437966e1954e45ea730a454')
    archive=module('cubic_archive',ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    review['verifiedGitFiles']=archive.source_archive(common,capture,review['sourceRevision'])
    sys.path.insert(0,str(capture/'source/tools'))
    dependencies=module('cubic_environment',capture/'source/tools/golgi-reference-resume.py')
    assert dependencies.environment()==common.read(capture/'reference-preflight.json')['environment']
    runtimes=[common.read(capture/name) for name in ('managed-runtime-before.json','managed-runtime-after-legacy-source.json',
        'managed-runtime-after-candidate.json','managed-runtime-after-output.json','managed-runtime-after.json','reference/qualified-managed-runtime.json')]
    assert all(r==runtimes[0] for r in runtimes)
    config=common.read(capture/'configuration.json'); status=common.read(capture/'window-status.json')
    assert config['NoLiveMonitoring'] and config['IsolatedReceptorCubicComparison'] and config['CrossingLocalizationToleranceMs']==1e-9
    for flag in ('ComponentGatePassed','LegacySourceRegressionPassed','LegacyAcceptedStatesPreserved','CandidateMatrixComplete',
        'SourceOutputCaptureComplete','SourceOutputAuditPassed','CoupledNumericalCriteriaPassed','ManagedRuntimePreserved',
        'NumericalEnvironmentPreserved','FrozenPackageUnchanged'):
        assert status[flag]
    for flag in ('LiveBrainAccessed','OldSavesLoaded','PresynapticNeuronsExecuted','NativePhysicsExecuted',
        'MuscleDynamicsExecuted','PhysiologyQualified','AvatarRecoveryAssessed','FullUnitSuitePassed'):
        assert status[flag] is False
    gate=module('cubic_test_inventory',capture/'source/tools/native_crossing_gate.py')
    inventory=gate.gate.check_tests(capture,gate.COUNTS); assert inventory['passedCases']==124
    original=module('cubic_legacy_source',capture/'source/tools/analyse-observed-motor-source.py').audit(capture)
    assert original==common.read(capture/'source-analysis.json') and len(original['cases'])==5
    helper=module('cubic_provenance',ROOT/'tools/prepare-causal-motor.py')
    for folder in ('native-crossing-review-20261116','receptor-comparison-review-20261116','receptor-crossing-review-20261116'):
        prior=ROOT/'artifacts'/folder; helper.verify(prior,['review.json'])
        r=common.read(prior/'review.json'); assert r['operatorReportedCompletion']
        assert common.digest(Path(r['capture'])/'backup-manifest.json')==r['manifestSha256']
    prior=common.read(ROOT/'artifacts/receptor-comparison-review-20261116/review.json')
    assert prior['originalStatus']=='FAILED' and not prior['coupledNumericalCriteriaPassed']
    old_capture=Path(prior['capture']); helper.verify(old_capture,['window-status.json'])
    assert common.read(old_capture/'window-status.json')['Status']=='FAILED'
    spec=common.read(capture/'receptor-membrane-input.json'); criteria=spec['criteria']
    assert spec['crossingInterpolation']=='CubicHermite'
    old_spec=common.read(old_capture/'receptor-membrane-input.json')
    assert {k:v for k,v in spec.items() if k!='crossingInterpolation'}==old_spec
    raw=common.read(capture/'membrane-study/results.json'); legacy=common.read(capture/'legacy-state-analysis.json')
    hashes=common.read(capture/'reference/legacy-trajectories.json')
    assert legacy['completed'] and legacy['passed'] and legacy['originalFailedStatusPreserved']
    assert len(hashes)==len(legacy['cases'])==len(raw['cases'])==18
    for original_hash,result,saved in zip(hashes,raw['cases'],legacy['cases']):
        assert original_hash['key']==result['key']==saved['key']
        sha=common.digest(capture/'membrane-study'/result['key']/'trajectory.csv')
        assert saved==dict(key=result['key'],originalSha256=sha,candidateSha256=sha,exactAcceptedStateBytesPreserved=True)
        assert sha==original_hash['trajectorySha256']==result['trajectorySha256']
    report=common.read(capture/'membrane-reference/comparison.json')
    assert report['completed'] and report['numericalCriteriaPassed'] and report['frozenInputsAndEnvironmentPreserved']
    assert report['criteria']==criteria and report['presynapticEventsAreFixtures'] and not report['presynapticNeuronsExecuted']
    candidate_audit=module('cubic_candidate_audit',capture/'source/tools/audit-receptor-membrane.py')
    print('Rechecking 18 saved native traces and 36 source/receiver captures...',flush=True)
    candidate=candidate_audit.audit(capture); assert candidate['summary']==report['candidateAudit']
    output_audit=module('cubic_output_audit',capture/'source/tools/analyse-receptor-motor-output.py')
    source_output=output_audit.audit(capture,candidate)
    assert source_output==report['sourceOutputAudit']==common.read(capture/'motor-output-analysis.json')
    reference=module('cubic_saved_metrics',capture/'source/tools/receptor_membrane_reference.py')
    def forbidden(*args,**kwargs): raise RuntimeError('Review cannot integrate a model or rerun controls.')
    for name in ('solve','system','analytic_controls','jacobian_control'): setattr(reference,name,forbidden)
    parser=module('cubic_geometry',capture/'source/tools/motoneuron-reference-check.py')
    _,soma,dendrite=parser.model(capture/'reference/motor-source')
    sections=[soma]+[dendrite]*4
    cap=.01*np.array([np.pi*x['diam']*x['L'] for x in sections])*np.array([x['cm'] for x in sections])
    assert len(report['analyticControls'])==6 and all(r['passed'] for r in report['analyticControls'])
    assert sum(len(c['jacobianControls']) for c in report['cases'])==36
    assert [c['key'] for c in report['cases']]==[c['key'] for c in spec['cases']]
    counts=dict(referenceContrasts=0,candidateContrasts=0,refinementContrasts=0)
    predictions=common.read(capture/'reference/saved-endpoint-crossings.json')['rows']; events=[]
    for case in report['cases']:
        print('Rechecking numerical records: '+case['key'],flush=True)
        native=candidate['cases'][case['key']]['conditions']; saved=[]
        for control in case['jacobianControls']:
            assert max(control['relativeDirectionalErrors'])<=criteria['jacobianRelativeError']
        for method,record in zip(spec['referenceMethods'],case['referenceMethods']):
            assert method['key']==record['key']
            with np.load(capture/'membrane-reference'/(case['key']+'-'+method['key']+'.npz'),allow_pickle=False) as data:
                t=data['timeMs']; state=data['state']; crossings=data['crossingMs']
                assert reference.physical(data['acceptedState'],cap,criteria)==record['acceptedNodes']
                assert reference.physical(state,cap,criteria)==record['comparisonPoints']
                assert record['acceptedNodes']['passed'] and record['comparisonPoints']['passed']
                assert crossings.tolist()==record['crossingsMs']
                saved.append((t,state,crossings))
        assert len(saved)==len(case['referenceMethods'])==3 and all(np.array_equal(x[0],saved[0][0]) for x in saved)
        for (left,right),stored in zip(((0,1),(0,2),(1,2)),case['referenceContrasts']):
            error=reference.errors(saved[left][1],saved[right][1],saved[left][2],saved[right][2])
            assert error==stored['error'] and stored['passed']==reference.meets(error,criteria,'reference') and stored['passed']
            counts['referenceContrasts']+=1
        grid,primary,reference_crossings=saved[1]
        for condition,stored in zip(native,case['candidateContrasts']):
            t=condition['time']; y=condition['state']; indices=np.searchsorted(grid,t)
            assert np.array_equal(grid[indices],t)
            error=reference.errors(y,primary[indices],condition['crossings'],reference_crossings)
            physical=reference.physical(y,cap,criteria)
            assert error==stored['error'] and physical==stored['physical']
            assert stored['passed']==(physical['passed'] and reference.meets(error,criteria,'candidate')) and stored['passed']
            counts['candidateContrasts']+=1
            predicted=sorted((r for r in predictions if r['scenario']==case['key'] and r['maximumStepMs']==condition['maximumStepMs']),key=lambda r:r['ordinal'])
            assert len(predicted)==len(condition['crossings'])==len(reference_crossings)
            for ordinal,(event,exact,old) in enumerate(zip(condition['crossings'],reference_crossings,predicted)):
                discrepancy=abs(float(event)-old['estimates']['candidate']['timeMs'])
                assert discrepancy<=config['CrossingLocalizationToleranceMs']
                events.append(dict(scenario=case['key'],maximumStepMs=condition['maximumStepMs'],ordinal=ordinal,
                    nativeCrossingMs=float(event),referenceCrossingMs=float(exact),
                    referenceErrorMs=abs(float(event)-float(exact)),savedEndpointDiscrepancyMs=discrepancy))
        assert len(case['referenceContrasts'])==len(case['candidateContrasts'])==3 and len(case['refinement'])==2
        for (coarse,fine),stored in zip(zip(case['candidateContrasts'],case['candidateContrasts'][1:]),case['refinement']):
            checks={k:coarse['error'][k] is not None and fine['error'][k] is not None and
                fine['error'][k]<=max(criteria['refinementFloor'],criteria['refinementRatio']*coarse['error'][k])
                for k in ('voltageMv','gate','calciumMm','receptor','crossingMs')}
            assert checks==stored['checks'] and stored['passed']==all(checks.values()) and stored['passed']
            counts['refinementContrasts']+=1
        assert case['passed']
    assert counts==dict(referenceContrasts=18,candidateContrasts=18,refinementContrasts=12) and len(events)==27
    assert report['quietHasNoCrossings'] and report['nontrivialExcitedCrossings']
    all_errors=[c['error'] for row in report['cases'] for c in row['candidateContrasts']]
    maximum_errors={k:max(r[k] for r in all_errors) for k in ('voltageMv','gate','calciumMm','receptor','crossingMs')}
    review.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,
        passedTests=124,testInventory=inventory,originalSourceConditions=5,originalSourcesExactlyPreserved=True,
        originalAcceptedStateTrajectoriesPreserved=18,originalFailedCapturePreserved=True,originalBoundsChanged=False,
        savedCandidateAndOutputAuditsReproduced=True,savedNumericalContrastsReproduced=True,
        recordedAnalyticControls=6,recordedJacobianControls=36,contrastCounts=counts,
        candidateAudit=candidate['summary'],sourceOutputAudit=source_output,events=events,
        maximumCandidateErrors=maximum_errors,maximumSavedEndpointDiscrepancyMs=max(e['savedEndpointDiscrepancyMs'] for e in events),
        coupledNumericalCriteriaPassed=True,modelsExecutedDuringReview=False,testsExecutedDuringReview=False,
        rootSearchRerun=False,presynapticNeuronalAuthorityQualified=False,movingBodyCouplingQualified=False,
        physiologyQualified=False,avatarRecoveryAssessed=False)
    output.mkdir()
    (output/'review.json').write_text(json.dumps(review,indent=2,allow_nan=False)+'\n',encoding='utf-8')
    print(json.dumps({k:v for k,v in review.items() if k not in ('testInventory','candidateAudit','sourceOutputAudit','events')},indent=2),flush=True)


if __name__=='__main__':
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    p=argparse.ArgumentParser(); p.add_argument('--operator-reported-complete',required=True,action='store_true'); p.parse_args()
    main()
