"""Review reported observed sensory ingress component completion; saved evidence only."""
import argparse
from datetime import datetime,timezone
import importlib.util
import json
from pathlib import Path
import sys

ROOT=Path(__file__).resolve().parents[1]


def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m


def main(capture,reported):
    assert reported,'Operator completion report required; do not inspect a pending window.'
    assert capture.parent==ROOT/'artifacts' and capture.name.startswith('afferent-ingress-')
    out=capture.parent/(capture.name+'-review');assert not out.exists()
    files=module('afferent_ingress_files',ROOT/'tools/review-olive-and-motor-20261109.py')
    archive=module('afferent_ingress_archive',ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    revision=files.read(capture/'configuration.json')['Revision']
    result=files.verify(capture,['test-binaries.json','probe-binaries.json'],revision)
    result['verifiedGitFiles']=archive.source_archive(files,capture,revision)
    sys.path.insert(0,str(capture/'source/tools'))
    try:
        gate=module('completed_afferent_ingress_gate',capture/'source/tools/afferent_ingress_gate.py')
        gate.runtime(capture);tests=gate.inventory.check_tests(capture,gate.COUNTS)
    finally:sys.path.pop(0)
    assert tests['passedCases']==264
    analysis=dict(completed=True,componentChecksPassed=True,tests=tests,observedIngressChecksPassed=True,
        independentCoupledTrajectoryQualified=False,stationaryInitializationQualified=False,spatialGeometryCalibrated=False,
        afferentFiringQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    assert analysis==files.read(capture/'analysis.json')
    assert files.digest(capture/'managed-runtime-before.json')==files.digest(capture/'managed-runtime-after.json')
    status=files.read(capture/'window-status.json');assert status['PassedFocusedTests']==264
    for key in ('ComponentChecksPassed','TestExecutionAttempted','ObservedIngressChecksPassed','MatchesQualifiedManagedRuntime',
                'QualifiedDependenciesPreserved','ManagedRuntimePreserved','FrozenPackageUnchanged'):assert status[key] is True,key
    for key in ('IndependentCoupledTrajectoryQualified','StationaryInitializationQualified','SpatialGeometryCalibrated','AfferentFiringQualified',
                'LiveBrainAccessed','OldSavesLoaded','NativeBodyExecuted','PhysiologyQualified','AvatarRecoveryAssessed','FullUnitSuitePassed'):assert status[key] is False,key
    result.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,passedTests=264,
        componentCriteriaPassed=True,observedIngressChecksPassed=True,savedAnalysisExactlyReproduced=True,analysis=analysis,
        analysisSha256=files.digest(capture/'analysis.json'),modelsExecutedDuringReview=False,testsExecutedDuringReview=False,
        independentCoupledTrajectoryQualified=False,stationaryInitializationQualified=False,afferentFiringQualified=False,
        physiologyQualified=False,avatarRecoveryAssessed=False)
    out.mkdir();(out/'review.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
    print(f"Verified {result['verifiedFilesPerCopy']} files per C:/D: copy and 264 observed sensory ingress component cases; no model or test rerun.",flush=True)


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser();parser.add_argument('capture',type=Path);parser.add_argument('--operator-reported-complete',action='store_true')
    args=parser.parse_args();main(args.capture.resolve(),args.operator_reported_complete)
