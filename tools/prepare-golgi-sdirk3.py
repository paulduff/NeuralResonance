"""Freeze the third-order long comparison only after its component review passes."""
import argparse
from datetime import datetime, timezone
from hashlib import sha256
import json
from pathlib import Path
import shutil
import subprocess
import uuid
from zipfile import ZipFile
from golgi_sdirk_protocol import protocol

ROOT=Path(__file__).resolve().parents[1]
read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
git=lambda *a:subprocess.check_output(['git',*a],cwd=ROOT,encoding='utf-8').strip()

def digest(path):
    h=sha256()
    with path.open('rb') as stream:
        for block in iter(lambda:stream.read(4*1024*1024),b''):h.update(block)
    return h.hexdigest().upper()

def verify(capture,names):
    manifest=read(capture/'backup-manifest.json');backup=Path(manifest['Destination'])
    assert digest(capture/'backup-manifest.json')==digest(backup/'backup-manifest.json')
    entries={e['Path'].replace('\\','/'):e for e in manifest['Files']}
    for name in names:
        for folder in (capture,backup):assert digest(folder/name)==entries[name]['Sha256'],name

def main(component_review):
    assert not git('status','--porcelain'),'Commit before freezing.'
    revision=git('rev-parse','HEAD');assert revision==git('rev-parse','origin/main'),'Push before freezing.'
    verify(component_review,['review.json']);review=read(component_review/'review.json')
    assert review['operatorReportedCompletion'] and review['componentChecksPassed']
    assert review['componentTestsPassed']==43 and review['solverOrder']==3 and len(review['activeRefinement'])==16
    component=Path(review['capture']);assert digest(component/'backup-manifest.json')==review['captureManifestSha256']
    names=['source/Structures/_SharedRuntime/'+n for n in review['sourceFiles']]
    verify(component,names)
    for name,expected in review['sourceFiles'].items():
        relative='Structures/_SharedRuntime/'+name
        assert digest(component/'source'/relative)==expected
        assert (ROOT/relative).read_text(encoding='utf-8-sig')==(component/'source'/relative).read_text(encoding='utf-8-sig'),name
    previous_review=ROOT/'artifacts/golgi-sdirk-review-20261109';verify(previous_review,['review.json'])
    previous=read(previous_review/'review.json');assert previous['originalAnalysisReproduced'] and previous['all30ConditionsCompleted']
    baseline=Path(previous['capture']);assert digest(baseline/'backup-manifest.json')==previous['manifestSha256']
    selected=[e['Path'].replace('\\','/') for e in read(baseline/'backup-manifest.json')['Files']
        if e['Path'].replace('\\','/').startswith(('reference-baseline/','passive-reference/'))]
    selected.append('reference-preflight.json');assert len(selected)==len(set(selected))==102
    verify(baseline,selected)
    spec=protocol(3);assert len(spec['cases'])==38 and spec['criteria']==protocol(2)['criteria']
    stamp=datetime.now(timezone.utc).strftime('%Y%m%d');package=ROOT/'artifacts'/('golgi-sdirk3-'+stamp+'-'+uuid.uuid4().hex[:6])
    backup=Path('D:/DNNE-desktop-runs/UnityWorld')/package.name
    assert not package.exists() and not backup.exists();package.mkdir()
    subprocess.run(['git','archive','--format=zip','--output='+str(package/'source.zip'),revision],cwd=ROOT,check=True)
    with ZipFile(package/'source.zip') as archive:
        assert all(not Path(n).is_absolute() and '..' not in Path(n).parts for n in archive.namelist());archive.extractall(package/'source')
    copied=[]
    for name in selected:
        destination='preceding-reference-preflight.json' if name=='reference-preflight.json' else name
        target=package/destination;target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(baseline/name,target)
        assert digest(target)==digest(baseline/name);copied.append(dict(source=name,target=destination,sha256=digest(target)))
    (package/'sdirk-input.json').write_text(json.dumps(spec,indent=2)+'\n',encoding='utf-8')
    (package/'provenance.json').write_text(json.dumps(dict(componentReview=review,previousComparisonReview=previous,
        componentReviewManifestSha256=digest(component_review/'backup-manifest.json'),
        previousReviewManifestSha256=digest(previous_review/'backup-manifest.json'),copiedFiles=copied,
        solverMatchesComponents=True,originalCapturesUnchanged=True),indent=2)+'\n',encoding='utf-8')
    shutil.copy2(package/'source/tools/backup-unity-run.ps1',package/'backup-run.ps1')
    files=[dict(Path=p.relative_to(package).as_posix(),Sha256=digest(p)) for p in sorted(package.rglob('*')) if p.is_file()]
    classes=dict(GolgiSdirk3Tests=11,GolgiSdirkTests=7,GolgiReferenceCellTests=8,GolgiChannelKineticsTests=16,SolutionManifestTests=1)
    python=ROOT/'artifacts/numerical-reference-env-20261102/Scripts/python.exe';dotnet=shutil.which('dotnet')
    assert python.is_file() and dotnet
    config=dict(Revision=revision,PreparedUtc=datetime.now(timezone.utc).isoformat(),PackageFiles=files,
        DotnetExecutable=dotnet,PythonExecutable=str(python),BackupDestination=str(backup),NoLiveMonitoring=True,
        SolverOrder=3,ReusePassiveReferences=True,ExpectedTestCases=43,TestClasses=list(classes),TestClassCounts=classes,
        TestFilter='|'.join('FullyQualifiedName~'+n for n in classes),ExpectedConditions=38,
        ComponentsReviewed=True,LiveBrainAccessed=False,OldSavesLoaded=False)
    (package/'configuration.json').write_text(json.dumps(config,indent=2)+'\n',encoding='utf-8')
    pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,PreparedOnly=True,NoLiveMonitoring=True,
        Launcher=str(package/'source/tools/start-golgi-sdirk-window.ps1'))
    (ROOT/'artifacts'/('golgi-sdirk3-prepared-'+stamp+'.json')).write_text(json.dumps(pointer,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(pointer,indent=2))

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--component-review',type=Path,required=True)
    args=parser.parse_args();main(args.component_review.resolve())
