"""Freeze a native rubral/muscle study only after the operator-reported source gate is reviewed."""
import argparse
from datetime import datetime,timezone
import importlib.util
import json
from pathlib import Path,PurePosixPath
import shutil
import subprocess
import sys
import uuid
from zipfile import ZipFile

ROOT=Path(__file__).resolve().parents[1]


def module(name,path):
    spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m);return m


prior=module('rubral_native_source_gate',ROOT/'tools/rubral_circuit_gate.py');h=prior.h;gate=prior.gate
native=module('rubral_native_previous_packager',ROOT/'tools/prepare-native-motor-bridge.py')
transition=module('rubral_explicit_environment',ROOT/'tools/rubral_reference_environment.py')
COUNTS=dict(prior.COUNTS)
for name,count in dict(native.COUNTS,MotorUnitCalciumTests=13,IsometricMotorUnitPlantTests=15).items():
    assert name not in COUNTS or COUNTS[name]==count
    COUNTS[name]=count


def save(path,value):
    with h.io(path).open('x',encoding='utf-8') as f:json.dump(value,f,indent=2,allow_nan=False);f.write('\n')


def load_review(folder):
    # A review is an explicit prerequisite, never permission to poll its source.
    h.verify(folder,['review.json']);r=h.read(folder/'review.json')
    assert r['operatorReportedCompletion'] and r['savedAnalysisExactlyReproduced']
    capture=Path(r['capture']);assert h.digest(capture/'backup-manifest.json')==r['manifestSha256']
    return r,capture


