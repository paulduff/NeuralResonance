"""Freeze the full sensory comparison only after reported native completion.

Preparation/preflight inspect preserved completed evidence, never live processes.
All controls and model/reference execution belong to the visible frozen window.
"""
import argparse
import ast
from datetime import datetime, timezone
import importlib
import json
from pathlib import Path, PurePosixPath
import shutil
import subprocess
import sys
import unittest
import uuid
from zipfile import ZipFile

from afferent_precision_gate import h, read, save
from cleft_trajectory_reference import base
from canal_afferent_reference_protocol import protocol
import canal_afferent_gate as native_gate

ROOT = Path(__file__).resolve().parents[1]
NATIVE = Path('D:/DNNE-build-work/canal-afferent-20261119-6d0480')
NATIVE_REVISION = 'c8466ff24b58588ec140cd8ef411883af244b899'
WAVEFORM = ROOT / 'artifacts/canal-waveform-20261119-2dd76a-review'
WAVEFORM_REVISION = 'deb4b145bab025864bc49f827e81084c5a0307a7'
CONTROL_CLASSES = {
    'check_canal_afferent_reference_sampling': ('ReferenceSamplingControls', 7),
    'check_canal_afferent_reference_analysis': ('ReferenceAnalysisControls', 8),
}
NATIVE_PRESERVED = sorted(set(native_gate.PRESERVED + native_gate.SENSORY_PRESERVED + [
    'src/NRE.SimAvatar/AvatarCanalAfferentClock.cs', 'src/NRE.SimAvatar/AvatarCanalAfferentInput.cs',
    'tests/NeuralResonanceEngine.DNNE.Tests/CanalAfferentInputTests.cs',
    'tools/NativeMotorBridgeProbe/CanalAfferentStudy.cs', 'tools/canal_afferent_protocol.py',
    'tools/canal_afferent_analysis.py', 'tools/canal_afferent_gate.py']))
WAVEFORM_PRESERVED = ['tools/canal_afferent_reference_input.py', 'tools/check_canal_afferent_reference_input.py',
    'tools/coupled_canal_reference.py', 'tools/canal_waveform_study.py', 'tools/canal_waveform_gate.py',
    'tools/canal_bundle_protocol.py', 'docs/reference-data/vestibular-ifediba-2007.json']
RESERVE_BYTES = 32 * 1024**3


def relative(name):
    path = PurePosixPath(name)
    assert name and not path.is_absolute() and '..' not in path.parts and ':' not in name and '\\' not in name
    return path


def control_inventory(root):
    expected = []
    for name, (class_name, count) in CONTROL_CLASSES.items():
        tree = ast.parse((root / 'tools' / (name + '.py')).read_text(encoding='utf-8'))
        classes = [node for node in tree.body if isinstance(node, ast.ClassDef) and node.name == class_name]
        assert len(classes) == 1
        methods = sorted(node.name for node in classes[0].body if isinstance(node, ast.FunctionDef) and node.name.startswith('test_'))
        assert len(methods) == len(set(methods)) == count
        expected.extend(name + '.' + class_name + '.' + method for method in methods)
    assert len(expected) == 15
    return sorted(expected)


def verified_origin(folder, names):
    """Bind each selected original file to both original manifest copies."""
    manifest = read(folder / 'backup-manifest.json')
    entries = {r['Path'].replace('\\', '/'): r for r in manifest['Files']}
    assert len(entries) == len(manifest['Files'])
    for name in entries:
        relative(name)
    names = sorted(set(names))
    h.verify(folder, names)
    return dict(folder=str(folder), backup=manifest['Destination'], manifestSha256=h.digest(folder / 'backup-manifest.json'),
                files=[dict(path=n, sha256=entries[n]['Sha256'], bytes=entries[n]['Bytes']) for n in names])


def verify_originals(provenance):
    for origin in provenance['origins']:
        folder = Path(origin['folder'])
        manifest = read(folder / 'backup-manifest.json')
        assert manifest['Destination'] == origin['backup']
        assert h.digest(folder / 'backup-manifest.json') == origin['manifestSha256']
        h.verify(folder, [row['path'] for row in origin['files']])
        for row in origin['files']:
            assert h.digest(folder / row['path']) == row['sha256']


