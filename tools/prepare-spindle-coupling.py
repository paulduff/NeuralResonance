"""Freeze reviewed inputs and source; no tests or model calculations."""
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

for key in ('OMP_NUM_THREADS','OPENBLAS_NUM_THREADS','MKL_NUM_THREADS','NUMEXPR_NUM_THREADS'):
    os.environ[key]='1'
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'tools'))
from spindle_coupled_population import protocol


def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path)
    result=importlib.util.module_from_spec(spec); spec.loader.exec_module(result); return result


def save(path,value):
    with path.open('x',encoding='utf-8') as stream:
        json.dump(value,stream,indent=2,allow_nan=False); stream.write('\n')


def main():
    h=module('coupling_package',ROOT/'tools/prepare-causal-motor.py')
    assert not h.git('status','--porcelain'),'Commit clean source first.'
    revision=h.git('rev-parse','HEAD'); assert revision==h.git('rev-parse','origin/main'),'Push first.'
    reviews={}; captures={}
    for kind,folder,rev in (
        ('fixed','spindle-fixed-review-20261116','7cce1244227399f598ff4e90e9621d10e7de2414'),
        ('transport','spindle-characteristics-review-20261116','683b0cf0eb4774b935558b557438c78b60add02c')):
        path=ROOT/'artifacts'/folder; h.verify(path,['review.json']); review=h.read(path/'review.json')
        assert review['operatorReportedCompletion'] and review['sourceRevision']==rev
        capture=Path(review['capture']); assert h.digest(capture/'backup-manifest.json')==review['manifestSha256']
        reviews[kind]=(path,review); captures[kind]=capture
    assert reviews['fixed'][1]['savedAnalysisExactlyReproduced'] and reviews['fixed'][1]['capturedFixedLengthAccuracyPassed']
    assert reviews['fixed'][1]['independentReferences']==18 and reviews['fixed'][1]['candidateComparisons']==54
    assert reviews['transport'][1]['savedRecordsReconciled'] and reviews['transport'][1]['analyticControlsPassed']==12
    assert reviews['transport'][1]['historyCases']==144 and reviews['transport'][1]['cadenceComparisons']==96
    fixed=captures['fixed']; retained=captures['transport']
    labels=[f'{f}-{p}-{s}' for f in ('bag','chain') for p in ('quiet-hold','active-hold','drive-on-off')
            for s in ('dop853-strict','dop853-tighter','radau-strict')]
    fixed_files=['references/'+label+'/'+name for label in labels for name in ('trace.npz','result.json')]
    fixed_files+=['reference-preflight.json','reference-postflight.json','spindle-fixed-input.json','spindle-fixed-analysis.json','source/tools/spindle_fixed_reference.py']
    retained_source=h.io(retained/'reference-source')
    retained_files=['reference-source/'+p.relative_to(retained_source).as_posix() for p in sorted(retained_source.rglob('*')) if p.is_file()]
    retained_files+=['reference-preflight.json','reference-postflight.json','source/tools/spindle_characteristic_transport.py']
    h.verify(fixed,fixed_files); h.verify(retained,retained_files)
    for capture,name in ((fixed,'spindle_fixed_reference.py'),(retained,'spindle_characteristic_transport.py')):
        assert (ROOT/'tools'/name).read_text(encoding='utf-8-sig')==h.io(capture/'source/tools'/name).read_text(encoding='utf-8-sig'),name
    env=module('coupling_environment',ROOT/'tools/golgi-reference-resume.py').environment()
    for capture in (fixed,retained):
        assert h.read(capture/'reference-preflight.json')['environment']==env
        post=h.read(capture/'reference-postflight.json'); assert post['unchanged'] and post['environment']==env
    assert h.digest(ROOT/'tools/spindle-reference-LICENSE.txt')==h.digest(retained/'reference-source/upstream/LICENSE')
    package=ROOT/'artifacts'/('spindle-coupling-'+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6])
    backup=Path('D:/DNNE-desktop-runs/UnityWorld')/package.name
    assert not package.exists() and not backup.exists(); package.mkdir()
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
                with target.open('xb') as stream: stream.write(archive.read(entry))
    provenance=[]
    def copy(capture,name,destination):
        target=h.io(package/destination); target.parent.mkdir(parents=True,exist_ok=True)
        shutil.copyfile(h.io(capture/name),target); assert h.digest(target)==h.digest(capture/name)
        provenance.append(dict(capture=str(capture),original=name,path=destination,sha256=h.digest(target)))
    for label in labels:
        copy(fixed,'references/'+label+'/trace.npz','reference/fixed/'+label+'.npz')
        copy(fixed,'references/'+label+'/result.json','reference/fixed/'+label+'.json')
    for name in retained_files:
        if name.startswith('reference-source/'): copy(retained,name,name)
    for kind,capture in captures.items():
        for name in ('reference-preflight.json','reference-postflight.json'):
            copy(capture,name,'reference/'+kind+'-'+name)
        copy(reviews[kind][0],'review.json','reference/'+kind+'-review.json')
    copy(fixed,'spindle-fixed-input.json','reference/fixed-input.json')
    copy(fixed,'spindle-fixed-analysis.json','reference/fixed-analysis.json')
    save(package/'preparation-environment.json',env); save(package/'coupling-input.json',protocol())
    save(package/'provenance.json',dict(copied=provenance,sourceRevision='721ba03cb775261548766ed1349c4bf2e0e35385',
        sourceLicense='AGPL-3.0-only',controlsExecutedDuringPreparation=False,modelsExecutedDuringPreparation=False))
    shutil.copyfile(package/'source/tools/backup-unity-run.ps1',package/'backup-run.ps1')
    frozen=[dict(Path=p.relative_to(h.io(package)).as_posix(),Sha256=h.digest(p)) for p in sorted(h.io(package).rglob('*')) if p.is_file()]
    save(package/'configuration.json',dict(Revision=revision,PreparedUtc=datetime.now(timezone.utc).isoformat(),PackageFiles=frozen,
        PythonExecutable=sys.executable,BackupDestination=str(backup),NoLiveMonitoring=True,IsolatedSpindleCoupling=True,
        ExpectedAnalyticControls=12,ExpectedFixedCases=18,ExpectedFixedComparisons=54,ExpectedMovingCases=36))
    pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,NoLiveMonitoring=True,
        Launcher=str(package/'source/tools/start-spindle-coupling-window.ps1'))
    save(package.parent/(package.name+'-prepared.json'),pointer)
    print(json.dumps(pointer,indent=2),flush=True)


if __name__=='__main__':
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    main()
