"""Audit the reported +60 mV boundary-test failure without rerunning tests."""
import argparse
from collections import Counter
from datetime import datetime,timezone
import importlib.util
import json
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[1]


def module(name,path):
    s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m


def review(capture,reported):
    assert reported and capture.parent==ROOT/'artifacts'
    out=capture.parent/(capture.name+'-review');assert not out.exists()
    files=module('endpoint_failure_files',ROOT/'tools/review-olive-and-motor-20261109.py')
    archive=module('endpoint_failure_archive',ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    revision='314a072e72831ea479ecb134049b9400c21ea9e9'
    result=files.verify(capture,['test-binaries.json','probe-binaries.json'],revision,expected_status='FAILED',expected_exit=1)
    result['verifiedGitFiles']=archive.source_archive(files,capture,revision)
    cfg=files.read(capture/'configuration.json');status=files.read(capture/'window-status.json')
    assert cfg['Revision']==revision and cfg['ExpectedTestCases']==332 and cfg['IsolatedAfferentEndpoint']
    assert status['TestExecutionAttempted'] and status['SpectrumControlsPassed'] and not status['ComponentChecksPassed']
    for k in ('MatchesQualifiedManagedRuntime','QualifiedDependenciesPreserved','ManagedRuntimePreserved','FrozenPackageUnchanged'):assert status[k]
    for k in ('NumericalTrajectoryCriteriaPassed','LiveBrainAccessed','OldSavesLoaded','NativeBodyExecuted','PhysiologyQualified','AvatarRecoveryAssessed'):assert not status[k]
    assert files.digest(capture/'managed-runtime-before.json')==files.digest(capture/'managed-runtime-after.json')
    assert files.read(capture/'spectrum-controls.json')==dict(completed=True,passed=8,expected=8,modelsExecuted=False)
    sys.path.insert(0,str(capture/'source/tools'))
    try:
        gate=module('endpoint_failed_frozen_gate',capture/'source/tools/afferent_endpoint_gate.py');gate.runtime(capture)
        expected=gate.inventory.inventory(capture/'source',gate.COUNTS)
    finally:sys.path.pop(0)
    ns={'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'};trx=ET.parse(capture/'test-results/focused.trx')
    rows=trx.findall('.//t:UnitTestResult',ns);definitions={}
    for test in trx.findall('.//t:TestDefinitions/t:UnitTest',ns):
        method=test.find('t:TestMethod',ns);definitions[test.attrib['id']]=method.attrib['className']+'.'+method.attrib['name']
    assert len(rows)==len({r.attrib['testName'] for r in rows})==332
    assert Counter(definitions[r.attrib['testId']] for r in rows)==expected==Counter(cfg['ExpectedTestMethods'])
    failed=[r for r in rows if r.attrib['outcome']=='Failed'];assert len(failed)==1
    assert sum(r.attrib['outcome']=='Passed' for r in rows)==331
    assert failed[0].attrib['testName'].endswith('OneSidedBoundaryLimitsAreContinuous(boundary: 60)')
    message=failed[0].find('.//t:Message',ns).text;stack=failed[0].find('.//t:StackTrace',ns).text
    assert 'IndexOutOfRangeException' in message and 'AvatarAfferentMembraneKernel.Table' in stack
    for name in ('original-study','study','reference-trajectories','original-replay.json','trajectory-analysis.json'):assert not (capture/name).exists(),name
    result.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,passedTests=331,failedTests=1,
        exactTestInventoryVerified=True,spectrumControlsPassed=8,failureMessage=message,failureStack=stack,
        originalReplayStarted=False,candidateTrajectoriesStarted=False,independentReferencesStarted=False,
        originalFailurePreserved=True,modelsExecutedDuringReview=False,testsExecutedDuringReview=False)
    out.mkdir();(out/'review.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(dict(output=str(out),verifiedFiles=result['verifiedFilesPerCopy'],passedTests=331,failedTests=1),indent=2),flush=True)


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    p=argparse.ArgumentParser();p.add_argument('capture',type=Path);p.add_argument('--operator-reported-complete',action='store_true');a=p.parse_args()
    review(a.capture.resolve(),a.operator_reported_complete)
