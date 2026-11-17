"""Freeze completed spindle evidence and new reference source; no numerical execution."""
from datetime import datetime, timezone
import importlib.util
import json
import os
from pathlib import Path, PurePosixPath
import shutil
import subprocess
import sys
import uuid
from zipfile import ZipFile

for key in ('OMP_NUM_THREADS','OPENBLAS_NUM_THREADS','MKL_NUM_THREADS','NUMEXPR_NUM_THREADS'): os.environ[key]='1'
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'tools'))
from spindle_eulerian_reference import protocol


def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path)
    result=importlib.util.module_from_spec(spec);spec.loader.exec_module(result);return result


def save(path,value):
    with path.open('x',encoding='utf-8') as stream:
        json.dump(value,stream,indent=2,allow_nan=False);stream.write('\n')


def main():
    h=module('eulerian_package_helpers',ROOT/'tools/prepare-causal-motor.py')
    assert not h.git('status','--porcelain'),'Commit clean source first.'
    revision=h.git('rev-parse','HEAD');assert revision==h.git('rev-parse','origin/main'),'Push first.'
    review_folder=ROOT/'artifacts/spindle-coupling-review-20261117'
    h.verify(review_folder,['review.json']);review=h.read(review_folder/'review.json')
    assert review['operatorReportedCompletion'] and review['sourceRevision']=='0398904f04e58ec99e588cf05b841f799eea32ba'
    assert review['savedRecordsReconciled'] and review['analyticControlsPassed']==12 and review['movingCases']==36
    capture=Path(review['capture']);assert h.digest(capture/'backup-manifest.json')==review['manifestSha256']
    source_root=h.io(capture/'reference-source')
    source_names=['reference-source/'+p.relative_to(source_root).as_posix() for p in sorted(source_root.rglob('*')) if p.is_file()]
    fixed_names=['reference/fixed/'+f'{f}-{p}-{s}'+ext for f in ('bag','chain')
        for p in ('quiet-hold','active-hold','drive-on-off') for s in ('dop853-strict','dop853-tighter','radau-strict') for ext in ('.npz','.json')]
    moving_names=['moving/'+f'{f}-{p}-range20.25-{step}us'+ext for f in ('bag','chain')
        for p in ('stretch','stretch-return','shorten-return') for step in (1000,500,250) for ext in ('.npz','.json','-summary.json')]
    names=source_names+fixed_names+moving_names+['reference-preflight.json','reference-postflight.json','reference/fixed-review.json',
        'source/tools/spindle_coupled_population.py','source/tools/analyse-spindle-coupling.py','coupling-input.json','moving-analysis.json']
    h.verify(capture,names)
    for name in ('spindle_coupled_population.py','analyse-spindle-coupling.py'):
        assert (ROOT/'tools'/name).read_text(encoding='utf-8-sig')==h.io(capture/'source/tools'/name).read_text(encoding='utf-8-sig')
    fixed_review=h.read(capture/'reference/fixed-review.json');fixed_capture=Path(fixed_review['capture'])
    assert fixed_review['operatorReportedCompletion'] and fixed_review['capturedFixedLengthAccuracyPassed']
    assert h.digest(fixed_capture/'backup-manifest.json')==fixed_review['manifestSha256']
    constructor_names=[f'native/{f}-active-hold-250us.mat' for f in ('bag','chain')]
    h.verify(fixed_capture,constructor_names)
    environment=module('eulerian_environment',ROOT/'tools/golgi-reference-resume.py').environment()
    assert environment==h.read(capture/'reference-preflight.json')['environment']
    assert h.read(capture/'reference-postflight.json')['unchanged'] and environment==h.read(capture/'reference-postflight.json')['environment']
    assert h.digest(ROOT/'tools/spindle-reference-LICENSE.txt')==h.digest(capture/'reference-source/upstream/LICENSE')
    package=ROOT/'artifacts'/('spindle-eulerian-'+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6])
    backup=Path('D:/DNNE-desktop-runs/UnityWorld')/package.name
    assert not package.exists() and not backup.exists();package.mkdir()
    subprocess.run(['git','archive','--format=zip','--output='+str(package/'source.zip'),revision],cwd=ROOT,check=True)
    with ZipFile(package/'source.zip') as archive:
        for entry in archive.infolist():
            path=PurePosixPath(entry.filename)
            assert not path.is_absolute() and '..' not in path.parts and ':' not in entry.filename and '\\' not in entry.filename
            assert ((entry.external_attr>>16)&0o170000)!=0o120000
            target=h.io(package/'source'/path)
            if entry.is_dir(): target.mkdir(parents=True,exist_ok=True)
            else:
                target.parent.mkdir(parents=True,exist_ok=True)
                with target.open('xb') as stream:stream.write(archive.read(entry))
    copied=[]
    def copy(source,name,destination):
        target=h.io(package/destination);target.parent.mkdir(parents=True,exist_ok=True)
        shutil.copyfile(h.io(source/name),target);assert h.digest(target)==h.digest(source/name)
        copied.append(dict(capture=str(source),original=name,path=destination,sha256=h.digest(target)))
    for name in source_names+fixed_names: copy(capture,name,name)
    for name in moving_names: copy(capture,name,'reference/'+name)
    for fiber,name in zip(('bag','chain'),constructor_names):copy(fixed_capture,name,f'reference/constructor/{fiber}.mat')
    for name in ('reference-preflight.json','reference-postflight.json','coupling-input.json','moving-analysis.json'):
        copy(capture,name,'reference/coupling-'+name)
    copy(review_folder,'review.json','reference/coupling-review.json')
    copy(capture,'reference/fixed-review.json','reference/fixed-review.json')
    save(package/'preparation-environment.json',environment);save(package/'eulerian-input.json',protocol())
    save(package/'provenance.json',dict(copied=copied,sourceRevision='721ba03cb775261548766ed1349c4bf2e0e35385',
        license='AGPL-3.0-only',candidateSourcePreserved=True,testsExecutedDuringPreparation=False,modelsExecutedDuringPreparation=False))
    shutil.copyfile(package/'source/tools/backup-unity-run.ps1',package/'backup-run.ps1')
    files=[dict(Path=p.relative_to(h.io(package)).as_posix(),Sha256=h.digest(p)) for p in sorted(h.io(package).rglob('*')) if p.is_file()]
    save(package/'configuration.json',dict(Revision=revision,PreparedUtc=datetime.now(timezone.utc).isoformat(),PackageFiles=files,
        PythonExecutable=sys.executable,PythonExecutableSha256=h.digest(sys.executable),BackupDestination=str(backup),
        NoLiveMonitoring=True,IsolatedEulerianSpindle=True,ExpectedAnalyticControls=12,ExpectedFixedCases=6,
        ExpectedFixedContrasts=18,ExpectedMovingSolves=42,ExpectedTemporalContrasts=12,ExpectedSpatialContrasts=18,
        ExpectedRangeContrasts=6,ExpectedCandidateContrasts=18))
    pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,NoLiveMonitoring=True,
        Launcher=str(package/'source/tools/start-spindle-eulerian-window.ps1'))
    save(package.parent/(package.name+'-prepared.json'),pointer);print(json.dumps(pointer,indent=2),flush=True)


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    main()
