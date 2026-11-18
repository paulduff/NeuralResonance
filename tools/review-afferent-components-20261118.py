"""Read only operator-reported completed afferent components; no test/model run."""
import argparse
from datetime import datetime,timezone
import importlib.util
import json
from pathlib import Path
import sys

ROOT=Path(__file__).resolve().parents[1]
REVISION='751f92fc6fa1101f36229481744c62ee0e82f8e4'


def module(name,path):
    s=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(s);s.loader.exec_module(m);return m


def main(reported):
    assert reported,'Operator completion report required; do not inspect a pending run.'
    out=ROOT/'artifacts/afferent-components-review-20261118';assert not out.exists()
    capture=ROOT/'artifacts/afferent-components-20261118-d3fbc9'
    files=module('afferent_review_files',ROOT/'tools/review-olive-and-motor-20261109.py')
    archive=module('afferent_review_git',ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    result=files.verify(capture,['test-binaries.json','probe-binaries.json'],REVISION)
    result['verifiedGitFiles']=archive.source_archive(files,capture,REVISION)
    sys.path.insert(0,str(capture/'source/tools'))
    try:gate=module('completed_afferent_gate',capture/'source/tools/afferent_component_gate.py')
    finally:sys.path.pop(0)
    gate.runtime(capture);tests=gate.prior.component.inventory.check_tests(capture,gate.COUNTS)
    assert files.digest(capture/'managed-runtime-before.json')==files.digest(capture/'managed-runtime-after.json')
    status=files.read(capture/'window-status.json');assert status['PassedFocusedTests']==166
    for key in ('ComponentChecksPassed','TestExecutionAttempted','AfferentComponentChecksPassed','MatchesQualifiedManagedRuntime',
                'QualifiedDependenciesPreserved','ManagedRuntimePreserved','FrozenPackageUnchanged'):assert status[key] is True,key
    for key in ('IndependentCoupledTrajectoryQualified','StationaryInitializationQualified','SpatialGeometryCalibrated',
                'AfferentFiringQualified','LiveBrainAccessed','OldSavesLoaded','NativeBodyExecuted','PhysiologyQualified',
                'AvatarRecoveryAssessed','FullUnitSuitePassed'):assert status[key] is False,key
    analysis=dict(completed=True,componentChecksPassed=True,tests=tests,independentCoupledTrajectoryQualified=False,
        stationaryInitializationQualified=False,spatialGeometryCalibrated=False,afferentFiringQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    assert analysis==files.read(capture/'analysis.json')
    result.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,passedTests=166,
        componentCriteriaPassed=True,savedAnalysisExactlyReproduced=True,analysis=analysis,
        modelsExecutedDuringReview=False,testsExecutedDuringReview=False,independentCoupledTrajectoryQualified=False,
        stationaryInitializationQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    out.mkdir();(out/'review.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
    print(f"Verified {result['verifiedFilesPerCopy']} files per C:/D: copy and all 166 component cases; saved analysis reproduced without model/test execution.",flush=True)


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    p=argparse.ArgumentParser();p.add_argument('--operator-reported-complete',action='store_true');main(p.parse_args().operator_reported_complete)
