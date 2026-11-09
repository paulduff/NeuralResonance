"""Review a reported-complete endpoint comparison without integrating any model."""
import argparse
from datetime import datetime, timezone
import importlib.util
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
read = lambda p: json.loads(p.read_text(encoding='utf-8-sig'))


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


def main(capture, output):
    assert not output.exists()
    hashing = module('motor_boundary_hashes', ROOT / 'tools/review-olive-and-motor-20261109.py')
    status = read(capture / 'window-status.json')
    assert status['Status'] in ('DIAGNOSTICS_COMPLETE', 'FAILED')
    expected_exit = 0 if status['Status'] == 'DIAGNOSTICS_COMPLETE' else 1
    report = hashing.verify(capture, ['test-binaries.json', 'probe-binaries.json'],
                            '2465fdb8026768f2348e7bda53a4375ff203eb28', status['Status'], expected_exit)
    assert not any(status[k] for k in ('LiveBrainAccessed', 'OldSavesLoaded', 'PhysiologyQualified', 'AvatarRecoveryAssessed'))
    # A partial study without all traces cannot be promoted by this reviewer.
    # Its failure needs a separate partial-evidence review, preserving the capture.
    analysis = module('motor_boundary_analysis', ROOT / 'tools/analyse-motor-boundary.py').audit(capture)
    assert analysis == read(capture / 'boundary-analysis.json')
    assert status['NumericalCriteriaPassed'] == analysis['numericalCriteriaPassed']
    assert status['PassedFocusedTests'] == 12
    import xml.etree.ElementTree as ET
    tests = ET.parse(capture / 'test-results/focused.trx').findall('.//t:UnitTestResult',
        {'t': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'})
    assert len(tests) == 12 and all(t.attrib['outcome'] == 'Passed' for t in tests)
    report.update(operatorReportedCompletion=True, reviewedUtc=datetime.now(timezone.utc).isoformat(),
                  savedAnalysisReproduced=True, modelExecuted=False, passedFocusedTests=12, analysis=analysis)
    output.mkdir()
    (output / 'review.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({k: v for k, v in report.items() if k != 'analysis'}, indent=2))
    print('All original motor numerical gates passed:', analysis['numericalCriteriaPassed'])


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('capture', type=Path); parser.add_argument('output', type=Path)
    parser.add_argument('--operator-reported', action='store_true', required=True)
    args = parser.parse_args(); main(args.capture.resolve(), args.output.resolve())