def prepare(review_folder, reported):
    if not reported:
        raise ValueError('Preparation requires the operator final footer and a completed, D:-verified native review.')
    assert not h.git('status', '--porcelain'), 'Commit first.'
    revision = h.git('rev-parse', 'HEAD')
    assert revision == h.git('rev-parse', 'origin/main'), 'Push first.'
    review_folder = review_folder.resolve()
    assert review_folder == (ROOT / 'artifacts' / (NATIVE.name + '-review')).resolve()
    h.verify(review_folder, ['review.json', 'analysis.json'])
    review = read(review_folder / 'review.json')
    assert review['operatorReportedCompletion'] and review['connectedAccountingPassed'] and review['savedAnalysisExactlyReproduced']
    assert review['sourceRevision'] == NATIVE_REVISION and Path(review['capture']) == NATIVE
    assert review['passedTests'] == 284 and review['sourceSteps'] == 2293760 and review['nativeRuns'] == 6
    assert not review['independentConnectedAccuracyQualified'] and not review['physiologicalCalibrationQualified']
    assert h.digest(NATIVE / 'backup-manifest.json') == review['manifestSha256']
    assert h.digest(NATIVE / 'analysis.json') == review['analysisSha256']
    assert read(NATIVE / 'analysis.json') == read(review_folder / 'analysis.json')
    h.verify(WAVEFORM, ['review.json', 'analysis.json'])
    waveform = read(WAVEFORM / 'review.json')
    assert waveform['operatorReportedCompletion'] and waveform['numericalCriteriaPassed']
    assert waveform['savedComparisonExactlyReproduced'] and waveform['savedReferenceMatrixProductsVerified']
    assert waveform['sourceRevision'] == WAVEFORM_REVISION and waveform['passedAnalyticControls'] == 21 and waveform['verifiedQueries'] == 1060
    wave_capture = Path(waveform['capture'])
    assert h.digest(wave_capture / 'backup-manifest.json') == waveform['manifestSha256']
    assert h.digest(wave_capture / 'analysis.json') == waveform['analysisSha256']
    assert read(wave_capture / 'analysis.json') == read(WAVEFORM / 'analysis.json')
    spec = protocol(read(NATIVE / 'baseline/bundle-input.json'), read(NATIVE / 'sensory/trajectory-input.json'))
    assert spec['connected'] == read(NATIVE / 'connected-input.json')
    assert read(wave_capture / 'baseline/bundle-input.json') == spec['connected']['bundle']
    environment = base.environment()
    assert environment == read(NATIVE / 'preparation-environment.json') == read(wave_capture / 'preparation-environment.json')
    tests = control_inventory(ROOT)
    native_names = ['connected-input.json', 'analysis.json', 'study/input.json', 'study/results.json',
        'window-status.json', 'test-analysis.json', 'configuration.json', 'baseline/bundle-input.json',
        'sensory/trajectory-input.json', 'sensory/source-decisions.json', 'qualified-managed-runtime.json',
        'managed-runtime-before.json', 'managed-runtime-after.json', 'preparation-environment.json']
    for condition in spec['conditions']:
        for count in spec['connected']['sourceStepsPerInterval']:
            native_names += [f'study/{condition}-n{count}/{name}' for name in ('initial.json', 'final.json', 'trajectory.csv.gz')]
    # Hash all original capture files, including packets/events and both archive
    # copies. Copy only the exact subset needed by the independent comparison.
    native_all = [r['Path'].replace('\\', '/') for r in read(NATIVE / 'backup-manifest.json')['Files']]
    wave_all = [r['Path'].replace('\\', '/') for r in read(wave_capture / 'backup-manifest.json')['Files']]
    origins = [verified_origin(NATIVE, native_all), verified_origin(wave_capture, wave_all),
               verified_origin(review_folder, ['review.json', 'analysis.json']), verified_origin(WAVEFORM, ['review.json', 'analysis.json'])]
    for capture, paths in ((NATIVE, NATIVE_PRESERVED), (wave_capture, WAVEFORM_PRESERVED)):
        for name in paths:
            assert (ROOT / name).read_text(encoding='utf-8-sig') == h.io(capture / 'source' / name).read_text(encoding='utf-8-sig'), name
    input_bytes = sum(h.io(NATIVE / name).stat().st_size for name in native_names)
    assert shutil.disk_usage('D:/').free >= RESERVE_BYTES + 2 * input_bytes, 'Space for both frozen input copies and reference output required.'
    package = Path('D:/DNNE-build-work') / ('canal-reference-' + datetime.now(timezone.utc).strftime('%Y%m%d') + '-' + uuid.uuid4().hex[:6])
    backup = Path('D:/DNNE-desktop-runs/UnityWorld') / package.name
    assert not package.exists() and not backup.exists()
    package.mkdir()
    subprocess.run(['git', 'archive', '--format=zip', '--output=' + str(package / 'source.zip'), revision], cwd=ROOT, check=True)
    with ZipFile(package / 'source.zip') as archive:
        assert archive.comment.decode().strip() == revision
        for entry in archive.infolist():
            name = relative(entry.filename)
            assert ((entry.external_attr >> 16) & 0o170000) != 0o120000
            target = h.io(package / 'source' / name)
            if entry.is_dir():
                target.mkdir(parents=True, exist_ok=True)
            else:
                target.parent.mkdir(parents=True, exist_ok=True)
                with target.open('xb') as stream:
                    stream.write(archive.read(entry))
    copied = []

    def copy(source, name):
        target = h.io(package / relative(name))
        target.parent.mkdir(parents=True, exist_ok=True)
        with h.io(source).open('rb') as original, target.open('xb') as output:
            shutil.copyfileobj(original, output, length=4 * 1024**2)
        assert h.digest(source) == h.digest(target)
        copied.append(dict(path=name, source=str(source), sha256=h.digest(source)))

    for name in native_names:
        copy(NATIVE / name, 'native/' + name)
    copy(review_folder / 'review.json', 'reviewed-connected.json')
    copy(review_folder / 'analysis.json', 'reviewed-connected-analysis.json')
    copy(WAVEFORM / 'review.json', 'reviewed-waveform.json')
    copy(WAVEFORM / 'analysis.json', 'reviewed-waveform-analysis.json')
    copy(wave_capture / 'analysis.json', 'baseline/waveform-analysis.json')
    for precision in (60, 90):
        copy(wave_capture / f'baseline/angular-{precision}.json', f'baseline/angular-{precision}.json')
    copy(NATIVE / 'sensory/source-decisions.json', 'source-decisions.json')
    copy(NATIVE / 'qualified-managed-runtime.json', 'qualified-managed-runtime.json')
    copy(package / 'source/tools/backup-unity-run.ps1', 'backup-run.ps1')
    preserved = []
    for capture, paths in ((NATIVE, NATIVE_PRESERVED), (wave_capture, WAVEFORM_PRESERVED)):
        for name in paths:
            expected = h.digest(capture / 'source' / name)
            assert h.digest(package / 'source' / name) == expected, 'Frozen byte identity differs: ' + name
            preserved.append(dict(path=name, capture=str(capture), sha256=expected))
    save(package / 'continuous-reference-input.json', spec)
    save(package / 'preparation-environment.json', environment)
    save(package / 'provenance.json', dict(origins=origins, copiedFiles=copied, preservedSources=preserved,
        operatorReportedCompletion=True, originalNativeCaptureReadOnly=True, originalBoundsChanged=False,
        modelsExecutedDuringPreparation=False, testsExecutedDuringPreparation=False, pendingCaptureAccessed=False))
    files = [dict(Path=f.relative_to(h.io(package)).as_posix(), Sha256=h.digest(f))
             for f in sorted(h.io(package).rglob('*')) if f.is_file()]
    save(package / 'configuration.json', dict(Revision=revision, PackageFiles=files, BackupDestination=str(backup),
        PythonExecutable=sys.executable, PythonExecutableSha256=h.digest(sys.executable),
        IsolatedContinuousCanalReference=True, NoLiveMonitoring=True, ExpectedControls=15, ExpectedControlMethods=tests,
        ExpectedReferenceCases=6, ExpectedNativeSteps=2293760, ExpectedUniqueSamplesPerReference=655361,
        MinimumPhaseReserveBytes=RESERVE_BYTES, NewNativeCaptureRequested=False, LiveSelectionChanged=False,
        PhysiologicalCalibrationQualified=False, AvatarBalanceAssessed=False, ArchiveCopiesShareDrive=True))
    pointer = dict(Package=str(package), Backup=str(backup), Revision=revision, NoLiveMonitoring=True,
                   Launcher=str(package / 'source/tools/start-canal-reference-window.ps1'))
    save(ROOT / 'artifacts' / (package.name + '-prepared.json'), pointer)
    print(json.dumps(pointer, indent=2), flush=True)


