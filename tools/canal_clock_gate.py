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
import canal_frame_gate as frame
import receptor_membrane_gate as inventory
from canal_clock_protocol import protocol

ROOT=Path(__file__).resolve().parents[1]
COUNTS=dict(frame.COUNTS,ClockedCoupledCanalTests=21)
PRESERVED=frame.PRESERVED+['src/NRE.SimAvatar/AvatarCanalPrimeBasis.cs','src/NRE.SimAvatar/AvatarCanalSkullInput.cs',
    'tests/NeuralResonanceEngine.DNNE.Tests/CanalSkullInputTests.cs','tools/coupled_canal_reference.py','tools/coupled_canal_analysis.py']


def prepare():
    assert not h.git('status','--porcelain'),'Commit first.'
    revision=h.git('rev-parse','HEAD');assert revision==h.git('rev-parse','origin/main'),'Push first.'
    assert shutil.disk_usage('D:/').free>=10*1024**3
    reviewed=ROOT/'artifacts/canal-frame-20261118-a80ee0-review';h.verify(reviewed,['review.json','analysis.json'])
    review=read(reviewed/'review.json');assert review['operatorReportedCompletion'] and review['repairedSavedComparisonPassed'] and review['focusedTestsPassed']==100
    capture=Path(review['capture']);assert h.digest(capture/'backup-manifest.json')==review['manifestSha256']
    names=['frame-input.json','reference-60.json','reference-90.json','reference-controls.json','qualified-managed-runtime.json','source-interpretation.json']
    h.verify(capture,names+['source/'+n for n in PRESERVED])
    for name in PRESERVED:assert (ROOT/name).read_text(encoding='utf-8-sig')==h.io(capture/'source'/name).read_text(encoding='utf-8-sig'),name
    methods=inventory.inventory(ROOT,COUNTS);assert sum(methods.values())==121
    spec=protocol(read(capture/'frame-input.json'))
    assert spec['expectedNativeSteps']==199*sum(spec['subdivisions'])==356608
    package=Path('D:/DNNE-build-work')/('canal-clock-'+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6]);backup=Path('D:/DNNE-desktop-runs/UnityWorld')/package.name
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
    copy(reviewed/'review.json','reviewed-frame.json');copy(reviewed/'analysis.json','reviewed-frame-analysis.json')
    copy(capture/'qualified-managed-runtime.json','qualified-managed-runtime.json')
    copy(package/'source/tools/backup-unity-run.ps1','backup-run.ps1')
    save(package/'clock-input.json',spec);save(package/'preparation-environment.json',base.environment())
    save(package/'provenance.json',dict(frameManifestSha256=review['manifestSha256'],
        baselineFiles=[dict(path=n,sha256=h.digest(capture/n)) for n in names],
        preservedSources=[dict(path=n,sha256=h.digest(capture/'source'/n)) for n in PRESERVED],
        sourceDirectionsNormalized=False,biologicalParametersChanged=False,liveSelectionChanged=False,
        modelsExecutedDuringPreparation=False,testsExecutedDuringPreparation=False))
    files=[dict(Path=p.relative_to(h.io(package)).as_posix(),Sha256=h.digest(p)) for p in sorted(h.io(package).rglob('*')) if p.is_file()]
    dotnet=shutil.which('dotnet');assert dotnet
    save(package/'configuration.json',dict(Revision=revision,PackageFiles=files,BackupDestination=str(backup),
        DotnetExecutable=dotnet,DotnetExecutableSha256=h.digest(dotnet),PythonExecutable=sys.executable,PythonExecutableSha256=h.digest(sys.executable),
        IsolatedCanalClock=True,NoLiveMonitoring=True,ExpectedTestCases=121,ExpectedClassCounts=COUNTS,ExpectedTestMethods=methods,
        TestFilter='|'.join('FullyQualifiedName~'+c for c in COUNTS),ExpectedNativeRuns=3,ExpectedNativeSteps=356608,ExpectedReferenceSamples=203777,
        NewNativePhysicsRequested=False,LiveSelectionChanged=False,PhysiologyQualified=False,AvatarRecoveryAssessed=False))
    pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,NoLiveMonitoring=True,Launcher=str(package/'source/tools/start-canal-clock-window.ps1'))
    save(ROOT/'artifacts'/(package.name+'-prepared.json'),pointer);print(json.dumps(pointer,indent=2),flush=True)


def preflight(package):
    cfg=read(package/'configuration.json');p=read(package/'provenance.json');review=read(package/'reviewed-frame.json')
    assert cfg['IsolatedCanalClock'] and cfg['NoLiveMonitoring'] and cfg['ExpectedTestCases']==121 and cfg['ExpectedClassCounts']==COUNTS
    assert cfg['ExpectedNativeRuns']==3 and cfg['ExpectedNativeSteps']==356608 and cfg['ExpectedReferenceSamples']==203777
    assert review['operatorReportedCompletion'] and review['repairedSavedComparisonPassed'] and review['manifestSha256']==p['frameManifestSha256']
    assert read(package/'reviewed-frame-analysis.json')['angularInputChecksPassed']
    assert read(package/'clock-input.json')==protocol(read(package/'baseline/frame-input.json'))
    assert {r['path'] for r in p['preservedSources']}==set(PRESERVED)
    for r in p['preservedSources']:assert h.digest(package/'source'/r['path'])==r['sha256'],r['path']
    for r in p['baselineFiles']:assert h.digest(package/'baseline'/r['path'])==r['sha256'],r['path']
    for row in cfg['PackageFiles']:assert h.digest(package/row['Path'])==row['Sha256'],row['Path']
    for tool in ('Dotnet','Python'):assert h.digest(cfg[tool+'Executable'])==cfg[tool+'ExecutableSha256']
    assert base.environment()==read(package/'preparation-environment.json')
    for directory in read(package/'qualified-managed-runtime.json')['directories']:
        root=h.io(directory['path']);assert {str(f) for f in root.rglob('*') if f.is_file()}=={str(h.io(r['path'])) for r in directory['files']}
        for row in directory['files']:assert h.digest(row['path'])==row['sha256']
    print('Original head evidence, printed basis, mechanical equations, reference methods and dependencies preserved; no model/test executed.',flush=True)


def runtime(package):
    preflight(package);assert read(package/'managed-runtime-before.json')==read(package/'qualified-managed-runtime.json')


def check_tests(package):
    runtime(package);result=inventory.check_tests(package,COUNTS);save(package/'test-analysis.json',result)


def reference(package):
    runtime(package)
    from canal_clock_reference import run
    run(package)


def compare(package):
    runtime(package);assert inventory.check_tests(package,COUNTS)['passedCases']==121
    from canal_clock_analysis import analyse
    result=analyse(package);save(package/'analysis.json',result)
    for row in result['runs']:print(row['subdivisions'],row['accountingPassed'],row['error'],flush=True)
    if not result['numericalCriteriaPassed']:raise RuntimeError('Clocked canal criteria failed; retain all traces, source values and original bounds.')
    print('Original clocks and held-input causality reconciled across 356,608 mechanical substeps; continuous motion and physiology remain separate.',flush=True)


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    p=argparse.ArgumentParser();p.add_argument('command',choices=['prepare','preflight','postflight','runtime','check-tests','reference','compare']);p.add_argument('path',type=Path,nargs='?');a=p.parse_args()
    if a.command=='prepare':prepare()
    else:dict(preflight=preflight,postflight=preflight,runtime=runtime,**{'check-tests':check_tests},reference=reference,compare=compare)[a.command](a.path.resolve())
