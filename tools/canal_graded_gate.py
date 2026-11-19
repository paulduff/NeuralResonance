"""Freeze the original head replay and qualify physical-clock organ integration."""
import argparse
from datetime import datetime,timezone
from pathlib import Path,PurePosixPath
import json
import shutil
import subprocess
import sys
import uuid
from zipfile import ZipFile
from afferent_precision_gate import h,read,save
from cleft_trajectory_reference import base
import receptor_membrane_gate as inventory
from canal_graded_protocol import protocol
import canal_clock_gate as previous

ROOT=Path(__file__).resolve().parents[1]
COUNTS=dict(previous.COUNTS,GradedCoupledCanalTests=35)
PRESERVED=previous.PRESERVED+['src/NRE.SimAvatar/AvatarClockedCoupledCanal.cs',
    'tests/NeuralResonanceEngine.DNNE.Tests/ClockedCoupledCanalTests.cs',
    'tools/NativeMotorBridgeProbe/CanalClockStudy.cs','tools/canal_clock_protocol.py',
    'tools/canal_clock_reference.py','tools/canal_clock_analysis.py','tools/canal_clock_gate.py']


def prepare():
    assert not h.git('status','--porcelain'),'Commit first.'
    revision=h.git('rev-parse','HEAD');assert revision==h.git('rev-parse','origin/main'),'Push first.'
    assert shutil.disk_usage('D:/').free>=10*1024**3
    reviewed=ROOT/'artifacts/canal-clock-20261119-274f31-review';h.verify(reviewed,['review.json','analysis.json','transient-diagnosis.json'])
    review=read(reviewed/'review.json');assert review['operatorReportedCompletion'] and review['originalFailurePreserved'] and not review['numericalCriteriaPassed'] and review['passedTests']==121 and review['referenceCriteriaPassed'] and review['allClockAndAccountingChecksPassed']
    capture=Path(review['capture']);assert h.digest(capture/'backup-manifest.json')==review['manifestSha256']
    names=['clock-input.json','analysis.json','window-status.json','reference-trajectories/samples.npz',
        'baseline/frame-input.json','baseline/reference-60.json','baseline/reference-90.json','baseline/source-interpretation.json','qualified-managed-runtime.json']
    h.verify(capture,names+['source/'+n for n in PRESERVED])
    for name in PRESERVED:assert (ROOT/name).read_text(encoding='utf-8-sig')==h.io(capture/'source'/name).read_text(encoding='utf-8-sig'),name
    methods=inventory.inventory(ROOT,COUNTS);assert sum(methods.values())==156
    spec=protocol(read(capture/'clock-input.json')['frame'])
    assert spec['expectedNativeSteps']==491729 and spec['expectedReferenceSamples']==459293
    package=Path('D:/DNNE-build-work')/('canal-graded-'+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6]);backup=Path('D:/DNNE-desktop-runs/UnityWorld')/package.name
    assert not package.exists() and not backup.exists();package.mkdir()
    subprocess.run(['git','archive','--format=zip','--output='+str(package/'source.zip'),revision],cwd=ROOT,check=True)
    with ZipFile(package/'source.zip') as z:
        for entry in z.infolist():
            name=PurePosixPath(entry.filename)
            assert not name.is_absolute() and '..' not in name.parts and ':' not in entry.filename and '\\' not in entry.filename
            assert ((entry.external_attr>>16)&0o170000)!=0o120000
            target=h.io(package/'source'/name)
            if entry.is_dir():target.mkdir(parents=True,exist_ok=True)
            else:
                target.parent.mkdir(parents=True,exist_ok=True)
                with target.open('xb') as f:f.write(z.read(entry))
    def copy(src,name):
        target=package/name;target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(h.io(src),h.io(target));assert h.digest(src)==h.digest(target)
    for name in names:copy(capture/name,'baseline/'+name)
    copy(reviewed/'review.json','reviewed-uniform.json');copy(reviewed/'analysis.json','reviewed-uniform-analysis.json')
    copy(reviewed/'transient-diagnosis.json','reviewed-uniform-transients.json')
    copy(capture/'qualified-managed-runtime.json','qualified-managed-runtime.json')
    copy(package/'source/tools/backup-unity-run.ps1','backup-run.ps1')
    save(package/'graded-input.json',spec);save(package/'preparation-environment.json',base.environment())
    save(package/'provenance.json',dict(uniformManifestSha256=review['manifestSha256'],
        baselineFiles=[dict(path=n,sha256=h.digest(capture/n)) for n in names],
        preservedSources=[dict(path=n,sha256=h.digest(capture/'source'/n)) for n in PRESERVED],
        sourceDirectionsNormalized=False,biologicalParametersChanged=False,liveSelectionChanged=False,
        modelsExecutedDuringPreparation=False,testsExecutedDuringPreparation=False))
    files=[dict(Path=p.relative_to(h.io(package)).as_posix(),Sha256=h.digest(p)) for p in sorted(h.io(package).rglob('*')) if p.is_file()]
    dotnet=shutil.which('dotnet');assert dotnet
    save(package/'configuration.json',dict(Revision=revision,PackageFiles=files,BackupDestination=str(backup),
        DotnetExecutable=dotnet,DotnetExecutableSha256=h.digest(dotnet),PythonExecutable=sys.executable,PythonExecutableSha256=h.digest(sys.executable),
        IsolatedGradedCanal=True,NoLiveMonitoring=True,ExpectedTestCases=156,ExpectedClassCounts=COUNTS,ExpectedTestMethods=methods,
        TestFilter='|'.join('FullyQualifiedName~'+c for c in COUNTS),ExpectedNativeRuns=3,ExpectedNativeSteps=491729,ExpectedReferenceSamples=459293,
        NewNativePhysicsRequested=False,LiveSelectionChanged=False,PhysiologyQualified=False,AvatarRecoveryAssessed=False))
    pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,NoLiveMonitoring=True,Launcher=str(package/'source/tools/start-canal-graded-window.ps1'))
    save(ROOT/'artifacts'/(package.name+'-prepared.json'),pointer);print(json.dumps(pointer,indent=2),flush=True)


