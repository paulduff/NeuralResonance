"""Freeze and reconcile the declared fresh motor-clock regression workflow."""
import argparse
from collections import Counter
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path, PurePosixPath
import shutil
import subprocess
import sys
import uuid
import xml.etree.ElementTree as ET
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('epoch_package_helpers', ROOT/'tools/native_avatar_tendon_study.py')
helper = importlib.util.module_from_spec(spec); spec.loader.exec_module(helper)
COUNTS = dict(FreshMotorEpochTests=13, MotoneuronReferenceTests=11, MotoneuronCrossingTests=8,
    MotorReceptorContactTests=14, ReceptorDrivenMotoneuronTests=15, ReceptorMotorSourceTests=19,
    DescendingMotorPoolTests=18, DeclaredDescendingProjectionTests=8,
    MotorAxonTransportTests=32, MotorAxonReplayBufferTests=16, SolutionManifestTests=1)
helper.COUNTS = COUNTS
read, write, digest = helper.read, helper.write, helper.digest


def unity_inputs():
    spec = importlib.util.spec_from_file_location('epoch_unity_inputs', ROOT/'tools/unity_metadata.py')
    result = importlib.util.module_from_spec(spec); spec.loader.exec_module(result)
    return result


def population_initial():
    spec = importlib.util.spec_from_file_location('epoch_population_initial', ROOT/'tools/muscle_population_initial.py')
    result = importlib.util.module_from_spec(spec); spec.loader.exec_module(result)
    return result


def initial_balance_reference():
    spec = importlib.util.spec_from_file_location('initial_balance_decimal_reference', ROOT/'tools/muscle_initial_balance_reference.py')
    result = importlib.util.module_from_spec(spec); spec.loader.exec_module(result)
    return result


def configure(closed_loop=False, articulated=False, attachment=False, head=False, initial_balance=False, frame_map=False, named_muscles=False, population=False):
    named_muscles = named_muscles or population
    if named_muscles: assert not (initial_balance or frame_map)
    head = head or named_muscles
    initial_balance = initial_balance or frame_map
    if initial_balance: assert not (articulated or attachment or head)
    attachment = attachment or head
    articulated = articulated or attachment
    closed_loop = closed_loop or articulated or initial_balance
    helper.COUNTS = dict(COUNTS)
    if closed_loop:
        helper.COUNTS.update(ClosedLoopMotorScheduleTests=14, CausalMotorPlantTests=27,
                            DynamicMuscleTests=16, MusclePathGeometryTests=31)
    if initial_balance:
        helper.COUNTS.update(MuscleInitialBalanceTests=20)
        if frame_map: helper.COUNTS.update(MuscleAnatomicalFrameTests=25)
        return 288 if frame_map else 263
    if articulated:
        helper.COUNTS.update(ArticulatedTendonBridgeTests=34, MuscleHingeClockTests=18)
        if attachment:
            helper.COUNTS.update(PassiveMotorAttachmentTests=20)
            if head:
                helper.COUNTS.update(NativeArticulatedHeadInputTests=16)
                if named_muscles: helper.COUNTS.update(NamedMuscleBridgeTests=28)
                if population: helper.COUNTS.update(MuscleInitialBalanceTests=20)
                return 379 if population else 359 if named_muscles else 331
            return 315
        return 295
    return 243 if closed_loop else 155


