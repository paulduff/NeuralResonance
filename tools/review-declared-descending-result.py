"""Review completed declared source/contact controls; no tests or integration."""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import shutil
import sys

ROOT = Path(__file__).resolve().parents[1]
CAPTURE = Path('D:/DNNE-build-work/declared-descending-20261119-82111a')
REVISION = '9416df8a15c5bc9057e4b7f15f6144904bb90f21'


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


def review(package, reported):
    if not reported:
        raise ValueError('Require the operator final footer before opening this capture.')
    assert package == CAPTURE
    output = ROOT / 'artifacts' / (package.name + '-review')
    assert not output.exists(), 'Preserve earlier reviews.'
    common = module('descending_files', ROOT / 'tools/review-olive-and-motor-20261109.py')
    archive = module('descending_git', ROOT / 'tools/review-spindle-root-and-causal-motor-20261113.py')
    result = common.verify(package, ['test-binaries.json', 'probe-binaries.json'], REVISION, progress_every=1000)
    result['verifiedGitFiles'] = archive.source_archive(common, package, REVISION)
    status = common.read(package / 'window-status.json')
    for key in ('ComponentChecksPassed', 'TestExecutionAttempted', 'DescendingAnatomyComponentChecksPassed',
            'MatchesQualifiedManagedRuntime', 'QualifiedDependenciesPreserved', 'ManagedRuntimePreserved',
            'FrozenPackageUnchanged', 'UpstreamInputsAreFixtures'):
        assert status[key] is True, key
    for key in ('MotorFiringQualified', 'DescendingMotorTrajectoryAccuracyQualified', 'IndependentCoupledTrajectoryQualified',
            'StationaryInitializationQualified', 'SpatialGeometryCalibrated', 'AfferentFiringQualified', 'LiveBrainAccessed',
            'OldSavesLoaded', 'NativeBodyExecuted', 'PhysiologyQualified', 'AvatarRecoveryAssessed', 'FullUnitSuitePassed'):
        assert status[key] is False, key
    assert status['PassedFocusedTests'] == 180
    assert common.digest(package / 'managed-runtime-before.json') == common.digest(package / 'managed-runtime-after.json')
    sys.path.insert(0, str(package / 'source/tools'))
    try:
        gate = module('descending_frozen_gate', package / 'source/tools/declared_descending_gate.py')
        gate.runtime(package)
        tests = gate.test_evidence(package)
        partition = gate.partition_audit(package)
        assert all(partition['checks'][k] for k in ('ReticularEqual', 'SpinalEqual', 'CanonicalDeliveriesEqual', 'CanonicalWaitingEqual'))
        source = gate.source_audit(package)
        analysis = common.read(package / 'analysis.json')
        assert analysis == dict(completed=True, componentChecksPassed=True, descendingAnatomyComponentChecksPassed=True,
            tests=tests, sourceEvidence=source, partitionComparison=partition, upstreamInputsAreFixtures=True,
            motorFiringQualified=False, descendingMotorTrajectoryAccuracyQualified=False,
            independentCoupledTrajectoryQualified=False, stationaryInitializationQualified=False, spatialGeometryCalibrated=False,
            afferentFiringQualified=False, physiologyQualified=False, avatarRecoveryAssessed=False)
        assert tests['passedCases'] == 180 and tests['exactMethodMultiplicity']
    finally:
        sys.path.pop(0)
    result.update(reviewedUtc=datetime.now(timezone.utc).isoformat(), operatorReportedCompletion=True,
        passedTests=180, passedClasses=tests['passedClasses'], exactTestInventory=True, componentCriteriaPassed=True,
        descendingAnatomyComponentChecksPassed=True, sourceEvidence=source, savedAnalysisExactlyReproduced=True,
        analysisSha256=common.digest(package / 'analysis.json'), upstreamInputsAreFixtures=True,
        motorFiringQualified=False, descendingMotorTrajectoryAccuracyQualified=False, modelsExecutedDuringReview=False,
        testsExecutedDuringReview=False, referenceSolvesExecutedDuringReview=False, pendingCaptureAccessed=False,
        independentCoupledAccuracyQualified=False, physiologicalCalibrationQualified=False, afferentFiringQualified=False,
        liveBrainAuthorityQualified=False, avatarBalanceAssessed=False, archiveCopiesShareDrive=True)
    output.mkdir()
    (output / 'review.json').write_text(json.dumps(result, indent=2, allow_nan=False) + '\n', encoding='utf-8')
    shutil.copyfile(package / 'analysis.json', output / 'analysis.json')
    shutil.copyfile(__file__, output / Path(__file__).name)
    print(json.dumps(dict(output=str(output), verifiedFiles=result['verifiedFilesPerCopy'], passedTests=180,
        sourceEvidence=source, motorFiringQualified=False), indent=2), flush=True)


if __name__ == '__main__':
    if not __debug__:
        raise RuntimeError('Assertions must remain enabled.')
    parser = argparse.ArgumentParser()
    parser.add_argument('package', type=Path)
    parser.add_argument('--operator-reported-complete', action='store_true')
    args = parser.parse_args()
    if not args.operator_reported_complete:
        parser.error('Wait for the final operator report; no capture opened without it.')
    review(args.package.resolve(), args.operator_reported_complete)