def preflight(package):
    cfg, provenance = read(package / 'configuration.json'), read(package / 'provenance.json')
    assert cfg['IsolatedContinuousCanalReference'] and cfg['NoLiveMonitoring']
    assert cfg['ExpectedControls'] == 15 and cfg['ExpectedReferenceCases'] == 6
    assert cfg['ExpectedNativeSteps'] == 2293760 and cfg['ExpectedUniqueSamplesPerReference'] == 655361
    assert cfg['MinimumPhaseReserveBytes'] == RESERVE_BYTES
    assert not any(cfg[k] for k in ('NewNativeCaptureRequested', 'LiveSelectionChanged', 'PhysiologicalCalibrationQualified', 'AvatarBalanceAssessed'))
    assert provenance['operatorReportedCompletion'] and provenance['originalNativeCaptureReadOnly']
    assert not any(provenance[k] for k in ('originalBoundsChanged', 'modelsExecutedDuringPreparation', 'testsExecutedDuringPreparation', 'pendingCaptureAccessed'))
    assert cfg['ExpectedControlMethods'] == control_inventory(package / 'source')
    spec = read(package / 'continuous-reference-input.json')
    assert spec == protocol(read(package / 'native/baseline/bundle-input.json'), read(package / 'native/sensory/trajectory-input.json'))
    assert spec['connected'] == read(package / 'native/connected-input.json') == read(package / 'native/study/input.json')
    review = read(package / 'reviewed-connected.json')
    assert review['operatorReportedCompletion'] and review['connectedAccountingPassed'] and review['savedAnalysisExactlyReproduced']
    assert review['sourceRevision'] == NATIVE_REVISION and Path(review['capture']) == NATIVE
    assert review['passedTests'] == 284 and review['sourceSteps'] == 2293760 and review['nativeRuns'] == 6
    assert h.digest(package / 'native/analysis.json') == review['analysisSha256']
    assert read(package / 'native/analysis.json') == read(package / 'reviewed-connected-analysis.json')
    waveform = read(package / 'reviewed-waveform.json')
    assert waveform['operatorReportedCompletion'] and waveform['numericalCriteriaPassed'] and waveform['passedAnalyticControls'] == 21
    assert waveform['sourceRevision'] == WAVEFORM_REVISION and waveform['verifiedQueries'] == 1060
    assert h.digest(package / 'baseline/waveform-analysis.json') == waveform['analysisSha256']
    assert read(package / 'baseline/waveform-analysis.json') == read(package / 'reviewed-waveform-analysis.json')
    for recorded in (review, waveform):
        origin = next(row for row in provenance['origins'] if row['folder'] == recorded['capture'])
        assert origin['manifestSha256'] == recorded['manifestSha256']
    expected_sources = {(str(NATIVE), n) for n in NATIVE_PRESERVED} | {(waveform['capture'], n) for n in WAVEFORM_PRESERVED}
    assert {(r['capture'], r['path']) for r in provenance['preservedSources']} == expected_sources
    for row in provenance['preservedSources']:
        assert h.digest(package / 'source' / relative(row['path'])) == row['sha256'], row['path']
    for row in provenance['copiedFiles']:
        assert h.digest(package / relative(row['path'])) == row['sha256'], row['path']
    for row in cfg['PackageFiles']:
        assert h.digest(package / relative(row['Path'])) == row['Sha256'], row['Path']
    assert h.digest(cfg['PythonExecutable']) == cfg['PythonExecutableSha256']
    assert base.environment() == read(package / 'preparation-environment.json') == read(package / 'native/preparation-environment.json')
    assert read(package / 'source-decisions.json') == read(package / 'native/sensory/source-decisions.json')
    assert read(package / 'qualified-managed-runtime.json') == read(package / 'native/managed-runtime-before.json') == read(package / 'native/managed-runtime-after.json')
    print('Reviewed native capture, independent input, initial state, equations, original limits and numerical dependencies preserved; no model/test executed by preflight.', flush=True)


