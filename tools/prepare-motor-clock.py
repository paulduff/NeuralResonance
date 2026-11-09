"""Freeze the elapsed-time arithmetic diagnosis; no neuron or study executes."""
from datetime import datetime,timezone
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
digest=lambda p:sha256(p.read_bytes()).hexdigest().upper()
git=lambda *a:subprocess.check_output(['git',*a],cwd=ROOT,encoding='utf-8').strip()

def verify(root,names):
    manifest=read(root/'backup-manifest.json');backup=Path(manifest['Destination'])
    assert digest(root/'backup-manifest.json')==digest(backup/'backup-manifest.json')
    entries={e['Path'].replace('\\','/'):e for e in manifest['Files']}
    for name in names:
        for folder in (root,backup):assert digest(folder/name)==entries[name]['Sha256'],name

def main():
    assert not git('status','--porcelain'),'Commit before freezing.'
    revision=git('rev-parse','HEAD');assert revision==git('rev-parse','origin/main'),'Push before freezing.'
    review=ROOT/'artifacts/olive-motor-review-20261109';verify(review,['motor-review.json'])
    result=read(review/'motor-review.json');assert result['recomputedArithmeticPoints']==396 and not result['causalAttributionEstablished']
    assert result['originalNumericalStatus']=='FAILED' and not result['originalBoundsChanged']
    capture=Path(result['capture']);assert digest(capture/'backup-manifest.json')==result['manifestSha256']
    names=['source/tools/MotoneuronReferenceProbe/ActiveStudy.cs',
        'source/Structures/_SharedRuntime/MotoneuronReferenceCell.cs','source/Structures/_SharedRuntime/MotoneuronChannelKinetics.cs']
    verify(capture,names)
    for name in names:assert (capture/name).read_text(encoding='utf-8-sig')==(ROOT/name.removeprefix('source/')).read_text(encoding='utf-8-sig'),name
    stamp=datetime.now(timezone.utc).strftime('%Y%m%d');package=ROOT/'artifacts'/('motor-clock-'+stamp+'-'+uuid.uuid4().hex[:6])
    backup=Path('D:/DNNE-desktop-runs/UnityWorld')/package.name
    assert not package.exists() and not backup.exists();package.mkdir()
    subprocess.run(['git','archive','--format=zip','--output='+str(package/'source.zip'),revision],cwd=ROOT,check=True)
    with ZipFile(package/'source.zip') as archive:
        assert all(not Path(n).is_absolute() and '..' not in Path(n).parts for n in archive.namelist());archive.extractall(package/'source')
    (package/'provenance.json').write_text(json.dumps(dict(fixedStateReview=result,reviewManifestSha256=digest(review/'backup-manifest.json'),
        unchangedSource=[dict(path=n,sha256=digest(capture/n)) for n in names],modelExecuted=False),indent=2)+'\n',encoding='utf-8')
    shutil.copy2(package/'source/tools/backup-unity-run.ps1',package/'backup-run.ps1')
    files=[dict(Path=p.relative_to(package).as_posix(),Sha256=digest(p)) for p in sorted(package.rglob('*')) if p.is_file()]
    dotnet=shutil.which('dotnet');assert dotnet
    config=dict(Revision=revision,OriginalRevision=result['sourceRevision'],PreparedUtc=datetime.now(timezone.utc).isoformat(),
        PackageFiles=files,DotnetExecutable=dotnet,PythonExecutable=sys.executable,BackupDestination=str(backup),
        NoLiveMonitoring=True,OriginalClockSourceReviewed=True,ExpectedCases=6,ModelExecuted=False)
    (package/'configuration.json').write_text(json.dumps(config,indent=2)+'\n',encoding='utf-8')
    pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,PreparedOnly=True,NoLiveMonitoring=True,
        Launcher=str(package/'source/tools/start-motor-clock-window.ps1'))
    (ROOT/'artifacts'/('motor-clock-prepared-'+stamp+'.json')).write_text(json.dumps(pointer,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(pointer,indent=2))

if __name__=='__main__':main()
