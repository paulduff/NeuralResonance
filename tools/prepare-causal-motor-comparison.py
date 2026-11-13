"""Freeze the full coupled comparison after verified component/numerical reviews."""
import argparse
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

for name in ('OPENBLAS_NUM_THREADS', 'OMP_NUM_THREADS', 'MKL_NUM_THREADS', 'NUMEXPR_NUM_THREADS'):
    os.environ[name] = '1'
ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'tools'))
from causal_motor_protocol import make_protocol


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    value = importlib.util.module_from_spec(spec); spec.loader.exec_module(value)
    return value


def main(component_review):
    helper = module('component_packaging', ROOT / 'tools/prepare-causal-motor.py')
    io, read, digest, verify, git = helper.io, helper.read, helper.digest, helper.verify, helper.git
    assert not git('status', '--porcelain'), 'Commit clean source first.'
    revision = git('rev-parse', 'HEAD'); assert revision == git('rev-parse', 'origin/main'), 'Push first.'
    verify(component_review, ['review.json']); component = read(component_review / 'review.json')
    assert component['operatorReportedCompletion'] and component['passedTests'] == 120
    assert component['newCausalTests'] == 27 and component['exactQualifiedCorePreserved']
    capture = Path(component['capture']); assert digest(capture / 'backup-manifest.json') == component['manifestSha256']
    same = ['src/NRE.SimAvatar/' + name for name in ('AvatarCausalMotorPlant.cs', 'AvatarDynamicMuscle.cs',
        'AvatarMotorUnitCalcium.cs', 'AvatarIsometricMotorUnitPlant.cs', 'AvatarMotorAxonReceiver.cs')]
    same += ['tests/NeuralResonanceEngine.DNNE.Tests/' + name + '.cs' for name in helper.COUNTS]
    same += ['Protocol/MotorAxonPublisher.cs', 'Protocol/MotorAxonProtocol.cs', 'Shared.Contracts/PhysicalSampleProtocol.cs']
    verify(capture, ['source/' + name for name in same])
    for name in same:
        assert (ROOT / name).read_text(encoding='utf-8-sig') == io(capture / 'source' / name).read_text(encoding='utf-8-sig'), name
    component_config = read(capture / 'configuration.json')
    assert component_config['ExpectedClassCounts'] == helper.COUNTS
    assert component_config['ExpectedTestMethods'] == component['expectedTestMethods']

    muscle_review = ROOT / 'artifacts/dynamic-muscle-comparison-review-20261113'
    verify(muscle_review, ['review.json']); muscle = read(muscle_review / 'review.json')
    assert muscle['operatorReportedCompletion'] and muscle['savedAnalysisExactlyReproduced']
    assert muscle['passedTests'] == 93 and muscle['analysis']['numericalCriteriaPassed']
    original = Path(muscle['capture']); assert digest(original / 'backup-manifest.json') == muscle['manifestSha256']
    evidence = ['reference/modelParameter.mat', 'reference/arrivals.jsonl', 'reference/author-source-review.json',
                'reference/neuronal-source-review.json', 'reference-preflight.json']
    reference_code = ['tools/dynamic_muscle_protocol.py', 'tools/dynamic_muscle_reference.py']
    verify(original, evidence + ['source/' + name for name in reference_code])
    for name in reference_code:
        assert (ROOT / name).read_text() == io(original / 'source' / name).read_text(), name
    environment = module('numerical_environment', ROOT / 'tools/golgi-reference-resume.py').environment()
    assert environment == read(original / 'reference-preflight.json')['environment'], 'Qualified numerical dependencies changed.'
    declared = make_protocol(original / 'reference/modelParameter.mat', original / 'reference/arrivals.jsonl')
    assert len(declared['cases']) == 8 and len(declared['resolutions']) == 5 and len(declared['referenceSettings']) == 3
    stamp = datetime.now(timezone.utc).strftime('%Y%m%d')
    package = ROOT / 'artifacts' / ('causal-motor-comparison-' + stamp + '-' + uuid.uuid4().hex[:6])
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
    (package / 'reference').mkdir()
    for name in evidence:
        destination = package / ('reference/qualified-numerical-preflight.json' if name == 'reference-preflight.json' else name)
        shutil.copyfile(original / name, destination); assert digest(destination) == digest(original / name)
    shutil.copyfile(component_review / 'review.json', package / 'reference/causal-component-review.json')
    shutil.copyfile(muscle_review / 'review.json', package / 'reference/muscle-comparison-review.json')
    (package / 'causal-input.json').write_text(json.dumps(declared, indent=2, allow_nan=False) + '\n')
    (package / 'preparation-environment.json').write_text(json.dumps(environment, indent=2) + '\n')
    shutil.copyfile(package / 'source/tools/backup-unity-run.ps1', package / 'backup-run.ps1')
    files = [dict(Path=p.relative_to(io(package)).as_posix(), Sha256=digest(p))
             for p in sorted(io(package).rglob('*')) if p.is_file()]
    dotnet = shutil.which('dotnet'); assert dotnet
    config = dict(Revision=revision, PreparedUtc=datetime.now(timezone.utc).isoformat(), PackageFiles=files,
        DotnetExecutable=dotnet, PythonExecutable=sys.executable, BackupDestination=str(backup), NoLiveMonitoring=True,
        ExpectedTestCases=120, ExpectedClassCounts=helper.COUNTS, ExpectedTestMethods=component['expectedTestMethods'],
        TestFilter=component_config['TestFilter'], IsolatedCausalMotorComparison=True,
        ComponentReviewPassed=True, QualifiedMuscleCorePreserved=True, LiveBodySelected=False, NoTimingMeasured=True,
        IndependentCoupledReferenceQualified=False, PhysiologicalCalibrationQualified=False, AvatarRecoveryAssessed=False,
        ExpectedCandidateConditions=40, ExpectedReferenceSolves=24, ExpectedAnalyticMethodChecks=4, ExpectedTendonQuadratureCases=4)
    (package / 'configuration.json').write_text(json.dumps(config, indent=2) + '\n')
    pointer = dict(Package=str(package), Backup=str(backup), Revision=revision, PreparedOnly=True, NoLiveMonitoring=True,
        Launcher=str(package / 'source/tools/start-causal-motor-comparison-window.ps1'))
    (package.parent / (package.name + '-prepared.json')).write_text(json.dumps(pointer, indent=2) + '\n')
    print(json.dumps(pointer, indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('--component-review', type=Path, required=True)
    args = parser.parse_args(); main(args.component_review.resolve())
