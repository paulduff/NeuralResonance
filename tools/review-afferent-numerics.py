"""Audit an operator-completed nerve comparison using saved witnesses only."""
import argparse
from datetime import datetime,timezone
import importlib.util
import json
from pathlib import Path
import sys

ROOT=Path(__file__).resolve().parents[1]


def module(name,path):
    s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m


def main(capture,reported):
    assert reported,'Operator completion report required; do not inspect a pending window.'
    assert capture.parent==ROOT/'artifacts' and capture.name.startswith('afferent-numerics-')
    out=capture.parent/(capture.name+'-review');assert not out.exists()
    files=module('nerve_numerical_files',ROOT/'tools/review-olive-and-motor-20261109.py')
    archive=module('nerve_numerical_git',ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    revision=files.read(capture/'configuration.json')['Revision']
    result=files.verify(capture,['test-binaries.json','probe-binaries.json'],revision)
    result['verifiedGitFiles']=archive.source_archive(files,capture,revision)
    sys.path.insert(0,str(capture/'source/tools'))
    try:
        gate=module('completed_nerve_numerical_gate',capture/'source/tools/afferent_numerical_gate.py')
        analysis=module('completed_nerve_numerical_analysis',capture/'source/tools/afferent_numerical_analysis.py')
        gate.runtime(capture);tests=gate.component.prior.component.inventory.check_tests(capture,gate.COUNTS)
        reproduced=analysis.analyse(capture)
    finally:sys.path.pop(0)
    assert reproduced==files.read(capture/'analysis.json') and reproduced['numericalCriteriaPassed']
    assert files.digest(capture/'managed-runtime-before.json')==files.digest(capture/'managed-runtime-after.json')
    status=files.read(capture/'window-status.json');assert status['PassedFocusedTests']==166 and tests['passedCases']==166
    for key in ('ComponentChecksPassed','TestExecutionAttempted','AfferentComponentChecksPassed','NumericalCriteriaPassed','MatchesQualifiedManagedRuntime',
                'QualifiedDependenciesPreserved','ManagedRuntimePreserved','FrozenPackageUnchanged'):assert status[key] is True,key
    for key in ('IndependentCoupledTrajectoryQualified','StationaryInitializationQualified','SpatialGeometryCalibrated','AfferentFiringQualified',
                'LiveBrainAccessed','OldSavesLoaded','NativeBodyExecuted','PhysiologyQualified','AvatarRecoveryAssessed','FullUnitSuitePassed'):assert status[key] is False,key
    result.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,passedTests=166,
        numericalCriteriaPassed=True,savedAnalysisExactlyReproduced=True,analysisSha256=files.digest(capture/'analysis.json'),
        counts=reproduced['counts'],modelsExecutedDuringReview=False,testsExecutedDuringReview=False,
        timeIntegrated=False,afferentFiringQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    out.mkdir();(out/'review.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
    print(f"Verified {result['verifiedFilesPerCopy']} files per C:/D: copy, 166 component cases and all saved numerical comparisons; no model or test executed.",flush=True)


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    p=argparse.ArgumentParser();p.add_argument('capture',type=Path);p.add_argument('--operator-reported-complete',action='store_true')
    a=p.parse_args();main(a.capture.resolve(),a.operator_reported_complete)
