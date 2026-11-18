"""Continue only the independent comparison of an operator-completed native capture.

The original 360,428-file capture remains read-only. Full C:/D: integrity and
source/test provenance are checked in the visible window before using it.
No neuron, muscle candidate, Unity player or old test suite is rerun here.
"""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path, PurePosixPath
import shutil
import subprocess
import sys
import uuid
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[1]
FAILED_REVISION = '827f04554fb61191ae2cf489b3aa54618f76c4c0'


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec); spec.loader.exec_module(result)
    return result


gate = module('continuation_native_gate', ROOT/'tools/rubral_native_gate.py')
h = gate.h
save = gate.save


def prepare(capture, reported):
    assert reported, 'Wait for the operator completion report.'
    assert not h.git('status', '--porcelain'), 'Commit clean source first.'
    revision = h.git('rev-parse', 'HEAD')
    assert revision == h.git('rev-parse', 'origin/main'), 'Push first.'
    # Selected completed metadata now; the expensive full capture audit belongs
    # to the independent window and is a prerequisite for any new integration.
    names = ['configuration.json', 'window-status.json', 'coupled-reference.log',
             'preparation-environment.json', 'reference-controls-summary.json',
             'reference-controls/controls.json', 'provenance.json']
    h.verify(capture, names)
    previous = h.read(capture/'configuration.json')
    status = h.read(capture/'window-status.json')
    assert previous['Revision'] == status['Revision'] == FAILED_REVISION
    assert status['Status'] == 'FAILED' and status['ExitCode'] == 1
    assert status['Error'].endswith('coupled-reference.log exited 1')
    assert status['NativeDriverSucceeded'] and status['PassedFocusedTests'] == 281
    assert status['ManagedRuntimePreserved'] and status['ReferenceEnvironmentPreserved']
    assert not (capture/'rubral-reference').exists()
    environment = module('continuation_dependencies', ROOT/'tools/golgi-reference-resume.py').environment()
    assert environment == h.read(capture/'preparation-environment.json'), 'Do not silently migrate the runtime again.'
    package = ROOT/'artifacts'/('rubral-comparison-'+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6])
    backup = Path('D:/DNNE-desktop-runs/UnityWorld')/package.name
    assert not package.exists() and not backup.exists(); package.mkdir()
    # Generate this independently from Git, never by copying the capture's zip.
    # The visible window verifies it without depending on its inherited PATH.
    git_reference = package/'capture-git-reference.zip'
    subprocess.run(['git', 'archive', '--format=zip', '--output='+str(git_reference), FAILED_REVISION], cwd=ROOT, check=True)
    with ZipFile(git_reference) as archive:
        assert archive.comment.decode().strip() == FAILED_REVISION
    subprocess.run(['git', 'archive', '--format=zip', '--output='+str(package/'source.zip'), revision], cwd=ROOT, check=True)
    with ZipFile(package/'source.zip') as archive:
        for entry in archive.infolist():
            name = PurePosixPath(entry.filename)
            assert not name.is_absolute() and '..' not in name.parts and ':' not in entry.filename and '\\' not in entry.filename
            assert ((entry.external_attr >> 16) & 0o170000) != 0o120000
            target = h.io(package/'source'/name)
            if entry.is_dir(): target.mkdir(parents=True, exist_ok=True)
            else:
                target.parent.mkdir(parents=True, exist_ok=True)
                with target.open('xb') as stream: stream.write(archive.read(entry))
    copied = []
    for directory in ('reference', 'reference-controls'):
        root = h.io(capture/directory)
        copied.extend(directory+'/'+p.relative_to(root).as_posix() for p in root.rglob('*') if p.is_file())
    copied += ['reference-controls-summary.json', 'preparation-environment.json']
    h.verify(capture, copied)
    for name in copied:
        target = h.io(package/name); target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(h.io(capture/name), target)
        assert h.digest(capture/name) == h.digest(target)
    shutil.copyfile(package/'source/tools/backup-unity-run.ps1', package/'backup-run.ps1')
    # All reused numerical code must match the completed native capture.
    same = ['tools/'+name for name in ('analyse-native-hinge-comparison.py', 'analyse-native-motor-bridge.py',
        'analyse-observed-native-body.py', 'native_hinge_reference.py', 'native_hinge_protocol.py',
        'native_hinge_reference_controls.py', 'dynamic_muscle_reference.py', 'causal_motor_reference.py',
        'golgi-reference-resume.py', 'rubral_reference_environment.py')]
    for name in same:
        assert h.io(package/'source'/name).read_bytes() == h.io(capture/'source'/name).read_bytes(), name
    save(package/'continuation-input.json', dict(operatorReportedCompletion=True,
        capture=str(capture), captureManifestSha256=h.digest(capture/'backup-manifest.json'),
        gitReferenceArchive=dict(path=git_reference.name, sha256=h.digest(git_reference), revision=FAILED_REVISION,
                                 generatedFromGitDuringPreparation=True),
        captureRevision=FAILED_REVISION, copiedFiles=[dict(path=n, sha256=h.digest(package/n)) for n in copied],
        unchangedNumericalSources=[dict(path=n, sha256=h.digest(package/'source'/n)) for n in same],
        originalBoundsChanged=False, modelsExecutedDuringPreparation=False, testsExecutedDuringPreparation=False))
    files = [dict(Path=p.relative_to(h.io(package)).as_posix(), Sha256=h.digest(p))
             for p in sorted(h.io(package).rglob('*')) if p.is_file()]
    config = {k: previous[k] for k in ('IsolatedRubralNative', 'NoLiveMonitoring', 'ExpectedClassCounts',
        'ExpectedTestCases', 'ExpectedTestMethods', 'ExpectedNativeCases', 'ExpectedNativePhysicsSteps',
        'ExpectedIndependentReferences', 'EnvironmentTransitionDeclared', 'ExpectedEnvironmentControls',
        'ExpectedReferenceControlCounts')}
    config.update(Revision=revision, PackageFiles=files, BackupDestination=str(backup),
        NativeCapture=str(capture), ReferenceOnlyContinuation=True, OperatorReportedCompletion=True, SelfContainedSourceArchive=True,
        PythonExecutable=sys.executable, PythonExecutableSha256=h.digest(sys.executable),
        NativePhysicsRerun=False, NeuronalCandidateRerun=False, OriginalBoundsChanged=False,
        PhysiologyQualified=False, AvatarRecoveryAssessed=False)
    save(package/'configuration.json', config)
    pointer = dict(Package=str(package), Backup=str(backup), Revision=revision, PreparedOnly=True,
        Launcher=str(package/'source/tools/start-rubral-comparison-window.ps1'), NoLiveMonitoring=True)
    save(package.parent/(package.name+'-prepared.json'), pointer)
    print(json.dumps(pointer, indent=2), flush=True)