def prepare(closed_loop=False, articulated=False, attachment=False, head=False, initial_balance=False, frame_map=False, named_muscles=False, population=False):
    named_muscles = named_muscles or population
    head = head or named_muscles
    initial_balance = initial_balance or frame_map
    attachment = attachment or head
    articulated = articulated or attachment
    closed_loop = closed_loop or articulated or initial_balance
    total = configure(closed_loop, articulated, attachment, head, initial_balance, frame_map, named_muscles, population)
    assert not helper.git('status', '--porcelain'), 'Commit clean source first.'
    revision = helper.git('rev-parse', 'HEAD')
    assert revision == helper.git('rev-parse', 'origin/main'), 'Push first.'
    balance_declaration = None; balance_runtime = None
    named_declaration = None; unity = None
    if named_muscles:
        changed = sorted(helper.git('diff','--name-only','7002e56',revision,'--','src','Protocol','Shared.Contracts','Structures',
            'ControlProgram','UnityWorld','tests','tools/NativeMotorBridgeProbe').splitlines())
        expected = sorted(['UnityWorld/Assets/NRE/Runtime/'+name+'.cs' for name in
            ('ArticulatedTendonWire','ArticulatedMusclePathOwner','ArticulatedTendonBridge')] +
            ['tools/NativeMotorBridgeProbe/NativeArticulatedBody.cs','tools/NativeMotorBridgeProbe/NamedMuscleNativeStudy.cs',
             'tools/NativeMotorBridgeProbe/Program.cs','tools/NativeMotorBridgeProbe/MusclePopulationInitialStudy.cs',
             'tests/NeuralResonanceEngine.DNNE.Tests/NamedMuscleBridgeTests.cs'])
        assert changed == expected, changed
        named_declaration = dict(baseRevision=helper.git('rev-parse','7002e56'), changedFiles=changed,
                                 originalBiologicalKernelsAndTestsUnchanged=True)
        unity = Path(read(Path('D:/DNNE-build-work/declared-body-mechanics-20261123-203560/configuration.json'))['UnityExecutable'])
        assert unity.is_file()
    if initial_balance:
        changed = sorted(helper.git('diff', '--name-only', '05a1ed5', revision, '--',
            'src', 'Protocol', 'Shared.Contracts', 'Structures', 'ControlProgram', 'UnityWorld', 'tests').splitlines())
        expected_changed = ['src/NRE.SimAvatar/AvatarMuscleInitialBalance.cs',
                            'tests/NeuralResonanceEngine.DNNE.Tests/MuscleInitialBalanceTests.cs']
        if frame_map: expected_changed += ['src/NRE.SimAvatar/AvatarMuscleFrameMap.cs',
                            'tests/NeuralResonanceEngine.DNNE.Tests/MuscleAnatomicalFrameTests.cs']
        assert changed == sorted(expected_changed), changed
        balance_runtime = initial_balance_reference().runtime()
        balance_declaration = dict(baseRevision=helper.git('rev-parse', '05a1ed5'), changedRuntimeFiles=changed,
                                  originalKernelsAndExistingTestsUnchanged=True)
    methods = helper.inventory(); assert sum(methods.values()) == total
    prefix = 'muscle-population-initial-' if population else 'named-muscle-native-' if named_muscles else 'muscle-frames-' if frame_map else 'muscle-initial-' if initial_balance else 'articulated-head-' if head else 'motor-attachment-' if attachment else 'tendon-wire-' if articulated else 'motor-closure-' if closed_loop else 'motor-epoch-'
    name = prefix+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6]
    package = Path('D:/DNNE-build-work')/name; backup = Path('D:/DNNE-desktop-runs/UnityWorld')/name
    assert not package.exists() and not backup.exists(); package.mkdir()
    subprocess.run(['git', 'archive', '--format=zip', '--output='+str(package/'source.zip'), revision], cwd=ROOT, check=True)
    with ZipFile(package/'source.zip') as archive:
        for item in archive.infolist():
            path = PurePosixPath(item.filename)
            assert not path.is_absolute() and '..' not in path.parts and ':' not in item.filename and '\\' not in item.filename
            assert ((item.external_attr >> 16) & 0o170000) != 0o120000
            target = package/'source'/path
            if item.is_dir(): target.mkdir(parents=True, exist_ok=True)
            else:
                target.parent.mkdir(parents=True, exist_ok=True)
                with target.open('xb') as stream: stream.write(archive.read(item))
    shutil.copyfile(package/'source/tools/backup-unity-run.ps1', package/'backup-run.ps1')
    if named_muscles:
        metadata = unity_inputs()
        write(package/'unity-avatar-inputs.json',metadata.freeze_avatar_assets(ROOT/'UnityWorld',package/'source/UnityWorld'))
        write(package/'unity-metadata.json',metadata.validate(package/'source/UnityWorld'))
    if frame_map or named_muscles:
        spec = importlib.util.spec_from_file_location('frame_input_preservation', ROOT/'tools/native_articulated_head_study.py')
        preservation = importlib.util.module_from_spec(spec); spec.loader.exec_module(preservation)
        input_source = ROOT/'artifacts/lower-leg-anatomy-input-20261123'
        names = [e['Path'].replace('\\','/') for e in read(input_source/'backup-manifest.json')['Files']] if named_muscles else ['binding-input.json']
        preservation.protected_copy(input_source, package/'anatomical-input', names)
        if named_muscles:
            reviewed = ROOT/'artifacts/muscle-frames-20261123-581e1f-review'
            proof = read(reviewed/'review.json')
            assert proof['operatorReportedCompletion'] and proof['frameArithmeticPassed'] and proof['passedTests']==288 and proof['originalPoints']==44
            preservation.protected_copy(reviewed,package/'frame-review',[e['Path'].replace('\\','/') for e in read(reviewed/'backup-manifest.json')['Files']])
    if population:
        population_initial().prepare(package)
        balance_runtime = initial_balance_reference().runtime()
    files = [dict(Path=p.relative_to(package).as_posix(), Sha256=digest(p)) for p in sorted(package.rglob('*')) if p.is_file()]
    dotnet = Path(shutil.which('dotnet')); python = Path(sys.executable)
    write(package/'configuration.json', dict(Revision=revision, PackageFiles=files, BackupDestination=str(backup),
        PythonExecutable=str(python), PythonExecutableSha256=digest(python), PythonRuntime=helper.python_runtime(),
        DotnetExecutable=str(dotnet), DotnetExecutableSha256=digest(dotnet), NoLiveMonitoring=True,
        FreshMotorEpochRegression=True, ClosedLoopMotorScheduleRegression=closed_loop,
        ArticulatedTendonBridgeRegression=articulated,
        PassiveMotorAttachmentRegression=attachment, ArticulatedHeadInputRegression=head,
        MuscleInitialBalanceRegression=initial_balance, NewInitialBalanceCases=20 if initial_balance else 0,
        MuscleFrameMapRegression=frame_map, NewFrameCases=25 if frame_map else 0,
        NamedMuscleRegression=named_muscles, NewNamedMuscleCases=28 if named_muscles else 0, NamedMuscleChangeDeclaration=named_declaration,
        UnityExecutable=str(unity) if unity else None,UnityExecutableSha256=digest(unity) if unity else None,
        UnityBuildWorkspace=str(Path('D:/DNNE-build-work')/(name+'-unity-build')) if named_muscles else None,
        NativeNamedMuscleStudy=named_muscles and not population,NamedNativeCases=12 if named_muscles and not population else 0,
        MusclePopulationInitialStudy=population,PopulationInitialCases=4 if population else 0,
        AvatarBuildInputChecks=8 if named_muscles else 0,
        NamedNativeLimits=dict(forceNewtons=1e-10,pathLengthMeters=2e-12,nativePointMeters=2e-6,responseRadians=1e-6) if named_muscles else None,
        InitialBalanceChangeDeclaration=balance_declaration, InitialBalanceReferenceRuntime=balance_runtime,
        ExpectedTestCases=total, NewClockCases=0 if closed_loop else 13, NewScheduleCases=14 if closed_loop and not articulated and not initial_balance else 0,
        NewBridgeCases=34 if articulated and not attachment else 0, NewAttachmentCases=20 if attachment and not head else 0, NewHeadCases=16 if head and not named_muscles else 0,
        ExistingRegressionCases=351 if population else 331 if named_muscles else 243 if initial_balance else 315 if head else 295 if attachment else 261 if articulated else 229 if closed_loop else 142,
        ExpectedTestMethods=methods, TestFilter='|'.join('FullyQualifiedName~'+name for name in helper.COUNTS),
        OriginalCaptureAccessed=named_muscles, NativeUnityExecuted=False, LiveBrainAccessed=False, OldSavesLoaded=False,
        IndependentCoupledTrajectoryQualified=False, PhysiologicalFiringQualified=False, AvatarBalanceQualified=False))
    preflight(package)
    print(json.dumps(dict(Package=str(package), Backup=str(backup), Revision=revision,
        Launcher=str(package/'source/tools/start-fresh-motor-epoch-window.ps1')), indent=2), flush=True)


