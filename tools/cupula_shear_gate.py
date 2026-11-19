"""Freeze source evidence and qualify the declared cupula spatial component."""
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
import canal_graded_gate as previous

ROOT=Path(__file__).resolve().parents[1]
COUNTS=dict(previous.COUNTS,CupulaShearFieldTests=23)
PRESERVED=previous.PRESERVED+['src/NRE.SimAvatar/AvatarCanalStepSchedule.cs','src/NRE.SimAvatar/AvatarClockedGradedCanal.cs',
    'tests/NeuralResonanceEngine.DNNE.Tests/GradedCoupledCanalTests.cs','tools/canal_graded_reference.py',
    'tools/canal_graded_analysis.py','tools/canal_graded_protocol.py','tools/canal_graded_gate.py']


def prepare():
    assert not h.git('status','--porcelain'),'Commit first.'
    revision=h.git('rev-parse','HEAD');assert revision==h.git('rev-parse','origin/main'),'Push first.'
    assert shutil.disk_usage('D:/').free>=10*1024**3
    reviewed=ROOT/'artifacts/canal-graded-20261119-7880d2-review';h.verify(reviewed,['review.json','analysis.json'])
    review=read(reviewed/'review.json');assert review['operatorReportedCompletion'] and review['numericalCriteriaPassed'] and review['passedTests']==156 and review['referenceCriteriaPassed'] and review['allClockAndAccountingChecksPassed']
    capture=Path(review['capture']);assert h.digest(capture/'backup-manifest.json')==review['manifestSha256']
    names=['qualified-managed-runtime.json']
    h.verify(capture,names+['source/'+n for n in PRESERVED])
    for name in PRESERVED:assert (ROOT/name).read_text(encoding='utf-8-sig')==h.io(capture/'source'/name).read_text(encoding='utf-8-sig'),name
    methods=inventory.inventory(ROOT,COUNTS);assert sum(methods.values())==179
    sources=ROOT/'artifacts/cupula-mapping-source-20261119-32f7a8';h.verify(sources,['source-review.json','PMC8050243.html'])
    source_review=read(sources/'source-review.json')
    assert source_review['catalogSha256']==h.digest(ROOT/'docs/reference-data/cupula-shear-closure.json').lower()
    assert not source_review['modelsExecuted'] and not source_review['testsExecuted']
    package=Path('D:/DNNE-build-work')/('cupula-shear-'+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6]);backup=Path('D:/DNNE-desktop-runs/UnityWorld')/package.name
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
    copy(reviewed/'review.json','reviewed-clock.json');copy(reviewed/'analysis.json','reviewed-clock-analysis.json')
    copy(sources/'source-review.json','mapping-source-review.json')
    copy(capture/'qualified-managed-runtime.json','qualified-managed-runtime.json')
    copy(package/'source/tools/backup-unity-run.ps1','backup-run.ps1')
    save(package/'preparation-environment.json',base.environment())
    save(package/'provenance.json',dict(gradedClockManifestSha256=review['manifestSha256'],
        baselineFiles=[dict(path=n,sha256=h.digest(capture/n)) for n in names],
        preservedSources=[dict(path=n,sha256=h.digest(capture/'source'/n)) for n in PRESERVED],
        sourceDirectionsNormalized=False,biologicalParametersChanged=False,liveSelectionChanged=False,
        modelsExecutedDuringPreparation=False,testsExecutedDuringPreparation=False))
    files=[dict(Path=p.relative_to(h.io(package)).as_posix(),Sha256=h.digest(p)) for p in sorted(h.io(package).rglob('*')) if p.is_file()]
    dotnet=shutil.which('dotnet');assert dotnet
    save(package/'configuration.json',dict(Revision=revision,PackageFiles=files,BackupDestination=str(backup),
        DotnetExecutable=dotnet,DotnetExecutableSha256=h.digest(dotnet),PythonExecutable=sys.executable,PythonExecutableSha256=h.digest(sys.executable),
        IsolatedCupulaShear=True,NoLiveMonitoring=True,ExpectedTestCases=179,ExpectedClassCounts=COUNTS,ExpectedTestMethods=methods,
        TestFilter='|'.join('FullyQualifiedName~'+c for c in COUNTS),
        NewNativePhysicsRequested=False,LiveSelectionChanged=False,PhysiologyQualified=False,AvatarRecoveryAssessed=False))
    pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,NoLiveMonitoring=True,Launcher=str(package/'source/tools/start-cupula-shear-window.ps1'))
    save(ROOT/'artifacts'/(package.name+'-prepared.json'),pointer);print(json.dumps(pointer,indent=2),flush=True)


def preflight(package):
    cfg=read(package/'configuration.json');p=read(package/'provenance.json');review=read(package/'reviewed-clock.json')
    assert cfg['IsolatedCupulaShear'] and cfg['NoLiveMonitoring'] and cfg['ExpectedTestCases']==179 and cfg['ExpectedClassCounts']==COUNTS
    assert review['operatorReportedCompletion'] and review['numericalCriteriaPassed'] and review['referenceCriteriaPassed']
    assert review['manifestSha256']==p['gradedClockManifestSha256'] and review['passedTests']==156
    assert read(package/'reviewed-clock-analysis.json')['numericalCriteriaPassed']
    sources=read(package/'mapping-source-review.json')
    assert sources['catalogSha256']==h.digest(package/'source/docs/reference-data/cupula-shear-closure.json').lower()
    assert not sources['modelsExecuted'] and not sources['testsExecuted']
    assert {r['path'] for r in p['preservedSources']}==set(PRESERVED)
    for r in p['preservedSources']:assert h.digest(package/'source'/r['path'])==r['sha256'],r['path']
    for r in p['baselineFiles']:assert h.digest(package/'baseline'/r['path'])==r['sha256'],r['path']
    for row in cfg['PackageFiles']:assert h.digest(package/row['Path'])==row['Sha256'],row['Path']
    for tool in ('Dotnet','Python'):assert h.digest(cfg[tool+'Executable'])==cfg[tool+'ExecutableSha256']
    assert base.environment()==read(package/'preparation-environment.json')
    for directory in read(package/'qualified-managed-runtime.json')['directories']:
        root=h.io(directory['path']);assert {str(f) for f in root.rglob('*') if f.is_file()}=={str(h.io(r['path'])) for r in directory['files']}
        for row in directory['files']:assert h.digest(row['path'])==row['sha256']
    print('Reviewed graded clock, earlier mechanical kernels, declared spatial reduction and dependencies preserved; no model/test executed.',flush=True)


def runtime(package):
    preflight(package);assert read(package/'managed-runtime-before.json')==read(package/'qualified-managed-runtime.json')


def check_tests(package):
    runtime(package);result=inventory.check_tests(package,COUNTS);save(package/'test-analysis.json',result)
    save(package/'analysis.json',dict(completed=True,componentCriteriaPassed=True,passedCases=result['passedCases'],
        independentTrajectoryComparisonExecuted=False,coupledSensoryAccuracyQualified=False,anatomicalCalibrationQualified=False,
        physiologicalBundleMapQualified=False,afferentFiringQualified=False,avatarBalanceAssessed=False))
    print('179 component cases passed, including 23 spatial shear/attachment controls. No anatomical or connected sensory qualification.',flush=True)


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    p=argparse.ArgumentParser();p.add_argument('command',choices=['prepare','preflight','postflight','runtime','check-tests']);p.add_argument('path',type=Path,nargs='?');a=p.parse_args()
    if a.command=='prepare':prepare()
    else:dict(preflight=preflight,postflight=preflight,runtime=runtime,**{'check-tests':check_tests})[a.command](a.path.resolve())
