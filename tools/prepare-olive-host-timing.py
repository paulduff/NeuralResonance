"""Freeze matched host timing only after the operator reports other studies finished."""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import shutil
import subprocess
import sys
import uuid
from zipfile import ZipFile
from olive_host_timing_protocol import protocol, schedule

ROOT = Path(__file__).resolve().parents[1]
git = lambda *a: subprocess.check_output(['git', *a], cwd=ROOT, encoding='utf-8').strip()


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec); spec.loader.exec_module(result)
    return result


def main(review_folder):
    assert not git('status', '--porcelain'), 'Commit before freezing.'
    revision = git('rev-parse', 'HEAD')
    assert revision == git('rev-parse', 'origin/main'), 'Push before freezing.'
    common = module('host_timing_capture_io', ROOT/'tools/review-olive-and-motor-20261109.py')
    read, digest, io = common.read, common.digest, common.io
    manifest = read(review_folder/'backup-manifest.json')
    backup_review = Path(manifest['Destination'])
    assert digest(review_folder/'backup-manifest.json') == digest(backup_review/'backup-manifest.json')
    assert len(manifest['Files']) == 1 and manifest['Files'][0]['Path'] == 'review.json'
    for folder in (review_folder, backup_review):
        assert digest(folder/'review.json') == manifest['Files'][0]['Sha256']
    report = read(review_folder/'review.json')
    assert report['operatorReportedCompletion'] and report['savedAnalysisExactlyReproduced']
    assert report['passedFocusedTests'] == 11 and report['analysis']['accuracyPassed']
    assert len(report['analysis']['cases']) == 6 and report['analysis']['exactControlsAcrossModes']
    assert report['analysis']['legacyControlGroups'] == report['analysis']['hostControlGroups'] == 12
    capture = Path(report['capture'])
    verified = common.verify(capture, ['test-binaries.json','reference-binaries.json','candidate-binaries.json'],
                             '91c53eab1ae51f9270a732e051eb0cc30782d352')
    assert all(report[k] == v for k,v in verified.items())
    owned = ['Structures/_SharedRuntime/OlivaryMembrane.cs', 'tools/OliveBatchProbe/OlivaryBatchKernel.cs',
        'tools/OliveBatchProbe/OlivaryCpuBatch.cs', 'tools/CudaInteropProbe/CudaDriverProbe.cs',
        'tools/OliveCudaProbe/OlivaryCudaBatch.cs', 'tools/OliveCudaProbe/CudaDriverProbe.Olive.cs',
        'tools/OliveCudaProbe/BatchControls.cs', 'tools/OliveCudaProbe/olivary.cu',
        'tools/OliveCudaTimingProbe/CudaDriverProbe.Memory.cs',
        'tools/OliveCudaHostProbe/OlivaryCudaBatch.Host.cs', 'tools/OliveCudaHostProbe/HostControls.cs']
    for path in owned:
        assert (ROOT/path).read_text(encoding='utf-8-sig') == (capture/'source'/path).read_text(encoding='utf-8-sig'), path
    copies = {}
    for folder in ('cpu-baseline','timing-evidence','recorded'):
        for path in (capture/folder).rglob('*'):
            if path.is_file():
                name = path.relative_to(capture).as_posix(); copies[name] = name
    for mode in ('reference','candidate'):
        for name in ('controls.json','host-controls.json'):
            copies[f'study/{mode}/{name}'] = f'accuracy-controls/{mode}/{name}'
    stamp = datetime.now(timezone.utc).strftime('%Y%m%d')
    package = ROOT/'artifacts'/('olive-host-timing-'+stamp+'-'+uuid.uuid4().hex[:6])
    backup = Path('D:/DNNE-desktop-runs/UnityWorld')/package.name
    assert not package.exists() and not backup.exists(); package.mkdir()
    subprocess.run(['git','archive','--format=zip','--output='+str(package/'source.zip'),revision], cwd=ROOT, check=True)
    with ZipFile(package/'source.zip') as archive:
        assert all(not Path(n).is_absolute() and '..' not in Path(n).parts for n in archive.namelist())
        archive.extractall(package/'source')
    copied = []
    for old, new in copies.items():
        target = package/new; target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(capture/old, target)
        assert digest(target) == digest(capture/old)
        copied.append(dict(source=old,target=new,sha256=digest(target)))
    for name, value in [('host-timing-protocol.json',protocol()), ('schedule.json',schedule()),
        ('provenance.json',dict(hostAccuracyReview=report, reviewManifestSha256=digest(review_folder/'backup-manifest.json'),
                               unchangedSources=owned,copiedFiles=copied))]:
        (package/name).write_text(json.dumps(value, indent=2)+'\n', encoding='utf-8')
    shutil.copy2(package/'source/tools/backup-unity-run.ps1', package/'backup-run.ps1')
    files = [dict(Path=p.relative_to(io(package)).as_posix(),Sha256=digest(p))
             for p in sorted(io(package).rglob('*')) if p.is_file()]
    dotnet = shutil.which('dotnet'); assert dotnet
    config = dict(Revision=revision,PreparedUtc=datetime.now(timezone.utc).isoformat(),PackageFiles=files,
        DotnetExecutable=dotnet,PythonExecutable=sys.executable,BackupDestination=str(backup),NoLiveMonitoring=True,
        HostAccuracyReviewed=True,RequiresOtherStudiesFinished=True,ExpectedTestCases=11,ExpectedCases=6,
        ExpectedProcesses=72,ExpectedTrials=144,ExpectedControlGroups=24,
        TestFilter='FullyQualifiedName~OlivaryBatchTests|FullyQualifiedName~SolutionManifestTests',
        TestClasses=['OlivaryBatchTests','SolutionManifestTests'],LiveBrainAccessed=False,OldSavesLoaded=False)
    (package/'configuration.json').write_text(json.dumps(config, indent=2)+'\n', encoding='utf-8')
    pointer = dict(Package=str(package),Backup=str(backup),Revision=revision,PreparedOnly=True,NoLiveMonitoring=True,
        OtherStudiesReportedFinished=True,Launcher=str(package/'source/tools/start-olive-host-timing-window.ps1'))
    (ROOT/'artifacts'/('olive-host-timing-prepared-'+stamp+'.json')).write_text(json.dumps(pointer, indent=2)+'\n', encoding='utf-8')
    print(json.dumps(pointer, indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--host-review', type=Path, required=True)
    parser.add_argument('--other-studies-finished', action='store_true', required=True)
    args = parser.parse_args(); main(args.host_review.resolve())
