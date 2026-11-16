"""Freeze completed spindle inputs and an unexecuted transport comparison."""
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

for name in ('OMP_NUM_THREADS','OPENBLAS_NUM_THREADS','MKL_NUM_THREADS','NUMEXPR_NUM_THREADS'):
    os.environ[name]='1'
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'tools'))
from spindle_population_transport import protocol
from spindle_characteristic_transport import protocol as characteristic_protocol


def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path)
    value=importlib.util.module_from_spec(spec);spec.loader.exec_module(value);return value


def save(path,value):
    with path.open('x',encoding='utf-8') as stream:
        json.dump(value,stream,indent=2,allow_nan=False);stream.write('\n')


def main():
    h=module('transport_packaging',ROOT/'tools/prepare-causal-motor.py')
    assert not h.git('status','--porcelain'),'Commit clean source first.'
    revision=h.git('rev-parse','HEAD');assert revision==h.git('rev-parse','origin/main'),'Push first.'
    reviewed=ROOT/'artifacts/spindle-slack-v2-review-20261116'
    h.verify(reviewed,['review.json']);review=h.read(reviewed/'review.json')
    assert review['operatorReportedCompletion'] and review['forceLandscapeNumericsPassed'] and review['savedRecordsReconciled']
    assert review['sourceRevision']=='e09d37db87b91a0e8df43f6db4fd66124be9228c'
    assert review['analyticControlsPassed']==8 and review['savedStateQueries']==2812 and review['noRootStates']==31
    capture=Path(review['capture']);assert h.digest(capture/'backup-manifest.json')==review['manifestSha256']
    transport_folder=ROOT/'artifacts/spindle-transport-review-20261116'
    h.verify(transport_folder,['review.json']);transport_review=h.read(transport_folder/'review.json')
    assert transport_review['operatorReportedCompletion'] and transport_review['savedRecordsReconciled']
    assert transport_review['savedArrayComparisons']==25308 and transport_review['historyCases']==144
    assert transport_review['sourceRevision']=='6c7788c6f6f4e2734702069486bf0d2704c5b20a'
    assert h.digest(Path(transport_review['capture'])/'backup-manifest.json')==transport_review['manifestSha256']
    transport_capture=Path(transport_review['capture'])
    unchanged=['tools/spindle_population_transport.py','tools/analyse-spindle-population-transport.py']
    h.verify(transport_capture,['source/'+name for name in unchanged])
    for name in unchanged:
        assert (ROOT/name).read_text(encoding='utf-8-sig')==h.io(transport_capture/'source'/name).read_text(encoding='utf-8-sig'),name
    # Retain the completed projection-diffusion diagnosis as explicit provenance.

    labels=[f'{f}-{p}-{s}us' for f in ('bag','chain') for p in ('stretch-shorten','imposed-below-slack') for s in (1000,500,250)]
    files=['native/'+label+'.mat' for label in labels]
    reference_root=h.io(capture/'reference-source')
    files+=['reference-source/'+p.relative_to(reference_root).as_posix() for p in sorted(reference_root.rglob('*')) if p.is_file()]
    files+=['spindle-slack-input.json','reference-preflight.json','reference-postflight.json']
    h.verify(capture,files)
    assert h.digest(ROOT/'tools/spindle-reference-LICENSE.txt')==h.digest(capture/'reference-source/upstream/LICENSE')
    env=module('transport_environment',ROOT/'tools/golgi-reference-resume.py').environment()
    assert env==h.read(capture/'reference-preflight.json')['environment']
    assert h.read(capture/'reference-postflight.json')==dict(environment=env,unchanged=True,modelExecuted=False)
    package=ROOT/'artifacts'/('spindle-characteristics-'+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6])
    backup=Path('D:/DNNE-desktop-runs/UnityWorld')/package.name
    assert not package.exists() and not backup.exists();package.mkdir()
    subprocess.run(['git','archive','--format=zip','--output='+str(package/'source.zip'),revision],cwd=ROOT,check=True)
    with ZipFile(package/'source.zip') as archive:
        for entry in archive.infolist():
            path=PurePosixPath(entry.filename)
            assert not path.is_absolute() and '..' not in path.parts and ':' not in entry.filename and '\\' not in entry.filename
            assert ((entry.external_attr>>16)&0o170000)!=0o120000
            target=h.io(package/'source'/path)
            if entry.is_dir():target.mkdir(parents=True,exist_ok=True)
            else:
                target.parent.mkdir(parents=True,exist_ok=True)
                with target.open('xb') as stream:stream.write(archive.read(entry))
    provenance=[]
    for name in files:
        relative=name if name.startswith(('native/','reference-source/')) else 'reference/previous-'+name
        target=h.io(package/relative);target.parent.mkdir(parents=True,exist_ok=True)
        shutil.copyfile(h.io(capture/name),target);assert h.digest(target)==h.digest(capture/name)
        provenance.append(dict(original=name,path=relative,sha256=h.digest(target)))
    shutil.copyfile(reviewed/'review.json',package/'reference/review.json')
    save(package/'preparation-environment.json',env)
    save(package/'transport-input.json',protocol())
    save(package/'characteristic-input.json',characteristic_protocol())
    shutil.copyfile(transport_folder/'review.json',package/'reference/transport-review.json')
    save(package/'provenance.json',dict(capture=str(capture),manifestSha256=review['manifestSha256'],
        copied=provenance,transportReview=transport_review,sourceRevision='721ba03cb775261548766ed1349c4bf2e0e35385',sourceLicense='AGPL-3.0-only',
        controlsExecutedDuringPreparation=False,modelsExecutedDuringPreparation=False))
    # Parse immutable MAT observations only; no candidate/reference force or transport evaluation.
    parser=module('transport_input_parser',ROOT/'tools/analyse-spindle-population-transport.py')
    parsed,_=parser.inputs(package)
    save(package/'preparation-input-check.json',dict(capturedStates=sum(len(r['states']) for r in parsed),
        trajectories=[dict(label=r['label'],states=len(r['states'])) for r in parsed],modelExecuted=False))
    shutil.copyfile(package/'source/tools/backup-unity-run.ps1',package/'backup-run.ps1')
    frozen=[dict(Path=p.relative_to(h.io(package)).as_posix(),Sha256=h.digest(p)) for p in sorted(h.io(package).rglob('*')) if p.is_file()]
    configuration=dict(Revision=revision,PreparedUtc=datetime.now(timezone.utc).isoformat(),PackageFiles=frozen,
        PythonExecutable=sys.executable,BackupDestination=str(backup),NoLiveMonitoring=True,IsolatedSpindleCharacteristics=True,
        ExpectedAnalyticControls=12,ExpectedStateQueries=25308,ExpectedHistoryCases=144,ExpectedCadenceComparisons=96,
        ExpectedContinuityProbes=sum(int(b['signChangeAcrossJump']) for e in review['noRootEvidence'] for b in e['discontinuities']))
    save(package/'configuration.json',configuration)
    pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,NoLiveMonitoring=True,
        Launcher=str(package/'source/tools/start-spindle-characteristics-window.ps1'))
    save(package.parent/(package.name+'-prepared.json'),pointer)
    print(json.dumps(pointer,indent=2),flush=True)


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    main()
