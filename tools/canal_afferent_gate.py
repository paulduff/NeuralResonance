"""Freeze the timed mechanical-to-observed-neuronal connection."""
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
import canal_bundle_gate as previous
import afferent_loaded_gate as sensory
from canal_afferent_protocol import protocol

ROOT=Path(__file__).resolve().parents[1]
COUNTS=dict(previous.COUNTS,CanalAfferentInputTests=25,AfferentEventObserverTests=20,VestibularAfferentDynamicsTests=15,ObservedAfferentIngressTests=20)
PRESERVED=previous.PRESERVED+['src/NRE.SimAvatar/AvatarCanalBundleInput.cs','src/NRE.SimAvatar/AvatarCanalDensePoint.cs',
    'tests/NeuralResonanceEngine.DNNE.Tests/CanalBundleInputTests.cs','tools/NativeMotorBridgeProbe/CanalBundleStudy.cs',
    'tools/canal_bundle_protocol.py','tools/canal_bundle_analysis.py','tools/canal_bundle_gate.py']
SENSORY_PRESERVED=sensory.PRESERVED



def prepare():
    assert not h.git('status','--porcelain'),'Commit first.'
    revision=h.git('rev-parse','HEAD');assert revision==h.git('rev-parse','origin/main'),'Push first.'
    assert shutil.disk_usage('D:/').free>=30*1024**3
    reviews={key:ROOT/'artifacts'/name for key,name in (
        ('bundle','canal-bundle-20261119-bd15d1-review'),('sensory','afferent-loaded-20261118-cd9fba-review'))}
    evidence={}
    for key,folder in reviews.items():
        h.verify(folder,['review.json','analysis.json']);evidence[key]=read(folder/'review.json')
        assert evidence[key]['operatorReportedCompletion']
        assert h.digest(Path(evidence[key]['capture'])/'backup-manifest.json')==evidence[key]['manifestSha256']
    assert evidence['bundle']['numericalCriteriaPassed'] and evidence['bundle']['passedTests']==204
    assert evidence['sensory']['numericalTrajectoryCriteriaPassed'] and evidence['sensory']['quietDriftPassed']
    capture=Path(evidence['bundle']['capture']);loaded=Path(evidence['sensory']['capture'])
    names=['bundle-input.json','analysis.json','window-status.json','study/geometry.json','study/r4/trajectory.csv.gz','qualified-managed-runtime.json']
    names += [f'study/r4/endpoint-{j:03}.json' for j in range(12)]
    sensory_names=['trajectory-input.json','trajectory-analysis.json','rest-observations.json','source-decisions.json','study/quiet-ticks64/initial.json','qualified-managed-runtime.json']
    h.verify(capture,names+['source/'+n for n in PRESERVED]);h.verify(loaded,sensory_names+['source/'+n for n in SENSORY_PRESERVED])
    for root,paths in ((capture,PRESERVED),(loaded,SENSORY_PRESERVED)):
        for name in paths:assert (ROOT/name).read_text(encoding='utf-8-sig')==h.io(root/'source'/name).read_text(encoding='utf-8-sig'),name
    assert read(capture/'qualified-managed-runtime.json')==read(loaded/'qualified-managed-runtime.json')
    methods=inventory.inventory(ROOT,COUNTS);assert sum(methods.values())==284
    spec=protocol(read(capture/'bundle-input.json'),read(loaded/'trajectory-input.json'))
    package=Path('D:/DNNE-build-work')/('canal-afferent-'+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6])
    backup=Path('D:/DNNE-desktop-runs/UnityWorld')/package.name
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
    for name in sensory_names:copy(loaded/name,'sensory/'+name)
    for key,folder in reviews.items():
        copy(folder/'review.json','reviewed-'+key+'.json');copy(folder/'analysis.json','reviewed-'+key+'-analysis.json')
    copy(capture/'qualified-managed-runtime.json','qualified-managed-runtime.json')
    copy(package/'source/tools/backup-unity-run.ps1','backup-run.ps1')
    save(package/'connected-input.json',spec);save(package/'preparation-environment.json',base.environment())
    save(package/'provenance.json',dict(bundleManifestSha256=evidence['bundle']['manifestSha256'],sensoryManifestSha256=evidence['sensory']['manifestSha256'],
        baselineFiles=[dict(path=n,sha256=h.digest(capture/n)) for n in names],
        sensoryFiles=[dict(path=n,sha256=h.digest(loaded/n)) for n in sensory_names],
        preservedSources=[dict(path=n,sha256=h.digest(capture/'source'/n)) for n in PRESERVED],
        sensorySources=[dict(path=n,sha256=h.digest(loaded/'source'/n)) for n in SENSORY_PRESERVED],
        biologicalParametersChanged=False,liveSelectionChanged=False,originalActivationDefaultChanged=False,
        modelsExecutedDuringPreparation=False,testsExecutedDuringPreparation=False))
    files=[dict(Path=p.relative_to(h.io(package)).as_posix(),Sha256=h.digest(p)) for p in sorted(h.io(package).rglob('*')) if p.is_file()]
    dotnet=shutil.which('dotnet');assert dotnet
    save(package/'configuration.json',dict(Revision=revision,PackageFiles=files,BackupDestination=str(backup),
        DotnetExecutable=dotnet,DotnetExecutableSha256=h.digest(dotnet),PythonExecutable=sys.executable,PythonExecutableSha256=h.digest(sys.executable),
        IsolatedCanalAfferent=True,NoLiveMonitoring=True,ExpectedTestCases=284,ExpectedClassCounts=COUNTS,ExpectedTestMethods=methods,
        TestFilter='|'.join('FullyQualifiedName~'+c for c in COUNTS),ExpectedNativeRuns=6,ExpectedSourceSteps=2293760,
        NewNativePhysicsRequested=False,LiveSelectionChanged=False,PhysiologyQualified=False,AvatarRecoveryAssessed=False))
    pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,NoLiveMonitoring=True,Launcher=str(package/'source/tools/start-canal-afferent-window.ps1'))
    save(ROOT/'artifacts'/(package.name+'-prepared.json'),pointer);print(json.dumps(pointer,indent=2),flush=True)


