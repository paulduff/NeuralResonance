"""Freeze a forcing-only replay after the operator-reported midpoint controls pass.

All models, tests and array comparisons execute in the separate visible window.
Preparation only verifies completed evidence, copies immutable inputs and hashes.
"""
import argparse
import ast
from datetime import datetime, timezone
import difflib
import hashlib
import importlib
import json
from pathlib import Path
import shutil
import subprocess
import sys
import unittest
import uuid
from zipfile import ZipFile
import canal_midpoint_gate as component
import canal_afferent_reference_gate as original
from afferent_precision_gate import h, read, save
from cleft_trajectory_reference import base
from canal_midpoint_protocol import protocol

ROOT = Path(__file__).resolve().parents[1]
CONTROL = Path('D:/DNNE-build-work/canal-midpoint-20261119-f8a807')
CONTROL_REVISION = '7b50528fb3a2e29bccc52a5e8a01984dd791b6d3'
REFERENCE = Path('D:/DNNE-build-work/canal-reference-20261119-2a402c')
REFERENCE_REVISION = 'fab52528b3d4250be2d647929d70d0c6273a5700'
NATIVE = original.NATIVE
RESERVE_BYTES = 32 * 1024**3
CHANGED = {
    'tools/NativeMotorBridgeProbe/CanalAfferentStudy.cs': ('4b04639d29c8fb5ba8486dcfc3b36f607568fa2a21c2867072b582cc57fb6639', '25eeeaea1762ad145c88a74dc8cb2c875119d6d4622942794c4520c307ba979d'),
    'tools/NativeMotorBridgeProbe/Program.cs': ('a50ceb5b45850bb3d0a3fe92b4f8d191bd1a8a2f13db4e1c23d0f8ad6c6b742f', '022a95f42242993e01c1f01267fb8813075179207ed91127c01eed3a6f7eb14d'),
    'tools/canal_afferent_analysis.py': ('03f9453fc8de2673b3e3354ba65e45920490e558efd2daab75623e077f71461d', 'f6dc78f6cca1ea31fecd8a42b52347b1706f6b28cfaa52cdd869edff5f5990a4'),
    'tools/canal_afferent_reference_analysis.py': ('b5f256492254caf5e897ba9e46a97a6dc0e491141d89b18d9be3677fb551c897', '8829a2ab337ca7a3f299b35cbc3fb5df8f13cdfdd3fa53dfa9d82a978958f930'),
}
PRESERVED = sorted((set(component.PRESERVED) | set(component.CHANGED) | {
    'tools/canal_midpoint_gate.py', 'tests/NeuralResonanceEngine.DNNE.Tests/CanalAfferentMidpointTests.cs',
}) - set(CHANGED))
CONTROL_CLASSES = dict(original.CONTROL_CLASSES,
    check_canal_midpoint_comparison=('MidpointComparisonControls', 8))


def changes(before_root, after_root):
    rows = []
    for name, (before_hash, after_hash) in CHANGED.items():
        before = h.io(before_root / name).read_text(encoding='utf-8-sig')
        after = h.io(after_root / name).read_text(encoding='utf-8-sig')
        assert hashlib.sha256(before.encode()).hexdigest() == before_hash, name
        assert hashlib.sha256(after.encode()).hexdigest() == after_hash, name
        rows.append(dict(path=name, originalSha256=h.digest(before_root / name), candidateSha256=h.digest(after_root / name),
            normalizedOriginalSha256=before_hash, normalizedCandidateSha256=after_hash,
            diff=''.join(difflib.unified_diff(before.splitlines(keepends=True), after.splitlines(keepends=True),
                fromfile='original/' + name, tofile='candidate/' + name))))
    # The shared comparison tail differs only in its explicitly supplied roots.
    name = 'tools/canal_afferent_reference_analysis.py'
    old = h.io(before_root / name).read_text(encoding='utf-8-sig').split("    output = package / 'continuous-comparison'", 1)[1]
    new = h.io(after_root / name).read_text(encoding='utf-8-sig').split("    output = package / 'continuous-comparison'", 1)[1]
    old = old.replace('ReferenceReader(package, p, source,', 'ReferenceReader(reference_package, p, source,')
    old = old.replace('NativeReader(stack, package, p, condition, count, equation)',
                      'NativeReader(stack, package, p, condition, count, equation, native_directory)')
    assert old == new, 'Original comparison arithmetic/limits changed.'
    return rows


