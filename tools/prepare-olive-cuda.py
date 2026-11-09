"""Freeze CUDA membrane comparison with reviewed CPU traces and pinned NVRTC."""
from datetime import datetime, timezone
from hashlib import sha256
import json
from pathlib import Path
import shutil
import subprocess
import sys
import uuid
from zipfile import ZipFile

ROOT=Path(__file__).resolve().parents[1]
read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
git=lambda *a:subprocess.check_output(['git',*a],cwd=ROOT,encoding='utf-8').strip()
def digest(path):
    h=sha256()
    with path.open('rb') as stream:
        for block in iter(lambda:stream.read(4*1024*1024),b''):h.update(block)
    return h.hexdigest().upper()
def verified(capture,names):
    manifest=read(capture/'backup-manifest.json'); backup=Path(manifest['Destination'])
    assert digest(capture/'backup-manifest.json')==digest(backup/'backup-manifest.json')
    files={e['Path'].replace('\\','/'):e for e in manifest['Files']}
    for name in names:
        for folder in (capture,backup):
            assert digest(folder/name)==files[name]['Sha256'],name
    return files,backup


def main():
    assert not git('status','--porcelain'),'Commit before freezing.'
    revision=git('rev-parse','HEAD'); assert revision==git('rev-parse','origin/main'),'Push before freezing.'
    review=ROOT/'artifacts/olive-motor-review-20261109'; verified(review,['olive-review.json'])
    report=read(review/'olive-review.json')
    assert report['passedTests']==87 and report['savedBytesReverified'] and report['analysis']['exactBatchCases']==6
    cpu=Path(report['capture'])
    names=['study/protocol.json','study/results.json','study/analysis.json']
    for case in report['analysis']['evidence']:
        names.extend('study/'+case['key']+'/'+n for n in ('design.json','reference.bin','result.json'))
    owned=['Structures/_SharedRuntime/OlivaryMembrane.cs','tools/OliveBatchProbe/OlivaryBatchKernel.cs','tools/OliveBatchProbe/OlivaryCpuBatch.cs']
    verified(cpu,names+['source/'+n for n in owned])
    assert digest(cpu/'backup-manifest.json')==report['manifestSha256'] and len(names)==21
    dependency=ROOT/'artifacts/cuda-nvrtc-13.0.88'; origin=read(dependency/'dependency.json')
    assert origin['archiveSha256']=='8c50a52467826167e0dbe99936140c52d62272bfc5849fe2d6587d050c8c5d29'
    selected=['nvrtc-13.0.88.zip','redistrib_13.0.2.json',origin['compilerDll'],origin['builtinsDll']]
    selected+=sorted(e['path'] for e in origin['files'] if Path(e['path']).name.startswith('LICENSE'))
    assert len(selected)>=5
    dep_files,dep_backup=verified(dependency,selected+['dependency.json'])
    stamp=datetime.now(timezone.utc).strftime('%Y%m%d')
    package=ROOT/'artifacts'/('olive-cuda-'+stamp+'-'+uuid.uuid4().hex[:6]); backup=Path('D:/DNNE-desktop-runs/UnityWorld')/package.name
    assert not package.exists() and not backup.exists(); package.mkdir()
    subprocess.run(['git','archive','--format=zip','--output='+str(package/'source.zip'),revision],cwd=ROOT,check=True)
    with ZipFile(package/'source.zip') as archive:
        assert all(not Path(n).is_absolute() and '..' not in Path(n).parts for n in archive.namelist())
        archive.extractall(package/'source')
    for name in owned: assert digest(package/'source'/name)==digest(cpu/'source'/name),'Qualified CPU source changed: '+name
    for name in names:
        target=package/'cpu-baseline'/name.removeprefix('study/'); target.parent.mkdir(parents=True,exist_ok=True)
        shutil.copy2(cpu/name,target); assert digest(target)==digest(cpu/name)
    for name in selected:
        target=package/'compiler'/name; target.parent.mkdir(parents=True,exist_ok=True); shutil.copy2(dependency/name,target)
        assert digest(target)==dep_files[name]['Sha256']
    shutil.copy2(dependency/'dependency.json',package/'compiler-origin.json')
    (package/'provenance.json').write_text(json.dumps(dict(cpuReview=report,reviewManifestSha256=digest(review/'backup-manifest.json'),
        cpuFiles=[dict(path=n,sha256=digest(cpu/n)) for n in names],cpuSourceUnchanged=True,compilerSource=str(dependency),compilerBackup=str(dep_backup)),indent=2)+'\n',encoding='utf-8')
    shutil.copy2(package/'source/tools/backup-unity-run.ps1',package/'backup-run.ps1')
    files=[dict(Path=p.relative_to(package).as_posix(),Sha256=digest(p)) for p in sorted(package.rglob('*')) if p.is_file()]
    dotnet=shutil.which('dotnet'); assert dotnet
    config=dict(Revision=revision,PreparedUtc=datetime.now(timezone.utc).isoformat(),PackageFiles=files,DotnetExecutable=dotnet,
        PythonExecutable=sys.executable,BackupDestination=str(backup),NoLiveMonitoring=True,ExpectedTestCases=11,
        TestFilter='FullyQualifiedName~OlivaryBatchTests|FullyQualifiedName~SolutionManifestTests',
        TestClasses=['OlivaryBatchTests','SolutionManifestTests'],ExpectedGpuCases=6,ExpectedControlGroups=6,
        CpuBatchReviewed=True,LiveBrainAccessed=False,OldSavesLoaded=False,SpeedupMeasured=False)
    (package/'configuration.json').write_text(json.dumps(config,indent=2)+'\n',encoding='utf-8')
    pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,PreparedOnly=True,NoLiveMonitoring=True,
        Launcher=str(package/'source/tools/start-olive-cuda-window.ps1'))
    (ROOT/'artifacts'/('olive-cuda-prepared-'+stamp+'.json')).write_text(json.dumps(pointer,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(pointer,indent=2))


if __name__=='__main__':main()
