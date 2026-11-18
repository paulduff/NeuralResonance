"""Freeze isolated mechanical controls; all execution belongs in visible PowerShell."""
import argparse
from datetime import datetime, timezone
import hashlib
from io import BytesIO
import json
from pathlib import Path, PurePosixPath
import shutil
import subprocess
import sys
import uuid
from zipfile import ZipFile
import receptor_membrane_gate as inventory
from afferent_precision_gate import h, read, save
from cleft_trajectory_reference import base

ROOT = Path(__file__).resolve().parents[1]
BASELINE = 'abd7de9'
COUNTS = dict(CoupledCanalMechanicsTests=23, ClockedVestibularMechanicsTests=16,
              SkullKinematicsTests=12, VestibularBundleInputTests=25)
SOURCE_FILES = ['PMC3005417.html', 'FD23-mathml.xml', 'FD24-mathml.xml', 'FD25-mathml.xml',
                'FD26-mathml.xml', 'ifediba-2007-transcription.json', 'source-review.json']


def prepare():
    assert not h.git('status', '--porcelain'), 'Commit first.'
    revision = h.git('rev-parse', 'HEAD')
    assert revision == h.git('rev-parse', 'origin/main'), 'Push first.'
    assert shutil.disk_usage('D:/').free >= 8 * 1024**3
    source_evidence = ROOT / 'artifacts/vestibular-mechanics-source-20261118'
    h.verify(source_evidence, SOURCE_FILES)
    runtime_source = ROOT / 'artifacts/vestibular-bundle-20261118-092238'
    h.verify(runtime_source, ['managed-runtime-before.json'])
    methods = inventory.inventory(ROOT, COUNTS)
    parent = Path('D:/DNNE-build-work'); parent.mkdir(exist_ok=True)
    package = parent / ('coupled-canal-' + datetime.now(timezone.utc).strftime('%Y%m%d') + '-' + uuid.uuid4().hex[:6])
    backup = Path('D:/DNNE-desktop-runs/UnityWorld') / package.name
    assert not package.exists() and not backup.exists()
    package.mkdir()
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
    # Compare unchanged pre-candidate Avatar kernels against the committed parent.
    baseline_revision = h.git('rev-parse', BASELINE)
    original = subprocess.check_output(['git', 'archive', '--format=zip', baseline_revision,
        'src/NRE.SimAvatar', 'Protocol', 'Structures/_SharedRuntime', 'UnityWorld/Assets/NRE/Runtime'], cwd=ROOT)
    (package / 'baseline-kernels.zip').write_bytes(original)
    protected = []
    with ZipFile(BytesIO(original)) as archive:
        for entry in archive.infolist():
            if not entry.filename.endswith('.cs'): continue
            data = archive.read(entry)
            assert data == (package / 'source' / entry.filename).read_bytes(), entry.filename
            protected.append(dict(path=entry.filename, sha256=hashlib.sha256(data).hexdigest().upper()))
    (package / 'source-evidence').mkdir()
    for name in SOURCE_FILES:
        shutil.copyfile(source_evidence / name, package / 'source-evidence' / name)
    shutil.copyfile(runtime_source / 'managed-runtime-before.json', package / 'qualified-managed-runtime.json')
    shutil.copyfile(package / 'source/tools/backup-unity-run.ps1', package / 'backup-run.ps1')
    save(package / 'preparation-environment.json', base.environment())
    save(package / 'protocol.json', dict(version='coupled-canal-component-controls-v1', baselineRevision=baseline_revision,
        protectedSources=protected, sourceFiles=SOURCE_FILES, expectedCases=sum(COUNTS.values()),
        coupledCanalControls=23, existingMechanicalControls=53, sourceFrame='declared source-prime acceleration components',
        sourceSpecimen='right human labyrinth 1; printed precision',
        testsExecutedDuringPreparation=False, modelsExecutedDuringPreparation=False,
        independentPublishedTrajectoryComparison=False, skullFrameConversionQualified=False,
        physiologicalCalibrationQualified=False, localBundleMapQualified=False, liveSelectionChanged=False))
    files = [dict(Path=p.relative_to(h.io(package)).as_posix(), Sha256=h.digest(p))
             for p in sorted(h.io(package).rglob('*')) if p.is_file()]
    dotnet = shutil.which('dotnet'); assert dotnet
    save(package / 'configuration.json', dict(Revision=revision, PackageFiles=files,
        DotnetExecutable=dotnet, DotnetExecutableSha256=h.digest(dotnet), PythonExecutable=sys.executable,
        PythonExecutableSha256=h.digest(sys.executable), BackupDestination=str(backup),
        IsolatedCoupledCanal=True, NoLiveMonitoring=True, ExpectedTestCases=sum(COUNTS.values()),
        ExpectedClassCounts=COUNTS, ExpectedTestMethods=methods, TestFilter='|'.join('FullyQualifiedName~' + c for c in COUNTS),
        MechanicalCalibrationQualified=False, IndependentCoupledTrajectoryQualified=False,
        StationaryInitializationQualified=False, SpatialGeometryCalibrated=False, AfferentFiringQualified=False,
        PhysiologyQualified=False, AvatarRecoveryAssessed=False, LiveSelectionChanged=False, ArchiveCopiesShareDrive=True))
    pointer = dict(Package=str(package), Backup=str(backup), Revision=revision, NoLiveMonitoring=True,
        Launcher=str(package / 'source/tools/start-coupled-canal-window.ps1'))
    save(ROOT / 'artifacts' / (package.name + '-prepared.json'), pointer)
    print(json.dumps(pointer, indent=2), flush=True)