def storage(package):
    available = shutil.disk_usage(package).free
    output = package / ('storage-' + uuid.uuid4().hex[:8] + '.json')
    save(output, dict(requiredBytes=RESERVE_BYTES, availableBytes=available, passed=available >= RESERVE_BYTES))
    assert available >= RESERVE_BYTES, 'Preserve output; insufficient phase/backup reserve.'


def controls(package):
    assert not (package / 'analytic-controls.json').exists(), 'Preserve the existing controls; use a fresh package.'
    expected = read(package / 'configuration.json')['ExpectedControlMethods']
    suite = unittest.TestSuite()
    for name, (class_name, _) in CONTROL_CLASSES.items():
        module = importlib.import_module(name)
        suite.addTests(unittest.defaultTestLoader.loadTestsFromTestCase(getattr(module, class_name)))
    assert sorted(test.id() for test in suite) == expected

    class RecordedResult(unittest.TextTestResult):
        def __init__(self, *args, **kwargs):
            super().__init__(*args, **kwargs)
            self.started, self.passed = [], []

        def startTest(self, test):
            self.started.append(test.id())
            super().startTest(test)

        def addSuccess(self, test):
            self.passed.append(test.id())
            super().addSuccess(test)

    result = unittest.TextTestRunner(verbosity=2, resultclass=RecordedResult).run(suite)
    exact = sorted(result.started) == sorted(result.passed) == expected
    report = dict(expected=15, executed=result.testsRun, successful=result.wasSuccessful(), exactInventory=exact,
        started=result.started, passed=result.passed, failures=[dict(test=str(t), trace=s) for t, s in result.failures],
        errors=[dict(test=str(t), trace=s) for t, s in result.errors], skipped=result.skipped)
    save(package / 'analytic-controls.json', report)
    assert result.testsRun == 15 and result.wasSuccessful() and not result.skipped and exact, 'Analytic controls failed or incomplete.'


