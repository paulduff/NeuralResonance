"""Review two operator-completed captures from saved evidence only."""
import argparse
from datetime import datetime, timezone
from decimal import Decimal, localcontext
import importlib.util
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec); spec.loader.exec_module(result); return result


def restore(value, key=''):
    if isinstance(value, dict): return {k:restore(v,k) for k,v in value.items()}
    if isinstance(value, list): return [restore(v,key) for v in value]
    return value if key in ('id','leftId','rightId','compartmentId','reservoirId') else Decimal(value)


def main(reported):
    assert reported, 'Explicit operator completion report required.'
    common = module('skull_open_integrity', ROOT/'tools/review-olive-and-motor-20261109.py')
    archive = module('skull_open_archive', ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    output = ROOT/'artifacts/skull-lock-admission-review-20261118'
    assert not output.exists()
    capture = ROOT/'artifacts/unity-skull-20261118-ac78c5'
    skull = common.verify(capture,['test-binaries.json','probe-binaries.json'],
                          '40a78fd9f2cf30d29c4d67452fc3b84f5491d935','FAILED',1)
    skull['verifiedGitFiles'] = archive.source_archive(common,capture,skull['sourceRevision'])
    frozen = module('skull_lock_frozen_gate',capture/'source/tools/native_skull_gate.py')
    tests = frozen.gate.check_tests(capture,frozen.COUNTS); assert tests['passedCases'] == 80
    runtime = frozen.runtime(capture)
    assert common.digest(capture/'managed-runtime-before.json') == common.digest(capture/'managed-runtime-after.json')
    for row in common.read(capture/'player-files.json'):
        Path(row['Path']).relative_to(capture/'player'); assert common.digest(row['Path']) == row['Sha256']
    for path in common.io(capture/'source/UnityWorld').rglob('*.cs'):
        assert common.digest(path) == common.digest(capture/'built-project'/path.relative_to(common.io(capture/'source/UnityWorld')))
    failure = common.read(capture/'study/bridge/failure.json')
    assert failure['physicsStep'] == 1 and failure['expectedSequence'] == 2
    assert 'Native request lock control incomplete.' in failure['error']
    for sequence, operation, before, after in [(1,'inspect',0,0),(2,'step',0,1)]:
        request = common.read(capture/f'study/bridge/request-{sequence:06}.json')
        reply = common.read(capture/f'study/bridge/reply-{sequence:06}.json')
        assert request['sequence'] == reply['sequence'] == sequence
        assert request['operation'] == reply['operation'] == operation
        assert request['expectedStep'] == before and reply['physicsStep'] == after
        assert request['sessionId'] == reply['sessionId'] == failure['sessionId']
    assert not (capture/'study/bridge/native-request-lock-control.json').exists()
    assert not (capture/'study/bridge/host-reply-lock-control.json').exists()
    assert len(list((capture/'study/bridge').glob('request-*.json'))) == 2
    study = common.read(capture/'study/study-result.json')
    assert not study['completed'] and study['dedicatedPlayerStoppedOnFailure']
    skull.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,
        passedTests=80,tests=tests,managedRuntime=runtime,failedAtLockControlAdmission=True,failedPhysicsStep=1,
        failedRequestSequence=2,lockCountersWereNotSaved=True,nativeSamplingQualified=False,
        modelsExecutedDuringReview=False,testsExecutedDuringReview=False,avatarRecoveryAssessed=False)
    output.mkdir(); (output/'review.json').write_text(json.dumps(skull,indent=2)+'\n',encoding='utf-8')
    print('Skull failed admission: 80 tests and all '+str(skull['verifiedFilesPerCopy'])+' C:/D: files verified.',flush=True)

    output = ROOT/'artifacts/cleft-open-review-20261118'; assert not output.exists()
    capture = ROOT/'artifacts/cleft-open-20261117-db4794'
    opened = common.verify(capture,['test-binaries.json','probe-binaries.json'],
                           '04c4fd61f4df36ba438dfff26b41db28d278d625')
    opened['verifiedGitFiles'] = archive.source_archive(common,capture,opened['sourceRevision'])
    sys.path.insert(0,str(capture/'source/tools'))
    try: base = module('completed_open_cleft_analysis',capture/'source/tools/analyse-cleft-open.py')
    finally: sys.path.pop(0)
    spec = base.prerequisites(capture)
    tests = module('completed_open_test_inventory',ROOT/'tools/receptor_membrane_gate.py').check_tests(capture,base.COUNTS)
    assert tests['passedCases'] == 51
    assert common.digest(capture/'managed-runtime-before.json') == common.digest(capture/'managed-runtime-after.json')
    before=common.read(capture/'reference-preflight.json')['environment']; after=common.read(capture/'reference-postflight.json')
    assert after['unchanged'] and before == after['environment'] == common.read(capture/'preparation-environment.json')
    status=common.read(capture/'window-status.json')
    for key in ('FocusedTestsPassed','NumericalCriteriaPassed','ClosedRegressionPassed','ConstitutiveArithmeticEvaluated',
                'NumericalEnvironmentUnchanged','ManagedRuntimePreserved','FrozenPackageUnchanged'): assert status[key] is True,key
    for key in ('TimeIntegrated','SpatialGeometryCalibrated','AfferentFiringQualified','NativeBodyExecuted','LiveBrainAccessed',
                'OldSavesLoaded','PhysiologyQualified','AvatarRecoveryAssessed','FullUnitSuitePassed'): assert status[key] is False,key
    assert common.read(capture/'study/input.json') == {k:spec[k] for k in ('version','openVersion','spatialVersion','cases','expectedCases','expectedClosedCases')}
    assert common.read(capture/'study/results.json') == dict(completed=True,closedRows=624,openRows=192,
        timeIntegrated=False,spatialGeometryCalibrated=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    for name in base.FILES: assert (capture/'study/closed'/name).read_bytes() == (capture/'reference/closed'/name).read_bytes()
    old_fps={row['input']['index']:row['fingerprint'] for row in
        (json.loads(line) for line in (capture/'reference/closed/spatial.jsonl').read_text().splitlines())}
    fingerprints={}; maximum=Decimal(0); count=0
    with (capture/'study/open.jsonl').open() as native,(capture/'independent-open.jsonl').open() as reference:
        for expected,a,b in zip(spec['cases'],native,reference,strict=True):
            row=json.loads(a); saved=json.loads(b)
            assert set(row)=={'input','fingerprint','spatialFingerprint','evaluation'} and row['input']==expected
            assert row['spatialFingerprint']==old_fps[expected['spatial']['index']]
            identity=json.dumps([row['spatialFingerprint'],expected['reservoir'],expected['faces']],sort_keys=True)
            assert len(row['fingerprint'])==64 and fingerprints.setdefault(identity,row['fingerprint'])==row['fingerprint']
            assert saved['index']==count and saved['input']==expected and saved['precision']==[60,90]
            values=[restore(v) for v in saved['expected']]; assert len(values)==2
            with localcontext() as context:
                context.prec=90
                base.base.compare_values(values[0],values[1],Decimal(spec['referenceAbsoluteTolerance']),Decimal(spec['referenceRelativeTolerance']))
                ratio=base.base.compare_values(row['evaluation'],values[1],Decimal(spec['candidateAbsoluteTolerance']),Decimal(spec['candidateRelativeTolerance']))
                assert str(ratio)==saved['maximumAllowedFraction']; maximum=max(maximum,ratio)
            count+=1
    assert count==192 and len(set(fingerprints.values()))==len(fingerprints)
    analysis=dict(completed=True,numericalComponentCriteriaPassed=True,tests=tests,closedRows=624,openRows=count,
        closedRecordsBitwisePreserved=True,maximumAllowedFraction=str(maximum),decimalPrecisions=[60,90],
        originalBoundsChanged=False,timeIntegrated=False,spatialGeometryCalibrated=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    assert analysis==common.read(capture/'analysis.json')
    opened.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,
        savedAnalysisExactlyReproduced=True,independentSavedComparisonsRechecked=True,numericalCriteriaPassed=True,
        passedTests=51,analysis=analysis,modelsExecutedDuringReview=False,testsExecutedDuringReview=False,
        timeIntegrated=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    output.mkdir(); (output/'review.json').write_text(json.dumps(opened,indent=2)+'\n',encoding='utf-8')
    print('Open cleft: 51 tests, 624 byte-identical regression and 192 high-precision comparisons verified; '+
          str(opened['verifiedFilesPerCopy'])+' files per C:/D: copy; maximum allowed fraction '+str(maximum),flush=True)


if __name__=='__main__':
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser(); parser.add_argument('--operator-reported-complete',action='store_true')
    main(parser.parse_args().operator_reported_complete)
