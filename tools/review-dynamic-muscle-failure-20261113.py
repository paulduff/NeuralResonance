"""Audit reported cd1577 evidence without running tests or solving a model."""
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

import numpy as np

ROOT = Path(__file__).resolve().parents[1]


def main():
    spec = importlib.util.spec_from_file_location('capture_audit', ROOT / 'tools/review-olive-and-motor-20261109.py')
    common = importlib.util.module_from_spec(spec); spec.loader.exec_module(common)
    package = ROOT / 'artifacts/dynamic-muscle-comparison-20261113-cd1577'
    revision = '66e136dc464254d7936c77332085a6ae6f75bb9d'
    review = common.verify(package, ['test-binaries.json', 'probe-binaries.json'], revision, 'FAILED', 1)
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
    print('Frozen source, binaries and complete C:/D: capture verified.', flush=True)

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

    sys.path.insert(0, str(package / 'source/tools'))
    audit = common.module('frozen_dynamic_audit', package / 'source/tools/analyse-dynamic-muscle.py')
    protocol = audit.validate(package)
    assert protocol == common.read(package / 'dynamic-study/protocol.json')
    environment_matches = audit.environment(package) == common.read(package / 'reference-preflight.json')['environment']
    assert environment_matches, 'Reference environment differs at review; preserve and investigate.'
    results = common.read(package / 'dynamic-study/results.json')
    assert results['completed'] and results['successfulCases'] == len(results['cases']) == 33
    assert {r['key'] for r in results['cases']} == {c['key'] + '-h' + str(h) for c in protocol['cases'] for h in protocol['stepsMs']}
    original = [json.loads(line) for line in (package / 'reference/arrivals.jsonl').read_text().splitlines()]
    records = []; reference_records = []; stored_candidates = {}
    for case in protocol['cases']:
        ref = audit.Reference(protocol, case); n = ref.n
        grid_ms = np.arange(round(case['durationMs'] / protocol['sampleMs']) + 1) * protocol['sampleMs']
        grid = grid_ms / 1000
        for step in protocol['stepsMs']:
            key = case['key'] + '-h' + str(step)
            entry = next(r for r in results['cases'] if r['key'] == key)
            assert entry['completed'] and entry['sampleCount'] == len(grid) and entry['delivered'] == len(case['events'])
            inputs = common.read(package / 'dynamic-study' / (key + '-inputs.json'))
            assert inputs['scenario'] == case and inputs['stepMs'] == step
            arrivals = [json.loads(line) for line in (package / 'dynamic-study' / (key + '-arrivals.jsonl')).read_text().splitlines()]
            assert arrivals == inputs['arrivals']
            assert [a['arrivalAtBodyMs'] for a in arrivals] == [e['arrivalAtMs'] for e in case['events']]
            if case['replayOriginalArrivals']:
                assert arrivals == original
            values = np.loadtxt(package / 'dynamic-study' / (key + '.csv'), delimiter=',', skiprows=1)
            assert values.shape == (len(grid), 16 + 9 * n) and np.isfinite(values).all()
            assert np.array_equal(values[:, 0], grid_ms)
            state = np.concatenate([values[:, 2:5]] + [values[:, 17 + 9 * i:23 + 9 * i] for i in range(n)] + [values[:, 12:14]], axis=1)
            stored_candidates[key] = (state, values[:, 5:12])
            records.append(dict(key=key, samples=len(grid), delivered=len(arrivals), finite=True,
                sampleDomainPassed=ref.domain(state, 0)))
        for settings in protocol['referenceSettings']:
            key = case['key'] + '-' + settings['key']
            path = package / 'dynamic-reference' / (key + '.npz')
            if not path.exists():
                continue
            with np.load(path, allow_pickle=False) as saved:
                assert np.array_equal(saved['timeSeconds'], grid)
                state, forces = saved['state'], saved['forces']
                assert state.shape == (len(grid), 3 + 6 * n + 2) and forces.shape == (len(grid), 7)
                assert np.isfinite(state).all() and np.isfinite(forces).all()
                stats = common.read(package / 'dynamic-reference' / (key + '-statistics.json'))
                candidate, candidate_forces = stored_candidates[case['key'] + '-h' + str(protocol['stepsMs'][-1])]
                errors = audit.errors(candidate, state, candidate_forces, forces, n)
                reference_records.append(dict(key=key, statistics=stats, sampleDomainPassed=ref.domain(state),
                    finestCandidateErrors=errors, finestCandidateWithinBounds=audit.within(errors, protocol['candidateBounds'])))
    assert len(reference_records) == 21
    ordering = [common.read(p) for p in sorted((package / 'dynamic-reference').glob('*-status.json'))]
    assert len(ordering) == 9 and all(r['completed'] for r in ordering)
    failure = common.read(package / 'dynamic-reference/active-stretch-dop853-failure.json')
    assert not failure['completed'] and 'need at least one array to concatenate' in failure['error']
    assert not (package / 'dynamic-reference/execution.json').exists() and not (package / 'dynamic-analysis.json').exists()
    case = next(c for c in protocol['cases'] if c['key'] == 'active-stretch')
    ref = audit.Reference(protocol, case)
    with np.load(package / 'dynamic-reference/active-stretch-dop853-partial.npz', allow_pickle=False) as partial:
        times, internal, sampled = partial['internalTimes'], partial['internalStates'], partial['partialSamples']
        left, right = times[0], times[-1]
        assert left == failure['completedBoundarySeconds'] == .500015625 and right == .50003125
        grid = partial['timeSeconds']
        assert not ((grid >= left) & (grid < right)).any()
        assert np.isfinite(internal).all() and ref.domain(internal)
        filled = np.isfinite(sampled).all(axis=1)
        assert np.array_equal(filled, grid < left)
        partial_info = dict(lastRecordedBoundarySeconds=float(left), integratedButUnrecordedEndpointSeconds=float(right),
            gapMilliseconds=float((right - left) * 1000), filledSampleCount=int(filled.sum()),
            finalIntegratedState=internal[-1].tolist(), integratedIntervalHasNoRequestedSamples=True)
    review.update(reviewedUtc=datetime.now(timezone.utc).isoformat(), operatorReportedCompletion=True,
        originalStatus='FAILED', originalFailurePreserved=True, passedTests=93,
        candidateExecution=records, completedReferenceSolves=reference_records, completedSourceOrdering=ordering,
        failure=partial_info, environmentMatchesPreflightAtReview=True,
        diagnosis='Empty dense-output query between adjacent load and motor-arrival boundaries; state integration had succeeded.',
        numericalComparisonComplete=False, physiologicalCalibrationComplete=False, liveBodyIntegrated=False,
        testsExecutedDuringReview=False, modelsSolvedDuringReview=False)
    output = ROOT / 'artifacts/dynamic-muscle-failure-review-20261113'; output.mkdir()
    (output / 'review.json').write_text(json.dumps(review, indent=2, allow_nan=False) + '\n')
    print(json.dumps(dict(output=str(output), verifiedFilesPerCopy=review['verifiedFilesPerCopy'],
        verifiedBytesPerCopy=review['verifiedBytesPerCopy'], manifestSha256=review['manifestSha256'],
        passedTests=93, candidateTraces=33, completedReferences=21, completedOrderingControls=9,
        completedReferencesWithinFinestCandidateBounds=sum(r['finestCandidateWithinBounds'] for r in reference_records),
        failure=partial_info), indent=2))


if __name__ == '__main__':
    main()
