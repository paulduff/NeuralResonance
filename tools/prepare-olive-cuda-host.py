"""Freeze the host-workspace accuracy comparison after a verified cost review."""
from datetime import datetime,timezone
import importlib.util
import json
from pathlib import Path
import shutil
import subprocess
import sys
import uuid
from zipfile import ZipFile

ROOT=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('cost_preparation',ROOT/'tools/prepare-olive-cuda-cost.py')
common=importlib.util.module_from_spec(spec);spec.loader.exec_module(common)
spec=importlib.util.spec_from_file_location('host_analysis',ROOT/'tools/analyse-olive-cuda-host.py')
analysis=importlib.util.module_from_spec(spec);spec.loader.exec_module(analysis)
git=common.git;read=common.read;digest=common.digest


def main():
    assert not git('status','--porcelain'),'Commit before freezing.'
    revision=git('rev-parse','HEAD');assert revision==git('rev-parse','origin/main'),'Push before freezing.'
    review=ROOT/'artifacts/olive-cost-review-20261109';common.verify(review,['review.json'])
    report=read(review/'review.json')
    assert report['operatorReportedCompletion'] and report['savedAnalysisExactlyReproduced'] and report['accuracyPassed']
    assert report['trials']==180 and report['passedFocusedTests']==11 and report['controlGroups']==18
    capture=Path(report['capture']);assert digest(capture/'backup-manifest.json')==report['manifestSha256']
    owned=['Structures/_SharedRuntime/OlivaryMembrane.cs','tools/OliveBatchProbe/OlivaryBatchKernel.cs',
        'tools/OliveBatchProbe/OlivaryCpuBatch.cs','tools/CudaInteropProbe/CudaDriverProbe.cs',
        'tools/OliveCudaProbe/OlivaryCudaBatch.cs','tools/OliveCudaProbe/CudaDriverProbe.Olive.cs',
        'tools/OliveCudaProbe/BatchControls.cs','tools/OliveCudaProbe/olivary.cu',
        'tools/OliveCudaTimingProbe/CudaDriverProbe.Memory.cs']
    names=['source/'+n for n in owned]
    names += [p.relative_to(capture).as_posix() for p in (capture/'cpu-baseline').rglob('*') if p.is_file()]
    names += ['timing-evidence/'+n for n in ['device.json','compiler.json','olivary.ptx']]
    copies={n:n for n in names if not n.startswith('source/')}
    for count in [192,3072,12288]:
        for step in [.025,.00625]:
            key=f'cells-{count}-h-{step}'
            for kind in ['reports','final']:
                source=f'study/{key}/round-0/control/measured-{kind}.bin.gz'
                names.append(source);copies[source]=f'recorded/{key}/measured-{kind}.bin.gz'
    common.verify(capture,names)
    for name in owned:
        assert (ROOT/name).read_text(encoding='utf-8')==(capture/'source'/name).read_text(encoding='utf-8'),'Reviewed source changed: '+name
    stamp=datetime.now(timezone.utc).strftime('%Y%m%d')
    package=ROOT/'artifacts'/('olive-cuda-host-'+stamp+'-'+uuid.uuid4().hex[:6])
    backup=Path('D:/DNNE-desktop-runs/UnityWorld')/package.name
    assert not package.exists() and not backup.exists();package.mkdir()
    subprocess.run(['git','archive','--format=zip','--output='+str(package/'source.zip'),revision],cwd=ROOT,check=True)
    with ZipFile(package/'source.zip') as archive:
        assert all(not Path(n).is_absolute() and '..' not in Path(n).parts for n in archive.namelist())
        archive.extractall(package/'source')
    copied=[]
    for old,new in copies.items():
        target=package/new;target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(capture/old,target)
        assert digest(target)==digest(capture/old);copied.append(dict(source=old,target=new,sha256=digest(target)))
    protocol=dict(version='olive-cuda-host-accuracy-v1',counts=[192,3072,12288],steps=[.025,.00625],frames=128,
        modes=['reference','candidate'],legacyControls=analysis.LEGACY,hostControls=analysis.EXTRA,
        exactRecordedBytesRequired=True,speedMeasured=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    for name,value in [('host-protocol.json',protocol),('provenance.json',dict(costReview=report,
        reviewManifestSha256=digest(review/'backup-manifest.json'),unchangedSources=owned,copiedFiles=copied))]:
        (package/name).write_text(json.dumps(value,indent=2)+'\n',encoding='utf-8')
    shutil.copy2(package/'source/tools/backup-unity-run.ps1',package/'backup-run.ps1')
    files=[dict(Path=p.relative_to(package).as_posix(),Sha256=digest(p)) for p in sorted(package.rglob('*')) if p.is_file()]
    dotnet=shutil.which('dotnet');assert dotnet
    config=dict(Revision=revision,PreparedUtc=datetime.now(timezone.utc).isoformat(),PackageFiles=files,
        DotnetExecutable=dotnet,PythonExecutable=sys.executable,BackupDestination=str(backup),NoLiveMonitoring=True,
        CostReviewed=True,SpeedMeasured=False,ExpectedTestCases=11,ExpectedCases=6,ExpectedLegacyControls=6,ExpectedHostControls=6,
        TestFilter='FullyQualifiedName~OlivaryBatchTests|FullyQualifiedName~SolutionManifestTests',
        TestClasses=['OlivaryBatchTests','SolutionManifestTests'],LiveBrainAccessed=False,OldSavesLoaded=False)
    (package/'configuration.json').write_text(json.dumps(config,indent=2)+'\n',encoding='utf-8')
    pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,PreparedOnly=True,NoLiveMonitoring=True,
        Launcher=str(package/'source/tools/start-olive-cuda-host-window.ps1'))
    (ROOT/'artifacts'/('olive-cuda-host-prepared-'+stamp+'.json')).write_text(json.dumps(pointer,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(pointer,indent=2))


if __name__=='__main__':main()
