"""Reuse reported, reviewed analytic controls without changing tested Python code.

This wrapper adds verified evidence to a newly prepared, not-yet-backed-up full
reference package. It never executes controls or opens an unreported native run.
"""
import argparse
from contextlib import redirect_stdout
from io import StringIO
import json
from pathlib import Path
import shutil

import canal_afferent_reference_gate as original
from afferent_precision_gate import h, read, save
from cleft_trajectory_reference import base

ROOT = Path(__file__).resolve().parents[1]
REVIEW = ROOT / 'artifacts/canal-controls-20261119-29468a-review'
CAPTURE = Path('D:/DNNE-build-work/canal-controls-20261119-29468a')
REVISION = '0cf7dc3fa070513a6f6341db48d845176e55a1bb'


def reviewed_controls():
    h.verify(REVIEW, ['review.json', 'controls.json'])
    review = read(REVIEW / 'review.json')
    assert review['operatorReportedCompletion'] and review['passedAnalyticControls'] == 16
    assert review['exactControlInventory'] and review['successfulLogInventoryMatches']
    assert Path(review['capture']) == CAPTURE and review['sourceRevision'] == REVISION
    assert h.digest(CAPTURE / 'backup-manifest.json') == review['manifestSha256']
    assert h.digest(REVIEW / 'controls.json') == review['controlsSha256']
    assert not any(review[k] for k in ('modelsExecutedDuringReview', 'testsExecutedDuringReview',
        'referenceSolvesExecutedDuringReview', 'nativeCaptureAccessed', 'connectedSensoryAccuracyQualified',
        'eventTimeAccuracyQualified', 'physiologicalCalibrationQualified', 'liveBrainAuthorityQualified', 'avatarBalanceAssessed'))
    names = [row['path'] for row in review['preservedPythonSources']]
    assert len(names) == len(set(names)) == 396
    assert all(n.startswith('tools/') and n.endswith('.py') for n in names)
    for name in names:
        original.relative(name)
    h.verify(CAPTURE, ['analytic-controls.json', 'preparation-environment.json'] + ['source/' + n for n in names])
    assert h.digest(CAPTURE / 'analytic-controls.json') == review['controlsSha256']
    assert h.digest(CAPTURE / 'preparation-environment.json') == review['environmentSha256']
    assert base.environment() == read(CAPTURE / 'preparation-environment.json')
    return review


def authoring_check():
    review = reviewed_controls()
    for row in review['preservedPythonSources']:
        # Working-tree checkout line endings may differ. The frozen package
        # below must pass raw byte hashes for EVERY preserved Python file.
        assert (ROOT / row['path']).read_text(encoding='utf-8-sig') == \
            h.io(CAPTURE / 'source' / row['path']).read_text(encoding='utf-8-sig'), row['path']
    assert original.control_inventory(ROOT) == review['controlMethods']
    print('Reviewed controls and all 396 preserved Python source texts match; no test/model executed or pending native capture opened.', flush=True)
    return review


