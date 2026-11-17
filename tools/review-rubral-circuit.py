"""Review an operator-reported completed circuit capture without executing cells or tests."""
import argparse
from datetime import datetime,timezone
import importlib.util
import json
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]


def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m


def review(capture,output,reported):
    if not reported:raise ValueError('Wait for the operator completion report before reading this capture.')
    assert not output.exists(),'Preserve earlier reviews.'
    common=module('circuit_review_integrity',ROOT/'tools/review-olive-and-motor-20261109.py')
    config=common.read(capture/'configuration.json')
    assert config['IsolatedRubralBodyEpoch'] and config['NoLiveMonitoring'] and config['ExpectedTestCases']==161
    result=common.verify(capture,['test-binaries.json','probe-binaries.json'],config['Revision'])
    archive=module('circuit_review_archive',ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    result['verifiedGitFiles']=archive.source_archive(common,capture,result['sourceRevision'])
    frozen=module('circuit_completed_audit',capture/'source/tools/rubral_circuit_gate.py')
    analysis=frozen.audit(capture)
    assert analysis==common.read(capture/'analysis.json') and analysis['bodyEpochSourceGatePassed'] and analysis['tests']['passedCases']==161
    assert common.digest(capture/'managed-runtime-before.json')==common.digest(capture/'managed-runtime-after.json')==common.digest(capture/'reference/qualified-managed-runtime.json')
    status=common.read(capture/'window-status.json')
    assert status['BodyEpochSourceGatePassed'] and status['ManagedRuntimePreserved'] and status['FocusedTestsPassed'] and status['SuccessfulConditions']==10
    for key in ('LiveBrainAccessed','OldSavesLoaded','UpstreamNumericalAccuracyQualified','LearnedContactEfficacyTransferred',
                'NativePhysicsExecuted','MuscleDynamicsExecuted','PhysiologyQualified','AvatarRecoveryAssessed','FullUnitSuitePassed'):
        assert status[key] is False,key
    result.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,
        savedAnalysisExactlyReproduced=True,analysis=analysis,bodyEpochSourceGatePassed=True,passedTests=161,
        testsExecutedDuringReview=False,modelsExecutedDuringReview=False,nativePhysicsExecuted=False,
        physiologyQualified=False,avatarRecoveryAssessed=False)
    output.mkdir(parents=True)
    with (output/'review.json').open('x',encoding='utf-8') as f:json.dump(result,f,indent=2,allow_nan=False);f.write('\n')
    print(json.dumps({k:v for k,v in result.items() if k!='analysis'},indent=2),flush=True)


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('capture',type=Path);parser.add_argument('output',type=Path)
    parser.add_argument('--operator-reported-complete',action='store_true',required=True);args=parser.parse_args()
    review(args.capture.resolve(),args.output.resolve(),args.operator_reported_complete)
