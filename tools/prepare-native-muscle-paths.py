"""Freeze native Unity muscle path force checks after verified analytic geometry."""
import argparse
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
COUNTS = dict(MusclePathGeometryTests=31, CausalMotorPlantTests=27, DynamicMuscleTests=16, SolutionManifestTests=1)


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec); spec.loader.exec_module(result); return result


def main():
    helper = module('package_helpers', ROOT / 'tools/prepare-causal-motor.py')
    assert not helper.git('status', '--porcelain'), 'Commit clean source first.'
    revision = helper.git('rev-parse', 'HEAD'); assert revision == helper.git('rev-parse', 'origin/main'), 'Push first.'
    review_folder = ROOT / 'artifacts/muscle-path-review-20261113'
    helper.verify(review_folder, ['review.json']); review = helper.read(review_folder / 'review.json')
    assert review['operatorReportedCompletion'] and review['exactGeneratedSourceVerified']
    assert review['passedTests'] == 75 and review['passedGeometryCases'] == 31
    capture = Path(review['capture'])
    assert helper.digest(capture / 'backup-manifest.json') == review['manifestSha256']
    same = ['src/NRE.SimAvatar/AvatarCausalMotorPlant.cs', 'src/NRE.SimAvatar/AvatarDynamicMuscle.cs',
            'src/NRE.SimAvatar/AvatarMusclePath.cs', 'UnityWorld/Assets/NRE/Runtime/MusclePathGeometry.cs']
    helper.verify(capture, ['source/' + name for name in same])
    for name in same:
        assert (ROOT / name).read_text(encoding='utf-8-sig') == helper.io(capture / 'source' / name).read_text(encoding='utf-8-sig')
    generated = module('unity_path_generator', ROOT / 'tools/prepare-unity-muscle-paths.py').generate(ROOT)
    assert generated == (ROOT / 'UnityWorld/Assets/NRE/Runtime/MusclePathGeometry.cs').read_text(encoding='utf-8-sig')
    methods = {}
    for cls, count in COUNTS.items():
        code = (ROOT / 'tests/NeuralResonanceEngine.DNNE.Tests' / (cls + '.cs')).read_text(encoding='utf-8-sig')
        found = re.findall(r'((?:\s*\[[^\n]+\])+\s*)public\s+(?:async\s+)?(?:void|Task)\s+(\w+)\(', code)
        members = {name: (1 if '[Fact]' in attrs else attrs.count('[InlineData('))
                   for attrs, name in found if '[Fact]' in attrs or '[Theory]' in attrs}
        assert sum(members.values()) == count and all(members.values()), cls
        methods.update({'NeuralResonanceEngine.DNNE.Tests.' + cls + '.' + name: n for name, n in members.items()})
    stamp = datetime.now(timezone.utc).strftime('%Y%m%d')
    package = ROOT / 'artifacts' / ('native-muscle-paths-' + stamp + '-' + uuid.uuid4().hex[:6])
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
    shutil.copyfile(review_folder / 'review.json', package / 'reference/muscle-path-review.json')
    shutil.copyfile(package / 'source/tools/backup-unity-run.ps1', package / 'backup-run.ps1')
    files = [dict(Path=p.relative_to(helper.io(package)).as_posix(), Sha256=helper.digest(p))
             for p in sorted(helper.io(package).rglob('*')) if p.is_file()]
    dotnet = shutil.which('dotnet'); assert dotnet
    config = dict(Revision=revision, PreparedUtc=datetime.now(timezone.utc).isoformat(), PackageFiles=files,
        DotnetExecutable=dotnet, BackupDestination=str(backup), NoLiveMonitoring=True,
        ExpectedTestCases=sum(COUNTS.values()), ExpectedClassCounts=COUNTS, ExpectedTestMethods=methods,
        TestFilter='|'.join('FullyQualifiedName~' + name for name in COUNTS),
        IsolatedNativeMusclePaths=True, GeneratedUnitySource=True, QualifiedCouplingCorePreserved=True,
        LiveBodySelected=False, NoTimingMeasured=True, NativeUnityQualified=False,
        PhysiologicalCalibrationQualified=False, AvatarRecoveryAssessed=False)
    unity = Path('C:/Program Files/Unity/Hub/Editor/6000.3.19f1/Editor/Unity.exe')
    assert unity.is_file(), 'Pinned Unity installation required.'
    workspace = ROOT / 'artifacts' / (package.name + '-build-work')
    assert not workspace.exists(), 'Fresh build workspace required.'
    config.update(UnityExecutable=str(unity), UnityExecutableSha256=helper.digest(unity),
        UnityBuildWorkspace=str(workspace), ExpectedUnityVersion='6000.3.19f1',
        SessionId=uuid.uuid4().hex, ExpectedNativeCases=24, ExpectedNativePhysicsSteps=48)
    (package / 'configuration.json').write_text(json.dumps(config, indent=2) + '\n')
    pointer = dict(Package=str(package), Backup=str(backup), Revision=revision, PreparedOnly=True, NoLiveMonitoring=True,
        Launcher=str(package / 'source/tools/start-native-muscle-paths-window.ps1'))
    (package.parent / (package.name + '-prepared.json')).write_text(json.dumps(pointer, indent=2) + '\n')
    print(json.dumps(pointer, indent=2))


if __name__ == '__main__':
    argparse.ArgumentParser(description=__doc__).parse_args(); main()
