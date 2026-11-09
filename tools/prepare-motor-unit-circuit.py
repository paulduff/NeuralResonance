"""Freeze the isolated somatic-source/muscle experiment only after a passing reviewed motor comparison."""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import shutil
import subprocess
import uuid
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[1]
read = lambda p: json.loads(p.read_text(encoding='utf-8-sig'))


def main(review_folder):
    spec = importlib.util.spec_from_file_location('motor_package_helpers', ROOT / 'tools/prepare-motor-boundary.py')
    helpers = importlib.util.module_from_spec(spec); spec.loader.exec_module(helpers)
    digest, verify, git = helpers.digest, helpers.verify, helpers.git
    assert not git('status', '--porcelain'), 'Commit before freezing.'
    revision = git('rev-parse', 'HEAD'); assert revision == git('rev-parse', 'origin/main'), 'Push before freezing.'
    verify(review_folder, ['review.json']); review = read(review_folder / 'review.json')
    assert review['operatorReportedCompletion'] and review['savedAnalysisReproduced']
    assert review['passedFocusedTests'] == 12 and review['analysis']['numericalCriteriaPassed']
    capture = Path(review['capture'])
    assert digest(capture / 'backup-manifest.json') == review['manifestSha256']
    selected = {'boundary-analysis.json': 'reference/boundary-analysis.json',
                'boundary-input.json': 'reference/boundary-input.json',
                'boundary-study/results.json': 'reference/source-results.json',
                'boundary-study/protocol.json': 'reference/source-metadata.json'}
    for name in ('quiet', 'soma-6000'):
        selected['boundary-study/' + name + '-h0.003125.csv'] = 'reference/' + name + '-h0.003125.csv'
    neuronal = ['Structures/_SharedRuntime/MotoneuronReferenceCell.cs', 'Structures/_SharedRuntime/MotoneuronChannelKinetics.cs',
                'tools/MotoneuronReferenceProbe/ClockAudit.cs', 'tools/MotoneuronReferenceProbe/ActiveStudy.cs']
    entries = verify(capture, list(selected) + ['source/' + n for n in neuronal])
    for name in neuronal:
        assert (ROOT / name).read_text(encoding='utf-8-sig') == (capture / 'source' / name).read_text(encoding='utf-8-sig'), name
    transport_review = ROOT / 'artifacts/olive-motor-review-20261109'
    verify(transport_review, ['olive-review.json']); transport = read(transport_review / 'olive-review.json')
    assert transport['passedTests'] == 87 and transport['savedBytesReverified']
    old_transport = Path(transport['capture'])
    assert digest(old_transport / 'backup-manifest.json') == transport['manifestSha256']
    body = ['Protocol/MotorAxonProtocol.cs', 'Protocol/MotorAxonPublisher.cs', 'Protocol/MotorAxonReplayBuffer.cs',
            'src/NRE.SimAvatar/AvatarMotorAxonReceiver.cs', 'src/NRE.SimAvatar/AvatarMotorUnitCalcium.cs',
            'src/NRE.SimAvatar/AvatarIsometricMotorUnitPlant.cs']
    verify(old_transport, ['source/' + n for n in body])
    for name in body:
        assert (ROOT / name).read_text(encoding='utf-8-sig') == (old_transport / 'source' / name).read_text(encoding='utf-8-sig'), name
    calcium = ROOT / 'artifacts/motor-calcium-20261106-7cde4b'
    calcium_names = {'muscle-input/modelParameter.mat': 'muscle-input/modelParameter.mat',
                     'source/tools/motor-calcium-reference.py': 'reference-source/motor-calcium-reference.py',
                     'calcium-reference/comparison.json': 'reference/earlier-calcium-comparison.json'}
    calcium_entries = verify(calcium, list(calcium_names))
    assert read(calcium / 'calcium-reference/comparison.json')['numericalCriteriaPassed']
    stamp = datetime.now(timezone.utc).strftime('%Y%m%d')
    package = ROOT / 'artifacts' / ('motor-unit-circuit-' + stamp + '-' + uuid.uuid4().hex[:6])
    backup = Path('D:/DNNE-desktop-runs/UnityWorld') / package.name
    assert not package.exists() and not backup.exists(); package.mkdir()
    subprocess.run(['git', 'archive', '--format=zip', '--output=' + str(package / 'source.zip'), revision], cwd=ROOT, check=True)
    with ZipFile(package / 'source.zip') as archive:
        assert all(not Path(n).is_absolute() and '..' not in Path(n).parts for n in archive.namelist())
        archive.extractall(package / 'source')
    copied = []
    for origin, selection, verified in ((capture, selected, entries), (calcium, calcium_names, calcium_entries)):
        for source, target in selection.items():
            destination = package / target; destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(origin / source, destination)
            assert digest(destination) == verified[source]['Sha256']
            copied.append(dict(origin=str(origin), source=source, target=target, sha256=digest(destination)))
    (package / 'provenance.json').write_text(json.dumps(dict(motorReview=review, transportReview=transport,
        reviewManifestSha256=digest(review_folder / 'backup-manifest.json'),
        transportReviewManifestSha256=digest(transport_review / 'backup-manifest.json'),
        calciumManifestSha256=digest(calcium / 'backup-manifest.json'), copiedFiles=copied,
        sourceEquationsAndSchedulerUnchanged=True, bodyComponentsUnchanged=True), indent=2) + '\n', encoding='utf-8')
    shutil.copy2(package / 'source/tools/backup-unity-run.ps1', package / 'backup-run.ps1')
    files = [dict(Path=p.relative_to(package).as_posix(), Sha256=digest(p)) for p in sorted(package.rglob('*')) if p.is_file()]
    dotnet = shutil.which('dotnet'); python = ROOT / 'artifacts/numerical-reference-env-20261102/Scripts/python.exe'
    assert dotnet and python.is_file()
    # 32 original transport + 16 replay + 13 calcium + 15 plant + 11 motor + 1 manifest.
    classes = ['MotorAxonTransportTests', 'MotorAxonReplayBufferTests', 'MotorUnitCalciumTests',
               'IsometricMotorUnitPlantTests', 'MotoneuronReferenceTests', 'SolutionManifestTests']
    config = dict(Revision=revision, PreparedUtc=datetime.now(timezone.utc).isoformat(), PackageFiles=files,
        DotnetExecutable=dotnet, PythonExecutable=str(python), BackupDestination=str(backup), NoLiveMonitoring=True,
        SourceNumericalReviewPassed=True, ExpectedConditions=8, ExpectedTestCases=88, TestClasses=classes,
        TestFilter='|'.join('FullyQualifiedName~' + c for c in classes))
    (package / 'configuration.json').write_text(json.dumps(config, indent=2) + '\n', encoding='utf-8')
    pointer = dict(Package=str(package), Backup=str(backup), Revision=revision, PreparedOnly=True,
                   NoLiveMonitoring=True, Launcher=str(package / 'source/tools/start-motor-unit-circuit-window.ps1'))
    (ROOT / 'artifacts' / ('motor-unit-circuit-prepared-' + stamp + '.json')).write_text(json.dumps(pointer, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(pointer, indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('--motor-review', type=Path, required=True)
    args = parser.parse_args(); main(args.motor_review.resolve())
