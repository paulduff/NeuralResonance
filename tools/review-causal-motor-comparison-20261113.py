"""Audit the operator-reported completed coupled study; no tests or ODE solves."""
from collections import Counter
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]


def main():
    spec = importlib.util.spec_from_file_location('capture_audit', ROOT / 'tools/review-olive-and-motor-20261109.py')
    common = importlib.util.module_from_spec(spec); spec.loader.exec_module(common)
    source = common.module('source_archive_audit', ROOT / 'tools/review-spindle-root-and-causal-motor-20261113.py')
    package = ROOT / 'artifacts/causal-motor-comparison-20261113-2acbfd'
    revision = '910a1d6f98ce39335d490b7dcbdb92decb7033b4'
    review = common.verify(package, ['test-binaries.json', 'probe-binaries.json'], revision)
    review['verifiedGitFiles'] = source.source_archive(common, package, revision)
    print('Frozen source, binaries and full C:/D: capture verified.', flush=True)
    config = common.read(package / 'configuration.json')
    counts = dict(CausalMotorPlantTests=27, DynamicMuscleTests=16, MotorUnitCalciumTests=13,
        IsometricMotorUnitPlantTests=15, MotorAxonTransportTests=32, MotorAxonReplayBufferTests=16, SolutionManifestTests=1)
    assert config['ExpectedClassCounts'] == counts and config['ExpectedTestCases'] == 120
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
    assert len(cases) == len({c.attrib['testName'] for c in cases}) == 120
    assert all(c.attrib['outcome'] == 'Passed' for c in cases)
    assert Counter(c.attrib['testName'].split('(', 1)[0] for c in cases) == expected
    status = common.read(package / 'window-status.json')
    assert status['ReferenceControlsPassed'] and status['IndependentNumericalCriteriaPassed'] and status['ReferenceEnvironmentUnchanged']
    sys.path.insert(0, str(package / 'source/tools'))
    audit = common.module('frozen_causal_audit', package / 'source/tools/analyse-causal-motor.py')
    environment = audit.environment(package)
    assert environment == common.read(package / 'preparation-environment.json')
    assert environment == common.read(package / 'reference-preflight.json')['environment']
    assert environment == common.read(package / 'reference-postflight.json')['environment']
    assert environment == common.read(package / 'reference/qualified-numerical-preflight.json')['environment']
    analysis = audit.audit(package)
    assert analysis == common.read(package / 'causal-analysis.json')
    assert analysis['completed'] and analysis['numericalCriteriaPassed'] and len(analysis['cases']) == 8
    assert analysis['candidateConditions'] == 40 and analysis['independentReferences'] == 24 and analysis['originalRecordedArrivals'] == 28
    assert all(c['passed'] for c in analysis['cases'])
    for field in ('neuronalModelExecuted', 'liveUnityIntegrated', 'physiologyQualified', 'avatarRecoveryAssessed'):
        assert not analysis[field]
    protocol = common.read(package / 'causal-input.json')
    finest = [candidate for case in analysis['cases'] for candidate in case['candidates'][3:]]
    worst = {name: max(c['errors'][name] for candidate in finest for c in candidate['contrasts'])
             for name in protocol['candidateBounds']}
    defects = {name: max(c['energy'][name] for c in finest)
               for name in ('maximumCumulativeDefectJoules', 'totalAbsoluteDefectJoules')}
    for name in ('src/NRE.SimAvatar/AvatarCausalMotorPlant.cs', 'src/NRE.SimAvatar/AvatarDynamicMuscle.cs',
                 'tools/causal_motor_protocol.py', 'tools/causal_motor_reference.py', 'tools/analyse-causal-motor.py'):
        assert (ROOT / name).read_text(encoding='utf-8-sig') == common.io(package / 'source' / name).read_text(encoding='utf-8-sig'), name
    controls = common.read(package / 'reference-controls.json')
    review.update(reviewedUtc=datetime.now(timezone.utc).isoformat(), operatorReportedCompletion=True,
        passedTests=120, analyticMechanicalMethodChecks=controls['analyticMethodChecks'], tendonQuadratureCases=controls['tendonQuadratureCases'],
        savedAnalysisExactlyReproduced=True, originalBoundsChanged=False, environmentUnchanged=True,
        currentCandidateSourceMatches=True, analysis=analysis, maximumFinestErrors=worst,
        maximumFinestCouplingDefects=defects, prospectiveCandidateBounds=protocol['candidateBounds'],
        finestPhysicalStepMs=protocol['resolutions'][3]['physicalStepMs'],
        testsExecutedDuringReview=False, modelsSolvedDuringReview=False)
    output = ROOT / 'artifacts/causal-motor-comparison-review-20261113'; output.mkdir()
    (output / 'review.json').write_text(json.dumps(review, indent=2, allow_nan=False) + '\n')
    print(json.dumps({k: v for k, v in review.items() if k not in ('analysis', 'prospectiveCandidateBounds')}, indent=2), flush=True)


if __name__ == '__main__':
    main()
