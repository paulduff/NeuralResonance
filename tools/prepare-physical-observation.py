"""Freeze combined body/contact checks; no tests or model execution in preparation."""
from datetime import datetime, timezone
from hashlib import sha256
import json
import os
from pathlib import Path, PurePosixPath
import shutil
import subprocess
import uuid
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[1]


def io_path(path):
    path = Path(path).resolve()
    return Path('\\\\?\\' + str(path)) if os.name == 'nt' and not str(path).startswith('\\\\?\\') else path


def main():
    def git(*args):
        return subprocess.check_output(['git', *args], cwd=ROOT, encoding='utf-8').strip()
    if git('status', '--porcelain'):
        raise RuntimeError('Commit clean source before freezing.')
    revision = git('rev-parse', 'HEAD')
    if revision != git('rev-parse', 'origin/main'):
        raise RuntimeError('Push before freezing.')
    dotnet = shutil.which('dotnet')
    if not dotnet:
        raise RuntimeError('No dotnet executable.')
    stamp = datetime.now(timezone.utc).strftime('%Y%m%d')
    package = ROOT / 'artifacts' / ('physical-observation-' + stamp + '-' + uuid.uuid4().hex[:6])
    backup = Path('D:/DNNE-desktop-runs/UnityWorld') / package.name
    if package.exists() or backup.exists():
        raise RuntimeError('Fresh capture required.')
    package.mkdir()
    subprocess.run(['git', 'archive', '--format=zip', '--output=' + str(package / 'source.zip'), revision],
                   cwd=ROOT, check=True)
    with ZipFile(package / 'source.zip') as archive:
        for entry in archive.infolist():
            p = PurePosixPath(entry.filename)
            if p.is_absolute() or '..' in p.parts or ':' in entry.filename or '\\' in entry.filename:
                raise RuntimeError('Unsafe archive path.')
            if ((entry.external_attr >> 16) & 0o170000) == 0o120000:
                raise RuntimeError('Archive links are not supported.')
            destination = io_path(package / 'source' / p)
            if entry.is_dir():
                destination.mkdir(parents=True, exist_ok=True)
            else:
                destination.parent.mkdir(parents=True, exist_ok=True)
                with destination.open('xb') as stream:
                    stream.write(archive.read(entry))
    shutil.copyfile(package / 'source/tools/backup-unity-run.ps1', package / 'backup-run.ps1')
    files = [dict(Path=p.relative_to(io_path(package)).as_posix(), Sha256=sha256(p.read_bytes()).hexdigest().upper())
             for p in sorted(io_path(package).rglob('*')) if p.is_file()]
    counts = dict(PhysicalObservationTests=31, PhysicalSampleProducerTests=16, PhysicalSampleTransportTests=22, SolutionManifestTests=1)
    configuration = dict(Revision=revision, PreparedUtc=datetime.now(timezone.utc).isoformat(),
        PackageFiles=files, DotnetExecutable=dotnet, BackupDestination=str(backup),
        NoLiveMonitoring=True, ExpectedTestCases=sum(counts.values()), ExpectedClassCounts=counts,
        TestFilter='|'.join('FullyQualifiedName~' + name for name in counts),
        LiveBodySelected=False, ModelExecuted=False, NoTimingMeasured=True)
    (package / 'configuration.json').write_text(json.dumps(configuration, indent=2) + '\n', encoding='utf-8')
    pointer = dict(Package=str(package), Backup=str(backup), Revision=revision, PreparedOnly=True,
                   NoLiveMonitoring=True, Launcher=str(package / 'source/tools/start-physical-observation-window.ps1'))
    with (package.parent / (package.name + '-prepared.json')).open('x', encoding='utf-8') as stream:
        stream.write(json.dumps(pointer, indent=2) + '\n')
    print(json.dumps(pointer, indent=2))


if __name__ == '__main__':
    main()
