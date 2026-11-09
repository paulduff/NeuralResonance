"""Freeze cost attribution after the reviewed CPU/CUDA timing outcome; do not launch."""
from datetime import datetime, timezone
from hashlib import sha256
import json
from pathlib import Path
import shutil
import subprocess
import sys
import uuid
from zipfile import ZipFile
from olive_cost_protocol import protocol,schedule

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

def remove_cost_hooks(text,name):
    # An explicit source equivalence check, not a general C# rewrite. Removing
    # declared optional partial hooks must reproduce the previously timed source.
    text='\n'.join(line for line in text.splitlines() if not line.endswith('// COST_HOOK'))+'\n'
    if name.endswith('/OlivaryCudaBatch.cs'):
        text=text.replace('internal sealed partial class OlivaryCudaBatch','internal sealed class OlivaryCudaBatch')
        text=text.replace('var raw = _cuda.Download(_reportBuffer);\n        CheckGuards(raw);',
            'var raw = _cuda.Download(_reportBuffer); CheckGuards(raw);')
        # The hook declarations add one empty line before Dispose.
        text=text.replace('    }\n\n\n    public void Dispose()', '    }\n\n    public void Dispose()')
    return text

def source_review(capture):
    owned=['Structures/_SharedRuntime/OlivaryMembrane.cs',
        'tools/OliveBatchProbe/OlivaryBatchKernel.cs','tools/OliveBatchProbe/OlivaryCpuBatch.cs',
        'tools/CudaInteropProbe/CudaDriverProbe.cs','tools/OliveCudaProbe/OlivaryCudaBatch.cs',
        'tools/OliveCudaProbe/CudaDriverProbe.Olive.cs','tools/OliveCudaProbe/BatchControls.cs',
        'tools/OliveCudaProbe/olivary.cu','tools/OliveCudaTimingProbe/CudaDriverProbe.Memory.cs']
    verify(capture,['source/'+n for n in owned]);rows=[]
    for name in owned:
        old=(capture/'source'/name).read_text(encoding='utf-8-sig');current=(ROOT/name).read_text(encoding='utf-8-sig')
        hook_only=name.endswith(('/OlivaryCudaBatch.cs','/CudaDriverProbe.Olive.cs'))
        assert (remove_cost_hooks(current,name) if hook_only else current)==old,'Reviewed execution changed: '+name
        rows.append(dict(path=name,previousSha256=digest(capture/'source'/name),currentSha256=digest(ROOT/name),onlyOptionalCostHooksAdded=hook_only))
    return rows

def main():
    assert not git('status','--porcelain'),'Commit before freezing.'
    revision=git('rev-parse','HEAD');assert revision==git('rev-parse','origin/main'),'Push before freezing.'
    review=ROOT/'artifacts/olive-timing-review-20261109';verify(review,['review.json'])
    report=read(review/'review.json')
    assert report['operatorReportedCompletion'] and report['completeAnalysisReproduced'] and report['accuracyPassed']
    assert (report['passedFocusedTests'],report['measuredTrials'],report['warmupTrials'])==(11,240,48)
    capture=Path(report['capture']);assert digest(capture/'backup-manifest.json')==report['manifestSha256']
    sources=source_review(capture)
    names=[p.relative_to(capture).as_posix() for step in (.025,.00625)
        for p in sorted((capture/f'cpu-baseline/cells-192-h-{step}').rglob('*')) if p.is_file()]
    names+=['study/'+n for n in ('device.json','compiler.json','olivary.ptx','olivary.cu')]
    verify(capture,names)
    stamp=datetime.now(timezone.utc).strftime('%Y%m%d');package=ROOT/'artifacts'/('olive-cuda-cost-'+stamp+'-'+uuid.uuid4().hex[:6])
    backup=Path('D:/DNNE-desktop-runs/UnityWorld')/package.name
    assert not package.exists() and not backup.exists();package.mkdir()
    subprocess.run(['git','archive','--format=zip','--output='+str(package/'source.zip'),revision],cwd=ROOT,check=True)
    with ZipFile(package/'source.zip') as archive:
        assert all(not Path(n).is_absolute() and '..' not in Path(n).parts for n in archive.namelist());archive.extractall(package/'source')
    copied=[]
    for name in names:
        destination=name.replace('study/','timing-evidence/',1) if name.startswith('study/') else name
        target=package/destination;target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(capture/name,target)
        assert digest(target)==digest(capture/name);copied.append(dict(source=name,target=destination,sha256=digest(target)))
    for name,value in [('cost-protocol.json',protocol()),('schedule.json',schedule()),
        ('provenance.json',dict(timingReview=report,reviewManifestSha256=digest(review/'backup-manifest.json'),sources=sources,copiedFiles=copied))]:
        (package/name).write_text(json.dumps(value,indent=2)+'\n',encoding='utf-8')
    shutil.copy2(package/'source/tools/backup-unity-run.ps1',package/'backup-run.ps1')
    files=[dict(Path=p.relative_to(package).as_posix(),Sha256=digest(p)) for p in sorted(package.rglob('*')) if p.is_file()]
    dotnet=shutil.which('dotnet');assert dotnet
    config=dict(Revision=revision,PreparedUtc=datetime.now(timezone.utc).isoformat(),PackageFiles=files,
        DotnetExecutable=dotnet,PythonExecutable=sys.executable,BackupDestination=str(backup),NoLiveMonitoring=True,
        TimingReviewed=True,RequiresOtherStudiesFinished=True,ExpectedTestCases=11,ExpectedCases=6,ExpectedProcesses=90,ExpectedTrials=180,
        TestFilter='FullyQualifiedName~OlivaryBatchTests|FullyQualifiedName~SolutionManifestTests',
        TestClasses=['OlivaryBatchTests','SolutionManifestTests'],LiveBrainAccessed=False,OldSavesLoaded=False)
    (package/'configuration.json').write_text(json.dumps(config,indent=2)+'\n',encoding='utf-8')
    pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,PreparedOnly=True,NoLiveMonitoring=True,
        AwaitingOtherStudiesCompletion=True,Launcher=str(package/'source/tools/start-olive-cuda-cost-window.ps1'))
    (ROOT/'artifacts'/('olive-cuda-cost-prepared-'+stamp+'.json')).write_text(json.dumps(pointer,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(pointer,indent=2))

if __name__=='__main__':main()
