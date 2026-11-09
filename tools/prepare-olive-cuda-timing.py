"""Freeze a timing-only runner after the independently reviewed GPU comparison."""
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
def digest(p):
    h=sha256()
    with p.open('rb') as stream:
        for block in iter(lambda:stream.read(4*1024*1024),b''): h.update(block)
    return h.hexdigest().upper()
def verified(capture,names):
    manifest=read(capture/'backup-manifest.json'); backup=Path(manifest['Destination'])
    assert digest(capture/'backup-manifest.json')==digest(backup/'backup-manifest.json')
    entries={e['Path'].replace('\\','/'):e for e in manifest['Files']}
    for name in names:
        for folder in (capture,backup):
            assert digest(folder/name)==entries[name]['Sha256'],name
    return backup

def main():
    assert not git('status','--porcelain'),'Commit before freezing.'
    revision=git('rev-parse','HEAD'); assert revision==git('rev-parse','origin/main'),'Push before freezing.'
    review=ROOT/'artifacts/olive-cuda-review-20261109'; verified(review,['review.json'])
    report=read(review/'review.json'); assert report['savedBytesReverified'] and report['analysis']['numericalAgreementPassed']
    assert report['passedFocusedTests']==11 and report['comparedCellFrames']==461250
    capture=Path(report['capture']); assert digest(capture/'backup-manifest.json')==report['manifestSha256']
    names=[p.relative_to(capture).as_posix() for folder in ('cpu-baseline','compiler') for p in sorted((capture/folder).rglob('*')) if p.is_file()]
    names+=['compiler-origin.json','gpu-analysis.json','window-status.json']
    owned=['Structures/_SharedRuntime/OlivaryMembrane.cs',
        'tools/OliveBatchProbe/OlivaryBatchKernel.cs','tools/OliveBatchProbe/OlivaryCpuBatch.cs',
        'tools/CudaInteropProbe/CudaDriverProbe.cs','tools/CudaInteropProbe/NvrtcCompiler.cs',
        'tools/OliveCudaProbe/OlivaryCudaBatch.cs','tools/OliveCudaProbe/CudaDriverProbe.Olive.cs',
        'tools/OliveCudaProbe/CompilerDependency.cs','tools/OliveCudaProbe/BatchControls.cs','tools/OliveCudaProbe/olivary.cu']
    verified(capture,names+['source/'+n for n in owned])
    stamp=datetime.now(timezone.utc).strftime('%Y%m%d'); package=ROOT/'artifacts'/('olive-cuda-timing-'+stamp+'-'+uuid.uuid4().hex[:6])
    backup=Path('D:/DNNE-desktop-runs/UnityWorld')/package.name
    assert not package.exists() and not backup.exists(); package.mkdir()
    subprocess.run(['git','archive','--format=zip','--output='+str(package/'source.zip'),revision],cwd=ROOT,check=True)
    with ZipFile(package/'source.zip') as archive:
        assert all(not Path(n).is_absolute() and '..' not in Path(n).parts for n in archive.namelist()); archive.extractall(package/'source')
    for name in names:
        target=package/name; target.parent.mkdir(parents=True,exist_ok=True); shutil.copy2(capture/name,target)
        assert digest(target)==digest(capture/name)
    # Prior terminal status is evidence, not the status of this new window.
    (package/'window-status.json').rename(package/'prior-window-status.json')
    for name in owned: assert digest(package/'source'/name)==digest(capture/'source'/name),'Reviewed arithmetic/boundary changed: '+name
    (package/'provenance.json').write_text(json.dumps(dict(gpuReview=report,
        reviewManifestSha256=digest(review/'backup-manifest.json'),unchangedSourceFiles=owned,
        copiedFiles=[dict(sourcePath=n,targetPath='prior-window-status.json' if n=='window-status.json' else n,
            sha256=digest(capture/n)) for n in names]),indent=2)+'\n',encoding='utf-8')
    shutil.copy2(package/'source/tools/backup-unity-run.ps1',package/'backup-run.ps1')
    files=[dict(Path=p.relative_to(package).as_posix(),Sha256=digest(p)) for p in sorted(package.rglob('*')) if p.is_file()]
    dotnet=shutil.which('dotnet'); assert dotnet
    config=dict(Revision=revision,PreparedUtc=datetime.now(timezone.utc).isoformat(),PackageFiles=files,
        DotnetExecutable=dotnet,PythonExecutable=sys.executable,BackupDestination=str(backup),NoLiveMonitoring=True,
        GpuComparisonReviewed=True,RequiresOtherStudiesFinished=True,ExpectedTestCases=11,ExpectedCases=12,ExpectedTrials=288,
        TestFilter='FullyQualifiedName~OlivaryBatchTests|FullyQualifiedName~SolutionManifestTests',
        TestClasses=['OlivaryBatchTests','SolutionManifestTests'],LiveBrainAccessed=False,OldSavesLoaded=False)
    (package/'configuration.json').write_text(json.dumps(config,indent=2)+'\n',encoding='utf-8')
    pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,PreparedOnly=True,NoLiveMonitoring=True,
        AwaitingOtherStudiesCompletion=True,Launcher=str(package/'source/tools/start-olive-cuda-timing-window.ps1'))
    (ROOT/'artifacts'/('olive-cuda-timing-prepared-'+stamp+'.json')).write_text(json.dumps(pointer,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(pointer,indent=2))

if __name__=='__main__': main()
