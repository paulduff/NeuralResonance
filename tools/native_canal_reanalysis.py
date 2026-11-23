"""Reanalyse completed native sensory evidence with explicit session-ID mapping."""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path, PurePosixPath
import shutil
import subprocess
import sys
import unittest
import uuid
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT/'tools'))
import native_avatar_tendon_study as h
from cleft_trajectory_reference import base
ORIGINAL = Path('D:/DNNE-build-work/native-canal-20261123-184323')
REVISION = 'f851ee1be3ac0e0c708c9e8db5f82ff4d08f22d0'
CHANGED = 'tools/native_canal_accounting.py'


def declared_repair(source):
    before = (ORIGINAL/'source'/CHANGED).read_text(encoding='utf-8-sig')
    replacements = [
        ('from head_observation_identity import original_observation\n',
         'from head_observation_identity import original_observation\nfrom native_geometry_identity import CrossSessionGeometry\n'),
        ('    observations = []\n', '    geometry = CrossSessionGeometry(ack[0], original[0])\n    observations = []\n'),
        ("        assert all(a[k] == old[k] for k in ('geometryJson', 'stateJson', 'paths', 'applicationsJson'))\n",
         '        geometry.check(a, old)\n'),
        ('    return dict(case=folder.name, originalPhysicalEndpointsExact=True, nativeHeadPayloadsExact=True,\n',
         '    return dict(case=folder.name, originalPhysicalStatesExact=True, nativeHeadPayloadsExact=True,\n'
         '                crossSessionGeometry=geometry.report(),\n')]
    for old, new in replacements:
        assert before.count(old) == 1; before = before.replace(old, new)
    assert (source/CHANGED).read_text(encoding='utf-8-sig') == before
    preserved = 0
    with ZipFile(ORIGINAL/'source.zip') as archive:
        for name in archive.namelist():
            if name.endswith('.py') and name != CHANGED:
                assert (source/name).read_text(encoding='utf-8-sig') == archive.read(name).decode('utf-8-sig').replace('\r\n', '\n'), name
                preserved += 1
    return dict(originalPythonFilesUnchanged=preserved, changedOriginalFile=CHANGED,
                change='Cross-session instance IDs mapped by exact object name; both body/path hashes independently verified.',
                rawPhysicalStateAndForceEqualityRetained=True, originalNumericalLimitsChanged=False,
                originalSensoryAnalysisUnchanged=True, liveIdentityRulesChanged=False, nativeCaptureRerun=False)


def controls_names():
    from check_native_geometry_identity import NativeGeometryIdentityControls
    names = ['check_native_geometry_identity.NativeGeometryIdentityControls.'+n
             for n in unittest.defaultTestLoader.getTestCaseNames(NativeGeometryIdentityControls)]
    assert len(names) == 25
    return names


def prepare(review):
    assert not h.git('diff', '--name-only') and not h.git('diff', '--cached', '--name-only'), 'Commit tracked changes first.'
    # The unfinished muscle initializer is explicitly excluded from this Python-
    # only git archive. It is neither compiled nor executed by this study.
    untracked = h.git('ls-files', '--others', '--exclude-standard').splitlines()
    assert set(untracked) <= {'src/NRE.SimAvatar/AvatarMuscleInitialBalance.cs'}, untracked
    revision = h.git('rev-parse', 'HEAD'); assert revision == h.git('rev-parse', 'origin/main')
    proof = h.read(review/'review.json')
    assert proof['operatorReportedCompletion'] and proof['originalFailurePreserved'] and proof['completedCases'] == 6
    assert proof['sourceRevision'] == REVISION and Path(proof['capture']) == ORIGINAL
    assert h.digest(ORIGINAL/'backup-manifest.json') == proof['manifestSha256']
    declaration = declared_repair(ROOT)
    package = Path('D:/DNNE-build-work')/('native-canal-reanalysis-'+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6])
    backup = Path('D:/DNNE-desktop-runs/UnityWorld')/package.name
    assert not package.exists() and not backup.exists(); package.mkdir()
    subprocess.run(['git', 'archive', '--format=zip', '--output='+str(package/'source.zip'), revision], cwd=ROOT, check=True)
    with ZipFile(package/'source.zip') as archive:
        for entry in archive.infolist():
            path = PurePosixPath(entry.filename)
            assert not path.is_absolute() and '..' not in path.parts and ':' not in entry.filename and '\\' not in entry.filename
            assert ((entry.external_attr >> 16) & 0o170000) != 0o120000
            target = package/'source'/path
            if entry.is_dir(): target.mkdir(parents=True, exist_ok=True)
            else:
                target.parent.mkdir(parents=True, exist_ok=True)
                with target.open('xb') as stream: stream.write(archive.read(entry))
    for source, name in [(review/'review.json', 'original-review.json'), (review/'failure-operands.json', 'failure-operands.json'),
                         (package/'source/tools/backup-unity-run.ps1', 'backup-run.ps1')]:
        shutil.copyfile(source, package/name)
    h.write(package/'declaration.json', declaration)
    h.write(package/'numerical-environment.json', base.environment())
    files = [dict(Path=p.relative_to(package).as_posix(), Sha256=h.digest(p)) for p in sorted(package.rglob('*')) if p.is_file()]
    h.write(package/'configuration.json', dict(Revision=revision, PackageFiles=files, BackupDestination=str(backup),
        NoLiveMonitoring=True, SavedNativeCanalReanalysis=True, OriginalCapture=str(ORIGINAL),
        OriginalManifestSha256=proof['manifestSha256'], ExpectedControls=25, ExpectedControlMethods=controls_names(),
        ExpectedSourceSteps=516096, ExpectedCases=6, PythonExecutable=sys.executable,
        PythonExecutableSha256=h.digest(sys.executable), PythonRuntime=h.python_runtime(),
        NativeCaptureRerun=False, NativeBodyExecuted=False, SensoryModelExecuted=False,
        OriginalNumericalLimitsChanged=False, IndependentConnectedAccuracyQualified=False,
        PhysiologicalFiringQualified=False, NeuronalMotorAuthority=False, AvatarStandingQualified=False,
        ExcludedUntrackedAuthoringFiles=untracked))
    pointer = dict(Package=str(package), Backup=str(backup), Revision=revision,
                   Launcher=str(package/'source/tools/start-native-canal-reanalysis-window.ps1'))
    h.write(ROOT/'artifacts'/(package.name+'-prepared.json'), pointer)
    print(json.dumps(pointer, indent=2), flush=True)


