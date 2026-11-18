"""Qualify complete saved motor accounting through lossless archive mounts."""
import argparse
import ast
from contextlib import ExitStack
from datetime import datetime, timezone
import hashlib
import importlib.util
import json
from pathlib import Path, PurePosixPath
import shutil
import subprocess
import sys
import uuid
from zipfile import ZipFile
import numpy as np
from afferent_precision_gate import h, read, save
from cleft_trajectory_reference import base
from native_evidence_archive import EvidenceArchive
from native_evidence_view import EvidenceView

ROOT = Path(__file__).resolve().parents[1]
KEYS = ('repeat', 'body-finer')
READERS = ['analyse-rubral-native-body.py', 'analyse-native-hinge-comparison.py', 'analyse-native-motor-bridge.py',
           'native_hinge_protocol.py', 'native_hinge_reference.py', 'native_hinge_reference_controls.py',
           'causal_motor_reference.py', 'dynamic_muscle_reference.py']


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    value = importlib.util.module_from_spec(spec); spec.loader.exec_module(value)
    return value


def reader_change(original, candidate):
    old = ast.parse(original.read_text(encoding='utf-8-sig'))
    new = ast.parse(candidate.read_text(encoding='utf-8-sig'))
    old_records = [n for n in old.body if isinstance(n, ast.FunctionDef) and n.name == 'records']
    new_records = [n for n in new.body if isinstance(n, ast.FunctionDef) and n.name == 'records']
    assert len(old_records) == len(new_records) == 1
    expected_old = ast.parse("def records(path):\n    with gzip.open(path,'rt',encoding='utf-8') as f:\n        for line in f:yield json.loads(line)\n").body[0]
    expected_new = ast.parse("def records(path):\n    with path.open('rb') as raw, gzip.open(raw,'rt',encoding='utf-8') as f:\n        for line in f:yield json.loads(line)\n").body[0]
    assert ast.dump(old_records[0]) == ast.dump(expected_old)
    assert ast.dump(new_records[0]) == ast.dump(expected_new)
    old.body.remove(old_records[0]); new.body.remove(new_records[0])
    assert ast.dump(old) == ast.dump(new), 'Only the declared stream-opening adapter may differ.'


