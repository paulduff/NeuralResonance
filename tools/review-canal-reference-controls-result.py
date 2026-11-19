"""Review reported analytic-control evidence without rerunning any control."""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import re
import shutil
import sys
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[1]
CAPTURE = Path('D:/DNNE-build-work/canal-controls-20261119-29468a')
REVISION = '0cf7dc3fa070513a6f6341db48d845176e55a1bb'


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    value = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(value)
    return value


def review(package, reported):
    if not reported:
        raise ValueError('Wait for the operator final footer; do not probe the pending controls.')
    assert package == CAPTURE
    output = ROOT / 'artifacts' / (package.name + '-review')
    assert not output.exists(), 'Preserve any previous review.'
    common = module('control_files', ROOT / 'tools/review-olive-and-motor-20261109.py')
    archive = module('control_git', ROOT / 'tools/review-spindle-root-and-causal-motor-20261113.py')
    result = common.verify(package, [], REVISION, progress_every=1000)
    result['verifiedGitFiles'] = archive.source_archive(common, package, REVISION)
    status = common.read(package / 'window-status.json')
    assert status['AnalyticControlsPassed'] is True and status['SourceAndDependenciesPreserved'] is True
    for name in ('NeuronalModelsExecuted', 'NativeBodyExecuted', 'PendingCaptureAccessed', 'ConnectedReferenceExecuted',
                 'LiveBrainAccessed', 'OldSavesLoaded', 'PhysiologyQualified', 'AvatarBalanceAssessed', 'FullUnitSuitePassed'):
        assert status[name] is False, name
    sys.path.insert(0, str(package / 'source/tools'))
    try:
        gate = module('control_frozen_gate', package / 'source/tools/canal_reference_controls_gate.py')
        gate.preflight(package)
        gate.require_controls(package)  # Read-only saved-record checks, not execution.
        expected = gate.control_inventory(package / 'source')
    finally:
        sys.path.pop(0)
    checks = common.read(package / 'analytic-controls.json')
    assert checks['expected'] == checks['executed'] == len(expected) == 16
    assert checks['successful'] and checks['exactInventory']
    assert sorted(checks['started']) == sorted(checks['passed']) == expected
    assert not checks['failures'] and not checks['errors'] and not checks['skipped']
    log = (package / 'controls.log').read_text(encoding='utf-8-sig')
    names = re.findall(r'^(test_\w+) \((check_canal_afferent_reference_(?:sampling\.ReferenceSamplingControls|analysis\.ReferenceAnalysisControls))(?:\.\1)?\) \.\.\. ok$', log, re.M)
    assert sorted(cls + '.' + method for method, cls in names) == expected, 'Saved successful method lines differ from exact control inventory.'
    # These are preservation fingerprints, not a claim that every imported or
    # archived Python function was exercised. A later reuse must match actual
    # control/worker/support code and dependencies, not just the method count.
    with ZipFile(package / 'source.zip') as source:
        python_names = sorted(name for name in source.namelist() if name.startswith('tools/') and name.endswith('.py'))
    preserved = [dict(path=name, sha256=common.digest(package / 'source' / name)) for name in python_names]
    result.update(reviewedUtc=datetime.now(timezone.utc).isoformat(), operatorReportedCompletion=True,
        passedAnalyticControls=16, exactControlInventory=True, successfulLogInventoryMatches=True,
        controlsSha256=common.digest(package / 'analytic-controls.json'),
        environmentSha256=common.digest(package / 'preparation-environment.json'),
        preservedPythonSources=preserved, controlMethods=expected,
        modelsExecutedDuringReview=False, testsExecutedDuringReview=False, referenceSolvesExecutedDuringReview=False,
        nativeCaptureAccessed=False, connectedSensoryAccuracyQualified=False, eventTimeAccuracyQualified=False,
        physiologicalCalibrationQualified=False, liveBrainAuthorityQualified=False, avatarBalanceAssessed=False,
        archiveCopiesShareDrive=True)
    output.mkdir()
    (output / 'review.json').write_text(json.dumps(result, indent=2, allow_nan=False) + '\n', encoding='utf-8')
    shutil.copyfile(package / 'analytic-controls.json', output / 'controls.json')
    shutil.copyfile(__file__, output / Path(__file__).name)
    print(json.dumps(dict(output=str(output), verifiedFiles=result['verifiedFilesPerCopy'],
        passedAnalyticControls=16, preservedPythonSources=len(preserved), noControlsRerun=True), indent=2), flush=True)


if __name__ == '__main__':
    if not __debug__:
        raise RuntimeError('Assertions must remain enabled.')
    parser = argparse.ArgumentParser()
    parser.add_argument('package', type=Path)
    parser.add_argument('--operator-reported-complete', action='store_true')
    args = parser.parse_args()
    if not args.operator_reported_complete:
        parser.error('Wait for the operator final footer; no capture will be opened without that report.')
    review(args.package.resolve(), args.operator_reported_complete)
