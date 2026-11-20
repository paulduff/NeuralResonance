"""Review reported saved-data accounting; do not repeat the analysis or model."""
import argparse
from collections import Counter
from datetime import datetime, timezone
import importlib.util
import json
import math
from pathlib import Path
import shutil
import sys
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[1]
CAPTURE=Path('D:/DNNE-build-work/head-turn-reanalysis-20261120-2859f6')
REVISION='44199c0b526a8b7f863697a5bcd4fc5d92a92f4a'


def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path);result=importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result);return result


def review(package):
    assert package==CAPTURE
    output=ROOT/'artifacts'/(package.name+'-review');assert not output.exists(),'Preserve earlier reviews.'
    common=module('head_saved_files',ROOT/'tools/review-olive-and-motor-20261109.py')
    archive=module('head_saved_git',ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    result=common.verify(package,['test-binaries.json','probe-binaries.json'],REVISION,progress_every=1000)
    result['verifiedGitFiles']=archive.source_archive(common,package,REVISION)
    status=common.read(package/'window-status.json');cfg=common.read(package/'configuration.json')
    for key in ('SavedCaptureReanalysis','ConnectedAccountingPassed','AnalyticControlsPassed','ComponentChecksPassed',
                'TestExecutionAttempted','MatchesQualifiedManagedRuntime','QualifiedDependenciesPreserved','ManagedRuntimePreserved','FrozenPackageUnchanged'):
        assert status[key] is True,key
    for key in ('PythonRuntimeChangedThisRun','NativeCaptureAttempted','NativeUnityCompilationQualified','IndependentCoupledTrajectoryQualified',
                'AfferentFiringQualified','LiveBrainAccessed','OldSavesLoaded','NativeBodyExecuted','PhysiologyQualified','AvatarRecoveryAssessed','FullUnitSuitePassed'):
        assert status[key] is False,key
    assert status['PassedFocusedTests']==37 and status['PassedAnalyticControls']==40
    assert common.digest(package/'managed-runtime-before.json')==common.digest(package/'managed-runtime-after.json')
    sys.path.insert(0,str(package/'source/tools'))
    try:
        gate=module('head_saved_gate',package/'source/tools/head_turn_reanalysis_gate.py')
        gate.preflight(package);gate.runtime(package) # Hash/source/environment checks only.
        expected=gate.inventory.inventory(package/'source',gate.COUNTS)
        assert expected==cfg['ExpectedTestMethods']
        ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
        tree=ET.parse(package/'test-results/focused.trx');rows=tree.findall('.//t:UnitTestResult',ns)
        assert len(rows)==len({r.attrib['testName'] for r in rows})==37 and all(r.attrib['outcome']=='Passed' for r in rows)
        methods={}
        for test in tree.findall('.//t:TestDefinitions/t:UnitTest',ns):
            method=test.find('t:TestMethod',ns);methods[test.attrib['id']]=method.attrib['className']+'.'+method.attrib['name']
        assert Counter(methods[r.attrib['testId']] for r in rows)==Counter(expected)
        assert common.read(package/'test-analysis.json')==dict(passedCases=37,exactMethodMultiplicity=True,nativeUnityCompilationQualified=False)
        assert common.read(package/'analytic-controls.json')==dict(completed=True,passed=True,cases=40,methods=gate.control_methods(package/'source'),failures=[],errors=[],skipped=[])
        assert common.read(package/'failure-replay.json')==dict(originalComparisonFailed=True,declaredSchemaIdentityPassed=True)
        original=gate.ORIGINAL
    finally:sys.path.pop(0)
    p=common.read(original/'connected-input.json');a=common.read(package/'analysis.json')
    original_runs=common.read(original/'study/results.json')['runs']
    assert a['completed'] and a['connectedAccountingPassed'] and a['prescribedMotionAccountingPassed']
    assert a['sourceSteps']==2654208 and sum(r['steps'] for r in a['runs'])==2654208
    assert [(r['condition'],r['sourceStepsPerInterval']) for r in a['runs']]==[(c,n) for c in p['conditions'] for n in p['sourceStepsPerInterval']]
    for flag in ('independentConnectedAccuracyQualified','physiologicalCalibrationQualified','afferentFiringQualified','avatarBalanceAssessed'):
        assert a[flag] is False
    events=[];summaries=[]
    for row,saved in zip(a['runs'],original_runs,strict=True):
        condition,count=row['condition'],row['sourceStepsPerInterval']
        assert (saved['condition'],saved['count'])==(condition,count)
        assert row['events']==saved['events'] and row['steps']==saved['steps']==18*count and row['packets']==saved['packets']==18*count//512
        residuals=row['independentLedgerResiduals'];assert residuals.keys()==p['ledgerResidualBounds'].keys()
        conserved=all(math.isfinite(residuals[k]) and 0<=residuals[k]<=bound for k,bound in p['ledgerResidualBounds'].items())
        arithmetic=row['heldInputArithmeticMaximumFraction'];assert math.isfinite(arithmetic) and 0<=arithmetic<=1
        assert row['conservationPassed']==conserved and row['accountingPassed']==(conserved and arithmetic<=1)
        assert row['accountingPassed']
        assert all(len(v)==2 and all(math.isfinite(x) for x in v) and v[0]<=v[1] for v in row['voltageRangesMv'].values())
        folder=original/'study'/f'{condition}-n{count}'
        with (folder/'events.jsonl').open(encoding='utf-8') as stream:captured=[json.loads(line) for line in stream]
        assert len(captured)==row['events']
        for receipt in captured:
            event=receipt['event'];interval=receipt['interval']
            assert event['ordinal']==1 and event['completedStep']==receipt['completedStep']==receipt['startingStep']+1
            assert interval['startMs']<=event['crossing']['timeMs']<=interval['endMs'] and interval['startMv']<0<=interval['endMv']
            events.append(dict(condition=condition,sourceStepsPerInterval=count,originalReceipt=receipt))
        summaries.append({k:row[k] for k in ('condition','sourceStepsPerInterval','steps','packets','events','heldInputArithmeticMaximumFraction','conservationPassed','bundleRangeNm')})
    assert [r['events'] for r in summaries]==[0,0,0,0,1,1]
    result.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,passedTests=37,passedAnalyticControls=40,
        exactTestInventory=True,originalFailurePreserved=True,savedAccountingCriteriaReconciled=True,sourceSteps=2654208,runs=summaries,
        analysisSha256=common.digest(package/'analysis.json'),originalManifestSha256=cfg['OriginalManifestSha256'],
        fullAccountingRecomputedDuringReview=False,modelsExecutedDuringReview=False,testsExecutedDuringReview=False,
        independentConnectedAccuracyQualified=False,physiologicalCalibrationQualified=False,afferentFiringQualified=False,
        liveBrainAuthorityQualified=False,avatarBalanceAssessed=False)
    output.mkdir()
    for name,value in [('review.json',result),('observed-events.json',events)]:
        (output/name).write_text(json.dumps(value,indent=2,allow_nan=False)+'\n',encoding='utf-8')
    shutil.copyfile(package/'analysis.json',output/'analysis.json');shutil.copyfile(__file__,output/Path(__file__).name)
    print(json.dumps(dict(output=str(output),verifiedFiles=result['verifiedFilesPerCopy'],runs=summaries),indent=2),flush=True)


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser();parser.add_argument('package',type=Path);parser.add_argument('--operator-reported-complete',action='store_true');args=parser.parse_args()
    if not args.operator_reported_complete:parser.error('Require the final operator report.')
    review(args.package.resolve())
