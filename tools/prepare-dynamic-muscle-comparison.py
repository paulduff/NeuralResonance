"""Freeze the coupled comparison only after the reported component review."""
import argparse
from datetime import datetime, timezone
from hashlib import sha256
import importlib.util
import json
import os
from pathlib import Path, PurePosixPath
import shutil
import subprocess
import uuid
from zipfile import ZipFile
from dynamic_muscle_protocol import make_protocol

ROOT = Path(__file__).resolve().parents[1]
read = lambda p: json.loads(p.read_text(encoding='utf-8-sig'))


def io(path):
    value = str(Path(path).resolve())
    return Path('\\\\?\\' + value) if os.name == 'nt' and not value.startswith('\\\\?\\') else Path(value)


def main(review_folder):
    module_spec = importlib.util.spec_from_file_location('package_helpers', ROOT / 'tools/prepare-motor-boundary.py')
    helper = importlib.util.module_from_spec(module_spec); module_spec.loader.exec_module(helper)
    verify, digest, git = helper.verify, helper.digest, helper.git
    assert not git('status', '--porcelain'), 'Commit clean source first.'
    revision = git('rev-parse', 'HEAD'); assert revision == git('rev-parse', 'origin/main'), 'Push first.'
    verify(review_folder, ['muscle-review.json'])
    component = read(review_folder / 'muscle-review.json')
    assert component['operatorReportedCompletion'] and component['passedTests'] == 93 and component['newComponentTests'] == 16
    capture = Path(component['capture'])
    assert digest(capture / 'backup-manifest.json') == component['manifestSha256']
    same = ['src/NRE.SimAvatar/AvatarDynamicMuscle.cs', 'src/NRE.SimAvatar/AvatarMotorUnitCalcium.cs',
        'src/NRE.SimAvatar/AvatarIsometricMotorUnitPlant.cs', 'src/NRE.SimAvatar/AvatarMotorAxonReceiver.cs',
        'tests/NeuralResonanceEngine.DNNE.Tests/DynamicMuscleTests.cs']
    verify(capture, ['source/' + name for name in same])
    for name in same:
        assert (ROOT / name).read_text(encoding='utf-8-sig') == (capture / 'source' / name).read_text(encoding='utf-8-sig'), name
    source_review = ROOT / 'artifacts/motor-unit-circuit-review-20261109'
    verify(source_review, ['review.json']); source = read(source_review / 'review.json')
    assert source['operatorReportedCompletion'] and source['savedAnalysisExactlyReproduced'] and source['passedFocusedTests'] == 88
    assert source['analysis']['numericalCriteriaPassed'] and source['analysis']['originalEventsPreserved']
    original = Path(source['capture']); assert digest(original / 'backup-manifest.json') == source['manifestSha256']
    original_names = {'circuit-study/packet-10/arrivals.jsonl': 'arrivals.jsonl',
        'circuit-study/packet-10/muscle.csv': 'isometric.csv'}
    verify(original, list(original_names))
    reference = ROOT / 'artifacts/motor-mechanics-source-20261113'
    verify(reference, ['review.json', 'upstream.zip'])
    reviewed = read(reference / 'review.json')
    assert reviewed['upstreamRevision'] == 'c317be803f01965a1ca8fc4cbad1ba9156433af8'
    stamp = datetime.now(timezone.utc).strftime('%Y%m%d')
    package = ROOT / 'artifacts' / ('dynamic-muscle-comparison-' + stamp + '-' + uuid.uuid4().hex[:6])
    backup = Path('D:/DNNE-desktop-runs/UnityWorld') / package.name
    assert not package.exists() and not backup.exists(); package.mkdir()
    subprocess.run(['git', 'archive', '--format=zip', '--output=' + str(package / 'source.zip'), revision], cwd=ROOT, check=True)
    with ZipFile(package / 'source.zip') as archive:
        for entry in archive.infolist():
            path = PurePosixPath(entry.filename)
            assert not path.is_absolute() and '..' not in path.parts and ':' not in entry.filename and '\\' not in entry.filename
            assert ((entry.external_attr >> 16) & 0o170000) != 0o120000
            target = io(package / 'source' / path)
            if entry.is_dir():
                target.mkdir(parents=True, exist_ok=True)
            else:
                target.parent.mkdir(parents=True, exist_ok=True)
                with target.open('xb') as stream:
                    stream.write(archive.read(entry))
    destination = package / 'reference'; destination.mkdir()
    shutil.copyfile(review_folder / 'muscle-review.json', destination / 'component-review.json')
    shutil.copyfile(source_review / 'review.json', destination / 'neuronal-source-review.json')
    shutil.copyfile(reference / 'review.json', destination / 'author-source-review.json')
    shutil.copyfile(reference / 'upstream.zip', destination / 'upstream.zip')
    for name, target in original_names.items():
        shutil.copyfile(original / name, destination / target)
        assert digest(destination / target) == digest(original / name)
    with ZipFile(reference / 'upstream.zip') as archive:
        data = archive.read('modelParameter.mat')
        assert sha256(data).hexdigest() == reviewed['parameterSha256']
        (destination / 'modelParameter.mat').write_bytes(data)
    spec = make_protocol(destination / 'modelParameter.mat', destination / 'arrivals.jsonl')
    (package / 'dynamic-input.json').write_text(json.dumps(spec, indent=2, allow_nan=False) + '\n')
    shutil.copyfile(package / 'source/tools/backup-unity-run.ps1', package / 'backup-run.ps1')
    files = [dict(Path=p.relative_to(io(package)).as_posix(), Sha256=digest(p)) for p in sorted(io(package).rglob('*')) if p.is_file()]
    counts = dict(DynamicMuscleTests=16, MotorUnitCalciumTests=13, IsometricMotorUnitPlantTests=15,
        MotorAxonTransportTests=32, MotorAxonReplayBufferTests=16, SolutionManifestTests=1)
    python = ROOT / 'artifacts/numerical-reference-env-20261102/Scripts/python.exe'; dotnet = shutil.which('dotnet')
    assert python.is_file() and dotnet
    config = dict(Revision=revision, PreparedUtc=datetime.now(timezone.utc).isoformat(), PackageFiles=files,
        DotnetExecutable=dotnet, PythonExecutable=str(python), BackupDestination=str(backup), NoLiveMonitoring=True,
        ExpectedTestCases=93, ExpectedClassCounts=counts, TestFilter='|'.join('FullyQualifiedName~' + name for name in counts),
        LiveBodySelected=False, IsolatedDynamicMuscle=True, NoTimingMeasured=True, ComponentReviewPassed=True,
        ExpectedCandidateConditions=33, ExpectedReferenceSolves=33, ExpectedSourceOrderingAttempts=15,
        ExpectedReferenceSamplingTests=3, ExpectedReferenceSamplingMethodChecks=6)
    (package / 'configuration.json').write_text(json.dumps(config, indent=2) + '\n')
    pointer = dict(Package=str(package), Backup=str(backup), Revision=revision, PreparedOnly=True, NoLiveMonitoring=True,
        Launcher=str(package / 'source/tools/start-dynamic-muscle-comparison-window.ps1'))
    (package.parent / (package.name + '-prepared.json')).write_text(json.dumps(pointer, indent=2) + '\n')
    print(json.dumps(pointer, indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('--component-review', type=Path, required=True)
    args = parser.parse_args(); main(args.component_review.resolve())
