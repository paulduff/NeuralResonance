"""Freeze isolated receptor component checks; no model/test execution here."""
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path, PurePosixPath
import re
import shutil
import subprocess
import uuid
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[1]
COUNTS = dict(MotorReceptorContactTests=14, MotoneuronReferenceTests=11,
    MotorAxonTransportTests=32, MotorAxonReplayBufferTests=16, SolutionManifestTests=1)


def main():
    spec = importlib.util.spec_from_file_location('packaging', ROOT / 'tools/prepare-causal-motor.py')
    helper = importlib.util.module_from_spec(spec); spec.loader.exec_module(helper)
    io, read, digest, verify, git = helper.io, helper.read, helper.digest, helper.verify, helper.git
    assert not git('status', '--porcelain'), 'Commit clean source first.'
    revision = git('rev-parse', 'HEAD'); assert revision == git('rev-parse', 'origin/main'), 'Push first.'
    evidence = ROOT / 'artifacts/motor-receptor-source-20261116'
    manifest = read(evidence / 'backup-manifest.json')
    evidence_names = [e['Path'].replace('\\', '/') for e in manifest['Files']]
    verify(evidence, evidence_names)
    source = read(evidence / 'source-manifest.json')
    assert source['commit'] == '937ed5d0d04519d4cd7af474a0f6c53a6e437e06' and len(source['files']) == 3
    assert read(evidence / 'authoring-review.json')['prospectiveTestCases'] == 14
    reviewed = ROOT / 'artifacts/observed-motor-source-review-20261116'
    verify(reviewed, ['review.json']); review = read(reviewed / 'review.json')
    assert review['operatorReportedCompletion']
    capture = Path(review['capture']); assert digest(capture / 'backup-manifest.json') == review['manifestSha256']
    preserved = ['Structures/_SharedRuntime/MotoneuronReferenceCell.cs', 'Structures/_SharedRuntime/MotoneuronChannelKinetics.cs',
        'Protocol/MotorAxonPublisher.cs', 'Protocol/MotorAxonReplayBuffer.cs', 'src/NRE.SimAvatar/AvatarMotorAxonReceiver.cs']
    verify(capture, ['source/' + name for name in preserved])
    for name in preserved:
        assert (ROOT / name).read_text(encoding='utf-8-sig') == io(capture / 'source' / name).read_text(encoding='utf-8-sig'), name
    methods = {}
    for cls, count in COUNTS.items():
        code = (ROOT / 'tests/NeuralResonanceEngine.DNNE.Tests' / (cls + '.cs')).read_text(encoding='utf-8-sig')
        found = re.findall(r'((?:\s*\[[^\n]+\])+\s*)public\s+(?:async\s+)?(?:void|Task)\s+(\w+)\(', code)
        members = {name: 1 if '[Fact]' in attrs else attrs.count('[InlineData(')
                   for attrs, name in found if '[Fact]' in attrs or '[Theory]' in attrs}
        assert sum(members.values()) == count and all(members.values()), cls
        methods.update({'NeuralResonanceEngine.DNNE.Tests.' + cls + '.' + name: n for name, n in members.items()})
    package = ROOT / 'artifacts' / ('motor-receptor-' + datetime.now(timezone.utc).strftime('%Y%m%d') + '-' + uuid.uuid4().hex[:6])
    backup = Path('D:/DNNE-desktop-runs/UnityWorld') / package.name
    assert not package.exists() and not backup.exists(); package.mkdir()
    subprocess.run(['git', 'archive', '--format=zip', '--output=' + str(package / 'source.zip'), revision], cwd=ROOT, check=True)
    with ZipFile(package / 'source.zip') as archive:
        for entry in archive.infolist():
            path = PurePosixPath(entry.filename)
            assert not path.is_absolute() and '..' not in path.parts and ':' not in entry.filename and '\\' not in entry.filename
            assert ((entry.external_attr >> 16) & 0o170000) != 0o120000
            target = io(package / 'source' / path)
            if entry.is_dir(): target.mkdir(parents=True, exist_ok=True)
            else:
                target.parent.mkdir(parents=True, exist_ok=True)
                with target.open('xb') as stream: stream.write(archive.read(entry))
    reference = package / 'reference'; reference.mkdir()
    for name in evidence_names:
        target = io(reference / 'receptor-source' / name); target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(io(evidence / name), target)
    shutil.copyfile(reviewed / 'review.json', reference / 'motor-source-review.json')
    (reference / 'core-preservation.json').write_text(json.dumps(dict(capture=str(capture),
        sourceManifestSha256=review['manifestSha256'], preservedFiles=[dict(path=n, currentSha256=digest(ROOT / n),
        qualifiedSha256=digest(capture / 'source' / n), normalizedTextExactlyEqual=True) for n in preserved]), indent=2) + '\n')
    shutil.copyfile(package / 'source/tools/backup-unity-run.ps1', package / 'backup-run.ps1')
    files = [dict(Path=p.relative_to(io(package)).as_posix(), Sha256=digest(p)) for p in sorted(io(package).rglob('*')) if p.is_file()]
    configuration = dict(Revision=revision, PreparedUtc=datetime.now(timezone.utc).isoformat(), PackageFiles=files,
        DotnetExecutable=shutil.which('dotnet'), BackupDestination=str(backup), NoLiveMonitoring=True, NoTimingMeasured=True,
        IsolatedMotorReceptor=True, QualifiedMotorCorePreserved=True, ExpectedTestCases=sum(COUNTS.values()),
        ExpectedClassCounts=COUNTS, ExpectedTestMethods=methods, TestFilter='|'.join('FullyQualifiedName~' + c for c in COUNTS),
        LiveBodySelected=False, IndependentReceptorReferenceQualified=False, PhysiologicalCalibrationQualified=False,
        AvatarRecoveryAssessed=False)
    assert configuration['DotnetExecutable']
    (package / 'configuration.json').write_text(json.dumps(configuration, indent=2) + '\n')
    pointer = dict(Package=str(package), Backup=str(backup), Revision=revision, PreparedOnly=True, NoLiveMonitoring=True,
        Launcher=str(package / 'source/tools/start-motor-receptor-window.ps1'))
    (package.parent / (package.name + '-prepared.json')).write_text(json.dumps(pointer, indent=2) + '\n')
    print(json.dumps(pointer, indent=2), flush=True)


if __name__ == '__main__': main()
