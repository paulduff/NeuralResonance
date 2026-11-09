"""Freeze the full coupled-cell study after a completed, reviewed component window.

Preparation verifies saved evidence only. No solver or test executes here.
"""
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
digest=lambda p:sha256(p.read_bytes()).hexdigest().upper()
git=lambda *a:subprocess.check_output(['git',*a],cwd=ROOT,encoding='utf-8').strip()


def verified_file(capture, name):
    manifest=read(capture/'backup-manifest.json'); backup=Path(manifest['Destination'])
    assert digest(capture/'backup-manifest.json')==digest(backup/'backup-manifest.json')
    entry=next(e for e in manifest['Files'] if e['Path'].replace('\\','/')==name)
    for folder in (capture,backup):
        assert digest(folder/name)==entry['Sha256'],name
    return capture/name


def main(component_review):
    assert not git('status','--porcelain'),'Commit before freezing.'
    revision=git('rev-parse','HEAD'); assert revision==git('rev-parse','origin/main'),'Push before freezing.'
    review=read(verified_file(component_review,'review.json'))
    assert review['operatorReportedCompletion'] and review['componentChecksPassed'] and review['componentTestsPassed']==32
    component=Path(review['capture'])
    assert digest(component/'backup-manifest.json')==review['captureManifestSha256']
    component_config=read(verified_file(component,'configuration.json'))
    assert component_config['Revision']==review['sourceRevision']
    # Require the same solver that passed components. The only permitted addition
    # is the new physical-trace export, itself covered by the repeated tests.
    for name,expected in review['sourceFiles'].items():
        previous=verified_file(component,'source/Structures/_SharedRuntime/'+name)
        assert digest(previous)==expected
        old=previous.read_text(encoding='utf-8-sig')
        current=(ROOT/'Structures/_SharedRuntime'/name).read_text(encoding='utf-8-sig')
        if name=='GolgiSdirkCell.cs':
            begin=current.index('    // Physical trace, not a restart checkpoint:')
            end=current.index('    public GolgiSdirkCell(',begin)
            current=current[:begin]+current[end:]
        assert current==old,'Solver changed after component qualification: '+name
    reference_review=ROOT/'artifacts/golgi-resume-review-20261109'
    reference=read(verified_file(reference_review,'review.json'))
    assert reference['allOriginalReferenceCriteriaPassed'] and reference['recomputedReferenceComparisons']==12
    baseline=Path(reference['capture'])
    baseline_config=read(verified_file(baseline,'configuration.json'))
    assert baseline_config['Revision']==reference['sourceRevision']=='2d86a516046d016c5f8b69eaf37220bb43c0c166'
    assert read(verified_file(baseline,'window-status.json'))['Status']=='DIAGNOSTICS_COMPLETE'
    entries=read(baseline/'backup-manifest.json')['Files']
    selected=[e['Path'].replace('\\','/') for e in entries if e['Path'].replace('\\','/').startswith(('golgi-input/','retained-reference/','reference-source/'))]
    selected+=['study/no-sk-radau-strict.npz','study/new-solve.json','study/analysis.json']
    assert len(selected)==98 and len(set(selected))==98
    for name in selected: verified_file(baseline,name)

    stamp=datetime.now(timezone.utc).strftime('%Y%m%d')
    package=ROOT/'artifacts'/('golgi-sdirk-'+stamp+'-'+uuid.uuid4().hex[:6])
    backup=Path('D:/DNNE-desktop-runs/UnityWorld')/package.name
    assert not package.exists() and not backup.exists(); package.mkdir()
    subprocess.run(['git','archive','--format=zip','--output='+str(package/'source.zip'),revision],cwd=ROOT,check=True)
    with ZipFile(package/'source.zip') as archive:
        assert all(not Path(n).is_absolute() and '..' not in Path(n).parts for n in archive.namelist())
        archive.extractall(package/'source')
    for name in selected:
        target=package/'reference-baseline'/name; target.parent.mkdir(parents=True,exist_ok=True)
        shutil.copy2(baseline/name,target); assert digest(target)==digest(baseline/name)
    (package/'sdirk-input.json').write_text(json.dumps(protocol(),indent=2)+'\n',encoding='utf-8')
    provenance=dict(componentReview=review,referenceReview=reference,
        componentReviewManifestSha256=digest(component_review/'backup-manifest.json'),
        referenceReviewManifestSha256=digest(reference_review/'backup-manifest.json'),
        baselineManifestSha256=digest(baseline/'backup-manifest.json'),
        selectedReferenceFiles=[dict(path=n,sha256=digest(baseline/n)) for n in selected],
        originalCapturesUnchanged=True,solverEquationsMatchComponents=True)
    (package/'provenance.json').write_text(json.dumps(provenance,indent=2)+'\n',encoding='utf-8')
    shutil.copy2(package/'source/tools/backup-unity-run.ps1',package/'backup-run.ps1')
    python=ROOT/'artifacts/numerical-reference-env-20261102/Scripts/python.exe'; dotnet=shutil.which('dotnet')
    assert python.is_file() and dotnet
    classes=['GolgiSdirkTests','GolgiReferenceCellTests','GolgiChannelKineticsTests','SolutionManifestTests']
    files=[dict(Path=p.relative_to(package).as_posix(),Sha256=digest(p)) for p in sorted(package.rglob('*')) if p.is_file()]
    config=dict(Revision=revision,PreparedUtc=datetime.now(timezone.utc).isoformat(),PackageFiles=files,
        DotnetExecutable=dotnet,PythonExecutable=str(python),BackupDestination=str(backup),NoLiveMonitoring=True,
        ExpectedTestCases=32,TestClasses=classes,TestFilter='|'.join('FullyQualifiedName~'+n for n in classes),
        ExpectedConditions=30,ComponentsReviewed=True,LiveBrainAccessed=False,OldSavesLoaded=False)
    (package/'configuration.json').write_text(json.dumps(config,indent=2)+'\n',encoding='utf-8')
    pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,PreparedOnly=True,NoLiveMonitoring=True,
        Launcher=str(package/'source/tools/start-golgi-sdirk-window.ps1'))
    (ROOT/'artifacts'/('golgi-sdirk-prepared-'+stamp+'.json')).write_text(json.dumps(pointer,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(pointer,indent=2))


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--component-review',type=Path,required=True)
    args=parser.parse_args(); main(args.component_review.resolve())