def require_controls(package):
    result = read(package / 'analytic-controls.json')
    expected = read(package / 'configuration.json')['ExpectedControlMethods']
    assert result['expected'] == result['executed'] == 15 and result['successful'] and result['exactInventory']
    assert sorted(result['started']) == sorted(result['passed']) == expected
    assert not result['errors'] and not result['failures'] and not result['skipped']


def execute(command, package):
    preflight(package)
    if command == 'postflight':
        verify_originals(read(package / 'provenance.json'))
        print('Both completed original captures and their protected copies remain unchanged.', flush=True)
    elif command == 'storage':
        storage(package)
    elif command == 'controls':
        controls(package)
    elif command == 'reference':
        require_controls(package)
        storage(package)
        from canal_afferent_continuous_reference import run
        run(package)
    elif command == 'compare':
        require_controls(package)
        storage(package)
        from canal_afferent_reference_analysis import analyse
        result = analyse(package)
        for case in result['cases']:
            print(case['condition'], 'reference', case['referenceCriteriaPassed'], 'finest', case['finestAllEndpointCriteriaPassed'],
                'refinement', case['commonTimeRefinement']['passed'], flush=True)


if __name__ == '__main__':
    if not __debug__:
        raise RuntimeError('Assertions must remain enabled.')
    parser = argparse.ArgumentParser()
    parser.add_argument('command', choices=['prepare', 'preflight', 'postflight', 'storage', 'controls', 'reference', 'compare'])
    parser.add_argument('path', type=Path)
    parser.add_argument('--operator-reported-complete', action='store_true')
    args = parser.parse_args()
    if args.command == 'prepare':
        prepare(args.path, args.operator_reported_complete)
    else:
        execute(args.command, args.path.resolve())
