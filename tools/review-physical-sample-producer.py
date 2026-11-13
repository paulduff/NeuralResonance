"""Review the operator-reported producer capture; never execute tests or a model."""
import argparse
from collections import Counter
from datetime import datetime, timezone
from hashlib import sha256
import importlib.util
import json
from pathlib import Path
import re
import subprocess
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
CAPTURE = ROOT / 'artifacts/physical-producer-20261113-efe4af'
REVISION = 'af9faf305e9c18215b5d3c3670e63b240467eb93'
COUNTS = dict(PhysicalSampleProducerTests=16, PhysicalSampleTransportTests=22, SolutionManifestTests=1)


def main(output):
    if output.exists():
        raise FileExistsError('Preserve the earlier review: ' + str(output))
    spec = importlib.util.spec_from_file_location('completed_capture_hashes', ROOT / 'tools/review-olive-and-motor-20261109.py')
    common = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(common)
    manifest = common.read(CAPTURE / 'backup-manifest.json')
    assert Path(manifest['Source']) == CAPTURE
    assert Path(manifest['Destination']) == Path('D:/DNNE-desktop-runs/UnityWorld') / CAPTURE.name
    report = common.verify(CAPTURE, ['test-binaries.json'], REVISION)
    config = common.read(CAPTURE / 'configuration.json')
    status = common.read(CAPTURE / 'window-status.json')
    assert config['NoLiveMonitoring'] and config['NoTimingMeasured']
    assert not config['LiveBodySelected'] and not config['ModelExecuted']
    assert config['ExpectedClassCounts'] == COUNTS
    assert status['ProducerAndTransportChecksPassed']
    assert not any(status[k] for k in ('LiveBrainAccessed', 'OldSavesLoaded', 'ModelExecuted',
        'LiveUnityIntegrated', 'TimingMeasured', 'PhysiologyQualified', 'AvatarRecoveryAssessed', 'FullUnitSuitePassed'))

    tested_files = ['src/NRE.SimAvatar/AvatarPhysicalSampleProducer.cs',
        'src/NRE.SimAvatar/AvatarPhysicalSampleTransport.cs', 'Shared.Contracts/PhysicalSampleProtocol.cs',
        'UnityWorld/Qualification/corrected-body-fallen-example.json',
        'tools/start-physical-sample-producer-window.ps1']
    expected_names = set()
    for name, count in COUNTS.items():
        file = 'tests/NeuralResonanceEngine.DNNE.Tests/' + name + '.cs'
        tested_files.append(file)
        declarations = re.findall(r'\[Fact\]\s+public\s+(?:async\s+)?(?:void|Task)\s+(\w+)\s*\(',
                                  common.io(CAPTURE / 'source' / file).read_text(encoding='utf-8-sig'))
        assert len(declarations) == len(set(declarations)) == count
        expected_names.update('NeuralResonanceEngine.DNNE.Tests.' + name + '.' + method for method in declarations)
    line_ending_conversions = []
    for file in tested_files:
        committed = subprocess.check_output(['git', 'show', REVISION + ':' + file], cwd=ROOT)
        frozen = common.io(CAPTURE / 'source' / file).read_bytes()
        if committed != frozen:
            # git archive applies this repository's Windows checkout line ends.
            # Admit precisely LF -> CRLF, not arbitrary text/whitespace changes.
            # The frozen archive, source and both backup byte hashes stay strict.
            assert b'\r' not in committed and frozen == committed.replace(b'\n', b'\r\n'), 'Frozen source differs from Git: ' + file
            line_ending_conversions.append(dict(file=file, gitSha256=sha256(committed).hexdigest().upper(),
                frozenSha256=sha256(frozen).hexdigest().upper(), conversion='LF to CRLF only', lines=committed.count(b'\n')))

    cases = ET.parse(CAPTURE / 'test-results/focused.trx').findall('.//t:UnitTestResult',
        {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'})
    names = [c.attrib['testName'] for c in cases]
    counts = dict(Counter(n.rsplit('.', 1)[0].rsplit('.', 1)[-1] for n in names))
    assert counts == COUNTS and all(c.attrib['outcome'] == 'Passed' for c in cases)
    assert len(names) == len(set(names)) == config['ExpectedTestCases'] == status['PassedFocusedTests'] == 39
    assert set(names) == expected_names
    report.update(operatorReportedCompletion=True, reviewedUtc=datetime.now(timezone.utc).isoformat(),
        testedSourceAndFixtureMatchGitWithRecordedLineEndings=True, verifiedGitFiles=tested_files,
        gitArchiveLineEndingConversions=line_ending_conversions, frozenByteHashesUnchanged=True,
        passedFocusedTests=39, testCounts=counts, passedTestNames=sorted(names),
        producerAndTransportChecksPassed=True, modelExecutedDuringReview=False, testsExecutedDuringReview=False,
        liveUnityIntegrated=False, physiologyQualified=False, avatarRecoveryAssessed=False, fullUnitSuitePassed=False)
    output.mkdir()
    (output / 'review.json').write_text(json.dumps(report, indent=2, allow_nan=False) + '\n', encoding='utf-8')
    print(json.dumps({k: v for k, v in report.items() if k not in
                     ('passedTestNames', 'verifiedGitFiles', 'gitArchiveLineEndingConversions')}, indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('output', type=Path)
    parser.add_argument('--operator-reported', action='store_true', required=True)
    args = parser.parse_args()
    main(args.output.resolve())
