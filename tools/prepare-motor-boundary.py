"""Freeze a scheduling-only neuronal comparison after a verified clock outcome."""
import argparse
from datetime import datetime,timezone
from hashlib import sha256
import json
from pathlib import Path
import shutil
import subprocess
import uuid
from zipfile import ZipFile
from motor_boundary_protocol import protocol,original_protocol

ROOT=Path(__file__).resolve().parents[1]
read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
git=lambda *a:subprocess.check_output(['git',*a],cwd=ROOT,encoding='utf-8').strip()
def digest(path):
    h=sha256()
    with path.open('rb') as stream:
        for block in iter(lambda:stream.read(4*1024*1024),b''):h.update(block)
    return h.hexdigest().upper()

def verify(root,names):
    manifest=read(root/'backup-manifest.json');backup=Path(manifest['Destination'])
    assert digest(root/'backup-manifest.json')==digest(backup/'backup-manifest.json')
    entries={e['Path'].replace('\\','/'):e for e in manifest['Files']}
    for name in names:
        for folder in (root,backup):assert digest(folder/name)==entries[name]['Sha256'],name
    return entries

def main(clock_review):
    assert not git('status','--porcelain'),'Commit before freezing.'
    revision=git('rev-parse','HEAD');assert revision==git('rev-parse','origin/main'),'Push before freezing.'
    verify(clock_review,['review.json']);clock=read(clock_review/'review.json')
    assert clock['operatorReportedCompletion'] and clock['savedRowAnalysisReproduced']
    assert clock['analysis']['originalClockMismatchReproduced'] and clock['analysis']['endpointDifferenceConservesRepresentedTime']
    clock_capture=Path(clock['capture']);assert digest(clock_capture/'backup-manifest.json')==clock['manifestSha256']
    same=['Structures/_SharedRuntime/MotoneuronReferenceCell.cs','Structures/_SharedRuntime/MotoneuronChannelKinetics.cs']
    verify(clock_capture,['source/'+n for n in same]+['source/tools/MotoneuronReferenceProbe/ClockAudit.cs'])
    for name in same:assert (ROOT/name).read_text(encoding='utf-8-sig')==(clock_capture/'source'/name).read_text(encoding='utf-8-sig'),name
    current=(ROOT/'tools/MotoneuronReferenceProbe/ClockAudit.cs').read_text(encoding='utf-8-sig')
    for signature in ('Int128 Units','double BoundaryDuration'):current=current.replace('internal static '+signature,'private static '+signature)
    assert current==(clock_capture/'source/tools/MotoneuronReferenceProbe/ClockAudit.cs').read_text(encoding='utf-8-sig'),'Clock arithmetic changed after its qualification.'
    review=ROOT/'artifacts/compute-cell-review-20261109';verify(review,['motor-review.json']);old_review=read(review/'motor-review.json')
    assert old_review['originalStatus']=='FAILED' and old_review['passedScenarios']==8 and old_review['biologicalInputsAndOriginalBoundsUnchanged']
    original=Path(old_review['capture']);entries=read(original/'backup-manifest.json')['Files']
    selected={}
    for name in ('protocol.json','results.json'):selected['motoneuron-active-study/'+name]='baseline/'+name
    selected['motoneuron-active-reference/comparison.json']='baseline/comparison.json'
    for name in ('motoneuron-active-reference.py','motoneuron-reference-check.py','motoneuron_active_protocol.py','motoneuron_bounded_jacobian.py'):
        selected['source/tools/'+name]='reference-source/'+name
        assert (ROOT/'tools'/name).read_text(encoding='utf-8-sig')==(original/'source/tools'/name).read_text(encoding='utf-8-sig'),name
    for entry in entries:
        name=entry['Path'].replace('\\','/')
        if name.startswith('motoneuron-active-reference/') and name.endswith('.npz'):selected[name]='references/'+Path(name).name
    trace_names=['motoneuron-active-study/'+s['key']+'-h'+str(h)+'.csv' for s in original_protocol()['cases'] for h in original_protocol()['stepsMs']]
    verified=verify(original,list(selected)+trace_names+['motoneuron-active-input.json'])
    assert read(original/'motoneuron-active-input.json')==original_protocol()
    assert len([n for n in selected if n.endswith('.npz')])==30 and len(trace_names)==30
    stamp=datetime.now(timezone.utc).strftime('%Y%m%d');package=ROOT/'artifacts'/('motor-boundary-'+stamp+'-'+uuid.uuid4().hex[:6])
    backup=Path('D:/DNNE-desktop-runs/UnityWorld')/package.name
    assert not package.exists() and not backup.exists();package.mkdir()
    subprocess.run(['git','archive','--format=zip','--output='+str(package/'source.zip'),revision],cwd=ROOT,check=True)
    with ZipFile(package/'source.zip') as archive:
        assert all(not Path(n).is_absolute() and '..' not in Path(n).parts for n in archive.namelist());archive.extractall(package/'source')
    copied=[]
    for source,target in selected.items():
        destination=package/target;destination.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(original/source,destination)
        assert digest(destination)==verified[source]['Sha256'];copied.append(dict(source=source,target=target,sha256=digest(destination)))
    trace_hashes={Path(n).stem:verified[n]['Sha256'] for n in trace_names}
    values=[('original-input.json',original_protocol()),('boundary-input.json',protocol()),('baseline/control-trace-hashes.json',trace_hashes),
        ('provenance.json',dict(clockReview=clock,originalReview=old_review,clockReviewManifestSha256=digest(clock_review/'backup-manifest.json'),
            originalReviewManifestSha256=digest(review/'backup-manifest.json'),originalManifestSha256=digest(original/'backup-manifest.json'),
            copiedFiles=copied,controlTraceHashesVerifiedOnBothCopies=True,neuronalEquationsUnchanged=True,clockFunctionsUnchanged=True))]
    for name,value in values:(package/name).write_text(json.dumps(value,indent=2)+'\n',encoding='utf-8')
    shutil.copy2(package/'source/tools/backup-unity-run.ps1',package/'backup-run.ps1')
    files=[dict(Path=p.relative_to(package).as_posix(),Sha256=digest(p)) for p in sorted(package.rglob('*')) if p.is_file()]
    dotnet=shutil.which('dotnet');python=ROOT/'artifacts/numerical-reference-env-20261102/Scripts/python.exe';assert dotnet and python.is_file()
    config=dict(Revision=revision,PreparedUtc=datetime.now(timezone.utc).isoformat(),PackageFiles=files,DotnetExecutable=dotnet,
        PythonExecutable=str(python),BackupDestination=str(backup),NoLiveMonitoring=True,ClockReviewed=True,
        RequiresCostStudyFinished=True,ExpectedTestCases=12,ExpectedCandidateConditions=30,ExpectedControlConditions=30,
        TestFilter='FullyQualifiedName~MotoneuronReferenceTests|FullyQualifiedName~SolutionManifestTests',
        TestClasses=['MotoneuronReferenceTests','SolutionManifestTests'])
    (package/'configuration.json').write_text(json.dumps(config,indent=2)+'\n',encoding='utf-8')
    pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,PreparedOnly=True,NoLiveMonitoring=True,
        AwaitingCostStudyCompletion=True,Launcher=str(package/'source/tools/start-motor-boundary-window.ps1'))
    (ROOT/'artifacts'/('motor-boundary-prepared-'+stamp+'.json')).write_text(json.dumps(pointer,indent=2)+'\n',encoding='utf-8');print(json.dumps(pointer,indent=2))

if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--clock-review',type=Path,required=True);args=parser.parse_args();main(args.clock_review.resolve())
