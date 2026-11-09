"""Audit the operator-reported CUDA cost capture; no model, CUDA or timing run."""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


def main():
    common = module('cost_capture_io', ROOT/'tools/review-olive-and-motor-20261109.py')
    capture = ROOT/'artifacts/olive-cuda-cost-20261109-fedff9'
    output = ROOT/'artifacts/olive-cost-review-20261109'
    if output.exists():
        raise FileExistsError('Preserve the existing review: '+str(output))
    report = common.verify(capture, ['test-binaries.json', 'control-binaries.json',
                                   'host-binaries.json', 'device-binaries.json'],
                           '0d94dd2cd7d0b402451e03c508ef253cacd93c26')
    print('Completed C:/D: capture, frozen source and executed binaries verified.', flush=True)
    config, status = (common.read(capture/name) for name in ('configuration.json', 'window-status.json'))
    assert config['NoLiveMonitoring'] and status['PassedFocusedTests'] == config['ExpectedTestCases'] == 11
    assert status['CompletedTrials'] == 180 and status['ProcessScheduleChecked']
    assert status['CostMeasured'] and status['AccuracyPassed']
    ns = {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    tests = ET.parse(capture/'test-results/focused.trx').findall('.//t:UnitTestResult', ns)
    assert len(tests) == 11 and all(t.attrib['outcome'] == 'Passed' for t in tests)
    counts = {name: sum(t.attrib['testName'].startswith('NeuralResonanceEngine.DNNE.Tests.'+name+'.') for t in tests)
              for name in ('OlivaryBatchTests', 'SolutionManifestTests')}
    assert list(counts.values()) == [10, 1]
    # The reviewed analyzer text equals the one used by the finished window.
    # Universal newline reads account for checkout CRLF versus archive LF;
    # verify() above still requires exact frozen source/backup byte hashes.
    # It reads saved bytes only; the original package remains immutable.
    for name in ('analyse-olive-cuda-cost.py', 'olive_cost_protocol.py'):
        assert (ROOT/'tools'/name).read_text(encoding='utf-8') == (capture/'source/tools'/name).read_text(encoding='utf-8')
    analysis = module('cost_saved_byte_audit', capture/'source/tools/analyse-olive-cuda-cost.py').audit(capture)
    assert analysis == common.read(capture/'cost-analysis.json'), 'Saved cost analysis did not reproduce exactly.'
    assert analysis['completed'] and analysis['accuracyPassed'] and analysis['phaseAccountingPassed']
    assert analysis['processes'] == analysis['measuredTrials'] == analysis['warmups'] == 90
    assert analysis['trials'] == 180 and analysis['controlGroups'] == 18 and len(analysis['cases']) == 6
    summary = []
    for case in analysis['cases']:
        modes = case['modes']
        host = modes['host']
        summary.append(dict(case=case['key'], controlMedianMs=modes['control']['medianMs'],
                            hostMedianMs=host['medianMs'], deviceMedianMs=modes['device']['medianMs'],
                            hostRatioToControl=host['ratioToControlMedian'],
                            deviceRatioToControl=modes['device']['ratioToControlMedian'],
                            hostPhaseMedianMs=host['medianPhaseMs'],
                            hostMedianOutsidePhasesMs=host['medianOutsidePhasesMs'],
                            deviceEventMedianMs=modes['device']['medianDeviceEventMs'],
                            materiallyPerturbedModes=[m for m in ('host', 'device')
                                                      if modes[m]['materialInstrumentationDifference']]))
    report.update(reviewedUtc=datetime.now(timezone.utc).isoformat(), operatorReportedCompletion=True,
                  savedAnalysisExactlyReproduced=True, passedFocusedTests=11, testCounts=counts,
                  scheduleShape=common.read(capture/'schedule-check.json'),
                  processes=90, trials=180, measuredTrials=90, warmups=90, controlGroups=18,
                  accuracyPassed=True, exactAcrossModesRoundsAndWarmups=True,
                  phaseAccountingPassed=True, summaries=summary,
                  modelExecutedDuringReview=False, benchmarkExecutedDuringReview=False,
                  wholeBrainThroughputMeasured=False, physiologyQualified=False, avatarRecoveryAssessed=False)
    output.mkdir()
    (output/'review.json').write_text(json.dumps(report, indent=2)+'\n', encoding='utf-8')
    print(json.dumps(dict(verifiedFilesPerCopy=report['verifiedFilesPerCopy'],
                          verifiedBytesPerCopy=report['verifiedBytesPerCopy'],
                          manifestSha256=report['manifestSha256'], summaries=summary), indent=2), flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--operator-reported', action='store_true', required=True)
    parser.parse_args()
    main()
