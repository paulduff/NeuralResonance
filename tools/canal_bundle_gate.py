"""Freeze the reviewed mechanics and spatial field for their timed connection."""
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
import cupula_shear_gate as previous
from canal_bundle_protocol import protocol

ROOT=Path(__file__).resolve().parents[1]
COUNTS=dict(previous.COUNTS,CanalBundleInputTests=25)
PRESERVED=previous.PRESERVED+['src/NRE.SimAvatar/AvatarCupulaShearField.cs',
    'tests/NeuralResonanceEngine.DNNE.Tests/CupulaShearFieldTests.cs',
    'docs/reference-data/cupula-shear-closure.json','tools/NativeMotorBridgeProbe/CanalGradedStudy.cs']


def prepare():
    assert not h.git('status','--porcelain'),'Commit first.'
    revision=h.git('rev-parse','HEAD');assert revision==h.git('rev-parse','origin/main'),'Push first.'
    assert shutil.disk_usage('D:/').free>=10*1024**3
    reviews={key:ROOT/'artifacts'/name for key,name in (
        ('clock','canal-graded-20261119-7880d2-review'),('cupula','cupula-shear-20261119-8ec244-review'))}
    evidence={}
    for key,folder in reviews.items():
        h.verify(folder,['review.json','analysis.json']);evidence[key]=read(folder/'review.json')
        assert evidence[key]['operatorReportedCompletion']
        assert h.digest(Path(evidence[key]['capture'])/'backup-manifest.json')==evidence[key]['manifestSha256']
    assert evidence['clock']['numericalCriteriaPassed'] and evidence['clock']['referenceCriteriaPassed']
    assert evidence['cupula']['componentCriteriaPassed'] and evidence['cupula']['passedTests']==179
    capture=Path(evidence['clock']['capture']);component=Path(evidence['cupula']['capture'])
    names=['graded-input.json','analysis.json','window-status.json','reference-trajectories/samples.npz',
        'study/input.json','study/parameters.json','study/basis.json','study/results.json','qualified-managed-runtime.json']
    for r in (1,2,4):
        names += [f'study/r{r}/{n}' for n in ('initial.json','final.json','trajectory.csv.gz')]
        names += [f'study/r{r}/endpoint-{j:03}.json' for j in range(201)]
    h.verify(capture,names);h.verify(component,['source/'+n for n in PRESERVED])
    for name in PRESERVED:assert (ROOT/name).read_text(encoding='utf-8-sig')==h.io(component/'source'/name).read_text(encoding='utf-8-sig'),name
    methods=inventory.inventory(ROOT,COUNTS);assert sum(methods.values())==204
    spec=protocol(read(capture/'graded-input.json'))
    package=Path('D:/DNNE-build-work')/('canal-bundle-'+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6])
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
    for key,folder in reviews.items():
        copy(folder/'review.json','reviewed-'+key+'.json');copy(folder/'analysis.json','reviewed-'+key+'-analysis.json')
    copy(capture/'qualified-managed-runtime.json','qualified-managed-runtime.json')
    copy(package/'source/tools/backup-unity-run.ps1','backup-run.ps1')
    save(package/'bundle-input.json',spec);save(package/'preparation-environment.json',base.environment())
    save(package/'provenance.json',dict(gradedClockManifestSha256=evidence['clock']['manifestSha256'],
        cupulaManifestSha256=evidence['cupula']['manifestSha256'],
        baselineFiles=[dict(path=n,sha256=h.digest(capture/n)) for n in names],
        preservedSources=[dict(path=n,sha256=h.digest(component/'source'/n)) for n in PRESERVED],
        biologicalParametersChanged=False,liveSelectionChanged=False,modelsExecutedDuringPreparation=False,testsExecutedDuringPreparation=False))
    files=[dict(Path=p.relative_to(h.io(package)).as_posix(),Sha256=h.digest(p)) for p in sorted(h.io(package).rglob('*')) if p.is_file()]
    dotnet=shutil.which('dotnet');assert dotnet
    save(package/'configuration.json',dict(Revision=revision,PackageFiles=files,BackupDestination=str(backup),
        DotnetExecutable=dotnet,DotnetExecutableSha256=h.digest(dotnet),PythonExecutable=sys.executable,PythonExecutableSha256=h.digest(sys.executable),
        IsolatedCanalBundle=True,NoLiveMonitoring=True,ExpectedTestCases=204,ExpectedClassCounts=COUNTS,ExpectedTestMethods=methods,
        TestFilter='|'.join('FullyQualifiedName~'+c for c in COUNTS),ExpectedNativeRuns=3,ExpectedNativeSteps=491729,
        ExpectedDenseSamples=sum(len(v)*199 for v in spec['queryReferenceTicks'].values()),
        NewNativePhysicsRequested=False,LiveSelectionChanged=False,PhysiologyQualified=False,AvatarRecoveryAssessed=False))
    pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,NoLiveMonitoring=True,Launcher=str(package/'source/tools/start-canal-bundle-window.ps1'))
    save(ROOT/'artifacts'/(package.name+'-prepared.json'),pointer);print(json.dumps(pointer,indent=2),flush=True)