def require_review(package):
    config = h.read(package/'configuration.json'); inputs = h.read(package/'continuation-input.json')
    capture = Path(config['NativeCapture']); report = h.read(package/'input-review.json')
    assert config['ReferenceOnlyContinuation'] and config['OperatorReportedCompletion'] and inputs['operatorReportedCompletion']
    assert str(capture) == inputs['capture'] == report['capture']
    assert report['fullCaptureVerified'] and report['passedTests'] == 281
    assert report['sourceRevision'] == inputs['captureRevision'] == FAILED_REVISION
    assert h.digest(capture/'backup-manifest.json') == inputs['captureManifestSha256'] == report['manifestSha256']
    backup = Path(report['backup'])
    assert h.digest(backup/'backup-manifest.json') == report['manifestSha256']
    assert report['originalBoundsChanged'] is False
    return report


def verify_archive(package):
    config = h.read(package/'configuration.json'); inputs = h.read(package/'continuation-input.json')
    assert config['SelfContainedSourceArchive'] and config['ReferenceOnlyContinuation']
    assert inputs['operatorReportedCompletion'] and config['OperatorReportedCompletion']
    capture = Path(config['NativeCapture']); assert str(capture) == inputs['capture']
    assert h.digest(capture/'backup-manifest.json') == inputs['captureManifestSha256']
    reference = inputs['gitReferenceArchive']
    assert reference['path'] == 'capture-git-reference.zip' and reference['revision'] == FAILED_REVISION
    assert reference['generatedFromGitDuringPreparation']
    assert h.digest(package/reference['path']) == reference['sha256']
    entry = next(e for e in config['PackageFiles'] if e['Path'] == reference['path'])
    assert entry['Sha256'] == reference['sha256']
    common = module('continuation_archive_io', ROOT/'tools/review-olive-and-motor-20261109.py')
    archive = module('continuation_archive', ROOT/'tools/review-spindle-root-and-causal-motor-20261113.py')
    return archive.source_archive(common, capture, FAILED_REVISION, reference_archive=package/reference['path'])


