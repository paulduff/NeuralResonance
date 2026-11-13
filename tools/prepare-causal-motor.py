"""Freeze the causal motor/body component checks after qualified muscle evidence."""
import argparse
from datetime import datetime, timezone
from hashlib import sha256
import json
import os
from pathlib import Path, PurePosixPath
import re
import shutil
import subprocess
import uuid
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[1]
COUNTS = dict(CausalMotorPlantTests=27, DynamicMuscleTests=16, MotorUnitCalciumTests=13,
              IsometricMotorUnitPlantTests=15, MotorAxonTransportTests=32,
              MotorAxonReplayBufferTests=16, SolutionManifestTests=1)
read = lambda p: json.loads(io(p).read_text(encoding='utf-8-sig'))
git = lambda *a: subprocess.check_output(['git', *a], cwd=ROOT, encoding='utf-8').strip()


def io(path):
    path = str(Path(path).resolve())
    return Path('\\\\?\\' + path) if os.name == 'nt' and not path.startswith('\\\\?\\') else Path(path)


def digest(path):
    h = sha256()
    with io(path).open('rb') as stream:
        for block in iter(lambda: stream.read(4 * 1024 * 1024), b''):
            h.update(block)
    return h.hexdigest().upper()


def verify(folder, names):
    manifest = read(folder / 'backup-manifest.json'); backup = Path(manifest['Destination'])
    assert digest(folder / 'backup-manifest.json') == digest(backup / 'backup-manifest.json')
    entries = {e['Path'].replace('\\', '/'): e for e in manifest['Files']}
    for name in names:
        for root in (folder, backup):
            assert digest(root / name) == entries[name]['Sha256'], name


def main(review_folder):
    assert not git('status', '--porcelain'), 'Commit clean source first.'
    revision = git('rev-parse', 'HEAD'); assert revision == git('rev-parse', 'origin/main'), 'Push first.'
    verify(review_folder, ['review.json']); review = read(review_folder / 'review.json')
    assert review['operatorReportedCompletion'] and review['savedAnalysisExactlyReproduced']
    assert review['passedTests'] == 93 and review['passedAnalyticBoundaryTests'] == 3 and review['passedBoundaryMethodChecks'] == 6
    assert review['analysis']['completed'] and review['analysis']['numericalCriteriaPassed']
    capture = Path(review['capture'])
    assert digest(capture / 'backup-manifest.json') == review['manifestSha256']
    same = ['src/NRE.SimAvatar/AvatarMotorUnitCalcium.cs', 'src/NRE.SimAvatar/AvatarMotorAxonReceiver.cs',
            'src/NRE.SimAvatar/AvatarIsometricMotorUnitPlant.cs',
            'tests/NeuralResonanceEngine.DNNE.Tests/DynamicMuscleTests.cs']
    muscle = 'src/NRE.SimAvatar/AvatarDynamicMuscle.cs'
    verify(capture, ['source/' + name for name in same + [muscle]])
    for name in same:
        assert (ROOT / name).read_text(encoding='utf-8-sig') == io(capture / 'source' / name).read_text(encoding='utf-8-sig'), name
    # The ONLY delta permitted in the qualified muscle is this explicitly
    # isolated same-time boundary observation. Match its full source verbatim;
    # removing a broad regex block could hide changes to the actual solver.
    addition = '''    // Explicit partition boundary: physics has completed at this SAME time.
    // Change only the observed total path length; retain every fiber/calcium
    // state and ledger. The owner must qualify splitting error and account for
    // physical boundary work. This is not future-frame interpolation or an ODE
    // advancement, and it is never called by the prescribed-reference rig.
    public void ObserveCompletedBodyLength(double physicalTimeMs, double lengthMeters)
    {
        RequireOperational();
        if (physicalTimeMs != TimeMs || !double.IsFinite(lengthMeters) || lengthMeters <= 0)
            throw new ArgumentException("Physical geometry must belong to the muscle's completed time.");
        _ = Forces(_state, lengthMeters); // Validate before changing the boundary.
        _length = lengthMeters;
    }

'''
    code = (ROOT / muscle).read_text(encoding='utf-8-sig')
    assert code.count(addition) == 1 and code.replace(addition, '') == io(capture / 'source' / muscle).read_text(encoding='utf-8-sig'), 'Qualified muscle core changed.'
    methods = {}
    for cls, count in COUNTS.items():
        code = (ROOT / 'tests/NeuralResonanceEngine.DNNE.Tests' / (cls + '.cs')).read_text(encoding='utf-8-sig')
        found = re.findall(r'((?:\s*\[[^\n]+\])+\s*)public\s+(?:async\s+)?(?:void|Task)\s+(\w+)\(', code)
        members = {name: (1 if '[Fact]' in attrs else attrs.count('[InlineData('))
                   for attrs, name in found if '[Fact]' in attrs or '[Theory]' in attrs}
        assert sum(members.values()) == count and all(members.values()), cls
        methods.update({'NeuralResonanceEngine.DNNE.Tests.' + cls + '.' + name: n for name, n in members.items()})
    stamp = datetime.now(timezone.utc).strftime('%Y%m%d')
    package = ROOT / 'artifacts' / ('causal-motor-' + stamp + '-' + uuid.uuid4().hex[:6])
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
    shutil.copyfile(review_folder / 'review.json', package / 'reference/muscle-comparison-review.json')
    shutil.copyfile(capture / 'source' / muscle, package / 'reference/qualified-AvatarDynamicMuscle.cs')
    (package / 'reference/core-preservation.json').write_text(json.dumps(dict(
        QualifiedMuscleSha256=digest(capture / 'source' / muscle), CandidateMuscleSha256=digest(ROOT / muscle),
        ExactQualifiedCorePreserved=True, SoleAddition=addition), indent=2) + '\n')
    shutil.copyfile(package / 'source/tools/backup-unity-run.ps1', package / 'backup-run.ps1')
    files = [dict(Path=p.relative_to(io(package)).as_posix(), Sha256=digest(p))
             for p in sorted(io(package).rglob('*')) if p.is_file()]
    dotnet = shutil.which('dotnet'); assert dotnet
    config = dict(Revision=revision, PreparedUtc=datetime.now(timezone.utc).isoformat(), PackageFiles=files,
        DotnetExecutable=dotnet, BackupDestination=str(backup), NoLiveMonitoring=True,
        ExpectedTestCases=sum(COUNTS.values()), ExpectedClassCounts=COUNTS, ExpectedTestMethods=methods,
        TestFilter='|'.join('FullyQualifiedName~' + name for name in COUNTS),
        IsolatedCausalMotor=True, QualifiedMuscleCorePreserved=True, LiveBodySelected=False, NoTimingMeasured=True,
        IndependentCoupledReferenceQualified=False, PhysiologicalCalibrationQualified=False, AvatarRecoveryAssessed=False)
    (package / 'configuration.json').write_text(json.dumps(config, indent=2) + '\n')
    pointer = dict(Package=str(package), Backup=str(backup), Revision=revision, PreparedOnly=True, NoLiveMonitoring=True,
        Launcher=str(package / 'source/tools/start-causal-motor-window.ps1'))
    (package.parent / (package.name + '-prepared.json')).write_text(json.dumps(pointer, indent=2) + '\n')
    print(json.dumps(pointer, indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('--muscle-review', type=Path, required=True)
    args = parser.parse_args(); main(args.muscle_review.resolve())