def preflight(package):
    cfg=read(package/'configuration.json');p=read(package/'provenance.json');review=read(package/'reviewed-clock.json')
    assert cfg['IsolatedCanalBundle'] and cfg['NoLiveMonitoring'] and cfg['ExpectedTestCases']==204 and cfg['ExpectedClassCounts']==COUNTS
    assert review['operatorReportedCompletion'] and review['numericalCriteriaPassed'] and review['referenceCriteriaPassed']
    assert review['manifestSha256']==p['gradedClockManifestSha256'] and review['passedTests']==156
    component=read(package/'reviewed-cupula.json')
    assert component['operatorReportedCompletion'] and component['componentCriteriaPassed'] and component['passedTests']==179
    assert component['manifestSha256']==p['cupulaManifestSha256']
    assert read(package/'reviewed-clock-analysis.json')==read(package/'baseline/analysis.json')
    assert read(package/'baseline/window-status.json')['Status']=='DIAGNOSTICS_COMPLETE'
    spec=protocol(read(package/'baseline/graded-input.json'));assert read(package/'bundle-input.json')==spec
    assert cfg['ExpectedNativeRuns']==3 and cfg['ExpectedNativeSteps']==491729
    assert cfg['ExpectedDenseSamples']==sum(len(v)*199 for v in spec['queryReferenceTicks'].values())
    assert {r['path'] for r in p['preservedSources']}==set(PRESERVED)
    for r in p['preservedSources']:assert h.digest(package/'source'/r['path'])==r['sha256'],r['path']
    for r in p['baselineFiles']:assert h.digest(package/'baseline'/r['path'])==r['sha256'],r['path']
    for row in cfg['PackageFiles']:assert h.digest(package/row['Path'])==row['Sha256'],row['Path']
    for tool in ('Dotnet','Python'):assert h.digest(cfg[tool+'Executable'])==cfg[tool+'ExecutableSha256']
    assert base.environment()==read(package/'preparation-environment.json')
    for directory in read(package/'qualified-managed-runtime.json')['directories']:
        root=h.io(directory['path']);assert {str(f) for f in root.rglob('*') if f.is_file()}=={str(h.io(r['path'])) for r in directory['files']}
        for row in directory['files']:assert h.digest(row['path'])==row['sha256']
    print('Reviewed head/mechanics, spatial field, saved references, original limits and dependencies preserved; no model/test executed.',flush=True)


def runtime(package):
    preflight(package);assert read(package/'managed-runtime-before.json')==read(package/'qualified-managed-runtime.json')


def check_tests(package):
    runtime(package);save(package/'test-analysis.json',inventory.check_tests(package,COUNTS))


def compare(package):
    runtime(package)
    from canal_bundle_analysis import analyse
    result=analyse(package);save(package/'analysis.json',result)
    for row in result['runs']:print(row['refinement'],row['arithmeticPassed'],row['denseError'],flush=True)
    if not result['numericalCriteriaPassed']:raise RuntimeError('Timed bundle criteria failed; retain every trajectory and the original limits.')
    print('Original mechanical trajectories retained exactly; sampled dense states and declared bundle geometry reconciled. No sensory neuron or live body executed.',flush=True)


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    p=argparse.ArgumentParser();p.add_argument('command',choices=['prepare','preflight','postflight','runtime','check-tests','compare']);p.add_argument('path',type=Path,nargs='?');a=p.parse_args()
    if a.command=='prepare':prepare()
    else:dict(preflight=preflight,postflight=preflight,runtime=runtime,**{'check-tests':check_tests},compare=compare)[a.command](a.path.resolve())
