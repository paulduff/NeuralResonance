"""Freeze the native body -> existing sensory cell -> nucleus experiment."""
import argparse
from datetime import datetime, timezone
import importlib.util
from pathlib import Path, PurePosixPath
import shutil
import subprocess
import sys
import uuid
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT/'tools'))
BASE = '35bdd86'
NATIVE = Path('D:/DNNE-build-work/declared-body-mechanics-20261123-203560')
SENSORY = Path('D:/DNNE-build-work/head-turn-afferent-20261120-2e0ed3')


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    value = importlib.util.module_from_spec(spec); spec.loader.exec_module(value)
    return value


h = module('native_canal_head_helpers', ROOT/'tools/native_articulated_head_study.py')
read, write, digest = h.read, h.write, h.digest


def prepare():
    assert not h.h.git('status', '--porcelain'), 'Commit clean source first.'
    revision = h.h.git('rev-parse', 'HEAD')
    assert revision == h.h.git('rev-parse', 'origin/main')
    changed = set(h.h.git('diff', '--name-only', BASE, revision, '--', 'src', 'Structures', 'ControlProgram',
                        'tests', 'Shared.Contracts', 'Protocol', 'UnityWorld', 'tools/NativeMotorBridgeProbe').splitlines())
    assert changed == {'tools/NativeMotorBridgeProbe/Program.cs', 'tools/NativeMotorBridgeProbe/ArticulatedCanalNativeStudy.cs'}, changed
    name = 'native-canal-'+datetime.now(timezone.utc).strftime('%Y%m%d')+'-'+uuid.uuid4().hex[:6]
    package = Path('D:/DNNE-build-work')/name; backup = Path('D:/DNNE-desktop-runs/UnityWorld')/name
    assert not package.exists() and not backup.exists(); package.mkdir()
    for label, run in [('native', NATIVE.name), ('sensory', 'head-turn-reanalysis-20261120-2859f6')]:
        review = ROOT/'artifacts'/(run+'-review'); proof = read(review/'review.json')
        assert proof['operatorReportedCompletion']
        if label == 'native':
            assert proof['passedComponentCases'] == 341 and proof['headReceipts'] == 324 and proof['exactOriginalQuietReplays']
        else:
            assert proof['savedAccountingCriteriaReconciled'] and proof['originalFailurePreserved'] and proof['sourceSteps'] == 2654208
        h.protected_copy(review, package/(label+'-review'), [e['Path'].replace('\\', '/') for e in read(review/'backup-manifest.json')['Files']])
    subprocess.run(['git', 'archive', '--format=zip', '--output='+str(package/'source.zip'), revision], cwd=ROOT, check=True)
    with ZipFile(package/'source.zip') as archive:
        for entry in archive.infolist():
            path = PurePosixPath(entry.filename)
            assert not path.is_absolute() and '..' not in path.parts and ':' not in entry.filename and '\\' not in entry.filename
            assert ((entry.external_attr >> 16) & 0o170000) != 0o120000
            target = package/'source'/path
            if entry.is_dir():
                target.mkdir(parents=True, exist_ok=True)
            else:
                target.parent.mkdir(parents=True, exist_ok=True)
                with target.open('xb') as stream: stream.write(archive.read(entry))
    player_names = [Path(e['Path']).relative_to(NATIVE).as_posix() for e in read(NATIVE/'player-files.json')]
    assert player_names and all(n.startswith('player/') for n in player_names)
    source_names = [p.relative_to(ROOT).as_posix() for p in (ROOT/'UnityWorld/Assets/NRE').rglob('*.cs')]
    for name in source_names:
        assert (ROOT/name).read_text(encoding='utf-8-sig') == (NATIVE/'source'/name).read_text(encoding='utf-8-sig'), name
    h.protected_copy(NATIVE, package/'qualified-player', player_names+['player-files.json']+['source/'+n for n in source_names])
    h.protected_copy(NATIVE, package/'native-evidence', ['configuration.json', 'analysis.json']+
        [f'conditions/no-joint-friction/native/{condition}/{file}' for condition in ('quiet', 'hip', 'shoulder')
         for file in ('input.json', 'acknowledged.jsonl')])
    h.protected_copy(NATIVE, package/'component-evidence', ['managed-runtime-before.json'])
    h.protected_copy(SENSORY, package/'sensory-evidence', ['connected-input.json', 'study/quiet-n16384/initial.json'])
    shutil.copyfile(package/'sensory-evidence/connected-input.json', package/'connected-input.json')
    shutil.copyfile(package/'source/tools/backup-unity-run.ps1', package/'backup-run.ps1')
    python = Path(sys.executable); dotnet = Path(shutil.which('dotnet'))
    files = [dict(Path=p.relative_to(package).as_posix(), Sha256=digest(p)) for p in sorted(package.rglob('*')) if p.is_file()]
    write(package/'configuration.json', dict(Revision=revision, PackageFiles=files, BackupDestination=str(backup),
        NoLiveMonitoring=True, NativeCanalStudy=True, Visible=True, PlayerBuildReused=True, ComponentCasesReused=341,
        PlayerRelativePath='qualified-player/player/DNNE Unity World.exe',
        OriginalRelativePath='native-evidence/conditions/no-joint-friction/native',
        ExpectedCases=6, ExpectedPhysicsSteps=48, ExpectedEndpoints=54, ExpectedHeadReceipts=162,
        ExpectedSourceSteps=516096, ExpectedPackets=1008, SourceStepsPerInterval=[8192, 16384],
        MechanicsMode='no-joint-friction', OriginalSensoryFixturePreserved=True,
        PythonExecutable=str(python), PythonExecutableSha256=digest(python), PythonRuntime=h.h.python_runtime(),
        DotnetExecutable=str(dotnet), DotnetExecutableSha256=digest(dotnet),
        IndependentConnectedAccuracyQualified=False, PhysiologicalFiringQualified=False, NeuronalMotorAuthority=False, AvatarStandingQualified=False))
    preflight(package)
    import json
    print(json.dumps(dict(Package=str(package), Backup=str(backup), Revision=revision,
                         Launcher=str(package/'source/tools/start-native-canal-window.ps1')), indent=2), flush=True)


