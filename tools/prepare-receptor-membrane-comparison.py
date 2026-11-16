"""Freeze the coupled comparison only after a reported, reviewed source gate.

This prepares files and inspects dependency metadata. It runs no tests or cells.
Invoke with the existing qualified numerical Python; do not install dependencies.
"""
import argparse
from datetime import datetime, timezone
from hashlib import sha1
import importlib.util
import json
import os
from pathlib import Path, PurePosixPath
import shutil
import subprocess
import sys
import uuid
from zipfile import ZipFile

for key in ('OPENBLAS_NUM_THREADS', 'OMP_NUM_THREADS', 'MKL_NUM_THREADS', 'NUMEXPR_NUM_THREADS'):
    os.environ[key] = '1'
ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'tools'))
from receptor_membrane_protocol import protocol


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    value = importlib.util.module_from_spec(spec); spec.loader.exec_module(value)
    return value


def check_environment(package):
    if not __debug__: raise RuntimeError('Dependency assertions must remain enabled.')
    assert Path(__file__).resolve() == (package / 'source/tools/prepare-receptor-membrane-comparison.py').resolve()
    h = module('receptor_environment_packaging', ROOT / 'tools/prepare-causal-motor.py')
    config = h.read(package / 'configuration.json')
    assert config['IsolatedReceptorMembraneComparison'] and config['NoLiveMonitoring']
    for entry in config['PackageFiles']: assert h.digest(package / entry['Path']) == entry['Sha256']
    environment = module('receptor_preflight_environment', ROOT / 'tools/golgi-reference-resume.py').environment()
    assert environment == h.read(package / 'reference-preflight.json')['environment'], 'Numerical dependencies changed.'
    print(json.dumps(dict(numericalEnvironmentPreserved=True, numpy=environment['numpy'], scipy=environment['scipy'],
        modelsExecuted=False, testsExecuted=False)), flush=True)