def controls_inventory(root):
    result = []
    for name, (class_name, count) in CONTROL_CLASSES.items():
        tree = ast.parse((root / 'tools' / (name + '.py')).read_text(encoding='utf-8-sig'))
        classes = [n for n in tree.body if isinstance(n, ast.ClassDef) and n.name == class_name]
        assert len(classes) == 1
        methods = [n.name for n in classes[0].body if isinstance(n, ast.FunctionDef) and n.name.startswith('test_')]
        assert len(methods) == len(set(methods)) == count
        result += [name + '.' + class_name + '.' + m for m in methods]
    assert len(result) == 24
    return sorted(result)


def prepare(review_folder, reported):
    if not reported:
        raise ValueError('Wait for the midpoint final footer and its verified passing review; no pending capture opened.')
    assert not h.git('status', '--porcelain'), 'Commit first.'
    revision = h.git('rev-parse', 'HEAD')
    assert revision == h.git('rev-parse', 'origin/main'), 'Push first.'
    assert review_folder == (ROOT / 'artifacts' / (CONTROL.name + '-review')).resolve()
    h.verify(review_folder, ['review.json', 'analysis.json'])
    review = read(review_folder / 'review.json')
    assert review['operatorReportedCompletion'] and review['midpointComponentChecksPassed'] and review['savedAnalysisExactlyReproduced']
    assert review['sourceRevision'] == CONTROL_REVISION and Path(review['capture']) == CONTROL and review['passedTests'] == 172
    assert h.digest(CONTROL / 'backup-manifest.json') == review['manifestSha256']
    assert h.digest(CONTROL / 'analysis.json') == review['analysisSha256']
    failure_folder = ROOT / 'artifacts' / (REFERENCE.name + '-review')
    h.verify(failure_folder, ['review.json', 'analysis.json', 'failure.json'])
    failure = read(failure_folder / 'review.json')
    assert failure['operatorReportedCompletion'] and failure['originalFailurePreserved'] and failure['savedReportCriteriaReconciled']
    assert failure['sourceRevision'] == REFERENCE_REVISION and failure['comparedNativeSteps'] == 2293760
    assert not failure['sampledContinuousInputAccuracyPassed']
    assert h.digest(REFERENCE / 'backup-manifest.json') == failure['manifestSha256']
    assert h.digest(REFERENCE / 'continuous-comparison/analysis.json') == failure['analysisSha256']
    native_review_folder = ROOT / 'artifacts' / (NATIVE.name + '-review')
    h.verify(native_review_folder, ['review.json', 'analysis.json'])
    native_review = read(native_review_folder / 'review.json')
    assert native_review['operatorReportedCompletion'] and native_review['connectedAccountingPassed'] and native_review['savedAnalysisExactlyReproduced']
    assert native_review['sourceRevision'] == original.NATIVE_REVISION and Path(native_review['capture']) == NATIVE
    assert h.digest(NATIVE / 'backup-manifest.json') == native_review['manifestSha256']
    assert h.digest(NATIVE / 'analysis.json') == native_review['analysisSha256']
    reference_names = [r['Path'].replace('\\', '/') for r in read(REFERENCE / 'backup-manifest.json')['Files']
                       if r['Path'].replace('\\', '/').startswith('continuous-reference/')]
    assert len(reference_names) == 373
    reference_names += ['continuous-reference-input.json', 'source-decisions.json', 'preparation-environment.json',
                        'continuous-comparison/analysis.json', 'window-status.json']
    native_names = ['baseline/bundle-input.json', 'baseline/study/geometry.json', 'baseline/study/r4/trajectory.csv.gz',
                    'sensory/trajectory-input.json', 'sensory/study/quiet-ticks64/initial.json']
    native_names += [f'baseline/study/r4/endpoint-{head:03}.json' for head in range(2, 12)]
    # Protect every original Python source, including dynamically loaded modules.
    h.verify(REFERENCE, ['source.zip'])
    with ZipFile(REFERENCE / 'source.zip') as archive:
        python_names = sorted(n for n in archive.namelist() if n.startswith('tools/') and n.endswith('.py') and n not in CHANGED)
    assert len(python_names) == 396
    for name in python_names:
        assert (ROOT / name).read_text(encoding='utf-8-sig') == h.io(REFERENCE / 'source' / name).read_text(encoding='utf-8-sig'), name
    for name in PRESERVED:
        assert (ROOT / name).read_text(encoding='utf-8-sig') == h.io(CONTROL / 'source' / name).read_text(encoding='utf-8-sig'), name
    changes(CONTROL / 'source', ROOT)
    origins = [original.verified_origin(REFERENCE, reference_names + ['source.zip'] + ['source/' + n for n in python_names]),
               original.verified_origin(NATIVE, native_names),
               original.verified_origin(CONTROL, ['analysis.json', 'managed-runtime-before.json'] + ['source/' + n for n in PRESERVED] + ['source/' + n for n in CHANGED]),
               original.verified_origin(review_folder, ['review.json', 'analysis.json']),
               original.verified_origin(failure_folder, ['review.json', 'analysis.json', 'failure.json'])]
    environment = base.environment()
    assert environment == read(REFERENCE / 'preparation-environment.json') == read(CONTROL / 'preparation-environment.json')
    original_spec = read(REFERENCE / 'continuous-reference-input.json')
    assert original_spec == original.protocol(read(NATIVE / 'baseline/bundle-input.json'), read(NATIVE / 'sensory/trajectory-input.json'))
    input_bytes = sum(h.io(REFERENCE / n).stat().st_size for n in reference_names) + sum(h.io(NATIVE / n).stat().st_size for n in native_names)
    assert shutil.disk_usage('D:/').free >= RESERVE_BYTES + 2 * input_bytes, 'Reserve space for new capture and both copies.'
    package = Path('D:/DNNE-build-work') / ('canal-midpoint-trajectory-' + datetime.now(timezone.utc).strftime('%Y%m%d') + '-' + uuid.uuid4().hex[:6])
    backup = Path('D:/DNNE-desktop-runs/UnityWorld') / package.name
    assert not package.exists() and not backup.exists()
    package.mkdir()
    subprocess.run(['git', 'archive', '--format=zip', '--output=' + str(package / 'source.zip'), revision], cwd=ROOT, check=True)
    with ZipFile(package / 'source.zip') as archive:
        assert archive.comment.decode().strip() == revision
        for entry in archive.infolist():
            name = original.relative(entry.filename)
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
        target = h.io(package / original.relative(name))
        target.parent.mkdir(parents=True, exist_ok=True)
        with h.io(source).open('rb') as src, target.open('xb') as dst:
            shutil.copyfileobj(src, dst, 4 * 1024**2)
        assert h.digest(source) == h.digest(target)
        copied.append(dict(path=name, source=str(source), sha256=h.digest(source)))

    for name in reference_names:
        target = {'continuous-comparison/analysis.json': 'original-failed-analysis.json', 'window-status.json': 'original-failed-status.json'}.get(name, name)
        copy(REFERENCE / name, target)
    for name in native_names:
        copy(NATIVE / name, name)
    for folder, name, target in ((review_folder, 'review.json', 'reviewed-midpoint.json'),
            (review_folder, 'analysis.json', 'reviewed-midpoint-analysis.json'),
            (failure_folder, 'review.json', 'reviewed-failure.json'),
            (native_review_folder, 'review.json', 'reviewed-native.json'),
            (CONTROL, 'managed-runtime-before.json', 'qualified-managed-runtime.json')):
        copy(folder / name, target)
    for name in CHANGED:
        copy(CONTROL / 'source' / name, 'declared-original/' + name)
    copy(package / 'source/tools/backup-unity-run.ps1', 'backup-run.ps1')
    preserved = [dict(path=n, sha256=h.digest(CONTROL / 'source' / n)) for n in PRESERVED]
    reference_python = [dict(path=n, sha256=h.digest(REFERENCE / 'source' / n)) for n in python_names]
    for row in preserved + reference_python:
        assert h.digest(package / 'source' / row['path']) == row['sha256'], row['path']
    save(package / 'connected-input.json', protocol(original_spec))
    save(package / 'provenance.json', dict(origins=origins, copiedFiles=copied, preservedSources=preserved,
        referencePythonSources=reference_python, declaredChanges=changes(package / 'declared-original', package / 'source'),
        operatorReportedCompletion=True, originalBoundsChanged=False, neuronalEquationsChanged=False,
        referenceOdesRerun=False, pendingCaptureAccessed=False, modelsExecutedDuringPreparation=False, testsExecutedDuringPreparation=False))
    files = [dict(Path=f.relative_to(h.io(package)).as_posix(), Sha256=h.digest(f)) for f in sorted(h.io(package).rglob('*')) if f.is_file()]
    dotnet = shutil.which('dotnet')
    assert dotnet
    save(package / 'configuration.json', dict(Revision=revision, PackageFiles=files, BackupDestination=str(backup),
        PythonExecutable=sys.executable, PythonExecutableSha256=h.digest(sys.executable),
        DotnetExecutable=dotnet, DotnetExecutableSha256=h.digest(dotnet), IsolatedCanalMidpointTrajectory=True,
        NoLiveMonitoring=True, ExpectedTestCases=172, ExpectedClassCounts=component.COUNTS,
        ExpectedTestMethods=component.inventory_methods(package / 'source'), TestFilter='|'.join('FullyQualifiedName~' + c for c in component.COUNTS),
        ExpectedControls=24, ExpectedControlMethods=controls_inventory(package / 'source'), ExpectedSourceSteps=2293760,
        IndependentReferencesReused=6, OriginalBoundsChanged=False, ReferenceOdesRerun=False,
        LiveSelectionChanged=False, PhysiologyQualified=False, AvatarRecoveryAssessed=False))
    preflight(package)
    pointer = dict(Package=str(package), Backup=str(backup), Revision=revision, NoLiveMonitoring=True,
                   Launcher=str(package / 'source/tools/start-canal-midpoint-trajectory-window.ps1'))
    save(ROOT / 'artifacts' / (package.name + '-prepared.json'), pointer)
    print(json.dumps(pointer, indent=2), flush=True)