def preflight(package):
    cfg = read(package/'configuration.json')
    assert cfg['NoLiveMonitoring'] and cfg['NativeCanalStudy'] and cfg['Visible'] and cfg['PlayerBuildReused']
    assert cfg['ComponentCasesReused'] == 341 and cfg['OriginalSensoryFixturePreserved']
    assert cfg['SourceStepsPerInterval'] == [8192, 16384] and cfg['MechanicsMode'] == 'no-joint-friction'
    assert [cfg[k] for k in ('ExpectedCases', 'ExpectedPhysicsSteps', 'ExpectedEndpoints', 'ExpectedHeadReceipts', 'ExpectedSourceSteps', 'ExpectedPackets')] == [6, 48, 54, 162, 516096, 1008]
    for key in ('IndependentConnectedAccuracyQualified', 'PhysiologicalFiringQualified', 'NeuronalMotorAuthority', 'AvatarStandingQualified'):
        assert cfg[key] is False
    for entry in cfg['PackageFiles']:
        assert digest(package/entry['Path']) == entry['Sha256'], entry['Path']
    for name in ('Python', 'Dotnet'):
        assert digest(cfg[name+'Executable']) == cfg[name+'ExecutableSha256']
    assert cfg['PythonRuntime'] == h.h.python_runtime()
    assert digest(package/'connected-input.json') == digest(package/'sensory-evidence/connected-input.json')
    print('Qualified native player, original sensory fixture, biological kernels and runtime preserved; no model/test executed by preflight.', flush=True)


def compare(package):
    from native_canal_analysis import analyse
    result = analyse(package)
    write(package/'analysis.json', result)
    if not result['connectedAccountingPassed']:
        raise RuntimeError('Connected sensory accounting failed; preserve every trace and original bound.')
    print('Six actual body/sensory/nucleus cases reconciled: 48 physical steps, 162 head receipts and 516,096 sensory steps. Independent accuracy and standing remain unqualified.', flush=True)


if __name__ == '__main__':
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    parser = argparse.ArgumentParser()
    parser.add_argument('stage', choices=('prepare', 'preflight', 'runtime', 'compare'))
    parser.add_argument('package', nargs='?', type=Path)
    args = parser.parse_args()
    if args.stage == 'prepare': prepare()
    else:
        assert args.package is not None
        {'preflight': preflight, 'runtime': h.runtime, 'compare': compare}[args.stage](args.package.resolve())
