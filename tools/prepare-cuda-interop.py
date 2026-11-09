"""Freeze source and the verified standalone compiler without executing either."""
from datetime import datetime, timezone
from hashlib import sha256
import json
from pathlib import Path
import shutil
import subprocess
import sys
import uuid
from zipfile import ZipFile

root=Path(__file__).resolve().parents[1]
git=lambda *a:subprocess.check_output(['git',*a],cwd=root,encoding='utf-8').strip()
read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
def digest(path):
    h=sha256()
    with path.open('rb') as stream:
        for block in iter(lambda:stream.read(4*1024*1024),b''):h.update(block)
    return h.hexdigest()
assert not git('status','--porcelain'),'Commit before freezing.'
revision=git('rev-parse','HEAD');assert revision==git('rev-parse','origin/main'),'Push before freezing.'
dependency=root/'artifacts/cuda-nvrtc-13.0.88';origin=read(dependency/'dependency.json');backup_manifest=read(dependency/'backup-manifest.json')
assert origin['archiveSha256']=='8c50a52467826167e0dbe99936140c52d62272bfc5849fe2d6587d050c8c5d29'
assert (dependency/'backup-manifest.json').read_bytes()==(Path(backup_manifest['Destination'])/'backup-manifest.json').read_bytes()
backed={e['Path'].replace('\\','/'):e for e in backup_manifest['Files']}
selected=['nvrtc-13.0.88.zip','redistrib_13.0.2.json',origin['compilerDll'],origin['builtinsDll']]
selected+=sorted(e['path'] for e in origin['files'] if Path(e['path']).name.startswith('LICENSE'))
assert len(selected)>=5
for relative in selected+['dependency.json']:
    entry=backed[relative]
    for folder in (dependency,Path(backup_manifest['Destination'])):
        path=folder/relative
        assert path.stat().st_size==entry['Bytes'] and digest(path).upper()==entry['Sha256']
stamp=datetime.now(timezone.utc).strftime('%Y%m%d')
package=root/'artifacts'/('cuda-interop-'+stamp+'-'+uuid.uuid4().hex[:6]);destination=Path('D:/DNNE-desktop-runs/UnityWorld')/package.name
assert not package.exists() and not destination.exists();package.mkdir()
subprocess.run(['git','archive','--format=zip','--output='+str(package/'source.zip'),revision],cwd=root,check=True)
with ZipFile(package/'source.zip') as archive:
    assert all(not Path(n).is_absolute() and '..' not in Path(n).parts for n in archive.namelist())
    archive.extractall(package/'source')
for relative in selected:
    target=package/'compiler'/relative;target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(dependency/relative,target)
    assert digest(target).upper()==backed[relative]['Sha256']
shutil.copy2(dependency/'dependency.json',package/'compiler-origin.json')
shutil.copy2(package/'source/tools/backup-unity-run.ps1',package/'backup-run.ps1')
files=[dict(Path=p.relative_to(package).as_posix(),Sha256=digest(p).upper()) for p in sorted(package.rglob('*')) if p.is_file()]
dotnet=shutil.which('dotnet');assert dotnet
config=dict(Revision=revision,PreparedUtc=datetime.now(timezone.utc).isoformat(),PackageFiles=files,
    DotnetExecutable=dotnet,PythonExecutable=sys.executable,BackupDestination=str(destination),NoLiveMonitoring=True,
    ExpectedCases=6,CompilerSubset=selected,CompilerSource=str(dependency),CompilerBackup=backup_manifest['Destination'],
    LiveBrainAccessed=False,OldSavesLoaded=False,SpeedupMeasured=False,PhysiologyQualified=False)
(package/'configuration.json').write_text(json.dumps(config,indent=2)+'\n',encoding='utf-8')
pointer=dict(Package=str(package),Backup=str(destination),Revision=revision,NoLiveMonitoring=True,
    Launcher=str(package/'source/tools/start-cuda-interop-window.ps1'),PreparedOnly=True)
(root/'artifacts'/('cuda-interop-prepared-'+stamp+'.json')).write_text(json.dumps(pointer,indent=2)+'\n',encoding='utf-8')
print(json.dumps(pointer,indent=2))
