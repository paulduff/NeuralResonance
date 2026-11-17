"""Freeze source, declared fixtures and dependency metadata; execute no model/test."""
from datetime import datetime, timezone
import importlib.util
import json
import os
from pathlib import Path, PurePosixPath
import shutil
import subprocess
import sys
import uuid
from zipfile import ZipFile

for key in ('OMP_NUM_THREADS', 'OPENBLAS_NUM_THREADS', 'MKL_NUM_THREADS', 'NUMEXPR_NUM_THREADS'): os.environ[key] = '1'
ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'tools'))
from vestibular_numerics_protocol import protocol, COUNTS


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec); spec.loader.exec_module(result); return result


def save(path, value):
    with path.open('x', encoding='utf-8') as stream:
        json.dump(value, stream, indent=2, allow_nan=False); stream.write('\n')


def main():
    assert (ROOT / 'tools/start-vestibular-numerics-window.ps1').is_file(), \
        'The frozen vestibular comparison requires its visible PowerShell launcher.'
    h = module('vestibular_package_helpers', ROOT / 'tools/prepare-causal-motor.py')
    gate = module('vestibular_inventory', ROOT / 'tools/receptor_membrane_gate.py')
    assert not h.git('status', '--porcelain'), 'Commit clean source first.'
    revision = h.git('rev-parse', 'HEAD'); assert revision == h.git('rev-parse', 'origin/main'), 'Push first.'
    preservation_revision = 'f50baeb1435553060afa950c6c3897cdfaa4fc7a'
    preserved = ['src/NRE.SimAvatar/AvatarClockedVestibularMechanics.cs',
        'tests/NeuralResonanceEngine.DNNE.Tests/ClockedVestibularMechanicsTests.cs',
        'ControlProgram/VestibularLabyrinthTransducer.cs', 'ControlProgram/PhysicalBodyTransducerRuntime.cs',
        'Shared.Contracts/PhysicalSampleProtocol.cs', 'src/NRE.SimAvatar/AvatarPhysicalObservationProducer.cs',
        'src/NRE.SimAvatar/AvatarPhysicalObservationTransport.cs']
    for name in preserved:
        expected = subprocess.check_output(['git', 'show', preservation_revision+':'+name], cwd=ROOT).decode('utf-8-sig')
        assert expected.replace('\r\n', '\n') == (ROOT / name).read_text(encoding='utf-8-sig'), name
    methods = gate.inventory(ROOT, COUNTS); assert sum(COUNTS.values()) == 62
    environment = module('vestibular_dependency_metadata', ROOT / 'tools/golgi-reference-resume.py').environment()
    assert environment['numpy'] == '2.2.6' and environment['scipy'] == '1.16.3'
    package = ROOT / 'artifacts' / ('vestibular-numerics-'+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6])
    backup = Path('D:/DNNE-desktop-runs/UnityWorld') / package.name
    assert not package.exists() and not backup.exists(); package.mkdir()
    subprocess.run(['git', 'archive', '--format=zip', '--output='+str(package / 'source.zip'), revision], cwd=ROOT, check=True)
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
    save(package / 'vestibular-input.json', protocol())
    save(package / 'preparation-environment.json', environment)
    save(package / 'provenance.json', dict(candidatePreservationRevision=preservation_revision,
        preservedSourceFiles=preserved, testsExecutedDuringPreparation=False, modelsExecutedDuringPreparation=False,
        noPendingCaptureRead=True, liveSelectionChanged=False))
    shutil.copyfile(package / 'source/tools/backup-unity-run.ps1', package / 'backup-run.ps1')
    files = [dict(Path=p.relative_to(h.io(package)).as_posix(), Sha256=h.digest(p))
             for p in sorted(h.io(package).rglob('*')) if p.is_file()]
    dotnet = shutil.which('dotnet'); assert dotnet
    save(package / 'configuration.json', dict(Revision=revision, PreparedUtc=datetime.now(timezone.utc).isoformat(),
        PackageFiles=files, DotnetExecutable=dotnet, DotnetExecutableSha256=h.digest(dotnet),
        PythonExecutable=sys.executable, PythonExecutableSha256=h.digest(sys.executable), BackupDestination=str(backup),
        IsolatedVestibularNumerics=True, NoLiveMonitoring=True, ExpectedConditions=20, ExpectedNativeCaptures=40,
        ExpectedReferenceTrajectories=60, ExpectedControls=6, ExpectedTestCases=62, ExpectedClassCounts=COUNTS,
        ExpectedTestMethods=methods, TestFilter='|'.join('FullyQualifiedName~'+c for c in COUNTS),
        LiveSelectionChanged=False, PhysiologyQualified=False, AvatarRecoveryAssessed=False))
    pointer = dict(Package=str(package), Backup=str(backup), Revision=revision, NoLiveMonitoring=True,
        Launcher=str(package / 'source/tools/start-vestibular-numerics-window.ps1'))
    save(package.parent / (package.name+'-prepared.json'), pointer)
    print(json.dumps(pointer, indent=2), flush=True)


if __name__ == '__main__':
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    main()
