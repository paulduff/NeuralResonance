"""Freeze third-order coupled Golgi component checks; no simulation or testing."""
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
digest=lambda p:sha256(p.read_bytes()).hexdigest().upper()

def verify(capture,names):
    manifest=read(capture/'backup-manifest.json');backup=Path(manifest['Destination'])
    assert digest(capture/'backup-manifest.json')==digest(backup/'backup-manifest.json')
    entries={r['Path'].replace('\\','/'):r for r in manifest['Files']}
    for name in names:
        for folder in (capture,backup):assert digest(folder/name)==entries[name]['Sha256'],name

def main():
    assert not git('status','--porcelain'),'Commit before freezing.'
    revision=git('rev-parse','HEAD');assert revision==git('rev-parse','origin/main'),'Push before freezing.'
    review=ROOT/'artifacts/golgi-sdirk-review-20261109';verify(review,['review.json'])
    report=read(review/'review.json');assert report['all30ConditionsCompleted'] and report['originalAnalysisReproduced']
    assert report['originalStatus']=='FAILED' and not report['numericalCriteriaPassed']
    capture=Path(report['capture']);assert digest(capture/'backup-manifest.json')==report['manifestSha256']
    unchanged=['Structures/_SharedRuntime/'+n+'.cs' for n in ('GolgiCable','GolgiChannelKinetics','GolgiReferenceCell')]
    verify(capture,['source/'+p for p in unchanged]+['source/Structures/_SharedRuntime/GolgiSdirkCell.cs'])
    for name in unchanged:assert (ROOT/name).read_text()==(capture/'source'/name).read_text(),name
    suffix=lambda p:p.read_text().split('    private static void Validate(State state)',1)[1]
    assert suffix(ROOT/'Structures/_SharedRuntime/GolgiSdirkCell.cs')==suffix(capture/'source/Structures/_SharedRuntime/GolgiSdirkCell.cs')
    stamp=datetime.now(timezone.utc).strftime('%Y%m%d')
    package=ROOT/'artifacts'/('golgi-sdirk3-components-'+stamp+'-'+uuid.uuid4().hex[:6])
    backup=Path('D:/DNNE-desktop-runs/UnityWorld')/package.name
    assert not package.exists() and not backup.exists();package.mkdir()
    subprocess.run(['git','archive','--format=zip','--output='+str(package/'source.zip'),revision],cwd=ROOT,check=True)
    with ZipFile(package/'source.zip') as archive:
        assert all(not Path(n).is_absolute() and '..' not in Path(n).parts for n in archive.namelist());archive.extractall(package/'source')
    shutil.copy2(review/'review.json',package/'preceding-comparison-review.json')
    shutil.copy2(package/'source/tools/backup-unity-run.ps1',package/'backup-run.ps1')
    (package/'provenance.json').write_text(json.dumps(dict(previousFailedCapture=str(capture),
        previousManifestSha256=report['manifestSha256'],reviewManifestSha256=digest(review/'backup-manifest.json'),
        unchangedBiologicalFiles=unchanged,nonlinearStageAndBiologicalEquationsUnchanged=True),indent=2)+'\n',encoding='utf-8')
    files=[dict(Path=p.relative_to(package).as_posix(),Sha256=digest(p)) for p in sorted(package.rglob('*')) if p.is_file()]
    classes=dict(GolgiSdirk3Tests=11,GolgiSdirkTests=7,GolgiReferenceCellTests=8,GolgiChannelKineticsTests=16,SolutionManifestTests=1)
    dotnet=shutil.which('dotnet');assert dotnet
    configuration=dict(Revision=revision,PreparedUtc=datetime.now(timezone.utc).isoformat(),PackageFiles=files,
        DotnetExecutable=dotnet,PythonExecutable=sys.executable,BackupDestination=str(backup),NoLiveMonitoring=True,
        SolverOrder=3,ExpectedTestCases=sum(classes.values()),TestFilter='|'.join('FullyQualifiedName~'+name for name in classes),
        TestClasses=list(classes),TestClassCounts=classes,LiveBrainAccessed=False,OldSavesLoaded=False,GpuExecuted=False,SpeedupMeasured=False)
    (package/'configuration.json').write_text(json.dumps(configuration,indent=2)+'\n',encoding='utf-8')
    pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,NoLiveMonitoring=True,
        Launcher=str(package/'source/tools/start-golgi-sdirk-components-window.ps1'),PreparedOnly=True)
    (ROOT/'artifacts'/('golgi-sdirk3-components-prepared-'+stamp+'.json')).write_text(json.dumps(pointer,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(pointer,indent=2))

if __name__=='__main__':main()