def prepare(review_folder):
    assert not h.git('status', '--porcelain'), 'Commit first.'
    revision = h.git('rev-parse', 'HEAD'); assert revision == h.git('rev-parse', 'origin/main'), 'Push first.'
    assert shutil.disk_usage('D:/').free >= 8 * 1024**3
    h.verify(review_folder, ['review.json', 'analysis.json'])
    review = read(review_folder / 'review.json')
    assert review['operatorReportedCompletion'] and review['recordingCriteriaPassed'] and review['controlsPassed'] == 24
    completed = Path(review['capture']); cfg = read(completed / 'configuration.json')
    assert h.digest(completed / 'backup-manifest.json') == h.digest(Path(review['backup']) / 'backup-manifest.json') == review['manifestSha256']
    original = Path(cfg['OriginalCapture']); motor = read(completed / 'reviewed-motor.json'); receipt = motor['originalFullAuditReceipt']
    original_backup = Path(receipt['backup'])
    assert original == Path(receipt['capture']) and receipt['fullCaptureVerified'] and receipt['passedTests'] == 287
    assert h.digest(original / 'backup-manifest.json') == h.digest(original_backup / 'backup-manifest.json') == receipt['manifestSha256']
    manifest = read(original_backup / 'backup-manifest.json')
    indexed = {row['Path'].replace('\\', '/'): row for row in manifest['Files']}
    assert base.environment() == read(completed / 'preparation-environment.json')
    reader_change(original_backup / 'source/tools/analyse-rubral-native-body.py', ROOT / 'tools/analyse-rubral-native-body.py')
    for name in READERS[1:]:
        assert (ROOT / 'tools' / name).read_text(encoding='utf-8-sig') == (original_backup / 'source/tools' / name).read_text(encoding='utf-8-sig'), name
    package = Path('D:/DNNE-build-work') / ('native-accounting-' + datetime.now(timezone.utc).strftime('%Y%m%d') + '-' + uuid.uuid4().hex[:6])
    backup = Path('D:/DNNE-desktop-runs/UnityWorld') / package.name
    assert not package.exists() and not backup.exists(); package.mkdir()
    subprocess.run(['git', 'archive', '--format=zip', '--output=' + str(package / 'source.zip'), revision], cwd=ROOT, check=True)
    with ZipFile(package / 'source.zip') as archive:
        for entry in archive.infolist():
            name = PurePosixPath(entry.filename)
            assert not name.is_absolute() and '..' not in name.parts and ':' not in entry.filename and '\\' not in entry.filename
            assert ((entry.external_attr >> 16) & 0o170000) != 0o120000
            target = h.io(package / 'source' / name)
            if entry.is_dir(): target.mkdir(parents=True, exist_ok=True)
            else:
                target.parent.mkdir(parents=True, exist_ok=True)
                with target.open('xb') as stream: stream.write(archive.read(entry))

    def copy(source, relative, expected=None):
        target = package / relative; target.parent.mkdir(parents=True, exist_ok=True)
        if expected is not None: assert h.digest(source) == expected
        shutil.copyfile(source, target); assert h.digest(source) == h.digest(target)

    for name in ('reviewed-motor.json', 'reviewed-analysis.json', 'preparation-environment.json'):
        h.verify(completed, [name]); copy(completed / name, name)
    h.verify(completed, ['source/tools/native_evidence_archive.py'])
    copy(completed / 'source/tools/native_evidence_archive.py', 'reviewed-archive-implementation.py')
    copy(review_folder / 'review.json', 'reviewed-recording.json')
    copy(review_folder / 'analysis.json', 'reviewed-recording-analysis.json')
    copy(package / 'source/tools/backup-unity-run.ps1', 'backup-run.ps1')
    for name in READERS:
        copy(original_backup / 'source/tools' / name, 'reviewed-readers/' + name, indexed['source/tools/' + name]['Sha256'])
    reference_rows = []
    for name, row in indexed.items():
        if name.startswith('reference/'):
            copy(original_backup / name, 'audit-input/' + name, row['Sha256'])
            reference_rows.append(dict(path='audit-input/' + name, sha256=row['Sha256']))
    prior_protocol = read(completed / 'protocol.json'); cases = []
    for key in KEYS:
        record = next(row for row in review['cases'] if row['key'] == key)
        h.verify(completed, ['captures/' + key + '.zip', key + '-expected.json'])
        copy(completed / 'captures' / (key + '.zip'), 'archives/' + key + '.zip', record['archiveSha256'])
        copy(completed / (key + '-expected.json'), key + '-expected.json')
        prefix = 'refinement/' + key + '/'; headers = []
        for name, row in indexed.items():
            if name.startswith(prefix) and not name.startswith(prefix + 'native-bridge/strong-left/'):
                suffix = name[len(prefix):]
                copy(original_backup / name, 'headers/' + key + '/' + suffix, row['Sha256'])
                headers.append(dict(path=suffix, bytes=row['Bytes'], sha256=row['Sha256']))
        assert len(headers) == 8
        original_case = next(row for row in prior_protocol['cases'] if row['key'] == key)
        cases.append(dict(key=key, originalCapture=str(original_backup / 'refinement' / key),
            archive='archives/' + key + '.zip', archiveSha256=record['archiveSha256'], expectedMembers=key + '-expected.json',
            headers='headers/' + key, expectedHeaders=sorted(headers, key=lambda r: r['path']),
            fixture=original_case['fixture'], physicsResolution=1 if key == 'repeat' else 3,
            muscleSubsteps=2 if key == 'repeat' else 1))
    save(package / 'protocol.json', dict(version='native-archive-accounting-comparison-v1', expectedControls=12, cases=cases,
        referenceFiles=reference_rows, originalManifestSha256=receipt['manifestSha256'], sourceRecordsRemain=True,
        actualPhysicsSteps=39995, physicalSamples=39997, oldSourcesModified=False,
        newRecordingCleanupEnabled=False, modelsWillExecute=False, nativePhysicsWillExecute=False,
        fullMotorMatrixQualified=False, physiologyQualified=False, avatarRecoveryAssessed=False))
    files = [dict(Path=p.relative_to(h.io(package)).as_posix(), Sha256=h.digest(p))
             for p in sorted(h.io(package).rglob('*')) if p.is_file()]
    save(package / 'configuration.json', dict(Revision=revision, PackageFiles=files, BackupDestination=str(backup),
        IsolatedNativeAccounting=True, OperatorReportedCompletion=True, NoLiveMonitoring=True, ExpectedControls=12, ExpectedCaptures=2,
        PythonExecutable=sys.executable, PythonExecutableSha256=h.digest(sys.executable), OriginalCapture=str(original),
        ModelsWillExecute=False, NativePhysicsWillExecute=False, OriginalPackageWillBeModified=False, ArchiveCopiesShareDrive=True))
    pointer = dict(Package=str(package), Backup=str(backup), Revision=revision, NoLiveMonitoring=True,
                   Launcher=str(package / 'source/tools/start-native-accounting-window.ps1'))
    save(ROOT / 'artifacts' / (package.name + '-prepared.json'), pointer)
    print(json.dumps(pointer, indent=2), flush=True)