def prepare(review_folder,environment_review=None):
    assert not h.git('status','--porcelain'),'Commit clean source first.'
    revision=h.git('rev-parse','HEAD');assert revision==h.git('rev-parse','origin/main'),'Push first.'
    source_review,source_capture=load_review(review_folder)
    assert source_review['bodyEpochSourceGatePassed'] and source_review['passedTests']==161
    assert source_review['analysis']['bodyEpochSourceGatePassed'] and source_review['analysis']['sourceFrames']==120000
    body_review_folder=ROOT/'artifacts/observed-native-body-review-20261116';body_review,body_capture=load_review(body_review_folder)
    assert body_review['numericalCriteriaPassed'] and body_review['passedTests']==160 and not body_review['originalBoundsChanged']
    metadata=module('rubral_unity_metadata',ROOT/'tools/unity_metadata.py');metadata.validate(ROOT/'UnityWorld')
    generated=module('rubral_path_generator',ROOT/'tools/prepare-unity-muscle-paths.py').generate(ROOT)
    assert generated==(ROOT/'UnityWorld/Assets/NRE/Runtime/MusclePathGeometry.cs').read_text(encoding='utf-8-sig')
    circuit_code=[p.relative_to(h.io(source_capture/'source')).as_posix() for p in sorted(h.io(source_capture/'source/Structures/_SharedRuntime').rglob('*.cs'))]
    circuit_code+=['tools/NativeMotorBridgeProbe/'+name for name in ('RubralMotorCircuit.cs','ObservedMotorSource.cs')]
    circuit_code+=['Protocol/'+name+'.cs' for name in ('MotorAxonProtocol','MotorAxonPublisher','MotorAxonReplayBuffer')]
    body_code=['src/NRE.SimAvatar/'+name+'.cs' for name in ('AvatarCausalMotorPlant','AvatarDynamicMuscle','AvatarMotorUnitCalcium','AvatarMotorAxonReceiver','AvatarMusclePath')]
    body_code+=['UnityWorld/Assets/NRE/Runtime/'+name+'.cs' for name in ('MuscleHingeBridge','MuscleHingeWire','DiagnosticBridgeFiles','MusclePathGeometry')]
    body_code+=['tools/NativeMotorBridgeProbe/'+name+'.cs' for name in ('NativeHingeBody','ObservedMotorSource','WirePreflight')]
    body_code+=['tools/'+name+'.py' for name in ('native_hinge_reference','native_hinge_reference_controls','causal_motor_reference','dynamic_muscle_reference',
        'golgi-reference-resume','native_hinge_protocol','analyse-native-motor-bridge','analyse-observed-native-body')]
    extended='tools/analyse-native-hinge-comparison.py'
    originals=['reference/study/'+key+'/'+name for key in prior.old.CASES for name in ('input.json','topology.json','source-frames.jsonl.gz','packets1/trajectory.csv','packets1/final.json')]
    originals+=['reference/study/drive-4/contacts-disabled/'+name for name in ('trajectory.csv','final.json')]
    reference_names=['muscle-input.json','failed-request-000033.json','last-reply-000032.json','qualified-hinge-input.json',
        'qualified-environment.json','hinge-refinement-review.json','quiet-h0.003125.csv']
    h.verify(source_capture,originals+['analysis.json','managed-runtime-before.json']+['source/'+name for name in circuit_code])
    h.verify(body_capture,['reference/'+name for name in reference_names]+['source/'+name for name in body_code+[extended]])
    preserved=[]
    for capture,names in ((source_capture,circuit_code),(body_capture,body_code)):
        for name in names:
            assert h.io(ROOT/name).read_text(encoding='utf-8-sig')==h.io(capture/'source'/name).read_text(encoding='utf-8-sig'),name
            preserved.append(dict(path=name,reviewedCapture=str(capture),qualifiedSha256=h.digest(capture/'source'/name)))
    code=(ROOT/extended).read_text(encoding='utf-8-sig')
    old_signature='def candidate_data(folder, case):';new_signature='def candidate_data(folder, case, *, allowed_steps=(512,1024,2048)):'
    old_guard="assert len(rows) == case['physicalSteps'] and case['physicalSteps'] in (512,1024,2048)"
    new_guard="assert len(rows) == case['physicalSteps'] and case['physicalSteps'] in allowed_steps"
    assert code.count(new_signature)==code.count(new_guard)==1
    assert code.replace(new_signature,old_signature).replace(new_guard,old_guard)==h.io(body_capture/'source'/extended).read_text(encoding='utf-8-sig'),extended
    environment=module('rubral_reference_environment',ROOT/'tools/golgi-reference-resume.py').environment()
    previous_environment=h.read(body_capture/'reference/qualified-environment.json')
    declaration=None
    if environment_review is None:
        assert environment==previous_environment,'Qualified numerical dependencies changed; inspect and declare the transition explicitly.'
    else:
        h.verify(environment_review,['qualified-environment.json','current-environment.json','transition-declaration.json'])
        assert previous_environment==h.read(environment_review/'qualified-environment.json')
        assert environment==h.read(environment_review/'current-environment.json')
        declaration=h.read(environment_review/'transition-declaration.json')
        transition.validate(previous_environment,environment,declaration)
    methods=gate.inventory(ROOT,COUNTS);assert sum(methods.values())==sum(COUNTS.values())
    package=ROOT/'artifacts'/('rubral-native-'+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6])
    backup=Path('D:/DNNE-desktop-runs/UnityWorld')/package.name;workspace=ROOT/'artifacts'/(package.name+'-build-work')
    assert not package.exists() and not backup.exists() and not workspace.exists();package.mkdir()
    subprocess.run(['git','archive','--format=zip','--output='+str(package/'source.zip'),revision],cwd=ROOT,check=True)
    with ZipFile(package/'source.zip') as archive:
        for entry in archive.infolist():
            name=PurePosixPath(entry.filename);assert not name.is_absolute() and '..' not in name.parts and ':' not in entry.filename and '\\' not in entry.filename
            assert ((entry.external_attr>>16)&0o170000)!=0o120000
            target=h.io(package/'source'/name)
            if entry.is_dir():target.mkdir(parents=True,exist_ok=True)
            else:
                target.parent.mkdir(parents=True,exist_ok=True)
                with target.open('xb') as f:f.write(archive.read(entry))
    def copy(source,destination):
        target=h.io(package/destination);target.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(h.io(source),target)
        assert h.digest(source)==h.digest(target)
    for name in originals:copy(source_capture/name,'reference/rubral/'+name.removeprefix('reference/'))
    for name in reference_names:copy(body_capture/'reference'/name,'reference/'+name)
    copy(source_capture/'analysis.json','reference/circuit-gate/analysis.json');copy(review_folder/'review.json','reference/circuit-review.json')
    copy(source_capture/'managed-runtime-before.json','reference/qualified-managed-runtime.json')
    copy(body_review_folder/'review.json','reference/observed-body-review.json')
    if declaration is not None:copy(environment_review/'transition-declaration.json','reference/environment-transition.json')
    copy(package/'source/tools/backup-unity-run.ps1','backup-run.ps1')
    save(package/'preparation-environment.json',environment)
    save(package/'unity-metadata.json',metadata.validate(package/'source/UnityWorld'))
    save(package/'provenance.json',dict(sourceReview=source_review,bodyReview=body_review,preservedSources=preserved,
        comparisonDecoderExtension=dict(path=extended,qualifiedSha256=h.digest(body_capture/'source'/extended),defaultAcceptedStepCountsUnchanged=True,newExplicitStepCount=7999),
        environmentTransition=declaration,modelsExecutedDuringPreparation=False,testsExecutedDuringPreparation=False))
    dotnet=shutil.which('dotnet');unity=Path('C:/Program Files/Unity/Hub/Editor/6000.3.19f1/Editor/Unity.exe');assert dotnet and unity.is_file()
    files=[dict(Path=p.relative_to(h.io(package)).as_posix(),Sha256=h.digest(p)) for p in sorted(h.io(package).rglob('*')) if p.is_file()]
    save(package/'configuration.json',dict(Revision=revision,PackageFiles=files,BackupDestination=str(backup),NoLiveMonitoring=True,
        IsolatedRubralNative=True,QualifiedCouplingCorePreserved=True,ExpectedTestCases=sum(COUNTS.values()),ExpectedClassCounts=COUNTS,ExpectedTestMethods=methods,
        TestFilter='|'.join('FullyQualifiedName~'+name for name in COUNTS),ExpectedNativeCases=7,ExpectedNativePhysicsSteps=55993,ExpectedIndependentReferences=15,
        EnvironmentTransitionDeclared=declaration is not None,ExpectedEnvironmentControls=10,
        ExpectedReferenceControlCounts=dict(geometryCases=49,mechanicalMethodChecks=6,crossMethodChecks=3),
        ExpectedUnityVersion='6000.3.19f1',UnityBuildWorkspace=str(workspace),UnityExecutable=str(unity),UnityExecutableSha256=h.digest(unity),
        DotnetExecutable=dotnet,DotnetExecutableSha256=h.digest(dotnet),PythonExecutable=sys.executable,PythonExecutableSha256=h.digest(sys.executable),
        LiveBodySelected=False,NoTimingMeasured=True,PhysiologyQualified=False,AvatarRecoveryAssessed=False))
    pointer=dict(Package=str(package),Backup=str(backup),Revision=revision,PreparedOnly=True,NoLiveMonitoring=True,
        Launcher=str(package/'source/tools/start-rubral-native-window.ps1'))
    save(package.parent/(package.name+'-prepared.json'),pointer);print(json.dumps(pointer,indent=2),flush=True)


