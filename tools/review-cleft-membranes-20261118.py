"""Review operator-completed coupled derivatives using only saved numerical records."""
import argparse
from datetime import datetime, timezone
from decimal import Decimal, localcontext
import importlib.util
import json
from pathlib import Path
import sys

ROOT=Path(__file__).resolve().parents[1]


def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m


def restore(value,key=''):
    if isinstance(value,dict):return {k:restore(v,k) for k,v in value.items()}
    if isinstance(value,list):return [restore(v,key) for v in value]
    return value if key in ('id','leftId','rightId','compartmentId','reservoirId') else Decimal(value)


def main(reported):
    assert reported,'Operator completion report required.'
    output=ROOT/'artifacts/cleft-membranes-review-20261118';assert not output.exists()
    capture=ROOT/'artifacts/cleft-membranes-20261118-74e956'
    common=module('membrane_review_files',ROOT/'tools/review-olive-and-motor-20261109.py')
    archive=module('membrane_review_git',ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    reviewed=common.verify(capture,['test-binaries.json','probe-binaries.json'],'274214bc6c44538fbf0312ef730dcac843d437e3')
    reviewed['verifiedGitFiles']=archive.source_archive(common,capture,reviewed['sourceRevision'])
    sys.path.insert(0,str(capture/'source/tools'))
    try: gate=module('completed_membrane_gate',capture/'source/tools/cleft_membrane_gate.py')
    finally:sys.path.pop(0)
    spec=gate.prerequisites(capture);gate.runtime(capture)
    tests=gate.inventory.check_tests(capture,gate.COUNTS);assert tests['passedCases']==108
    assert common.digest(capture/'managed-runtime-before.json')==common.digest(capture/'managed-runtime-after.json')
    after=common.read(capture/'reference-postflight.json')
    assert after['unchanged'] and after['environment']==common.read(capture/'reference-preflight.json')['environment']==common.read(capture/'preparation-environment.json')
    status=common.read(capture/'window-status.json')
    for name in ('FocusedTestsPassed','NumericalCriteriaPassed','RegressionPassed','ComponentStudyExecutionAttempted',
                 'CoupledDerivativeEvaluated','MatchesQualifiedManagedRuntime','NumericalEnvironmentUnchanged','ManagedRuntimePreserved','FrozenPackageUnchanged'):
        assert status[name] is True,name
    for name in ('TimeIntegrated','SpatialGeometryCalibrated','AfferentFiringQualified','NativeBodyExecuted','LiveBrainAccessed',
                 'OldSavesLoaded','PhysiologyQualified','AvatarRecoveryAssessed','FullUnitSuitePassed'):
        assert status[name] is False,name
    assert common.read(capture/'study/input.json')=={k:spec[k] for k in ('version','kernelVersion','cases','expectedCases','expectedClosedRows','expectedOpenRows')}
    assert common.read(capture/'study/results.json')==dict(completed=True,closedRows=624,openRows=192,membraneRows=216,
        timeIntegrated=False,spatialGeometryCalibrated=False,afferentFiringQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    for name in gate.REGRESSION:assert (capture/'study/regression'/name).read_bytes()==(capture/'reference/regression'/name).read_bytes(),name
    maximum=Decimal(0);count=0;fingerprints={}
    with (capture/'study/membranes.jsonl').open() as native,(capture/'independent-membranes.jsonl').open() as saved:
        for expected,a,b in zip(spec['cases'],native,saved,strict=True):
            row=json.loads(a);reference=json.loads(b)
            assert set(row)=={'input','fingerprint','evaluation'} and row['input']==expected
            identity=json.dumps([expected[k] for k in ('hairParameters','calyxParameters','topology')],sort_keys=True)
            assert len(row['fingerprint'])==64 and fingerprints.setdefault(identity,row['fingerprint'])==row['fingerprint']
            assert reference['index']==count and reference['input']==expected and reference['precision']==[60,90]
            values=[restore(r) for r in reference['expected']];assert len(values)==2
            with localcontext() as context:
                context.prec=90
                gate.base.compare_values(values[0],values[1],Decimal(spec['referenceAbsoluteTolerance']),Decimal(spec['referenceRelativeTolerance']))
                ratio=gate.base.compare_values(row['evaluation'],values[1],Decimal(spec['candidateAbsoluteTolerance']),Decimal(spec['candidateRelativeTolerance']))
                assert str(ratio)==reference['maximumAllowedFraction'];maximum=max(maximum,ratio)
            count+=1
    assert count==216 and len(set(fingerprints.values()))==len(fingerprints)
    analysis=dict(completed=True,numericalComponentCriteriaPassed=True,tests=tests,closedRows=624,openRows=192,membraneRows=count,
        regressionRecordsBitwisePreserved=True,maximumAllowedFraction=str(maximum),decimalPrecisions=[60,90],originalBoundsChanged=False,
        timeIntegrated=False,spatialGeometryCalibrated=False,afferentFiringQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    assert analysis==common.read(capture/'analysis.json')
    reviewed.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,
        savedAnalysisExactlyReproduced=True,independentSavedComparisonsRechecked=True,numericalCriteriaPassed=True,
        passedTests=108,analysis=analysis,modelsExecutedDuringReview=False,testsExecutedDuringReview=False,
        timeIntegrated=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    output.mkdir();(output/'review.json').write_text(json.dumps(reviewed,indent=2)+'\n',encoding='utf-8')
    print(f"Verified {reviewed['verifiedFilesPerCopy']} files per C:/D: copy, 108 tests, 816 byte-identical regression records and 216 saved 60/90-digit comparisons.",flush=True)
    print('Maximum fraction of allowed numerical error: '+str(maximum),flush=True)


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    p=argparse.ArgumentParser();p.add_argument('--operator-reported-complete',action='store_true');main(p.parse_args().operator_reported_complete)
