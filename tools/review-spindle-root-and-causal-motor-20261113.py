"""Review the two operator-reported captures; no tests or model integration."""
from collections import Counter
from datetime import datetime, timezone
from io import BytesIO
import importlib.util
import json
from pathlib import Path
import re
import subprocess
import xml.etree.ElementTree as ET
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[1]


def source_archive(common, package, revision):
    archive = subprocess.check_output(['git', 'archive', '--format=zip', revision], cwd=ROOT)
    count = 0
    with ZipFile(BytesIO(archive)) as git, ZipFile(package / 'source.zip') as frozen:
        assert frozen.comment.decode().strip() == revision and git.namelist() == frozen.namelist()
        for entry in frozen.infolist():
            assert git.read(entry.filename) == frozen.read(entry)
            if not entry.is_dir():
                assert common.io(package / 'source' / entry.filename).read_bytes() == frozen.read(entry)
                count += 1
    return count


def main():
    spec = importlib.util.spec_from_file_location('capture_audit', ROOT / 'tools/review-olive-and-motor-20261109.py')
    common = importlib.util.module_from_spec(spec); spec.loader.exec_module(common)
    package = ROOT / 'artifacts/spindle-state-20261113-ae7ae4'
    revision = 'b60ee5b95908e5c866b5ed83014947bf14dbb0af'
    review = common.verify(package, [], revision, 'FAILED', 1)
    review['verifiedGitFiles'] = source_archive(common, package, revision)
    assert common.read(package / 'runtime-before.json') == common.read(package / 'runtime-after.json') == common.read(package / 'preparation-verification.json')
    base = common.module('frozen_spindle_root_audit', package / 'source/tools/spindle-reference.py')
    candidate, api = base.state_candidate(); assert candidate.verify(package, api) == 18
    roots = common.read(package / 'preflight-root-diagnostics.json')
    assert roots['stage'] == 'preflight' and not roots['boundChanged'] and not roots['kineticsExecuted']
    assert {r['fiber'] for r in roots['fibers']} == {'bag', 'chain'}
    for r in roots['fibers']:
        assert r['initialStatePreserved'] and r['initialState'] == [1] + [0] * 81 + [1, 0]
        assert r['trialStressesPa'] == r['analyticPassiveStressesPa']
        assert r['scalarGuessInfo'] == -6 and r['scalarGuessRootNm'] is None and r['scalarGuessResidualPa'] is None
        assert r['scalarGuessOutput']['intervaliterations'] == 14 and r['scalarGuessOutput']['iterations'] == 0
        assert r['scalarGuessOutput']['funcCount'] == 15 and 'No valid initial bracketing' in r['scalarGuessOutput']['message']
        assert r['bracketInfo'] == 1 and r['bracketError'] == '' and r['bracketResidualPa'] == 0
        assert r['bracketRootNm'] == r['slackLengthNm']
    manifest = common.read(package / 'runtime-files.json')
    name = 'mingw64/share/octave/11.3.0/m/optimization/fzero.m'
    entry = next(e for e in manifest['files'] if e['path'] == name)
    source = Path(common.read(package / 'configuration.json')['RuntimeDirectory']) / name
    assert common.digest(source) == entry['sha256'].upper()
    code = source.read_text()
    assert 'aa = ifelse (a == 0, 0.1, sign (a) * 0.1);' in code
    assert 'for srch = [-.01 +.025 -.05 +.10 -.25 +.50 -1 +2.5 -5 +10 -50 +100 -500 +1000]' in code
    assert 'b = aa + aa*srch;' in code
    locations = [.1 + .1 * x for x in (-.01, .025, -.05, .1, -.25, .5, -1, 2.5, -5, 10, -50, 100, -500, 1000)]
    assert min(locations) == -49.9 and max(locations) == 100.1
    assert all(r['slackLengthNm'] - r['initialLengthNm'] < min(locations) for r in roots['fibers'])
    assert not (package / 'state-controls.json').exists() and not (package / 'mechanics/execution.json').exists()
    review.update(reviewedUtc=datetime.now(timezone.utc).isoformat(), operatorReportedCompletion=True,
        initialRootDiagnostics=roots, runtimePreparationBeforeAfterMatch=True, pinnedFzeroSha256=common.digest(source),
        scalarZeroSearchDisplacementsNm=locations, scalarSearchMissesBothRoots=True,
        bracketedRootsExactWithZeroResidual=True, completedFullStateControls=False, completedTrajectories=0,
        cause='The pinned scalar-start search reaches only -49.9 nm; required displacements are -200 and -50 nm.',
        independentNumericalAccuracyQualified=False, testsExecutedDuringReview=False, modelsSolvedDuringReview=False)
    output = ROOT / 'artifacts/spindle-root-review-20261113'; output.mkdir()
    (output / 'review.json').write_text(json.dumps(review, indent=2, allow_nan=False) + '\n')
    (output / 'pinned-fzero.m').write_bytes(source.read_bytes())
    print('Spindle root: full C:/D: capture and frozen source verified; finite search range explains both failures.', flush=True)
    print(json.dumps({k: review[k] for k in ('verifiedFilesPerCopy', 'verifiedBytesPerCopy', 'verifiedGitFiles', 'manifestSha256')}, indent=2))

    package = ROOT / 'artifacts/causal-motor-20261113-848e50'
    revision = '4c234d51d0134845283ad1f169fc5ce57e7d8f7e'
    motor = common.verify(package, ['test-binaries.json'], revision)
    motor['verifiedGitFiles'] = source_archive(common, package, revision)
    config = common.read(package / 'configuration.json')
    counts = dict(CausalMotorPlantTests=27, DynamicMuscleTests=16, MotorUnitCalciumTests=13,
        IsometricMotorUnitPlantTests=15, MotorAxonTransportTests=32, MotorAxonReplayBufferTests=16, SolutionManifestTests=1)
    assert config['ExpectedClassCounts'] == counts and config['ExpectedTestCases'] == 120
    expected = Counter()
    for cls, count in counts.items():
        code = common.io(package / 'source/tests/NeuralResonanceEngine.DNNE.Tests' / (cls + '.cs')).read_text()
        members = re.findall(r'((?:\s*\[[^\n]+\])+\s*)public\s+(?:async\s+)?(?:void|Task)\s+(\w+)\(', code)
        methods = {name: (1 if '[Fact]' in attrs else attrs.count('[InlineData('))
            for attrs, name in members if '[Fact]' in attrs or '[Theory]' in attrs}
        assert sum(methods.values()) == count
        expected.update({'NeuralResonanceEngine.DNNE.Tests.' + cls + '.' + name: n for name, n in methods.items()})
    assert dict(expected) == config['ExpectedTestMethods']
    ns = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    cases = ET.parse(package / 'test-results/focused.trx').findall('.//t:UnitTestResult', ns)
    assert len(cases) == len({c.attrib['testName'] for c in cases}) == 120
    assert all(c.attrib['outcome'] == 'Passed' for c in cases)
    assert Counter(c.attrib['testName'].split('(', 1)[0] for c in cases) == expected
    preservation = common.read(package / 'reference/core-preservation.json')
    muscle = common.io(package / 'source/src/NRE.SimAvatar/AvatarDynamicMuscle.cs')
    original = package / 'reference/qualified-AvatarDynamicMuscle.cs'
    addition = preservation['SoleAddition']; text = muscle.read_text(encoding='utf-8-sig')
    assert text.count(addition) == 1 and text.replace(addition, '') == original.read_text(encoding='utf-8-sig')
    assert common.digest(muscle) == preservation['CandidateMuscleSha256']
    assert common.digest(original) == preservation['QualifiedMuscleSha256']
    same = ['src/NRE.SimAvatar/AvatarCausalMotorPlant.cs', 'src/NRE.SimAvatar/AvatarDynamicMuscle.cs']
    assert all((ROOT / name).read_text() == common.io(package / 'source' / name).read_text() for name in same)
    motor.update(reviewedUtc=datetime.now(timezone.utc).isoformat(), operatorReportedCompletion=True,
        passedTests=120, newCausalTests=27, exactQualifiedCorePreserved=True, currentCandidateSourceMatches=True,
        expectedTestMethods=dict(expected), independentCoupledReferenceQualified=False, nativeUnityIntegrated=False,
        physiologyQualified=False, avatarRecoveryAssessed=False, testsExecutedDuringReview=False, modelsSolvedDuringReview=False)
    output = ROOT / 'artifacts/causal-motor-review-20261113'; output.mkdir()
    (output / 'review.json').write_text(json.dumps(motor, indent=2, allow_nan=False) + '\n')
    print('Causal motor: full C:/D: capture, frozen source, binaries, all 120 cases and qualified core verified.', flush=True)
    print(json.dumps({k: motor[k] for k in ('verifiedFilesPerCopy', 'verifiedBytesPerCopy', 'verifiedGitFiles', 'manifestSha256')}, indent=2))


if __name__ == '__main__':
    main()