def preflight(package):
    cfg=read(package/'configuration.json');p=read(package/'provenance.json');review=read(package/'reviewed-bundle.json');loaded=read(package/'reviewed-sensory.json')
    assert cfg['IsolatedCanalAfferent'] and cfg['NoLiveMonitoring'] and cfg['ExpectedTestCases']==284 and cfg['ExpectedClassCounts']==COUNTS
    assert review['operatorReportedCompletion'] and review['numericalCriteriaPassed'] and review['passedTests']==204
    assert review['manifestSha256']==p['bundleManifestSha256'] and read(package/'reviewed-bundle-analysis.json')==read(package/'baseline/analysis.json')
    assert loaded['operatorReportedCompletion'] and loaded['numericalTrajectoryCriteriaPassed'] and loaded['quietDriftPassed']
    assert loaded['manifestSha256']==p['sensoryManifestSha256'] and read(package/'reviewed-sensory-analysis.json')==read(package/'sensory/trajectory-analysis.json')
    assert read(package/'baseline/window-status.json')['Status']=='DIAGNOSTICS_COMPLETE'
    assert read(package/'connected-input.json')==protocol(read(package/'baseline/bundle-input.json'),read(package/'sensory/trajectory-input.json'))
    assert cfg['ExpectedNativeRuns']==6 and cfg['ExpectedSourceSteps']==2293760
    assert read(package/'qualified-managed-runtime.json')==read(package/'sensory/qualified-managed-runtime.json')
    assert {r['path'] for r in p['preservedSources']}==set(PRESERVED)
    assert {r['path'] for r in p['sensorySources']}==set(SENSORY_PRESERVED)
    for key,folder in (('preservedSources','source'),('sensorySources','source'),('baselineFiles','baseline'),('sensoryFiles','sensory')):
        for r in p[key]:assert h.digest(package/folder/r['path'])==r['sha256'],r['path']
    for row in cfg['PackageFiles']:assert h.digest(package/row['Path'])==row['Sha256'],row['Path']
    for tool in ('Dotnet','Python'):assert h.digest(cfg[tool+'Executable'])==cfg[tool+'ExecutableSha256']
    assert base.environment()==read(package/'preparation-environment.json')
    for directory in read(package/'qualified-managed-runtime.json')['directories']:
        root=h.io(directory['path']);assert {str(f) for f in root.rglob('*') if f.is_file()}=={str(h.io(r['path'])) for r in directory['files']}
        for row in directory['files']:assert h.digest(row['path'])==row['sha256']
    print('Reviewed timed mechanics, loaded sensory state, original neuronal equations, limits and dependencies preserved; no model/test executed.',flush=True)


def runtime(package):
    preflight(package);assert read(package/'managed-runtime-before.json')==read(package/'qualified-managed-runtime.json')


def check_tests(package):
    runtime(package);save(package/'test-analysis.json',inventory.check_tests(package,COUNTS))


def compare(package):
    runtime(package)
    from canal_afferent_analysis import analyse
    result=analyse(package);save(package/'analysis.json',result)
    for row in result['runs']:print(row['condition'],row['sourceStepsPerInterval'],row['accountingPassed'],'events',row['events'],flush=True)
    if not result['connectedAccountingPassed']:raise RuntimeError('Connected source accounting/conservation failed; preserve all traces and original limits.')
    print('Original mechanical input, continuous neuronal state, charge/ion accounting and observed packets reconciled. Independent connected accuracy and physiology remain unqualified.',flush=True)



if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    p=argparse.ArgumentParser();p.add_argument('command',choices=['prepare','preflight','postflight','runtime','check-tests','compare']);p.add_argument('path',type=Path,nargs='?');a=p.parse_args()
    if a.command=='prepare':prepare()
    else:dict(preflight=preflight,postflight=preflight,runtime=runtime,**{'check-tests':check_tests},compare=compare)[a.command](a.path.resolve())
