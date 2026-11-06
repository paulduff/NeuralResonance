"""Freeze reviewed contact evidence and pushed source; no simulation or live state."""
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
read=lambda path:json.loads(path.read_text(encoding='utf-8-sig'))
digest=lambda raw:sha256(raw).hexdigest().upper()
git=lambda *args:subprocess.check_output(['git',*args],cwd=root,encoding='utf-8').strip()
assert not git('status','--porcelain'),'Commit before freezing.'
revision=git('rev-parse','HEAD');assert revision==git('rev-parse','origin/main'),'Push before freezing.'
reviewed=root/'artifacts/cortical-recipient-review-20261106'
review=read(reviewed/'review.json')
assert review['sourceRevision']=='aeb40be364e2e44dd4d83e47f9a9641d326c6765'
assert review['recipientDeliveryPassed'] and review['previousFiveControlsReproduced'] and review['observationInvariancePassed']
review_manifest=read(reviewed/'backup-manifest.json')
review_bytes=(reviewed/'review.json').read_bytes()
assert digest(review_bytes)==next(e['Sha256'] for e in review_manifest['Files'] if e['Path']=='review.json')
assert review_bytes==(Path(review_manifest['Destination'])/'review.json').read_bytes()
previous=Path(review['capture']);previous_backup=Path(review['backup'])
manifest=read(previous/'backup-manifest.json')
expected={e['Path'].replace('\\','/'):e['Sha256'] for e in manifest['Files']}
def verified(name):
    raw=(previous/name).read_bytes()
    assert digest(raw)==expected[name] and raw==(previous_backup/name).read_bytes(),name
    return raw
unchanged=[]
for filename in ('ContactPlasticity.cs','PlasticityRules.cs','SynapseState.cs','MemoryLearningEvidence.cs'):
    path='Structures/_SharedRuntime/'+filename
    raw=verified('source/'+path)
    blob=subprocess.check_output(['git','show',revision+':'+path],cwd=root)
    # Windows git archive uses CRLF here while git show exposes the LF blob.
    # Check source text now, then require exact archived bytes below as well.
    assert raw.replace(b'\r\n',b'\n')==blob.replace(b'\r\n',b'\n'), 'Learning source changed: '+path
    unchanged.append(dict(path=path,sha256=digest(raw)))
stamp=datetime.now(timezone.utc).strftime('%Y%m%d')
package=root/'artifacts'/('cortical-contact-'+stamp+'-'+uuid.uuid4().hex[:6])
backup=Path('D:/DNNE-desktop-runs/UnityWorld')/package.name
assert not package.exists() and not backup.exists()
package.mkdir()
subprocess.run(['git','archive','--format=zip','--output='+str(package/'source.zip'),revision],cwd=root,check=True)
with ZipFile(package/'source.zip') as archive:
    assert all(not Path(n).is_absolute() and '..' not in Path(n).parts for n in archive.namelist())
    archive.extractall(package/'source')
for source in unchanged:
    assert digest((package/'source'/source['path']).read_bytes())==source['sha256'], 'Archived learning source changed: '+source['path']
target=package/'input';target.mkdir()
(target/'review.json').write_bytes(review_bytes)
cases=[]
for name,count in (('continuous-colocated',654),('continuous-neighbor',145),('continuous-simultaneous',145),('continuous-unmodulated',150)):
    relative='study/'+name+'/learning.jsonl';raw=verified(relative)
    lines=raw.decode('utf-8-sig').splitlines();selected=[];indices=[]
    for index,line in enumerate(lines,1):
        row=json.loads(line)
        if row['before']['sourceNeuron']=='memory-feature-140':
            assert row['before']['target']==row['after']['target']==166
            selected.append(line);indices.append(index)
    assert len(selected)==count
    payload=('\n'.join(selected)+'\n').encode('utf-8')
    (target/(name+'.jsonl')).write_bytes(payload)
    cases.append(dict(key=name,file=name+'.jsonl',sha256=digest(payload),events=count,
        originalFile=relative,originalSha256=digest(raw),originalLineNumbers=indices))
spec=dict(version='cortical-contact-replay-v1',sourceRevision=review['sourceRevision'],cases=cases,
    originalCapture=str(previous),originalBackup=str(previous_backup),unchangedLearningSource=unchanged,
    interventions=['Recorded','NoTagCapture','NoDopamine','NoAcetylcholine'],
    fixedSpikesAndActivity=True,liveBrainAccessed=False,learningRuleRepaired=False,biologicalConsolidationQualified=False)
(target/'input.json').write_text(json.dumps(spec,indent=2)+'\n')
shutil.copy2(package/'source/tools/backup-unity-run.ps1',package/'backup-run.ps1')
files=[dict(Path=p.relative_to(package).as_posix(),Sha256=digest(p.read_bytes())) for p in sorted(package.rglob('*')) if p.is_file()]
dotnet=shutil.which('dotnet');assert dotnet
config=dict(Revision=revision,PreparedUtc=datetime.now(timezone.utc).isoformat(),PackageFiles=files,
    DotnetExecutable=dotnet,PythonExecutable=sys.executable,BackupDestination=str(backup),NoLiveMonitoring=True,
    ExpectedReplayCases=16,ExpectedCadenceCases=27,OriginalConsolidationAssertionUnchanged=True,
    LiveBrainAccessed=False,OldSavesLoaded=False)
(package/'configuration.json').write_text(json.dumps(config,indent=2)+'\n')
pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,NoLiveMonitoring=True,
    Launcher=str(package/'source/tools/start-cortical-contact-window.ps1'))
(root/'artifacts'/('cortical-contact-launch-'+stamp+'.json')).write_text(json.dumps(pointer,indent=2)+'\n')
print(json.dumps(pointer,indent=2))
