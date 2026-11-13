"""Review the three operator-reported captures, without tests or integration."""
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
NS = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}


def main():
    spec = importlib.util.spec_from_file_location('capture_audit', ROOT / 'tools/review-olive-and-motor-20261109.py')
    common = importlib.util.module_from_spec(spec); spec.loader.exec_module(common)

    def source(package, revision):
        data = subprocess.check_output(['git', 'archive', '--format=zip', revision], cwd=ROOT)
        with ZipFile(BytesIO(data)) as git, ZipFile(package / 'source.zip') as frozen:
            assert frozen.comment.decode().strip() == revision and git.namelist() == frozen.namelist()
            count = 0
            for entry in frozen.infolist():
                assert git.read(entry.filename) == frozen.read(entry)
                if not entry.is_dir():
                    assert common.io(package / 'source' / entry.filename).read_bytes() == frozen.read(entry)
                    count += 1
        return count

    def tests(package, counts, outcomes):
        config = common.read(package / 'configuration.json')
        assert config['ExpectedClassCounts'] == counts and config['ExpectedTestCases'] == sum(counts.values())
        expected = Counter()
        for name, count in counts.items():
            text = common.io(package / 'source/tests/NeuralResonanceEngine.DNNE.Tests' / (name + '.cs')).read_text()
            methods = re.findall(r'((?:\s*\[[^\n]+\])+\s*)public\s+(?:async\s+)?(?:void|Task)\s+(\w+)\(', text)
            members = {method: (1 if '[Fact]' in attributes else attributes.count('[InlineData('))
                for attributes, method in methods if '[Fact]' in attributes or '[Theory]' in attributes}
            assert sum(members.values()) == count and all(members.values()), (name, members)
            expected.update({'NeuralResonanceEngine.DNNE.Tests.' + name + '.' + method: n for method, n in members.items()})
        cases = ET.parse(package / 'test-results/focused.trx').findall('.//t:UnitTestResult', NS)
        assert len(cases) == len({c.attrib['testName'] for c in cases}) == sum(counts.values())
        assert Counter(c.attrib['testName'].split('(', 1)[0] for c in cases) == expected
        assert Counter(c.attrib['outcome'] for c in cases) == outcomes
        return cases

    muscle = ROOT / 'artifacts/dynamic-muscle-20261113-f9970f'
    rev = '23d6a0618968d10c92cf1afaaefa7cb19354e41d'
    motor = common.verify(muscle, ['test-binaries.json'], rev)
    motor['verifiedGitFiles'] = source(muscle, rev)
    counts = dict(DynamicMuscleTests=16, MotorUnitCalciumTests=13, IsometricMotorUnitPlantTests=15,
        MotorAxonTransportTests=32, MotorAxonReplayBufferTests=16, SolutionManifestTests=1)
    cases = tests(muscle, counts, {'Passed': 93})
    snapshots = {}
    for case in cases:
        if '.DynamicMuscleTests.' not in case.attrib['testName']:
            continue
        output = case.find('.//t:StdOut', NS)
        if output is None:
            continue
        for line in output.text.splitlines():
            if ': {' in line:
                label, value = line.split(': ', 1)
                if value.startswith('{"TimeMs":'):
                    assert label not in snapshots
                    snapshots[label] = json.loads(value)
    assert len(snapshots) == 10, list(snapshots)
    status = common.read(muscle / 'window-status.json')
    assert status['DynamicMuscleAndRegressionChecksPassed'] and status['ModelExecutionAttempted'] and status['MotorEventsAreTestFixtures']
    assert not any(status[k] for k in ('LiveBrainAccessed', 'OldSavesLoaded', 'LiveUnityIntegrated', 'TimingMeasured',
        'PhysiologyQualified', 'AvatarRecoveryAssessed', 'FullUnitSuitePassed'))
    motor.update(operatorReportedCompletion=True, passedTests=93, newComponentTests=16,
        snapshots=snapshots, independentReferenceComparisonPassed=False, physiologicalCalibrationComplete=False,
        testsExecutedDuringReview=False, modelsExecutedDuringReview=False)
    print('Dynamic muscle: all 93 cases and complete C:/D: evidence verified.', flush=True)

    profile = ROOT / 'artifacts/physical-observation-20261113-64ad44'
    rev = 'd3f148e5e281f3354b4c391f0f2b160768807abb'
    body = common.verify(profile, ['test-binaries.json'], rev, 'FAILED', 1)
    body['verifiedGitFiles'] = source(profile, rev)
    cases = tests(profile, dict(PhysicalObservationTests=31, PhysicalSampleProducerTests=16,
        PhysicalSampleTransportTests=22, SolutionManifestTests=1), {'Passed': 69, 'Failed': 1})
    failed = next(c for c in cases if c.attrib['outcome'] == 'Failed')
    assert failed.attrib['testName'].endswith('PhysicalObservationTests.WrongStreamClockSampleIdentityAndPayloadAreRejected')
    assert 'No exception was thrown' in failed.find('.//t:Message', NS).text
    old = common.io(profile / 'source/tests/NeuralResonanceEngine.DNNE.Tests/PhysicalObservationTests.cs').read_text()
    protocol = common.io(profile / 'source/Shared.Contracts/PhysicalObservationProtocol.cs').read_text()
    assert 'p with { Version = 2 }' in old and 'public const int Version = 2;' in protocol
    replacement = common.read(ROOT / 'artifacts/unity-body-contact-review-20261113/review.json')
    assert replacement['passedTests'] == 74 and replacement['exactSavedAuditReproduction']
    body.update(operatorReportedCompletion=True, passedTests=69, failedTests=1, originalStatus='FAILED',
        failedTest=failed.attrib['testName'], diagnosis='Same stale protocol-v2 rejection fixture as 162523; corrected 7800c5 already passes its complete scope.',
        originalFailurePreserved=True, testsExecutedDuringReview=False, modelsExecutedDuringReview=False)
    print('Older profile: 69 passed, one previously repaired fixture failure; complete C:/D: evidence verified.', flush=True)

    spindle = ROOT / 'artifacts/spindle-reference-20261113-98c085'
    rev = 'c0ce3e9e7a42a25d19821819b0a47e7933cc2e1d'
    sensory = common.verify(spindle, [], rev)
    sensory['verifiedGitFiles'] = source(spindle, rev)
    before = common.read(spindle / 'runtime-before.json')
    assert before == common.read(spindle / 'runtime-after.json') == common.read(spindle / 'preparation-verification.json')
    assert before['verifiedRuntimeFiles'] == 59532 and before['verifiedCompatibilityFiles'] == 18
    frozen = common.module('frozen_spindle_audit', spindle / 'source/tools/spindle-reference.py')
    assert frozen.verify_compatibility(spindle) == 18
    analysis = frozen.analyse(spindle)
    assert analysis == common.read(spindle / 'mechanics-analysis.json')
    assert analysis['complete'] and len(analysis['trajectories']) == 30 and len(analysis['boundaries']) == 2
    assert len(analysis['refinementComparisons']) == 20 and not analysis['failedExecutions']
    config = common.read(spindle / 'configuration.json')
    archive = Path(config['ExternalFiles'][0]['Path'])
    assert common.digest(archive) == frozen.UPSTREAM_HASH
    with ZipFile(archive) as upstream:
        prefix = 'spindleModelTutorial-' + frozen.UPSTREAM + '/'
        for entry in upstream.infolist():
            assert entry.filename.startswith(prefix)
            if not entry.is_dir():
                assert common.io(spindle / 'upstream' / entry.filename[len(prefix):]).read_bytes() == upstream.read(entry)
    resolution = common.read(spindle / 'mechanics/resolution.json')
    control = resolution['emptyOptionsCompatibility']
    assert control['emptyArgumentRejected'] and control['threeAndStructExactlyEqual'] and control['maximumExponentialError'] <= 1e-8
    sensory.update(operatorReportedCompletion=True, savedAnalysisReproduced=True, runtimeBeforeAndAfterMatched=True,
        verifiedRecordedRuntimeFiles=59532, verifiedCompatibilityFiles=18, compatibilityControl=control,
        analysis=analysis, testsExecutedDuringReview=False, modelsExecutedDuringReview=False)
    print('Spindle: 30 finite trajectories, two boundaries and 20 descriptive comparisons reproduced offline.', flush=True)
    output = ROOT / 'artifacts/muscle-spindle-profile-review-20261113'; output.mkdir()
    for name, value in (('muscle-review.json', motor), ('profile-review.json', body), ('spindle-review.json', sensory)):
        (output / name).write_text(json.dumps(dict(reviewedUtc=datetime.now(timezone.utc).isoformat(), **value), indent=2, allow_nan=False) + '\n')
    print(json.dumps(dict(output=str(output), captures={name: {k: v[k] for k in ('verifiedFilesPerCopy', 'verifiedBytesPerCopy', 'manifestSha256')}
        for name, v in (('muscle', motor), ('profile', body), ('spindle', sensory))}), indent=2), flush=True)


if __name__ == '__main__':
    main()