def preflight(package):
    config=h.read(package/'configuration.json')
    assert config['IsolatedRubralNative'] and config['NoLiveMonitoring'] and config['ExpectedClassCounts']==COUNTS and config['ExpectedTestCases']==sum(COUNTS.values())
    assert config['ExpectedNativeCases']==7 and config['ExpectedNativePhysicsSteps']==55993 and config['ExpectedIndependentReferences']==15
    for entry in config['PackageFiles']:assert h.digest(package/entry['Path'])==entry['Sha256'],entry['Path']
    review=h.read(package/'reference/circuit-review.json');analysis=h.read(package/'reference/circuit-gate/analysis.json')
    assert review['operatorReportedCompletion'] and review['savedAnalysisExactlyReproduced'] and review['bodyEpochSourceGatePassed'] and review['analysis']==analysis
    assert analysis['bodyEpochSourceGatePassed'] and analysis['tests']['passedCases']==161
    environment=module('rubral_native_preflight_environment',package/'source/tools/golgi-reference-resume.py').environment()
    assert environment==h.read(package/'preparation-environment.json')
    previous=h.read(package/'reference/qualified-environment.json')
    if config['EnvironmentTransitionDeclared']:
        transition.validate(previous,environment,h.read(package/'reference/environment-transition.json'))
    else:assert environment==previous
    assert config['ExpectedEnvironmentControls']==10 and config['ExpectedReferenceControlCounts']==dict(geometryCases=49,mechanicalMethodChecks=6,crossMethodChecks=3)
    print('Reviewed circuit, preserved source and declared numerical environment verified; no model or test executed.',flush=True)


