"""Review the two operator-reported captures; no tests or physical models run."""
import argparse
from collections import Counter
from datetime import datetime, timezone
from hashlib import sha256
import importlib.util
from io import BytesIO
import json
from pathlib import Path
import re
import subprocess
import xml.etree.ElementTree as ET
from zipfile import ZipFile

ROOT = Path(__file__).resolve().parents[1]
BASE_COUNTS = dict(PhysicalSampleProducerTests=16, PhysicalSampleTransportTests=22, SolutionManifestTests=1)


def main(output):
    if output.exists():
        raise FileExistsError('Preserve previous review: ' + str(output))
    spec = importlib.util.spec_from_file_location('completed_capture_hashes', ROOT / 'tools/review-olive-and-motor-20261109.py')
    common = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(common)

    def source_and_tests(capture, revision, counts):
        # git archive applies this repository's declared Windows line endings.
        # Compare every archive member and its extracted source, not just names.
        regenerated = subprocess.check_output(['git', 'archive', '--format=zip', revision], cwd=ROOT)
        with ZipFile(BytesIO(regenerated)) as git_archive, ZipFile(capture / 'source.zip') as frozen:
            assert frozen.comment.decode('ascii').strip() == revision
            assert set(git_archive.namelist()) == set(frozen.namelist())
            files = 0
            for entry in frozen.infolist():
                assert frozen.read(entry) == git_archive.read(entry.filename)
                if not entry.is_dir():
                    assert common.io(capture / 'source' / entry.filename).read_bytes() == frozen.read(entry)
                    files += 1
        expected_names = set()
        for name, count in counts.items():
            source = common.io(capture / 'source/tests/NeuralResonanceEngine.DNNE.Tests' / (name + '.cs')).read_text(encoding='utf-8-sig')
            names = re.findall(r'\[Fact\]\s+public\s+(?:async\s+)?(?:void|Task)\s+(\w+)\s*\(', source)
            assert len(names) == len(set(names)) == count
            expected_names.update('NeuralResonanceEngine.DNNE.Tests.' + name + '.' + n for n in names)
        results = ET.parse(capture / 'test-results/focused.trx').findall('.//t:UnitTestResult',
            {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'})
        names = [r.attrib['testName'] for r in results]
        assert len(names) == len(set(names)) == sum(counts.values()) and set(names) == expected_names
        actual_counts = dict(Counter(n.rsplit('.', 1)[0].rsplit('.', 1)[-1] for n in names))
        assert actual_counts == counts
        return files, results

    unity = ROOT / 'artifacts/unity-physical-sample-20261113-1ff02e'
    revision = 'b41dc3afc69ca1a7e0ca7afda48e8492392b561e'
    report = common.verify(unity, ['test-binaries.json', 'probe-binaries.json'], revision)
    assert Path(report['backup']) == Path('D:/DNNE-desktop-runs/UnityWorld') / unity.name
    git_files, tests = source_and_tests(unity, revision, BASE_COUNTS)
    assert all(t.attrib['outcome'] == 'Passed' for t in tests)
    config = common.read(unity / 'configuration.json')
    status = common.read(unity / 'window-status.json')
    assert config['ExpectedClassCounts'] == BASE_COUNTS and config['ExpectedTestCases'] == 39
    assert config['NoLiveMonitoring'] and config['ActualUnityPhysicsRequired'] and not config['LiveBodySelected']
    assert status['UnityBuildSucceeded'] and status['UnityPhysicsChecksPassed'] and status['ProducerAndTransportChecksPassed']
    assert not any(status[k] for k in ('LiveBrainAccessed', 'OldSavesLoaded', 'LiveUnityIntegrated',
        'NeuronalModelExecuted', 'TimingMeasured', 'PhysiologyQualified', 'AvatarRecoveryAssessed', 'FullUnitSuitePassed'))
    player_files = common.read(unity / 'player-files.json')
    for entry in player_files:
        path = Path(entry['Path']); path.relative_to(unity / 'player')
        assert common.digest(path) == entry['Sha256']
    compiled_sources = 0
    built = common.io(unity / 'built-project/Assets')
    for path in built.rglob('*.cs'):
        relative = path.relative_to(built)
        original = common.io(unity / 'source/UnityWorld/Assets' / relative)
        assert path.read_bytes() == original.read_bytes()
        compiled_sources += 1
    assert compiled_sources > 0
    # The saved binary's --audit entry only reads originals/packets and replays
    # in-memory delivery. Its source explicitly branches before player startup.
    probe = unity / 'source/artifacts/unity-physical-probe/bin/UnityPhysicalSampleProbe/release/UnityPhysicalSampleProbe.dll'
    audit_text = subprocess.check_output([config['DotnetExecutable'], str(probe), '--audit', str(unity / 'study')],
                                        cwd=ROOT, encoding='utf-8')
    audit = json.loads(audit_text)
    assert audit == common.read(unity / 'study/saved-analysis.json')
    result = common.read(unity / 'study/study-result.json')
    assert result['completed'] and result['exitCode'] == 0 and result['stepAndBodyDeliveryChecksPassed']
    assert result['postMovementCaptureFailureStopped'] and result['directAndWrongOwnerStepsRejected']
    assert result['actualPhysicsSteps'] == audit['actualPhysicsSteps'] == 201
    assert result['capturedAndConsumedSamples'] == audit['deliveredBodySamples'] == 201
    assert result['completeThroughStep'] == audit['completeThroughStep'] == 200
    assert audit['originalGridSamples'] == 202 and audit['heldBodyInspections'] == 4
    assert audit['originalTypedObservationsExactlyPreserved'] and not audit['contactsTransportedThroughBodyLane']
    assert result['maximumPelvisDisplacementMeters'] > .05 and result['maximumContactForceNewtons'] > 0
    report.update(passedTests=39, verifiedGitArchiveFiles=git_files, verifiedBuiltCSharpFiles=compiled_sources,
        verifiedPlayerFiles=len(player_files), exactSavedAuditReproduction=True, result=result, savedAudit=audit,
        testsExecutedDuringReview=False, physicalModelExecutedDuringReview=False)

    contacts = ROOT / 'artifacts/physical-observation-20261113-8c18b7'
    revision = 'f263abf83675c53f26a804648794adf8bcffc767'
    failed_report = common.verify(contacts, ['test-binaries.json'], revision, expected_status='FAILED', expected_exit=1)
    assert Path(failed_report['backup']) == Path('D:/DNNE-desktop-runs/UnityWorld') / contacts.name
    counts = dict(PhysicalObservationTests=26, **BASE_COUNTS)
    git_files, tests = source_and_tests(contacts, revision, counts)
    failures = [t for t in tests if t.attrib['outcome'] == 'Failed']
    passed = [t.attrib['testName'] for t in tests if t.attrib['outcome'] == 'Passed']
    assert len(failures) == 24 and len(passed) == 41
    messages = []
    for case in failures:
        assert '.PhysicalObservationTests.' in case.attrib['testName']
        message = case.find('.//t:Message', {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}).text
        assert "JsonPropertyInfo 'isValid'" in message and 'NeuralSubstrateSupply' in message and 'does not specify a setter' in message
        messages.append(dict(test=case.attrib['testName'], message=message))
    assert sum('.PhysicalObservationTests.' not in name for name in passed) == 39
    failed_report.update(verifiedGitArchiveFiles=git_files, passedTests=41, failedTests=24,
        bodyRegressionAndManifestPassed=39, combinedObservationChecksPassed=2, failedTestMessages=messages,
        failureMechanism='Required JSON metadata applied to computed getter-only NeuralSubstrateSupply.IsValid',
        combinedObservationQualified=False, testsExecutedDuringReview=False, modelExecutedDuringReview=False)
    output.mkdir()
    (output / 'unity-review.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    (output / 'contact-failure-review.json').write_text(json.dumps(failed_report, indent=2) + '\n', encoding='utf-8')
    (output / 'review.json').write_text(json.dumps(dict(operatorReportedBoth=True, reviewedUtc=datetime.now(timezone.utc).isoformat(),
        unityCapture=unity.name, contactsCapture=contacts.name, modelsExecuted=False, testsExecuted=False), indent=2) + '\n', encoding='utf-8')
    for label, value in [('Unity', report), ('Contacts', failed_report)]:
        print(label, json.dumps({k: value[k] for k in ('verifiedFilesPerCopy', 'verifiedBytesPerCopy', 'manifestSha256', 'passedTests')}))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('output', type=Path)
    parser.add_argument('--operator-reported-both', action='store_true', required=True)
    args = parser.parse_args()
    main(args.output.resolve())
