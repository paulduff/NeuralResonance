"""Review reported prescribed head-motion controls without running tests or models."""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import shutil
import sys

ROOT=Path(__file__).resolve().parents[1]
CAPTURE=Path('D:/DNNE-build-work/head-turn-20261119-1047b8')
REVISION='04df5c84e0216122c9f80142044dc3cea811df0a'


def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path)
    result=importlib.util.module_from_spec(spec);spec.loader.exec_module(result);return result


def review(package,reported):
    if not reported:raise ValueError('Wait for the operator final footer before accessing this capture.')
    assert package==CAPTURE
    output=ROOT/'artifacts'/(package.name+'-review')
    assert not output.exists(),'Preserve earlier reviews.'
    common=module('nucleus_files',ROOT/'tools/review-olive-and-motor-20261109.py')
    archive=module('nucleus_git',ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    result=common.verify(package,['test-binaries.json','probe-binaries.json'],REVISION,progress_every=1000)
    result['verifiedGitFiles']=archive.source_archive(common,package,REVISION)
    status=common.read(package/'window-status.json')
    for name in ('ComponentChecksPassed','TestExecutionAttempted','PrescribedKinematicsChecksPassed','MatchesQualifiedManagedRuntime',
                 'QualifiedDependenciesPreserved','ManagedRuntimePreserved','FrozenPackageUnchanged'):
        assert status[name] is True,name
    for name in ('IndependentCoupledTrajectoryQualified','StationaryInitializationQualified','SpatialGeometryCalibrated',
                 'AfferentFiringQualified','LiveBrainAccessed','OldSavesLoaded','NativeBodyExecuted',
                 'PhysiologyQualified','AvatarRecoveryAssessed','FullUnitSuitePassed'):
        assert status[name] is False,name
    assert status['PassedFocusedTests']==32
    assert common.digest(package/'managed-runtime-before.json')==common.digest(package/'managed-runtime-after.json')
    sys.path.insert(0,str(package/'source/tools'))
    try:
        gate=module('nucleus_frozen_gate',package/'source/tools/head_turn_gate.py')
        gate.runtime(package)
        tests=gate.inventory.check_tests(package,gate.COUNTS)
        analysis=common.read(package/'analysis.json')
        assert analysis==dict(completed=True,componentChecksPassed=True,prescribedKinematicsChecksPassed=True,tests=tests,
            independentCoupledTrajectoryQualified=False,stationaryInitializationQualified=False,spatialGeometryCalibrated=False,
            afferentFiringQualified=False,physiologyQualified=False,avatarRecoveryAssessed=False)
        assert tests['passedCases']==32 and tests['exactMethodMultiplicity']
    finally:sys.path.pop(0)
    result.update(reviewedUtc=datetime.now(timezone.utc).isoformat(),operatorReportedCompletion=True,
        passedTests=32,passedClasses=tests['passedClasses'],exactTestInventory=True,componentCriteriaPassed=True,
        prescribedKinematicsChecksPassed=True,savedAnalysisExactlyReproduced=True,analysisSha256=common.digest(package/'analysis.json'),
        prescribedInputControlsOnly=True,modelsExecutedDuringReview=False,testsExecutedDuringReview=False,
        referenceSolvesExecutedDuringReview=False,pendingCaptureAccessed=False,independentCoupledAccuracyQualified=False,
        physiologicalCalibrationQualified=False,afferentFiringQualified=False,liveBrainAuthorityQualified=False,
        avatarBalanceAssessed=False,archiveCopiesShareDrive=True)
    output.mkdir()
    (output/'review.json').write_text(json.dumps(result,indent=2,allow_nan=False)+'\n',encoding='utf-8')
    shutil.copyfile(package/'analysis.json',output/'analysis.json')
    shutil.copyfile(__file__,output/Path(__file__).name)
    print(json.dumps(dict(output=str(output),verifiedFiles=result['verifiedFilesPerCopy'],passedTests=32,
        prescribedKinematicsChecksPassed=True,physiologicalFiringQualified=False),indent=2),flush=True)


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser();parser.add_argument('package',type=Path);parser.add_argument('--operator-reported-complete',action='store_true')
    args=parser.parse_args()
    if not args.operator_reported_complete:parser.error('Wait for the operator final footer; no capture opened without it.')
    review(args.package.resolve(),args.operator_reported_complete)