def preflight(package):
    cfg=read(package/'configuration.json');p=read(package/'provenance.json');review=read(package/'reviewed-uniform.json')
    assert cfg['IsolatedGradedCanal'] and cfg['NoLiveMonitoring'] and cfg['ExpectedTestCases']==156 and cfg['ExpectedClassCounts']==COUNTS
    assert cfg['ExpectedNativeRuns']==3 and cfg['ExpectedNativeSteps']==491729 and cfg['ExpectedReferenceSamples']==459293
    assert review['operatorReportedCompletion'] and review['originalFailurePreserved'] and not review['numericalCriteriaPassed'] and review['referenceCriteriaPassed'] and review['manifestSha256']==p['uniformManifestSha256']
    assert read(package/'reviewed-uniform-analysis.json')==read(package/'baseline/analysis.json')
    assert read(package/'baseline/window-status.json')['Status']=='FAILED'
    assert read(package/'graded-input.json')==protocol(read(package/'baseline/clock-input.json')['frame'])
    assert {r['path'] for r in p['preservedSources']}==set(PRESERVED)
    for r in p['preservedSources']:assert h.digest(package/'source'/r['path'])==r['sha256'],r['path']
    for r in p['baselineFiles']:assert h.digest(package/'baseline'/r['path'])==r['sha256'],r['path']
    for row in cfg['PackageFiles']:assert h.digest(package/row['Path'])==row['Sha256'],row['Path']
    for tool in ('Dotnet','Python'):assert h.digest(cfg[tool+'Executable'])==cfg[tool+'ExecutableSha256']
    assert base.environment()==read(package/'preparation-environment.json')
    for directory in read(package/'qualified-managed-runtime.json')['directories']:
        root=h.io(directory['path']);assert {str(f) for f in root.rglob('*') if f.is_file()}=={str(h.io(r['path'])) for r in directory['files']}
        for row in directory['files']:assert h.digest(row['path'])==row['sha256']
    print('Original failed clock, head evidence, mechanical equations, accuracy limits and dependencies preserved; no model/test executed.',flush=True)


def runtime(package):
    preflight(package);assert read(package/'managed-runtime-before.json')==read(package/'qualified-managed-runtime.json')


def check_tests(package):
    runtime(package);result=inventory.check_tests(package,COUNTS);save(package/'test-analysis.json',result)


def reference(package):
    runtime(package)
    from canal_graded_reference import run
    run(package)


def compare(package):
    runtime(package);assert inventory.check_tests(package,COUNTS)['passedCases']==156
    from canal_graded_analysis import analyse
    result=analyse(package);save(package/'analysis.json',result)
    for row in result['runs']:print(row['refinement'],row['accountingPassed'],row['error'],flush=True)
    if not result['numericalCriteriaPassed']:raise RuntimeError('Graded canal criteria failed; retain all traces, source values and original bounds.')
    print('Original clocks and held-input causality reconciled across 491,729 graded mechanical substeps; continuous motion and physiology remain separate.',flush=True)


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    p=argparse.ArgumentParser();p.add_argument('command',choices=['prepare','preflight','postflight','runtime','check-tests','reference','compare']);p.add_argument('path',type=Path,nargs='?');a=p.parse_args()
    if a.command=='prepare':prepare()
    else:dict(preflight=preflight,postflight=preflight,runtime=runtime,**{'check-tests':check_tests},reference=reference,compare=compare)[a.command](a.path.resolve())