def preflight(package):
    cfg = read(package / 'configuration.json'); protocol = read(package / 'protocol.json')
    assert cfg['IsolatedCoupledCanal'] and cfg['NoLiveMonitoring'] and cfg['ExpectedClassCounts'] == COUNTS
    assert cfg['ExpectedTestCases'] == protocol['expectedCases'] == sum(COUNTS.values()) == 76
    assert inventory.inventory(package / 'source', COUNTS) == cfg['ExpectedTestMethods']
    for row in cfg['PackageFiles']: assert h.digest(package / row['Path']) == row['Sha256'], row['Path']
    for tool in ('Dotnet', 'Python'): assert h.digest(cfg[tool + 'Executable']) == cfg[tool + 'ExecutableSha256']
    assert base.environment() == read(package / 'preparation-environment.json')
    for directory in read(package / 'qualified-managed-runtime.json')['directories']:
        root = h.io(directory['path'])
        assert {str(p) for p in root.rglob('*') if p.is_file()} == {str(h.io(e['path'])) for e in directory['files']}
        for row in directory['files']: assert h.digest(row['path']) == row['sha256']
    with ZipFile(package / 'baseline-kernels.zip') as archive:
        assert archive.comment.decode().strip() == protocol['baselineRevision']
        assert {row['path'] for row in protocol['protectedSources']} == {n for n in archive.namelist() if n.endswith('.cs')}
        for row in protocol['protectedSources']:
            assert archive.read(row['path']) == (package / 'source' / row['path']).read_bytes()
            assert h.digest(package / 'source' / row['path']) == row['sha256']
    catalog = read(package / 'source/docs/reference-data/vestibular-ifediba-2007.json')
    assert catalog == read(package / 'source-evidence/ifediba-2007-transcription.json')
    assert catalog['retainedHtmlSha256'].upper() == h.digest(package / 'source-evidence/PMC3005417.html')
    for key in ('testsExecutedDuringPreparation', 'modelsExecutedDuringPreparation', 'independentPublishedTrajectoryComparison',
                'skullFrameConversionQualified', 'physiologicalCalibrationQualified', 'localBundleMapQualified', 'liveSelectionChanged'):
        assert protocol[key] is False, key
    print('Pinned canal equations, pre-candidate kernels, numerical dependencies and managed runtime preserved; no model/test executed.', flush=True)


def runtime(package):
    preflight(package)
    assert read(package / 'managed-runtime-before.json') == read(package / 'qualified-managed-runtime.json')


def check_tests(package):
    preflight(package)
    result = inventory.check_tests(package, COUNTS)
    save(package / 'analysis.json', dict(completed=True, componentChecksPassed=True, tests=result,
        coupledCanalChecksPassed=True, independentPublishedTrajectoryQualified=False,
        sourceToSkullFrameQualified=False, localBundleMapQualified=False, mechanicalCalibrationQualified=False,
        independentCoupledTrajectoryQualified=False, stationaryInitializationQualified=False,
        spatialGeometryCalibrated=False, afferentFiringQualified=False, physiologyQualified=False, avatarRecoveryAssessed=False))


if __name__ == '__main__':
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    parser = argparse.ArgumentParser()
    parser.add_argument('command', choices=['prepare', 'preflight', 'postflight', 'runtime', 'check-tests'])
    parser.add_argument('package', type=Path, nargs='?')
    args = parser.parse_args()
    if args.command == 'prepare': prepare()
    else: dict(preflight=preflight, postflight=preflight, runtime=runtime, **{'check-tests':check_tests})[args.command](args.package.resolve())
