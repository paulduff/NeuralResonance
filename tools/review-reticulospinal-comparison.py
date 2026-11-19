"""Review the reported full-trace retry; never inspect an unreported run."""
import argparse
from collections import Counter
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import shutil
import sys
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[1]
CAPTURE=Path('D:/DNNE-build-work/reticulospinal-20261119-a974ff')
REVISION='de0087852dbcee7ba564a03ce55df9c9398dc035'


def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path)
    result=importlib.util.module_from_spec(spec);spec.loader.exec_module(result);return result


def review(package,reported):
    if not reported:raise ValueError('Wait for the operator final footer before accessing this capture.')
    assert package==CAPTURE
    output=ROOT/'artifacts'/(package.name+'-review');assert not output.exists(),'Preserve earlier reviews.'
    common=module('recurrent_files',ROOT/'tools/review-olive-and-motor-20261109.py')
    archive=module('recurrent_git',ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    status=common.read(package/'window-status.json');assert status['Status'] in ('DIAGNOSTICS_COMPLETE','FAILED')
    successful=status['Status']=='DIAGNOSTICS_COMPLETE'
    result=common.verify(package,['test-binaries.json','probe-binaries.json'],REVISION,status['Status'],0 if successful else 1,progress_every=1000)
    result['verifiedGitFiles']=archive.source_archive(common,package,REVISION)
    for name in ('TestExecutionAttempted','MatchesQualifiedManagedRuntime','QualifiedDependenciesPreserved','ManagedRuntimePreserved','FrozenPackageUnchanged'):
        assert status[name] is True,name
    for name in ('IndependentCoupledTrajectoryQualified','StationaryInitializationQualified','SpatialGeometryCalibrated',
                 'AfferentFiringQualified','LiveBrainAccessed','OldSavesLoaded','NativeBodyExecuted','PhysiologyQualified','AvatarRecoveryAssessed','FullUnitSuitePassed'):
        assert status[name] is False,name
    assert common.digest(package/'managed-runtime-before.json')==common.digest(package/'managed-runtime-after.json')
    sys.path.insert(0,str(package/'source/tools'))
    partition=None;partition_error=None
    try:
        gate=module('recurrent_frozen_gate',package/'source/tools/reticulospinal_gate.py');gate.runtime(package)
        expected=gate.inventory_methods(package/'source');assert sum(expected.values())==146
        ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
        trx=ET.parse(common.io(package/'test-results/focused.trx'));rows=trx.findall('.//t:UnitTestResult',ns);definitions={}
        for unit in trx.findall('.//t:TestDefinitions/t:UnitTest',ns):
            method=unit.find('t:TestMethod',ns);assert method is not None and unit.attrib['id'] not in definitions
            definitions[unit.attrib['id']]=method.attrib['className']+'.'+method.attrib['name']
        assert len(rows)==len({r.attrib['testName'] for r in rows})==146
        assert Counter(definitions[r.attrib['testId']] for r in rows)==expected
        assert all(r.attrib['outcome'] in ('Passed','Failed') for r in rows)
        failed=[dict(method=definitions[r.attrib['testId']],error=r.findtext('t:Output/t:ErrorInfo/t:Message',namespaces=ns))
            for r in rows if r.attrib['outcome']=='Failed']
        if common.io(package/'partition-evidence/runs.json').is_file() and common.io(package/'partition-evidence/comparison.json').is_file():
            try:partition=gate.partition_audit(package)
            except Exception as error:partition_error=type(error).__name__+': '+str(error)
        else:partition_error='Complete runs/comparison evidence was not produced.'
        if successful:
            assert not failed and partition_error is None and partition is not None
            assert all(partition['checks'][k] for k in ('ReticularEqual','SpinalEqual','CanonicalDeliveriesEqual','CanonicalWaitingEqual'))
            tests=gate.test_evidence(package);analysis=common.read(package/'analysis.json')
            assert analysis==dict(completed=True,componentChecksPassed=True,recurrentOwnershipChecksPassed=True,tests=tests,
                partitionComparison=partition,independentCoupledTrajectoryQualified=False,stationaryInitializationQualified=False,
                spatialGeometryCalibrated=False,afferentFiringQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False)
            assert status['PassedFocusedTests']==146 and status['ComponentChecksPassed'] and status['RecurrentOwnershipChecksPassed']
    finally:sys.path.pop(0)
    result.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,
        passedTests=146-len(failed),failedTests=len(failed),failures=failed,exactTestInventory=True,originalStatus=status['Status'],
        recurrentOwnershipChecksPassed=successful,savedAnalysisExactlyReproduced=successful,
        partitionComparison=partition,partitionAuditError=partition_error,
        modelsExecutedDuringReview=False,testsExecutedDuringReview=False,pendingCaptureAccessed=False,
        independentCoupledAccuracyQualified=False,physiologyQualified=False,avatarBalanceAssessed=False,archiveCopiesShareDrive=True)
    if successful:result['analysisSha256']=common.digest(package/'analysis.json')
    output.mkdir();(output/'review.json').write_text(json.dumps(result,indent=2,allow_nan=False)+'\n',encoding='utf-8')
    shutil.copyfile(package/('analysis.json' if successful else 'window-status.json'),output/('analysis.json' if successful else 'failure.json'))
    shutil.copyfile(__file__,output/Path(__file__).name)
    print(json.dumps(dict(output=str(output),verifiedFiles=result['verifiedFilesPerCopy'],passedTests=result['passedTests'],
        failedTests=result['failedTests'],recurrentOwnershipChecksPassed=successful,partitionComparison=partition,partitionAuditError=partition_error),indent=2),flush=True)


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser();parser.add_argument('package',type=Path);parser.add_argument('--operator-reported-complete',action='store_true')
    args=parser.parse_args()
    if not args.operator_reported_complete:parser.error('Wait for the operator final footer; no capture opened without it.')
    review(args.package.resolve(),args.operator_reported_complete)
