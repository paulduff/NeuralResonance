"""Audit operator-completed coupled dynamics components; no model/test rerun."""
import argparse
from datetime import datetime,timezone
import importlib.util
import json
from pathlib import Path
import sys

ROOT=Path(__file__).resolve().parents[1]
def module(name,path):
    s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m


def main(reported):
    assert reported,'Operator completion report required.'
    out=ROOT/'artifacts/cleft-dynamics-review-20261118';assert not out.exists()
    capture=ROOT/'artifacts/cleft-dynamics-20261118-ca2f31'
    files=module('dynamics_review_files',ROOT/'tools/review-olive-and-motor-20261109.py')
    archive=module('dynamics_review_git',ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    result=files.verify(capture,['test-binaries.json','probe-binaries.json'],'b4f0d57fa93fab7f935547345d8e078a05270608')
    result['verifiedGitFiles']=archive.source_archive(files,capture,result['sourceRevision'])
    sys.path.insert(0,str(capture/'source/tools'))
    try:gate=module('completed_dynamics_gate',capture/'source/tools/cleft_dynamics_gate.py')
    finally:sys.path.pop(0)
    gate.runtime(capture);tests=gate.inventory.check_tests(capture,gate.COUNTS)
    assert files.digest(capture/'managed-runtime-before.json')==files.digest(capture/'managed-runtime-after.json')
    status=files.read(capture/'window-status.json');assert status['PassedFocusedTests']==120
    for key in ('ComponentChecksPassed','TestExecutionAttempted','CoupledTimeIntegrationChecked','MatchesQualifiedManagedRuntime',
                'QualifiedDependenciesPreserved','ManagedRuntimePreserved','FrozenPackageUnchanged'):assert status[key] is True,key
    for key in ('IndependentCoupledTrajectoryQualified','StationaryInitializationQualified','SpatialGeometryCalibrated',
                'AfferentFiringQualified','LiveBrainAccessed','OldSavesLoaded','NativeBodyExecuted','PhysiologyQualified',
                'AvatarRecoveryAssessed','FullUnitSuitePassed'):assert status[key] is False,key
    analysis=dict(completed=True,componentChecksPassed=True,tests=tests,independentCoupledTrajectoryQualified=False,
        stationaryInitializationQualified=False,spatialGeometryCalibrated=False,afferentFiringQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    assert analysis==files.read(capture/'analysis.json')
    result.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,passedTests=120,
        componentCriteriaPassed=True,savedAnalysisExactlyReproduced=True,analysis=analysis,
        modelsExecutedDuringReview=False,testsExecutedDuringReview=False,independentCoupledTrajectoryQualified=False,
        stationaryInitializationQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    out.mkdir();(out/'review.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
    print(f"Verified {result['verifiedFilesPerCopy']} files per C:/D: copy and all 120 component cases; saved analysis reproduced without model/test execution.",flush=True)


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    p=argparse.ArgumentParser();p.add_argument('--operator-reported-complete',action='store_true');main(p.parse_args().operator_reported_complete)
