"""Freeze and audit head-coordinate components; run tests/probes only in the visible window."""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path, PurePosixPath
import shutil
import subprocess
import sys
import uuid
from zipfile import ZipFile

ROOT=Path(__file__).resolve().parents[1]
def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path); result=importlib.util.module_from_spec(spec);spec.loader.exec_module(result);return result
h=module('skull_package_helpers',ROOT/'tools/prepare-causal-motor.py')
gate=module('skull_test_inventory',ROOT/'tools/receptor_membrane_gate.py')
vp=module('skull_default_protocol',ROOT/'tools/vestibular_numerics_protocol.py')
COUNTS=dict(vp.COUNTS,SkullKinematicsTests=12)
def save(path,value):
    with h.io(path).open('x',encoding='utf-8') as stream:json.dump(value,stream,indent=2,allow_nan=False);stream.write('\n')

def prepare():
    assert not h.git('status','--porcelain'),'Commit clean source first.'
    revision=h.git('rev-parse','HEAD');assert revision==h.git('rev-parse','origin/main'),'Push first.'
    review_folder=ROOT/'artifacts/vestibular-numerics-review-20261117';h.verify(review_folder,['review.json'])
    review=h.read(review_folder/'review.json');assert review['operatorReportedCompletion'] and review['savedAnalysisExactlyReproduced']
    assert review['passedTests']==62 and review['analysis']['numericalCriteriaPassed']
    baseline=Path(review['capture']);assert h.digest(baseline/'backup-manifest.json')==review['manifestSha256']
    baseline_files=[p.relative_to(h.io(baseline)).as_posix() for p in sorted(h.io(baseline/'study').rglob('*')) if p.is_file()]
    assert len(baseline_files)==61
    preserved=['ControlProgram/VestibularLabyrinthTransducer.cs','ControlProgram/PhysicalBodyTransducerRuntime.cs',
        'Shared.Contracts/PhysicalSampleProtocol.cs','Shared.Contracts/PhysicalObservationProtocol.cs',
        'tools/NativeMotorBridgeProbe/VestibularMechanicsStudy.cs','tools/vestibular_numerics_protocol.py']
    h.verify(baseline,baseline_files+['source/'+name for name in preserved]+['managed-runtime-before.json'])
    for name in preserved:assert h.io(ROOT/name).read_text(encoding='utf-8-sig')==h.io(baseline/'source'/name).read_text(encoding='utf-8-sig'),name
    methods=gate.inventory(ROOT,COUNTS);assert sum(COUNTS.values())==74
    package=ROOT/'artifacts'/('skull-kinematics-'+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6])
    backup=Path('D:/DNNE-desktop-runs/UnityWorld')/package.name
    assert not package.exists() and not backup.exists();package.mkdir()
    subprocess.run(['git','archive','--format=zip','--output='+str(package/'source.zip'),revision],cwd=ROOT,check=True)
    with ZipFile(package/'source.zip') as archive:
        for entry in archive.infolist():
            name=PurePosixPath(entry.filename)
            assert not name.is_absolute() and '..' not in name.parts and ':' not in entry.filename and '\\' not in entry.filename
            assert ((entry.external_attr>>16)&0o170000)!=0o120000
            target=h.io(package/'source'/name)
            if entry.is_dir():target.mkdir(parents=True,exist_ok=True)
            else:
                target.parent.mkdir(parents=True,exist_ok=True)
                with target.open('xb') as stream:stream.write(archive.read(entry))
    references=[]
    for name in baseline_files:
        destination=h.io(package/'reference'/name);destination.parent.mkdir(parents=True,exist_ok=True)
        shutil.copyfile(h.io(baseline/name),destination)
        references.append(dict(Path='reference/'+name,Sha256=h.digest(destination)))
    shutil.copyfile(h.io(baseline/'managed-runtime-before.json'),package/'reference/qualified-managed-runtime.json')
    save(package/'vestibular-input.json',vp.protocol())
    save(package/'provenance.json',dict(review=review,preservedSources=preserved,baselineFiles=references,
        defaultMechanicsRegression=True,testsExecutedDuringPreparation=False,modelsExecutedDuringPreparation=False,
        nativeHeadCaptureExecuted=False,liveSelectionChanged=False))
    shutil.copyfile(package/'source/tools/backup-unity-run.ps1',package/'backup-run.ps1')
    files=[dict(Path=p.relative_to(h.io(package)).as_posix(),Sha256=h.digest(p)) for p in sorted(h.io(package).rglob('*')) if p.is_file()]
    dotnet=shutil.which('dotnet');assert dotnet
    save(package/'configuration.json',dict(Revision=revision,PackageFiles=files,
        DotnetExecutable=dotnet,DotnetExecutableSha256=h.digest(dotnet),PythonExecutable=sys.executable,PythonExecutableSha256=h.digest(sys.executable),
        BackupDestination=str(backup),IsolatedSkullKinematics=True,NoLiveMonitoring=True,ExpectedConditions=20,ExpectedNativeCaptures=40,
        ExpectedTestCases=74,ExpectedClassCounts=COUNTS,ExpectedTestMethods=methods,
        TestFilter='|'.join('FullyQualifiedName~'+name for name in COUNTS),NativeHeadCaptureRequested=False,PhysiologyQualified=False,AvatarRecoveryAssessed=False))
    pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,
        Launcher=str(package/'source/tools/start-skull-kinematics-window.ps1'))
    save(package.parent/(package.name+'-prepared.json'),pointer);print(json.dumps(pointer,indent=2),flush=True)

def compare(package):
    tests=gate.check_tests(package,COUNTS)
    assert h.read(package/'vestibular-input.json')==vp.protocol()
    original=h.read(package/'provenance.json')['baselineFiles']
    native=h.read(package/'study/results.json')
    assert native['completed'] and len(native['cases'])==20
    names=set()
    for entry in original:
        name=entry['Path'].removeprefix('reference/')
        assert h.digest(package/entry['Path'])==entry['Sha256']==h.digest(package/name),name
        names.add(name)
    assert names=={p.relative_to(h.io(package)).as_posix() for p in h.io(package/'study').rglob('*') if p.is_file()}
    assert h.digest(package/'managed-runtime-before.json')==h.digest(package/'reference/qualified-managed-runtime.json')
    analysis=dict(completed=True,componentChecksPassed=True,tests=tests,defaultNativeFilesPreserved=61,
        defaultConditionsPreserved=20,defaultNativeCapturesPreserved=40,newSkullComponentCases=12,
        nativeHeadCaptureExecuted=False,independentHeadTrajectoryQualified=False,continuousHeadMotionAccuracyQualified=False,
        neuronalModelExecuted=False,liveBrainAccessed=False,physiologyQualified=False,avatarRecoveryAssessed=False)
    save(package/'analysis.json',analysis);print(json.dumps(analysis,indent=2),flush=True)

if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser();parser.add_argument('command',choices=['prepare','check-tests','compare']);parser.add_argument('package',type=Path,nargs='?');args=parser.parse_args()
    if args.command=='prepare':prepare()
    elif args.command=='check-tests':print(json.dumps(gate.check_tests(args.package.resolve(),COUNTS),indent=2))
    else:compare(args.package.resolve())
