"""Freeze an independent comparison of already-reviewed native bridge trajectories."""
from datetime import datetime, timezone
import importlib.util
import json
import os
from pathlib import Path, PurePosixPath
import shutil
import subprocess
import sys
import uuid
from zipfile import ZipFile

for name in ('OPENBLAS_NUM_THREADS','OMP_NUM_THREADS','MKL_NUM_THREADS','NUMEXPR_NUM_THREADS'):
    os.environ[name]='1'
ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'tools'))
from native_hinge_protocol import make_protocol, CASES


def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path);value=importlib.util.module_from_spec(spec);spec.loader.exec_module(value);return value


def main():
    helper=module('packaging',ROOT/'tools/prepare-causal-motor.py')
    io,read,digest,verify,git=helper.io,helper.read,helper.digest,helper.verify,helper.git
    assert not git('status','--porcelain'),'Commit clean source first.'
    revision=git('rev-parse','HEAD');assert revision==git('rev-parse','origin/main'),'Push first.'
    native_review=ROOT/'artifacts/native-motor-bridge-review-20261116'
    numerical_review=ROOT/'artifacts/causal-motor-comparison-review-20261113'
    verify(native_review,['review.json']);verify(numerical_review,['review.json'])
    native=read(native_review/'review.json');numerical=read(numerical_review/'review.json')
    assert native['operatorReportedCompletion'] and native['passedTests']==81 and native['savedAnalysisExactlyReproduced']
    assert native['analysis']['originalCriteriaPassed'] and native['analysis']['actualPhysicsSteps']==4096
    assert numerical['operatorReportedCompletion'] and numerical['passedTests']==120 and numerical['savedAnalysisExactlyReproduced']
    assert numerical['analysis']['numericalCriteriaPassed']
    capture=Path(native['capture']);qualified=Path(numerical['capture'])
    assert digest(capture/'backup-manifest.json')==native['manifestSha256']
    assert digest(qualified/'backup-manifest.json')==numerical['manifestSha256']
    native_evidence=['native-bridge/'+case+'/'+name for case in CASES for name in ('initial.json','final.json','steps.jsonl','source-0.json','source-1.json')]
    native_evidence+=['reference/muscle-input.json','bridge-analysis.json']
    native_code=['src/NRE.SimAvatar/'+name for name in ('AvatarCausalMotorPlant.cs','AvatarDynamicMuscle.cs','AvatarMotorUnitCalcium.cs','AvatarMusclePath.cs')]
    native_code+=['UnityWorld/Assets/NRE/Runtime/'+name for name in ('MuscleHingeBridge.cs','MuscleHingeWire.cs','DiagnosticBridgeFiles.cs')]
    native_code+=['tools/NativeMotorBridgeProbe/NativeHingeBody.cs']
    reference_code=['tools/'+name for name in ('causal_motor_reference.py','dynamic_muscle_reference.py','golgi-reference-resume.py')]
    verify(capture,native_evidence+['source/'+name for name in native_code])
    verify(qualified,['reference-preflight.json']+['source/'+name for name in reference_code])
    for original,names in ((capture,native_code),(qualified,reference_code)):
        for name in names:
            assert (ROOT/name).read_text(encoding='utf-8-sig')==io(original/'source'/name).read_text(encoding='utf-8-sig'),name
    environment=module('numerical_environment',ROOT/'tools/golgi-reference-resume.py').environment()
    assert environment==read(qualified/'reference-preflight.json')['environment'],'Qualified numerical dependencies changed.'
    spec=make_protocol(capture/'native-bridge',capture/'reference/muscle-input.json')
    assert len(spec['cases'])==4 and len(spec['referenceSettings'])==3
    package=ROOT/'artifacts'/('native-hinge-comparison-'+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6])
    backup=Path('D:/DNNE-desktop-runs/UnityWorld')/package.name
    assert not package.exists() and not backup.exists();package.mkdir()
    subprocess.run(['git','archive','--format=zip','--output='+str(package/'source.zip'),revision],cwd=ROOT,check=True)
    with ZipFile(package/'source.zip') as archive:
        for entry in archive.infolist():
            path=PurePosixPath(entry.filename)
            assert not path.is_absolute() and '..' not in path.parts and ':' not in entry.filename and '\\' not in entry.filename
            assert ((entry.external_attr>>16)&0o170000)!=0o120000
            target=io(package/'source'/path)
            if entry.is_dir():target.mkdir(parents=True,exist_ok=True)
            else:
                target.parent.mkdir(parents=True,exist_ok=True)
                with target.open('xb') as stream:stream.write(archive.read(entry))
    copied=[]
    def copy(original,destination):
        target=io(package/destination);target.parent.mkdir(parents=True,exist_ok=True)
        shutil.copyfile(io(original),target);assert digest(original)==digest(target)
        copied.append(dict(original=str(original),path=destination,sha256=digest(target)))
    for name in native_evidence:copy(capture/name,name)
    copy(native_review/'review.json','reference/native-review.json')
    copy(numerical_review/'review.json','reference/qualified-muscle-review.json')
    copy(capture/'backup-manifest.json','reference/native-capture-manifest.json')
    copy(qualified/'backup-manifest.json','reference/qualified-muscle-manifest.json')
    copy(qualified/'reference-preflight.json','reference/qualified-numerical-preflight.json')
    for name,value in (('hinge-input.json',spec),('preparation-environment.json',environment),
                       ('provenance.json',dict(copied=copied,operatorReportedNativeCapture=str(capture),
                            fullNativeCapturePreviouslyAudited=True,fullNativeCaptureFiles=27120,
                            nativePhysicsReexecuted=False,qualifiedSourcePreserved=native_code+reference_code))):
        with (package/name).open('x') as stream:json.dump(value,stream,indent=2,allow_nan=False)
    shutil.copyfile(package/'source/tools/backup-unity-run.ps1',package/'backup-run.ps1')
    files=[dict(Path=p.relative_to(io(package)).as_posix(),Sha256=digest(p)) for p in sorted(io(package).rglob('*')) if p.is_file()]
    configuration=dict(Revision=revision,PreparedUtc=datetime.now(timezone.utc).isoformat(),PackageFiles=files,
        PythonExecutable=sys.executable,BackupDestination=str(backup),NoLiveMonitoring=True,
        IsolatedCapturedNativeComparison=True,VerifiedNativeCapture=True,QualifiedReferenceCorePreserved=True,
        ExpectedNativeCases=4,ExpectedCapturedSteps=4096,ExpectedReferenceSolves=12,ExpectedGeometryControls=49,
        ExpectedMechanicalMethodChecks=6,ExpectedCrossMethodChecks=3,NativePhysicsReexecuted=False,
        LiveBodySelected=False,PhysicalStepRefinementAssessed=False,MuscleStepRefinementAssessed=False,
        FullCoupledTrajectoryQualified=False,PhysiologyQualified=False,AvatarRecoveryAssessed=False)
    (package/'configuration.json').write_text(json.dumps(configuration,indent=2)+'\n')
    pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,PreparedOnly=True,NoLiveMonitoring=True,
                 Launcher=str(package/'source/tools/start-native-hinge-comparison-window.ps1'))
    (package.parent/(package.name+'-prepared.json')).write_text(json.dumps(pointer,indent=2)+'\n')
    print(json.dumps(pointer,indent=2),flush=True)


if __name__=='__main__':main()