def preflight(package):
    cfg = read(package/'configuration.json')
    closed_loop = cfg.get('ClosedLoopMotorScheduleRegression', False)
    articulated = cfg.get('ArticulatedTendonBridgeRegression', False)
    attachment = cfg.get('PassiveMotorAttachmentRegression', False)
    head = cfg.get('ArticulatedHeadInputRegression', False)
    initial_balance = cfg.get('MuscleInitialBalanceRegression', False)
    frame_map = cfg.get('MuscleFrameMapRegression', False)
    named_muscles = cfg.get('NamedMuscleRegression', False)
    population = cfg.get('MusclePopulationInitialStudy', False)
    assert (not articulated or closed_loop) and (not attachment or articulated) and (not head or attachment)
    total = configure(closed_loop, articulated, attachment, head, initial_balance, frame_map, named_muscles, population)
    assert cfg['NoLiveMonitoring'] and cfg['FreshMotorEpochRegression'] and cfg['ExpectedTestCases'] == total
    assert cfg['NewClockCases'] == (0 if closed_loop else 13) and cfg['ExistingRegressionCases'] == (351 if population else 331 if named_muscles else 243 if initial_balance else 315 if head else 295 if attachment else 261 if articulated else 229 if closed_loop else 142)
    assert cfg.get('NewScheduleCases', 0) == (14 if closed_loop and not articulated and not initial_balance else 0)
    assert cfg.get('NewInitialBalanceCases', 0) == (20 if initial_balance else 0)
    if initial_balance:
        assert closed_loop
        # Initial-condition selection must not silently change any biological,
        # transport, native or existing test equation in this study.
        declared = cfg['InitialBalanceChangeDeclaration']
        assert declared['originalKernelsAndExistingTestsUnchanged'] and declared['baseRevision'].startswith('05a1ed5')
        expected_changed = ['src/NRE.SimAvatar/AvatarMuscleInitialBalance.cs',
                           'tests/NeuralResonanceEngine.DNNE.Tests/MuscleInitialBalanceTests.cs']
        if frame_map: expected_changed += ['src/NRE.SimAvatar/AvatarMuscleFrameMap.cs',
                           'tests/NeuralResonanceEngine.DNNE.Tests/MuscleAnatomicalFrameTests.cs']
        assert declared['changedRuntimeFiles'] == sorted(expected_changed)
        assert cfg['InitialBalanceReferenceRuntime'] == initial_balance_reference().runtime()
    if frame_map:
        assert initial_balance and cfg['NewFrameCases'] == 25
        binding = read(package/'anatomical-input/binding-input.json')
        assert binding['version'] == 'paired-lower-leg-anatomical-input-v1'
        assert binding['selectedMuscles'] == 10 and binding['selectedPathPoints'] == 44 and not binding['mappedCoordinatesProduced']
    if named_muscles:
        assert head and not initial_balance and not frame_map and cfg['NewNamedMuscleCases'] == 28
        declared = cfg['NamedMuscleChangeDeclaration']
        assert declared['baseRevision'].startswith('7002e56') and declared['originalBiologicalKernelsAndTestsUnchanged']
        assert declared['changedFiles'] == sorted(['UnityWorld/Assets/NRE/Runtime/'+name+'.cs' for name in
            ('ArticulatedTendonWire','ArticulatedMusclePathOwner','ArticulatedTendonBridge')] +
            ['tools/NativeMotorBridgeProbe/NativeArticulatedBody.cs','tools/NativeMotorBridgeProbe/NamedMuscleNativeStudy.cs',
             'tools/NativeMotorBridgeProbe/Program.cs','tools/NativeMotorBridgeProbe/MusclePopulationInitialStudy.cs',
             'tests/NeuralResonanceEngine.DNNE.Tests/NamedMuscleBridgeTests.cs'])
        assert digest(cfg['UnityExecutable']) == cfg['UnityExecutableSha256']
        assert cfg['NativeNamedMuscleStudy']==(not population) and cfg['NamedNativeCases']==(0 if population else 12)
        assert cfg['AvatarBuildInputChecks']==8
        metadata = unity_inputs()
        assert read(package/'unity-avatar-inputs.json')==metadata.avatar_assets(package/'source/UnityWorld')
        assert read(package/'unity-metadata.json')==metadata.validate(package/'source/UnityWorld')
        assert cfg['NamedNativeLimits']==dict(forceNewtons=1e-10,pathLengthMeters=2e-12,nativePointMeters=2e-6,responseRadians=1e-6)
        proof=read(package/'frame-review/review.json')
        assert proof['operatorReportedCompletion'] and proof['frameArithmeticPassed'] and proof['passedTests']==288 and proof['originalPoints']==44
    if population:
        assert named_muscles and cfg['PopulationInitialCases']==4
        assert cfg['InitialBalanceReferenceRuntime']==initial_balance_reference().runtime()
        population_initial().preflight(package)
    assert cfg.get('NewBridgeCases', 0) == (34 if articulated and not attachment else 0)
    assert cfg.get('NewAttachmentCases', 0) == (20 if attachment and not head else 0)
    assert cfg.get('NewHeadCases', 0) == (16 if head and not named_muscles else 0)
    assert cfg['OriginalCaptureAccessed'] == named_muscles
    for key in ('NativeUnityExecuted', 'LiveBrainAccessed', 'OldSavesLoaded',
                'IndependentCoupledTrajectoryQualified', 'PhysiologicalFiringQualified', 'AvatarBalanceQualified'):
        assert cfg[key] is False, key
    for entry in cfg['PackageFiles']: assert digest(package/entry['Path']) == entry['Sha256'], entry['Path']
    for name in ('Python', 'Dotnet'): assert digest(cfg[name+'Executable']) == cfg[name+'ExecutableSha256']
    assert cfg['PythonRuntime'] == helper.python_runtime(), 'Declared stdlib/binaries changed.'
    assert cfg['ExpectedTestMethods'] == helper.inventory()
    print('Frozen clock/schedule/bridge candidate, exact inventory and dependencies verified; no model/test executed by preflight.', flush=True)