def preflight(package, originals=True):
    cfg, prov = read(package / 'configuration.json'), read(package / 'provenance.json')
    assert cfg['IsolatedCanalMidpointTrajectory'] and cfg['NoLiveMonitoring']
    assert cfg['ExpectedSourceSteps'] == 2293760 and cfg['IndependentReferencesReused'] == 6
    assert cfg['ExpectedTestCases'] == 172 and cfg['ExpectedClassCounts'] == component.COUNTS
    assert cfg['ExpectedTestMethods'] == component.inventory_methods(package / 'source')
    assert cfg['ExpectedControls'] == 24 and cfg['ExpectedControlMethods'] == controls_inventory(package / 'source')
    assert not any(cfg[k] for k in ('OriginalBoundsChanged', 'ReferenceOdesRerun', 'LiveSelectionChanged', 'PhysiologyQualified', 'AvatarRecoveryAssessed'))
    assert prov['operatorReportedCompletion']
    assert not any(prov[k] for k in ('originalBoundsChanged', 'neuronalEquationsChanged', 'referenceOdesRerun',
        'pendingCaptureAccessed', 'modelsExecutedDuringPreparation', 'testsExecutedDuringPreparation'))
    assert changes(package / 'declared-original', package / 'source') == prov['declaredChanges']
    assert {r['path'] for r in prov['preservedSources']} == set(PRESERVED)
    for row in prov['preservedSources'] + prov['referencePythonSources']:
        assert h.digest(package / 'source' / row['path']) == row['sha256'], row['path']
    for row in cfg['PackageFiles']:
        assert h.digest(package / row['Path']) == row['Sha256'], row['Path']
    for row in prov['copiedFiles']:
        assert h.digest(package / row['path']) == row['sha256'], row['path']
    reviewed = read(package / 'reviewed-midpoint.json')
    assert reviewed['sourceRevision'] == CONTROL_REVISION and reviewed['operatorReportedCompletion'] and reviewed['midpointComponentChecksPassed']
    assert reviewed['passedTests'] == 172 and h.digest(package / 'reviewed-midpoint-analysis.json') == reviewed['analysisSha256']
    failure = read(package / 'reviewed-failure.json')
    assert failure['sourceRevision'] == REFERENCE_REVISION and failure['originalFailurePreserved'] and failure['savedReportCriteriaReconciled']
    assert h.digest(package / 'original-failed-analysis.json') == failure['analysisSha256']
    assert read(package / 'original-failed-status.json')['Status'] == 'FAILED'
    spec = read(package / 'continuous-reference-input.json')
    assert spec == original.protocol(read(package / 'baseline/bundle-input.json'), read(package / 'sensory/trajectory-input.json'))
    assert read(package / 'connected-input.json') == protocol(spec)
    assert base.environment() == read(package / 'preparation-environment.json')
    for directory in read(package / 'qualified-managed-runtime.json')['directories']:
        root = h.io(directory['path'])
        assert {str(f) for f in root.rglob('*') if f.is_file()} == {str(h.io(r['path'])) for r in directory['files']}
        for row in directory['files']:
            assert h.digest(row['path']) == row['sha256']
    if originals:
        original.verify_originals(prov)
    print('Original failed comparison, six reference solutions, neuronal kernels and original limits preserved; no model/test executed by preflight.', flush=True)