def main(review_folder):
    if not __debug__: raise RuntimeError('Preparation assertions must remain enabled.')
    h = module('receptor_comparison_packaging', ROOT / 'tools/prepare-causal-motor.py')
    assert Path(h.git('rev-parse', '--show-toplevel')).resolve() == ROOT.resolve()
    assert not h.git('status', '--porcelain'), 'Commit clean source first.'
    revision = h.git('rev-parse', 'HEAD'); assert revision == h.git('rev-parse', 'origin/main'), 'Push first.'
    h.verify(review_folder, ['review.json', 'managed-runtime.json'])
    gate = h.read(review_folder / 'review.json')
    assert gate['operatorReportedCompletion'] and gate['gateCriteriaPassed'] and gate['savedAnalysisExactlyReproduced']
    assert gate['passedTests'] == 97 and gate['originalSourceConditions'] == 5
    assert h.digest(review_folder / 'managed-runtime.json') == gate['managedRuntimeSha256']
    capture = Path(gate['capture'])
    assert h.digest(capture / 'backup-manifest.json') == gate['manifestSha256']
    h.verify(capture, ['source.zip', 'configuration.json', 'probe-binaries.json', 'managed-runtime-before.json',
        'managed-runtime-after.json', 'source-analysis.json', 'test-results/focused.trx'])
    assert h.digest(capture / 'managed-runtime-before.json') == h.digest(capture / 'managed-runtime-after.json') == gate['managedRuntimeSha256']
    # Reuse exact compiled files and retain their original source archive.
    # Permit documentation/rig additions, but no intervening compilation input.
    changed = h.git('diff', '--name-only', gate['sourceRevision'], revision).splitlines()
    build_inputs = {'.cs', '.csproj', '.props', '.targets', '.sln', '.slnx', '.resx'}
    assert not [n for n in changed if Path(n).suffix.lower() in build_inputs or
        Path(n).name.lower() in ('global.json', 'nuget.config')], 'Compiled source changed since the gate; repeat its qualification.'
    original_probe = Path(gate['qualifiedProbeDirectory'])
    original_probe.relative_to(capture / 'source/artifacts')
    binaries = h.read(capture / 'probe-binaries.json')
    assert len(binaries) == gate['qualifiedProbeFiles'] and binaries
    binary_names = []
    for entry in binaries:
        p = Path(entry['Path']); p.relative_to(original_probe)
        assert h.digest(p) == entry['Sha256']
        binary_names.append(p.relative_to(capture).as_posix())
    h.verify(capture, binary_names)

    numerical_folder = ROOT / 'artifacts/causal-motor-comparison-review-20261113'
    motor_folder = ROOT / 'artifacts/motor-unit-circuit-review-20261109'
    h.verify(numerical_folder, ['review.json']); h.verify(motor_folder, ['review.json'])
    numerical = h.read(numerical_folder / 'review.json'); motor = h.read(motor_folder / 'review.json')
    for review in (numerical, motor):
        assert review['operatorReportedCompletion'] and review['savedAnalysisExactlyReproduced']
    assert numerical['analysis']['numericalCriteriaPassed']
    numerical_capture = Path(numerical['capture']); motor_capture = Path(motor['capture'])
    for path, review in ((numerical_capture, numerical), (motor_capture, motor)):
        assert h.digest(path / 'backup-manifest.json') == review['manifestSha256']
    environment_code = 'tools/golgi-reference-resume.py'
    motor_code = ['tools/' + n for n in ('motoneuron-active-reference.py', 'motoneuron-reference-check.py',
        'motoneuron_active_protocol.py', 'motoneuron_bounded_jacobian.py')]
    h.verify(numerical_capture, ['reference-preflight.json', 'source/' + environment_code])
    h.verify(motor_capture, ['source/' + n for n in motor_code])
    for original, names in ((numerical_capture, [environment_code]), (motor_capture, motor_code)):
        for name in names:
            assert (ROOT / name).read_text(encoding='utf-8-sig') == h.io(original / 'source' / name).read_text(encoding='utf-8-sig'), name
    environment = module('receptor_comparison_environment', ROOT / environment_code).environment()
    assert environment == h.read(numerical_capture / 'reference-preflight.json')['environment'], 'Qualified numerical dependencies changed.'

    spec = protocol()
    motor_git = ROOT / 'artifacts/reference-motoneuron-239582-20261106'
    source_names = ['na3rp.mod', 'naps.mod', 'kdrRL.mod', 'mAHP.mod', 'gh.mod', 'L_Ca_inact.mod',
        'FivecomptcMG/FivecomptcMG_0/FivecomptcMG_0.hoc']
    source_blobs = []
    for name in source_names:
        key = spec['motorSourceRevision'] + ':' + name
        blob = subprocess.check_output(['git', 'show', key], cwd=motor_git)
        expected = subprocess.check_output(['git', 'rev-parse', key], cwd=motor_git, text=True).strip()
        assert sha1(b'blob ' + str(len(blob)).encode('ascii') + b'\0' + blob).hexdigest() == expected
        source_blobs.append((name, expected, blob))
    receptor_source = ROOT / 'artifacts/motor-receptor-source-20261116'
    h.verify(receptor_source, ['source-manifest.json', 'ampa.mod', 'gabaa.mod', 'README'])
    receptor_manifest = h.read(receptor_source / 'source-manifest.json')
    assert receptor_manifest['commit'] == spec['receptorSourceRevision'] and receptor_manifest['repository'] == 'ModelDBRepository/18500'
    for entry in receptor_manifest['files']:
        data = (receptor_source / entry['path']).read_bytes()
        assert len(data) == entry['bytes'] and h.digest(receptor_source / entry['path']) == entry['sha256'].upper()
        assert sha1(b'blob ' + str(len(data)).encode('ascii') + b'\0' + data).hexdigest() == entry['gitBlobSha1']
    minimum_free = 20 * 1024**3
    for disk in (ROOT, Path('D:/DNNE-desktop-runs/UnityWorld')):
        assert shutil.disk_usage(disk).free >= minimum_free, 'Require 20 GiB free on each capture/backup drive.'
    package = ROOT / 'artifacts' / ('receptor-membrane-comparison-' + datetime.now(timezone.utc).strftime('%Y%m%d') + '-' + uuid.uuid4().hex[:6])
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
    copied = []

    def copy(original, destination):
        target = h.io(package / destination); target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(h.io(original), target); assert h.digest(original) == h.digest(target)
        copied.append(dict(original=str(original), path=destination, sha256=h.digest(target)))

    probe_relative = 'source/artifacts/receptor-membrane-qualified-probe'
    probe_binaries = []
    for entry in binaries:
        relative = probe_relative + '/' + Path(entry['Path']).relative_to(original_probe).as_posix()
        copy(Path(entry['Path']), relative)
        probe_binaries.append(dict(Path=str(package / relative), Sha256=entry['Sha256']))
    for original, destination in (
        (review_folder / 'review.json', 'reference/gate-review.json'),
        (review_folder / 'managed-runtime.json', 'reference/qualified-managed-runtime.json'),
        (capture / 'source.zip', 'reference/qualified-probe-source.zip'),
        (capture / 'backup-manifest.json', 'reference/gate-capture-manifest.json'),
        (numerical_folder / 'review.json', 'reference/numerical-environment-review.json'),
        (motor_folder / 'review.json', 'reference/motor-reference-review.json'),
        (numerical_capture / 'reference-preflight.json', 'reference-preflight.json'),
        (receptor_source / 'source-manifest.json', 'reference/receptor-source/source-manifest.json')):
        copy(original, destination)
    for name in ('ampa.mod', 'gabaa.mod', 'README'): copy(receptor_source / name, 'reference/receptor-source/' + name)
    motor_sources = []
    for name, blob_id, data in source_blobs:
        destination = 'reference/motor-source/' + PurePosixPath(name).name
        target = package / destination; target.parent.mkdir(parents=True, exist_ok=True); target.write_bytes(data)
        motor_sources.append(dict(upstreamPath=name, path=destination, gitBlobSha1=blob_id, sha256=h.digest(target)))
    provenance = dict(gateReview=gate, numericalReview=numerical, motorReferenceReview=motor,
        compiledProbeSourceRevision=gate['sourceRevision'], exactQualifiedProbeReused=True,
        compilationInputsUnchanged=True, motorSourceFiles=motor_sources, copied=copied,
        testsExecutedDuringPreparation=False, modelsExecutedDuringPreparation=False)
    for name, value in [('receptor-membrane-input.json', spec), ('provenance.json', provenance), ('probe-binaries.json', probe_binaries)]:
        (package / name).write_text(json.dumps(value, indent=2, allow_nan=False) + '\n')
    shutil.copyfile(package / 'source/tools/backup-unity-run.ps1', package / 'backup-run.ps1')
    files = [dict(Path=p.relative_to(h.io(package)).as_posix(), Sha256=h.digest(p)) for p in sorted(h.io(package).rglob('*')) if p.is_file()]
    dotnet = h.read(capture / 'configuration.json')['DotnetExecutable']
    qualified_runtime = h.read(review_folder / 'managed-runtime.json')
    assert Path(dotnet).resolve() == Path(qualified_runtime['host']).resolve() and h.digest(dotnet) == qualified_runtime['hostSha256']
    config = dict(Revision=revision, PreparedUtc=datetime.now(timezone.utc).isoformat(), PackageFiles=files,
        DotnetExecutable=dotnet, DotnetExecutableSha256=h.digest(dotnet), PythonExecutable=sys.executable,
        PythonExecutableSha256=h.digest(sys.executable), BackupDestination=str(backup), NoLiveMonitoring=True,
        IsolatedReceptorMembraneComparison=True, CompiledProbeSourceRevision=gate['sourceRevision'],
        QualifiedProbeRelativePath=probe_relative + '/NeuralResonanceEngine.DNNE.Tests.dll',
        MotorSourceRevision=spec['motorSourceRevision'], ReceptorSourceRevision=spec['receptorSourceRevision'],
        ExpectedCandidateConditions=18, ExpectedReferenceConditions=18, ExpectedContactDeliveries=648,
        ExpectedAnalyticControls=6, ExpectedJacobianControls=36, ExpectedReferenceContrasts=18,
        ExpectedCandidateContrasts=18, ExpectedRefinementContrasts=12, MinimumFreeBytes=minimum_free,
        PresynapticEventsAreFixtures=True, PresynapticNeuronsExecuted=False, NativePhysicsRequested=False,
        PhysiologyQualified=False, AvatarRecoveryAssessed=False)
    (package / 'configuration.json').write_text(json.dumps(config, indent=2) + '\n')
    pointer = dict(Package=str(package), Backup=str(backup), Revision=revision, PreparedOnly=True, NoLiveMonitoring=True,
        Launcher=str(package / 'source/tools/start-receptor-membrane-comparison-window.ps1'))
    (package.parent / (package.name + '-prepared.json')).write_text(json.dumps(pointer, indent=2) + '\n')
    print(json.dumps(pointer, indent=2), flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    action = parser.add_mutually_exclusive_group(required=True)
    action.add_argument('--gate-review', type=Path)
    action.add_argument('--check-environment', type=Path)
    args = parser.parse_args()
    if args.check_environment: check_environment(args.check_environment.resolve())
    else: main(args.gate_review.resolve())
