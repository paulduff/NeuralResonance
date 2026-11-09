"""Verify reported-complete captures and reproduce saved analysis; never run a model."""
import argparse
from collections import Counter
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
SPECS = {
    'motor': dict(
        capture='motor-unit-circuit-20261109-83e7ee',
        revision='e12fff09af30f078e229ed2f56c71b4af93886ec',
        binaries=['test-binaries.json', 'probe-binaries.json'],
        analyzer='analyse-motor-unit-circuit.py', result='circuit-analysis.json',
        dependencies=['motor_boundary_protocol.py'],
        tests=dict(MotorAxonTransportTests=32, MotorAxonReplayBufferTests=16,
                   MotorUnitCalciumTests=13, IsometricMotorUnitPlantTests=15,
                   MotoneuronReferenceTests=11, SolutionManifestTests=1)),
    'host': dict(
        capture='olive-cuda-host-20261109-aeb5f2',
        revision='91c53eab1ae51f9270a732e051eb0cc30782d352',
        binaries=['test-binaries.json', 'reference-binaries.json', 'candidate-binaries.json'],
        analyzer='analyse-olive-cuda-host.py', result='host-analysis.json',
        dependencies=[], tests=dict(OlivaryBatchTests=10, SolutionManifestTests=1)),
}


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


def main(kind, output):
    if output.exists():
        raise FileExistsError('Preserve the earlier review: ' + str(output))
    spec = SPECS[kind]
    capture = ROOT / 'artifacts' / spec['capture']
    common = module('completed_capture_hashes', ROOT / 'tools/review-olive-and-motor-20261109.py')
    report = common.verify(capture, spec['binaries'], spec['revision'])
    print('Complete C:/D: capture, frozen inputs and executed binaries verified.', flush=True)
    config = common.read(capture / 'configuration.json')
    status = common.read(capture / 'window-status.json')
    assert config['NoLiveMonitoring']
    assert not any(status[k] for k in ('LiveBrainAccessed', 'OldSavesLoaded',
                                     'PhysiologyQualified', 'AvatarRecoveryAssessed', 'FullUnitSuitePassed'))
    tests = ET.parse(capture / 'test-results/focused.trx').findall(
        './/t:UnitTestResult', {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'})
    counts = dict(Counter(t.attrib['testName'].rsplit('.', 1)[0].rsplit('.', 1)[-1] for t in tests))
    assert counts == spec['tests'] and all(t.attrib['outcome'] == 'Passed' for t in tests)
    assert len(tests) == config['ExpectedTestCases'] == status['PassedFocusedTests']
    assert set(config['TestClasses']) == set(counts)
    # Read the reviewed frozen analyzer, accounting only for Git LF/CRLF text
    # conversion. Exact capture byte hashes have already been checked above.
    frozen_tools = capture / 'source/tools'
    for name in [spec['analyzer'], *spec['dependencies']]:
        assert (ROOT / 'tools' / name).read_text(encoding='utf-8') == (frozen_tools / name).read_text(encoding='utf-8')
    sys.path.insert(0, str(frozen_tools))
    try:
        analyzer = module('saved_' + kind + '_analysis', frozen_tools / spec['analyzer'])
        if kind == 'motor':
            # The only solve branch is explicitly disabled. Reference arrays
            # already exist in the immutable, hashed completed capture.
            analysis = analyzer.audit(capture, solve_references=False)
        else:
            analysis = analyzer.audit(capture)
    finally:
        sys.path.pop(0)
    assert analysis == common.read(capture / spec['result']), 'Saved analysis did not reproduce exactly.'
    assert analysis['completed']
    if kind == 'motor':
        assert status['SourceAndTransportPassed'] and status['NumericalCriteriaPassed']
        assert all(analysis[k] for k in ('allSourceBytesReproduced', 'originalEventsPreserved',
            'packetAndRetryInvariancePassed', 'disconnectionControlPassed', 'refinementPassed', 'numericalCriteriaPassed'))
        assert len(analysis['cases']) == config['ExpectedConditions'] == 8
        assert not any(analysis[k] for k in ('physiologicalCalibrationComplete', 'activeAxonQualified',
            'neuromuscularJunctionQualified', 'liveMotorAuthorityQualified', 'avatarRecoveryAssessed'))
    else:
        assert status['AccuracyPassed'] and not status['SpeedMeasured']
        assert analysis['accuracyPassed'] and analysis['exactControlsAcrossModes'] and analysis['contextCleanupReported']
        assert len(analysis['cases']) == config['ExpectedCases'] == 6
        assert analysis['legacyControlGroups'] == analysis['hostControlGroups'] == 12
        assert not any(analysis[k] for k in ('speedMeasured', 'physiologyQualified', 'avatarRecoveryAssessed'))
    report.update(operatorReportedCompletion=True, reviewedUtc=datetime.now(timezone.utc).isoformat(),
                  savedAnalysisExactlyReproduced=True, passedFocusedTests=len(tests), testCounts=counts,
                  modelExecutedDuringReview=False, referenceSolvedDuringReview=False,
                  cudaExecutedDuringReview=False, benchmarkExecutedDuringReview=False,
                  physiologyQualified=False, avatarRecoveryAssessed=False, analysis=analysis)
    output.mkdir()
    (output / 'review.json').write_text(json.dumps(report, indent=2, allow_nan=False) + '\n', encoding='utf-8')
    print(json.dumps({k: v for k, v in report.items() if k != 'analysis'}, indent=2), flush=True)
    print('All saved checks reproduced. No model, CUDA execution or benchmark ran during review.', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('kind', choices=SPECS)
    parser.add_argument('output', type=Path)
    parser.add_argument('--operator-reported', action='store_true', required=True)
    args = parser.parse_args()
    main(args.kind, args.output.resolve())