def runtime(package):
    preflight(package, originals=False)
    assert read(package / 'managed-runtime-before.json') == read(package / 'qualified-managed-runtime.json')


def check_tests(package):
    result = component.test_evidence(package)
    partition = component.partition_audit(package)
    assert all(partition['checks'][k] for k in ('ReticularEqual', 'SpinalEqual', 'CanonicalDeliveriesEqual', 'CanonicalWaitingEqual'))
    save(package / 'test-analysis.json', dict(tests=result, partitionComparison=partition))


def controls(package):
    cfg = read(package / 'configuration.json')
    assert cfg['ExpectedControlMethods'] == controls_inventory(package / 'source')
    suite = unittest.TestSuite()
    methods = []
    for name, (class_name, _) in CONTROL_CLASSES.items():
        tests = unittest.defaultTestLoader.loadTestsFromTestCase(getattr(importlib.import_module(name), class_name))
        methods += [test.id() for test in tests]
        suite.addTests(tests)
    assert sorted(methods) == cfg['ExpectedControlMethods'] and suite.countTestCases() == 24
    result = unittest.TextTestRunner(verbosity=2).run(suite)
    evidence = dict(completed=True, passed=result.wasSuccessful() and result.testsRun == 24 and not result.skipped,
        cases=result.testsRun, methods=sorted(methods), failures=[(t.id(), text) for t, text in result.failures],
        errors=[(t.id(), text) for t, text in result.errors], skipped=result.skipped)
    save(package / 'analytic-controls.json', evidence)
    if not evidence['passed']:
        raise RuntimeError('Comparison controls failed; no native replay may follow.')


if __name__ == '__main__':
    if not __debug__:
        raise RuntimeError('Assertions must remain enabled.')
    parser = argparse.ArgumentParser()
    parser.add_argument('command', choices=['prepare', 'preflight', 'postflight', 'runtime', 'check-tests', 'controls', 'account', 'compare'])
    parser.add_argument('path', type=Path)
    parser.add_argument('--operator-reported-complete', action='store_true')
    args = parser.parse_args()
    if args.command == 'prepare':
        if not args.operator_reported_complete:
            parser.error('Wait for the final footer and a passing completed review; no pending capture opened.')
        prepare(args.path.resolve(), True)
    elif args.command in ('account', 'compare'):
        from canal_midpoint_comparison import accounting, compare
        (accounting if args.command == 'account' else compare)(args.path.resolve())
    else:
        dict(preflight=preflight, postflight=preflight, runtime=runtime, **{'check-tests': check_tests}, controls=controls)[args.command](args.path.resolve())
