"""Audit operator-reported connected roots using retained matrix witnesses only."""
import argparse
from datetime import datetime,timezone
import importlib.util
import json
from pathlib import Path
import shutil
import sys

ROOT=Path(__file__).resolve().parents[1]


def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m


def main(capture,reported):
    assert reported,'Operator completion report required.'
    assert capture.parent==ROOT/'artifacts' and capture.name.startswith('afferent-rest-')
    out=capture.parent/(capture.name+'-audit-review');assert not out.exists()
    files=module('rest_review_files',ROOT/'tools/review-olive-and-motor-20261109.py')
    archive=module('rest_review_archive',ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    revision=files.read(capture/'configuration.json')['Revision'];status=files.read(capture/'window-status.json')
    assert status['Status']=='FAILED' and not status['NumericalStationaryCriteriaPassed']
    result=files.verify(capture,['test-binaries.json','probe-binaries.json'],revision,expected_status='FAILED',expected_exit=1)
    result['verifiedGitFiles']=archive.source_archive(files,capture,revision)
    sys.path.insert(0,str(capture/'source/tools'))
    try:
        gate=module('completed_rest_gate',capture/'source/tools/afferent_rest_gate.py')
        gate.runtime(capture);tests=gate.inventory.check_tests(capture,gate.COUNTS)
        analysis=module('corrected_saved_rest_analysis',ROOT/'tools/afferent_rest_analysis.py')
        out.mkdir()
        # Preserve the exact audit implementation beside its result. Every
        # biological equation and numerical acceptance bound is from the capture.
        audit_files=['afferent_rest_analysis.py','afferent_spectrum_witness.py',Path(__file__).name]
        for name in audit_files:shutil.copyfile(ROOT/'tools'/name,out/name)
        audited=analysis.analyse(capture,output=out/'rest-analysis.json',require_all=False)
    finally:sys.path.pop(0)
    assert files.digest(capture/'managed-runtime-before.json')==files.digest(capture/'managed-runtime-after.json')
    assert tests['passedCases']==status['PassedFocusedTests']==196
    for name in ('ComponentChecksPassed','TestExecutionAttempted','NativeSearchesAttempted','IndependentSearchesAttempted',
                 'NumericalEnvironmentUnchanged','MatchesQualifiedManagedRuntime','ManagedRuntimePreserved','FrozenPackageUnchanged'):
        assert status[name] is True,name
    result.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,
        passedTests=196,originalFailurePreserved=True,savedAnalysisCompletedWithLayoutRepair=True,
        exactRecordedEigenResidualsReproduced=True,originalBoundsChanged=False,
        numericalStationaryCriteriaPassed=audited['numericalStationaryCriteriaPassed'],
        independentlyVerifiedNativeRoots=audited['independentlyVerifiedNativeRoots'],independentRoots=audited['independentRoots'],
        nativeSearchFailures=[r['key'] for r in audited['cases'] if not r['nativeSearchReportedRoot']],
        analysisSha256=files.digest(out/'rest-analysis.json'),
        auditSources=[dict(path=n,sha256=files.digest(out/n)) for n in audit_files],
        modelsExecutedDuringReview=False,testsExecutedDuringReview=False,newEigenSolveExecuted=False,
        physiologicalRestQualified=False,afferentFiringQualified=False,avatarRecoveryAssessed=False)
    (out/'review.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
    print(f"Saved audit complete: {result['independentlyVerifiedNativeRoots']}/18 native roots; {result['independentRoots']}/18 independent roots. Original failure retained.",flush=True)


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    p=argparse.ArgumentParser();p.add_argument('capture',type=Path);p.add_argument('--operator-reported-complete',action='store_true')
    a=p.parse_args();main(a.capture.resolve(),a.operator_reported_complete)
