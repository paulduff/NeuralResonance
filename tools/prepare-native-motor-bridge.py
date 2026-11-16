"""Freeze an isolated native motor/body bridge after verified hinge and muscle evidence."""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path, PurePosixPath
import re
import shutil
import subprocess
import sys
import uuid
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[1]
COUNTS = dict(MusclePathGeometryTests=31, CausalMotorPlantTests=27, DynamicMuscleTests=16, DiagnosticBridgeFileTests=6, MuscleHingeClockTests=12, SolutionManifestTests=1)


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec); spec.loader.exec_module(result); return result


def main(refinement=False,neuronal=False):
    assert not (refinement and neuronal)
    counts=dict(COUNTS)
    if neuronal:counts.update(ObservedMotorSourceTests=8,MotorAxonTransportTests=32,MotorAxonReplayBufferTests=16,MotoneuronReferenceTests=11)
    helper = module('package_helpers', ROOT / 'tools/prepare-causal-motor.py')
    assert not helper.git('status', '--porcelain'), 'Commit clean source first.'
    revision = helper.git('rev-parse', 'HEAD'); assert revision == helper.git('rev-parse', 'origin/main'), 'Push first.'
    metadata = module('unity_metadata', ROOT / 'tools/unity_metadata.py')
    metadata.validate(ROOT / 'UnityWorld')
    review_folder = ROOT / 'artifacts/native-hinge-review-20261116'
    helper.verify(review_folder, ['review.json']); review = helper.read(review_folder / 'review.json')
    assert review['operatorReportedCompletion'] and review['savedAnalysisExactlyReproduced'] and review['nativeHingeChecksPassed']
    assert review['passedTests'] == 75 and review['passedNativeCases'] == 24 and not review['originalBoundsChanged']
    capture = Path(review['capture'])
    assert helper.digest(capture / 'backup-manifest.json') == review['manifestSha256']
    same = ['src/NRE.SimAvatar/AvatarCausalMotorPlant.cs', 'src/NRE.SimAvatar/AvatarDynamicMuscle.cs',
            'src/NRE.SimAvatar/AvatarMusclePath.cs', 'UnityWorld/Assets/NRE/Runtime/MusclePathGeometry.cs',
            'UnityWorld/Assets/NRE/Runtime/MusclePathNativeQualification.cs',
            'UnityWorld/Assets/NRE/Runtime/MusclePathHingeQualification.cs']
    helper.verify(capture, ['source/' + name for name in same])
    for name in same:
        assert (ROOT / name).read_text(encoding='utf-8-sig') == helper.io(capture / 'source' / name).read_text(encoding='utf-8-sig')
    muscle_review_folder = ROOT / 'artifacts/causal-motor-comparison-review-20261113'
    helper.verify(muscle_review_folder, ['review.json'])
    muscle_review = helper.read(muscle_review_folder / 'review.json')
    assert muscle_review['operatorReportedCompletion'] and muscle_review['savedAnalysisExactlyReproduced']
    assert muscle_review['analysis']['numericalCriteriaPassed'] and not muscle_review['originalBoundsChanged']
    muscle_capture = Path(muscle_review['capture'])
    assert helper.digest(muscle_capture / 'backup-manifest.json') == muscle_review['manifestSha256']
    muscle_same = ['src/NRE.SimAvatar/AvatarCausalMotorPlant.cs', 'src/NRE.SimAvatar/AvatarDynamicMuscle.cs',
        'src/NRE.SimAvatar/AvatarMotorUnitCalcium.cs', 'src/NRE.SimAvatar/AvatarMotorAxonReceiver.cs']
    helper.verify(muscle_capture, ['causal-input.json'] + ['source/' + name for name in muscle_same])
    for name in muscle_same:
        assert (ROOT / name).read_text(encoding='utf-8-sig') == helper.io(muscle_capture / 'source' / name).read_text(encoding='utf-8-sig'), name
    failure_review_folder = ROOT / 'artifacts/native-motor-bridge-failure-review-20261116'
    helper.verify(failure_review_folder, ['review.json'])
    failure_review = helper.read(failure_review_folder / 'review.json')
    assert failure_review['operatorReportedCompletion'] and failure_review['savedRequestClockExactlyCorrect']
    assert failure_review['completedQuietSteps'] == 9 and failure_review['rejectedBeforePhysics']
    failure_capture = Path(failure_review['capture'])
    assert helper.digest(failure_capture / 'backup-manifest.json') == failure_review['manifestSha256']
    failure_inputs = {'failed-request-000033.json': 'native-bridge/quiet/bridge/request-000033.json',
        'last-reply-000032.json': 'native-bridge/quiet/bridge/reply-000032.json'}
    helper.verify(failure_capture, list(failure_inputs.values()))
    read_review_folder = ROOT / 'artifacts/native-bridge-read-failure-review-20261116'
    helper.verify(read_review_folder, ['review.json'])
    read_review = helper.read(read_review_folder / 'review.json')
    assert read_review['operatorReportedCompletion'] and read_review['wire']['originalDecimalFailureReproduced']
    assert read_review['wire']['exactScalarRoundTrips'] == 1040 and read_review['nativeReplyPreserved']
    assert read_review['nativeSteps'] == 2288 and read_review['completedPlantSteps'] == 2287
    generated = module('unity_path_generator', ROOT / 'tools/prepare-unity-muscle-paths.py').generate(ROOT)
    assert generated == (ROOT / 'UnityWorld/Assets/NRE/Runtime/MusclePathGeometry.cs').read_text(encoding='utf-8-sig')
    methods = {}
    for cls, count in counts.items():
        code = (ROOT / 'tests/NeuralResonanceEngine.DNNE.Tests' / (cls + '.cs')).read_text(encoding='utf-8-sig')
        found = re.findall(r'((?:\s*\[[^\n]+\])+\s*)public\s+(?:async\s+)?(?:void|Task)\s+(\w+)\(', code)
        members = {name: (1 if '[Fact]' in attrs else attrs.count('[InlineData('))
                   for attrs, name in found if '[Fact]' in attrs or '[Theory]' in attrs}
        assert sum(members.values()) == count and all(members.values()), cls
        methods.update({'NeuralResonanceEngine.DNNE.Tests.' + cls + '.' + name: n for name, n in members.items()})
    stamp = datetime.now(timezone.utc).strftime('%Y%m%d')
    prefix='observed-native-body-' if neuronal else 'native-hinge-refinement-' if refinement else 'native-motor-bridge-'
    comparison=None
    if refinement:
        from native_hinge_refinement_protocol import CONDITIONS,CRITERIA
        comparison_review=ROOT/'artifacts/native-hinge-comparison-review-20261116'
        helper.verify(comparison_review,['review.json']);compared=helper.read(comparison_review/'review.json')
        assert compared['operatorReportedCompletion'] and compared['savedAnalysisExactlyReproduced'] and compared['capturedTrajectoryAgreementPassed']
        comparison=Path(compared['capture']);assert helper.digest(comparison/'backup-manifest.json')==compared['manifestSha256']
        selected=['hinge-input.json','hinge-analysis.json','reference-preflight.json','reference-postflight.json','reference-controls/controls.json']
        selected+=['hinge-reference/'+p.name for p in (comparison/'hinge-reference').iterdir() if p.is_file()]
        selected+=['native-bridge/'+case+'/'+name for case in ('quiet','left','right','balanced') for name in ('initial.json','final.json','steps.jsonl','source-0.json','source-1.json')]
        code=['tools/'+name for name in ('native_hinge_reference.py','native_hinge_reference_controls.py','causal_motor_reference.py','dynamic_muscle_reference.py','golgi-reference-resume.py')]
        helper.verify(comparison,selected+['source/'+name for name in code])
        for name in code:assert (ROOT/name).read_text()==helper.io(comparison/'source'/name).read_text(),name
        environment=module('numeric_environment',ROOT/'tools/golgi-reference-resume.py').environment()
        assert environment==helper.read(comparison/'reference-preflight.json')['environment'],'Qualified numerical dependencies changed.'
    if neuronal:
        source_review_folder=ROOT/'artifacts/observed-motor-source-review-20261116'
        refinement_review_folder=ROOT/'artifacts/native-hinge-refinement-review-20261116'
        helper.verify(source_review_folder,['review.json']);helper.verify(refinement_review_folder,['review.json'])
        source_review=helper.read(source_review_folder/'review.json');refinement_review=helper.read(refinement_review_folder/'review.json')
        assert source_review['operatorReportedCompletion'] and source_review['savedAnalysisExactlyReproduced'] and source_review['sourceAdapterCriteriaPassed']
        assert refinement_review['operatorReportedCompletion'] and refinement_review['savedAnalysisExactlyReproduced'] and refinement_review['numericalCriteriaPassed']
        source_capture=Path(source_review['capture']);refinement_capture=Path(refinement_review['capture'])
        assert helper.digest(source_capture/'backup-manifest.json')==source_review['manifestSha256']
        assert helper.digest(refinement_capture/'backup-manifest.json')==refinement_review['manifestSha256']
        source_names=['reference/'+n for n in ('boundary-input.json','boundary-analysis.json','source-results.json','source-metadata.json','quiet-h0.003125.csv','soma-6000-h0.003125.csv')]
        neuronal_code=['Structures/_SharedRuntime/MotoneuronReferenceCell.cs','Structures/_SharedRuntime/MotoneuronChannelKinetics.cs',
            'tools/MotoneuronReferenceProbe/ClockAudit.cs','Protocol/MotorAxonPublisher.cs','Protocol/MotorAxonReplayBuffer.cs','Protocol/MotorAxonProtocol.cs',
            'src/NRE.SimAvatar/AvatarMotorAxonReceiver.cs']
        helper.verify(source_capture,source_names+['source/'+n for n in neuronal_code])
        for name in neuronal_code:assert (ROOT/name).read_text()==helper.io(source_capture/'source'/name).read_text(),name
        numerical_code=['tools/'+n for n in ('native_hinge_reference.py','native_hinge_reference_controls.py','causal_motor_reference.py',
            'dynamic_muscle_reference.py','golgi-reference-resume.py','analyse-native-hinge-comparison.py','analyse-native-motor-bridge.py')]
        native_code=['UnityWorld/Assets/NRE/Runtime/'+n for n in ('MuscleHingeBridge.cs','MuscleHingeWire.cs','DiagnosticBridgeFiles.cs')]
        native_code+=['tools/NativeMotorBridgeProbe/NativeHingeBody.cs']
        helper.verify(refinement_capture,['qualified/hinge-input.json','preparation-environment.json']+['source/'+n for n in numerical_code+native_code])
        for name in numerical_code+native_code:assert (ROOT/name).read_text()==helper.io(refinement_capture/'source'/name).read_text(),name
        environment=module('numeric_environment',ROOT/'tools/golgi-reference-resume.py').environment()
        assert environment==helper.read(refinement_capture/'preparation-environment.json'),'Qualified numerical dependencies changed.'
    package = ROOT / 'artifacts' / (prefix + stamp + '-' + uuid.uuid4().hex[:6])
    backup = Path('D:/DNNE-desktop-runs/UnityWorld') / package.name
    assert not package.exists() and not backup.exists(); package.mkdir()
    subprocess.run(['git', 'archive', '--format=zip', '--output=' + str(package / 'source.zip'), revision], cwd=ROOT, check=True)
    with ZipFile(package / 'source.zip') as archive:
        for entry in archive.infolist():
            path = PurePosixPath(entry.filename)
            assert not path.is_absolute() and '..' not in path.parts and ':' not in entry.filename and '\\' not in entry.filename
            assert ((entry.external_attr >> 16) & 0o170000) != 0o120000
            target = helper.io(package / 'source' / path)
            if entry.is_dir():
                target.mkdir(parents=True, exist_ok=True)
            else:
                target.parent.mkdir(parents=True, exist_ok=True)
                with target.open('xb') as stream:
                    stream.write(archive.read(entry))
    (package / 'reference').mkdir()
    (package / 'unity-metadata.json').write_text(json.dumps(metadata.validate(package / 'source/UnityWorld'), indent=2) + '\n')
    shutil.copyfile(review_folder / 'review.json', package / 'reference/native-hinge-review.json')
    shutil.copyfile(muscle_review_folder / 'review.json', package / 'reference/causal-motor-review.json')
    shutil.copyfile(muscle_capture / 'causal-input.json', package / 'reference/muscle-input.json')
    shutil.copyfile(failure_review_folder / 'review.json', package / 'reference/failed-bridge-review.json')
    shutil.copyfile(read_review_folder / 'review.json', package / 'reference/read-failure-review.json')
    for name, source in failure_inputs.items(): shutil.copyfile(failure_capture / source, package / 'reference' / name)
    if neuronal:
        for name in source_names:shutil.copyfile(source_capture/name,package/name)
        shutil.copyfile(source_review_folder/'review.json',package/'reference/source-review.json')
        shutil.copyfile(refinement_review_folder/'review.json',package/'reference/hinge-refinement-review.json')
        shutil.copyfile(refinement_capture/'qualified/hinge-input.json',package/'reference/qualified-hinge-input.json')
        shutil.copyfile(refinement_capture/'preparation-environment.json',package/'reference/qualified-environment.json')
        (package/'preparation-environment.json').write_text(json.dumps(environment,indent=2)+'\n')
    if refinement:
        shutil.copyfile(comparison_review/'review.json',package/'reference/hinge-comparison-review.json')
        shutil.copyfile(comparison/'backup-manifest.json',package/'reference/qualified-comparison-manifest.json')
        for name in selected:
            target=helper.io(package/'qualified'/name);target.parent.mkdir(parents=True,exist_ok=True)
            shutil.copyfile(helper.io(comparison/name),target);assert helper.digest(target)==helper.digest(comparison/name)
        for condition in CONDITIONS:
            folder=package/'conditions'/condition['key'];(folder/'reference').mkdir(parents=True)
            for name in ('muscle-input.json','failed-request-000033.json','last-reply-000032.json'):
                shutil.copyfile(package/'reference'/name,folder/'reference'/name)
            options={k:condition[k] for k in ('physicsResolution','muscleSubsteps')}
            (folder/'run-options.json').write_text(json.dumps(options,indent=2)+'\n')
        (package/'preparation-environment.json').write_text(json.dumps(environment,indent=2)+'\n')
    shutil.copyfile(package / 'source/tools/backup-unity-run.ps1', package / 'backup-run.ps1')
    files = [dict(Path=p.relative_to(helper.io(package)).as_posix(), Sha256=helper.digest(p))
             for p in sorted(helper.io(package).rglob('*')) if p.is_file()]
    dotnet = shutil.which('dotnet'); assert dotnet
    config = dict(Revision=revision, PreparedUtc=datetime.now(timezone.utc).isoformat(), PackageFiles=files,
        DotnetExecutable=dotnet, BackupDestination=str(backup), NoLiveMonitoring=True,
        ExpectedTestCases=sum(counts.values()), ExpectedClassCounts=counts, ExpectedTestMethods=methods,
        TestFilter='|'.join('FullyQualifiedName~' + name for name in counts),
        IsolatedNativeMuscleBridge=True, GeneratedUnitySource=True, QualifiedCouplingCorePreserved=True,
        IsolatedNativeRefinement=refinement,
        IsolatedNeuronalNative=neuronal,
        LiveBodySelected=False, NoTimingMeasured=True, NativeUnityQualified=False,
        PhysiologicalCalibrationQualified=False, AvatarRecoveryAssessed=False)
    unity = Path('C:/Program Files/Unity/Hub/Editor/6000.3.19f1/Editor/Unity.exe')
    assert unity.is_file(), 'Pinned Unity installation required.'
    workspace = ROOT / 'artifacts' / (package.name + '-build-work')
    assert not workspace.exists(), 'Fresh build workspace required.'
    config.update(UnityExecutable=str(unity), UnityExecutableSha256=helper.digest(unity),
        UnityBuildWorkspace=str(workspace), ExpectedUnityVersion='6000.3.19f1',
        SessionId=uuid.uuid4().hex, ExpectedNativeCases=12, ExpectedNativePhysicsSteps=4096,
        ExactScalarWireVersion=2, ExpectedExactScalarRoundTrips=1040, RequireOriginalDecimalFailureReproduction=True,
        RequireCompletedReplyLockControl=True,
        PythonExecutable=sys.executable, PythonExecutableSha256=helper.digest(sys.executable))
    if refinement:config.update(RefinementConditions=CONDITIONS,RefinementCriteria=CRITERIA,ExpectedNativeCases=60,ExpectedNativePhysicsSteps=26624)
    if neuronal:config.update(ExpectedNativeCases=7,ExpectedNativePhysicsSteps=14336,ExpectedSourceConditions=5,ExpectedIndependentReferences=12)
    (package / 'configuration.json').write_text(json.dumps(config, indent=2) + '\n')
    pointer = dict(Package=str(package), Backup=str(backup), Revision=revision, PreparedOnly=True, NoLiveMonitoring=True,
        Launcher=str(package / 'source/tools/start-native-motor-bridge-window.ps1'))
    (package.parent / (package.name + '-prepared.json')).write_text(json.dumps(pointer, indent=2) + '\n')
    print(json.dumps(pointer, indent=2))


if __name__ == '__main__':
    parser=argparse.ArgumentParser(description=__doc__);group=parser.add_mutually_exclusive_group()
    group.add_argument('--refinement',action='store_true');group.add_argument('--neuronal',action='store_true')
    args=parser.parse_args();main(args.refinement,args.neuronal)
