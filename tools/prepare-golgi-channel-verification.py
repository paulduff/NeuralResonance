"""Freeze committed source for a visible, unattended Golgi-channel verification.

No experiment or test is executed here. Builds/tests run from this new source
snapshot in PowerShell. Existing brain states and completed captures are not read.
"""
from datetime import datetime, timezone
from hashlib import sha256
import json
from pathlib import Path
import shutil
import subprocess
import sys
import uuid
from zipfile import ZipFile


REPO = Path(__file__).resolve().parents[1]
REFERENCE_REVISION = '2ffaeca558235d61222240fc74247cab1cd02fe7'


def git(*args, cwd=REPO):
    return subprocess.check_output(['git', *args], cwd=cwd, encoding='utf-8').strip()


def write(path, data):
    path.write_text(json.dumps(data, indent=2, allow_nan=False) + '\n', encoding='utf-8')


def main():
    if sys.argv[1:] not in ([], ['--cell']):
        raise ValueError('Use no arguments for channels, or --cell for the coupled-cell verification.')
    cell = sys.argv[1:] == ['--cell']
    assert not git('status', '--porcelain'), 'Commit coherent source before freezing a verification.'
    revision = git('rev-parse', 'HEAD')
    assert revision == git('rev-parse', 'origin/main'), 'Push the source before preparing the operator package.'
    reference = REPO / 'artifacts/reference-golgi-solinas-112685-20261106'
    assert git('rev-parse', 'HEAD', cwd=reference) == REFERENCE_REVISION
    assert not git('status', '--porcelain', cwd=reference)
    python = REPO / 'artifacts/numerical-reference-env-20261102/Scripts/python.exe'
    assert python.is_file(), 'Use the existing isolated NumPy/SciPy environment.'
    dotnet = shutil.which('dotnet'); assert dotnet
    stamp = datetime.now(timezone.utc).strftime('%Y%m%d')
    package = REPO / 'artifacts' / (('golgi-cell-' if cell else 'golgi-channels-') + stamp + '-' + uuid.uuid4().hex[:6])
    backup = Path('D:/DNNE-desktop-runs/UnityWorld') / package.name
    assert not package.exists() and not backup.exists()
    package.mkdir()
    archive = package / 'source.zip'
    subprocess.run(['git', 'archive', '--format=zip', '--output=' + str(archive), revision], cwd=REPO, check=True)
    with ZipFile(archive) as z:
        for name in z.namelist():
            assert not Path(name).is_absolute() and '..' not in Path(name).parts
        z.extractall(package / 'source')
    (package / 'reference').mkdir()
    hashes = {}
    for name in git('ls-files', cwd=reference).splitlines():
        destination = package / 'reference' / name
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(reference / name, destination)
        hashes[name] = sha256(destination.read_bytes()).hexdigest()
    write(package / 'reference/manifest.json', dict(revision=REFERENCE_REVISION,
        repository='https://github.com/ModelDBRepository/112685', sha256=hashes))
    for source, target in [('tools/analyse-golgi-channels.py', 'analyse-golgi-channels.py'),
                           ('tools/backup-unity-run.ps1', 'backup-run.ps1')]:
        shutil.copy2(package / 'source' / source, package / target)
    if cell:
        for name in ('analyse-golgi-cell.py', 'golgi-active-reference.py'):
            shutil.copy2(package / 'source/tools' / name, package / name)
    (package / 'source-revision.txt').write_text(revision + '\n', encoding='utf-8')
    package_files = [dict(Path=p.relative_to(package).as_posix(), Sha256=sha256(p.read_bytes()).hexdigest().upper())
                     for p in sorted(package.rglob('*')) if p.is_file()]
    write(package / 'configuration.json', dict(Revision=revision, PreparedUtc=datetime.now(timezone.utc).isoformat(),
        DotnetExecutable=dotnet, PythonExecutable=str(python), PackageFiles=package_files,
        BackupDestination=str(backup), NoLiveMonitoring=True, WholeCellPhysiologyQualified=False,
        VerificationKind='cell' if cell else 'channels',
        KnownUnresolvedFullSuiteFailure='NeuronalMemoryTests.RepeatedCorticalExperienceGraduallyRaisesConsolidation'))
    pointer = dict(Package=str(package), Backup=str(backup), Revision=revision, NoLiveMonitoring=True,
        Launcher=str(package / 'source/tools/start-golgi-channel-window.ps1'), PackageFiles=len(package_files))
    write(REPO / (('artifacts/golgi-cell-launch-' if cell else 'artifacts/golgi-channel-launch-') + stamp + '.json'), pointer)
    print(json.dumps(pointer, indent=2))


if __name__ == '__main__':
    main()