def reference_controls(package):
    # All numeric controls and transition fixtures execute only in the visible
    # window, including the independent mechanical integrator's six solves.
    preflight(package)
    environment_controls=transition.controls()
    spec=h.read(package/'reference/qualified-hinge-input.json')
    runner=module('rubral_fresh_reference_controls',package/'source/tools/native_hinge_reference_controls.py')
    result=runner.run_controls(spec,spec['nativeGeometry'],package/'reference-controls')
    counts=dict(geometryCases=49,mechanicalMethodChecks=6,crossMethodChecks=3)
    assert result['completed'] and result['passed'] and not result['biologicalDynamicsIntegrated'] and not result['nativePhysicsExecuted']
    assert all(len(result[k])==n and all(r['passed'] for r in result[k]) for k,n in counts.items())
    save(package/'reference-controls-summary.json',dict(completed=True,passed=True,environmentControls=environment_controls,
        referenceControlCounts=counts,controlsSha256=h.digest(package/'reference-controls/controls.json'),
        environmentTransitionDeclared=h.read(package/'configuration.json')['EnvironmentTransitionDeclared'],
        historicalRuntimeEquivalenceEstablished=False,physiologyQualified=False))
    check_reference_controls(package)


def load_reference(package):
    # This study has an explicit interpreter-transition policy. The historical
    # observed-body loader intentionally requires its original interpreter and
    # must not be reused here or weakened for its existing callers.
    preflight(package)
    check_reference_controls(package)
    base=module('rubral_hinge_comparison',package/'source/tools/analyse-native-hinge-comparison.py')
    bridge=module('rubral_native_mechanics',package/'source/tools/analyse-native-motor-bridge.py')
    review=h.read(package/'reference/hinge-refinement-review.json')
    assert review['operatorReportedCompletion'] and review['savedAnalysisExactlyReproduced'] and review['numericalCriteriaPassed']
    return base,bridge


def check_reference_controls(package):
    summary=h.read(package/'reference-controls-summary.json');result=h.read(package/'reference-controls/controls.json')
    config=h.read(package/'configuration.json')
    assert summary['completed'] and summary['passed'] and summary['environmentControls']==transition.CONTROL_NAMES
    assert summary['referenceControlCounts']==config['ExpectedReferenceControlCounts']
    assert summary['environmentTransitionDeclared']==config['EnvironmentTransitionDeclared']
    assert not summary['historicalRuntimeEquivalenceEstablished'] and not summary['physiologyQualified']
    assert summary['controlsSha256']==h.digest(package/'reference-controls/controls.json')
    assert result['completed'] and result['passed'] and not result['biologicalDynamicsIntegrated'] and not result['nativePhysicsExecuted']
    assert all(len(result[k])==n and all(r['passed'] for r in result[k]) for k,n in summary['referenceControlCounts'].items())
    return summary


if __name__=='__main__':
    if not __debug__:raise RuntimeError('Assertions must remain enabled.')
    parser=argparse.ArgumentParser();parser.add_argument('command',choices=['prepare','preflight','check-tests','reference-controls']);parser.add_argument('path',type=Path)
    parser.add_argument('--environment-review',type=Path);args=parser.parse_args()
    path=args.path.resolve()
    if args.command=='prepare':prepare(path,args.environment_review.resolve() if args.environment_review else None)
    elif args.command=='preflight':preflight(path)
    elif args.command=='reference-controls':reference_controls(path)
    else:print(json.dumps(gate.check_tests(path,COUNTS),indent=2),flush=True)