def verify_input(package, final=False):
    config = h.read(package/'configuration.json'); inputs = h.read(package/'continuation-input.json')
    assert config['ReferenceOnlyContinuation'] and config['OperatorReportedCompletion'] and inputs['operatorReportedCompletion']
    capture = Path(config['NativeCapture'])
    assert str(capture) == inputs['capture'] and h.digest(capture/'backup-manifest.json') == inputs['captureManifestSha256']
    # Resolve every source dependency before the much larger native-file audit.
    git_files = verify_archive(package)
    print(f'{git_files} original Git/source files reconciled without invoking Git.', flush=True)
    common = module('continuation_integrity', ROOT/'tools/review-olive-and-motor-20261109.py')
    print('Verifying every saved native file on C: and D:; no simulation is running in this stage.', flush=True)
    result = common.verify(capture, ['test-binaries.json', 'probe-binaries.json'], FAILED_REVISION, 'FAILED', 1, progress_every=10000)
    result['verifiedGitFiles'] = git_files
    for entry in h.read(capture/'player-files.json'):
        path = Path(entry['Path']); path.relative_to(capture/'player')
        assert h.digest(path) == entry['Sha256']
    tests = gate.gate.check_tests(capture, gate.COUNTS)
    assert tests['passedCases'] == 281
    assert h.digest(capture/'managed-runtime-before.json') == h.digest(capture/'managed-runtime-after.json') == h.digest(capture/'reference/qualified-managed-runtime.json')
    for row in inputs['copiedFiles']:
        assert h.digest(capture/row['path']) == h.digest(package/row['path']) == row['sha256']
    for row in inputs['unchangedNumericalSources']:
        assert h.digest(capture/'source'/row['path']) == h.digest(package/'source'/row['path']) == row['sha256']
    log = h.io(capture/'coupled-reference.log').read_text(encoding='utf-8-sig')
    assert 'base,b=old.load(package)' in log and 'reference/qualified-environment.json' in log and log.rstrip().endswith('AssertionError')
    assert not (capture/'rubral-reference').exists() and not (capture/'native-source-analysis.json').exists()
    gate.preflight(package); gate.check_reference_controls(package)
    result.update(fullCaptureVerified=True, operatorReportedCompletion=True, passedTests=tests['passedCases'],
        originalFailure='Historical loader rejected the explicitly declared interpreter transition before any reference solve.',
        originalBoundsChanged=False, modelsExecutedDuringReview=False, testsExecutedDuringReview=False,
        physiologyQualified=False, avatarRecoveryAssessed=False)
    if final:
        assert result == require_review(package), 'Completed original capture changed during continuation.'
    save(package/('input-postflight.json' if final else 'input-review.json'), result)
    print('Original native capture, tests, sources and both backup copies verified.', flush=True)


def loader_controls(package):
    # This is the actual production loader with the real prepared package and
    # complete current runtime, not a mocked success or a test of an alternative.
    require_review(package)
    base, bridge = gate.load_reference(package)
    assert callable(base.candidate_data) and callable(bridge.audit_wire)
    frozen = module('historical_observed_loader', Path(h.read(package/'configuration.json')['NativeCapture'])/'source/tools/analyse-observed-native-body.py')
    try:
        frozen.load(package)
    except AssertionError as error:
        frame = error.__traceback__
        while frame.tb_next is not None: frame = frame.tb_next
        assert Path(frame.tb_frame.f_code.co_filename).name == 'analyse-observed-native-body.py'
        assert frame.tb_frame.f_code.co_name == 'load' and frame.tb_lineno == 37
        historical_rejected = True
    else:
        raise AssertionError('Expected preserved old loader to reject this declared transition.')
    controls = gate.transition.controls()
    save(package/'loader-controls.json', dict(completed=True, repairedProductionLoaderAccepted=True,
        historicalLoaderReproducedFailure=historical_rejected, transitionControls=controls,
        modelExecuted=False, historicalRuntimeEquivalenceEstablished=False))
    print('Original loader failure reproduced; repaired production loader and ten transition controls passed.', flush=True)


if __name__ == '__main__':
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=['prepare', 'verify-input', 'postflight', 'loader-controls'])
    parser.add_argument('path', type=Path)
    parser.add_argument('--operator-reported-complete', action='store_true')
    args = parser.parse_args(); path = args.path.resolve()
    if args.command == 'prepare': prepare(path, args.operator_reported_complete)
    elif args.command == 'loader-controls': loader_controls(path)
    else: verify_input(path, final=args.command == 'postflight')