def prepare(native_review, reported):
    if not reported:
        raise ValueError('The full reference still requires the operator native-completion report and its verified review.')
    review = authoring_check()
    # The original preparer keeps its native-review, source, dependency and
    # unchanged-bound checks. Capture its structured pointer, not a directory
    # search that might accidentally select another or pending package.
    captured = StringIO()
    try:
        with redirect_stdout(captured):
            original.prepare(native_review, reported)
    except Exception:
        print(captured.getvalue(), end='', flush=True)
        raise
    pointer = json.loads(captured.getvalue())
    package = Path(pointer['Package'])
    assert not any((package / name).exists() for name in ('launch.json', 'window-status.json', 'backup-manifest.json', 'analytic-controls.json'))
    cfg = read(package / 'configuration.json')
    assert cfg['Revision'] == pointer['Revision'] and cfg['ExpectedControlMethods'] == review['controlMethods']
    shutil.copyfile(package / 'configuration.json', package / 'configuration-before-controls.json')
    (package / 'verified-controls').mkdir()
    for source, destination in ((REVIEW / 'review.json', 'verified-controls/review.json'),
        (REVIEW / 'controls.json', 'analytic-controls.json'),
        (CAPTURE / 'preparation-environment.json', 'verified-controls/environment.json')):
        shutil.copyfile(h.io(source), h.io(package / destination))
        assert h.digest(source) == h.digest(package / destination)
    save(package / 'verified-controls/provenance.json', dict(reviewManifestSha256=h.digest(REVIEW / 'backup-manifest.json'),
        captureManifestSha256=review['manifestSha256'], controlsSha256=review['controlsSha256'],
        baseConfigurationSha256=h.digest(package / 'configuration-before-controls.json'),
        originalFrozenRevision=REVISION, testsRerun=False, pendingNativeCaptureAccessed=False))
    cfg['VerifiedAnalyticControlsReused'] = True
    cfg['PackageFiles'] = [dict(Path=f.relative_to(h.io(package)).as_posix(), Sha256=h.digest(f))
        for f in sorted(h.io(package).rglob('*')) if f.is_file() and f != h.io(package / 'configuration.json')]
    # This is preparation of a fresh package, before its first backup or launch.
    # Retain the original declaration above and bind it into the final one.
    (package / 'configuration.json').write_text(json.dumps(cfg, indent=2, allow_nan=False), encoding='utf-8')
    preflight(package)
    pointer['VerifiedAnalyticControlsReused'] = True
    (ROOT / 'artifacts' / (package.name + '-prepared.json')).write_text(json.dumps(pointer, indent=2), encoding='utf-8')
    print(json.dumps(pointer, indent=2), flush=True)


def preflight(package):
    original.preflight(package)
    cfg = read(package / 'configuration.json')
    assert cfg['VerifiedAnalyticControlsReused'] is True
    review = read(package / 'verified-controls/review.json')
    provenance = read(package / 'verified-controls/provenance.json')
    assert review['operatorReportedCompletion'] and review['passedAnalyticControls'] == 16
    assert review['exactControlInventory'] and review['successfulLogInventoryMatches']
    assert review['sourceRevision'] == provenance['originalFrozenRevision'] == REVISION
    assert review['manifestSha256'] == provenance['captureManifestSha256']
    assert h.digest(package / 'analytic-controls.json') == review['controlsSha256'] == provenance['controlsSha256']
    assert h.digest(package / 'configuration-before-controls.json') == provenance['baseConfigurationSha256']
    assert not provenance['testsRerun'] and not provenance['pendingNativeCaptureAccessed']
    assert review['controlMethods'] == cfg['ExpectedControlMethods'] == original.control_inventory(package / 'source')
    assert h.digest(package / 'verified-controls/environment.json') == review['environmentSha256']
    assert base.environment() == read(package / 'verified-controls/environment.json') == read(package / 'preparation-environment.json')
    rows = review['preservedPythonSources']
    assert len(rows) == len({r['path'] for r in rows}) == 396
    for row in rows:
        assert h.digest(package / 'source' / original.relative(row['path'])) == row['sha256'], row['path']
    original.require_controls(package)
    print('Reused 16 verified analytic controls; every preserved Python file and numerical dependency matches. No controls rerun.', flush=True)


if __name__ == '__main__':
    if not __debug__:
        raise RuntimeError('Assertions must remain enabled.')
    parser = argparse.ArgumentParser()
    parser.add_argument('command', choices=['authoring-check', 'prepare', 'verified-controls'])
    parser.add_argument('path', type=Path, nargs='?')
    parser.add_argument('--operator-reported-complete', action='store_true')
    args = parser.parse_args()
    if args.command == 'authoring-check':
        authoring_check()
    elif args.command == 'prepare':
        if args.path is None:
            parser.error('A completed native review is required.')
        prepare(args.path, args.operator_reported_complete)
    else:
        if args.path is None:
            parser.error('A frozen reference package is required.')
        preflight(args.path.resolve())
