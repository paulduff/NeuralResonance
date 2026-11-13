"""Verify the operator-reported geometry capture without rerunning tests."""
from collections import Counter
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import re
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]


def main():
    spec = importlib.util.spec_from_file_location('capture_audit', ROOT / 'tools/review-olive-and-motor-20261109.py')
    common = importlib.util.module_from_spec(spec); spec.loader.exec_module(common)
    package = ROOT / 'artifacts/muscle-paths-20261113-2d0fa4'
    revision = '31a6f457ecb29dffc15467e2e84cc8a73d6deceb'
    review = common.verify(package, ['test-binaries.json'], revision)
    archive = common.module('source_archive_audit', ROOT / 'tools/review-spindle-root-and-causal-motor-20261113.py')
    review['verifiedGitFiles'] = archive.source_archive(common, package, revision)
    config = common.read(package / 'configuration.json')
    counts = dict(MusclePathGeometryTests=31, CausalMotorPlantTests=27, DynamicMuscleTests=16, SolutionManifestTests=1)
    assert config['ExpectedClassCounts'] == counts and config['ExpectedTestCases'] == 75
    expected = Counter()
    for cls, count in counts.items():
        code = common.io(package / 'source/tests/NeuralResonanceEngine.DNNE.Tests' / (cls + '.cs')).read_text()
        found = re.findall(r'((?:\s*\[[^\n]+\])+\s*)public\s+(?:async\s+)?(?:void|Task)\s+(\w+)\(', code)
        methods = {name: (1 if '[Fact]' in attrs else attrs.count('[InlineData('))
                   for attrs, name in found if '[Fact]' in attrs or '[Theory]' in attrs}
        assert sum(methods.values()) == count and all(methods.values())
        expected.update({'NeuralResonanceEngine.DNNE.Tests.' + cls + '.' + name: n for name, n in methods.items()})
    assert dict(expected) == config['ExpectedTestMethods']
    ns = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    cases = ET.parse(package / 'test-results/focused.trx').findall('.//t:UnitTestResult', ns)
    assert len(cases) == len({c.attrib['testName'] for c in cases}) == 75
    assert all(c.attrib['outcome'] == 'Passed' for c in cases)
    assert Counter(c.attrib['testName'].split('(', 1)[0] for c in cases) == expected
    generated = common.module('frozen_path_generator', package / 'source/tools/prepare-unity-muscle-paths.py').generate(package / 'source')
    assert generated == common.io(package / 'source/UnityWorld/Assets/NRE/Runtime/MusclePathGeometry.cs').read_text(encoding='utf-8-sig')
    for name in ('src/NRE.SimAvatar/AvatarMusclePath.cs', 'UnityWorld/Assets/NRE/Runtime/MusclePathGeometry.cs',
                 'src/NRE.SimAvatar/AvatarCausalMotorPlant.cs', 'src/NRE.SimAvatar/AvatarDynamicMuscle.cs'):
        assert (ROOT / name).read_text(encoding='utf-8-sig') == common.io(package / 'source' / name).read_text(encoding='utf-8-sig')
    review.update(reviewedUtc=datetime.now(timezone.utc).isoformat(), operatorReportedCompletion=True,
        passedTests=75, passedGeometryCases=31, passedRegressionCases=44, expectedTestMethods=dict(expected),
        exactGeneratedSourceVerified=True, currentCandidateSourceMatches=True, nativeUnityQualified=False,
        anatomicalCalibrationQualified=False, physiologyQualified=False, avatarRecoveryAssessed=False,
        testsExecutedDuringReview=False, modelsSolvedDuringReview=False)
    output = ROOT / 'artifacts/muscle-path-review-20261113'; output.mkdir()
    (output / 'review.json').write_text(json.dumps(review, indent=2, allow_nan=False) + '\n')
    print(json.dumps({k: v for k, v in review.items() if k != 'expectedTestMethods'}, indent=2), flush=True)


if __name__ == '__main__':
    main()
