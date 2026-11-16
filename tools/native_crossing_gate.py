"""Freeze the optional native crossing gate. Preparation executes no tests/models."""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path, PurePosixPath
import shutil
import subprocess
import uuid
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[1]


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec); spec.loader.exec_module(result)
    return result


gate = module('native_crossing_old_gate', ROOT / 'tools/receptor_membrane_gate.py')
COUNTS = dict(gate.COUPLED, ReceptorMotorSourceTests=19, MotoneuronCrossingTests=8)


def save(path, value):
    path.write_text(json.dumps(value, indent=2, allow_nan=False) + '\n', encoding='utf-8')


def prepare():
    h = gate.helper()
    assert Path(h.git('rev-parse', '--show-toplevel')).resolve() == ROOT.resolve()
    assert not h.git('status', '--porcelain'), 'Commit clean source first.'
    revision = h.git('rev-parse', 'HEAD')
    assert revision == h.git('rev-parse', 'origin/main'), 'Push frozen source first.'
    reviews = {}
    for key, folder in [('membrane', 'receptor-membrane-review-20261116'),
                        ('crossing', 'receptor-crossing-review-20261116')]:
        path = ROOT / 'artifacts' / folder
        h.verify(path, ['review.json'])
        reviews[key] = h.read(path / 'review.json')
        assert reviews[key]['operatorReportedCompletion']
        capture = Path(reviews[key]['capture'])
        assert h.digest(capture / 'backup-manifest.json') == reviews[key]['manifestSha256']
    assert reviews['membrane']['gateCriteriaPassed'] and reviews['membrane']['passedTests'] == 111
    assert reviews['membrane']['originalSourceConditions'] == 5
    crossing = reviews['crossing']
    assert crossing['savedArithmeticReconciled'] and crossing['recordedAnalyticControlsPassed'] == 5
    assert crossing['recordedEndpointPairs'] == 27 and crossing['refinementComparisonsPassed'] == 12
    assert not crossing['originalBoundsChanged'] and not crossing['nativeImplementationQualified']
    baseline = Path(reviews['membrane']['capture'])
    # All reviewed source/reference preservation guards remain intact. These
    # files implement the old input drive, channel equations and delivery path.
    preserved = gate.PRESERVED + [gate.SOURCE_ADAPTER, 'Structures/_SharedRuntime/MotorReceptorContact.cs']
    names = ['reference/' + name for name in gate.REFERENCES] + ['source/' + name for name in preserved]
    h.verify(baseline, names)
    for name in preserved:
        assert (ROOT / name).read_text(encoding='utf-8-sig') == h.io(baseline / 'source' / name).read_text(encoding='utf-8-sig'), name
    methods = gate.inventory(ROOT, COUNTS)
    assert sum(COUNTS.values()) == 124
    package = ROOT / 'artifacts' / ('native-crossing-' + datetime.now(timezone.utc).strftime('%Y%m%d') + '-' + uuid.uuid4().hex[:6])
    backup = Path('D:/DNNE-desktop-runs/UnityWorld') / package.name
    assert not package.exists() and not backup.exists(); package.mkdir()
    subprocess.run(['git', 'archive', '--format=zip', '--output=' + str(package / 'source.zip'), revision], cwd=ROOT, check=True)
    with ZipFile(package / 'source.zip') as archive:
        for entry in archive.infolist():
            name = PurePosixPath(entry.filename)
            assert not name.is_absolute() and '..' not in name.parts and ':' not in entry.filename and '\\' not in entry.filename
            assert ((entry.external_attr >> 16) & 0o170000) != 0o120000
            target = h.io(package / 'source' / name)
            if entry.is_dir(): target.mkdir(parents=True, exist_ok=True)
            else:
                target.parent.mkdir(parents=True, exist_ok=True)
                with target.open('xb') as stream: stream.write(archive.read(entry))
    reference = package / 'reference'; reference.mkdir()
    copied = []
    for name in gate.REFERENCES:
        original = baseline / 'reference' / name
        shutil.copyfile(h.io(original), reference / name)
        copied.append(dict(original=str(original), path='reference/' + name, sha256=h.digest(reference / name)))
    save(package / 'provenance.json', dict(reviews=reviews, copied=copied, preservedSourceFiles=preserved,
        testsExecutedDuringPreparation=False, modelsExecutedDuringPreparation=False,
        newModeIsOptional=True, liveSelectionChanged=False, fullReceptorNumericalMatrixRequired=True))
    shutil.copyfile(package / 'source/tools/backup-unity-run.ps1', package / 'backup-run.ps1')
    files = [dict(Path=p.relative_to(h.io(package)).as_posix(), Sha256=h.digest(p))
             for p in sorted(h.io(package).rglob('*')) if p.is_file()]
    dotnet = shutil.which('dotnet')
    python = Path('C:/Users/duff_/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe')
    assert dotnet and python.is_file()
    save(package / 'configuration.json', dict(Revision=revision, PreparedUtc=datetime.now(timezone.utc).isoformat(),
        PackageFiles=files, DotnetExecutable=dotnet, DotnetExecutableSha256=h.digest(dotnet),
        PythonExecutable=str(python), PythonExecutableSha256=h.digest(python), BackupDestination=str(backup),
        NoLiveMonitoring=True, IsolatedNativeCrossingGate=True, ExpectedConditions=5, ExpectedTestCases=124,
        ExpectedClassCounts=COUNTS, ExpectedTestMethods=methods,
        TestFilter='|'.join('FullyQualifiedName~' + c for c in COUNTS), NativePhysicsRequested=False,
        IndependentCoupledReferenceQualified=False, PhysiologyQualified=False, AvatarRecoveryAssessed=False))
    pointer = dict(Package=str(package), Backup=str(backup), Revision=revision, PreparedOnly=True, NoLiveMonitoring=True,
        Launcher=str(package / 'source/tools/start-native-crossing-window.ps1'))
    save(package.parent / (package.name + '-prepared.json'), pointer)
    print(json.dumps(pointer, indent=2), flush=True)


if __name__ == '__main__':
    if not __debug__: raise RuntimeError('Assertions must remain enabled.')
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=['prepare', 'check-tests'])
    parser.add_argument('package', type=Path, nargs='?')
    args = parser.parse_args()
    if args.command == 'prepare': prepare()
    else:
        assert args.package is not None
        print(json.dumps(gate.check_tests(args.package.resolve(), COUNTS), indent=2), flush=True)
