"""Freeze the cortical fixture diagnosis; no tests, live state or services run."""
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
git=lambda *args:subprocess.check_output(['git',*args],cwd=root,encoding='utf-8').strip()
assert not git('status','--porcelain'),'Commit before preparing.'
revision=git('rev-parse','HEAD');assert revision==git('rev-parse','origin/main'),'Push before preparing.'
stamp=datetime.now(timezone.utc).strftime('%Y%m%d')
package=root/'artifacts'/('cortical-memory-'+stamp+'-'+uuid.uuid4().hex[:6])
backup=Path('D:/DNNE-desktop-runs/UnityWorld')/package.name
assert not package.exists() and not backup.exists()
package.mkdir()
subprocess.run(['git','archive','--format=zip','--output='+str(package/'source.zip'),revision],cwd=root,check=True)
with ZipFile(package/'source.zip') as archive:
    assert all(not Path(n).is_absolute() and '..' not in Path(n).parts for n in archive.namelist())
    archive.extractall(package/'source')
reviewed=root/'artifacts/cortical-memory-review-20261106/review.json'
review=json.loads(reviewed.read_text(encoding='utf-8-sig'))
assert review['sourceRevision']=='2b89c98a5d5e37ea710a618bc9d3a8149cd7503d' and review['observationInvariancePassed']
previous=Path(review['capture']);previous_backup=Path(review['backup'])
baseline=previous/'study/analysis.json';backup_baseline=previous_backup/'study/analysis.json'
assert sha256(baseline.read_bytes()).digest()==sha256(backup_baseline.read_bytes()).digest()
manifest=json.loads((previous/'backup-manifest.json').read_text(encoding='utf-8-sig'))
entry=next(e for e in manifest['Files'] if e['Path'].replace('\\','/')=='study/analysis.json')
assert sha256(baseline.read_bytes()).hexdigest().upper()==entry['Sha256'].upper()
(package/'cortical-baseline').mkdir()
shutil.copy2(baseline,package/'cortical-baseline/analysis.json')
shutil.copy2(reviewed,package/'cortical-baseline/review.json')
shutil.copy2(package/'source/tools/backup-unity-run.ps1',package/'backup-run.ps1')
files=[dict(Path=p.relative_to(package).as_posix(),Sha256=sha256(p.read_bytes()).hexdigest().upper()) for p in sorted(package.rglob('*')) if p.is_file()]
dotnet=shutil.which('dotnet');assert dotnet
config=dict(Revision=revision,PreparedUtc=datetime.now(timezone.utc).isoformat(),PackageFiles=files,
    DotnetExecutable=dotnet,PythonExecutable=sys.executable,BackupDestination=str(backup),NoLiveMonitoring=True,
    LiveBrainAccessed=False,OldSavesLoaded=False,OriginalConsolidationTestUnchanged=True,
    ExpectedTestCases=10,
    TestFilter='FullyQualifiedName~RepeatedCorticalExperienceGraduallyRaisesConsolidation|FullyQualifiedName~SolutionManifestTests|FullyQualifiedName~DeclaredSynapticTargetTests')
(package/'configuration.json').write_text(json.dumps(config,indent=2)+'\n')
pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,NoLiveMonitoring=True,
    Launcher=str(package/'source/tools/start-cortical-memory-window.ps1'))
(root/'artifacts'/('cortical-memory-launch-'+stamp+'.json')).write_text(json.dumps(pointer,indent=2)+'\n')
print(json.dumps(pointer,indent=2))
