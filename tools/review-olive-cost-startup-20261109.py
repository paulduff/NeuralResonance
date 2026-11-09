"""Review the operator-reported cost runner failure without running CUDA or tests."""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import xml.etree.ElementTree as ET
from olive_cost_protocol import schedule, protocol, MODES

ROOT = Path(__file__).resolve().parents[1]


def main():
    spec = importlib.util.spec_from_file_location('capture_io', ROOT/'tools/review-olive-and-motor-20261109.py')
    common = importlib.util.module_from_spec(spec); spec.loader.exec_module(common)
    capture = ROOT/'artifacts/olive-cuda-cost-20261109-06f78b'
    report = common.verify(capture, ['test-binaries.json']+[m+'-binaries.json' for m in MODES],
                           'f8561578320143a30f0fd0a95c9c914d72e711b0', expected_status='FAILED', expected_exit=1)
    config, status = (common.read(capture/name) for name in ('configuration.json', 'window-status.json'))
    assert 'Incomplete process schedule.' in status['Error'] and not status['CostMeasured'] and not status['AccuracyPassed']
    assert config['NoLiveMonitoring'] and status['PassedFocusedTests'] == config['ExpectedTestCases'] == 11
    ns = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    cases = ET.parse(capture/'test-results/focused.trx').findall('.//t:UnitTestResult', ns)
    assert len(cases) == 11 and all(c.attrib['outcome'] == 'Passed' for c in cases)
    counts = {name: sum(c.attrib['testName'].startswith('NeuralResonanceEngine.DNNE.Tests.'+name+'.') for c in cases)
              for name in ('OlivaryBatchTests', 'SolutionManifestTests')}
    assert list(counts.values()) == [10, 1]
    assert common.read(capture/'schedule.json') == schedule() and len(schedule()) == 90
    assert common.read(capture/'cost-protocol.json') == protocol()
    root = capture/'study'; controls = []
    assert {p.name for p in root.iterdir()} == {'controls'}
    assert not (capture/'completed-processes.json').exists() and not (capture/'cost-analysis.json').exists()
    for mode in MODES:
        folder = root/'controls'/mode
        assert common.read(folder/'result.json') == dict(completed=True, mode=mode, controls=True, contextDisposed=True)
        rows = common.read(folder/'controls.json')
        assert [r['name'] for r in rows] == ['seed-transfer-and-ownership','invalid-requests-atomic','thread-ownership',
                                            'untouched-cell','identity-permutation','numerical-failure-atomic']
        assert all(r['passed'] for r in rows) and rows[1]['rejected'] == 6 and rows[4]['frames'] == 64
        assert all(r['before'] == r['after'] for r in rows if 'before' in r)
        assert len(rows[4]['matchedStateHashes']) == 64 and len(rows[5]['explicitNextRequestHash']) == 64
        assert common.read(folder/'memory-after.json')['ownedBufferBytes'] == 0
        controls.append(dict(mode=mode, controlsPassed=len(rows), contextDisposed=True, ownedBufferBytesAfter=0))
    report.update(reviewedUtc=datetime.now(timezone.utc).isoformat(), operatorReportedCompletion=True,
                  passedFocusedTests=11, testCounts=counts, scheduleMatchesAll90DeclaredEntries=True,
                  completedControlVariants=controls, measuredTrials=0, costMeasured=False,
                  failureStage='PowerShell schedule shape before timed process loop', originalFailurePreserved=True,
                  modelExecutedDuringReview=False, physiologyQualified=False, avatarRecoveryAssessed=False)
    output = ROOT/'artifacts/olive-cost-startup-review-20261109'; output.mkdir()
    (output/'review.json').write_text(json.dumps(report, indent=2)+'\n', encoding='utf-8')
    print(json.dumps(report, indent=2), flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--operator-reported', action='store_true', required=True)
    parser.parse_args(); main()