def preflight(package):
    cfg = h.read(package/'configuration.json'); proof = h.read(package/'original-review.json')
    assert cfg['SavedNativeCanalReanalysis'] and cfg['NoLiveMonitoring'] and not cfg['NativeCaptureRerun']
    assert cfg['ExpectedControlMethods'] == controls_names() and cfg['ExpectedControls'] == 25
    assert cfg['ExpectedSourceSteps'] == 516096 and cfg['ExpectedCases'] == 6
    for key in ('NativeBodyExecuted', 'SensoryModelExecuted', 'OriginalNumericalLimitsChanged', 'IndependentConnectedAccuracyQualified',
                'PhysiologicalFiringQualified', 'NeuronalMotorAuthority', 'AvatarStandingQualified'):
        assert cfg[key] is False
    for entry in cfg['PackageFiles']: assert h.digest(package/entry['Path']) == entry['Sha256'], entry['Path']
    assert h.digest(sys.executable) == cfg['PythonExecutableSha256'] and h.python_runtime() == cfg['PythonRuntime']
    assert cfg['PythonRuntime'] == h.read(ORIGINAL/'configuration.json')['PythonRuntime']
    assert base.environment() == h.read(package/'numerical-environment.json')
    assert declared_repair(package/'source') == h.read(package/'declaration.json')
    spec = importlib.util.spec_from_file_location('native_canal_reanalysis_files', ROOT/'tools/review-olive-and-motor-20261109.py')
    common = importlib.util.module_from_spec(spec); spec.loader.exec_module(common)
    verified = common.verify(ORIGINAL, ['probe-binaries.json'], REVISION, 'FAILED', 1, progress_every=1000)
    assert verified['manifestSha256'] == cfg['OriginalManifestSha256'] == proof['manifestSha256']
    print('Original failure, both saved copies, all sensory limits and numerical dependencies preserved. No Unity or neuronal model executed.', flush=True)


def controls(package):
    names = h.read(package/'configuration.json')['ExpectedControlMethods']
    suite = unittest.TestSuite(unittest.defaultTestLoader.loadTestsFromName(n) for n in names)
    result = unittest.TextTestRunner(verbosity=2).run(suite)
    evidence = dict(cases=result.testsRun, methods=names, passed=result.wasSuccessful() and result.testsRun == 25 and not result.skipped,
                    failures=[(t.id(), s) for t, s in result.failures], errors=[(t.id(), s) for t, s in result.errors], skipped=result.skipped)
    h.write(package/'identity-controls.json', evidence)
    assert evidence['passed'], 'Geometry identity controls failed; retain saved evidence.'


def analyse(package):
    from native_canal_analysis import analyse as account
    assert h.read(package/'identity-controls.json')['passed']
    print('Checking six SAVED native cases and all 516,096 recorded sensory steps. No body or neuronal integration.', flush=True)
    result = account(ORIGINAL)
    h.write(package/'analysis.json', result)
    assert result['completed'] and result['connectedAccountingPassed'] and result['sourceSteps'] == 516096
    for row in result['runs']:
        print(row['condition'], row['sourceStepsPerInterval'], 'recorded events', row['events'], 'accounting', row['accountingPassed'], flush=True)
    print('Saved body/sensory accounting reconciled. Sensory firing, independent coupled accuracy and standing remain separate.', flush=True)


if __name__ == '__main__':
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    p = argparse.ArgumentParser(); p.add_argument('command', choices=['prepare', 'preflight', 'controls', 'analyse']); p.add_argument('path', type=Path)
    a = p.parse_args(); {'prepare': prepare, 'preflight': preflight, 'controls': controls, 'analyse': analyse}[a.command](a.path.resolve())
