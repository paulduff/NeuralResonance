"""Freeze committed performance tooling and inputs. Does not run a simulation."""
from datetime import datetime, timezone
from hashlib import sha256
import argparse
import json
from pathlib import Path
import shutil
import subprocess
import sys
import uuid
from zipfile import ZipFile

root=Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser();parser.add_argument('--prior-review',type=Path);args=parser.parse_args()
git=lambda *a:subprocess.check_output(['git',*a],cwd=root,encoding='utf-8').strip()
assert not git('status','--porcelain'),'Commit before freezing.'
revision=git('rev-parse','HEAD');assert revision==git('rev-parse','origin/main'),'Push before freezing.'
stamp=datetime.now(timezone.utc).strftime('%Y%m%d')
package=root/'artifacts'/('dnne-compute-'+stamp+'-'+uuid.uuid4().hex[:6])
backup=Path('D:/DNNE-desktop-runs/UnityWorld')/package.name
assert not package.exists() and not backup.exists()
package.mkdir()
subprocess.run(['git','archive','--format=zip','--output='+str(package/'source.zip'),revision],cwd=root,check=True)
with ZipFile(package/'source.zip') as archive:
    assert all(not Path(n).is_absolute() and '..' not in Path(n).parts for n in archive.namelist())
    archive.extractall(package/'source')
shutil.copy2(package/'source/tools/backup-unity-run.ps1',package/'backup-run.ps1')
if args.prior_review is not None:
    reviewed=args.prior_review.resolve();raw=reviewed.read_bytes();review=json.loads(raw)
    assert review['verifiedCopies'] and review['sourceRevision']=='67e7ea55c997276614f88243fe895f14afc48c0e'
    assert review['completedNeuralRuns']==42 and review['exactNeuralObservationPairs']==18
    manifest=json.loads((reviewed.parent/'backup-manifest.json').read_text(encoding='utf-8-sig'))
    assert raw==(Path(manifest['Destination'])/reviewed.name).read_bytes()
    assert sha256(raw).hexdigest().upper()==next(e['Sha256'] for e in manifest['Files'] if e['Path']==reviewed.name)
    (package/'prior-compute-review.json').write_bytes(raw)
files=[dict(Path=p.relative_to(package).as_posix(),Sha256=sha256(p.read_bytes()).hexdigest().upper())
       for p in sorted(package.rglob('*')) if p.is_file()]
dotnet=shutil.which('dotnet');assert dotnet
classes=['BiologicalContactLearningTests','DeclaredSynapticTargetTests','StructurePerformanceEvidenceTests','ComputeStateEvidenceTests','IsometricMotorUnitPlantTests','SolutionManifestTests']
config=dict(Revision=revision,PreparedUtc=datetime.now(timezone.utc).isoformat(),PackageFiles=files,
    DotnetExecutable=dotnet,PythonExecutable=sys.executable,BackupDestination=str(backup),NoLiveMonitoring=True,
    ExpectedTestCases=56,ExpectedNeuralObservationPairs=18,ExpectedMotorSerializationPairs=9,
    TestFilter='|'.join('FullyQualifiedName~'+name for name in classes),
    TestClasses=classes,LiveBrainAccessed=False,OldSavesLoaded=False,GpuMeasured=False,UnityPhysicsMeasured=False)
(package/'configuration.json').write_text(json.dumps(config,indent=2)+'\n',encoding='utf-8')
pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,NoLiveMonitoring=True,
    Launcher=str(package/'source/tools/start-dnne-compute-window.ps1'))
(root/'artifacts'/('dnne-compute-launch-'+stamp+'.json')).write_text(json.dumps(pointer,indent=2)+'\n',encoding='utf-8')
print(json.dumps(pointer,indent=2))
