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


def configure(closed_loop=False, articulated=False):
    closed_loop = closed_loop or articulated
    helper.COUNTS = dict(COUNTS)
    if closed_loop:
        helper.COUNTS.update(ClosedLoopMotorScheduleTests=14, CausalMotorPlantTests=27,
                            DynamicMuscleTests=16, MusclePathGeometryTests=31)
    if articulated:
        helper.COUNTS.update(ArticulatedTendonBridgeTests=34, MuscleHingeClockTests=18)
        return 295
    return 243 if closed_loop else 155


def prepare(closed_loop=False, articulated=False):
    closed_loop = closed_loop or articulated
    total = configure(closed_loop, articulated)
    assert not helper.git('status', '--porcelain'), 'Commit clean source first.'
    revision = helper.git('rev-parse', 'HEAD')
    assert revision == helper.git('rev-parse', 'origin/main'), 'Push first.'
    methods = helper.inventory(); assert sum(methods.values()) == total
    prefix = 'tendon-wire-' if articulated else 'motor-closure-' if closed_loop else 'motor-epoch-'
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
    files = [dict(Path=p.relative_to(package).as_posix(), Sha256=digest(p)) for p in sorted(package.rglob('*')) if p.is_file()]
    dotnet = Path(shutil.which('dotnet')); python = Path(sys.executable)
    write(package/'configuration.json', dict(Revision=revision, PackageFiles=files, BackupDestination=str(backup),
        PythonExecutable=str(python), PythonExecutableSha256=digest(python), PythonRuntime=helper.python_runtime(),
        DotnetExecutable=str(dotnet), DotnetExecutableSha256=digest(dotnet), NoLiveMonitoring=True,
        FreshMotorEpochRegression=True, ClosedLoopMotorScheduleRegression=closed_loop,
        ArticulatedTendonBridgeRegression=articulated,
        ExpectedTestCases=total, NewClockCases=0 if closed_loop else 13, NewScheduleCases=14 if closed_loop and not articulated else 0,
        NewBridgeCases=34 if articulated else 0, ExistingRegressionCases=261 if articulated else 229 if closed_loop else 142,
        ExpectedTestMethods=methods, TestFilter='|'.join('FullyQualifiedName~'+name for name in helper.COUNTS),
        OriginalCaptureAccessed=False, NativeUnityExecuted=False, LiveBrainAccessed=False, OldSavesLoaded=False,
        IndependentCoupledTrajectoryQualified=False, PhysiologicalFiringQualified=False, AvatarBalanceQualified=False))
    preflight(package)
    print(json.dumps(dict(Package=str(package), Backup=str(backup), Revision=revision,
        Launcher=str(package/'source/tools/start-fresh-motor-epoch-window.ps1')), indent=2), flush=True)


def preflight(package):
    cfg = read(package/'configuration.json')
    closed_loop = cfg.get('ClosedLoopMotorScheduleRegression', False)
    articulated = cfg.get('ArticulatedTendonBridgeRegression', False)
    assert not articulated or closed_loop
    total = configure(closed_loop, articulated)
    assert cfg['NoLiveMonitoring'] and cfg['FreshMotorEpochRegression'] and cfg['ExpectedTestCases'] == total
    assert cfg['NewClockCases'] == (0 if closed_loop else 13) and cfg['ExistingRegressionCases'] == (261 if articulated else 229 if closed_loop else 142)
    assert cfg.get('NewScheduleCases', 0) == (14 if closed_loop and not articulated else 0)
    assert cfg.get('NewBridgeCases', 0) == (34 if articulated else 0)
    for key in ('OriginalCaptureAccessed', 'NativeUnityExecuted', 'LiveBrainAccessed', 'OldSavesLoaded',
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
    total = configure(closed_loop, articulated)
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
    if articulated: analysis.update(newBridgeCases=34, hostProtocolChecksPassed=True, nativeFullBodyBridgeQualified=False)
    write(package/'analysis.json', analysis)
    print(f'All {total} clock/schedule and existing regression cases passed. Whole-loop timing, physiological firing and standing remain separate.', flush=True)


if __name__ == '__main__':
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    parser = argparse.ArgumentParser(); parser.add_argument('stage', choices=('prepare','preflight','runtime','check-tests'))
    parser.add_argument('package', nargs='?', type=Path); parser.add_argument('--closed-loop', action='store_true')
    parser.add_argument('--articulated', action='store_true'); args = parser.parse_args()
    if args.stage == 'prepare': prepare(args.closed_loop, args.articulated)
    else:
        assert not args.closed_loop and not args.articulated, 'The frozen configuration selects the protocol after preparation.'
        assert args.package is not None
        {'preflight':preflight, 'runtime':runtime, 'check-tests':check_tests}[args.stage](args.package.resolve())
