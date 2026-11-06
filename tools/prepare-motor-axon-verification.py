"""Freeze pushed source for an independent visible motor-transport test window.

No tests, live services, old saves or prior experiment results are accessed here.
"""
from datetime import datetime, timezone
from hashlib import sha256
import json
from pathlib import Path
import shutil
import subprocess
import uuid
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[1]


def git(*args):
    return subprocess.check_output(['git', *args], cwd=ROOT, encoding='utf-8').strip()


def write(path, value):
    path.write_text(json.dumps(value, indent=2, allow_nan=False)+'\n', encoding='utf-8')


def main():
    assert not git('status', '--porcelain'), 'Commit the coherent candidate before freezing.'
    revision = git('rev-parse', 'HEAD')
    assert revision == git('rev-parse', 'origin/main'), 'Push before packaging.'
    dotnet = shutil.which('dotnet'); assert dotnet
    stamp = datetime.now(timezone.utc).strftime('%Y%m%d')
    package = ROOT/'artifacts'/('motor-axon-'+stamp+'-'+uuid.uuid4().hex[:6])
    backup = Path('D:/DNNE-desktop-runs/UnityWorld')/package.name
    assert not package.exists() and not backup.exists()
    package.mkdir()
    subprocess.run(['git','archive','--format=zip','--output='+str(package/'source.zip'),revision],cwd=ROOT,check=True)
    with ZipFile(package/'source.zip') as archive:
        assert all(not Path(n).is_absolute() and '..' not in Path(n).parts for n in archive.namelist())
        archive.extractall(package/'source')
    shutil.copy2(package/'source/tools/backup-unity-run.ps1',package/'backup-run.ps1')
    files=[dict(Path=p.relative_to(package).as_posix(),Sha256=sha256(p.read_bytes()).hexdigest().upper())
        for p in sorted(package.rglob('*')) if p.is_file()]
    write(package/'configuration.json',dict(Revision=revision,PreparedUtc=datetime.now(timezone.utc).isoformat(),
        DotnetExecutable=dotnet,PackageFiles=files,BackupDestination=str(backup),ExpectedTestCases=33,
        TestFilter='FullyQualifiedName~MotorAxonTransportTests|FullyQualifiedName~SolutionManifestTests',
        NoLiveMonitoring=True,LiveMotorPathEnabled=False))
    pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,
        Launcher=str(package/'source/tools/start-motor-axon-window.ps1'),NoLiveMonitoring=True)
    write(ROOT/'artifacts'/('motor-axon-launch-'+stamp+'.json'),pointer)
    print(json.dumps(pointer,indent=2))


if __name__=='__main__':
    main()