def preflight(package):
    cfg = read(package / 'configuration.json'); protocol = read(package / 'protocol.json')
    assert cfg['IsolatedNativeAccounting'] and cfg['OperatorReportedCompletion'] and cfg['NoLiveMonitoring']
    assert cfg['ExpectedControls'] == protocol['expectedControls'] == 12 and cfg['ExpectedCaptures'] == len(protocol['cases']) == 2
    assert tuple(row['key'] for row in protocol['cases']) == KEYS
    for row in cfg['PackageFiles']: assert h.digest(package / row['Path']) == row['Sha256'], row['Path']
    assert h.digest(cfg['PythonExecutable']) == cfg['PythonExecutableSha256']
    assert base.environment() == read(package / 'preparation-environment.json')
    assert (package / 'source/tools/native_evidence_archive.py').read_text(encoding='utf-8-sig') == (package / 'reviewed-archive-implementation.py').read_text(encoding='utf-8-sig')
    reader_change(package / 'reviewed-readers/analyse-rubral-native-body.py', package / 'source/tools/analyse-rubral-native-body.py')
    for name in READERS[1:]:
        assert (package / 'source/tools' / name).read_text(encoding='utf-8-sig') == (package / 'reviewed-readers' / name).read_text(encoding='utf-8-sig'), name
    controls_tree = ast.parse((package / 'source/tools/check-native-evidence-view.py').read_text(encoding='utf-8-sig'))
    names = [node.args[0].value for node in ast.walk(controls_tree) if isinstance(node, ast.Call) and isinstance(node.func, ast.Name)
             and node.func.id == 'check' and node.args and isinstance(node.args[0], ast.Constant)]
    assert len(names) == len(set(names)) == 12
    recorded = read(package / 'reviewed-recording.json'); motor = read(package / 'reviewed-motor.json')
    assert recorded['recordingCriteriaPassed'] and recorded['operatorReportedCompletion'] and recorded['controlsPassed'] == 24
    assert motor['numericalCriteriaPassed'] and motor['savedAnalysisExactlyReproduced']
    original = motor['originalFullAuditReceipt']
    assert h.digest(Path(original['capture']) / 'backup-manifest.json') == h.digest(Path(original['backup']) / 'backup-manifest.json') == protocol['originalManifestSha256']
    assert h.digest(package / 'reviewed-analysis.json') == motor['analysisSha256']
    for row in protocol['referenceFiles']: assert h.digest(package / row['path']) == row['sha256']
    for case in protocol['cases']:
        expected = next(row for row in recorded['cases'] if row['key'] == case['key'])
        assert h.digest(package / case['archive']) == case['archiveSha256'] == expected['archiveSha256']
        assert Path(case['originalCapture']) == Path(original['backup']) / 'refinement' / case['key']
        for row in case['expectedHeaders']:
            assert h.digest(package / case['headers'] / row['path']) == h.digest(Path(case['originalCapture']) / row['path']) == row['sha256']
    for key in ('oldSourcesModified', 'newRecordingCleanupEnabled', 'modelsWillExecute', 'nativePhysicsWillExecute',
                'fullMotorMatrixQualified', 'physiologyQualified', 'avatarRecoveryAssessed'):
        assert protocol[key] is False, key
    print('Original motor capture identity, all accounting assertions, archive inputs and numerical dependencies preserved; no model/test executed.', flush=True)
    return protocol


def controls(package):
    preflight(package)
    module('visible_evidence_view_controls', package / 'source/tools/check-native-evidence-view.py').run(package / 'controls')


def array_digests(data):
    return {key: dict(shape=list(value.shape), dtype=str(value.dtype), sha256=hashlib.sha256(value.tobytes()).hexdigest().upper())
            for key, value in data.items() if isinstance(value, np.ndarray)}


