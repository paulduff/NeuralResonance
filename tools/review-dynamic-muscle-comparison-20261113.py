"""Review the operator-reported completed comparison; no tests or integration."""
from collections import Counter
from datetime import datetime, timezone
from io import BytesIO
import importlib.util
import json
from pathlib import Path
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[1]


def main():
    spec = importlib.util.spec_from_file_location('capture_audit', ROOT / 'tools/review-olive-and-motor-20261109.py')
    common = importlib.util.module_from_spec(spec); spec.loader.exec_module(common)
    package = ROOT / 'artifacts/dynamic-muscle-comparison-20261113-95e59f'
    revision = 'cc84abe349f7fbd4a442b2da2ad7e120611ba272'
    review = common.verify(package, ['test-binaries.json', 'probe-binaries.json'], revision)
    data = subprocess.check_output(['git', 'archive', '--format=zip', revision], cwd=ROOT)
    with ZipFile(BytesIO(data)) as git, ZipFile(package / 'source.zip') as frozen:
        assert frozen.comment.decode().strip() == revision and git.namelist() == frozen.namelist()
        files = 0
        for entry in frozen.infolist():
            assert git.read(entry.filename) == frozen.read(entry)
            if not entry.is_dir():
                assert common.io(package / 'source' / entry.filename).read_bytes() == frozen.read(entry)
                files += 1
    review['verifiedGitFiles'] = files
    print('Frozen source, binaries and full C:/D: capture verified.', flush=True)
    counts = dict(DynamicMuscleTests=16, MotorUnitCalciumTests=13, IsometricMotorUnitPlantTests=15,
        MotorAxonTransportTests=32, MotorAxonReplayBufferTests=16, SolutionManifestTests=1)
    config = common.read(package / 'configuration.json')
    assert config['ExpectedClassCounts'] == counts and config['ExpectedTestCases'] == 93
    expected = Counter()
    for name, count in counts.items():
        code = common.io(package / 'source/tests/NeuralResonanceEngine.DNNE.Tests' / (name + '.cs')).read_text()
        methods = re.findall(r'((?:\s*\[[^\n]+\])+\s*)public\s+(?:async\s+)?(?:void|Task)\s+(\w+)\(', code)
        members = {method: (1 if '[Fact]' in attributes else attributes.count('[InlineData('))
                   for attributes, method in methods if '[Fact]' in attributes or '[Theory]' in attributes}
        assert sum(members.values()) == count and all(members.values())
        expected.update({'NeuralResonanceEngine.DNNE.Tests.' + name + '.' + method: n for method, n in members.items()})
    ns = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    cases = ET.parse(package / 'test-results/focused.trx').findall('.//t:UnitTestResult', ns)
    assert len(cases) == len({c.attrib['testName'] for c in cases}) == 93
    assert Counter(c.attrib['testName'].split('(', 1)[0] for c in cases) == expected
    assert all(c.attrib['outcome'] == 'Passed' for c in cases)
    sampling = common.read(package / 'reference-sampling-tests.json')
    assert sampling == dict(passed=True, tests=3, methodChecks=6, analyticalOracle=True, physiologicalModelExecuted=False)
    code = common.io(package / 'source/tools/test-dynamic-muscle-sampling.py').read_text()
    names = re.findall(r'    def (test_\w+)\(self\):', code)
    log = common.io(package / 'reference-sampling-tests.log').read_text()
    assert len(names) == 3 and all(name + ' (__main__.RecordingBoundaryTests.' + name + ') ... ok' in log for name in names)
    assert 'Ran 3 tests' in log and 'OK' in log
    status = common.read(package / 'window-status.json')
    assert status['ReferenceSamplingTestsPassed'] and status['IndependentNumericalCriteriaPassed']
    sys.path.insert(0, str(package / 'source/tools'))
    audit = common.module('frozen_dynamic_audit', package / 'source/tools/analyse-dynamic-muscle.py')
    analysis = audit.audit(package)
    assert analysis == common.read(package / 'dynamic-analysis.json')
    assert analysis['completed'] and analysis['numericalCriteriaPassed'] and len(analysis['cases']) == 11
    assert analysis['candidateConditions'] == analysis['independentReferences'] == 33
    assert analysis['sourceOrderingAttempts'] == 15 and analysis['exactRecordedArrivalsPreserved']
    assert all(case['passed'] for case in analysis['cases'])
    for field in ('originalBoundsChanged', 'nativeMatlabExecuted', 'liveBodyIntegrated', 'physiologicalCalibrationComplete', 'avatarRecoveryAssessed'):
        assert not analysis[field]
    finest = [case['candidates'][-1] for case in analysis['cases']]
    quantities = next(iter(finest[0]['errorsAgainst'].values())).keys()
    worst = {name: max(error[name] for candidate in finest for error in candidate['errorsAgainst'].values()) for name in quantities}
    accounting = {key: max(c[key] for case in analysis['cases'] for c in case['candidates']) for key in
        ('releaseWaveformError', 'calciumLedgerError', 'momentumLedgerError')}
    accounting['finestKineticLedgerError'] = max(c['kineticLedgerError'] for c in finest)
    ordering = [control for case in analysis['cases'] for control in case['sourceOrdering']]
    legacy = [c['legacyControl'] for case in analysis['cases'] for c in case['candidates'] if c['legacyControl'] is not None]
    assert len(legacy) == 1 and legacy[0]['passed']
    review.update(reviewedUtc=datetime.now(timezone.utc).isoformat(), operatorReportedCompletion=True,
        passedTests=93, passedAnalyticBoundaryTests=3, passedBoundaryMethodChecks=6,
        savedAnalysisExactlyReproduced=True, analysis=analysis, maximumFinestErrors=worst,
        maximumAccountingErrors=accounting, legacyControl=legacy[0],
        completedSourceOrderingControls=sum(c['completed'] for c in ordering),
        failedSourceOrderingControls=[c for c in ordering if not c['completed']],
        testsExecutedDuringReview=False, modelsSolvedDuringReview=False)
    output = ROOT / 'artifacts/dynamic-muscle-comparison-review-20261113'; output.mkdir()
    (output / 'review.json').write_text(json.dumps(review, indent=2, allow_nan=False) + '\n')
    print(json.dumps({k: v for k, v in review.items() if k != 'analysis'}, indent=2), flush=True)


if __name__ == '__main__':
    main()