def runtime(package):
    # Both executables select the same framework declarations. App-local files
    # are independently frozen; the probe hashes only selected shared runtimes.
    roots = [package/'source/artifacts'/name/'bin'/project/'release' for name,project in
             [('motor-epoch-tests','NeuralResonanceEngine.DNNE.Tests'),('motor-epoch-runtime','NativeMotorBridgeProbe')]]
    configs = [read(root/'NeuralResonanceEngine.DNNE.Tests.runtimeconfig.json')['runtimeOptions'] for root in roots]
    def frameworks(c): return c.get('frameworks', [c['framework']] if 'framework' in c else [])
    assert frameworks(configs[0]) == frameworks(configs[1]) and frameworks(configs[0])
    for key in ('tfm', 'rollForward', 'applyPatches'):
        assert configs[0].get(key) == configs[1].get(key), key
    captured = read(package/'managed-runtime-before.json')
    assert captured['framework'].startswith('.NET 10.') and captured['directories']
    assert not captured['modelsExecuted'] and not captured['testsExecuted']
    assert captured['hostSha256'] == read(package/'configuration.json')['DotnetExecutableSha256']
    print('Matching declared test/probe frameworks and selected runtime recorded; no scientific equivalence inferred.', flush=True)


def check_tests(package):
    cfg = read(package/'configuration.json'); ns = {'t':'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    closed_loop = cfg.get('ClosedLoopMotorScheduleRegression', False)
    articulated = cfg.get('ArticulatedTendonBridgeRegression', False)
    attachment = cfg.get('PassiveMotorAttachmentRegression', False)
    head = cfg.get('ArticulatedHeadInputRegression', False)
    initial_balance = cfg.get('MuscleInitialBalanceRegression', False)
    frame_map = cfg.get('MuscleFrameMapRegression', False)
    named_muscles = cfg.get('NamedMuscleRegression', False)
    population = cfg.get('MusclePopulationInitialStudy', False)
    total = configure(closed_loop, articulated, attachment, head, initial_balance, frame_map, named_muscles, population)
    tree = ET.parse(package/'test-results/focused.trx')
    definitions = {r.attrib['id']:r.find('t:TestMethod', ns).attrib for r in tree.findall('.//t:UnitTest', ns)}
    rows = tree.findall('.//t:UnitTestResult', ns); actual = Counter()
    for row in rows:
        assert row.attrib['outcome'] == 'Passed', row.attrib
        method = definitions[row.attrib['testId']]
        actual[method['className'].split(',')[0]+'.'+method['name']] += 1
    assert len(rows) == total and dict(actual) == cfg['ExpectedTestMethods'] == helper.inventory()
    analysis = dict(completed=True, clockComponentChecksPassed=True, passedCases=total,
        newClockCases=cfg['NewClockCases'], existingRegressionCases=cfg['ExistingRegressionCases'], exactMethodMultiplicity=True,
        prescribedInputFixtures=True, oldCapturesRetimed=False, nativeUnityExecuted=False,
        independentCoupledTrajectoryQualified=False, physiologicalFiringQualified=False, avatarBalanceQualified=False)
    if closed_loop: analysis.update(newScheduleCases=cfg['NewScheduleCases'], splitScheduleComponentChecksPassed=True, activeSensoryFeedbackQualified=False)
    if articulated: analysis.update(newBridgeCases=cfg['NewBridgeCases'], hostProtocolChecksPassed=True, nativeFullBodyBridgeQualified=False)
    if attachment: analysis.update(newAttachmentCases=cfg.get('NewAttachmentCases',20), physicalInitializationChecksPassed=True, nativePassiveInitializationQualified=False)
    if head: analysis.update(newHeadCases=16, originalHeadInputChecksPassed=True, nativeHeadDeliveryQualified=False)
    if named_muscles: analysis.update(newHeadCases=0,newNamedMuscleCases=28,namedMuscleProtocolChecksPassed=True,nativeNamedMuscleDeliveryQualified=False)
    if initial_balance or population:
        independent = initial_balance_reference().compare(package)
        analysis.update(newInitialBalanceCases=20, initialMuscleBalanceChecksPassed=True,
            independentInitialBalancePassed=independent['passed'], liveMuscleGeometrySelected=False)
    if frame_map:
        spec = importlib.util.spec_from_file_location('frame_independent_comparison', ROOT/'tools/muscle_frame_reference.py')
        reference = importlib.util.module_from_spec(spec); spec.loader.exec_module(reference)
        frames = reference.compare(package)
        analysis.update(newFrameCases=25,declaredAttachmentPoints=44,declaredMuscles=10,
                        independentFrameArithmeticPassed=frames['passed'],anatomicalCalibrationQualified=False)
    write(package/'analysis.json', analysis)
    print(f'All {total} component/regression cases passed. Whole-loop timing, physiological firing and standing remain separate.', flush=True)


def named_compare(package):
    cfg=read(package/'configuration.json');assert cfg['NativeNamedMuscleStudy'] and cfg['NamedMuscleRegression']
    spec=importlib.util.spec_from_file_location('named_native_saved_comparison',ROOT/'tools/named_muscle_native_analysis.py')
    comparison=importlib.util.module_from_spec(spec);spec.loader.exec_module(comparison);comparison.compare(package)


if __name__ == '__main__':
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    parser = argparse.ArgumentParser(); parser.add_argument('stage', choices=('prepare','preflight','runtime','check-tests','named-compare','population-compare'))
    parser.add_argument('package', nargs='?', type=Path); parser.add_argument('--closed-loop', action='store_true')
    parser.add_argument('--articulated', action='store_true'); parser.add_argument('--attachment', action='store_true'); parser.add_argument('--head', action='store_true')
    parser.add_argument('--initial-balance', action='store_true'); parser.add_argument('--frame-map', action='store_true'); parser.add_argument('--named-muscles', action='store_true')
    parser.add_argument('--population-initial', action='store_true'); args = parser.parse_args()
    if args.stage == 'prepare': prepare(args.closed_loop, args.articulated, args.attachment, args.head, args.initial_balance, args.frame_map, args.named_muscles, args.population_initial)
    else:
        assert not args.closed_loop and not args.articulated and not args.attachment and not args.head and not args.initial_balance and not args.frame_map and not args.named_muscles and not args.population_initial, 'The frozen configuration selects the protocol after preparation.'
        assert args.package is not None
        {'preflight':preflight, 'runtime':runtime, 'check-tests':check_tests,'named-compare':named_compare,
         'population-compare':lambda p:population_initial().compare(p)}[args.stage](args.package.resolve())
