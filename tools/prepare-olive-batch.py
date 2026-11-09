"""Freeze the isolated CPU batch comparison, without running any calculation."""
from datetime import datetime, timezone
from hashlib import sha256
from io import BytesIO
import json
from pathlib import Path
import shutil
import subprocess
import sys
import uuid
from zipfile import ZipFile

root=Path(__file__).resolve().parents[1]
git=lambda *a:subprocess.check_output(['git',*a],cwd=root,encoding='utf-8').strip()
assert not git('status','--porcelain'),'Commit before freezing.'
revision=git('rev-parse','HEAD');assert revision==git('rev-parse','origin/main'),'Push before freezing.'
reference=Path('Structures/_SharedRuntime/OlivaryMembrane.cs')
# The original equations must still match the reviewed CPU baseline's source.
baseline=root/'artifacts/dnne-compute-20261109-fb9716'
read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
config=read(baseline/'configuration.json')
reference_hash=next(e['Sha256'] for e in config['PackageFiles'] if e['Path']=='source/'+reference.as_posix())
assert sha256((baseline/'source'/reference).read_bytes()).hexdigest().upper()==reference_hash
# Compare archive bytes, not raw Git blob bytes: Windows archive conversion can
# emit CRLF while `git show` returns LF. Keep the captured byte hash gate exact.
reference_archive=subprocess.check_output(['git','archive','--format=zip','HEAD',reference.as_posix()],cwd=root)
with ZipFile(BytesIO(reference_archive)) as archive:current_source=archive.read(reference.as_posix())
assert sha256(current_source).hexdigest().upper()==reference_hash,'Original reference changed.'
stamp=datetime.now(timezone.utc).strftime('%Y%m%d');package=root/'artifacts'/('olive-batch-'+stamp+'-'+uuid.uuid4().hex[:6])
backup=Path('D:/DNNE-desktop-runs/UnityWorld')/package.name
assert not package.exists() and not backup.exists();package.mkdir()
subprocess.run(['git','archive','--format=zip','--output='+str(package/'source.zip'),revision],cwd=root,check=True)
with ZipFile(package/'source.zip') as archive:
    assert all(not Path(n).is_absolute() and '..' not in Path(n).parts for n in archive.namelist())
    archive.extractall(package/'source')
assert sha256((package/'source'/reference).read_bytes()).hexdigest().upper()==reference_hash
shutil.copy2(package/'source/tools/backup-unity-run.ps1',package/'backup-run.ps1')
files=[dict(Path=p.relative_to(package).as_posix(),Sha256=sha256(p.read_bytes()).hexdigest().upper()) for p in sorted(package.rglob('*')) if p.is_file()]
dotnet=shutil.which('dotnet');assert dotnet
classes=['OlivaryBatchTests','MotorAxonReplayBufferTests','MotorAxonTransportTests','MotorUnitCalciumTests','IsometricMotorUnitPlantTests','SolutionManifestTests']
configuration=dict(Revision=revision,PreparedUtc=datetime.now(timezone.utc).isoformat(),PackageFiles=files,
    DotnetExecutable=dotnet,PythonExecutable=sys.executable,BackupDestination=str(backup),NoLiveMonitoring=True,
    ExpectedTestCases=87,ExpectedBatchCases=6,TestFilter='|'.join('FullyQualifiedName~'+name for name in classes),TestClasses=classes,
    OriginalMembraneSha256=reference_hash,OriginalMembraneRevision=config['Revision'],
    LiveBrainAccessed=False,OldSavesLoaded=False,GpuExecuted=False,SpeedupMeasured=False)
(package/'configuration.json').write_text(json.dumps(configuration,indent=2)+'\n',encoding='utf-8')
pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,NoLiveMonitoring=True,
    Launcher=str(package/'source/tools/start-olive-batch-window.ps1'),PreparedOnly=True)
(root/'artifacts'/('olive-batch-prepared-'+stamp+'.json')).write_text(json.dumps(pointer,indent=2)+'\n',encoding='utf-8')
print(json.dumps(pointer,indent=2))