def captures(package):
    protocol = preflight(package); passed = read(package / 'controls/controls.json')
    assert passed['completed'] and passed['passed'] == passed['expected'] == 12 and all(row['passed'] for row in passed['cases'])
    old = module('archive_legacy_native_accounting', package / 'reviewed-readers/analyse-rubral-native-body.py')
    candidate = module('archive_stream_native_accounting', package / 'source/tools/analyse-rubral-native-body.py')
    arrays = module('archive_accounting_hinge_arrays', package / 'reviewed-readers/analyse-native-hinge-comparison.py')
    wire = module('archive_accounting_native_wire', package / 'reviewed-readers/analyse-native-motor-bridge.py')
    old_analysis = read(package / 'reviewed-analysis.json'); old_recording = read(package / 'reviewed-recording-analysis.json')
    output = package / 'comparisons'; output.mkdir(); reports = []
    for case in protocol['cases']:
        key = case['key']; args = (arrays, wire, case['physicsResolution'], case['muscleSubsteps'], True)
        print('FULL ORIGINAL SOURCE/PACKET/NATIVE AUDIT ' + key, flush=True)
        _, original_spec, original_data, original_report = old.audit_at(package / 'audit-input', Path(case['originalCapture']), *args)
        assert original_report == next(row['source'] for row in old_analysis['cases'] if row['key'] == key)
        save(output / (key + '-original-spec.json'), original_spec)
        save(output / (key + '-original-accounting.json'), original_report)
        print('FULL ARCHIVED SOURCE/PACKET/NATIVE AUDIT ' + key, flush=True)
        with ExitStack() as stack:
            archive = stack.enter_context(EvidenceArchive(package / case['archive'], case['archiveSha256']))
            verified = archive.verify(read(package / case['expectedMembers']))
            view = stack.enter_context(EvidenceView(package / case['headers'], case['expectedHeaders'], {'native-bridge/strong-left': archive}))
            _, archived_spec, archived_data, archived_report = candidate.audit_at(package / 'audit-input', view.root, *args)
        assert original_spec == archived_spec and original_report == archived_report
        assert original_data.keys() == archived_data.keys() == {'strong-left'}
        original_values = original_data['strong-left']; archived_values = archived_data['strong-left']
        assert original_values.keys() == archived_values.keys()
        for field, value in original_values.items():
            if isinstance(value, np.ndarray):
                other = archived_values[field]
                assert isinstance(other, np.ndarray) and value.dtype == other.dtype and value.shape == other.shape and value.tobytes() == other.tobytes(), field
            else: assert value == archived_values[field], field
        digests = array_digests(archived_values)
        assert digests == next(row['arrayDigests'] for row in old_recording['cases'] if row['key'] == key)
        save(output / (key + '-archived-spec.json'), archived_spec)
        save(output / (key + '-archived-accounting.json'), archived_report)
        reports.append(dict(key=key, allArchivedMembersVerified=verified, fullAccountingExactlyEqual=True,
            originalReviewedAccountingReproduced=True, allDecodedArraysBitwiseEqual=True, arrayDigests=digests,
            actualPhysicsSteps=original_report['actualPhysicsSteps'], sourceFrames=original_report['cases'][0]['sourceFrames'],
            motorRows=original_report['cases'][0]['motorRows'], originalSourcesModified=False, newRawCleanupEnabled=False))
    assert sum(row['actualPhysicsSteps'] for row in reports) == protocol['actualPhysicsSteps']
    save(package / 'analysis.json', dict(completed=True, recordingCriteriaPassed=True, controlsPassed=12, cases=reports,
        fullSourcePacketNativeAccountingPreserved=True, originalSourcesModified=False, modelsExecuted=False,
        nativePhysicsExecuted=False, liveCaptureConsumerChanged=False, newRawCleanupEnabled=False,
        fullMotorMatrixQualified=False, physiologyQualified=False, avatarRecoveryAssessed=False))


if __name__ == '__main__':
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    parser = argparse.ArgumentParser(); parser.add_argument('command', choices=['prepare', 'preflight', 'postflight', 'controls', 'captures'])
    parser.add_argument('path', type=Path); args = parser.parse_args()
    dict(prepare=prepare, preflight=preflight, postflight=preflight, controls=controls, captures=captures)[args.command](args.path.resolve())
